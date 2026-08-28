# Unity 桌面端：新 UI 必须同时出 Prefab

## 规则（强制）

1. **登记** `DesktopPrefabCatalog.All`（`fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopPrefabCatalog.cs`）。
2. **Ensure 回调**一步完成：缺则创建 + 灌布局（禁止只做空壳）。
3. **管理器创建**：`Fish Social → UI Prefab 管理` → 缺失项点「创建」，或「补齐全部」。
4. **运行时**：`Resources.Load("Desktop/Prefabs/…")` / `DesktopUiPrefabFactory` / `DesktopFeaturePanelFactory`；**禁止**新功能默认 `new GameObject` 拼整页。
5. **禁止**再加 `Fish Social/Bake Xxx` 单面板菜单。
6. Debug IMGUI（如玩法 Debug）可例外；UGUI 必须 Prefab。

## 验收

- Catalog 有行；磁盘有 `.prefab`；运行时无整页 BuildFallback（缺 Prefab 应 Warning 并引导补齐）。
- 列表项用独立 item Prefab（如 `ShopItemCard`）。

## 审计

见 [Unity桌面UI预制体审计-2026-08-25.md](./Unity桌面UI预制体审计-2026-08-25.md)。
