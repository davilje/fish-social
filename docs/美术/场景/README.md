# 场景维护说明

分层和树叶风动预览在 [motion.html](motion.html)，也收在仓库根目录 [现行预览](../../../现行预览.html) 的「场景」。

预览直接读取 Unity 项目内的场景资源和 `motion.json`，覆盖原始 30 张系列场景及千岛湖，共 31 套。当前方案只使用最早的完整原图 `background.png`，叠加要动的树叶、植被前层。完整原图同时作为固定后层，天空、云、太阳、月亮及其余景物均保留在原图中，不单独拆层和移动。原图逐字节保留，风动层在原画布位置从原图取色；原始分辨率下静止叠加的 RGBA 像素差为 0。

系列场景每套为完整原图和两层风动植被，共 3 层；千岛湖保留完整原图、两层树叶和六块灌木，共 9 层，中央两块灌木的风动幅度仍为 2.2。21 个鱼塘的 Unity 预制体和两处 Overlay 导出资源已同步，座椅、角色、浮漂与水线坐标保持现有值。通过「显示图层」中的勾选按钮自由组合有效图层，提供「全选／全部隐藏」和「归位检查对齐」；切换场景后默认全选。总预览链接加 `#scene` 可直接进入「场景」。

当前图层保存在 `fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/pastel-scenes-v1/` 和 `layered/qiandao-wind-v3/`，同步到两处 Overlay 资源目录。资源依赖以各场景 `layout.json`、`motion.json` 为准。

2026-10-29 按要求从清理备份中恢复全部 31 套场景的 `sky.png`（无云天空）和 `cloud-complete.png`（含天空的完整云景补绘），包括原有 Unity `.meta`，同步到两处 Overlay 资源目录。恢复文件逐字节等同于清理前版本，没有重新绘制。`layout.json` 的 `previewLayers` 单独登记这两张备选资源，预览提供勾选按钮，默认关闭；正式 `layers` 和风动配置保持“完整原图＋风动植被”。要单独查看天空或云景，先点击「全部隐藏」再勾选对应按钮。恢复清单见 `/Users/red/Desktop/fish/output/original-backdrop-leaves-v1/restored-sky-clouds.json`。

2026-10-29 已将本次不再引用的独立天空、云景、前中后景和固定植被层连同 `.meta` 移入 `/Users/red/.Trash/fish-static-scene-layers-20261029/`。逐场景像素检查及资源清理记录在 `/Users/red/Desktop/fish/output/original-backdrop-leaves-v1/`。Overlay 风动加载器支持无独立云层的配置；Windows 应用需要重新打包后使用更新后的加载逻辑。

2026-10-28 已清理无引用的旧拆层版本、天气云库、独立重绘样板和历次拆层实验副本，连同对应 Unity `.meta` 移入 `/Users/red/.Trash/fish-unused-scene-layers-20261028/`。原始 30 张完整图保留在 `/Users/red/Desktop/fish/output/scene-revision/pastel-30-maps-v1/`；千岛湖完整原图保留在 `/Users/red/Desktop/fish/output/scene-revision/approved-scene-originals/qiandao-original.png`；项目内 `series/pastel-v1/` 的最初选图保留。详细清单见 `/Users/red/Desktop/fish/output/scene-resource-cleanup-v1/result.json`。历史实施文档中的旧输出路径属于清理前记录。

| 文档 | 记录状态 |
|---|---|
| [overlay-scene-update-and-editor-menus](overlay-scene-update-and-editor-menus.md) | 专题说明／实施记录 |
| [pond-pastel-series-rollout](pond-pastel-series-rollout.md) | 专题说明／实施记录 |

