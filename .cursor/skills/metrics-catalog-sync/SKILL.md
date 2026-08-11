---
name: metrics-catalog-sync
description: >-
  Keep the fishing metrics catalog authoritative and fresh: shared/metrics-schema.ts,
  scripts/build-metrics-events-xlsx.py, and root v0.4.4-埋点表清单.xlsx (docs/planning/reports copy).
  Use when adding/changing/deprecating fishing_metrics or structured log eventTypes,
  implementing observability specs (D-L2-*), accepting metrics-related features, or
  when the user mentions 埋点表 / metrics-schema / planning:metrics-xlsx.
---

# 埋点表与需求开发同步

权威链路（禁止只改 xlsx 手工格）：

1. **代码契约**：`shared/metrics-schema.ts`（及写入点 `server/src/**`）  
2. **生成脚本字典**：`scripts/build-metrics-events-xlsx.py` 的 `EVENTS`  
3. **导出表**：`npm run planning:metrics-xlsx` → 仓库根目录 **`v0.4.4-埋点表清单.xlsx`**（并同步 `docs/planning/reports/` 副本）

策划进度表仍走 [`planning-progress-sync`](../planning-progress-sync/SKILL.md)；**埋点变更额外执行本 Skill**。

## 何时必须执行

| 时机 | 动作 |
|------|------|
| 新埋点 / 改 payload 字段 / 改默认是否落库 | 改 schema + EVENTS + 重生 xlsx |
| 埋点相关 spec **已确认**（设计定稿） | EVENTS 中状态/字段写清「已确认待实现」或目标契约 |
| 埋点相关功能 **已实现/验收** | EVENTS 状态→已实现；schema 与代码一致；重生 xlsx |
| 废弃默认写入（如 D-L2-15 tick） | 状态标「已废弃默认」并注明开关环境变量 |
| 用户问「埋点表是否最新」 | 对照 schema ↔ EVENTS ↔ 最近 xlsx 生成时间，过期则重生 |

## Checklist — 需求开发（埋点相关）

1. 写/更新 `docs/planning/specs/…`（状态已确认/已实现）  
2. `scripts/planning/build-master-plan-xlsx.py` 登记编号 + `npm run planning:master-xlsx`（见 planning-progress-sync）  
3. 生成/更新 `docs/planning/prompts/*-dev.prompt.md`，**文内强制**提醒：实现时跑本 Skill  
4. 若设计已定字段：先改 `build-metrics-events-xlsx.py` 的触发时机/必填字段/示例 payload（可与实现同 PR）  
5. 实现代码时：`metrics-schema.ts` 与写入路径一致  
6. **`npm run planning:metrics-xlsx`**  
7. 相关 `verify:server-observability`（或专项 verify）通过  
8. CHANGELOG / specs README 补一行  

## Checklist — 仅刷新埋点表

```bash
# 1. 编辑 scripts/build-metrics-events-xlsx.py（EVENTS）
# 2. 如有字段契约变更，同步 shared/metrics-schema.ts
npm run planning:metrics-xlsx
```

确认输出：仓库根目录 `v0.4.4-埋点表清单.xlsx`（及 `docs/planning/reports/` 副本）。

## EVENTS 行约定

| 列 | 要求 |
|----|------|
| 事件名 | 与 `event_type` / schema `eventType` 一致 |
| 必填字段 | metrics 实际校验或文档约定字段 |
| 示例 payload | 反映**当前默认落库**形状（短码用短码示例） |
| 状态 | `已实现` / `已确认` / `已废弃默认` / `已废弃` |
| 落点文件 | 主写入文件 |

相位短码（D-L2-16）示例 payload：`{"f":4,"t":5,"c":"bite_hook"}`，并在触发时机注明 0～8 字典。

## 与运营/扩容

- 默认不写的事件（tick）必须在表中可见，避免策划以为还有全量 miss。  
- 环境变量开关（`METRICS_BITE_TICK_PERSIST`、`METRICS_SKIP_BOTS` 等）写在「触发时机」或 ops 文档，并在表状态中点出。

## 关联

- 计划表：`planning-progress-sync`  
- npm：`planning:metrics-xlsx` · `verify:server-observability`  
- 近期规格：D-L2-15 咬钩计数 · D-L2-16 相位短码  
