import asyncio
import inspect
import io
import json
import os
import pathlib
import sys
import tempfile
import logging

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import tempo_sdk as sdk  # noqa: E402


def assert_equal(expected, actual, name):
    if expected != actual:
        raise AssertionError(f"{name}: expected {expected!r}, got {actual!r}")


def assert_true(value, name):
    if not value:
        raise AssertionError(f"{name}: expected true")


def assert_raises(exc_type, func, name):
    try:
        func()
    except exc_type:
        return
    except Exception as exc:
        raise AssertionError(f"{name}: expected {exc_type.__name__}, got {type(exc).__name__}") from exc
    raise AssertionError(f"{name}: expected {exc_type.__name__}")


def assert_ksortable_id(value, prefix, name):
    assert_true(value.startswith(prefix), name + " prefix")
    assert_equal(32, len(value), name + " length")
    parts = value[len(prefix):].split("_")
    assert_equal(2, len(parts), name + " segments")
    assert_true(len(parts[0]) > 0, name + " timestamp segment")
    assert_true(len(parts[1]) > 0, name + " random segment")


def request():
    return sdk.StepRequest(
        protocol_version="1.0",
        tenant_id="ten_1",
        data_flow_id="flow_1",
        flow_run_id="run_1",
        step_run_id="sru_1",
        request_id="req_1",
        data={"value": 5},
        metadata={"trace": "abc"},
        previous_result=sdk.StepResultType.SUCCESS,
    )


def test_public_api_coverage():
    expected_exports = [
        "V1",
        "CURRENT",
        "SUPPORTED",
        "PROTOCOL_VERSION_ENV",
        "SUPPORTED_PROTOCOL_VERSIONS_ENV",
        "StepResultType",
        "StepRequest",
        "StepResult",
        "TempoStepHost",
        "TempoStepLogger",
        "TempoExecutionContext",
        "normalize_protocol_version",
        "is_supported_protocol_version",
        "correlate_result",
        "success",
        "error",
        "exception_result",
        "step",
        "supported_protocol_environment",
        "create_logger_from_environment",
        "get_current_execution_context",
    ]
    assert_equal(expected_exports, sdk.__all__, "exports")

    expected_members = {
        "StepRequest": ["from_dict", "from_json", "to_dict", "to_json"],
        "StepResult": ["from_dict", "from_json", "to_dict", "to_json"],
        "TempoStepHost": ["deserialize_request", "run", "run_async", "serialize_result"],
        "TempoExecutionContext": ["current"],
    }
    for cls_name, members in expected_members.items():
        actual = sorted(name for name, value in inspect.getmembers(getattr(sdk, cls_name)) if not name.startswith("_") and callable(value))
        assert_equal(members, actual, cls_name + " public methods")

    assert_equal(
        ["ERROR", "EXCEPTION", "MAX_ITERATIONS_EXCEEDED", "SUCCESS", "TIMEOUT"],
        sorted(item.name for item in sdk.StepResultType),
        "result enum",
    )


def test_versions():
    assert_equal("1.0", sdk.V1, "v1")
    assert_equal("1.0", sdk.CURRENT, "current")
    assert_equal(("1.0",), sdk.SUPPORTED, "supported")
    assert_equal("TEMPO_PROTOCOL_VERSION", sdk.PROTOCOL_VERSION_ENV, "protocol env")
    assert_equal("TEMPO_SUPPORTED_PROTOCOL_VERSIONS", sdk.SUPPORTED_PROTOCOL_VERSIONS_ENV, "supported env")
    assert_true(sdk.is_supported_protocol_version(None), "none supported")
    assert_true(sdk.is_supported_protocol_version(" 1.0 "), "trim supported")
    assert_equal("1.0", sdk.normalize_protocol_version("1.0"), "normalize")
    assert_raises(ValueError, lambda: sdk.normalize_protocol_version("9.9"), "unsupported")


def test_request_model():
    defaults = sdk.StepRequest()
    assert_equal("1.0", defaults.protocol_version, "default protocol")
    assert_ksortable_id(defaults.data_flow_id, "flow_", "default flow")
    assert_ksortable_id(defaults.request_id, "req_", "default request")
    assert_raises(ValueError, lambda: sdk.StepRequest(protocol_version="9.9"), "bad protocol")

    raw = {
        "protocolVersion": "1.0",
        "tenantId": "ten_1",
        "dataFlowId": "flow_1",
        "flowRunId": "run_1",
        "stepRunId": "sru_1",
        "requestId": "req_1",
        "data": {"x": 2},
        "metadata": {"m": 3},
        "previousResult": "Error",
    }
    req = sdk.StepRequest.from_dict(raw)
    assert_equal("ten_1", req.tenant_id, "tenant")
    assert_equal(sdk.StepResultType.ERROR, req.previous_result, "previous result")
    assert_equal(raw, req.to_dict(), "request round trip dict")
    assert_equal(raw, sdk.StepRequest.from_json(json.dumps(raw)).to_dict(), "request round trip json")
    assert_raises(ValueError, lambda: sdk.StepRequest.from_json("[]"), "request object required")


def test_result_model_and_helpers():
    req = request()
    result = sdk.StepResult.from_dict({"result": "Timeout", "dataFlowId": "flow_x", "requestId": "req_x"})
    assert_equal(sdk.StepResultType.TIMEOUT, result.result, "result enum parse")
    assert_equal("Timeout", json.loads(result.to_json())["result"], "result json enum")
    assert_equal(result.to_dict(), sdk.StepResult.from_json(result.to_json()).to_dict(), "result round trip")
    assert_raises(ValueError, lambda: sdk.StepResult.from_json("[]"), "result object required")

    success = sdk.success(req, {"ok": True})
    assert_equal(sdk.StepResultType.SUCCESS, success.result, "success")
    assert_equal(req.request_id, success.request_id, "success correlation")
    assert_equal(req.metadata, success.metadata, "metadata fallback")

    error = sdk.error(req, {"valid": False}, {"reason": "bad"})
    assert_equal(sdk.StepResultType.ERROR, error.result, "error")
    assert_equal({"reason": "bad"}, error.metadata, "error metadata")

    exception = sdk.exception_result(req, RuntimeError("boom"))
    assert_equal(sdk.StepResultType.EXCEPTION, exception.result, "exception")
    assert_equal("boom", exception.exception, "exception message")
    assert_equal(req.data_flow_id, exception.data_flow_id, "exception correlation")

    no_request = sdk.exception_result(None, "no request")
    assert_equal("unknown", no_request.data_flow_id, "no request flow")
    assert_ksortable_id(no_request.request_id, "req_", "no request id")

    other = sdk.StepResult(data_flow_id="wrong", request_id="wrong")
    sdk.correlate_result(other, req)
    assert_equal(req.data_flow_id, other.data_flow_id, "correlate flow")
    assert_equal(req.request_id, other.request_id, "correlate request")


def test_decorator_and_runner():
    req = request()

    @sdk.step
    def handler(r):
        return {"handled": r.data["value"]}

    assert_true(getattr(handler, "__tempo_step__", False), "decorator marker")

    out = io.StringIO()
    code = sdk.TempoStepHost.run(handler, io.StringIO(req.to_json()), out)
    assert_equal(0, code, "run code")
    result = sdk.StepResult.from_json(out.getvalue())
    assert_equal(sdk.StepResultType.SUCCESS, result.result, "run success")
    assert_equal(req.request_id, result.request_id, "run correlation")
    assert_equal({"handled": 5}, result.data, "run data")

    async def async_handler(r):
        await asyncio.sleep(0)
        return sdk.error(r, {"async": True})

    out = io.StringIO()
    sdk.TempoStepHost.run(async_handler, io.StringIO(req.to_json()), out)
    async_result = sdk.StepResult.from_json(out.getvalue())
    assert_equal(sdk.StepResultType.ERROR, async_result.result, "async result")
    assert_equal({"async": True}, async_result.data, "async data")

    def throwing(_):
        raise RuntimeError("handler boom")

    out = io.StringIO()
    sdk.TempoStepHost.run(throwing, io.StringIO(req.to_json()), out)
    thrown = sdk.StepResult.from_json(out.getvalue())
    assert_equal(sdk.StepResultType.EXCEPTION, thrown.result, "throw result")
    assert_equal("handler boom", thrown.exception, "throw message")
    assert_equal(req.request_id, thrown.request_id, "throw correlation")

    out = io.StringIO()
    sdk.TempoStepHost.run(handler, io.StringIO("not-json"), out)
    invalid = sdk.StepResult.from_json(out.getvalue())
    assert_equal(sdk.StepResultType.EXCEPTION, invalid.result, "invalid result")
    assert_equal("unknown", invalid.data_flow_id, "invalid flow")

    serialized = sdk.TempoStepHost.serialize_result(sdk.success(req, {"ok": True}))
    assert_equal(sdk.StepResultType.SUCCESS, sdk.StepResult.from_json(serialized).result, "serialize helper")
    assert_equal(req.to_dict(), sdk.TempoStepHost.deserialize_request(req.to_json()).to_dict(), "deserialize helper")


def test_launch_environment_helper():
    os.environ.pop(sdk.SUPPORTED_PROTOCOL_VERSIONS_ENV, None)
    assert_equal("1.0", sdk.supported_protocol_environment(), "default supported env")
    os.environ[sdk.SUPPORTED_PROTOCOL_VERSIONS_ENV] = "1.0,1.1"
    assert_equal("1.0,1.1", sdk.supported_protocol_environment(), "custom supported env")


def test_logging_context_and_file():
    handle, log_path = tempfile.mkstemp(prefix="tempo-py-sdk-", suffix=".log")
    os.close(handle)
    os.unlink(log_path)

    previous = {
        "TEMPO_RUN_LOG_FILE": os.environ.get("TEMPO_RUN_LOG_FILE"),
        "TEMPO_RUN_ASSIGNMENT_ID": os.environ.get("TEMPO_RUN_ASSIGNMENT_ID"),
        "TEMPO_STEP_ID": os.environ.get("TEMPO_STEP_ID"),
        "TEMPO_WORKER_ID": os.environ.get("TEMPO_WORKER_ID"),
    }

    os.environ["TEMPO_RUN_LOG_FILE"] = log_path
    os.environ["TEMPO_RUN_ASSIGNMENT_ID"] = "ras_1"
    os.environ["TEMPO_STEP_ID"] = "step_1"
    os.environ["TEMPO_WORKER_ID"] = "wrk_1"

    try:
        req = request()

        def handler(_):
            context = sdk.get_current_execution_context()
            assert_true(context is not None, "execution context available")
            context.logger.info("logger-info")
            print("console-info")
            logging.getLogger().info("root-info")
            sys.stderr.write("console-error\n")
            return {
                "hasContext": context is not None,
                "runAssignmentId": context.run_assignment_id,
                "stepId": context.step_id,
                "workerId": context.worker_id,
            }

        out = io.StringIO()
        code = sdk.TempoStepHost.run(handler, io.StringIO(req.to_json()), out)
        assert_equal(0, code, "logging run code")

        result = sdk.StepResult.from_json(out.getvalue())
        assert_equal(sdk.StepResultType.SUCCESS, result.result, "logging result")
        assert_equal(True, result.data["hasContext"], "context flag")
        assert_equal("ras_1", result.data["runAssignmentId"], "assignment id")
        assert_equal("step_1", result.data["stepId"], "step id")
        assert_equal("wrk_1", result.data["workerId"], "worker id")
        assert_true(os.path.exists(log_path), "log file created")

        with open(log_path, "r", encoding="utf-8") as log_file:
            log_text = log_file.read()
        assert_true("logger-info" in log_text, "logger output captured")
        assert_true("console-info" in log_text, "print redirected to file")
        assert_true("root-info" in log_text, "root logging redirected to file")
        assert_true("console-error" in log_text, "stderr redirected to file")
        assert_true("console-info" not in out.getvalue(), "protocol stdout remains clean")
    finally:
        for key, value in previous.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        if os.path.exists(log_path):
            os.unlink(log_path)


if __name__ == "__main__":
    test_public_api_coverage()
    test_versions()
    test_request_model()
    test_result_model_and_helpers()
    test_decorator_and_runner()
    test_launch_environment_helper()
    test_logging_context_and_file()
    print("Tempo Python SDK test app PASS")
