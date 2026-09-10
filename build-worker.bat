@ECHO OFF
SETLOCAL

IF "%~1" == "" GOTO :Usage

PUSHD "%~dp0"
SET IMAGE_TAG=%~1

ECHO.
ECHO Building and pushing jchristn77/tempo-worker:%IMAGE_TAG% and jchristn77/tempo-worker:latest to Docker Hub
docker buildx build --pull --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/tempo-worker:%IMAGE_TAG% --tag jchristn77/tempo-worker:latest --push -f src/Tempo.Worker/Dockerfile .
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO Loading jchristn77/tempo-worker:%IMAGE_TAG% and jchristn77/tempo-worker:latest into the local Docker image store
docker buildx build --pull --load --tag jchristn77/tempo-worker:%IMAGE_TAG% --tag jchristn77/tempo-worker:latest -f src/Tempo.Worker/Dockerfile .
IF ERRORLEVEL 1 GOTO :Error

POPD
GOTO :Done

:Usage
ECHO.
ECHO Provide a tag argument
ECHO Example: build-worker.bat v0.3.0
GOTO :Exit

:Error
POPD
ECHO.
ECHO Build failed
EXIT /B 1

:Done
ECHO.
ECHO Done

:Exit
ENDLOCAL
@ECHO ON
