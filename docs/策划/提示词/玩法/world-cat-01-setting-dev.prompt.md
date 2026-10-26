# 开发提示词：猫咪世界观与场景描述（WORLD-CAT-01）

你是 Fish Social 的内容与桌面客户端开发。按规格改文案和显示字段，勿扩需求。

## 必读

1. `docs/策划/规格/世界观/猫咪世界观与场景描述.md`（**已确认** / **WORLD-CAT-01**）
2. `docs/策划/规格/玩法/中国鱼种与区域分布.md`（鱼种表和 pondId 不要改）
3. `shared/生成/game-data/ponds.json` 现有 `bioRegion`、`realWorldRef`

## 顺序

1. 为五个现有 bioRegion 写上规格里的分区名和短介绍。
2. 每个塘增加 `sceneBlurb`，显示名可以改，`pondId` 不变。
3. 世界地图和选塘详情改读新文案，界面不再显示 `realWorldRef`。
4. 不实现 STEAM-DESKTOP-22J 的布局，不改出鱼表和票价。

## 验收

对照 spec §5。完成后按 Skill `planning-progress-sync` 把 spec 改为 **已实现**，并 `npm run planning:master-xlsx`。

## 派发

```text
@docs/策划/提示词/玩法/world-cat-01-setting-dev.prompt.md 按此实现 WORLD-CAT-01
```
