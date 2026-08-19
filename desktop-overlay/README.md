# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id> [--width=960] [--height=560]
```

默认窗口为 `960×560`、无边框、透明、置顶，不显示在任务栏。鱼塘场景、钓位和塘内宠物（`64×64`）由 Unity 推送的 `pond_snapshot` 字段驱动。

可替换资源（可选，放在 exe 旁 `OverlayResources/`）：

- `pond.png`：鱼塘背景
- `cat.png`：猫咪占位图，Overlay 显示为 `64×64`（序列帧仍可从 128 资源缩放）

未提供时使用占位水面/岸线和矢量猫。

## IPC

协议为 JSON Lines，`version` 当前为 `1`，状态必须带递增 `sequence`：

```json
{"type":"state","version":1,"sequence":12,"loginState":"Authenticated","connectionState":"Connected","pondName":"静水湾","pondId":"pond-calm","fishingPhase":"waiting","petVisualState":"fishing","ownNickname":"我","sessionFishingMs":120000,"hookDeadlineMs":0,"ownSpotId":"calm-spot-1","ownX":240,"ownY":400,"hasOwnPosition":true,"mainWindowRaised":false,"spots":[{"id":"calm-spot-1","x":240,"y":400}],"users":[{"playerId":"p2","nickname":"同塘玩家","spotId":"calm-spot-2","x":400,"y":360,"hasPosition":true,"petVisualState":"idle","fishingPhase":"idle","sessionFishingMs":0,"hookDeadlineMs":0}]}
```

Overlay 只渲染 Unity 推送的字段，不连接 Socket，不推断第二套状态机。`users` 为同塘其他玩家，按 `playerId` 复用，快照全量覆盖。IPC 不传贴图。`mainWindowRaised=true` 时 Overlay 取消置顶，让 Unity 主窗口盖在上面；主窗口隐藏到托盘后恢复置顶。

Overlay 回传命令：

```json
{"type":"command","version":1,"command":"open_main"}
```

支持命令：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`
- `menu_pond` / `menu_map` / `menu_shop` / `menu_friends` / `menu_catch` / `menu_gallery` / `menu_profile` / `menu_settings`
- `player_open_profile` / `player_add_friend` / `player_open_dm` / `player_like_recent`（需带 `playerId` + `commandId`）
- `send_pond_chat`（需带 `text`，trim 后 1–200 字；走 Unity Socket 发公屏，Overlay 不直连）
- `hide_to_tray`
- `quit_app`

状态可附带 `recentChats[]`（`messageId` / `playerId` / `nickname` / `text` / `sentAtMs`）与 `ownPlayerId`。新消息在对应宠物头顶弹出气泡（0.3s 缩放渐显，停留 5s 后渐隐）；左下为可收缩聊天窗（收起仅显示最新一条，展开可输入）。完整聊天与私聊仍在 Unity 主窗口。

Overlay 收到旧 `sequence` 时丢弃，不覆盖当前状态。打开主窗口不得销毁鱼塘会话。

## 构建

需要 Windows/.NET Framework 4.8 和 WPF 构建工具：

```text
msbuild desktop-overlay/FishSocialOverlay.csproj /p:Configuration=Release
```

将输出的 `FishSocialOverlay.exe` 放到 Unity Player 同目录、`FishSocialOverlay/` 或 `Overlay/` 子目录中。
