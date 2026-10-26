# 猫咪完整姿势换肤 · 九种猫咪素材

> 批次说明（2026-10-26）：下文九种猫咪及其数量是该批次的交付记录；后续新增角色参见[丧彪说明](sangbiao/README.md)及各角色运行时 manifest。导出流程、资源路径和兼容规则仍可参考，当前角色数量以运行时清单为准。

## 本轮扩充

完整动作覆盖橘猫、三花、银渐层、棕虎斑、奶油、英短蓝猫、黑猫、白猫、奶牛猫。橘猫沿用动作基准；三花保留上一轮连续性修订并进行耳斑回归检查。其余七种分别绘制 44 张独立姿势，共 308 张，每种覆盖 63 张源帧、16 个运行时动作、74 个帧槽和 296 张角色图层。打窝、离席、空军收杆和两条复位均包含在内。

打开 `breeds.html` 查看全部品种，支持动作切换、原版对照、逐帧与慢放。`full.html` 继续保留三花修订前后的对照。整张画稿只按画布缩放，不单独拉伸头或手；按照原动画遮挡掩码分配到 body/head/far_hand/hands，鱼竿、鱼、鱼线、浮标和鱼饵保持共享素材及原图层顺序。

新增画稿位于 `breeds/<skin>/painted/`，每张旁边的 JSON 保存来源、哈希及轮廓交并比；`painted-poses.json` 是导出清单，`contact.png` 是 44 张姿势总览。原始准备稿来源见 `breed-key-imports.json`，局部重绘记录见 `breed-special-imports.json`、`breed-final-retouches.json`。`<skin>-character/`、`<skin>-full/` 分别保存角色和完整合成预览。Unity 位于 `Assets/StreamingAssets/Fishing/<skin>/`；Overlay 位于 `OverlayResources/fishing/<skin>/`。

头像从各自的准备姿势 head 图层裁出，保存到 `image/profile/`、Unity Cozy avatars 和 Overlay Cozy avatars。钓鱼纪念照使用对应收获帧。选择相应头像即可解析到同名皮肤。当前只提供九种完整皮肤；历史 `gray/siamese/tuxedo` 入口兼容到默认橘猫，旧图片已清理。自定义头像保留原哈希分配位置，其中已退役的位置显示橘猫，原有橘猫、三花猫、白猫分配不变。详见 [当前资源调用与清理说明](resource-routing.md)。

导出依次运行（Node + sharp；可用 `SHARP_MODULE` 指向 sharp 安装目录）：

```sh
node tools/fishing-animation/review_breed_pack.mjs cow
node tools/fishing-animation/review_breed_pack.mjs cow --accept-geometry
node tools/fishing-animation/build_calico_fishing_pack.mjs --skin=cow --install
node tools/fishing-animation/install_breed_identity.mjs cow
node tools/fishing-animation/build_breed_review.mjs
node tools/fishing-animation/verify_breed_packs.mjs
```

只有完整 44 姿势且轮廓交并比不低于 0.9 的集合才能导出。几何检查不能证明花斑连续性或握点完全准确。逐帧绘制仍存在细微笔触、边界和色调波动，因此 `productionReady` 保持 false，不能把素材齐全当作美术终验通过。

以下为上一轮三花猫修订记录。

## 当前交付范围

此前四张关键姿势使用绘制稿，新增帧却通过 `paintCalico` 的空间色块规则重着色，导致两者画风、花斑和笔触不一致。本次移除此实现，不再允许缺图时用脚本生成毛色补齐。
三花猫使用 44 张独立完整姿势稿，覆盖 63 张源动作帧。本轮保留 `effort-02` 作为统一花斑参考，对另外 43 张进行局部修稿；原四张关键稿仍在 `calico/` 归档，准备、收杆和收获的活动版本已改用新修稿。相同原图复用同一张新稿，导出 16 个动作、74 个帧槽、每端 296 张角色图层。Unity 和 Overlay 已同步修订资源；原版橘猫、共享道具、动作时序不变。
本轮修正展示收获第 2 帧 `catch-01`、收获后复位第 3 帧 `reset_catch-02` 的耳斑方向，统一为画面左橙右黑；统一脸颊大块橙斑与腰侧黑斑的轮廓，并修掉持续发力末帧多余的黑尾环。
细小笔触边缘仍非像素级锁定，不能宣称完全消除所有波动。整体轮廓通过不代表握点、遮挡或毛色连续性全部通过；`productionReady` 保持 false。其他品种当时未更新，现已按上方本轮扩充单独绘制。

关键姿势在原角色四层合成后的轮廓交并比：准备 97.0%、发力 97.5%、收杆 94.3%、收获 92.7%。
收杆与收获还存在边缘和握点偏差，四帧之间的花斑边界也需继续统一。不能把这些数值当作视觉验收通过。
对照页已检查四种视图、四张姿势切换与交替对照；浏览器无脚本错误。检查结果见 `validation.json`。

Unity 与 Native Overlay 已增加三花猫资源路由，原版橘猫和已有鱼素材保持不变。
没有重新引入 CatRigActor，也没有自动推算肩膀、拼装头部、拉伸手臂或修剪鱼竿的实现。

## 查看

打开 `full.html` 查看新整组预览，支持按实际帧时长播放、0.5×/0.25× 慢放、动作切换、前后逐帧、仅猫咪/带道具切换，并可选择修正前或原版橘猫进行对照。
`index.html` 保留原四张关键姿势的对照工具，可查看完整角色、原版叠影、双色轮廓差异、原道具遮挡试配。
“交替对照”比较同一姿势的橘猫与三花猫，不是连续动作播放。
新图仅按整个画布映射到 640 × 512，没有调整单个身体部位。

红色区域代表新稿超出原轮廓，青色区域代表新稿未覆盖原轮廓。
轮廓重合率采用 alpha ≥ 128 的交并比，只检查整体几何，不代表表情、毛色连续性或握点合格。
道具试配沿用原版四个角色层的 alpha 蒙版，旨在暴露手与鱼竿的偏差；这些蒙版不自动成为新品种的生产分层。

## 素材结构

- `calico-painted/`：44 张绘制原稿，保留 imagegen 原始输出。
- `calico-continuity/`：本轮 43 张局部修稿，保留 imagegen 输出；当前活动稿由 `painted-poses.json` 指向此目录，统一参考稿 `effort-02` 沿用原文件。
- `continuity-imports.json`：局部修稿及耳斑、尾环专项修正的来源与提示词。
- `continuity-geometry.json`：新修稿对上一版轮廓检查，最低交并比 97.60%，仅用于检查姿势漂移。
- `continuity-browser-qa.json`：本轮预览加载、切换与播放检查。
- `painted-imports.json`：新增绘制的提示词及原始生成文件记录。
- `painted-poses.json`：每个原姿势哈希对应的唯一绘制稿、校验值和检查状态。`geometry-reviewed` 仅表示已核对姿势，不代表最终美术验收。
- `calico-character/`、`calico-full/`：63 张同画布角色图及带道具合成图。
- `painted-export/`：与两端资源一致的导出副本。
- `painted-validation.json`：当前导出检查；旧 `validation.json` 仅记录原四张关键稿研究，不代表新版整组验收。
- `calico/`：由内置 imagegen 绘制的完整姿势 PNG，保持生成文件原样与透明通道。
- `reference/`：对应原版猫咪底图、逐帧角色蒙版与独立道具，全部复制原文件。底图手掌处有透明预留，工作台按原层级叠加四个原角色层后才作为完整轮廓比较。
- `prompts.json`：实际使用的绘制提示词及生成记录。
- `manifest.json`：当前草稿、原图与新图校验值、全部源帧的运行时映射。
- `data.js`：相同清单的浏览器版本，支持直接通过 file URL 打开。
- `embedded.js`：原图与新图的无损内嵌副本，使本地文件页面可以读取 alpha 进行轮廓分析，无需关闭浏览器安全限制。

## 后续生产规则

1. 按原帧描绘每个品种的完整姿势。头、身体、手臂是一幅连贯绘画；花斑位置随同一块身体表面变化，不在每帧随意重排。
2. 校准原始画布、落脚点、前后爪握点和外轮廓。仅看缩略图相似不足以通过；尤其检查过顶收杆和展示收获的近手轮廓。
3. 在绘画通过后，从同一张完整姿势图提取必要的前后爪遮挡层。若原蒙版不吻合，则为该姿势重画蒙版，不能靠移动单个手或削掉鱼竿掩盖问题。
4. 原版鱼竿、线、浮标、饵、鱼继续独立，沿用原动作时序。核对竿柄始终位于两手正确的前后关系中、鱼不会遮住握持手掌。
5. 补齐全部 63 张源帧，按清单复用为 16 个运行时动作、74 个帧槽；循环、起止衔接使用明确的同一张帧，不凭近似姿势替代。
6. 完整序列检查花斑跳变、体积变化、脚底滑动、握点偏移和遮挡后，再导出 Unity 与 Overlay 的同一套资源。

工作台明确标注尚存的连续性问题。`productionReady` 保持 false，不将文件完整或轮廓重合率当作视觉验收通过。

## 重建清单

在仓库根目录运行：

```sh
node tools/fishing-animation/build_calico_fishing_pack.mjs
node tools/fishing-animation/build_calico_fishing_pack.mjs --install
node tools/fishing-animation/verify_calico_continuity.mjs
```

需要 Node.js 与 sharp；可用 `SHARP_MODULE` 指定已有 sharp 模块路径。默认只更新本地预览和导出目录，`--install` 同步 Unity 与 Overlay。

工具按完整画布将绘制稿映射为 640 × 512，不逐部位缩放。原四层蒙版仅用于判断像素归属；新稿轮廓外扩部分参考附近蒙版，保留完整新图的每个像素，分层重新叠加必须逐像素还原新稿。此自动归属仍需要逐帧检查握杆遮挡，不能代替精细手掌蒙版。鱼竿、鱼线、鱼、浮标和饵沿用原文件。

导出前校验全部绘制稿、哈希和源帧覆盖。安装要求每张稿完成姿势检查且轮廓交并比不低于 0.9；缺稿或来源不明直接失败，不回退到程序色块。轮廓阈值只防止明显错位，不能保证花斑连续。

回归检查覆盖两张错误帧的左右耳斑颜色、新旧姿势轮廓、两端与导出副本一致性、原动作时序。该检查不把图像哈希或轮廓重合率误当作连续性指标；本轮没有启动 Unity Player 或 Windows Native Overlay。
