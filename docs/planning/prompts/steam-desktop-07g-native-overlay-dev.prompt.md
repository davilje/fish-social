# STEAM-DESKTOP-07G：原生桌面宠物 Overlay 开发提示词

## 任务

实现 `STEAM-DESKTOP-07G`：使用独立的轻量 Windows 原生程序承载桌面宠物 Overlay，替代第二个 Unity Player + `UniWindowController` 的方案。

## 必读文档

- `docs/planning/specs/Steam原生桌面宠物Overlay.md`
- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`

## 强制架构约束

- Overlay 不得使用 Unity。
- Overlay 不得引用 Steamworks.NET、Socket.IO、鱼塘业务程序集或 Unity 场景。
- Unity 主程序仍是 Steam、REST、Socket.IO、鱼塘和库存状态的唯一权威。
- Unity 只通过 Named Pipe 向 Overlay 推送状态，Overlay 只回传 UI 命令。
- Overlay 只能有一个实例。
- Overlay 启停、崩溃、管道断开不得阻塞 Unity 主线程。
- 不得修改 `mobile/`、`server/`、`shared/` 的业务逻辑。

## 实现要求

### 原生窗口

- 新增独立 Windows 原生项目，推荐 WPF；如仓库已有 Windows 客户端基础设施，可复用但不得引入 Unity Runtime。
- 输出 `FishSocialOverlay.exe`。
- 窗口默认 `960×480`。
- `WindowStyle=None`、透明背景、置顶。
- 宠物区域支持拖动。
- Overlay 内容区域支持鱼塘场景、自己的猫咪和同塘其他用户猫咪。
- 每只猫咪的显示基准尺寸为 `128×128`，资源保持等比例。
- Overlay 右键打开菜单，至少提供“打开主窗口”“隐藏 Overlay”“退出”。
- 透明区域不拦截桌面点击。
- 不显示 Unity Skybox、Unity Splash 或默认场景。
- Overlay 空闲时使用有限刷新频率，禁止无限循环高频刷新。

### IPC

实现有版本和序列号的 JSON Lines Named Pipe 协议。

Unity → Overlay 状态至少包含：

```json
{
  "type": "state",
  "version": 1,
  "sequence": 1,
  "loginState": "Authenticated",
  "connectionState": "Connected",
  "pondName": "pond-calm",
  "fishingPhase": "waiting"
}
```

Overlay → Unity 命令至少包含：

```json
{
  "type": "command",
  "version": 1,
  "command": "open_main"
}
```

实现以下命令：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`

收到旧序列号状态时必须丢弃，不能覆盖新状态。

### Unity 侧

- 将当前 Overlay 启动逻辑替换为原生 Overlay 进程控制器。
- 不得在进入鱼塘时切换 Unity 主窗口到全屏、透明或置顶。
- 进程启动失败只显示可恢复错误，不得卡住登录、鱼塘和主窗口。
- Unity 主窗口关闭时可靠关闭 Overlay。
- Overlay 退出后可重新启动，不能产生进程或管道泄漏。

## 验收流程

1. 只启动 Unity 主程序，确认普通窗口可缩放、可拖动。
2. 登录并进入鱼塘，确认主窗口仍可操作。
3. 启动 Overlay，确认出现 `960×480` 透明无边框窗口。
4. 确认 Overlay 显示鱼塘场景和猫咪，且不显示 Unity Skybox 或 Unity 默认场景背景。
5. 拖动宠物，确认 Overlay 移动而 Unity 主窗口不移动。
6. 点击 Overlay 的“打开主窗口”，确认 Unity 窗口获得焦点。
7. 右键 Overlay，确认菜单出现且“打开主窗口”不会触发 `leave_pond`。
8. 关闭 Overlay，确认 Unity 主程序不崩溃、不冻结。
9. 强制结束 Overlay，确认 Unity 主程序仍可继续运行。
10. 后端断开、重连和高频状态更新时，确认 CPU/内存稳定。

## 禁止事项

- 不要再次通过 `Process.Start` 启动同一个 Unity 可执行文件作为 Overlay。
- 不要在 Overlay 中创建 Unity Camera、Canvas 或 EventSystem。
- 不要使用 Unity 全屏 Player 伪装透明窗口。
- 不要把 Overlay 的窗口句柄交给 Unity 主窗口管理器处理。

## 交付物

- 原生 Overlay 项目源码；
- Unity 侧进程控制和 IPC 适配；
- 协议说明；
- Windows Development Build；
- 启停、拖动、窗口缩放、异常退出和性能测试记录。
