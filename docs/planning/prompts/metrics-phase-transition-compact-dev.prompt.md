<!-- 来源: docs/planning/specs/埋点优化-相位跳转短码.md（D-L2-16） -->
<!-- 用途: 后端 — fishing_phase_transition 短码落库；更新 schema + 埋点表 xlsx -->

你是 Fish Social **后端开发 Agent**。实现 **D-L2-16：相位跳转短码（保留 from/to）**。

## 必读

1. [`docs/planning/specs/埋点优化-相位跳转短码.md`](../specs/埋点优化-相位跳转短码.md) — 字典 0～8、payload 契约、验收  
2. `.cursor/skills/metrics-catalog-sync/SKILL.md` — **改埋点必须同步埋点表**  
3. 代码：`server/src/fishingObservability.ts`（`recordPhaseTransition`）· `shared/types.ts` · `shared/metrics-schema.ts`  
4. 生成器：`scripts/build-metrics-events-xlsx.py` → `npm run planning:metrics-xlsx`

## 任务（P0）

1. 在 `shared/` 增加相位码字典与 `phaseToCode` / `codeToPhase`（0～8，与 spec 表一致）。  
2. `recordPhaseTransition` → metrics 的 payload **仅** `{ f, t, c }`（或等价短键）；日志可继续全称。  
3. `phase_transition_invalid` 同步短码策略。  
4. 更新 `metrics-schema.ts` + `build-metrics-events-xlsx.py`，执行 `npm run planning:metrics-xlsx`。  
5. 单测或 verify：短码可逆；observability verify 仍绿。

## P1（时间允许）

- `METRICS_SKIP_BOTS=1`  
- 合法 transition 采样；非法始终写  

## 不做

- 不要改成只有 1～4 或只存当前阶段、丢掉 from。  
- 不改玩法状态机本身。

## 完成后

1. Spec 状态 → **已实现** + 完成时间；`build-master-plan-xlsx.py` + `npm run planning:master-xlsx`
2. **必须**按 `.cursor/skills/metrics-catalog-sync/SKILL.md` 更新 schema + `build-metrics-events-xlsx.py` + `npm run planning:metrics-xlsx`
3. CHANGELOG / specs README
4. 确认埋点表 xlsx 已生成

## 派发

`@docs/planning/prompts/metrics-phase-transition-compact-dev.prompt.md` 按此实现 D-L2-16
