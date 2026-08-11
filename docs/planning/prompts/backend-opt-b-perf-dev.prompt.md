<!-- 来源: docs/planning/specs/后端优化-B-热路径性能.md -->
<!-- 用途: @backend-dev（客户端事件需 @frontend-dev 协作）— BE-OPT-B -->

你是 Fish Social **后端工程师**（若新增 Socket 事件，同步前端）。实现 **BE-OPT-B**（PERF-01～PERF-05）。

## 必读

1. `docs/planning/specs/后端优化-B-热路径性能.md`
2. `docs/planning/specs/BUG修复-会话计时广播回归.md`（**禁止**重蹈 BUG-07）
3. `server/src/serverLoops.ts` · `pondEcology.ts` · `pondUserManager.ts`

## 顺序

1. PERF-01 生态 emit 改用不建全量 snapshot
2. PERF-02 缩短/拆分生态写事务（谨慎测）
3. PERF-03 会话计时轻量事件 + 客户端合并时长；跑 `verify:session-timer-broadcast`
4. PERF-05 spot weight 缓存
5. PERF-04 处理 dead `consumeDirtyUsers`（接线或删除并文档说明）

## 硬约束

不要为了省带宽把 session timer 改回会丢 `sessionFishingMs` 的 dirty 合并。

## 完成后

计划表 BE-OPT-B → 已实现 → `npm run planning:master-xlsx`
