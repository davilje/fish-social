# 开发提示词：Overlay 换阶段卡帧与收杆重播（BUG-56）

你负责 Fish Social **Unity 桌面 + 猫咪分层贴图**。只做 **BUG-56**。  
不改 `mobile/`、`server/`、`shared/` 钓鱼规则。不要把提高帧率或重启进程当成修复。

## 必读

1. `docs/策划/规格/缺陷/BUG修复-Overlay阶段切换卡帧与收杆重播.md`（**已确认** / **BUG-56**）
2. 承接：`docs/策划/规格/缺陷/BUG修复-Unity自己钓鱼表现权威.md`（BUG-54，本地表现器保留）
3. 预估文件：
   - `fish-social-unity/Assets/Scripts/Desktop/Pet/FishingLayerArt.cs`
   - `fish-social-unity/Assets/Scripts/Desktop/Pet/SpriteFramePetRenderer.cs`
   - `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`（`FishingArtWarmupRoutine`、`FlushNativeOverlayState`）
   - `fish-social-unity/Assets/Scripts/Desktop/OwnFishingPresenter.cs`（`BeginStopFishing`、`ReconcileFromServer`、`Clear`）

## 方案要点

1. 预热和 `Open` 使用英雄猫真实槽位的 draw 宽高，不能只用 256×256。单帧解码 ≤ 50ms。`FlushNativeOverlayState` 里禁止同步读 PNG。操作帧先推 phase，贴图可下一帧补上。
2. 一次收杆只播一段。本地已是这次收杆产生的 `stopping` / `resolving` / `seated` 时，再来的 `stop_fishing` 忽略，直到新的 `start_fishing`。`Clear` 后不能退回去读服务端仍在钓鱼的旧 phase 再开播收杆。
3. 忽略时打日志：commandId、本地 phase、服务端 phase。
4. 有效操作仍发给服务器。ACK 失败回滚保持 BUG-54。

## 验收（Debug 包，各 10 次）

- 占位/开始/收杆/离席/强制咬钩：`overlay_command_sent` → `local_presentation_applied` P50 ≤ 100ms，P95 ≤ 250ms
- 操作后 2 秒内 `frame_stall` ≥ 250ms 为 0
- 连续两次收杆，第二次没有新的 `phase=stopping|resolving`

## 完成后

- [ ] spec §6 勾完并改状态为「已实现」
- [ ] `CHANGELOG.md` 记一条
- [ ] `npm run planning:master-xlsx` 把 BUG-56 写成已实现并填完成日期

## 派发

```text
@docs/策划/提示词/缺陷/bug-56-overlay-stall-replay-dev.prompt.md 按此实现 BUG-56
```

建议角色：Unity 桌面（不要派给改 server 的对话）
