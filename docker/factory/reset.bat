@ECHO OFF
SETLOCAL EnableExtensions

SET "SCRIPT_DIR=%~dp0"
FOR %%I IN ("%SCRIPT_DIR%.") DO SET "FACTORY_DIR=%%~fI"
FOR %%I IN ("%SCRIPT_DIR%..") DO SET "DOCKER_DIR=%%~fI"
SET "COMPOSE_FILE=%DOCKER_DIR%\compose.yaml"
SET "HELPER_IMAGE=alpine:3.20"

ECHO.
ECHO This will fully reset the Tempo Docker deployment to factory default.
ECHO It will stop containers, remove deployment data, restore docker config files,
ECHO recreate named data volumes, and copy factory defaults into those volumes.
ECHO.
SET /P CONFIRM=Type RESET to continue: 
IF NOT "%CONFIRM%"=="RESET" (
  ECHO.
  ECHO Aborted
  GOTO :Done
)

FOR /F "tokens=2 delims=: " %%I IN ('docker compose -f "%COMPOSE_FILE%" config ^| findstr /B /C:"name:"') DO IF NOT DEFINED PROJECT_NAME SET "PROJECT_NAME=%%I"
IF NOT DEFINED PROJECT_NAME (
  ECHO.
  ECHO Unable to determine the Docker Compose project name
  GOTO :Error
)

ECHO.
ECHO Stopping deployment and removing existing data volumes...
docker compose -f "%COMPOSE_FILE%" down --remove-orphans --volumes
IF ERRORLEVEL 1 GOTO :Error

CALL :RemoveLegacyVolume tempo_server_config
IF ERRORLEVEL 1 GOTO :Error

CALL :RemoveLegacyVolume tempo_worker_config
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreConfigFile tempo_server_config\tempo.json "%DOCKER_DIR%\tempo.server.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreConfigFile tempo_worker_config\tempo.worker.json "%DOCKER_DIR%\tempo.worker.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreConfigFile tempo_mcp_config\tempo.mcp.json "%DOCKER_DIR%\tempo.mcp.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_server_db
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_server_artifacts
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_server_logs
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_worker_logs
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_run_logs
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_server_runtime_cache
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_server_scratch
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume dashboard_logs
IF ERRORLEVEL 1 GOTO :Error

CALL :ResetVolume tempo_mcp_config
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO Factory reset complete
ECHO Restart the deployment with:
ECHO   docker compose -f "%COMPOSE_FILE%" up -d
GOTO :Done

:RestoreConfigFile
SETLOCAL EnableExtensions
SET "RELATIVE_SOURCE=%~1"
SET "TARGET_FILE=%~2"
SET "SOURCE_FILE=%FACTORY_DIR%\%RELATIVE_SOURCE%"

IF NOT EXIST "%SOURCE_FILE%" (
  ECHO Missing factory file: "%SOURCE_FILE%"
  ENDLOCAL & EXIT /B 1
)

COPY /Y "%SOURCE_FILE%" "%TARGET_FILE%" >NUL
IF ERRORLEVEL 1 (
  ENDLOCAL & EXIT /B 1
)

ENDLOCAL & EXIT /B 0

:ResetVolume
SETLOCAL EnableExtensions
SET "LOGICAL_NAME=%~1"
SET "SOURCE_DIR=%FACTORY_DIR%\%LOGICAL_NAME%"
SET "VOLUME_NAME=%PROJECT_NAME%_%LOGICAL_NAME%"

IF NOT EXIST "%SOURCE_DIR%" (
  ECHO Missing factory directory: "%SOURCE_DIR%"
  ENDLOCAL & EXIT /B 1
)

ECHO Restoring %LOGICAL_NAME%...
docker volume create --label com.docker.compose.project=%PROJECT_NAME% --label com.docker.compose.volume=%LOGICAL_NAME% "%VOLUME_NAME%" >NUL
IF ERRORLEVEL 1 (
  ENDLOCAL & EXIT /B 1
)

docker run --rm -v "%VOLUME_NAME%:/target" -v "%SOURCE_DIR%:/source:ro" %HELPER_IMAGE% sh -c "set -eu; mkdir -p /target; rm -rf /target/* /target/.[!.]* /target/..?* 2>/dev/null || true; cp -a /source/. /target/ 2>/dev/null || true; find /target -name .gitkeep -delete"
IF ERRORLEVEL 1 (
  ENDLOCAL & EXIT /B 1
)

ENDLOCAL & EXIT /B 0

:RemoveLegacyVolume
SETLOCAL EnableExtensions
SET "LOGICAL_NAME=%~1"
SET "VOLUME_NAME=%PROJECT_NAME%_%LOGICAL_NAME%"

docker volume inspect "%VOLUME_NAME%" >NUL 2>&1
IF NOT ERRORLEVEL 1 docker volume rm -f "%VOLUME_NAME%" >NUL

ENDLOCAL & EXIT /B 0

:Error
ECHO.
ECHO Factory reset failed
EXIT /B 1

:Done
ECHO.
ENDLOCAL
@ECHO ON
