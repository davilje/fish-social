# 抛竿与收杆鱼线修复 · 2026-10-30

范围为 cast、reel、reel_empty。收杆仍从第 3 张原关键帧（Source=2）起修复；抛竿追加两张道具关键帧，扩为 6 帧。三段仍各 600ms；当前 11 个角色共用修复后的道具资源。

## 抛竿六帧追加

原帧对应新帧 1、2、5、6；新第 3 帧为过顶，第 4 帧为向前甩出。时长依次为 140、70、70、70、70、180ms，共 600ms。两帧分别复用原后摆和前甩的身体四层像素，鱼竿绕各自握点旋转 40° / -40°，浮标移动至原画坐标 (140,60) / (400,90)，鱼线按对应端点重新绘制。它们是独立 PNG 道具关键帧，不是新增猫咪身体原画，也不是只增加重复播放槽。

空中浮标采用单调三次插值，过顶/前甩阶段不施加水面下拉；第 5 到第 6 帧才接入水面补偿。抛线事件位于 Source=2、210ms，入水事件位于 Source=5、420ms。Unity 的六帧加载校验、三处资源树的 11 角色 manifest、inventory、音画标记已同步。原 63 姿势图集作为来源保留，运行时每角色由 74 帧增为 76 帧，两个新增帧通过 propKey 标明来源。

## 原因与处理

- 旧补帧控制点固定为 min(竿尖Y,钩端Y) 上方，形成不随受力变化的上拱；关键帧又回退原 PNG，线形不一致。
- 新曲线连接现有竿尖与钩端，使用端点速度的法向滞后和重力法向分量，限制弯曲量；带鱼收杆更紧，空钩/浮标较松。属于物理启发的确定性美术曲线，不是完整绳索动力学。
- 关键帧和补帧调用同一几何函数，固定钩口与竿尖连接，不重新生成角色。
- 原空军线 PNG 同时包含浮标下面的 35px 子线，补帧替换后丢失；已把子线与鱼钩合并在 bait 层，并与浮标同移，避免分离和子线伸缩。
- 原收杆从第 3 帧起仍把已过头顶的鱼向水面下拉。第 2 到第 3 帧之间渐退水面补偿，第 3 帧及之后为零，恢复原空中鱼轨迹；子线与钩使用相同浮标水面偏移。
- 水面补偿基准使用真实竿尖/端点坐标，不再用曲线 alpha 外接矩形极值充当两个连接点。

## 资源与复跑

`scripts/fishing_line_geometry.py` 是共享几何；`scripts/export-cast-six-keys.py` 幂等扩展抛竿素材和清单，`scripts/export-fishing-swing-lines.py` 将 12 张鱼线、4 张钩/子线素材写入 OverlayResources、Unity 内 OverlayResources 镜像和 Unity StreamingAssets/Fishing。保留既有 PNG GUID/meta，道具尺寸640×512；角色四层保留各自原分辨率。旧线与钩保存在 `docs/美术/钓鱼动作/line-review/original/`，收杆预览左栏保留该对照，抛竿左栏已展示新版六帧。

```sh
python3 scripts/export-cast-six-keys.py
python3 scripts/export-fishing-swing-lines.py
python3 scripts/export-fishing-water-contact.py
python3 scripts/export-fishing-motion.py
python3 scripts/art/export-water-source-markers.py
python3 scripts/test_fishing_swing_lines.py
dotnet run --project desktop-overlay/tests/FishSocialOverlay.MotionTests.csproj
node scripts/verify-fishing-motion-preview.cjs
```

重新导出工作台素材后须依次重跑以上五个 exporter，以免旧生成器覆盖六帧扩展与鱼线修正。此轮未重建 mobile 整帧图集，也未修改历史桌面工作台的源生成器。

## 验证

- 5 项 Python 回归通过：六帧/时长/身体复用/标记同步、收杆前两帧与其他动作不变、端点锁定、曲率/松弛限制、三端资源一致。
- 22/22 .NET 动作核心测试通过，包含新增抛竿空中阶段不被水面拉低检查；19/19 音效标记与策略测试通过。
- Unity EditMode `DesktopFishingAnimationTests` 148/148 通过，包含更新后的六帧清单加载与播放验证；结果保存于 `docs/美术/钓鱼动作/cast-six-key-review/unity-animation-tests.xml`。六帧导出重复运行后 308 个相关文件字节一致。
- 浏览器 11 个角色 × 14 动作 × 4 时点 = 616 次渲染通过；无页面错误，睡醒和打窝等现有检查通过。
- Native WPF 工程跨平台编译成功，0 error；仅现有 netcoreapp3.1 生命周期警告。未运行 Windows Player，未声称用户验收。
- 人工检查三段第 3 帧截图；截图位于 `docs/美术/钓鱼动作/line-review/`。预览读取正式 Overlay 数据；Unity 静态关键帧读取相同新 PNG，Overlay 附加子帧曲线由 motion-samples 提供。
