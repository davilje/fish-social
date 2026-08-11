# 开发提示词：离塘导航失效与收杆按钮闪烁（BUG-17）

你是 Fish Social **前端** Agent（`.cursor/rules/frontend-dev-agent.mdc`，仅改 `mobile/`）。按规格修复「返回地图无效 → 死屏『请先加入鱼塘』」与「收杆按钮闪两下」。

## 必读

1. `docs/planning/specs/BUG修复-离塘导航失效与收杆按钮闪烁.md`（**已实现** / **BUG-17**）
2. `docs/planning/specs/BUG修复-切页误离塘与计时中断.md`（BUG-06 口径：切页**不**离塘）
3. `mobile/app/pond/[id].tsx`：`handleLeaveToMap` · `handleLeaveToSocial` · 底栏按钮 · `remainingDailyFishingMs`
4. `mobile/lib/usePondSocket.ts`：`emitLeavePond` / `leftPondRef` / socket effect 与 cleanup
5. 勿回归：BUG-13 头顶秒表不闪 0、BUG-14 钓鱼中剩余逐秒下降

## 顺序

1. **先导航后离塘**：`router.canGoBack() ? router.back() : router.replace('/')`；确认导航生效后再发 `leave_pond`
2. **闩锁可复位**：`leftPondRef` 允许复位；页面仍挂载却无服务端 session 时能重新 `join_pond`
3. **死态自愈**：收到 `请先加入鱼塘` 或 `me` 丢失且仍在鱼塘页时，自动重新 join 一次（带节流与提示）
4. **社交出口**：按 BUG-06 保持会话，但确保鱼塘页卸载不静默丢会话
5. **按钮中间态**：`stopping` 显示「收杆中…」并禁用；开始钓鱼 ack 后保持 pending 直到进入活跃相位（pending 设 3s 超时回落）

## 非目标

改服务端结算（见 BUG-16）、改 8h 上限与底栏插值语义、重做导航栈。

## 验收

对照 spec §4（含 Web 无历史直接打开 `/pond/<id>` 能返回地图）；完成后按 Skill `planning-progress-sync` Checklist B → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/bugfix-leave-pond-navigation-dev.prompt.md 按此实现 BUG-17
```

建议角色：`@frontend-dev`
