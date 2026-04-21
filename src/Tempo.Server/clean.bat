@echo off
REM Remove the SQLite database, logs, and generated settings file so the next
REM run boots fresh. Intended for dev/test cycles only.

setlocal enabledelayedexpansion
set "HERE=%~dp0"
pushd "%HERE%" >nul

echo [clean] Working dir: %cd%

if exist "tempo.db" (
    del /f /q "tempo.db"
    echo [clean] removed tempo.db
)
if exist "tempo.db-journal" del /f /q "tempo.db-journal"
if exist "tempo.db-wal"     del /f /q "tempo.db-wal"
if exist "tempo.db-shm"     del /f /q "tempo.db-shm"

if exist "tempo.json" (
    del /f /q "tempo.json"
    echo [clean] removed tempo.json
)

if exist "logs" (
    rmdir /s /q "logs"
    echo [clean] removed logs\
)

echo [clean] done.
popd >nul
endlocal
