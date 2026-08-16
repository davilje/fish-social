# Steam 桌面宠物 UI 需求拆分

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 桌面宠物 UI 需求拆分 |
| 编号 | **STEAM-DESKTOP-UI** |
| 类型 | 功能配套 / 美术资源 |
| 负责人 | Unity 程序 + 美术 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-13** |
| 上位需求 | `STEAM-DESKTOP-EPIC`、`STEAM-DESKTOP-01`、`STEAM-DESKTOP-07` |

## 1. 拆分原则

- `STEAM-DESKTOP-07A～07F` 是程序功能需求，程序 UI、Prefab、状态绑定和交互直接包含在对应功能项中。
- `STEAM-DESKTOP-ART-01` 是单独的美术资源替换需求，不与每个程序功能一一对应。
- `STEAM-DESKTOP-ART-02` 是后续美术管线：Unity Canvas Prefab 导出 Overlay 像素布局，不把 Prefab 扔进 WPF。
- 程序先使用通用 UGUI、占位图标和基础样式完成可运行功能。
- 美术优先交付猫咪宠物和鱼塘环境；菜单、弹窗等资源后续可替换，不要求重写业务逻辑。
- 复用已完成的 Steam 认证、鱼塘会话、Socket、桌面壳和服务端权威逻辑。

## 2. 需求边界

| 类型 | 编号 | 内容 |
|------|------|------|
| 功能 | `STEAM-DESKTOP-07A～07F` | Unity 程序实现桌面宠物、鱼塘、多人表现、菜单、主窗口页签、通知和主流程 |
| 美术 | `STEAM-DESKTOP-ART-01` | 猫咪、鱼塘环境和后续可替换的基础视觉资源 |
| 美术 | `STEAM-DESKTOP-ART-02` | Overlay：Unity `960×480` Prefab → 布局 JSON → WPF 像素一一对应 |

## 3. 共同边界

- 不修改 Node 鱼塘、生态、库存、钓鱼和社交权威逻辑。
- 不把 Steam Lobby 当作鱼塘权威。
- 弹窗打开和关闭不得触发 `leave_pond`。
- 右键菜单只作用于 Fish Social 窗口/宠物区域。
- 不修改 `mobile/`。
- 第一阶段允许使用占位美术资源，后续替换不应重写业务逻辑。

## 4. 总体验收

- [ ] `STEAM-DESKTOP-07A～07F` 各自完成程序验收。
- [ ] `STEAM-DESKTOP-ART-01` 完成猫咪和鱼塘资源交付。
- [ ] `STEAM-DESKTOP-ART-02` 完成 Overlay 布局导出与像素对齐（后续，不阻塞 07E/07F）。
- [ ] 桌面宠物主视图、鱼塘场景和同塘玩家宠物方向统一。
- [ ] 右键菜单切主窗口页签不破坏鱼塘会话；唤起时主窗口高于 Overlay。
- [ ] 通知、托盘和重连状态可理解。
- [ ] Windows Development Build 完成主流程验证。

## 5. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-16 | 策划 | 07E 改为页签；右键唤起主窗口须高于 Overlay |
| 2026-08-16 | 策划 | 增补后续美术项 `STEAM-DESKTOP-ART-02`：Overlay 场景布局管线 |
| 2026-08-13 | 主 Agent | 修正拆分方式：程序 UI 并入 07A～07F 功能项，美术合并为单一资源替换需求 |
