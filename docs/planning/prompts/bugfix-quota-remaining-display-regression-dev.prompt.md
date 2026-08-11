# 开发提示词：进塘与钓鱼剩余展示回归（BUG-20）

你是 Fish Social **前端 + 后端** Agent。在 **BUG-19** 单一口径已落地的前提下，修复两类展示回归；**禁止**再改 checkpoint 前移展示锚点，或恢复 `todayFishingMs - elapsed` 反推。

## 必读

1. `docs/planning/specs/BUG修复-进塘与钓鱼剩余展示回归.md`（**已实现** / **BUG-20**）
2. `docs/planning/specs/BUG修复-每日额度单一口径重构.md`（**BUG-19**）
3. `mobile/lib/fishingDuration.ts` · `usePondSocket.ts` · `mobile/app/pond/[id].tsx`
4. `server/src/socketPondHandlers.ts` · `shared/types.ts`
5. 回归：`npm run verify:bug19-quota` · `npx tsx scripts/verify-bug14-daily-remaining.ts`

## 实现要点（对照已落地代码）

1. **钓鱼中剩余**：开钓冻结 `todayFishingBaseMs`，已用 = 冻结基线 + `(now - sessionStartedAt)`；禁止用 local/server `sessionFishingMs` 差值扣 remaining。
2. **checkpoint**：本地基线不得随 enrich 上调；仅日切允许下降。
3. **join_pond ack**：返回 `todayFishingBaseMs` / `todayRemainingMs` / `quotaDateKey`；客户端写入 seed，快照前可种本人 stub。
4. **合并**：snapshot / joined / updated 与 seed 合并；`pond_user_updated` 本人缺失时 upsert。
5. **闲置**：`effectiveTodayUsedMs` 取 base / ms / remaining 反推中的 max。
6. **底栏**：ack 已带额度即可显示，不必死等完整 snapshot；仍禁止误显「今日已满」。

## 非目标

- 不改 8h / 上海换日 / BUG-19 settle 出口
- 不重做 FISH-SPOT-1 选点交互

## 验收

对照 spec §5；完成后 Checklist B：

- [x] `verify:bug19-quota` + `verify-bug14-daily-remaining` 通过
- [x] 手动：未选点剩余正确；开钓剩余走动
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/bugfix-quota-remaining-display-regression-dev.prompt.md 按此实现 BUG-20
```

建议角色：`@frontend-dev`（主）+ `@backend-dev`（join ack / self updated）
