# 格子、头像与页面统一更新

> 2026-10-26 起退出当前阅读目录。格子与头像效果已进入后续手账，现行说明见 [手账入口](../../../../../设计/界面/手账/README.md)。

## 已保存的资源

本轮直接修改 Unity uGUI 预制体及其 Variant，不依赖运行时重新生成页面布局。
预制体目录为 `fish-social-unity/Assets/Resources/Desktop/Prefabs/`。

- `CatchSlot`、`ShopItemCard`：插画底板、等比居中图片、名称及辅助信息分区；支持背包、商店和装备的不同格宽。
- `ShowcaseSlot`：用于图鉴和展示鱼获，图片区与文字区独立。个人中心展示柜采用四列两行八格，右侧展示插画与鱼获属性。
- `PanelProfileEdit`：左侧昵称和简介，右侧六种猫咪头像与可滚动展示柜；保留保存资料、选鱼及清空格子的业务接线。页面和选鱼弹窗关闭键均位于右上角。
- `PanelPondSelection`、`PondSelectionCard`：统一标题、指南、鱼塘信息卡与操作入口，修正底板遮挡图标。
- `PanelWorldMap`：左侧地图、右侧详情和操作；清除重复标题并修正地图底板层级。
- 相关详情底板使用 Sliced，插画使用 Simple + Preserve Aspect，避免把鱼获或商品插画当成九宫格拉伸。

## 猫咪头像

资源位于 `fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars/`。
六种形象为橘猫、三花猫、灰猫、暹罗猫、燕尾猫、白猫，按既有默认头像标识映射。
橘猫沿用现有头像，其余为相同构图方向的独立透明 PNG。导入类型为 Sprite，最大纹理尺寸 512。
主界面和个人资料的默认猫咪头像统一通过 `DesktopDefaultAvatars.Portrait` 读取；自定义上传头像仍保留原有加载流程，本轮未验证该流程与主界面的一致性。

## 重建与检查

编辑器入口 `CozyRedesignAuthoring.BatchApplyAndPreview` 保存布局并渲染预览。
预览输出在 Unity 工程的 `Library/CozyRedesignPreviews/`，使用示例数据，不代表服务器实时状态。
本轮交付预览副本在 `/Users/red/Desktop/fish/ui-refinement/预览/`。

2026-10-18 检查结果：

- 预制体验证：通过，见 `Library/CozyRedesign.validation.txt`。
- EditMode：234 项中 229 通过，5 项失败。失败断言均为旧的 `plate-default/hover` 与实际 `plate-large-default/hover` 贴图引用不一致；没有通过修改预期来掩盖差异。
- 本轮新增头像唯一性、表单节点、地图遮挡、格子自适应边界测试通过。
- 已逐页检查离线渲染截图；未进行 Windows Player 联网操作验收。

测试记录位于 `/Users/red/Desktop/fish/ui-refinement/tests.xml`。
