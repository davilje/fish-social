# 开发提示词：Overlay HUD 正式素材与文字对齐（STEAM-DESKTOP-14C）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**（协同美术换 OverlayHud Sprite）。动态按钮文案仍走 IPC，禁止烤进 PNG。

## 必读

1. `docs/planning/specs/Steam桌面端-14COverlayHUD正式素材与文字对齐.md`（**已实现** / **STEAM-DESKTOP-14C**）
2. `docs/planning/specs/Steam桌面Overlay分塘底图与HUD同步.md`、`Steam桌面端-13OverlayHUD聊天栏与预制体对齐.md`
3. `OverlayHudExporter.cs`、`OverlayHudLayout.cs`

## 顺序

1. Prefab 换正式 Image；导出拷到 `OverlayResources/hud/`。
2. JSON 增加 fontFile / fontSize / fontColor / fontWeight / textAlign / contentAlign；字体放到 `hud/fonts/`。
3. Overlay **停止**对所有 TextBlock 强制左对齐。
4. 自检 spec §5（矩形 ≤1px；字形允许 1px 级差）。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：**须用户确认** 后再把 spec 改为已实现并刷新计划表。

```text
@docs/planning/prompts/steam-desktop-14c-hud-art-text-dev.prompt.md 按此实现 STEAM-DESKTOP-14C
```
