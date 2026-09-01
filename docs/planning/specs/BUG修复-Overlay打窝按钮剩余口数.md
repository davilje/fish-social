# Bug：打窝成功后按钮多出「-12」一类剩余口数

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 打窝按钮只保留次数，去掉剩余口数后缀 |
| 编号 | **BUG-25** |
| 类型 | **Bug修复**（`STEAM-DESKTOP-13C` 回归） |
| 负责人 | Overlay / Unity 桌面工程师 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-01** |
| 上位需求 | `FEAT-GROUND-01`、`STEAM-DESKTOP-13C` |

---

## 1. 现象与含义（给策划/玩家）

打窝成功后按钮类似：**`打窝1/50 -12`** 或 **`打窝1/50 ·12`**。

| 片段 | 含义 | 来源 |
|------|------|------|
| `1/50` | 已叠层 / 上限 | `groundbait.stackCount` / `maxStackCount`（默认 50） |
| **后面的 12** | **本层窝料还剩几次咬钩**，不是扣金币、不是 -12 金 | `groundbait.bitesLeft` ← 窝料表 `maxBites` |

`shared/generated/game-data/groundbaits.json`：

| 窝料 | `maxBites` | 刚打完一层时按钮上可能看到的数 |
|------|------------|--------------------------------|
| 基础 `gb-basic` | 8 | 8 |
| 混合 `gb-mix` | 10 | 10 |
| 精品 `gb-premium` | **12** | **12** |

服务端每次结算咬钩 `bitesLeft - 1`，到 0 清窝（`server/src/groundbait.ts`）。`durationMin` 与口数 **先到失效**。

Overlay 现网拼接（`MainWindow.ApplyGroundbaitStatus`）：

```text
打窝{stack}/{max}  +  若 bitesLeft>0 则追加  " ·" + bitesLeft
```

中间点 `·` 在小字号按钮上容易看成 **减号**，所以会被读成 `-12`。

**13C 已定**：按钮 **只** 写 `打窝n/50`，**不**展示咬钩加成、尺寸加成、**剩余口数**。权威仍算 `bitesLeft`，HUD 不画。本单按 13C 收回归。

---

## 2. 目标

- 打窝按钮文案恒为 **`打窝{stack}/50`**（max 以 IPC `groundbaitMaxStack` 为准，默认 50）。
- 成功提示仍走 `txt_error`：「打窝成功，希望鱼儿能快快长大」（13C）。
- 不改扣金、等待、叠层曲线、口数/时长失效。

### 2.1 非目标

- 不在按钮上改成「剩余 12 口」等更长文案（若以后要展示，另开需求）。
- 不改 Unity 主窗口商店窝料说明。

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | Overlay 按钮 | P0 | 删除 `GroundbaitBitesLeft` 拼进 `Content` 的分支 |
| 2 | 回归 | P0 | `txt_groundbait` 仍常隐；无咬钩%/尺寸旁路 |

IPC 字段 `groundbaitBitesLeft` 可保留给以后，只是 HUD 不用。

---

## 4. 技术影响

- `desktop-overlay/MainWindow.xaml.cs` `ApplyGroundbaitStatus`
- 对照 `docs/planning/specs/Steam桌面端-13COverlay打窝HUD收口.md`
- 不改 `server/` / `shared/` 打窝公式

---

## 5. 验收标准

- [x] 打窝成功后按钮仅为 `打窝1/50`（或当前层数），无 `·12` / `-12` / 口数
- [x] 再打一层变为 `打窝2/50`，仍无口数
- [x] `txt_error` 成功句仍在
- [x] 服务端口数/失效逻辑不变（咬够仍清窝）
- [x] 用户验收通过（2026-09-01）

---

## 6. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 已确认：解释 -12=剩余口数；HUD 按 13C 去掉 |
| 2026-09-01 | Overlay | 已实现：按钮只拼 `打窝{stack}/{max}` |
| 2026-09-01 | 策划 | 用户验收通过 |
