# 《晴窗小憩》Suno 试作提示词

2026-10-31。依据当前首页原创候选的配器、气氛和段落设计，交由 Suno 重新创作同方向的纯音乐。当前尚未向 Suno 提交，尚无 Suno 生成音频。

## 填写方式

打开 <https://suno.com/create>，选择 **Custom**，开启 **Instrumental**；歌词留空。标题可用「晴窗小憩 · Suno 试作」。在 Styles 填下面第一段，在 Advanced Options → Exclude 填第二段（界面有该选项时）。

## Styles — 直接复制

```text
Original instrumental chamber-folk theme for a cozy game's home screen. Around 85 BPM, G major, 4/4, about 90–120 seconds. Clear, warm nylon-string classical guitar in the foreground: delicate fingerpicked melody, rolling arpeggios, gently moving thumb bass, soft plucks with natural ringing tails. A breathy piccolo in its mellow lower register answers selected phrases; very soft viola and cello enter sparingly in the middle. Sunlight through curtains, a safe little room, quiet happiness, tender companionship. Relaxed and lightly playful, with a memorable lyrical motif and thoughtful harmonic movement. Four connected sections: guitar theme, varied theme with woodwind replies, a warmer chamber development, then an intimate varied return. Natural phrasing, subtle rubato and expressive dynamics, short breathing spaces, restrained counterpoint. Intimate acoustic recording, clear separation, subtle room ambience. Gentle cadence with ringing tails, suited to repeated background listening.
```

## Exclude — 直接复制

```text
piano, vocals, humming, choir, drums, electronic beats, synth pads, epic orchestral swells, harsh high flute, lo-fi noise, heavy reverb
```

## 对应中文意图

古典尼龙吉他在前景，清晰轻拨、分解和弦和拇指低音；柔和短笛只在选定乐句回应，中段少量中提琴和大提琴提供暖意。有可记住的旋律动机、和声发展和变化回归。情绪是被阳光与温柔包围的闲暇时光，放松但有轻微活泼感。乐句有呼吸、强弱和细微速度变化，保留拨弦尾音。避免钢琴、人声、强节拍、尖锐高音和铺满全曲的氛围垫。

速度、调性、时长、配器及段落均为生成目标，实际结果需要试听核对；文字提示不保证重现当前候选的旋律或直接得到无缝循环。

## 回收试听

生成结果仍作为候选试听，后续音频统一加入根目录 `现行预览.html#audio` 的首页音乐区与当前候选对比；用户采用前不替换正式游戏 BGM。

## Suno 官方字段说明

- Custom / Instrumental：[官方帮助](https://help.suno.com/en/articles/3726721)。
- Advanced Options / Exclude：[官方帮助](https://help.suno.com/en/articles/3161921)。
