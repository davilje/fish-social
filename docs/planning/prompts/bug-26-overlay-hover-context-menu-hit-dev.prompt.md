# 开发提示词：悬停浮窗与右键菜单指针命中（BUG-26）

你是 Fish Social 的 **Overlay**工程师。按规格修同一条命中/捕获链，勿扩需求。

本单 **已实现**（2026-09-01 用户验收）。若回归，先读 spec §2.2 / §7，不要再把「指针不在菜单上」写成自动关菜单。

## 必读

1. `docs/planning/specs/BUG修复-Overlay悬停与右键菜单指针命中.md`（**已实现** / **BUG-26**）
2. 09A 菜单仍只他人；09B/18 浮窗文案与 actor-hint 不在本单
3. 现网对照：`FindPetArtAt` vs `FindSocialPetAt`、`OverlayInteractionState.MenuSuppressesHover`、`GetCursorPos` 重算悬停

## 顺序（最终口径）

1. `HTCLIENT` / 猫身几何命中对 **所有** actor（含自己）；社交菜单单独 `HasPlayerContextMenu`。
2. 塘级 `UpdatePointerHover`，不要只靠 `_body` Enter/Leave。
3. 悬停门闩只用 `MenuSuppressesHover`（打开 `Begin`，关闭 `End`）。**禁止**用 `ContextMenu.IsOpen` 当悬停开关。
4. 右键 **Down 记意图、Up 再打开**；菜单 `StaysOpen=true`；左键 Overlay 或点菜单项才关。
5. 关菜单后 `GetCursorPos` + `ForceResyncPointerHover`；未抑制时 200ms 轮询补 `MouseMove` 丢失。
6. **禁止** `MouseMove` / 轮询里「指针不在 Popup 上就 ForceDismiss」（菜单会闪关）。
7. 关掉 Window/Pond 默认 `ContextMenuService`；透明点穿（14）不要回退。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 自己的猫可悬停；他人右键 09A；菜单稳住直到左键 Overlay；关掉后悬停能恢复
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/bug-26-overlay-hover-context-menu-hit-dev.prompt.md 按此实现 BUG-26
```
