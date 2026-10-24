# 背包与动态留言修复

日期：2026-10-24。状态：已实现，Unity EditMode 专项 17/17 通过。

- 道具页停止查询商店／装备接口，移除钓竿、鱼饵、船具重复列表及装备操作；恢复 Web 空袋提示。鱼获页保留卡片与选中项，切回时恢复照片、描述和按钮状态。
- 移除旧 `EquipItem` 控件，它的 y=498 与新详情区重叠。详情五行放在 y=418–518，操作区从 y=532 开始；出售恢复 Web 的 204×58，回鱼与分享保留在右侧。档位限制说明放入详情行。
- `Library/CozyInputBase.prefab` 的 `m_Placeholder` 原为空，提示字不受 InputField 管理；修复为已有 Placeholder Text 引用，所有继承实例同步受益。
- 回归覆盖道具页不请求装备、连续切换和关闭重开、空背包按钮禁用、三种缩放的描述／按钮边界、中文／多行／空格输入、清空与重新显示、基础输入框继承。
- 用已保存预制体生成并检查背包、道具空态和留言输入截图。命令：`CozyRedesignPreview.RenderInventoryComments`。输出在 Unity 的 `Library/CozyRedesignPreviews/`。

测试：`CozyBagCommentRegressionTests`、`CozySocialRefinementTests`、`CozyJournalLayoutOwnershipTests`。Windows 发布包实机点击与最终视觉待验收。
