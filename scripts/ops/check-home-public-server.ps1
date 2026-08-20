# STEAM-DESKTOP-10A home public-IP checklist
$ErrorActionPreference = 'Continue'
$Port = 3001
$HealthUrl = "http://127.0.0.1:$Port/health"

Write-Host ''
Write-Host '=== Fish Social home public server check (STEAM-DESKTOP-10A) ===' -ForegroundColor Cyan
Write-Host ''

Write-Host '[1] LAN IPv4:' -ForegroundColor Yellow
Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
  Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
  ForEach-Object { Write-Host ('  - ' + $_.IPAddress + '  (' + $_.InterfaceAlias + ')') }

Write-Host ''
Write-Host ('[2] Local health: ' + $HealthUrl) -ForegroundColor Yellow
try {
  $resp = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 5
  Write-Host ('  OK HTTP ' + [int]$resp.StatusCode + '  ' + $resp.Content) -ForegroundColor Green
} catch {
  Write-Host ('  FAIL  ' + $_.Exception.Message) -ForegroundColor Red
  Write-Host '  Start server with dev.bat or npm run server / server:clean' -ForegroundColor Red
}

Write-Host ''
Write-Host ('[3] Port ' + $Port + ' Listen:') -ForegroundColor Yellow
$listen = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($listen) {
  $listen | Select-Object -First 5 LocalAddress, OwningProcess | Format-Table | Out-String | Write-Host
} else {
  Write-Host '  No Listen socket (server may be down)' -ForegroundColor Red
}

Write-Host ''
Write-Host '[4] Plan A next steps (manual):' -ForegroundColor Yellow
Write-Host ('  1) Windows Firewall allow inbound TCP ' + $Port)
Write-Host ('  2) Router: WAN TCP ' + $Port + ' -> LAN_IP:' + $Port)
Write-Host '  3) Confirm WAN is a real public IP (not CGNAT)'
Write-Host ('  4) Phone 4G open http://PUBLIC_IP:' + $Port + '/health')
Write-Host '  5) Remote player server.json:'
Write-Host '     { "serverBaseUrl": "http://PUBLIC_IP:3001" }'
Write-Host '  6) Player Steam logged in with App 2713340'
Write-Host ''
Write-Host 'Client Settings can Save server URL and Test /health.'
Write-Host ''
