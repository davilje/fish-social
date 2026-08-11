# BUG 修复：切页误离塘与钓鱼计时中断

| 状态 | **已实现** | 目标版本 v0.5.1 |
|------|------------|-----------------|
| 优先级 | P0 | 挂机 / 钓鱼时长体验阻断 |
| 范围 | **客户端为主**；服务端复用既有 disconnect 宽限与 checkpoint，无新 API |
| 前置 | [`BUG修复-挂机断线离位.md`](./BUG修复-挂机断线离位.md)（**已实现**）· [`服务器架构优化路线图-v0.5.md`](./服务器架构优化路线图-v0.5.md)（一期 checkpoint） |
| 需求表 | [`../reports/切页误离塘修复方案-v0.5.1.xlsx`](../reports/切页误离塘修复方案-v0.5.1.xlsx) |
| 关联 | [`排查-挂机断线诊断阶段2-4.md`](./排查-挂机断线诊断阶段2-4.md) §阶段3（leave 归因，**需修订**） |

---

## 0. 文档目的

玩家反馈：在鱼塘页点击右上角 **背包 / 商店 / 图鉴 / Debug / 社交** 等入口后，头顶钓鱼计时会停、表现为「断线/离塘」。

本 BUG 根因是 **客户端把「页面跳转」当成「主动离塘」**，与服务端 60s disconnect 宽限设计冲突。本文档定义：

1. 何时应 `leave_pond`（显式离塘）
2. 何时仅断开 Socket、走 `disconnect` + 重连恢复
3. 客户端 / 验收 / 文档修订范围

---

## 1. 问题现象

| 用户操作 | 当前行为 | 期望行为 |
|----------|----------|----------|
| 返回地图 | `leave_pond(navigation_back)` | **保持**：显式离塘 |
| 打开社交页 | `leave_pond(navigation_social)` | **不**离塘；Socket 可断，走宽限重连 |
| 编辑资料（从鱼塘） | `leave_pond(navigation_profile)` | **不**离塘 |
| 打开 Debug `/admin` | 卸载时 `leave_pond(unmount)` | **不**离塘 |
| 背包 / 商店 / 图鉴（Modal） | 同页 Modal，无 leave | 计时继续；若仍停需单独排查 UI |
| 鱼塘页组件卸载 | cleanup 默认 `leave_pond(unmount)` | **禁止**默认 leave |

**日志证据**（用户现场）：

```text
leave_pond reason=navigation_back
join_pond joinKind=fresh  // 再进塘为新 userId，非 reconnect
```

---

## 2. 根因分析

### 2.1 显式离塘过宽（P0）

`mobile/app/pond/[id].tsx`：

- `handleLeaveToSocial` → `leavePondWithReason('navigation_social')` 后 `router.push('/social')`
- `onEditProfile` → `leavePondWithReason('navigation_profile')`
- `handleLeaveToMap` → `leavePondWithReason('navigation_back')`（**正确，保留**）

阶段 2–4 诊断文档曾将「去社交 = 离塘」标为符合设计；**产品行为已变更**，以本文档为准。

### 2.2 卸载默认离塘（P0）

`mobile/lib/usePondSocket.ts` cleanup：

```typescript
if (!leftPondRef.current) {
  emitLeavePond('unmount', activePondId);
}
socket.disconnect();
```

任意路由离开鱼塘页（含 Debug）都会 `leave_pond(unmount)`，服务端立即清会话与钓位，计时终止。

### 2.3 Socket 断开 vs 离塘（设计澄清）

| 事件 | 服务端语义 | 钓位 / 计时 |
|------|------------|-------------|
| `leave_pond` | 主动离塘 | 立即释放 spot，结束会话计时逻辑 |
| `socket disconnect` | 断线宽限 60s | 保持 `disconnected` phase，可 `join_pond` reconnect 恢复 |

**原则**：切页导航 **不得** 先发 `leave_pond`；若 Socket 因页面卸载断开，依赖服务端宽限 + 回塘 `reconnect`。

### 2.4 背包 / 商店 / 图鉴（Modal）

三者为同页 `Modal`，不触发路由卸载，**不应**产生 `leave_pond`。若计时仍停：

1. 查 `pond_user_updated` 是否仍在收（服务端 1s 广播）
2. 查 `PondCharacter` 是否在 Modal 打开时仍挂载

不在本期必改范围，列为 **F1 跟进项**。

---

## 3. 优化方案

### 3.1 离塘语义（产品决策）

**仅以下场景发送 `leave_pond`：**

| 场景 | reason | 说明 |
|------|--------|------|
| 用户点击「← 地图」 | `navigation_back` | 明确离开鱼塘 |
| 用户点击「收起鱼竿」后离塘（若有） | `user_stop` / 现有 stop 流程 | 按状态机 |
| 认证失效被踢回登录 | `auth_redirect` | 保留 |
| 用户确认「离开鱼塘」类按钮（若有） | `user_explicit` | 未来扩展 |

**以下场景禁止 `leave_pond`：**

- 打开社交 / Debug / 个人资料页
- 鱼塘页 React 卸载（`unmount`）
- 打开背包 / 商店 / 图鉴 Modal

`navigation_social` / `navigation_profile` 保留在类型中供**历史 metrics 只读**，新代码不再写入。

### 3.2 客户端改动（P0）

#### C1 `usePondSocket.ts`

1. **删除** cleanup 中 `emitLeavePond('unmount')`
2. cleanup 仅 `socket.disconnect()`（不断开宽限前的服务端状态）
3. 导出 `leavePondExplicit(reason)`，仅给「返回地图」等显式入口调用
4. 可选：`disconnectSocketOnly()` 用于调试，默认 cleanup 即可

#### C2 `pond/[id].tsx`

1. **删除** `handleLeaveToSocial` 内的 `leavePondWithReason`
2. **删除** `onEditProfile` 内的 `leavePondWithReason`
3. **保留** `handleLeaveToMap` → `leavePondWithReason('navigation_back')`
4. Social / Admin：`router.push` 前 **不** leave

#### C3 回塘恢复

1. 从社交/Debug/地图再进入同一鱼塘：`join_pond` 应走 **reconnect**（同 `playerId`，60s 内）
2. 验收：`joinKind: 'reconnect'`，`spotId` / `fishingPhase` / `sessionFishingMs` 连续

#### C4（P1 可选）全局 Pond Socket

若仅去掉 `leave_pond` 仍因 `socket.disconnect` 导致计时短暂停顿，可二期引入 `PondSocketProvider`（App 级单例 Socket，路由切换不断连）。**本期不强制**。

### 3.3 服务端改动（P0 验证为主）

- **无新接口**；确认现有路径：
  - `disconnect` → `handleDisconnect` → 60s timer
  - `join_pond` + `findDisconnectedUserByPlayerId` → reconnect
  - checkpoint（v0.5）在宽限内可恢复 spot/phase
- 若 reconnect 失败，查 `player_pond_session` 与 `restoreDisconnectedUser`

### 3.4 文档与埋点修订

| 文档 | 修订 |
|------|------|
| `排查-挂机断线诊断阶段2-4.md` §阶段3 | 社交/Debug 跳转 **不再** 预期 `leave_pond` |
| `挂机断线排查-v0.4.3.md` | 更新 leave 归因说明 |
| `verify-afk-diag.ts` | 用例改为：切社交 **无** leave，回塘 reconnect |

---

## 4. 需求条目（与 xlsx 对应）

| 编号 | 标题 | 优先级 |
|------|------|--------|
| L1 | 移除社交/资料跳转前 leave_pond | P0 |
| L2 | 移除 usePondSocket unmount 默认 leave | P0 |
| L3 | 仅返回地图显式 leave_pond | P0 |
| L4 | 切页断 Socket 依赖 disconnect 宽限 | P0 |
| L5 | 回塘 reconnect 与计时连续验收 | P0 |
| L6 | 修订诊断文档与 verify 脚本 | P1 |
| F1 | Modal 计时仍停时 UI/广播排查 | P2 |

详见 xlsx 各 sheet。

---

## 5. 测试计划

| 用例 | 步骤 | 预期 |
|------|------|------|
| T1 返回地图 | 钓鱼中点「← 地图」 | `leave_pond(navigation_back)`；离塘 |
| T2 打开社交 | 钓鱼中点社交 | **无** leave；可有 disconnect；spot 保留 |
| T3 社交回塘 | 60s 内再进同一塘 | `joinKind=reconnect`；计时连续 |
| T4 Debug | 钓鱼中进 Debug 再返回 | 无 leave；reconnect 或计时恢复 |
| T5 背包 Modal | 打开背包 | 无 leave；计时继续 |
| T6 宽限外 | 断开后 >60s 再进 | 新 session 可接受；spot 释放 |

**命令**：

- `npm run verify:disconnect-reconnect`
- `npm run verify:session-checkpoint`
- 更新后 `npm run verify:afk-diag`

---

## 6. 开发交接

客户端提示词：[`pond-navigation-leave-fix-dev.prompt.md`](../prompts/pond-navigation-leave-fix-dev.prompt.md)

---

## 7. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-07-09 | **已实现**：L1–L6 客户端仅返回地图显式 leave；社交/资料/unmount 不再 leave；修订诊断文档与 verify |
| 2026-07-09 | 初稿：切页误离塘方案与 xlsx 需求表 |
