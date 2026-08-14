# FishSocialOverlay

独立的 Windows WPF 桌面宠物 Overlay。它不引用 Unity、Steamworks.NET 或 Fish Social 业务程序集，只通过 Named Pipe 接收状态并发送 UI 命令。

## 启动

```text
FishSocialOverlay.exe --pipe=FishSocialOverlay-<unity-process-id>
```

默认窗口为 `480×420`、无边框、透明、置顶，不显示在任务栏。

## IPC

协议为 JSON Lines，`version` 当前为 `1`，状态必须带递增 `sequence`：

```json
{"type":"state","version":1,"sequence":12,"loginState":"Authenticated","connectionState":"Connected","pondName":"pond-calm","fishingPhase":"waiting"}
```

Overlay 回传命令：

```json
{"type":"command","version":1,"command":"open_main"}
```

支持命令：

- `open_main`
- `hide_overlay`
- `quit_overlay`
- `request_snapshot`

Overlay 收到旧 `sequence` 时丢弃，不覆盖当前状态。

## 构建

需要 Windows/.NET Framework 4.8 和 WPF 构建工具：

```text
msbuild desktop-overlay/FishSocialOverlay.csproj /p:Configuration=Release
```

将输出的 `FishSocialOverlay.exe` 放到 Unity Player 同目录、`FishSocialOverlay/` 或 `Overlay/` 子目录中。
