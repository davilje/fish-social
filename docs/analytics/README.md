# 鱼塘数据分析归档

每次运行模拟或导出后，分析结果会归档到 `runs/`，可在浏览器中对比查看。

## 快速开始

```bash
# 完整流水线：模拟 → compact → 分析 → HTML 报告 → 归档
npm run analytics:pond-day

# 仅基于已有 data.json 重建报告与归档
npm run analytics:build

# 运营日报（昨日 Asia/Shanghai，含 aggregate + 报告 + 索引）
npm run analytics:daily
# 补跑指定日期：npm run analytics:daily -- --date=2026-07-05
```

## 浏览器查看

| 页面 | 说明 |
|------|------|
| [../ops/运营平台.html](../ops/运营平台.html) | **运营总入口**（日报/运维台/健康检查） |
| [index.html](./index.html) | 归档索引，勾选多份报告后跳转对比 |
| [compare.html](./compare.html) | 多版本指标/时间线/结论对比 |
| [live-vs-sim.html](./live-vs-sim.html) | 线上 vs 模拟对照 |
| [pond-day-simulation/report.html](./pond-day-simulation/report.html) | 最新 24h 模拟报告 |
| [pond-ecology-initial/report.html](./pond-ecology-initial/report.html) | 初始生态报告 |

## 目录结构

```
docs/analytics/
├── index.html              # 归档索引
├── compare.html            # 对比页
├── manifest.json           # 归档清单（自动生成）
├── runs/
│   └── <date>-pond-day-<version>/
│       ├── meta.json       # 元数据
│       ├── compact.json    # 压缩数据
│       ├── analysis.json   # 分析指标
│       └── report.html     # 单份报告
└── pond-day-simulation/    # 最新产出（工作目录）
    ├── data.json
    ├── compact.json
    ├── analysis.json
    └── report.html
```

## 对比用法

1. 打开 `index.html`
2. 勾选 2 份及以上 **pond-day** 归档
3. 点击「对比已选」
4. 在对比页切换钓鱼人数/鱼塘，查看指标差异与时间线叠加
