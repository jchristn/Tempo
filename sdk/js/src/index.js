"use strict";

const crypto = require("node:crypto");

const V1 = "1.0";
const CURRENT = V1;
const SUPPORTED = Object.freeze([V1]);
const PROTOCOL_VERSION_ENV = "TEMPO_PROTOCOL_VERSION";
const SUPPORTED_PROTOCOL_VERSIONS_ENV = "TEMPO_SUPPORTED_PROTOCOL_VERSIONS";
const RUN_LOG_FILE_ENV = "TEMPO_RUN_LOG_FILE";
const ID_LENGTH = 32;
const ID_CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
let CURRENT_EXECUTION_CONTEXT = null;

const StepResultType = Object.freeze({
  Success: "Success",
  Timeout: "Timeout",
  Error: "Error",
  Exception: "Exception",
  MaxIterationsExceeded: "MaxIterationsExceeded"
});

function newId(prefix) {
  const timestamp = Date.now().toString(36);
  const randomLength = Math.max(1, ID_LENGTH - prefix.length - timestamp.length - 1);
  let random = "";
  for (let i = 0; i < randomLength; i += 1) random += ID_CHARS[crypto.randomInt(ID_CHARS.length)];
  return prefix + timestamp + "_" + random;
}

function isSupportedProtocolVersion(version) {
  const normalized = version === undefined || version === null || String(version).trim() === "" ? CURRENT : String(version).trim();
  return SUPPORTED.some((item) => item.toLowerCase() === normalized.toLowerCase());
}

function normalizeProtocolVersion(version) {
  const normalized = version === undefined || version === null || String(version).trim() === "" ? CURRENT : String(version).trim();
  const match = SUPPORTED.find((item) => item.toLowerCase() === normalized.toLowerCase());
  if (!match) throw new Error("Unsupported Tempo step protocol version '" + normalized + "'.");
  return match;
}

function resultValue(value) {
  if (value === undefined || value === null) return null;
  return String(value);
}

class StepRequest {
  constructor(value = {}) {
    this.protocolVersion = normalizeProtocolVersion(value.protocolVersion ?? value.protocol_version ?? CURRENT);
    this.tenantId = value.tenantId ?? value.tenant_id ?? null;
    this.dataFlowId = value.dataFlowId ?? value.data_flow_id ?? newId("flow_");
    this.flowRunId = value.flowRunId ?? value.flow_run_id ?? null;
    this.stepRunId = value.stepRunId ?? value.step_run_id ?? null;
    this.requestId = value.requestId ?? value.request_id ?? newId("req_");
    this.data = value.data ?? null;
    this.metadata = value.metadata ?? null;
    this.previousResult = resultValue(value.previousResult ?? value.previous_result);
    if (!this.dataFlowId) throw new Error("dataFlowId is required.");
    if (!this.requestId) throw new Error("requestId is required.");
  }

  static fromObject(value) {
    if (!value || Array.isArray(value) || typeof value !== "object") throw new Error("StepRequest object is required.");
    return new StepRequest(value);
  }

  static fromJson(value) {
    const parsed = JSON.parse(value);
    return StepRequest.fromObject(parsed);
  }

  toObject() {
    return {
      protocolVersion: this.protocolVersion,
      tenantId: this.tenantId,
      dataFlowId: this.dataFlowId,
      flowRunId: this.flowRunId,
      stepRunId: this.stepRunId,
      requestId: this.requestId,
      data: this.data,
      metadata: this.metadata,
      previousResult: this.previousResult
    };
  }

  toJson() {
    return JSON.stringify(this.toObject());
  }
}

class StepResult {
  constructor(value = {}) {
    this.protocolVersion = normalizeProtocolVersion(value.protocolVersion ?? value.protocol_version ?? CURRENT);
    this.tenantId = value.tenantId ?? value.tenant_id ?? null;
    this.dataFlowId = value.dataFlowId ?? value.data_flow_id ?? newId("flow_");
    this.flowRunId = value.flowRunId ?? value.flow_run_id ?? null;
    this.stepRunId = value.stepRunId ?? value.step_run_id ?? null;
    this.requestId = value.requestId ?? value.request_id ?? newId("req_");
    this.result = resultValue(value.result) || StepResultType.Success;
    this.data = value.data ?? null;
    this.exception = value.exception ?? value.exceptionMessage ?? null;
    this.metadata = value.metadata ?? null;
    if (!this.dataFlowId) throw new Error("dataFlowId is required.");
    if (!this.requestId) throw new Error("requestId is required.");
  }

  static fromObject(value) {
    if (!value || Array.isArray(value) || typeof value !== "object") throw new Error("StepResult object is required.");
    return new StepResult(value);
  }

  static fromJson(value) {
    const parsed = JSON.parse(value);
    return StepResult.fromObject(parsed);
  }

  toObject() {
    return {
      protocolVersion: this.protocolVersion,
      tenantId: this.tenantId,
      dataFlowId: this.dataFlowId,
      flowRunId: this.flowRunId,
      stepRunId: this.stepRunId,
      requestId: this.requestId,
      result: this.result,
      data: this.data,
      exception: this.exception,
      metadata: this.metadata
    };
  }

  toJson() {
    return JSON.stringify(this.toObject());
  }
}

class TempoStepLogger {
  constructor(write) {
    this._write = typeof write === "function" ? write : () => {};
  }

  debug(...args) { this._write("DEBUG", args); }
  info(...args) { this._write("INFO", args); }
  warn(...args) { this._write("WARN", args); }
  error(...args) { this._write("ERROR", args); }
}

function correlateResult(result, request) {
  if (!(result instanceof StepResult)) throw new Error("result must be StepResult.");
  if (!(request instanceof StepRequest)) throw new Error("request must be StepRequest.");
  result.protocolVersion = request.protocolVersion;
  result.tenantId = request.tenantId;
  result.dataFlowId = request.dataFlowId;
  result.flowRunId = request.flowRunId;
  result.stepRunId = request.stepRunId;
  result.requestId = request.requestId;
  return result;
}

function success(request, data = null, metadata = undefined) {
  return correlateResult(new StepResult({
    result: StepResultType.Success,
    data,
    metadata: metadata === undefined ? request.metadata : metadata
  }), request);
}

function error(request, data = null, metadata = undefined) {
  const result = success(request, data, metadata);
  result.result = StepResultType.Error;
  return result;
}

function exceptionResult(request, errorValue, metadata = undefined) {
  const message = errorValue && errorValue.message ? errorValue.message : String(errorValue);
  return new StepResult({
    protocolVersion: request ? request.protocolVersion : CURRENT,
    tenantId: request ? request.tenantId : null,
    dataFlowId: request ? request.dataFlowId : "unknown",
    flowRunId: request ? request.flowRunId : null,
    stepRunId: request ? request.stepRunId : null,
    requestId: request ? request.requestId : newId("req_"),
    result: StepResultType.Exception,
    exception: message,
    metadata: metadata === undefined ? (request ? request.metadata : null) : metadata
  });
}

function step(fn) {
  fn.__tempoStep = true;
  return fn;
}

function supportedProtocolEnvironment(env = process.env) {
  return env[SUPPORTED_PROTOCOL_VERSIONS_ENV] || SUPPORTED.join(",");
}

function createLoggerFromEnvironment(env = process.env) {
  const file = env[RUN_LOG_FILE_ENV];
  if (!file) return null;
  return new TempoStepLogger((severity, args) => {
    const text = args.map((value) => {
      if (typeof value === "string") return value;
      try { return JSON.stringify(value); } catch { return String(value); }
    }).join(" ");
    if (!text) return;
    require("node:fs").mkdirSync(require("node:path").dirname(file), { recursive: true });
    require("node:fs").appendFileSync(file, new Date().toISOString() + " [" + severity + "] " + text + "\n", "utf8");
  });
}

function getCurrentExecutionContext() {
  return CURRENT_EXECUTION_CONTEXT;
}

async function readInput(input) {
  if (input === undefined || input === null) input = process.stdin;
  if (typeof input === "string") return input;
  if (typeof input.read === "function") {
    const direct = input.read();
    if (typeof direct === "string") return direct;
  }
  let data = "";
  for await (const chunk of input) data += chunk;
  return data;
}

function writeOutput(output, value) {
  output = output || process.stdout;
  if (typeof output === "function") {
    output(value);
    return;
  }
  output.write(value);
}

class TempoStepHost {
  static deserializeRequest(value) {
    return StepRequest.fromJson(value);
  }

  static serializeResult(result) {
    return result.toJson();
  }

  static async run(handler, options = {}) {
    let request = null;
    let result;
    const env = options.env || process.env;
    const logger = createLoggerFromEnvironment(env);
    const previousContext = CURRENT_EXECUTION_CONTEXT;
    const restore = patchConsole(logger);
    try {
      const inputText = await readInput(options.input);
      request = TempoStepHost.deserializeRequest(inputText);
      CURRENT_EXECUTION_CONTEXT = {
        tenantId: request.tenantId,
        dataFlowId: request.dataFlowId,
        flowRunId: request.flowRunId,
        runAssignmentId: env.TEMPO_RUN_ASSIGNMENT_ID || null,
        stepId: env.TEMPO_STEP_ID || null,
        stepRunId: request.stepRunId,
        workerId: env.TEMPO_WORKER_ID || null,
        logger
      };
      const maybeResult = await handler(request);
      result = maybeResult instanceof StepResult ? correlateResult(maybeResult, request) : success(request, maybeResult);
    } catch (err) {
      result = exceptionResult(request, err);
    } finally {
      restore();
      CURRENT_EXECUTION_CONTEXT = previousContext;
    }
    writeOutput(options.output, TempoStepHost.serializeResult(result));
    return 0;
  }
}

function patchConsole(logger) {
  if (!logger) return () => {};

  const original = {
    log: console.log,
    info: console.info,
    warn: console.warn,
    error: console.error,
    debug: console.debug,
    stderrWrite: process.stderr.write
  };

  console.log = (...args) => logger.info(...args);
  console.info = (...args) => logger.info(...args);
  console.warn = (...args) => logger.warn(...args);
  console.error = (...args) => logger.error(...args);
  console.debug = (...args) => logger.debug(...args);
  process.stderr.write = (chunk, encoding, callback) => {
    const text = typeof chunk === "string" ? chunk : Buffer.from(chunk).toString(typeof encoding === "string" ? encoding : "utf8");
    logger.error(text);
    if (typeof callback === "function") callback();
    return true;
  };

  return () => {
    console.log = original.log;
    console.info = original.info;
    console.warn = original.warn;
    console.error = original.error;
    console.debug = original.debug;
    process.stderr.write = original.stderrWrite;
  };
}

module.exports = {
  V1,
  CURRENT,
  SUPPORTED,
  PROTOCOL_VERSION_ENV,
  SUPPORTED_PROTOCOL_VERSIONS_ENV,
  StepResultType,
  StepRequest,
  StepResult,
  TempoStepLogger,
  TempoStepHost,
  normalizeProtocolVersion,
  isSupportedProtocolVersion,
  correlateResult,
  success,
  error,
  exceptionResult,
  step,
  supportedProtocolEnvironment,
  createLoggerFromEnvironment,
  getCurrentExecutionContext
};
