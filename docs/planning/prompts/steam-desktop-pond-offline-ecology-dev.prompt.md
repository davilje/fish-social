# 开发交接提示词：空鱼塘休眠与生态离线补算（STEAM-DESKTOP-05）

你是 Fish Social 的后端 Agent。请在不改变现有鱼群数值规则和客户端权威边界的前提下，实现 `STEAM-DESKTOP-05`：活跃鱼塘运行实时生态 Tick，空鱼塘休眠，玩家重新进入时按时间差完成幂等补算。

## 必读

1. `docs/planning/specs/空鱼塘休眠与生态离线补算.md`
2. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
3. `server/src/pondEcology.ts`
4. `server/src/serverLoops.ts`
5. `server/src/db.ts`
6. `server/src/pondSession.ts` 或实际进塘处理模块
7. `shared/pondEcology.ts`

## 现状重点

- `server/src/pondEcology.ts` 已有成长、迁移、补充、权重刷新和 `tickAllPonds()`。
- 当前 `tickAllPonds()` 会遍历全部鱼塘，需要改为只推进有连接玩家的活跃鱼塘。
- 现有鱼成长使用 `bornAt`/绝对时间曲线，应保持幂等。
- `pond_state` 当前已有多个 `last_*` 时间字段，需要新增并迁移 `last_simulated_at`。

## 实现顺序

1. 增加数据库迁移和 `last_simulated_at` 默认值/回填策略。
2. 抽离可复用的单鱼塘推进函数。
3. 实现 `ensurePondEcologyCurrent` 或等价唤醒接口。
4. 在进塘快照前执行事务化补算。
5. 修改实时循环，空鱼塘跳过高频 Tick 和广播。
6. 玩家离开最后一个连接后进入休眠状态。
7. 增加迁移、补充、成长、重启和并发唤醒测试。
8. 增加补算指标和大时间跨度保护。

## 幂等要求

- 成长使用绝对时间计算，不得按重放次数重复累加。
- 迁移事件按 `last_migration_at` 推进，每个事件只能执行一次。
- 离线迁移不要只使用不可重放的 `Math.random()`；使用事件时间/pondId/fishId 等确定性种子，或保存等价事件边界。
- 补充不得超过 `maxPopulation`。
- 所有鱼状态变更与 `last_simulated_at` 前移必须在同一事务。
- 事务失败时不能前移时间锚点。
- 多人同时进塘只能有一个补算事务生效。

## 性能要求

- 空鱼塘不执行完整生态循环。
- 成长直接按绝对时间计算。
- 迁移/补充设置最大重放步数和最大补算时长。
- 超过上限时使用确定性的压缩补算，并记录 `catchupCompacted`。
- 记录 pondId、offlineMs、replaySteps、migrated、supplemented、durationMs。

## 不要做

- 不改鱼种、品质、成长曲线等产品数值。
- 不创建一塘一服务器。
- 不把生态计算搬到 Unity。
- 不接 Steam 登录、Lobby 或 Relay。
- 不在空鱼塘广播无人接收的 `pond_ecology_updated`。

## 验收命令与场景

至少增加并运行：

- 空鱼塘跳过实时 Tick
- 空塘时间推进后成长变化
- 离线迁移和补充
- 重复唤醒幂等
- 服务器重启连续
- 并发进塘单次补算
- 大时间跨度压缩保护

现有测试和验证脚本必须继续通过：

```text
npm test
npm run verify:engineering
```

建议角色：`@backend-dev`。完成后回写 `STEAM-DESKTOP-05` 规格验收和计划表状态。
