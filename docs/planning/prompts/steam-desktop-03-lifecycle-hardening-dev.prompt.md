# 开发交接提示词：STEAM-DESKTOP-03 Lobby 生命周期加固

你是 Fish Social 的后端主责 + Unity 联调工程师。请修复 `STEAM-DESKTOP-03` 中 Lobby 临时入口与持久化鱼塘生命周期之间的边界风险。

## 必读文件

1. `docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md`
2. `docs/planning/specs/空鱼塘休眠与生态离线补算.md`
3. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
4. `server/src/socialRoutes.ts`
5. `server/src/socketPondHandlers.ts`
6. `server/src/socketLifecycle.ts`
7. `server/src/pondEcology.ts`
8. `server/src/pondSession.ts`
9. `server/src/__tests__/pondEcologyOffline.test.ts`
10. `fish-social-unity/Assets/Scripts/Desktop/Social/`

## 当前事实

当前 `socialLobbies` 是 Node 进程内存 Map，只能表示临时 Steam Lobby 入口：

- 它不能拥有鱼塘生命周期。
- 它不能决定鱼塘是否继续运行。
- 它不能保存玩家资产、鱼获、库存或生态状态。
- 服务端重启后，内存映射可能丢失。
- 当前加入接口需要补充 Lobby 成员/邀请真实性校验。

已有正确基础：

- `pondId` 是持久化鱼塘实体。
- `pond_state.last_simulated_at` 已存在。
- `ensurePondEcologyCurrent()` 已有事务和幂等补算。
- `tickAllPonds()` 已只对有真人在线的鱼塘执行高频 Tick。
- `join_pond` 已在进入前调用生态唤醒补算。

## 不可违反的架构规则

```text
Steam Lobby = 临时社交入口 / 邀请关系
pondId      = 持久化鱼塘实体
Node/DB     = 鱼塘、玩家、生态和权限权威
```

必须满足：

- Lobby 关闭不删除鱼塘。
- Lobby 房主离开不关闭鱼塘。
- Lobby 失效只影响新的 Lobby 加入。
- 已经进入鱼塘的玩家不因 Lobby 关闭被踢出。
- 最后一个真人离开后，鱼塘停止高频 Tick并进入休眠。
- 空鱼塘的成长、迁移和补充通过离线时间继续积累。
- 任意合法入口重新进入同一 `pondId` 时，先执行幂等离线补算，再恢复实时 Tick。
- 不为每个鱼塘启动常驻服务器进程。
- Lobby 不能成为进入鱼塘的唯一条件。

## 修复任务一：Lobby 映射生命周期

选择并实现一种明确的重启恢复方案，优先方案如下：

### 推荐方案：Steam 元数据 + Node 短期缓存

1. 创建 Lobby 时把 `pondId`、`gameVersion`、`protocolVersion` 写入 Steam Lobby 元数据。
2. Node 可将 `lobbyId → pondId` 写入数据库作为短期缓存。
3. Node 重启后，加入请求不能只依赖进程内 Map。
4. 如果缓存不存在，使用可信的 Steam Lobby 元数据恢复映射。
5. 如果 Lobby 已不存在，只拒绝新的 Lobby 加入，不影响 `pondId`。
6. 缓存必须有过期/失效处理，不能无限保留已经结束的 Lobby。

如果当前环境无法调用 Steam 服务端读取 Lobby 元数据，必须明确采用“服务端重启后 Lobby 失效、鱼塘仍有效”的降级策略，并返回稳定错误码，不得伪装成 Lobby 仍然可加入。

## 修复任务二：Lobby 成员真实性校验

当前 `/api/social/lobby/join` 不得只凭 lobbyId、JWT 和版本号放行。

实现以下任一种安全方案：

### 方案 A：Steam 成员校验

- 服务端验证当前 SteamID64 是否属于指定 Lobby。
- 验证 Lobby 的 AppID、版本和 `pondId` 元数据。
- 校验失败返回稳定错误码。

### 方案 B：短期邀请凭证

由服务端签发短期邀请凭证，至少绑定：

```text
steamId64
lobbyId
pondId
gameVersion
protocolVersion
expiresAt
nonce
```

加入时必须同时提供凭证，并由 Node 验证签名、过期时间、SteamID64 和 Lobby/pondId 一致性。

禁止：

- 信任客户端自报 SteamID64。
- 信任客户端自报 playerId。
- 只凭 lobbyId 直接进入鱼塘。
- 将 Lobby 房主当作永久鱼塘所有者。

## 修复任务三：鱼塘休眠与唤醒边界

核对并保证：

1. `tickAllPonds()` 只对存在真人在线会话的鱼塘执行高频 Tick。
2. 真人断开后，鱼塘不再执行高频生态 Tick。
3. 断线宽限期不能导致空鱼塘持续高频 Tick。
4. `join_pond`、地图入口、好友入口和新 Lobby 入口都调用统一的：

```text
ensurePondEcologyCurrent(pondId)
```

5. 补算和 `last_simulated_at` 更新在同一事务内。
6. 并发唤醒同一鱼塘只能有一次有效补算。
7. 补算失败时不得建立“已成功进入但生态未同步”的半成功状态。
8. Lobby 关闭、房主离开和 Lobby 过期不得删除 `pond_state` 或鱼塘业务数据。

## 修复任务四：测试

新增或完善服务端测试，至少覆盖：

### Lobby 生命周期

- 创建 Lobby 后能映射到正确 `pondId`。
- 房主离开 Lobby，鱼塘仍存在。
- 房主关闭 Lobby，鱼塘仍存在。
- Lobby 失效后，新的 Lobby 加入被拒绝。
- Lobby 失效后，已有鱼塘仍可通过地图/好友入口进入。
- Node 重启后，按选定策略恢复或明确拒绝 Lobby 加入。

### 成员与权限

- 合法 Lobby 成员可以加入。
- 非成员不能仅凭 lobbyId 加入。
- 错误 SteamID64/playerId 被拒绝。
- 错误 AppID、版本或协议被拒绝。
- 邀请凭证过期、重复使用或篡改被拒绝。

### 生态休眠与唤醒

- 有真人在线时高频 Tick 正常运行。
- 最后一个真人离开后高频 Tick 停止。
- 空闲期间 `last_simulated_at` 不被高频循环伪推进。
- 重新进入时按照离线时间补算。
- 同一唤醒时间重复调用不会重复补算。
- 并发唤醒只有一个调用产生有效离线补算。
- Lobby 关闭后重新进入仍能触发补算。

## 交付标准

- 不修改 Steam Lobby 作为临时社交入口的定位。
- 不把 Lobby 改造成玩家数据或鱼塘数据库。
- 不引入每鱼塘常驻进程。
- 不进入 Unity P3 场景、美术和复杂 UI 开发。
- 保留简化 UI，只修复生命周期和权限反馈。
- 运行：

```powershell
npm test -- --run
npm run verify:engineering
```

- 输出测试结果、重启恢复策略和未覆盖边界。
- 更新 `STEAM-DESKTOP-03` 需求文档验收项，但只有测试通过后才能标记“已实现”。

建议角色：`@backend-dev` 主责服务端，`@frontend-dev` 负责 Unity Lobby 状态和错误提示联调。
