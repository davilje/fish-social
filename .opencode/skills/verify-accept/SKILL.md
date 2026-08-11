---
name: verify-accept
description: 开发完成后验证、验收、结案，并同步项目开发需求计划表进度
license: MIT
compatibility: opencode
metadata:
  audience: frontend-dev, backend-dev, producer
  workflow: planning
---

# 验证与验收工作流

开发 Agent 完成功能实现后的标准化收尾流程。**进度以仓库根目录 `项目开发需求计划表.xlsx` 为准**；结案必须回写生成脚本并重生 xlsx。

## 适用场景

前端/后端开发 Agent 完成代码实现后，验证功能正确性并标记完成。

## 步骤

1. **验证**：运行自动化验收脚本
   ```bash
   npm run planning:verify -- vX.Y.Z
   # 或专项：npm run verify:server-boot / verify:server-observability 等
   ```

2. **验收**：验证通过后标记实现完成
   ```bash
   npm run planning:accept -- vX.Y.Z
   ```

3. **同步计划表进度（必须）** — 以表为准：
   - 更新 `scripts/planning/build-master-plan-xlsx.py` 中对应编号状态 → `已实现`，填写完成时间
   - 更新 `docs/planning/specs/<功能>.md` 状态 → **已实现**
   - 更新 `specs/README.md` / 相关 Kickoff 门禁文案
   - 运行（**同时更新计划表 + 策划进度看板**）：
     ```bash
     npm run planning:master-xlsx
     ```
   - 确认根目录 `策划进度看板.html` 已刷新（打开可看离上线/千人还差多少）
   - 详细 checklist 见项目 Cursor Skill：`.cursor/skills/planning-progress-sync/SKILL.md`

4. **通知**：告知项目经理开发已完成，需策划执行结案
   ```bash
   npm run planning:close -- vX.Y.Z
   ```

## 验证范围

| Agent | 验证内容 |
|-------|----------|
| frontend-dev | TypeScript 编译、关键组件渲染测试 |
| backend-dev | SQLite 迁移、API 端点响应、Socket 事件 |

## 可能的问题

- 验证失败 → 修复代码后重新运行 `verify`
- 验收后发现新问题 → 在 `docs/planning/handoffs/vX.Y.Z.json` 中追加 TODO
- 只改了 spec 没重生 xlsx → 计划表与事实不一致；必须跑 `planning:master-xlsx`
