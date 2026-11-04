<!-- fishing-rule-status: historical-analysis; reviewed: 2026-11-04 -->
> **历史方案／实验，非现行要求。** 本文保留当时参数与结论供追溯，不能用于验收当前成长或预测现行鱼获；正文旧复现命令已停用。请先看[现行规则](../../策划/规格/玩法/钓鱼系统现行规则.md)，现行成长T80为1／2／3／5／7／10／14天，以入塘口径和实际共享实现为准。

# 鱼塘初始生态分析报告

由 `scripts/export-pond-ecology-report.ts` 自动生成，对应 Canvas「鱼塘初始生态 · 体型 · 品质 · 成长」。

## 文件

| 文件 | 说明 |
|------|------|
| [report.html](report.html) | 完整报告（图标墙 + 图表 + 表格），浏览器直接打开 |
| [data.json](data.json) | 原始数据（四塘品质/体长分布 + 成长曲线点） |
| [charts/](charts) | 静态 SVG 图表 |

## 重新生成

```bash
npm run export:pond-ecology
```

生成时间：2026-07-01T19:13:30.942Z
