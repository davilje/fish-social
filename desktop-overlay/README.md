# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。


## 性能优化第一批（2026-10-23）

角色离场与关窗统一停止定时器、清理引用；动画图层按实际路径共享，64 MiB 像素载荷预算的 LRU 保留热帧，活跃资源受保护。预热先应用正式尺寸，新角色不再清除全场缓存。状态日志新增 actors、activeClips、cachedFrames、cachePixelBytes、layerDecodes。

缓存行为 8/8、静态回归 39/39，正式 WPF 与原生测试交叉编译通过；Windows 原生运行与实际性能待验收。见 [实现与验收](../docs/engineering/2026-10-23-overlay-performance-batch1.md)。

## 手账 Overlay 收口（2026-10-23）

已换成桌面手账同源按钮、纸张、输入底板、图标和头像；取消按钮回弹，气泡改为 120ms 淡入淡出。移除常驻状态栏和正常鱼塘钓获／回鱼／脱钩弹窗，角色钓鱼动画及真实结算继续运行。需重建 EXE 并同步全部 OverlayResources。

资源同步：从仓库根运行 `python3 scripts/art/sync-overlay-journal-assets.py`。完整参数、导出和 Windows 待验收项见 [交接说明](COZY_UI_HANDOFF.md)。

## 钓鱼动画九层包 v2（2026-10-13）

优先读取 `OverlayResources/fishing/<petId>/manifest.json`，角色四层和公共道具五层按每帧顺序、时长叠加；源画布 640×512，不是下面旧平面回退的 256×256。新增空军与两种复位，`reel_empty` 为新增展示状态。完整说明见 `../docs/art/animations/2026-10-13-fishing-animation-port.md`。旧 EXE 不兼容九层资源，必须重新构建并分发最新资源；其他原有猫种保持平面回退。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id> [--width=960] [--height=560]
```

默认窗口为 `960×560`、无边框、透明、置顶，不显示在任务栏。合成透明处点击穿透到桌面；猫、座位、HUD 与可见塘图仍命中 Overlay。鱼塘场景、钓位和塘内宠物由 Unity 推送的 `pond_snapshot` 字段驱动。宠物**显示** `64×64`（`BodySize` / `CatSize`）；序列帧**源图**正方形 `256×256`，`Stretch.Uniform` 缩进显示槽。鼠标悬停热区仅为这 `64×64` 猫身（不含昵称条），时长浮窗约 `80×28`，水平居中对齐猫身。

可替换资源（放在 **exe 同目录** 的 `OverlayResources/`）：

- `pond.png`：旧的全塘共用底图（建议 `960×560`）
- `ponds/<pondId>.png`：分塘底图（ART-03）
- `layouts/<pondId>.json`：**ART-02 场景布局**。平移与底图宽度用 **`pond.width`**（通常来自 `ponds/<pondId>.png` 像素宽）；`canvas` 仍是 Prefab 根画布。宽于 960 时显示左右箭头。
- `seats/_default.png`：座位椅图回退（14A；优先用布局里 `actor-seat` / spot 的 `sprite`）
- `fishing/<petId>/manifest.json`：当前九种猫咪的分层动作包。
- `fishing/<petId>/body/<clip>/<frame>/`：body、head、far_hand、hands。
- `fishing/shared/<clip>/<frame>/`：rod、line、fish、bobber、bait。
- `hud/cozy/characters/avatars/<petId>.png`：当前头像及缺动画时的回退图片。
- `hud/overlay-hud.json`：HUD 控件位置。
- `status/hooked.png`、`status/groundbait.png`：咬钩、打窝头顶图标。

`petId` 支持 orange、calico、silver、brown、cream、blue、black、white、cow。历史 gray、siamese、tuxedo 统一兼容为默认橘猫。旧 pets 目录和几何占位猫已移除。

16 个片段与 Unity 使用相同的 640×512 画布、逐帧层级和时长。使用前检查完整九层；缺图时显示当前猫咪头像，不拼接旧动画。

Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay** 可同步当前分层包。资源缓存在内存中，更新后需重启 Overlay。完整路径及资源生产流程见 [猫咪资源调用说明](../docs/art/cat-skins/resource-routing.md)。

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

状态可附带 `recentChats[]`（`messageId` / `playerId` / `nickname` / `text` / `sentAtMs`）与 `ownPlayerId`。新消息在对应宠物头顶弹出气泡（120ms 淡入，停留 5s 后以 120ms 淡出）；左下为可收缩聊天窗（收起仅显示最新一条，展开可输入）。完整聊天与私聊仍在 Unity 主窗口。

Overlay 收到旧 `sequence` 时丢弃，不覆盖当前状态。打开主窗口不得销毁鱼塘会话。

## 构建

需要 Windows/.NET Framework 4.8 和 WPF 构建工具：

```text
msbuild desktop-overlay/FishSocialOverlay.csproj /p:Configuration=Release
```

将输出的 `FishSocialOverlay.exe` 放到 Unity Player 同目录、`FishSocialOverlay/` 或 `Overlay/` 子目录中。
