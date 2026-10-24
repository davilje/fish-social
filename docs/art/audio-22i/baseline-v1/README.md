# v1 冻结基线

2026-10-24 在替换正式音频前复制原始 14 个 WAV、manifest 和 CREDITS。另从旧试听 assets 复制 bite/reel/escape/groundbait 四种 v1 候选，以便统一路径进行 A/B；这四条仍由 `../build_preview_audio.py` 和 `../policy.json` 定义，不属于原生产 manifest 的14条。

此目录只用于比较，正式游戏读取 StreamingAssets/Audio 的 v2。不要更新这里的旧音色来替代新版本。旧生成脚本保留作历史记录，已防止覆盖含 v2 配置的生产目录。
