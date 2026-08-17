# STEAM-DESKTOP-08A：世界地图与鱼塘选择

请只实现 `STEAM-DESKTOP-08A`，不要修改服务端鱼塘规则。

## 必读

- `docs/planning/specs/Steam桌面端-08A世界地图与鱼塘选择.md`
- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/PanelRouter.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`

## 要求

- 创建 `PanelWorldMap.prefab`，使用大图、Viewport、拖动和滚轮缩放。
- 鱼塘坐标必须由 `pondId` 配置驱动，不写死到按钮代码。
- 切页不 `leave_pond`。
- 世界地图坐标表不是 ART-02（ART-02 只管 Overlay 塘内像素）。
- 点击坐标后显示详情；进入沿用 Connect/Join → Overlay → 隐藏主窗口流程。
- 处理边界、缩放、满员、断线、失败和重试。

## 禁止

- 不重写鱼塘权威、钓鱼规则或 Overlay。
- 不在本地伪造在线人数和鱼塘状态。
