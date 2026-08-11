<!-- 来源: docs/planning/specs/架构-单实例容量与真人隔离-R2-3.md -->
<!-- 用途: 可选轻量实现 I1–I4；纯文档验收可不跑本提示词 -->

你是 Fish Social **后端开发 Agent**。实现 **R2-3 轻量容量守卫（I1–I4）**。文档口径以规格为准。

## 必读

1. [`docs/planning/specs/架构-单实例容量与真人隔离-R2-3.md`](../specs/架构-单实例容量与真人隔离-R2-3.md) §3.2 / §4 / §5
2. 现有 Bot 配额：R2-5（`MAX_BOTS_PER_POND` 等）— **不要改 Bot 驱逐逻辑**，只豁免 Bot 不受真人 Socket 顶

## 范围

| ID | 任务 |
|----|------|
| I1 | `MAX_HUMAN_SOCKETS`（默认 200）写入 `.env.example` 并在服务端读取 |
| I2 | 真人超限软拒绝新 join / 新会话；错误码明确 |
| I3 | Admin summary 或 `/ready` 暴露 human/bot 计数与 limit |
| I4 | `capacity_reject` 结构化日志 |

## 不做

- Redis / 多机 / PG 全面迁移
- 踢掉已在线真人
- 千人压测报告

## 完成后

- [ ] 勾选 spec §5 轻量实现验收
- [ ] CHANGELOG 实现节
- [ ] 计划表 R2-3 → 已实现（若仅文档已先标已实现，则本提示词对应补完成时间即可）
