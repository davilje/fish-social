# 历史钓鱼脚本原文

2026-11-04。下列脚本基于已被替代的规则，不再作为当前验收或模拟入口。原文以`.txt`保存，避免误执行；原路径保留清晰报错。文件摘要登记在[现行规则登记](../../fishing-rules/current-rules.json)，用于校验保留内容未丢失。

当前入口： [现行规则](../../../docs/策划/规格/玩法/钓鱼系统现行规则.md)。使用 `npm run verify:fishing-current`、`npm run report:fishing-current`；成长压力模型为 `npm run simulate:fish-entry`，不是完整鱼获预测。

| 原入口 | 历史原文 |
| --- | --- |
| `scripts/a0-verify.ts` | [scripts__a0-verify.ts.txt](scripts__a0-verify.ts.txt) |
| `scripts/simulate-pond-day.ts` | [scripts__simulate-pond-day.ts.txt](scripts__simulate-pond-day.ts.txt) |
| `scripts/pond-viz-data.ts` | [scripts__pond-viz-data.ts.txt](scripts__pond-viz-data.ts.txt) |
| `scripts/export-pond-ecology-report.ts` | [scripts__export-pond-ecology-report.ts.txt](scripts__export-pond-ecology-report.ts.txt) |
| `scripts/generate-bite-escape-report.ts` | [scripts__generate-bite-escape-report.ts.txt](scripts__generate-bite-escape-report.ts.txt) |
| `docs/数据分析/fish-growth-opening-2026-11-02/analyze.py` | [docs__数据分析__fish-growth-opening-2026-11-02__analyze.py.txt](docs__数据分析__fish-growth-opening-2026-11-02__analyze.py.txt) |
| `docs/数据分析/fish-growth-opening-2026-11-02/simulate_harvest.py` | [docs__数据分析__fish-growth-opening-2026-11-02__simulate_harvest.py.txt](docs__数据分析__fish-growth-opening-2026-11-02__simulate_harvest.py.txt) |
| `docs/数据分析/fish-growth-opening-2026-11-02/simulate_full_pond.py` | [docs__数据分析__fish-growth-opening-2026-11-02__simulate_full_pond.py.txt](docs__数据分析__fish-growth-opening-2026-11-02__simulate_full_pond.py.txt) |
| `docs/数据分析/fish-growth-opening-2026-11-02/analyze_occupancy_cycles.py` | [docs__数据分析__fish-growth-opening-2026-11-02__analyze_occupancy_cycles.py.txt](docs__数据分析__fish-growth-opening-2026-11-02__analyze_occupancy_cycles.py.txt) |
| `docs/数据分析/fish-growth-opening-2026-11-02/compare_restock_50_40_10.py` | [docs__数据分析__fish-growth-opening-2026-11-02__compare_restock_50_40_10.py.txt](docs__数据分析__fish-growth-opening-2026-11-02__compare_restock_50_40_10.py.txt) |
| `docs/数据分析/fish-growth-45m-2026-11-01/simulate.py` | [docs__数据分析__fish-growth-45m-2026-11-01__simulate.py.txt](docs__数据分析__fish-growth-45m-2026-11-01__simulate.py.txt) |
| `docs/数据分析/fish-growth-2026-11-01/build_chart.py` | [docs__数据分析__fish-growth-2026-11-01__build_chart.py.txt](docs__数据分析__fish-growth-2026-11-01__build_chart.py.txt) |

不提供默认历史重跑开关；研究旧模型时应在独立历史输出目录中另建有历史标识的实验，禁止覆盖现行报告或充当真实日报基准。
