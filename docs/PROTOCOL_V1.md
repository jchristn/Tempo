# Protocol v1

Process-backed runtimes communicate over stdin/stdout with one JSON request and
one JSON result. The current protocol normalizes to `1.0`.

Request:

```json
{
  "protocolVersion": "1.0",
  "tenantId": "ten_...",
  "dataFlowId": "flow_...",
  "flowRunId": "run_...",
  "stepRunId": "sru_...",
  "requestId": "req_...",
  "data": {},
  "metadata": {},
  "previousResult": null
}
```

Result:

```json
{
  "protocolVersion": "1.0",
  "tenantId": "ten_...",
  "dataFlowId": "flow_...",
  "flowRunId": "run_...",
  "stepRunId": "sru_...",
  "requestId": "req_...",
  "result": "Success",
  "data": {},
  "exception": null,
  "metadata": {}
}
```

`result` is one of Tempo's step result names, including `Success`, `Error`,
`Exception`, and timeout-related outcomes. Invalid JSON, unsupported protocol
versions, and missing required correlation fields fail the step.

External process settings bound JSON input, stdout, stderr, and parsed output
size. Binary payload transport is out of scope for protocol v1 and should be
designed as a separate additive extension.

## Launch Environment

Tempo passes the negotiated protocol version to external processes in
`TEMPO_PROTOCOL_VERSION`. It also passes the comma-separated server-supported
set in `TEMPO_SUPPORTED_PROTOCOL_VERSIONS`.

The stdin `protocolVersion` field remains authoritative for each request. SDK
shims can use the launch environment for an early compatibility check before
reading stdin, then still preserve the request protocol version and correlation
fields in the emitted `StepResult`.

Protocol `1.x` changes are additive-only. Breaking protocol changes require a
new major protocol version and a server window where both old and new versions
can be negotiated from artifact manifests.
