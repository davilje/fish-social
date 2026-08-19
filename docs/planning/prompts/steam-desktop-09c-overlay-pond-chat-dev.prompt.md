<!-- 来源: docs/planning/specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md -->

你是 Fish Social **Unity + 原生 Overlay 开发 Agent**。实现 **STEAM-DESKTOP-09C：Overlay 鱼塘聊天气泡与输入**。

## 必读

1. [`docs/planning/specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md`](../specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md)
2. [`docs/planning/specs/Steam桌面端Web功能对齐设计.md`](../specs/Steam桌面端Web功能对齐设计.md) §3.1 鱼塘社交（字段与 200 字上限）
3. [`SocialPondSessionController.cs`](../../../fish-social-unity/Assets/Scripts/Desktop/SocialPondSessionController.cs) · [`SocialSocketClient.cs`](../../../fish-social-unity/Assets/Scripts/Desktop/Auth/SocialSocketClient.cs)
4. [`MainWindow.xaml`](../../../desktop-overlay/MainWindow.xaml)（08G 底部栏布局）

## 必须做

1. **Unity**：订阅 `chat_message`，维护最近 N=20 条环形缓冲；`PublishState` 附带 `recentChats[]`。
2. **IPC**：Overlay → Unity `send_pond_chat { text }`；Unity 走现有 Socket 发送；ACK/错误回 `errorMessage`。
3. **WPF**：聊天气泡层（侧栏流或固定区）+ 底部紧凑输入条（与 08G 操作栏共存）。
4. **规则**：200 字、trim、空消息禁用；断线/拒绝有提示。
5. **07E 并存**：主窗口 PanelSocial 完整聊天仍可用；私聊不在 Overlay。

## 不做

- Overlay 私聊 / 好友列表
- 富文本 / 图片 / @
- 无限历史滚动

## 完成后

- [ ] 勾选 spec §5
- [ ] 更新 07E CHANGELOG 补 09C 扩展说明
- [ ] 验收后更新 spec 状态与 CHANGELOG
