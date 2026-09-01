# Bug：Overlay 悬停浮窗与右键菜单同一指针命中链失效

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 猫身指针命中统一，修复悬停浮窗与右键菜单互相打穿 |
| 编号 | **BUG-26** |
| 类型 | **Bug修复**（`STEAM-DESKTOP-09A` / `STEAM-DESKTOP-09B`） |
| 负责人 | Overlay 工程师 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-01** |
| 上位需求 | `STEAM-DESKTOP-09A`、`STEAM-DESKTOP-09B`、`STEAM-DESKTOP-14` |

---

## 1. 现象

同一条 Overlay 指针链路同时搞坏两件事：

| 现象 | 玩家感受 |
|------|----------|
| 悬停浮窗 | 自己的猫常常不出浮窗；他人猫有时也不稳。打开右键菜单再关掉后，再悬停没反应，要点座位才恢复 |
| 右键菜单 | 点自己的猫或透明环/状态区时，09A 社交菜单出不来，或弹出系统菜单、点穿桌面；菜单开着时悬停被清掉 |

热区本应是 **actor-hit**（否则 actor-pet / 64×64 猫身），09A 菜单仍只对 **他人**。

---

## 2. 根因

分两层：命中链（初版）和菜单生命周期（回归后真正卡住悬停的原因）。

### 2.1 命中过滤过窄（初版）

Overlay 曾经把「猫身是否吃鼠标」绑在 **是否有社交菜单** 上：

1. `HitsSocialPetArt` / `FindSocialPetAt` 只认 `HasPlayerContextMenu`（他人）。自己的猫进不了 `HTCLIENT`，WPF 收不到 `MouseMove` / 右键。
2. 热区挂在 `_body` / `_image` 上，指针在猫身内部子节点之间移动会误触发 Leave，浮窗闪掉。
3. Window / PondScene / Actor 未关 `ContextMenuService`，右键抬起再弹系统菜单。

### 2.2 菜单关掉后悬停永久失效（验收回归）

透明 Overlay（`AllowsTransparency` + `ShowActivated=false` + `WM_NCHITTEST` 点穿）上，WPF `ContextMenu` 不可靠：

1. **`ContextMenu.IsOpen` / `Closed` 会卡住**：菜单视觉上已经没了（点 Overlay 关掉），但 `IsOpen` 仍为 true，或 `Closed` 根本不触发。用这两个值当悬停门闩，悬停会永远被挡住。点座位能恢复，是因为那次左键让 WPF 真正走完一次关闭，不是 `take_spot` 本身。
2. **不要用「指针不在菜单上」自动关菜单**：菜单是独立 Popup，指针通常停在鱼塘上。`MouseMove` / 轮询里 `ForceDismiss` 会把菜单闪一下就关，同时把 `IsOpen` 弄得更乱。
3. **不要在 `RightButtonDown` 打开菜单**：同一手势的 `RightButtonUp` 会被当成点在菜单外。应在 **右键抬起** 再 `IsOpen=true`。
4. 关菜单后透明窗经常收不到 `MouseMove`，必须用 `GetCursorPos` 重算猫身几何命中，不能只等 WPF 鼠标事件。

文案（钓到几条）和浮窗落在 actor-hint **不是**本单，见 **STEAM-DESKTOP-18**。

---

## 3. 目标

- 全体塘内猫（含自己）的猫身热区都能进 WPF：悬停 300ms 出浮窗；他人右键出 09A 菜单。
- 自己的猫：悬停可以，右键 **不**出 09A（仍可出场景产品菜单，与 09A 非目标一致）。
- 打开/关闭右键菜单后，指针还在猫身上则悬停能恢复；不必点座位、也不必把鼠标移出 Overlay。
- 菜单本身要稳住，直到左键点 Overlay 或点菜单项；透明处仍点穿桌面（14）。

### 3.1 非目标

- 不改 09A 菜单项（资料/加好友/私聊/点赞）。
- 不改 STEAM-DESKTOP-18 浮窗文案与 actor-hint 位置。
- 不改 `mobile/`。

---

## 4. 功能范围（最终实现）

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 猫身命中 | P0 | `FindPetArtAt` / `HitTestsPetArt` / `HTCLIENT` 对 **所有** actor；社交菜单另走 `FindSocialPetAt` |
| 2 | 塘级悬停 | P0 | `PondScenePresenter.UpdatePointerHover` 按几何 `SetPointerOverPet`；200ms 轮询 + `GetCursorPos` |
| 3 | 菜单会话开关 | P0 | `OverlayInteractionState.MenuSuppressesHover` **自有开关**，禁止用 `ContextMenu.IsOpen` 当悬停门闩 |
| 4 | 菜单打开时机 | P0 | `PreviewMouseRightButtonDown` 只记意图；`RightButtonUp` 再打开。`StaysOpen=true`，左键 Overlay / 点菜单项才关 |
| 5 | 系统菜单 | P0 | Window / PondScene `ContextMenuService.IsEnabled=false`；产品/社交菜单不挂在 `FrameworkElement.ContextMenu` |

---

## 5. 技术影响

- `desktop-overlay/MainWindow.xaml(.cs)`：右键 Down/Up 分步、左键 `TryDismissMenusFromLeftClick`、`GetCursorPos` 重算悬停
- `desktop-overlay/OverlayPetActor.cs`：`SetPointerOverPet`、`StaysOpen` 社交菜单
- `desktop-overlay/OverlayInteractionState.cs`：`MenuSuppressesHover` / `BeginMenuHoverSuppress` / `EndMenuHoverSuppress`
- `desktop-overlay/PondScenePresenter.cs`：`UpdatePointerHover` / `ForceResyncPointerHover`
- 不改服务端占座 / 钓鱼公式

---

## 6. 验收标准

- [x] 悬停自己的猫 ≥300ms 出浮窗；移出消失
- [x] 悬停他人的猫同样出浮窗
- [x] 右键他人猫身（含透明环/状态附近仍算猫身命中时）出 09A，不出系统菜单
- [x] 右键自己的猫不出 09A
- [x] 右键他人猫或空白处打开菜单后，菜单会稳住，直到左键点 Overlay 任意区域或点菜单项
- [x] 关掉菜单后，指针仍在猫身上则悬停能再出来，不必点座位、也不必把鼠标移出 Overlay
- [x] 透明桌面点穿仍在（14）；空白处右键仍是产品菜单

---

## 7. 修复过程（实现记录）

| 轮次 | 做法 | 结果 |
|------|------|------|
| 1 | 猫身命中放开到全体 actor；塘级几何悬停；`ContextMenuOpen` 标志 + `Closed` 后 `ResyncPointerHover` | 自己可悬停、他人可出菜单；**关菜单后悬停仍死** |
| 2 | 认定 `Closed` 不触发 → `ReconcileContextMenuFlag`、zombie 定时器、200ms 轮询；`SetPointerOverPet` 在 `_pointerInside` 已 true 时重开计时器 | 用户复现不变：右键一次，悬停到落座才恢复 |
| 3 | `MouseMove` / 轮询：菜单开着且指针不在菜单上则 `ForceDismiss` | **回归**：菜单瞬间关闭；悬停仍失效（指针本就在鱼塘上，不在 Popup 上） |
| 4（本轮验收） | 见下 | 菜单稳住；关菜单后悬停恢复 |

**第 4 轮落地（2026-09-01 用户验收通过）：**

1. 悬停是否抑制只看 `MenuSuppressesHover`。打开菜单 `Begin`，左键关菜单 / `Closed` / 点菜单项 `End`。**不要**用 `ContextMenu.IsOpen` 或旧 `ContextMenuOpen` 反推。
2. 产品菜单与社交菜单 `StaysOpen = true`，避免透明窗把「点在 Overlay 上」当成系统外部点击把菜单拆掉却不走 `Closed`。
3. 右键按下只记录「他人社交 / 空白产品」；**抬起**再 `IsOpen = true`。
4. 左键 `Window.PreviewMouseLeftButtonDown` 关菜单并恢复悬停；该次点击不拖窗口。
5. 关菜单后 `GetCursorPos` → `ForceResyncPointerHover`；200ms 轮询在未抑制时持续重算（透明窗可能没有 `MouseMove`）。
6. **禁止**在 `MouseMove` / 轮询里因「指针不在菜单上」自动关菜单。

---

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 回归修复验收通过：菜单 StaysOpen + 右键抬起打开 + 自有悬停开关；关菜单后 GetCursorPos 恢复。状态 **已实现** |
| 2026-09-01 | 策划 | 初验后复现：右键一次悬停死、落座才恢复；中间一轮 MouseMove 自动关菜单造成闪关 |
| 2026-09-01 | 策划 | 初验标记已实现（命中链）；随后用户报菜单后悬停仍失效 |
| 2026-09-01 | 策划 | 已确认：悬停与右键同一命中/捕获链，单号 BUG-26 |
