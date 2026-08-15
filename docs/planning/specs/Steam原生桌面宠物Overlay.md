# Steam 原生桌面宠物 Overlay

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 原生桌面宠物 Overlay |
| 编号 | **STEAM-DESKTOP-07G** |
| 类型 | Unity 功能开发 |
| 负责人 | Windows 客户端工程师 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-14** |
| 上位需求 | `STEAM-DESKTOP-07`、`STEAM-DESKTOP-07F` |

## 1. 背景与目标

当前桌面宠物表现应先稳定在 Unity 主窗口内。透明桌面助手不能通过启动第二个 Unity Player 实现，因为第二个 Unity 进程会重复加载场景、Camera、渲染循环和原生窗口插件，可能造成全屏、Skybox、主窗口被覆盖、窗口样式损坏及 CPU/内存异常。

07G 将 Overlay 改为独立的轻量 Windows 原生程序。它只显示宠物和状态，通过 Named Pipe 与 Unity 主程序通信，不承载 Steam、Socket.IO、鱼塘、库存或钓鱼权威逻辑。

## 2. 功能范围

- 创建独立的 `FishSocialOverlay.exe` 原生 Windows 程序。
- 默认窗口尺寸为 `960×480`（07B 起的正式鱼塘 Overlay；早期占位可为更小窗口）。
- 无边框、透明、置顶，可拖动。
- 只对场景/宠物区域进行命中测试，透明区域允许点击穿透桌面。
- 渲染 Unity 推送的鱼塘场景、钓位、自己的猫和（07C）同塘玩家；猫咪基准 `128×128`。
- **序列帧在 Overlay 本地播放：** 按 `petVisualState` 切本地帧，Named Pipe **不传图片或逐帧数据**。
- 显示登录状态、鱼塘名称、钓位和钓鱼状态。
- 支持打开主窗口、隐藏 Overlay 和退出 Overlay。
- Unity 主程序负责启动、关闭、监控 Overlay，以及唯一的 Socket 连接。
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
  "pondName": "静水湾",
  "pondId": "pond-calm",
  "fishingPhase": "waiting",
  "petVisualState": "fishing",
  "ownSpotId": "calm-spot-1",
  "hasOwnPosition": true,
  "ownX": 240,
  "ownY": 400,
  "spots": [{"id": "calm-spot-1", "x": 240, "y": 400}],
  "sequence": 12
}
```

`petVisualState` 取值：`idle`、`fishing`、`hooked`、`catching`、`dragging`、`offline`。Overlay 据此选本地序列帧，不推断第二套状态机。07C 可在同一 `state` 消息中增加同塘用户数组（`playerId`、昵称、钓位、坐标、`petVisualState`），仍不传贴图。

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
- 不在 Overlay 中自行推断鱼塘权威、伪造玩家或维护第二套钓鱼状态机；只渲染 Unity 推送的场景与 `petVisualState`。
- 不通过 IPC 传输序列帧贴图或逐帧像素。
- 不在 07G 中引入正式猫咪美术、Spine 或复杂换装。
- 不修改 Node、mobile 或 shared 的业务协议。

## 6. 验收标准

- [ ] Overlay 是独立原生进程，不加载 Unity Player。
- [ ] 启动后窗口为 `480×420`、无边框、透明、置顶。
- [ ] Overlay 不显示 Skybox、场景背景或 Unity 启动画面。
- [ ] Overlay 可拖动，主窗口仍可正常点击、缩放和拖动。
- [ ] Overlay CPU 占用稳定，不因空闲渲染持续升高。
- [ ] Overlay 关闭后 Unity 主程序不崩溃、不阻塞、不改变窗口模式。
- [ ] 管道断开和 Overlay 异常退出不会卡死 Unity 主线程。
- [ ] 状态序列号可防止旧状态覆盖新状态。
- [ ] Windows Development Build 通过主窗口、鱼塘、Overlay 启停和退出测试。

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-15 | 主 Agent | 明确 Overlay 渲染 Unity 推送的鱼塘/宠物；序列帧本地播放；IPC 只传 `petVisualState` 与位置，不传图、不连 Socket |
| 2026-08-14 | 主 Agent | 新增 07G，替代第二 Unity Player + UniWindowController 的透明 Overlay 方案 |
