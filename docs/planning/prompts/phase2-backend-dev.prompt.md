# Phase 2 后端开发交接提示词

> 面向 @backend-dev。下一迭代权威：`docs/planning/specs/Phase2-下一迭代开发计划.md`。

---

## 必读

0. Kickoff：`docs/planning/specs/phase2-开发Kickoff.md`
1. **本迭代**：`docs/planning/specs/Phase2-下一迭代开发计划.md`（以此为执行顺序）
2. 详规：`docs/planning/specs/phase2-开发计划.md` §3.4~3.7（ARC-09/10/11 · BUG-08）
3. 风险：`docs/planning/specs/Phase2-剩余事项设计与风险.md`
4. 已交付：ARC-06/07/08 · DP-A · DP-B · BUG-11

## 本迭代范围（工程轨）

### N1 — 先做（约 5d）

| 编号 | 标题 | 参考 |
|------|------|------|
| ARC-09 | 统一日志/指标 API | phase2-开发计划.md §3.4 |
| ARC-10 | 安全加固（限流/连接上限/dev-token） | §3.5 |
| BUG-08 | F1 Modal 计时（可与 ARC-10 并行，前端） | §3.7 |

### N2 — 紧跟（约 3d）

| 编号 | 标题 | 参考 |
|------|------|------|
| ARC-11 | 单元测试 + CI | §3.6 |

## 不在本 prompt

- FEAT-05 → `feat05-c-phase-dev.prompt.md`
- D-L3-* → `data-platform-phase2-dev.prompt.md`（从 **DP-C** 起）
- C4 / R2-3 / 千人多机扩容

## 实施要求

- 按 N1 → N2；ARC-09 与 ARC-10 可同周，**ARC-11 在拆分与限流落地后**
- 每项完成后按 Skill `planning-progress-sync` 回写计划表并 `npm run planning:master-xlsx`
- 回归：`verify:server-boot` · `verify:server-observability` · `verify:auth` · `verify:engineering`

## 开工口令

```
按 Phase2-下一迭代开发计划：先 ARC-09，并行 ARC-10 与 BUG-08，再 ARC-11
```
