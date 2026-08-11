# 开发交接提示词：Unity Windows 桌面端基础壳（STEAM-DESKTOP-04）

你是 Fish Social 的 Unity 前端工程师。请在现有 Unity 工程 `fish-social-unity/` 中实现 `STEAM-DESKTOP-04` 及其 04A～04F 子任务，建立可运行、可构建、低打扰的 Windows 桌面端基础壳。

## 必读

1. `docs/planning/specs/Unity Windows桌面端基础壳.md`
2. `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
3. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
4. `docs/planning/architecture/Unity迁移决策记录.md`

## 工程位置

```text
C:\Users\Administrator\Projects\fish-social\fish-social-unity
```

只在 `fish-social-unity/` 内创建 Unity 代码和资源。不要把 `mobile/`、`server/`、`shared/` 复制到 `Assets/`。

## 实现顺序

1. **04A**：确认工程基线、Windows 构建、目录和空场景。
2. **04B**：实现生命周期、窗口模式、最小化/恢复和窗口配置保存。
3. **04C**：实现托盘显示/隐藏/退出，后台隐藏时避免高频渲染。
4. **04D**：实现主界面和四个功能占位：鱼塘、好友/聊天、鱼获/背包、设置。
5. **04E**：实现窗口/通知设置持久化和统一通知接口，先用模拟事件验证。
6. **04F**：构建 Windows Development Build，执行启动、窗口、托盘、设置、通知冒烟。

## 必须遵守

- 关闭窗口隐藏到托盘并继续运行；托盘菜单必须提供真正退出。
- 第一阶段只支持 Windows。
- 支持普通窗口、全屏和无边框；暂不做置顶、透明、开机启动和多平台。
- 不接 Steam Ticket、Steam Lobby、Steam Networking 或 Node Socket.IO。
- 不实现真实鱼塘、钓鱼、库存、聊天业务，只提供可替换占位和接口边界。
- 本地只保存窗口、通知等非权威偏好，不保存金币、库存、鱼获、每日额度或密钥。
- 不提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`。

## 验收

- Unity 工程可打开、运行和构建 Windows 包。
- 三种窗口模式可切换，窗口配置重启后恢复。
- 关闭窗口进入托盘，托盘可恢复或退出。
- 主界面四个入口可进入/返回，窗口缩放不重叠。
- 模拟鱼咬钩、好友邀请和连接错误通知可以显示或关闭。
- 托盘隐藏后无持续高频渲染。

建议提交拆分：

```text
feat(unity): add desktop window shell
feat(unity): add tray and notification settings
test(unity): verify Windows shell smoke flow
```

完成后运行 Unity Windows 冒烟验证，并将 04A～04F 的验收结果回写计划表。
