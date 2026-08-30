# Overlay 宠物相位图标与上钩环（STEAM-DESKTOP-14D）

```text
OverlayResources/status/fishing.png     ← waiting / baiting / casting / 竿在水里
OverlayResources/status/hooked.png      ← hooked
OverlayResources/status/hook-ring.png   ← 上钩进度环占位（Unity Image Filled / Radial 360）
```

小图标建议 **20×20** 透明 PNG；进度环建议 **80×80** 圆环（中间透明）。缺文件时 Overlay 用矢量占位，不崩。

Unity 角色位预制体 `OverlayPondActor` 的 `actor-ring` 使用 `Image Type = Filled`、`Method = Radial 360`、`Origin = Top`。在各塘 Overlay 布局 Prefab 里拖 `*-pet` / `*-name` / `*-status` / `*-ring` / `*-seat` 即可调位置和尺寸，再 **Export Overlay Layout**。
