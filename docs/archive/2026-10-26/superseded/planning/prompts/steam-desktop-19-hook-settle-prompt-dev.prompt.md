# 开发提示词：咬钩结算弹窗（STEAM-DESKTOP-19）

> 2026-10-26 起不再派发。正常鱼塘不再做单次结算卡。

你是 Fish Social **后端 + Unity 桌面 / Overlay + Web 弹窗**工程师。按规格实现，勿扩需求。

## 必读

1. `docs/archive/2026-10-26/superseded/planning/specs/Steam桌面端-19咬钩结算弹窗.md`（已归档，勿再实现）
2. Web 参考：`mobile/components/CatchFishModal.tsx`
3. 回鱼：`server/src/catchSettlement.ts`、`server/src/returnFish.ts`、`shared/returnFee.ts` `isReturnEligible`
4. Overlay chrome：`desktop-overlay/MainWindow.xaml` `OverlayPromptChrome`

## 顺序

1. 服务端：`advanceFromHooked` 人类成功上钩一律 `settleAcceptedCatch(..., { auto: true })`（出售档也入包）。不要再靠 `fish_bite` + `accept_catch` 作为正式领取。
2. Overlay：`fish_catch_settled` / `fish_miss` 填 `overlayPromptKind`=`hook_keep`|`hook_return`|`hook_escape`。文案见 spec §3.2。
3. 去掉 Overlay「领取鱼获」与 Unity `accept_catch` 动作表；空闲坐席第三钮只有离席。
4. `confirm_overlay_prompt` 只关弹窗。倒计时 5s 自动关。
5. Web `CatchFishModal` / `usePondSocket` 听 `fish_catch_settled`，确认不再 `accept_catch`。
6. 教学关 STEAM-DESKTOP-11 弹窗不要改坏。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 出售档入包弹窗；回鱼档可回显示价格；脱钩弹窗
- [x] Overlay 无「领取鱼获」
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/desktop/steam-desktop-19-hook-settle-prompt-dev.prompt.md 按此实现 STEAM-DESKTOP-19
```
