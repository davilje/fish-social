<!-- 来源: docs/planning/specs/Steam桌面端-09BOverlay悬停状态与钓鱼时长.md -->



你是 Fish Social **Unity + 原生 Overlay 开发 Agent**。实现 **STEAM-DESKTOP-09B：悬停钓鱼时长 Tooltip**（IPC 字段 + 悬停交互）。



## 必读



1. [`docs/planning/specs/Steam桌面端-09BOverlay悬停状态与钓鱼时长.md`](../specs/Steam桌面端-09BOverlay悬停状态与钓鱼时长.md)

2. [`docs/planning/specs/Steam桌面端-09DOverlay布局与角色表现优化.md`](../specs/Steam桌面端-09DOverlay布局与角色表现优化.md) — **默认**状态 icon/圆环/昵称/64px 在 09D

3. [`OverlayPondStateBuilder.cs`](../../../fish-social-unity/Assets/Scripts/Desktop/OverlayPondStateBuilder.cs)

4. [`OverlayPetActor.cs`](../../../desktop-overlay/OverlayPetActor.cs)



## 必须做



1. **IPC DTO**：`NativeOverlayActorDto`（及 own）增加 `fishingPhase` · `sessionFishingMs` · `hookDeadlineMs`；Unity 从 snapshot + `session_timer_tick` 填充。

2. **悬停**：在 **64×64 猫身**上 `MouseEnter` 300ms → Tooltip **仅**本局时长或收杆剩余；`MouseLeave` 关闭。浮窗约 80×28，水平居中对齐猫身（不要用含昵称的整块 Actor 当热区）。

3. **不重复 09D**：Tooltip 不含状态 icon/圆环文案（默认表现由 09D 负责）。



## 不做



- 默认状态/圆环 UI（09D）

- 改 PERF-03b / server



## 完成后



- [x] 勾选 spec §5

- [x] 与 09D 联调验收（IPC + Tooltip 基线来自 09D；本次补 BUG-13 合并与拖动抑制）

- [x] 验收后更新 spec 状态与 CHANGELOG


