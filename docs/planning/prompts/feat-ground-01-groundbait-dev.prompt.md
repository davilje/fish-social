# 开发提示词：打窝机制（FEAT-GROUND-01）

你是 Fish Social **后端状态机 + Unity Overlay** 开发 Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/打窝机制.md`（**已确认** / **FEAT-GROUND-01**）
2. `docs/planning/specs/钓具与鱼饵配置.md`（咬钩饵分离）
3. `docs/planning/specs/Steam桌面端-08GOverlay钓鱼操作栏.md`、`钓位点位线索文字泡.md`（气泡样式）
4. `server/src/fishingStateMachine.ts`

## 顺序

1. 数值表 sheet `groundbaits`（含 maxBonus/stackK/尺寸字段）+ 常量 `maxStackCount=50` → export。
2. 状态机：`seated` ↔ `groundbaiting`；禁止 groundbaiting 直进 baiting。
3. Socket `groundbait_start`：扣金、计时、stack+1；非线性 `bonus = maxBonus*(1-exp(-k*stack))`；附近鱼临时 sizeBonus。
4. Overlay：`seated` 并列【开始钓鱼】【打窝】；结束后回选项；金不足文字气泡；满 50 禁用。
5. 换窝料重置层；离塘/换位清空；与 GEAR-01 按口饵互不替代。
6. 埋点 `groundbait_cast_started` / `groundbait_applied` / `groundbait_rejected` → metrics-catalog-sync。
7. 自检（建议 `verify:feat-ground-01`）。

## 不做

- 全塘共享窝、窝料库存、线性叠乘、船联动、Mobile

## 验收

对照 spec §5；完成后 Checklist B 回写 **已实现**。

## 派发

```text
@docs/planning/prompts/feat-ground-01-groundbait-dev.prompt.md 按此实现 FEAT-GROUND-01
```

建议角色：`@backend-dev` + Unity Overlay。
