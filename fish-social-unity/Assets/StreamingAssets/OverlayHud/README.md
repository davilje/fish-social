# Overlay HUD 源图（Unity 用）

裁好的 PNG（RGBA）可放在：

- **`Assets/Desktop/OverlayArt/`**（当前美术资源主目录）
- **`Assets/StreamingAssets/OverlayHud/`**（可选，与 brief 英文命名一致）

在 Unity 中设为 **Sprite (2D and UI)**，拖到 `Assets/Resources/Desktop/Prefabs/OverlayHud.prefab` 对应控件的 **Image → Source Image**。

**不必手改 `DesktopOverlayHudWidget.spriteFile`**：Export 会以 Prefab 上 Image 绑定的 Sprite 为准，并自动写回 `spriteFile`。

**按钮文字**：在 Prefab 子物体 `Label`（`Text`）里填写；留空或删除 `Text` 组件即导出 `"label":""`（纯图标按钮）。字体样式仅在有 `Text` 组件时导出。

**点击反馈**：Overlay 对带 `sprite` 的按钮做 0.2s 的 1→1.1→1 缩放（不导出 Unity ColorBlock）。

建议命名（与 brief 一致）：

```text
ui_btn_plate_sm.png
ui_btn_plate_pan.png
ui_btn_plate_xs.png
ui_panel_status.png
ui_panel_chat_dock.png
ui_panel_chat_input.png
ico_menu.png
ico_map.png
…
```

换图后必须二选一：

1. **Fish Social → Export Overlay HUD**，再打包；或  
2. 直接 **Fish Social → 打包/Debug|Release**（现已自动先 Export HUD 再拷贝）。

导出目标：`desktop-overlay/OverlayResources/hud/`（Overlay 运行时读这里）。  
只改 Prefab 不 Export，旧 `overlay-hud.json` / PNG 仍会进包。

不要把「开始钓鱼」等动态字烤进 PNG。
