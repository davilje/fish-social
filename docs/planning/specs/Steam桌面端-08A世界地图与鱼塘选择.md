# STEAM-DESKTOP-08A：世界地图与鱼塘选择

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08A` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 完成时间 | **2026-08-17** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07A～07G |

## 目标

在 Steam 主窗口增加 Web 风格的世界地图。地图是一张可拖动、可缩放的超大尺寸 Image，鱼塘坐标与最终地图美术资源绑定。

## 功能范围

- 独立 `PanelWorldMap.prefab`。
- 大图拖动、滚轮缩放、边界限制和重置视图。
- 鱼塘坐标标记、名称、主题、在线人数和容量。
- 点击坐标显示鱼塘详情。
- 点击进入后沿用现有流程：加入鱼塘、启动 Overlay、隐藏主窗口。
- 当前鱼塘恢复、进入失败、满员和断线提示。

## 入口与约束

- 主窗口独立页签；底栏和 Overlay 菜单用 `menu_map` 唤起，不 `leave_pond`。
- Prefab 为唯一页骨架；脚本只绑定数据和手势。
- 世界地图坐标表 **不是** `STEAM-DESKTOP-ART-02`。ART-02 管 Overlay 塘内像素摆放；本需求只管主窗口大地图上的 `pondId` 标记。

## 数据与边界

- 坐标由 `pondId` 绑定，使用布局 JSON 或 ScriptableObject 管理，不写死在 UI 代码。
- 服务端鱼塘状态和进入权限为权威。
- 不在本需求实现鱼塘场景、钓鱼规则或 Overlay。
- 不改 Node 进塘协议；满员/权限失败只展示服务端结果。

## 验收

- [x] 地图可拖动、缩放且不会拖出有效边界。
- [x] 点击视觉坐标能命中正确 `pondId`。
- [x] 地图资源替换后坐标仍与当前鱼塘目录位置一致。
- [x] 点击鱼塘后能正确进入并显示 Overlay。
- [x] 进入失败、满员、断线和重试状态清晰。

## 说明

- 已导入 Web/shared 鱼塘目录中的 20 个鱼塘，包含 `pondId`、名称、`regionId`、主题、地图坐标和容量。
- Prefab 管理工具已通过验收，支持查看、修改高亮、更新、新增和删除无效 Prefab。

## 关联文件

- Web 基准：`mobile/components/WorldMapView.tsx`、`mobile/lib/useWorldMap.ts`
- Unity 入口：`fish-social-unity/Assets/Scripts/Desktop/UI/PanelRouter.cs`
- 规划资源：`docs/planning/specs/Steam桌面Overlay场景布局管线.md`
