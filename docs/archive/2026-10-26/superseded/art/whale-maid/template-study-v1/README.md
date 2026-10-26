# 大肥鱼：猫咪动画模板关键帧试绘

> 已归档。这是六姿势试绘，不是现行素材。现行大肥鱼是 [hd-v2](../../../../../../art/dafeiyu/hd-v2/README.md)。同批旧全动作包在 [full-v1](../../dafeiyu/full-v1/index.html)。

角色现统一命名为“大肥鱼”（`dafeiyu`）。

本轮为三组、六个姿势的试绘。不是完整序列帧包，也没有替换运行时的猫咪资源。

| 组 | 模板帧 | 内容 |
| --- | --- | --- |
| 准备 | ready-00、bait-02 | 准备握竿、挂饵 |
| 发力 | bite-03、effort-03 | 咬钩后仰、持续发力 |
| 收获 | catch-00、catch-04 | 收获起势、展示收获 |

## 外观和姿势依据

外观参考为用户提供的蓝发鲸鱼女仆原素材：以左侧睁眼帧为基准，庆祝图仅补充表情参考。原始参考保存在 `shared/identity.png` 和 `shared/celebration.png`。

姿势模板来自 `docs/art/cat-skins/reference/<帧名>/`，帧顺序来自 `docs/art/cat-skins/full-pose-manifest.json`。保留原 640 × 512 坐标系、帧名、道具坐标和逐帧渲染顺序。形象改为人形女仆，不能以猫咪轮廓重合率判断人体绘图是否合格。

鱼竿已改为 `head-layer-order-v1`：共享 PNG 保留完整杆身，不再烘焙原橘猫头部的透明缺口。握竿帧按后手 → 完整鱼竿 → 当前角色头部 → 前手绘制，头部和头发用本角色自己的透明轮廓遮住杆身。展示收获中靠在后方的鱼竿继续使用该帧的后景顺序。

绘图调用内置 imagegen，单帧编辑以原猫咪合成帧为姿势参考；具体提示词保存在 `prompts/`。编辑参考放大为 1280 × 1024，再左右各加 128 像素透明留白。生成稿统一取 `[128,0,1280,1024]`，等比例缩回 640 × 512；不拉伸头部、手臂或裙子来强行匹配。

## 资源结构

- `templates/<帧名>/`：原角色及原分层、合成稿、生成时使用的姿势模板。
- `generated/<帧名>.png`：内置 imagegen 原始输出；`*-registered.png` 是统一画布后的角色，`*-review.png` 是浅色背景检查稿。
- `frames/<帧名>/character.png`：新角色透明整帧。
- `frames/<帧名>/body.png`：身体、裙装、后侧长发、鲸尾。
- `frames/<帧名>/head.png`：头、脸、头饰及上部头发。
- `frames/<帧名>/far_hand.png`、`hands.png`：后手和前手/袖口的静态遮挡分区。
- `frames/<帧名>/rod.png`、`line.png`、`fish.png`、`bobber.png`、`bait.png`：逐字节复制原模板道具，未重新生成。
- `composite.png`：按原帧 `renderLayerOrder` 合成；`whale-with-seat.png`、`cat-with-seat.png` 是带默认板凳的透明对照图。
- `shared/seat-stool-v3.png`：直接复制 `desktop-overlay/OverlayResources/seats/seat-stool-v3.png`。
- `manifest.json`：原道具 SHA-256、图层顺序、文件位置及像素分区检查结果。
- `index.html`：可直接打开的原猫咪/新角色对照页，可关闭任意图层、显示原手部范围和原角色轮廓。

板凳保留为独立场景层，未画进角色。对照页用统一位置 `[118,380,280,170]` 展示，扩展预览画布至 640 × 600，避免板凳底部被截断。这个矩形仅供试绘对照，不修改工程鱼塘布局。

## 分层方式与校验范围

`layer-regions.json` 为每张新绘图单独指定头、后手、前手区域；其余像素归身体。不是复用猫咪剪影去裁掉女仆长发。四张角色层互斥，完整保留新绘图的可见像素；脚本校验不丢失、不重复。道具在原有逐帧层级中参与合成。

这属于序列帧中的静态遮挡分层。头和身体切口、袖口切口没有为独立旋转补画隐藏区域，不能直接当作骨骼绑定素材使用。像素校验通过仅说明切图可还原整幅绘图、原道具未变，不代表握点、人体坐姿、表情连续性已经完成美术验收。

后续精修重点：挂饵手势与鱼饵的接触、双手与竿把的握点、长发和鲸尾轮廓在连续帧中的一致性，以及板凳座面与人形坐姿的关系。先确认这六张的形象和模板匹配方向，再扩展其他动作与中间帧。

## 重建

在仓库根目录，环境可解析 `sharp` 时执行：

```sh
node docs/archive/2026-10-26/superseded/art/whale-maid/template-study-v1/prepare.mjs
node docs/archive/2026-10-26/superseded/art/whale-maid/template-study-v1/build.mjs
```

可通过环境变量 `SHARP_MODULE` 指定 sharp 的安装目录。重建不调用图像生成，也不会安装或修改 Unity / Overlay 运行时资源。
