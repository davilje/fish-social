# 桌面游戏声音接入（2026-10-23）

## 行为

- 游戏启动后播放原创背景音乐《池畔晴日 / Pond Daylight》，80 秒立体声循环，轻柔的钢琴／木琴音色，无人声和鼓点。
- 按钮、菜单开合、选项切换、成功／失败反馈、抛竿、入水／咬钩、收获配套音效。禁用按钮和鼠标悬停不播放。
- 点击自己的猫咪和成功收获触发三种猫叫的随机变体；2.5 秒冷却，连续点击不叠加。
- Unity 与 WPF 桌面悬浮窗共用 Unity 播放器；WPF 通过已有 IPC 的 `play_audio` 命令、`text` cue 字段发送交互反馈，不另开播放器。
- 主窗口隐藏到托盘时音乐继续播放；音效与音乐音量分别控制。音量为 0 时静音。

正式界面入口：**设置 → 托盘与其他**，向下滚动可见「背景音乐」「音效与猫叫」两条音量滑杆；JournalV2 设置也同步接入。

## 音量与生命周期

- 默认音乐 35%，音效与猫叫 70%；保存为本地 PlayerPrefs 的 `FishSocial.Audio.MusicVolume` 与 `FishSocial.Audio.EffectsVolume`。
- 音乐、交互／钓鱼音效、猫叫使用三个 AudioSource，互不打断；音乐使用 `streamAudio` 加载。
- 音量修改延迟合并保存，退出时刷新；NaN／Infinity 恢复默认，越界值限制到 0–1。
- 游戏运行无需下载、Python 或额外音频依赖；全部 WAV 由 Unity 的 StreamingAssets 随包复制。

## 素材与维护

- 资源、来源与授权：`fish-social-unity/Assets/StreamingAssets/Audio/` 下的 `manifest.json` 和 `CREDITS.md`。
- 14 个 WAV，总计约 14 MiB。统一 PCM 16-bit、44.1 kHz，音效单声道、音乐立体声。
- 原创音乐与音效可用 `python tools/audio/build_desktop_audio.py` 重建；生成依赖 NumPy、SciPy、SoundFile。首次重建会下载三个已记录的 CC0 Freesound 来源；源码输入缓存不纳入 Git。
- 无第三方依赖的资源检查：`python3 tools/audio/verify_desktop_audio.py`，验证 14 个文件的 SHA-256、PCM 格式、时长、峰值、RMS 和音乐循环边界。
- 猫叫 01 为作者明确说明的人声拟猫表演，02、03 为真实猫叫；均从标记 CC0 的来源取得并处理。

## 验证记录

- 资源完整性：14/14 通过；音乐 80 秒，首尾对应声道采样差为 0；无削波或空音频。
- WPF 正式项目：macOS 上启用 Windows Targeting 交叉编译，0 errors。Windows 原生鼠标／拖动与扬声器听感仍需目标设备验收。
- Unity 2021.3.29f1c1 EditMode 专项：`FishSocial.Tests.Editor.DesktopAudioTests` **11/11 通过**。覆盖独立音量持久化、静音与异常值、三路非空间播放、音乐循环、设置布局／重新打开、按钮语义和真实 UnityWebRequest 解码全部 14 个 WAV（含流式立体声音乐）。
- Unity 正式脚本编译成功；相关文件 `git diff --check` 与两份 Python 脚本语法检查通过。
- 试听页与本轮原始验证输出：`/Users/red/Desktop/fish/output/game-audio/index.html`、`unity-test-results.xml`、`unity-tests.log`、`validation.json`。
