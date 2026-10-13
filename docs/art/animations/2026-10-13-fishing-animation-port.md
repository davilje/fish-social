# 钓鱼动画 v18：Unity 与 Windows Overlay 接入

更新日期：2026-10-13。基于已确认的 animation-studio v18，不重新绘制、不恢复已移除的帽子和围巾，不修改服务端钓鱼概率、收益、时间或埋点。

## 1. 本次同步的内容

- Unity 与 WPF 使用同一份 640×512 RGBA 分层 PNG 和 v2 manifest。
- 全部 51 张源动作帧，包含无竿站立、坐下掏竿、挂饵、抛钩、等待、咬钩后仰、循环发力、收杆、空军、展示鱼获和两种复位。
- 抛竿与收杆的大幅度运动、鱼越过头顶的轨迹、鱼竿在两手之间的遮挡，以及竿与头部的逐像素遮挡都保留源图结果。
- 坐下最后帧与准备帧一致；复位第一帧与各自结果末帧一致，最后帧回到准备姿势。
- 主窗口首页和鱼塘页的静态猫替换为已绑定真实 PetStateController 的动画猫；Windows 桌宠也使用新播放器。

这是更新分层序列帧播放器和素材，不是生成 Unity `.anim`、Animator 或骨骼动画。关键帧烘焙不在本次交付中。

## 2. 文件组织

Unity：`fish-social-unity/Assets/StreamingAssets/Fishing/`。

Windows：`desktop-overlay/OverlayResources/fishing/`，构建后复制到 EXE 同目录的 `OverlayResources/fishing/`。

两处具有相同结构：

```text
Fishing/
  orange/manifest.json
  orange/body/<clip>/<frame>/body.png
  orange/body/<clip>/<frame>/head.png
  orange/body/<clip>/<frame>/far_hand.png
  orange/body/<clip>/<frame>/hands.png
  silver/body/...                 # 同上
  brown/body/...
  cream/body/...
  blue/body/...
  shared/<clip>/<frame>/rod.png
  shared/<clip>/<frame>/line.png
  shared/<clip>/<frame>/fish.png
  shared/<clip>/<frame>/bobber.png
  shared/<clip>/<frame>/bait.png
  inventory.json
  source-animation-v18.json
```

导出包包含 1550 张独立 PNG，每一帧的空图层也是有效透明 PNG，不用隐藏上一帧来冒充空图层。每一端约 99 MiB。旧 `cat.png` 可保留兼容其他旧工具，但九层播放器不读取或叠加它；新 manifest 中也不允许出现 cat 层。

## 3. 精确拆层与深度

默认由后到前：`body → head → far_hand → rod → line → fish → hands → bobber → bait`。

这不是全程固定的堆叠规则：每帧按自己的 `clips.<clip>.frames[index]` 重排图层。四个猫咪部位由 `pose-contours-v1` 动作轮廓拆分，同一原图像素只归一个部位，不叠加两份手臂、不重复画头。鱼竿穿头部分已经在导出的 PNG 中按遮挡消除，不能重新套用粗矩形裁剪。

`cat` 是旧兼容整身图，不可与 body/head 同时画。鱼、鱼线、浮标、饵料均为独立序列帧。

全部部件使用一致的画布、原点、缩放和脚底锚点。`place` 保留工程原有桌宠脚底定位，动作和换肤不能分别居中或单独按非透明边界缩放。

## 4. 动作表与时长

| 工作台动作 | 工程 clip | 帧数 | 播放方式 / 后续 |
|---|---|---:|---|
| 无竿站立 | idle | 1 | 保持；取 sit 的首帧 |
| 站立到准备 | sit | 4 | 每帧 120ms，之后 ready |
| 准备 | ready | 1 | 保持 |
| 挂饵 | bait | 4 | 220、220、260、300ms；等待权威阶段 |
| 抛钩 | cast | 4 | 140、140、140、180ms；等待权威阶段 |
| 等待 | fishing | 1 | 保持，对应工作台 wait |
| 咬钩后仰 | bite | 4 | 120、140、140、160ms，之后 effort |
| 持续发力 | effort | 6 | 140、140、160、140、140、160ms，从第 0 帧循环 |
| 咬钩完整片段 | hooked | 10 | bite 4 帧 + effort 6 帧，从第 4 帧循环 |
| 成功收杆出水 | reel | 6 | 每帧 100ms，总计 600ms，之后 catch |
| 空军收杆 | reel_empty | 6 | 每帧 100ms，总计 600ms，之后 reset_empty |
| 展示收获 | catch | 7 | 100、120、120、160、160、180、360ms，之后 reset_catch 或待领取保持 |
| 收获复位 | reset_catch | 4 | 每帧 120ms，之后 ready |
| 空军复位 | reset_empty | 4 | 每帧 120ms，之后 ready |

51 张源帧完整映射；增加 idle 别名和 hooked 组合后，每种皮肤有 14 个 clip、62 个索引。这 62 个索引不代表新增了 62 张独特动作。

## 5. 状态与播放流程

服务端仍然控制逻辑阶段。客户端只消费现有状态：

- `seated` → Sit/ready；初次从 idle 坐下才播放 sit，不反复坐下。
- `baiting` → bait，`casting` → cast，`waiting` → fishing。
- `hooked` → hooked，前 4 帧后仰，然后只循环发力的后 6 帧。服务器什么时候结束咬钩，动画什么时候退出循环；预览工具的秒数仅影响本地预览。
- `resolving + phaseContext.outcome=catch` → reel → catch。
- `resolving + phaseContext.outcome=escape` → reel_empty → reset_empty → ready。`escaped` 是 miss 事件 reason，兼容解析但不是标准 outcome。
- `stopping` 且无待领取鱼获 → 空军收杆；有待领取鱼获 → 成功分支。
- 有待领取鱼获时，保持 catch 最后一帧；领取后才复位，不自动把仍未领取的鱼藏掉。
- 自动结算或无待领取鱼获时，播放结果和复位，再衔接最新服务端状态。
- 收杆/展示/复位途中收到坐下、待机、挂饵、抛竿、等待状态，只保留最新一个状态，避免截断收杆，或回放一长串过时事件。
- 新咬钩、离线、拖动及换角色可立即中断旧流程。相同状态重复推送不能从第 0 帧重新开始。

`PondUserDto.phaseContext` 在用户合并时保留；miss/catch 事件提供本地结果兜底，进入新一轮阶段清理旧结果。已有 PetVisualState 0–9 数值不变，新增 EmptyReel=10，IPC 字符串为 `reel_empty`。Named Pipe 总协议版本不变。

每帧独立计时读取 `durationsMs[index]`。Unity 使用 unscaledDeltaTime 累积时间，WPF DispatcherTimer 根据当前帧设置间隔，不再一律按 125ms。两端都在完整显示最后一帧后才换片段，循环起点 0 合法。

## 6. 换肤边界

当前五种 skin：orange、silver、brown、cream、blue。这些是已存在的**同一形象配色版本**，转换器精确切出四个猫咪图层；不会在 Unity 运行时重新涂色。

它们不是三花、奶牛、蓝猫、银渐层等独立品种。工程账号原来的 gray/calico/tuxedo 等 petId 不强行映射到这些配色，仍走已有平面素材回退，不修改玩家头像、服务端猫种目录或账户数据。真正独立花纹品种仍需其对应的完整动作美术。

换素材应保持全部 frame 的画布、锚点、拆层和逐帧顺序。鱼种替换也应提供每帧对齐的 fish 层，直接换一张大小不同的鱼而不保持抓握点与挂钩点会穿帮。

## 7. Unity 预览工具

打开 **Window → Fish Social → Fishing Animation Preview**：

1. 选择五种源配色和 14 个动作。
2. 一键播放、暂停、重播、逐帧检查。
3. 分别播放完整成功流程和完整空军流程。
4. 设置 hooked 循环时长，观察后仰与持续发力如何分离。
5. 查看当前帧时长、后续片段、实际图层顺序；可开关九个图层并逐层检查。

这是 Editor 本地美术预览，不发送开始钓鱼/收杆、不改变账号皮肤。运行游戏时使用真实服务端状态。

## 8. 导出与校验

`tools/fishing-animation/export_fishing_pack.py` 要求 Python、Pillow；源目录为完整 animation-studio v18。转换器直接复制橘猫/道具 PNG，其他配色按 `atlasIndex` 从四张 body/head/far_hand/hands 图集中裁切，保留所有 RGB/Alpha 像素。

```bash
python3 tools/fishing-animation/export_fishing_pack.py --source /path/to/animation-studio --output /tmp/fishing-v2 --previous-manifest fish-social-unity/Assets/StreamingAssets/Fishing/orange/manifest.json
python3 tools/fishing-animation/verify_animation_port.py --source /path/to/animation-studio --pack /tmp/fishing-v2 --baseline-manifest fish-social-unity/Assets/StreamingAssets/Fishing/orange/manifest.json
```

验证器另需 NumPy；完整验证需要源 clips、组合预览和皮肤图集，不能只给源 JSON。`inventory.json` 记录导出内容，source-animation-v18.json 保留动作来源，manifest 记录源 JSON SHA-256。提交记录中的验证报告是实际已运行的像素级静态验证，不是 Unity 运行录像。

## 9. 运行验收与限制

已在素材端运行 11 项移植验证，以及 23 项工作台/精确拆层回归，均通过。检查了 1550 张 PNG 的画布、透明度、源像素一致性、九层顺序、互斥部位、全部配色切片和动作衔接。

Unity EditMode 回归已加入 `Assets/Tests/Editor/DesktopFishingAnimationTests.cs`，包含 manifest 格式、逐帧计时、循环、结果保持、中断和状态映射。**当前本机没有 Unity/.NET 构建环境，没有执行这些 C# 测试，也没有完成游戏运行和 Windows 桌宠联机视觉验收。**

在 Unity Test Runner 中执行 EditMode 测试，再运行主窗口检查首页和鱼塘动画。Windows 必须重新构建 `desktop-overlay/FishSocialOverlay.csproj`，并随新二进制分发最新 OverlayResources/fishing；旧 EXE 的七层加载器不能正确播放 v2 九层资源。该 csproj 当前目标为 netcoreapp3.1/WPF，需要对应 Windows 构建环境；构建参数沿用项目既有流程，不需要调整服务端。

验收至少覆盖：无竿站立→坐下、抛到脑后、后仰→循环发力、成功与逃脱两条收杆、待领取保持→领取复位、自动结算→复位、新咬钩/拖动/离线打断，以及其他玩家的 escape 结果。
