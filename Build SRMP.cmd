@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-SRMP.ps1" -Install -Package %*
set "EXITCODE=%ERRORLEVEL%"

echo.
if not "%EXITCODE%"=="0" (
    echo SRMP build/install failed with exit code %EXITCODE%.
) else (
    echo SRMP built, installed into SRML\Mods, and packaged successfully.
)
pause
exit /b %EXITCODE%
