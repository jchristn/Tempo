@ECHO OFF
SETLOCAL

IF "%~1" == "" GOTO :Usage

PUSHD "%~dp0"
SET IMAGE_TAG=%~1

ECHO.
ECHO ====================================================================
ECHO Building tempo-server:%IMAGE_TAG%
ECHO ====================================================================
CALL build-server.bat "%IMAGE_TAG%"
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO ====================================================================
ECHO Building tempo-mcp:%IMAGE_TAG%
ECHO ====================================================================
CALL build-mcp.bat "%IMAGE_TAG%"
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO ====================================================================
ECHO Building tempo-worker:%IMAGE_TAG%
ECHO ====================================================================
CALL build-worker.bat "%IMAGE_TAG%"
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO ====================================================================
ECHO Building tempo-ui:%IMAGE_TAG%
ECHO ====================================================================
CALL build-dashboard.bat "%IMAGE_TAG%"
IF ERRORLEVEL 1 GOTO :Error

POPD
GOTO :Done

:Usage
ECHO.
ECHO Provide a tag argument
ECHO Example: build-all.bat v0.3.0
GOTO :Exit

:Error
POPD
ECHO.
ECHO Build failed
EXIT /B 1

:Done
ECHO.
ECHO All builds completed successfully
ECHO Done

:Exit
ENDLOCAL
@ECHO ON
