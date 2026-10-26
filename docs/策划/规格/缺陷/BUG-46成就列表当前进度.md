# BUG-46 · 成就列表当前进度

| 字段 | 内容 |
|------|------|
| 功能名称 | 成就列表显示相对目标的当前进度 |
| 编号 | BUG-46 |
| 状态 | **已确认** |
| 目标版本 | hotfix |
| 优先级 | P1 |
| 类型 | 功能 |
| 设计时间 | 2026-10-16（2026-10-22 定稿） |
| 完成时间 | |
| 关联全景 | `docs/策划/规格/玩法/钓鱼相册与成就.md` §3.4；桌面手账成就页 |
| 来源 Agent | 策划 |

## 1. 背景与目标

### 1.1 现象与复现

打开桌面手账的成就页即可稳定看到。不经过 Overlay，也不经过钓鱼 Socket。

| 项 | 内容 |
|----|------|
| 操作 | 登录后打开成就页，「全部成就」 |
| 预期 | 未完成项能看出离目标还差多少 |
| 实际 | 卡片只有「✓ 已解锁」或「尚未解锁」。右侧奖章只显示已解锁条数 / 目录条数 |
| 平台 | Unity 桌面主窗口。与服务端进程启动时间无关 |
| 频率 | 打开该页即出现 |

### 1.2 时间戳证据

这是资料接口缺字段，不是操作延迟。下面各节点不适用，不把责任归到网络或帧率。

```text
Overlay command_sent            不适用
Unity overlay_command_received  不适用
Socket event_sent               不适用
Server event_received           不适用
Socket ack_received             不适用
Unity state_queued              不适用
Overlay state_received          不适用
```

代码证据（2026-10-22，对照当前工程）：

- `server/src/achievements.ts` 的 `progressValue` 已经按 `catch_count`、`codex_count`、`return_count`、`max_size`、`album_pins` 算出当前值，但只在 `tryUnlockAchievements` 里和 `conditionValue` 比较。
- `buildAchievementViews` 写进响应的只有定义字段、`unlocked`、`unlockedAt`。同一请求里不算、也不下发当前值。
- `shared/albumTypes.ts` 的 `AchievementView` 同样没有当前值。
- Unity `AchievementViewDto` 与之对齐。`JournalV2AchievementController.RenderCards` 只画解锁文案。
- 他人资料走 `forPublic`：未解锁成就整条不下发。隐藏且未解锁的成就，自己的客户端列表也会滤掉。

因此 2026-10-21「不改 server/shared、等客户端自己有字段再画条」做不到。当前值只存在于服务端查询，客户端没有背包、图鉴、回鱼次数和相册钉选的同一口径。

### 1.3 根因分层

| 层 | 判断 |
|----|------|
| 服务端逻辑 | 主因。进度函数已存在，视图组装时丢弃了结果 |
| 数据库 | 不新增表。解锁仍只记 `player_achievements`；当前值继续现算 |
| Socket / ACK | 不适用。走既有 `GET /api/players/:id/profile-hub` |
| Unity 主线程 | 卡片没有进度条，因为 DTO 没有数 |
| Named Pipe / Overlay | 不适用 |

### 1.4 目标

- 看自己的成就时，未完成且可见的卡片显示当前值 / 目标值，并有一条不超过 100% 的进度。
- 已解锁卡片保持「已解锁」，进度算满。
- 解锁条件、隐藏规则、他人可见范围不变。

### 1.5 非目标

- 不新增成就，不改 `conditionType` / `conditionValue`，不改解锁时机。
- 不把未解锁成就显示给别人，也不把隐藏且未解锁的成就显示给自己。
- 不改右侧「已解锁 / 目录条数」奖章，那是目录完成度，不是单条进度。
- 不改 `mobile/`。移动端没有这条成就列表。
- 不加新的 `fishing_metrics` 事件。
- 不做 Steam 成就同步。

## 2. 用户与场景

| 角色 | 场景 | 期望 |
|------|------|------|
| 自己 | 全部成就里看一条未完成、非隐藏的成就 | 看到当前 / 目标，进度条按比例填充 |
| 自己 | 已完成页签 | 每条都是已解锁，进度满 |
| 自己 | 进度为 0 | 显示 0 / 目标，条为空 |
| 他人 | 打开只读资料里的成就 | 仍只有已解锁项，没有别人的未完成进度 |

## 3. 功能范围

### 3.1 数据

`AchievementView` 增加：

| 字段 | 类型 | 说明 |
|------|------|------|
| `currentValue` | number | 与解锁判断同一口径的当前值。计数为整数；`max_size` 为米 |

`buildAchievementViews` 对每种 `conditionType` 只查一次，同一请求内复用。未知类型为 0。

已解锁也下发真实 `currentValue`（可以大于目标）。客户端不靠这个字段决定是否解锁，仍以 `unlocked` 为准。

他人视图：未解锁项继续整条省略，因此不会带出未完成进度。

旧客户端忽略未知 JSON 字段。新客户端若收到缺字段（反序列化成 0）且 `unlocked` 为真，按已满处理，不要画成 0 / 目标。

### 3.2 界面

只改手账成就卡片（`JournalV2AchievementController` 的卡片）：

- 未解锁：状态行改为「当前 / 目标」。计数直接显示整数。`max_size` 显示米，最多两位小数，去掉末尾多余的 0，例如 `0.4 / 0.8 米`。
- 进度条填充 = `min(currentValue, conditionValue) / conditionValue`。目标为 0 时不除，条为空。
- 已解锁：状态仍是「✓ 已解锁」，条满。不要在已解锁上写超出目标的数字。
- 隐藏且未解锁：继续不进列表。
- 「全部成就 / 已完成的成就」两个页签、排序、`isHidden` 过滤保持现状。

### 3.3 API

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/players/:id/profile-hub` | `achievements[]` 增加 `currentValue` |
| Socket | 无 | `achievement_unlocked` 仍只带解锁通知，不带进度 |

## 4. 技术影响

### 4.1 涉及文件

- `shared/albumTypes.ts`：`AchievementView.currentValue`
- `server/src/achievements.ts`：视图组装时写入，按条件类型缓存查询
- `fish-social-unity/Assets/Scripts/Desktop/Auth/UnityNetworkDtos.cs`：`AchievementViewDto.currentValue`
- `fish-social-unity/Assets/Scripts/Desktop/JournalV2/Views/JournalV2AchievementController.cs`：卡片文案和进度条

不改解锁 SQL，不改成就表。

### 4.2 回滚

去掉 `currentValue` 字段和卡片上的条，恢复「尚未解锁」文案。已写入的解锁记录不动。

## 5. 验收标准

- [ ] 自己的一条未完成计数成就（例如图鉴条数未达 `conditionValue`）显示「当前整数 / 目标整数」，条长与比值一致且不超过满格。
- [ ] 进度为 0 时显示 0 / 目标，条为空。
- [ ] `max_size` 未达标时单位是米，数值与 `progressValue` 的最大尺寸一致。
- [ ] 已解锁卡片仍写「✓ 已解锁」，条为满；不出现 0 / 目标。
- [ ] 隐藏且未解锁的成就不出现。他人资料里看不到未解锁成就，也看不到其 `currentValue`。
- [ ] 解锁阈值和触发时机与改前一致。同一 `conditionType` 的多条成就在一次 profile-hub 里共用一次进度查询。
- [ ] 右侧奖章仍是已解锁条数 / 目录条数。

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 成就很多时重复查库 | 同一次视图组装按 `conditionType` 缓存 |
| 旧服务端没有 `currentValue` | 已解锁且数值为 0 时客户端画满条 |
| 回鱼次数依赖 `fishing_metrics` | 与现有解锁判断同一查询，不另造口径 |

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-16 | 策划 | 记在 Cozy 还原 spec：缺当前进度 |
| 2026-10-21 | 策划 | 改为功能、待开发，并曾限制不改 server/shared |
| 2026-10-22 | 策划 | 核实进度只在解锁时计算。定稿：下发 `currentValue`。状态 **已确认** |
