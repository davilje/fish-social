# 开发提示词：钓具与鱼饵配置（FEAT-GEAR-01）

你是 Fish Social **后端 + Unity 商店/装备**开发 Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/钓具与鱼饵配置.md`（**已确认** / **FEAT-GEAR-01**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（数值表字段、玩家等级）
3. `docs/planning/specs/Steam桌面端-08B商店与装备.md`（已实现商店基线）

## 顺序

1. 确认 PROG-01 数值表中 `rods` / `baits` / `vessels` 已导出 JSON。
2. 竿：弱加成、品质与 D3 `catchGroup` 弱适配；超规格成功满 N 销毁；无可用竿不可开钓；新手发入门竿。
3. 饵：基础无限；进阶 Lv2/3/4 解锁；不进货；按次扣金；按鱼 `diet` 自动选用。
4. 船：商店可买，`enabledUse=false`，无使用逻辑。
5. Steam 商店 UI 对齐展示与禁用态。
6. verify / 自检。

## 不做

- 船 QTE、轮线配件养成、竿数值碾压式成长

## 验收

对照 spec §验收；完成后回写计划表 **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/feat-gear-01-rods-baits-dev.prompt.md 按此实现 FEAT-GEAR-01
```

建议角色：`@backend-dev` + Unity 桌面端（08B 商店/装备）。
