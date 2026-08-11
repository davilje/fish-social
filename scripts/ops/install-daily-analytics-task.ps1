# 安装 / 更新 Windows 计划任务：每天 00:30 跑运营日报（昨日 T-1）
# 用法（仓库根目录）：
#   npm run ops:install-daily-task
# 卸载：
#   npm run ops:uninstall-daily-task

param(
  [switch]$Unregister,
  [string]$TaskName = 'FishSocial-DailyAnalytics',
  [string]$Time = '00:30'
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Runner = Join-Path $Root 'scripts\ops\run-daily-analytics.ps1'

if (-not (Test-Path $Runner)) {
  throw "Runner not found: $Runner"
}

function Remove-TaskQuiet([string]$name) {
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & schtasks.exe /Delete /TN $name /F 1>$null 2>$null
  $ErrorActionPreference = $prev
}

if ($Unregister) {
  Remove-TaskQuiet $TaskName
  Write-Host "Unregistered task: $TaskName"
  exit 0
}

$ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$tr = "`"$ps`" -NoProfile -ExecutionPolicy Bypass -File `"$Runner`""

Remove-TaskQuiet $TaskName
$ErrorActionPreference = 'Continue'
$createOut = & schtasks.exe /Create /TN $TaskName /SC DAILY /ST $Time /RL LIMITED /F /TR $tr 2>&1
$createCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($createCode -ne 0) {
  throw "schtasks create failed (exit $createCode): $createOut"
}
Write-Host ($createOut | Out-String).Trim()

Write-Host "OK: Scheduled task '$TaskName' daily at $Time"
Write-Host "  Runner: $Runner"
Write-Host "  WorkingDirectory: set inside runner → $Root"
Write-Host "  Logs: $(Join-Path $Root 'logs\daily-analytics')"
Write-Host ""
Write-Host "Verify: schtasks /Query /TN $TaskName /V /FO LIST"
Write-Host "Test now: schtasks /Run /TN $TaskName"
