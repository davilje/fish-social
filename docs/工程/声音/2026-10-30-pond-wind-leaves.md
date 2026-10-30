# 2026-10-30 独立风声与树叶沙沙声

当前湖面分场景与间歇风叶实现见[最新记录](2026-10-30-lake-gust.md)；下文保留本阶段历史数据。

用户追加「独立添加风声、树叶沙沙声」。游戏与[统一预览](../../../现行预览.html#audio)现使用三条独立环境音轨，可与现有水声叠加，钓鱼场景继续关闭 BGM 与按钮音。

| 音轨 | 文件 | 时长 | 固定增益 | 录音声场 |
|---|---|---|---|---|
| 水声 | `ambience/pond_shore.wav` | 48 秒 | 0.65 | 原始立体声，原 WAV 哈希保留 |
| 风声 | `ambience/pond_wind.wav` | 56 秒 | 0.45 | 官方预览实际为双单声道，未人工扩宽 |
| 树叶沙沙声 | `ambience/pond_leaves.wav` | 48 秒 | 0.40 | 保留原始立体声及录音自身轻微风声 |

风声来自 mario1298 的 [a gentle breeze, wind 1](https://freesound.org/people/mario1298/sounds/181250/)，叶声来自 ecfike 的 [Wind and Leaves.wav](https://freesound.org/people/ecfike/sounds/183496/)，均为作者页面明确 CC0 的两条不同实录。使用官方 HQ MP3 预览，保留固定下载 SHA 和许可证据，未宣称原始无损录音。两轨各取连续片段，轻滤低频／尖锐高频，以 8 秒交叉过渡循环、单一常量增益处理；没有合成噪声、压缩、硬限幅、人工立体声或额外加入的鸟叫／人声。叶轨包含现场轻风，不是分离出的干声。

## 接入与控制

- `ambience-manifest.json` 升级为 version2 `loops` 数组。Unity 使用三条独立流式环境声源，连同既有音乐循环、四条短音声部共八个声源。原四短音仲裁不变。
- 各轨音量 API：`DesktopAudioService.GetAmbienceLevel(id)`、`SetAmbienceLevel(id, value)`，id 为 `water`／`wind`／`leaves`；各自 PlayerPrefs 保存 0–1，默认 1。有效输出为「音效总音量 × 固定增益 × 各轨保存音量 × 淡入淡出」。单轨归零立即停该轨，不影响另外两轨。
- 沿用 2 秒场景／隐藏淡入淡出；总音量静音、组件禁用立即停止全部。音乐音量不影响三轨。钓鱼场景的 BGM／UI 拦截继续生效。
- 预览分别提供三轨播放、淡出及音量滑杆，也可播放／淡出全部。单轨操作不重置其他音轨；各轨音量保存在当前浏览器。首次加载或刷新不自动播放。停止全部会取消待播，隐藏／恢复遵从各轨原播放选择。

## 验证

- [Unity EditMode 36/36 通过](pond-wind-leaves-2026-10-30/tests.json)，[原始 XML](pond-wind-leaves-2026-10-30/editmode.xml)。包括三环境 WAV 实际流式解码、八声源、各轨音量／保存／归零互不干扰、全局静音恢复、场景／隐藏／禁用及旧单轨配置兼容。
- [资源及混音测量](2026-10-30-pond-wind-leaves-verification.json)：50 条正式 WAV 格式／哈希／循环衔接通过，原 47 条已采用资源及 cue-config 不变。风／叶 4×真峰约 0.115，三轨循环首尾 PCM 差为 0。三环境＋两钓鱼＋一猫叫全音量保守真峰上界 **−1.93092 dBTP**；短音缓存仍为 3,383,056 字节。
- [风叶资源生成与复现](2026-10-30-pond-wind-leaves-assets.json)：固定源下载 SHA、官方 CC0 页面快照 SHA、缓存重建 WAV/meta 哈希一致，篡改源缓存会拒绝并保护输出。
- [浏览器测试与截图](pond-wind-leaves-2026-10-30/preview/browser-validation.json)：独立试听、三层叠加、循环跨边界、独立停止／归零／音量保存、隐藏恢复、总停止及待播竞态，无 BGM 请求、HTTP／JavaScript 错误，桌面与 390px 手机布局正常。[统一入口验证](pond-wind-leaves-2026-10-30/preview/integration-validation.json)通过真实点击启音，不依赖自动播放豁免。

测试在 macOS Unity 编辑器及 Chrome 执行；未重新构建 Windows 包，未代替用户听感验收或 Windows 画声延迟实测。

## 复现

```sh
python tools/audio/build_pond_ambience.py --sources <water-cache>
python tools/audio/build_pond_wind_leaves.py --sources <wind-leaves-cache>
python tools/audio/verify_desktop_audio.py
python tools/audio/verify_pond_ambience.py
```

两个生成器通过 `tools/audio/ambience_bank.py` 仅更新自身轨道，保留其他轨道、来源和生成脚本记录。完整来源及许可位于正式 `Audio/CREDITS.md` 与环境声清单；沿用现有 StreamingAssets 离线打包流程。
