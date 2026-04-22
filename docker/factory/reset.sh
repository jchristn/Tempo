#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$DOCKER_DIR/compose.yaml"
HELPER_IMAGE="alpine:3.20"

echo
echo "This will fully reset the Tempo Docker deployment to factory default"
echo "It will stop containers, remove deployment data, restore docker config files,"
echo "recreate named data volumes, and copy factory defaults into those volumes"
echo
read -r -p "Type RESET to continue: " CONFIRM
if [[ "$CONFIRM" != "RESET" ]]; then
  echo
  echo "Aborted"
  exit 0
fi

PROJECT_NAME="$(docker compose -f "$COMPOSE_FILE" config | awk '/^name: / { print $2; exit }')"
if [[ -z "$PROJECT_NAME" ]]; then
  echo
  echo "Unable to determine the Docker Compose project name" >&2
  exit 1
fi

reset_volume() {
  local logical_name="$1"
  local source_dir="$SCRIPT_DIR/$logical_name"
  local volume_name="${PROJECT_NAME}_${logical_name}"

  if [[ ! -d "$source_dir" ]]; then
    echo "Missing factory directory: $source_dir" >&2
    exit 1
  fi

  echo "Restoring $logical_name..."
  docker volume create \
    --label "com.docker.compose.project=${PROJECT_NAME}" \
    --label "com.docker.compose.volume=${logical_name}" \
    "$volume_name" >/dev/null

  docker run --rm \
    -v "${volume_name}:/target" \
    -v "${source_dir}:/source:ro" \
    "$HELPER_IMAGE" \
    sh -c 'set -eu; mkdir -p /target; rm -rf /target/* /target/.[!.]* /target/..?* 2>/dev/null || true; cp -a /source/. /target/ 2>/dev/null || true; find /target -name .gitkeep -delete'
}

restore_config_file() {
  local source_file="$1"
  local target_file="$2"

  if [[ ! -f "$source_file" ]]; then
    echo "Missing factory file: $source_file" >&2
    exit 1
  fi

  cp "$source_file" "$target_file"
}

remove_legacy_volume() {
  local logical_name="$1"
  local volume_name="${PROJECT_NAME}_${logical_name}"
  if docker volume inspect "$volume_name" >/dev/null 2>&1; then
    docker volume rm -f "$volume_name" >/dev/null
  fi
}

echo
echo "Stopping deployment and removing existing data volumes..."
docker compose -f "$COMPOSE_FILE" down --remove-orphans --volumes

remove_legacy_volume tempo_server_config
remove_legacy_volume tempo_worker_config

restore_config_file "$SCRIPT_DIR/tempo_server_config/tempo.json" "$DOCKER_DIR/tempo.server.json"
restore_config_file "$SCRIPT_DIR/tempo_worker_config/tempo.worker.json" "$DOCKER_DIR/tempo.worker.json"
restore_config_file "$SCRIPT_DIR/tempo_mcp_config/tempo.mcp.json" "$DOCKER_DIR/tempo.mcp.json"

reset_volume tempo_server_db
reset_volume tempo_server_artifacts
reset_volume tempo_server_logs
reset_volume tempo_worker_logs
reset_volume tempo_server_runtime_cache
reset_volume tempo_server_scratch
reset_volume dashboard_logs
reset_volume tempo_mcp_config

echo
echo "Factory reset complete"
echo "Restart the deployment with:"
echo "  docker compose -f \"$COMPOSE_FILE\" up -d"
