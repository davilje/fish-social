# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id> [--width=960] [--height=560]
```

默认窗口为 `960×560`、无边框、透明、置顶，不显示在任务栏。合成透明处点击穿透到桌面；猫、座位、HUD 与可见塘图仍命中 Overlay。鱼塘场景、钓位和塘内宠物由 Unity 推送的 `pond_snapshot` 字段驱动。宠物**显示** `64×64`（`BodySize` / `CatSize`）；序列帧**源图**正方形 `256×256`，`Stretch.Uniform` 缩进显示槽。鼠标悬停热区仅为这 `64×64` 猫身（不含昵称条），时长浮窗约 `80×28`，水平居中对齐猫身。

可替换资源（放在 **exe 同目录** 的 `OverlayResources/`）：

- `pond.png`：旧的全塘共用底图（建议 `960×560`）
- `ponds/<pondId>.png`：分塘底图（ART-03）
- `layouts/<pondId>.json`：**ART-02 场景布局**（钓位/装饰像素表）。有表则停用 `MapToScene`
- `seats/_default.png`：座位椅图回退（14A；优先用布局里 `actor-seat` / spot 的 `sprite`）
- `pets/<petId>/<state>-0.png`：**按猫种分套**（推荐，与 Unity 主窗口同名）
- `hud/overlay-hud.json`：HUD 控件位置（ART-03）
- `status/fishing.png`、`status/hooked.png`：宠物相位小图标（14D，缺图用矢量占位）
- `status/hook-ring.png`：上钩进度环占位（Unity Radial 360）
- `cat.png`：旧的全局回退（全塘同一只，不要再当正式资源）

`petId` 与头像一致：`orange` / `calico` / `gray` / `siamese` / `tuxedo` / `white`。  
`state`：`idle` / `fishing` / `hooked` / `catching` / `dragging` / `offline`。

未提供对应文件时用占位水面/岸线和矢量猫。完整说明见 [OverlayResources/pets/README.md](OverlayResources/pets/README.md)。

### 自己换猫咪（按猫种，和主窗口同一形象）

每种猫一套姿势，塘里每个玩家用自己头像对应的那套。**不要**再只放一张根目录 `cat.png`。

1. 把钓鱼橘猫存成：

```text
desktop-overlay/OverlayResources/pets/orange/fishing-0.png
```

其它猫种同样放到 `pets/calico/`、`pets/gray/` 等。

2. Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay**，或重新打 Debug 包。这会拷到：
   - `FishSocialOverlay.exe` 旁的 `OverlayResources/pets/`
   - Unity `StreamingAssets/Pet/`（主窗口）

3. 完全退出游戏再开。头像是橘猫的玩家，钓鱼时显示 `fishing-0.png`。

正方形透明 PNG，**源图 `256×256`**（等比例亦可）。Overlay 显示槽固定 `64×64`。图缓存在内存里，换文件必须重启 Overlay。

## IPC

协议为 JSON Lines，`version` 当前为 `1`，状态必须带递增 `sequence`：

```json
{"type":"state","version":1,"sequence":12,"loginState":"Authenticated","connectionState":"Connected","pondName":"静水湾","pondId":"pond-calm","fishingPhase":"waiting","petVisualState":"fishing","ownNickname":"我","sessionFishingMs":120000,"hookDeadlineMs":0,"ownSpotId":"calm-spot-1","ownX":240,"ownY":400,"hasOwnPosition":true,"mainWindowRaised":false,"spots":[{"id":"calm-spot-1","x":240,"y":400}],"users":[{"playerId":"p2","nickname":"同塘玩家","spotId":"calm-spot-2","x":400,"y":360,"hasPosition":true,"petVisualState":"idle","fishingPhase":"idle","sessionFishingMs":0,"hookDeadlineMs":0}]}
```

Overlay 只渲染 Unity 推送的字段，不连接 Socket，不推断第二套状态机。`users` 为同塘其他玩家，按 `playerId` 复用，快照全量覆盖。IPC 不传贴图或 Prefab。有本地 `layouts/<pondId>.json` 时，猫和钓位使用表内像素，不再按 `spots[].x/y` 自动缩放。`mainWindowRaised=true` 时 Overlay 取消置顶，让 Unity 主窗口盖在上面；主窗口隐藏到托盘后恢复置顶。

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
