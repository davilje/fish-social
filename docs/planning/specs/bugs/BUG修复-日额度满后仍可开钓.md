# BUG-23：日扣满 4 次 / 满 8h 后仍可开始钓鱼

## 元信息

| 字段 | 内容 |
|------|------|
| 编号 | `BUG-23` |
| 类型 | Bug修复 |
| 状态 | **已实现** |
| 设计时间 | **2026-08-23** |
| 完成时间 | **2026-08-23** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 依赖 | FEAT-PROG-01 入场扣费、`MAX_DAILY_FISHING_MS` |

## 1. 现象与复现

1. 收费塘钓鱼，累计有效时长触发入场费扣款 **4 次**（对齐 8h）。
2. 停钓回到 `seated` 后再点「开始钓鱼」。
3. **实际**：仍可开钓。  
4. **期望**（对齐 Web）：今日钓满 8h / 日扣满后 **不可开始钓鱼**；**仍可落座**。

## 2. 根因

- `canStartFishingWithFee` 仅在 `needsFeeToContinue`（下一刀付不起）时拦截；**charges 达 `maxFeeChargesPerDay` 后未拦截**。
- 自动续钓 `advanceFromResolving → enterBaiting` 未检查日额度 / 扣费上限。
- `take_spot` 在满 8h 时直接拒绝，与「可落座、不可开钓」不符。

## 3. 修复

| 项 | 改动 |
|----|------|
| 开钓门禁 | `charges >= maxFeeChargesPerDay` → 拒绝「今日钓鱼时长已用完」 |
| 日额度 | 保留 `getTodayFishingMs >= MAX_DAILY_FISHING_MS` |
| 自动续钓 | 额度满或扣费满 → 停 `seated`，不续钓 |
| 结算 tick | 扣满 4 次或日额度满 → `handleStopFishing` |
| 落座 | 移除 `take_spot` 的 8h 拒绝 |

## 4. 验收

- [x] 收费塘扣满 4 次后 `start_fishing` 失败，文案含「已用完」
- [x] 日累计 ≥8h 后不可开钓
- [x] 满额后仍可 `take_spot` / 落座
- [x] `npm run verify:quota-daycap`

## 5. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-23 | 开发 | 修复并验收脚本落地 |
