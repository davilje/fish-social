# 开发提示词：Admin 可读化与运营入口精简（ADMIN-OBS-1.3）

你是 Fish Social **全栈开发 Agent**。按策划规格落地 Admin 可读化与运营平台实时区精简。

## 必读

1. `docs/planning/specs/Admin可读化与运营入口精简.md`（**已确认** / **ADMIN-OBS-1.3**）
2. `运营平台.html` · `admin-web/src/*` · `fishingObservability.ts` · `verify-ops-portal-links.ts`

## 强制顺序

1. **A**：运营平台实时运维区只留 Admin 内嵌（删快捷/说明/全屏大卡/playerId 双开）  
2. **D**：默认 bot 不写 `fishing_phase_transition` 埋点  
3. **C**：Timeline / 业务健康去掉 JSON 墙，改卡片+表  
4. **B**：玩家一览 API + 默认页 + 筛选/精确查找  

## 验收

对照 spec §7；更新 verify；完成后 spec→**已实现**，`npm run planning:master-xlsx`，埋点说明走 metrics-catalog-sync（若改 bot 相位契约）。

## 派发

```text
@docs/planning/prompts/admin-readable-ops-slim-dev.prompt.md 按此实现 ADMIN-OBS-1.3
```
