<!-- 来源: docs/planning/specs/BUG修复-BI与游戏Web入口不可用.md（BUG-12） -->
<!-- 用途: 运维入口 + 分析导出 — BI latest 404 / 游戏 Web :8082 可达性 -->

你是 Fish Social **全栈开发 Agent**（偏运维入口与分析脚本）。修复 **BUG-12：BI latest 404 与游戏 Web 端口打不开**。

目标：策划从 `http://localhost:3001/ops/` 点「BI CSV（latest）」**不再 404**；点「游戏 Web」时**要么能开，要么明确提示需 `dev.bat` / `npm run web`**（可选 bat 自动拉起 :8082）。

## 必读

1. [`docs/planning/specs/BUG修复-BI与游戏Web入口不可用.md`](../specs/BUG修复-BI与游戏Web入口不可用.md) — **权威：根因 §3、方案 §4、验收 §5**
2. 关联：
   - [`docs/ops/warehouse-export.md`](../../ops/warehouse-export.md)
   - [`docs/planning/specs/数据平台-DP-D-BI与合规交接.md`](../specs/数据平台-DP-D-BI与合规交接.md)（D-L3-06 产物约定）
3. 现有文件：
   - 根目录 `运营平台.html`（BI / 游戏 Web 卡片与 `/health` 探活）
   - `打开运营平台.bat`（**当前只起 :3001**）
   - `scripts/analytics/export-warehouse.mjs`（写 `docs/analytics/warehouse/{date}/` + 复制到 `latest/`）
   - `server/src/createApp.ts`（`express.static` → `/analytics`）
   - `README.md`（运维 / 开发入口说明）

## 背景（勿误修）

| 表象 | 真因 |
|------|------|
| BI 打不开 | `/analytics/warehouse/latest/` **无 `index.html`** → static **404**；CSV/`manifest.json` **文件本身 200** |
| 游戏 Web 打不开 | ops bat **不起 Expo :8082**；需 `dev.bat` / `npm run web` |

**不要**改钓鱼逻辑、Admin API、日报聚合口径。

---

## 本次范围（必须）

| ID | 任务 |
|----|------|
| A | `latest/index.html`（导出时自动生成）+ 运营平台 BI 链接可用 |
| B | 运营平台对 `:8082` 探活与文案；bat/文档对齐 |
| C | 验收：手动 curl + 可选小脚本；更新 spec / 计划表 |

## 明确不做（P1 可另开）

- 排查游戏服 HTTP 假死根因（事件循环）
- Metabase / `WAREHOUSE_UPLOAD_URL`
- 改 `dev.bat` / `start_dev.py` 核心行为（除非为文档一致性小改）

---

## 任务 A — BI 入口（P0）

### A1 导出后生成索引页

改 `scripts/analytics/export-warehouse.mjs`：在复制 CSV + `manifest.json` 到 `latest/` 之后，**写入** `docs/analytics/warehouse/latest/index.html`。

页面要求：

- 中文说明：聚合 CSV，建议 Excel 打开；默认无明文 playerId
- 列出当次 `manifest.json` 中的文件（或磁盘上的 `*.csv` + `manifest.json`）为相对链接
- 展示 `dateKey` / 生成时间（读 manifest）
- 链回 `/analytics/index.html`、`/ops/`（相对或绝对均可，以经 `:3001` 打开为准）
- 样式简洁即可，可参考 `docs/analytics/index.html` 配色，勿引入构建工具

可选（推荐）：同步写一份到 `docs/analytics/warehouse/{dateKey}/index.html`。

可选：`docs/analytics/warehouse/index.html` 列出已有日期目录（读目录或只链 latest）——非阻塞。

### A2 运营平台卡片

改根目录 `运营平台.html`：

- BI 卡片保持 `/analytics/warehouse/latest/`（有 index 后即可），副文案改为「CSV 下载页 / Excel」
- 若仍担心目录问题，可增加次要链接到 `manifest.json`

`docs/ops/` 下若有旧跳转页，保持指向 `/ops/`，不必复制整页。

### A3 补种现有 latest

对本机已有 `docs/analytics/warehouse/latest/`：**要么**跑一次：

```bash
npm run analytics:export-warehouse -- --date=YYYY-MM-DD
```

（选有数据的日期，或脚本支持的最近日）  
**要么**在 PR 内提交一份合理的 `latest/index.html`（下次导出覆盖）。勿提交密钥。

---

## 任务 B — 游戏 Web 可达性（P0）

### B1 运营平台探活

在 `运营平台.html`（与现有 `/health` pill 同脚本风格）：

1. 探测 `http://localhost:8082/`（`file:` 协议下用绝对 URL；经 `:3001` 打开时也用绝对 `http://localhost:8082/`，避免同源误判）
2. 成功：游戏 Web 卡片可点，可选小徽章「Web 在线」
3. 失败：卡片灰显或保留链接但旁边 **醒目提示**：「游戏 Web 未启动 — 请运行 `dev.bat` 或 `npm run web`」
4. 脚注写清：**运维入口（:3001）≠ 游戏客户端（:8082）**

### B2 `打开运营平台.bat`

在现有起服逻辑之后增加 **8082 探测**：

- 若已 200：打印 `[OK] 游戏 Web 已在运行`
- 若未起：二选一实现（优先 1）：
  1. **默认自动**：`start "fish-social-web" /MIN cmd /c "npm run web"`，等待最多 ~60s 探测 8082（失败只 warn，不阻断打开 `/ops/`）
  2. 或环境变量 / 参数 `OPS_START_WEB=0` 可关闭自动拉起

文档字符串更新 bat 顶部 REM。

### B3 README

在「运营与策划入口」表旁补一句：玩客户端用 `dev.bat`；仅运维可用 `打开运营平台.bat`（现已可选拉 Web）。

---

## 任务 C — 验收

手动（游戏服需在跑）：

```bash
# A：目录不再 404
curl -s -o NUL -w "%{http_code}" http://127.0.0.1:3001/analytics/warehouse/latest/
# 期望 200，body 含 csv 或「CSV」字样

curl -s -o NUL -w "%{http_code}" http://127.0.0.1:3001/analytics/warehouse/latest/manifest.json
# 期望 200

# 导出后 index 仍在
npm run analytics:export-warehouse -- --date=<已有日>
# 再 curl latest/ → 200
```

B：

1. 停掉 Expo 后刷新 `/ops/` → 应提示 Web 未启动  
2. `打开运营平台.bat`（或手动 `npm run web`）后 → 探活变绿，http://localhost:8082/ 可开  
3. `dev.bat` 全量：`:3001/ops/` 与 `:8082` 均可

可选：小脚本 `scripts/verify-ops-portal-links.ts` 检查 latest/ → 200（若仓库无 verify 惯例可只手动）。

---

## 完成后

1. Spec `BUG修复-BI与游戏Web入口不可用.md` 状态 → **已实现**，补完成时间与变更记录
2. `scripts/planning/build-master-plan-xlsx.py`：BUG-12 → `已实现` + 完成时间当天，然后：

```bash
npm run planning:master-xlsx
```

3. 更新 `docs/planning/specs/README.md`、`docs/planning/CHANGELOG.md`（简短一行）
4. 回复：改动文件列表 + curl/探活结果摘要

## commit 建议（仅当用户要求提交时）

```text
fix(ops): make BI warehouse latest browsable and clarify game web :8082

Export writes latest/index.html so /analytics/warehouse/latest/ is not 404.
Ops portal probes Expo web and bat can start npm run web when missing.
```

---

## 交给 Agent 的一句话

`@docs/planning/prompts/bugfix-bi-web-portal-dev.prompt.md` 按此实现 BUG-12
