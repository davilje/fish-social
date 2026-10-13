---
name: planning-progress-sync
description: >-
  End-to-end Fish Social planning workflow: create requirement specs, register
  rows in 项目开发需求计划表.xlsx, regenerate 策划进度看板.html, then write and hand off
  a docs/planning/prompts/*-dev.prompt.md for developers; on acceptance flip
  status to 已实现 with dates. Use when creating/confirming specs, updating the
  plan table or progress board, user says 验收/已完成/已实现/已确认, after
  planning:accept, or finishing a prompt in docs/planning/prompts/.
---

# 策划流程：需求文档 → 计划表 → 进度看板 → 开发提示词 → 完成回写

本 Skill 覆盖策划落地全链路。**进度权威源是生成脚本**，禁止只改 xlsx/HTML 手工格。

| 产出 | 路径 | 如何更新 |
|------|------|----------|
| 需求规格 | `docs/planning/specs/<名称>.md` | 手写 / 改状态 |
| 计划表 | 仓库根 `项目开发需求计划表.xlsx` | 改脚本后 `npm run planning:master-xlsx` |
| 进度看板 | 仓库根 `策划进度看板.html` | 同上（自动跟跑） |
| 开发提示词 | `docs/planning/prompts/<slug>-dev.prompt.md` | **刷新看板后必写**，并贴给用户 |
| 同步副本 | `docs/planning/` 同名 xlsx/html | master-xlsx 自动同步 |

## 何时执行（必须读 Skill）

| 用户/场景 | 走哪条 Checklist |
|-----------|------------------|
| 新需求定稿、写 spec、「已确认」 | **A 创建**（含看板后交付开发提示词） |
| 登记计划表 / 刷新看板 / handoff | **A3→A4** + CLI |
| 开发验收、「已完成/已实现」、`planning:accept`、prompt 收尾 | **B 完成**（自动回写状态） |

每次改完 `build-master-plan-xlsx.py` 后：**必须** `npm run planning:master-xlsx`（xlsx + HTML + docs 副本）。

## 权威数据源（顺序）

1. **`scripts/planning/build-master-plan-xlsx.py`** ← 先改这里的行数据
2. **`npm run planning:master-xlsx`** → 根目录 xlsx + `策划进度看板.html` + `docs/planning/` 副本
3. 对应 **spec** 元信息「状态」
4. 辅助：`sync_spec_status.py`、`export_specs_xlsx_refined.py`（维护用，不是主路径）

看板生成器：`scripts/planning/build-producer-progress-html.py`（由 master-xlsx 调用；也可单独 `npm run planning:progress-html`）。

---

## Checklist A — 需求创建（设计定稿）

### A1. 写需求文档

1. 复制模板 [`docs/planning/templates/功能规格模板.md`](../../../docs/planning/templates/功能规格模板.md) → `docs/planning/specs/<功能名>.md`
2. 元信息 **状态** → `已确认`（用户确认范围后）
3. §验收标准写成可勾选条目；标明非目标
4. 更新 `specs/README.md`、`CHANGELOG.md` 策划节（开发提示词在 **A4** 写，勿跳过）

策划**不**改 `mobile/` / `server/` / `shared/`。

### A2. 登记计划表行（必做）

在 `build-master-plan-xlsx.py` 的「开发计划」数组新增或改行：

```python
# 列序：当前状态, 编号, 类型, 需求名称, 层级, 版本/阶段, 优先级, 说明, 文档路径, 设计时间, 完成时间
['已确认', 'XXX-01', '功能', '需求简称', '—', 'hotfix', 'P0',
 '一句话说明', 'docs/planning/specs/<功能名>.md', 'YYYY-MM-DD', ''],
```

| 列 | 创建时 |
|----|--------|
| 当前状态 | `已确认` |
| 设计时间 | **当天** `YYYY-MM-DD`（必填） |
| 完成时间 | `''` 留空 |
| 文档路径 | 指向 specs md |

编号规则示例：`FISH-*` 功能、`OPS-*` 运维、`D-L*` 数据平台、`BUG-*` 修复、`BE-OPT-*` 后端优化、`ADMIN-OBS-*` Admin。

若同一编号还出现在其它 sheet 数组里，一并改一致。

### A3. 重生表 + 看板

```bash
npm run planning:master-xlsx
```

可选版本级移交（会顺带生成部分 prompt，**仍须做 A4 确认/补全**）：

```bash
npm run planning:handoff -- vX.Y.Z
# 或旧路径：npm run planning:confirm -- vX.Y.Z
```

### A4. 提供开发用提示词（刷新看板后必做）

1. 写入或更新 `docs/planning/prompts/<slug>-dev.prompt.md`  
   - 命名：`<短横线 slug>-dev.prompt.md`（例：`ops-catch-inventory-admin-dev.prompt.md`）  
   - 结构见 [reference.md](reference.md)#开发提示词结构；也可对照 [`templates/开发交接说明.md`](../../../docs/planning/templates/开发交接说明.md)
2. 在 `prompts/README.md` 表中登记一行（若该目录有索引表）
3. **回复用户时必须交付**（缺一不可）：
   - prompt 文件路径
   - 一句派发命令，便于新开开发对话直接 `@`：
     ```text
     @docs/planning/prompts/<slug>-dev.prompt.md 按此实现 <编号>
     ```
   - 建议目标角色（`@frontend-dev` / `@backend-dev` / 两者）
4. 有版本交接文档时：可另跑 `npm run planning:prompt -- vX.Y.Z`，但专项 hotfix 以手写 `*-dev.prompt.md` 为准

### A5. 特殊同步

- 新增「千人扩容」类编号 → 改 `build-producer-progress-html.py` 里 `CAPACITY_STAGES` 的 `ids`
- 涉及埋点 / `fishing_metrics` → 同时执行 [metrics-catalog-sync](../metrics-catalog-sync/SKILL.md)

---

## Checklist B — 需求完成（验收后自动回写）

开发完成、用户验收、或 `planning:accept` / prompt 勾完后，**Agent 主动执行**本清单，不要等用户再催「更新表」。

1. 核对代码与相关 `npm run verify:*`（或 `npm run planning:verify -- vX.Y.Z`）
2. 改 `build-master-plan-xlsx.py` 对应行：
   - 状态 → `已实现`
   - **完成时间** → 当天（必填）
   - **设计时间** 已有则保留；缺失则补定稿日或完成日
3. spec 元信息状态 → **已实现**；§验收全部 `[x]`；变更记录补一行
4. `specs/README.md` / Kickoff 门禁文案同步
5. （可选）`npm run planning:accept -- vX.Y.Z` / `planning:close`
6. **`npm run planning:master-xlsx`**（必须，刷新 xlsx + `策划进度看板.html`）
7. CHANGELOG 实现节
8. 回复用户：编号、状态、设计/完成时间；提示打开根目录 **`策划进度看板.html`**
9. 若改过埋点 → 再跑 metrics-catalog-sync

版本级 CLI：

```bash
npm run planning:verify -- vX.Y.Z
npm run planning:accept -- vX.Y.Z
npm run planning:close -- vX.Y.Z
```

---

## 字段与状态

| 列 | 创建（已确认） | 完成（已实现） |
|----|----------------|----------------|
| 当前状态 | `已确认` | `已实现` |
| 设计时间 | 必填当天 | 保留 |
| 完成时间 | 空 | 必填当天 |

状态枚举：`未开始` · `已确认` · `已实现` · `已定稿` · `已文档化` · `已废弃`

## 命令速查

```bash
npm run planning:master-xlsx      # xlsx + 策划进度看板（根目录 + docs 副本）
npm run planning:progress-html    # 仅重生看板
npm run planning:handoff -- vX.Y.Z
npm run planning:verify -- vX.Y.Z
npm run planning:accept -- vX.Y.Z
npm run planning:close -- vX.Y.Z
npm run planning:status -- vX.Y.Z
```

**不要**用 `npm run planning:export` 当主路径（输出位置/口径与 master 表不一致）。

## 看板说明

- 根目录 `策划进度看板.html`：总进度、按类型条、四阶容量（含千人）、开放待办
- 运营入口：`运营平台.html` / `打开运营平台.bat` → http://localhost:3001/ops/

## 关联

- 流程长文：`docs/planning/策划到开发工作流.md`、`docs/planning/多Agent工作流.md`
- Opencode：`.opencode/skills/handoff-workflow`、`.opencode/skills/verify-accept`
- 埋点表：`.cursor/skills/metrics-catalog-sync/SKILL.md`
- 行格式与文件清单细节：[reference.md](reference.md)
