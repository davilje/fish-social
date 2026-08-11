<!-- 来源: docs/planning/specs/Phase2-下一迭代开发计划.md + C-调优与状态机.md -->
<!-- 用途: 后端/前端 — FEAT-05 C 期子任务 -->

你是 Fish Social **开发 Agent**（后端为主，C5/C6 需前端/动效协作）。实现 **FEAT-05 C 期调优与状态机**（**C4 不做**）。

## 必读

1. `docs/planning/specs/Phase2-下一迭代开发计划.md` §3.3（排期）
2. `docs/planning/specs/C-调优与状态机.md`（子任务权威）
3. `docs/planning/specs/状态机需求描述.md`（C6 规格）
4. 前置：ARC-08 **已实现**；建议 **ARC-11 起步后再做 C6**

## 顺序（严格）

| 步 | 子任务 | 说明 |
|----|--------|------|
| G1 | **C1** 热更配置 + **C7** 灰度 | 可并行 |
| G2 | **C5** 图鉴/钓鱼日志 | |
| G3 | **C6** 完整状态机与阶段动画 | 再 **C2** Bot |
| G4 | **C3** 金币 Sink | 模拟通过或稳定后再做 |

不做：C4 繁衍遗传 · FEAT-06。

## 每项完成后（强制）

按 Skill `.cursor/skills/planning-progress-sync/SKILL.md`：若 FEAT-05 整包验收才标 **已实现**；子任务阶段性完成可在 spec 变更记录写明，整包完成再改计划表 FEAT-05 完成时间。

## 验收回归

```bash
npm run verify:server-boot
npm run verify:session-timer-broadcast
npm run verify:disconnect-reconnect
# 按子任务新增 verify:feat05-c1 等（如有）
```

## 开工

从 **C1 + C7** 开始实现。
