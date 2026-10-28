# Unity / Overlay 本地化文本表

更新日期：2026-10-29。当前保留 **1,882 条配置、2,631 处来源**，英文全部已配置，已接入 Unity / Overlay 运行时。新增 4 条语言设置文案，并剔除 1 条好友请求身份昵称兜底。本轮另移除被运行时覆盖的装备页初始标题来源，保留同 Key 的装备操作按钮来源。此前清理在 2,427 条配置、3,779 处来源基础上，追加移除 **548 条配置、1,156 处来源**。

Excel 保留「文本配置」「来源映射」「使用说明」三张表；黄色 zhCN、enUS 和备注列可编辑。JSON 保存原文、翻译、参数和逐处来源。运行时通过语义 Key 读取导出的文本资源。设置 → 托盘与其他 → 界面语言，可选择简体中文或 English，立即刷新已打开和隐藏界面的对应文本，并将选择保存至 FishSocial.Language。Unity 通过 IPC 的 language 字段同步 Overlay。

## 当前保留范围

只保留 Unity / Overlay 玩家实际需要本地化的正式标题、按钮、标签、输入提示、空状态、教程、通知、操作反馈、鱼名、物品名、品质、成就、线索和描述。有效 Prefab 初始文字和可动态打开的状态继续保留。

| 类型 | 配置数 |
|---|---:|
| 运行时界面文本 | 1,054 |
| 游戏数据文案 | 388 |
| 预制体与场景初始文本 | 301 |
| 关联服务端提示 | 136 |
| HUD 配置 | 3 |
| 合计 | **1,882** |

## 本轮追加清理

| 内容 | 移除来源数 | 判断依据 |
|---|---:|---|
| 昵称身份数据 | 118 | 包含 105 个机器人预设昵称、1 个自动昵称模板及写入账号/会话/作者资料的默认昵称；与玩家昵称同样保持原值。 |
| Unity 编辑器 | 392 | 全部 /Editor/ 脚本来源，以及 UNITY_EDITOR 分支的宠物美术同步菜单/对话框。界面烘焙脚本的字串也不单独收录。 |
| HUD 导出与资源诊断 | 16 | 包括「导出 Cozy HUD JSON」、导出结果/失败、布局 widgets/画布/父级/尺寸校验和加载诊断。 |
| 无显示用途的成功回调 | 3 | Socket 连接成功消息仅写日志；Lobby 权限通过消息未被成功分支使用。 |
| 固定格式 | 7 | Lv./ID/变量排版及固定 m 单位组合，没有需翻译自然语言。参数鱼名、品质等保留独立配置。 |
| 隐藏或覆盖的初始文字 | 16 | 固定隐藏的 Header 状态/比赛按钮，以及分享弹窗、动态卡片、成就等被运行时无条件重写的初始值。 |
| 未显示重复数据及旧流程 | 604 | 602 处种池 speciesName 超出正式摘要每塘前三个有效项；另有旧鱼饵购买接口及无调用入口的会话验证成功提示共 2 处。 |
| 合计 | **1,156** | 共用 Key 还有实际显示来源时只删对应来源。 |

机器人昵称是有效账号数据，只是不进入本地化表；本轮没有删除账号、昵称字段或其他业务数据，没有修改运行逻辑。此前按「固定 NPC 昵称」保留的机器人池昵称已按本次口径移除。

以下仍保留：昵称标签、校验错误、界面的空昵称显示兜底；会传入 Overlay 错误区的重连/断开/失败提示；完整鱼种主表名称；摘要实际显示的种池鱼名；鱼塘地域、钓竿子类型和观察线索。不能仅凭 Debug、Socket 等词删除实际玩家提示。独立 Overlay 出警入口确会调用相关路由，其玩家反馈仍保留。

通知说明已去掉调试用“模拟连接错误”；窗口设置说明已改为自动保存，与实际操作一致。

## 排除规则

不收录 Debug/Log、控制台输出、追踪、性能诊断、运维、管理工具、Unity 编辑器、资源制作、烘焙/导出工具、协议标识、控件对象名/路径、比较值、SQL、资源路径、富文本标记、纯数值和固定单位格式。实际用户昵称、签名、聊天、动态、评论不预填翻译。

抽取器依次应用：

1. scripts/text-catalog/debug_filter.py：调试文件/分支/控件、Debug 与日志调用。
2. inactive-sources.json / inactive_filter.py：已审查的不生效或非显示来源。
3. localization-sources.json / localization_filter.py：本地化排除清单及全部 Unity /Editor/ 来源。
4. key-registry.json / key_registry.py：稳定语义 Key 与旧 Key 迁移。

规则按内部身份 Key、文件、提取方式、位置及表达式指纹匹配。Unity GameObject 来源忽略文档序号变化。同方法同文不同用途时加行内容指纹和参数列号，避免误删显示来源。改写、启用旧功能或调整数据排序/摘要上限后须复审；未命中规则不代表已经审查通过。

历史清理：调试阶段移除 255 条/308 处；不生效阶段移除 1,275 条/2,187 处；本地化阶段从 4,215 条/7,144 处累计清理至当前规模，共移除 2,336 条/4,521 处。JSON supplementalLocalizationReview 记录清理增量，localizationFilter 记录累计结果。历史清理规模不含本次新增的 4 条语言设置文案及追加剔除的 1 条好友请求身份昵称兜底。

## Key 与翻译

现行 1,882 个 Key 全部为小写英文、点号分层、段内 snake_case，例如：

- ui.fishing.overlay.button.start_fishing
- game.shop.rod.bamboo.name
- server.shop.groundbait.insufficient_coins

模块为 common、auth、fishing、map、shop、catch、codex、profile、social、leaderboard、settings、onboarding、home。名称表达用途，独立于翻译措辞和行号；数据实体使用稳定 ID。

注册表保留旧哈希 Key 到语义 Key 的映射，已删除的条目不会因此恢复。旧工作簿只有在条目集合匹配现行清单时才可导入：已删除的 Key、重复行、缺行均会拒绝，不能直接导入清理前的旧表。新增未审查来源使用 unreviewed.* 及内部 sourceKey；审查后登记正式名称，运行 extract.py --normalize-keys-only。不要在 Excel 手改 Key。

当前 1,882 条 enUS 全部非空。supportedLocales 为 zh-CN → zhCN、en-US → enUS。保留占位符编号、格式和次数，以及原数值、换行和富文本标签。变量数值和单位不换算，jin 为市斤（500g）。修改中文后复核对应英文。

## 使用命令

只校验 Excel，不写文件：

```sh
node scripts/text-catalog/build.mjs --validate-xlsx
```

导入中英文/备注、同步运行时资源并刷新工作簿：

```sh
TEXT_CATALOG_PYTHON=python3 node scripts/text-catalog/build.mjs --import-xlsx
```

直接编辑 JSON 后，导出 Unity 和 Overlay 资源，再按正常流程构建客户端：

```sh
python3 scripts/text-catalog/export-runtime.py
```

导出生成 Assets/Resources/Localization/text-catalog.json、prefab-bindings.json，以及两份 OverlayResources/Localization/text-catalog.json。发布构建会携带这些资源；Excel 文件不会被游戏直接读取。

只清理现有清单，不新增来源：

```sh
python3 scripts/text-catalog/extract.py --filter-localization-only
node scripts/text-catalog/build.mjs
```

全量重新抽取：

```sh
python3 -m pip install -r scripts/text-catalog/requirements.txt
python3 scripts/text-catalog/extract.py
python3 scripts/text-catalog/export-runtime.py
node scripts/text-catalog/build.mjs
```

先导入尚未保存到 JSON 的 Excel 编辑，再重新生成。相同来源身份的 Key、中英文和备注保留；新增英文留空待译。导入同时检查中英文占位符，失败时整批拒绝 JSON 写入。缺少英文列的旧版表格保留 JSON 英文，但条目集合必须匹配当前清单。

build.mjs 使用 Codex 随附的 @oai/artifact-tool；其他环境可用 TEXT_CATALOG_NODE_MODULES 指定依赖。

## 历史静态清理校验

- 按 1,156 处明确来源决策清理，保留条目的 Key、原文、中英文、参数和备注不变。
- 配置、来源数、文件统计和累计排除数一致；过滤和 Key 规范化保持幂等。
- 临时副本重抽取核对已删除来源不会恢复，保留语义 Key 和翻译；同期新增/改写来源未并入主表。
- Excel 全部配置及 2,631 处来源与 JSON 对齐，保留三张表、列宽、冻结设置及样式，并渲染检查。
- 配置总数不代表所有文字同时可见。未包含图片内嵌文字 OCR、第三方插件或未连接读取的数据库/运营配置。
- 这些是早期静态清理的检查；后续运行时接入和修复另见下节。

## 运行时绑定与验证

C# 固定文案使用 Text(key)，模板使用 Format(key, args)，日期格式使用 FormatDate。文本赋值通过 SetText / SetContent / SetHeader / SetToolTip 保留刷新绑定；Concat、Join、LocalizedStringBuilder 和 Transform 保留组合文本的翻译计划。Prefab 按资源路径和完整控件路径绑定，XAML 使用显式 Key。游戏主数据、商店目录、地图和成就显示字段单独绑定，服务错误和系统提示在显示边界翻译。

不按任意字符串做全局替换。玩家昵称、机器人昵称、输入框内容、签名、聊天、动态和评论保持原文；模板中的昵称参数也保持原文。编辑器与日志仍不进入玩家文本表。

已验证：共享目录的模板、参数、组合、日期与昵称保护回归；Unity 整工程编译、实际 headless 中英切换/偏好保存，以及 27 个 Prefab 的 340 条绑定；Overlay 使用真实 Windows WPF 引用完成交叉编译。Windows 实际窗口的布局仍需在 Windows 检查。

## 显示错误修复（2026-10-29）

- 实际 Unity Play 测试捕获设置初始化期间 `ConditionalWeakTable.TryGetValue` 的 `InvalidCastException`。共享翻译计划改为带锁的弱引用身份映射，Unity 游戏数据字段去重也不再使用该表；相同内容但不同 Key 的标签仍各自绑定，玩家文字不继承翻译计划。
- 正式面板、分享弹窗和结算入口在实例化时立即绑定文本；克隆已有控件时复制其翻译计划。个人资料页将固定节点名与显示文字分离，避免语言改变控件查找路径。
- 修复特效选项、装备状态、社交卡片的字符串拼接，以及状态复制、结算日期造成的语言切换失效。
- 装备缓存页切换时也刷新当前分类标题；可见页面标题绑定到页面标题 Key，不再误用 Equip 操作按钮文案。
- 缩短 126 条按钮、页签、下拉选项和窄标签英文；按钮标签按可用空间适配，不缩放正文。特效关闭档位使用 Off，避免误译为 Close。移除设置说明中的无效操作指引；装备左栏分三行显示钓竿、鱼饵、等级，去掉鱼饵页也出现的“选择钓竿”提示。
- Overlay 的首次 Loaded 与旧绑定不再覆盖新的动态内容；额度提示比较支持中英文，悬浮计时保留翻译计划。鱼获公告同时翻译品质和鱼种，昵称保持原文。
- 抽取器支持多参数 Concat，并排除 TabNodeNames 等固定节点标识，避免把控件路径重新收录成翻译文案。

本轮额外检查真实中文冷启动、设置各页签、装备钓竿/鱼饵、打开面板后切语言与控件克隆；通过 Unity 实际渲染截图核对，而非只比对文本表。共享库另有 GC、相同中文对应不同 Key、并发和用户文本保护回归；Overlay 使用生产绑定、IPC DTO 和真实文本资源的回归，并交叉编译。独立 Mono 压力测试未复现旧版嵌入式 Unity 异常，不把其通过结果视为排除实际运行问题的证据。
