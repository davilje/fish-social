# 开发提示词：每日钓鱼时长上海日重置（FISH-DAILY-1）

你是 Fish Social **后端 Agent**。按规格把每日 8h 额度改为 **Asia/Shanghai 0 点** 换日，并正确处理跨日仍在钓。

## 必读

1. `docs/planning/specs/每日钓鱼时长-上海日重置.md`（**已实现** / **FISH-DAILY-1**）
2. `server/src/pondUserManager.ts`（`todayKey` / `getTodayFishingMs` / `addTodayFishingMs` / `flushFishingSessionToToday` / `enrichPondUser`）
3. `server/src/pondSession.ts`（`startFishing` 8h 校验）
4. `server/src/leaderboard.ts` 的上海日 key 写法可复用

## 要点

1. `todayKey()` → 上海日，禁止 UTC `toISOString().slice(0,10)`  
2. **跨日仍在钓**：flush 旧日时长 → 今日已用归零（仅计新日）→ **相位/钓位/pending 不动** → 剩余时长刷新  
3. 在 tick/enrich/读写今日时长路径检测日界，避免 UI 与校验仍用旧日  
4. 加 verify（可假时钟）覆盖跨日在钓  

## 验收

对照 spec §6；完成后 spec→**已实现**，`npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/daily-fishing-shanghai-rollover-dev.prompt.md 按此实现 FISH-DAILY-1
```
