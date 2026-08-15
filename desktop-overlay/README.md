# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。**不连接** REST 或 Socket.IO。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id>
```

默认窗口为 `960×480`、无边框、透明、置顶，不显示在任务栏。鱼塘场景、钓位和猫咪（`128×128`）由 Unity 推送的快照字段驱动。

## 序列帧

Overlay **本地**按 `petVisualState` 播放序列帧，Named Pipe **不传图片**。可替换资源放在 exe 旁 `OverlayResources/`：

- `pond.png`：鱼塘背景
- `cat.png` 或 `cat-<state>-<n>.png`：自己的猫 / 状态帧（`idle`、`fishing`、`hooked`、`catching`、`dragging`、`offline`）

未提供时使用占位水面和矢量猫（可按状态变色）。

## IPC

协议为 JSON Lines，`version` 当前为 `1`，状态必须带递增 `sequence`：

```json
{"type":"state","version":1,"sequence":12,"loginState":"Authenticated","connectionState":"Connected","pondName":"静水湾","pondId":"pond-calm","fishingPhase":"waiting","petVisualState":"fishing","ownSpotId":"calm-spot-1","hasOwnPosition":true,"spots":[{"id":"calm-spot-1","x":240,"y":400}]}
```

Overlay 只渲染上述字段，不推断第二套状态机。07C 可在同一消息中增加同塘用户数组，仍不传贴图。

Overlay 回传命令：

```json
{"type":"command","version":1,"command":"open_main"}
```

支持命令：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`

Overlay 收到旧 `sequence` 时丢弃。打开主窗口不得销毁鱼塘会话。

## 构建

需要 Windows/.NET Core 3.1 和 WPF 构建工具。Unity 菜单 **Fish Social → Build Windows Release + Native Overlay** 会 `dotnet publish` 到 Player 输出目录。
