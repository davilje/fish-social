# 原版猫咪钓鱼手账 UI：正式替换实施记录

日期：2026-10-21。项目：`fish-social-unity`，Unity `2021.3.29f1c1`。本轮修改工作区代码及资源，未提交 Git、未发布线上服务或 Windows 安装包。

## 当前结果

2026-10-22：用户已认可远端 `be6f2bdf` 的逐项对齐修复，确认沿用该版本。正式入口继续使用下述实现，JournalV2 完整替换推进结束。版本边界及本地回归补充见 [已认可基线记录](2026-10-22-cozy-journal-approved-baseline.md)。本文原有 2026-10-21 测试计数和截图保留为历史证据。

正式主壳、十个原型页面及原有辅助页已接入手账主题和现有业务。正式入口仍为 `DesktopShellUi` / `PanelRouter` / `Resources/Desktop/Prefabs`；没有使用 WebView、整页截图或 `JournalSamples/ReferenceAssets` 替代运行时 UI。共享素材、文字、按钮、Toggle、InputField 和 ScrollRect 均为原生 uGUI。

查看 [17 个正式页面/子状态截图](../../art/cozy-journal/production/index.html) 与 [机器可读验证记录](../../art/cozy-journal/production/evidence.json)。截图使用正式保存的预制体，搭配编辑器样例玩家/库存数据；鱼塘目录、照片和美术取项目实际资源。它证明保存后的布局可渲染，不等同 Steam 真账号联机验收。原版互动参考仍在 [cozy-journal](prototype/cozy-journal/index.html)。

## 实施内容

| 范围 | 正式落点与行为 |
|---|---|
| 主壳 | `DesktopShell.prefab` 新增常驻 WorldMapEntry；登录后统一1672×941、MatchWidthOrHeight(.5)，保留七入口、头像/金币/经验/鱼获、铃铛及设置。旧运行时排列不再覆盖作者布局 |
| 主题 | `CozyJournalTheme`读取 ui-manifest 的 PPU、九宫格、透明外延；纸纹使用Multiply；正式按钮保存default/hover/pressed/selected/disabled资源，卡片选择胶带/描边与固定命中区分离 |
| 地图/选塘 | `PondSelectionState`统一选中塘、档位、费用、准入与忙/失败状态；`PondDetailView`统一真实照片、滚动资料、固定档位与入塘区；免费/可进入筛选、搜索、清除、真实数量、重试。两入口互跳保持档位；同塘切换档位触发原有结算重入 |
| 地图标记 | 原生浮漂/锁、选中爪印、悬停名称、平滑定位；仅选中的可进入浮标持续漂浮，皮肤刷新不会重新生成矩形按钮底 |
| 背包 | 鱼获/真实道具库存、装备操作，保留出售/回塘/分享与真实资格；NEW已读按登录账号隔离，本地浏览状态不改变服务器库存 |
| 商店/装备 | 三类商品及现有装备分类，保留真实价格/解锁/拥有/装备状态；加载重试、购买/装备忙锁；成功/失败后才触发表现 |
| 成就/排行 | 成就共享数据、分类及完成筛选；排行三名领奖台、列表、玩家卡和真实资料跳转；无本地领奖加金币或伪造关注 |
| 个人中心 | 资料/展示柜/图鉴/相册/成就五子页保留；编辑昵称/简介/头像与展示柜沿用真实保存；他人资料清理旧账号数据并保持只读 |
| 动态 | 公开/好友/我的三个分页流；评论移到右栏，切换及异步回包隔离，Socket原位更新保留评论草稿；原有点赞/评论/删评论/作者跳转保留 |
| 发布 | 新增 `JournalComposeModal.prefab`，真实库存选鱼、160字可选心情、公开/仅好友、加载重试、提交忙锁、失败保留草稿、成功刷新我的动态、Esc/Tab焦点；基础布局已持久化，运行时只生成鱼获列表 |
| 设置/辅助页 | 窗口/通知/服务器/托盘四组及四档分辨率保留；登录、聊天、好友行、结算、上下文菜单和旧兼容页迁入共同主题；OverlayHud继承状态同步，未重做WPF钓鱼场景布局 |
| 动效 | 可取消/重入安全的切页、导航浮漂、数字滚动、稳定倾角/首次可见贴入、成功印章/失败反馈、普通/新鱼收获队列与跳过、皮肤姿势；同屏一个持续循环，窗口隐藏暂停，减少动效立即归位 |

## 服务端增量

- `GET /api/posts/mine/:playerId`：必须本人认证；按稳定时间/ID顺序分页，返回本人所有可见范围的动态，不通过过滤一页公开流凑结果。
- 原 `POST /api/posts` 增加可选 `text`（最多160字），校验文字类型、可见范围及库存归属；保留旧 `ShareFish` 调用兼容。分享不消耗鱼获。
- 仍要求真实鱼获；本轮不增加无鱼纯文字动态、单向关注、成就领奖这些独立产品机制。已有好友、私聊、解锁展示语义保留。
- Unity新客户端的本人分页及附言需要配套部署这份服务端改动。此轮没有操作线上环境。

## 预制体与重建

作者入口：`Fish Social → Cozy Redesign → Apply`。新增发布弹窗也可独立执行 `Fish Social → Cozy Journal → Bake Publish Modal`。源文件为 `CozyRedesignAuthoring.cs`、`.Journal.cs`、`CozyJournalComposerAuthoring.cs`。

旧Variant暴露了被下游引用的stripped object ID。普通SaveAsPrefabAsset会更换或移除部分别名，盲目追加又会产生同对象双别名。本轮 `SaveJournalPrefab`按source对象和实例保留唯一canonical ID，一次性重写本地引用，避免好友行子节点丢失和嵌套样式失效。Repair也使用该保存路径；回归测试实际连续保存、重载并检查节点/资源，而非只查文本。

`CozyUiBuildGuard`保留严格结构检查，并新增世界入口、统一缩放、共享鱼塘详情、Multiply和journal资源状态/PPU/insets检查。原有Variant关系保留；新增发布弹窗为独立原生prefab。

生成可查看证据：先在Unity执行 `Fish Social → Cozy Redesign → Render Saved Prefab Previews`，再运行 `node scripts/art/build-cozy-journal-production-preview.cjs`（需要sharp）。输出 `docs/art/cozy-journal/production/`；原始PNG保留于Unity `Library/CozyRedesignPreviews/`。

## 验证结果

| 检查 | 结果 |
|---|---|
| Unity脚本实际导入/编译 | 通过 |
| Unity EditMode全套 | **347通过，0失败，0跳过** |
| 正式预制体构建守卫/布局验证 | **PASS** |
| 原生页面渲染 | **17个页面/子状态完成**；检查地图、选塘、商店、设置、成就、排行榜、发布弹窗等；修复发现的溢出、选中标记、纸面边距问题 |
| 本人动态/发布HTTP + SQLite | **8/8通过**；认证、跨账号、可见范围、55条分页、非法输入、旧分享兼容、不消耗库存 |
| shared TypeScript构建 | 通过 |
| server全量TypeScript | 未通过，存在原有Express参数类型、Database命名空间、埋点枚举和shop导入等类型错误；本轮新增接口无新增类型诊断，HTTP集成已通过 |
| 本地预览浏览器检查 | Chrome/Playwright切换17状态、图片解码、键盘切页与原尺寸按钮通过，0页面错误 |
| Git空白检查 | 通过 |

测试包含快速切页与重入、隐藏/减动效、队列上限/去重、跨账号已读、鱼塘准入/档位/异步取消、长评论与草稿保持、发布预制体保存重载、透明外延幂等、真实卡片选中和多次保存后嵌套样式。

## 仍需平台与视觉验收

- 本机为macOS且Unity仅安装MacStandaloneSupport；没有声称完成Windows构建、Steam账号登录/联机、原生Overlay与托盘全过程、Windows DPI/中文IME/四档窗口实机验证。
- 2026-10-22 用户已认可远端修复版的实际效果，范围为该版本的 UI 表现。此认可不等同自动化逐像素等价结论，40项补充UI和新增角色姿势的导入记录也不代表每项均被单独验收。
- 原型中的部分额外装饰表现仍保留备用资源，例如购买商品弧线飞入背包、出售金币飞入、输入框独立错误皮肤、复杂空态插画及音效。本轮已提供真实结果印章/失败反馈与必要文字状态，没有用本地演示成功替代业务。
- A01–A30实际序列化引用和动态加载证据已写入 `coverage.json`，标为integrated、partial或available-supplemental；未使用的备用素材没有虚报为全部接入。

`STEAM-DESKTOP-21`记录为已实现，设计/实现日期为2026-10-21，UI 效果认可日期为2026-10-22。Windows/Steam 平台验收保留待办，不将整个功能标为全平台已验收。
