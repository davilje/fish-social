@echo off
setlocal
cd /d "%~dp0"
chcp 65001 >nul

REM One-click current preview. Static files only; does not start the game server.
REM Reuses 127.0.0.1:8769 when it is already serving this folder.

set "PORT=8769"
set "URL=http://127.0.0.1:%PORT%/preview-current.html"

where python >nul 2>&1
if errorlevel 1 (
  where py >nul 2>&1
  if errorlevel 1 (
    echo [错误] 未找到 Python。请先安装 Python，再双击本文件。
    pause
    exit /b 1
  )
  set "PY=py -3"
) else (
  set "PY=python"
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { $r = Invoke-WebRequest -Uri '%URL%' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {} ; exit 1"
if errorlevel 1 (
  echo [启动] 正在打开本地预览服务 %PORT% ...
  start "fish-social-preview" /MIN /D "%~dp0" cmd /c "%PY% -m http.server %PORT% --bind 127.0.0.1"
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "for ($i=0; $i -lt 20; $i++) { try { $r = Invoke-WebRequest -Uri '%URL%' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {} ; Start-Sleep -Milliseconds 400 }; exit 1"
  if errorlevel 1 (
    echo [错误] 预览服务没有在 %PORT% 端口就绪。请看最小化窗口 fish-social-preview。
    pause
    exit /b 1
  )
)

start "" "%URL%"
echo 已打开现行预览。可关闭本窗口；请保留最小化的 fish-social-preview 窗口。
ping 127.0.0.1 -n 3 >nul
exit /b 0
