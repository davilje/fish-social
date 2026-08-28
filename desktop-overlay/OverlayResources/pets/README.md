# Overlay 宠物资源（按猫种分套）

Unity 主窗口和 Overlay **共用同一套名字**。`petId` 来自玩家头像（与 `shared/defaultAvatars.ts` 一致），姿势来自 `petVisualState`。

## 目录（权威源）

```text
desktop-overlay/OverlayResources/pets/<petId>/<state>-0.png
```

| petId | 头像 | 中文 |
|-------|------|------|
| orange | cat_avatar_orange.png | 橘猫 |
| calico | cat_avatar_calico.png | 三花猫 |
| gray | cat_avatar_gray.png | 灰猫 |
| siamese | cat_avatar_siamese.png | 暹罗猫 |
| tuxedo | cat_avatar_tuxedo.png | 燕尾猫 |
| white | cat_avatar_white.png | 白猫 |

`<state>`：`idle` / `fishing` / `hooked` / `catching` / `dragging` / `offline`

## 你现在有一张钓鱼橘猫时

1. 保存为：

```text
desktop-overlay/OverlayResources/pets/orange/fishing-0.png
```

2. Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay**（或重新 Debug 打包）。
3. 重启游戏 / Overlay。

塘内头像是橘猫、且状态为钓鱼的玩家会显示这张图。其它猫种仍走占位矢量猫，直到你补上对应文件夹。

只有一张图时：同一文件也可复制为 `idle-0.png`，否则待机会回退用 `fishing-0.png`。

## 查找顺序（每种猫）

1. `pets/<petId>/<state>-0.png`（可 `-1` `-2` … 序列帧）
2. `pets/<petId>/<state>.png`
3. 缺姿势时：`idle` → `fishing` → `cat.png`
4. 最后才用旧的全局 `OverlayResources/cat.png`（全塘同一只，不推荐）

不要再把一张图命名为根目录 `cat.png` 指望区分玩家——那会让所有人长一样。
