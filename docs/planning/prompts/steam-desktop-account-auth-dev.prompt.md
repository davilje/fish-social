# 开发交接提示词：Steam 身份、账号绑定与安全会话（STEAM-DESKTOP-02）

你是 Fish Social 的后端 + Unity 联调 Agent。请按 `STEAM-DESKTOP-02` 实现 Steam Ticket 登录、SteamID64 与内部 playerId 绑定、JWT 会话和安全错误处理。

## 必读

1. `docs/planning/specs/Steam身份账号绑定与安全会话.md`
2. `docs/planning/specs/Steam桌面端产品定位与信息架构.md`
3. `docs/planning/specs/Steam桌面端独立游戏转型计划.md`
4. `server/src/` 中现有 auth、player、JWT 和 Socket 鉴权代码
5. `shared/types.ts`
6. `fish-social-unity/` 当前 Unity 工程

## 已确认规则

- Steam 版建立新档，不迁移旧移动端数据。
- 一个 SteamID64 只能绑定一个 playerId。
- 第一阶段只支持 Steam 登录。
- 第一阶段不提供自助解绑/换绑。
- Node 是账号、JWT、鱼获、库存和会话的权威。
- Steam Web API Key 只能放 Node 服务端环境变量，不得进入 Unity 或 Git。

## 实现顺序

### 后端

1. 增加 `steam_accounts` 映射表和迁移。
2. 增加 Steam Ticket 验证适配器。
3. 增加 `POST /api/auth/steam`。
4. 服务端使用 Steam `AuthenticateUserTicket` 验证 Ticket。
5. 验证成功后查找或创建 playerId。
6. 复用现有 JWT/Refresh 会话能力。
7. 对空 Ticket、错误 App ID、无效 Ticket、绑定冲突和限流返回稳定错误码。
8. 增加单元测试和安全审计日志测试。

### Unity

1. 增加 Steam 初始化和登录状态抽象。
2. Steam 运行时获取 Ticket。
3. 调用 Node Steam 登录接口。
4. 保存短期游戏会话，不保存 Web API Key。
5. 将登录状态提供给后续桌面主界面和 Socket 客户端。
6. Steam 未启动、Ticket 失效、服务端拒绝时显示普通用户可理解的错误。

## 当前实施 checkpoint

以下工作已经完成，不要重复搭建：

- Unity Package Manager 已安装 Steamworks.NET `2025.163.0`。
- Windows 构建已确认 `SteamAPI initialized`。
- AppID 为 `2713340`，本地开发使用 `steam_appid.txt`。
- `steam_api64.dll` 已接入 `Assets/Plugins/x86_64/`。
- Unity 已有 `SteamworksTicketProvider`，负责 Steam 初始化、回调和 `GetAuthTicketForWebApi`。
- `SteamAuthController` 已将 Ticket、AppID、identity 发送到 `/api/auth/steam`。

当前只完成 SteamAPI 初始化，**不能宣称 STEAM-DESKTOP-02 已完成**。

## 当前开发任务

### Unity 必做

1. 在桌面主界面增加“Steam 登录”按钮。
2. 按钮调用 `DesktopAppBootstrap.Instance.SteamAuth.BeginLogin()`。
3. 订阅 `SteamAuthController.StateChanged` 和 `ErrorMessage`。
4. 显示初始化、获取 Ticket、服务端验证、成功和失败状态。
5. 登录成功后保存短期 Access Token，并为后续 REST/Socket 客户端提供读取接口。
6. 不打印完整 Ticket、Access Token 或 Refresh Token。

### 服务端联调

1. 使用真实 `.env` 启动 Node 服务端：

```text
STEAM_AUTH_ENABLED=true
STEAM_APP_ID=2713340
STEAM_WEB_API_KEY=<server-only>
STEAM_AUTH_IDENTITY=fish-social-server-v1
```

2. 确认服务端请求 `partner.steam-api.com` 的
   `ISteamUserAuth/AuthenticateUserTicket`。
3. 验证首次登录创建 `playerId`。
4. 验证同一 Steam 账号重复登录复用原 `playerId`。
5. 验证错误 AppID、无效 Ticket、Steam 未启动和服务端不可用的错误提示。
6. 登录成功后验证 JWT REST 请求和 Socket 鉴权。

### 交付与验收

- 先在 Steam 客户端运行的 Windows Development Build 中联调。
- 本地 Fake Ticket 只能用于接口和错误处理测试，不能替代真实 Steam 验收。
- 不要把 Publisher Web API Key 写入 Unity、`shared/`、日志或 Git。
- 完成后运行相关验证、记录测试结果，再回写 `STEAM-DESKTOP-02` 为“已实现”。

## 当前剩余目标：登录后的 REST / Socket 会话验收

真实 Steam Ticket 登录已完成首次创建和重复登录复用验证。当前只处理登录成功后的会话使用，不扩大到好友、Lobby、鱼塘业务或 Steam 商店发布。

### Unity 客户端

1. 在 `fish-social-unity/Assets/Scripts/Desktop/Auth/` 增加窄接口 `IAuthenticatedApiClient`。
2. 登录成功后使用 `SteamAuthController` 内存中的 Access Token 发起受保护 REST 请求：
   - `GET /api/inventory/{playerId}`
   - Header：`Authorization: Bearer <accessToken>`
3. 增加“验证当前会话”入口或开发构建专用按钮，显示：
   - 会话验证成功
   - 服务端返回 401/403
   - 服务不可用
4. 不在 UI、Console、文件或异常文本中显示完整 JWT。
5. 不把 Token 写入 PlayerPrefs、Assets 或 Git；退出进程即清理。
6. 登录失败或 SignOut 后，REST 验证入口必须不可用。

### Socket 鉴权

1. 先确认 Unity 使用的 Socket.IO 客户端方案与许可证，禁止把移动端 TypeScript 源码复制进 Unity。
2. 将 Socket 连接封装为 `ISocialSocketClient`，Token 只能通过 `auth.token` 传入。
3. 连接成功后只验证现有 Socket 鉴权和最小 `join_pond` 握手，不实现好友/Lobby/真实鱼塘业务。
4. 无 Token、过期 Token、篡改 Token 必须被服务端拒绝。
5. 若当前 Unity 包暂未引入可审计的 Socket.IO 客户端，先提供 Node `socket.io-client` 集成测试作为服务端门禁，并明确标记 Unity Socket 接入未完成，不得伪造通过。

### 服务端 / 测试

1. 保持 Node 作为 JWT 和玩家数据权威，不新增客户端可自报 `playerId` 的信任路径。
2. 增加 REST Bearer 成功/401/403 测试。
3. 增加 Socket `auth.token` 成功/缺失/伪造测试。
4. 验证 Steam 登录返回的 JWT 与现有 `verifyPlayerToken`、Socket middleware 兼容。
5. 测试日志不得出现完整 Ticket、JWT 或 Publisher Web API Key。

### 本阶段验收命令

```bash
npm test
npm run verify:auth
```

如增加专用验证脚本，补充 `npm run verify:steam-auth-session`，并分别报告：

- 真实 Steam 首次登录：已通过
- 真实 Steam 重复登录复用：已通过
- REST Bearer 会话：待本阶段完成
- Socket JWT 会话：待本阶段完成

### 完成门槛

- REST 请求使用真实 Steam 登录返回的 JWT 成功访问受保护资源。
- 合法 JWT 可以通过 Socket 鉴权，缺失/篡改 JWT 被拒绝。
- Unity 不泄露或持久化敏感凭据。
- 以上验证通过后，才将 `STEAM-DESKTOP-02` spec、计划表和看板标记为“已实现”。

## 环境变量

使用示例变量名，不要提交真实值：

```text
STEAM_APP_ID=
STEAM_WEB_API_KEY=
STEAM_AUTH_ENABLED=false
```

## Steamworks 前置边界

- 必须先创建 Steamworks App 并获得 AppID。
- 测试账号必须拥有该 App 的访问权限。
- 需要配置 Windows Depot/Build，使测试包可以在 Steam 客户端环境启动。
- 商店页面可以保持未公开或 Coming Soon；不等待宣传素材、定价和正式发行配置。
- 客户端使用 `ISteamUser::GetAuthTicketForWebApi` 获取 Ticket。
- 服务端使用 `ISteamUserAuth::AuthenticateUserTicket` 验证 Ticket。
- Publisher Web API Key 只能放服务端环境变量，不能进入 Unity。

没有真实 Steam App ID 或 Web API Key 时：

- 可以实现接口、迁移、错误码、Fake Ticket 测试和 Unity 状态机。
- 必须明确标记为本地模拟。
- 不得把模拟通过写成真实 Steam 验收通过。

## 安全禁止项

- 不信任客户端自报 SteamID64。
- 不把 Web API Key放进 Unity、`shared/`、日志或前端配置。
- 不打印完整 Ticket、JWT 或 Refresh Token。
- 不用 SteamID64 直接替代现有 playerId。
- 不修改库存、鱼获、金币和每日额度的客户端权威边界。

## 验收

- 首次真实 Steam 登录创建唯一 playerId。
- 同一 Steam 账号再次登录复用 playerId。
- 错误 Ticket、错误 App ID、重复绑定均被拒绝。
- 登录成功后可以获得 JWT，并通过现有 Socket 鉴权。
- 本地模拟测试与真实 Steam 测试分离。
- 运行相关 `verify:*`、单元测试和构建检查。

建议角色：`@backend-dev` 主责服务端，`@frontend-dev` 联调 Unity 登录状态；完成后回写 `STEAM-DESKTOP-02` 验收状态。
