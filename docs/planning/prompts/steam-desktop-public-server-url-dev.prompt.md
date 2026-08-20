# 开发交接提示词：STEAM-DESKTOP-10 公网联调与服务器地址配置

你是 Fish Social 的 Unity Steam 桌面端开发 Agent。请按 `STEAM-DESKTOP-10` 实现可配置 `serverBaseUrl`，使本机 / 局域网 / 云主机联调只需改配置，不必改代码重编。

## 必读

1. `docs/planning/specs/Steam桌面端公网联调与服务器地址配置.md`
2. `docs/planning/specs/Steam身份账号绑定与安全会话.md`
3. `docs/ops/server-env.md`（仅对照 Steam 环境变量约定，不要把 Key 打进客户端）
4. `fish-social-unity/Assets/Scripts/Desktop/Auth/SteamAuthController.cs`
5. `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`
6. `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
7. `fish-social-unity/Assets/Scripts/Desktop/Auth/SocialSocketClient.cs`

## 范围

### 做

- P0：启动时解析服务器地址，优先级：
  1. 环境变量 `FISH_SOCIAL_SERVER_URL`
  2. EXE 同目录 `server.json` 的 `serverBaseUrl`
  3. 默认 `http://localhost:3001`
- P0：将解析结果统一注入 Steam 登录、Lobby REST、Socket.IO。
- P0：非法 / 空配置时回退默认并打警告日志。
- P1：启动日志或设置页可见当前服务器地址（不含密钥）。
- 可选：构建产物旁提供 `server.json.example`。

### 不做

- 不改服务端 Steam 登录、Lobby、鱼塘业务逻辑。
- 不实现 HTTPS / 域名证书。
- 不把 `STEAM_WEB_API_KEY`、`JWT_SECRET` 写入 Unity 或 `server.json`。
- 不租云、不改防火墙（运维手册由人执行）。

## 实现要点

1. 新增小型配置解析类（例如 `DesktopServerConfig`），在 Bootstrap 早期调用。
2. `SteamAuthController` 当前 `[SerializeField] serverBaseUrl` 须可被运行时覆盖。
3. `SocialLobbyController.Configure(..., serverBaseUrl)` 与 Auth / Socket 使用同一字符串。
4. 路径解析：优先 `Application.dataPath` 的上级（Standalone 下多为 EXE 目录）读取 `server.json`；Editor 下可用项目根或约定路径并文档说明。
5. 日志示例：`[DesktopShell] serverBaseUrl=http://...`（可脱敏 host 以外信息不必打印）。

## `server.json` 示例

```json
{
  "serverBaseUrl": "http://192.168.1.100:3001"
}
```

## 验收

- [ ] 无配置时本机 localhost 行为不回归。
- [ ] 修改 `server.json` 后重启 EXE，请求打到新地址。
- [ ] 环境变量可覆盖 `server.json`。
- [ ] Steam 登录、Lobby、进塘 Socket 同源。
- [ ] Windows Development Build 验证通过并记录结果。

## 完成后

1. 自检规格 §7.1。
2. 告知用户验收步骤（本机 + 可选局域网 IP）。
3. 用户确认后由策划回写计划表状态为 **已实现**。

建议角色：Unity 桌面端 / `@frontend-dev`（仅 `fish-social-unity/`）。
