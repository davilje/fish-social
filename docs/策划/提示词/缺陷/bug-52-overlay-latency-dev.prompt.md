# 开发提示词：Overlay 操作延迟与卡顿（BUG-52）

你负责 Fish Social 的 WPF Overlay 与 Unity 桌面状态发布。只做 **BUG-52**。不改服务端广播频率；不把调高帧率、重启进程当作修复。

## 必读

1. `docs/策划/规格/缺陷/BUG修复-OverlayHUD布局与交互.md` §1.5、§2、§3 BUG-52、§4.5
2. `desktop-overlay/MainWindow.xaml.cs`：管道读线程（约 460～490 行 `Dispatcher.BeginInvoke(HandleMessage)`）、`HandleMessage` state 分支、`WindowHook` 命中缓存、`ResyncPointerHover` 节流（工作区未提交，保留）
3. `desktop-overlay/IpcProtocol.cs` `LatencyTrace`
4. `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs` `PublishNativeOverlayState` 及 51 处调用

## 证据基线（修前）

- 最近 60 秒 2048 条 state，34.1 条/秒，间隔 P50 7ms / P95 17ms，约每帧两次
- `%TEMP%\FishSocialOverlay-latency.log` 335MB，每条 state 在 UI 线程同步 `File.AppendAllText`

## 顺序

1. 先补计时：`overlay_state_applied`（处理耗时 ms）、命令到对应 state 的 commandId 关联；记录修前 P50/P95。
2. Overlay 合并 state：读线程只留最新一条，UI 线程无待处理 state 时才投递；命令结果、错误、连接状态不丢；投递优先级低于输入。
3. Unity 同帧发布合并（脏标记、帧末发送），无变化不发，空闲 ≤10 条/秒，命令结果立即发。
4. `LatencyTrace` 后台缓冲写、state 抽样、10MB 轮转。
5. 合并与节流留环境变量开关，便于对比。改 `IpcProtocol.cs` 等受保护文件时同步 `cozy_source_contract.json` 对应哈希并说明。

## 验收

对照 spec §4.5 量化指标。完成后按 Skill `planning-progress-sync`：BUG-52 改为 **已实现**，填完成时间，`npm run planning:master-xlsx`。

- [ ] state ≤15 条/秒（空闲 ≤10），UI 待处理 state ≤1
- [ ] 本地点击反馈 P50 ≤50ms、P95 ≤100ms；命令往返 P50/P95 有记录，>2s 提示超时
- [ ] 拖动 10 秒不掉帧，悬停卡片正常
- [ ] 1 小时日志 ≤10MB
- [ ] 首次 / 连续 / 重复点击；退出、切换、重进鱼塘；断线有失败提示

## 派发

```text
@docs/策划/提示词/缺陷/bug-52-overlay-latency-dev.prompt.md 按此实现 BUG-52
```

建议角色：Overlay 客户端 + Unity 桌面（本仓库 Cursor 对话）。服务端耗时需另取服务端时间戳，不在本项改服务端。
