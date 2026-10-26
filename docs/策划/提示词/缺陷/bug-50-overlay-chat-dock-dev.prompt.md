# 开发提示词：Overlay 聊天栏 10 行、折叠按钮对齐、发送后清空（BUG-50）

你负责 Fish Social 的 WPF Overlay。只做 **BUG-50**。不改 `server/`、`shared/`、`mobile/`；不改服务端聊天校验。

## 必读

1. `docs/策划/规格/缺陷/BUG修复-OverlayHUD布局与交互.md` §1.3、§3 BUG-50、§4.3
2. `desktop-overlay/MainWindow.xaml.cs`：`ApplyHudChatDockLayout`、`ChatDockToggle_OnClick`、`TrySendPondChat`、`ApplyPondChat`、`FinishChatAck`
3. `desktop-overlay/OverlayHudLayout.cs`：`ComputeChatDockExpandedHeight`、聊天子控件布局
4. `desktop-overlay/OverlayChatPresenter.cs`（行字号与行距）
5. Unity：`fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs` `send_pond_chat` → `ExecuteOverlayCommand`

## 顺序

1. 展开高度按 10 行计算，面板向上长；小视口下按可用高度截断并保留滚动。
2. 折叠按钮固定在面板底行右侧，展开前后屏幕位置不变；发送按钮放它左侧同一行。换一个看得出「收起」的图标。
3. 发送确认改为按 commandId 的结果回执（Unity 在 `ExecuteOverlayCommand` 回调里带回 commandId 与 ok/message）。成功清空输入框，失败保留并提示。旧 Unity 构建没有回执时退回超时逻辑。若改 `IpcProtocol.cs`，同步更新 `cozy_source_contract.json` 中它的哈希并说明原因。
4. 不再用 `RecentChats` 文本比对或任意 `ErrorMessage` 结束等待。

## 验收

对照 spec §4.3。完成后按 Skill `planning-progress-sync`：BUG-50 改为 **已实现**，填完成时间，`npm run planning:master-xlsx`。

- [ ] 展开可见 10 行
- [ ] 展开 / 收起按钮位置不跳，与发送按钮同行居中
- [ ] 连续发送 5 条（含重复文字）每次清空；断线发送保留并提示；打窝提示显示期间发送也能清空

## 派发

```text
@docs/策划/提示词/缺陷/bug-50-overlay-chat-dock-dev.prompt.md 按此实现 BUG-50
```

建议角色：Overlay 客户端（`desktop-overlay/`），Unity 侧只加命令回执。
