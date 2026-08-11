# 停机与探活（BE-OPT-C / STAB-06）

优雅停机（SIGTERM/SIGINT）期间进程进入 draining：

- `/ready` → **503** `{ ok:false, draining:true }`
- `/health` → **503** `{ ok:false, draining:true }`（与 ready 对齐，**不可调度**）

负载均衡 / K8s 探活请同时看 `/ready`；停机中两者均勿继续导流。

相关：`SHUTDOWN_TIMEOUT_MS`（默认 8000）、Admin SSE 仅 `/api/admin/live-session` 允许 `?key=`（见规格 STAB-05 §2.2）。

验收：`npm run verify:backend-opt-c`
