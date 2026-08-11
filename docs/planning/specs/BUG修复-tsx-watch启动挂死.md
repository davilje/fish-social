# BUG 修复：tsx watch 启动挂死（3001 永不监听）

| 状态 | **已实现** | 目标版本 v0.6.1-hotfix |
|------|------------|------------------------|
| 优先级 | **P0** | 本地开发完全不可用 |
| 编号 | **BUG-11** | 计划表 `项目开发需求计划表.xlsx` |
| 范围 | 服务端启动链路：`tsx watch` · 模块循环依赖 · Logger transport |
| 前置 | Phase 0/1 数据平台（pino Logger、correlationId、debugSampler） |
| 关联 | [`BUG修复-演示模式与自动打开浏览器.md`](./BUG修复-演示模式与自动打开浏览器.md)（BUG-09/10 为下游表象） |

---

## 1. 问题

执行 `dev.bat` / `npm run dev` 后：

| 表象 | 表现 |
|------|------|
| preview.html | 服务端 `:3001` 始终红点 |
| 浏览器 | 多数情况下不自动打开（`start_dev` 等 `/health` 超时） |
| Fish Social Web | 一直「服务端未连接」/ 演示模式 |

**实测**：`npx tsx src/index.ts`（无 watch）可正常启动；`tsx watch`（`server` 的 `dev` 脚本）打印 `Database ready` 后挂死，**3001 永不 Listen**。

---

## 2. 根因

### 2.1 直接触发

`server/package.json`：

```json
"dev": "tsx watch src/index.ts"
```

`npm run server` / `npm run dev` 均走 **tsx watch**。

### 2.2 挂死机制（Phase 0/1 引入）

在 `tsx watch` 子进程中，模块图存在循环依赖，加载中段死锁：

```
fishingObservability ↔ sessionRegistry
logger → debugSampler → db → logger
```

探针：`await import('./auth.js')`（拉取 observability ↔ sessionRegistry）在 watch 下卡住；无 watch 时可完成。

次要放大因素：开发环境 `pino-pretty` / `pino-roll` **worker transport** 与 `tsx watch` 叠加，加剧卡顿（非唯一条件）。

### 2.3 下游连锁

```
tsx watch 挂死
  → /health 永不 200
  → start_dev.py 不 open_browser
  → preview 3001 红
  → 客户端 demoMode / 未连接
```

> **说明**：BUG-09/10 已改善客户端重连与 Expo 开浏览器，但 **API 进程未 listen 时仍不可用**。本 BUG 是当前本地联调阻断的根因。

### 2.4 次要缺陷

`scripts/clean-ports.mjs`（`predev`）文件不完整，仅定义 `PORTS` 无释放逻辑——不导致挂死，但削弱端口清理。

---

## 3. 修复方案

### 方案 A（P0，立即恢复开发）— 必做

将服务端 `dev` 改为 **无 watch** 的 `tsx`：

```json
"dev": "tsx src/index.ts"
```

可选：热重载改用 `nodemon --exec tsx src/index.ts`（避免 tsx 自带 watch 与 pino worker 冲突）。

**验收**：`npm run server` 后 10s 内 `GET http://localhost:3001/health` → 200。

### 方案 B（P0，根治循环依赖）— 必做

| 改动 | 做法 |
|------|------|
| `sessionRegistry.ts` | **禁止**顶层 `import { logStructuredEvent } from './fishingObservability.js'`；在 `bindPlayer` 等调用点 **动态 import** 或抽纯函数日志到 `logger` |
| `logger.ts` ↔ `debugSampler.ts` | `isDebugSampled` 改为函数内 lazy require / 延迟 import；或 debug 采样不经过 logger 模块顶层 |
| `db.ts` ↔ `logger.ts` | `timedDbQuery` 内延迟取 `logWarn`，避免顶层互相 import |

**验收**：`npx tsx watch src/index.ts` 也能在 15s 内 listen（可选回归）；至少无 watch 路径稳定。

### 方案 C（P1，Logger 稳健）— 建议

- 开发默认：**同步 stdout**（不启 pino transport worker）
- `LOG_PRETTY=1` 时可用 `pino-pretty` 管道或同步 pretty，避免 `transport.target` worker
- `LOG_DIR` 落盘仅生产或显式开启时用 `pino-roll`

### 方案 D（P2，运维脚本）— 建议

补全 `scripts/clean-ports.mjs`，或删除空 `predev`，与 `scripts/ports.mjs free` 对齐。

### `start_dev.py`（P1，体验）

- API 就绪即可尝试打开 Web（不必双端都 OK 才开浏览器）
- 或 API 失败时明确打印：「服务端未 listen，请查 tsx watch / 循环依赖」

---

## 4. 验收标准

| # | 用例 | 预期 |
|---|------|------|
| 1 | `dev.bat` 或 `npm run dev` | 120s 内 `localhost:3001/health` 返回 `ok: true` |
| 2 | preview.html | 服务端绿点 |
| 3 | Web 进鱼塘 | Socket 连接成功，非演示模式（服务端已起时） |
| 4 | 控制台 | 出现 `Fish Social server running on http://localhost:3001` |
| 5 | （方案 B 后）`tsx watch` | 可选：同样能 listen，不挂在 `Database ready` |

**脚本**（建议）：`scripts/verify-server-boot.ts` — spawn `npm run server`，轮询 `/health`，超时失败。

---

## 5. 非目标

- 不改钓鱼业务逻辑、状态机、埋点语义
- 不强制上 Docker / 生产进程管理器（属 Phase 2 ARC-06）

---

## 6. 开发交接

[`docs/planning/prompts/bugfix-tsx-watch-hang-dev.prompt.md`](../prompts/bugfix-tsx-watch-hang-dev.prompt.md)

```
@docs/planning/prompts/bugfix-tsx-watch-hang-dev.prompt.md 按此实现
```

---

## 7. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | **已实现**：dev 改 `tsx` 无 watch；拆 observability↔sessionRegistry、logger↔db 循环；Logger 开发默认同步 stdout；新增 verify:server-boot |
| 2026-07-12 | 初稿：登记 BUG-11；根因 tsx watch + 循环依赖；方案 A~D |
