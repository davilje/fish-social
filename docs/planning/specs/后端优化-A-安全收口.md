# 后端优化 A — 安全收口

| 字段 | 内容 |
|------|------|
| 功能名称 | REST 鉴权收口 · 注册加固 · 客户端日志 · 删号盐 |
| 编号 | **BE-OPT-A** |
| 子项 | SEC-01～SEC-06 |
| 状态 | **已实现** |
| 设计时间 | **2026-07-12** |
| 完成时间 | **2026-07-12** |
| 优先级 | **P0** |
| 工期估 | 1～2 人天 |
| 前置 | 无（可立即开工） |
| 总表 | [`后端优化-问题汇总与分批计划.md`](./后端优化-问题汇总与分批计划.md) |
| 开发提示词 | [`backend-opt-a-security-dev.prompt.md`](../prompts/backend-opt-a-security-dev.prompt.md) |

---

## 1. 背景与目标

### 1.1 背景

Socket 与多数写接口已有 JWT；但**注册发 token**、**玩家域读接口**、**client-logs**、**删号 pepper** 仍有公网风险。上云/对外演示前必须收口。

### 1.2 目标

1. 无法再用未授权请求冒充任意玩家拿 JWT（除明确的开发旁路）
2. 玩家私有数据读接口必须鉴权，且 token.playerId 与路径/查询主体一致
3. client-logs 不可匿名灌库
4. 生产环境缺 `PLAYER_ERASE_PEPPER` **拒绝启动**（与 `JWT_SECRET` 同级）

### 1.3 非目标

- 改社交玩法、加邀请码体系（可预留 hook，本批不做产品流程）
- Admin 运维台 RBAC 重构（已有；本批只禁 query key 见批次 C）
- 关闭 `AUTH_DISABLED` 开发旁路（仍限 development）

---

## 2. 范围与验收

| # | 子项 | 改动要点 | 验收 |
|---|------|----------|------|
| 1 | SEC-01 注册 | 生产：注册不得对任意 playerId 直接签 token；方案三选一或组合：①仅 `AUTH_DISABLED`/dev 开放旧行为 ②注册改为「创建新 UUID + 返回 token」禁止指定他人 id ③需邀请码/设备绑定（本批至少落地 ①+②） | 生产态指定已有 `playerId` 注册拿 token → 401/403；dev 行为有文档 |
| 2 | SEC-02/03 读鉴权 | `requireAuth` + `resolveAuthedPlayerId`：`/api/players/:id`（若含隐私字段）、`/api/friends/*`、`/api/posts/friends/:id`、`/api/inventory/:id`、`/api/dm/*`；公开墙/搜索可保留但不得泄露私信与背包 | 无 token → 401；token 与路径 playerId 不符 → 401/403 |
| 3 | SEC-04 DM | GET 会话前必须鉴权；仅会话双方可读；`markConversationRead` 仅本人 | 未登录改已读失败 |
| 4 | SEC-05 client-logs | `requireAuth`（或 HMAC）；`logs.length` 硬顶（如 ≤50）；可选沿用全局限流 | 超限 400；未登录 401 |
| 5 | SEC-06 pepper | `NODE_ENV=production` 且未设 pepper → `throw` 启动失败；更新 `.env.example` | 缺 env 起不来 |

### 2.1 公开可读例外（须写进代码注释）

| 路径 | 是否保持公开 | 说明 |
|------|--------------|------|
| `GET /api/world` / ponds 列表 | 可 | 非 PII |
| `GET /api/posts/wall` | 可 | 已是公开墙 |
| `GET /api/players/:id/public-view` | 可（建议校验 viewer 或降敏） | 他人主页；不得返回私信/背包 |
| `GET /api/players/search` | 可 | 仅公开昵称级字段 |

---

## 3. 技术影响

### 3.1 涉及文件（预估）

- `server/src/socialRoutes.ts`
- `server/src/createApp.ts`（inventory、client-logs）
- `server/src/playerAnonymize.ts` / 启动入口 `index.ts`
- `server/src/auth.ts`（必要时抽 `assertSelf`）
- `.env.example`
- `scripts/verify-auth.ts` 或新建 `scripts/verify-backend-opt-a.ts`
- 客户端：确保读接口已带 Authorization（`mobile/` 若缺则同步补）

### 3.2 风险

| 风险 | 缓解 |
|------|------|
| 客户端未带 token 导致读挂 | 先扫 mobile API 调用；dev 可临时 AUTH_DISABLED |
| 注册改 UUID 破坏旧存档 | 文档说明；提供「已有号登录」路径（已有 JWT） |

---

## 4. 验收清单

- [x] SEC-01～06 全部满足 §2
- [x] `npm run verify:auth` 绿；新增 `verify:backend-opt-a` 覆盖未授权读/灌日志
- [x] `.env.example` 含 `PLAYER_ERASE_PEPPER`
- [x] 变更记录补本 spec；计划表 BE-OPT-A → 已实现

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | 已确认；自后端再排查 SEC-01～06 |
| 2026-07-12 | **已实现**：注册禁冒充 · requireSelf 读鉴权 · DM · client-logs 上限 · pepper 生产硬失败 · `verify:backend-opt-a` |
| 2026-07-12 | 续作：production 注册强制 mint UUID；运维说明 [`../../ops/auth-register.md`](../../ops/auth-register.md) |
