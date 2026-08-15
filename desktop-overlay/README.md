# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id>
```

默认窗口为 `960×480`、无边框、透明、置顶，不显示在任务栏。鱼塘场景、钓位和自己的猫咪（`128×128`）由 Unity 推送的 `pond_snapshot` 字段驱动。

可替换资源（可选，放在 exe 旁 `OverlayResources/`）：

- `pond.png`：鱼塘背景
- `cat.png`：自己的猫咪，显示为 `128×128`

未提供时使用占位水面/岸线和矢量猫。

## IPC

协议为 JSON Lines，`version` 当前为 `1`，状态必须带递增 `sequence`：

```json
{"type":"state","version":1,"sequence":12,"loginState":"Authenticated","connectionState":"Connected","pondName":"静水湾","pondId":"pond-calm","fishingPhase":"waiting","petVisualState":"fishing","ownSpotId":"calm-spot-1","ownX":240,"ownY":400,"hasOwnPosition":true,"spots":[{"id":"calm-spot-1","x":240,"y":400}],"users":[{"playerId":"p2","nickname":"同塘玩家","spotId":"calm-spot-2","x":400,"y":360,"hasPosition":true,"petVisualState":"idle"}]}
```

Overlay 只渲染 Unity 推送的字段，不连接 Socket，不推断第二套状态机。`users` 为同塘其他玩家，按 `playerId` 复用，快照全量覆盖。IPC 不传贴图。

Overlay 回传命令：

```json
{"type":"command","version":1,"command":"open_main"}
```

支持命令：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`
- `menu_pond` / `menu_friends` / `menu_catch` / `menu_gallery` / `menu_settings`
- `hide_to_tray`
- `quit_app`

Overlay 收到旧 `sequence` 时丢弃，不覆盖当前状态。打开主窗口不得销毁鱼塘会话。

## 构建

需要 Windows/.NET Framework 4.8 和 WPF 构建工具：

```text
msbuild desktop-overlay/FishSocialOverlay.csproj /p:Configuration=Release
```

将输出的 `FishSocialOverlay.exe` 放到 Unity Player 同目录、`FishSocialOverlay/` 或 `Overlay/` 子目录中。
