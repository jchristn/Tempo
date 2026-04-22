#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/compose.yaml"
TAIL_LINES="${1:-200}"

if ! [[ "$TAIL_LINES" =~ ^[0-9]+$ ]]; then
  echo "Provide an optional numeric line count." >&2
  echo "Example: ./tail-server-log.sh 200" >&2
  exit 1
fi

SERVICE_ID="$(docker compose -f "$COMPOSE_FILE" ps -q tempo-server 2>/dev/null || true)"
if [[ -z "$SERVICE_ID" ]]; then
  echo "tempo-server is not running in docker compose." >&2
  echo "Start it with: docker compose -f \"$COMPOSE_FILE\" up -d tempo-server" >&2
  exit 1
fi

echo
echo "Tailing /var/lib/tempo-server/logs/tempo.log from tempo-server (last ${TAIL_LINES} lines)"
exec docker compose -f "$COMPOSE_FILE" exec tempo-server sh -lc "mkdir -p /var/lib/tempo-server/logs && touch /var/lib/tempo-server/logs/tempo.log && tail -n ${TAIL_LINES} -f /var/lib/tempo-server/logs/tempo.log"
