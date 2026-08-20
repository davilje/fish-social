# Steam 桌面端公网联调与服务器地址配置

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Steam 桌面端公网联调与服务器地址配置 |
| 编号 | **STEAM-DESKTOP-10** |
| 类型 | 功能 / 运维联调 |
| 负责人 | Unity 工程师（开发）+ 运维/策划（部署手册） |
| 状态 | **已实现** |
| 优先级 | P1 |
| 目标版本 | v1.0-steam-desktop |
| 设计时间 | **2026-08-20** |
| 上位规格 | [`Steam桌面端独立游戏转型计划.md`](./Steam桌面端独立游戏转型计划.md) |
| 关联 | [`Steam身份账号绑定与安全会话.md`](./Steam身份账号绑定与安全会话.md)、[`docs/ops/server-env.md`](../../ops/server-env.md) |

---

## 1. 背景与目标

### 1.1 背景

当前 Node 服务默认在本机运行，Unity 桌面端将 `serverBaseUrl` 写死为 `http://localhost:3001`。外部 Steam 用户即使拥有有效 Ticket，也连不到本机服务。

公网联调需要同时解决：

1. 服务端可被外网访问；
2. 客户端能指向正确的服务器地址，且换 IP / 迁云时不必改代码重编。

### 1.2 目标

- 选定可重复执行的小规模公网联调方案：**本机开发 → 本机/局域网验收 → 需要外网时再上云**。
- Unity 启动时从配置读取 `serverBaseUrl`，统一用于 Steam 登录、REST 与 Socket.IO。
- 文档化服务端 `.env`、探活、Steam 权限与迁云步骤，使本机服可平滑迁移到云主机。

### 1.3 非目标

- 不改造 Steam Ticket / JWT 协议。
- 不实现正式 Steam 商店分发与 Playtest 后台。
- 本阶段不做 HTTPS / WSS 与域名证书（纯 `http://公网IP:3001` 可联调）。
- 家用宽带公网映射的逐步操作与设置页保存/测连通见 **STEAM-DESKTOP-10A**。
- 不迁移移动端旧账号数据。

---

## 2. 最优方案（已确认）

```text
测试玩家 Steam 客户端
  → Unity EXE（读 server.json / 环境变量）
  → Node :3001（本机或云主机）
  → Steam Web API 验票
  → JWT + REST / Socket.IO 进塘
```

| 阶段 | 服务器 | 客户端 `serverBaseUrl` | 何时使用 |
|------|--------|------------------------|----------|
| 开发与自测 | 本机 `npm run server` | `http://localhost:3001` | **当前默认** |
| 局域网联机 | 本机局域网 IP | `http://192.168.x.x:3001` | **当前可用**（同 WiFi） |
| 公网联调（保留） | 云主机公网 IP | `http://云公网IP:3001` | 需要外网玩家时再做；见 **STEAM-DESKTOP-10B** |
| 本机公网映射 | — | — | **不可行**（本环境 CGNAT）；见 10A 废弃说明 |

**结论：** 当前版本保证本机 / 局域网联机；通过已实现的 `server.json` **随时可切换**到云服务器地址，无需改业务代码。本机公网端口映射因运营商大 NAT 放弃。

---

## 3. 用户与场景

| 角色 | 场景 | 期望结果 |
|------|------|----------|
| 开发者 | 本机启动 EXE + 本机 Node | 仍可连 `localhost` 完成日常开发 |
| 联调负责人 | 发给同网段朋友 EXE + `server.json` | 朋友用局域网 IP 登录成功 |
| 外测负责人 | 云主机放行 3001，朋友改 `server.json` | 外网 Steam 登录、Lobby、进塘可用 |
| 运维 | 本机数据迁到云 | 拷贝库文件与 `.env` 后，客户端只改地址即可继续玩 |

---

## 4. 功能范围

### 4.1 功能列表

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 可配置服务器地址 | P0 | EXE 旁 `server.json` 的 `serverBaseUrl`；环境变量 `FISH_SOCIAL_SERVER_URL` 优先覆盖 |
| 2 | 统一注入连接入口 | P0 | `SteamAuthController`、`SocialLobbyController`、Socket 客户端共用同一地址 |
| 3 | 缺省回退 | P0 | 无配置时仍为 `http://localhost:3001` |
| 4 | 启动可观察 | P1 | 日志或设置页显示当前服务器地址（不含密钥） |
| 5 | 运维联调手册 | P0（文档） | 本机 / 局域网 / 云主机 checklist（见 §6） |

### 4.2 交互与配置

`server.json` 示例（与 EXE 同目录）：

```json
{
  "serverBaseUrl": "http://123.45.67.89:3001"
}
```

优先级：

```text
FISH_SOCIAL_SERVER_URL > server.json > 代码默认 localhost:3001
```

非法 URL、空字符串时回退默认并打警告日志。

### 4.3 规则

- 禁止把 `STEAM_WEB_API_KEY`、`JWT_SECRET` 写入 Unity 或 `server.json`。
- 不改变 Steam AppID `2713340` 与 `STEAM_AUTH_IDENTITY=fish-social-server-v1` 的既有约定。
- Lobby / 鱼塘业务逻辑不变；仅解决“连哪台服务器”。

---

## 5. 技术影响

### 5.1 数据模型

无新表。服务端继续使用现有 `steam_accounts` 与 SQLite。

### 5.2 API / Socket

无新 API。客户端仍调用：

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `POST /api/auth/steam` | Ticket 登录 |
| REST | `/api/social/lobby/*` | Lobby |
| Socket | Socket.IO WebSocket | 鱼塘会话 |

### 5.3 涉及文件（预估，开发侧）

- `fish-social-unity/Assets/Scripts/Desktop/Auth/SteamAuthController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/Social/SocialLobbyController.cs`
- `fish-social-unity/Assets/Scripts/Desktop/DesktopAppBootstrap.cs`
- 新增：服务器地址解析辅助类（如 `DesktopServerConfig.cs`）
- 构建产物旁示例：`server.json.example`（可选）

策划不改 `server/` 业务源码；运维按 §6 配置现有 `.env`。

---

## 6. 运维联调手册

### 6.1 本机开发（默认路径）

```text
1. .env 开启 STEAM_AUTH_ENABLED 与 Steam 四项配置
2. npm run server
3. 浏览器确认 http://localhost:3001/health
4. Unity Development Build；无 server.json 或指向 localhost
5. Steam 登录 → 自测主流程
```

### 6.2 局域网联调

```text
1. 本机 ipconfig 取 IPv4
2. Windows 防火墙放行 3001
3. 朋友机访问 http://局域网IP:3001/health
4. 朋友 EXE 旁 server.json → http://局域网IP:3001
5. 朋友 Steam 已登录且拥有 App 2713340
```

### 6.3 公网云主机联调

```text
1. 云主机安全组放行 TCP 3001
2. 部署 Node，配置生产向 .env（强 JWT_SECRET；勿 AUTH_DISABLED）
3. 建议 OPS_STATIC_ENABLED=false
4. 外网探活 http://公网IP:3001/health
5. 测试机 server.json → http://公网IP:3001
6. 验证：Steam 登录 → 刷新好友 → 创建 Lobby → 进塘
```

生产向 `.env` 最小集：

```env
NODE_ENV=production
PORT=3001
JWT_SECRET=强随机密钥
ADMIN_SECRET=强随机密钥
PLAYER_ERASE_PEPPER=强随机密钥
ALLOWED_ORIGINS=http://公网IP:3001
STEAM_AUTH_ENABLED=true
STEAM_APP_ID=2713340
STEAM_WEB_API_KEY=仅服务端
STEAM_AUTH_IDENTITY=fish-social-server-v1
OPS_STATIC_ENABLED=false
```

### 6.4 本机迁云

```text
1. 云主机安装 Node，同步仓库或发布包
2. 拷贝 .env（轮换密钥）与 data/fish-social.db（若保留进度）
3. 启动服务并探活
4. 客户端只改 server.json 中的地址
5. 无需改 Steam 登录业务代码
```

---

## 7. 验收标准

### 7.1 开发验收（P0）

- [x] 无配置时默认连 `http://localhost:3001`，本机开发不回归。
- [x] EXE 旁 `server.json` 可切换服务器地址，无需改代码重编即可连新地址。
- [x] `FISH_SOCIAL_SERVER_URL` 优先于 `server.json`。
- [x] Steam 登录、Lobby API、Socket 进塘使用同一 `serverBaseUrl`。
- [x] 日志不打印 JWT、Steam Ticket、Web API Key。

### 7.2 联调验收（部署侧）

- [x] 目标服务器外网或局域网可访问 `/health`。
- [x] 真实 Steam 账号登录成功并获得 `playerId`。
- [ ] 创建 Lobby / 进塘主路径可用（按当前已实现功能范围）。
- [ ] 从本机切到云主机时，仅改配置即可继续联调。

---

## 8. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 家用无公网 IP / 运营商大 NAT | 优先云主机；家用映射仅备选 |
| 客户端仍写死 localhost | 本需求 P0 消除 |
| 公网暴露 Admin / 运维页 | `OPS_STATIC_ENABLED=false` |
| Steam App 权限不足 | 测试账号需拥有 2713340 |
| SQLite 单文件并发与备份 | 小规模可接受；迁云时显式备份 |

---

## 9. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-20 | 策划 Agent | 初稿：最优方案为本机开发可迁云；P0 可配置 serverBaseUrl；登记 STEAM-DESKTOP-10 |
| 2026-08-20 | Unity 开发 | 已实现 DesktopServerConfig、Bootstrap 注入、设置页展示与 server.json.example；待用户 Windows Build 验收 |
| 2026-08-20 | 用户验收 | Windows Build：`source=file:.../server.json`，设置页显示 localhost:3001，Steam 登录正常；P0 验收通过 |
| 2026-08-20 | 策划 Agent | 方案 A 本机公网映射拆出 STEAM-DESKTOP-10A |
| 2026-08-20 | 策划 Agent | 确认基线=本机/局域网；上云为保留项 STEAM-DESKTOP-10B；10A 因 CGNAT 废弃 |
