# 开发提示词：悬停浮窗条数与 actor-hint（STEAM-DESKTOP-18）

你是 Fish Social 的 **Overlay / Unity 桌面**工程师（服务端只读抄 `sessionCatchCount`）。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-18Overlay悬停浮窗条数与hint定位.md`（**已实现** / **STEAM-DESKTOP-18**）
2. 09B 仅时长口径被本单补丁；命中/菜单归 BUG-26
3. 预估：`OverlayHoverPresenter`、`OverlayPetActor._hintAnchor`、`IpcProtocol.sessionCatchCount`、`OverlayPondStateBuilder`

## 顺序

1. 浮窗两行：时长行 + `钓到N条!` / `空军`。
2. 卡片锚 actor-hint 槽（透明锚点，Collapsed hint 徽章不作 TranslatePoint）；水平居中、底对齐向上长。
3. IPC own+users 带 `sessionCatchCount`；Unity merge / 自己结算 +1；服务端 enrich 只读，bot 无账本为 0。
4. 不要改 09A 菜单项，不要把 hover 写进 `_hintBadge`。

代码若已合入，对照 §验收补缺口。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [ ] hint 槽位置 + 条数行
- [ ] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-18-hover-catch-hint-dev.prompt.md 按此实现 STEAM-DESKTOP-18
```
