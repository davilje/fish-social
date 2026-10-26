# UNITY-P1 / UNITY-P2 验收与 Debug 记录

日期：2026-08-12  
状态：已验收

## 一、已验证内容

- Steam Ticket 登录成功，并复用同一 `playerId`。
- Unity 使用 JWT 连接 Node Socket.IO，连接状态真实反映连接、断开和重连。
- 完成 `register_player`、`join_pond`、`pond_snapshot`、`take_spot`、`start_fishing`、`stop_fishing`、`fish_bite`、`accept_catch`、`inventory_updated` 闭环。
- Unity 不计算咬钩概率、鱼获结果或库存，服务端保持唯一权威。
- `hooked` 后可收到待领取鱼获，领取后背包显示鱼获数量、种类、品质和尺寸。
- 服务端关闭时客户端不崩溃；服务端恢复后可重新认证并恢复鱼塘会话。
- `FISHING_TEST_MODE=instant` 生效：咬钩检查间隔 1 秒，测试模式下鱼获结果不再依赖随机咬钩和逃脱概率。

## 二、Debug 内容与过程

### 1. phase 始终显示“请先选择钓位”

现象：服务端已经切换 phase，但 Unity UI 仍显示固定文案。

原因：`DesktopShellUi.OnPondSnapshot` 将 phase 硬编码为“请先选择钓位”，没有读取当前玩家的 `fishingPhase`。

修复：

- DTO 增加当前玩家状态字段。
- `SocialPondSessionController` 从 `pond_snapshot` 和 `pond_user_updated` 更新当前玩家。
- UI 根据服务端状态显示 `idle`、`seated`、`waiting`、`hooked`、`stopping` 等 phase。

### 2. 收杆后无法重新开始，重复收杆仍显示成功

现象：收杆后立即开始会失败；重复点击收杆多次都显示成功。

原因：

- 客户端没有同步 `stopping → seated` 状态。
- 服务端对部分重复收杆路径提供幂等响应，旧客户端会把响应统一显示为“操作成功”。

修复：

- 客户端同步 `pond_user_updated`。
- `stopping` 期间阻止重新开始并提示当前 phase。
- 非钓鱼状态下客户端不再重复发送收杆请求。
- 收杆完成回到 `seated` 后允许再次开始。

### 3. 领取鱼获提示“没有待领鱼获”

现象：UI 显示 `hooked` 后立即点击领取，但客户端提示没有待领鱼获。

原因：服务端进入 `hooked` 后，还要等待收杆窗口结束，才会发送 `fish_bite` 和创建待领取鱼获。此时客户端的 `_latestCatch` 尚未生成。

修复：

- `hooked` 状态下提前点击领取时，改为提示“鱼已咬钩，正在结算”。
- 快速测试模式将收杆窗口缩短为约 1 秒。

### 4. 背包没有更新显示

现象：服务端已完成 `accept_catch`，Unity 背包没有变化。

原因：Unity Socket 客户端未解析和转发 `inventory_updated` 事件，UI 也没有订阅该事件。

修复：

- 增加 `inventory_updated` 数组 DTO 解析。
- `SocialPondSessionController` 保存当前背包并转发更新事件。
- UI 显示鱼获数量、种类、品质和尺寸。

### 5. 快速模式仍长时间停留在 waiting

现象：启动日志已经显示：

```text
[fishing-test] instant=true biteCheckMs=1000
```

但用户仍长时间停留在 `waiting`。

排查证据：

- 日志显示反复发送 `fishing_float_text kind=miss`。
- 快速模式已经跳过随机咬钩概率，因此 `miss` 的实际原因是选中钓位没有可抽取鱼。
- 重连恢复的是原有钓位；此前鱼获已经消耗了该钓位的鱼。

修复：

- 快速测试模式下，选中钓位无鱼时自动补充一条测试鱼到该钓位。
- 该逻辑仅在非生产环境的 `FISHING_TEST_MODE=instant` 下启用，不改变正式生态规则。

## 三、最终简要说明

UNITY-P1/P2 已完成并通过真实 Unity Windows Development Build 验收。Unity 已具备基于 Steam JWT 的 Socket.IO 薄客户端能力，可完成进塘、选位、钓鱼、咬钩、领取鱼获、背包更新及断线重连。期间修复了 phase 状态不同步、重复收杆、咬钩结算时序、背包事件遗漏和快速测试钓位无鱼导致永久 waiting 等问题。服务端仍是钓鱼、鱼获和库存的唯一权威。
