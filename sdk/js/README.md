# Tempo JavaScript SDK

Protocol v1 SDK for Node.js artifact step handlers.

## API

- Protocol constants: `V1`, `CURRENT`, `SUPPORTED`,
  `PROTOCOL_VERSION_ENV`, `SUPPORTED_PROTOCOL_VERSIONS_ENV`
- Version helpers: `normalizeProtocolVersion`,
  `isSupportedProtocolVersion`
- Models: `StepRequest`, `StepResult`, `StepResultType`
- Result helpers: `success`, `error`, `exceptionResult`, `correlateResult`
- Handler marker: `step(fn)`
- Logging helpers: `TempoStepLogger`, `createLoggerFromEnvironment`
- Ambient context: `getCurrentExecutionContext()`
- Runner: `TempoStepHost.run(handler)`

## Handler

```js
const { TempoStepHost, step } = require("tempo-sdk");

const handler = step((request) => {
  return { ok: true, input: request.data };
});

TempoStepHost.run(handler).then((code) => process.exit(code));
```

Returning plain JSON-serializable data creates a `Success` result. Returning a
`StepResult` preserves the explicit result state. Exceptions are mapped to
`Exception` result envelopes with request correlation preserved when possible.

## Logging

Tempo reserves stdout for protocol JSON. Use the ambient logger or ordinary
`console.*` calls only through `TempoStepHost.run`, which redirects them to the
file-backed log sink when `TEMPO_RUN_LOG_FILE` is present.

```js
const { TempoStepHost, getCurrentExecutionContext, step } = require("tempo-sdk");

const handler = step((request) => {
  const ctx = getCurrentExecutionContext();
  ctx?.logger?.info("processing order");
  console.log("this also goes to the run log");
  return { ok: true, input: request.data };
});

TempoStepHost.run(handler).then((code) => process.exit(code));
```

## Test App

Run from the repository root:

```powershell
npm.cmd --prefix .\sdk\js test
```

The test app validates every exported SDK symbol and every public model/host
method.
