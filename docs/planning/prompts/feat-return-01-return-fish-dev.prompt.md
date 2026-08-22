# 开发提示词：回鱼机制（FEAT-RETURN-01）

你是 Fish Social **后端为主、Unity Steam UI 为辅**的开发 Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/回鱼机制.md`（**已确认** / **FEAT-RETURN-01**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（卖价 `calcFishSellPrice`、双熟练度）
3. `server/src/inventory.ts`、卖出 API、`DesktopCatchBagModalView.cs`

## 顺序

1. 数值表 sheet `return_rules`（字段见 spec）→ `npm run game-data:export`。
2. `POST /api/inventory/return-to-pond`：准入（品质/尺寸/满尺寸/在塘）→ 删包 → 塘内增重 → 发金+XP。
3. 卖出路径保持不回写塘实体。
4. Unity：收杆弹窗 + 背包「回鱼」；失败原因明确。
5. 埋点 `fish_returned_to_pond` → 按 metrics-catalog-sync 入库。
6. 自检脚本（建议 `verify:feat-return-01`）。

## 不做

- 跨塘回鱼、保管箱、Mobile 对齐、全量 return log 表（可选后置）

## 验收

对照 spec §5；完成后按 Skill `planning-progress-sync` Checklist B 回写 **已实现**。

## 派发

```text
@docs/planning/prompts/feat-return-01-return-fish-dev.prompt.md 按此实现 FEAT-RETURN-01
```

建议角色：`@backend-dev` + Unity 收杆/背包 UI。
