# 开发提示词：钓点手动选择（FISH-SPOT-1）

你是 Fish Social **前端 + 后端** Agent。按规格把「开始钓鱼自动占点」改为「先手动选点落座，再开钓」。

## 必读

1. `docs/planning/specs/钓点手动选择.md`（**已实现** / **FISH-SPOT-1**）
2. 现状：`pond/[id].tsx` 用 `freeSpot ?? botSpot` 自动选点后 `start_fishing`；服务端 `start_fishing` = `startFishing(占点)` + `beginFishingSequence`
3. `PondScene.tsx`（钓点渲染）· `usePondSocket.ts` · `pondSession.startFishing` · `socketPondHandlers`

## 顺序

1. **服务端**：拆分占点与开钓（建议 `take_spot` + 收紧后的 `start_fishing`）；开钓要求已有 `spotId`  
2. **共享类型**：补 Socket 事件 / payload  
3. **客户端**：场景点击可用钓点 → `take_spot`；删除自动 `freeSpot` 逻辑；底栏未落座禁用开钓  
4. **交互**：拖地图 vs 点选冲突处理好；钓鱼中禁止换座  
5. demo 模式对齐；自检验收清单  

## 非目标

改咬钩公式、日额度、bot 自动进塘策略、大改 Tilemap。

## 验收

对照 spec §5；完成后按 Skill `planning-progress-sync` Checklist B → **已实现** + `npm run planning:master-xlsx`。

## 派发

```text
@docs/planning/prompts/fish-spot-manual-select-dev.prompt.md 按此实现 FISH-SPOT-1
```

建议角色：`@frontend-dev` + `@backend-dev`
