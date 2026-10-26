# 参考配色与池塘类型试装

采用用户提供的参考配色：青蓝天空和水面、橄榄绿植被、青绿屋顶、米白墙体、暖木色。六个环境层使用内置 image_gen 校色；提示词与生成来源记录在 provenance.json。云、水纹、猫咪与木凳沿用原素材。未加入全局灰色滤镜。

## 已落地范围

- 类型：pond / 池塘。当前目录下该类型仅有 pond-novice「城郊练杆塘」，20 个钓位。
- Unity：Assets/Desktop/OverlayArt/Ponds/layered/pond-village-v3/，预制体 Assets/Desktop/OverlayLayouts/pond-novice.prefab。
- 桌面端：OverlayResources/ponds/layered/pond-village-v3/ 与 layouts/pond-novice.json。
- 本次客户端导入分层静态资源。云漂移、完整树网格晃动、水纹流动在 index.html 与 motion-preview.mp4 中验证，尚未接入客户端运行时。

## 统一坐标

1680×560 逻辑画布，左上角坐标原点。10 个图层共用 layout.json；天空、云×3、远林、水底、水纹、草岸、完整树群、房屋依次排列。材质透明留白保留，不按各关重新裁边。导出坐标保留三位小数。

木凳落地 Y=416，猫咪脚底使用已校验的接触点。新场景引用 layerLayout，批量应用也会从同一份表恢复各层位置。仅池塘类型试装入口会筛选 pond 类型所有关卡；新增同类型关卡时共用此表。

Unity 菜单：Fish Social > Pond Art > Apply Layered Pond Pilot And Export。

## 验证

- Unity 2021.3.29f1c1 批处理导入、预制体保存、PNG/JSON 导出和实际渲染成功。
- 全 21 关、420 钓位资源引用和猫咪/座位接触点检查通过。
- 本次仅 pond-novice 的预制体及布局变化，其他 20 关文件哈希未变。
- 10 层导出位置与统一模板最大误差约 0.000563 逻辑像素。
- 连续树网格 723 个测试状态无翻折；树干固定区域 479534 像素在不同时间帧逐像素一致。
- Mac 上未运行 Windows WPF 客户端；其引用、图层和坐标已通过静态检查。

实际预览和完整检查报告：/Users/red/Desktop/fish/output/scene-revision/animated-villager-v3/。

执行 python3 scripts/desktop/verify-pond-art.py 可核查每层位置、资源引用和座位接触点。
