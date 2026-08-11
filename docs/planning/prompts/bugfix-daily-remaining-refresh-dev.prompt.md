# 开发提示词：今日剩余时长不刷新（BUG-14）

你是 Fish Social **前端为主** Agent（必要时微调 tick 字段）。按规格修复底栏「今日剩余」在钓鱼中不刷新。

## 必读

1. `docs/planning/specs/BUG修复-今日剩余时长不刷新.md`（**已实现** / **BUG-14**）
2. `mobile/app/pond/[id].tsx`（`remainingMs`）· `usePondSocket.ts`（`session_timer_tick`）· `pondUserManager.enrichPondUser` / `computeFishingDuration`
3. 勿回归 `docs/planning/specs/BUG修复-垂钓中头顶0秒闪烁.md`（BUG-13）

## 顺序

1. 明确 `todayFishingMs` 是基线还是 enrich 含本局；开钓时缓存 baseline，避免双计  
2. 底栏：`remaining = 8h - usedDisplay`，钓鱼中用 `baseline + (now - fishingStartedAt)`（或等价稳妥式）  
3. 鱼塘页对「在钓」开本地 `now` 定时刷新（250ms～1s）  
4. 自检：钓中剩余下降；停钓对齐；头顶会话秒表不闪 0  

## 非目标

改日切、改 8h 常量、改事件名。

## 验收

对照 spec §5；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/bugfix-daily-remaining-refresh-dev.prompt.md 按此实现 BUG-14
```

建议角色：`@frontend-dev`（主）
