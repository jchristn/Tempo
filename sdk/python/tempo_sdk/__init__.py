"""Tempo protocol v1 SDK for Python artifact step handlers."""

from __future__ import annotations

import asyncio
import builtins
import inspect
import json
import logging
import os
import secrets
import string
import sys
import time
from dataclasses import dataclass
from enum import Enum
from typing import Any, Callable, Iterable, Optional

V1 = "1.0"
CURRENT = V1
SUPPORTED = (V1,)
PROTOCOL_VERSION_ENV = "TEMPO_PROTOCOL_VERSION"
SUPPORTED_PROTOCOL_VERSIONS_ENV = "TEMPO_SUPPORTED_PROTOCOL_VERSIONS"
_ID_LENGTH = 32
_ID_CHARS = string.ascii_letters + string.digits

__all__ = [
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

_current_execution_context = None


class StepResultType(str, Enum):
    """Tempo step result states."""

    SUCCESS = "Success"
    TIMEOUT = "Timeout"
    ERROR = "Error"
    EXCEPTION = "Exception"
    MAX_ITERATIONS_EXCEEDED = "MaxIterationsExceeded"


def is_supported_protocol_version(version: Optional[str]) -> bool:
    """Return True when *version* is supported. Empty values default to current."""

    normalized = CURRENT if version is None or not str(version).strip() else str(version).strip()
    return any(item.lower() == normalized.lower() for item in SUPPORTED)


def normalize_protocol_version(version: Optional[str]) -> str:
    """Return the canonical protocol version or raise ValueError."""

    normalized = CURRENT if version is None or not str(version).strip() else str(version).strip()
    for item in SUPPORTED:
        if item.lower() == normalized.lower():
            return item
    raise ValueError(f"Unsupported Tempo step protocol version '{normalized}'.")


def _new_id(prefix: str) -> str:
    timestamp = _base36(int(time.time() * 1000))
    random_length = max(1, _ID_LENGTH - len(prefix) - len(timestamp) - 1)
    random = "".join(secrets.choice(_ID_CHARS) for _ in range(random_length))
    return f"{prefix}{timestamp}_{random}"


def _base36(value: int) -> str:
    chars = "0123456789abcdefghijklmnopqrstuvwxyz"
    if value == 0:
        return "0"
    result = ""
    while value:
        value, remainder = divmod(value, 36)
        result = chars[remainder] + result
    return result


def _result_value(value: StepResultType | str | None) -> str | None:
    if value is None:
        return None
    if isinstance(value, StepResultType):
        return value.value
    return str(value)


def _result_type(value: StepResultType | str | None) -> StepResultType | None:
    if value is None:
        return None
    if isinstance(value, StepResultType):
        return value
    return StepResultType(str(value))


@dataclass
class StepRequest:
    """Tempo protocol request envelope."""

    protocol_version: str = CURRENT
    tenant_id: Optional[str] = None
    data_flow_id: str = ""
    flow_run_id: Optional[str] = None
    step_run_id: Optional[str] = None
    request_id: str = ""
    data: Any = None
    metadata: Any = None
    previous_result: Optional[StepResultType] = None

    def __post_init__(self) -> None:
        self.protocol_version = normalize_protocol_version(self.protocol_version)
        if not self.data_flow_id:
            self.data_flow_id = _new_id("flow_")
        if not self.request_id:
            self.request_id = _new_id("req_")
        if self.previous_result is not None:
            self.previous_result = _result_type(self.previous_result)

    @classmethod
    def from_dict(cls, value: dict[str, Any]) -> "StepRequest":
        """Create a request from a protocol dictionary."""

        return cls(
            protocol_version=value.get("protocolVersion", CURRENT),
            tenant_id=value.get("tenantId"),
            data_flow_id=value.get("dataFlowId") or "",
            flow_run_id=value.get("flowRunId"),
            step_run_id=value.get("stepRunId"),
            request_id=value.get("requestId") or "",
            data=value.get("data"),
            metadata=value.get("metadata"),
            previous_result=_result_type(value.get("previousResult")),
        )

    @classmethod
    def from_json(cls, value: str) -> "StepRequest":
        """Create a request from JSON."""

        loaded = json.loads(value)
        if not isinstance(loaded, dict):
            raise ValueError("StepRequest JSON must be an object.")
        return cls.from_dict(loaded)

    def to_dict(self) -> dict[str, Any]:
        """Convert the request to a protocol dictionary."""

        return {
            "protocolVersion": self.protocol_version,
            "tenantId": self.tenant_id,
            "dataFlowId": self.data_flow_id,
            "flowRunId": self.flow_run_id,
            "stepRunId": self.step_run_id,
            "requestId": self.request_id,
            "data": self.data,
            "metadata": self.metadata,
            "previousResult": _result_value(self.previous_result),
        }

    def to_json(self) -> str:
        """Convert the request to compact JSON."""

        return json.dumps(self.to_dict(), separators=(",", ":"))


@dataclass
class StepResult:
    """Tempo protocol result envelope."""

    protocol_version: str = CURRENT
    tenant_id: Optional[str] = None
    data_flow_id: str = ""
    flow_run_id: Optional[str] = None
    step_run_id: Optional[str] = None
    request_id: str = ""
    result: StepResultType = StepResultType.SUCCESS
    data: Any = None
    exception: Optional[str] = None
    metadata: Any = None

    def __post_init__(self) -> None:
        self.protocol_version = normalize_protocol_version(self.protocol_version)
        if not self.data_flow_id:
            self.data_flow_id = _new_id("flow_")
        if not self.request_id:
            self.request_id = _new_id("req_")
        self.result = _result_type(self.result) or StepResultType.SUCCESS

    @classmethod
    def from_dict(cls, value: dict[str, Any]) -> "StepResult":
        """Create a result from a protocol dictionary."""

        return cls(
            protocol_version=value.get("protocolVersion", CURRENT),
            tenant_id=value.get("tenantId"),
            data_flow_id=value.get("dataFlowId") or "",
            flow_run_id=value.get("flowRunId"),
            step_run_id=value.get("stepRunId"),
            request_id=value.get("requestId") or "",
            result=_result_type(value.get("result")) or StepResultType.SUCCESS,
            data=value.get("data"),
            exception=value.get("exception"),
            metadata=value.get("metadata"),
        )

    @classmethod
    def from_json(cls, value: str) -> "StepResult":
        """Create a result from JSON."""

        loaded = json.loads(value)
        if not isinstance(loaded, dict):
            raise ValueError("StepResult JSON must be an object.")
        return cls.from_dict(loaded)

    def to_dict(self) -> dict[str, Any]:
        """Convert the result to a protocol dictionary."""

        return {
            "protocolVersion": self.protocol_version,
            "tenantId": self.tenant_id,
            "dataFlowId": self.data_flow_id,
            "flowRunId": self.flow_run_id,
            "stepRunId": self.step_run_id,
            "requestId": self.request_id,
            "result": self.result.value,
            "data": self.data,
            "exception": self.exception,
            "metadata": self.metadata,
        }

    def to_json(self) -> str:
        """Convert the result to compact JSON."""

        return json.dumps(self.to_dict(), separators=(",", ":"))


def correlate_result(result: StepResult, request: StepRequest) -> StepResult:
    """Copy host-owned correlation fields from request to result."""

    result.protocol_version = request.protocol_version
    result.tenant_id = request.tenant_id
    result.data_flow_id = request.data_flow_id
    result.flow_run_id = request.flow_run_id
    result.step_run_id = request.step_run_id
    result.request_id = request.request_id
    return result


def success(request: StepRequest, data: Any = None, metadata: Any = None) -> StepResult:
    """Create a success result correlated to request."""

    return correlate_result(
        StepResult(result=StepResultType.SUCCESS, data=data, metadata=metadata if metadata is not None else request.metadata),
        request,
    )


def error(request: StepRequest, data: Any = None, metadata: Any = None) -> StepResult:
    """Create an error result correlated to request."""

    result = success(request, data, metadata)
    result.result = StepResultType.ERROR
    return result


def exception_result(request: Optional[StepRequest], exc: BaseException | str, metadata: Any = None) -> StepResult:
    """Create an exception result correlated to request when available."""

    message = str(exc)
    return StepResult(
        protocol_version=request.protocol_version if request else CURRENT,
        tenant_id=request.tenant_id if request else None,
        data_flow_id=request.data_flow_id if request else "unknown",
        flow_run_id=request.flow_run_id if request else None,
        step_run_id=request.step_run_id if request else None,
        request_id=request.request_id if request else _new_id("req_"),
        result=StepResultType.EXCEPTION,
        exception=message,
        metadata=metadata if metadata is not None else (request.metadata if request else None),
    )


def step(func: Callable[..., Any]) -> Callable[..., Any]:
    """Mark a function as a Tempo step handler."""

    setattr(func, "__tempo_step__", True)
    return func


class TempoStepLogger:
    """Simple file-backed step logger."""

    def __init__(self, write: Callable[[str, Any], None]) -> None:
        self._write = write

    def debug(self, *args: Any) -> None:
        self._write("DEBUG", args)

    def info(self, *args: Any) -> None:
        self._write("INFO", args)

    def warn(self, *args: Any) -> None:
        self._write("WARN", args)

    def error(self, *args: Any) -> None:
        self._write("ERROR", args)


@dataclass
class TempoExecutionContext:
    """Ambient execution context for the currently running handler."""

    tenant_id: Optional[str]
    data_flow_id: Optional[str]
    flow_run_id: Optional[str]
    run_assignment_id: Optional[str]
    step_id: Optional[str]
    step_run_id: Optional[str]
    worker_id: Optional[str]
    logger: Optional[TempoStepLogger]

    @staticmethod
    def current() -> Optional["TempoExecutionContext"]:
        return _current_execution_context


def get_current_execution_context() -> Optional[TempoExecutionContext]:
    """Return the ambient Tempo execution context when running under TempoStepHost."""

    return _current_execution_context


def create_logger_from_environment(env: Optional[dict[str, str]] = None) -> Optional[TempoStepLogger]:
    """Create a file-backed logger from Tempo launch environment variables."""

    env = env or os.environ
    path = env.get("TEMPO_RUN_LOG_FILE")
    if not path:
        return None

    def write(severity: str, args: Any) -> None:
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        parts = []
        for value in args:
            if isinstance(value, str):
                parts.append(value)
            else:
                try:
                    parts.append(json.dumps(value))
                except Exception:
                    parts.append(str(value))
        text = " ".join(parts)
        if not text:
            return
        with open(path, "a", encoding="utf-8") as handle:
            handle.write(f"{time.strftime('%Y-%m-%dT%H:%M:%S', time.gmtime())}.{int((time.time() % 1) * 1000000):06d}Z [{severity}] {text}\n")

    return TempoStepLogger(write)


class TempoStepHost:
    """stdin/stdout runner and JSON helpers."""

    @staticmethod
    def deserialize_request(value: str) -> StepRequest:
        """Deserialize request JSON."""

        return StepRequest.from_json(value)

    @staticmethod
    def serialize_result(result: StepResult) -> str:
        """Serialize a result envelope."""

        return result.to_json()

    @staticmethod
    async def run_async(handler: Callable[[StepRequest], Any], input_stream: Any = None, output_stream: Any = None) -> int:
        """Read one request, invoke handler, and write one result."""

        input_stream = input_stream or sys.stdin
        output_stream = output_stream or sys.stdout
        request: Optional[StepRequest] = None
        logger = create_logger_from_environment()
        scope_restore = _install_logging_redirects(logger)
        try:
            text = input_stream.read()
            request = TempoStepHost.deserialize_request(text)
            global _current_execution_context
            previous_context = _current_execution_context
            _current_execution_context = TempoExecutionContext(
                tenant_id=request.tenant_id,
                data_flow_id=request.data_flow_id,
                flow_run_id=request.flow_run_id,
                run_assignment_id=os.environ.get("TEMPO_RUN_ASSIGNMENT_ID"),
                step_id=os.environ.get("TEMPO_STEP_ID"),
                step_run_id=request.step_run_id,
                worker_id=os.environ.get("TEMPO_WORKER_ID"),
                logger=logger,
            )
            maybe_result = handler(request)
            if inspect.isawaitable(maybe_result):
                maybe_result = await maybe_result
            if isinstance(maybe_result, StepResult):
                result = correlate_result(maybe_result, request)
            else:
                result = success(request, maybe_result)
        except BaseException as exc:
            result = exception_result(request, exc)
        finally:
            _current_execution_context = previous_context if "previous_context" in locals() else None
            scope_restore()
        output_stream.write(TempoStepHost.serialize_result(result))
        return 0

    @staticmethod
    def run(handler: Callable[[StepRequest], Any], input_stream: Any = None, output_stream: Any = None) -> int:
        """Synchronous wrapper around run_async."""

        return asyncio.run(TempoStepHost.run_async(handler, input_stream, output_stream))


def supported_protocol_environment() -> str:
    """Return the comma-separated supported protocol value used by Tempo launch env."""

    return os.environ.get(SUPPORTED_PROTOCOL_VERSIONS_ENV, ",".join(SUPPORTED))


def _install_logging_redirects(logger: Optional[TempoStepLogger]) -> Callable[[], None]:
    if logger is None:
        return lambda: None

    original_print = builtins.print
    original_stderr = sys.stderr
    original_handlers = list(logging.getLogger().handlers)
    original_level = logging.getLogger().level

    def patched_print(*args: Any, sep: str = " ", end: str = "\n", file: Any = None, flush: bool = False) -> None:
        text = sep.join("" if arg is None else str(arg) for arg in args)
        if end and end != "\n":
            text += end
        logger.info(text.rstrip("\n"))

    class _LoggerStream:
        def write(self, value: str) -> None:
            if value and value.strip():
                logger.error(value.rstrip("\n"))

        def flush(self) -> None:
            return

    class _TempoHandler(logging.Handler):
        def emit(self, record: logging.LogRecord) -> None:
            logger.info(self.format(record))

    builtins.print = patched_print
    sys.stderr = _LoggerStream()
    handler = _TempoHandler()
    handler.setFormatter(logging.Formatter("%(message)s"))
    root = logging.getLogger()
    root.handlers = [handler]
    root.setLevel(logging.INFO)

    def restore() -> None:
        builtins.print = original_print
        sys.stderr = original_stderr
        root.handlers = original_handlers
        root.setLevel(original_level)

    return restore
