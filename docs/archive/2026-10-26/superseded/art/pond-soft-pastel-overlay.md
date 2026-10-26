# 柔软粉彩绘本 Overlay 场景替换

> 历史方案：用户指出逐层重绘偏色后，当前已改用[原图保色修订](pond-exact-pastel-overlay.md)。本文记录此前的 10 层重绘版本。

2026-10-25，按用户选择，以原版 `four-reference-textures-v1/03-mint-violet.png` 为配色和纹理基准，将城郊练杆塘 `pond-novice` 的环境替换为柔软粉彩绘本效果。

## 资源与接入

沿用现有 `layered/pond-village-v3/` 资源路径和 Sprite GUID，以便已保存的 Prefab、布局 JSON 和打包资源直接使用新图。该目录名是兼容路径；当前内容已更新为粉彩版本。

- Unity：`fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/pond-village-v3/`
- Overlay 源资源：`desktop-overlay/OverlayResources/ponds/layered/pond-village-v3/`
- 本地运行目录：`fish-social-unity/FishSocialOverlay/OverlayResources/ponds/layered/pond-village-v3/`

10 张 PNG 分别是天空、3 个云实例、远林、水面底色、水面波纹、草地与岸石、完整树组、猫咪小屋。树干与树冠保留在同一透明图层。草岸、水面、云与建筑可以分别编辑。云实例共用新绘云素材并保留原来的透明度比例。

素材通过内置 image_gen 按原图层轮廓、画布留白和粉彩参考重绘。原始像素尺寸、1680×560 场景坐标、groundY=416、图层顺序和切图位置不变。导出仅做尺寸适配与原有云透明度处理，没有额外调色滤镜。角色、座椅和 UI 继续使用项目原资源，未烘焙进背景。

当前仍为静态分层环境；未新增云、树、水的运行时动画。

## 验证

- Unity 2021.3.29f1c1 批处理导入、导出、资源同步及场景渲染成功。
- 10 张 PNG 尺寸与原资源一致，透明层保留 RGBA。
- 55 个既有布局、Prefab、图层布局和 PNG meta 文件 SHA-256 不变。
- `scripts/desktop/verify-pond-art.py` 通过：21 关、420 钓位，角色与座位接触点、图层坐标、引用和源资源一致。
- Overlay 运行目录 3,894 个文件与源资源逐一 SHA-256 一致。
- 检查了完整场景、单猫视窗、20 个钓位和 Unity 实际钓鱼动作近景。
- macOS 本机未运行 Windows WPF 客户端。重启 Overlay 后进入城郊练杆塘检查实际显示；若运行的是另一个安装目录，需将更新后的 OverlayResources 同步到该执行文件旁。

## 留档

- 提示词和生成来源：[pond-soft-pastel-provenance.json](pond-soft-pastel-provenance.json)
- 本地素材、原资源备份、分层预览和 Unity 日志：`/Users/red/Desktop/fish/output/scene-revision/pastel-overlay-v1/`
- 预览入口：该目录下 `index.html`，支持新旧切换、单猫/20 钓位和透明图层查看。
- 仅替换 `pond-novice` 对应的池塘主题，其余场景主题没有改动。
