# 开发提示词：运营平台入口体验（OPS-UX-1）

你是 Fish Social **前端 / 全栈开发 Agent**。按策划规格把运营平台从「说明书式入口」升级为「今日可干活」的台子。

## 必读

1. `docs/planning/specs/运营平台入口体验增强.md`（**已确认** / **OPS-UX-1**）
2. 现状：`运营平台.html` · `admin-web/src/App.tsx` · `scripts/ops/run-daily-analytics.ps1` · `scripts/analytics/daily-pipeline.mjs` · `scripts/verify-ops-portal-links.ts`

## 强制顺序

1. **阶段 A**：日批状态文件 + compact 告警字段 + 报告 `#alerts` + 今日运维条（KPI / 告警 / 日报 CTA / 服务摘要）  
2. **阶段 B**：每日必看 3 卡 + 工程区折叠 + Admin `?tab=&playerId=` 深链 + 入口快捷按钮  
3. **阶段 C**：归档分栏文案、刷新按钮、内嵌条抛光（可同 PR）

## 关键契约（勿改名）

- 状态文件：`docs/analytics/daily-batch-status.json`（字段见 spec §5.1）  
- compact 增加：`alertCount` / `alertBad` / `alertWarn`  
- Admin：`/admin-web/?tab=timeline&playerId=...` 等  

## 验收

- 对照 spec §4.1 A6、§4.2 B4、§8 总表勾选  
- 扩展 `npm run verify:ops-portal-links`（或新增 verify）覆盖今日条与深链约定  
- 完成后：spec → **已实现**；`build-master-plan-xlsx.py` + `npm run planning:master-xlsx`；CHANGELOG  

## 不要做

- 大盘 SPA / 替换 Admin / 改玩法埋点  

## 派发

```text
@docs/planning/prompts/ops-portal-ux-dev.prompt.md 按此实现 OPS-UX-1
```
