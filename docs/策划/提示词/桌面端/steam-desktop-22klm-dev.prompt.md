# STEAM-DESKTOP-22K / 22L / 22M 开发与复验

目标角色：`@frontend-dev`、`@backend-dev`，美术资源按当前姿势绘制契约处理。

规格：`docs/策划/规格/桌面端/Steam桌面端-22KLM呼吸瞌睡与狸花猫.md`。

沿用当前开发分支和真实资源；不要另建一套 SVG 角色，也不要把闭眼头整体替换成尺寸不同的头像。呼吸每帧从原姿势计算，保持脚底、两手握竿、竿尖和浮漂连接。

2026-10-30 已应用用户确认的加强版：清醒 ±2.2%、睡着 ±2.8%，6.4 秒八阶段低头/停顿/抬头，最高额外前倾 8°。Unity 与 Overlay 共用 `FishingRestGeometry`；Unity 顶点网格、WPF 冻结绘图分片分别处理身体四层，鱼竿刚性跟随握点。Unity 187/187、Overlay 7/7 通过；WPF 实机性能需在 Windows 复验。

交付入口固定为仓库根 `现行预览.html#rest`；今后所有预览都登记到该总页导航，专项页面作为嵌入内容。不得只交付孤立的专项预览链接。

服务器默认 30 分钟无人照看后在 waiting 入睡。`fishing_attention` 只操作认证用户，重连、换皮、快照不重置计时。睡眠咬钩走原逃鱼结算，不发鱼获。Unity 转发权威状态到 Overlay，ACK 不触发本地唤醒；点击本人而非拖动或悬停才发送照看。

复验服务器睡眠测试、Unity 睡眠与运动 EditMode、Overlay RestMotion / Motion / WaterContact 测试、动作浏览器预览；在 Windows 验证真实联机及 DPI。所有原生编译与 Windows 实机运行分开记录。

复验命令入口：`@docs/策划/提示词/桌面端/steam-desktop-22klm-dev.prompt.md 复验 STEAM-DESKTOP-22K/22L/22M 的 Windows 联机与视觉效果`。
