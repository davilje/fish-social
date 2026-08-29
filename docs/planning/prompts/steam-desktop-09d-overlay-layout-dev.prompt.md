<!-- 来源: docs/planning/specs/Steam桌面端-09DOverlay布局与角色表现优化.md -->

你是 Fish Social **Unity + 原生 Overlay 开发 Agent**。实现 **STEAM-DESKTOP-09D：Overlay 布局与角色表现优化**。

## 必读

1. [`docs/planning/specs/Steam桌面端-09DOverlay布局与角色表现优化.md`](../specs/Steam桌面端-09DOverlay布局与角色表现优化.md)
2. [`desktop-overlay/MainWindow.xaml`](../../../desktop-overlay/MainWindow.xaml) · [`OverlayPetActor.cs`](../../../desktop-overlay/OverlayPetActor.cs)
3. [`PondScenePresenter.cs`](../../../desktop-overlay/PondScenePresenter.cs)
4. Web 圆环参考：[`鱼塘场景与社交列表UI优化.md`](../specs/鱼塘场景与社交列表UI优化.md) §3.1 D

## 必须做

1. **尺寸**：Overlay `960×560`（WPF + Unity 启动参数一致）；场景区无底部横栏。
2. **左上收纳菜单**：默认收起；展开纵向含「打开主界面」+ 原 08G 钓鱼操作 + 错误区；位于状态/鱼塘/钓位胶囊**上方**；移除原底部 `ControlBar` 与右上「打开主界面」按钮。
3. **角色**：`BodySize/CatSize=64`（源图 256×256）；**每位**用户昵称在宠物上方默认显示；Bot **不加**「·机」。
4. **默认表现**：钓鱼中 icon/短文案；上钩 **圆环进度条**（用 `hookDeadlineMs`）；idle 不显示。
5. **悬停**：≥300ms Tooltip **仅**本局时长或收杆剩余；热区 = 猫身 64×64；浮窗约 80×28 居中对齐猫身（依赖 09B IPC 字段；本需求可一并实现 DTO）。
6. **回归**：08G 命令语义不变；不 `leave_pond`；拖动/钓位/ACK 不退化。

## 不做

- 09A 玩家右键菜单、09C 聊天 UI
- 改 server/shared/mobile
- ART-02 JSON 导出器（仅文档记画布 960×560）

## 完成后

- [x] 勾选 spec §4
- [x] 更新 ART-02 spec 画布高度说明
- [x] 验收后更新 CHANGELOG（2026-08-20 已实现）
