# Tempo Docker Factory Defaults

This directory contains the factory-default payload copied into Docker named volumes by:

- `docker/factory/reset.bat`
- `docker/factory/reset.sh`

The factory content is used in two ways:

- `tempo_server_config`, `tempo_worker_config`, and `tempo_mcp_config` provide the gold-copy JSON config files restored by the reset scripts
- the remaining folders match the logical named data volumes from `docker/compose.yaml`

Named data volumes:

- `tempo_server_db`
- `tempo_server_artifacts`
- `tempo_server_logs`
- `tempo_worker_logs`
- `tempo_run_logs`
- `tempo_server_runtime_cache`
- `tempo_server_scratch`
- `dashboard_logs`
- `tempo_mcp_config`

Notes:

- `tempo_server_config/tempo.json` is restored to `docker/tempo.server.json`, which Compose bind-mounts into `Tempo.Server`.
- `tempo_worker_config/tempo.worker.json` is restored to `docker/tempo.worker.json`, which Compose bind-mounts into every `Tempo.Worker` container.
- `tempo_mcp_config/tempo.mcp.json` is both the gold-copy `Tempo.McpServer` config and the seed payload copied into the MCP config volume.
- `tempo_server_db/tempo.db` is intentionally empty. On the next Tempo.Server start, SQLite schema creation and Tempo hydration rebuild the default database state.
- `tempo_server_artifacts` is intentionally empty. On the next Tempo.Server start, source steps, imported artifacts, and startup sample artifacts rebuild into this persisted blob store and remain available across later container recreation.
- `tempo_worker_logs` is intentionally empty. Scaled workers recreate per-worker subdirectories under this shared log volume, and Tempo.Server reads the same volume read-only through its log-viewer surface.
- `tempo_run_logs` is intentionally empty. Tempo.Server and Tempo.Worker share this volume for per-run log capture so run logs survive container restarts and remain visible to the tenant-scoped run-log APIs and dashboard.
- worker runtime-cache and scratch storage remain container-local anonymous volumes so scaled workers do not share mutable runtime state.
- remaining named log, cache, and scratch directories are restored to empty factory-default contents.

The reset scripts remove deployment data, restore the bind-mounted Docker config files, recreate the named data volumes, copy these factory files into the volumes, and then leave the deployment stopped so it can be started cleanly.
