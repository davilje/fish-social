@echo off
setlocal
cd /d "%~dp0"
where powershell >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 powershell。
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\ops\check-home-public-server.ps1"
echo.
pause
exit /b %errorlevel%
