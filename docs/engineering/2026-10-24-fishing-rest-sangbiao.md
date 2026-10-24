# 2026-10-24 挂机呼吸、瞌睡与丧彪

状态：2026-10-24 已实现，Windows 实机验收待进行。

## 实现链路

1. 服务端 `fishingSleep.ts` 管理照看时间、入睡阈值；会话表保存睡眠、截止时间及照看时间。
2. `fishingStateMachine.ts` 在等待时检查到期，并让睡眠咬钩立即走逃鱼处理。配置由 `FISHING_SLEEP_AFTER_MS` 控制，默认 1800000ms。
3. Unity Socket DTO 与合并器保留字段缺省／显式 null 区别；本人和其他玩家的睡眠状态通过 Native Overlay 状态消息传递。
4. Overlay 点击命中本人角色后区分点击／拖动，发送 `fishing_attention`；Unity 转发到认证 Socket。服务器状态返回后才改变表情。
5. 呼吸采用固定脚底的有界矩阵；瞌睡增加轻微前倾、慢呼吸及 Zzz。头、身体、手和鱼竿整体同步，鱼线顶端跟随竿尖，末端保持浮漂位置。
6. 原等待图保留全部非眼部像素，新增 `head-sleep.png`；可选资源缺失时保留原等待图，不清空角色。资源被活动 fishing clip 缓存引用计数保护。

## 可配置参数与资源

- 服务端：`FISHING_SLEEP_AFTER_MS`，默认 30 分钟，范围 1 分钟–24 小时，0 禁止新入睡。已睡着仍需点击照看。
- 睡眠状态：`fishingAsleep` 布尔值；`fishingSleepAt` 为 UTC 毫秒或 null。原协议缺少字段时不崩溃。
- Native 主资源：`desktop-overlay/OverlayResources/fishing/<角色>/body/fishing/0/head-sleep.png`。
- Unity 配套 Native 镜像：`fish-social-unity/FishSocialOverlay/OverlayResources/fishing/`。
- 新角色资源与绘制溯源：`docs/art/cat-skins/sangbiao/`；闭眼绘制溯源：`docs/art/fishing-rest/`。
- 当前动作预览：`docs/art/fishing-motion/index.html`。模拟按钮只演示交互，不连接服务端，也不缩短正式 30 分钟阈值。

接入检查发现 Unity 配套 Native Overlay 镜像缺失五种猫咪的等待主体，另外四种仍使用旧图。现已同步现有十种角色和共用道具的 2,828 个缺失／差异文件；Unity 正式 StreamingAssets 与 Native 的等待原图逐一相同，闭眼不会混用不同底图。

## 验证记录

只记录实际运行结果，不将跨平台编译记为 Windows 运行验收。

- Overlay RestMotion：6/6，包含 24 小时缩放边界、脚底固定、过渡连续、离开重置、鱼线两端、水面及 IPC null 兼容。
- Native Overlay：macOS 上跨编译通过，仍保留原 `.NET Core 3.1` 生命周期警告。
- Unity 最终 EditMode 203/203：钓鱼动画 148、休息动效 38、睡眠桥接 17；结果 `/tmp/fish-sleep-rest-eleven-final-tests.xml`。
- 服务器最终专项 35/35：睡眠 28、会话 3、计时器 4。包括睡眠打窝在消费前拒绝、点击后可打窝；报告 `/tmp/fish-sleep-review-evidence/targeted-tests.json`。
- 服务器 TSC 与 HEAD 基线比对：原有 34 项、当前 34 项，新增 0；全量旧测试存在阶段数量断言失败，未当成全部通过。对照证据在 `/tmp/fish-sleep-review-evidence/`。
- Overlay 原运动核心 18/18，通过全部 11 角色资源；与 RestMotion 合计 24 组。
- WPF 原生回归项目 29 组编译通过，未在 macOS 执行 Windows WPF。
- 浏览器 11 角色 × 14 动作 × 4 时间点，共 616 次采样；全部 11 角色闭眼、固定脚底、模拟逃鱼和点击唤醒通过。报告 `docs/art/fishing-motion/browser-verification.json`。
- 11 张闭眼图保留原分辨率和 alpha，眼部之外 RGBA 差异为 0；资源镜像 3,654 文件逐一一致。证据 `docs/art/fishing-rest/verification.json` 和 `integration-verification.json`。
- 丧彪 44 独立绘图复用成 63 原姿势、16 钓鱼动作和 74 帧槽；888 张角色层及 1,998 项九层文件校验通过。家园只补站立／坐姿；家园行走、进食、翻滚、蜷睡另需绘制，当前不借用别的猫咪冒充。

美术由内置 imagegen 绘制；提示词及选稿来源分别保存于 `docs/art/cat-skins/sangbiao/prompts/` 与 `docs/art/fishing-rest/provenance.json`。没有用程序重着色或 SVG 生成新角色。

更新须同时包含服务器、Unity 客户端与 Native Overlay。只替换 Overlay 可看到资源／动效代码，但旧服务器不会发布睡眠判定，旧 Unity 不会转发照看指令。

Windows x64 自包含包：`dist/overlay-fishing-rest-sangbiao-2026-10-24.zip`（251,702,422 字节）。SHA-256：`b97445c75ed20d9065ae39928858366983079620d04836242cfbc86399dba57c`。此包仅包含 Native Overlay；不代表服务端部署或 Unity Player 已发布。

## 复跑

```sh
dotnet run --project desktop-overlay/tests/FishSocialOverlay.RestMotionTests.csproj
dotnet run --project desktop-overlay/tests/FishSocialOverlay.MotionTests.csproj
node scripts/verify-fishing-motion-preview.cjs
node tools/fishing-animation/build_sleep_faces.mjs verify
node tools/fishing-animation/verify_sangbiao.mjs
```

Unity EditMode 测试类：`DesktopFishingAnimationTests`、`DesktopFishingRestMotionTests`、`DesktopFishingSleepTests`。Playwright、Chrome、Sharp 不在默认模块路径时按脚本提供的环境变量配置。

Windows 验证重点：30 分钟到期或测试环境配置到 1 分钟、点击／拖动区分、多人睡眠同步、睡眠脱钩无奖励、重连保持、换皮与 DPI 下鱼线及闭眼拼接。最终验收由用户执行。
