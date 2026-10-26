# 原图保色的粉彩场景修订

> 历史版本。后续按用户要求从 30 张设计图选用 17 张替换全部鱼塘，当前采用情况见[全场景替换记录](../../../../art/scenes/pond-pastel-series-rollout.md)。

2026-10-25。用户否定逐层重绘导致的颜色与笔触变化，要求完全依据其提供的柔软粉彩绘本截图，并随后授权重新绘制、要求风格细节色彩一致。

## 当前采用

城郊练杆塘 `pond-novice` 使用 `layered/pond-soft-pastel-exact-v1/layout.json`，替代先前逐层重绘版本。原图没有的小屋未带入新版。天空、云、远景、完整树组、草地岸石、水面及反光共七个图层，同一模板统一放置。

- Unity：`fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/pond-soft-pastel-exact-v1/`
- WPF 源资源：`desktop-overlay/OverlayResources/ponds/layered/pond-soft-pastel-exact-v1/`
- 本地运行目录：`fish-social-unity/FishSocialOverlay/OverlayResources/ponds/layered/pond-soft-pastel-exact-v1/`
- 关卡引用已同步修改；其他 20 关保持原状。

## 如何避免再次偏色

从用户 1642×958 截图制作母图，仅在明确的角色、钓具、UI 遮挡区域及底部 UI 修补区域采用内置 image_gen 的重绘结果。图外区域不采用生成结果，直接复制原图 RGB。修补掩码、输入原图、母图和逐像素校验均留档。

母图 **84.5508% 的像素直接保留原图数值**；保护区域 RGB 最大差值为 **0**。剩余区域包含遮挡及其局部接缝，是重建内容，无法证明与原先不可见的背景一致。不能将此结果描述成整张图完全无差异。

七层使用互补的像素归属掩码，重组与母图 RGBA 最大差值为 **0**。图层适合当前静态合成；它们不是完整补背的独立动画素材，不能直接平移云或摆动树冠而宣称不会露底。树干与树冠归入同一层。

## 项目宽画幅适配

原截图接近窗口视野，项目是可横向移动的 1680×560 场景。保留主树组比例，从母图 x=80、y=35 开始取画面，以约 0.6 的等比尺度放置。右侧和底部的原图外区域另行续绘；续绘按材质映射到原图实际 RGB 色板，并在接口处做局部过渡。续绘区不属于原图逐像素保证范围。截图边缘暗角不作为整张新地图边界，运行时沿用 Overlay 原有边缘处理。

所有项目图层使用相同的 2800×934 源画布和 1680×560 放置矩形；Unity 导入为 Bilinear、无压缩、无 mipmap、RGBA。运行时缩放会插值，因此原始分辨率的 RGB 校验不能表述为任意屏幕分辨率下逐像素相同。

## 验证和来源

- Unity 2021.3.29f1c1 导入、应用共享模板、导出、场景渲染及运行资源同步通过。
- 21 关、420 个钓位的场景、资源引用、猫咪与座位接触点检查通过。
- 更新前后该关卡所有非环境对象逐项相同，其余关卡 Prefab 和 JSON SHA-256 不变。
- 3,902 个 Overlay 源资源与运行目录 SHA-256 一致。
- 对照页七层图片加载及原图/母图/修补掩码切换通过。
- Windows WPF 未在本机 macOS 运行；需要重启 Windows Overlay 查看实际显示。

预览、母图、掩码、图层、生成原始结果、校验和 Unity 渲染位于 `/Users/red/Desktop/fish/output/scene-revision/exact-pastel-overlay-v1/`，入口 `index.html`。提示词和采用范围见 [pond-exact-pastel-provenance.json](pond-exact-pastel-provenance.json)。
