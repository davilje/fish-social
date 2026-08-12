# STEAM-DESKTOP-07F：桌面宠物主流程与恢复验收

请在 `fish-social-unity/` 内收口并验收 STEAM-DESKTOP-07A～07E。

## 必读

- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/`

## 主流程

```text
Steam 启动
→ 登录
→ 桌面宠物
→ 进入鱼塘
→ 自己和同塘玩家宠物
→ 开始钓鱼
→ 最小化/托盘挂机
→ 鱼咬钩通知
→ 恢复窗口收鱼
→ 右键打开弹窗
→ 关闭弹窗回到鱼塘
→ 断线重连后恢复服务端快照
```

## 验收范围

- Windows Development Build 可启动。
- Steam 登录和鱼塘进入正常。
- 自己与同塘玩家表现正常。
- 右键菜单和弹窗不破坏鱼塘会话。
- 托盘隐藏、恢复、通知和退出正常。
- 断线恢复后不出现残留玩家或伪造状态。

## 交付

- Unity Development Build。
- 07A～07E 验收结果。
- 失败步骤、截图、Unity 日志和服务端日志。
- 不修改已完成的 Steam 认证、Lobby、鱼塘生态和 Node 权威逻辑。
