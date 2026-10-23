# 离塘返回：关闭 Overlay → 恢复桌面 → 展开结算

日期：2026-10-23。STEAM-DESKTOP-21；已实现，Windows 原生视觉待验收。本条取代上一轮仅发起关闭后恢复窗口的时序。

## 原因

原路径没有重新加载整个桌面，但会切换首页。`CloseOverlay` 只发起后台关闭，返回代码立即继续恢复主窗口；关闭 worker 启动的 taskkill 本身也异步返回。收到结算数据、切换首页、恢复窗口和原生窗退出因此可能交错。弹窗纸张原来淡入，背景遮罩却立即全量出现，容易产生整页刷新／闪回的观感。

## 当前顺序

1. 申请离塘时拦住结算展示，防止主界面原本可见时提前弹出；重复退出不再启动第二条流程。
2. 服务器离塘成功后关闭 Overlay，后台确认其进程已退出才完成关闭任务。确认等待最多 5 秒，不阻塞 Unity 主线程。
3. 关闭完成后准备桌面首页并恢复窗口。沿用现有页面实例；没有新增重新加载界面或资料 API 的操作。
4. 等桌面恢复及页面切换完成，并至少经过一帧，再释放结算展示。
5. 结算纸张与底边栏开页共用 `CozyMotion.OpenJournalSheet`：200ms OutCubic、从下方 16px 上移淡入；遮罩同时由透明渐显，之后正文按原顺序分组出现。减少动效使用短淡入，取消位移。

关闭失败／取消不会播放结算；超时会恢复桌面操作入口并显示失败信息。该异常分支不代表 Overlay 已退出。

## 验证

Unity EditMode **75/75** 通过，包括新增关闭任务顺序／失败／取消检查，以及正常、减少动效下结算与底边栏逐时刻位置和透明度对照。原先的结算提前关闭、隐藏恢复、正文顺序和页面切换回归一起通过。本轮修改文件的 `git diff --check` 通过。

测试筛选：`OverlayReturnRegressionTests;CozyWorkspaceCompletionMotionTests;CozyWorkspaceSettlementTests;CozyJournalFlipParityTests;CozyWorkspaceMotionTests`，均位于 `FishSocial.Tests.Editor`。使用 Unity 2021.3.29f1c1 的 EditMode Test Runner。

Windows 仍需重新构建 Unity 桌面端并录屏核对实际进程退出、窗口恢复与结算入场的先后；本轮未替换已安装程序。本轮没有修改 WPF 二进制源码，承接前一轮 Overlay 构建即可。
