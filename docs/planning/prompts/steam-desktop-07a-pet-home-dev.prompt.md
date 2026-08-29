# STEAM-DESKTOP-07A：桌面宠物主视图与鱼塘入口

请在 `fish-social-unity/` 内实现桌面宠物主视图与鱼塘入口。
本任务只运行一个 Unity 主程序；严禁启动第二个 Unity Player、透明 Unity Overlay 或 `UniWindowController`。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`

## 范围

- 启动后显示自己的 2D 猫咪宠物。
- 使用一个可替换的空白正方形 2D 猫咪占位 UI：主窗口建议 `256×256` 或等比例；**Overlay 显示槽为 64×64**（源图同样 256×256），纯色/线框即可，不制作最终美术。
- 宠物容器保持固定正方形比例，图片/Prefab 通过单一资源引用接入，后续替换资源不得改动布局和业务逻辑。
- 动画表现采用“序列帧 + 宠物状态机”方案，不接入 Spine 作为本阶段前置依赖。
- 状态机至少覆盖 `idle`、`fishing`、`hooked`、`catching`、`dragging`、`offline`；当前无正式美术时可复用同一张占位图，通过状态文字和颜色区分。
- 状态机必须与渲染器解耦，后续可在不改网络、窗口和业务状态代码的前提下替换为 Spine Renderer。
- 显示当前 Steam 登录、Socket、鱼塘和钓鱼状态。
- 提供进入/恢复当前鱼塘入口。
- 提供返回桌面宠物主视图入口。
- 进入鱼塘时只切换 Unity 主程序内的面板，不创建新进程、不创建新窗口。
- 先允许使用简化占位美术资源，不阻塞交互验证。

## 边界

- 不实现多人鱼塘场景细节、右键菜单、完整弹窗。
- 不实现 Spine 资源、正式猫咪美术和复杂换装。
- 不实现透明桌面 Overlay、系统级置顶、桌面穿透和独立宠物进程；这些能力属于 `STEAM-DESKTOP-07G`。
- 不修改 Node、Steam 认证和鱼塘权威逻辑。
- 不修改 `mobile/`、`server/`、`shared/`。

## 验收

- Windows Development Build 启动后可看到桌面宠物。
- 宠物占位 UI 为正方形，窗口缩放不变形，替换 Sprite/Texture2D 后无需修改业务代码。
- 登录状态和当前钓鱼状态可见。
- 可进入鱼塘并返回桌面宠物视图。
- 状态切换不会创建重复会话。
- 进入鱼塘后 CPU/内存不会因启动第二个 Unity Player 增长。
- 主窗口仍可正常缩放、拖动、切换普通窗口/无边框/全屏。
