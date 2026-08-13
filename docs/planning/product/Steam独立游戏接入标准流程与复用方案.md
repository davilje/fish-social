# Steam 独立游戏接入标准流程与复用方案

## 文档定位

本文是 Steam 独立游戏项目的**参考方案**，用于把一次完整的 Steam + Unity + Node 接入过程沉淀为可复用模板。

- 文档性质：已文档化的参考策略，不直接代表一个待开发功能。
- 当前适用项目：Fish Social。
- 后续适用项目：新的 Steam 独立游戏、Unity Windows 客户端、Node/其他服务端。
- 核心原则：客户端负责调用 Steam，服务端负责可信身份、账号映射和业务数据权威。

---

## 一、当前项目位于策划进度表的哪一步

当前不是重新开始 Steam 转型，而是处于：

> **STEAM-DESKTOP-02「Steam 身份、账号绑定与安全会话」的开发实施阶段，尚未达到“已实现”验收。**

当前相关条目状态：

| 编号 | 当前状态 | 说明 |
|---|---|---|
| STEAM-DESKTOP-EPIC | 已确认 | Steam 桌面端转型总计划，尚未整体收口 |
| STEAM-DESKTOP-01 | 已确认 | 产品定位与信息架构已确认，仍可继续细化 |
| STEAM-DESKTOP-02 | 已确认 | 当前正在实施 Steamworks.NET、Ticket、服务端认证联调 |
| STEAM-DESKTOP-03 | 已确认 | 好友、Lobby、邀请尚未开始 |
| STEAM-DESKTOP-04 | 已实现 | Unity Windows 桌面基础壳已完成并通过构建冒烟 |
| STEAM-DESKTOP-05 | 已实现 | 空鱼塘休眠与生态离线补算已完成 |

### 当前已完成的 STEAM-DESKTOP-02 子步骤

- Unity Package Manager 已安装 Steamworks.NET。
- Unity 已能够初始化 Steam API。
- Steam 客户端通信已验证成功。
- AppID 已配置为 `2713340`。
- Unity 端已加入真实 Ticket Provider。
- `steam_api64.dll` 接入路径已确定。
- 服务端已具备 Publisher Web API 验证适配层和 `identity` 配置。

### 当前尚未完成的出口

- Unity 界面尚未接入“Steam 登录”按钮。
- 尚未从 Unity 真实获取 Ticket 并发送到 Node。
- 尚未完成一次真实 `AuthenticateUserTicket` 验证。
- 尚未完成 SteamID64 与内部 `playerId` 的真实绑定验收。
- 尚未验证登录成功后的 JWT 和 Socket 连接。
- 尚未完成真实登录失败、重复登录、错误 AppID 的验收矩阵。

因此，当前正确的策划状态仍是**已确认/开发中**，不能提前改为“已实现”。

---

## 二、可复用的标准阶段

任何新的 Steam 独立游戏都可以按以下阶段拆分。阶段之间有明确出口，允许 Unity 壳、服务端和产品设计并行，但不能跳过前置条件。

### Phase 0：产品与平台边界确认

确认以下内容：

- 目标平台：Steam Windows、Steam Deck 或其他平台。
- 游戏是否必须依赖 Steam 客户端。
- 是否使用 Steam 登录、好友、Lobby、邀请、成就、云存档。
- 账号是否新建，是否迁移旧账号。
- Node/专用服务器/Steam Networking 的权威边界。
- 客户端是否允许离线运行。

出口：

- 产品定位文档。
- 信息架构和核心循环。
- Steam 功能清单。
- 非目标清单。

### Phase 1：Steamworks 后台与账号准备

项目方提供：

- Steamworks 合作方账号。
- Steam AppID。
- 测试 Steam 账号。
- Publisher Web API Key。
- 测试服务器地址。
- Windows 测试包渠道。

后台准备：

- 测试账号拥有 App 访问权限。
- 配置 Windows Depot/Build。
- 准备未公开或 Coming Soon 的测试应用。
- 确认所需 Steamworks 接口权限。

安全规则：

- Web API Key 只放服务端。
- AppID 可以进入客户端。
- Ticket 只短暂存在内存中。
- 不把真实 Key、Ticket、JWT 提交到 Git。

### Phase 2：Unity Windows 基础壳

先建立可独立运行的客户端容器：

- Unity Windows 工程。
- 窗口、全屏、无边框。
- 托盘和后台运行。
- 主界面和功能占位。
- 设置和通知接口。
- Windows Development Build。

出口：

- 未安装 Unity Editor 的 Windows 机器可以启动。
- 关闭、隐藏、恢复、退出行为明确。
- 有稳定的启动入口，后续 Steam 和网络功能通过适配层接入。

Fish Social 的对应条目是 `STEAM-DESKTOP-04`，目前已实现。

### Phase 3：Steam 身份与安全会话

标准链路：

```text
Unity
  → SteamAPI.Init
  → SteamUser.GetAuthTicketForWebApi(identity)
  → POST /api/auth/steam
  → Node 调用 AuthenticateUserTicket
  → 获得可信 SteamID64
  → 查找或创建 playerId
  → 签发 JWT/Refresh Token
  → Unity 连接 REST/Socket
```

Unity 必须实现：

- Steam 初始化。
- Steam 未启动提示。
- Ticket 获取。
- Ticket 回调和超时。
- 登录状态机。
- 登录按钮和状态展示。
- 不保存 Publisher Web API Key。

服务端必须实现：

- `/api/auth/steam`。
- AppID 校验。
- Ticket 非空和格式校验。
- Steam Publisher API 验证。
- SteamID64 与 `playerId` 唯一映射。
- JWT/Refresh 会话。
- 限流、安全日志和稳定错误码。

推荐数据模型：

```text
steam_accounts
  steam_id64 UNIQUE
  player_id UNIQUE
  app_id
  created_at
  last_login_at
  revoked_at NULL
```

验收：

- 首次登录创建唯一玩家。
- 同一 Steam 账号再次登录复用玩家。
- 错误 Ticket、错误 AppID、重复绑定均被拒绝。
- 登录成功后可以访问现有 REST/Socket。

### Phase 4：网络薄客户端

身份完成后，再接入业务网络：

- REST Client。
- Socket.IO 或其他实时协议。
- JWT 注入。
- 断线重连。
- 服务端错误展示。
- 客户端不保存业务权威数据。

客户端只保存短期会话和非权威偏好；鱼获、库存、金币、每日额度、鱼塘状态由服务端维护。

### Phase 5：好友、Lobby 与邀请

在身份和网络稳定后再实现：

- Steam 好友列表。
- Lobby 创建与加入。
- 邀请链接或 Steam Overlay 邀请。
- Lobby 保存 `pondId` 或房间标识。
- Node 校验玩家是否有权进入目标鱼塘。

Steam Lobby 负责发现和邀请，Node 仍负责业务权限和鱼塘权威。

对应 Fish Social 条目：`STEAM-DESKTOP-03`。

### Phase 6：游戏生态和后台挂机

将实时逻辑拆成：

- 有玩家时：实时 Tick。
- 无玩家时：鱼塘休眠。
- 玩家重新进入时：根据 `last_simulated_at` 做幂等离线补算。

离线补算必须：

- 支持鱼群迁移、成长、补充。
- 使用事务。
- 可重复执行而不重复结算。
- 限制最大补算时间和步数。

对应 Fish Social 条目：`STEAM-DESKTOP-05`。

### Phase 7：Steam 发布与运营

最后再处理：

- Depot/Build 上传。
- 测试分支。
- 商店页面。
- 成就、云存档和统计。
- 崩溃日志。
- 版本升级与回滚。
- 正式发行前安全审计。

不要把商店素材、定价和正式发行配置作为 Steam 身份接入的前置阻塞项。

---

## 三、复用项目的最小目录模板

```text
project/
├─ client-unity/
│  ├─ Assets/
│  │  ├─ Plugins/x86_64/steam_api64.dll
│  │  └─ Scripts/Steam/
│  ├─ Packages/manifest.json
│  └─ steam_appid.txt              # 本地开发用
├─ server/
│  ├─ src/steamAuth.*
│  └─ migrations/
├─ shared/
├─ docs/
│  ├─ product/
│  └─ security/
├─ .env.example                    # 只放变量名和示例
└─ .gitignore
```

Unity Package Manager 推荐依赖：

```text
https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.163.0
```

本地开发还需要：

```text
steam_appid.txt
2713340
```

正式 Steam 启动时，不应依赖把 `steam_appid.txt` 放进正式包来代替 Steam 发布配置。

---

## 四、每个新 Steam 项目的参数清单

```text
项目名称：
Steam AppID：
Steamworks SDK 版本：
Unity 版本：
Steamworks.NET 版本：
Windows 构建目标：
服务端地址：
Steam Auth identity：
Publisher Web API Key 存放位置：
测试 Steam 账号：
是否迁移旧账号：
是否使用 Steam Lobby：
是否使用 Steam Networking：
是否使用云存档：
是否支持离线：
```

其中 Publisher Web API Key 只允许填写“服务端密钥管理位置”，不要填写真实 Key 到文档或客户端配置。

---

## 五、通用验收清单

### 工程

- [ ] Unity 工程可打开。
- [ ] Steamworks.NET 版本固定。
- [ ] Windows `steam_api64.dll` 架构正确。
- [ ] `steam_appid.txt` 只用于本地开发。
- [ ] Git 不包含 `Library/`、日志、密钥和真实 Ticket。

### Steam

- [ ] Steam 客户端运行时 `SteamAPI.Init` 成功。
- [ ] Steam 未运行时显示明确错误。
- [ ] AppID 校验通过。
- [ ] Ticket 能够获取。
- [ ] Ticket 不写入普通日志。

### 服务端

- [ ] Publisher API Key 只存在服务端。
- [ ] `AuthenticateUserTicket` 验证成功。
- [ ] 错误 Ticket 和错误 AppID 被拒绝。
- [ ] SteamID64 映射唯一。
- [ ] JWT/Refresh 会话正常。
- [ ] 登录接口具有限流和安全审计。

### 产品

- [ ] 登录成功后能进入主界面。
- [ ] 登录失败有普通用户可理解的提示。
- [ ] 断线和重连行为明确。
- [ ] 玩家数据仍由服务端权威维护。
- [ ] Steam 功能与游戏核心循环解耦。

---

## 六、Fish Social 当前下一步

当前继续执行 `STEAM-DESKTOP-02`，顺序如下：

1. 将 Steam 登录按钮接入 `SteamAuthController.BeginLogin()`。
2. 显示初始化、获取 Ticket、服务端验证、成功和失败状态。
3. 启动 Node 服务端并确认真实 `.env` 配置。
4. 使用测试 Steam 账号完成真实 Ticket 验证。
5. 确认首次登录创建 `playerId`。
6. 退出后再次登录，确认复用同一 `playerId`。
7. 登录成功后接入 JWT 和 Socket。
8. 完成验收后，才将 `STEAM-DESKTOP-02` 回写为“已实现”。

完成 `STEAM-DESKTOP-02` 后，再进入 `UNITY-P1/P2` 网络薄客户端和 `STEAM-DESKTOP-03` 好友/Lobby，不直接跳到完整 Steam Networking。

---

## 七、方案复用原则

每个新 Steam 游戏只替换：

- AppID。
- 测试账号。
- 游戏业务数据模型。
- 产品主循环。
- Steam 功能开关。
- 服务端域名和部署配置。

以下内容应尽量复用：

- Unity Steamworks 适配层。
- Ticket 到服务端的认证链路。
- SteamID64 到内部账号的绑定模型。
- JWT/Refresh 会话机制。
- 错误码和安全审计规范。
- Windows 桌面壳和构建检查。
- Steam 接入验收清单。

不要复用真实密钥、玩家数据、数据库文件或某个项目的硬编码 AppID。

---

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-08-12 | 根据 Fish Social 当前 Steamworks.NET 接入进度，整理阶段归属、实施出口和新 Steam 项目复用模板 |
