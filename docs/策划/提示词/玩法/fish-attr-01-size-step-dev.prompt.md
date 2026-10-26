# 开发提示词：体长体重步进（FISH-ATTR-01）

你是 Fish Social 后端。只改体长和体重的步进，不改成长曲线和概率系数。

## 必读

1. `docs/策划/规格/玩法/钓鱼体长体重步进与公式评判.md`（**已确认** / **FISH-ATTR-01**）
2. `shared/fishing.ts` 的 `round2`、`growFishSizeV2`、`calcEscapeGrowthSize`
3. `LENGTH_WEIGHT_A/B` 只用于展示

## 顺序

1. 体长在出生、成长、脱钩加成之后按 0.05 米取整，不低于 0.02 米，不超过品质上限。
2. 体重展示按 0.1 公斤取档，不入库。
3. 卖价和经验使用取整后的体长。
4. 不要修改 `GROWTH_CURVE_*`、`ESCAPE_AT_40M`、`BITE_BASE_SCALE`、`SIZE_BITE_K`、`NEAR_MAX_SPAWN_CHANCE`。

## 验收

对照 spec §6。完成后把 spec 改为 **已实现**，并 `npm run planning:master-xlsx`。

## 派发

```text
@docs/策划/提示词/玩法/fish-attr-01-size-step-dev.prompt.md 按此实现 FISH-ATTR-01
```
