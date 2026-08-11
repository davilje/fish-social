# 开发提示词：鱼塘叠加层与背包社交收口（FEAT-UI-2）

你是 Fish Social **前端 + 后端** Agent。按规格实现 FEAT-UI-2，勿扩需求。

## 必读

1. `docs/planning/specs/鱼塘叠加层与背包社交收口.md`（**已确认** / **FEAT-UI-2**）
2. `PondScene.tsx` · `PondCharacter.tsx` · `BackpackModal.tsx` · `bots.ts` · `leaderboard.ts`

## 顺序

1. **A** 场景 Overlay：气泡 + 全部飘字提到最高层，按角色坐标定位  
2. **B** 排行榜改 `inventory` 日/周最大鱼（含 bot）；保留空台/前 50 空槽  
3. **C** 去掉 bot 普通渔获随机发动态，仅史诗+  
4. **D** 背包固定至少 80 格 + 纵向滚动  

## 验收

对照 spec §5；完成后按 Skill `planning-progress-sync` 回写已实现。

## 派发

```text
@docs/planning/prompts/pond-overlay-backpack-social-dev.prompt.md 按此实现 FEAT-UI-2
```
