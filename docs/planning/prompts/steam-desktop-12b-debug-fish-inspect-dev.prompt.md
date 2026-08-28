# 开发提示词：Overlay Debug 鱼塘/钓位查看（STEAM-DESKTOP-12B）

你是 Fish Social **后端 + Unity/Overlay** Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/Steam桌面端-Debug鱼塘钓位查看.md`（**已确认** / **STEAM-DESKTOP-12B**）
2. 依赖：`docs/planning/specs/Steam桌面端-玩法Debug菜单.md`（STEAM-DESKTOP-12）
3. 参考：`server/src/gameplayDebug.ts`、`server/src/fishingDebug.ts`、`server/src/pondEcology.ts`、`desktop-overlay/MainWindow.xaml`、`NativeOverlayStateDto.cs`、`DesktopAppBootstrap.cs`

## 顺序

1. 服务端：`GET /api/debug/gameplay/pond-fish`、`GET /api/debug/gameplay/spot-stats`、`POST force_bite` + `getPondFishById` / `forceDebugBiteHook`
2. Unity：API 拉取 → 写入 Overlay state debug 载荷；解析 `list_pond_fish` / `list_spot_fish` / `spot_stats` / `force_bite:<id>`
3. Overlay：弹窗 Width=468、三列 UI；列表点选属性；强制上钩按钮
4. `npm run verify:steam-desktop-12b`（或等价脚本）自检

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 相关 verify 通过（`npm run verify:steam-desktop-12b`）
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/steam-desktop-12b-debug-fish-inspect-dev.prompt.md 按此实现 STEAM-DESKTOP-12B
```
