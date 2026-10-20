# 钓鱼场景美术 V8

20 个世界地图鱼塘与独立新手塘已接入。构图为后景陆地、连续低岸钓位、前景唯一水面。

## 资源与引用

- `Assets/Desktop/OverlayArt/Ponds/catalog.json`：21 个鱼塘的背景、座椅与装饰变体配置。
- `Ponds/maps/`：湖泊、池塘、水库、河流、近海、河口、野钓共 7 张透明背景。
- `Ponds/modules/`：12 件独立透明装饰素材。
- `Ponds/seats/`：树桩、小板凳、青灰石台、折叠椅四种座椅。
- `Ponds/Prefabs/`：可直接复用的装饰与座椅模块。

当前 20 个世界地图鱼塘及新手塘的默认座位均为木质小板凳 `seat-stool-v3`，包括全部 420 个钓位。默认回退贴图也使用同一板凳。树桩、青灰石台和折叠椅的 PNG、Sprite 导入设置、独立 Prefab 及 catalog 尺寸/承坐点配置完整保留，作为备用资源；桌面导出目录同时保留相应 PNG。

鱼塘 Prefab 直接引用导入的 Sprite，使用约 3:1 的 1680×560 逻辑画布。原图约 2172×724，保持平滑采样、透明、完整矩形网格；导出尺寸以场景矩形为准，不取纹理原始像素尺寸。

湖泊使用开阔自然景观，池塘使用院落与凉亭，河流使用绿化步行街，水库使用坝墙，长江故道野塘使用树林与铁丝网。近海和河口分别使用沙滩灯塔、芦苇红树林。

小板凳采用 V3：更紧凑的单猫比例与水平坐面；V2 青灰石台替代原有砂黄色；新增草绿色帆布、棕色交叉支架折叠椅。树桩保留当前版本。

备用折叠椅采用 V2：逻辑宽度由 86 调整为 104，靠背加高，前后四条支腿与脚点分别拉开；承坐点位于贴图 `(0.60, 0.58)`，落地点为 `0.96`。独立座椅模块也使用 catalog 的逻辑宽度，近景按猫咪和椅子的完整范围取景，避免裁掉椅脚。

四种座椅按贴图原始宽高比显示。catalog 的 `width` 为逻辑宽度，`supportX/supportY` 为 PNG 左上角起算的归一化承坐点，`groundY` 为实际落地点。承坐点使用 `FishingLayerArt.SeatContact`，与当前动画 manifest 的 `place.referenceFoot` 同源；128×128 猫咪槽中的接触点为 `(34.625, 101)`，包含贴图透明留白，不能按旧猫咪插画的身体比例估算。落地点距对应岸线 8 个逻辑像素。不要仅替换贴图而沿用旧板凳坐标。`defaultSeat` 指定导出的默认回退座椅。

共享 `OverlayPondActor.prefab` 和所有鱼塘实例均保存实际接触位置及白色 Image 颜色。美术近景直接渲染保存的猫咪 Sprite，不替换为旧 `orange/fishing-0.png`；额外的 `*-runtime-ready.png`、`*-runtime-fishing.png` 使用真实 `SpriteFramePetRenderer`，分别检查准备和钓鱼姿势。验证覆盖全部钓位与九种猫咪动画定位数据。

## 调整和导出

1. 打开对应 `<pondId>.prefab`，钓位继续绑定原有 GameData spotId。
2. Fish Social → Export Overlay Layout（当前或全部）：导出到仓库 `desktop-overlay/OverlayResources/layouts/`。共享背景与装饰按相对路径同步。
3. Fish Social → Pond Art → Apply V8 And Export：根据 catalog 重新应用全套背景、装饰、座椅及岸线位置，会覆盖手动摆放。
4. Fish Social → Pond Art → Validate V8 References：检查保存的引用；命令行 `python3 scripts/desktop/verify-pond-art.py` 检查导出配置、资源一致性与全部钓位。

Overlay 不直接加载这些 Unity 布局 Prefab，实际使用导出的 JSON 和 PNG；修改后需重新复制或打包 OverlayResources。

不要把 HUD、按钮、聊天条放入场景 Prefab。修改共享 OverlayPondActor 仍会影响所有钓位。Prefab 编辑页存在未保存修改时会停止应用；干净的编辑页会暂时退出并在完成后重新打开。Unity 实际渲染预览位于 `Library/PondArtPreviews/`，含全部场景和四种猫咪座椅近景。
