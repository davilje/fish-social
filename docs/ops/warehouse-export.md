# BI 仓库 CSV 导出（D-L3-06）

## 用途

将每日**聚合级**运营数据导出为 CSV，便于 Excel 查看 ≥30 日趋势（无需打开 HTML 日报）。

**默认不含明文 `player_id`**。

## 运行

```bash
# 指定日期（需已有 summary / daily 表或先跑日批）
npm run analytics:export-warehouse -- --date=2026-07-05

# 随日批自动执行（末尾一步，失败不阻断 HTML）
npm run analytics:daily -- --date=2026-07-05
```

## 产出目录

```
docs/analytics/warehouse/
├── index.html           # 日期目录索引（导出时刷新）
├── YYYY-MM-DD/
│   ├── index.html       # 当日下载页
│   ├── daily_pond_stats.csv
│   ├── daily_kpi.csv
│   ├── daily_economy.csv
│   ├── daily_ecology.csv
│   └── manifest.json
└── latest/              # 最近一次导出副本
    ├── index.html       # 浏览入口（必有，否则 /latest/ 目录 URL 404）
    ├── *.csv
    └── manifest.json
```

经游戏服静态托管打开：`http://localhost:3001/analytics/warehouse/latest/`（运营平台「BI CSV」卡片同址）。

## Excel 30 日折线（示例）

1. 补跑或积累 ≥30 天 `warehouse/YYYY-MM-DD/daily_kpi.csv`
2. Excel → 数据 → 自文件夹 → 选择 `docs/analytics/warehouse`
3. 合并 `daily_kpi.csv`，以 `date_key` 为 X 轴、`kpi_daily_catch` 等为 Y 轴插入折线图

## 可选

- Metabase：将 `warehouse` 目录挂为 CSV 数据源（本仓库不强制 docker-compose）
- `WAREHOUSE_UPLOAD_URL`：未来可扩展 OSS 上传（当前未实现）

## 验收

```bash
npm run verify:ops-portal-links
npm run verify:data-platform-dp-d
npm run verify:daily-ops-report
```

浏览器：游戏服运行时打开 `http://localhost:3001/analytics/warehouse/latest/` 应见文件列表（非 404）。
