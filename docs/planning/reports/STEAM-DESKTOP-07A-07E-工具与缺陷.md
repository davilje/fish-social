# STEAM-DESKTOP-07A～07E：开发工具与缺陷说明

日期：2026-08-17  
范围：`STEAM-DESKTOP-07A`～`07E`（不含未验收的 `07F`；原生 Overlay 进程编号为 `07G`，但 07A～07E 的鱼塘画面实际画在 Overlay 上，相关工具一并列出）  
状态：07A～07E 已实现；父需求 `STEAM-DESKTOP-07` 仍为已确认（07F 未完成）

---

## 0. 怎么读

- **工具**：开发或打包时要用的菜单、协议、资源约定；不是玩法功能本身。
- **缺陷**：07A～07E 开发过程中发现并已修掉的问题。未修或留给 07F 的，单独标明。
- 产品硬规则（全程有效）：只跑一个 Unity 进程；Overlay 不连 Socket、不跑 uGUI；IPC 不传图；打开主窗口 / 切页 / 回托盘不得 `leave_pond`。

---

## 1. 横切工具（07A～07E 共用）

这些工具不是某一个子需求独有，后面各阶段都在用。

### 1.1 Unity 菜单（`DesktopBuildMenu.cs`）

| 菜单 | 作用 |
|------|------|
| **Fish Social → Build Windows Development Player** | 打 Windows 64 位 Development Player（含调试），同时发布原生 Overlay，并复制 `steam_appid.txt` 到输出目录 |
| **Fish Social → Build Windows Release + Native Overlay** | 同上，但打 Release（不含 Development / Allow Debugging） |
| **Fish Social → Open Desktop Main Scene** | 打开 `Assets/Scenes/DesktopMain.unity` |

输出约定：

- Unity 主程序：`fish-social-unity/Builds/Windows64/FishSocialDesktop.exe`
- Overlay：同目录下 `FishSocialOverlay/FishSocialOverlay.exe`
- 构建失败时弹出对话框并写 Console，**不会**把 Unity 编辑器退出（曾把构建失败伪装成编辑器闪退，已修）
- 若 `.exe` 正在运行，会提示先退出再构建

### 1.2 Prefab 检查菜单（`DesktopPrefabBaker.cs`，类名 `DesktopPrefabValidator`）

| 菜单 | 作用 |
|------|------|
| **Fish Social → Validate Desktop Prefabs** | 检查 4 个功能页 Prefab 是否存在，以及是否挂了对应 View 脚本 |

Prefab 路径（唯一功能页来源）：

- `Assets/Resources/Desktop/Prefabs/PanelSocial.prefab`
- `Assets/Resources/Desktop/Prefabs/PanelCatch.prefab`
- `Assets/Resources/Desktop/Prefabs/PanelGallery.prefab`
- `Assets/Resources/Desktop/Prefabs/PanelSettings.prefab`

改 UI 的正确流程：在 Unity 里打开 Prefab → 改 → Ctrl+S → 重新打包。不要手写 YAML Prefab，也不要让运行时重建整页骨架。

已去掉的危险菜单：Bake Prefab、自动改写 Settings 布局。那些会把运行时动态 UI 存进 Prefab，按钮看起来在、实际点了没反应。

### 1.3 Named Pipe IPC

Unity 主进程 ↔ `FishSocialOverlay.exe`，JSON Lines，`version=1`，状态带递增 `sequence`。

Unity → Overlay（状态，不传图）：

- 登录 / 连接 / 鱼塘名 / `fishingPhase` / `petVisualState` / 自己钓位坐标
- 同塘 `spots`、`users`（含机器人）
- `mainWindowRaised`：主窗口抬起时 Overlay 取消 `Topmost`

Overlay → Unity（命令）：

- `open_main`、`hide_overlay`、`quit_overlay`、`request_snapshot`
- `menu_pond` / `menu_friends` / `menu_catch` / `menu_gallery` / `menu_settings`
- `hide_to_tray`、`quit_app`

### 1.4 Overlay 本地资源目录

放在 Overlay exe 旁的 `OverlayResources/`：

| 文件 | 用途 |
|------|------|
| `pond.png` | 鱼塘背景 |
| `cat.png` | 默认猫 |
| `cat-<state>-N.png` | 按 `petVisualState` 本地播序列帧（如 `cat-idle-0.png`） |

Unity 只推状态字符串，不传贴图。没有正式图时用占位水面/岸线和矢量猫。

主窗口可替换路径：`Resources/Pet/`。状态机 `PetStateController` 与渲染器解耦：主窗口 `SpriteFramePetRenderer`，Overlay 用同等接口的 WPF 本地播帧。

---

## 2. 按需求分别说明

### 07A 桌面宠物主视图与鱼塘入口

完成时间：2026-08-14

#### 开发的工具 / 能力

| 项 | 说明 |
|----|------|
| `PetStateController` | 宠物状态机：`idle` / `fishing` / `hooked` / `catching` / `dragging` / `offline` |
| `SpriteFramePetRenderer` | 主窗口序列帧渲染；没有正式美术时同一张占位图 + 状态文字区分 |
| 主窗口鱼塘入口 | 登录后进塘 / 恢复塘，状态栏显示当前钓鱼阶段 |
| Overlay 启动链路 | 进塘后由 Unity 拉起 `FishSocialOverlay.exe`（进程本身属 07G，07A 负责入口与状态推送） |

#### 修复的缺陷

| 现象 | 原因 | 处理 |
|------|------|------|
| 想做透明桌宠，第二 Unity Player + UniWindowController | 全屏、Skybox、主窗口阻塞、窗口样式损坏、CPU 高 | **放弃**第二 Unity Player；07A～07F 只跑一个 Unity；透明 Overlay 改原生 WPF（07G） |
| 动画方案未定，容易绑死 Spine | Spine 不是首版前置 | 收口为「序列帧 + 状态机」；渲染器可替换，不改网络和鱼塘权威 |
| 打开主界面等于离塘 | 误把切窗当成离开会话 | 打开主窗口不 `leave_pond`、不重建 Socket |

---

### 07B 2D 鱼塘环境与自己的猫咪

完成时间：2026-08-14

#### 开发的工具 / 能力

| 项 | 说明 |
|----|------|
| `OverlayPondStateBuilder` | 把服务端 `pond_snapshot` 的塘、钓位、自己的位置拷进 Overlay DTO |
| Overlay `MapToScene` | 把服务端世界坐标自动缩放到 Overlay `960×480`（后续 ART-02 有布局 JSON 后可停用自动缩放） |
| Overlay 自己的猫 | `128×128`，按 `petVisualState` 本地切帧 |

#### 修复的缺陷

| 现象 | 原因 | 处理 |
|------|------|------|
| 主窗口再画一套完整鱼塘 | 双画面、双权威感 | 鱼塘场景和自己的猫**优先画在 Overlay**；主窗口只留状态栏和恢复入口 |
| Overlay 自己推算钓位 | Overlay 不应有第二套状态机 | Overlay 只渲染 Unity 推送字段；旧 `sequence` 丢弃 |

---

### 07C 同塘玩家宠物与状态同步

完成时间：2026-08-15

#### 开发的工具 / 能力

| 项 | 说明 |
|----|------|
| Overlay `users[]` DTO | 扩展现有 IPC，不新开 Socket 协议 |
| 按 `playerId` 复用对象 | 进出场不反复创建；快照全量覆盖后删掉不在列表里的猫 |
| 岸边等待位 `WaitingLane` | 同塘玩家还没有钓位时，在岸边排队，不叠在同一点 |
| `fishingPhase` → `petVisualState` | Unity 映射后只推状态名；Overlay 本地播对应序列帧 |

#### 修复的缺陷

| 现象 | 原因 | 处理 |
|------|------|------|
| 离塘玩家还留在 Overlay | 只加不删，或增量更新漏删 | 快照全量 `keep` 集合，不在名单里的移除 |
| 想给 Overlay 单独连 Socket | 会重复 `join_pond`、双会话 | Overlay 不连网；同步沿用现有 `pond_snapshot` / `pond_user_joined/left/updated` |
| IPC 传序列帧图片 | 带宽和协议膨胀 | 禁止传图；只传 `petVisualState` |
| 打开主界面同塘人消失或自己离塘 | 切窗误发 `leave_pond` | 打开主界面不离塘，Overlay 继续播帧 |

**留给 07F：** 若某次 IPC 省略 `users` 字段（反序列化为 null），当前 Overlay 可能不会清掉旧猫。07C 已按「有列表则全量覆盖」实现，空字段边界仍待主流程验收。

---

### 07D 桌面宠物右键菜单

完成时间：2026-08-16

#### 开发的工具 / 能力

| 项 | 说明 |
|----|------|
| Overlay 右键菜单 | 当前鱼塘、好友与聊天、鱼获/背包、图鉴、设置、隐藏到托盘、退出 |
| 主窗口产品区域右键 | 同一套入口，菜单只分发事件，不改服务端状态 |
| IPC 菜单命令 | `menu_pond` / `menu_friends` / `menu_catch` / `menu_gallery` / `menu_settings` / `hide_to_tray` / `quit_app` |

#### 修复的缺陷

| 现象 | 原因 | 处理 |
|------|------|------|
| 点 Windows 桌面也被游戏菜单拦住 | 全局钩子 / 系统桌面拦截 | **不拦截**系统桌面右键；只在产品窗口 / Overlay 鱼塘区域出菜单 |
| 点菜单把人踢出鱼塘 | 菜单当导航离塘 | 菜单操作不 `leave_pond` |
| Overlay 菜单想在 Overlay 里弹 uGUI | Overlay 禁止业务 UI | 菜单经 IPC 交给 Unity 主窗口切页 |

---

### 07E 主窗口功能页签（好友/聊天、背包、图鉴、设置）

完成时间：2026-08-17（用户确认 Prefab 方案完全正确）

#### 开发的工具 / 能力

| 项 | 说明 |
|----|------|
| `DesktopFeaturePanelFactory` | 只 `Resources.Load` + Instantiate 四个 Prefab；缺 Prefab 报错，**没有**运行时拼页回退 |
| 四个功能 Prefab | 页骨架在 Editor 里画；列表行、背包格子仍按数据运行时生成 |
| View `BindPrefab()` | 按物体名找到 Tabs、输入框、按钮、滚动区，运行时绑点击 |
| IPC `mainWindowRaised` | 从 Overlay 菜单唤起后主窗口盖住 Overlay；回托盘后 Overlay 恢复置顶 |
| Prefab 校验菜单 | 见 §1.2 |

正确改 UI：打开对应 Prefab → 改布局/文案 → Ctrl+S → 重新打包。

#### 修复的缺陷

| 现象 | 原因 | 处理 |
|------|------|------|
| Overlay 挡住主窗口功能页 | Overlay 默认 `Topmost`，只 `Show`/`Focus` 不够 | 推 `mainWindowRaised`；Overlay 暂时取消置顶 |
| 功能做成 1040×580 弹窗，和底栏/菜单重复 | 弹窗层与主窗口页签两套入口 | **取消功能弹窗**；菜单和底栏只切主窗口页签 |
| 设置按钮看得见、点了没反应 | Baker 把运行时 UI 存成 Prefab；私有字段引用和 `onClick` **不会**随 Prefab 序列化；`Bind()` 见已有子节点就跳过 `Build()` | 工厂只加载 Prefab；View 每次按名字重新绑按钮 |
| 社交 / 背包 / 图鉴引用全是 null | 同上，运行时没绑到 Prefab 里的控件 | `BindPrefab()` 按路径查找 |
| 设置页超出内容区、按钮挤在一起 | 没有 ScrollRect；曾用 `DestroyImmediate` 重建布局把层级打乱 | Prefab 内滚动布局；禁止运行时拆页重建 |
| 手写 YAML Prefab 报 `PPtr cast failed` | 绕过 Unity 序列化 | 只允许 Editor 保存的 Prefab |
| 「Socket 数据解析失败：空引用」 | 多半是 `chat_message` 处理里 `DesktopModalUi.Clear(null)` 等 UI NRE，被当成解析失败 | 解析与 handler 拆开；UI 空引用不再伪装成 Socket 解析错误 |
| Steam 好友列表几乎是空的 | 只显示已在塘的人；没自动 `RefreshFriends`；没订 `FriendsChanged` | 打开好友页刷新并订阅；列表含 Steam 在线/离线好友 |
| 点「邀请进塘」失败或偶发 | 邀请与 `CreateLobby` 竞态 | 无 Lobby 时先提示创建，邀请走已有 Lobby |
| 聊天点发送后永远发不出去 | `_pond` 为空仍进发送；`_sending` 卡死；`_chatInput` 为空当成空消息 | 空引用直接失败并复位 `_sending` |
| Editor 编译 CS0122 | Prefab 工具在 Editor 程序集访问了 internal `DesktopModalUi` | `DesktopModalUi` 改为 public |
| 切页 / 关窗离塘 | 页签当路由卸载 | 切页不 `leave_pond`、不重建 Socket / Overlay |

---

## 3. 不在 07A～07E 出口（避免混进本期）

| 项 | 归属 | 说明 |
|----|------|------|
| 托盘气泡、断线恢复、主流程联调 | **07F** | 未验收，不要当 07E 已做完 |
| Overlay 进程本身、透明置顶拖拽 | **07G** | 架构已落地且 07A～07E 在用，计划表仍为已确认 |
| Overlay 像素布局 JSON | **ART-02** | 有表后停用 `MapToScene` 自动缩放 |
| 正式猫/塘贴图替换 | **ART-01** | 换图不改状态机和网络 |
| 关闭后进程残留 | **BUG-21**（04 壳） | 联调时常碰到，不是 07A～07E 编号 |
| 窗口模式 / 托盘 / 通知开关 | **STEAM-DESKTOP-04** | 07E 设置页复用，不重复当 07E 新工具 |

---

## 4. 改 Prefab / 重打包备忘

1. 关闭正在运行的 `FishSocialDesktop.exe`。
2. Unity 打开对应 Prefab，改完 Ctrl+S。
3. 可选：菜单 **Fish Social → Validate Desktop Prefabs**。
4. **Fish Social → Build Windows Development Player**（IPC 或 Overlay 有改动时 Overlay 会一起发）。
5. 跑新的 `Builds/Windows64/FishSocialDesktop.exe`。
