# Unity / Overlay 文本表

整理日期：2026-10-28（北京时间）。项目根目录：`fish-social/`。

已扫描 **921 个文件**，整理为 **5,745 条配置记录**，保留 **9,639 处来源映射**。记录按模块、类型、提取方式和原文去重，并非玩家当前可见文字的数量。扫描时间和各文件指纹见 JSON 的 `generatedAt`、`files`。

| 文件 | 用途 |
|---|---|
| [unity-overlay文本表.xlsx](unity-overlay文本表.xlsx) | 供策划与文案编辑。包含「文本配置」「来源映射」「使用说明」三个工作表，配置表支持筛选，黄色列可编辑。 |
| [unity-overlay文本表.json](unity-overlay文本表.json) | 配置底表，保存 Key、原文、zhCN、占位符、备注、逐处来源、参数表达式和扫描文件指纹。 |

## 覆盖范围

- `fish-social-unity/Assets/`：运行时 C#、界面构建脚本、场景、预制体及文字覆盖项、ScriptableObject、JSON 游戏数据和资源配置。
- `fish-social-unity/FishSocialOverlay/` 与 `desktop-overlay/`：XAML、C#、HUD 和资源配置；重复资源保留全部来源。
- `shared/`：客户端关联的名称、品质、装备、鱼塘、成就、标签、线索及限制提示。
- `server/src/`：补充关联服务端源码中可扫描的提示，单独分类。运营后台、指标、模拟、迁移和测试文件不纳入。

| 类型 | 配置记录数 |
|---|---:|
| 运行时界面文本 | 1,806 |
| 游戏数据文案 | 527 |
| HUD配置 | 6 |
| 预制体与场景初始文本 | 972 |
| 关联服务端提示 | 277 |
| HUD预览占位 | 3 |
| 界面预览样例 | 707 |
| 玩法调试 | 136 |
| 编辑器与界面构建 | 981 |
| 资源配置与说明 | 257 |
| 日志与诊断 | 73 |
| 合计 | **5,745** |

不纳入空字符串、代码注释、文档正文、第三方包、测试、构建产物、缓存、资源路径、协议标识、SQL、正则及内部状态签名。图片内嵌文字未做 OCR，外部数据库和运营配置未连接读取。昵称、签名、聊天、动态、评论等用户内容不预填；主进程与服务端动态字段另见 JSON 的 `externalText`。

## 配置与编辑

**当前是配置底表，运行时尚未通过 Key 读取本表。修改 Excel 或 JSON 不会自动更改游戏界面。** 现有 C#、XAML、预制体及业务数据保持原状；接入时可依据来源映射逐处迁移。

1. 在 Excel 的「文本配置」中按模块、类型筛选，只修改 `zhCN（可编辑）` 和 `备注（可编辑）`。
2. 保留 Key、原始文本及来源映射。Key 由模块、类型、提取方式和原文的 SHA-256 前 16 位生成，不依赖行号。
3. 完整保留 `{0}`、`{1}`、`{0:N0}` 等参数的编号、格式与出现次数；允许调整它们在句中的位置。
4. 保存 Excel 后执行导入命令，将编辑值写回 JSON。JSON 是重新生成工作簿时的输入，**未导入的 Excel 编辑会被重新生成覆盖**。

「来源映射」一行对应一次出现，包含文件、行列、方法或控件/JSON Pointer、原表达式和参数。拼接与插值统一为编号模板，条件分支文字另行收录。不同来源同一编号参数的表达式可能不同，应按所在来源绑定。

预制体、场景和样例文本可能被运行时代码覆盖，不据此判定现行界面。已知离席按钮的 HUD JSON 为「离开座位」，XAML/运行时代码为「离席」，分别保留；HUD 的「哈哈」「txt_groundbait」「txt_error」已标为预览占位。

## 维护命令

在仓库根目录执行。Python 依赖见 [requirements.txt](scripts/text-catalog/requirements.txt)，建议安装到独立虚拟环境。

```sh
python3 -m pip install -r scripts/text-catalog/requirements.txt
python3 scripts/text-catalog/extract.py
node scripts/text-catalog/build.mjs
```

导入已编辑的工作簿并刷新 JSON、Excel：

```sh
node scripts/text-catalog/build.mjs --import-xlsx
```

`build.mjs` 使用 Codex 随附的 `@oai/artifact-tool`。本机 Node 路径为 `/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`；其他环境可用 `TEXT_CATALOG_NODE_MODULES` 指定相应的 `node_modules` 目录。

重新抽取时，相同 Key 的 JSON `zhCN` 和备注保留。已经修改、随后从源码删除的文本保留在 `retiredEntries`；不会自动加入现行工作簿。每次扫描的文件列表、SHA-256 指纹和分类计数保存在 JSON 中。

## 校验情况

- 核对 Key 唯一性、配置与来源数量、文件及行号有效性、占位符一致性。
- 核对 Overlay 主窗口 58 个非空显示属性及主题窗口标题，共 59 处显示属性，均保留对应来源。
- 验证拼接、数字加法、条件分支、插值格式与对齐等代表性模板。
- 保存后逐条核对 Excel 的 Key、原文、配置值和来源数量；以临时副本验证有效编辑导入，以及占位符缺失时拒绝写入 JSON。
- 渲染检查三个工作表的表头、正文、编辑列及说明；未改动游戏运行逻辑，因此未执行 Unity 编译或玩法测试。

抽取器在以下 5 个文件中遇到语法恢复节点，已继续遍历。核验这些节点仅包含赋值符号或 TypeScript 类型导入/泛型，没有未解析的中文文本；这不是对项目编译结果的判断。

| 文件 | 行号 | 恢复节点 |
|---|---|---|
| `fish-social-unity/Assets/Scripts/Desktop/Editor/CozyRedesignAuthoring.cs` | 71、74、79 | `=` |
| `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopProfileHubPanel.cs` | 1257 | `=` |
| `server/src/socialRoutes.ts` | 443 | 类型导入 |
| `server/src/socketLifecycle.ts` | 23 | Socket 类型导入 |
| `server/src/socketPondHandlers.ts` | 57、60 | 泛型与闭合符号 |
