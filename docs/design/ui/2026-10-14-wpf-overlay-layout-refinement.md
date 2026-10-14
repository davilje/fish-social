# WPF Overlay 小菜单与聊天输入专项修复

## 基准和交付方式

- 基准仓库：`/Users/davilje/fish-social`；HEAD：`67917a32b5f8de2e9af8c0e1ce169b8dfb9e30f1`。
- 交付目录：`/Users/red/Desktop/fish/ui-layout-refinement/overlay-patch/`，全部采用仓库相对路径，是替换文件而不是可直接运行的完整项目。
- 只修改本补丁目录，不写目标仓库，不提交、不切分支。最终补丁文件均通过 `apply_patch` 生成或修改。
- 不修改 Unity 脚本/预制体、网络/IPC/业务回调、PNG、现有测试或其 protected hashes。
- 保留用户已有的 `steam_api64.dll.meta` 删除、`Packages/packages-lock.json` 修改和未跟踪 Steamworks 包。

## 实际覆盖链与根因

1. `MainWindow` 构造期间调用 `OverlayHudLayout.TryApply`，读取可执行文件目录下的 `OverlayResources/hud/overlay-hud.json`；不是从源码仓库或当前工作目录读取。随后初始视口、加载窗口和切换视口都会调用 `Relayout`。
2. JSON 将控件移出原 XAML 的 StackPanel/Grid，设置实际位置/尺寸，再应用文字/按钮样式。源 JSON 的小按钮 `contentAlign=left` 覆盖主题原有的居中，因此仅改 XAML 对齐不会修好实际 HUD。
3. 原 JSON 在菜单组内重复使用三个槽位：toggle/map 同为 y=0，open-main/shop 同为 y=40，exit/friends 同为 y=80。展开菜单会让可点击按钮和图标重叠；这是布局重叠，不是 PNG 绘制错误。
4. `chat_input` 原高度 32、组件 padding 为上下各 6；`ApplyChatInputChrome` 又将 TextBox 拉伸进此面板。TextBox 模板把 Padding 用作 `PART_ContentHost.Margin`，另外 TextBox 的内容宿主还需要处理输入 Padding。补丁消除把同一内容内边距再次当宿主外边距的路径，并显式连接宿主垂直对齐；不再依赖只写 TextBox.VerticalContentAlignment。
5. 原 TextBox 的 `MaxLines=1` 还会约束内容测量。改为 `AcceptsReturn=False`、`TextWrapping=NoWrap` 保持单行，去掉额外行数高度上限。
6. 原无面板分支先设置 Padding、再调用 `NormalizeForCanvas`，后者把 Padding 清零。现在在两个宿主分支都完成之后设置 Padding，避免备用路径覆盖修复。
7. 原 `ExportCozyJson` 重新读取原始 JSON，只做资源映射，不会保存修好的尺寸/对齐。现在运行时加载与导出使用同一个布局归一化入口，避免导出后恢复旧布局。

### 关于黑色图标的证据边界

当前 HEAD 中右侧按钮的 JSON label 已为空，`CozyTheme.LoadInkIcon` 已使用现有 `cozy/icons-light/*.png` 的 alpha，并将 RGB 着色为 `#75472E`。未发现当前源码中用黑色 Unicode/emoji 替代这些 PNG 的路径，不能把用户实际看到的黑色图标未经验证地归因于字体或声称已在 Windows 重现。

本补丁保留上述 PNG 雕刻轮廓和棕色渲染链；按固定 widget id 清空图标按钮 label，阻止旧导出配置携带的字体符号叠在 PNG 上，并将其内容居中。没有重绘、替换或修改任何图标资源，也没有不必要地修改已正确的 `CozyTheme.cs`。

## 每个交付文件与原因

| 仓库相对路径 | 修改与原因 |
| --- | --- |
| `desktop-overlay/OverlayHudLayout.cs` | 运行时与导出共享布局修正；菜单九个独立槽位横向居中，保留折叠菜单 toggle/open/exit 原先前三个位置；图标按钮空文字且居中；小视口调整行高确保菜单全部在窗内，不缩放图标或字体；旧输入 padding 的上下值限定为 0–4；输入和发送行至少 36 高，占位文本与实际输入区域保持一致；占位 TextBlock 用字体行距计算顶部 padding，使 Canvas 中的文字本身居中；两个宿主分支最后统一应用 Padding。 |
| `desktop-overlay/CozyTheme.xaml` | 默认输入 padding 为 `8,4,8,4`；移除内容宿主重复外边距，Padding 只作为宿主内容内边距；绑定垂直对齐，禁用垂直滚动条并保留必要的 `PART_ContentHost`。 |
| `desktop-overlay/MainWindow.xaml` | 无 JSON 时的输入控件高度/最小高度为 36，padding 为 `8,4`、字号 12；去除 MaxLines 高度上限但保留单行与原 MaxLength/事件；发送按钮同高；占位文本左右内缩；补全六个菜单 PNG 按钮的名称提示。 |
| `desktop-overlay/tests/test_overlay_layout_refinement.py` | 新增十项独立静态回归测试：覆盖调用顺序、PNG 棕色/alpha 路径、独立菜单槽位、三个视口、模板与备用输入路径、输入/占位/发送几何、较大字体、重复导出模型幂等性，以及原视口/动画方法保护哈希。未更改原测试或契约。 |
| `docs/design/ui/2026-10-14-wpf-overlay-layout-refinement.md` | 本说明，记录覆盖链、逐文件修改、验证结果和未完成的 Windows 验收。 |

### 运行时与导出后的布局数值

- 标准菜单的槽位依次为 toggle、打开主界面、离塘、地图、商店、好友、背包、排行、设置。各槽位 40 高，38 宽的按钮在 40 宽的 rail 内 x=1，展开后不重叠。
- 960×560 和 800×400 保持设计行高；600×300 的行高约 30.22，九行位于 y=8 到 y=280，图标仍保持原 16 DIP。
- 当前配置输入行由 32 升至 36；上下 padding 各 4，内容区域 28 高。发送按钮由 28 升至 36，并与输入行垂直居中。
- 占位矩形与输入内区域相同，文字另按字体行距居中。聊天日志底边 110、输入顶边 118，保留 8 的间隔。
- 展开聊天高度由现有计算入口按子控件底边得到 164（含底部 10），仍保持聊天 dock 底边固定、向上展开。
- 原两份 `overlay-hud.json` 均不修改；正常加载会修正其内存 DTO，用户导出的新文件包含修正后的对齐、坐标、尺寸和组件 padding。重新导入/重复导出不会累加位移。

## 本地验证（2026-10-14）

验证使用 `/private/tmp/` 下的完整 `desktop-overlay` 只读来源副本，再覆盖本补丁；测试不会写目标仓库。临时副本运行后删除。

- 修改的两个 XAML：XML 解析通过。
- `OverlayHudLayout.cs`：tree-sitter C# 语法树无错误。此项不等同编译或类型检查。
- 新增独立测试：**10/10 通过**。
- 既有测试：**16/18 通过**，两个失败在修改前就存在，修改后完全相同，无新增失败。
- 既有 protected 文件/业务方法/视口与动画方法哈希检查通过；74 张 PNG 哈希检查通过。
- 目标仓库 `desktop-overlay` 无 diff，最终 HEAD 未变；用户 Steamworks/package 变更仍保留。

### 两项原有失败（未绕过）

1. `test_final_json_has_real_shared_components_and_preserves_widget_ids`：契约期望 JSON SHA256 为 `6fd047dc538578c3a02b7b1c46d96f16adc64720f5b70a17d8cdb5116b0d5d27`，当前 HEAD 文件实际为 `c70a851bed13675a6a7ef224618a369b85b7a1caed8838b59c8b17e554fcc226`。
2. `test_hud_surface_corners_fit_without_compression`：原 JSON 的 `chat_input.h=32`，静态测试按其原 paper slice 得到上下和 40，断言 `32 > 40` 失败。消费者本来会映射 compact paper；本次进一步修正运行时行高，但按要求不重写原 JSON 或修改测试以掩盖失败。

新增测试的几何部分是与 C# 源码公式绑定的 Python 静态模型，不是执行 C#，也不是 WPF 布局/字体/IME 的真实渲染测试。本机无 Windows WPF 构建能力，未执行 WPF build、BAML 验证或 Windows 界面截图验收。

## 应用和 Windows 验收

1. 确认目标仍为本说明的 HEAD。将补丁目录中这五个文件按相对路径合入仓库；不要整目录替换 `desktop-overlay` 或 Unity 工程。
2. 用既有 Windows WPF 构建环境执行 `dotnet build desktop-overlay/FishSocialOverlay.csproj`。项目仍为 `netcoreapp3.1`；本次不更改 SDK、csproj、依赖或部署脚本。
3. 发布整个新 Overlay 输出及其 `OverlayResources` 到实际运行位置，再重新启动 Overlay。只更新源码或 JSON、却继续运行旧 exe/dll/BAML，不会应用模板与运行时修复。
4. 在 960×560、800×400、600×300 检查菜单折叠/展开、每个按钮命中、图标居中和棕色、hover/pressed/disabled/focus；不应出现上下重叠的按钮或字体符号。
5. 展开聊天，输入中文、英文、数字、`gjpqy` 等下伸部字符，检查完整显示、光标、横向长文本、IME 候选、占位文本位置；发送/Enter 行为应与原逻辑相同。
6. 反复折叠/展开聊天、调整视口，检查输入行高度稳定、日志和发送行不重叠、聊天底边固定。
7. 导出到新 JSON，检查菜单不再有重复槽位、图标按钮 label 为空且 `contentAlign=center`、输入 height≥36、padding 上下≤4；重新加载并重复导出应无漂移。验证原当前 HUD 文件仍未被导出操作覆盖。
8. 如果新输出仍呈现黑色图标，核对正在运行的进程路径、exe/dll/BAML 的新构建时间，以及 exe 旁 PNG 资源；当前源码已有 PNG 棕色着色，不能仅靠源码修改推断旧进程已经更新。

合入后可独立执行新增静态测试：

```sh
python3 -B -m unittest discover -s desktop-overlay/tests -p test_overlay_layout_refinement.py -v
```

完整现有测试仍应如实显示上述两个基线失败，不应修改 protected hashes 来把它们伪装成通过。
