# Steam 桌面 Overlay：悬停浮窗本局条数与 actor-hint 定位

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 悬停浮窗展示本局钓获，并锚在 OverlayPondActor actor-hint |
| 编号 | **STEAM-DESKTOP-18** |
| 类型 | **功能**（`STEAM-DESKTOP-09B` 补丁） |
| 负责人 | Overlay / Unity 桌面工程师；服务端仅只读展示字段 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-01** |
| 上位需求 | `STEAM-DESKTOP-09B`、`STEAM-DESKTOP-ART-02` |
| 关联全景 | Overlay 宠物悬停、本局渔获 |

---

## 1. 背景与目标

### 1.1 背景

09B 浮窗 **仅** 时长，水平居中猫身、出现在角色簇上方。产品要看见本局钓了几条，并把浮窗放到预制体 **actor-hint** 槽（与引导 hint 同槽，卡片另层）。

指针打穿、菜单后悬停丢失见 **BUG-26**，本单不管命中。

### 1.2 目标

- 悬停两行：第一行时长/收杆/打窝倒计时；第二行 **`钓到X条!`** 或 **`空军`**。
- 浮窗锚在 **actor-hint**：槽内水平居中，底对齐槽底、文案向上长（与 hint/气泡同一套生长规则）。
- 引导用 `_hintBadge` 仍独立；只共用槽位，不把 hover 文案写进 hint 徽章。

### 1.3 非目标

- 不改 09D/14E 默认 icon/圆环。
- 不在浮窗里重复相位中文或 icon。
- 不做今日 8h 额度。
- 不改 BUG-26 命中/捕获。

---

## 2. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 玩家 | 悬停自己或他人猫 ≥300ms | 浮窗在 hint 槽；见本局时长 + 条数 |
| 玩家 | 本局 0 条 | 第二行 `空军` |
| 玩家 | 上钩 / 打窝带倒计时 | 第一行 `收杆 mm:ss` / `打窝 mm:ss` |
| 引导 | 同时出 actor-hint 教学泡 | 教学泡仍走 hint 徽章；hover 在 HoverLayer |

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 文案 | P0 | 行1 `BuildHoverDurationLine`；行2 `sessionCatchCount>0` → `钓到N条!` 否则 `空军`。无时长时仍可只出条数行 |
| 2 | 位置 | P0 | `OverlayHoverPresenter` 对齐 hint 槽（常驻透明 `_hintAnchor`，Collapsed 的 hint 徽章不可作 TranslatePoint） |
| 3 | IPC | P0 | own + users：`sessionCatchCount`（int，缺省 0） |
| 4 | Unity | P0 | snapshot / merge 带条数；自己 `FishCatchSettled` 可 +1；他人用塘用户字段 |
| 5 | 服务端 | P0 | 展示用：`enrichPondUser` 填 `sessionCatchCount`（如 `getSessionCatchCount`）；bot 无账本则 0→空军。不改结算公式 |

### 3.1 时长行（相对 09B）

| 相位 | 第一行 |
|------|--------|
| 上钩且 `hookDeadlineMs` | `收杆 mm:ss` |
| 打窝且倒计时 | `打窝 mm:ss` |
| 钓鱼中 / 打窝中 / 有会话锚点 | `本局 mm:ss` |

---

## 4. 技术影响

- `desktop-overlay/OverlayHoverPresenter.cs`、`OverlayPetActor.cs`
- `desktop-overlay/IpcProtocol.cs`
- `fish-social-unity/.../NativeOverlayStateDto.cs`、`OverlayPondStateBuilder.cs`、`PondUserMerge.cs`
- `shared/types.ts` `PondUser.sessionCatchCount?`（展示字段）
- `server/src/pondUserManager.ts` / `pondSessionLedger.ts`（只读抄条数）

布局 JSON / Baker 已有 `kind: actor-hint`（约 84×20）。

---

## 5. 验收标准

- [x] 悬停浮窗两行：时长（若有）+ `钓到N条!` / `空军`
- [x] 浮窗落在该座 actor-hint，不贴在簇顶再往上飘
- [x] 条数随本局结算增加；冷进塘与 snapshot 一致；bot 无记录显示空军
- [x] 引导 hint 与 hover 可同时存在、不抢同一控件
- [x] 不回退 BUG-26 的自己猫悬停 / 菜单后恢复

---

## 6. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 用户验收通过，状态改为 **已实现** |
| 2026-09-01 | 策划 | 已确认：09B 补丁，条数 + actor-hint；命中问题归 BUG-26 |
