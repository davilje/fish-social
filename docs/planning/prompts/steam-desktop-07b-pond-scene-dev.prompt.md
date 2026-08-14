# STEAM-DESKTOP-07B：2D 鱼塘环境与自己的猫咪

**状态：已实现（2026-08-15）**

请在 `fish-social-unity/` 内实现 2D 鱼塘环境、钓位和自己的猫咪表现，主要渲染目标为已验收的 `960×480` 原生 Overlay。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialPondSessionController.cs`

## 范围

- 显示 2D 池塘环境、水面和钓位。
- 显示自己的猫咪、当前钓位和基础钓鱼状态。
- Overlay 默认尺寸为 `960×480`，自己的猫咪基准尺寸为 `128×128`。
- 主窗口隐藏时，鱼塘场景和自己的猫咪继续在 Overlay 中渲染。
- Overlay 恢复主窗口时不得销毁或重建鱼塘会话。
- 以服务端 `pond_snapshot` 和现有会话状态为数据源。
- 预留可替换的宠物和场景资源接口。

## 边界

- 不实现同塘其他玩家表现，那属于 07C。
- 不负责原生 Overlay 窗口生命周期和 IPC 建设，那属于 07G；本需求消费已有 Overlay 渲染/状态接口。
- 不改变服务端鱼塘、生态和钓鱼规则。
- 不使用本地伪造快照作为多人状态。

## 验收

- 进入合法 pond 后能看到 2D 鱼塘。
- 自己的猫咪和钓位位置稳定。
- Overlay 中场景缩放和 `128×128` 猫咪尺寸稳定，不因窗口切换变形。
- 开始/收竿状态能反映到宠物表现。
- 断线或快照更新不会重复创建场景对象。
