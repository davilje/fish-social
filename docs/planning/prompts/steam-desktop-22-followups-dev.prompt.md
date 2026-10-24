# STEAM-DESKTOP-22 后续需求交接

目标角色：Unity／Overlay 前端工程师；美术与动效师参与资源方案。

派发：`@docs/planning/prompts/steam-desktop-22-followups-dev.prompt.md 按本轮用户指定的批次设计与实现 STEAM-DESKTOP-22D–J`。

当前用户仅要求登记 D–J，尚未授权本轮制作这些资源。下一批由用户指定范围后执行；不要自动把全部七项都视为开工指令。

先读 `docs/planning/specs/Steam桌面端-22鱼塘表现修复与后续迭代.md`。保留 A–C 的读条、聊天预览输入路由和水面接触修复，以及之前 Overlay 的后台解码、缓存和暂停优化。

D 补帧；E 入塘／离席脚底与站起结束再离开；F 打窝起手／循环／收尾；G 背景岸线和水面美术；H 钓鱼事件特效；I 音效时机与音量；J 世界地图信息层级与交互。先按所选项提交具体可交互／动作预览，再按用户认可方案接入。

更换钓鱼动画图层时运行 `scripts/export-fishing-water-contact.py` 同步接触边界；不要移动猫咪去补偿鱼线。Windows 原生 20 组与新项测试必须实际执行，不以 macOS 交叉编译代替验收。完成后同步权威计划脚本、spec、CHANGELOG，并重生计划表／看板。
