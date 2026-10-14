# Windows Overlay Cozy UI 交接

## 范围与整合顺序

- 已读仓库根 `AGENTS.md`；只直接编辑本 staging 子目录。未写只读仓库、未提交、未做 Unity 或移植脚本。
- 最新、可直接反向同步的 HUD 真源：`OverlayResources/hud/overlay-hud.json`，不是本说明中的示例。
- JSON 仍是 `960×560`、原有 **32 个 widget id**，保留 `parentId`、动作绑定、可见性默认值及文件名；矩形和资源引用已经实际更新。
- 新增共享主题：`CozyTheme.cs`、`CozyTheme.xaml`，由 `App.xaml` 合并。前者负责 PNG 加载、单色 alpha 保留、九宫格纸张和菜单按压状态；后者提供按钮、纸张、输入、滚动条、右键菜单、子菜单、分隔线、Tooltip 模板。
- 同源 73 PNG 没有改动。不要仅拷 JSON 或只替换颜色；需要一起整合主题、C# 接入点、XAML、Cozy PNG。

## 已修改路径

```text
App.xaml
CozyTheme.cs
CozyTheme.xaml
MainWindow.xaml
MainWindow.xaml.cs
OverlayHudLayout.cs
OverlayChatPresenter.cs
OverlayChatBubblePresenter.cs
OverlayHoverPresenter.cs
OverlayPetActor.cs
OverlayStatusIcons.cs
OverlayResources/hud/overlay-hud.json
tests/test_cozy_ui_source.py
tests/cozy_source_contract.json
COZY_UI_HANDOFF.md
```

`FishSocialOverlay.csproj` 未改；现有 SDK 默认包含新 C#/XAML，原 `OverlayResources` 输出复制规则继续使用。

## 最终 JSON 合同

顶层实际新增 `components`，不是旁路说明：`OverlayHudLayout` 反序列化、补齐旧 JSON 缺省值，并将组件参数写入共享动态资源；普通按钮和嵌套聊天按钮都经过 `ApplyCozyButtonVisual`。旧 JSON 仍可加载，由运行时补 Cozy 映射。

```json
{
  "width": 960,
  "height": 560,
  "components": {
    "button": {
      "sprite": "cozy/buttons/plate-default.png",
      "spriteHover": "cozy/buttons/plate-hover.png",
      "spritePressed": "cozy/buttons/plate-pressed.png",
      "spriteSlice": [64, 64, 64, 64],
      "pressedOffsetY": 3,
      "iconSize": 16,
      "disabledOpacity": 0.45,
      "padding": [2, 0, 2, 0]
    },
    "paper": {
      "sprite": "cozy/panels/paper.png",
      "spriteSlice": [64, 64, 64, 64],
      "padding": [12, 8, 12, 8]
    },
    "input": {
      "sprite": "cozy/panels/paper.png",
      "spriteSlice": [64, 64, 64, 64],
      "padding": [8, 6, 8, 6]
    },
    "scroll": { "width": 6 }
  },
  "widgets": [
    {
      "id": "btn_fishing_toggle", "parentId": "dock_fishing", "kind": "button",
      "x": 78, "y": 0, "w": 74, "h": 36, "z": 100, "visibleDefault": true,
      "sprite": "cozy/buttons/plate-default.png",
      "spriteHover": "cozy/buttons/plate-hover.png",
      "spritePressed": "cozy/buttons/plate-pressed.png",
      "spriteSlice": [64, 64, 64, 64],
      "iconSprite": "cozy/icons-light/rod.png",
      "label": "开始钓鱼", "fontColor": "#FF75472E"
    }
  ]
}
```

上例只展示一个按钮；整合时读取实际 JSON 的全部 32 项。每个实际 button 都显式重复三态路径和 `spriteSlice`，因此 Unity 导入不必先实现顶层组件解析。`iconSprite` 是独立前景 Image，不能把它当按钮 Background。pan 按钮使用“向左/向右”文字，资源没有方向箭头 PNG，未用 SVG、emoji 或 Unicode 三角代替。

- `spriteSlice` 顺序为 **left, bottom, right, top**，单位是源纹理像素，与 Unity `Sprite.border` 相同。三态 plate 都是 **407×400**，三态和 `panels/paper.png` 的 border 都为 **64**；不再使用旧聊天 `[26,18,22,22]` 或 scroll `[10,10,10,10]`。
- `padding` 顺序为 WPF **left, top, right, bottom**。Unity 原点换算沿用父代理既有 HUD 导入逻辑；JSON 的 `x/y` 是左上坐标，非 Unity pivot 坐标。
- 图标尺寸 **16 DIP**，读取 `icons-light` 的 alpha 并输出 **INK #75472E**，保留雕刻轮廓、抗锯齿与透明度。奶油 **#FFF7E9**、coral **#F9A33F**、periwinkle **#92A3E7** 为共享主题色，所有 JSON 文本显式写 `#FF75472E`。
- `fontColor` 沿用现有 WPF 合同 **#AARRGGBB**，不要直接交给 Unity 按 #RRGGBBAA 解析；`#FF75472E` 对应 Unity RGBA **#75472EFF**。
- Button 的 normal/hover/pressed 是三张真实 PNG，而非背景换色。原位透明命中宿主不动；只有内层 Plate（含图标/文字）按下向下 **3 DIP**，松开、离开或失去按压后回正常状态。菜单行也使用同一三态 PNG/位移。原 HUD click pulse 动画保留。
- `dock_chat` 是专用 nested Canvas，纸张层不接收点击；嵌套 `chat_toggle`/`chat_send` 使用同一个共享按钮 Style。聊天展开时 paper 随高度重新九宫格，不拉伸整张贴图。
- 纸张 source/slice、输入内边距、按钮内边距/位移/图标大小/disabled opacity、scroll width 均有实际消费路径。`CozyPaper` Style 使用共用 paper padding；显式 JSON 子 Rect 的 dock/status 不重复套这层 padding。

## 最终矩形与图标映射

| 根组件 | x,y,w,h | 说明 |
|---|---|---|
| menu_rail | 900,180,40,360 | 原 9 个菜单项的 y=0..320，贴右边缘 |
| dock_chat | 16,506,320,42 | 收起底边 548；展开高度 160，向上增长 |
| dock_fishing | 363,508,234,40 | 三按钮 x=0/78/156，74×36 |
| cap_status | 720,472,164,76 | 状态/塘/钓位分行，144px 文本槽 |
| txt_groundbait | 363,486,234,18 | 钓鱼组上方单行 |
| txt_error | 16,16,928,24 | 保留根提示与运行时内容 |
| btn_pan_left/right | 16/864,260,28,40 | 不与右侧 rail 重叠 |
| btn_debug_police/gameplay | 800/844,428,38,36 | 维持已有权限/可见性控制 |

聊天展开：log `(12,12,260,98)`；input `(12,118,260,32)`；send `(282,120,28,28)`；toggle `(282,7,28,28)`；scroll `(300,40,6,70)`；placeholder `(20,124,244,20)`。log/input 不重叠。JSON 父级坐标、收起/展开逻辑和 viewport reanchor 算法未改；600×300 时聊天宽度允许压缩，不做整窗缩放。

| id | icons-light PNG |
|---|---|
| btn_menu_toggle/settings, btn_debug_gameplay | settings.png |
| btn_menu_map / shop / friends / catch / leaderboard | map.png / shop.png / social.png / bag.png / trophy.png |
| btn_open_main / btn_exit_pond | home.png / lock.png |
| btn_groundbait / btn_fishing_toggle / btn_catch_leave | leaf.png / rod.png / gift.png |
| chat_toggle / chat_send / btn_debug_police | social.png / check.png / bell.png |

所有动作字符串和 existing NamedElement references 保持；并没有给领取或离席新增第二套状态机。玩家右键、产品右键、窗口尺寸子菜单保留开启/关闭、bot 禁用、选中勾选与事件处理。新增本地“导出 HUD JSON”入口只导出布局/映射，不发 IPC；使用 Windows 系统保存对话框，拒绝覆盖正在读取的源 JSON。

`MainWindow.xaml` 回退聊天缩为 320px 左下纸张卡，回退信息卡减小宽度；Debug 卡靠右、限制高度并随小窗口约束布局，详情采用独立列、不再盖住条目按钮。Debug 仍是按现有业务打开的**模态窗口**，显式打开时遮罩仍会阻止场景交互，不宣称其不遮挡场景；普通 HUD 不新增全窗不透明层。悬停、昵称、提示、聊天气泡使用共享纸张且仍不接收点击。

## 不变项与验证

- 角色源帧仍 **640×512**（不是改成 640×640）；Actor 的座位、帧装载、九层渲染、计时/转换、pop/fade、progress ring 都没有改业务。状态小图标换 Cozy hook/leaf/rod；progress ring PNG 与矢量进度裁切保持。
- 透明窗口、点击穿透、IPC、座位、viewport 预设 **960×560 / 800×400 / 600×300**、ScenePan 与背景淡出相关文件保持原字节；保护 fixture 从只读基线提取，不依赖运行时访问外部仓库。
- 可在 staging 运行：`python3 -m unittest discover -s tests -v`。检查 XAML XML、所有 named/event 引用、原业务方法/Actor 帧与动画方法 SHA、16 个未改业务文件、73 PNG SHA、三态尺寸/边框、嵌套按钮接入、实际 JSON id/组件参数/布局、无旧深色 chrome。
- 此为**静态/源回归**，不等价于 WPF 编译或视觉截图验收。当前 macOS 环境没有 `dotnet`，未运行或声称运行 Windows WPF。

## Windows 编译与业务验收风险

1. 在有相应 Windows .NET/WPF SDK 的环境运行 `dotnet build FishSocialOverlay.csproj`。项目保留 `netcoreapp3.1`；需验证 ResourceDictionary/BAML、动态资源数值和 `mscorlib` 类型解析、新 C#/XAML 自动包含；本任务没有修改构建/部署。
2. 输出目录必须有三态 plate、paper、使用的 icons-light PNG；缺图会退 cream/透明图标，不使用默认 WPF 或 emoji，但图标按钮可能无语义图案，不能漏发资源。
3. Windows 实测 hover/按住/松开/移出、Space 键、disabled、嵌套聊天按钮与原 click pulse；100%/150% DPI 的 paper 接缝和 3 DIP 位移需要截图确认。
4. 检查 IME、TextBox 的 `PART_ContentHost`、placeholder 对齐、聊天展开高度 160、滚动条、发送 ACK 与按钮权限。共享 input padding 已实接入，而非原 placeholder-offset 外边距再次叠加。
5. 对三种裁切预设实测底部 HUD、侧 rail、右键尺寸选择及勾选。同步 Unity Rect 时以最终 JSON 为准；正常 HUD 的预留边缘区是否覆盖某塘实际 Actor/浮标仍需真实快照验收，源静态检查不能保证所有塘内容位置。
6. Debug 主/查看/详情、强制上钩/钓获、出警、产品右键与玩家右键菜单互斥/悬停恢复必须 Windows IPC 冒烟。源回归已保护旧命令和权限回调，但尚无运行时验证。
7. 系统“导出 HUD JSON”文件选择器保持操作系统原生外观，这是 OS 文档选择器，不是遗漏的 WPF 控件。

父代理请整合同目录源文件与已提供 PNG，然后从最终 JSON 反向写 Unity OverlayHud id 的 RectTransform、三态 Image、独立 Icon/Text 和 SpriteBorder；OverlayPondActor 的 Variants 迁移、Unity 组件库及部署由父代理负责。
