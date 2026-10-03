#!/usr/bin/env bash
# Builds and pushes every Tempo image (server, MCP server, worker, dashboard) with the given tag.
# Equivalent of build-all.bat.
set -euo pipefail

if [[ $# -lt 1 || -z "${1:-}" ]]; then
  echo
  echo "Provide a tag argument"
  echo "Example: ./build-all.sh v0.3.0"
  exit 1
fi

cd "$(dirname "${BASH_SOURCE[0]}")"
IMAGE_TAG="$1"

for component in server:tempo-server mcp:tempo-mcp worker:tempo-worker dashboard:tempo-ui; do
  script="${component%%:*}"
  image="${component##*:}"
  echo
  echo "===================================================================="
  echo "Building ${image}:${IMAGE_TAG}"
  echo "===================================================================="
  if ! "./build-${script}.sh" "${IMAGE_TAG}"; then
    echo
    echo "Build failed"
    exit 1
  fi
done

echo
echo "All builds completed successfully"
echo "Done"
