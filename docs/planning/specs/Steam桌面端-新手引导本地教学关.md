# STEAM-DESKTOP-11：新手引导本地教学关

## 元信息

| 字段 | 内容 |
|------|------|
| 编号 | `STEAM-DESKTOP-11` |
| 类型 | 功能 |
| 状态 | **已实现** |
| 设计时间 | **2026-08-21** |
| 完成时间 | **2026-08-21** |
| 目标版本 | v0.7 |
| 优先级 | P0 |
| 依赖 | FEAT-PROG-01 完成态 API、STEAM-DESKTOP-07G Overlay IPC |
| 关联 | [`鱼塘分级与玩家成长.md`](./鱼塘分级与玩家成长.md) §3.4 |

---

## 1. 背景与目标

### 1.1 背景

首版把新手关做成真实 `join pond-novice` + 服务端钓鱼状态机。结果上钩间隔、咬钩概率、收杆时长都跟正式塘走（约 60 秒检测 + 生态 RNG + 按体长算圆圈）。引导文案写「约 5 秒必上钩」，体验对不上；还被饵、扣费、自动续钓、离塘卡住。

`NOVICE_WAIT_BITE_MS` / `NOVICE_HOOK_MS` 仅常量，未接入咬钩循环。

### 1.2 目标

- Overlay 教学关 **纯前端状态机**：坐下 → 开始钓鱼 → 装饵/抛竿 → **5 秒必上钩** → **5 秒圆圈** → 收鱼 → **自动入包并弹窗**（对齐 Web `CatchFishModal`）。
- 引导气泡贴在自己宠物上方并随换位/离席移动；**不**在 Overlay 顶部再叠一条相同文案；主窗口不重复贴引导条。
- 引导中锁定世界地图等功能栏（含 Overlay 菜单与主窗口导航）；仅设置可进（调试重置）。完成后解锁。
- **不** `join pond-novice`。服务端只权威保存 `onboardingCompleted`；收鱼后 `POST /api/progress/complete-onboarding`（发入门鲫鱼）。
- 鱼获弹窗点「获得」或 5 秒自动关闭 → 再出「新手引导完成」确认 → 点确认关 Overlay、回世界地图。

### 1.3 非目标

- 不改正式塘咬钩间隔、概率、收杆曲线。
- 不发入门竿（GEAR-01）。
- Overlay 不直连 Socket；教学关无公屏聊天、无其他玩家。
- Mobile / Web 引导对齐。
- 不把 `pond-novice` 从准入表删除（完成后再 join 仍拒绝）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 新用户 | Steam 登录未完成引导 | 强制 Overlay 教学关；phase 显示装饵/抛竿/等待/上钩/收鱼 |
| 引导中 | 换钓位 / 离席 | 引导气泡跟着角色走，不残留在旧坐标 |
| 引导中 | 点「收杆」 | 跑鱼，回到已落座，提示不要收杆 |
| 引导中 | 世界地图 / Overlay 功能菜单 | 禁用，提示先完成引导 |
| 引导完成 | 鱼获弹窗确认或 5 秒自动关 | 再出完成确认；点确认后关 Overlay、回世界地图 |
| 开发 | 设置「重置新手引导」 | 清完成态并重播本地五步 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 本地教学状态机 | P0 | idle→seated→baiting→casting→waiting(5s)→hooked(5s)→resolving→自动入包 |
| 2 | Overlay 合成快照 | P0 | 本地钓位 + 自己宠物；`connectionState=Connected`；无 `exit_pond` / `accept_catch` |
| 3 | Overlay 命令拦截 | P0 | `take_spot` / `start_fishing` / `stop_fishing` / `leave_spot` / `confirm_overlay_prompt` |
| 4 | 完成接口 | P0 | 收鱼后 `POST /api/progress/complete-onboarding`；首次发灰鲫 |
| 5 | 鱼获/完成弹窗 | P0 | 对齐 Web：自动入包 + 提示窗；确认或 5 秒关闭后再出完成确认 |
| 6 | 功能栏锁定 | P0 | 引导中禁用世界地图等导航；设置除外 |
| 7 | 重置 | P0 | 设置「重置新手引导」+ `POST /api/progress/reset-onboarding` |

### 3.2 交互与 UI

1. 左键空钓位坐下（引导气泡在角色上方，换位/离席跟随）。
2. 右下「开始钓鱼」→ 状态依次为装饵 / 抛竿 / 等待。
3. 等待约 5 秒必上钩。
4. 上钩圆圈约 5 秒；**不要点「收杆」**（收杆=跑鱼）。
5. 收鱼后 **自动入包**，弹出鱼获窗（鲫鱼/灰）；点「获得」或 5 秒自动关闭。
6. 弹出「新手引导已完成」；点「确认」关闭 Overlay，解锁功能栏并回世界地图。

引导中：主窗口与 Overlay 功能菜单禁用（设置可进）；隐藏 Overlay 无效。聊天发送提示「教学关暂不支持」。Overlay 顶部不再重复贴引导条。角色状态文案按 phase：装饵 / 抛竿 / 等待 / 上钩 / 收鱼，不得一律显示「钓鱼」。

### 3.3 规则与数值

| 项 | 值 |
|----|----|
| 等待上钩 | 5000 ms，成功率 100% |
| 收杆窗口 | 5000 ms，窗口走完必收成 |
| 入门鱼获 | `crucian` / gray / 约 0.18m，记 `pond-novice` |
| 正式塘 | 仍用 `FISH_BITE_CHECK_MS` + `rollBiteHook`，本票不改 |

---

## 4. 技术影响

### 4.1 数据模型

无新表。沿用 `player_fishing_progress.onboarding_completed`。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/progress/me` | 登录后是否开教学关 |
| REST | `POST /api/progress/complete-onboarding` | 领取时打完成；首次发入门鱼 |
| REST | `POST /api/progress/reset-onboarding` | 调试重置 |
| Socket | `join_pond` / `start_fishing` / `accept_catch` | **教学关不调用** |

### 4.3 涉及路径

- `fish-social-unity/Assets/Scripts/Desktop/Onboarding/`
- `fish-social-unity/Assets/Scripts/Desktop/OverlayPondStateBuilder.cs`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- `server/src/socialRoutes.ts`（完成时发鱼，不改咬钩循环）
- Overlay：`guideTip` 仅角色头顶气泡；新增 `lockFeatureNav`、`overlayPromptKind/Title/Body/DeadlineMs`；命令 `confirm_overlay_prompt`

---

## 5. 验收标准

- [x] 未完成引导的账号登录后进 Overlay 教学关，**不**出现正式塘那种长时间空等 / 随机 miss
- [x] 坐下 → 开始钓鱼后经历装饵/抛竿/等待约 5 秒上钩，圆圈约 5 秒；**无需点领取鱼获**
- [x] 收鱼后自动入包并出鱼获窗；确认或 5 秒后出「引导完成」；点确认关 Overlay 回世界地图
- [x] 换钓位/离席时引导气泡跟角色走；Overlay 顶部与 Unity 主窗口无重复引导条
- [x] Overlay 状态胶囊与角色短文案按 phase 显示装饵/抛竿/等待/上钩/收鱼
- [x] 引导中世界地图等功能栏（主窗口导航 + Overlay 菜单）禁用；完成后解锁
- [x] 上钩期间点「收杆」跑鱼，可重新开始钓鱼；点退出鱼塘不能跳过
- [x] 完成后 `onboardingCompleted=true`，再 join `pond-novice` 被拒；可进已解锁公开塘
- [x] 公开塘领取后仍自动续钓（skip-casting 不变）
- [x] 设置「重置新手引导」可重播本地教学关

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| Overlay 在 `Disconnected` 时按钮不可用 | 教学关强制 `connectionState=Connected` |
| 主窗口仍可点地图进塘 | 引导中锁定功能栏 + `AllowedPondIdOnly=pond-novice` 且不发起 join |

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-21 | 策划 | 初稿：教学关改本地前端，服务端只保留完成态 |
| 2026-08-21 | 策划 | 气泡跟随、去掉顶栏重复文案、锁功能栏、Web 式自动入包弹窗、phase 文案 |
| 2026-08-21 | 策划 | 用户验收通过，状态改为已实现 |
