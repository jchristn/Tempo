@ECHO OFF
SETLOCAL

IF "%~1" == "" GOTO :Usage

PUSHD "%~dp0"
SET IMAGE_TAG=%~1

ECHO.
ECHO Building jchristn77/tempo-ui:%IMAGE_TAG% and jchristn77/tempo-ui:latest
docker buildx build --pull --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/tempo-ui:%IMAGE_TAG% --tag jchristn77/tempo-ui:latest --push -f dashboard/Dockerfile dashboard
IF ERRORLEVEL 1 GOTO :Error

POPD
GOTO :Done

:Usage
ECHO.
ECHO Provide a tag argument
ECHO Example: build-dashboard.bat v0.3.0
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
