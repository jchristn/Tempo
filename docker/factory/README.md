# Tempo Docker Factory Defaults

This directory contains the factory-default payload copied into Docker named volumes by:

- `docker/factory/reset.bat`
- `docker/factory/reset.sh`

The folder names match the logical volume names from `docker/compose.yaml`:

- `tempo_server_config`
- `tempo_server_db`
- `tempo_server_logs`
- `tempo_server_runtime_cache`
- `tempo_server_scratch`
- `dashboard_logs`
- `tempo_mcp_config`

Notes:

- `tempo_server_config/tempo.json` is the gold-copy Tempo.Server container config.
- `tempo_mcp_config/tempo.mcp.json` is the gold-copy Tempo.McpServer container config.
- `tempo_server_db/tempo.db` is intentionally empty. On the next Tempo.Server start, SQLite schema creation and Tempo hydration rebuild the default database state.
- log, cache, and scratch directories are restored to empty factory-default contents.

The reset scripts remove deployment data, recreate the named volumes, copy these factory files into the volumes, and then leave the deployment stopped so it can be started cleanly.
