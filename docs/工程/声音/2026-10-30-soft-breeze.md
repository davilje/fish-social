# 2026-10-30 轻风与清晰叶响

> 后续用户已确认应用连续交融方案，当前正式接入见[采用与验证](2026-10-30-flowing-adoption.md)。本页保留当时试听／实现记录。

根据「风声太响、太持久，像刮大风；叶声不清晰」的反馈，降低风声、缩短单阵，并从同一树叶实录中保留更多细碎摩擦声。正式 Unity 与[统一预览](../../../现行预览.html?audio=20261030-soft-breeze#audio)共用新资源与混音参数。

| 参数 | 调整前 | 当前 |
|---|---|---|
| 风基础增益 | 0.45 | 0.18，约降低 8 dB |
| 每阵风时长 | 6–10 秒 | 3–4.5 秒 |
| 叶声延后 | 2–4 秒 | 0.8–1.4 秒 |
| 叶声时长 | 6–9 秒 | 3–4.8 秒，风先退、叶声随后收尾 |
| 起音／收尾 | 2／3 秒 | 0.7／1.5 秒 |
| 叶基础增益 | 0.40 | 0.65 |
| 叶声滤波 | 240–3800 Hz | 450–6500 Hz |

完整一阵之后安静 20–38 秒，入场等待 6–12 秒。风叶仍非持续循环。水面档位、21 塘映射及钓鱼场景 BGM／按钮音静默规则沿用上一轮。用户各轨已保存的相对音量继续乘于新的基础增益。

叶声仍来自 ecfike 的 CC0 `Wind and Leaves.wav`，使用同一 128–184 秒片段、8 秒交叠，保持原立体声；本轮重新处理叶 WAV，未生成噪声、加入压缩或硬限幅。来源和处理记录见[资源报告](2026-10-30-soft-breeze-leaves-assets.json)及正式 `Audio/CREDITS.md`。风源和三条水声文件没有改变。

[前后测量](soft-breeze-2026-10-30/mix-comparison.json)显示，叶声 80–450 Hz 能量占比从约 19.4% 降至 1.6%，2500–6500 Hz 占比从约 25.5% 提至 57.5%；结合基础增益后，叶声整段 RMS 提高约 2.2 dB。这些测量说明频谱与电平变化，不代替用户听感判断。新叶 SHA256：`8332a842a5cc398d173786974688cf75f77a97fef6a9eab087097482bf363540`；预览媒体 URL 使用该内容哈希，避免继续播放缓存的旧叶声。

验证：

- [Unity EditMode 40/40](soft-breeze-2026-10-30/tests.json)，[原始 XML](soft-breeze-2026-10-30/editmode.xml)：实际五条环境 WAV 解码、新时长／增益、短阵起止包络、叶声拖尾、静音与隐藏取消。
- [资源与混音检查](2026-10-30-soft-breeze-verification.json)：52 WAV 格式／哈希通过，原 47 条采用音频不变；保守叠加真峰上界为 −1.285 dBTP，未发生削波。
- [浏览器检查](soft-breeze-2026-10-30/preview/browser-validation.json)及[统一入口嵌入检查](soft-breeze-2026-10-30/preview/integration-validation.json)：短风包络、风停叶尾、新叶内容哈希、独立混音及真实用户点击播放。

验证环境为 macOS Unity 编辑器与 Chrome，本轮未重建 Windows 安装包。

复现叶声：

```sh
python tools/audio/build_pond_wind_leaves.py --sources <leaf-cache> --report docs/工程/声音/2026-10-30-soft-breeze-leaves-assets.json
python tools/audio/verify_desktop_audio.py
python tools/audio/verify_pond_ambience.py --report docs/工程/声音/2026-10-30-soft-breeze-verification.json
```
