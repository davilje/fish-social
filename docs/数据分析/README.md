# 鱼塘数据分析与历史归档

先看[钓鱼系统现行规则](../策划/规格/玩法/钓鱼系统现行规则.md)。旧模拟与校准保留为历史资料，不能当作最新需求、实际鱼获预测或当前服务验收依据。

## 快速开始

```bash
# 现行规则、版本和隔离库回归
npm run verify:fishing-current

# 当前概率说明（静态计算，不是每小时鱼获仿真）
npm run report:fishing-current

# 当前成长与补鱼压力模型（外部给定捕获负载，不是实际产量预测）
npm run simulate:fish-entry

# 运营日报（昨日 Asia/Shanghai，含 aggregate + 报告 + 索引）
npm run analytics:daily
# 补跑指定日期：npm run analytics:daily -- --date=2026-07-05
```

`simulate:pond-day`、`export:pond-ecology`、`report:bite-escape`、`verify:a0`及旧模拟报告重建链已明确停用，执行会报错并提示现行入口。旧原文保存在[历史脚本目录](../../scripts/legacy/fishing-2026-11-04/README.md)。真实运营日报继续使用实际数据；缺少规则版本匹配的完整流程模拟时，模拟基准显示未建立，不套用历史模型。

## 浏览器查看

| 页面 | 说明 |
|------|------|
| [../运维/运营平台.html](../运维/运营平台.html) | **运营总入口**（日报/运维台/健康检查） |
| [index.html](index.html) | 归档索引，勾选多份报告后跳转对比 |
| [compare.html](compare.html) | 多版本指标/时间线/结论对比 |
| [live-vs-sim.html](live-vs-sim.html) | 线上 vs 模拟对照 |
| [../../现行预览.html#fish-rules](../../现行预览.html#fish-rules) | 当前有效需求、现状、兼容和历史边界 |
| [../../现行预览.html#fish-probability](../../现行预览.html#fish-probability) | 当前概率审计 |
| [../../现行预览.html#fish-growth](../../现行预览.html#fish-growth) | 当前成长与补鱼实现、压力模型及未部署事项 |
| [鱼塘日模拟/report.html](鱼塘日模拟/report.html) | 历史24小时模拟，参数已被替代 |
| [鱼塘生态初值/report.html](鱼塘生态初值/report.html) | 历史初始生态报告 |

## 目录结构

```
docs/数据分析/
├── index.html              # 归档索引
├── compare.html            # 对比页
├── manifest.json           # 归档清单（自动生成）
├── 运行记录/
│   └── <date>-pond-day-<version>/
│       ├── meta.json       # 元数据
│       ├── compact.json    # 压缩数据
│       ├── analysis.json   # 分析指标
│       └── report.html     # 单份报告
└── 鱼塘日模拟/    # 历史模型原始产出（不再覆盖为现行）
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
