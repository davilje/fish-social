# Overlay 交互、进出鱼塘与资料刷新回归修复

日期：2026-10-23。关联：STEAM-DESKTOP-21。状态：已实现，Windows 原生交互和视觉待验收。

## 五项修改

1. **聊天栏**：原生点击穿透改用 WPF 的 InputHitTest，遵循 IsHitTestVisible，避免装饰层挡住底下聊天按钮的命中判定；折叠栏整条可点击展开。展开底板用本次设置的宽高计算，不再读取上一轮布局的 ActualHeight。聊天历史、输入和发送协议保留。
2. **退出 Overlay 与收杆记录**：先把隐藏主窗口切到首页，留一帧更新画面后再恢复原生窗口；解除动效暂停也移到窗口恢复之后。服务器结算先排队，等主窗口可见、鱼塘退出操作及页面切换结束，再创建遮罩和结算页并播放已有纸张／正文分组入场动效。避免旧页面、已展开的遮罩和切页动画同时闪现。
3. **进入鱼塘闪屏**：去除启动时空 pondId 的默认鱼塘加载；目标背景加载期间不显示几何降级场景，背景与装饰就绪后才显示场景层。真正缺失图片时仍可降级。移除主界面入口先切旧 Pond 页的步骤。启动前保存目标快照，复用已隐藏 Overlay 时显示命令携带最低快照序号，收到目标状态并加载完成后才显示场景，兼容旧命令序号 0。
4. **保存头像后资料卡未更新**：编辑页成功写入资料后通知个人中心，立即更新本人资料缓存中的头像、昵称、简介和展示位，清除已绘制标记；重新打开时先展示保存结果，再刷新服务端完整资料。即使后续展示鱼获保存失败，已经成功保存的头像也同步；不会覆盖其他玩家资料卡。
5. **右键菜单**：删除 WPF Overlay 右键菜单中的“当前鱼塘”，其余入口保留。

本轮改动包括 Unity 主程序与 WPF Overlay，两者需要一起构建部署。未修改 Web 预览、素材或服务端玩法。

## 验证

- Unity EditMode 专项 **34/34**：新增 4 个用例覆盖启动前目标快照、本人头像更新／他人资料隔离、隐藏或路由中不创建结算遮罩；保留保存成功／失败、资料布局、结算分组动效和页面切换回归，并补部分保存成功的通知检查。
- Overlay Python 源／资源检查 **48/48**。
- 正式 WPF 和 Windows 原生测试项目交叉编译 **0 errors**，5 条既有 netcoreapp3.1 提示。
- Windows 原生测试扩展到 **19 组**，增加三种窗口尺寸下聊天展开／收起、装饰层穿透、输入命中和删除菜单检查；强化首次启动与快速换塘的显示就绪检查。macOS 上仅编译，未运行这 19 组。
- `git diff --check` 通过。没有据此宣称 Windows 视觉验收完成；已安装 EXE 未替换。

## 复跑

仓库根目录：

```sh
python3 -m unittest discover -s desktop-overlay/tests -p 'test_*.py'
dotnet build desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj -p:EnableWindowsTargeting=true
```

Unity 2021.3.29f1c1 使用 `-runTests -testPlatform EditMode`，`-testFilter` 为：

```text
FishSocial.Tests.Editor.OverlayReturnRegressionTests;FishSocial.Tests.Editor.CozyWorkspaceCompletionMotionTests;FishSocial.Tests.Editor.CozyWorkspaceProfileTests;FishSocial.Tests.Editor.CozyWorkspaceSettlementTests;FishSocial.Tests.Editor.CozyJournalFlipParityTests
```

Windows 实机：

```powershell
dotnet run --project desktop-overlay/tests/FishSocialOverlay.PerformanceTests.csproj
```

人工验收顺序：

1. 在 960×560、800×400、600×300 和 100%／150% DPI，分别点聊天图标及折叠栏展开，输入并发送消息，再连续收起／展开；检查真实服务端消息和焦点。
2. 从鱼塘退出，录屏确认先出现首页，然后收杆记录从初始状态正常入场；同时测试主界面原本已打开、服务器结算延迟、减少动效、连续进出鱼塘。
3. 冷启动直接进入非默认鱼塘，再从已隐藏的 Overlay 切换另一鱼塘；不得出现默认塘、几何降级图或旧塘闪回。加载期间允许场景暂不可见，HUD 保留。
4. 更换头像保存后返回“我的钓鱼时光”，头像和昵称立即一致；再验证展示鱼获保存失败后的返回，以及查看其他玩家资料不被污染。
5. 右键菜单不再出现“当前鱼塘”。
