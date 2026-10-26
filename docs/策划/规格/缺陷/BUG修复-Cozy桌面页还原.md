# Bug：Cozy 桌面页还原（布局、质感收尾、编辑器测试）

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Unity 桌面 Cozy 页面对照设计稿的缺陷登记 |
| 编号 | **BUG-27** … **BUG-47**（分项状态以计划表为准） |
| 类型 | **Bug修复** |
| 负责人 | Unity 桌面 UI |
| 状态 | **分项**（计划表：已验收 / 已实现；BUG-46 已确认，见专项 spec） |
| 目标版本 | hotfix |
| 优先级 | P0（布局）/ P1（测试与剩余对齐） |
| 设计时间 | **2026-10-16** |
| 完成时间 | **2026-10-16**（BUG-27～45、BUG-47）；BUG-46 方案 2026-10-22 已确认，尚未实现 |
| 关联全景 | `docs/归档/2026-10-26/版本/策划/产品/v0.1.0-功能全景.md` §5.6 背包、§5.8 个人中心；桌面实现在 `fish-social-unity` |
| 验收口径 | 用户于 2026-10-16 对「继续修复其余问题」之前的交付回复「已验收通过」 |

计划表状态不从本文件的「状态」格回写。`ENSURE_PLAN_ROWS` 里每一行自己的状态才算数。

---

## 1. 现象与复现

对照设计 PNG 与 Unity 2021.3.29f1c1 里保存的 Prefab。编辑器导出层级到 `Library/CozyRedesign.inspect.txt`，再用 `CozyRedesignPreview` 把页面渲染成 PNG。本批不经过 Overlay、Named Pipe、Socket 或服务端，那些层记为不适用。

| 环境 | 记录 |
|------|------|
| 客户端 | Unity 2021.3.29f1c1，工程 `fish-social-unity`，UGUI 旧版 `Text` |
| 平台 | Windows 桌面主窗口页面，不是 Overlay |
| 复现 | 布局类在当次 Prefab 上稳定复现；EditMode 失败在 HEAD 上复跑仍是 12 个 |
| 服务端进程 | 不适用。成就进度条缺字段是 DTO 形状问题，不是当次请求超时 |

### 1.1 用户已验收（BUG-27～35）

用户在字体与九宫格接入之后回复「已验收通过」，覆盖此前已修好的 6 个布局问题和两处 Apply 副作用，以及圆体首行把字截掉的问题。

| 编号 | 操作 | 预期 | 实际 |
|------|------|------|------|
| BUG-27 | 打开个人中心展示柜、图鉴、相册 | 格子和照片可见 | 新建纸张是最后一个子节点，画在格子和照片上面 |
| BUG-28 | 看个人中心窗口右缘 | 内容停在窗口内 | 旧节点 `Reflow_DetailPaper` 仍激活，米色块伸出窗口 |
| BUG-29 | 看图鉴详情 | 标题和「已解锁 6/50」上下分开 | `Detail` 铺满整块，标题叠在正文上 |
| BUG-30 | 看相册照片 | 照片落在槽位内 | `Photo` 写死 613×206，槽位约 508 宽，溢出成白条 |
| BUG-31 | 打开设置 | 与其他页一样的 1370×650 底板和两栏 | 根节点 ScrollRect 背景被清成透明；设置页不在统一尺寸列表；旧 `Reflow_InfoRail` 还开着 |
| BUG-32 | 打开商店或装备详情 | 长说明可滚动，不压购买按钮 | 说明固定 314×230，运行时拼成长文，压住 y=432 的按钮 |
| BUG-33 | 对同一 Prefab 再跑一遍 Apply | 已切好的图集子图 ID 不变 | `ImportAtlas()` 每次把子图 ID 清掉 |
| BUG-34 | 打开背包并滚动鱼获 | 列表跟着滚 | Viewport 下有两个 `Slots`，ScrollRect 指到空的那个 |
| BUG-35 | 换上 Resource Han Rounded 后看标题 | 「小橘」「我的鱼获」和尺寸行可见 | 字体首行比雅黑高约 30%，单行被整行截掉 |

### 1.2 已修好、尚未再次验收（BUG-36～45）

「已验收通过，继续修复其余问题」之后完成。预览 PNG 与 EditMode 219/219 已核对。还没有 Development Build 里的真实账号验收，所以计划表是 **已实现**，不是已验收。

| 编号 | 操作 | 预期 | 实际 |
|------|------|------|------|
| BUG-36 | 看出售、购买等主按钮 | 左侧是金币图标 | `icons-light/coin.png` 等浅色图标破损，看起来是白点 |
| BUG-37 | 看背包左侧「鱼获」 | 选中为桃色底板 | 选中态要求 `interactable`，唯一分类被设成不可点，于是变灰 |
| BUG-38 | 打开鱼塘选择，或跑 Apply | 卡片纵向排列，无警告 | `Cards` 已有 `VerticalLayoutGroup`，再加 `GridLayoutGroup` 失败并提前 return；旧 `Reflow_InfoRail` 从新侧栏右侧露出 |
| BUG-39 | 看展示柜、图鉴卡片 | 只显示裁切后的鱼图 | `ShowcaseSlot` 自带的小鱼图标叠在新插画上 |
| BUG-40 | 连续 Apply 两次 | 第二次结果与第一次一致，校验通过 | 选中按钮把悬停底板存进 Prefab，`DesignCategoryFish` 校验失败；成就徽章 sibling 调整把底板挪到文字上面 |
| BUG-41 | 跑九宫格 EditMode | 网格按九宫格切分 | `Image.OnPopulateMesh` 有两个重载，按名字反射抛 `AmbiguousMatchException`（9 例） |
| BUG-42 | 解析缺少 `coins` 的 `{}` | 失败且结果为 null | `TryParseResponse` 返回 false，但仍带出半解析对象 |
| BUG-43 | 给 Toggle 的 `Bg` 走旧表面换皮 | 套上 `panels/paper.png` 并保持 Sliced | `ApplyLegacySurface` 允许这条路径，随后调用的 `ApplyPaper` 因父级是 Selectable 直接返回 |
| BUG-44 | 目录测试用一个不存在的 Overlay 名 | 只对 `OverlayPondLayout` 走自定义生成 | 测试仍用 `OverlayFixture`；`bd97d0c` 已把自定义生成收窄到 `OverlayPondLayout` |
| BUG-45 | 对照设计稿看页签、卡片和详情 | 选中有描边；详情是图标、标签、数值；卡片有鱼图 | 选中态、详情行、图集裁切和页面字段原先都没接上 |

### 1.3 未修复（BUG-46、BUG-47）

| 编号 | 操作 | 预期 | 实际 | 状态 |
|------|------|------|------|------|
| BUG-46 | 打开成就中心 | 未完成项能看到相对目标的进度 | 只有「已完成 / 未完成」徽章。`AchievementViewDto` 有 `conditionValue`、`unlocked`，没有当前进度 | **已确认**，方案见 [BUG-46成就列表当前进度.md](BUG-46成就列表当前进度.md) |
| BUG-47 | 在展示柜点一条鱼 | 详情区用图标、标签、数值分行 | 已改为鱼名加分行；空选中仍是提示句 | **已实现** |

游戏内 Development Build 核对不是单独缺陷。BUG-36～45 的玩家侧验收还没做。

---

## 2. 时间戳证据

本批是编辑器 Prefab、预览 PNG 和 EditMode，没有钓鱼命令。

| 节点 | 本批 |
|------|------|
| Overlay `command_sent` | 不适用 |
| Unity `overlay_command_received` | 不适用 |
| Socket `event_sent` / 服务端 / ACK | 不适用 |
| Unity 主线程改 Prefab | 2026-10-16，请求文件 `Temp/CozyRedesign.request` 的 apply / preview / test 返回 `OK` |
| 预览 | `Library/CozyRedesignPreviews/*.png`；连续两次 apply 后页面一致（成就徽章文字可见） |
| EditMode | 修复前 207/219（12 个失败在 HEAD 上同样失败）；修复后 `passed=219 failed=0 skipped=0` |

没有对应时间戳的层不归因。不把本批说成服务器慢或网络慢。

---

## 3. 根因分层

| 层 | 判断 |
|----|------|
| 服务端 / 数据库 | BUG-46 已核实：`progressValue` 只用于解锁判断，`buildAchievementViews` 不下发当前值。方案改为下发 `currentValue`，见专项 spec |
| Socket / ACK | 不适用 |
| Unity 主线程与 Prefab | 主因。绝对坐标、旧 `Reflow_*` 未关、兄弟顺序决定谁盖住谁、Apply 不是幂等的 |
| Named Pipe | 不适用 |
| Overlay 渲染 | 不适用。主窗口 UGUI 预览已核对 |

---

## 4. 修复与验收

### 4.1 已验收（BUG-27～35）

- 纸张背景放到格子下面；隐藏 `Reflow_DetailPaper`、设置页 `Reflow_InfoRail`。
- 图鉴标题与正文拆开；相册照片跟随槽位宽度。
- 设置页进入统一 1370×650，滚动视口指向 `SettingsViewport`。
- 商店、装备说明放进按钮上方的滚动区。
- 已切好的图集不再重写子图 ID；背包 ScrollRect 指向真正的 `Slots`。
- 字体改名为 Fish Cozy Rounded，行高调到接近雅黑（首行/两行约 23/51px）。

- [x] 预览中展示柜、图鉴、相册、设置不再空格子、溢出或标题重叠
- [x] 用户 2026-10-16 回复「已验收通过」

### 4.2 已实现、待游戏内验收（BUG-36～45）

- 浅色金币、社交图标按亮度挖空，主按钮白点消失。
- 选中页签在不可点击时仍用选中底板，不降成灰色。
- 鱼塘卡片保持纵向列表；去掉加网格和提前 return；隐藏鱼塘页 `Reflow_InfoRail`。
- 展示柜卡片隐藏旧 `Icon` / `CozyIcon`。
- `ButtonAt` 写样式前清掉选中态；徽章和资料纸只在底板排到文字后面时才调整层级。
- 测试指定 `OnPopulateMesh(VertexHelper)`；缺字段时把解析结果置空；Toggle 背景走不拦截 Selectable 的私有铺纸；目录测试改为 `OverlayPondLayout`。
- 页签和卡片写入选中底板；背包、商店、资料用图标 + 标签 + 数值行；鱼图按图集内容裁切。

- [x] EditMode 219/219
- [x] 连续两次 apply，预览一致，校验通过
- [ ] Development Build、真实账号：背包、个人中心、商店、装备、成就、鱼塘选择

### 4.3 未修复

- [ ] BUG-46 成就当前进度。已拆到 [BUG-46成就列表当前进度.md](BUG-46成就列表当前进度.md)，本文件不再作为实现依据。需要改 `server/`、`shared/` 下发 `currentValue`
- [x] BUG-47 展示柜 `ShowFishDetail` 改为与背包详情相同的分行。不新做整页，不改相册文案。预览 `PanelProfileHub-展示柜` 可见品质、体长、重量、捕获时间、来源塘、食性、脱钩率、推荐鱼饵

### 4.4 回滚

Prefab 与脚本都在 `fish-social-unity/`。回滚对应提交即可。不要单独回滚 `.meta` 而留下新的 `plate-selected.png`。BUG-42 的失败路径会把 `parsed` 置空，现有调用方已判空。

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-10-16 | 登记 BUG-27～47。27～35 已验收；36～45 已实现；46、47 已确认未做 |
| 2026-10-16 | BUG-47 展示柜详情改为分行 → **已实现**。BUG-46 仍未做 |
| 2026-10-21 | BUG-46 从 Bug 修复改为功能，状态待开发 |
| 2026-10-22 | BUG-46 方案定稿为 **已确认**，实现以专项 spec 为准 |
