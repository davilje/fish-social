# FEAT-FISH-CN-01 开发交接：中国鱼种与区域分布

你是 Fish Social **后端 + shared + Unity 文案** 开发 Agent。策划已完成，**只写代码与数值表**。

## 必读（顺序）

1. `docs/planning/specs/中国鱼种与区域分布.md` — **以最新修订为准**（无 habitat 表）
2. `docs/planning/specs/appendix/中国鱼种名录-v1.md` — 50 种权威清单
3. `docs/planning/specs/塘生态数值表驱动.md` — `pond_fish_pool` 列与出鱼接线（FEAT-POOL-01）
4. `scripts/game-data/build_balance_xlsx.py` / `fish_cn_data.py` — 扩表入口

## 与 POOL-01 关系

- **本票**：有什么鱼、塘叫什么、**每塘种池终稿**（xlsx `pond_fish_pool`）。
- **POOL-01**：从表 spawn、品质权重∩种带、公式常数、重量展示。
- 同迭代：先 POOL 接线，再用本票 50 种 + 全塘 pool **覆盖**模板数据。

## P0 实现清单

1. xlsx：`fish_species` 50 行（附录列 + `qualityMin=1` / `qualityMax` by rarity）；`ponds` 改中文名 + `bioRegion`/`waterType`/`realWorldRef`；全塘 `pond_fish_pool`（**仅种**，无 quality 列）。
2. **不要**新增或依赖 `fish_species_habitat` sheet（已废弃）。
3. `npm run game-data:build` + `game-data:export`
4. `shared/fish.ts`：种库读表；`LEGACY_SPECIES_ID_MAP`；删除外来 ID。
5. `pondCatalog.ts` + Unity `WorldMapPonds.json` 塘名同步 §3.2。
6. DB migration：旧 ID 映射（spec §3.6）；塘内可重 seed。
7. `scripts/verify-fish-cn-01.ts` + `verify:fish-cn-01`

## 非目标

- 改咬钩/脱钩公式形状
- 新塘 / 改 tile
- Mobile 全量
- 恢复 habitat 中间表

## 完成后

- [ ] spec §5 全勾；状态 → **已实现**（须用户验收）
- [ ] `build-master-plan-xlsx.py` 完成时间当天
- [ ] `npm run planning:master-xlsx`

## 自检

```bash
npm run game-data:export
npm run verify:fish-cn-01
npm run verify:feat-pool-01
```

## 派发

```text
@docs/planning/prompts/feat-fish-cn-01-species-regions-dev.prompt.md 按此实现 FEAT-FISH-CN-01
```

建议角色：`@backend-dev`（兼 shared + Unity 文案/GameData）。
