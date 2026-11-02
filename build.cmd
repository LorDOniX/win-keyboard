@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set "buildExitCode=%ERRORLEVEL%"
echo.
if "%buildExitCode%"=="0" (
    echo Build OK: "%~dp0dist\CeskaKlavesnice.exe"
) else (
    echo Build FAILED. Exit code: %buildExitCode%
)
if /I not "%~1"=="--no-pause" pause
exit /b %buildExitCode%
