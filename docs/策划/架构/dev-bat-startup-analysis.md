# dev.bat 启动分析报告

> 作者：后端维护分析 Agent (@backend-ops)
> 日期：2026-07-12
> 状态：🔴 发现多处阻塞性问题

---

## 问题现象

执行 dev.bat 或 python scripts/start_dev.py --wait-key 后，项目无法正常启动。典型症状：

1. 端口清理脚本执行但端口未被释放
2. 服务端启动超时（wait_for_http 等待 120 秒后放弃）
3. Web 客户端无法访问

---

## 文件审查清单

| 文件 | 行数 | 关键发现 |
|------|------|----------|
| scripts/ports.mjs | 339 | ⛔ **wmic 调用在 Win10 已弃用，实际执行失败** |
| scripts/clean-ports.mjs | 30 | ✅ netstat+taskkill 路径正确，但作为 predev 前置不足 |
| scripts/start_dev.py | 278 | ⚠️ 端口清理依赖 ports.mjs，受 wmic 故障牵连 |
| scripts/expo-web.mjs | 28 | ✅ 基本正确 |
| package.json | 66 | ⚠️ predev 与 start_dev.py 有重复清理，存在竞争窗口 |
| .env | 5 | ✅ 开发环境配置完整（含 AUTH_DISABLED=1） |

---

## 发现的问题

### 🔴 P0 — wmic 调用在 Windows 10/11 上执行失败

**严重度：关键 — 端口清理完全失效**

#### 根因

scripts/ports.mjs 在 3 个函数中使用了 wmic 命令：

| 函数 | 行号 | wmic 命令 | 失败后果 |
|------|------|-----------|----------|
| listNodeProcesses() | 65 | wmic process where "name='node.exe'" get ProcessId,CommandLine /format:list | 返回空数组 → 找不到残留 dev 进程 |
| getProcessCommand(pid) | 131-132 | wmic process where "ProcessId=`${pid}" get CommandLine /format:list | 返回空字符串 → 无法判断进程是否可杀 |
| getProcessName(pid) | 154-155 | wmic process where "ProcessId=`${pid}" get Name /format:list | 返回空字符串 → 同上 |

**实测结果**：在 Windows 10 专业版上执行 wmic process where "name='node.exe'" get ProcessId /format:list 报错：

`
wmic : 没有能够实现
`

而 wmic os get caption 正常工作。说明 **WMI 的 Win32_Process 提供程序在当前系统上不可用**。这是微软弃用 wmic（已公告将移除）后的常见现象。

#### 影响链

`
ports.mjs free --force
  → getListeningPids('netstat') ✓ 发现端口有 PID
  → getProcessCommand(pid)        ✗ wmic 失败 → 返回 ''
  → getProcessName(pid)           ✗ wmic 失败 → 返回 ''
  → isKillableDevProcess(pid, '') → name='', cmd='' → 返回 false ❌
  → collectFreeTargets() → 0 个可释放目标
  → ">>> 端口与 dev 进程已空闲" (误报！)
  → 端口未被释放，仍然被旧进程占用
`

#### 影响范围

- start_dev.py 调用 ports.mjs free --force → **端口和残留进程都不会被清理**
- 
pm run ports:free → **同样失效**
- 
pm run ports:check → listNodeProcesses() 返回空，不会显示任何 Fish Social 进程（即使正在运行）
- indStrayFishSocialPids() → 永远不会找到残留进程

#### 截图关键证据

`
PS> wmic process where "name='node.exe'" get ProcessId /format:list
wmic : 没有能够实现
`

---

### 🟡 P1 — predev 脚本 (clean-ports.mjs) 与 ports.mjs free 冗余且有竞争窗口

#### 根因

1. **重复逻辑**：start_dev.py 先调 ports.mjs free，然后 
pm run dev 的 predev 又调 clean-ports.mjs。同样的事做了两遍。
2. **竞争窗口**：ports.mjs free 因 wmic 故障已失效，但 clean-ports.mjs 实质上是对的（用 netstat + taskkill）。然而 clean-ports.mjs 的触发时机在 
pm run dev 开始时，而 start_dev.py 的 wait_for_http 在 
pm run dev 进程启动后立即开始。
3. **时机问题**：
pm run dev 的执行链路是 predev(clean-ports) → build:shared → concurrently(server + web)。虽然端口在 server 启动前已清理，但：
   - clean-ports.mjs 的 indstr ":3001 " 模式缺少对 IPv6 [::]:3001 的匹配（已在 clean-ports.mjs 中核实，实际测试发现 [::]:3001   包含 :3001  子串，可以匹配）

#### 建议

- 移除 predev 脚本，将此逻辑合并到 ports.mjs 中（统一出入口）
- 或：移除 start_dev.py 中对 ports.mjs free 的调用，完全依赖 predev + clean-ports.mjs

---

### 🟡 P1 — clean-ports.mjs 的 indstr 可能漏杀部分进程

#### 问题

clean-ports.mjs:8:
`js
const out = execSync(
etstat -ano | findstr ": ", ...);
`

indstr ":3001 " 的 indstr 是 Windows CMD 命令，在 PowerShell 环境下通过 execSync 调用时，如果系统 locale 非英文（如中文 Windows），indstr 的行为可能异常。

#### 建议

改用 PowerShell 的 Select-String 或直接在 Node.js 中解析 
etstat -ano 输出，避免依赖 indstr 的 locale 行为。也可复用 ports.mjs 中已有的 getListeningPids() 函数（它已用纯 JS 解析 netstat 输出）。

---

### 🟡 P2 — wait_for_http 串行等待，首启动可能超时

#### 根因

start_dev.py:183-184：
`python
api_ok = wait_for_http(f"http://localhost:{server_port}/health", timeout_sec=120)
web_ok = wait_for_http(web_url, timeout_sec=120) if npm_script == "dev" else True
`

1. **串行等待**：等 API 就绪后再等 Web，最大总等待时间 = 240 秒
2. **首次启动慢**：
pm run build:shared 需要编译 shared 库（5-15 秒），	sx watch 首次启动也需要冷编译（8-20 秒）。在旧硬件上，API 可能在 30-40 秒后才就绪，虽不至于超时但感受差。
3. **	sx watch 冷启动 vs 热启动**：第一次 	sx watch 会编译整个 server 目录的全部依赖树，后续启动有缓存会快很多。

#### 建议

- 可改并行等待（并发检测 API 和 Web），将两路 120 秒重叠。
- 或：在 wait_for_http 中添加更频繁的探测（当前 1.5 秒间隔，可缩至 0.5 秒）和指数退避。
- 或：在 
pm run dev 启动前先跑 
pm run build:shared，确保共享库已编译。

---

### 🟢 P3 — .env 缺少 JWT_SECRET，但 AUTH_DISABLED=1 已覆盖

#### 评估

当前 .env：
`
PORT=3001
EXPO_WEB_PORT=8082
NODE_ENV=development
AUTH_DISABLED=1
ADMIN_SECRET=fish-social-debug
`

- JWT_SECRET 未设置，但 AUTH_DISABLED=1 生效，服务端 ssertAuthConfigured() 会跳过 JWT 校验 → ✅
- start_dev.py 的 pply_dev_auth_defaults() 也有兜底，即使 .env 没设也会自动注入 → ✅
- 当前配置在开发模式下是完整的

**注意**：.env.example 中 AUTH_DISABLED 被注释掉，建议更新 .env.example 为实际开发推荐配置。

---

### 🟢 P3 — dev.bat 路径与 Python 环境

#### 评估

- dev.bat 使用 cd /d "%~dp0" 切换到脚本所在目录 → ✅
- where python 检查 + 友好错误提示 → ✅
- python scripts\start_dev.py --wait-key %* 传递命令行参数 → ✅
- 当前系统 Python 3.12.7 已安装 → ✅

---

## 根因总结图

`
用户执行 dev.bat
  │
  ├─ dev.bat → python start_dev.py --wait-key
  │     │
  │     ├─ load_dotenv()  ✓
  │     │
  │     ├─ ports.mjs free --force  ── [wmic 故障] ──→ ❌ 端口未清理
  │     │     │                                         │
  │     │     ├─ getListeningPids()  ✓ (netstat)        │
  │     │     ├─ listNodeProcesses() ✗ (wmic) ──────────┤
  │     │     └─ isKillableDevProcess() ✗ (缺 name/cmd) │
  │     │                                               ▼
  │     │                                     ╔══════════════════╗
  │     │                                     ║ 端口被旧进程占用 ║
  │     │                                     ║ 但脚本认为已空闲 ║
  │     │                                     ╚══════════════════╝
  │     │
  │     ├─ npm run dev
  │     │     │
  │     │     ├─ predev: clean-ports.mjs → netstat+taskkill ✓
  │     │     │     (端口此时才真正释放)
  │     │     │
  │     │     └─ dev: build:shared → concurrently(server, web)
  │     │           │
  │     │           ├─ tsx watch src/index.ts
  │     │           │     ├─ 编译慢 (冷启动 8-20s)
  │     │           │     └─ startListening() → 绑定 :3001
  │     │           │
  │     │           └─ expo-web.mjs → expo start --web :8082
  │     │
  │     └─ wait_for_http(:3001/health, 120s) → 串行
  │       wait_for_http(:8082, 120s)          → 串行
  │
  └─ 结果：端口清理不可靠 + 首次编译慢 → 启动体验差
`

---

## 修复建议

### 修复 1（P0）— 替换 ports.mjs 中所有 wmic 调用为 powershell Get-CimInstance

**目标文件**：scripts/ports.mjs

将 3 处 wmic 调用统一替换为 PowerShell Get-CimInstance：

**listNodeProcesses() 替换方案**：
`js
function listNodeProcesses() {
  const rows = [];
  if (isWin) {
    let out = '';
    try {
      out = execSync(
        'powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { `        'powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { .Name -eq \'node.exe\' } | Select-Object ProcessId,CommandLine | ConvertTo-Csv -NoTypeInformation"',.Name -eq \'node.exe\' } | Select-Object ProcessId,CommandLine | ConvertTo-Csv -NoTypeInformation"',
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }
      );
    } catch {
      return rows;
    }
    for (const line of out.split(/\r?\n/).filter(Boolean)) {
      if (line.startsWith('#')) continue; // skip CSV header
      const [cmdLine, pidStr] = line.split(',');
      const pid = Number(pidStr?.replace(/"/g, '').trim());
      const command = cmdLine?.replace(/"/g, '').trim() ?? '';
      if (pid > 0 && command) rows.push({ pid, command });
    }
    return rows;
  }
  // ... Unix 路径不变
}
`

**getProcessCommand(pid) 替换方案**：
`js
function getProcessCommand(pid) {
  if (isWin) {
    try {
      const out = execSync(
        powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter 'ProcessId=`${pid}' | Select-Object -ExpandProperty CommandLine",
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }
      );
      return out.trim();
    } catch {
      return '';
    }
  }
  // ... Unix 路径不变
}
`

**getProcessName(pid) 替换方案**：
`js
function getProcessName(pid) {
  if (isWin) {
    try {
      const out = execSync(
        powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter 'ProcessId=`${pid}' | Select-Object -ExpandProperty Name",
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }
      );
      return out.trim().toLowerCase();
    } catch {
      return '';
    }
  }
  // ... Unix 路径不变
}
`

**性能考量**：Get-CimInstance Win32_Process 每次调用会查询 WMI，但在本地开发启动的场景下（每秒最多调用几次），性能开销可忽略（<50ms/次）。

---

### 修复 2（P1）— 统一端口清理入口，消除冗余

**方案 A（推荐）**：
- 移除 package.json 中的 "predev": "node scripts/clean-ports.mjs"
- 将 clean-ports.mjs 的核心逻辑（netstat + taskkill ALL PID on port）整合到 ports.mjs 的 
unFree() 中作为 fallback
- start_dev.py 继续调用 ports.mjs free --force，由 ports.mjs 统一处理

**方案 B（最小改动）**：
- 保留 predev + clean-ports.mjs
- 修改 start_dev.py：在调用 ports.mjs free 后，直接运行 clean-ports.mjs 作为保底
- 避免依赖 wmic 的进程发现逻辑

**方案 C（最快修复）**：
- 在 ports.mjs 的 listNodeProcesses() 和 getProcessCommand()/getProcessName() 中，当 wmic 抛出异常时，回退到 	asklist + 
etstat 解析

---

### 修复 3（P2）— 优化 wait_for_http 等待体验

1. **并行检测**：将 API 和 Web 的 wait_for_http 改为 concurrent.futures.ThreadPoolExecutor 并行执行
2. **减少探测间隔**：从 1.5 秒缩至 0.5 秒，加快就绪判断
3. **增加启动进度提示**：每 10 秒打印一次"等待中…（已等待 N 秒）"提高可观测性
4. **预构建 shared**：在 start_dev.py 中，调用 ports.mjs free 后，预先运行 
pm run build:shared，缩短后续 npm run dev 的启动时间

---

### 修复 4（P3）— 更新 .env.example

将 AUTH_DISABLED=1 和 NODE_ENV=development 从注释改为推荐值，让新开发者开箱即用。

---

## 优先修复顺序

| 优先级 | 修复项 | 预估工时 | 影响范围 |
|--------|--------|----------|----------|
| **P0** | ports.mjs — 替换 wmic 为 Get-CimInstance | 1 小时 | 所有端口管理功能恢复 |
| **P1** | 统一端口清理入口，消除冗余 | 0.5 小时 | 清理逻辑简化，消除隐形竞争 |
| **P2** | wait_for_http 并行 + 缩短间隔 | 0.5 小时 | 首启动体验改善 |
| **P3** | 更新 .env.example | 0.2 小时 | 新开发者上手体验 |

---

## verify:* 建议

| verify 目标 | 覆盖项 |
|-------------|--------|
| erify:phase1-core | 应增加端到端启动测试：dev.bat → 等待 120 秒 → curl /health → 验证 200 |
| 新增 erify:port-cleanup | 启动 
ode scripts/ports.mjs check → 验证能在有 node 进程时正确发现 |
| 手动验证 | 执行 
pm run ports:check 后自己开一个 
ode -e "setInterval(()=>{},1e6)" 再跑一遍，验证能找到该进程 |

---

## 附录：wmic 故障实测日志

`
PS> wmic os get caption /format:list
Caption=Microsoft Windows 10 专业版    ← 正常

PS> wmic process where "name='node.exe'" get ProcessId /format:list
wmic : 没有能够实现                      ← 失败！Win32_Process 查询不可用

PS> wmic process where "ProcessId=0" get Name /format:list
wmic : 没有能够实现                      ← 同理失败

PS> powershell Get-CimInstance Win32_Process -Filter 'ProcessId=0' | Select-Object Name
System Idle Process                      ← PowerShell 替代方案正常
`

---

> 报告完毕。核心结论：**wmic 弃用是根因**，一旦修复，端口清理将恢复正轨。predev + clean-ports.mjs 作为临时 fallback 可以缓解，但不可靠。推荐优先 P0 修复。

