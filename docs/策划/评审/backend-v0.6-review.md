# 后端健康度评估 — v0.6

| 维度 | 评分 | 趋势 |
|------|------|------|
| **总体健康度** | **B+** | ↑ 持续改善中 |
| 代码可维护性 | B+ | ↑ 架构拆分后显著提升 |
| 测试覆盖 | B- | → 有验收脚本但缺单元测试 |
| 安全性 | B | ↑ RBAC + JWT 补齐 |
| 性能 | B+ | ↑ 批量写入 + dirty 索引 |
| 部署与运维 | B- | → 缺 Dockerfile 和 CI |
| 架构一致性 | B+ | ↑ 模块收敛明显 |
| 缺失/未完成 | C+ | → P2 推迟项较多 |

---

## 1. 后端健康度总评

### 评分：B+（良好，有关注点）

**评价总结**：经过 v0.4.4 ~ v0.6 三个版本的密集迭代，后端已经从"快速原型"进化到"可运维产品"阶段。核心钓鱼逻辑完整坚固，可观测性体系初具规模，架构问题（鉴权/定时器/会话/入口）已全部修复。当前处于 **P0/P1 基础设施完成、P2 能力推迟** 的阶段。

**各模块成熟度**：

```
钓鱼核心逻辑 (A0-C)        ██████████ 95% - 完整
数值系统 (D1-D12)          ██████████ 95% - 完整
经济系统 (B0-B1)           ████████░░ 80% - 基础完好，缺深度 sink
可观测性 Phase 0           ██████████ 95% - 完整
可观测性 Phase 1           ██████████ 95% - 10/10 验收通过
架构修复 (R0-R2)           ██████████ 95% - 三期全部实现
Bug 修复                   ██████████ 90% - 核心问题已修复
C 期功能 (状态机动画等)     ██░░░░░░░░ 20% - 仅文档化
经济 sink (C3)             █░░░░░░░░░ 10% - 未开始
```

---

## 2. 各维度详细评估

### 2.1 代码可维护性 — B+

**正向**：
- 架构拆分成功：`index.ts` 瘦身为纯编排层，`createApp` / `socketLifecycle` / `socketPondHandlers` / `serverLoops` 职责分明
- 注册中心模式：`sessionRegistry.ts` 统一 playerId↔socketId↔userId 映射，`timerRegistry.ts` 统一定时器生命周期
- TypeScript 全覆盖，类型定义一致，无 `any` 滥用
- 迁移模式统一：19 个 migration 文件均遵循统一模式
- 日志模式统一：`logStructuredEvent` + `recordFishingMetric` 贯穿

**问题**：
- `gameState.ts`（816 行）是神级模块，同时处理会话管理、塘用户管理、Bot 管理、聊天、checkpoint、快照构建，应拆为 3~4 个独立文件
- `fishingStateMachine.ts`（750 行）体量较大但可接受（核心业务逻辑）
- `db.ts`（284 行）混合 DDL schema 定义、migration 调用链、运行时 DB 初始化 → 应拆为 `schema.ts` + `migrations/index.ts`
- 日志 API 双轨：`logger.ts` 的 `logInfo/logWarn/logError` 与 `fishingObservability.ts` 的 `logStructuredEvent` 并存
- 指标 API 双轨：`recordFishingMetric` 与 `recordStructuredMetric` 功能重叠
- 少量硬编码中文错误消息混在业务逻辑中

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| P2 | 拆分 `gameState.ts` → `pondSession.ts` + `pondUserManager.ts` + `pondChat.ts` | 2d |
| P2 | 统一日志/指标 API 为单一入口 | 1d |
| P2 | 将 `db.ts` 的 DDL 拆出到独立 schema 文件 | 0.5d |
| P3 | 错误消息 i18n（或统一到 shared 常量） | 1d |

---

### 2.2 测试覆盖 — B-

**正向**：
- 10 个验收脚本覆盖主要路径（phase1-core 15/15、auth、session-checkpoint、observability 等）

**问题**：
- **零单元测试**：所有验证都是集成/E2E 脚本，无 `*.test.ts` 文件
- 关键核心逻辑无隔离测试：`fishingStateMachine` phase transition 边缘情况、`rollBiteHook` 概率计算、`pondEcology` 鱼群算法、`timerRegistry` 并发
- 验收脚本非自动化：不集成到 CI
- 无压力/负载测试（这是实时多人游戏）

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| **P1** | 为 `fishingStateMachine` 核心 phase 迁移路径加单元测试（vitest） | 2d |
| **P1** | 为 `timerRegistry` 并发安全加单元测试 | 1d |
| **P1** | 配置 CI（GitHub Actions）自动运行所有 `verify:*` | 1d |
| P2 | `rollBiteHook` 概率分布统计测试（Monte Carlo） | 1d |
| P2 | Bot 行为集成测试 | 1.5d |
| P2 | 基础负载测试（k6 或 wrk）摸底单实例上限 | 2d |

---

### 2.3 安全性 — B

**正向**：
- JWT 鉴权（R0-1）：Socket.IO `io.use` 校验 + HTTP `requireAuth`
- Admin RBAC（P1-E1）：viewer/operator/admin 三级，无配置向后兼容
- SQL 注入防护：参数化查询
- 日志合规：`maskSensitiveFields`
- 身份不匹配检测：`identity_mismatch` 事件

**问题**：
- Admin 密钥以查询参数传递（`?key=xxx`），会出现在服务器日志和浏览器历史中
- **开发环境 /api/auth/dev-token 无保护**：`NODE_ENV=development` 时任何人都可生成任意 playerId 的 token
- 无请求频率限制（Rate Limiting）
- 无请求体大小限制（`express.json()` 默认 100kb）
- 无输入校验库（zod/joi）

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| **P1** | 开发环境 dev-token 加 IP 白名单或禁用（仅本地可用） | 0.5d |
| P2 | Admin API 统一使用 Header（`X-Admin-Key`），移除 URL query 传 key | 0.5d |
| P2 | 添加 express-rate-limit 中间件 | 0.5d |
| P2 | 配置 `express.json({ limit: '1mb' })` | 0.1d |

---

### 2.4 性能 — B+

**正向**：
- 批量写入降写压（R2-2）：metrics 队列 1s/50 条批量 flush
- Dirty 索引广播（R2-1）：只广播脏用户
- Waiting 索引：咬钩循环只遍历 waiting 状态，O(n)→O(k)
- WAL 模式：SQLite `journal_mode=WAL`
- Prometheus 指标默认关闭

**问题**：
- 内存使用随用户增长：全部在线态在 Map 中无上限
- `[...ensurePond().values()]` 频繁创建数组，GC 压力大
- 生态 tick 30s 执行 `tickAllPonds()` — 鱼多时可能超时
- 无全局连接数上限
- 无 SQLite 自动备份

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| **P1** | 添加全局最大连接数配置 `MAX_CONNECTIONS` | 0.5d |
| P2 | `ensurePond()` 迭代优化 | 0.5d |
| P2 | SQLite 定时备份脚本（cron 每日） | 0.5d |
| P2 | 生态 tick 超时告警 | 0.2d |

---

### 2.5 部署与运维 — B-

**正向**：
- 优雅停机分阶段执行
- 端口冲突自动重试
- 监控 Docker 编排（loki + grafana）
- Grafana dashboard 预置
- 告警规则 + Webhook

**问题**：
- **无服务器 Dockerfile** → 无法容器化部署
- **无主 docker-compose.yml** → 需手动启动
- `.env` 加载方式原始（手工解析）
- 无 CI/CD
- `/health` 版本号硬编码
- `version: '0.1.0'` 与 package.json 不同步

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| **P1** | 创建 Dockerfile（多阶段构建） | 1d |
| **P1** | 创建主 docker-compose.yml（服务 + 监控一体化） | 1d |
| P2 | 替换 .env 加载为 `dotenv` 包 | 0.3d |
| P2 | 配置 GitHub Actions CI | 1d |
| P2 | `/health` 版本号从 package.json 动态读取 | 0.2d |

---

### 2.6 架构一致性 — B+

**正向**：
- 模块职责收敛良好、注册中心统一、定时器统一、状态机合法迁移表
- 迁移脚本统一模式

**问题**：
- `gameState.ts` 职责过多
- 双日志 API、双 metrics API
- Bot 逻辑分散：gameState / bots / botHookCatch
- 部分 `console.log/warn` 绕过日志系统

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| P2 | 统一日志 API | 1d |
| P2 | 统一 metrics API | 1d |
| P2 | 所有 `console.*` 替换为 logger 调用 | 0.5d |
| P2 | Bot 逻辑集中到 `bots.ts` | 1d |

---

### 2.7 缺失/未完成 — C+

**推迟到 Phase 2 的影响**：

| 推迟项 | 影响 |
|--------|------|
| C 期功能（C1~C7） | 状态机动画、热更、Sink、遗传、图鉴均未开始 — **高** |
| 金币 sink（C3） | 无耐久消耗，金币长期可能通胀 |
| PostgreSQL 迁移 | SQLite 写压力上限存在，但短期够用 |
| OTel 分布式追踪 | 单实例场景收益有限 |

**其他缺口**：
- **Mobile JWT token 管理**：客户端尚无 token 存储/续期逻辑（服务端已做，客户端未接入）
- 图鉴系统：`codex.ts` 已存在基础方法但未接入
- 遗传系统未开始
- 热更新系统未开始
- BUG-08 F1 Modal 计时：已知未排查

**建议**：

| 优先级 | 建议 | 工作量 |
|--------|------|--------|
| **P1** | Mobile JWT token 管理 | 2d |
| **P1** | 金币 sink 最小可行：渔具耐久消耗（C3 subset） | 2d |
| P2 | 图鉴系统（利用已有 codex.ts） | 3d |
| P2 | 评估 C 期优先级并排期 | 1d |

---

## 3. 需要立即关注的问题（高优先级）

### P0 — 影响生产稳定性

1. **无 Dockerfile / 主 docker-compose.yml** — 当前服务无法容器化部署，只能 `npm run dev` 方式运行

### P1 — 影响日常开发与运营效率

2. **零单元测试** — 核心状态机、概率计算、并发 timer 无隔离测试
3. **Mobile JWT token 管理缺失** — 客户端鉴权未完成
4. **Dev token 端点无保护** — 测试/预发布环境有风险
5. **gameState.ts 过大**（816 行）— 阻碍后续功能开发
6. **SQLite 备份缺失** — 数据全在单文件

### P2 — 后续迭代应注意

7. 双日志/指标 API — 新人上手 confusion
8. 性能基准缺失 — 无负载测试
9. 经济 sink 未开始 — 金币通胀风险

---

## 4. 建议的后续改进路线

### 短期（v0.6.x — 巩固期）

```
1. [1d] Dockerfile + 主 docker-compose.yml（服务 + 监控一体化）
2. [0.3d] .env 加载替换为 dotenv 包
3. [2d] Mobile JWT token 管理（客户端接入）
4. [2d] gameState.ts 拆分
5. [1d] 统一日志 API
```

### 中期（v0.7.x — 质量与测试）

```
6. [2d] 核心单元测试：fishingStateMachine + timerRegistry
7. [1d] CI 配置：GitHub Actions
8. [2d] 金币 sink 最小可行：渔具耐久消耗
9. [1d] 性能摸底：k6 负载测试
10. [0.5d] Dev token 端点保护
```

### 长期（v1.0 — C 期与经济）

```
11. [3d] 图鉴系统
12. [5d] 状态机动画接入（C6）
13. [3d] 遗传系统（C5）
14. [持续] 经济 sink 调优
15. [文档] PostgreSQL 迁移路线评估
```

---

## 5. 整体趋势

```
v0.4          v0.4.4        v0.5          v0.6 (当前)    目标 v1.0
│             │             │             │              │
C-            C+            B-            B+             A-
原始原型      加可观测      架构修复      Phase 1 完成    + 测试覆盖
无鉴权        phase 迁移    JWT/定时器    Loki/Prom       + 经济系统
无定时器管理   埋点补全      入口拆分      RBAC/SSE       + 容器化
无优雅停机                  优雅停机      Log 合规        + CI/CD
                           Session恢复   客户端日志
```

**关键结论**：后端已经从"能跑就行"进化到"可以运维但需要巩固质量"的阶段。当前最大的短板是**测试覆盖**和**容器化部署**，这两项补齐后将达到 A- 级别。
