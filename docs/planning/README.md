# Fish Social 策划文档

本目录存放产品策划文档与协作规范，与 `mobile/`、`server/`、`shared/` 源码并列维护。

## 快速入口

| 文档 | 说明 |
|------|------|
| [HANDOFF.md](./HANDOFF.md) | **策划 → 开发 Agent 交接规范** |
| [WORKFLOW.md](./WORKFLOW.md) | 策划工作流（策划只写文档） |
| [templates/开发交接说明.md](./templates/开发交接说明.md) | 复制给开发 Agent 的提示词模板 |
| [INDEX.md](./INDEX.md) | 文档索引与版本对照 |
| [product/v0.1.0-功能全景.md](./product/v0.1.0-功能全景.md) | 当前版本完整功能策划（基线） |
| [product/钓鱼世界与鱼塘场景优化策略.md](./product/钓鱼世界与鱼塘场景优化策略.md) | 场景优化策略参考（REF-SCENE-1，非开发需求） |
| [product/Unity移植工程路径蓝图.md](./product/Unity移植工程路径蓝图.md) | Unity 移植工程路径参考（REF-UNITY-1，非开发需求） |

## 目录结构

```
docs/planning/
├── README.md                 # 本文件：入口与目录说明
├── WORKFLOW.md               # 策划工作流规范
├── INDEX.md                  # 文档索引、版本与代码对照表
├── CHANGELOG.md              # 策划文档本身的变更记录
├── product/                  # 产品全景与版本快照
│   └── v0.1.0-功能全景.md
├── specs/                    # 专项策划（生态数值、社交、商业化等）
└── templates/                # 文档模板
    ├── 功能规格模板.md
    └── 版本变更记录模板.md
```

## 版本约定

- 策划版本号与 `mobile/app.json` 中 `expo.version` 对齐（当前 **0.1.0**）。
- 每个 App 小版本至少对应一份 `product/vX.Y.Z-功能全景.md` 快照；大改可另增 `specs/` 专项文档。
- 功能开发与策划文档**同 PR 或同迭代**更新，避免文档滞后。

## 谁该读什么

| 角色 | 建议阅读 |
|------|----------|
| 策划 / 产品 | `product/` 全景 + `WORKFLOW.md` |
| 开发 | 对应模块的 `specs/` + 全景中的 API / 数据模型章节 |
| 测试 | 全景中的业务流程、权限规则、已知限制 |
| AI / 新成员 | `README.md` → `product/v0.1.0-功能全景.md` |
