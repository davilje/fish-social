# 开发提示词：每日额度单一口径重构（BUG-19）

你是 Fish Social **前端 + 后端** Agent。按规格做**结构性重构**，禁止再打「merge / 反推基线 / 局部 ack」类补丁。目标：消灭约 30s 跳变、收杆回弹 8h、重进「今日已满」。

## 必读

1. `docs/planning/specs/BUG修复-每日额度单一口径重构.md`（**已实现** / **BUG-19**）
2. 背景定责：`BUG-14`（整局锚点插值）× `BUG-16`（30s 前移锚点）冲突；`BUG-15`/`BUG-18` 叠加多写入口
3. `server/src/pondUserManager.ts` · `fishingStateMachine.ts` · `socketPondHandlers.ts` · `serverLoops.ts`
4. `mobile/lib/fishingDuration.ts` · `usePondSocket.ts` · `mobile/app/pond/[id].tsx`
5. `shared/types.ts`；回归 `npm run verify:fish-daily-shanghai`、`verify:pond-navigation`

## 实现顺序

1. **字段契约**：`todayFishingBaseMs`=DB 已落账；`sessionStartedAt`=本局展示锚点；`todayRemainingMs` 由服务端 enrich；`todayFishingMs` 仅派生兼容（= base + sessionElapsed）。
2. **结算出口**：收紧 `settleFishingSession`：
   - `checkpoint`：只写 DB / 更新 base，**禁止**改 `sessionStartedAt`
   - `finalize`：结算剩余段后清空展示锚点
   - stop / leave / disconnect / 相位真正结束全部只走此出口
3. **改造 30s 扫描**：`syncHumanQuotaAndEmit` 在钓路径改 `checkpoint`，删除「advance 前移 fishingStartedAt/sessionStartedAt」。
4. **收杆顺序**：`stop_fishing` **先 finalize**，ack 返回 `todayFishingBaseMs` / `todayRemainingMs` / `quotaDateKey`，再广播与 ack 同口径的用户；stopping 动画不得成为唯一结算点。
5. **开钓**：`start_fishing` 校验 `seated` + 剩余 > 0；设置 `sessionStartedAt=now`（及兼容期 `fishingStartedAt` 同步，但额度逻辑只认 session）。
6. **客户端**：删除 `todayFishingMs - elapsed` 反推；剩余只信 base + session 或 `todayRemainingMs`；收杆 ack 立即采用最终 base；禁止 finalize 后复活锚点。
7. **脏数据**：verify 检测异常满额行；文档/注释说明运维校正，不自动清空全服。
8. **测试**：扩展或新增 verify——跨 30s 扫描剩余不回满；收杆 +≈elapsed；重进一致；checkpoint 不改 sessionStartedAt；重复 settle 不双计。

## 非目标

- 不改 8h / 上海 00:00 规则
- 不做离线挂机计时
- 不重做 FISH-SPOT-1 / BUG-17 / 演示开关

## 验收

对照 spec §5；完成后按 Skill `planning-progress-sync` Checklist B：

- [ ] `npm run verify:fish-daily-shanghai`（及本单新增 verify）通过
- [ ] 手动：开钓 >60s 无跳变；收杆不回弹；重进剩余正确
- [ ] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/bugfix-daily-quota-single-source-dev.prompt.md 按此实现 BUG-19
```

建议角色：`@backend-dev` + `@frontend-dev`（先服务端契约，再客户端只读）
