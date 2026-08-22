# 开发提示词：钓鱼相册 / 图鉴 / 资料 / 成就（FEAT-ALBUM-01）

你是 Fish Social **Unity 主窗 UI + 后端 API** 开发 Agent。按规格实现，勿扩需求。**须先对齐 UI 草案再大改壳。**

## 必读

1. `docs/planning/specs/钓鱼相册与成就.md`（**已确认** / **FEAT-ALBUM-01**）
2. `docs/design/ui/steam-profile-hub.md`（个人中心 IA / wireframe）
3. `docs/planning/specs/Steam桌面端-08E个人中心与资料编辑.md`、既有图鉴 / showcase
4. （可选）`docs/planning/specs/回鱼机制.md`（回鱼入相册/成就）

## 顺序

1. 确认/细化 `docs/design/ui/steam-profile-hub.md`（可与 @ui-designer 协作）；禁止缩回双弹窗割裂主路径。
2. 表 `achievements` + 云库 `player_album_pins` / `player_achievements`；聚合 API `profile-hub`。
3. Unity：新个人中心壳（侧栏：资料 / 展示柜 / 图鉴 / 相册 / 成就）；迁入 08E 编辑与图鉴能力。
4. 相册墙：精选 N=12、钉选、自动候选规则；showcase 8 格并存。
5. 成就解锁检查 + toast + 他人只读可见（隐私）。
6. 埋点 `achievement_unlocked` / `album_pin_changed` / `profile_hub_opened` → metrics-catalog-sync。
7. 自检（建议 `verify:feat-album-01`）。

## 不做

- 全量时间线日志、Steam 成就 API、AI 纪念照、Mobile 对齐

## 验收

对照 spec §5；完成后 Checklist B 回写 **已实现**。

## 派发

```text
@docs/planning/prompts/feat-album-01-profile-codex-album-dev.prompt.md 按此实现 FEAT-ALBUM-01
```

建议角色：Unity 主窗 UI 为主 + `@backend-dev`；UI 稿可先 `@ui-designer`。
