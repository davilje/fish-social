# 钓鱼动作对照 · 22D / 22F

打开 `index.html`，可直接通过 `file://` 查看。左侧为原时间表，右侧为新的运动采样及按阶段循环。

- 十种现有角色共用道具运动数据，各自的身体、头、手素材保持原样。
- 抛竿 4 → 13，收杆及空军 6 → 16，打窝 8 → 18 个采样；不增加 PNG，不修改原动作总时长。
- 打窝可设置持续秒数或点击「结束打窝」，结束后收手并恢复准备。
- `source-rig.json` 保留原工作台的轨迹坐标；`preview-data.js` 与正式 `motion-samples.json` 同源生成。
- `browser-verification.json`、三张 comparison 截图为本轮 Web 检查结果；不是 Windows 原生截图。

完整接入、复跑和限制见 [交付说明](../../engineering/2026-10-24-overlay-motion-22df.md)。
