# 美术资源与接入

| 类别 | 入口 |
|---|---|
| 角色与皮肤 | [猫咪绘制与导出](cat-skins/README.md)、[资源路由沿革](cat-skins/resource-routing.md)、[丧彪新增角色](cat-skins/sangbiao/README.md) |
| 鱼塘场景 | [更新与导出菜单](scenes/overlay-scene-update-and-editor-menus.md)、[当前场景工程记录](../engineering/scenes/README.md) |
| 水面特效 | [正式采用记录](../engineering/water-effects/2026-10-25-water-effects-22h-adoption.md)、[v4 素材](water-effects-22h/magic-v4/README.md) |
| 登录 | [五猫完整窗口 v4](login-pastel-2026-10-26/README.md) |
| 风格与出图 | [视觉规范](direction/style-guide.md)、[ComfyUI 配置](direction/comfyui-setup.md) |
| 旧方案 | [历史归档](../archive/2026-10-26/README.md) |

正式引用资源位于项目的 `image/`、`mobile/assets/`、`fish-social-unity/Assets/` 和 `desktop-overlay/OverlayResources/`。此处保留设计规范、绘制来源、审核预览和出图 staging；仍被工具或预览引用的素材与清单随文档保留。

角色资源以各皮肤的 `Assets/StreamingAssets/Fishing/<skin>/manifest.json` 和 `OverlayResources/fishing/<skin>/manifest.json` 为运行依据。九种猫咪说明记录较早批次；新增角色以各角色说明及运行时清单为准。

ComfyUI 预设见 [config/art/direction.json](../../config/art/direction.json)。运行 `npm run art:comfyui:health` 检查服务，按视觉规范使用 `npm run art:generate` 生成候选素材。
