# Cozy UI 持久化切图

所有 PNG 已提供 TextureImporter 元数据。按钮与面板使用 Sliced，Sprite Border 64；人物、图标和场景为 Simple。
三态按钮保持同一源图裁切尺寸。CozyButtonVisual 字段与 Prefab 内的 Image/按钮状态均指向真实导入 Sprite，不使用临时 Sprite.Create。
icons 为单色雕刻图案，icons-light 为主操作按钮上的浅色同轮廓版本；不改变图案几何。
home-sakura 为依据确认的首页参考图生成的无 UI 场景，meow-fish 为独立透明 Logo。猫咪头像裁切自现有 v18 分层 head，其他角色/图标/鱼与纸纹复用已确认切图。
asset-manifest.json 记录 GUID、大小、border 与 Resources 路径。data/player-levels.json 只读复制已有游戏数据，供 HUD 显示经验上限。
完整说明与未执行的 Unity 验收：docs/design/ui/2026-10-14-cozy-prefab-repair.md。
