# 开发交接提示词：桌面端关闭后进程残留（BUG-21）

请修复 Unity Windows 桌面端关闭窗口后进程仍驻留的问题。

## 已确认规则

- 点击右上角关闭按钮应真正退出 `FishSocialDesktop.exe`。
- 托盘菜单中的“隐藏窗口”仍保留后台运行能力。
- 托盘菜单中的“退出游戏”必须清理托盘线程、图标和主进程。
- 禁止通过结束 Node.js、Unity Editor 或 Unity Hub 进程掩盖问题。
- 重复启动同一桌面端只允许一个实例运行。

## 实现要求

1. 检查并修复 `Application.wantsToQuit` 关闭拦截逻辑。
2. 使用 Windows 进程级互斥锁阻止重复实例。
3. 确保正常退出时 `SystemTrayService` 清理托盘线程和图标。
4. 增加 Windows Development Build 验收步骤。

## 验收

- 关闭 EXE 后进程列表中不再存在 `FishSocialDesktop.exe`。
- 对应 `UnityCrashHandler64.exe` 一并退出。
- 托盘显式隐藏仍能恢复窗口。
- 重复启动不会产生第二个桌面端进程。

