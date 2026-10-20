# 鱼竿完整性与角色遮挡

原鱼竿在导出时减掉了橘猫头部的透明度。它是针对橘猫轮廓的贴图，不适合直接共享给头形不同的猫咪或鲸鱼娘；角色没有盖住原缺口时，就会看到杆身镂空。

当前使用 `rodOcclusionModel: head-layer-order-v1`。恢复未经头部裁切的完整杆身，继续沿用角色四层、道具五层的九层结构。握竿时保持 `far_hand < rod < head < hands`：远手在杆后，近手在杆前，角色自己的头部自然盖住杆身。身体仍在杆后，不会把整根鱼竿藏到身后。收获展示中原本停在身后的鱼竿保留其后景顺序；鱼、线、浮漂、饵沿用各帧原先的前后关系。

必须读取每帧的顺序：源稿使用 `renderLayerOrder`，Unity / Overlay 使用 `clips[clip].frames[index]`。顶层 `layers` 只是图层集合，不能作为固定绘制顺序。无需增加运行时代码裁切、轮廓蒙版贴图或为每个品种制作不同的残缺鱼竿。新增角色仍须提供准确、无重叠的头部与双手拆层，不能任意把手划进头部。

## 本次范围

- 橘猫、三花、银渐层、棕猫、奶油猫、蓝猫、黑猫、白猫、奶牛猫，覆盖 16 个运行片段、每只 74 个帧记录，共 666 帧。
- 从源脚本重放 63 个源帧的原始鱼竿几何和变换，恢复完整杆身；未改角色画稿、花斑、姿势、位置或动作时长。
- 同步 Unity、Windows Overlay 的共享鱼竿与分层顺序，重建 144 张移动端 / Web 动作图集、完整预览和纪念照。
- 鲸鱼娘六个关键姿势试绘也改用完整杆身和自己的头部遮挡；仍为试绘，未作为完整角色安装。

## 维护与验证

`rod-source/` 保存恢复的 63 张完整杆身及其 SHA-256、逐帧层级和原始 rig 参数。恢复工具会对比源角色像素，发现角色几何有变化就停止，不能用旧轮廓处理新角色。

```sh
python3 tools/fishing-animation/recover_complete_rods.py --authoring-root /path/to/fish --output docs/art/cat-skins/rod-source
node tools/fishing-animation/repair_rod_occlusion.mjs --source-animation /path/to/fish/output/animation-studio/animation.json
node tools/fishing-animation/export_cat_client_assets.mjs
node tools/fishing-animation/build_breed_review.mjs
node docs/art/whale-maid/template-study-v1/prepare.mjs
node docs/art/whale-maid/template-study-v1/build.mjs
python3 tools/fishing-animation/verify_rod_occlusion.py
node tools/fishing-animation/verify_cat_resource_routing.mjs
node tools/fishing-animation/verify_breed_packs.mjs
```

Python 需要 Pillow、NumPy，Node 图像脚本需要 Sharp；资源路由检查另需 TypeScript。可通过 `SHARP_MODULE`、`TYPESCRIPT_MODULE` 指定本地依赖。重新恢复前须先用更新后的源工作台脚本导出 `animation.json`，不可再将旧版预裁切杆身导入。普通旧版五色导出器不能覆盖当前九种绘制角色。

`rod-occlusion-repair.json` 记录资源修复，`rod-occlusion-verification.json` 记录九只猫逐帧检查：完整杆身哈希、两端一致、分层无重叠、双手夹竿和真实合成中的头部遮挡。它们是素材及合成检查，不代表 Unity 或 Windows Player 实机验收。
