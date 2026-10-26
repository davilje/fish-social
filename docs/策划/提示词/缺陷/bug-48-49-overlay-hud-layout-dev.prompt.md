# 开发提示词：Overlay HUD 导出边距统一与布局重排（BUG-48、BUG-49）

你负责 Fish Social 的 WPF Overlay（`desktop-overlay/`）和 Unity 编辑器导出（`fish-social-unity/Assets/Scripts/Desktop/Editor/`）。只做 **BUG-48**，再做 **BUG-49**。不改 `server/`、`shared/`、`mobile/`。

## 必读

1. `docs/策划/规格/缺陷/BUG修复-OverlayHUD布局与交互.md` §1.1、§1.2、§3 BUG-48/49、§4.1、§4.2
2. `fish-social-unity/Assets/Scripts/Desktop/Editor/OverlayHudExporter.cs`（组件默认值约 900～930 行、`ValidateComponentBorder`、契约检查约 1035 行）
3. `fish-social-unity/Assets/Scripts/Desktop/Editor/DesktopBuildMenu.cs` `ExportOverlayHudForBuild`、`CopyOverlayResources`
4. `desktop-overlay/CozyTheme.cs`（`SpriteBorder` 12、`PaperBorder` 22、`CompactBorder` 10，工作区未提交）
5. `desktop-overlay/OverlayHudLayout.cs` `TryApply`、`CompleteCozyComponents`、`ApplyCozyLayoutRefinement`、`FitPanButton`
6. `desktop-overlay/OverlayHudWidgets.cs` `Required`；`MainWindow.xaml` 三个 `Legacy*HudStack`

## 顺序

### BUG-48（先合）

1. 导出器的按钮 / 大纸 / 小纸边距改为 12 / 22 / 10，与 `CozyTheme` 同源或加测试比对。
2. `MainWindow` 回退提示不再写进会被 state 覆盖的 `ErrorText`；屏幕上持续可见并写日志。
3. Unity 执行 `Fish Social → Export Overlay HUD`，再跑 `Validate Overlay HUD Export Contract`。
4. 更新 `desktop-overlay/tests/cozy_source_contract.json` 的 JSON 哈希与控件顺序。只改本次确实改过的文件哈希；`protected_file_sha256` 里与本次无关的旧失败不要顺手重写。

### BUG-49

1. 在 `OverlayHud.prefab` 删除 `btn_menu_toggle`、6 个菜单按钮和 `btn_open_main`；同步 `Required`、`MenuRailOrder`、绑定与菜单栏布局代码。
2. 右键菜单提供「打开主界面」：确认「当前鱼塘」能唤出主界面，否则加一项 `open_main`。
3. 按 spec §1.2 / §4.2 放置：退出鱼塘右下（906,504,38×40）、状态卡在其左、钓鱼按钮底部居中、聊天栏 16,506,320×42、Debug 按钮宽约 88、平移按钮 64×36 居中。把运行时 `FitPanButton` 改动落进 Prefab。
4. XAML 回退布局改成同样排布。
5. 重新导出 JSON，从 Unity 打包一次，确认构建目录 JSON 与源一致、布局未变。

## 验收

对照 spec §4.1、§4.2 勾选。完成后按 Skill `planning-progress-sync`：BUG-48、BUG-49 改为 **已实现**，填完成时间，`npm run planning:master-xlsx`。

- [ ] 打包后 Overlay 是 JSON 布局，不是回退布局
- [ ] 无左上齿轮、无菜单栏、无打开主界面按钮；右键菜单可到所有页面
- [ ] 右下状态卡 + 退出鱼塘，底部居中钓鱼按钮，左下 320 宽聊天栏，互不重叠
- [ ] `PYTHONUTF8=1 python -B -m unittest desktop-overlay.tests.test_cozy_ui_source desktop-overlay.tests.test_overlay_layout_refinement` 中本组相关用例通过

## 派发

```text
@docs/策划/提示词/缺陷/bug-48-49-overlay-hud-layout-dev.prompt.md 按此实现 BUG-48、BUG-49
```

建议角色：Overlay / Unity 桌面客户端（本仓库 Cursor 对话直接改 `desktop-overlay/` 与 `fish-social-unity/`）。Unity 编辑器已开着时用编辑器菜单，不要 batchmode。
