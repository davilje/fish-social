# UNITY-EPIC：Unity 移植分阶段需求总表开发交接

你是 Fish Social 的 Unity 前端/技术负责人。策划已完成 `UNITY-EPIC` 产品规划，请按阶段执行，不要重复实现已验收的 P0～P2。

## 必读文档

1. `docs/planning/specs/Unity移植-分阶段需求清单.md`
2. `docs/planning/product/Unity移植工程路径蓝图.md`
3. `docs/planning/architecture/Unity迁移决策记录.md`
4. `docs/planning/architecture/Unity网络薄客户端契约-v1.md`
5. `fish-social-unity/README.md`

## 当前状态

- `UNITY-P0`、`UNITY-P1`、`UNITY-P2`：已实现，不得回退或重复开发。
- `UNITY-P3`：已定稿，待实现等距 Tile 场景、相机、钓位、宠物和多人状态表现。
- `UNITY-P4`：已定稿，待实现不依赖 Expo 的 Unity 主循环和功能壳层。
- `UNITY-P5`：已定稿，待完成 Steam Windows 包、日志、回滚和 RN 退役策略。
- 实际 Unity 工程目录：`fish-social-unity/`。

## 实施顺序

### 第一阶段：UNITY-P3

- 在 `fish-social-unity/Assets/` 中建立可配置的 Tilemap/场景表现层。
- 支持岸、水、路分层、正交相机拖拽、格子与 `spotId` 映射。
- 使用服务端快照驱动自己和同塘玩家的宠物、钓鱼相位和鱼获反馈。
- 完成多人角色按 y 排序/遮挡和断线后按服务端快照恢复。
- 不修改 Node 的 FSM、咬钩公式、库存和生态权威。

### 第二阶段：UNITY-P4

- 将选塘、背包、商店、图鉴、鱼获弹窗、好友/聊天和排行榜按现有 REST/Socket 契约迁入 Unity。
- 主循环必须在 Unity 内完成：选塘 → 进塘 → 钓鱼 → 收鱼 → 查看/处理鱼获。
- 弹窗打开/关闭不得触发 `leave_pond`，托盘隐藏不得伪造或清空会话。
- Admin 与运营平台继续保留为浏览器功能。

### 第三阶段：UNITY-P5

- 固化 Windows Steam 构建流程和环境变量注入方式。
- 对接客户端错误/崩溃日志到 `client-logs` 或等价方案，不记录 Token、Ticket 或密钥。
- 编写启动、登录、进塘、钓鱼、收鱼、托盘、断线恢复和回滚冒烟清单。
- 明确协议兼容期内 RN 紧急修复策略和 Expo 商店包退役时间点。

## 验收要求

- 每个阶段只在其验收条目全部通过后，才将计划表状态改为 `已实现` 并填写完成日期。
- P3：场景可拖拽、多人排序可控、真实网络状态可见。
- P4：主循环不依赖 Expo，背包/商店/社交功能可从 Unity 进入和返回。
- P5：Windows Steam 包可启动，有日志、回滚和兼容策略。
- 运行相关 Unity 构建/冒烟验证，并保留日志或报告。
- 完成后更新对应 spec 的验收勾选、`docs/planning/CHANGELOG.md` 和计划表；然后运行 `npm run planning:master-xlsx`。

## 禁止事项

- 不把 FSM、咬钩、收鱼、库存或生态权威迁入客户端。
- 不修改 `mobile/`、`server/`、`shared/` 以外的无关模块。
- 不把 Admin/运营平台迁入 Unity。
- 不将 `Library/`、`Temp/`、构建产物、Steam 密钥、Ticket 或 JWT 提交到版本库。

## 开工口令

先执行 `UNITY-P3`，完成并验收后再进入 `UNITY-P4`，最后执行 `UNITY-P5`。
