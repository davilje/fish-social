# 场景维护说明

分层、三层循环云和风吹树叶的预览在 [motion.html](motion.html)，也收在仓库根目录 [现行预览](../../../现行预览.html) 的「场景」。

预览直接读取 Unity 项目内的场景资源和 `motion.json`，支持全部已分配鱼塘和 14 个候选场景。当前 version 3 云团配置已接入：30 张系列场景及原千岛湖共 31 套场景已替换为水平云，三层以不同速度持续水平循环，大云缓慢平移；树叶动效保留。可使用 1×、10×、30× 查看，暂停后切换场景仍会显示首帧。总预览链接加 `#scene` 可直接进入「场景」。

| 文档 | 记录状态 |
|---|---|
| [overlay-scene-update-and-editor-menus](overlay-scene-update-and-editor-menus.md) | 专题说明／实施记录 |
| [pond-pastel-series-rollout](pond-pastel-series-rollout.md) | 专题说明／实施记录 |

天气云 v7：新增六类云型、五色系素材库，31 套场景采用九套天气配置；v9 为每场景一种云型、3～4 个不同轮廓。预览状态栏显示当前天气。配置及素材见 `fish-social-unity/Assets/Desktop/OverlayArt/Ponds/CloudLibrary/WeatherV9/`；完整产出见 `/Users/red/Desktop/fish/output/scene-revision/weather-clouds-v9/README.md`。
