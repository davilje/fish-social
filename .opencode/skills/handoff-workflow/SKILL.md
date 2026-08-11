---
name: handoff-workflow
description: 策划/分析 Agent 产出 spec 后，执行 handoff 移交到开发 Agent 的完整工作流
license: MIT
compatibility: opencode
metadata:
  audience: producer, data-analyst, backend-ops, art-director, ui-designer
  workflow: planning
---

# Handoff 工作流

从 spec 确认到开发移交，以及开发完成后更新计划表与看板的标准化流程。

## 适用场景

策划/分析 Agent 完成 spec 编写并获用户确认后，需将需求移交给开发 Agent。

## 步骤

### Phase 1：移交开发

1. **确认 spec 状态**：docs/planning/specs/功能.md 中状态字段设为 **已确认**
2. **更新计划表**：仓库根目录 项目开发需求计划表.xlsx 登记新版本
3. **生成开发交接文档**：在 docs/planning/prompts/ 生成分角色 prompt
4. **更新 manifest**：docs/planning/handoffs/vX.Y.Z.json 登记 pipeline 状态
5. **运行命令**：
npm run planning:handoff -- vX.Y.Z [--source 角色ID]

### Phase 2：开发完成后（重要！）

开发 Agent 实现完成后，**必须**执行以下步骤更新计划表和看板：

1. **更新 build 脚本中的状态**：在 scripts/planning/build-master-plan-xlsx.py 中，将该需求条目的状态从 已确认 改为 已实现，并填写完成日期
2. **更新 spec 文档状态**：在 docs/planning/specs/功能.md 中将状态字段改为 **已实现**
3. **重建计划表与看板**：运行 
npm run planning:master-xlsx
   - 这会同时更新根目录 项目开发需求计划表.xlsx 和 策划进度看板.html
   - 同步副本也会写入 docs/planning/
4. **记录 CHANGELOG**：在 docs/planning/CHANGELOG.md 追加实现记录

> **注意**：
npm run planning:master-xlsx 是唯一正确的命令（包含 build-master-plan-xlsx.py 和 build-producer-progress-html.py）。**不要使用** 
npm run planning:export，该命令输出位置错误。

## handoff 后

通知项目经理召唤对应开发 Agent：
- @frontend-dev 读 prompts/vX.Y.Z-frontend-dev.prompt.md
- @backend-dev 读 prompts/vX.Y.Z-backend-dev.prompt.md

## 角色 source 值

| 角色 | --source 值 | 默认目标 |
|------|-------------|----------|
| 策划 | planning | 自动路由 |
| 数据分析 | data-analysis | backend-dev |
| 后端维护 | backend-ops-analysis | backend-dev |
| 原画 | art | frontend-dev |
| 动效师 | motion | frontend-dev |
| UI设计师 | ui | frontend-dev |
| 美术总监 | art-director | 多目标 |
