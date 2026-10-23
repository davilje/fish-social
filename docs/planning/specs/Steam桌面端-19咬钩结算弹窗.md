# Steam 桌面 Overlay：咬钩结算弹窗（钓到 / 脱钩）

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 咬钩结束结算弹窗；取消「领取鱼获」；按收费档区分入包 / 回鱼文案 |
| 编号 | **STEAM-DESKTOP-19** |
| 类型 | **功能** |
| 负责人 | Overlay / Unity 桌面；服务端自动结算；Web 弹窗对齐 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-02** |
| 上位需求 | Web `CatchFishModal`、`FEAT-RETURN-02`、`STEAM-DESKTOP-08G`、`STEAM-DESKTOP-15` |
| 关联全景 | 咬钩结算、回鱼档、Overlay 挂机主路径 |
| 计划表编号 | STEAM-DESKTOP-19 |

---

## 2026-10-23 变更：取消 Overlay 单次结算卡

用户要求钓获和脱钩不再弹窗，以免遮挡角色动画。正常鱼塘 `hook_keep / hook_return / hook_escape` 已在 Unity 下发与 WPF 接收两端停用；以下旧版弹卡文案、5 秒展示和确认按钮要求仅作历史记录，不再是当前验收目标。自动入包／回鱼、取消领取按钮、离席与结算事件保持。

本次变更归入 STEAM-DESKTOP-21 的 Overlay 收口；Python 34/34、Unity 新增专项 5/5 通过。Windows 原生 EXE 需重新构建并实机验收。教学提示与离塘总账保留。详见 `docs/design/ui/2026-10-23-overlay-journal-refinement.md`。

## 1. 背景与目标

### 1.1 背景

Web 端咬钩窗口结束时会弹出 `CatchFishModal`：钓到或脱钩一目了然。桌面 Overlay 挂机主路径没有这张结算卡，第三钮「领取鱼获」还依赖手动 `accept_catch`。咬钩结束后服务端会自动续钓并把领取钮藏掉（STEAM-DESKTOP-15），待领约 13 秒过期，玩家会感觉「咬了钩却钓不到」。

### 1.2 目标

- 上钩相位结束立刻弹出结算卡：**钓到了** 或 **脱钩了**。
- **取消** Overlay（及桌面调试页）「领取鱼获」按钮；鱼获在弹窗出现前已由服务端结算。
- 按进塘档位与是否可回鱼区分文案：
  - **出售档**，以及 **回鱼档但本条不可回**：只显示尺寸、体重，并提示 **已放入背包**。
  - **回鱼档且本条可回**：显示回鱼尺寸、体重、回鱼价格（金币）。

### 1.3 非目标

- 不改脱钩率、上钩时长、回鱼准入公式（品质 / 市斤门槛仍走 `isReturnEligible`）。
- 不改离塘总账弹窗（`DesktopPondSettlementModalView`）。
- 不改教学关本地弹窗流程（STEAM-DESKTOP-11 仍用 `confirm_overlay_prompt`）。
- 不把物种名、品质名放进出售档 / 不可回鱼结算卡（用户要求只出尺寸体重 + 入包）。
- 不改 `PHASE_MS`、打窝、15 的开始/收杆/打窝时序（仅废除第三钮「领取」分支）。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 上钩圈走完，未脱钩，出售档 | 弹窗「钓到了！」；尺寸、体重；「已放入背包」；鱼进背包 |
| 玩家 | 上钩圈走完，未脱钩，回鱼档但未达回鱼门槛 | 同上（入包，不显示金币） |
| 玩家 | 上钩圈走完，回鱼档且可回 | 弹窗「钓到了！」；尺寸、体重、回鱼价格 N 金币；鱼已回塘发金 |
| 玩家 | 上钩圈走完判定脱钩，或上钩时点「收杆」 | 弹窗「脱钩了！」；鱼溜走文案；不入包 |
| 玩家 | 坐席空闲 | 第三钮只有「离席」，不再出现「领取鱼获」 |
| 玩家 | 5 秒不点 | 弹窗自动关闭；钓鱼循环继续 |

---

## 3. 功能范围

### 3.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 服务端咬钩成功即结算 | P0 | 出售档与回鱼档均 `settleAcceptedCatch`（`auto: true`）；不再等 `accept_catch` |
| 2 | Overlay 结算弹窗 | P0 | 复用现有 `OverlayPromptChrome`；`fish_catch_settled` / `fish_miss` 驱动 |
| 3 | 去掉领取钮 | P0 | Overlay 第三钮仅离席；Unity 不再下发 `accept_catch` |
| 4 | 文案分档 | P0 | 见 §3.2 |
| 5 | Web 弹窗对齐 | P0 | `CatchFishModal` 改为展示已结算结果，确认只关闭 |

### 3.2 交互与 UI

权威展示走 Overlay 中央弹窗（与教学关同一套 chrome）。按钮文案 **知道了**。倒计时 **N 秒后自动关闭**（`FISHING_PROMPT_AUTO_CLOSE_MS` = 5s）。

| 结果 | 标题 | 正文（仅这些行） |
|------|------|------------------|
| 入包（出售档，或回鱼档不可回） | 钓到了！ | `尺寸：Xm` / `体重：Ykg` / `已放入背包` |
| 回鱼档可回 | 钓到了！ | `尺寸：Xm` / `体重：Ykg` / `回鱼价格：N 金币` |
| 脱钩 | 脱钩了！ | `鱼儿挣脱了鱼钩，溜走了` |

尺寸用 `sizeM` 两位小数 + `m`；体重用 `calcFishWeightKg` / Unity `DesktopGameData.CalcWeightKg` 与 `formatFishWeight`。回鱼价格用结算 payload 的 `gold`。

`overlayPromptKind`：`hook_keep` / `hook_return` / `hook_escape`。点「知道了」或倒计时结束发 `confirm_overlay_prompt`，只关弹窗，不再 `accept_catch`。

STEAM-DESKTOP-15 坐席第三钮：**不再**「有待领 → 领取鱼获」；空闲坐席固定「离席」。

### 3.3 规则与数值

- 回鱼是否发生仍只看 `returnFeeMode === 'auto_return'` + `isReturnEligible`（`tryAutoReturnFish`）。出售档即使鱼够大也只入包。
- 不再创建需要玩家领取的 pending；`processWaitingBiteTick` 不再被待领锁死。
- `accept_catch` Socket **保留**（Debug / 旧客户端），正式路径不再依赖。

---

## 4. 技术影响

### 4.1 数据模型

不新增表。`fish_catch_settled` 已有 `sizeM`、`autoReturned`、`gold`。

### 4.2 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| Socket S2C | `fish_catch_settled` | 咬钩成功结算后推送；Overlay / Web 弹窗 |
| Socket S2C | `fish_miss` | 脱钩；Overlay / Web 弹窗 |
| Socket S2C | `fish_bite` | 正式路径不再作为领取入口（Debug `deferAutoSettle` 除外） |
| Overlay IPC | `overlayPrompt*` | Unity 填结算文案；`confirm_overlay_prompt` 关闭 |

### 4.3 涉及文件（预估）

- `server/src/fishingStateMachine.ts`：成功上钩一律自动结算
- `server/src/catchSettlement.ts`：入包文案不再写死「未达回鱼标准」（出售档也入包）
- `fish-social-unity/.../OverlayPondStateBuilder.cs`、`SocialPondSessionController.cs`、`SocialSocketClient.cs`、`DesktopAppBootstrap.cs`
- `desktop-overlay/MainWindow.xaml.cs`：第三钮去掉领取；倒计时结束关弹窗
- `mobile/components/CatchFishModal.tsx`、`mobile/lib/usePondSocket.ts`

---

## 5. 验收标准

- [x] 出售档咬钩成功：弹窗尺寸+体重+已放入背包；背包有鱼；无「领取鱼获」
- [x] 回鱼档不可回：同上（入包，无金币行）
- [x] 回鱼档可回：弹窗尺寸+体重+回鱼价格；金币增加；背包无该条
- [x] 脱钩或上钩时收杆：弹窗「脱钩了！」
- [x] Overlay 空闲坐席第三钮只有「离席」
- [x] 5 秒或点「知道了」关闭弹窗，不重复结算
- [x] Web `CatchFishModal` 文案与上表一致
- [x] 教学关 Overlay 弹窗不被本单打断

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 旧 Overlay 仍点领取 | 服务端已入包，`accept_catch` 返回没有待领取，不重复发鱼 |
| 与 STEAM-DESKTOP-15 第三钮表冲突 | 本单废除领取分支；15 开始/收杆/打窝时序不变 |
| Web 仍等 `fish_bite` | 同步改为听 `fish_catch_settled` |

回滚：恢复出售档 `fish_bite` + Overlay 领取钮。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 初稿：对齐 Web 咬钩弹窗；取消领取；分档入包/回鱼文案 |
| 2026-09-01 | 开发 | 服务端自动结算；Overlay 弹窗；去掉领取钮；Web 弹窗对齐 → **已实现** |
| 2026-09-02 | 策划 | 用户验收通过 |
