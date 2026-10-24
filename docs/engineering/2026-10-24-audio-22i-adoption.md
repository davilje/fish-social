# STEAM-DESKTOP-22I 当前方案应用与旧资源清理

本次按用户指令「将当前方案应用到项目内，剔除旧版无效资源」采用 v5 配乐与 46 条已确认的 v3 音效／猫声。采用记录始于 2026-10-24，最终清理验证于 2026-10-25 凌晨完成。此次为资源与维护工具收尾，保留既有 Unity／WPF 音频接入和其他任务的并行修改。

## 正式内容

- 正式资源目录：`fish-social-unity/Assets/StreamingAssets/Audio/`，47 条 WAV，与 manifest／cue-config 引用完全一致，无额外旧音频或孤立 WAV meta。
- 配乐：《日光下的闲谈》，v5，85.333333 秒；真实轻拨吉他主奏与指弹伴奏，无钢琴。配乐 SHA-256：`383ad0c0c1e9c3b11a4be5710039799ae5db3e40c6bb10d57872599d7375b516`。
- 所有 47 条音频及 cue 配置与采用时哈希一致。3 条猫叫 metadata 仅将旧来源说明指向留档位置，没有改变声音、许可或处理参数。
- Unity `DesktopAudioService` 从当前 StreamingAssets/Audio 读取配置及 `music/pond_daylight.wav`；WPF 继续通过既有事件与画面 marker 接入，没有独立重复音频包。
- [采用清单](audio-22i-adoption.json)／[已确认音效记录](audio-22i-sfx-acceptance.json)／[来源与许可](../../fish-social-unity/Assets/StreamingAssets/Audio/CREDITS.md)。

## 清理范围

删除旧根目录试听音频、baseline-v1～v4、v2～v4 试听音频与旧实现：合计 169 条音频、101,680,674 字节（约 96.97 MiB）；连同伴随文件共 386 个文件、102,378,326 字节。详见包含路径与删除前哈希的 [清理清单](audio-22i-cleanup.json)。另删除 12 个旧生成／安装／验证工具及其输入清单，详见 [工具清理记录](audio-22i-adoption/tools-cleanup-verification.json)；同时移除无效 Python 缓存与临时输出。

根试听入口和 v2／v3／v4 仅保留跳转到 v5 的 HTML；当前页移除所有旧版对比按钮、请求和失效链接。仓库音频只剩 47 条正式 WAV＋2 条当前 v5 音乐试听 WAV（完整生成输出和 8 小节片段）。当前完整生成输出用于校验和重建，不属于旧资源。

历史来源、许可与 manifest 移至 [retired](audio-22i-retired/README.md)，不保留旧 WAV，不作为运行或构建输入。既有工程验证报告加退役说明，保留原始事实。仓库外仍用于当前素材重建的源录音缓存保留；并行 22H 资源、既有非音频构建产物不属于本次删除范围。

## 后续维护

仅保留当前四个音频工具及 `tools/audio/accepted-effects.json`：

```sh
python tools/audio/compose_sunlight_v5.py --sources <录音缓存目录>
python tools/audio/integrate_music_v5.py --check-only
python tools/audio/integrate_music_v5.py
python tools/audio/verify_desktop_audio.py
python tools/audio/verify_music_v5.py --report docs/engineering/audio-22i-adoption/music-verification.json
```

音效锁独立保存 46 条资源处理／来源记录与配置哈希，安装不再读取已删除的历史基线。安装会核对采用记录中的全部 47 条音频，其他音乐哈希不会被静默采用。校验脚本默认只读，写报告须显式 `--report`。音乐源缓存缺少 tree 时可以从固定官方 Git tree 初始化并校验身份。

## 清理后验证

- [重建与保护校验](audio-22i-adoption/tools-cleanup-verification.json)：实际执行缓存源重建、安装和两项资源验证，47 条正式 WAV 哈希不变；8 项篡改／未采用资源拒绝用例通过。
- [缺失 tree 初始化](audio-22i-adoption/composer-cold-tree-verification.json)：临时缓存预放 26 个已验证录音／许可等输入，删除 tree 后真实联网获取固定 tree 并校验，再完整渲染，4 个当前音乐输出哈希一致；篡改 tree 映射在写素材前被拒绝。此项不是所有源录音从全空缓存下载的测试。

- [资源／混音检查](audio-22i-adoption/music-verification.json)：47 条采用哈希、46 条音效锁、配置、PCM 格式、循环、主题片段与完整曲对应关系通过。短音解码 3,383,056 字节，小于 4 MiB。满音量最坏混音上界 −1.30088 dBTP；循环首尾差 0。
- [浏览器检查](audio-22i-adoption/browser-validation.json)：当前音乐主题／完整循环切换、循环与停止、纸张与水声按钮、正常钓鱼 8 个 cue、独立音量与持久化通过；本次最大短声部 2/4；旧目录请求、HTTP 错误和 JS 错误均为 0。九场景入口保留，当前专项运行范围以报告为准。
- [桌面截图](audio-22i-adoption/preview-desktop.png)／[手机截图](audio-22i-adoption/preview-mobile.png)：已检查“已采用方案”状态、布局与无横向溢出。
- 旧基线目录及媒体确认不存在；旧页面仅重定向。活动代码／当前试听不引用旧音频。规划生成脚本仅更新 22I 说明，已执行 `planning:master-xlsx` 同步计划表和看板。

本次没有重新生成 Windows 可执行包，也没有进行 Windows 真机画声延迟测试；现有旧 Builds 目录不含此音频包，不能作为本版打包或实机证据。Windows 至少 30 事件同步测量仍按专项规格后续执行。
