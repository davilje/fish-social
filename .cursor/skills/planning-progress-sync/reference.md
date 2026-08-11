# planning-progress-sync — 参考细节

## 新需求文件清单

```
docs/planning/specs/<功能名>.md                 # 主 spec，状态=已确认 / 已实现
docs/planning/specs/vX.Y.Z-开发交接.md           # 可选：含交接提示词代码块
docs/planning/prompts/<slug>-dev.prompt.md       # A4：刷新看板后必写并交付用户
scripts/planning/build-master-plan-xlsx.py       # 计划表行权威源（必改）
项目开发需求计划表.xlsx（仓库根）               # master-xlsx 生成
策划进度看板.html（仓库根）                     # master-xlsx 生成
docs/planning/ 下同名 xlsx/html                  # 同步副本
docs/planning/CHANGELOG.md
docs/planning/specs/README.md
docs/planning/prompts/README.md                  # 登记新 prompt 一行
```

## 开发提示词结构

```markdown
# 开发提示词：<短标题>（<编号>）

你是 Fish Social **前端 / 后端 Agent**（按范围改）。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/<功能名>.md`（**已确认** / **<编号>**）
2. （相关已实现口径 / 依赖 spec）
3. （预估关键文件路径）

## 顺序

1. …
2. …
3. verify / 自检

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [ ] 相关 `npm run verify:*`（若有）
- [ ] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

\`\`\`text
@docs/planning/prompts/<slug>-dev.prompt.md 按此实现 <编号>
\`\`\`
```

## 计划表行模板（复制用）

```python
['已确认', 'PREFIX-01', '功能', '短标题', '—', 'hotfix', 'P0',
 '一句话说明', 'docs/planning/specs/某功能.md', '2026-07-19', ''],
```

完成后改为：

```python
['已实现', 'PREFIX-01', '功能', '短标题', '—', 'hotfix', 'P0',
 '一句话说明', 'docs/planning/specs/某功能.md', '2026-07-19', '2026-07-20'],
```

类型常用值：`功能` · `数据平台` · `架构` · `运维` · `Bug修复` · `后端优化`

## Spec 元信息最小集

| 字段 | 内容 |
|------|------|
| 功能名称 | |
| 状态 | 草案 / 评审中 / **已确认** / **已实现** |
| 目标版本 | vX.Y.Z 或 hotfix |
| 计划表编号 | 与 xlsx 行一致（建议在文首或变更记录注明） |

必须有可勾选的 **验收标准** 章节。

## 创建定稿时 Agent 回复模板（A3+A4 后）

```
已登记计划表并刷新看板：
- 编号：XXX-01 · 状态：已确认 · 设计时间：YYYY-MM-DD
- 已执行：npm run planning:master-xlsx
- 看板：仓库根目录「策划进度看板.html」

开发提示词已就绪：
docs/planning/prompts/<slug>-dev.prompt.md

新开开发对话，粘贴：
@docs/planning/prompts/<slug>-dev.prompt.md 按此实现 XXX-01
建议角色：@backend-dev（或 @frontend-dev）
```

## 完成时 Agent 回复模板

```
已回写计划表：
- 编号：XXX-01
- 状态：已实现
- 设计时间：YYYY-MM-DD
- 完成时间：YYYY-MM-DD
- 已执行：npm run planning:master-xlsx
请打开仓库根目录「策划进度看板.html」查看进度。
```

## CAPACITY_STAGES（千人阶梯）

若新编号属于阶段 4（千人）容量项：编辑 `scripts/planning/build-producer-progress-html.py` 中 `CAPACITY_STAGES`，把编号加入对应阶的 `ids`，并视情况去掉 `future: True`。

## 常见错误

| 错误 | 正确做法 |
|------|----------|
| 只改 xlsx / HTML | 改 `build-master-plan-xlsx.py` 再 master-xlsx |
| 刷新看板后不给开发提示词 | **A4**：写 `prompts/*-dev.prompt.md` 并贴派发命令 |
| 验收后忘改脚本行 | Checklist B 第 2 步 |
| 完成时间留空 | 已实现必须填完成日 |
| 用 `planning:export` 当结案 | 用 `planning:master-xlsx` |
| 改了埋点没更埋点表 | metrics-catalog-sync |
