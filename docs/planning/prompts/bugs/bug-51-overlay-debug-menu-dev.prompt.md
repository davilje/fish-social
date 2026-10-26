# 开发提示词：Overlay Debug 菜单点击无反应（BUG-51）

你负责 Fish Social 的 WPF Overlay 与 Unity 桌面命令处理。只做 **BUG-51**。不新增 Debug 动作；不改 `server/`、`shared/`、`mobile/`。

## 必读

1. `docs/planning/specs/bugs/BUG修复-OverlayHUD布局与交互.md` §1.4、§3 BUG-51、§4.4
2. `desktop-overlay/MainWindow.xaml.cs`：`SendGameplayDebug`、`GameplayDebug*_OnClick`、`HandleMessage` 中 `ErrorText.Text = message.ErrorMessage`
3. `desktop-overlay/MainWindow.xaml` `GameplayDebugModal`
4. `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs` `ExecuteOverlayGameplayDebug`、`GameplayDebugGate`

## 顺序

1. 先加证据：Overlay 发送、Unity 收到、Unity 完成各写一行，带同一 commandId 和 action。复现一次，把结论补进 spec §3 BUG-51（去掉「待验证」）。
2. Debug 弹窗内加状态行，显示执行中 / 成功 / 失败原因，按 commandId 回写。
3. 全局 `ErrorText` 临时提示至少停留 3 秒，或只在 `ErrorMessage` 变化时更新，不被每条 state 清空。
4. 按第 1 步结论修掉剩余原因（客户端未开 Debug、命令没到 Unity、服务端拒绝等）。

## 验收

对照 spec §4.4。完成后按 Skill `planning-progress-sync`：BUG-51 改为 **已实现**，填完成时间，`npm run planning:master-xlsx`。

- [ ] 17 个按钮每个 1 秒内有执行中提示，并给出成功或失败原因
- [ ] 日志三段 commandId 一致
- [ ] 「升级」「+100万金币」后数值变化

## 派发

```text
@docs/planning/prompts/bugs/bug-51-overlay-debug-menu-dev.prompt.md 按此实现 BUG-51
```

建议角色：Overlay 客户端 + Unity 桌面（本仓库 Cursor 对话）。
