# STEAM-DESKTOP-07E：桌面宠物功能弹窗层

请在 `fish-social-unity/` 内实现桌面宠物功能弹窗层。

## 必读

- `docs/planning/specs/Steam桌面端Web功能对齐设计.md`
- `docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md`
- `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
- `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopShellUi.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`

## 范围

- 主窗口底部横向按钮行进入好友/聊天、鱼获/背包、图鉴和设置弹窗。
- 好友/聊天：在线钓友、鱼塘公共聊天、好友列表/请求/邀请和好友私聊。
- 鱼获/背包：至少 80 格、金币、鱼获详情、出售和分享到动态。
- 图鉴：鱼种网格、解锁状态、鱼种详情、累计捕获和推荐鱼饵。
- 设置：窗口模式/尺寸、托盘行为、鱼咬钩通知、好友邀请通知和免打扰。
- 统一打开、关闭、返回和错误提示接口。

## Web 对齐要求

- 功能字段和权限以 `Steam桌面端Web功能对齐设计.md` 与现有 Web 组件为准，布局按 Steam 桌面窗口重新设计。
- 复用现有 REST/Socket 数据，不新增第二套库存、图鉴、好友或聊天规则。
- 鱼塘聊天最多 200 字，私聊最多 300 字；背包至少显示 80 格。
- 必须处理 loading、empty、error、401/403、断线和重试状态。
- 统一消费已有桌面 Socket 会话，不为每个弹窗创建独立连接。

## 强制边界

- 弹窗打开和关闭不得触发 `leave_pond`。
- 不得清空当前 pond、玩家宠物或多人场景状态。
- 暂无真实数据的功能可以使用明确占位，但不得伪造服务端结果。
- 业务数据继续从现有 API/Socket 获取。
- 从 Overlay 菜单打开主窗口后，弹窗不得重复创建鱼塘、Overlay 或 Socket 会话。
- 动态墙、好友 Feed、排行榜和商店不纳入本需求，另立桌面功能需求。

## 验收

- 右键菜单可以打开各弹窗。
- 弹窗可关闭并回到原鱼塘状态。
- 弹窗切换不会重复创建或销毁鱼塘会话。
- 网络错误和空数据有清晰提示。
- Web 端背包、图鉴、鱼塘聊天、好友和私聊的核心数据在 Steam 端可见。
- `inventory_updated`、`codex_unlocked`、`friend_request`、`dm_message` 能更新对应弹窗。
- 弹窗关闭和窗口切换不触发 `leave_pond`。
