---
name: overlay-hud-image-prompt
description: >-
  Writes paste-ready gpt-image / HUD UI kit prompts for Fish Social Overlay
  sprites (transparent PNG atlas, no baked text). Use when the user asks for
  Overlay/HUD UI art, 提示词, gpt-image prompt, sprite sheet, 图标, 底板, 状态图标,
  hook-ring, or says 按之前的写法出提示词. Do not generate images unless the user
  explicitly asks to 出图/生成图片.
---

# Overlay HUD 出图提示词

用户要 Overlay / HUD UI 时：**只输出可粘贴给 gpt-image 的提示词**，不要调用生图工具，不要写代码、不改 Prefab。

除非用户明确说「生成图片 / 出图 / 画一张」。

## 固定骨架（必须原样保留）

风格段、透明底约束、禁止文字，用下面原文，不要改写色值或后缀。

**开头**

```text
Fish Social casual fishing game HUD UI kit sprite sheet, flat 2D cartoon, cute cat and small fish motif, soft pastel palette teal water #2A6F8F cream #E8D5A3 coral #E07A5F dark panel #0F1820 outline #1B3A4A, clean vector-like shapes, rounded rectangles, thick soft outlines, minimal shading, no gradients overkill.

Create ONE transparent PNG atlas with clearly separated tiles in a tidy grid, generous padding between tiles, no overlapping, no text labels, no letters, no numbers, no watermark.
```

**结尾**

```text
Fully transparent background everywhere outside the tiles, PNG with alpha channel, isolated UI assets only, no 3D, no photo, no drop shadows onto the background, no readable text of any kind.
```

## 中间段怎么填

按用户点名的零件写 `Row N — …`，英文描述图形，**禁止**在 prompt 里要求画出汉字/字母/数字。

| 用户说法 | 写成 |
|----------|------|
| 按钮底板 | chrome plates only (empty faces, no icons on them) + aspect |
| 图标 | flat UI icons, centered, bold silhouette readable at small size, single-color fill with outline, each on its own tile |
| 半透面板 | semi-transparent dark rounded … empty interior |
| 上钩环 | circular hollow donut ring, even stroke, transparent center, no ticks |

一套相关零件 → **一份 atlas 提示词**。用户只要单件 → 仍用同一骨架，中间只写那一格。

回复里给：

1. 一句中文对照（入库名 / 用途）
2. 一个 ` ```text ` 代码块（整段提示词，可直接复制）

不要解释风格、不要生成图、不要改仓库资源。
