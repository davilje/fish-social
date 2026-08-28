# 美术资源目录

| 子目录 | 负责人 | 内容 |
|--------|--------|------|
| `direction/` | 美术总监 (@art-director) | 美术风格规范、视觉指南、ComfyUI 对接说明 |
| `generated/` | 出图脚本 staging | `npm run art:generate` 输出（PNG 默认不入库，见 `manifest.json`） |
| `animations/` | 动效师 (@motion-designer) | Lottie/SVG 动效文件及接入说明 |

项目根目录 `mobile/assets/` / `fish-social-unity/Assets/` 存放正式引用资源，`docs/art/` 存放规范、设计说明与出图 staging。

## ComfyUI 出图管线

1. 本地 ComfyUI（默认 `http://127.0.0.1:8188`）+ `ComfyUI-APIimage` 节点  
2. 配置见 [direction/comfyui-setup.md](./direction/comfyui-setup.md)  
3. 风格 / preset：[direction/style-guide.md](./direction/style-guide.md)、[config/art/direction.json](../../config/art/direction.json)

```bash
npm run art:comfyui:health
npm run art:generate -- --preset desktop-cat --prompt "orange tabby cat fishing, side view"
```

## 把图换进 Overlay / 主窗口

按 **猫种 id + 姿势** 投放，塘内每人用自己头像对应的那套（与 Unity 同名）。不要只用一张全局 `cat.png`。

权威目录：[desktop-overlay/OverlayResources/pets/README.md](../../desktop-overlay/OverlayResources/pets/README.md)

```text
desktop-overlay/OverlayResources/pets/orange/fishing-0.png
```

Unity 菜单 **Fish Social → 同步宠物美术到 StreamingAssets + Overlay** 后重启。
