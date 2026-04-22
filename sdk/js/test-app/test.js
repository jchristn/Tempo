"use strict";

const sdk = require("../src");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");

function assertEqual(expected, actual, name) {
  try {
    assert.deepStrictEqual(actual, expected);
  } catch (err) {
    throw new Error(name + ": " + err.message);
  }
}

function assertTrue(value, name) {
  if (!value) throw new Error(name + ": expected true");
}

function assertThrows(fn, name) {
  try {
    fn();
  } catch {
    return;
  }
  throw new Error(name + ": expected exception");
}

function assertKSortableId(value, prefix, name) {
  assertTrue(value.startsWith(prefix), name + " prefix");
  assertEqual(32, value.length, name + " length");
  const parts = value.slice(prefix.length).split("_");
  assertEqual(2, parts.length, name + " segments");
  assertTrue(parts[0].length > 0, name + " timestamp segment");
  assertTrue(parts[1].length > 0, name + " random segment");
}

function request() {
  return new sdk.StepRequest({
    protocolVersion: "1.0",
    tenantId: "ten_1",
    dataFlowId: "flow_1",
    flowRunId: "run_1",
    stepRunId: "sru_1",
    requestId: "req_1",
    data: { value: 5 },
    metadata: { trace: "abc" },
    previousResult: sdk.StepResultType.Success
  });
}

function testPublicApiCoverage() {
  assertEqual([
    "CURRENT",
    "PROTOCOL_VERSION_ENV",
    "SUPPORTED",
    "SUPPORTED_PROTOCOL_VERSIONS_ENV",
    "StepRequest",
    "StepResult",
    "StepResultType",
    "TempoStepHost",
    "TempoStepLogger",
    "V1",
    "correlateResult",
    "createLoggerFromEnvironment",
    "error",
    "exceptionResult",
    "getCurrentExecutionContext",
    "isSupportedProtocolVersion",
    "normalizeProtocolVersion",
    "step",
    "success",
    "supportedProtocolEnvironment"
  ], Object.keys(sdk).sort(), "exports");

  assertEqual(["constructor", "toJson", "toObject"], Object.getOwnPropertyNames(sdk.StepRequest.prototype).sort(), "StepRequest prototype");
  assertEqual(["fromJson", "fromObject", "length", "name", "prototype"], Object.getOwnPropertyNames(sdk.StepRequest).sort(), "StepRequest static");
  assertEqual(["constructor", "toJson", "toObject"], Object.getOwnPropertyNames(sdk.StepResult.prototype).sort(), "StepResult prototype");
  assertEqual(["fromJson", "fromObject", "length", "name", "prototype"], Object.getOwnPropertyNames(sdk.StepResult).sort(), "StepResult static");
  assertEqual(["deserializeRequest", "length", "name", "prototype", "run", "serializeResult"], Object.getOwnPropertyNames(sdk.TempoStepHost).sort(), "TempoStepHost static");
  assertEqual(["Error", "Exception", "MaxIterationsExceeded", "Success", "Timeout"], Object.keys(sdk.StepResultType).sort(), "result enum");
}

function testVersions() {
  assertEqual("1.0", sdk.V1, "v1");
  assertEqual("1.0", sdk.CURRENT, "current");
  assertEqual(["1.0"], sdk.SUPPORTED, "supported");
  assertEqual("TEMPO_PROTOCOL_VERSION", sdk.PROTOCOL_VERSION_ENV, "protocol env");
  assertEqual("TEMPO_SUPPORTED_PROTOCOL_VERSIONS", sdk.SUPPORTED_PROTOCOL_VERSIONS_ENV, "supported env");
  assertTrue(sdk.isSupportedProtocolVersion(null), "null supported");
  assertTrue(sdk.isSupportedProtocolVersion(" 1.0 "), "trim supported");
  assertEqual("1.0", sdk.normalizeProtocolVersion("1.0"), "normalize");
  assertThrows(() => sdk.normalizeProtocolVersion("9.9"), "bad protocol");
  assertEqual("1.0", sdk.supportedProtocolEnvironment({}), "default supported env");
  assertEqual("1.0,1.1", sdk.supportedProtocolEnvironment({ TEMPO_SUPPORTED_PROTOCOL_VERSIONS: "1.0,1.1" }), "custom supported env");
}

function testRequestModel() {
  const defaults = new sdk.StepRequest();
  assertEqual("1.0", defaults.protocolVersion, "default protocol");
  assertKSortableId(defaults.dataFlowId, "flow_", "default flow id");
  assertKSortableId(defaults.requestId, "req_", "default request id");
  assertThrows(() => new sdk.StepRequest({ protocolVersion: "9.9" }), "unsupported request");
  assertThrows(() => new sdk.StepRequest({ dataFlowId: "" }), "empty flow");
  assertThrows(() => new sdk.StepRequest({ requestId: "" }), "empty request");
  assertThrows(() => sdk.StepRequest.fromObject([]), "request object required");

  const raw = {
    protocolVersion: "1.0",
    tenantId: "ten_1",
    dataFlowId: "flow_1",
    flowRunId: "run_1",
    stepRunId: "sru_1",
    requestId: "req_1",
    data: { x: 2 },
    metadata: { m: 3 },
    previousResult: "Error"
  };
  const req = sdk.StepRequest.fromObject(raw);
  assertEqual(raw, req.toObject(), "request dict");
  assertEqual(raw, sdk.StepRequest.fromJson(JSON.stringify(raw)).toObject(), "request json");
  assertEqual(raw, sdk.TempoStepHost.deserializeRequest(JSON.stringify(raw)).toObject(), "deserialize helper");
}

function testResultModelAndHelpers() {
  const req = request();
  const raw = { protocolVersion: "1.0", dataFlowId: "flow_x", requestId: "req_x", result: "Timeout", data: { a: 1 }, exception: null, metadata: null, tenantId: null, flowRunId: null, stepRunId: null };
  const result = sdk.StepResult.fromObject(raw);
  assertEqual(raw, result.toObject(), "result dict");
  assertEqual(raw, sdk.StepResult.fromJson(JSON.stringify(raw)).toObject(), "result json");
  assertThrows(() => sdk.StepResult.fromObject([]), "result object required");

  const success = sdk.success(req, { ok: true });
  assertEqual(sdk.StepResultType.Success, success.result, "success");
  assertEqual(req.requestId, success.requestId, "success correlation");
  assertEqual(req.metadata, success.metadata, "metadata fallback");

  const error = sdk.error(req, { valid: false }, { reason: "bad" });
  assertEqual(sdk.StepResultType.Error, error.result, "error");
  assertEqual({ reason: "bad" }, error.metadata, "error metadata");

  const exception = sdk.exceptionResult(req, new Error("boom"));
  assertEqual(sdk.StepResultType.Exception, exception.result, "exception");
  assertEqual("boom", exception.exception, "exception message");
  assertEqual(req.dataFlowId, exception.dataFlowId, "exception correlation");

  const noRequest = sdk.exceptionResult(null, "no request");
  assertEqual("unknown", noRequest.dataFlowId, "no request flow");
  assertKSortableId(noRequest.requestId, "req_", "no request id");

  const other = new sdk.StepResult({ dataFlowId: "wrong", requestId: "wrong" });
  sdk.correlateResult(other, req);
  assertEqual(req.dataFlowId, other.dataFlowId, "correlate flow");
  assertEqual(req.requestId, other.requestId, "correlate request");
  assertThrows(() => sdk.correlateResult({}, req), "bad result");
  assertThrows(() => sdk.correlateResult(other, {}), "bad request");

  const serialized = sdk.TempoStepHost.serializeResult(success);
  assertEqual(sdk.StepResultType.Success, sdk.StepResult.fromJson(serialized).result, "serialize helper");
}

async function testDecoratorAndRunner() {
  const req = request();
  const handler = sdk.step((r) => ({ handled: r.data.value }));
  assertTrue(handler.__tempoStep, "decorator marker");

  const output = { text: "", write(chunk) { this.text += chunk; } };
  const code = await sdk.TempoStepHost.run(handler, { input: req.toJson(), output });
  assertEqual(0, code, "run code");
  const result = sdk.StepResult.fromJson(output.text);
  assertEqual(sdk.StepResultType.Success, result.result, "run success");
  assertEqual(req.requestId, result.requestId, "run correlation");
  assertEqual({ handled: 5 }, result.data, "run data");

  const asyncOutput = { text: "", write(chunk) { this.text += chunk; } };
  await sdk.TempoStepHost.run(async (r) => sdk.error(r, { async: true }), { input: req.toJson(), output: asyncOutput });
  const asyncResult = sdk.StepResult.fromJson(asyncOutput.text);
  assertEqual(sdk.StepResultType.Error, asyncResult.result, "async result");
  assertEqual({ async: true }, asyncResult.data, "async data");

  const throwOutput = { text: "", write(chunk) { this.text += chunk; } };
  await sdk.TempoStepHost.run(() => { throw new Error("handler boom"); }, { input: req.toJson(), output: throwOutput });
  const thrown = sdk.StepResult.fromJson(throwOutput.text);
  assertEqual(sdk.StepResultType.Exception, thrown.result, "throw result");
  assertEqual("handler boom", thrown.exception, "throw message");
  assertEqual(req.requestId, thrown.requestId, "throw correlation");

  const invalidOutput = { text: "", write(chunk) { this.text += chunk; } };
  await sdk.TempoStepHost.run(handler, { input: "not-json", output: invalidOutput });
  const invalid = sdk.StepResult.fromJson(invalidOutput.text);
  assertEqual(sdk.StepResultType.Exception, invalid.result, "invalid result");
  assertEqual("unknown", invalid.dataFlowId, "invalid flow");
}

async function testLoggingContextAndFile() {
  const logPath = path.join(os.tmpdir(), "tempo-js-sdk-" + Date.now() + "-" + Math.random().toString(16).slice(2) + ".log");
  const env = {
    ...process.env,
    TEMPO_RUN_LOG_FILE: logPath,
    TEMPO_RUN_ASSIGNMENT_ID: "ras_1",
    TEMPO_STEP_ID: "step_1",
    TEMPO_WORKER_ID: "wrk_1"
  };

  try {
    const req = request();
    const output = { text: "", write(chunk) { this.text += chunk; } };
    const code = await sdk.TempoStepHost.run(() => {
      const context = sdk.getCurrentExecutionContext();
      assertTrue(!!context, "execution context available");
      context.logger.info("logger-info");
      console.log("console-info");
      console.error("console-error");
      return {
        hasContext: !!context,
        runAssignmentId: context.runAssignmentId,
        stepId: context.stepId,
        workerId: context.workerId
      };
    }, { input: req.toJson(), output, env });

    assertEqual(0, code, "logging run code");
    const result = sdk.StepResult.fromJson(output.text);
    assertEqual(sdk.StepResultType.Success, result.result, "logging run success");
    assertEqual(true, result.data.hasContext, "context flag");
    assertEqual("ras_1", result.data.runAssignmentId, "assignment id");
    assertEqual("step_1", result.data.stepId, "step id");
    assertEqual("wrk_1", result.data.workerId, "worker id");
    assertTrue(fs.existsSync(logPath), "log file created");

    const logText = fs.readFileSync(logPath, "utf8");
    assertTrue(logText.includes("logger-info"), "logger output captured");
    assertTrue(logText.includes("console-info"), "stdout redirected to file");
    assertTrue(logText.includes("console-error"), "stderr redirected to file");
    assertTrue(!output.text.includes("console-info"), "protocol stdout remains clean");
  } finally {
    if (fs.existsSync(logPath)) fs.unlinkSync(logPath);
  }
}

(async function main() {
  testPublicApiCoverage();
  testVersions();
  testRequestModel();
  testResultModelAndHelpers();
  await testDecoratorAndRunner();
  await testLoggingContextAndFile();
  console.log("Tempo JavaScript SDK test app PASS");
})().catch((err) => {
  console.error(err && err.stack ? err.stack : err);
  process.exit(1);
});
