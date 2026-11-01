@echo off
setlocal
cd /d "%~dp0"
chcp 65001 >nul
where py >nul 2>&1
if not errorlevel 1 (
  py -3 scripts\start_preview.py
) else (
  where python >nul 2>&1
  if errorlevel 1 (
    echo [错误] 未找到 Python 3。请先安装 Python 3。
    pause
    exit /b 1
  )
  python scripts\start_preview.py
)
if errorlevel 1 pause
