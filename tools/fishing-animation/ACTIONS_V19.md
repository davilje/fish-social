# 打窝与离席动画（v19）

当前九种绘制角色统一使用 `head-layer-order-v1` 鱼竿遮挡：完整共享杆身，握竿帧按远手 → 鱼竿 → 头部 → 近手绘制，不再从共享杆身中预先挖掉橘猫头部。具体顺序以每帧清单为准，见 `docs/美术/猫咪形象/rod-occlusion.md`。下文五色及原验收记录属于最初 v19 记录。

- 打窝 `groundbait`：8 帧，980 ms。收竿腾手、捧起窝料、送手抛撒、窝料散落、恢复准备。
- 离席 `leave`：4 帧，580 ms。准备坐姿、收竿后空手坐姿、起身、空手站立。
- 打窝复用已拆层的挂饵姿势，离席逆序复用坐下姿势；本次不是重新绘制角色。窝料轨迹独立烘焙在 `bait` 图层。
- 保持 640 × 512 画布、九层绘制顺序和五套原有配色。源工作台为 14 个状态、63 帧；运行包含待机和组合咬钩，共 16 个片段、每皮肤 74 帧记录。

## 播放接线

Unity 收到 `groundbaiting` 后映射 `Groundbait`，传给 overlay 的动作名为 `groundbait`。单次播放结束进入 `ready`，相同状态快照不会反复触发。

角色从在座状态变为 `idle` 时播放 `leave`，再进入 `idle`。离线、拖动直接中断；收获或空军收杆尚未完成时，沿用原有延迟切换流程。离席最后一帧与待机逐层一致，重新入座播放 `sit`。

## 文件

- `../output/animation-studio/index.html`：选择“打窝”“离席”，逐帧或播放查看。
- `../output/animation-studio/clips/groundbait`、`clips/leave`：逐帧 PNG、分层图、源角色及拆层预览。
- `pack/`：Unity 与 overlay 共用的完整运行素材包。
- `unity/`、`overlay/`：本次目标代码副本；`install.py` 仅更新开发分支相关文件和动画包，检查并发修改，保留已有 `.meta`。

源生成：`build_animation_studio.py`；转换：`unity-animation-port/export_fishing_pack.py`。Unity 的 Fishing Animation Preview 下拉列表自动显示新增片段。

## 验证记录

Unity 2021.3.29f1c1 EditMode 的 `DesktopFishingAnimationTests`：100 项通过（包含打窝完成后不重复触发、离席返回待机、再次坐下）。源素材 17 项、精细拆层 7 项、运行素材包 11 项、工作台时间轴 6 项检查通过。Windows WPF overlay 已更新代码和素材，本机未执行 Windows 发布或视觉验收。
