# Admin 排障增强 v1.2（ADMIN-OBS-1.2）

| 字段 | 内容 |
|------|------|
| 功能名称 | Admin 排障增强 v1.2 |
| 编号 | **ADMIN-OBS-1.2** |
| 状态 | **已实现**（P1；P2 未做） |
| 优先级 | P1（排障体验）· P2（运维闭环） |
| 目标版本 | v1.2 |
| 设计时间 | 2026-07-12 |
| 完成时间 | **2026-07-12** |
| 前置 | [`Admin-能力不足分析与补充方案.md`](./Admin-能力不足分析与补充方案.md)（**ADMIN-OBS-1.1 MVP 已实现**） |
| 关联 | [`排查-挂机断线诊断阶段2-4.md`](./排查-挂机断线诊断阶段2-4.md) · D-L2-13/14 |

---

## 1. 背景与目标

### 1.1 背景

v1.1 已落地：`live-state`、Inspector 读内存、`server_start/stop`、锚点修复、admin-web 关键映射修复。  
自助确认「计时卡死 / 进程重启」的北极星已达成。

仍缺口（原方案 §5.6 / A-07~A-15）：Timeline 着色不完整、activeFishers UI 不全、Bot 观感污染、无诊断包、client-logs 与剩余 Admin API 无 Web 面板。

### 1.2 目标

- 排障员在 **Timeline + Inspector + Debug** 三处形成一致着色与人机分列
- 一键导出诊断包，减少多 Tab 手工拼凑
- （P2）客户端日志可进 Admin；常用运维 API 有只读 Web 入口

### 1.3 非目标

- 不改玩法 / 咬钩公式 / 状态机枚举
- 不替换运营日报、不新建 Grafana 大盘
- 不实现多实例 / Redis（见 R2-3 / 阶段 4）
- 不重做 v1.1 已验收接口契约（可扩展字段，禁止破坏性改名）

---

## 2. 用户与场景

| 角色 | 场景 | 期望 |
|------|------|------|
| 开发 | 弱网断线后看 Timeline | disconnect / reconnect / checkpoint_restore / server_start **按 SOP 着色** |
| 策划 | 看某塘谁在钓 | activeFishers 表可见；human / bot 分列 |
| 运维 | 交一份排障材料给同事 | 一键 diag-pack（json）含 live-state + 24h timeline + checkpoint |

---

## 3. 功能范围

### 3.1 P1（本迭代必须）

| ID | 功能点 | 说明 |
|----|--------|------|
| B1 | Timeline SOP 着色 | 对齐挂机 SOP：短暂断线黄、超时红、离塘灰、进程恢复紫/橙、计时风险红 banner |
| B2 | activeFishers UI | admin-web Fishing Debug **与** 手机 `AdminPondFishDebugGrid` 均渲染锚点字段表 |
| B3 | Bot 分列 | 鱼塘概览 / Inspector `pondUsers` 明确 humanCount / botCount；列表可筛选「仅真人」 |
| B4 | Inspector 卡片强化 | Phase / SessionMs / StartedAt 空值红字；Checkpoint 摘要；最近 10 条关键事件 |

### 3.2 P2（可顺延）

| ID | 功能点 | 说明 |
|----|--------|------|
| C1 | 一键诊断包 | `GET /api/admin/players/:id/diag-pack` → JSON（或 zip）；含 live-state、timeline(24h)、checkpoint、近 50 条 error |
| C2 | client-logs 上报 | mobile 在 `__DEV__` 或持 Admin Key 时批量 `POST /api/client-logs`；Admin 可查 |
| C3 | 激活 `pushLiveSession` | phase / 计时变更时推送，降低 SSE 纯轮询 |
| C4 | 只读运维 Tab | Logs / Config / Gray / Privacy（只读优先） |

---

## 4. 技术影响

### 4.1 API

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/admin/players/:playerId/diag-pack` | P2；复用 live-state + timeline 查询 |
| REST | 现有 client-logs | P2；补 mobile 上报器 |
| 内部 | `pushLiveSession` | P2；接 sessionTimer / phase 变更 |

### 4.2 涉及文件（预估）

- `server/src/liveSessionInspector.ts`、`admin.ts`、`fishingDebug.ts`
- `admin-web/src/pages/{Timeline,LiveInspector,FishingDebug,Ponds}Page.tsx`
- `mobile/components/AdminPondFishDebugGrid.tsx`
- （P2）`mobile` clientLogger · 新 admin-web Tab

---

## 5. 验收标准

### P1

- [x] Timeline 对 `disconnect` / `reconnect` / `disconnect_timeout` / `leave_pond` / `checkpoint_restore` / `server_start` 有可区分着色
- [x] admin-web 与手机 Debug 均能看到 activeFishers 的 `fishingStartedAt` / `sessionFishingMs` / `fishingPhase`
- [x] 鱼塘概览或 Inspector 可见 human / bot 计数，且可筛「仅真人」
- [x] Inspector 在 `fishingStartedAt=null` 且 status=fishing 时红字告警（可与 live-state diagnostics 联动）

### P2

- [ ] diag-pack 下载/接口返回完整四块数据
- [ ] `__DEV__` 下客户端日志可出现在 Admin 查询结果
- [ ] 至少新增一个只读运维 Tab（Logs 或 Config）

### 验证脚本

- 扩展或新增 `npm run verify:admin-observability`（v1.2 用例：着色常量 / diag-pack 路由 / activeFishers UI 关键字）

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| Timeline 事件类型不全 | 缺类型时灰底 + raw type，不静默吞掉 |
| diag-pack 体积过大 | 默认 24h + error 50 条；query 可缩窗 |
| Bot 筛选误伤排障 | 默认「全部」，筛选为显式开关 |

---

## 7. 开发交接

**推荐顺序**：B1 → B4 → B2 → B3 →（P2）C1 → C2 → C3 → C4

**开发提示词**：[`docs/planning/prompts/admin-observability-v1.2-dev.prompt.md`](../prompts/admin-observability-v1.2-dev.prompt.md)

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-12 | 初稿：从 v1.1 方案拆出 P1/P2 续作规格 |
| 2026-07-12 | **P1 已实现**：SOP 着色 · 双端 activeFishers · 仅真人筛选 · Inspector 强化；`verify:admin-observability` 扩展 |
