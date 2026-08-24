# 阿里云 Windows 对照清单 + 同类 Steam 游戏后端选型说明

配套总手册：[`windows-cloud-deploy.md`](./windows-cloud-deploy.md)  
需求编号：STEAM-DESKTOP-10B

本文两块内容：

1. **阿里云 ECS（Windows）逐步对照清单** — 按页操作即可开服  
2. **Steam 独立游戏 / 轻社交后端常见云选型** — 行业惯例与 Fish Social 当前阶段对应关系  

---

## A. 阿里云 ECS（Windows）对照清单

### A0. 推荐下单参数（内测 / 小规模联机）

| 控制台项 | 建议选择 | 备注 |
|----------|----------|------|
| 产品 | **云服务器 ECS** | 不要选「仅内网」无公网带宽的规格 |
| 地域 | 靠近玩家（如华东 1 杭州 / 华东 2 上海） | 以后可多地域，现阶段单地域即可 |
| 实例规格 | **2 vCPU / 4 GiB** 起（如 `ecs.u1-c1m2.large` 或同级共享/通用型） | 更省可用 2C2G，联调够用但余量小 |
| 镜像 | **Windows Server 2022 数据中心版**（或 2019） | 与本仓库 Windows 部署手册一致 |
| 系统盘 | ESSD / 高效云盘 **≥ 40 GiB** | 含 Node、仓库、SQLite、日志 |
| 网络 | 专有网络 VPC + 公网 IP | **分配公网 IPv4**；带宽按量或固定 3～5 Mbps 联调足够 |
| 安全组 | 新建专用组（见 A2） | 与默认组隔离更清晰 |
| 登录 | 自定义密码 / 密钥（Windows 多为密码） | 强密码；勿用弱口令 |

> 费用：Windows 镜像通常含授权费，比同规格 Linux 贵。若只跑 Node 服务、团队能接受 Linux，可改 Linux + 同一套 `.env`（见总手册非 Windows 变体）；**本页按你选定的 Windows 路径写。**

### A1. 创建实例后立刻做

- [ ] 控制台记下：**公网 IP**、**实例 ID**、地域  
- [ ] 用本机远程桌面连上（默认 3389）  
- [ ] 在 ECS 控制台确认实例状态为「运行中」、带宽非 0  

### A2. 安全组规则（阿里云控制台）

入方向建议：

| 优先级 | 协议 | 端口 | 授权对象 | 说明 |
|--------|------|------|----------|------|
| 1 | TCP | **3001/3001** | `0.0.0.0/0`（联调）或玩家 IP 段 | 游戏 API + Socket.IO |
| 2 | TCP | **3389/3389** | **仅你的家庭/公司公网 IP/32** | RDP；禁止 `0.0.0.0/0` |
| — | — | 80/443 | 本阶段可不放开 | 未做 HTTPS / 未备案域名时不必开 |

出方向：保持默认允许（需访问 Steam Web API `api.steampowered.com` 等 HTTPS）。

- [ ] 安全组已绑定到该 ECS  
- [ ] 3389 未对全球开放  

### A3. Windows 内初始化

- [ ] 安装 **Node.js 20 LTS**  
- [ ] 安装 **Git**  
- [ ] 安装 **VS Build Tools（C++）**（`better-sqlite3`）  
- [ ] 本机防火墙放行 3001：  
  ```powershell
  New-NetFirewallRule -DisplayName "FishSocial-3001" -Direction Inbound -Protocol TCP -LocalPort 3001 -Action Allow
  ```

### A4. 部署代码与配置

- [ ] `git clone` / 拷贝仓库到例如 `C:\apps\fish-social`  
- [ ] `npm ci` → `npm run build:shared`（**在云主机上装依赖，勿拷 Linux/mac 的 node_modules**）  
- [ ] 根目录建 `.env`（生产项见总手册 §4；`ALLOWED_ORIGINS` 填 `http://公网IP:3001`）  
- [ ] `OPS_STATIC_ENABLED=false`  
- [ ] Steam 四项已填；`STEAM_WEB_API_KEY` 未进客户端  

### A5. 启动与探活

- [ ] 前台：`scripts\ops\windows-cloud-start.bat` 或 `npm run server`  
- [ ] 本机：`curl.exe http://127.0.0.1:3001/health` → `ok`  
- [ ] 外网（手机 4G）：`http://公网IP:3001/health` → `ok`  
- [ ] （推荐）NSSM 注册 `FishSocialServer` 开机自启 — 见总手册 §5.2  

### A6. 客户端切换

- [ ] EXE 旁 `server.json`：`{ "serverBaseUrl": "http://公网IP:3001" }`  
- [ ] 重启客户端 → Steam 登录 → 进塘冒烟  
- [ ] 需要回本地时改回 `localhost` / 局域网 IP  

### A7. 阿里云常见坑

| 现象 | 原因 / 处理 |
|------|-------------|
| 本机 health 通、外网不通 | 安全组未放 3001，或只开了「经典网络」错误组 |
| 公网 IP 会变 | 按量公网可能变化；要固定可绑 **弹性公网 IP（EIP）** |
| RDP 被扫爆破 | 3389 限制源 IP；可改 RDP 端口或开「安全加固」 |
| 出网访问 Steam 失败 | 检查出方向、公司代理、地域网络策略 |
| Windows 授权费偏高 | 内测可先用按量短开；稳定后再转包年 |
| 后续要上 `https://域名` | 国内域名需 **ICP 备案**；本阶段用 IP:3001 可跳过 |

### A8. 与总手册字段对照

| 总手册概念 | 阿里云对应 |
|------------|------------|
| 云主机 | ECS 实例 |
| 安全组 | ECS 安全组 |
| 公网 IP | 公网 IP / EIP |
| RDP | 远程连接 / 3389 |
| 磁盘备份 | 快照 / 云盘快照（建议每周） |
| 进程常驻 | NSSM 服务（非阿里云产品，装在系统内） |

---

## B. Steam 独立游戏「社交 / 联机」后端，业界一般用什么？

没有单一标准答案，按 **游戏类型与规模** 分层。和 Fish Social 最接近的是「**权威服务端 + Steam 票据登录 + 好友/房间/实时状态**」，而不是 FPS 专用高 tick UDP 战局服。

### B1. 常见分层（行业惯例）

| 层级 | 做什么 | 独立游戏常见做法 |
|------|--------|------------------|
| **账号与会话** | Steam Ticket / OpenID → 自有 `playerId`、JWT | 自建小 API（Node/Go/.NET）或 PlayFab / Epic Online Services 等 BaaS |
| **社交** | 好友、私聊、邀请、大厅 | Steamworks 原生（Lobby/好友）+ 少量自有 REST；或 Nakama / Photon / PlayFab Parties |
| **实时同步** | 房间状态、挂机鱼塘、聊天 | **单台/少量云主机**上的 WebSocket / Socket.IO / 自研 TCP；或 Photon / Mirror + 专用会话服 |
| **战局 Dedicated Server** | 射击/MOBA 权威模拟 | AWS GameLift、多地域 VM/容器、Agones；**休闲挂机社交通常用不到这么重** |
| **存档与运营** | DB、后台、指标 | SQLite→Postgres；云厂商 RDS；对象存储放资源 |

### B2. 「云服务器」具体选型分布（独立游戏视角）

| 阶段 | 更常见的选择 | 说明 |
|------|--------------|------|
| **原型 / 内测 / 小 DAU** | **一台云虚拟机（VPS/ECS/Lightsail/Droplet）** | DigitalOcean、Linode、AWS Lightsail、阿里云/腾讯云 ECS、Azure VM 都常见；跑 Node/Go + SQLite/Postgres |
| **Steam 社交能力** | **优先用 Steamworks**（Lobby、富文本、邀请） | 减少自建「社交网络」；自有服只做权威玩法与存档 |
| **要省事的后端** | PlayFab、Nakama、Heroic Labs、AccelByte、Photon Fusion/Realtime | 买能力不买「社交网络全家桶自研」 |
| **中重度多人同步** | AWS（含 **GameLift**）、GCP、Azure + 容器/K8s | 自动扩缩、多地域、会话调度；成本与复杂度明显高于单机 |
| **国内玩家为主** | **阿里云 / 腾讯云 / 华为云** ECS 或轻量应用服务器 | 延迟与支付习惯；域名 HTTPS 需备案 |

要点：

- Steam 独立游戏**很少**一上来就自建完整「社交网络」（Feed、关注图、推荐）。多数是 **Steam 好友图 + 自有玩法服**。  
- 「类似 Fish Social」的挂机/房间/轻社交：业界默认起步就是 **1～N 台普通云主机（常 Linux）+ 反向代理可选 + 托管 DB**，而不是 GameLift 级方案。  
- **Windows 云主机**在游戏服里相对少见（授权贵、生态偏 Linux），但若团队只熟 Windows / 与现有工具链一致，用阿里云 Windows ECS **完全可行**，只是成本略高。  
- 真正「社交网络感」偏重的产品，后端往往拆成：**BaaS 社交模块 + 自有玩法微服务**，而不是一台 ECS 扛所有。

### B3. Fish Social 当前应对齐哪一档？

| 能力 | 现状 | 对应业界档位 |
|------|------|--------------|
| Steam 登录 | 自有 Node 验票 | 自建会话 API（常见） |
| Lobby / 邀请 | Steam Lobby + 自有校验 | Steamworks + 薄后端（常见） |
| 鱼塘实时 | Socket.IO 单进程 | 单机实时服（内测标配） |
| 存档 | SQLite 单文件 | 独立游戏早期标配；DAU 起来再迁 Postgres/RDS |
| 部署目标 | **阿里云 Windows ECS :3001** | 「小规模自建权威服」档，合理 |

**不需要**为了「像大厂社交网络」去上 GameLift / 微服务拆分；等外网联调跑通、有稳定在线后再评估 Linux 化、HTTPS、多实例。

### B4. 一句话结论

> Steam 独立向的轻社交联机，主流是：**Steamworks 管好友与房间入口 + 一台（或少量）云虚拟机跑权威后端**；大厂级专用游戏云（GameLift 等）留给高并发战局。Fish Social 选 **阿里云 Windows ECS** 属于「可落地的内测/小规模生产」路径，与行业早期实践一致。

---

## 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-24 | 运维/策划 | 初稿：阿里云对照清单 + Steam 独立游戏后端选型说明 |
