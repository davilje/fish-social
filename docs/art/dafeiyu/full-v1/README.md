# 大肥鱼 · 全动作绘制与分层素材

原“鲸鱼娘”角色统一命名为 **大肥鱼**，资源 ID 为 `dafeiyu`。保留蓝色渐变长发、女仆头饰、蝴蝶结、深蓝裙与白围裙、鲸尾、白袜深色鞋。历史 `whale-maid/template-study-v1` 入口保留，避免旧链接失效；其页面和角色名称同步更新。

## 本次交付

- 44 张不同姿势：保留六张试绘，使用内置 imagegen 补绘 38 张。每张原始稿、生成提示和导入记录均保留。
- 对应原猫咪的 63 个源帧、14 个源动作；复用姿势使用同一张角色稿，避免重复绘制导致边界跳变。
- 按现有客户端清单导出 16 个运行片段、74 个帧槽和 16 张合成条带。
- 角色独立分为 `body / head / far_hand / hands`；道具独立分为 `rod / line / fish / bobber / bait`，板凳继续复用默认 v3 资源。
- [完整播放检查页](index.html)：逐帧、循环、倍速、收获与空军完整流程、发力时长、分层显隐及原猫咪同帧对照。

## 文件组织

| 路径 | 内容 |
| --- | --- |
| `plan.json` | 63 帧到 44 张姿势稿的映射，原动作时长和绘制依据 |
| `templates/` | 两帧一组的原猫咪姿势模板；不是角色最终素材 |
| `prompts/`、`generated/` | 内置 imagegen 的提示、19 张双帧透明原稿和返修稿 |
| `painted/` | 分割、规格化后的 44 张角色稿，640×512 |
| `registered/` | 与模板握点、落脚位置对齐后的角色稿 |
| `registration.json` | 缩放、平移、握点误差，便于检查和后续精修 |
| `layer-regions.json` | 每个姿势自己的头部与前后手分区 |
| `frames/<源帧 ID>/` | 完整角色、四个角色层、五个独立道具层、透明合成图和带凳预览 |
| `pack/dafeiyu/` | 独立运行包：`manifest.json` 与四个角色层 |
| `pack/shared/` | 与猫咪一致的五个道具层 |
| `atlases/` | 16 个运行片段的 320×256/帧透明序列条带 |
| `verification.json`、`browser-verification.json` | 数据、分层和实际浏览器播放检查结果 |

## 制作流程与遮挡

1. 从现有猫咪的 `full-pose-manifest.json` 和 `painted-poses.json` 提取动作、63 帧时长、44 种姿势以及握点。
2. 保留已画好的六个姿势，其他姿势使用原猫咪模板和大肥鱼外观稿共同约束 imagegen 绘制。只生成角色，道具继续取用工程原图。
3. 双帧原稿等比分成两个面板并统一为 640×512；按握点与脚底作整张角色的等比缩放和平移。角色不是由橘猫改色，也不使用骨骼扭曲生成新增姿势。
4. 依据每张角色自己的脸部与手部位置建立姿势分区，逐像素分到四个互斥图层。四层合回必须精确保留该张角色稿的所有可见像素。这里是绘制稿的分区，并没有补画被遮住的完整手臂；适用于这套帧姿势，不代表可任意旋转的骨骼原画。
5. 原鱼竿保持完整，不再按橘猫头型预先挖空。握竿时使用该帧的 `renderLayerOrder`，前后关系为身体、后手、鱼竿、角色头、前手。放下鱼竿、展示鱼等帧沿用各自的原始顺序。
6. `build.mjs` 输出分层帧、对照页数据、独立运行包和合成条带。站立、下蹲及离席对应站姿的预览不显示凳子。

## 动作和调用

播放页覆盖站坐转换、准备、挂饵、抛钩、等待、咬钩后仰、持续发力、收杆出水、空军收杆、展示收获、两种复位、打窝和离席。打窝与离席跟随当前猫咪动作模板中的姿势复用关系，不另造不同的动作轨迹。

`pack/dafeiyu/manifest.json` 保留现有运行清单格式，包含每个片段的时长、逐帧层级、源帧索引、循环及衔接信息。四个角色层读取 `dafeiyu/body/<运行片段>/<帧槽>/<层名>.png`，五个道具层读取 `shared/<运行片段>/<帧槽>/<层名>.png`。`seatVisible` 为逐帧的板凳显示标志。

## Unity 角色选择与持久化

已作为独立角色 `dafeiyu` 注册，选择入口是 **个人信息 → 编辑资料 → 选择角色 / 头像 → 大肥鱼 → 保存**。沿用现有默认角色的可选规则，没有新增付费或等级门槛。

列表来自 `DesktopDefaultAvatars.All`，使用现有 `AvatarChoice` 预制体生成选择项；`PanelProfileEdit` 及其编辑器生成脚本已同步标题。头像从大肥鱼准备姿势的头部层导出，首页、个人中心和 Overlay 共用这张图。保存沿用资料接口的 `avatarUrl` 字段，`/image/profile/cat_avatar_dafeiyu.png` 映射到 `dafeiyu`，因此不需要数据库迁移。文件名保留现有 `cat_avatar_` 前缀以兼容接口协议，并不复用任何猫咪的身份。

Unity 的 `FishingLayerArt.SkinIds`、Overlay 的角色识别以及服务端默认头像白名单均已加入该角色。现有缓存和皮肤刷新逻辑会在保存成功后刷新角色，下一次登录按保存的头像路径恢复。Unity StreamingAssets 与 Overlay 各安装 296 张角色层 PNG 和独立运行清单；鱼竿、鱼线、鱼、浮标、鱼饵和默认板凳继续共用原有资源。安装前逐一校验 370 个道具层文件，避免改变其他角色。

`installation.json` 记录安装范围，`manifest.json` 的 `installed` 为 `true`。手机/网页资源目录也同步头像、照片和 16 个片段条带，避免同一账号跨客户端时识别不到新角色。清理和导出工具从运行资源目录读取当前角色清单，以免后续处理误删新角色。

后端部署必须同时更新 `shared` 和 `server`，先构建 `shared` 再重启服务；只更新 Unity 而继续使用旧服务端白名单会导致保存失败。Windows Overlay 和 Player 需要重新构建后使用新增资源。

[接入验证记录](integration-verification.json)：Unity 2021.3.29f1c1 隔离工程实测通过预制体加载、选择项文字与头像、按钮点击及全部 16 个片段/74 帧槽加载；源码和内置测试编译通过。十个角色的资源路由、已构建的 shared 白名单与服务端启动校验均通过。未进行登录服务器后的真实保存/重新登录和 Windows 包运行。Overlay 旧 UI 快照测试仍有六项既有哈希差异，涉及未由本次修改的旧方法及布局文件，未在本次角色接入中重置这些快照。

## 重建与验证

需要 Node.js、Sharp，以及 Python 的 Pillow、NumPy；浏览器检查需要 Playwright 和 Chrome。使用环境变量 `SHARP_MODULE`、`PLAYWRIGHT_MODULE` 指向现有模块即可，无需改变项目依赖。

```sh
node docs/art/dafeiyu/full-v1/import-drawings.mjs
node docs/art/dafeiyu/full-v1/build.mjs
node tools/fishing-animation/install_dafeiyu.mjs
python3 docs/art/dafeiyu/full-v1/verify.py
node docs/art/dafeiyu/full-v1/verify-browser.mjs
node tools/fishing-animation/verify_cat_resource_routing.mjs
```

导入优先使用 `generated/pair-XX-retouch.png`，保留第一版原稿供追溯。不要重新运行 `prepare-full.mjs` 来覆盖已精修的制作计划。

验证检查全部源帧和动作映射、角色图层互斥与像素还原、315 份道具副本原样复用、74 个运行帧槽的资源与层级、16 张条带尺寸及动作边界复用；浏览器逐帧比对实际叠层与导出合成图，并测试收获和空军完整流程。资源路由检查覆盖全部十个角色的身份解析、服务端白名单、头像、Unity/Overlay 分层文件及客户端条带。Unity 编辑器回归测试包含大肥鱼完整 16 片段的加载，以及同状态换角色时刷新渲染器的行为。自动检查不代替人物绘制连贯性及游戏内最终视觉验收。本次没有运行 Windows Player。
