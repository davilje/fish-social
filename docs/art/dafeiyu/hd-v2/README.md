# 大肥鱼高清统一尺寸版本

本轮更新大肥鱼全部动作素材及对应客户端加载逻辑。44 张独立高清姿势稿组成 63 个源帧，映射为 16 个运行时动作、74 个播放帧位。进入 `index.html` 可逐帧查看新旧版本、切换猫咪模板、单独查看图层以及播放收获/空军完整流程。

## 分辨率约定

| 资源 | 旧版 | 当前版本 |
| --- | --- | --- |
| 单张绘制原稿 | 拼图中裁出的低分辨率角色 | 单姿势透明原稿 1402×1122 |
| 角色、头部、后手、前手图层 | 640×512 | 1280×1024 |
| 完整合成帧 | 640×512 | 1280×1024 |
| 网页/移动端图集的单帧 | 320×256 | 640×512 |
| 角色头像 | 256×256 | 512×512 |
| 角色放置逻辑画布 | 640×512 | 640×512，保持不变 |

鱼竿、鱼线、鱼、浮漂、饵及默认板凳继续复用已有共享资源。本轮没有重绘这些道具或其他九种猫咪。共享道具仍为 640×512；高清合成预览中按相同逻辑坐标采样，因此道具本身不会凭空增加细节。

## 尺寸跳变的修正

旧版按“手到脚底”的距离逐帧缩放整个人物，手臂抬高或落下会改变全身尺寸。新版本按统一面部尺度标定：可见面部皮肤宽度约 92 个逻辑像素，脚底基线 450，水平方向匹配动作握持区域。**不再用握竿手的高度决定角色缩放。**

面部皮肤宽度是校准代理指标，不等于真实头骨大小；发丝遮挡、表情与转头仍会改变外轮廓。`registration.json` 保留每张图的真实缩放、平移、手部位置和模板距离，不能把这个指标一致当成所有姿势或握持关系已通过人工验收。

拆层区域先在原稿上定位，再经过同一坐标变换导出，避免缩小后颜色连通导致脸部被错识别为手。`calibration.json` 记录选用的修订图及局部手部轮廓。角色图层按像素唯一归属拆分；构建脚本检查没有重复像素或丢失像素，并保留完整鱼竿及原有绘制顺序。

## 生产流程与文件

1. `generated/`：使用内置 `image_gen`，参考既有外观与姿势逐张重绘；不是把旧版模糊切图直接放大。
2. `retouched/`：手势修订候选；实际选用以 `calibration.json` 为准。
3. `prompts/`：原稿和修订提示词；`jobs.json` 记录参考关系。
4. `register.mjs`：对原始高清稿仅作一次最终尺寸重采样，输出统一 1280×1024 透明画布。640×512 临时采样只用于定位，不作为最终图片来源。
5. `build.mjs`：输出 `frames/` 的分层、透明合成和带板凳预览；`pack/` 为运行时素材；`atlases/` 为 16 个横向图集；`preview.png` 为动作总览。
6. `tools/fishing-animation/install_dafeiyu.mjs`：先验证尺寸、帧数、图层顺序及共享道具哈希，再同步 Unity、Overlay、网页/移动端图集、头像和钓鱼照片。安装记录为 `installation.json`。

运行时 manifest 使用 `bodyTextureScale: 2`，图集 catalog 使用 `atlasFrameScale.dafeiyu: 2`。Unity 与 Overlay 按逻辑尺寸放置角色，读取更高分辨率纹理；小尺寸显示时保留至多 2 倍显示像素，避免把所有高清原图长期解码驻留。移动端按图集比例计算帧偏移，显示大小及脚底锚点保持不变。旧猫咪缺省倍率仍为 1。

## 重建与验证

在仓库根目录执行（Node 可执行路径和依赖位置按机器设置）：

```sh
node docs/art/dafeiyu/hd-v2/register.mjs
node docs/art/dafeiyu/hd-v2/build.mjs
node tools/fishing-animation/install_dafeiyu.mjs > docs/art/dafeiyu/hd-v2/installation.json
node tools/fishing-animation/verify_cat_resource_routing.mjs
node --test tools/fishing-animation/dafeiyu_hd.test.mjs
node docs/art/dafeiyu/hd-v2/verify-browser.mjs
```

Sharp 不在标准模块路径时设置 `SHARP_MODULE`；测试还使用 `TYPESCRIPT_MODULE`；浏览器检查使用 `PLAYWRIGHT_MODULE`，可用 `CHROME_PATH` 指定 Chromium。

`browser-verification.json` 记录 63 帧逐层加载、自然像素尺寸和显示矩形检查；`browser-review.png` 为新旧对照截图。浏览器测试覆盖逐帧和两种完整流程的启动，不等同于 Windows Overlay 的实机动画验收。Unity 隔离工程结果与其他实际运行结果见 `verification.md`。

旧版对照在 `docs/archive/2026-10-26/superseded/art/dafeiyu/full-v1/`。当前安装器默认使用本目录；不要重新运行旧版构建或安装流程覆盖高清资源。
