@echo off
setlocal
cd /d "%~dp0"

where python >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 python，请安装 Python 3 并加入 PATH。
  pause
  exit /b 1
)

python scripts\start_dev.py --wait-key %*
set EXITCODE=%errorlevel%
exit /b %EXITCODE%
