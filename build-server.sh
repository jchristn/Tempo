#!/usr/bin/env bash
# Builds jchristn77/tempo-server for linux/amd64 and linux/arm64/v8, pushes it to Docker Hub with the
# given tag and :latest, then loads the same tags into the local Docker image store.
# Equivalent of build-server.bat.
set -euo pipefail

if [[ $# -lt 1 || -z "${1:-}" ]]; then
  echo
  echo "Provide a tag argument"
  echo "Example: ./build-server.sh v0.3.0"
  exit 1
fi

cd "$(dirname "${BASH_SOURCE[0]}")"
IMAGE_TAG="$1"

echo
echo "Building and pushing jchristn77/tempo-server:${IMAGE_TAG} and jchristn77/tempo-server:latest to Docker Hub"
docker buildx build --pull --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag "jchristn77/tempo-server:${IMAGE_TAG}" --tag jchristn77/tempo-server:latest --push -f src/Tempo.Server/Dockerfile .

echo
echo "Loading jchristn77/tempo-server:${IMAGE_TAG} and jchristn77/tempo-server:latest into the local Docker image store"
docker buildx build --pull --load --tag "jchristn77/tempo-server:${IMAGE_TAG}" --tag jchristn77/tempo-server:latest -f src/Tempo.Server/Dockerfile .

echo
echo "Done"
