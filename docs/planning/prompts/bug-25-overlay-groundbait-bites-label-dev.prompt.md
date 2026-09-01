# 开发提示词：打窝按钮去掉剩余口数（BUG-25）

你是 Fish Social 的 **Overlay**工程师。按 13C 收回归，勿扩需求。

## 必读

1. `docs/planning/specs/BUG修复-Overlay打窝按钮剩余口数.md`（**已确认** / **BUG-25**）
2. `docs/planning/specs/Steam桌面端-13COverlay打窝HUD收口.md`：按钮只 `打窝n/50`
3. `desktop-overlay/MainWindow.xaml.cs` `ApplyGroundbaitStatus` 里 `GroundbaitBitesLeft` 拼接

## 顺序

1. 按钮 Content 只拼 `打窝{stack}/{max}`。
2. 不要改 `server/` / `shared/` 口数权威。
3. 成功句仍走 `txt_error`。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 打窝后无 `·12` / `-12`
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/bug-25-overlay-groundbait-bites-label-dev.prompt.md 按此实现 BUG-25
```
