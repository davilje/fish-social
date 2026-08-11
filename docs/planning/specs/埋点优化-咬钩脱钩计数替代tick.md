# 埋点优化：咬钩/脱钩计数替代全量 tick

| 状态 | **已实现** | 目标版本 hotfix / 扩容准备 |
|------|------------|------------------------------|
| 完成时间 | **2026-07-12** | 设计时间 2026-07-12 |
| 优先级 | **P0**（千人存储与压测主库卫生） | |
| 编号 | **D-L2-15** | 计划表 `项目开发需求计划表.xlsx` |
| 范围 | `fishing_metrics` 写入 · 会话计数 · 日报/日聚合口径 · schema/verify |
| 触发 | 2026-07-12：库体积与千人估算；全量 `bite_tick_*` 约占历史 metrics ~70% |
| 关联 | [`运营日报-v1.md`](./运营日报-v1.md) §5.4 · [`服务器架构缺陷与埋点设计-v0.4.4.md`](./服务器架构缺陷与埋点设计-v0.4.4.md) · 存储估算（550 B/行 · tick=60s） |

---

## 1. 背景与目标

### 1.1 问题

当前 `waiting` 阶段**每分钟判定**均可能写入：

- `bite_tick_miss`（绝大多数）
- `bite_tick_hit`（随后通常还有 `bite_hook`，存在双记）

在单机机器人压测与未来千人～万人场景下，明细行线性膨胀，主库易被噪声打满；清理历史 bot tick 后又失去「整塘尝试密度」复盘能力——应用**聚合计数**替代**逐 tick 落库**。

### 1.2 目标

1. **默认不再**向 `fishing_metrics` 写入 `bite_tick_miss` / `bite_tick_hit`。
2. **咬钩成功**、**脱钩**各落库一行，并在 payload 中输出会话累计：咬钩次数、脱钩次数、（可选）自上次上钩以来的 miss 判定次数与等待时长。
3. 运营口径改为基于 `bite_hook` / `escape` / `catch_*`，并更新日聚合与日报文案。
4. 可选：Debug / 环境变量下恢复 tick 明细，供单机调参。

### 1.3 非目标

- 不改咬钩概率公式、状态机时序、客户端飘字。
- 不强制迁移历史已删的 tick 数据。
- 不在本需求内上 PostgreSQL（仍属 D-L2-04 演进）。

---

## 2. 产品口径（权威）

| 指标 | 旧口径 | **新口径** |
|------|--------|------------|
| 上钩次数 | （无清晰 KPI）/ 兼用 tick hit | `COUNT(bite_hook)` |
| 脱钩次数 | `COUNT(escape)` | 不变 |
| 脱钩率 | `escape / (escape + 上岸)` | **优先** `escape / bite_hook`（分母=上钩）；上岸仍可用 catch 作对照 |
| 「咬钩命中率」 | `bite_tick_hit / (hit+miss)` | **废弃 tick 命中率**；改展示：<br>① **上钩频率** = hooks / 钓鱼时长<br>②（可选）**判定效率** = hooks / (hooks + Σ missTicksOnHook) 仅来自成功时附带的累计 |
| 获鱼率 | catch 相关 | `catch_accept`（或等价上岸）/ `bite_hook` |

日报 §5.4「咬钩命中率」一行改为上述新口径，并在报告脚注标明「自 D-L2-15 起不再使用 tick hit/miss」。

---

## 3. 会话计数（内存）

挂在玩家本塘钓鱼会话（建议 `PondUser` / phase 附属结构，随 `fishing_start` 重置，随 leave/stop 丢弃或写入摘要）：

| 字段 | 含义 | 递增时机 |
|------|------|----------|
| `sessionHooks` | 本会话上钩次数 | `bite_hook` 成功进入 hooked |
| `sessionEscapes` | 本会话脱钩次数 | 记 `escape` 时 |
| `sessionMissTicks` | 本会话 miss 判定次数 | 每分钟 miss **仅内存 +1**，不落库 |
| `missTicksSinceLastHook` | 距上次上钩的 miss 数 | miss 时 +1；上钩成功后清零 |
| `waitingMsSinceLastHook` | 可选，距上次上钩等待毫秒 | 用 `fishingStartedAt` / 上次 hook 时间戳推算 |

---

## 4. 落库事件

### 4.1 停止写入（默认）

| event | 默认 |
|-------|------|
| `bite_tick_miss` | **不写** `fishing_metrics` |
| `bite_tick_hit` | **不写**（与 `bite_hook` 合并语义，避免双记） |

环境变量（建议）：

```text
METRICS_BITE_TICK_PERSIST=0   # 默认 0
METRICS_BITE_TICK_PERSIST=1   # 恢复旧行为（压测调参/Debug）
```

或仅当 `isDebugSampled(playerId)` 时写 tick（二选一，实现时定一种并写进 ops 说明）。

### 4.2 `bite_hook`（强化）

在现有 `speciesId` / `quality` 等基础上 **必须**增加：

```json
{
  "sessionHooks": 1,
  "sessionEscapes": 0,
  "sessionMissTicks": 42,
  "missTicksSinceLastHook": 42,
  "waitingMsSinceLastHook": 180000
}
```

`sessionHooks` 为递增后的值（含本次）。

### 4.3 `escape`（强化）

```json
{
  "sessionHooks": 3,
  "sessionEscapes": 1,
  "sessionMissTicks": 50
}
```

### 4.4 可选 P1：会话结束摘要

`fishing_stop` / `leave_pond`（本会话曾 fishing）增加一条轻量 summary 或扩展 payload：最终 `sessionHooks` / `sessionEscapes` / `sessionMissTicks`。用于「从未上钩」会话仍可统计尝试密度。

---

## 5. 下游改造

| 组件 | 改动 |
|------|------|
| `scripts/aggregate-daily-metrics.mjs` | `daily_pond_stats`：`bite_tick_hit` ← `COUNT(bite_hook)`；`bite_tick_miss` ← 日合计可用 `SUM(payload.missTicksSinceLastHook)`（仅成功样本）或改为新列 `hook_count`/`escape_count`（**推荐新列**，旧列兼容填充） |
| `compute-daily-summary` / 日报 HTML | 文案与公式按 §2 |
| `export-warehouse.mjs` / BI CSV | 同步列含义或加列 |
| `businessHealth.ts` | 咬钩率改用 hook/escape |
| `shared/metrics-schema.ts` | 更新必选/可选字段；tick 标为 deprecated 或 debug-only |
| `verify:server-observability` | 不再要求默认路径产生 tick 行；断言 hook/escape payload 含累计字段 |
| Admin timeline | 展示变稀为预期；summary 用 hook/escape 计数 |

表结构若增列：

```sql
-- daily_pond_stats 建议
hook_count INTEGER NOT NULL DEFAULT 0;    -- = bite_hook 日计数
escape_count INTEGER NOT NULL DEFAULT 0;
-- 过渡期：bite_tick_hit 写入 = hook_count；bite_tick_miss 可置 0 或填 SUM(missTicksSinceLastHook)
```

---

## 6. 验收标准

| # | 标准 |
|---|------|
| 1 | 默认配置下连续钓鱼 ≥10 分钟且多次 miss：**metrics 无新增** `bite_tick_miss`/`bite_tick_hit` |
| 2 | 上钩一次：恰有一条 `bite_hook`，payload 含 `sessionHooks`≥1，且含 `sessionMissTicks` 或 `missTicksSinceLastHook` |
| 3 | 脱钩一次：一条 `escape`，`sessionEscapes`≥1 |
| 4 | 日聚合 / 日报不再依赖「空 tick 表」导致命中率为 0；展示新口径或「—」+说明 |
| 5 | `METRICS_BITE_TICK_PERSIST=1`（或约定 Debug）时可恢复 tick 写入 |
| 6 | `npm run verify:server-observability`（及受影响的 daily verify）通过 |

---

## 7. 风险与说明

| 风险 | 缓解 |
|------|------|
| 「从未上钩」会话缺少 miss 轨迹 | P1 会话摘要；或 Debug 开 tick |
| 旧看板/脚本仍读 tick | 本需求强制改下游；CHANGELOG 注明破坏性口径 |
| 与 bot 压测 | 默认不写 tick 后压测主库更干净；需 tick 密度时开 persist 或独立压测库 |

---

## 8. 开发交接

提示词：[`../prompts/metrics-hook-escape-counters-dev.prompt.md`](../prompts/metrics-hook-escape-counters-dev.prompt.md)

```
@docs/planning/prompts/metrics-hook-escape-counters-dev.prompt.md 按此实现 D-L2-15
```

---

## 9. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | 立案 D-L2-15：**已确认**；设计时间 2026-07-12 |
| 2026-07-12 | **已实现**：默认停写 tick；hook/escape 会话累计；日聚合 hook_count/escape_count；日报新口径；`METRICS_BITE_TICK_PERSIST` |
