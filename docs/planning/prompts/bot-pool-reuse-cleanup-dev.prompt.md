# 开发提示词：机器人账号池与清理（FISH-BOT-1）

你是 Fish Social **后端 Agent**。按规格实现固定 bot 池复用、扩名，并提供一次性清理脚本（**不改看板历史**）。

## 必读

1. `docs/planning/specs/机器人账号池与清理.md`（**已实现** / **FISH-BOT-1**）
2. `server/src/bots.ts` · `pondUserManager.ts` · `players` / `inventory` / `fishing_metrics`

## 顺序

1. 扩 `BOT_NAMES`（≥80，够 100 人一名）  
2. `ensureBotPool(100)` + `enterPondFromPool`；删除运行时 `bot-${randomUUID()}` 新建  
3. 离塘回池可再抽；`MAX_BOTS_PER_POND` 默认改为塘容量（**20**），**允许 bot 填满鱼塘**；真人仍可踢 bot  
4. 运维脚本：清多余 bot 明细与账号；**禁止**重写 analytics / daily_* 看板聚合  
5. verify：池=100、同 id 可再进塘、无真人时可填满单塘、账号数不膨胀  

## 验收

对照 spec §6；完成后 spec→**已实现**，`npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/bot-pool-reuse-cleanup-dev.prompt.md 按此实现 FISH-BOT-1
```
