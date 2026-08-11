# 开发提示词：鱼塘场景与社交列表 UI 优化（FEAT-UI-1）

你是 Fish Social **前端为主、必要时后端** Agent。按规格实现 FEAT-UI-1，勿扩需求。

## 必读

1. `docs/planning/specs/鱼塘场景与社交列表UI优化.md`（**已确认** / **FEAT-UI-1**）
2. `mobile/components/PondCharacter.tsx` · `FishingFloatText.tsx` · `CatchFishModal.tsx` · `PondSocialPanel.tsx` · `PostCard.tsx` · `app/leaderboard.tsx` · `app/pond/[id].tsx`
3. `server/src/leaderboard.ts` · `server/src/fishingFloatText.ts`（bot 上榜、抛竿飘字）

## 顺序

1. **H** 修复动态评论区不可见  
2. **A/B/D** 默认仅自己状态；他人悬停/点按气泡；两档 icon；上钩环形倒计时（他人可只见环）  
3. **C** 抛竿 `fishing_float_text` kind=cast（同塘可见渐隐）  
4. **E** 获鱼/分享等统一自定义 Modal，去掉系统 Alert  
5. **F** 排行榜允许 bot；领奖台三席 + 前 50 滚动槽位恒显（可空）  
6. **G** 在线钓友纵向滚动 + 竖屏布局  

## 验收

对照 spec §5；完成后按 Skill `planning-progress-sync`：spec→**已实现** + 计划表完成时间 + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/pond-social-ui-optimize-dev.prompt.md 按此实现 FEAT-UI-1
```
