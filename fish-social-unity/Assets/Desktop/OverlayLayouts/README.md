# Overlay 鱼塘场景布局（STEAM-DESKTOP-ART-02）

每个 `pondId` 一份 **960×560** Prefab，用来摆该塘的背景装饰和钓位锚点。运行时 Overlay **不加载** 这些 Prefab，只读导出的 JSON。

## 怎么用

1. Unity 菜单 **Fish Social → UI Prefab 管理** → `OverlayPondLayout` → **创建**（或「补齐全部」）。  
   会生成 `Assets/Desktop/OverlayLayouts/<pondId>.prefab`（每个鱼塘一份）。
2. 打开某一塘的 Prefab，拖动黄色钓位（必须绑定已有 `spotId`，例如 `calm-spot-1`）。
3. **Fish Social → Export Overlay Layout**（当前塘）或 **Export Overlay Layout（全部塘）**。  
   写出 `desktop-overlay/OverlayResources/layouts/<pondId>.json`。失败不会写半份文件。
4. 重新打包或拷贝 OverlayResources 后重启 Overlay。有 JSON 的塘停用 `MapToScene`；没有 JSON 的塘仍自动缩放。

「全部塘」会给每个已有 Prefab 写 JSON；一旦写出，该塘 Overlay 就按表摆猫，不再自动缩放。未调过的塘可以先不导出。

## 规则

- 画布必须是 960×560，Scaler Constant Pixel Size、Scale Factor 1。
- 猫咪 Overlay 显示 **64×64**（源图 256×256）；钓位锚点对准猫脚底中心。
- 钓位 `spotId` 必须是该塘 GameData 里已有的 id，不能在 Prefab 里发明新钓位。
- 不要把菜单 / 钓鱼按钮 / 聊天条放进本 Prefab（那是 OverlayHud / ART-03）。
- 导出失败不会写半份 JSON。
