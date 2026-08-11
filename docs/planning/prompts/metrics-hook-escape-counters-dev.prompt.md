<!-- 来源: docs/planning/specs/埋点优化-咬钩脱钩计数替代tick.md（D-L2-15） -->
<!-- 用途: 后端埋点 + 日聚合/日报口径 — 停写 bite_tick 明细，hook/escape 带会话累计 -->

你是 Fish Social **后端 / 数据脚本开发 Agent**。实现 **D-L2-15：咬钩/脱钩计数替代全量 tick 落库**。

## 必读

1. **权威规格**：[`docs/planning/specs/埋点优化-咬钩脱钩计数替代tick.md`](../specs/埋点优化-咬钩脱钩计数替代tick.md) — §2～§6
2. 关联口径：[`docs/planning/specs/运营日报-v1.md`](../specs/运营日报-v1.md) §5.4（咬钩命中率须改）
3. 代码入口：
   - `server/src/fishingStateMachine.ts` — `processWaitingBiteTick` / `bite_hook` / `escape`
   - `server/src/fishingMetrics.ts` — `recordFishingMetric`
   - `server/src/pondUserManager.ts`（或会话结构所在）— 挂会话计数
   - `shared/metrics-schema.ts`
   - `scripts/aggregate-daily-metrics.mjs`
   - `scripts/analytics/compute-daily-summary.mjs`（及日报 HTML 生成）
   - `scripts/analytics/export-warehouse.mjs`
   - `server/src/businessHealth.ts`
   - `scripts/verify-server-observability.ts`

## 背景（勿误修）

- **不要**改咬钩概率、状态机相位时长、客户端飘字。
- 目标是：**默认不写** `bite_tick_miss` / `bite_tick_hit`；在 **`bite_hook` / `escape` 落库时带上累计计数**。
- 旧「tick 命中率」废弃，改为上钩频率 / 脱钩率 / 获鱼率（见 spec §2）。

---

## 任务 A — 会话计数（P0）

在玩家本塘钓鱼会话上维护（`fishing_start` 时清零）：

| 字段 | 行为 |
|------|------|
| `sessionHooks` | `bite_hook` 时 +1（先加再写入 payload） |
| `sessionEscapes` | `escape` 时 +1 |
| `sessionMissTicks` | 每分钟 miss 时 **仅内存** +1 |
| `missTicksSinceLastHook` | miss +1；上钩成功写库后清零 |
| `waitingMsSinceLastHook` | 可选但建议做 |

leave / stop / 换塘时丢弃或按 spec §4.4（P1）写摘要。

---

## 任务 B — 写入策略（P0）

1. `processWaitingBiteTick`：**miss** → 只更新内存计数，**不调用** `recordFishingMetric('bite_tick_miss')`（除非 persist 开关打开）。
2. **hit** → 不要单独写 `bite_tick_hit`；走现有上钩路径写 **`bite_hook`**，payload 合并：

```ts
{
  speciesId, quality, // 已有
  sessionHooks,
  sessionEscapes,
  sessionMissTicks,
  missTicksSinceLastHook,
  waitingMsSinceLastHook, // 若实现
}
```

3. `escape` 的 `recordFishingMetric` 同样带上 `sessionHooks` / `sessionEscapes` / `sessionMissTicks`。
4. 环境变量（名称可微调，须文档化）：

```text
METRICS_BITE_TICK_PERSIST=0  # 默认：不写 tick 行
METRICS_BITE_TICK_PERSIST=1  # 恢复旧 tick 明细（压测调参）
```

---

## 任务 C — Schema / Verify（P0）

1. `shared/metrics-schema.ts`：`bite_hook` / `escape` 增加上述可选/必选字段说明；`bite_tick_*` 标 deprecated 或仅 persist=1。
2. `verify:server-observability`：
   - 默认路径断言：**无**强制 tick 行；
   - 断言上钩后 DB/`getTimeline` 能看到带 `sessionHooks` 的 `bite_hook`；
   - 脱钩带 `sessionEscapes`。
3. 更新埋点表生成脚本（若仓库有 `build-metrics-events-xlsx.py`）备注 D-L2-15。

---

## 任务 D — 日聚合与日报 / BI（P0）

1. `aggregate-daily-metrics.mjs`：
   - **推荐**：`daily_pond_stats` 增加 `hook_count`、`escape_count`（迁移）；  
   - 过渡：`bite_tick_hit` 列填入 `COUNT(bite_hook)`；`bite_tick_miss` 填 `0` 或 `SUM(missTicksSinceLastHook)`（文档写明）。
2. `compute-daily-summary` + 日报 HTML：去掉「tick 命中率」或改为「上钩/脱钩/获鱼」新文案（spec §2）。
3. `export-warehouse.mjs`、`businessHealth.ts`：同步，避免除零/全 0 误报。

---

## 任务 E — 文档（P0）

- `docs/ops/` 或仓库 README 短节：如何开 `METRICS_BITE_TICK_PERSIST`
- `运营日报-v1.md` §5.4 可打补丁脚注「口径见 D-L2-15」（若本 PR 方便）

## P1（时间允许）

- `fishing_stop` / `leave_pond` 会话摘要（从未上钩也能看到 `sessionMissTicks`）

---

## 明确不做

- 不改玩法数值表、不 VACUUM、不删 bot 玩家表
- 不实现 PG 迁移

---

## 验收（手动 + 脚本）

```bash
npm run verify:server-observability
# 若有：
npm run verify:daily-ops-report
```

手动：

1. 默认 env：钓 ≥10 分钟多次空竿 → DB 无新 `bite_tick_*`
2. 上钩 → 一条 `bite_hook` 含累计字段
3. 脱钩 → 一条 `escape` 含累计字段
4. `METRICS_BITE_TICK_PERSIST=1` 时 tick 可再出现

---

## 完成后

1. Spec 状态 → **已实现**，完成时间当天
2. `build-master-plan-xlsx.py` 中 D-L2-15 → 已实现 + 完成时间；`npm run planning:master-xlsx`
3. `docs/planning/CHANGELOG.md`、`specs/README.md` 同步
4. 回复：改动文件 + verify 摘要 + 口径变更说明

## commit 建议（仅当用户要求提交）

```text
feat(metrics): replace per-tick bite rows with hook/escape session counters

Stop persisting bite_tick_miss/hit by default; attach sessionHooks/Escapes
and miss-tick totals on bite_hook/escape. Update daily aggregates and reports.
```

---

## 一句话派发

`@docs/planning/prompts/metrics-hook-escape-counters-dev.prompt.md` 按此实现 D-L2-15
