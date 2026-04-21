#!/usr/bin/env bash
# Remove the SQLite database, logs, and generated settings file so the next
# run boots fresh. Intended for dev/test cycles only.
set -e
cd "$(dirname "$0")"
echo "[clean] Working dir: $(pwd)"

for f in tempo.db tempo.db-journal tempo.db-wal tempo.db-shm tempo.json; do
    if [ -f "$f" ]; then
        rm -f "$f"
        echo "[clean] removed $f"
    fi
done

if [ -d logs ]; then
    rm -rf logs
    echo "[clean] removed logs/"
fi

echo "[clean] done."
