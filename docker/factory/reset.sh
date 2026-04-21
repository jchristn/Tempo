#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$DOCKER_DIR/compose.yaml"
HELPER_IMAGE="alpine:3.20"

echo
echo "This will fully reset the Tempo Docker deployment to factory default"
echo "It will stop containers, remove deployment data, recreate named volumes,"
echo "and copy the contents of docker/factory into those volumes"
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

echo
echo "Stopping deployment and removing existing data volumes..."
docker compose -f "$COMPOSE_FILE" down --remove-orphans --volumes

reset_volume tempo_server_config
reset_volume tempo_server_db
reset_volume tempo_server_logs
reset_volume tempo_server_runtime_cache
reset_volume tempo_server_scratch
reset_volume dashboard_logs
reset_volume tempo_mcp_config

echo
echo "Factory reset complete"
echo "Restart the deployment with:"
echo "  docker compose -f \"$COMPOSE_FILE\" up --build -d"
