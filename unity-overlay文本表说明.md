# Unity / Overlay 本地化文本表

整理日期：2026-10-28（北京时间）。本轮逐来源审查原表 **4,215 条配置、7,144 处来源**，移除 **1,788 条配置、3,365 处来源**；**95 个共用 Key** 只移除非显示或无需翻译的来源。当前保留 **2,427 条配置、3,779 处来源**。此前清理保留原 Key、原文、zhCN、备注、占位符及来源参数；388 条记录按剩余显示用途修正分类。

| 文件 | 用途 |
|---|---|
| [unity-overlay文本表.xlsx](unity-overlay文本表.xlsx) | 三张表：「文本配置」「来源映射」「使用说明」。黄色 zhCN、enUS、备注列可编辑。 |
| [unity-overlay文本表.json](unity-overlay文本表.json) | 配置底表，保存原文、编辑值、逐处来源、参数表达式、扫描指纹和过滤记录。 |

## 保留哪些文本

只保留需要翻译给 Unity / Overlay 玩家看的自然语言内容：正式标题、按钮、标签、输入提示、空状态、教程、通知、操作反馈、鱼名、物品名、品质、成就、线索、描述及必要的日期格式。包括界面构建脚本烘焙进正式 Prefab 的文案，以及运行时可打开的弹窗、页签和提示；不能仅凭父物体初始关闭就删除。

| 类型 | 配置记录数 |
|---|---:|
| 运行时界面文本 | 1,433 |
| 游戏数据文案 | 434 |
| 预制体与场景初始文本 | 312 |
| 关联服务端提示 | 245 |
| HUD配置 | 3 |
| 合计 | **2,427** |

## 已移除哪些内容

- 调试窗口、编辑器菜单和资源管理工具、性能日志、Debug.Log、服务启动诊断、运维告警、后台审批提示。
- 控件对象名和查找路径、文本条件比较值、音效匹配标识、HTTP 方法、协议码、Socket 转义、SQL、富文本标记。
- 纯数字、计数/分辨率格式、排版符号、图标字形、SI 单位和固定产品品牌。含等级、数量或单位的完整自然语言句子仍保留。
- 动画、音效、美术及公式制作说明；没有被读取展示的经验表鱼名注释、权重表中文注释、标签定义和数值表重复显示名。
- OverlayLayouts 美术布局标记、预览昵称和聊天、示意数据、废弃 FeatureModalRoot、默认关闭的产品快捷菜单，以及固定关闭且不会重新启用的历史标签。
- 被运行时无条件覆盖的 HUD 左右按钮配置/导出文字；对应实际赋值源码仍保留。HUD 的三个 dock 按钮配置继续保留。
- 仅移动端或管理后台使用、未进入 Unity / Overlay 显示链的共享标签和旧物品名称。

同文不同用途按**具体来源**处理。例如按钮路径与显示标签可以使用同一个中文字符串，只删除路径来源。实际错误提示即使带有 Steam、HTTP 或资源术语，只要会展示给玩家仍保留。固定 NPC/机器人昵称属于游戏内容；用户自己填写的昵称、签名、聊天、动态和评论不预填翻译。

仍在正式界面显示的整段说明保留原文，包括通知说明对模拟按钮的介绍、设置说明对隐藏保存按钮的介绍、背包对回鱼/分享的介绍、船具占位字段和钓竿设计解释；这些需要另行修改业务文案，不能当作未使用来源删除。

## 清理规则与覆盖范围

原扫描覆盖 921 个文件，范围包括 `fish-social-unity/Assets/`、`fish-social-unity/FishSocialOverlay/`、`desktop-overlay/`，以及关联 `shared/`、`server/src/` 玩家提示。文件指纹及原扫描时间见 JSON 的 `files`、`generatedAt`；本轮审查时间、基线、数量和规则指纹见 `localizationFilter`。这是文案库存及静态显示用途审查，不是当前画面同时可见文字的计数。

抽取器自动依次应用三层规则：

1. `scripts/text-catalog/debug_filter.py`：此前已移除 255 条调试配置、308 处来源。
2. `inactive-sources.json` / `inactive_filter.py`：此前已移除 1,275 条不生效配置、2,187 处来源。
3. `localization-sources.json` / `localization_filter.py`：本轮移除 1,788 条配置、3,365 处来源，并修正保留项分类。

规则按 Key、文件、提取方式、方法/控件位置和表达式指纹匹配。具有 GameObject 标识的 Unity 序列化来源使用该稳定标识及字段路径，忽略重新导出造成的文档序号变化。同一方法中同文不同用途时，追加行内容指纹和参数列号，防止把显示标签随对象名一起删除；不依赖原行号。新增、改写或恢复功能后的来源须重新审查，不能将未命中既有规则等同于已确认需要本地化。

未包含图片内嵌文字 OCR、第三方插件界面，以及未连接读取的服务器数据库/运营配置。运行时动态输入字段另见 JSON `externalText`。抽取器在 5 个文件遇到语法恢复节点，保留 `parseWarnings`；这不是项目编译结论。

## Key 命名规范

当前全部 **2,427 个 Key** 已从哈希名称迁移为可读英文语义名称，配置数、文案、翻译和来源未变化。统一全小写、点号分层、段内使用 `snake_case`；名称表达功能用途，不能直接使用整句译文、数组位置、行号或随机哈希。数据实体沿用稳定 ID。

| 类别 | 格式 / 示例 |
|---|---|
| 玩家界面 | `ui.<模块>.<页面或控件>.<用途>` |
| 游戏显示数据 | `game.shop.rod.bamboo.name` |
| 服务端玩家提示 | `server.shop.groundbait.insufficient_coins` |
| 新增待审查文案 | `unreviewed.<模块>.<来源>.<位置>.text` |

模块统一使用 `common`、`auth`、`fishing`、`map`、`shop`、`catch`、`codex`、`profile`、`social`、`leaderboard`、`settings`、`onboarding`、`home`。用途常用 `title`、`button`、`label`、`hint`、`status`、`error`、`name`、`description`。同义但不同用途的条目使用页面、控件或初始状态区分，保持旧 Key 到新 Key 一对一，不合并配置。

正式名称登记在 `scripts/text-catalog/key-registry.json`，以 `legacyToSemantic` 保存迁移映射。名称不依赖中文/英文翻译内容或源码行号，修改翻译或重新抽取不会重新命名。历史排除规则继续使用内部来源指纹，过滤器会将语义名称还原为对应身份后匹配；无需改变先前审查结果。JSON `keyMigration` 记录注册版本和待审查 Key。

旧哈希 Key 工作簿仍可导入：导入器先解析旧名，再按对应语义 Key 更新中英文。若同一条目同时出现新、旧 Key，整批拒绝导入；未知 Key 也会拒绝。Excel「来源映射」使用新 Key 关联配置；Key 列已加宽并允许换行。

新增来源先分配符合格式的 `unreviewed.*` 临时名称，避免把未经审查的来源当作正式文案。JSON 的内部 `sourceKey` 只对未登记条目保留，用于重新抽取时维持身份；临时名称可能带有消歧序号，正式名称不可用这类序号代替语义。确认需要展示后，向注册表登记 `sourceKey → 正式名称`，再执行：

```sh
python3 scripts/text-catalog/extract.py --normalize-keys-only
node scripts/text-catalog/build.mjs
```

该命令只迁移现有清单的 Key，不重新扫描或新增文案。正式 Key 不应在 Excel 中手动改写；需要调整命名时同时维护迁移映射和相关引用。

## 英文本地化配置

当前 **2,427 条配置已全部填入 `enUS`**，对应 1,805 个唯一中文文本。`supportedLocales` 映射 `zh-CN → zhCN`、`en-US → enUS`，默认语言字段 `locale` 仍为 `zh-CN`。英文翻译以当前 `zhCN` 为准，保留原中文和来源；Key 已按上述规范迁移。

Excel 在中文列旁新增可编辑英文列，原有占位符、来源数、备注列顺延。导入命令支持新版中英文表，也兼容旧版没有英文列的工作簿；旧版导入会保留 JSON 已配置英文。导入前同时校验中英文占位符，发现错误时整批拒绝写入。可先只校验不写文件：

```sh
node scripts/text-catalog/build.mjs --validate-xlsx
```

重新抽取时按 Key 保留 `enUS`，新增文案的英文默认为空，供后续翻译。修改中文后须同时复核对应英文，脚本不会自动调用翻译服务。使用说明页显示英文已配置条数。

| 中文术语 | 英文 |
|---|---|
| 鱼塘 / 钓位 | Pond / Fishing Spot |
| 打窝 / 窝料 | Groundbait |
| 鱼获 / 背包 | Catch / Bag |
| 回鱼 / 自动回鱼 | Release / Auto-Release |
| 出售 / 收杆 | Sell / Reel In |
| 金币 / 新手引导 | Coins / Tutorial |
| 图鉴 / 手账 / 展示柜 | Fish Guide / Journal / Showcase |
| 普通、优良、稀有、史诗 | Common, Uncommon, Rare, Epic |
| 传说、神话、至尊 | Legendary, Mythic, Supreme |

数值、概率、收费与解锁条件不变；`jin` 表示市斤（500g），保留原数值和变量单位，不换算成磅。「回鱼出售」按钮根据实际 `sell_only` 功能译为 Keep to Sell，避免误指向回鱼奖励。真实地名使用英文地名/拼音，虚构 NPC 昵称采用英文语义名称。日期显示模式调整为有效的英文 .NET 格式，富文本标记和原换行保留。

**英文配置目前仍是文案底表，尚未接入 Unity / Overlay 运行时语言切换。**

## 编辑与维护

**当前是配置底表，运行时尚未通过 Key 读取本表。修改 Excel / JSON 不会自动改变游戏界面。** 本轮只清理文本表及抽取规则，没有修改游戏源码、Prefab 或玩法入口。

只修改 Excel「文本配置」的 `zhCN（可编辑）`、`enUS（可编辑）`、`备注（可编辑）`。保留 Key、原始文本和来源映射；完整保留 `{0}`、`{1}`、`{0:N0}` 等占位符的编号、格式及重复次数，可以调整句中顺序。来源映射中的表达式决定每个参数如何绑定。

只清理现有清单，不纳入工程中新增加的文本：

```sh
python3 scripts/text-catalog/extract.py --filter-localization-only
node scripts/text-catalog/build.mjs
```

导入 Excel 编辑后刷新 JSON 和工作簿：

```sh
node scripts/text-catalog/build.mjs --import-xlsx
```

全量重新抽取：

```sh
python3 -m pip install -r scripts/text-catalog/requirements.txt
python3 scripts/text-catalog/extract.py
node scripts/text-catalog/build.mjs
```

未导入的 Excel 编辑会被重新生成覆盖。JSON 中相同 Key 的中英文编辑值和备注在重新抽取时保留，分类修正不生成新 Key。已过滤来源不转入 `retiredEntries`；其他已编辑、随后从源码删除的文字按原机制保留在该归档中，不进入现行工作簿。

`build.mjs` 使用 Codex 随附的 `@oai/artifact-tool`。本机 Node 为 `/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`；其他环境可用 `TEXT_CATALOG_NODE_MODULES` 指定其目录。

## 校验

- 逐来源决策完整覆盖原表 7,144 处；输出与保留集合逐条一致，没有新增主表来源。
- 清理阶段保留项的 Key、原文、编辑值、备注、占位符和参数未改变，来源数及文件统计一致；后续 Key 迁移见下文。
- 验证重复清理幂等、源码行号偏移和 Prefab 文档重排后匹配；同文对象名被移除时显示标签保留。
- 在临时副本全量重抽取，核对已审查排除的原来源不会恢复。工程同期新增/改写的来源留在临时副本，未并入本轮主表。
- 核对 Excel 全部配置与来源行匹配 JSON，保留三张工作表、冻结窗格和表格样式，并渲染检查；后续为语义 Key 加宽关联列。
- 未修改游戏运行逻辑，未执行 Unity 或 Windows WPF 编译/玩法测试。

## 英文配置校验

- 全部 2,427 条英文非空、无残留汉字，占位符编号、格式和次数逐条匹配原文。
- 核对英文与中文的数值及换行；排名中「第一/第二」译为 1st/2nd，其余数值保持一致。
- 核对导出工作簿全部中英文值、Key、来源数和备注与 JSON 一致；原来源映射、三张工作表、列宽及冻结设置保留，新英文列沿用中文列样式。
- 使用临时副本验证旧表兼容、英文编辑导入，以及占位符遗漏时拒绝整批写入；全量重新抽取确认相同 Key 的英文保留。

## Key 迁移校验

- 全部 2,427 个旧 Key 一对一映射为唯一语义名称，格式统一；现行表没有临时 Key。
- 中英文、原文、参数、备注、来源和统计逐条保持不变，来源映射全部关联到新 Key。
- 验证只迁移 Key 及只清理来源均保持幂等；过滤规则在迁移前后得到相同删除集合。
- 临时副本验证全量重抽取保留语义 Key 和全部翻译，新增未审查来源进入临时命名空间。
- 验证旧 Key 工作簿可导入，以及同一配置混用新旧 Key 重复行时拒绝整批写入。
