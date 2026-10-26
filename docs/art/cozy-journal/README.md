# 原版手账 UI：资源交付与视觉纠偏

2026-10-21。视觉基准是 `docs/design/ui/prototype/cozy-journal/` 的原版可交互页面（含世界地图入口、鱼塘详情与档位分区修正）。先前手工近似拼装的三张 Unity 样板已由原版导出的样板替换，不能用初版样板作为后续设计依据。

## 查看结果

- `production/index.html`：正式 Unity 页面截图与实施验证；已接入游戏业务，截图使用编辑器样例数据。

- `compare.html`：10个页面、11个状态的原版 / Unity 同尺寸拖动对照，另提供4倍增强差异图。
- `index.html`：171张新增或整理后的资源，支持类别筛选、搜索、透明背景检查。
- 原版可交互页面：`../../design/ui/prototype/cozy-journal/index.html`。

在仓库根目录启动 `python3 -m http.server 8769` 后，访问 `http://127.0.0.1:8769/docs/art/cozy-journal/compare.html`。已有服务时直接打开；不需要再启动相同端口。

## 当前交付边界

| 内容 | 已完成 | 验收状态 |
|---|---|---|
| 共用 UI 切图 | 90张PNG、导入meta、源文件和参数表 | 50项已改为原版 CSS/SVG 导出；40项为补充设计或状态，不能自动视为原版验收通过 |
| 角色补充 | 10款皮肤 × 6姿势 = 60张，透明、同画布与注册点 | 新增姿势待视觉验收；原版橘猫仍是样板默认角色 |
| 鱼塘缩略图 | 21个实际场景prefab渲染，1024×640 | 记录真实来源；不把这些图片冒充原版预览的示意照片 |
| 原版布局对照 | 11个独立Unity样板prefab，独立视觉层、原生Text、命中区域 | 在Unity实际渲染；仍有字体栅格化、半透明合成和部分旋转文本差异 |
| 组合组件 | 12个独立组件prefab、纸纹Multiply材质及shader | 可供正式迁移取用；不是已经接入业务的共享Library替代品 |
| 正式游戏页面 | 已迁移正式预制体、共享主题、业务与动效；发布界面为原生预制体 | 自动测试与正式渲染见 production；Windows、Steam联机和最终美术仍待平台验收 |

**不能把“PNG齐全”“导入成功”“样板渲染成功”写成“全部UI已替换”或“像素完全一致”。** `previews/asset-validation.json` 记录资源验证，`previews/comparison-metrics.json` 记录图像差异。大面积相同背景会稀释整屏差异，因此统计数字不作为视觉通过率。

## 权威来源与使用方式

- `fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/ui-manifest.json` 是共享UI切图的权威参数表。每项记录源文件、源像素尺寸、PPU、九宫格border、用途和校正状态。
- `sources/css/` 保存原版计算样式对应的可编辑HTML。`sources/ui/` 保留初版SVG和仍在使用的矢量源；当manifest的source指向HTML时，同名旧SVG已被替代，不能据其重新导出覆盖新图。
- `contentInsets` 按设计单位记录透明外延。比如按钮本体58高、上下各32透明阴影，Image矩形应为122高，本体仍为58高。圆角和阴影不能随目标宽度拉伸；九宫格border是2×源像素。
- 纸纹直接复用原版256×128 PNG，以PPU 50平铺为512×256设计单位，透明度0.32、Multiply混合。`PaperMultiply.mat` / `CozyJournalMultiply.shader`提供该混合；普通alpha混合无法等价替代。
- 按钮hover/pressed复用同一表面，由RectTransform做原型规定的动作。纹理状态文件不内置位移，以免文字、点击区和底图脱节。
- `ReferenceAssets/layouts.json` 保存原版每层坐标、颜色、来源和原型文件SHA256。其PNG是用于还原原版的逐元素参考层，不是优化完成的运行时图集。样板中的文字使用Unity Text，业务值、价格、按钮文字没有烘焙到整页背景。
- 对照样板中场景、角色、道具和鱼图都取原版页面，以避免将“资源换了一张”和“界面还原误差”混在一起。
- 原有137张Cozy PNG继续保留；根目录`asset-manifest.json`从实际Unity AssetDatabase重建，包含真实导入尺寸、GUID、fileID与Sprite rect。不要使用旧README中的border值。

## 全资源组覆盖

逐组明细见 `coverage.json`：A01–A30均有资源或复用来源。其中A21对应60姿势，A29对应21场景图，其余28组由90张UI切图及既有资源覆盖。`referenceAligned`只统计直接按原版校正的条目，不代表整个组的所有组合已经验收。

角色原始图集位于`sources/characters/`。使用内置ImageGen生成，身份参考原项目头像；提示词和源图路径保存在`sources/character-generation.json`、`dafeiyu-generation.json`、`dafeiyu-spacing-generation.json`。角色补充不能覆盖原版确认的默认造型。

## 重建顺序

依赖Node.js、sharp、Playwright/Chrome与项目对应的Unity 2021.3。脚本优先读取本机安装包，当前机器另有Codex运行时路径作为fallback。

1. `node scripts/art/build-cozy-journal-assets.cjs` — 初始补充矢量资源。
2. **紧接着** `node scripts/art/refine-cozy-journal-assets.cjs` — 用原版CSS/SVG校正共享切图；只运行第1步会恢复近似底稿。
3. `node scripts/art/export-cozy-journal-characters.cjs` — 从保留的原始图集导出60姿势。
4. `node scripts/art/export-cozy-journal-reference.cjs` — 从运行在8769的原版导出布局和参考层；不修改原版文件。导出期间停用动效，以免参考截图与切图处于不同帧。
5. Unity菜单`Fish Social/Journal Art/Prepare and validate resources`导入全部资源、场景渲染、组件和样板；已有场景图时可使用`Rebuild reference samples`只重建导入、组件与参考样板。
6. `node scripts/art/compare-cozy-journal-reference.cjs` — 两端统一2×渲染后缩至1672×941，生成差异图。
7. `node scripts/art/build-cozy-journal-preview.cjs` 与 `node scripts/art/build-cozy-journal-comparison.cjs` — 更新素材页和对照页。
8. `node scripts/art/verify-cozy-journal-assets.cjs` — 检查尺寸、透明通道、GUID、PPU、九宫格边界、源文件及对照图。

正式迁移已实施，历史记录见 `../../archive/2026-10-26/superseded/design/ui/2026-10-21-cozy-journal-unity-implementation.md`。现行说明见 `../../design/ui/journal/README.md`。正式页面重建后可执行 `node scripts/art/build-cozy-journal-production-preview.cjs` 更新17个状态截图。上述步骤4–7仍是参考样板的对照链，不替代正式页面和Windows实机验收。
