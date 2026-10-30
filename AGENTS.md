# Fish Social — 多 Agent 协作团队

本项目使用 opencode 的多 Agent 系统进行协作开发。主 Agent（Build mode）担任**项目经理/制作人**，按需召唤 subagent 完成专项工作。

## 团队角色

### 策划与设计（产出文档，不写代码）

| Subagent | @引用 | 写权限 | 典型输出 |
|----------|-------|--------|----------|
| 策划 | `@producer` | `docs/策划/` | 功能规格 spec、开发交接 prompt |
| 美术总监 | `@art-director` | `docs/美术/风格/` | 美术风格规范、资源审核意见 |
| UI 设计师 | `@ui-designer` | `docs/设计/界面/` | UI 设计稿规范、交互说明 |

### 工程开发（写代码）

| Subagent | @引用 | 写权限 | 典型输出 |
|----------|-------|--------|----------|
| 前端工程师 | `@frontend-dev` | `mobile/` | 客户端 React Native/Expo 组件 |
| 后端工程师 | `@backend-dev` | `server/ shared/ scripts/` | Express API、Socket.io、SQLite 迁移 |

### 美术生产（写资源文件）

| Subagent | @引用 | 写权限 | 典型输出 |
|----------|-------|--------|----------|
| 原画 | `@concept-artist` | `mobile/assets/ docs/美术/` | PNG/SVG 资源、资源清单 |
| 动效师 | `@motion-designer` | `mobile/assets/animations/` | Lottie 动效、动效接入说明 |

### 数据分析与运维（产出报告，不写业务源码）

| Subagent | @引用 | 写权限 | 典型输出 |
|----------|-------|--------|----------|
| 数据分析师 | `@data-analyst` | `docs/数据分析/` | 模拟运行报告、生态调参建议 |
| 后端维护 | `@backend-ops` | `docs/策划/架构/` | 架构方案、性能优化报告 |

## 协作工作流

```
项目经理（主 Agent）
  │
  ├─ 1. @producer → 写 spec → handoff → 生成分角色 prompt
  │
  ├─ 2. @data-analyst / @backend-ops / @art-director / @ui-designer
  │      → 产出分析/规范 → handoff 给对应开发角色
  │
  ├─ 3. @concept-artist / @motion-designer
  │      → 产出资源 → handoff 给 @frontend-dev 接入
  │
  ├─ 4. @frontend-dev / @backend-dev
  │      → 实现功能 → verify → accept
  │
  └─ 5. 项目经理验收 → @producer close → 更新 CHANGELOG
```

### 启动新版本开发

```bash
# 1. 策划产出 spec
@producer 撰写功能规格

# 2. 自动移交
npm run planning:handoff -- vX.Y.Z

# 3. 开发实现（召唤对应 subagent）
@frontend-dev 实现客户端
@backend-dev 实现服务端

# 4. 验证与验收
npm run planning:verify -- vX.Y.Z
npm run planning:accept -- vX.Y.Z

# 5. 策划结案
@producer 执行结案
npm run planning:close -- vX.Y.Z
```

## 与现有 Cursor 系统的关系

### 预览交付约定（用户要求）

所有新增或更新的视觉预览必须同步纳入根目录 `现行预览.html` 的分类导航；最终以总预览页和对应稳定路由交付。专项页面作为总页内嵌内容保留，不再作为唯一交付入口。已有导航要复用，避免重复；旧链接与选择记录需要兼容。用户于 2026-10-30 明确要求后续所有预览统一到总页。

本项目同时维护 `.cursor/rules/`（Curor IDE 的 6 Agent 规则）和 `.opencode/agents/`（opencode 的 9 Agent 定义）。两者独立工作：

- **Cursor**：用于 IDE 内开发（已配置 6 角色）
- **Opencode**：用于 CLI/TUI 协作开发（9 角色，含美术总监/动效师/UI设计师）

`docs/策划/` 下的 handoff 协议是共享的，两个系统共用同一套 spec/handoff/verify 管道。

**策划全流程（写 spec → 登记计划表/看板 → 验收回写状态）**：见项目 Skill [`.cursor/skills/planning-progress-sync/SKILL.md`](.cursor/skills/planning-progress-sync/SKILL.md)。权威表：仓库根目录 [`项目开发需求计划表.xlsx`](项目开发需求计划表.xlsx)，经 `npm run planning:master-xlsx` 重生；**同时刷新** [`策划进度看板.html`](策划进度看板.html)；`docs/策划/` 下保留同步副本。运营入口：[`运营平台.html`](运营平台.html) 或 `打开运营平台.bat` → http://localhost:3001/ops/。

**埋点 / `fishing_metrics` 变更**：必须同步埋点表，见 [`.cursor/skills/metrics-catalog-sync/SKILL.md`](.cursor/skills/metrics-catalog-sync/SKILL.md)（`shared/metrics-schema.ts` + `scripts/build-metrics-events-xlsx.py` + `npm run planning:metrics-xlsx` → 根目录 `v0.4.4-埋点表清单.xlsx`，并同步 `docs/策划/报告/` 副本）。

## 效果预览统一入口

用户要求（2026-10-30）：今后所有效果预览合并到仓库根目录 `现行预览.html`，在对应分类内直接查看、播放和调参；新增或更新预览时同步总页入口、定位与旧链接迁移，交付总页链接。专项页面可作为总页嵌入内容，不再要求用户另开独立预览页。未获用户采用前，试听方案不写入正式游戏音频配置。
