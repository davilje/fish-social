<!-- 来源: docs/planning/specs/数据平台-DP-D-BI与合规交接.md -->
<!-- 用途: 后端 — D-L3-10 用户数据合规 · D-L3-06 BI 导出 -->

你是 Fish Social **后端开发 Agent**。实现数据平台 **DP-D** 仅剩两项。

## 必读

1. `docs/planning/specs/数据平台-DP-D-BI与合规交接.md`（**权威：范围、验收、顺序**）
2. `docs/planning/specs/数据平台-Phase2-稳定增长.md` §4 DP-D（摘要）
3. 已完成：运营日报 R1～R3、D-L3-01～05/07～09、D-L1-09；**勿重做**

## 实现顺序（严格）

### 1）D-L3-10 — 用户数据导出/删除脱敏（先做）

- `GET /api/admin/players/:playerId/export`
- `POST /api/admin/players/:playerId/erase` + **dry-run**
- metrics `player_id` 不可逆匿名化（HMAC + env pepper）
- 审计日志

### 2）D-L3-06 — BI / 数仓对接（后做）

- `scripts/analytics/export-warehouse.mjs` → 聚合 CSV（**默认无明文 playerId**）
- 挂入 `analytics:daily` 末尾（失败不阻断日报）
- `docs/ops/warehouse-export.md`
- Metabase **可选**，不挡验收（Excel 能看 30 日即可）

## 每项完成后（强制）

按 Skill `.cursor/skills/planning-progress-sync/SKILL.md`：

1. `build-master-plan-xlsx.py`：编号 → **已实现**，完成时间=当天  
2. `npm run planning:master-xlsx`  
3. 更新本交接 / Phase2 专文变更记录  

## 验收

```bash
npm run verify:data-platform-dp-d   # 新增
npm run analytics:export-warehouse -- --date=2026-07-05
npm run analytics:daily -- --date=2026-07-05
npm run verify:daily-ops-report     # 回归：日批未被 warehouse 弄挂
```

## 开工

从 **D-L3-10**（dry-run erase + export）开始实现。
