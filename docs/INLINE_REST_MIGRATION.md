# Inline REST Migration

Legacy flows may carry REST configuration directly in transition records.
Tempo keeps a read-path compatibility runtime for these flows, but new REST work
should use persisted `External.Rest` steps.

Admin route:

```http
POST /v1.0/migrations/inline-rest
```

Body options:

```json
{}
```

```json
{ "tenantId": "ten_..." }
```

```json
{ "tenantId": "ten_...", "flowId": "flow_..." }
```

The migrator creates deterministic persisted REST steps, rewrites flow
transitions to execution keys, and is safe to run multiple times.

