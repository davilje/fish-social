<!-- 来源: docs/planning/specs/BUG修复-tsx-watch启动挂死.md -->
<!-- 用途: 后端 — 修复 npm run dev / tsx watch 导致 3001 永不监听 -->

你是 Fish Social **后端开发 Agent**。修复 **BUG-11：tsx watch 启动挂死**。

## 必读

1. `docs/planning/specs/BUG修复-tsx-watch启动挂死.md`
2. `server/package.json`（当前 `"dev": "tsx watch src/index.ts"`）
3. 循环依赖文件：
   - `server/src/fishingObservability.ts` ↔ `server/src/sessionRegistry.ts`
   - `server/src/logger.ts` → `debugSampler.ts` → `db.ts` → `logger.ts`

## 背景

Phase 0/1 引入 pino Logger / debugSampler 后，`tsx watch` 下服务卡在 `Database ready`，HTTP 永不 listen。  
`npx tsx src/index.ts`（无 watch）可正常启动。导致 preview 3001 红、dev.bat 不弹窗、客户端「服务端未连接」。

---

## 任务 1（P0）恢复可启动 — 改 dev 脚本

`server/package.json`：

```json
"dev": "tsx src/index.ts"
```

若需热重载，优先：

```json
"dev": "nodemon --watch src --ext ts --exec tsx src/index.ts"
```

**不要**在未完成任务 2 前恢复 `tsx watch`。

---

## 任务 2（P0）拆循环依赖

### 2.1 `sessionRegistry.ts`

- 删除顶层 `import { logStructuredEvent } from './fishingObservability.js'`
- `bindPlayer` 等需要打日志处改为：

```ts
void import('./fishingObservability.js').then(({ logStructuredEvent }) => {
  logStructuredEvent('session_rebound', 'session_rebound', { ... });
});
```

或把 rebound 日志改为直接 `logInfo`/`logEvent`（来自 `logger.ts`，注意避免再引入环）。

### 2.2 `logger.ts` / `debugSampler.ts` / `db.ts`

- `logEvent` 内需要 `isDebugSampled` 时再动态 import `./debugSampler.js`
- `timedDbQuery` 内再动态 import `./logger.js` 的 `logWarn`，去掉 `db.ts` 顶层 `import { logWarn }`

**验收**：模块顶层不再形成 `observability↔sessionRegistry`、`logger↔db` 闭环。

---

## 任务 3（P1）Logger 开发模式稳健化

`server/src/logger.ts`：

- `NODE_ENV=development` 默认 **同步** `pino()` 写 stdout（**不使用** `transport: { target: 'pino-pretty' }`）
- 仅当 `LOG_PRETTY=1` 且显式允许时再用 pretty；优先避免 worker transport
- 落盘 `pino-roll` 仅当 `LOG_DIR` 非空且 `LOG_TO_FILE=1`（或生产）

保留现有 `logInfo` / `logEvent` API 与字段契约。

---

## 任务 4（P2）补全或移除空 predev

`scripts/clean-ports.mjs` 当前只有 `PORTS` 常量：

- **要么** 委托 `node scripts/ports.mjs free --force`
- **要么** 从根 `package.json` 删除 `"predev"`

---

## 任务 5 验收脚本

新增 `scripts/verify-server-boot.ts` +：

```json
"verify:server-boot": "npx tsx scripts/verify-server-boot.ts"
```

脚本：spawn `npm run server`（或 `tsx src/index.ts`），轮询 `http://127.0.0.1:3001/health`，15s 内 200 则 kill 子进程并 exit 0，否则 exit 1。

---

## 回归

```bash
npm run verify:server-boot
npm run verify:server-observability
npm run verify:auth
```

手动：

1. `dev.bat` → 控制台出现 `Fish Social server running`
2. preview.html 3001 绿
3. Web 进塘非演示模式

---

## 不改

- 不改钓鱼状态机 / 埋点语义
- 不改客户端 demo 超时逻辑（属 BUG-09，已实现）

---

## 完成后

1. 更新 `BUG修复-tsx-watch启动挂死.md` 状态 → **已实现**
2. 更新 `scripts/planning/build-master-plan-xlsx.py` 中 BUG-11 状态，并 `npm run planning:master-xlsx`
3. 补 `docs/planning/CHANGELOG.md`
4. 回复：改动文件 + verify 摘要

## commit 建议

```text
fix(server): unblock boot under npm run dev (break module cycles, drop tsx watch)

tsx watch deadlocked on fishingObservability↔sessionRegistry and logger↔db
cycles after Phase 0/1 logging. Use tsx without watch and lazy imports so
:3001 /health comes up again for local dev.
```
