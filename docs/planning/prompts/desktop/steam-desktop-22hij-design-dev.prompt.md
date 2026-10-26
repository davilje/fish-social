# 开发交接：STEAM-DESKTOP-22H / 22I / 22J

目标角色：Unity／Overlay 前端工程师；H 配合动效美术，I 配合声音设计，J 配合 UI／地图美术。后端工程师只补经核验缺失的事件身份、权威报价或加入幂等链路。

2026-10-24：本轮用户要求完善方案，已交付H/I v2、J v1规格与评审入口，未要求在此次设计回合接入全部正式代码。后续用户指定开始某项时，在该范围内执行本交接；不要把方案完成写成实现完成，也不要把旧试听视为新方案已获认可。

## 独立开发入口

- [22H 猫咪风格水面特效（v2）](steam-desktop-22h-water-effects-dev.prompt.md)
- [22I 钓鱼手记声音体验（v2）](steam-desktop-22i-audio-dev.prompt.md)
- [22J 世界地图（v1）](../../../archive/2026-10-26/superseded/planning/prompts/steam-desktop-22j-world-map-dev.prompt.md)（已归档，不再实现）

H/I 共享事件与尺寸档位，先核验已有公共实现。22J 已归档，下面涉及 J 的步骤和验收不再执行。各提示词只回写对应编号的完成状态。

## 必读

1. `docs/design/ui/steam-desktop-22hij/README.md`。
2. `docs/design/ui/steam-desktop-22hij/presentation-contract.md`。
3. `docs/planning/specs/desktop/Steam桌面端-22H钓鱼特效设计.md`。
4. `docs/planning/specs/desktop/Steam桌面端-22I声音体验设计.md`，已有 `docs/art/audio-22i/index.html` 与 `policy.json`。
5. `docs/archive/2026-10-26/superseded/planning/specs/Steam桌面端-22J世界地图设计.md`（已归档，不要实现）。
6. A–F 实现说明、当前工作区并行改动；保留最近用户认可版本，不重建整套 UI。

## 实施顺序

1. 记录实际基线 commit、现有未提交修改和目标平台；只读核验 catchId/resultId、预测动作身份、实际帧发布、当前动画源标记、地图坐标／费用数据。旧审计缺 ID 的结论已过时，不能照此新造一套 ID。
2. H/I 先统一事件身份和 marker 消费。原生 Overlay 与 Unity 兜底视图只能有一个表现 owner；声音仍由 Unity 统一播放。结果权威、预测可撤销、快照静默、取消／旧 epoch 丢弃。
3. H做猫咪同屏S/M/L/XL四档水花与简化／多人对照，确认水面接触后接6类效果；不制作收获确认、脱钩、收线视觉特效，严格池化、命中穿透和资源预算。I复用六场景事件脚本，重做纸张／翻页物音、柔和器乐及清晰真实四档水声试听；补关键结果紧邻、后台与压低恢复边界；以事件身份而非粗时间冷却去重。
4. I当前`policy.json`和试听WAV是v1比较基线，不代表v2音色。旧曲先重配／审听，合格才保留；不是默认沿用。正式关键咬钩／结果的不同 ID 不被旧冷却误吞；素材认可后才接正式 StreamingAssets，更新 manifest 与 CREDITS，不覆盖尚未认可的候选。
5. J 先更新 Web 可交互地图，使用真实配置与明确演示／未知状态；让筛选、地图、详情和固定操作区职责分离。展示所有错误／收费确认状态，随后按同一几何与时间轴接 Unity。两个 Web 入口复用逻辑，不再复制一套地图实现。
6. 地图图片、点位、标签方向、照片清单一起交付；不改变费用、解锁、巨物暂闭和新手地图规则。收费 join 重试先核对事务结果，不能重复提交扣费；未知金额不用假数字填满 UI。
7. H/I 可联合一轮验证；J 可独立验证。拆分代码与资源变更便于复核，保留开关做 Windows A/B，不把新设计参数当成已测性能。

## 验收与产物

- 每个 spec 的未勾验收条目逐项给证据；没跑 Windows 的条目保持待验收。
- 交付资源清单及 hash、更新后的 Web 对照、Unity/WPF 测试、冷加载／重复包／取消回归和 Windows 录屏／性能记录。
- H：6类水面效果，四档×至少两种水花／水纹；猫咪风格一致，形态不同而非单图缩放；所有猫种manifest、水面、24实例预算、简化和穿透。FX06/07/09不绑定，FX05只在hooked/effort实际破水，不在收杆或结果阶段触发。
- I：操作以纸张物音、配乐以柔和自然器乐、水声以真实分档录音／拟音；同ID一次、不同结果不漏、短音最多四声部、后台提示开关、真实帧同步、静音与音乐压低、授权。
- J：四档窗口、三档 DPI、长中文塘名、密集标签、筛选空态、真实费用、双击／超时／迟到回包、地图与列表共享门禁。
- 实现完成后只回写实际完成的编号与日期，保持其他项状态。更新权威计划脚本、spec、CHANGELOG，运行 `npm run planning:master-xlsx`。
- 本方案不新增业务埋点；若后续要求新增，再按 metrics-catalog-sync 同步。

## 派发示例

```text
@docs/planning/prompts/desktop/steam-desktop-22hij-design-dev.prompt.md 按方案实施 STEAM-DESKTOP-22H、22I、22J，先完成可交互与音画对照，再接入正式项目并逐项验证。
```

用户若只指定其中一个编号，实施与状态回写也只覆盖该编号及其必要依赖。
