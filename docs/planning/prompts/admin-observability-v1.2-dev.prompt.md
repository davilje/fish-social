<!-- 来源: docs/planning/specs/Admin-排障增强-v1.2.md（ADMIN-OBS-1.2） -->
<!-- 用途: admin-web + 手机 Debug + 可选 diag-pack — Admin 排障增强 P1/P2 -->

你是 Fish Social **全栈开发 Agent**（偏 admin-web + 后端小改）。实现 **Admin 排障增强 v1.2（ADMIN-OBS-1.2）**。

## 必读

1. [`docs/planning/specs/Admin-排障增强-v1.2.md`](../specs/Admin-排障增强-v1.2.md) — 权威范围与验收
2. [`docs/planning/specs/Admin-能力不足分析与补充方案.md`](../specs/Admin-能力不足分析与补充方案.md) — v1.1 背景；**MVP 已实现，勿重做**
3. 现有：`server/src/{admin,liveSessionInspector,fishingDebug,playerLiveState}.ts` · `admin-web/src/pages/*` · `mobile/components/AdminPondFishDebugGrid.tsx`

## 本次范围 = P1（必须）

| ID | 任务 |
|----|------|
| B1 | Timeline SOP 着色（disconnect / timeout / leave / checkpoint_restore / server_start） |
| B2 | admin-web + 手机 Debug 均渲染 activeFishers 锚点表 |
| B3 | human/bot 分列 +「仅真人」筛选 |
| B4 | Inspector 卡片：空锚点红字、checkpoint 摘要、最近关键事件 |

## 明确不做（除非用户点名做 P2）

- diag-pack、client-logs 上报、pushLiveSession、新运维 Tab（Logs/Config…）
- 改玩法 / 状态机 / 咬钩公式
- 破坏 v1.1 `live-state` 契约

## 完成后

- [ ] 自检 spec §5 P1 验收
- [ ] 扩展 `verify:admin-observability` 或新增 v1.2 检查
- [ ] 更新 spec 状态与 CHANGELOG（开发侧一条）
