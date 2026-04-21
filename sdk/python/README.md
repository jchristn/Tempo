# Tempo Python SDK

Protocol v1 SDK for Python artifact step handlers.

## API

- Protocol constants: `V1`, `CURRENT`, `SUPPORTED`,
  `PROTOCOL_VERSION_ENV`, `SUPPORTED_PROTOCOL_VERSIONS_ENV`
- Version helpers: `normalize_protocol_version`,
  `is_supported_protocol_version`
- Models: `StepRequest`, `StepResult`, `StepResultType`
- Result helpers: `success`, `error`, `exception_result`,
  `correlate_result`
- Handler marker: `@step`
- Runner: `TempoStepHost.run()` and `TempoStepHost.run_async()`

## Handler

```python
from tempo_sdk import TempoStepHost, step

@step
def handler(request):
    return {"ok": True, "input": request.data}

if __name__ == "__main__":
    raise SystemExit(TempoStepHost.run(handler))
```

Returning plain JSON-serializable data creates a `Success` result. Returning a
`StepResult` preserves the explicit result state. Exceptions are mapped to
`Exception` result envelopes with request correlation preserved when possible.

## Examples

Simple transform:

```python
@step
def transform(request):
    return {"orderId": request.data["orderId"], "score": 100}
```

Validation failure:

```python
def validate(request):
    if "orderId" not in request.data:
        return error(request, {"field": "orderId"}, {"reason": "missing"})
    return success(request, {"valid": True})
```

Exception:

```python
def fail(request):
    raise RuntimeError("upstream dependency failed")
```

JSON schema input:

```python
def schema_aware(request):
    schema_name = (request.metadata or {}).get("schema", "unknown")
    return {"schema": schema_name, "data": request.data}
```

## Test App

Run from the repository root:

```powershell
python .\sdk\python\test_app\test_sdk.py
```

The test app validates every exported SDK symbol and every public model/host
method.
