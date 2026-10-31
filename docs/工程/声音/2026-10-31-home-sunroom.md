# 2026-10-31 首页 BGM《晴窗小憩》试听候选

用户要求「根据原神中《将世事高枕》这曲希特拉莉房间的小曲的乐器和演奏风格，重新设计一曲首页面的 bgm」。本轮制作原创首页配乐候选，在[总预览页](../../../现行预览.html?audio=20261031-sunroom#audio)的声音分类直接试听；正式首页音乐仍为已采用的 v5《日光下的闲谈》，本次候选等待用户听感认可和采用。

## 配器与创作方向

参考曲为 HOYO-MiX《The World at Bay Beyond the Pillow／将世事高枕》，[官方曲目页](https://www.youtube.com/watch?v=_teoS2qjaMU)。[演奏署名线索](https://genshin-impact.fandom.com/wiki/The_World_at_Bay_Beyond_the_Pillow)列出古典吉他范晔、短笛程晓华与 Budapest Scoring Orchestra；此处说明参考配器依据，不将非官方转录 MIDI 的乐器标签当作原曲配器。

新曲以尼龙吉他的清晰轻拨、柔和分解和弦为主体，木管作短句应答，弦乐稀疏支撑；主题陈述后作发展与变化回归，以呼吸留白和强弱变化呈现阳光、温馨、轻松宁静的首页氛围。不使用钢琴。主题、和声、段落均另行创作，不使用参考曲录音、旋律、采样或 MIDI。

候选资源位于 `docs/美术/声音/home-sunroom/assets/`：

- `home-sunroom.wav`：完整候选。
- `excerpt.wav`：同一完整候选的短段试听。
- `score.json`：实际乐谱与演奏事件。
- `manifest.json`：来源、许可、处理、音频参数及哈希。

生成结果为 G 大调、4/4 拍、32 小节，标称 85 BPM，各小节随乐句在 80–87 BPM 间变化；完整 PCM 时长 90.587574 秒，短段 22.528912 秒，共 407 个音符事件。四个八小节段落分别为吉他主题（0:00）、木管应答（约 0:23）、弦乐展开（约 0:45）、变化回归（约 1:08）。吉他始终承担主体，短笛与弦乐在选定乐句加入；声部电平与具体演奏时间以 `score.json`、`manifest.json` 为准。

吉他来自 [Meta Nylon Guitar](https://github.com/facebookincubator/Meta-open-sound/tree/251c0be57d5d1b3242b91f53e0cde62ab1cb42d9/systems/instruments/meta-nylon-guitar)，短笛、中提琴组及大提琴组来自 [VSCO 2 Community Edition](https://github.com/sgossner/VSCO-2-CE/tree/440300901dfe9275fd84e0b7763af1f8443ae62e)，均为 CC0-1.0。清单共 42 条来源记录（37 条实际音频输入及 5 条许可／README），记有固定 commit、Git blob 与 SHA-256，原许可／README 随试听保留，详见[来源说明](../../美术/声音/home-sunroom/assets/CREDITS.md)。VSCO 文件名采用的八度命名与科学音高不同，渲染按源录音实测基频校准映射，使目标谱面音高正确；不是把整份乐谱统一降低八度。本曲使用真实乐器单音录音采样编曲及程序渲染，不是真人专为本项目整曲演奏，也不是音频生成模型产物。

吉他逐个使用对应谱面音符的原录音，不作整数半音转调；对原录音实测偏离 A440 的音准作小于 35 音分的微调，相当于轻微调弦。源音频字节及其来源哈希保持不变，校正作用于渲染，具体原始／校正后误差记录在本轮独立复核中。

## 试听与采用边界

总页提供完整试听、短段、音量和循环，保留旧首页音乐对照。音乐与已采用的湖面／风叶、混音录音、动作／猫叫轮流播放；离开声音分类或页面隐藏即停止。独立资源目录只是总页的依赖，不新增单独交付入口。

本次未替换 `Assets/StreamingAssets/Audio/music/pond_daylight.wav`，未改正式音乐／环境清单、采用记录、已确认短音锁及资源。钓鱼场景继续禁用 BGM／按钮音；已认可的湖面、连续交融风叶、合书关闭、动作和猫声保持。未重建 Windows 包，未执行 Windows 画声延迟实测。

## 验证记录

- 完整曲及短段均为 44.1kHz／PCM16／双声道；完整曲实测真峰约 −9.62974dBTP、RMS 约 −27.04363dBFS，短段真峰约 −10.08289dBTP、RMS 约 −26.99659dBFS。来源、乐谱、源基频映射、输出哈希与循环边界的本轮检查见[音频技术验证](home-sunroom-2026-10-31/technical-validation.json)及[谱面复核](home-sunroom-2026-10-31/score-review.json)。
- 最终完整曲 SHA-256 为 `4042304f6024b5f10df613f666ffafc630bb45a547101dd6e28488cf426d953e`，短段为 `ee8bb26372b7f65cc010760e7dc4b0f84850164c5941ef502ca6de6a053e642a`。清单与浏览器使用同一校准后音源。
- [首页音乐浏览器验证](home-sunroom-2026-10-31/preview/browser-validation.json)通过 7 组，覆盖真实播放、原生循环、音量、短段／旧首页音乐切换、环境／录音／动作猫叫互斥、停止、隐藏与总页导航销毁，以及桌面／390px 移动布局；无 JavaScript／HTTP 错误。
- [最终音源播放检查](home-sunroom-2026-10-31/preview/final-audio-smoke.json)核对上述最终哈希、实际解码、四段首次播放前定位与真实跨尾回环。本地静态服务不支持 Range 时，试听播放器将当前选曲完整读取为 Blob URL，确保原生进度跳转可用；切曲清除旧音源，离页回收 Blob。吉他实际渲染处理后的独立基频抽测残差均小于 2 音分，原录音偏音与校正值见谱面复核。
- [环境回归](home-sunroom-2026-10-31/environment-regression/browser-validation.json)通过 7 组，含约 29 秒实际环境播放，确认首页音乐卡片加入后保留当前风叶与原动作／猫叫行为。

本轮为音频资源与浏览器试听检查，未重新运行 Unity 专项。历史风叶的 Unity 41/41 和旧 v5 音乐验证不计为本曲通过证据。技术检查通过不代表用户已听感认可，新曲正式采用项继续留空。

## 策划同步

本次作为 STEAM-DESKTOP-22I 的首页音乐增量候选记录，保留历史已实现状态及设计日期 2026-10-24／实现日期 2026-10-30，在规格、交接提示词、计划表与看板中单独标明新曲待试听采用，不将候选误写为正式已采用。
