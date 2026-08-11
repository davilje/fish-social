# Phase 2 预启动检查报告

> 作者：后端维护分析 Agent (@backend-ops)
> 日期：2026-07-11
> 状态：✅ 服务器健康可启动 Phase 2 Sprint 1

---

## 检查清单总览

| 检查项 | 结果 | 严重度 |
|--------|------|--------|
| 服务端启动 | ✅ 正常启动（tsx watch） | — |
| /health 端点 | ✅ `{"ok":true,"uptimeSec":XX,"version":"0.1.0"}` | — |
| /ready 端点 | ✅ `{"ok":true,"db":"ok","metricsQueueDepth":0}` | — |
| 数据库连接 | ✅ SQLite WAL 模式，外键已启用 | — |
| 数据库完整性 | ✅ `PRAGMA integrity_check` → ok | — |
| 表结构完整 | ✅ 29 张表，含迁移新增表 | — |
| API 端点 | ✅ `/api/world` 返回 4 区域 4 池塘，`/api/inventory` 可查询 | — |
| 游戏配置 | ✅ 61 条配置项（含物种权重、逃脱率等） | — |
| 错误日志 | ✅ error_logs 表为空（0 条错误） | — |
| pending_catch_locks | ✅ 空表（无悬挂锁） | — |
| 钓鱼指标 | ✅ 27 种事件类型，11K+ 记录，持续写入 | — |
| Bot 生态 | ✅ Bot 循环正常运行，池塘自动补充/迁移 | — |
| Web 页面 (:8082) | ❌ 强制终止后 Zombie 进程残留，端口冲突 | **P0** |
| 端口占用 (:3001) | ✅ 无冲突，正常 LISTEN | — |
| expo 版本警告 | ⚠️ `react-native@0.76.5` 期望 `0.76.9` | P3 |
| Node.js 版本 | ✅ v20.11.1（兼容） | — |
| 依赖安装 | ✅ 所有 dependencies/devDependencies 完整 | — |
| 共享库构建 | ✅ shared/dist 已构建，tsc 成功 | — |

---

## 发现的问题

### P0 — 网页打不开：Metro 端口冲突导致 Web 服务未启动

- **现象**: `npm run dev` 后 Metro Bundler 报 `Port 8082 is being used by another process`，随后因非交互模式自动跳过 dev server。浏览器访问 `http://localhost:8082` 无响应。
- **根因**: 上一次 `npm run dev` 被强制终止（如 Ctrl+C、超时 kill）后，Metro 子进程残留，仍然持有 `:8082`。再次启动时，`dev-prestart` 脚本清理了部分 dev 进程但未清理 Metro，导致端口冲突。
- **影响**: Web 客户端完全不可用。但 API 服务端 (`:3001`) 正常启动。
- **修复**: 
  1. 改进 `dev-prestart` 脚本：在 `package.json` 的 `predev` 中增加 `npx kill-port 8082` 确保端口释放
  2. 或在 `scripts/expo-web.mjs` 中增加 `--port` 参数自动选择可用端口
- **验证**: 运行后 `netstat -ano | findstr :8082` 应无残留 LISTENING；`curl http://localhost:8082` 应返回 HTML

#### 1.0 `react-native` 版本不匹配（非阻塞警告）

- **现象**: Metro 输出 `react-native@0.76.5 - expected version: 0.76.9`
- **影响**: 不影响运行，但可能导致某些 API 行为不一致
- **建议**: 后续可执行 `npx expo install --fix` 对齐版本

### P1 — 启动前建议修复

#### 1.1 `showcase_fish_ids` JSON 序列化缺陷

- **文件**: `players` 表的 `showcase_fish_ids` 字段
- **现象**: 全部 630 名玩家的该字段值为 `[null,null,null,null,null,null,null,null]`，而非空数组 `[]`
- **影响**: 前端读取展示鱼时若未做 null 检查可能报错或显示空白占位
- **根因**: 玩家初始化代码写入 `Array(8).fill(null)` 或 `Array(8)` 后直接 JSON.stringify，未压缩空数组
- **修复建议**: 在 `server/src/players.ts` 的 `ensurePlayer` 函数中将默认值改为 `[]`；同时对已存在数据可写一条迁移脚本清理
- **verify**: `verify:phase1-core` 中应包含 `showcase_fish_ids` 格式校验

#### 1.2 生产环境缺少启动校验

- **文件**: `server/src/index.ts`
- **现象**: 仅 `assertAuthConfigured()` 和 `assertAdminSecurityConfigured()` 做了校验，但 `ALLOWED_ORIGINS` 在生产环境下缺失时也会在 `getCorsOriginPolicy()` 中报错，而 `getCorsOriginPolicy()` 在 `createApp()` 中才调用
- **影响**: 生产部署时如果漏配环境变量，会在路由注册后 crash，而非启动时即报错
- **建议**: 在 `index.ts` 的 `startListening` 前增加一个集中式环境变量校验函数，对所有生产必需变量（`JWT_SECRET`, `ADMIN_SECRET`, `ALLOWED_ORIGINS`）做前置检查

---

### P2 — 可以延到 Sprint 1 中修复

#### 2.1 部分 Bot 空鱼饵库存

- **现象**: 少数 bot 的 `bait_inventory` = `{}`（如 `bot-d537af7a-...`），而大部分 bot 有 `{"corn":5}` 或 `{"corn":5,"pellet":N}`
- **影响**: 不影响服务端稳定性，但空库存 bot 钓鱼逻辑可能出现意外行为
- **建议**: `gameState.ts` 中 bot 初始化时保证至少有基础饵料

#### 2.2 缺少 `.env.example`

- **文件**: 项目根目录
- **现象**: `.env` 仅有 5 个变量（PORT, EXPO_WEB_PORT, NODE_ENV, AUTH_DISABLED, ADMIN_SECRET），但代码中引用了 20+ 环境变量
- **建议**: 整理 `.env.example` 文件，列出所有可配置的环境变量及默认值

#### 2.3 Loki 日志传输默认禁用

- **现象**: 启动日志 `[loki] Loki disabled (LOKI_ENABLED != true)`，属正常行为
- **建议**: 无需操作，仅作记录

---

## 服务器快照

### 数据库统计

| 表名 | 行数 |
|------|------|
| players | 630（629 bot + 1 human） |
| inventory | 340 |
| pond_fish | 278（分布均衡：pond-bamboo=73, pond-calm=77, pond-mist=69, pond-sunset=59） |
| social_posts | 161 |
| game_config | 64 |
| fishing_metrics | 11K+（27 事件类型） |
| spot_bite_weights | 80（4 pond × 20 spots） |

### 游戏配置样本

```
BITE_LAMBDA=0.02
FISH_BITE_CHECK_MS=60000
MAX_BOTS_PER_POND=6
BOT_EVICT_POLICY=random
QUALITY_ESCAPE_BONUS_GRAY=0 ... QUALITY_ESCAPE_BONUS_GOLD=0.28
SPECIES_BITE_WEIGHT_* (0.04~0.12)
SPECIES_ESCAPE_RATE_* (0.08~0.25)
```

### 运行中服务

```
[fish supplement] pond-calm +3 active=0
[pond supplement] pond-calm +3 gap=6
[pond migration] pond-calm migrated=28
```

---

## 启动建议

**Phase 2 Sprint 1 可以启动，但必须先修复端口残留问题。** 建议修复优先级：

1. **P0（端口冲突）** → 修改 `predev` 脚本，每次启动前强制释放 `:8082`
2. **P1-1（showcase_fish_ids）** → 建议在 Sprint 1 中修复，耗时约 30 分钟
3. **P1-2（环境变量校验）** → 如果 Phase 2 涉及生产部署准备则建议修复，否则可延后
4. **P2/P3 项** → 可在 Sprint 开发中顺手修复

**推荐启动步骤**:
```
① 修改 package.json predev 脚本增加 kill-port 8082 (P0)
② 修复 showcase_fish_ids 默认值 (P1-1)
③ 运行 npm run verify:phase1-core 确保回归通过
④ 开始 Phase 2 Sprint 1 开发
```

## verify:* 建议

| verify 目标 | 覆盖项 |
|-------------|--------|
| `verify:phase1-core` | 应扩展校验 showcase_fish_ids 非 null |
| `verify:server-observability` | 健康检查、指标上报 |
| `verify:pond-navigation` | 池塘导航完整性 |
| `verify:session-checkpoint` | 断线重连功能 |

> 报告完毕。服务端整体健康，可以安全进入 Phase 2 开发。
