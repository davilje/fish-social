# 鱼塘初始生态分析报告

由 `scripts/export-pond-ecology-report.ts` 自动生成，对应 Canvas「鱼塘初始生态 · 体型 · 品质 · 成长」。

## 文件

| 文件 | 说明 |
|------|------|
| [report.html](./report.html) | 完整报告（图标墙 + 图表 + 表格），浏览器直接打开 |
| [data.json](./data.json) | 原始数据（四塘品质/体长分布 + 成长曲线点） |
| [charts/](./charts/) | 静态 SVG 图表 |

## 重新生成

```bash
npm run export:pond-ecology
```

生成时间：2026-07-01T19:13:30.942Z
