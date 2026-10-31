# 开发提示词：断线、断竿与上钩鱼体长映射

你是 Fish Social 后端与客户端开发协作者。按以下两份已确认规格实现 FISH-BREAK-01、FISH-SCALE-01；本交接文件的存在不代表游戏功能已实现。设计日期 2026-10-31，当前两项完成日期均为空。

## 必读

1. [钓鱼断线与断竿表现](../../规格/玩法/钓鱼断线与断竿表现.md)：自然脱钩内分配断线表现、成功后断竿、权威结果与清理。
2. [上钩鱼尺寸与体长映射](../../规格/玩法/上钩鱼尺寸与体长映射.md)：18 个控制点、对数分段插值、鱼嘴与持鱼锚点、遮罩与巨物。
3. [钓具与鱼饵配置](../../规格/玩法/钓具与鱼饵配置.md)：已实现的超规格累计销毁规则。
4. [体长体重步进与公式评判](../../规格/玩法/钓鱼体长体重步进与公式评判.md)：待开发的另票；本次不连带修改体长步进或业务曲线。
5. 仓库 `AGENTS.md`、`.cursor/skills/planning-progress-sync/SKILL.md`；涉及埋点实现时读 `.cursor/skills/metrics-catalog-sync/SKILL.md`。

## 分工与顺序

1. **后端（@backend-dev）**核对 `shared/gearRules.ts`、`shared/types.ts`、`server/src/fishingSession.ts`、`fishingStateMachine.ts`、`inventory.ts`、`gear.ts`。为同一结果生成一次断线表现选择；锁定当次鱼体长和钓竿，拒绝上钩/收尾中换装。成功领取返回当次断竿结果，不能用客户端装备差集猜测。
2. 补充兼容的结果字段及去重契约，保留现有成功率/总脱钩率、生态随机序列、超规格阈值、累计规则与失败耐久。断线不是额外一次业务失败，断竿不撤销已入包鱼获。
3. **客户端（@frontend-dev）**与动效协作者制作独立断线段、断竿段和接触锚点。将 0.02–50m 的18点映射存为一份版本化配置，Unity/WPF/预览共同读取。头尾根定义和源图透明边校准后再接入缩放。
4. `desktop-overlay/OverlayPetActor.cs`、`OverlayFishWaterMask.cs`、`OverlayFishingWaterContact.cs` 与 Unity `Assets/Scripts/Desktop/Pet/SpriteFramePetRenderer.cs`、`UnityFishingWaterMarkerBridge.cs` 同步接入。保持嘴/线端固定，持鱼补真实接触点；全矩阵后裁水，水下阶段继续隐藏。
5. 制作断线900ms、成功后断竿1100ms试调动作及十八点尺寸验证。所有新增视觉/试听进入根目录 `现行预览.html` 的现有钓鱼分类，兼容旧路由与选择；正式音频采用前先留试听。
6. 完成以下业务回归、边界测试和 Windows 视觉联测，再分别更新两票状态。不要因其中一票完成就把两票同时标为已实现。

## 验证与验收

先运行适用的现有检查，例如 `verify:feat-gear-01`、`verify:feat-gear-01-server` 与相关钓鱼/动作测试；新检查聚焦业务不变与几何边界，不用仅复述实现的断言替代验收。

- [ ] 断线条件分配及 r=1/1.5/2/3 的边界正确，同种子下总成功/失败数与原逻辑一致。
- [ ] 超规格阈值相等、第 N−1/N 次、失败不计数、成功后销毁旧竿、备用竿/无竿行为正确。
- [ ] 双端观察、重复领取、重复结果和重连不重复发奖、成长、扣耐久、销毁或发声。
- [ ] 十八点准确；0.30/1.20/4.00m 插值、全域单调、异常体长、96/128/192槽位与DPI换算正确。
- [ ] 鱼嘴/持鱼点误差≤1标准px；最小鱼可读，40m/50m巨物无槽位裁切、水面穿帮、HUD遮挡或鼠标阻塞。
- [ ] 上钩→真实出水→收杆→持鱼尺寸一致；水下不泄露鱼情，水花只由合法触水marker产生。
- [ ] 断线、断竿、隐藏恢复、切塘、离塘、角色销毁与多人同屏均清理完整；十一角色与 Windows 实机通过视觉验收。
- [ ] 中英文文案、正式资源清单和现行总预览同步；如改埋点，完成指标schema与埋点表同步。
- [ ] 对照各自spec全部验收项，按 planning-progress-sync 分别回写状态/完成日期，运行 `npm run planning:master-xlsx`，同步 CHANGELOG 与索引。保留未完成的平台验收事实。

## 派发

建议角色：@backend-dev + @frontend-dev，动效协作者负责断裂层与持鱼接触点。

```text
@docs/策划/提示词/玩法/fish-break-size-design-dev.prompt.md 按此实现 FISH-BREAK-01 和 FISH-SCALE-01
```
