# 开发提示词：占用座位不可顶机器人（STEAM-DESKTOP-17）

你是 Fish Social 的 **Overlay / Unity 桌面 + 后端**工程师。按规格改占座，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-17占用座位不可顶机器人.md`（**已确认** / **STEAM-DESKTOP-17**）
2. 废止 `FISH-SPOT-1`「点 bot 座可踢」；容量腾位 `evictBotsForHuman` 保留
3. 预估：`server/src/pondSession.ts`；`desktop-overlay/PondScenePresenter.cs`、`OverlaySpotMarker.cs`

## 顺序

1. `take_spot`：座位已有任何人（含 bot）→ 与真人占用相同失败，不 `removeBotUser`。
2. Overlay：占用座不发 `take_spot`；空座仍可点。
3. 09A 右键机器人不占座。有占座 verify 则改断言。
4. 不要改打窝/钓鱼公式，不要关 bot 进塘。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 点 bot 座不踢；点空座仍坐
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-17-occupied-seat-no-evict-dev.prompt.md 按此实现 STEAM-DESKTOP-17
```
