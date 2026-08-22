@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "PROJECT=%~dp0fish-social-unity"
set "LOG=%PROJECT%\Logs\build-win64-debug.log"
set "UNITY="

if defined UNITY_EDITOR if exist "%UNITY_EDITOR%" set "UNITY=%UNITY_EDITOR%"
if not defined UNITY if exist "%~dp0unity-editor-path.txt" (
  set /p UNITY=<"%~dp0unity-editor-path.txt"
)

if not defined UNITY (
  for /d %%D in ("C:\Program Files\Unity\Hub\Editor\*") do (
    if exist "%%~D\Editor\Unity.exe" set "UNITY=%%~D\Editor\Unity.exe"
  )
)
if not defined UNITY (
  for /d %%D in ("C:\Program Files\Unity\Hub\Editor\2021.3*") do (
    if exist "%%~D\Editor\Unity.exe" set "UNITY=%%~D\Editor\Unity.exe"
  )
)

if not defined UNITY (
  echo [错误] 找不到 Unity.exe。
  echo 可设置环境变量 UNITY_EDITOR，或在仓库根目录放 unity-editor-path.txt（一行完整路径）。
  pause
  exit /b 1
)

if not exist "%PROJECT%\Assets" (
  echo [错误] 找不到 Unity 工程：%PROJECT%
  pause
  exit /b 1
)

if not exist "%PROJECT%\Logs" mkdir "%PROJECT%\Logs"

echo 使用 Unity：%UNITY%
echo 工程：%PROJECT%
echo 日志：%LOG%
echo 正在一键打包 Debug 包（Development + Overlay）...
echo.

"%UNITY%" -quit -batchmode -nographics ^
  -projectPath "%PROJECT%" ^
  -executeMethod FishSocial.Desktop.Editor.DesktopBuildMenu.BuildWindowsDevelopment ^
  -logFile "%LOG%"
set EXITCODE=%errorlevel%

if not "%EXITCODE%"=="0" (
  echo.
  echo [失败] 退出码 %EXITCODE%，请查看日志：
  echo %LOG%
  pause
  exit /b %EXITCODE%
)

echo.
echo [完成] Debug 包：
echo %PROJECT%\Builds\Windows64-Debug\FishSocialDesktop.exe
echo 该包带 DEVELOPMENT_BUILD，Overlay 右上有 Debug 菜单，F8 也可呼出。
pause
exit /b 0
