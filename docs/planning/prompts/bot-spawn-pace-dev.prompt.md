<!-- 来源: docs/planning/specs/机器人进塘节奏与初始时长.md + 开发交接 -->
<!-- 用途: @backend-dev — FISH-BOT-2 -->

你是 Fish Social **后端工程师**。实现 **FISH-BOT-2**（启动 3～6、已钓时长随机、后续慢补至满塘）。

## 必读（按序）

1. `docs/planning/specs/机器人进塘节奏与初始时长.md`
2. `docs/planning/specs/机器人进塘节奏与初始时长-开发交接.md`
3. `server/src/bots.ts`（`tickSpawn` / `startBotLoop` / `tickBots`）
4. `server/src/pondUserManager.ts` → `startBotFishing`
5. `server/src/gameConfig.ts`

## 实现顺序

1. `startBotFishing`（或包装）支持 `elapsedMs`：`fishingStartedAt = now - elapsedMs`；**不**预写 `daily_fishing`
2. 新建 `bootstrapBots(io)`：每塘 `uniform(3,6)`；~75% 开钓 + 5～75min 回拨；其余 idle
3. 改 `tickSpawn`：删 while；未满 `MAX_BOTS_PER_POND` 时按 `BOT_SPAWN_CHANCE` **最多 +1**；允许最终满塘；新人可按 `BOT_JOIN_FISHING_CHANCE` 短回拨开钓
4. `startBotLoop`：用 `bootstrapBots` 替代启动立刻 while `tickSpawn`；Steady 仍 interval
5. 配置默认值写入 `gameConfig`（见 spec §2）；**不要**引入永久 `BOT_SOFT_CAP`
6. `scripts/verify-fish-bot-spawn-pace.ts` + `npm run verify:fish-bot-spawn-pace`

## 硬约束

- 启动瞬间每塘不得 ≈20
- 单次 spawn tick 不得 while 补满
- 长时间无真人时可补到满塘
- 保留 FISH-BOT-1 池复用与真人踢 bot

## 不做

改咬钩公式 · 拆/重建账号池 · FEAT-05 · 看板回写

## 完成后

1. [x] Spec / 计划表 **FISH-BOT-2** → **已实现** + 完成时间
2. [x] `docs/planning/CHANGELOG.md` 实现节
3. [x] `npm run planning:master-xlsx`
