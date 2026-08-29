# 开发提示词：分塘底图、猫咪序列帧、HUD Prefab 同步 Overlay（STEAM-DESKTOP-ART-03）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**。只实现本编号；不要改 Node 钓鱼权威。

## 必读

1. `docs/planning/specs/Steam桌面Overlay分塘底图与HUD同步.md`（**已确认** / **STEAM-DESKTOP-ART-03**）
2. `docs/planning/specs/Steam桌面Overlay场景布局管线.md`（ART-02：钓位像素；与 HUD JSON 分开）
3. `docs/planning/reports/Unity桌面UI新建须同步Prefab.md`
4. `desktop-overlay/PondScenePresenter.cs`、`OverlayPetActor.cs`、`MainWindow.xaml`
5. `fish-social-unity/Assets/Scripts/Desktop/Pet/PetStateController.cs`、`PetArtLoader.cs`

## 顺序

1. **分塘底图**：`OverlayResources/ponds/<pondId>.png`，按 IPC `pondId` 加载；回退 `_default.png` → `pond.png`。
2. **序列帧**：建齐 `pets/<petId>/<clip>/`；源图 **256×256**，Overlay 显示 **64×64**；`fishingPhase` → clip（`idle/sit/cast/fishing/hooked/reel`）由 Unity 映射后写入 `petVisualState`；Overlay 按夹播帧。悬停热区为猫身 64×64。
3. **HUD**：Catalog 登记 `OverlayHud` Prefab（960×560）；编辑器导出 `OverlayResources/hud/overlay-hud.json` + PNG；Overlay 有表则按像素摆控件，无表回退 XAML。
4. 构建拷贝 `ponds/`、`pets/`、`hud/` 到 Overlay exe 旁。
5. 自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

- [ ] spec §5 全部可勾选
- [ ] 无 HUD JSON 时 Overlay 仍可用现有 XAML
- [ ] Named Pipe 不传贴图；不改服务端钓鱼权威

## 禁止

- Overlay 内 uGUI / 第二 Unity Player / Pipe 传图或 Prefab
- 修改 `mobile/`、`server/`、`shared/` 钓鱼相位权威
- 把 ART-02 钓位表和 HUD 表合成一个 JSON
- 重写 07A～07E 主窗口业务页

## 派发

```text
@docs/planning/prompts/steam-desktop-art-03-pond-pet-hud-dev.prompt.md 按此实现 STEAM-DESKTOP-ART-03
```
