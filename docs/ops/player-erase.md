# 玩家数据导出与删号脱敏（D-L3-10）

## API

| 方法 | 路径 | 权限 |
|------|------|------|
| GET | `/api/admin/players/:playerId/export` | `admin`（`requireRole`） |
| POST | `/api/admin/players/:playerId/erase` | `admin` |

请求头：`X-Admin-Key: <ADMIN_SECRET>`

### 导出

返回 JSON 包：`profile` · `inventory` · `gear` · `codex` · `social` 摘要 · `metricsSummary`（按日聚合）。

### 删号 / 脱敏

查询参数或 body：

- `?dryRun=1` — 仅返回 `toDelete` / `toAnonymize` 行数，**不写库**

正式执行：

1. 清理 session / pending（DB 行）
2. 删除业务表（inventory、gear、codex、社交、players 等）
3. **匿名化** `fishing_metrics` · `daily_player_stats` 的 `player_id` → `anon_<HMAC>`
4. 写 `audit_log` + `[admin_audit]` 日志

## 环境变量

| 变量 | 说明 |
|------|------|
| `PLAYER_ERASE_PEPPER` | HMAC 盐；**生产必填**，禁止提交仓库 |
| `ADMIN_SECRET` | Admin API 密钥 |

## 生产护栏

- 执行 `erase` 前务必 `npm run db:backup`（或等价快照）
- 先 `dryRun=1` 核对行数
- 禁止在生产环境省略 `PLAYER_ERASE_PEPPER`

## 验收

```bash
npm run verify:data-platform-dp-d
```
