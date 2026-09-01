# Overlay 宠物相位图标与进度环

```text
OverlayResources/status/hooked.png      ← hooked 咬钩
OverlayResources/status/groundbait.png  ← groundbaiting 打窝中
OverlayResources/status/hook-ring.png   ← 咬钩 / 打窝进度环（剩余时间 Radial 360，缺图回退矢量）
OverlayResources/status/ring-bg.png     ← 进度环底图（actor-ring-bg）
OverlayResources/status/fishing.png     ← 保留素材，钓鱼中不再显示状态图标
```

小图标建议 **20×20** 透明 PNG；进度环与底图建议 **32×32** 圆环（中间透明）。缺 `hook-ring` 时 Overlay 用矢量占位；缺 `ring-bg` 时只隐藏底图，不崩。

头顶显示规则：

- **咬钩** `hooked`：状态位 `hooked.png` + `ring-bg` + `hook-ring`（填充随 `hookDeadlineMs` / `phaseEndsAt` 从满收到空）
- **打窝** `groundbaiting`：状态位 `groundbait.png` + 同一套环表示打窝进度
- **钓鱼中**（waiting / baiting / casting 等）：不显示状态图标和环

Unity 角色位预制体 `OverlayPondActor` 含 `actor-ring-bg`（底）与 `actor-ring`（Filled Radial 360, Origin=Top）。在各塘 Overlay 布局 Prefab 里拖 `*-pet` / `*-name` / `*-status` / `*-ring-bg` / `*-ring` / `*-seat` 即可调位置和尺寸，再 **Export Overlay Layout**。
