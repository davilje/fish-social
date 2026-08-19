# STEAM-DESKTOP-09C：Overlay 鱼塘聊天气泡与输入

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-09C` |
| 类型 | 功能 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P1 |
| 设计时间 | **2026-08-19** |
| 依赖 | 07E（鱼塘聊天语义）、08G（底部操作栏布局） |
| 口径变更 | **修订** `Steam桌面端Web功能对齐设计.md` §1「聊天仅主窗口」— 本需求允许 Overlay **轻量**鱼塘公屏聊天 |
| 前置参考 | [`鱼塘叠加层与背包社交收口.md`](./鱼塘叠加层与背包社交收口.md)（**FEAT-UI-2** · Web 场景聊天气泡层） |

---

## 1. 背景与目标

### 1.1 背景

07E 定稿：鱼塘公共聊天、私聊、好友列表均在 **主窗口 `PanelSocial`**；Overlay 右键「好友与聊天」只 `Show` 主窗口。

挂机主路径在 Overlay（08G 钓鱼操作栏）时，玩家无法在 Overlay 直接看公屏聊天或发一句话，必须切窗口。

Web/RN（FEAT-UI-2）已在鱼塘场景 **统一 Overlay 层** 绘制聊天气泡（不被角色遮挡）。Steam 原生 Overlay 尚无等价能力。

### 1.2 目标

| # | 目标 |
|---|------|
| G1 | Overlay 显示**鱼塘公屏**最近聊天（文字气泡，非完整聊天页） |
| G2 | Overlay 底部提供**紧凑输入框 + 发送**，支持鱼塘公屏发言 |
| G3 | 消息与发送仍经 Unity → Socket 权威；Overlay 不直连 |
| G4 | 私聊、好友列表、动态墙仍在主窗口（不在本需求范围） |

### 1.3 非目标

- 不做 Overlay 内私聊会话 UI
- 不做表情、图片、@、富文本
- 不做聊天历史无限滚动（仅最近 N 条，建议 N=20）
- 不替代主窗口 `PanelSocial` 完整聊天页
- 不做系统公告编辑

---

## 2. 用户与场景

| 角色 | 场景 | 期望 |
|------|------|------|
| 挂机玩家 | 在 Overlay 钓鱼 | 看到同塘他人刚发的公屏文字气泡 |
| 挂机玩家 | 想打招呼 | 底部输入框输入 ≤200 字 → 发送 |
| 玩家 | 发送失败/断线 | Overlay 显示错误，不 silent fail |
| 玩家 | 需要完整聊天记录 | 仍通过右键/主窗口打开 PanelSocial |

---

## 3. 功能范围

### 3.1 UI 布局

```text
960×480 Overlay
├── 鱼塘场景（07B/07C）
├── 聊天气泡层（最高层之一，角色之上、菜单之下）
│     └── 最近 N 条 pond chat 气泡（昵称 + 文本，渐隐或可滚动窄条）
├── 底部操作栏（08G：钓位/开钓/收杆/领鱼）
└── 聊天输入条（08G 之上或并列一行）
      ├── TextBox（placeholder「说点什么…」）
      └── 发送按钮
```

| 规则 | 约定 |
|------|------|
| 气泡定位 | 优先屏幕左下/右侧 **固定聊天流**（避免逐角色锚点复杂度）；P1 可选锚到发言人宠物上方 |
| 字数 | 200 字上限，与 07E / Web 一致 |
| 空消息 | 禁用发送 |
| 命中 | 输入条与按钮参与 HitTest；透明区仍穿透 |

### 3.2 数据流

```text
Socket chat_message (Unity)
  → 追加本地环形缓冲
  → PublishState 附带 recentChats[]
Overlay 渲染气泡

用户输入 → IPC send_pond_chat { text }
  → Unity SocialPondSessionController.SendChat(text)
  → Socket emit → 服务端 appendChatMessage
  → chat_message 广播 → 回推 Overlay
```

### 3.3 IPC 扩展

**Unity → Overlay**（`NativeOverlayStateDto`）：

```json
"recentChats": [
  {"messageId":"…","playerId":"p1","nickname":"Alice","text":"大家好","sentAtMs":1724…}
]
```

**Overlay → Unity**：

| command | 字段 |
|---------|------|
| `send_pond_chat` | `text`（必填，trim 后 1–200 字） |

ACK：成功清空输入框；失败设置 `errorMessage`。

### 3.4 与 07E 的关系

- 07E **保留**：完整聊天历史、私聊、好友、在线列表 → 主窗口。
- 09C **新增**：Overlay 仅 **鱼塘公屏** 的「最近消息预览 + 快捷发送」。
- 右键「好友与聊天」行为不变。

---

## 4. 技术影响

### 4.1 涉及文件（预估）

- `desktop-overlay/MainWindow.xaml` — 气泡层、输入条
- `desktop-overlay/MainWindow.xaml.cs`、`IpcProtocol.cs`
- `fish-social-unity/.../NativeOverlayStateDto.cs`
- `fish-social-unity/.../OverlayPondStateBuilder.cs`
- `fish-social-unity/.../SocialPondSessionController.cs` — 聊天缓冲与发送
- `fish-social-unity/.../SocialSocketClient.cs` — 已有 `chat_message`

### 4.2 API / Socket（复用）

| 类型 | 名称 | 说明 |
|------|------|------|
| Socket emit | `chat`（或项目现有鱼塘聊天事件名） | 发送 |
| Socket on | `chat_message` | 接收 |

---

## 5. 验收标准

- [ ] Overlay 可见最近鱼塘公屏消息（至少含昵称+文本）
- [ ] 输入框可发送 ≤200 字；空消息不可发
- [ ] 发送成功后自己与他人 Overlay 均能看到新气泡
- [ ] 断线/服务端拒绝有 `errorMessage` 或 toast
- [ ] 不 `leave_pond`；钓鱼操作栏仍可正常点击
- [ ] 主窗口 PanelSocial 聊天仍完整可用，与 Overlay 消息一致
- [ ] 私聊不在 Overlay 出现

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 与 08G 底部栏抢空间 | 输入条单行 32–40px；操作栏上移 |
| 消息过多挡场景 | 仅 N 条 + 渐隐；固定侧栏流 |
| 07E 文档冲突 | 本 spec 为准；07E CHANGELOG 补「09C 扩展」 |

---

## 7. 开发交接

**提示词**：[`docs/planning/prompts/steam-desktop-09c-overlay-pond-chat-dev.prompt.md`](../prompts/steam-desktop-09c-overlay-pond-chat-dev.prompt.md)

---

## 8. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-19 | 初稿：Overlay 公屏聊天气泡 + 输入；修订 07E「聊天仅主窗口」为「完整聊天在主窗口，Overlay 轻量公屏」 |
