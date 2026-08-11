<!-- 来源: docs/planning/specs/排行榜-入口与领奖台改版.md（FEAT-SOC-03b） -->

你是 Fish Social **全栈开发 Agent**（前端为主）。实现 **排行榜首页入口 + 领奖台改版（FEAT-SOC-03b）**。

## 必读

1. [`docs/planning/specs/排行榜-入口与领奖台改版.md`](../specs/排行榜-入口与领奖台改版.md)
2. 现有：`mobile/components/LeaderboardPanel.tsx` · `mobile/app/social.tsx` · `mobile/app/index.tsx` · `server/src/leaderboard.ts`

## 必须做

1. **周榜口径**：`weekly-king` 改为「本周单条最大鱼」（与 daily-biggest 同结构 `extra`），不再用 Σ 出售金币。  
2. **新页** `/leaderboard`：顶栏仅「每日排行 | 每周排行」；Top3 领奖台（中1左2右3）；4+ 纵向列表。  
3. **字段**：名次、头像、昵称、鱼种、尺寸、鱼塘名。  
4. **首页**世界地图增加排行榜入口 → `router.push('/leaderboard')`。  
5. **删除**客户端稀有/钓场子 Tab；社交排行 Tab 删除或改为跳转新页。  

## 不做

- 自研发奖、稀有榜 UI、改 Bot 排除规则以外的数据源哲学  

## 完成后

- [ ] 勾选 spec §4  
- [ ] 更新 FEAT-SOC-03b 状态与 CHANGELOG  
- [ ] 若有 verify：扩展或手测日/周最大鱼 + 无稀有入口  
