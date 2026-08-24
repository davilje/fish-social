# Steam 桌面端云服务器联调（保留项）

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | 云服务器联调与切换（方案 B） |
| 编号 | **STEAM-DESKTOP-10B** |
| 类型 | 运维联调 / 保留项 |
| 负责人 | 运维 + Unity（仅改配置） |
| 状态 | **已确认**（**方案已就绪 / 实施仍可暂缓**） |
| 优先级 | P2 |
| 目标版本 | v1.0-steam-desktop |
| 设计时间 | **2026-08-20** |
| 上位规格 | [`Steam桌面端公网联调与服务器地址配置.md`](./Steam桌面端公网联调与服务器地址配置.md)（STEAM-DESKTOP-10） |
| 部署手册 | [`docs/ops/windows-cloud-deploy.md`](../../ops/windows-cloud-deploy.md)（**Windows 云主机**） |
| 阿里云对照 | [`docs/ops/windows-cloud-aliyun-checklist.md`](../../ops/windows-cloud-aliyun-checklist.md) |

> **产品决策（2026-08-20）：** 当前版本以 **本机 / 局域网联机** 为默认可用路径。上云不阻塞当前版本；需要外网玩家时再执行本规格，通过改 `server.json` 切换，无需改客户端业务代码。  
> **2026-08-24：** 已补充 **Windows Server 云主机** 逐步部署手册；真正租机落地前状态仍可保持「保留」。

---

## 1. 背景

实测路由器 WAN 为 `100.111.98.148`（CGNAT `100.64.0.0/10`），外网查询公网为 `195.114.14.226`，二者不一致 → **本机公网端口映射（方案 A）不可行**。见 STEAM-DESKTOP-10A 结论文档。

因此外网联调改为 **保留项：云主机部署**。默认目标 OS：**Windows Server**（与现有桌面端/运维习惯一致）。

---

## 2. 当前版本基线（必须保持）

| 场景 | 服务器 | 客户端 `serverBaseUrl` |
|------|--------|------------------------|
| 本机自测 | 本机 `dev.bat` / `npm run server` | `http://localhost:3001` |
| 局域网联机 | 同一 WiFi 下的房主本机 | `http://房主局域网IP:3001`（如 `192.168.1.7`） |
| 公网（本方案） | Windows 云主机 Node | `http://云公网IP:3001` |

能力依赖（已实现，勿回退）：

- STEAM-DESKTOP-10：`server.json` / `FISH_SOCIAL_SERVER_URL`
- 设置页保存地址与测试 `/health`
- Steam Ticket 登录不限制地区（只要网络可达服务器）

---

## 3. 上云切换（摘要）

完整步骤见 **[`windows-cloud-deploy.md`](../../ops/windows-cloud-deploy.md)**。摘要：

```text
1. 租 Windows 云主机（独立公网 IPv4），安全组放行 TCP 3001（RDP 仅运维 IP）
2. 安装 Node 20 + VS Build Tools；拉取代码；npm ci；配置生产 .env
3. npm run server 或 NSSM 注册服务；探活 http://云公网IP:3001/health
4. 各客户端 server.json → { "serverBaseUrl": "http://云公网IP:3001" }
5. 重启客户端 → Steam 登录
```

切回本机 / 局域网：把 `server.json` 改回 `localhost` 或局域网 IP 并重启客户端即可。

### 非目标（本保留项仍不做）

- 不自动租云、不写云厂商 IaC
- 本阶段不强制 HTTPS / 域名证书
- 不迁移移动端旧档；不在云上跑 Expo `:8082`

---

## 4. 验收标准（仅在真正上云时勾选）

- [ ] 云主机 `/health` 外网可达
- [ ] 客户端仅改 `server.json` 即可从本机切换到云
- [ ] 再改回局域网地址可恢复本地联机
- [ ] Steam 登录在云地址下可用
- [ ] `OPS_STATIC_ENABLED=false`；Steam Key 未进客户端

---

## 5. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-08-20 | 策划 Agent | 立项为保留项；确认当前版本以本机/局域网联机为准，上云随时可切换 |
| 2026-08-24 | 运维/策划 | 挂接 Windows 云主机部署手册 `docs/ops/windows-cloud-deploy.md` |
| 2026-08-24 | 运维/策划 | 补充阿里云对照清单与行业选型 `docs/ops/windows-cloud-aliyun-checklist.md` |
