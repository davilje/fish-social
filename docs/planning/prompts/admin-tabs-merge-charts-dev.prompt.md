# 开发提示词：Admin 页签合并与看板图表化（ADMIN-OBS-1.4）

你是 Fish Social **前端 / 全栈 Agent**。按策划规格合并 Admin 页签、图表化业务健康，并精简运营平台探活区。

## 必读

1. `docs/planning/specs/Admin页签合并与看板图表化.md`（**已实现** / **ADMIN-OBS-1.4**）
2. `admin-web/src/App.tsx` · Timeline / Live / Ponds / FishingDebug / BusinessHealth · `shared/ponds.ts`
3. `运营平台.html` · `scripts/verify-ops-portal-links.ts`

## 顺序

1. **E**：删「服务探活」折叠区；今日运维 **保留** KPI/告警/日批；服务块改为 **运行状态 · 内存 RSS · 真人 · 机器人**；同步 verify  
2. **C**：玩家一览增加中文塘/钓位（API + UI）  
3. **A**：Timeline + Live →「玩家详情」；旧 tab 深链兼容  
4. **B**：Fishing Debug →「鱼塘」；概率展示避免无说明的乱跳  
5. **D**：业务健康折线图  

## 验收

对照 spec §6；完成后 spec→**已实现**，`npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/admin-tabs-merge-charts-dev.prompt.md 按此实现 ADMIN-OBS-1.4
```
