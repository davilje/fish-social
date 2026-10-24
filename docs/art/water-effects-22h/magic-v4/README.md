# 简约水魔法 v4

2026-10-24 根据用户“这个风格过头了，贴合游戏本身的细节量”反馈，以上一版图集为编辑目标、游戏原版猫咪同屏图为风格参考，使用内置 image_gen 重新绘制透明素材。

- [猫咪同屏预览](../index.html?style=magic)：当前水魔法选项已改用 v4，支持原版对照、四档、A/B、明暗水面、暂停／逐帧。
- [透明原图](water-soft-atlas.png)：4行 S/M/L/XL，4列水花A、水花B、水纹A、水纹B；16个形态，独立UV取样。
- [完整最终提示词](prompt.txt)；使用内置工具及透明背景，未使用 CLI fallback。
- [深水截图](preview-dark.png)、[浅水截图](preview-light.png)、[验证记录](verification.json)。

收敛内容：低饱和湖蓝、简单圆润外形、两三块颜色与少量高光，减少卷曲内纹和水滴。预览额外水滴最多2颗，去掉 v3 额外光晕和提亮滤镜。水魔法感只留在简洁卷曲水弧上，细节量以猫咪为准。

## 风格确认与正式资源

2026-10-24 用户确认“该风格可以”。此确认针对简约v4风格；不等同 Windows 性能、点击穿透或 H/I 音画实测验收。

正式资源现已从确认的图集中整理：8水花、8水纹、2水滴，18张独立透明PNG。切图移除邻格碎片，保留抗锯齿边缘，按四档规定尺寸统一锚点与8px透明留边；每档额外水滴最多2颗，原始图集与提示词完整保留。

- Overlay：`desktop-overlay/OverlayResources/effects/fishing/v4/`。
- Unity：`fish-social-unity/Assets/StreamingAssets/Effects/Fishing/v4/`。
- 随包Overlay：`fish-social-unity/FishSocialOverlay/OverlayResources/effects/fishing/v4/`。
- 构建：`node scripts/art/build-water-effects-22h-v4.cjs`，确定性切图／缩放／透明留边，无重新生成绘画内容。
- 正式解码合计557,056 bytes（0.53125MiB），高分辨率生成图集不会被运行时加载。
- [正式浅水图](production-light.png)、[正式深水图](production-dark-xl.png)、[简化图](production-reduced.png)、[正式资源／213预览组合验证](../verification-v4.json)、[交付记录](../../../engineering/2026-10-24-water-effects-22h-v4.md)。

v3保留作为过程记录，其细节密度已被用户否定；v2资源保留以便原版对照。该AI生成候选不沿用v2原创SVG的CC0声明。manifest记录原始图集、提示词、构建脚本、源区域及PNG的hash，并区分风格认可与实机验收。

## 追加动效规则

咬钩／水下挣扎只出波纹；波纹完整位于水线下，独立裁剪不影响上方水滴。抛竿约420ms、撒饵约620ms按正式22B与D插值源帧的真实接触点触发，发射后固定锚点。收杆source0真实鱼线／浮漂交点出小水花；鱼身约67ms跨线再按可见尺寸出水花，不能用结果事件代替实际触水。
