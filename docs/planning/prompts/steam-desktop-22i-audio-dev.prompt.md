# STEAM-DESKTOP-22I 音效优化开发交接

目标角色：Unity／Overlay 前端工程师；如需为结算增加稳定事件 ID，由后端工程师同步 DTO。

当前状态（2026-10-24）：用户已选择 22I；交互试听已制作，**等待用户认可试听方案后接入**。此前 2026-10-23 的基础音乐、音效、猫叫已经实现，保留其独立音量和离线资源。

2026-10-24 方案补充：以 `docs/planning/specs/Steam桌面端-22I声音体验设计.md` 和 `docs/design/ui/steam-desktop-22hij/presentation-contract.md` 为最新生产设计。四种候选指四个新 cue；当前代码已出现 catchId/resultId，以下按实际基线核验完整透传。

先读：

- `docs/engineering/2026-10-24-audio-22i-preview.md`
- `docs/art/audio-22i/index.html` 与 `policy.json`
- `docs/planning/specs/Steam桌面端-22鱼塘表现修复与后续迭代.md`

派发：`@docs/planning/prompts/steam-desktop-22i-audio-dev.prompt.md 在用户确认试听方案后，按此实现 STEAM-DESKTOP-22I`。

实现要求：

1. 把预览参数用于统一的音效仲裁层，明确会话、actor、轮次、eventId、cue、事件来源、时间；限制四个短音并发，关键提示优先，过时声音丢弃；不同真实咬钩／鱼获不能被旧 cue 冷却误吞。
2. 补齐可靠事件身份：核验当前 `CatchAccepted` 的 ACK 与结算是否复用已出现的 `catchId/resultId`，补齐缺失链路，不重新制造第二套 ID。不能把时间窗口或物种尺寸当作可靠 ID。
3. WPF 仅从自己的实际动画关键点发送挥竿、入水、收杆、撒饵标记；保留 D/F 的采样、打窝循环，以及 E 的入座／离席衔接。冷解码期间不提前响，取消与会话切换会丢弃旧标记。
4. 首次／重连快照静音；多人其他角色自动动作保持静音。咬钩提示来自真实事件，不依赖恢复快照；新增音色与普通入水区别。
5. UI 语义一致：导航 open、返回 close、切换 toggle、提交 click，结果按操作 ID 一次。聊天 Enter／按钮走同一成功提交入口，禁用按钮、悬停和拖猫不发声。
6. 手动猫叫至少 2.5s，自动庆祝至少 12s，庆祝与猫叫错开；音乐压低可恢复且不改变已保存滑杆值。
7. 确认素材后才从预览目录复制到正式 StreamingAssets，更新 manifest、来源文档与资源校验，保留现有音乐／猫叫授权。
8. 行为测试覆盖审计文档验收项；Windows 实机未执行时保持明确待验收，不能将试听模拟或交叉编译写成运行通过。
9. 正式实现完成后更新权威计划脚本状态／完成日期、spec、CHANGELOG；运行 `npm run planning:master-xlsx` 重生根目录与 docs 副本。不在预览阶段提前标已实现。
