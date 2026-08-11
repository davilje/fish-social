# 数据平台 DP-D — BI 导出与用户数据合规（需求交接）

| 字段 | 内容 |
|------|------|
| 状态 | **已实现**（2026-07-12） |
| 设计时间 | **2026-07-12** |
| 编号 | **D-L3-06** · **D-L3-10** |
| 优先级 | P2 |
| 目标版本 | 数据平台稳定增长 · DP-D |
| 前置 | 运营日报 R1～R3 / D-L3-01～05、07～09 **已实现** · D-L1-09 日志合规 **已实现** |
| 关联 | [`数据平台-Phase2-稳定增长.md`](./数据平台-Phase2-稳定增长.md) · 路线图 sheet `D-L3-06-BI对接` / `D-L3-10-数据合规` |
| 不做 | 实时数仓、多租户、OAuth、应用商店上架、千人多机扩容 |

---

## 0. 背景（策划可读）

| 编号 | 一句话 |
|------|--------|
| **D-L3-06** | 把每日运营数字**导出成表格文件**，方便用 Excel / 可选 BI 工具自己看 30 日趋势（不只靠 HTML 日报）。 |
| **D-L3-10** | 玩家要**导出自己的数据**或**删号**时：能打包给他；删号后指标里对不上真人（脱敏）。 |

**建议实现顺序：先 D-L3-10，再 D-L3-06**（导出文件不要带可还原身份；至少仓库导出默认脱敏或仅聚合表）。

---

## 1. D-L3-10 — 用户数据导出 / 删除脱敏

### 1.1 目标

1. **导出**：Admin（或授权角色）可导出指定玩家的数据包。  
2. **删除/脱敏**：删号后业务数据清理；`fishing_metrics` 等分析表中的 `player_id` **不可逆匿名化**，保留统计价值。  
3. **审计**：谁导出/谁删号、何时、影响行数必须可查。

### 1.2 导出包内容（最低集）

| 区块 | 来源（示意） | 说明 |
|------|--------------|------|
| profile | `players` | id、昵称、创建时间等公开资料字段 |
| inventory | `inventory` | 当前渔获列表（可截断大字段） |
| gear | gear 相关表 | 渔具/饵状态 |
| codex | 图鉴表 | 解锁记录 |
| social | friends / posts / dm 摘要 | 可不含对方全文，只含与己相关 ID 列表 |
| metrics_summary | `fishing_metrics` 聚合 | 按日计数，**导出给玩家本人时可含原 playerId**；给第三方/仓库用必须脱敏 |

产出格式：`JSON` 单文件或 `ZIP(JSON+README.txt)`。  
路径建议：`GET /api/admin/players/:playerId/export`（`requireRole('admin')` 或 `operator`+二次确认）。

### 1.3 删除 / 脱敏流程

`POST /api/admin/players/:playerId/erase`

| 步骤 | 行为 |
|------|------|
| 0 | **dry-run**（`?dryRun=1` 或 body）：只返回将删/将改行数，不写库 |
| 1 | 踢下线 / 清 session / pending / 塘内状态 |
| 2 | 删或清空业务表：inventory、gear、codex、friends、posts、dm、player 行等（清单写死在代码+文档） |
| 3 | **匿名化** `fishing_metrics.player_id`（及同类列）：`anon_` + HMAC/SHA256(playerId + 服务端 pepper)，不可逆 |
| 4 | 写 `admin_audit` / 结构化日志：`player_erase` · dryRun · counts |
| 5 | 响应：`{ ok, dryRun, anonymizedRows, deletedTables }` |

**保留**：按日聚合 `daily_*` 若仅含聚合、无明文 playerId 可保留；若含 playerId 同步匿名化。

### 1.4 非目标

- 玩家自助一键删号 UI（可后置；本需求先 Admin API）  
- 物理粉碎备份盘（运维流程另述）  
- 跨产品统一隐私中心  

### 1.5 验收

| # | 标准 |
|---|------|
| 1 | dry-run 返回预计行数且库不变 |
| 2 | 正式 erase 后：`GET .../timeline?playerId=原ID` 无真人可关联事件（或仅 anon） |
| 3 | export 返回完整最低集且有审计日志 |
| 4 | 误用无效 playerId → 4xx，无副作用 |
| 5 | `npm run verify:data-platform-dp-d`（或等价）覆盖 dry-run + erase + export |

### 1.6 风险护栏

- 生产 erase 前必须有 DB 备份（文档写进 `docs/ops/`）  
- pepper 仅环境变量，禁止提交仓库  
- 禁止在 erase 中「顺手」清空全表  

---

## 2. D-L3-06 — BI / 数仓对接

### 2.1 目标（MVP，可交付）

每日（或随 `analytics:daily`）导出**聚合级** CSV，运营用 Excel / 可选 Metabase 看 ≥30 日趋势。

**首版不做**：完整 ClickHouse/BigQuery 实时同步（可作为可选 Phase+，不挡验收）。

### 2.2 导出内容（最低集）

目录建议：`docs/analytics/warehouse/YYYY-MM-DD/` 或滚动 `docs/analytics/warehouse/latest/` + 按日归档。

| 文件 | 内容 | 身份字段 |
|------|------|----------|
| `daily_pond_stats.csv` | 塘日钓获、bite、断线、人口等 | 无 playerId |
| `daily_kpi.csv` | 来自运营日报 summary 的 KPI 扁平行 | 无 playerId |
| `daily_economy.csv` | faucet/sink/net（若日报已有） | 无 playerId |
| `daily_ecology.csv` | 各塘人口率/品质（若有） | 无 playerId |

**禁止**默认导出带明文 `player_id` 的明细 metrics；若提供「明细导出」须：

- 仅 Admin + 审计，或  
- 使用已匿名化 ID，且文档标明敏感级  

脚本：`scripts/analytics/export-warehouse.mjs`  
npm：`analytics:export-warehouse`；并由 `analytics:daily` 末尾调用（失败 log warn，不阻断日报 HTML）。

### 2.3 可选增强（非验收阻断）

| 项 | 说明 |
|----|------|
| Parquet | 同结构另存；无库时可只做 CSV |
| Metabase | docker-compose profile 或独立文档「如何挂 CSV 文件夹」；验收可用 Excel 代替 |
| OSS 上传 | `WAREHOUSE_UPLOAD_URL` 可选 |

### 2.4 验收

| # | 标准 |
|---|------|
| 1 | `npm run analytics:export-warehouse -- --date=YYYY-MM-DD` 产出上表 CSV |
| 2 | 连续补跑 ≥3 日目录齐全；手工或脚本能拼出 ≥3 点趋势 |
| 3 | CSV **默认无明文 playerId** |
| 4 | 文档：`docs/ops/warehouse-export.md`（如何跑、如何用 Excel 做 30 日折线） |
| 5 | 与日批联跑：`analytics:daily` 后 warehouse 目录更新 |

---

## 3. 实现顺序与工期

```
D1～D2  D-L3-10  dry-run + erase + export API + verify
D3～D4  D-L3-06  export-warehouse.mjs + daily 串联 + ops 文档
D5      verify:data-platform-dp-d + 补跑样例日
```

预估合计约 **5 人日**（与 Phase2 专文 DP-D 一致）。

---

## 4. 代码落点（建议）

| 模块 | 路径 |
|------|------|
| 导出/删号 API | `server/src/admin.ts` 或 `server/src/playerPrivacy.ts` |
| 匿名化工具 | `server/src/playerAnonymize.ts`（HMAC pepper） |
| 仓库导出 | `scripts/analytics/export-warehouse.mjs` |
| 日批挂钩 | `scripts/analytics/daily-pipeline.mjs` |
| 验收 | `scripts/verify-data-platform-dp-d.ts` |
| 运维 | `docs/ops/player-erase.md` · `docs/ops/warehouse-export.md` |

---

## 5. 开发交接口令

```
@docs/planning/prompts/data-platform-dp-d-dev.prompt.md 先 D-L3-10 再 D-L3-06；权威 docs/planning/specs/数据平台-DP-D-BI与合规交接.md
```

完成后按 Skill `planning-progress-sync` 将两编号标 **已实现** 并 `npm run planning:master-xlsx`。

---

## 6. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-12 | DP-D 实现：D-L3-10 export/erase + D-L3-06 warehouse CSV + verify:data-platform-dp-d |
| 2026-07-12 | 初稿：数据平台仅剩 D-L3-06/10；完成 DP-D 需求设计与验收；设计时间 2026-07-12 |
