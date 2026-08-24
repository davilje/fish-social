# Windows 云服务器部署方案（STEAM-DESKTOP-10B）

面向 **Windows Server 云主机** 的 Fish Social 游戏服部署手册。  
客户端仍通过已实现的 `server.json` / 设置页切换地址，**无需改业务代码重编**。

上位规格：[`Steam桌面端云服务器联调保留.md`](../planning/specs/Steam桌面端云服务器联调保留.md)  
环境变量权威表：[`server-env.md`](./server-env.md) · 模板：仓库根 [`.env.example`](../../.env.example)  
**阿里云逐步对照 + 行业选型：** [`windows-cloud-aliyun-checklist.md`](./windows-cloud-aliyun-checklist.md)

---

## 0. 结论与范围

| 项 | 选择 |
|----|------|
| 主机 OS | **Windows Server 2019/2022**（或同级云镜像） |
| 运行时 | **Node.js 20 LTS**（与 Dockerfile 对齐） |
| 进程 | `npm run server`（tsx 跑 `server/src`）+ **NSSM/WinSW 常驻** |
| 对外端口 | **TCP 3001**（HTTP + Socket.IO） |
| 本阶段 HTTPS | **不做**（联调用 `http://公网IP:3001`） |
| 数据库 | 默认 **SQLite** `data/fish-social.db` |
| 非目标 | 不写云厂商 IaC；不部署 Expo Web `:8082`；不暴露 `/ops` 静态页 |

---

## 1. 机器与网络选型

### 1.1 建议规格（小规模联调 / 内测）

| 资源 | 建议 |
|------|------|
| CPU | 2 vCPU |
| 内存 | 4 GB（≥2 GB 可跑，4 GB 更稳） |
| 磁盘 | 40 GB SSD |
| 公网 | **独立公网 IPv4**（勿选仅内网/无公网机型） |
| 地域 | 靠近玩家；国内云注意备案若后续挂域名/80/443 |

### 1.2 云安全组 / 防火墙（两层都要开）

| 方向 | 端口 | 来源 | 说明 |
|------|------|------|------|
| 入站 | TCP **3001** | 需要联机的玩家 IP，或临时 `0.0.0.0/0`（联调） | 游戏 API + Socket |
| 入站 | TCP **3389** | **仅运维 IP** | RDP；勿对全球开放 |
| 出站 | HTTPS 443 | 任意 | Steam Web API 验票 |

云控制台安全组放行后，还须在 Windows 本机防火墙放行（见 §4）。

---

## 2. 系统准备（首次）

用 RDP 登录云主机后：

1. **安装 Node.js 20 LTS**（官网 MSI，勾选加入 PATH）  
   ```powershell
   node -v   # 期望 v20.x
   npm -v
   ```
2. **安装 Git for Windows**（若用 git clone 发布）
3. **安装 Visual Studio Build Tools**（`better-sqlite3` 原生模块需要）  
   - 工作负载：**使用 C++ 的桌面开发**，或至少 MSVC + Windows SDK  
4. （可选）安装 **NSSM** 或 **WinSW**，用于开机自启与崩溃拉起

---

## 3. 代码与依赖

任选一种发布方式。

### 3.A Git 拉取（推荐联调）

```powershell
cd C:\apps
git clone <你的仓库URL> fish-social
cd C:\apps\fish-social
git checkout <发布分支或 tag>
npm ci
npm run build:shared
```

### 3.B 打包拷贝

在开发机：

```powershell
# 不要把 .env、data/*.db、node_modules 打进「源码包」时再在云上 npm ci
```

拷到云主机后同样执行 `npm ci` + `npm run build:shared`。

> **注意：** `better-sqlite3` 必须在 **目标 Windows 架构** 上编译/安装，不要从 macOS/Linux 拷贝 `node_modules`。

---

## 4. 生产环境变量

在仓库根目录创建 `.env`（**勿提交 Git**）：

```env
NODE_ENV=production
PORT=3001

JWT_SECRET=请换成足够长的随机串
ADMIN_SECRET=请换成足够长的随机串
PLAYER_ERASE_PEPPER=请换成足够长的随机串

# CORS：桌面端多为原生 HTTP，可先写云地址；若仅 Unity 原生客户端，可按实测再收紧
ALLOWED_ORIGINS=http://你的公网IP:3001

STEAM_AUTH_ENABLED=true
STEAM_APP_ID=2713340
STEAM_WEB_API_KEY=仅放在服务端的密钥
STEAM_AUTH_IDENTITY=fish-social-server-v1

# 公网务必关闭运维静态页
OPS_STATIC_ENABLED=false

# 禁止
# AUTH_DISABLED=1
# FISHING_TEST_MODE=instant
```

密钥生成示例（PowerShell）：

```powershell
-join ((48..57 + 65..90 + 97..122) | Get-Random -Count 48 | ForEach-Object {[char]$_})
```

### Windows 防火墙

管理员 PowerShell：

```powershell
New-NetFirewallRule -DisplayName "FishSocial-3001" -Direction Inbound -Protocol TCP -LocalPort 3001 -Action Allow
```

---

## 5. 启动与常驻

### 5.1 前台验证（先跑通再挂服务）

```powershell
cd C:\apps\fish-social
npm run server
```

期望日志含 `Fish Social server running on http://localhost:3001`。

本机探活：

```powershell
curl.exe http://127.0.0.1:3001/health
# 期望 {"ok":true,...}
```

外网探活（手机 4G 或另一台机器）：

```text
http://公网IP:3001/health
```

仓库也提供入口脚本：[`scripts/ops/windows-cloud-start.bat`](../../scripts/ops/windows-cloud-start.bat)

### 5.2 用 NSSM 注册 Windows 服务（推荐）

1. 下载 [NSSM](https://nssm.cc/download)，解压到例如 `C:\tools\nssm`
2. 管理员 CMD：

```bat
nssm install FishSocialServer "C:\Program Files\nodejs\npm.cmd" "run server"
nssm set FishSocialServer AppDirectory C:\apps\fish-social
nssm set FishSocialServer AppEnvironmentExtra NODE_ENV=production
nssm set FishSocialServer AppStdout C:\apps\fish-social\logs\service-stdout.log
nssm set FishSocialServer AppStderr C:\apps\fish-social\logs\service-stderr.log
nssm set FishSocialServer AppRotateFiles 1
nssm set FishSocialServer Start SERVICE_AUTO_START
nssm start FishSocialServer
```

说明：根目录 `.env` 仍由进程内加载；`AppEnvironmentExtra` 仅作补充。确保服务账户对 `C:\apps\fish-social\data` 有写权限。

### 5.3 备选：计划任务

「当用户登录时」或「启动时」运行 `windows-cloud-start.bat`，可靠性不如 NSSM。

---

## 6. 客户端切换（无需重编）

各 Steam 客户端 EXE 旁 `server.json`：

```json
{
  "serverBaseUrl": "http://公网IP:3001"
}
```

或设置页填写同一地址 → 保存 → **重启客户端** → Steam 登录。

切回本机 / 局域网：改回 `http://localhost:3001` 或 `http://局域网IP:3001` 并重启。

---

## 7. 数据与备份

| 路径 | 说明 |
|------|------|
| `data/fish-social.db` | 主库；迁云可拷贝开发机库以保留进度 |
| `data/backups/` | `npm run db:backup` 输出 |

建议：

1. 上线前、改配置前各做一次备份  
2. 用「任务计划程序」每日执行 `npm run db:backup`（在仓库根）  
3. 定期把备份拷出云主机

恢复见 [`db-backup-restore.md`](./db-backup-restore.md)。

---

## 8. 安全清单（上线前勾选）

- [ ] `NODE_ENV=production`，未开 `AUTH_DISABLED` / `FISHING_TEST_MODE`
- [ ] `JWT_SECRET` / `ADMIN_SECRET` / `PLAYER_ERASE_PEPPER` 均为强随机且与开发机不同
- [ ] `STEAM_WEB_API_KEY` 仅在服务器 `.env`，未进 Git / Unity / 客户端包
- [ ] `OPS_STATIC_ENABLED=false`
- [ ] RDP（3389）仅运维 IP；管理员密码强且非常用弱口令
- [ ] 云安全组 + Windows 防火墙均仅必要端口
- [ ] 外网 `/health` 200；`/ops/` 不可用或 404（静态已关）

---

## 9. 验收步骤

1. 云主机 `http://127.0.0.1:3001/health` → 200  
2. 外网 `http://公网IP:3001/health` → 200  
3. 客户端 `server.json` 指向云地址，重启后 Steam 登录成功  
4. 刷新好友 / 创建 Lobby / 进塘（按当前已实现范围）  
5. 改回局域网地址可恢复本地联机  

全部通过后，可将 **STEAM-DESKTOP-10B** 从「保留」推进为「已实现」并回写计划表。

---

## 10. 排障

| 现象 | 排查 |
|------|------|
| 本机 health 通、外网不通 | 云安全组、Windows 防火墙、是否真有公网 IP |
| Steam 登录失败 | `.env` Steam 四项、`STEAM_WEB_API_KEY`、服务器出网访问 Steam |
| `better-sqlite3` 安装失败 | 未装 VS Build Tools；或用了跨平台拷贝的 `node_modules` |
| 服务启动即退出 | 看 `logs/service-stderr.log`；常见为缺 `.env` 必填项 |
| CORS / 浏览器联调失败 | `ALLOWED_ORIGINS` 是否包含实际 Origin（Unity 原生一般不依赖 CORS） |
| 端口被占 | `netstat -ano \| findstr :3001` |

---

## 11. 回滚

1. NSSM：`nssm stop FishSocialServer`  
2. 恢复 `data/fish-social.db` 备份  
3. 如需退版本：`git checkout <旧 tag>` → `npm ci` → `npm run build:shared` → 启动  
4. 客户端改回原 `serverBaseUrl`

---

## 12. 后续可选项（本方案不做）

- IIS / Caddy / nginx 反代 + HTTPS 域名  
- Docker Desktop on Windows（可用仓库 `Dockerfile`，但本手册以裸机 Node 为准）  
- PostgreSQL 指标双写、多实例  

---

## 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-24 | 运维/策划 | 初稿：Windows 云主机部署方案，挂靠 STEAM-DESKTOP-10B |
