@echo off
setlocal
rem Fish Social — Windows cloud / production foreground start
rem Usage: run from anywhere; switches to repo root (two levels up from scripts\ops)

cd /d "%~dp0..\.."
if not exist "package.json" (
  echo [ERROR] Cannot find repo root package.json
  exit /b 1
)

if not exist ".env" (
  echo [ERROR] Missing .env in repo root. Copy .env.example and fill production secrets.
  echo See docs\ops\windows-cloud-deploy.md
  exit /b 1
)

echo [FishSocial] build shared + start server on PORT from .env ^(default 3001^)
call npm run build:shared
if errorlevel 1 exit /b 1
call npm run server
exit /b %ERRORLEVEL%
