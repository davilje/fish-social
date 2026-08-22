# 开发提示词：玩法 Debug 菜单（STEAM-DESKTOP-12）

你是 Fish Social **后端 + Unity 桌面端 / Overlay**开发 Agent。按规格实现统一玩法 Debug 菜单，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-玩法Debug菜单.md`（**已确认** / **STEAM-DESKTOP-12**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（等级 / 塘熟练度 / 每 2h 扣费）
3. `docs/planning/specs/禁止钓鱼塘巡警事件.md`（一键出警同源）
4. 现有禁止塘 Overlay「一键出警」实现（可复用触发接口）

## 顺序

1. 服务端 Debug API（Development / 显式开关）：升级、满级、塘熟练度、加金币、发鱼获、推进 +2h 入场费进度、强制出警。
2. Unity：可呼出/关闭菜单（F9 或 Overlay「Debug」入口）；列表按钮绑定上述动作。
3. 失败提示中文；成功后刷新进度 / 背包 / Overlay 状态。
4. Release 默认无入口；生产未开开关时接口拒绝。
5. 可选埋点 `gameplay_debug_action`（走 metrics-catalog-sync）。
6. verify / 自检对照 spec §5。

## 不做

- Admin Web 概率面板、任意数值自由输入编辑器
- Release 默认暴露

## 派发

```text
@docs/planning/prompts/steam-desktop-12-gameplay-debug-menu-dev.prompt.md 按此实现 STEAM-DESKTOP-12
```

建议角色：`@backend-dev` + Unity Overlay / 主窗。
