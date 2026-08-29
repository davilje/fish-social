# Overlay 宠物资源（按猫种 × 姿势分套）

Unity 主窗口和 Overlay **共用同一套路径**。`petId` 来自玩家头像（与 `shared/defaultAvatars.ts` 一致），姿势 clip 来自 `petVisualState`（STEAM-DESKTOP-ART-03）。

## 目录（权威源）

```text
desktop-overlay/OverlayResources/pets/<petId>/<clip>/0.png
desktop-overlay/OverlayResources/pets/<petId>/<clip>/1.png
…
```

| petId | 头像 | 中文 |
|-------|------|------|
| orange | cat_avatar_orange.png | 橘猫 |
| calico | cat_avatar_calico.png | 三花猫 |
| gray | cat_avatar_gray.png | 灰猫 |
| siamese | cat_avatar_siamese.png | 暹罗猫 |
| tuxedo | cat_avatar_tuxedo.png | 燕尾猫 |
| white | cat_avatar_white.png | 白猫 |

| clip | 中文 | fishingPhase |
|------|------|--------------|
| idle | 待机 | idle / disconnected |
| sit | 坐下 | seated / groundbaiting |
| cast | 抛竿 | baiting / casting |
| fishing | 等鱼 | waiting |
| hooked | 咬钩 | hooked |
| reel | 收杆 | resolving / stopping |

`dragging` / `offline` 无独立目录，回退 `idle`。

## 尺寸

| 项 | 约定 |
|----|------|
| 源图 | 正方形透明 PNG **256×256**（等比例；加载器不校验像素，但请按此出图） |
| Overlay 显示 | 固定 **64×64**（`OverlayPetActor.BodySize`），`Stretch.Uniform` |
| 悬停热区 | 仅猫身 **64×64**，不含昵称/状态条 |
| 悬停浮窗 | 约 **80×28**，水平居中对齐猫身，出现在角色簇上方 |

Unity 主窗口宠物用同一套源图目录，主视图占位仍可按 256 显示。

## 兼容旧文件

仍支持 `pets/<petId>/<clip>-0.png` 与 `fishing-0.png`（加载顺序靠后）。

## 同步

Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay**（或重新 Debug 打包），会把整个 `OverlayResources` 拷到 exe 旁。

## 查找顺序（每种猫、每个 clip）

1. `pets/<petId>/<clip>/N.png`
2. `pets/<petId>/<clip>-0.png`
3. 同猫 `fishing/` 或 `fishing-0.png`
4. 同猫 `idle/`
5. `cat.png` / 矢量占位
