@echo off
setlocal
set "SCRIPT=%~dp0installer\Install-SRMP.ps1"

if not exist "%SCRIPT%" (
  echo [SRMP] Installer script is missing: %SCRIPT%
  pause
  exit /b 2
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"
echo.
if not "%EXITCODE%"=="0" (
  echo [SRMP] Installation failed. Read the error above.
) else (
  echo [SRMP] Installation complete.
)
pause
exit /b %EXITCODE%
