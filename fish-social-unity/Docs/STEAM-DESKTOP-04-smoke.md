# STEAM-DESKTOP-04 Windows 桌面壳冒烟清单

构建产物：`fish-social-unity/Builds/Windows64/FishSocialDesktop.exe`  
环境：Windows 10/11，可不装 Unity Editor。

| # | 步骤 | 期望 | 结果 |
|---|------|------|------|
| 1 | 启动 Development Build | 进入主界面，无红错 | PASS |
| 2 | 左侧进入鱼塘 / 好友 / 鱼获 / 设置并返回主页 | 可进出，缩放窗口不重叠 | PASS |
| 3 | 设置中切换「普通窗口 / 无边框 / 全屏」 | 三种模式可切换且可来回恢复 | PASS |
| 4 | 保存设置后重启客户端 | 窗口模式与通知开关恢复 | PASS |
| 5 | 点击窗口关闭 | 隐藏到托盘，进程仍在 | PASS |
| 6 | 托盘「显示窗口」 | 窗口恢复 | PASS |
| 7 | 托盘「退出游戏」 | 进程真正退出 | PASS |
| 8 | 设置页触发模拟鱼咬钩 / 好友邀请 / 连接错误 | 启用时显示单条右上角提示 | PASS |
| 9 | 关闭「启用通知」或单项通知后再模拟 | 对应模拟事件完全静默 | PASS |
| 10 | 隐藏托盘期间观察 CPU | 帧率降至后台档（约 5 FPS），无持续高频渲染 | PASS |

编辑器快捷键（无托盘时）：`F9` 显示 · `F10` 隐藏 · `F12` 退出。

验收日期：2026-08-12；平台：Windows 10；构建：Windows Development Build。

构建命令示例：

```bat
"C:\Program Files\Unity\Hub\Editor\2021.3.29f1c1\Editor\Unity.exe" ^
  -quit -batchmode -nographics ^
  -projectPath "C:\Users\Administrator\Projects\fish-social\fish-social-unity" ^
  -executeMethod FishSocial.Desktop.Editor.DesktopBuildMenu.BuildWindowsDevelopment ^
  -logFile "C:\Users\Administrator\Projects\fish-social\fish-social-unity\Logs\build-win64.log"
```
