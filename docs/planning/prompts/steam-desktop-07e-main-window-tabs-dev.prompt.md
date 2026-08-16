# STEAM-DESKTOP-07E：主窗口功能页签（取代弹窗）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。按规格改 07E 口径，勿扩到 ART-02 或全服广播。

## 必读

1. `docs/planning/specs/Steam桌面端Web功能对齐设计.md`（**已确认** / **STEAM-DESKTOP-07E-DESIGN**）
2. `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md` §4.3、§6
3. `docs/planning/specs/Steam原生桌面宠物Overlay.md`（主窗口可见时 Overlay 让出置顶）
4. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
5. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopModalHost.cs`
6. `desktop-overlay/MainWindow.xaml`（`Topmost`）

旧提示词 `steam-desktop-07e-modal-layer-dev.prompt.md` **作废**，不要再按弹窗层实现。

## 顺序

1. 底栏与 Overlay 菜单（`menu_friends` 等）一律 `ShowFromTray` + `PanelRouter.Show(对应 ShellPanelId)`。停止 `OpenFeatureModal` / `DesktopModalHost` 承载整页功能。
2. 把 07E 已有数据能力（社交/背包/图鉴/设置）迁进对应页签，替换 `BuildFriends` / `BuildCatch` 等占位页。卖鱼等小型确认框可留，不得再做 1040×580 功能模态。
3. **层级（P0）：** 从 Overlay 菜单唤起主窗口后，主窗口必须盖住 Overlay。推荐：Pipe 增加 `mainWindowRaised` 或命令 `yield_topmost`，Overlay 暂时取消 `Topmost`；主窗口隐藏到托盘后 Overlay 恢复置顶。禁止永久把 Unity 主窗口做成桌面置顶宠物，禁止靠再开弹窗躲开 Overlay。
4. 将主窗口壳与各功能页收为 Editor Prefab，脚本只绑数据和切页。不要把 Prefab 交给 Overlay 实例化。
5. 切页、关窗不得 `leave_pond`，不得重建 Socket/Overlay。
6. 自检 07E-DESIGN §7。

## 验收

对照规格 §7。完成后须 **用户确认** 再按 `planning-progress-sync` Checklist B 标已实现。不要自行改成已实现。

## 禁止

- 功能整页弹窗、第二 Unity Player、Overlay 内 uGUI
- 修改 `mobile/`、`server/`、`shared/` 业务规则
- 重写 07A～07D 已验收的登录、进塘、右键菜单项集合
