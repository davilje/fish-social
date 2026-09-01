# STEAM-DESKTOP-09A：Overlay 玩家右键菜单

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-09A` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-19** |
| 完成时间 | **2026-08-20** |
| 依赖 | 07C（同塘玩家渲染）、07E（社交 API 对齐）、08G（Overlay 操作栏） |
| 前置参考 | Web `UserProfileModal` · `mobile/lib/socialApi.ts` |

---

## 1. 背景与目标

### 1.1 背景

当前 Overlay 仅在**整个鱼塘场景**右键时弹出产品级菜单（当前鱼塘、好友与聊天、背包等），**无法对单个同塘玩家**做社交操作。07E 将好友/聊天/资料放在主窗口页签，挂机时切主窗口成本较高。

产品 IA（`Steam桌面端产品定位与信息架构.md` §10.2）曾将「同塘玩家快捷菜单」标为 P1，但未写入 Steam 子需求。

### 1.2 目标

| # | 目标 |
|---|------|
| G1 | 在 Overlay 对其他玩家宠物 **右键** 弹出上下文菜单 |
| G2 | 菜单含：查看资料、添加好友、私聊、点赞互动 |
| G3 | 业务仍由 Unity + Node 权威；Overlay 只发 IPC，不直连 Socket |
| G4 | 操作不触发 `leave_pond`、不重建 Overlay |

### 1.3 非目标

- 不做 Overlay 内完整资料页（复杂资料仍走主窗口）
- 不做 Steam 好友列表替代；不新增服务端社交协议
- 不影响场景级产品右键菜单（空白/背景右键仍出原菜单）
- Bot 玩家：菜单可打开，加好友/私聊/点赞按服务端权限灰态或 toast；昵称**不**显示「·机」（见 **09D**）

---

## 2. 用户与场景

| 角色 | 场景 | 期望 |
|------|------|------|
| 挂机玩家 | Overlay 看到同塘他人 | 右键玩家 → 快速社交，无需先切主窗口 |
| 玩家 | 点「查看资料」 | 主窗口置顶并打开目标玩家资料摘要 |
| 玩家 | 点「添加好友」 | 发送好友请求；已是好友/已申请则提示 |
| 玩家 | 点「私聊」 | 主窗口置顶并切到私聊页，选中该好友会话 |
| 玩家 | 点「点赞互动」 | 对其最近一条可点赞动态 toggle like；无动态则提示 |

---

## 3. 功能范围

### 3.1 交互

```text
Overlay 同塘玩家 OverlayPetActor
  └─ 右键（MouseRightButtonUp）
       └─ ContextMenu（WPF，锚在鼠标位置）
            ├─ 查看资料
            ├─ 添加好友
            ├─ 私聊
            └─ 点赞互动
```

| 规则 | 约定 |
|------|------|
| 命中 | 仅 `OverlayPetActor`（含昵称/身体热区）；不与钓位选择、场景拖动冲突 |
| 自己 | 右键自己的猫不弹此菜单（或无项/走产品菜单） |
| 菜单层级 | 玩家菜单 **高于** 场景产品菜单；打开玩家菜单时不再触发场景 ContextMenu |
| 关闭 | 点击菜单外、Esc、或完成操作后关闭 |

### 3.2 IPC 命令（Overlay → Unity）

在现有 Named Pipe `command` 上扩展，**必须**带 `playerId`：

| command | 字段 | Unity 行为 |
|---------|------|------------|
| `player_open_profile` | `playerId` | `Show` 主窗口 → 打开他人资料（复用 REST `/api/players/:id/profile`） |
| `player_add_friend` | `playerId` | 调用现有好友请求 API |
| `player_open_dm` | `playerId` | `Show` 主窗口 → `PanelSocial` 私聊子页并选中 |
| `player_like_recent` | `playerId` | 拉取目标最近动态，对首条可点赞帖 toggle like（FEAT-SOC-01 API） |

命令需带递增 `commandId`；Unity ACK 失败时 Overlay toast 错误文案（走 `errorMessage` 回推）。

### 3.3 权限与灰态

| 项 | 规则 |
|----|------|
| 添加好友 | 已是好友 / 已 outgoing → 菜单项灰态或点击 toast |
| 私聊 | 非好友 → 灰态，提示先加好友 |
| 点赞 | 目标无公开动态 → toast「暂无动态」 |
| Bot | 资料可看；加好友/私聊按产品规则禁用 |

---

## 4. 技术影响

### 4.1 涉及文件（预估）

- `desktop-overlay/OverlayPetActor.cs` — 右键事件、ContextMenu
- `desktop-overlay/IpcProtocol.cs` — 命令字段
- `desktop-overlay/MainWindow.xaml.cs` — 命令发送
- `fish-social-unity/.../NativeOverlayStateDto.cs`、`NativeOverlayProcessController.cs`
- `fish-social-unity/.../DesktopAppBootstrap.cs` — 命令分发
- `fish-social-unity/.../SocialPondSessionController.cs` 或新建 `OverlayPlayerSocialBridge.cs`
- Unity UI：他人资料入口（可扩 `DesktopProfilePanel` 或轻量 Modal）

### 4.2 API（复用，无新 Socket）

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/players/:id/profile` | 资料 |
| REST | `POST /api/friends/request` | 加好友 |
| REST | `GET /api/social/posts?playerId=` | 最近动态（点赞用） |
| REST | `POST /api/social/posts/:id/like` | toggle like |
| Socket | `dm_message` | 私聊仍经 Unity 已有连接 |

---

## 5. 验收标准

- [x] 右键其他玩家弹出四项菜单；右键场景空白仍出原产品菜单
- [x] 「查看资料」打开主窗口并展示目标玩家资料摘要
- [x] 「添加好友」成功/重复/失败有明确反馈
- [x] 「私聊」打开主窗口私聊并选中目标（好友可用）
- [x] 「点赞互动」对最近可点赞动态生效；无动态有提示
- [x] 全程不 `leave_pond`、不重连 Socket、不重建 Overlay
- [x] Bot 与非好友权限符合 §3.3

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 与场景右键菜单冲突 | 事件 `Handled=true`；分层 HitTest |
| 他人资料 UI 未就绪 | 首版可 `Show` 主窗口 + 现有 Panel 扩展 |
| 点赞需拉动态列表 | 仅首条，失败 toast，不阻塞 Overlay |

---

## 7. 开发交接

**提示词**：[`docs/planning/prompts/steam-desktop-09a-overlay-player-menu-dev.prompt.md`](../prompts/steam-desktop-09a-overlay-player-menu-dev.prompt.md)

---

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-09-01 | BUG-26 回归验收：菜单 StaysOpen、右键抬起打开；关菜单后悬停恢复。本单菜单项不变 |
| 2026-09-01 | 右键出不来 / 菜单后悬停死 → **BUG-26**（与悬停同一命中链）；本单菜单项不变 |
| 2026-08-20 | 用户验收通过 → **已实现** |
| 2026-08-19 | 初稿：Overlay 单玩家右键菜单；四项社交动作；IPC 扩展；对齐 07E API |
