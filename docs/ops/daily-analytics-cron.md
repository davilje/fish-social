# 运营日报定时任务

## 数据前提

游戏服在统计日（Asia/Shanghai 自然日）需持续运行，并向 `fishing_metrics` 写入埋点。日报默认聚合 **昨日（T-1）** 数据。

## 运营平台入口（HTML）

浏览器打开：

[`运营平台.html`](./运营平台.html)

汇总：日报索引、对照、BI CSV、Admin 运维台、健康检查、本页定时说明。

## Windows（本机已提供一键安装）

在仓库根目录执行：

```bash
npm run ops:install-daily-task
```

会注册计划任务 **`FishSocial-DailyAnalytics`**：

| 项 | 值 |
|----|-----|
| 时间 | 每天 **00:30**（本机系统时间） |
| 动作 | `scripts/ops/run-daily-analytics.ps1` → `npm run analytics:daily` |
| 日志 | `logs/daily-analytics/` |
| 时区建议 | Windows 设为「中国标准时间」，与 Asia/Shanghai 对齐 |

> **已知故障（已修）**：runner 曾用 `$EnvFile` 读 `.env`，PowerShell 会把它解析成空的 `$env:File`，任务在 START 后立刻 exit 1，导致「没有最新线上日报」。现已改为 `[System.IO.Path]::Combine` + `$FsDotEnv`。排障先看 `logs/daily-analytics/*.log` 与 `schtasks /Query /TN FishSocial-DailyAnalytics /V` 的「上次结果」。

验证 / 立刻试跑：

```powershell
Get-ScheduledTask -TaskName FishSocial-DailyAnalytics
Start-ScheduledTask -TaskName FishSocial-DailyAnalytics
```

排障：看 `logs/daily-analytics/` 最新日志，以及 `docs/analytics/daily-batch-status.json`。  
Runner 使用 **绝对路径 `node …/daily-pipeline.mjs`**，并在 exit 0 时校验昨日 `report.html` 是否存在（避免历史上 `cmd /c npm` 空跑仍成功）。

卸载：

```bash
npm run ops:uninstall-daily-task
```

手工跑（带日志）：

```bash
npm run ops:daily
# 或
npm run analytics:daily
```

## Linux cron 示例

```cron
30 0 * * * cd /path/to/fish-social && DB_PATH=/path/to/fish-social.db npm run analytics:daily >> /var/log/fish-daily.log 2>&1
```

## 补跑历史日期

```bash
npm run analytics:daily -- --date=2026-07-05
```

产出目录：`docs/analytics/daily/<date>/report.html`

## Webhook 告警（D-L3-08）

日批末尾自动发送 `summary.alerts` 中达到阈值的告警：

| 变量 | 说明 |
|------|------|
| `DAILY_ALERT_WEBHOOK_URL` | 优先；未设则回落 `ALERT_WEBHOOK_URL` |
| `DAILY_ALERT_MIN_LEVEL` | `bad`（默认）或 `warn` |
| `DAILY_ALERT_DRY_RUN=1` | 仅打印 payload，不 POST |

无 Webhook 配置时跳过且 exit 0，不阻断日报生成。
