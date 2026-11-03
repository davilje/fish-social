# 鱼类百科参考手册

资料核对日期：2026-11-03。用途：帮助理解真实鱼类的分类、生活史和成长，为后续游戏讨论提供参考；不是已确认的数值修改方案。

统一阅读入口：根目录`现行预览.html#fish-encyclopedia`，或本地服务`http://127.0.0.1:8769/现行预览.html#fish-encyclopedia`。

## 交付物

- `鱼类百科全书.md`：完整正文，含50个项目条目的分类索引与30个重点档案。重点档案包含本次核对的身份、生态或数值信息，并非每条都有完整生长年表。
- `index.html`：带章节导航、鱼种搜索、生态组筛选、来源链接和移动端布局的阅读页。无网络请求依赖，外部来源链接除外。
- `鱼种索引.json`：50条目，区分项目配置字段与生物学资料。
- `文献曲线重算.json`：两项原始研究提供的模型参数与1～5年重算结果；不是实测个体数据。
- `growth-populations.png`：两项水域群体模型的绝对、相对体长对比。
- `culture-times.png`：不同养殖阶段的时间量级。起终点不同，不用于跨物种速度排名。

## 更新方法

编辑`阅读正文.md`以及`build.py`内的档案、来源和数值。`build.py`生成完整Markdown、页面正文、图表与JSON，再由`render.mjs`生成HTML；生成物不应单独修改。

本机运行：

```sh
PYTHONPATH=/Users/red/Desktop/fish/tmp/growth-chart-deps /Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 docs/数据分析/fish-encyclopedia-2026-11-03/build.py
/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node docs/数据分析/fish-encyclopedia-2026-11-03/render.mjs
```

工具依赖为本机已有matplotlib/numpy与marked。脚本只读取项目鱼种配置，不改业务数据、不触碰数据库。

## 来源与限制

来源链接就近附在正文和档案内。原始研究模型适用于相应水域与调查时期；FAO历史养殖实例仅用于认识阶段与时间尺度；数据库常见长度、最大记录、成熟与模型估计不能互相替代。

本版没有为未确定的中文泛称硬绑定唯一物种，没有将TL、SL、FL无依据换算，也没有将克/公斤无依据换算为厘米。项目典型长度字段仅在档案展开区注明，不作为现实测量范围。
