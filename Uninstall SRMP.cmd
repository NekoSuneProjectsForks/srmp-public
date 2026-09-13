@echo off
setlocal
set "SCRIPT=%~dp0installer\Uninstall-SRMP.ps1"

if not exist "%SCRIPT%" (
  echo [SRMP] Uninstaller script is missing: %SCRIPT%
  pause
  exit /b 2
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"
echo.
if not "%EXITCODE%"=="0" (
  echo [SRMP] Uninstall failed. Read the error above.
) else (
  echo [SRMP] Uninstall complete.
)
pause
exit /b %EXITCODE%
