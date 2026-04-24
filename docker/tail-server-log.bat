@ECHO OFF
SETLOCAL EnableExtensions

SET "TAIL_LINES=%~1"
IF "%TAIL_LINES%"=="" SET "TAIL_LINES=200"

ECHO %TAIL_LINES%| findstr /R "^[0-9][0-9]*$" >NUL
IF ERRORLEVEL 1 GOTO :Usage

PUSHD "%~dp0"
SET "COMPOSE_FILE=%CD%\compose.yaml"
SET "SERVICE_ID="

FOR /F %%I IN ('docker compose -f "%COMPOSE_FILE%" ps -q tempo-server 2^>NUL') DO SET "SERVICE_ID=%%I"
IF NOT DEFINED SERVICE_ID GOTO :NotRunning

ECHO.
ECHO Tailing /var/lib/tempo-server/logs/tempo.log from tempo-server ^(last %TAIL_LINES% lines^)
docker compose -f "%COMPOSE_FILE%" exec tempo-server sh -lc "mkdir -p /var/lib/tempo-server/logs && touch /var/lib/tempo-server/logs/tempo.log && tail -n %TAIL_LINES% -f /var/lib/tempo-server/logs/tempo.log"
IF ERRORLEVEL 1 GOTO :Error

POPD
GOTO :Done

:Usage
ECHO.
ECHO Provide an optional numeric line count.
ECHO Example: tail-server-log.bat 200
GOTO :Exit

:NotRunning
POPD
ECHO.
ECHO tempo-server is not running in docker compose.
ECHO Start it with: docker compose -f "%COMPOSE_FILE%" up -d tempo-server
EXIT /B 1

:Error
POPD
ECHO.
ECHO Unable to tail the tempo-server log
EXIT /B 1

:Done
ECHO.
ECHO Done

:Exit
ENDLOCAL
@ECHO ON
