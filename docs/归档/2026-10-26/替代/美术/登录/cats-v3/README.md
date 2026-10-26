# 登录场景：五只猫咪岸边吃烤鱼

> 2026-10-26 起退出当前阅读目录。这是 480×320 小窗方案。现行全窗口效果见 [五猫 v4](../../../../../../美术/登录/cats-v4/README.md)。

2026-10-26。基于用户已采用的简化粉彩 v2 背景，加入黑猫、奶牛猫、狸花猫「丧彪」、橘猫、三花猫，五只猫围着一盘烤鱼分享食物。

## 角色与绘制

五只角色参考当前项目 `Assets/Resources/Desktop/Theme/Cozy/手账/characters/{black,cow,sangbiao,orange,calico}/rest.png`。`references.html` 与 `cat-references.png` 是这些原资源的排版对照，不修改原角色素材。狸花使用丧彪，保留灰棕虎斑、奶油嘴胸、黄绿眼和环尾；没有以棕虎斑替代。

使用内置 image_gen 以 v2 背景和五猫参考板为输入，新绘吃鱼姿势；生成提示词见 `generation-prompt.txt`。这是依原角色形象重绘的静态场景，不是原动画帧拼贴，也未新增猫咪吃鱼动画。

## 应用

- 正式资源：`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/场景/login-background-cats-v3.png`，原始 1536×1024，不做像素后处理。
- `DesktopShell.prefab/LoginRoot` 使用新背景，Simple＋Preserve Aspect；原 v2 资源留作回退。
- 登录窗口保持 480×320，Logo 与文案收拢到上方，按钮位于 (108,111)，既有按钮整体缩放 .75，实际 264×40.5；底边 151.5，避开猫咪从约 y158 开始的区域。原按钮图层、命中、事件绑定保留。
- Logo (168,2,144,52.5)，副标题 y55／高17，欢迎 y73／高20，登录状态 y93／高17；页脚 y296／高18。各项仍是独立 UI，静态背景不含文字。
- `index.html` 切换 Unity 渲染／纯场景／上一版，支持实际尺寸。

本机验证使用 Unity 2021.3.29f1c1 编辑器；未重新打包 Windows 客户端，未执行 Windows Steam 登录。

验证完成：Unity 编译／Prefab 校验／主题重应用后背景检查；480×320 和 960×640 离屏渲染；按钮底边 151.5 小于猫咪安全区域起点 154；原有登录布局测试 2/2 通过。Web 三种模式图片加载、实际尺寸、390px 无横向溢出及无 JavaScript 错误检查通过。测试日志位于 `output/login-cats-v3/`。
