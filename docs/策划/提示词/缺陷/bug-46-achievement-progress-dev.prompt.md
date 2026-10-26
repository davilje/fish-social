# 开发提示词：成就列表当前进度（BUG-46）

你是 Fish Social 的后端与 Unity 桌面开发。按规格实现，勿扩需求。先改服务端字段，再改手账卡片。

## 必读

1. `docs/策划/规格/缺陷/BUG-46成就列表当前进度.md`（**已确认** / **BUG-46**）
2. `docs/策划/规格/玩法/钓鱼相册与成就.md` §3.4（解锁与隐藏规则，不要改）
3. `server/src/achievements.ts`、`shared/albumTypes.ts`、`fish-social-unity/Assets/Scripts/Desktop/JournalV2/Views/JournalV2AchievementController.cs`

## 顺序

1. `AchievementView` 与 `AchievementViewDto` 增加 `currentValue`。
2. `buildAchievementViews` 写入该字段。同一请求内每种 `conditionType` 只调用一次现有 `progressValue`。
3. 成就卡片：未解锁显示当前/目标与不超过满格的条；已解锁保持「✓ 已解锁」且条满。缺字段或已解锁但数值为 0 时按满条处理。
4. 自检 spec §5。不要改解锁条件、隐藏规则、他人资料过滤、右侧奖章计数，不要改 `mobile/`，不要加埋点。

## 验收

对照 spec §5。完成后按 Skill `planning-progress-sync`：spec → **已实现**，`build-master-plan-xlsx.py` 填完成时间，`npm run planning:master-xlsx`。

- [ ] 自己的未完成成就会显示当前/目标
- [ ] 他人看不到未解锁成就的进度
- [ ] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/策划/提示词/缺陷/bug-46-achievement-progress-dev.prompt.md 按此实现 BUG-46
```
