#!/usr/bin/env bash
set -euo pipefail

CONFIG_PATH="${TEMPO_WORKER_SETTINGS_FILE:-/var/lib/tempo-worker/config/tempo.worker.json}"
ADMIN_API_KEY="${TEMPO_WORKER_BOOTSTRAP_ADMIN_API_KEY:-}"

json_value() {
  python3 - "$CONFIG_PATH" "$1" <<'PY'
import json
import sys

config_path, key = sys.argv[1], sys.argv[2]

try:
    with open(config_path, "r", encoding="utf-8") as handle:
        value = json.load(handle)
    for part in key.split("."):
        if not isinstance(value, dict):
            raise KeyError(part)
        value = value.get(part)
    if value is None:
        raise KeyError(key)
    if isinstance(value, bool):
        print("true" if value else "false")
    else:
        print(value)
except Exception:
    sys.exit(1)
PY
}

config_or_empty() {
  if [[ -f "$CONFIG_PATH" ]]; then
    json_value "$1" 2>/dev/null || true
  fi
}

SERVER_ENDPOINT="${TEMPO_WORKER_SERVER_ENDPOINT:-$(config_or_empty serverEndpoint)}"
SERVER_ENDPOINT="${SERVER_ENDPOINT:-http://tempo-server:8901}"
WORKER_ID="${TEMPO_WORKER_ID:-$(config_or_empty workerId)}"
WORKER_NAME="${TEMPO_WORKER_NAME:-$(config_or_empty name)}"

if [[ -z "$WORKER_ID" ]]; then
  WORKER_ID="wrk_${HOSTNAME:-docker_worker}"
  WORKER_ID="${WORKER_ID//[^a-zA-Z0-9_]/_}"
fi

if [[ -z "$WORKER_NAME" ]]; then
  WORKER_NAME="tempo-worker-${HOSTNAME:-1}"
fi

export TEMPO_WORKER_SETTINGS_FILE="$CONFIG_PATH"
export TEMPO_WORKER_SERVER_ENDPOINT="$SERVER_ENDPOINT"
export TEMPO_WORKER_ID="$WORKER_ID"
export TEMPO_WORKER_NAME="$WORKER_NAME"

if [[ -z "${TEMPO_WORKER_TOKEN:-}" && -n "$ADMIN_API_KEY" ]]; then
  HEALTH_URL="${SERVER_ENDPOINT%/}/v1.0/api/health"
  ROTATE_URL="${SERVER_ENDPOINT%/}/v1.0/workers/${WORKER_ID}/rotate-token"

  until curl -fsS "$HEALTH_URL" >/dev/null; do
    echo "[tempo-worker-entrypoint] waiting for Tempo.Server at $SERVER_ENDPOINT"
    sleep 2
  done

  RESPONSE_FILE="$(mktemp)"
  HTTP_STATUS="$(
    curl -sS \
      -o "$RESPONSE_FILE" \
      -w "%{http_code}" \
      -X POST \
      -H "x-api-key: ${ADMIN_API_KEY}" \
      "$ROTATE_URL"
  )"

  if [[ "$HTTP_STATUS" != "200" ]]; then
    echo "[tempo-worker-entrypoint] failed to issue worker token for ${WORKER_ID}: HTTP ${HTTP_STATUS}" >&2
    cat "$RESPONSE_FILE" >&2 || true
    rm -f "$RESPONSE_FILE"
    exit 1
  fi

  TOKEN_VALUE="$(
    python3 - "$RESPONSE_FILE" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as handle:
    payload = json.load(handle)

token = payload.get("token")
if not token:
    raise SystemExit(1)

print(token)
PY
  )" || {
    echo "[tempo-worker-entrypoint] rotate-token response did not contain a token for ${WORKER_ID}" >&2
    cat "$RESPONSE_FILE" >&2 || true
    rm -f "$RESPONSE_FILE"
    exit 1
  }

  rm -f "$RESPONSE_FILE"
  export TEMPO_WORKER_TOKEN="$TOKEN_VALUE"
fi

exec dotnet /app/Tempo.Worker.dll --config "$CONFIG_PATH"
