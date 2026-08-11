<!-- 来源: docs/planning/specs/BUG修复-切页误离塘与计时中断.md -->
<!-- 用途: 客户端 — 离塘显式化 + 切页不断会话 -->

你是 Fish Social **移动端开发 Agent**。修复「切页误离塘导致钓鱼计时中断」。

## 必读

1. `docs/planning/specs/BUG修复-切页误离塘与计时中断.md`
2. `docs/planning/reports/切页误离塘修复方案-v0.5.1.xlsx`（L1–L6）
3. `mobile/lib/usePondSocket.ts` · `mobile/app/pond/[id].tsx`

## 背景

当前在打开社交/资料或页面卸载时会 `leave_pond`，服务端清塘内状态，计时停止。  
**目标**：仅用户点击「← 地图」时 `leave_pond`；其他导航仅 `socket.disconnect()`，依赖服务端 60s 宽限 + reconnect。

---

## 任务 1（P0）L1 — 社交 / 资料不再 leave

`mobile/app/pond/[id].tsx`：

- `handleLeaveToSocial`：**删除** `leavePondWithReason('navigation_social')`，只 `router.push('/social')`
- `onEditProfile`：**删除** `leavePondWithReason('navigation_profile')`，只 `router.push('/profile')`
- **保留** `handleLeaveToMap` 中的 `leavePondWithReason('navigation_back')`

---

## 任务 2（P0）L2 — unmount 不 leave

`mobile/lib/usePondSocket.ts` cleanup：

```typescript
// 删除：
// if (!leftPondRef.current) {
//   emitLeavePond('unmount', activePondId);
// }
// 保留：
socket.disconnect();
```

`leftPondRef` 仅在显式 `leavePondWithReason` 成功 emit 后置 `true`。

---

## 任务 3（P0）L3/L4/L5 — 验收

手测 + 日志：

| 操作 | 预期 |
|------|------|
| 钓鱼中 → 社交 → 60s 内回塘 | 无 `leave_pond`；回塘 `joinKind=reconnect`；计时连续 |
| 钓鱼中 → Debug → 返回 | 无 `leave_pond` |
| 钓鱼中 → ← 地图 | 有 `leave_pond(navigation_back)` |
| 打开背包 Modal | 无 leave；计时继续 |

运行（服务端应已具备宽限）：

```bash
npm run verify:disconnect-reconnect
npm run verify:session-checkpoint
```

---

## 任务 4（P1）L6 — 文档与 verify

1. 更新 `docs/planning/specs/排查-挂机断线诊断阶段2-4.md`：社交/Debug 不再预期 `leave_pond`
2. 调整 `scripts/verify-afk-diag.ts` 中与 `navigation_social` leave 相关的断言
3. 可选：新增 `scripts/verify-pond-navigation.ts` 轻量用例

---

## 不改

- 不实现全局 PondSocketProvider（F1/P1 可选二期）
- 不改服务端 leave/disconnect 核心逻辑（除非 reconnect 回归失败需修 bug）

---

## 完成后

1. 更新 spec 状态 → **已实现**
2. 更新 xlsx L1–L6 状态 → **已实现**
3. 补 `CHANGELOG.md`
4. 附手测步骤与关键日志片段

## commit 建议

```text
fix(mobile): explicit leave_pond only when exiting pond to map

Stop leaving pond on social/profile navigation and on hook unmount;
rely on server disconnect grace and reconnect for session continuity.
```
