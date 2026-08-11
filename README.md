# Fish Social 钓鱼社交

休闲社交挂机钓鱼游戏 — Google Play 第一版 + 第二版社交系统。

## 功能

### 核心玩法
- **世界地图**：划分多个鱼塘区域，点击进入
- **二维鱼塘**：圆圈头像 + 昵称，钓鱼点位挂机
- **实时同步**：鱼塘内用户位置、状态（钓鱼中）、钓鱼时长
- **鱼塘聊天**：每个鱼塘独立聊天频道
- **背包**：钓到的鱼可查看品质、尺寸；紫色及以上品质在鱼塘内公告
- **限制**：每塘最多 20 人，每人每天最多钓鱼 8 小时

### 社交系统（v2）
- **好友**：搜索玩家、发送申请、同意/拒绝；好友可查看彼此动态、私聊
- **动态墙**：所有公开动态（受分享设置影响）
- **关注动态**：仅展示好友分享的动态
- **分享设置**：每条动态 / 默认设置支持「所有人可见」或「仅好友可见」
- **出售鱼获**：背包内鱼可出售换取金币（金币暂无其他用途）

### 机器人（v2.1）
- **随机加入**：机器人会随机进入各鱼塘，占用钓位
- **让位机制**：真人进入时若鱼塘满员或钓位已满，自动移出一名机器人
- **自动钓鱼**：机器人随机开始/停止钓鱼，停留 1~3 小时后离开；**收竿后保留原钓位**，避免频繁换位置
- **分享鱼获**：机器人钓到鱼后会随机分享到动态墙，高品质鱼触发鱼塘公告

### 账号与个人（v2.2）
- **登录页**：输入昵称登录（可选上传头像），登录后进入主界面
- **个人信息**：头像、昵称、简介、8 格收藏品展示
- **背包**：左侧展示选中鱼获详情，右侧格子列表

## 技术栈

- **移动端**：React Native + Expo（Android / Google Play / Web 预览）
- **服务端**：Node.js + Express REST + Socket.io 实时同步
- **共享包**：`shared/` 类型、鱼种、经济公式

## 快速开始

```bash
# 安装依赖
npm install

# Windows 一键启动（推荐）：释放端口 → 启动服务 → 打开浏览器；关闭窗口自动释放端口
dev.bat

# 一键启动（编译 shared + 服务端 :3001 + Web :8082）
npm run dev

# Python 一键启动（推荐：自动释放端口 + 打开浏览器）
python scripts/start_dev.py
# Windows：启动后按任意键关闭并释放端口
dev.bat
# 前台常驻（Ctrl+C 停止）
python scripts/start_dev.py

# 或分别启动
npm run server          # 服务端
npm run web             # Web 预览 http://localhost:8082
npm run mobile          # Expo 移动端
```

### Python 启动器参数

| 命令 | 作用 |
|------|------|
| `python scripts/start_dev.py` | 释放端口 → 启动 server + web → 自动打开浏览器 |
| `python scripts/start_dev.py --check` | 仅查看端口占用 |
| `python scripts/start_dev.py --no-free` | 不释放端口，直接启动 |
| `python scripts/start_dev.py --server-only` | 只启动 API 服务端 |
| `python scripts/start_dev.py --wait-key` | 启动后按任意键关闭并释放端口（**dev.bat 默认**） |

Web 端在服务端未启动时会自动进入**演示模式**，可预览地图与鱼塘界面；社交、卖鱼等功能需服务端在线。

## 端口占用与故障排除

开发时 API 默认 **3001**、Expo Web 默认 **8082**（可在根目录 `.env` 中通过 `PORT` / `EXPO_WEB_PORT` 修改，参考 `.env.example`）。

| 命令 | 作用 |
|------|------|
| `npm run ports:check` | 查看 3001 / 8082 是否被占用及对应 PID |
| `npm run ports:free` | 释放本项目 dev 进程占用的端口（交互确认） |
| `npm run ports:free -- --force` | 跳过确认直接释放 |
| `npm run dev:clean` | 释放端口后启动 `npm run dev` |
| `npm run server:clean` | 仅释放 3001 后启动服务端 |

**常见原因**：终端直接关闭窗口、`tsx watch` / `expo` 子进程未退出、重复执行 `npm run dev`、多终端各跑一套 dev。

**推荐做法**：

1. 优先用 **`dev.bat`**（或 `python scripts/start_dev.py`）：关闭窗口或 `Ctrl+C` 会自动释放 3001 / 8082
2. 在运行 dev 的终端按 `Ctrl+C`，等进程退出后再关窗口
3. 若报 `EADDRINUSE`，先 `npm run ports:check`，再 `npm run ports:free` 或 `npm run dev:clean`
4. 改端口时同步 `mobile/lib/config.ts` 中的 API 地址（或后续改为读环境变量）

**关于 8082**：`npm run dev` 只会启动**一个** Web 服务（单端口）。若用 `dev.bat` 却看到两个浏览器标签页，通常是旧版 Expo 自动打开 + 脚本再打开各一次；现已改为仅 `dev.bat` 打开一次浏览器。

详细说明见 [docs/planning/specs/服务器维护-端口占用.md](./docs/planning/specs/服务器维护-端口占用.md)。

## 运营与策划入口（根目录）

| 文件 | 用途 |
|------|------|
| `运营平台.html` / `打开运营平台.bat` | 运维总入口；bat 保证 `:3001`，并**默认尝试拉起**游戏 Web `:8082`（`OPS_START_WEB=0` 可关）→ http://localhost:3001/ops/ |
| `策划进度看板.html` | 离正式上线 / 千人运营还差多少 |
| `项目开发需求计划表.xlsx` | 进度权威表（`npm run planning:master-xlsx` 重生） |
| `开发流程说明.html` / `preview.html` | 协作流程 / 游戏预览工具 |

玩客户端请用 **`dev.bat`**（server + web）；仅运维可用 `打开运营平台.bat`。**运维入口 ≠ 游戏 Web。** Admin / health **需要游戏服进程在跑**（不必盯着 Terminal：用 bat 最小化启动即可）。纯静态页可直接双击打开。

单实例真人容量见 [`docs/ops/human-capacity.md`](docs/ops/human-capacity.md) / [`docs/planning/specs/架构-单实例容量与真人隔离-R2-3.md`](docs/planning/specs/架构-单实例容量与真人隔离-R2-3.md)（默认 `MAX_HUMAN_SOCKETS=200`）。

## 一键预览工具

双击项目根目录的 **`preview.html`** 即可在浏览器中打开游戏预览工具，无需安装依赖。功能包括：

- 功能概览与系统架构说明
- 世界地图 / 鱼塘 / 社交界面示意
- 本地钓鱼模拟器（随机鱼获与售价）
- 检测服务端与 Web 是否在线，一键跳转游戏

## 社交 API 概览

| 功能 | 端点 |
|------|------|
| 注册/档案 | `POST /api/players/register`, `GET /api/players/:id` |
| 分享设置 | `PUT /api/players/:id/settings` |
| 好友 | `POST /api/friends/request`, `accept`, `reject` |
| 动态 | `GET /api/posts/wall`, `GET /api/posts/friends/:id`, `POST /api/posts` |
| 卖鱼 | `POST /api/inventory/sell` |
| 私聊 | `GET/POST /api/dm/...` |

Socket 事件：`register_player`、`dm_message`、`friend_request`

## 策划文档

完整功能策划与工作流见 **[docs/planning/](./docs/planning/)**：

- [功能全景 v0.1.0](./docs/planning/product/v0.1.0-功能全景.md)
- [策划工作流](./docs/planning/WORKFLOW.md)

## 项目结构

```
fish-social/
├── mobile/        # Expo 客户端（地图、鱼塘、社交中心）
├── server/        # REST API + WebSocket + 内存数据
├── shared/        # 共享类型、鱼种、售价公式
└── docs/planning/ # 产品策划文档与工作流
```
