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
