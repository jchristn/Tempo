@ECHO OFF
SETLOCAL EnableExtensions EnableDelayedExpansion

IF "%~1" == "" GOTO :Usage

SET "NUGET_API_KEY=%~1"
SET "ROOT=%~dp0"
SET "OUTPUT=%ROOT%artifacts\nuget"
SET "NUGET_SOURCE=https://api.nuget.org/v3/index.json"

ECHO.
ECHO Packing and publishing NuGet packages using project metadata

IF NOT EXIST "%OUTPUT%" MKDIR "%OUTPUT%"
IF ERRORLEVEL 1 GOTO :Error

CALL :PackAndPush "%ROOT%src\Tempo\Tempo.csproj"
IF ERRORLEVEL 1 GOTO :Error

CALL :PackAndPush "%ROOT%sdk\csharp\Tempo.Sdk\Tempo.Sdk.csproj"
IF ERRORLEVEL 1 GOTO :Error

ECHO.
ECHO Published NuGet packages successfully
GOTO :Done

:PackAndPush
SET "PROJECT_FILE=%~1"
SET "PACKAGE_ID="
SET "PACKAGE_VERSION="

CALL :ReadProjectMetadata "%PROJECT_FILE%" PACKAGE_ID PACKAGE_VERSION
IF ERRORLEVEL 1 EXIT /B 1

ECHO.
ECHO Packing !PACKAGE_ID! !PACKAGE_VERSION!

dotnet pack "%PROJECT_FILE%" -c Release -o "%OUTPUT%" /p:GeneratePackageOnBuild=false /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg
IF ERRORLEVEL 1 EXIT /B 1

SET "PACKAGE_FILE=%OUTPUT%\!PACKAGE_ID!.!PACKAGE_VERSION!.nupkg"
IF NOT EXIST "!PACKAGE_FILE!" (
  ECHO Missing package: "!PACKAGE_FILE!"
  EXIT /B 1
)

ECHO Publishing !PACKAGE_ID! !PACKAGE_VERSION! to NuGet
dotnet nuget push "!PACKAGE_FILE!" --api-key "%NUGET_API_KEY%" --source "%NUGET_SOURCE%" --skip-duplicate
IF ERRORLEVEL 1 EXIT /B 1

SET "SYMBOL_FILE=%OUTPUT%\!PACKAGE_ID!.!PACKAGE_VERSION!.snupkg"
IF EXIST "!SYMBOL_FILE!" (
  dotnet nuget push "!SYMBOL_FILE!" --api-key "%NUGET_API_KEY%" --source "%NUGET_SOURCE%" --skip-duplicate
  IF ERRORLEVEL 1 EXIT /B 1
)

EXIT /B 0

:ReadProjectMetadata
SET "%~2="
SET "%~3="
FOR /F "usebackq tokens=1,2 delims=|" %%A IN (`powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$projectFile = '%~1';" ^
  "$output = & dotnet msbuild $projectFile -getProperty:PackageId -getProperty:PackageVersion -getProperty:Version -getProperty:AssemblyName;" ^
  "$json = [string]::Join([Environment]::NewLine, $output);" ^
  "$result = ConvertFrom-Json -InputObject $json;" ^
  "$props = $result.Properties;" ^
  "$packageId = $props.PackageId;" ^
  "if ([string]::IsNullOrWhiteSpace($packageId)) { $packageId = $props.AssemblyName }" ^
  "if ([string]::IsNullOrWhiteSpace($packageId)) { $packageId = [System.IO.Path]::GetFileNameWithoutExtension($projectFile) }" ^
  "$packageVersion = $props.PackageVersion;" ^
  "if ([string]::IsNullOrWhiteSpace($packageVersion)) { $packageVersion = $props.Version }" ^
  "if (-not $packageVersion) { throw 'No PackageVersion or Version found in project file' }" ^
  "Write-Output ($packageId.Trim() + '|' + $packageVersion.Trim())"`) DO (
  SET "%~2=%%A"
  SET "%~3=%%B"
)

IF NOT DEFINED %~2 (
  ECHO Failed to read project metadata from "%~1"
  EXIT /B 1
)

IF NOT DEFINED %~3 (
  ECHO Failed to read package version from "%~1"
  EXIT /B 1
)

EXIT /B 0

:Usage
ECHO.
ECHO Provide a NuGet API key
ECHO Example: publish-nuget.bat YOUR_NUGET_API_KEY
GOTO :Done

:Error
ECHO.
ECHO NuGet publish failed
EXIT /B 1

:Done
ECHO.
ENDLOCAL
@ECHO ON
