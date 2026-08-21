# 开发提示词：鱼塘分级与玩家成长（FEAT-PROG-01）

你是 Fish Social **后端为主、Unity 桌面端为辅**的开发 Agent。按规格实现，勿扩需求。本票是 v0.7 R1 **首包**，后续 GEAR / 08A2 / RISK / SPOT 依赖本票规则与数值表。

## 必读

1. `docs/planning/specs/鱼塘分级与玩家成长.md`（**已实现** / **FEAT-PROG-01**）
2. `docs/planning/specs/钓鱼玩法扩展-v0.7-Epic.md`（范围与非目标）
3. 现有挂机钓鱼、日 8h 额度、Steam 登录相关服务端与 Unity 进塘路径

## 顺序

1. 建立 `钓鱼玩法固定数值表.xlsx` 与导出脚本 → JSON（Node + Unity 共读；**不进云库**）。
2. 七类 `pondCategory`、解锁/开放、`pond-novice` 与引导完成态（服务端权威）。
3. 玩家钓鱼等级 + 每塘 10 级熟练度；满/锁满停发塘 XP；时长塘 XP 按表。
4. 进塘确认后按有效钓鱼时长每满 2h 扣费；日最多 4 次；金币不足停钓并提示。
5. 新手完成态仍由服务端权威；**教学关交互见 STEAM-DESKTOP-11**（本地 Overlay，不进正式塘循环）。
6. 卖价公式 + 野外/水库相对高级乘子。
7. verify / 自检对照 spec 验收条。

## 不做

- 地图六区 UI（08A2）、竿饵断竿（GEAR）、巡警（RISK）、线索泡（SPOT）
- 船 QTE、巨物开放与榜、Mobile/Web 对齐

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 七类规则与扣费/新手/卖价按表落地
- [x] JSON 导出可被 Node/Unity 读取
- [x] 相关自测通过

## 派发

```text
@docs/planning/prompts/feat-prog-01-pond-tiers-growth-dev.prompt.md 按此实现 FEAT-PROG-01
```

建议角色：`@backend-dev`（主）+ Unity 桌面端配合新手 Overlay / 停钓提示。
