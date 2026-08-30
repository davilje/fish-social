# 开发提示词：Overlay 座位预制体统一（STEAM-DESKTOP-14A 修订）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。按修订后的 14A：**`OverlayPondActor` 为座位唯一真相**；消除 `kind=spot` 小圆点与 `actor-seat` 双轨冲突。不改 `spotId` 权威。

## 必读

1. `docs/planning/specs/Steam桌面端-14AOverlay钓位座位图.md`（**已实现** / **STEAM-DESKTOP-14A** 修订）
2. `docs/planning/specs/Steam桌面Overlay场景布局管线.md`（ART-02）
3. `OverlayPondActorBaker.cs`、`OverlayLayoutExporter.cs`、`PondScenePresenter.cs`、`OverlayPondLayout.cs`

## 顺序

1. **Unity 座位模板**：`OverlayPondActor` 内椅图可换；猫 / 名 / 状态 / 环相对椅对齐；Baker 嵌到每个 `kind=spot`；新 spot 宿主尺寸贴近座位簇。
2. **导出**：嵌套 `actor-*` 必须写出**画布绝对**像素；`actor-seat` / spot 椅图拷到 `seats/`。
3. **Overlay**：椅图优先 `actor-seat.sprite` → spot.sprite → `_default` → 圆点；猫落点优先 `actor-pet` 中心；点击仍走座位矩形 + 现有选位命令；空位半透明 / 落座隐藏规则不变。
4. 无 `actor-*` 旧 JSON 回退不崩。自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

```text
@docs/planning/prompts/steam-desktop-14a-seat-markers-dev.prompt.md 按此实现 STEAM-DESKTOP-14A
```
