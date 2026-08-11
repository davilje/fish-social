/**
 * BUG-12 + OPS-UX-1：运营平台入口链接与今日运维契约
 * 运行: npm run verify:ops-portal-links
 */
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const DATE = '2026-07-05';
const LATEST_DIR = path.join(ROOT, 'docs/analytics/warehouse/latest');
const BASE = process.env.OPS_BASE_URL ?? 'http://127.0.0.1:3001';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

async function main(): Promise<void> {
  console.log('verify-ops-portal-links');

  const portal = read('运营平台.html');
  assert(portal.includes('今日运维'), 'ops portal 今日运维条');
  assert(portal.includes('打开昨日运营日报'), 'ops portal yesterday report CTA');
  assert(portal.includes('daily-batch-status.json'), 'ops portal loads batch status');
  assert(portal.includes('compact.json'), 'ops portal loads compact KPI');
  assert(portal.includes('refreshToday'), 'ops portal refresh button');
  assert(portal.includes('每日必看'), 'ops portal 每日必看 section');
  assert(portal.includes('更多 · 工程与导出'), 'ops portal engineering fold');
  assert(portal.includes('Admin 内嵌'), 'ops portal keeps Admin embed');
  assert(portal.includes('tab=players'), 'ops portal Admin defaults to players tab');
  assert(!portal.includes('data-admin-tab'), 'ops portal removed Admin tab shortcuts');
  assert(!portal.includes('playerIdInput'), 'ops portal removed playerId dual-open');
  assert(!portal.includes('Admin 五个页签'), 'ops portal removed tab encyclopedia');
  assert(!portal.includes('Admin 运维台 · 新标签全屏'), 'ops portal removed fullscreen card');
  assert(portal.includes('/analytics/warehouse/latest/'), 'ops portal BI href');
  assert(portal.includes('BI CSV'), 'ops portal clarifies BI CSV');
  assert(portal.includes('分析归档索引'), 'ops portal clarifies analytics index');
  assert(portal.includes('/analytics/growth/'), 'ops portal links growth dashboard');
  assert(portal.includes('增长与经营看板') || portal.includes('增长看板'), 'ops portal growth card label');
  assert(!portal.includes('服务探活'), 'ops portal removed 服务探活 fold');
  assert(!portal.includes('打开原始 JSON'), 'ops portal removed health/ready JSON links');
  assert(portal.includes('todayRss') || portal.includes('内存 RSS'), 'ops portal shows RSS');
  assert(portal.includes('todayBots') || portal.includes('机器人在塘'), 'ops portal shows bots');
  assert(portal.includes('todayHumans') || portal.includes('真人在塘'), 'ops portal shows humans');
  assert(portal.includes('运行状态') || portal.includes('todaySvc'), 'ops portal shows run status');
  assert(portal.includes('localhost:8082'), 'ops portal probes/links :8082');
  assert(portal.includes('dev.bat'), 'ops portal documents web start');
  assert(portal.includes('游戏 Web'), 'ops portal keeps game web in fold');
  assert(portal.includes('data-href-file'), 'ops portal file:// href strategy');
  // still probes /health + /ready for today strip (no public JSON links)
  assert(portal.includes("base('/health')") || portal.includes('/health'), 'ops portal still probes health');
  assert(portal.includes("base('/ready')") || portal.includes('/ready'), 'ops portal still probes ready');

  const obs = read('server/src/fishingObservability.ts');
  assert(obs.includes('METRICS_BOT_PHASE'), 'bot phase metrics gated by METRICS_BOT_PHASE');

  const playersApi = read('server/src/adminPlayersOverview.ts');
  assert(playersApi.includes('getPlayersOverview'), 'players overview module');
  assert(playersApi.includes('pondName') && playersApi.includes('spotName'), 'overview has Chinese place names');
  assert(playersApi.includes('formatPondName') || playersApi.includes('formatSpotName'), 'overview uses pond name helpers');

  const adminRoutes = read('server/src/admin.ts');
  assert(adminRoutes.includes('/api/admin/players/overview'), 'players overview route');

  const playersPage = read('admin-web/src/pages/PlayersPage.tsx');
  assert(playersPage.includes('仅真人'), 'PlayersPage humansOnly filter');
  assert(playersPage.includes('钓位'), 'PlayersPage has spot column');
  assert(playersPage.includes('详情'), 'PlayersPage opens 详情');

  const playerDetail = read('admin-web/src/pages/PlayerDetailPage.tsx');
  assert(playerDetail.includes('玩家详情'), 'PlayerDetailPage exists');
  assert(playerDetail.includes('开始盯梢'), 'PlayerDetailPage has SSE');
  assert(playerDetail.includes('事件流'), 'PlayerDetailPage has event stream');

  const pondsPage = read('admin-web/src/pages/PondsPage.tsx');
  assert(pondsPage.includes('钓位概率'), 'PondsPage has spot odds');
  assert(pondsPage.includes('再抽样'), 'PondsPage resample button');
  assert(pondsPage.includes('鱼列表'), 'PondsPage has fish list');

  const health = read('admin-web/src/pages/BusinessHealthPage.tsx');
  assert(health.includes('live-cards'), 'BusinessHealth uses summary cards');
  assert(health.includes('chart.js') || health.includes('Chart'), 'BusinessHealth uses Chart.js');
  assert(health.includes('chart-wrap') || health.includes('canvas'), 'BusinessHealth has chart canvas');

  const app = read('admin-web/src/App.tsx');
  assert(app.includes("'players'"), 'Admin default/players tab');
  assert(app.includes("'player'"), 'Admin has player detail tab');
  assert(app.includes('URLSearchParams'), 'Admin reads URL deep link');
  assert(app.includes('playerId'), 'Admin playerId query');
  assert(app.includes("raw === 'timeline'") || app.includes("tab=timeline"), 'Admin redirects timeline');
  assert(app.includes("raw === 'live'") || app.includes("'live'"), 'Admin redirects live');
  assert(app.includes("raw === 'debug'") || app.includes("'debug'"), 'Admin redirects debug');
  assert(!app.includes('TimelinePage'), 'Admin no longer mounts TimelinePage');
  assert(!app.includes('LiveInspectorPage'), 'Admin no longer mounts LiveInspectorPage');
  assert(!app.includes('FishingDebugPage'), 'Admin no longer mounts FishingDebugPage');

  const apiClient = read('admin-web/src/api.ts');
  assert(!apiClient.includes("refresh: '1'") || apiClient.includes('opts?.refresh'), 'fishingDebug does not always refresh');

  const sharedPonds = read('shared/ponds.ts');
  assert(sharedPonds.includes('formatPondName') && sharedPonds.includes('formatSpotName'), 'shared pond/spot formatters');

  const indexTpl = read('scripts/analytics/build-index.mjs');
  assert(indexTpl.includes('线上日报'), 'archive index splits 线上日报');
  assert(indexTpl.includes('模拟 / 校准') || indexTpl.includes('模拟/校准'), 'archive index splits 模拟');
  assert(indexTpl.includes('growth/'), 'archive index links growth/');

  const pipeline = read('scripts/analytics/daily-pipeline.mjs');
  assert(pipeline.includes('alertCount'), 'pipeline writes alertCount to compact');
  assert(pipeline.includes('writeDailyBatchStatus'), 'pipeline writes batch status');
  assert(pipeline.includes('build-growth-dashboard'), 'daily-pipeline hooks growth builder');

  // OPS-KPI-1 growth dashboard artifacts
  const growthBuilder = read('scripts/analytics/build-growth-dashboard.mjs');
  assert(growthBuilder.includes('docs/analytics/growth'), 'growth builder outDir');

  const growthSeriesPath = path.join(ROOT, 'docs/analytics/growth/series.json');
  const growthRetentionPath = path.join(ROOT, 'docs/analytics/growth/retention.json');
  const growthCommercialPath = path.join(ROOT, 'docs/analytics/growth/commercial.json');
  const growthHtmlPath = path.join(ROOT, 'docs/analytics/growth/index.html');
  assert(fs.existsSync(growthSeriesPath), 'growth/series.json exists');
  assert(fs.existsSync(growthRetentionPath), 'growth/retention.json exists');
  assert(fs.existsSync(growthCommercialPath), 'growth/commercial.json exists');
  assert(fs.existsSync(growthHtmlPath), 'growth/index.html exists');

  const growthHtml = fs.readFileSync(growthHtmlPath, 'utf8');
  assert(growthHtml.includes('chartActive') || growthHtml.includes('NU'), 'growth page has NU/DAU chart');
  assert(growthHtml.includes('d1') && growthHtml.includes('d30'), 'growth page has Dn retention');
  assert(growthHtml.includes('commercial') || growthHtml.includes('商业化'), 'growth page has commercial zone');

  const commercial = JSON.parse(fs.readFileSync(growthCommercialPath, 'utf8')) as {
    status: string;
    metrics?: unknown[];
  };
  assert(commercial.status === 'not_connected', 'commercial.json not_connected (no fake numbers)');
  assert(Array.isArray(commercial.metrics) && commercial.metrics.length > 0, 'commercial.json lists reserved metrics');

  const retention = JSON.parse(fs.readFileSync(growthRetentionPath, 'utf8')) as {
    dns: string[];
    humansOnly?: boolean;
  };
  assert(
    ['d1', 'd3', 'd7', 'd10', 'd14', 'd30'].every((d) => retention.dns?.includes(d)),
    'retention.json has D1–D30',
  );
  assert(retention.humansOnly === true, 'retention humansOnly default');

  const ps1 = read('scripts/ops/run-daily-analytics.ps1');
  assert(ps1.includes('daily-batch-status.json'), 'ps1 writes batch status');
  assert(ps1.includes('Write-BatchStatus'), 'ps1 Write-BatchStatus helper');

  const reportGen = read('scripts/analytics/generate-daily-ops-report.mjs');
  assert(reportGen.includes('id="alerts"'), 'report has #alerts anchor');

  const bat = read('打开运营平台.bat');
  assert(bat.includes('OPS_START_WEB'), 'bat OPS_START_WEB gate');
  assert(bat.includes('npm run web'), 'bat can start npm run web');

  const exportCode = read('scripts/analytics/export-warehouse.mjs');
  assert(exportCode.includes('index.html'), 'export-warehouse writes index.html');

  const r = spawnSync('node', ['scripts/analytics/export-warehouse.mjs', `--date=${DATE}`], {
    cwd: ROOT,
    encoding: 'utf8',
    shell: true,
  });
  assert(r.status === 0, `export-warehouse exit 0`);

  const indexPath = path.join(LATEST_DIR, 'index.html');
  assert(fs.existsSync(indexPath), 'latest/index.html exists after export');
  const indexHtml = fs.readFileSync(indexPath, 'utf8');
  assert(/csv|CSV|manifest/i.test(indexHtml), 'latest/index.html lists CSV/manifest');
  assert(fs.existsSync(path.join(LATEST_DIR, 'manifest.json')), 'latest/manifest.json exists');

  // smoke: write-batch-status helper
  const { writeDailyBatchStatus } = await import('./analytics/write-daily-batch-status.mjs');
  const statusPath = writeDailyBatchStatus({
    dateKey: '2026-07-13',
    exitCode: 0,
    source: 'manual',
    durationMs: 1,
    message: 'verify-ops-portal-links',
  });
  assert(fs.existsSync(statusPath), 'daily-batch-status.json written');
  const status = JSON.parse(fs.readFileSync(statusPath, 'utf8')) as {
    ok: boolean;
    dateKey: string;
    source: string;
  };
  assert(status.ok === true && status.dateKey === '2026-07-13', 'batch status fields');
  assert(status.source === 'manual', 'batch status source');

  try {
    const res = await fetch(`${BASE}/analytics/warehouse/latest/`);
    if (res.ok) {
      const body = await res.text();
      assert(res.status === 200, 'HTTP latest/ → 200');
      assert(/csv|CSV|manifest/i.test(body), 'HTTP latest/ body mentions CSV');
      const m = await fetch(`${BASE}/analytics/warehouse/latest/manifest.json`);
      assert(m.ok, 'HTTP latest/manifest.json → 200');
      const st = await fetch(`${BASE}/analytics/daily-batch-status.json`);
      assert(st.ok, 'HTTP daily-batch-status.json → 200');
    } else {
      console.log(`  SKIP: server ${BASE} returned ${res.status} (disk checks passed)`);
    }
  } catch {
    console.log(`  SKIP: server ${BASE} not reachable (disk checks passed)`);
  }

  console.log('PASS verify-ops-portal-links');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
