# STEAM-DESKTOP-08F：好友列表与申请 Prefab

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08F` |
| 类型 | 功能 / UI |
| 状态 | **已实现** |
| 完成时间 | **2026-08-17** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E |

## 目标

为好友列表和好友申请建立独立 Prefab，修复当前接受/拒绝按钮重叠导致不可点击的问题。

## 功能范围

- 独立 `PanelFriends.prefab`。
- 好友列表、在线状态、搜索、邀请、私聊入口。
- 收件申请、发出申请和处理状态。
- 每条申请使用独立的接受和拒绝 Button。
- 接受/拒绝后刷新列表和申请状态。

## 与 07E 的关系

- 从现有 `PanelSocial` 抽出好友/申请，落地为 `PanelFriends.prefab`。
- **禁止** 主窗口同时保留两套好友列表 UI。
- 申请行骨架在 Prefab 里；列表数据仍可运行时填充。聊天/在线钓友/私聊可留在 `PanelSocial`。

## UI 约束

```text
头像/昵称/时间 | 接受 | 拒绝
```

- 接受和拒绝必须是不同 GameObject、不同 Button 和不同回调。
- 设置固定最小宽度、间距和 Raycast 区域。
- 禁止父级透明层、文本或 LayoutElement 覆盖按钮。
- 申请行需要在 100%、125%、150% DPI 下保持可点击。

## 验收

- [x] 接受按钮可单独点击并正确调用接口。
- [x] 拒绝按钮可单独点击并正确调用接口。
- [x] 处理后不会误触发另一按钮。
- [x] 好友列表、申请状态和错误提示能刷新。
- [x] 好友操作不离开鱼塘、不创建第二个 Socket。

## 关联文件

- Web 基准：`mobile/app/social.tsx`、`mobile/lib/socialApi.ts`
- Unity 现有：`DesktopSocialModalView`、`PanelSocial.prefab`
