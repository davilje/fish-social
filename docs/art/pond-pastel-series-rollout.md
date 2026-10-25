# 全鱼塘粉彩绘本场景替换

2026-10-25。按用户授权，从已生成的 30 张地图中选择 17 张，覆盖 20 个正式鱼塘及城郊练杆塘，共 21 个关卡。

## 美术与引用

- 采用完整静态底图，2172×724 源图映射到 1680×560 画布，统一放置矩形。原始 RGB/RGBA 像素不重绘、不调色、不加滤镜；仅必要时补充不透明 Alpha 通道。
- 统一粉彩块面绘本画法，按关卡选择晴日、黄昏、暖秋、雨后和月夜。湖泊保持自然开阔，水库有坝墙，河流有步道，近海有沙岸；野塘复用苇滩素材。
- 当前完整底图没有独立可动的云、树叶或水面图层，替换的是静态环境展示。已有角色动画与水面特效代码未变更。
- 清除关卡中旧环境切层和旧风格的装饰实例，避免叠加新底图；复用资源文件保留。
- 所有地图使用同一全幅坐标规则。每张图在 JSON 中记录源图水线及对应关卡岸线，按水线校准整组钓位高度，保持水平位置、角色尺寸和板凳接触关系。

## 项目位置

- Unity 美术：`fish-social-unity/Assets/Desktop/OverlayArt/Ponds/series/pastel-v1/`
- 逐关卡引用：`fish-social-unity/Assets/Desktop/OverlayArt/Ponds/catalog.json` 的 bindings，新增可选 image、layerLayout、shoreY；未覆盖的绑定仍使用共享 profile。
- 场景 Prefab：`fish-social-unity/Assets/Desktop/OverlayLayouts/pond-*.prefab`
- Overlay 源目录及本地运行目录分别为 `desktop-overlay/OverlayResources` 和 `fish-social-unity/FishSocialOverlay/OverlayResources`，布局、底图、旧路径兼容图均同步。
- 选塘界面 21 张预览图及来源清单已从新 Prefab 重新渲染：`Assets/Resources/Desktop/Theme/Cozy/journal/ponds/`、`pond-manifest.json`。

## 关卡选择

| 关卡 | 地图 | 配色 |
| --- | --- | --- |
| 千岛湖 | 弧坝晴湾 | 薄荷晴日 |
| 太湖、兴凯湖 | 雾漫苇岸 | 雾蓝雨后 |
| 洪泽湖 | 晚霞原野 | 珊瑚黄昏 |
| 鄱阳湖、滇池、洱海 | 白桦草甸 | 薄荷晴日 |
| 洞庭湖、查干湖 | 金叶林缘 | 杏金暖秋 |
| 镜泊湖 | 月光银草 | 紫蓝月夜 |
| 万绿湖 | 秋坡堤坝 | 杏金暖秋 |
| 北江 | 绿荫步道 | 薄荷晴日 |
| 丹江口水库 | 闸墙晚照 | 珊瑚黄昏 |
| 清江 | 雨后河园 | 雾蓝雨后 |
| 舟山近海 | 晚霞沙丘 | 珊瑚黄昏 |
| 厦门湾 | 月映芦花 | 紫蓝月夜 |
| 南澳近海 | 海松沙岸 | 薄荷晴日 |
| 长江故道野塘 | 青苇浅岸 | 薄荷晴日 |
| 青岛近海 | 雨后渔舍 | 雾蓝雨后 |
| 涠洲近海 | 金草海湾 | 杏金暖秋 |
| 城郊练杆塘 | 花园小院 | 薄荷晴日 |

## 使用与验证

调整美术绑定后使用 **Fish Social / Overlay / 应用场景资源并导出**；该操作保留角色内部布局，只校准整组高度，导出并同步运行目录，同时刷新选塘照片。手工编辑并保存 Prefab 后，仍可使用 **更新钓鱼场景到运行目录**。

Unity 2021.3.29f1c1 编译、应用、导出及渲染完成。`python3 scripts/desktop/verify-pond-art.py` 检查 21 关、420 钓位、贴图引用与所有猫种的座椅接触点。与本轮开始前比较，4,242 个非环境对象除钓位整组 Y 偏移外，其他导出字段完全相同；3,957 个 Overlay 源文件与运行目录 SHA-256 一致。17 张新图的解码像素与生成预览一致。

实际 Unity 渲染、逐关卡选择及验证文件：`/Users/red/Desktop/fish/output/scene-revision/pastel-project-rollout-v1/index.html`。原始 30 张及生成提示词：`/Users/red/Desktop/fish/output/scene-revision/pastel-30-maps-v1/`。本轮没有再次使用生成工具修改图片。

本机为 macOS，未运行 Windows WPF；重启 Windows Overlay 后加载新场景。本地目录同步不会自动更新其他机器的安装包。
