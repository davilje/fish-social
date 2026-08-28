# Fish Social — 美术方向（初版）

供 ComfyUI / gpt-image-2 出图时的风格锚点。脚本会把 preset 的 `stylePrefix` + 用户 prompt + `styleSuffix` 拼成最终提示词。

## 总体调性

- 轻松、治愈的休闲社交钓鱼游戏
- 2D 游戏插画，线条干净，色调偏柔和粉彩
- 避免写实照片感、避免赛博朋克霓虹、避免过多文字水印

## Preset 对应

| Preset | 用途 | 建议尺寸 |
|--------|------|----------|
| `desktop-cat` | Steam 桌面宠物猫概念 / 姿态参考 | 1024×1024 |
| `pond-scene` | Overlay 鱼塘场景概念（参考 960×480） | 1536×1024 |
| `ui-icon` | 状态图标、小标识 | 1024×1024 |
| `social-card` | 社交卡片装饰背景 | 1024×1024 |

## 命名与交付

- 脚本输出：`docs/art/generated/`（staging，大图默认不入库）
- 过审资源：`fish-social-unity/Assets/` 或 `mobile/assets/`
- 尺寸、锚点、导入说明随资源清单一并交付（见原画 Agent 规范）
