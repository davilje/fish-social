# Phase 2 — 全功能补齐与基础设施加固（v0.7 ~ v0.9）

| 字段 | 内容 |
|------|------|
| 状态 | **已确认** |
| 目标版本 | v0.7.x ~ v0.9.x |
| 前置 | **BUG-11（tsx watch 启动挂死）已修复且本地 `/health` 可用** · Phase 1（可观测性增强与运维平台）全部实现并验证通过 · 架构修复 R0~R2 全部实现 · 数值/生态 NUM-01~06 全部实现 · FEAT-01~04 全部实现 |

---

## 1. 背景与目标

### 1.1 背景

Phase 1 已完成可观测性增强。DP-A（OTel / Socket Tap / 业务健康看板）**已实现**。  
剩余开放项排期与风险见专文：[Phase2-剩余事项设计与风险.md](./Phase2-剩余事项设计与风险.md)（2026-07-12 **已确认**）。

当前仍开放：

1. **代码工程化**：gameState 拆分、统一日志 API、安全加固、单测/CI（ARC-08~11）
2. **核心玩法**：FEAT-05（C1/C2/C3/C5/C6/C7；C4 不做）
3. **数据平台剩余**：DP-B/C/D（幂等、admin-web、PG、L3、BI、合规）
4. **遗留 Bug**：BUG-08 F1 Modal 计时

已完成不在此列：Docker、Mobile JWT、DP-A、BUG-11。

### 1.2 目标

- **部署标准化**：Docker 多阶段构建 + docker-compose + env 模板，一键部署
- **鉴权闭环**：移动端 JWT 存储/续期/携带，杜绝伪造 playerId
- **代码质量**：gameState 拆分、统一日志 API、安全中间件、单元测试 + CI
- **核心玩法补齐**：热更配置、图鉴系统、完整状态机与阶段动画、Bot 适配新状态机、金币 Sink
- **数据平台扩展**：OpenTelemetry 追踪、Socket 全事件 Tap、PG 迁移就绪、业务健康检查、数据去重
- **运维效率提升**：admin-web 独立运维界面、L3 分析流水线（用户模型/生态报表/BI 对接等）
- **Bug 修复**：F1 Modal 计时排查

### 1.3 非目标

- FEAT-05-C4 繁衍品质遗传（产品明确前不做）
- R2-3 真实玩家隔离（已文档化）
- iOS App Store / Google Play 发布流程
- OAuth / 第三方登录 / 支付系统

---

## 2. Phase 2 范围总览（23 项）

### 2.1 A组 — 部署与基础鉴权（2 项，P0，5d）

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| ARC-06 | Docker 容器化部署 | **P0** | 3d | 无 |
| ARC-07 | Mobile JWT Token 管理 | **P0** | 2d | R0-1（已实现） |

### 2.2 B组 — 工程化与质量保障（5 项，P1~P2，10d）

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| ARC-08 | gameState.ts 拆分 | P1 | 2d | 无 |
| ARC-09 | 统一日志/指标 API | P1 | 2d | 现有 logger.ts |
| ARC-10 | 安全加固补完 | P1 | 2d | 建议 ARC-06 后 |
| ARC-11 | 单元测试 + CI | P1 | 3d | 建议 ARC-08/09 后 |
| BUG-08 | F1 Modal 计时排查 | P2 | 1d | 无 |

### 2.3 C组 — C 期核心玩法（6 子任务，P2，18d）

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| C1 | 数值调优基建（热更配置） | P2 | 3d | 无 |
| C5 | 图鉴/钓鱼日志 | P2 | 3d | B0/B1（已实现） |
| C7 | 灰度指标监控 | P2 | 2d | 无（与 C1 并行） |
| C6 | 完整状态机与阶段动画 | P2 | 5d | C1 + 引用《状态机需求描述.md》 |
| C2 | Bot 深度适配 | P2 | 2d | C6 |
| C3 | 金币 Sink 二期 | P2 | 3d | B0/B1 上线 1 月后 |

> C4（繁衍遗传）不做。以上 6 子任务均归属 FEAT-05。

### 2.4 D组 — 数据平台可观测性扩展（见专文）

> **已拆出专文**：[数据平台-Phase2-稳定增长.md](./数据平台-Phase2-稳定增长.md)（2026-07-12 **已确认**）。  
> 含 D-L1-10/12 · D-L2-04/09/10/14 · D-L3-02~10，Sprint DP-A~D。产品 Phase 2 Sprint 5 对齐 DP-A/B。

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| D-L1-10 | OpenTelemetry 分布式追踪 | P1 | 3d | Phase 0/1 |
| D-L1-12 | Socket 事件 Tap | P1 | 2d | D-L1-11（已实现） |
| D-L2-09 | 业务健康看板 | P1 | 1d | daily_*（已实现） |
| D-L2-10 | eventId 幂等 | P1 | 2d | — |

### 2.5 E组 — 存储与运维台

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| D-L2-04 | Metrics 存储演进（PG/Timescale） | P2 | 5d | D-L2-02/03 |
| D-L2-14 | admin-web 运维台 | P1 | 3d | D-L2-06/13 |

### 2.6 F组 — L3 分析流水线（原 Phase 1 欠账）

| 编号 | 标题 | 优先级 | 预估工期 | 依赖 |
|------|------|--------|----------|------|
| D-L3-02 | 线上实测 vs 模拟对照 | P1 | 2d | D-L3-01 |
| D-L3-03 | 留存与会话时长分析 | P1 | 2d | D-L2-03 |
| D-L3-04 | 经济 faucet/sink 日报 | P1 | 2d | D-L3-01 |
| D-L3-05 | 鱼塘生态健康日报 | P1 | 2d | D-L3-01 |
| D-L3-06 | BI / 数仓对接 | P2 | 3d | D-L3-02~05 |
| D-L3-07 | 规则版本切分分析 | P1 | 1d | — |
| D-L3-08 | 运营指标告警 | P1 | 2d | D-L3-01/05 |
| D-L3-09 | analytics 索引打通 | P1 | 2d | D-L3-01 |
| D-L3-10 | 用户数据导出/删除脱敏 | P2 | 1d | D-L1-09 |

---

## 3. 详细任务拆解

### 3.1 ARC-06：Docker 容器化部署（P0，3d）

**任务**：
1. 服务端 Dockerfile（多阶段构建，node:20-alpine）
2. docker-compose.yml（server + sqlite 持久卷 + 监控可选）
3. .env.example（全部环境变量模板）
4. npm scripts: `docker:build`, `docker:up`, `docker:down`

**验收**：
- `docker compose up -d` 一键启动
- `http://localhost:3001/health` 返回 200
- 容器重启 SQLite 数据不丢失
- 镜像体积 < 300MB

### 3.2 ARC-07：Mobile JWT Token 管理（P0，2d）

**任务**：
1. 移动端 token 存储（expo-secure-store）
2. 登录后自动获取 token（`POST /api/auth/dev-token`）
3. apiClient 自动附加 `Authorization: Bearer <token>`
4. Token 过期自动续期（提前 5 分钟刷新）
5. Socket.io 连接携带 `auth.token`

**验收**：
- 登录后 token 存入 SecureStore
- 每次 API 请求携带 Authorization 头
- Token 过期自动续期

### 3.3 ARC-08：gameState.ts 拆分（P1，2d）

**任务**：
1. 拆分为 `pondSession.ts`（会话管理）+ `pondUserManager.ts`（用户索引）+ `pondChat.ts`（聊天气泡）
2. `gameState.ts` 保留为外观层 re-export

**验收**：
- gameState.ts 行数减少 50%+
- 三个新模块单一职责
- `npm run verify:*` 全部通过

### 3.4 ARC-09：统一日志/指标 API（P1，2d）

**任务**：
1. 全局搜索 `logInfo(`, `logWarn(`, `logError(` 替换为 `logStructuredEvent`
2. 旧函数标记 `@deprecated`
3. metrics 写入统一到 metricsPrometheus.ts

**验收**：
- 无 `logInfo(/logWarn(` 调用残留
- 所有日志走 `logStructuredEvent`，结构一致

### 3.5 ARC-10：安全加固补完（P1，2d）

**任务**：
1. express-rate-limit 中间件（全局 100 req/s, auth 10 req/min）
2. 全局 Max Connections（默认 200）
3. Dev-token 仅 localhost 可用

**验收**：
- 超限返回 429
- Socket 连接超限断开
- Dev-token 生产环境不可用

### 3.6 ARC-11：单元测试 + CI（P1，3d）

**任务**：
1. vitest 配置（server + shared）
2. 状态机迁移测试（fishingStateMachine.ts）
3. Timer registry 测试（timerRegistry.ts）
4. GitHub Actions CI（build:shared → lint → test → verify:*）

**验收**：
- 核心模块测试覆盖率 ≥ 60%
- `npm test` 运行全部测试
- CI push/PR 自动触发

### 3.7 BUG-08：F1 Modal 计时排查（P2，1d）——已实现（2026-08-10）

**任务**：
1. 排查 Modal 打开时 `sessionFishingMs` 是否持续计算
2. 若计时停止则修复

**验收**：
- Modal 打开时钓鱼计时持续累加
- Modal 关闭后状态正确恢复
- `npm run verify:engineering` 通过，门禁按客户端 `isFishingActive` 与服务端 `SESSION_TIMER_PHASES` 语义校验

### 3.8 C1~C7

详见 [C-调优与状态机.md](./C-调优与状态机.md) 及 [状态机需求描述.md](./状态机需求描述.md)。

### 3.14~3.20 数据平台 P2 扩展

详见各 D-L 编号的描述。

---

## 4. 实施路线图

```
Sprint 1 (5d): 部署与鉴权
  ARC-06 Docker 容器化 (3d)
  ARC-07 Mobile JWT (2d)         ← 可并行

Sprint 2 (10d): 工程化与质量
  ARC-08 gameState 拆分 (2d)
  ARC-09 统一日志 API (2d)       ← 与 ARC-08 并行
  ARC-10 安全加固 (2d)           ← 与 ARC-08 并行
  BUG-08 F1 Modal (1d)           ← 前端并行
  ARC-11 单元测试+CI (3d)        ← Sprint 2 末

Sprint 3 (8d): C 期 Part 1
  C1 热更配置 (3d)
  C5 图鉴 (3d)                   ← 与 C1 并行
  C7 灰度监控 (2d)               ← 与 C1 并行

Sprint 4 (10d): C 期 Part 2
  C6 状态机+动画 (5d)            ← 依赖 C1
  C2 Bot 适配 (2d)              ← 依赖 C6
  C3 金币 Sink (3d)              ← 与 C6 并行

Sprint 5 (15d): 数据平台扩展
  D-L1-10 OTel (3d)
  D-L1-12 Socket Tap (2d)
  D-L2-09 业务健康 (1d)
  D-L2-10 数据去重 (2d)
  D-L2-04 PG 迁移 (5d)
  D-L2-14 admin-web (3d)
  D-L3-02~10 分析 (贯穿)
```

---

## 5. 验收总则

```bash
npm run build:shared
npm run verify:server-observability
npm run verify:auth
npm run verify:session-checkpoint
npm run verify:phase1-core
npm run verify:deploy               # Sprint 1
npm run verify:engineering          # Sprint 2
npm run verify:c-phase-part1        # Sprint 3
npm run verify:c-phase-part2        # Sprint 4
npm run verify:data-platform-p2     # Sprint 5
```

---

## 6. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-12 | 策划 | 数据平台 Phase2 专文定稿；D/E/F 组对齐三层清单命名；15 项设计时间 2026-07-12 |
| 2026-07-12 | 策划 | 增加硬性前置 BUG-11；Kickoff 见 [phase2-开发Kickoff.md](./phase2-开发Kickoff.md) |
| 2026-07-11 | 策划 | Phase 2 初稿：23 项需求分 6 组，5 Sprint 规划 |
