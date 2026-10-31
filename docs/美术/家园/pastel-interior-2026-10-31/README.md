# 粉彩室内效果提案

统一入口：仓库根目录 `现行预览.html#home-interior`，导航位于「家园与地图 → 室内画风提案」。提供主界面效果、完整室内原画、上一版对照、现有房间对照和猫咪显隐。主界面 UI、字体和猫咪复用当前浏览器原型，室内原画为本次新设计；当前为效果评审，未替换 Unity 正式家园资源。

当前默认使用第四版 room-pastel-v4.png，通过内置 image_gen 重绘。以现用粉彩钓鱼场景 lake-01-mint.png 为主要画风与色彩参照，第二版房间仅用于布局参考，猫咪用于判断角色与背景关系。使用薄荷青、奶油黄、鼠尾草绿和淡紫灰的连续色系，恢复粉彩绘本笔触、柔和彩色边缘与简化块面，减少写实木纹和统一粗描边。保留室内均匀光照和空置中央活动区。完整提示词为 [prompt-v4.txt](prompt-v4.txt)。生成源文件：/Users/red/.codex/generated_images/01a1532f-7ed4-7881-a467-94e6e4a376c5/exec-328095a0-2dd1-486f-90a6-aec540adbe51.png。

整合页增加「钓鱼场景对照」按钮，直接查看同一画风参照整图 fishing-style-reference.png。默认效果图为 main-screen-preview.png，前一版截图保留为 main-screen-v3.png。

第三版 room-pastel-v3.png 使用猫咪为主要画法参考，采用粗描边和简化平涂。用户认为它和现用彩绘钓鱼场景差距过大，保留用于上一版对照，不再默认展示。提示词为 [prompt-v3.txt](prompt-v3.txt)。

第二版 `room-pastel-v2.png` 通过内置 image_gen 对第一版定向重绘：去除墙面和地板的斑驳阳光、投影与高光碎片，使用均匀室内环境光；降低亮度与饱和度，墙面、木材、布料各自归整为连续色块，减少纹理密度。保留微弱物体明暗、接触阴影及原有布局，没有对图片应用整体灰色滤镜。完整编辑提示词为 [prompt-v2.txt](prompt-v2.txt)。生成源文件是 `/Users/red/.codex/generated_images/01a1532f-7ed4-7881-a467-94e6e4a376c5/exec-00860dbb-ed92-4d09-bf68-0fd1667cee6e.png`。第一版原画和主界面图保留为 `room-pastel-v1.png`、`main-screen-v1.png`，供对照。

原画 `room-pastel-v1.png` 使用内置 image_gen 生成。风格参考是最早 30 张粉彩场景中的 `lake-01-mint.png`，布局参考是 `output/home-room-proposal/room-v4-home.png`。采用奶油黄、鼠尾草绿、薄荷青、暖木与淡紫阴影，统一块面笔触；保留窗边猫窝、低矮钓具柜、鱼缸、猫爬架和中央活动区。完整提示词保存在 [prompt.txt](prompt.txt)。

生成源文件：`/Users/red/.codex/generated_images/01a1532f-7ed4-7881-a467-94e6e4a376c5/exec-c866d0e7-f38b-4f52-a37c-e5920e75c25c.png`。项目目录内保留原画副本；`main-screen-preview.png` 为使用既有主界面 UI 与猫咪叠加后的浏览器截图。
