# Fish Social — 运营日报日批（供 Windows 任务计划调用）
# 默认聚合昨日 Asia/Shanghai；日志写入 logs/daily-analytics/

$ErrorActionPreference = 'Stop'
$RootPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location -LiteralPath $RootPath

$LogDir = Join-Path $RootPath 'logs\daily-analytics'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$LogFile = Join-Path $LogDir ("daily-" + $Stamp + ".log")
$RelLog = "logs/daily-analytics/daily-$Stamp.log"
$Started = Get-Date

function Write-Log([string]$msg) {
  $line = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $msg
  Add-Content -LiteralPath $LogFile -Value $line -Encoding UTF8
  Write-Host $line
}

function Write-BatchStatus([int]$ExitCode, [string]$Message = '') {
  $statusDir = Join-Path $RootPath 'docs\analytics'
  New-Item -ItemType Directory -Force -Path $statusDir | Out-Null
  $statusPath = Join-Path $statusDir 'daily-batch-status.json'
  $shNow = [System.TimeZoneInfo]::ConvertTimeBySystemTimeZoneId((Get-Date).ToUniversalTime(), 'China Standard Time')
  $dateKey = $shNow.AddDays(-1).ToString('yyyy-MM-dd')
  $durationMs = [int]((Get-Date) - $Started).TotalMilliseconds
  $obj = [ordered]@{
    updatedAt  = (Get-Date).ToUniversalTime().ToString('o')
    dateKey    = $dateKey
    exitCode   = $ExitCode
    ok         = ($ExitCode -eq 0)
    source     = 'scheduled-task'
    logFile    = $RelLog
    durationMs = $durationMs
  }
  if ($Message) { $obj.message = $Message }
  ($obj | ConvertTo-Json -Compress) | Set-Content -LiteralPath $statusPath -Encoding UTF8
}

Write-Log "START root=$RootPath"

# Do not name vars $EnvXxx (PowerShell parses as $env:Xxx)
$FsDotEnv = [System.IO.Path]::Combine($RootPath, '.env')
if ([System.IO.File]::Exists($FsDotEnv) -and [string]::IsNullOrEmpty($env:DB_PATH)) {
  foreach ($line in [System.IO.File]::ReadAllLines($FsDotEnv)) {
    if ($line -match '^\s*DB_PATH\s*=\s*(.+)\s*$') {
      $env:DB_PATH = $Matches[1].Trim().Trim('"').Trim("'")
      Write-Log ("DB_PATH from .env = " + $env:DB_PATH)
    }
  }
}

$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodeCmd) {
  Write-Log 'ERROR: node not found in PATH'
  Write-BatchStatus 1 'node not found in PATH'
  exit 1
}

$pipeline = Join-Path $RootPath 'scripts\analytics\daily-pipeline.mjs'
if (-not (Test-Path -LiteralPath $pipeline)) {
  Write-Log ("ERROR: missing " + $pipeline)
  Write-BatchStatus 1 'daily-pipeline.mjs missing'
  exit 1
}

$env:DAILY_BATCH_SOURCE = 'scheduled-task'
$env:DAILY_BATCH_LOG = $RelLog

$nodeExe = $nodeCmd.Source
Write-Log ("Running: " + $nodeExe + " " + $pipeline)

# Prefer direct node (not cmd/npm) so scheduled tasks cannot "succeed" without running the pipeline.
$argList = @($pipeline)
$outCap = Join-Path $LogDir ("cap-out-" + $Stamp + ".txt")
$errCap = Join-Path $LogDir ("cap-err-" + $Stamp + ".txt")
$p = Start-Process -FilePath $nodeExe -ArgumentList $argList -WorkingDirectory $RootPath -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outCap -RedirectStandardError $errCap
foreach ($cap in @($outCap, $errCap)) {
  if (Test-Path -LiteralPath $cap) {
    Get-Content -LiteralPath $cap -Encoding UTF8 -ErrorAction SilentlyContinue | ForEach-Object {
      Add-Content -LiteralPath $LogFile -Value $_ -Encoding UTF8
      Write-Host $_
    }
    Remove-Item -LiteralPath $cap -Force -ErrorAction SilentlyContinue
  }
}
$exit = $p.ExitCode
if ($null -eq $exit) { $exit = 0 }

$shNow = [System.TimeZoneInfo]::ConvertTimeBySystemTimeZoneId((Get-Date).ToUniversalTime(), 'China Standard Time')
$expectDate = $shNow.AddDays(-1).ToString('yyyy-MM-dd')
$expectReport = Join-Path $RootPath ("docs\analytics\daily\" + $expectDate + "\report.html")
if (($exit -eq 0) -and (-not (Test-Path -LiteralPath $expectReport))) {
  Write-Log ("ERROR: pipeline exit 0 but missing " + $expectReport)
  $exit = 2
}

Write-Log ("FINISH exit=" + $exit + " log=" + $LogFile + " expectDate=" + $expectDate)
Write-BatchStatus $exit
exit $exit
