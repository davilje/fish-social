# 开发提示词：展示柜详情改成分行（BUG-47）

你是 Fish Social Unity 桌面 UI。只做 **BUG-47**。BUG-27～45 已落地，不要重做。BUG-46 成就进度条本项不做。

## 必读

1. `docs/planning/specs/bugs/BUG修复-Cozy桌面页还原.md`（**BUG-47**，状态已确认）
2. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileHubPanel.cs` 的 `ShowFishDetail`
3. 背包详情行的现成填法：`CozyItemArt.Rows`，以及 `DesktopCatchBagModalView` 里品质、体长、重量、售价、回鱼的分行

## 顺序

1. 展示柜点选一条鱼时，详情区用图标、标签、数值分行，不再把品质、体长、重量、捕获时间、来源塘拼成一段。
2. 空选中仍是「点击左侧格子查看鱼获资料。」
3. 不改 `server/`、`shared/`、`mobile/`。不给成就加进度条。
4. 预览展示柜页，确认分行可见、旧的小鱼图标不再叠在插画上。

## 验收

对照 spec §4.3 的 BUG-47。完成后按 Skill `planning-progress-sync`：该行改为 **已实现**，填写完成时间，再 `npm run planning:master-xlsx`。不要把 BUG-27～35 从 **已验收** 改回已实现。

- [x] 展示柜点鱼后详情是分行，不是一整段
- [x] 未改服务端成就 DTO
- [x] spec 中 BUG-47 勾选，计划表该行已实现

## 派发

```text
@docs/planning/prompts/bugs/bug-47-showcase-fish-detail-rows-dev.prompt.md 按此实现 BUG-47
```

建议角色：Unity 桌面 UI（本仓库 Cursor 对话直接改 `fish-social-unity/`）。不要派给 `@backend-dev`。
