# 开发交接提示词：STEAM-DESKTOP-06 Lobby 生命周期与邀请反馈优化

请在正式产品 Lobby 方案确认后实现 `STEAM-DESKTOP-06`。当前版本仅完成需求登记，不要直接按本提示词修改代码。

## 必读

1. `docs/planning/specs/Steam Lobby生命周期与邀请反馈优化.md`
2. `docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md`
3. `server/src/socialRoutes.ts`
4. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`
5. `fish-social-unity/Assets/Scripts/Desktop/Social/SteamSocialLobbyAdapter.cs`
6. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`

## 实现范围

- 明确普通成员离开、房主离开、房主关闭 Lobby 的服务端语义。
- 房主离开时实现产品确认后的房主转移或自动关闭，禁止产生不可管理的孤儿 Lobby。
- 保持 Lobby 生命周期与鱼塘数据、鱼塘会话、离线生态生命周期解耦。
- 将“邀请第一位好友”改为可确认具体目标的邀请流程。
- 展示好友名称、在线状态和必要的 SteamID 脱敏信息。
- 明确反馈服务端邀请 Token 生成和 Steam 邀请请求的成功/失败。
- 邀请失败不得清空或破坏当前已加入的 Lobby 状态。

## 约束

- 正式开发前重新确认最终产品交互，不假设当前验证版 UI 会保留。
- 不记录 JWT、Steam Ticket、API Key 或完整邀请 Token。
- 补充后端和 Unity 可执行的回归验证。

## 验收

- 普通成员离开不删除服务端 Lobby。
- 房主离开不会留下无主 Lobby。
- 关闭 Lobby 后新加入和新邀请均明确失败。
- 邀请目标和邀请结果对用户可见。
- 邀请失败后 Lobby 仍保持 `LobbyJoined`。
