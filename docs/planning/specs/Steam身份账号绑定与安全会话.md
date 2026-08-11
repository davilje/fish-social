# Steam 身份、账号绑定与安全会话

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 身份、账号绑定与安全会话 |
| 编号 | **STEAM-DESKTOP-02** |
| 负责人 | 后端 + Unity 工程师 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 设计时间 | **2026-08-12** |
| 上位规格 | [`Steam桌面端独立游戏转型计划.md`](./Steam桌面端独立游戏转型计划.md) |
| 产品前置 | [`Steam桌面端产品定位与信息架构.md`](./Steam桌面端产品定位与信息架构.md) |

---

## 1. 目标

让 Unity Windows 客户端使用 Steam 身份进入 Fish Social，并由 Node 服务端建立可信的游戏会话。

```text
Unity Steamworks
  → 获取 Steam Ticket
  → Node 验证 Ticket
  → 得到可信 SteamID64
  → 查找或创建 playerId
  → 签发游戏 JWT
  → Unity 使用 JWT 访问 REST / Socket
```

## 2. 已确认规则

- Steam 版建立新档，不迁移旧移动端数据。
- 一个 Steam 账号只能绑定一个 `playerId`。
- 第一阶段只支持 Steam 登录，不实现多登录方式。
- 第一阶段暂不提供用户自助解绑/换绑，异常情况走人工处理。
- Steam App ID 和 Web API Key 只存在服务端环境变量，不进入 Unity 包和 Git。
- 客户端不能通过自报 SteamID 登录。
- Node 继续负责玩家数据、库存、鱼获、每日额度和会话权限。

## 3. 功能范围

### 3.1 Unity 客户端

- 检测 Steam 客户端是否运行。
- 初始化 Steamworks。
- 获取当前 SteamID64 和加密 Ticket。
- 将 Ticket 发送给 Node 登录接口。
- 保存短期游戏 JWT。
- 连接断开、Ticket 过期和服务器拒绝时显示可理解错误。
- 不保存 Steam Web API Key。

### 3.2 Node 服务端

- 新增 Steam Ticket 登录接口。
- 服务端调用 Steam `AuthenticateUserTicket` 验证票据。
- 仅使用验证结果中的 SteamID64。
- 创建或查找 `steam_accounts` 与内部 `playerId` 映射。
- 一个 SteamID64 重复登录时复用同一个 playerId。
- 绑定冲突时拒绝登录并记录安全日志。
- 签发现有 JWT/Refresh 会话。
- 对 Socket 连接继续校验 JWT。

### 3.3 鉴权调用链

```text
Unity
  ↓ ISteamUser::GetAuthTicketForWebApi
Steam Ticket
  ↓ HTTPS
Fish Social Server
  ↓ ISteamUserAuth::AuthenticateUserTicket
Steamworks
  ↓ 返回可信 SteamID64
Fish Social Server
  ↓ 签发项目现有 JWT
Unity 使用 JWT 访问 REST / Socket
```

- `AuthenticateUserTicket` 必须由安全服务端调用。
- Publisher Web API Key 只存在服务端，不能放入 Unity 客户端。
- 商店页面、宣传素材、定价和正式发行配置不属于本需求前置。

### 3.4 数据模型

建议新增独立映射表，不直接把 SteamID 替换现有 playerId：

```text
steam_accounts
  id
  steam_id64 UNIQUE
  player_id UNIQUE
  app_id
  created_at
  last_login_at
  revoked_at NULL
```

实际字段命名需遵循现有数据库迁移规范。

## 4. 接口边界

建议接口：

```text
POST /api/auth/steam
Request: { ticket: string, appId: string }
Response: { ok, playerId, accessToken, refreshToken?, profile? }
```

服务端必须校验：

- Ticket 非空
- App ID 与服务端配置一致
- Steam 验证成功
- 账号绑定关系未冲突
- 请求未过期或重复滥用

## 5. 前置条件

### 必须由项目方提供

- Steamworks 合作方账号
- 已创建的 Steamworks App
- Steam App ID
- 可用于测试的 Steam 账号
- Steamworks SDK 或 Unity Steamworks 原生插件包
- 服务端 Steam Web API Key
- 本地/测试服务器地址和端口

商店页面可以保持未公开或 Coming Soon，不要求先完成正式素材、定价和发行配置。

### Steamworks 后台准备

- 为测试账号授予该 App 的访问权限。
- 配置测试分支和 Windows Depot/Build。
- 准备可以在 Steam 客户端环境启动的 Windows 测试包。
- 确认应用权限允许使用所需 Steamworks 接口。

### 必须先完成的工程条件

- `fish-social-unity/` 可构建 Windows 包
- Node 服务端现有 JWT 登录和 Socket 鉴权可运行
- 服务器环境变量有独立的 Steam 配置项
- `.gitignore` 排除本地密钥和测试配置
- 测试数据库可回滚

### 没有真实 Steam 参数时

可以先完成：

- 接口类型和错误码
- 数据库迁移
- Steam 验证服务适配器
- 本地假 Ticket 测试
- Unity 登录状态机

但不能宣称真实 Steam 登录验收完成。

## 6. 安全要求

- Web API Key 只放 Node 服务端环境变量或密钥管理服务。
- Ticket 不写入普通业务日志。
- Access Token 不写入 Unity 项目资源。
- 登录接口限流并记录失败原因。
- 绑定、拒绝、撤销和异常登录记录安全审计事件。
- 所有资产变更仍由 Node 权威处理。
- 不使用 SteamID64 直接替换现有 playerId。

## 7. 验收标准

- [ ] Steam 客户端运行时，Unity 能获取 Ticket。
- [ ] Node 能向 Steam 验证 Ticket，并拒绝伪造/错误 App ID。
- [ ] 客户端使用 `GetAuthTicketForWebApi`，服务端使用 `AuthenticateUserTicket`。
- [ ] 首次登录自动创建唯一 playerId。
- [ ] 同一 Steam 账号再次登录复用原 playerId。
- [ ] 绑定冲突、Ticket 失效和 Steam 未启动有明确错误。
- [ ] 登录成功后可签发 JWT 并连接现有 Socket。
- [ ] Web API Key、Ticket 和 Token 不进入 Git、Unity 包或普通日志。
- [ ] 本地测试和真实 Steam 测试分开，测试数据可清理。

## 8. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-12 | 主 Agent | 将 STEAM-DESKTOP-02 拆为 Steam Ticket、账号映射、JWT 会话和安全验收需求 |
