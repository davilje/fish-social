<!-- 来源: docs/planning/specs/后端优化-D-工程债与部署.md -->
<!-- 用途: @backend-dev — BE-OPT-D 总包（建议 C 完成后再开） -->

你是 Fish Social **后端工程师**。实现 **BE-OPT-D**（QUAL-01～QUAL-10），按 PR 切片交付。

## 必读

1. `docs/planning/specs/后端优化-Kickoff-C与D.md`
2. `docs/planning/specs/后端优化-D-工程债与部署.md`
3. 前置：**BE-OPT-C 建议已合入**（若并行，避开 `index.ts` / `admin.ts` / `createApp.ts` 冲突）

## PR 切片

| 切片 | 内容 | 可用子 prompt |
|------|------|----------------|
| **D1 必做** | QUAL-01 vitest ≥3 文件 | `backend-opt-d1-vitest-dev.prompt.md` |
| **D2** | QUAL-03 PG 批量 + 切读门禁 | `backend-opt-d2-pg-dev.prompt.md` |
| **D3** | QUAL-04/05/06/10 env · compose · 容量文档 | 本文件继续 |
| **D4** | QUAL-07/08 运维静态开关 · 生产日志掩码 | 本文件继续 |
| **D5 可选** | QUAL-02/09 拆模块 · cache 上限 | 本文件继续 |

未做切片必须在 CHANGELOG 写「延期」后再把整包标已实现。

## D3 要点

- `server/src/env.ts` 或 `docs/ops/server-env.md` + `.env.example`
- compose **禁止**默认 `ALLOWED_ORIGINS=*`
- 文档：`MAX_SOCKET_CONNECTIONS` vs `MAX_HUMAN_SOCKETS`

## D4 要点

- production 默认 `LOG_MASK_USER_DATA` 行为为掩码
- `OPS_STATIC_ENABLED`（名可变）：生产可关 `/planning` 等

## D5 要点

- 拆 `admin` 路由组 **或** fishingStateMachine 生命周期，二选一即可
- `fishingDebugCache` 设上限

## 不做

S4 多机 · FEAT-05 · 重做 A/B/C

## 验收后

**BE-OPT-D** → 已实现 → `npm run planning:master-xlsx`；可选将 **BE-OPT-00** 标已实现
