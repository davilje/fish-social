# 世界地图 · 疏林与场景植被 v1

2026-10-31，已替换 Unity 正式纹理与浏览器地图。[统一预览 · 世界地图](../../../../现行预览.html#world-map)，提供交互地图、Unity 完整界面、当前原画、植被调整前与最初配色共 5 种查看方式。

减少密集、重复的树冠，留出草地、道路与村庄。地区差异由白桦的细白树干、垂柳的下垂柳冠、海松的水平伞冠、局部金叶落叶林及河口苇草表现。未采用整幅渐变染色。品种采用当前场景可辨认的植物外观，非植物学鉴定，也不根据旧 theme/pondId 名称推断。例如 pond-bamboo 的实际图是白桦，pond-pine 的实际图为阔叶河园。对照依据见 [20 个场景植被对应表](sparse-biomes-vegetation-mapping.md)。地图是植物群落的缩略表达，不是逐棵树实例绑定。

当前完整原画 [world-map-sparse-biomes-v1.png](world-map-sparse-biomes-v1.png)。使用内置 image_gen 编辑：先减少密林、调整群落外形，再将水域校回此前的浅蓝青色。两步提示词分别为 [植被编辑](sparse-biomes-generation-prompt.txt) 与 [水色校正](sparse-biomes-water-correction-prompt.txt)。最终生成文件 exec-cbdac241-8acd-4f11-ba1a-78a7518770d9.png。宽高 1672×941。此前调整水色后的图保留为 world-map-hue-saturation-v2.png，用于植被变化对照。

同一 PNG 已复制到 fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/world-map.png 和浏览器原型 assets/world-map.png。Unity 世界地图导入、完整界面渲染与 20 个目的地检查通过；资源 GUID、PanelWorldMap Prefab 和目的地坐标文件校验保持原样。总预览查看、旧链接迁移及小窗口检查见 output/world-map-sparse-biomes-v1/。本轮未重新打包客户端。

以下为此前水色修订记录。

# 世界地图 · 色相与饱和度调整 v2

已应用至 Unity 与浏览器原型。[统一预览 · 世界地图](../../../../现行预览.html#world-map)，可切换地图交互、Unity 画面、当前原画和调整前原画。

纠正上一版压暗水域的问题：从 world-map-before.png 原图重新编辑蓝青色相与饱和度。水域取样平均 HSV 饱和度由 0.691 降至 0.419（约降低 39%）；同区域平均 CIELAB L* 从 67.31 至 69.34，明度接近原图、略有提高。样本依据原图蓝青色阈值选择，非全图统计，也不是逐像素色值锁定。

使用内置 image_gen，提示词 [map-color-prompt-hue-saturation-v2.txt](map-color-prompt-hue-saturation-v2.txt)。当前完整原画 world-map-hue-saturation-v2.png，源文件 exec-e15e8771-4b8e-4b1a-b9f4-26f649286be6.png。原构图、地图目的地数据和坐标沿用，正式纹理 fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/scenes/world-map.png 保留 GUID，浏览器原型 assets/world-map.png 使用同一 PNG。

验证文件在 output/login-map-hue-saturation-v2/。此前相关 23 项 EditMode 测试通过；此次仅替换图像并重新执行 Unity 渲染和地图校验、浏览器预览检查。未重新打包客户端。上一版 world-map-calm.png 保留历史对照，不再正式使用。
