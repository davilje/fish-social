# Steam 桌面 Overlay：中央钓鱼栏按钮时序

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 中央底部：开始钓鱼 / 收杆 / 打窝 / 离席 显隐与点击时序 |
| 编号 | **STEAM-DESKTOP-15** |
| 类型 | **功能**（含 Overlay 显隐回归 + 打窝中离席门禁） |
| 负责人 | Unity 桌面 / Overlay 工程师；离席放行需服务端 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-31** |
| 完成时间 | **2026-08-31** |
| 上位需求 | `STEAM-DESKTOP-08G`、`STEAM-DESKTOP-13C`、`FEAT-GROUND-01` |
| 关联全景 | Overlay 钓鱼操作栏；打窝并列循环 |

---

## 1. 背景与目标

### 1.1 现象与复现

中央底部三钮：`btn_fishing_toggle`（开始钓鱼 ↔ 收杆）、`btn_groundbait`（打窝 n/50）、`btn_catch_leave`（领取鱼获 / 离席）。

| # | 步骤 | 预期 | 现状 |
|---|------|------|------|
| A | 落座、未打窝，点「开始钓鱼」 | 主按钮变为「收杆」；打窝、离席隐藏 | 主按钮从「开始钓鱼」切走（看起来像消失），打窝/离席隐藏。可接受为旧表现 |
| B | 打窝完成（stack≥1）后再点「开始钓鱼」 | 与 A 相同：打窝钮应一并隐藏 | **打窝钮仍在**（因 Overlay 用 `stack > 0` 保活可见） |
| C | 钓鱼中点「收杆」 | 收杆钮保留；等停钓/收杆相位结束，文案改回「开始钓鱼」，**同时**出现打窝与离席 | **收杆立刻消失**；打窝、离席在 `stopping` 中间态就提前出现（Unity：`leave_spot` 在 `!CanStopFishing` 时放出；`stopping` 不在 `CanStopFishing`） |
| D | 点「打窝」等待 `castDurationMs` | 「开始钓鱼」仍在，点击无反应；等打窝结束才可开钓 | 无 `start_fishing` 时 Overlay **整颗收掉**主按钮 |
| E | 打窝进行中点「离席」 | 立刻退出打窝并离席，不必等撒窝结束 | 服务端 `leave_spot` **不放行** `groundbaiting`（仅 `seated` / `stopping` / `resolving`） |

可稳定复现。平台：Steam 桌面 Overlay + Unity + Node。本需求以 **phase 驱动 HUD**，不把责任判给网络或帧率。

### 1.2 时间戳与分层（Bug 专项）

本条是 **显隐状态机 / 门禁**，不是延迟投诉。实现与验收按 `fishingPhase` + `availableActions` 对齐即可。

| 层 | 判断 |
|----|------|
| Overlay UI | **已定位**：`ApplyFishingControls` / `ApplyGroundbaitStatus` 用 actions + `stack>0` 决定可见，未按「空闲坐席 vs 钓鱼/停钓」分组 |
| Unity 动作表 | **已定位**：`MapAvailableActions` 在 `hasSpot && !CanStopFishing` 时给 `leave_spot`，把 `stopping`、`groundbaiting` 与 `seated` 混在一起 |
| 服务端 | **已定位（E）**：`leaveSpot` 拒绝 `groundbaiting`。A–C 不要求改数值或 `PHASE_MS.stopping`（现 200ms） |
| Named Pipe / Socket 耗时 | 非本需求根因 |

若联调需要，节点仍用 UTC ms + `commandId`：`Overlay command_sent` → `Unity overlay_command_received` → Socket → `phase` 回传 → Overlay 刷新。禁止无时间戳断言「服务器慢」。

### 1.3 目标

- **主按钮不卸控件**：有座期间 `btn_fishing_toggle` 保持可见；只改文案「开始钓鱼」↔「收杆」和是否响应点击。
- **打窝、离席只在空闲坐席成对出现**：开钓后隐藏；点收杆后 **等到停钓事务结束**（phase 回到 `seated`，或出现待领鱼获）再同时出现。
- **打窝等待中**：主按钮仍显示「开始钓鱼」，点击无反应；「离席」可立即打断打窝并离席。
- 覆盖 `FEAT-GROUND-01` 原文「打窝不可取消、等完」：**仅离席可打断**；开始钓鱼仍不可在 `groundbaiting` 发出。

### 1.4 非目标

- 不改打窝扣金、`castDurationMs`、50 层、加成公式、13C 按钮次数文案与成功句。
- 不改 `PHASE_MS.stopping` / `resolving*` 毫秒（除非另开数值单）。
- 不改 Prefab 矩形、14C 字体对齐、ART-01。
- 不做「取消打窝但留座」第二按钮。
- 不改 `mobile/` 主路径（本单桌面 Overlay + Unity IPC + 必要的 `leave_spot` 门禁）。
- 不新增埋点（沿用既有 `spot_release` / 打窝事件）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 落座空闲 | 三钮：开始钓鱼、打窝 n/50、离席（有待领鱼获则第三钮为领取鱼获） |
| 玩家 | 点开始钓鱼（无论是否打过窝） | 主按钮文案变「收杆」且可点；打窝、离席同时隐藏 |
| 玩家 | 点打窝 | 主按钮仍为「开始钓鱼」，点了无反应；打窝钮不可再开一层；离席可点 |
| 玩家 | 打窝计时结束 | 回到空闲坐席：开始钓鱼可点；打窝可再点（未满 50） |
| 玩家 | 打窝中点离席 | 立刻离席；未完成层不叠加；已扣金币按既有打窝规则不退 |
| 玩家 | 钓鱼中点收杆 | 主按钮保持「收杆」，点击无第二次 `stop_fishing`；打窝/离席仍隐 |
| 玩家 | 收杆/结算相位结束 | 主按钮文案改为「开始钓鱼」；**同一帧**打窝与离席（或领取鱼获）出现 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 主按钮常驻 | P0 | 有 `spotId` 时不 `Collapsed`；无座整组钓鱼栏按 08G 隐藏 |
| 2 | 按 phase 显隐 | P0 | 见 §3.2 表，禁止再用 `stack>0` 单独保活打窝钮 |
| 3 | 收杆后等 phase | P0 | `stopping` / `resolving` 期间不放出打窝、离席 |
| 4 | 打窝中主按钮 | P0 | 可见、禁用或吞点击，不发 `start_fishing` |
| 5 | 打窝中离席 | P0 | Overlay 显示离席；Unity 下发 `leave_spot`；服务端放行 `groundbaiting` 并取消未完成 cast |

### 3.2 交互与 UI（相位表）

权威字段：IPC `fishingPhase`。`availableActions` 必须与下表一致，Overlay 不得在 `stopping` 因「没有 start/stop」而卸掉主按钮。

| `fishingPhase` | 主按钮文案 | 主按钮点击 | 打窝 | 第三钮 |
|----------------|------------|------------|------|--------|
| `seated` | 开始钓鱼 | 发 `start_fishing` | 显示；可点当有 `groundbait_start` | 有待领 → 领取鱼获；否则离席 |
| `groundbaiting` | 开始钓鱼 | **无反应**（不发命令、不提示也可） | 显示；不可再点 | **离席**（可点） |
| `baiting` / `casting` / `waiting` / `hooked` | 收杆 | 发 `stop_fishing` | 隐藏 | 隐藏 |
| `resolving` | 收杆 | 无反应（不重复停钓） | 隐藏 | 隐藏 |
| `stopping` | 收杆 | 无反应 | 隐藏 | 隐藏 |
| `idle` 或无座 | 隐藏 | — | 隐藏 | 隐藏（退出鱼塘仍走既有入口） |

**同一帧规则：** `stopping`/`resolving` → `seated`（或 seated + pending catch）时：主按钮文案切换与打窝、第三钮出现必须同一次 HUD 刷新，禁止先闪空一帧再出侧钮。

**乐观点击：** 允许按下后立刻改「可点→不可点」，但 **禁止** 因本地点击就 `Collapsed` 主按钮或提前 `Visible` 打窝/离席。显隐只跟下一次权威 phase。

### 3.3 规则与数值

- 打窝数值仍走 `FEAT-GROUND-01` / `shared` 既有表；本单不改公式。
- `groundbaiting` → 离席：取消未完成定时器；`stackCount` 不 +1；`clearGroundbait` 仍随离席清窝（与现离席一致）。
- `groundbaiting` **仍不可** `start_fishing`（服务端门禁保持）。
- 有待领鱼获时第三钮文案仍为「领取鱼获」，优先级高于「离席」（08G 不变）。

---

## 4. 技术影响

### 4.1 数据模型

不新增表或 IPC 字段。继续用 `fishingPhase`、`availableActions`、`groundbaitStack`。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| Socket | `leave_spot` | **放行 `groundbaiting`**：取消 cast、清座、清窝；ACK 与现离席相同 |
| Socket | `start_fishing` / `groundbait_start` / `stop_fishing` | 门禁不放宽（打窝中仍不可开钓） |

### 4.3 涉及文件（预估）

- `desktop-overlay/MainWindow.xaml.cs`：`ApplyFishingControls`、`ApplyGroundbaitStatus`
- `fish-social-unity/Assets/Scripts/Desktop/OverlayPondStateBuilder.cs`：`MapAvailableActions`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialPondSessionController.cs`：`CanStopFishing` 不必把 `stopping` 变成可再点收杆；动作表按 §3.2
- `server/src/pondSession.ts`（及打窝定时取消处）：`leave_spot` 允许 `groundbaiting`
- 不改 `mobile/`

---

## 5. 验收标准

- [x] 未打窝：点开始钓鱼后，打窝与离席隐藏；主按钮可见且文案为「收杆」
- [x] 打窝成功后再点开始钓鱼：打窝钮同样隐藏（不再因 stack>0 残留）
- [x] 点打窝后：主按钮仍为「开始钓鱼」；再点无 `start_fishing`；打窝结束前不可再叠一层
- [x] 打窝中点离席：不必等 `castDurationMs`；座空出；未完成层未叠加
- [x] 点收杆后：主按钮保持「收杆」直至 phase 离开 `stopping`/`resolving`；其间打窝、离席不出现
- [x] 收杆时间结束后：主按钮文案改为「开始钓鱼」，打窝与离席（或领取鱼获）**同时**出现
- [x] 连续连点开始/收杆/打窝：不重复发包、不闪隐、不提前出侧钮
- [x] 不改 `mobile/`；不改打窝经济数值

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 服务端仍拒打窝中离席 | 本单必须改 `leaveSpot` 相位白名单 |
| `stopping` 仅 200ms 仍觉得「一闪」 | HUD 已不再提前出离席；要加长收杆演出另开单 |
| pending catch 与离席抢钮 | 第三钮规则保持 08G：领鱼优先 |
| Overlay 只改可见、Unity 仍乱发 actions | 两边一起改，验收以玩家看见的按钮为准 |

回滚：恢复 actions 驱动显隐 + `leaveSpot` 旧白名单。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-31 | 策划 | 用户确认中央钓鱼栏时序：主按钮常驻改文案；打窝后开钓须藏打窝；收杆结束后同时出打窝+离席；打窝中开钓无反应、离席可打断 |
| 2026-08-31 | 开发 | Overlay 按 phase 显隐；Unity `availableActions` 对齐相位表；`leave_spot` 放行 `groundbaiting` → **已实现** |
| 2026-08-31 | 策划 | 用户验收通过 |
