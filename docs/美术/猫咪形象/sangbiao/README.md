# 狸花猫「丧彪」· sangbiao

本目录只保存丧彪的新绘稿、生产记录和检查页面。生成使用内置 image_gen，每个独立姿势单独绘制；没有用棕虎斑改名、程序色块或 SVG 冒充新角色。角色设计为暖灰棕中华狸花、深棕黑额纹和虎斑、环尾、奶油嘴胸、黄绿眼、略凶萌，无服饰。

## 交付状态

主任务随后确认已安装包通过：11 品种 motion 18/18、浏览器 616 次渲染、11 品种睡眼交互，并开始发布 native。这是主任务报告的验收结果，本美术任务没有重复执行或冒称启动 Player。原生 fishing 资源从此冻结；任何后续变更须先通知主任务，尤其不得改动已锁定等待头和睡眼素材。

44/44 独立绘制稿完成，63 源帧、16 动作/74 运行帧槽已安装到三端并注册；可以进行 11 品种联测，不再计划额外生成。三端共 888 张角色层哈希核对通过，含共享道具的九层检查共 1998 项通过；最低轮廓 IoU 91.96%，相对原姿势脚底最大偏移 5px。逻辑脚锚仍为 (264,450)，不能将脚底偏移统计理解成全部脚像素完全一致。

`full-contact.png` 已检查全部 63 源姿势的带杆合成；另外按原尺寸检查等待、抛竿、用力、空收、抱鱼、复位关键帧，未见明显脱手或大幅头身比例跳变。浏览器实际逐帧渲染 74 帧槽、9 图层开关和四分之一速播放通过，无加载错误。C# 实际角色路由与 FishingLayerArt manifest 解析通过，非法画布测试被拒绝。编译有两条预期 CS0436 警告（修改后的源类型覆盖既有程序集同名类型）。平台内 Unity/Windows Overlay 尚未运行，因此 productionReady 仍为 false；详见 `review.json`、`validation.json`、`integration-validation.json`、`browser-validation.json`。

等待 head SHA-256 保持 `42333db23b54b7218e00101d820a331809524a390eacc6b8b1ddb8bb9c6fe211`；其他任务的两份 head-sleep 保持 `0f45d475551922dc6e3ba53aa124e9844f9b17d54fc8ed4f2dd1019f13bd4db9`，未改蒙版/几何。早期 `shared-mirror-preflight.json` 的 106 项差异是历史检查记录，最终 370 个共享图层三端已一致；本任务未改共享道具。绘制间仍有轻微纹理、条纹和轮廓变化，视觉检查不承诺逐像素条纹连续。

## 当前生产规则

完整交付需要 44 张独立绘制稿，按源图 SHA-256 精确复用为 63 源帧、16 运行时动作、74 帧槽。角色四层为 body/head/far_hand/hands，五层道具继续使用各端已有 shared 资源。全部导出贴图 640×512，bodyTextureScale=1，脚锚 (264,450)，时序、每帧层序、动作跳转来自现有橘猫 manifest。

`head` 是历史像素归属名，不是独立解剖头部；某些帧承载整只猫。分层复用原四层 alpha 蒙版，沿用 build_calico_fishing_pack.mjs 的最大 alpha 和周围 6px 最近归属算法，逐像素检查不重复、不丢失。不能按层名旋转脖子或头。

全量生成及安装状态以 `validation.json` 为准。生成器缺任意绘制稿、来源哈希不符、轮廓 IoU 小于 0.9、脚底纵向偏移超过 6px，或者三端共享道具内容不同，均停止；不会回退到其他品种或程序重着色。几何合格不等于握点、条纹连续性和最终美术验收，productionReady 保持 false，直到完成平台内视觉验收。

## 来源和文件

- 原工作台：`/Users/red/Desktop/fish/output/animation-studio/animation.json` 及其 assets/clips；该工作台根目录没有 tools 或 scripts。
- 目标仓库可编辑原生资产：`docs/美术/猫咪形象/reference/<pose>/*.png` 全画布 RGBA 分层、`full-pose-manifest.json` 与运行时 JSON。已有猫品种目录未发现 PSD/KRA/ORA，保留 PNG 分层工作流。
- 复用规范：`tools/fishing-animation/build_calico_fishing_pack.mjs`、`register_breed_pose.mjs`、`install_breed_identity.mjs`、`cat_atlas.mjs`。原脚本不被本任务修改。
- `source-index.json`：44 个唯一姿势、源图哈希和生产路径。
- `prompts/<pose>.txt`：实际生成提示词。最初多参考图尝试出现姿势漂移，后续改为只输入对应原姿势。
- `originals/<pose>.png`：内置 image_gen 原始输出，保留真实 alpha。
- `painted/<pose>.png`：仅整画布缩放到 640×512，未单独移动、缩放头、手或脚，未抠假透明底。
- `records/<pose>.json`：生成文件原路径、原始与规范化 SHA-256、完整提示词、尺寸、透明像素数、轮廓 IoU 和脚底差值。
- `rejected/`：被替换的失败稿及当时的提示词/指标，不参与导出。
- 完整导出后：`frames/<source-id>/` 保存 63 源帧角色四层、角色整图和带道具合成图；`pack/sangbiao/` 是运行时分层包；`atlases/` 是 16 个客户端图集；`avatar.png` 是同准备姿势层裁出的头像。
- `index.html`：离线逐帧/慢放检查页，按实际九层顺序合成，可关闭任意层、切换浅/深/品红背景和脚锚线，同时显示原橘猫对照。

## 生产命令

在 `/Users/davilje/fish-social` 下运行。Node 和 Sharp 路径可替换为本机安装。

```sh
export SHARP_MODULE=/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp
NODE_BIN=/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node
"$NODE_BIN" tools/fishing-animation/prepare_sangbiao.mjs prepare
"$NODE_BIN" tools/fishing-animation/prepare_sangbiao.mjs import <pose-id> <image_gen-output.png>
"$NODE_BIN" tools/fishing-animation/build_sangbiao.mjs
"$NODE_BIN" tools/fishing-animation/build_sangbiao.mjs --install
"$NODE_BIN" tools/fishing-animation/build_sangbiao_home.mjs --install
"$NODE_BIN" tools/fishing-animation/verify_sangbiao.mjs
```

`prepare` 只准备索引和提示词，不调用其他图片 API。绘制必须通过内置 image_gen，先 view_image 对应参考。`import` 保留原始图及透明通道。`build` 仅操作本目录；`--install` 才同步以下 sangbiao 资源和必要索引。

唯一的提前交接命令是 `build_sangbiao.mjs --waiting-head`：只导出并锁定三端 fishing/0/head.png，供并行睡眼任务基于最终注册像素绘制，不创建 manifest 或注册角色。`waiting-head-registration.json` 记录 SHA-256；完整导出必须保持同一 head 哈希，避免破坏已绘睡眼贴图。完整安装复制既有文件，不删除其他任务添加的 head-sleep.png。

## 安装目标

- `desktop-overlay/OverlayResources/fishing/sangbiao/`
- `fish-social-unity/FishSocialOverlay/OverlayResources/fishing/sangbiao/`
- `fish-social-unity/Assets/StreamingAssets/Fishing/sangbiao/` 及 Unity meta
- `fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars/sangbiao.png` 及 meta
- 两个 OverlayResources 下 `hud/cozy/characters/avatars/sangbiao.png`
- `image/profile/cat_avatar_sangbiao.png`
- `image/fishing_photos/cat_fishing_sangbiao.png`
- `image/cats/sangbiao/`、`mobile/assets/cats/sangbiao/`：每帧 320×256 的 16 个横向图集，复用现有客户端规范
- 相应 fishing/inventory.json、Cozy asset-manifest.json、cats/catalog.json 增加 sangbiao，保留其他品种条目
- 三个 Cozy 根目录的 `journal/characters/sangbiao/{rest,sleep}.png`：家园静态映射，Unity 附 meta

新增生产/验证脚本：`tools/fishing-animation/prepare_sangbiao.mjs`、`build_sangbiao.mjs`、`build_sangbiao_home.mjs`、`verify_sangbiao.mjs`、`verify_sangbiao_preview.mjs`、`SangbiaoIntegrationCheck.cs`。预览测试使用 Playwright 与本机 Chrome；可通过 PLAYWRIGHT_MODULE 和 CHROME_BIN 指定路径。所有路径相对于 `/Users/davilje/fish-social`。

角色入口只改 shared/defaultAvatars.ts、Unity Pet/FishingLayerArt.cs 和 UI/DesktopDefaultAvatars.cs。Overlay 的 NormalizePetId 由主任务接入。本任务不改 OverlayPetActor、IpcProtocol、PondScenePresenter、其他桥接代码、server、docs/策划，也不提交 Git commit。

## 家园覆盖边界

`build_sangbiao_home.mjs` 复用真实新绘 sit-03 站姿、sit-00 坐姿，统一 0.9 倍导出至 384×328，底线 295；不重新着色、不改动原钓鱼图片。安装到三个 Cozy 根目录的 `journal/characters/sangbiao/rest.png` 和 `sleep.png`，Unity 附 Sprite meta，并登记现有 asset-manifest。这里 `sleep` 是 CozyHomeRoom 用于坐下的历史资源键，内容明确是坐姿，不能作为已完成蜷睡素材宣传。`home-coverage.json` 记录来源哈希、导出规则和实际缺项。

站立、静坐和普通位置移动会保持丧彪，不再因映射缺失回退橘猫。未安装 `home-room/poses/sangbiao-sleep.png`：进入床位时仍显示同品种静态姿势，尚无蜷睡。没有伪造动画 manifest 条目；CozyHomeAnimation.Find 缺项返回 null，CanWalk/CanFeed 为 false，缺少行走/喂食/抚摸翻滚动画。后续需要独立绘制：蜷睡、左右走起/走路/走停、左右进食（嘴部接触点配准）、翻滚序列。完整六姿势 journal 也尚未覆盖，仅提供家园当前消费的两项映射。钓鱼闭眼 head-sleep 不等于家园蜷睡。
