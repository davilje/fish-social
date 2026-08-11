# BUG 修复：四项体验问题

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | BUG 修复 batch（社交弹窗 / 鱼获竞争 / 头像统一 / 管理入口） |
| 类型 | Bug 修复 |
| 负责人 | 产品 |
| 状态 | **已实现** |
| 目标版本 | v0.1.1 |
| 关联全景章节 | product/v0.1.0-功能全景.md §5.3、§5.7、§5.8、§5.10 |

---

## 1. 背景与目标

### 1.1 背景

测试与使用中反馈 4 个体验/逻辑问题，影响社交可信度、鱼塘公平性与操作一致性。

### 1.2 目标

- 修复好友 UI 关闭闪屏
- 保证同一条鱼塘实体鱼同一时刻只能被一个玩家 pending
- 全站头像点击统一为同一套资料弹窗
- 管理员工具入口更易发现

### 1.3 非目标

- 不重构社交系统或鱼塘生态
- 不新增 `/profile/[playerId]` 独立路由（仍用 `UserProfileModal`）
- 不改变管理员鉴权方式（仍用 `ADMIN_SECRET`）

---

## 2. BUG 清单

| ID | 标题 | 优先级 | 端 |
|----|------|--------|-----|
| BUG-1 | 加好友后关闭弹窗闪「添加好友」 | P0 | 客户端 |
| BUG-2 | 多用户同时钓上同一条鱼 | P0 | 服务端 |
| BUG-3 | 头像点击弹窗不统一 | P0 | 客户端 |
| BUG-4 | 管理员工具入口过深 | P1 | 客户端 |

---

## 3. BUG-1：加好友后关闭弹窗闪「添加好友」

### 3.1 现象

在 `UserProfileModal` 中成功添加好友（或机器人自动同意）后关闭弹窗，关闭动画过程中会**一瞬间**重新出现「添加好友」按钮，随后消失。

### 3.2 复现步骤

1. 打开社交中心或鱼塘，点击非好友用户头像打开资料弹窗  
2. 点击「添加好友」，等待成功提示（或机器人 instant 同意）  
3. 立即点击关闭或点击遮罩关闭  
4. 观察关闭 fade 动画期间底部操作区

### 3.3 根因分析（策划核对，供开发参考）

`UserProfileModal.tsx` 中 `useEffect` 在 `visible === false` 时**立即**执行：

```ts
setRequestSent(false);
```

同时父组件 `friendIds` 依赖 `onFriendAdded` → `loadFriends()` **异步**刷新，存在竞态：

- 关闭时 `visible` 已为 `false`，本地 `requestSent` 被清零  
- 父级 `friendIds` 可能尚未包含新好友  
- `isFriend === false` 且 `hasPendingOutgoing === false` → 短暂渲染「添加好友」按钮  
- Modal `animationType="fade"` 关闭过程中内容仍可见，用户看到闪屏

相关文件：

- `mobile/components/UserProfileModal.tsx`（L63–71 状态重置）
- `mobile/app/social.tsx`、`mobile/app/pond/[id].tsx`（`onFriendAdded` 回调）

### 3.4 修复要求

任选一种或组合，**以关闭过程中不再闪「添加好友」为准**：

1. **延迟重置**：在 `onModalHide` / 动画结束后再清空 `requestSent` 等本地状态，而非 `visible` 变 false 时立刻清空  
2. **乐观更新**：`onFriendAdded` 时父组件**同步**将 `playerId` 并入 `friendIds`（或 outgoing 移除），再异步 `loadFriends` 校正  
3. **关闭期冻结 UI**：`visible === false` 时不再渲染好友操作区（或整卡只显示空白/骨架）  
4. **成功态保持**：若本次会话已添加成功，在弹窗完全卸载前始终显示「已是好友」或成功文案，不回落到按钮

### 3.5 验收标准

- [x] 添加好友成功后立即关闭弹窗，全程不出现「添加好友」按钮闪回  
- [x] 机器人自动同意场景同样通过  
- [x] 再次打开该用户弹窗，显示「已是好友」  
- [x] 添加失败、仅发送申请待同意场景，关闭时不误显示「已是好友」

---

## 4. BUG-2：多用户同时钓上同一条鱼

### 4.1 现象

同一鱼塘内，当 A 用户触发 `fish_bite` 后，B 用户也可能在相近时刻对**同一条** `pond_fish` 实体收到上钩弹窗。先领取者成功，后者领取时报「这条鱼已被他人钓走」，但**错误弹窗已展示**，体验差且不符合「一条鱼只能被一人钓」的预期。

### 4.2 复现步骤

1. 两账号同时进入同一鱼塘并「开始钓鱼」  
2. 等待多次 5 秒 bite 检测（可提高鱼塘鱼密度或临时调概率辅助测试）  
3. 观察是否出现两人几乎同时对同种同尺寸鱼上钩  
4. 一人点「获得」成功，另一人点「获得」失败

### 4.3 根因分析

`server/src/inventory.ts` → `rollPendingCatch`：

- `pickFishForBite` 从 DB 选鱼后，仅写入 **按 userId** 的 `pendingByUser`  
- **未**在鱼塘维度锁定 `pondFishId`  
- 同一 tick 或相邻 tick 内，多用户 `pickFishForBite` 可返回相同 `fish.id`  
- 鱼实体仅在 `acceptCatch` → `removePondFish` 时删除，pending 阶段可被多人引用

相关文件：

- `server/src/inventory.ts`（`rollPendingCatch`、`acceptCatch`、`clearPendingCatch`）
- `server/src/index.ts`（bite 定时器，L211–236）
- `server/src/pondEcology.ts`（`pickFishForBite`、`removePondFish`）

### 4.4 修复要求

**核心原则**：从 `fish_bite` 发出时刻起，该 `pondFishId` 不得再分配给其他玩家，直到 pending 释放或成功入背包。

建议实现（开发可调整细节）：

1. 增加全局（或按塘）锁：`pondFishId → userId`（或 `pendingByPondFishId`）  
2. `rollPendingCatch`：选鱼后若 `pondFishId` 已被他人 pending，**重选或跳过**本次 bite  
3. `clearPendingCatch`（离开鱼塘、超时、领取失败清理）：释放对应 `pondFishId` 锁  
4. `acceptCatch` 成功/失败后：释放锁；成功时 `removePondFish` 逻辑保持不变  
5. **可选**：pending 超时（如与客户端弹窗 5 秒对齐 + 缓冲）自动 `clearPendingCatch`，避免鱼永久锁死  
6. 机器人 `handleBotCatch`（`bots.ts`）直接 `removePondFish`，需与锁机制一致，避免与玩家 pending 冲突

### 4.5 验收标准

- [x] 两用户同时钓鱼，任意时刻不会对同一 `pondFishId` 各发一次 `fish_bite`  
- [x] 先领取者入背包，鱼塘实体删除  
- [x] 后触发者不应收到该鱼的 bite（应重选其他鱼或本次无 bite）  
- [x] 用户离开鱼塘或 pending 清理后，该鱼可再次被钓（若仍在塘中）  
- [x] 机器人钓鱼不与玩家 pending 产生双花

---

## 5. BUG-3：个人头像点击弹窗未统一

### 5.1 现象

不同页面点击头像行为不一致，用户无法形成统一心智。

### 5.2 现状对照

| 入口 | 当前行为 | 文件 |
|------|----------|------|
| 世界地图顶栏 · 自己的头像 | `router.push('/profile')` 编辑页 | `mobile/app/index.tsx` L58 |
| 社交中心 · 动态/好友列表头像 | `UserProfileModal` | `mobile/app/social.tsx` |
| 鱼塘 · 在线用户列表头像 | `UserProfileModal` | `mobile/app/pond/[id].tsx` + `OnlineUsersPanel` |
| 鱼塘 · 场景内角色头像 | **不可点击** | `PondCharacter.tsx` |
| 鱼塘顶栏 | **无**个人头像入口 | `pond/[id].tsx` AppHeader |
| 点击自己（在线列表） | `UserProfileModal` + 文案「请前往个人信息编辑」 | `UserProfileModal` isSelf 分支 |

### 5.3 目标行为（统一规范）

**所有可点击头像**（含地图顶栏自己、在线列表、动态 PostCard、鱼塘场景角色）均打开 **`UserProfileModal`**，展示与 [他人主页优化](./他人主页优化.md) 一致的资料内容。

| 对象 | 弹窗内容 |
|------|----------|
| **他人** | 简介 + 收藏品 + 动态 + 好友操作（现有逻辑） |
| **自己** | 同样展示公开资料（简介/收藏品/动态）；底部增加主按钮 **「编辑资料」** → `router.push('/profile')`；**不再**仅显示一句提示文案 |

### 5.4 实现建议

1. 抽取共用 hook 或小组件，例如 `useProfileModal()` + 顶层挂载单一 `UserProfileModal`，避免各页重复 state  
2. 地图页 `index.tsx`：顶栏头像改为打开 Modal（自己），非跳转 `/profile`  
3. `PondScene` / `PondCharacter`：头像或角色可点击，传入 `playerId` 打开 Modal（无 playerId 的演示用户保持不可点或提示）  
4. 鱼塘 `AppHeader`：可选增加与地图一致的小头像入口（与 BUG-4 一并考虑顶栏右侧布局）  
5. `/profile` 编辑页保留，仅作为 Modal 内「编辑资料」目的地  

### 5.5 验收标准

- [x] 地图顶栏点头像 → `UserProfileModal`（自己），可点「编辑资料」进 `/profile`  
- [x] 社交、鱼塘在线列表、动态卡片点头像 → 同一 Modal 组件、同一布局  
- [x] 鱼塘场景内点击他人角色/头像 → 打开其资料 Modal  
- [x] 自己 vs 他人展示逻辑符合上表，无页面单独 fork 一套 UI  

---

## 6. BUG-4：Debug 管理员工具入口过深

### 6.1 现象

管理页 `/admin` 入口仅在 **社交中心 → 设置 Tab 最底部**「🛠 Debug / 管理员工具」，路径：`/social` → 设置 → 链接，过深，调试/运营不便。

### 6.2 现状

- `mobile/app/social.tsx` L419–421：`router.push('/admin')`  
- `mobile/app/admin.tsx`：管理功能本身完整  

### 6.3 修复要求

**主入口调整**（P1 必做）：

1. 在 **世界地图顶栏**（`index.tsx`）右侧区域增加管理入口：建议齿轮图标或「Debug」文字按钮，与背包、社交并列或收纳在次要位置  
2. **保留**社交设置页底部链接作为备用入口（或改为弱样式「高级 / 管理」），避免唯一入口消失  

**可选**（P2，时间紧可不做）：

- 个人中心 `/profile` 底部增加「开发者工具」链接  

**展示说明**：

- 入口无需隐藏；点击仍进入现有 `/admin` 页（输入 Admin Key）  
- 生产包若需隐藏，可后续用环境变量控制（**本期不强制**）

### 6.4 验收标准

- [x] 从世界地图进入 `/admin` ≤ 2 次点击（例如：地图 → Debug 按钮 → 输入密钥）  
- [x] 社交设置内仍可进入管理页（备用）  
- [x] 不改变 admin API 鉴权逻辑  

---

## 7. 技术影响汇总

### 7.1 涉及文件（预估）

| BUG | 文件 |
|-----|------|
| BUG-1 | `mobile/components/UserProfileModal.tsx`，`mobile/app/social.tsx`，`mobile/app/pond/[id].tsx` |
| BUG-2 | `server/src/inventory.ts`，`server/src/index.ts`，可选 `server/src/bots.ts` |
| BUG-3 | `mobile/app/index.tsx`，`mobile/app/social.tsx`，`mobile/app/pond/[id].tsx`，`mobile/components/PondCharacter.tsx` 或 `PondScene.tsx`，新建 `useProfileModal`（建议） |
| BUG-4 | `mobile/app/index.tsx`，`mobile/app/social.tsx`（弱化原入口样式） |

### 7.2 API / 数据模型

- BUG-2：**无新 REST**；服务端内存增加 `pondFishId` 级锁  
- 其余：仅客户端 UI/状态，无 API 变更  

---

## 8. 测试计划

| 用例 | BUG |
|------|-----|
| 加好友后立即关弹窗 | BUG-1 |
| 双开浏览器同塘钓鱼 5 分钟 | BUG-2 |
| 逐页点头像对比 UI | BUG-3 |
| 从地图 1 点击进入 admin | BUG-4 |

---

## 9. 风险

| 风险 | 缓解 |
|------|------|
| BUG-2 锁未释放导致鱼不可用 | pending 超时 + leave_pond 清理 |
| BUG-3 重构 Modal 状态影响多页 | 抽 hook 单例挂载 |

---

## 10. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-06-30 | 策划 | 初稿，四项 BUG，状态已确认 |
| 2026-06-30 | 开发 | 四项 BUG 全部实现，状态→已实现 |
