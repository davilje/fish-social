@echo off
setlocal
cd /d "%~dp0"

REM One-click ops portal:
REM   1) Ensure game server :3001 is up
REM   2) Optionally ensure Expo Web :8082 (set OPS_START_WEB=0 to skip)
REM   3) Open http://localhost:3001/ops/
REM Playing the game client still prefers full stack via dev.bat.

where node >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 node，请先安装 Node.js。
  pause
  exit /b 1
)

set "ADMIN_SECRET=fish-social-debug"
if exist ".env" (
  for /f "usebackq tokens=1,* delims==" %%A in (".env") do (
    if /I "%%A"=="ADMIN_SECRET" set "ADMIN_SECRET=%%B"
  )
)

if not defined OPS_START_WEB set "OPS_START_WEB=1"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ProgressPreference='SilentlyContinue';" ^
  "try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:3001/health' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {}" ^
  "exit 1"
if errorlevel 1 (
  echo [启动] 游戏服未运行，正在后台启动 npm run server ...
  start "fish-social-server" /MIN cmd /c "set NODE_ENV=development&& set AUTH_DISABLED=1&& set ADMIN_SECRET=%ADMIN_SECRET%&& set PORT=3001&& npm run server"
  echo [等待] 等待服务就绪...
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "for ($i=0; $i -lt 60; $i++) { try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:3001/health' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { Write-Host 'OK'; exit 0 } } catch {} Start-Sleep -Seconds 1 }; Write-Host 'TIMEOUT'; exit 1"
  if errorlevel 1 (
    echo [错误] 60 秒内未能启动服务，请手动检查终端窗口 fish-social-server。
    pause
    exit /b 1
  )
) else (
  echo [OK] 游戏服已在运行
)

REM --- Game Web :8082 ---
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ProgressPreference='SilentlyContinue';" ^
  "try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8082/' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) { exit 0 } } catch {}" ^
  "exit 1"
if errorlevel 1 (
  if /I "%OPS_START_WEB%"=="0" (
    echo [提示] 游戏 Web :8082 未运行。运维页可照常使用；玩客户端请运行 dev.bat 或 npm run web。
  ) else (
    echo [启动] 游戏 Web 未运行，正在后台启动 npm run web ...
    start "fish-social-web" /MIN cmd /c "npm run web"
    echo [等待] 等待 :8082 就绪（最多 60 秒）...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "for ($i=0; $i -lt 60; $i++) { try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8082/' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) { Write-Host 'OK'; exit 0 } } catch {} Start-Sleep -Seconds 1 }; Write-Host 'TIMEOUT'; exit 1"
    if errorlevel 1 (
      echo [警告] 60 秒内 :8082 未就绪，运营平台仍可打开；请稍后手动 npm run web 或用 dev.bat。
    ) else (
      echo [OK] 游戏 Web 已在运行
    )
  )
) else (
  echo [OK] 游戏 Web 已在运行
)

start "" "http://localhost:3001/ops/"
echo 已打开运营平台。可关闭本窗口；请保留最小化的 fish-social-server / fish-social-web 窗口。
timeout /t 3 >nul
exit /b 0
