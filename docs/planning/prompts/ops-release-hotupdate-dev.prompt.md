# 开发提示词：发版与热更策略（OPS-RELEASE-1）

你是 Fish Social **后端运维 / 策划文档** Agent（写 `docs/planning/`；**可不改**玩法代码）。按规格完成 OPS-RELEASE-1，勿扩做成客户端 OTA。

## 必读

1. `docs/planning/specs/发版与热更策略.md`（**已实现** / **OPS-RELEASE-1**）
2. Runbook：`docs/planning/architecture/发版与热更-单机Runbook.md`
3. C1：`docs/planning/specs/C-调优与状态机.md` §C1 · `server/src/gameConfig.ts` · Admin 配置面板
4. `docs/planning/architecture/Unity迁移决策记录.md`（协议版本纪律，可引用）

## 顺序

1. 撰写 `docs/planning/architecture/发版与热更-单机Runbook.md`（分类 A/B/C、决策矩阵、不停服配置步骤、停服发版步骤、回滚、OTA 触发条件）  
2. 在 OPS-RELEASE-1 spec 变更记录挂上 runbook 链接；勾选验收  
3. （可选）运营平台加「发版说明」链到该文档  
4. **不要**接入 Expo Updates / 多机滚动  

## 验收

对照 spec §5；完成后 `planning-progress-sync` Checklist B → **已实现**。

## 派发

```text
@docs/planning/prompts/ops-release-hotupdate-dev.prompt.md 按此实现 OPS-RELEASE-1
```

建议角色：`@backend-ops`（主）或主 Agent 写文档；无需 `@frontend-dev`。
