# STEAM-DESKTOP-08G：Overlay 钓鱼操作栏

请只实现 `STEAM-DESKTOP-08G`。

## 必读

- `docs/planning/specs/Steam桌面端-08GOverlay钓鱼操作栏.md`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/NativeOverlayStateDto.cs`
- `fish-social-unity/Assets/Scripts/Desktop/NativeOverlayProcessController.cs`
- `desktop-overlay/MainWindow.xaml`
- `desktop-overlay/MainWindow.xaml.cs`

## 要求

- Overlay 底部常驻显示：选择钓位、开始钓鱼、收杆、领取鱼获。选钓位点击场景钓位标记，操作栏只反映状态。
- Named Pipe 新增版本化命令：`take_spot`、`start_fishing`、`stop_fishing`、`accept_catch`。Overlay 不连 Socket、不传图。
- Unity 继续调用 `SocialPondSessionController`，Overlay 不连接服务端、不执行业务规则。
- Unity 推送 phase、当前钓位、待领取鱼获、可用操作和错误状态。
- 将窗口拖动命中区域限制到背景，不能覆盖操作按钮。
- 操作失败不离塘、不重复创建 Socket/Overlay。

## 禁止

- 不在 WPF 中实现钓鱼概率、倒计时权威或库存结算。
- 不通过第二个 Unity Player 实现操作栏。
