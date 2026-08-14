# Steam 原生桌面宠物 Overlay

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 原生桌面宠物 Overlay |
| 编号 | **STEAM-DESKTOP-07G** |
| 类型 | Windows 原生客户端 |
| 负责人 | Windows 客户端工程师 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-14** |
| 上位需求 | `STEAM-DESKTOP-07`、`STEAM-DESKTOP-07F` |

## 1. 背景与目标

当前桌面宠物表现应先稳定在 Unity 主窗口内。透明桌面助手不能通过启动第二个 Unity Player 实现，因为第二个 Unity 进程会重复加载场景、Camera、渲染循环和原生窗口插件，可能造成全屏、Skybox、主窗口被覆盖、窗口样式损坏及 CPU/内存异常。

07G 将 Overlay 改为独立的轻量 Windows 原生程序。它只显示宠物和状态，通过 Named Pipe 与 Unity 主程序通信，不承载 Steam、Socket.IO、鱼塘、库存或钓鱼权威逻辑。

## 2. 功能范围

- 创建独立的 `FishSocialOverlay.exe` 原生 Windows 程序。
- 默认窗口尺寸为 `960×480`。
- 无边框、透明、置顶，可拖动。
- 只对宠物图像区域进行命中测试，透明区域允许点击穿透桌面。
- 显示鱼塘场景、自己的猫咪和同塘其他用户猫咪；每只猫咪基准尺寸为 `128×128`。
- 显示登录状态、鱼塘名称和钓鱼状态等 Unity 推送的状态。
- 右键显示菜单，支持打开主窗口、隐藏 Overlay 和退出 Overlay。
- 支持打开主窗口、隐藏 Overlay 和退出 Overlay。
- Unity 主程序负责启动、关闭和监控 Overlay。
- 通过 Named Pipe 传输状态和命令。

## 3. 技术边界

- Unity 主程序继续负责 Steam 登录、JWT、Socket.IO、鱼塘会话、库存、通知和权威状态。
- Overlay 不引用 Unity、Steamworks.NET、UniWindowController 或鱼塘业务程序集。
- Overlay 不加载 Unity 场景，不创建 Unity Camera，不运行 Unity 渲染循环。
- Overlay 进程异常退出、管道断开或启动失败不得阻塞 Unity 主线程。
- 不新增一塘一进程；全局最多一个 Overlay 实例。
- 不将访问令牌、库存或敏感数据写入 Overlay 文件或日志。

## 4. IPC 协议

Unity → Overlay：

```json
{
  "type": "state",
  "loginState": "Authenticated",
  "connectionState": "Connected",
  "pondName": "pond-calm",
  "fishingPhase": "waiting",
  "sequence": 12
}
```

Overlay → Unity：

```json
{
  "type": "command",
  "command": "open_main"
}
```

最低命令集：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`

## 5. 非目标

- 不在 Overlay 中实现 Steam 登录。
- 不在 Overlay 中连接 REST 或 Socket.IO。
- 不在 Overlay 中实现鱼塘会话、多人玩家业务或钓鱼状态机；Overlay 只渲染 Unity 推送的场景与状态。
- 不在 07G 中引入正式猫咪美术、Spine 或复杂换装。
- 不修改 Node、mobile 或 shared 的业务协议。

## 6. 验收标准

- [x] Overlay 是独立原生进程，不加载 Unity Player。
- [x] 启动后窗口为 `960×480`、无边框、透明、置顶。
- [x] Overlay 不显示 Skybox、场景背景或 Unity 启动画面。
- [x] Overlay 可拖动，主窗口仍可正常点击、缩放和拖动。
- [x] Overlay CPU 占用稳定，不因空闲渲染持续升高。
- [x] Overlay 关闭后 Unity 主程序不崩溃、不阻塞、不改变窗口模式。
- [x] 管道断开和 Overlay 异常退出不会卡死 Unity 主线程。
- [x] 状态序列号可防止旧状态覆盖新状态。
- [x] Overlay 可渲染鱼塘场景、自己的猫咪和同塘玩家猫咪，猫咪基准尺寸为 `128×128`。
- [x] Overlay 右键菜单可打开主窗口，且不触发离塘。
- [x] Windows Release Build 通过主窗口、鱼塘、Overlay 启停和退出测试；退出不再假死，也不再弹出 CMD。

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-14 | 主 Agent | 新增 07G，替代第二 Unity Player + UniWindowController 的透明 Overlay 方案 |
| 2026-08-15 | 主 Agent | 用户验收通过：原生 Overlay、Named Pipe、退出生命周期与无窗口 taskkill 兜底已落地；状态改为已实现 |
