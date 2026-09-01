# Overlay 宠物资源（按猫种 × 姿势分套）

Unity 主窗口和 Overlay **共用同一套路径**。`petId` 来自玩家头像（与 `shared/defaultAvatars.ts` 一致），姿势 clip 来自 `petVisualState`。

## 目录（权威源）

```text
desktop-overlay/OverlayResources/pets/<petId>/<clip>/0.png
desktop-overlay/OverlayResources/pets/<petId>/<clip>/1.png
…
```

橘猫六姿势与 clip 对应（文件夹名保持英文，供加载器匹配）：

| clip | 姿势 | fishingPhase / 时机 | 播放 |
|------|------|---------------------|------|
| `idle` | 站立 | `idle` / disconnected / 未入座 | 循环 |
| `sit` | 落座（无独立图时回退 `fishing`） | `seated` / `groundbaiting` | 循环 |
| `cast` | 甩杆 | `baiting` / `casting` | 播完停在末帧 |
| `fishing` | 坐着钓鱼 | `waiting` | 循环 |
| `hooked` | 用力拉杆 | `hooked` | 循环 |
| `reel` | 拉杆 | `resolving` / `stopping` | 播完停在末帧 |
| `catch` | 钓到鱼 | 收杆后 `seated` 且待领鱼获 | 播完停在末帧 |

`dragging` / `offline` 无独立目录，回退 `idle`。

| petId | 头像 | 中文 |
|-------|------|------|
| orange | cat_avatar_orange.png | 橘猫 |
| calico | cat_avatar_calico.png | 三花猫 |
| gray | cat_avatar_gray.png | 灰猫 |
| siamese | cat_avatar_siamese.png | 暹罗猫 |
| tuxedo | cat_avatar_tuxedo.png | 燕尾猫 |
| white | cat_avatar_white.png | 白猫 |

## 尺寸

| 项 | 约定 |
|----|------|
| 源图 | 正方形透明 PNG（256 或 512 均可；同猫各 clip 画布与脚底对齐） |
| Overlay 显示 | `actor-pet` 矩形（默认 128×128），`Stretch.Uniform` |
| 悬停热区 | `actor-hit` 角色盒（缺省回退整块 `actor-pet`） |

序列帧：同一 clip 目录内连续 `0.png`、`1.png`…（最多 16 帧）。缺号即停。

## 兼容旧文件

仍支持 `pets/<petId>/<clip>-0.png` 与 `fishing-0.png`（加载顺序靠后）。

## 同步

Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay**（或重新 Debug 打包），会把整个 `OverlayResources` 拷到 exe 旁。

## 查找顺序（每种猫、每个 clip）

1. `pets/<petId>/<clip>/N.png`
2. `pets/<petId>/<clip>-0.png`
3. `sit` 缺图 → `fishing/`；`catch` 缺图 → `reel/`
4. 同猫 `idle/`
5. 同猫 `fishing/`
6. `cat.png` / 矢量占位
