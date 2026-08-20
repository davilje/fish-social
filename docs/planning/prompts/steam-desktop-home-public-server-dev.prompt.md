# 开发交接提示词：STEAM-DESKTOP-10A 本机公网映射联调

你是 Fish Social 的 Unity Steam 桌面端开发 Agent。请按 `STEAM-DESKTOP-10A` 实现方案 A 配套能力：设置页编辑/保存 `server.json`、测试 `/health`，并提供本机检查脚本。

## 必读

1. `docs/planning/specs/Steam桌面端本机公网映射联调.md`
2. `docs/planning/specs/Steam桌面端公网联调与服务器地址配置.md`
3. `fish-social-unity/Assets/Scripts/Desktop/DesktopServerConfig.cs`
4. `fish-social-unity/Assets/Scripts/Desktop/UI/DesktopSettingsModalView.cs`

## 做

- `DesktopServerConfig` 增加写入 EXE/项目旁 `server.json`
- 设置页：服务器地址 Input、保存、测试连接（`GET {url}/health`）
- 保存成功后提示重启客户端再生效
- `scripts/ops/check-home-public-server.ps1` + 根目录 bat：打印局域网 IP、测本机 health、打印方案 A 提示

## 不做

- 不自动改路由器 / UPnP
- 不改服务端 Steam 业务
- 不实现 HTTPS

## 验收

按规格 §6 勾选开发项；外网 4G health 由用户在真实网络下确认。
