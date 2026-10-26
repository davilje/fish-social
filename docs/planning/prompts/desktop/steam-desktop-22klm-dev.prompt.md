# STEAM-DESKTOP-22K / 22L / 22M 开发与复验

目标角色：`@frontend-dev`、`@backend-dev`，美术资源按当前姿势绘制契约处理。

规格：`docs/planning/specs/desktop/Steam桌面端-22KLM呼吸瞌睡与狸花猫.md`。

沿用当前开发分支和真实资源；不要另建一套 SVG 角色，也不要把闭眼头整体替换成尺寸不同的头像。呼吸每帧从原姿势计算，保持脚底、两手握竿、竿尖和浮漂连接。

服务器默认 30 分钟无人照看后在 waiting 入睡。`fishing_attention` 只操作认证用户，重连、换皮、快照不重置计时。睡眠咬钩走原逃鱼结算，不发鱼获。Unity 转发权威状态到 Overlay，ACK 不触发本地唤醒；点击本人而非拖动或悬停才发送照看。

复验服务器睡眠测试、Unity 睡眠与运动 EditMode、Overlay RestMotion / Motion / WaterContact 测试、动作浏览器预览；在 Windows 验证真实联机及 DPI。所有原生编译与 Windows 实机运行分开记录。

复验命令入口：`@docs/planning/prompts/desktop/steam-desktop-22klm-dev.prompt.md 复验 STEAM-DESKTOP-22K/22L/22M 的 Windows 联机与视觉效果`。
