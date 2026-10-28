# 场景维护说明

分层和树叶风动预览在 [motion.html](motion.html)，也收在仓库根目录 [现行预览](../../../现行预览.html) 的「场景」。

预览直接读取 Unity 项目内的场景资源和 `motion.json`，覆盖原始 30 张系列场景及千岛湖，共 31 套。当前使用独立无云天空、完整静态云景补绘底层、后景、中景、前景，以及固定后层植被与风动前层植被。云景、太阳和月亮保持静止。千岛湖中央两块灌木的风动幅度为 2.2，已恢复与灌木原位匹配的坡地和栏杆。可单独查看每层，也可点击「归位检查对齐」。总预览链接加 `#scene` 可直接进入「场景」。

当前图层保存在 `fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/pastel-scenes-v1/` 和 `layered/qiandao-wind-v3/`，同步到两处 Overlay 资源目录。资源依赖以各场景 `layout.json`、`motion.json` 为准。

2026-10-28 已清理无引用的旧拆层版本、天气云库、独立重绘样板和历次拆层实验副本，连同对应 Unity `.meta` 移入 `/Users/red/.Trash/fish-unused-scene-layers-20261028/`。原始 30 张完整图保留在 `/Users/red/Desktop/fish/output/scene-revision/pastel-30-maps-v1/`；千岛湖完整原图保留在 `/Users/red/Desktop/fish/output/scene-revision/approved-scene-originals/qiandao-original.png`；项目内 `series/pastel-v1/` 的最初选图保留。详细清单见 `/Users/red/Desktop/fish/output/scene-resource-cleanup-v1/result.json`。历史实施文档中的旧输出路径属于清理前记录。

| 文档 | 记录状态 |
|---|---|
| [overlay-scene-update-and-editor-menus](overlay-scene-update-and-editor-menus.md) | 专题说明／实施记录 |
| [pond-pastel-series-rollout](pond-pastel-series-rollout.md) | 专题说明／实施记录 |

