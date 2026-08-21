# 开发提示词：世界地图分区与进塘扣费确认（STEAM-DESKTOP-08A2）

你是 Fish Social **Unity Steam 桌面端**开发 Agent。按规格实现，勿扩需求。扣费结算权威在服务端（PROG-01）；本票做地图展示与确认 UI。

## 必读

1. `docs/planning/specs/Steam桌面端-08A2世界地图分区与进塘扣费.md`（**已实现** / **STEAM-DESKTOP-08A2**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（分类/锁态/扣费口径）
3. 已实现 `STEAM-DESKTOP-08A` 世界地图相关脚本（如 `DesktopWorldMapPanel`）

## 顺序

1. 地图数据源改为导出 `ponds.json`（category、zone、fee、isOpen、坐标等）。
2. 不显示 `novice`；六类分区可辨；未解锁灰锁；巨物 `isOpen=false` 暂闭不可进。
3. 收费塘进塘前确认弹窗（每 2h 费用、今日扣费次数/剩余）；免票塘无购票流程。
4. 引导未完成不可进公开塘（与 PROG 一致）。
5. 服务端拒绝原因展示一致。
6. verify / 自检。

## 不做

- 新手塘上地图、扣费结算本身、最终六区美术插画（色块占位可）

## 验收

对照 spec §3；完成后回写计划表 **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/steam-desktop-08a2-map-zones-fee-dev.prompt.md 按此实现 STEAM-DESKTOP-08A2
```

建议角色：Unity 桌面端 / `@frontend-dev`（仅 `fish-social-unity/`）。
