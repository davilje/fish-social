# 开发提示词：今日额度跨日不刷新 / 误显已满（BUG-15）

你是 Fish Social **后端为主** Agent（客户端仅在确需时配合）。按规格修复「未钓鱼却今日已满」与上海日额度不自动恢复。

## 必读

1. `docs/planning/specs/BUG修复-今日额度跨日不刷新.md`（**已实现** / **BUG-15**）
2. `docs/planning/specs/每日钓鱼时长-上海日重置.md`（FISH-DAILY-1，已实现）
3. `server/src/pondUserManager.ts`：`ensureFishingDayRollover` · `enrichPondUser` · `addTodayFishingMs` · `getTodayFishingMs`
4. 勿回归：`BUG-14` 底栏插值、`BUG-13` 头顶秒表

## 顺序

1. **未在钓对齐 DB**：enrich / join / checkpoint 恢复时，非活跃钓鱼强制 `todayFishingMs = getTodayFishingMs(playerId)`（可封顶 MAX）  
2. **同日 dirty 内存**：`fishingDayKey === today` 时未在钓仍允许读库覆盖，禁止用 enrich 展示值写回当持久基线  
3. **跨日推送**：日切后对在塘用户 `emitPondUserUpdated`（或短周期扫描 rollover+emit），闲置也要能恢复额度  
4. **写入防护**：`addTodayFishingMs` 累计封顶；拒绝过旧/非法 `fishingStartedAt` 大段计时  
5. 扩展/新增 verify：脏内存被纠正、跨日闲置恢复；跑既有 `verify-fish-daily-shanghai-rollover`

## 非目标

改 8h 常量、Admin 清额度 UI、重做 BUG-14。

## 验收

对照 spec §4；完成后按 Skill `planning-progress-sync` Checklist B → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/bugfix-daily-quota-day-rollover-dev.prompt.md 按此实现 BUG-15
```

建议角色：`@backend-dev`（主）
