# STEAM-DESKTOP-22 后续需求交接

目标角色：Unity／Overlay 前端工程师；美术与动效师参与资源方案。

派发：`@docs/planning/prompts/steam-desktop-22-followups-dev.prompt.md 按本轮用户指定的批次设计与实现 STEAM-DESKTOP-22D–J`。

用户已授权并实现 D、E、F。G–J 仍只登记，后续由用户指定范围再执行。

先读 `docs/planning/specs/Steam桌面端-22鱼塘表现修复与后续迭代.md`。保留 A–C 的读条、聊天预览输入路由和水面接触修复，以及之前 Overlay 的后台解码、缓存和暂停优化。

D 补帧；E 入塘／离席脚底与站起结束再离开；F 打窝起手／循环／收尾；G 背景岸线和水面美术；H 钓鱼事件特效；I 音效时机与音量；J 世界地图信息层级与交互。先按所选项提交具体可交互／动作预览，再按用户认可方案接入。

D／F 当前实现：新增 `motion-samples.json` 与源姿势寻址，13/16/16/18 个轨迹采样保持原动作总时长；鱼线按竿尖与末端重新连接，不拉伸线宽。打窝 100ms 起手、720ms 循环、280ms 收尾，以真实 `groundbaiting` 退出为准。预览位于 `docs/art/fishing-motion/index.html`。不能把轨迹采样描述成新增猫咪原画，也不能把编译通过描述成 Windows 原生验收通过。后续保留这套资源映射和 `OverlayGroundbaitTimeline` 中断规则，详见 `docs/engineering/2026-10-24-overlay-motion-22df.md`。

更换钓鱼动画图层时运行 `scripts/export-fishing-water-contact.py` 同步接触边界；不要移动猫咪去补偿鱼线。Windows 原生 20 组与新项测试必须实际执行，不以 macOS 交叉编译代替验收。完成后同步权威计划脚本、spec、CHANGELOG，并重生计划表／看板。

2026-10-24 补充：用户已单独授权并完成 **22E**；后续开发保留 `OverlaySeatMotion`、`ApplyScenePlacement`、坐下／站起帧显示后计时，以及离席中重入保护。E 的 Windows 原生验收仍需执行，详见 `docs/engineering/2026-10-24-overlay-seat-transitions.md`。其他条目按各自授权推进。
