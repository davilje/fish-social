# 后端优化 — Kickoff（C 与 D）

| 字段 | 内容 |
|------|------|
| 状态 | **已确认** |
| 设计时间 | **2026-07-12** |
| 前置 | **BE-OPT-A** · **BE-OPT-B** → **已实现**（2026-07-12） |
| 本轮开放 | A～D **已合入**；下一批仅 **BE-OPT-E**（千人另立项） |
| 不做 | FEAT-05 玩法并行抢主路径（可另轨）· BE-OPT-E 千人代码 |

---

## 1. 进度快照

| 编号 | 状态 | 说明 |
|------|------|------|
| BE-OPT-A | ✅ 已实现 | 安全收口 |
| BE-OPT-B | ✅ 已实现 | 热路径性能 |
| BE-OPT-C | ✅ 已实现 | 停机 / 限流 / health / Admin key |
| BE-OPT-D | ✅ 已实现 | 工程债 / vitest / PG / env |
| BE-OPT-E | 已定稿 | 千人另立项 |
| BE-OPT-00 | ✅ 已实现 | 总表（A～D 收口） |

---

## 2. 召唤顺序

```text
# C 已验收；下一刀 D
@docs/planning/prompts/backend-opt-d-quality-dev.prompt.md
# 或先切 D1：
@docs/planning/prompts/backend-opt-d1-vitest-dev.prompt.md
```

---

## 3. 规格入口

| 批次 | Spec |
|------|------|
| C | [`后端优化-C-停机与稳定性.md`](./后端优化-C-停机与稳定性.md) |
| D | [`后端优化-D-工程债与部署.md`](./后端优化-D-工程债与部署.md) |
| 总表 | [`后端优化-问题汇总与分批计划.md`](./后端优化-问题汇总与分批计划.md) |

---

## 4. 验收后

按 `.cursor/skills/planning-progress-sync/SKILL.md`：对应编号 → **已实现** + 完成时间 → `npm run planning:master-xlsx`。

C 与 D 全部完成后，可将 **BE-OPT-00** 标已实现（或保留为索引文档已定稿）。

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | A/B 完成后 Kickoff C→D |
