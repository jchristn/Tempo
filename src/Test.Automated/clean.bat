@echo off
REM Cleanup script for Test.Automated
REM Deletes leftover test database files and log directories

echo Cleaning up test artifacts...
echo.

REM Delete test database files
set db_count=0
for %%f in (metrics_*.db) do (
    del /Q "%%f" 2>nul
    if not errorlevel 1 (
        set /a db_count+=1
    )
)

if %db_count% GTR 0 (
    echo Deleted %db_count% test database file^(s^)
) else (
    echo No test database files found
)

REM Delete test log directories
set log_count=0
for /D %%d in (test_logs_*) do (
    rd /S /Q "%%d" 2>nul
    if not errorlevel 1 (
        set /a log_count+=1
    )
)

if %log_count% GTR 0 (
    echo Deleted %log_count% test log director^(ies^)
) else (
    echo No test log directories found
)

REM Delete test request directories
set req_count=0
for /D %%d in (test_requests_*) do (
    rd /S /Q "%%d" 2>nul
    if not errorlevel 1 (
        set /a req_count+=1
    )
)

if %req_count% GTR 0 (
    echo Deleted %req_count% test request director^(ies^)
) else (
    echo No test request directories found
)

echo.
echo Cleanup complete!
