/**
 * 运营日报 v1.1 验收（MVP + R1/R2/R3）
 * 运行: npm run verify:daily-ops-report
 */
import fs from 'fs';
import os from 'os';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath, pathToFileURL } from 'url';
import Database from 'better-sqlite3';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const TEST_DATE = '2026-07-05';
const RULES_TEST = 'v0.4.1-test';

const MVP_KPI_IDS = [
  'kpi_daily_catch',
  'kpi_dau',
  'kpi_fishing_dau',
  'kpi_catch_per_fisher',
  'kpi_disconnect_rate',
  'kpi_abandon_rate',
  'kpi_avg_pop_ratio',
];

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

async function loadDateUtils() {
  return import(pathToFileURL(path.join(ROOT, 'scripts/analytics/date-utils.mjs')).href) as Promise<{
    shanghaiDayBounds: (d: string) => { dayStartMs: number; dayEndMs: number };
  }>;
}

function dayStart(dateKey: string, utils: { shanghaiDayBounds: (d: string) => { dayStartMs: number } }) {
  return utils.shanghaiDayBounds(dateKey).dayStartMs;
}

async function testShanghaiBounds(): Promise<void> {
  console.log('\n=== TC: Shanghai day bounds ===');
  const { shanghaiDayBounds } = await loadDateUtils();
  const b = shanghaiDayBounds('2026-07-11');
  assert(b.dayStartMs === Date.parse('2026-07-10T16:00:00.000Z'), '2026-07-11 start');
  assert(b.dayEndMs === Date.parse('2026-07-11T16:00:00.000Z'), '2026-07-11 end');
}

async function setupTestDb(dbPath: string): Promise<void> {
  const utils = await loadDateUtils();
  const db = new Database(dbPath);
  db.exec(`
    CREATE TABLE fishing_metrics (
      id TEXT PRIMARY KEY, event_type TEXT NOT NULL, player_id TEXT, pond_id TEXT,
      payload TEXT NOT NULL DEFAULT '{}', created_at INTEGER NOT NULL, dedup_key TEXT
    );
    CREATE TABLE daily_player_stats (
      date_key TEXT NOT NULL, player_id TEXT NOT NULL, catch_count INTEGER DEFAULT 0,
      escape_count INTEGER DEFAULT 0, disconnect_count INTEGER DEFAULT 0, fishing_ms INTEGER DEFAULT 0,
      PRIMARY KEY (date_key, player_id)
    );
    CREATE TABLE daily_pond_stats (
      date_key TEXT NOT NULL, pond_id TEXT NOT NULL, catch_count INTEGER DEFAULT 0,
      bite_tick_hit INTEGER DEFAULT 0, bite_tick_miss INTEGER DEFAULT 0, disconnect_count INTEGER DEFAULT 0,
      avg_population REAL, hook_count INTEGER DEFAULT 0, escape_count INTEGER DEFAULT 0,
      PRIMARY KEY (date_key, pond_id)
    );
    CREATE TABLE daily_pond_ecology (
      date_key TEXT NOT NULL, pond_id TEXT NOT NULL, population INTEGER DEFAULT 0,
      max_population INTEGER DEFAULT 0, pop_ratio REAL, quality_json TEXT DEFAULT '{}',
      avg_size_m REAL, PRIMARY KEY (date_key, pond_id)
    );
    CREATE TABLE pond_fish (
      id TEXT PRIMARY KEY, pond_id TEXT NOT NULL, species_id TEXT NOT NULL, quality TEXT NOT NULL,
      size_m REAL NOT NULL, born_at INTEGER NOT NULL, generation INTEGER DEFAULT 0, bite_weight REAL
    );
    CREATE TABLE players (
      player_id TEXT PRIMARY KEY, nickname TEXT NOT NULL, coins INTEGER DEFAULT 0,
      share_visibility TEXT DEFAULT 'public', avatar_url TEXT, bio TEXT DEFAULT '',
      showcase_fish_ids TEXT DEFAULT '[]', created_at INTEGER NOT NULL
    );
    CREATE TABLE game_config (config_key TEXT PRIMARY KEY, config_value TEXT NOT NULL, updated_at INTEGER NOT NULL);
    CREATE TABLE inventory (
      id TEXT PRIMARY KEY, player_id TEXT NOT NULL, species_id TEXT NOT NULL,
      quality TEXT NOT NULL, size_m REAL NOT NULL, caught_at INTEGER NOT NULL,
      pond_id TEXT
    );
  `);

  db.prepare("INSERT INTO game_config VALUES ('RULES_VERSION', ?, ?)").run(RULES_TEST, Date.now());

  const t0 = dayStart(TEST_DATE, utils);
  const t = (h: number) => t0 + h * 3_600_000;
  const tDay = (dateKey: string, h: number) => dayStart(dateKey, utils) + h * 3_600_000;

  const insert = db.prepare(
    `INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, created_at, dedup_key) VALUES (?,?,?,?,?,?,?)`,
  );

  const events: Array<[string, string, string | null, string | null, string, number, string | null]> = [
    ['e1', 'fishing_start', 'p1', 'pond-calm', '{}', t(1), null],
    ['e2', 'fishing_start', 'p2', 'pond-mist', '{}', t(2), null],
    ['e3', 'join_pond_success', 'p1', 'pond-calm', '{}', t(1), null],
    ['e4', 'join_pond_success', 'p2', 'pond-mist', '{}', t(2), null],
    ['e5', 'catch_accept', 'p1', 'pond-calm', '{"quality":"green"}', t(3), 'catch-1'],
    ['e6', 'catch_accept', 'p2', 'pond-mist', '{"quality":"blue"}', t(4), 'catch-2'],
    ['e7', 'catch_accept', 'p1', 'pond-calm', '{"quality":"gray"}', t(5), 'catch-3'],
    ['e8', 'bite_hook', 'p1', 'pond-calm', '{"sessionHooks":1,"sessionMissTicks":2,"missTicksSinceLastHook":2}', t(3), null],
    ['e9', 'bite_hook', 'p2', 'pond-mist', '{"sessionHooks":1,"sessionMissTicks":1,"missTicksSinceLastHook":1}', t(4), null],
    ['e9b', 'escape', 'p2', 'pond-mist', '{"sessionHooks":1,"sessionEscapes":1}', t(4), null],
    ['e10', 'disconnect', 'p1', 'pond-calm', '{}', t(6), null],
    ['e11', 'reconnect', 'p1', 'pond-calm', '{}', t(7), null],
    ['e12', 'disconnect_timeout', 'p2', 'pond-mist', '{}', t(8), null],
    ['e13', 'join_pond_attempt', 'p1', 'pond-calm', '{}', t(1), null],
    ['e14', 'join_pond_fail', 'p3', 'pond-calm', '{}', t(2), null],
    ['e15', 'leave_pond', 'p2', 'pond-mist', '{"reason":"manual"}', t(9), null],
    ['e16', 'phase_transition_invalid', 'p1', 'pond-calm', '{}', t(10), null],
    ['e17', 'gold_earn', 'p1', null, '{"amount":200,"source":"fish_sell"}', t(11), null],
    ['e18', 'bait_buy', 'p1', null, '{"cost":500,"baitId":"worm"}', t(12), null],
    ['e19', 'fishing_start', 'p_new', 'pond-calm', '{}', tDay('2026-07-06', 2), null],
  ];

  for (const row of events) insert.run(...row);

  // OPS-CATCH-1：产量/品质/分塘以背包为准（含 bot）
  const inv = db.prepare(
    `INSERT INTO inventory (id, player_id, species_id, quality, size_m, caught_at, pond_id) VALUES (?,?,?,?,?,?,?)`,
  );
  inv.run('inv-h1', 'p1', 'carp', 'green', 0.4, t(3), 'pond-calm');
  inv.run('inv-h2', 'p2', 'trout', 'blue', 0.5, t(4), 'pond-mist');
  inv.run('inv-h3', 'p1', 'carp', 'gray', 0.3, t(5), 'pond-calm');
  for (let i = 0; i < 5; i++) {
    inv.run(`inv-bot-${i}`, `bot-x-${i}`, 'carp', 'gray', 0.25, t(3 + i * 0.1), 'pond-bamboo');
  }

  for (const [d, suffix] of [
    ['2026-07-03', 'a'],
    ['2026-07-04', 'b'],
    ['2026-07-05', 'c'],
  ] as const) {
    insert.run(`eco-sink-${suffix}`, 'bait_buy', 'p1', null, '{"cost":600}', tDay(d, 5), null);
    insert.run(`eco-faucet-${suffix}`, 'gold_earn', 'p1', null, '{"amount":50,"source":"fish_sell"}', tDay(d, 6), null);
  }

  db.prepare(
    `INSERT INTO daily_player_stats (date_key, player_id, catch_count, fishing_ms) VALUES (?, ?, ?, ?)`,
  ).run(TEST_DATE, 'p1', 2, 3_600_000);
  db.prepare(
    `INSERT INTO daily_player_stats (date_key, player_id, catch_count, fishing_ms) VALUES (?, ?, ?, ?)`,
  ).run(TEST_DATE, 'p2', 1, 1_800_000);

  db.prepare(`INSERT INTO players (player_id, nickname, created_at) VALUES (?, ?, ?)`).run(
    'p_new',
    '新玩家',
    t0 + 3_600_000,
  );

  const fishInsert = db.prepare(
    `INSERT INTO pond_fish (id, pond_id, species_id, quality, size_m, born_at, generation, bite_weight) VALUES (?,?,?,?,?,?,?,?)`,
  );
  for (let i = 0; i < 70; i++) {
    fishInsert.run(`fc-${i}`, 'pond-calm', 'carp', 'green', 0.5, Date.now(), 0, 1);
  }
  for (let i = 0; i < 65; i++) {
    fishInsert.run(`fm-${i}`, 'pond-mist', 'trout', 'blue', 0.6, Date.now(), 0, 1);
  }
  for (let i = 0; i < 36; i++) {
    fishInsert.run(`fs-${i}`, 'pond-sunset', 'tuna', 'gray', 0.4, Date.now(), 0, 1);
  }
  for (let i = 0; i < 70; i++) {
    fishInsert.run(`fb-${i}`, 'pond-bamboo', 'koi', 'green', 0.55, Date.now(), 0, 1);
  }

  db.close();
}

async function testPipeline(): Promise<string> {
  console.log('\n=== TC: daily pipeline v1.1 ===');
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'fish-daily-'));
  const dbPath = path.join(tmpDir, 'test.db');
  await setupTestDb(dbPath);

  const result = spawnSync('node', ['scripts/analytics/daily-pipeline.mjs', `--date=${TEST_DATE}`], {
    cwd: ROOT,
    env: { ...process.env, DB_PATH: dbPath },
    encoding: 'utf8',
  });
  if (result.status !== 0) {
    console.error(result.stdout);
    console.error(result.stderr);
    throw new Error('daily-pipeline failed');
  }

  const outDir = path.join(ROOT, 'docs/analytics/daily', TEST_DATE);
  assert(fs.existsSync(path.join(outDir, 'summary.json')), 'summary.json');
  assert(fs.existsSync(path.join(outDir, 'ecology-snapshot.json')), 'ecology-snapshot.json');
  assert(fs.existsSync(path.join(outDir, 'report.html')), 'report.html');

  const summary = JSON.parse(fs.readFileSync(path.join(outDir, 'summary.json'), 'utf8'));
  for (const id of MVP_KPI_IDS) assert(id in summary.kpis, `kpis.${id}`);
  assert(summary.meta.rulesVersion === RULES_TEST, 'rulesVersion from game_config');
  assert(summary.meta.rulesVersion !== 'live', 'rulesVersion not hardcoded live');

  assert(summary.sections.economy.faucetTotal === 250, 'faucet > 0');
  assert(summary.sections.economy.sinkTotal === 1100, 'sink counted');
  assert(summary.sections.economy.net === -850, 'economy net negative');

  assert(summary.sections.catch.source === 'inventory', 'catch source inventory');
  assert(summary.sections.catch.total === 8, 'inventory total 3 human + 5 bot');
  assert(summary.sections.catch.human === 3, 'inventory human 3');
  assert(summary.sections.catch.bot === 5, 'inventory bot 5');
  assert(summary.kpis.kpi_daily_catch.value === 8, 'kpi_daily_catch = inventory total');
  assert(summary.kpis.kpi_daily_catch_bot.value === 5, 'kpi_daily_catch_bot > 0');
  assert(summary.kpis.kpi_daily_catch_human.value === 3, 'kpi_daily_catch_human');

  const byQuality = summary.sections.catch.byQuality || [];
  assert(byQuality.reduce((s: number, q: { count: number }) => s + q.count, 0) === 8, 'byQuality sum = 8');
  const gray = byQuality.find((q: { quality: string }) => q.quality === 'gray');
  assert(gray?.count === 6, 'byQuality gray = 1 human + 5 bot');
  assert(gray?.bot === 5, 'byQuality gray bot split');

  const byPond = summary.sections.catch.byPond || [];
  const calm = byPond.find((p: { pondId: string }) => p.pondId === 'pond-calm');
  const mist = byPond.find((p: { pondId: string }) => p.pondId === 'pond-mist');
  const bamboo = byPond.find((p: { pondId: string }) => p.pondId === 'pond-bamboo');
  assert(calm?.catches === 2 && calm?.catchesHuman === 2, 'pond-calm inventory catches');
  assert(mist?.catches === 1 && mist?.catchesHuman === 1, 'pond-mist inventory catches');
  assert(bamboo?.catches === 5 && bamboo?.catchesBot === 5, 'pond-bamboo bot inventory catches');

  assert(summary.sections.targetCompare.actual === 8, 'targetCompare.actual = inventory total');
  assert(summary.sections.targetCompare.actualHuman === 3, 'targetCompare.actualHuman');

  assert(summary.sections.retention.cohortSize === 1, 'retention cohort');
  assert(summary.sections.retention.d1Rate === 100, 'D1 retention 100%');

  assert(summary.sections.stability.reconnectCount === 1, 'reconnect count');
  assert(summary.sections.stability.phaseInvalidCount === 1, 'phase invalid');
  assert((summary.sections.ecology.ponds || []).length === 4, 'ecology 4 ponds');

  const sunset = summary.sections.ecology.ponds.find((p: { pondId: string }) => p.pondId === 'pond-sunset');
  assert(sunset?.popRatio === 60, 'pond-sunset popRatio 60%');
  assert(
    summary.alerts.some((a: { id: string }) => a.id === 'alert_pop_low'),
    'alert_pop_low from ecology snapshot',
  );
  assert(
    summary.alerts.some((a: { id: string }) => a.id === 'alert_economy_imbalance'),
    'alert_economy_imbalance',
  );

  const html = fs.readFileSync(path.join(outDir, 'report.html'), 'utf8');
  for (const kw of ['§2', '§4', '§5', '§6', '目标对照', '异常', RULES_TEST]) {
    assert(html.includes(kw), `report contains ${kw}`);
  }

  return path.join(outDir, 'summary.json');
}

function testWebhookDryRun(summaryPath: string): void {
  console.log('\n=== TC: D-L3-08 webhook dry-run ===');
  const r = spawnSync('node', ['scripts/analytics/send-daily-alert.mjs', summaryPath], {
    cwd: ROOT,
    env: { ...process.env, DAILY_ALERT_DRY_RUN: '1', DAILY_ALERT_WEBHOOK_URL: 'http://localhost/mock' },
    encoding: 'utf8',
  });
  assert(r.status === 0, 'dry-run exit 0');
  assert(r.stdout.includes('DRY_RUN'), 'dry-run prints payload');
  assert(r.stdout.includes('alert_pop_low') || r.stdout.includes('dateKey'), 'payload has alerts');
}

function testWebhookSkip(): void {
  console.log('\n=== TC: webhook skip when unset ===');
  const summaryPath = path.join(ROOT, 'docs/analytics/daily', TEST_DATE, 'summary.json');
  const r = spawnSync('node', ['scripts/analytics/send-daily-alert.mjs', summaryPath], {
    cwd: ROOT,
    env: { ...process.env, DAILY_ALERT_WEBHOOK_URL: '', ALERT_WEBHOOK_URL: '' },
    encoding: 'utf8',
  });
  assert(r.status === 0, 'no webhook exit 0');
}

function testIndex(): void {
  console.log('\n=== TC: analytics index ===');
  const r = spawnSync('node', ['scripts/analytics/build-index.mjs'], { cwd: ROOT, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(r.stderr || r.stdout);
  const html = fs.readFileSync(path.join(ROOT, 'docs/analytics/index.html'), 'utf8');
  assert(html.includes('最新运营日报') || html.includes(`daily/${TEST_DATE}/report.html`), 'index ops link');
}

async function main(): Promise<void> {
  console.log('verify-daily-ops-report-v1 (v1.1 R1-R3)');
  await testShanghaiBounds();
  const summaryPath = await testPipeline();
  testWebhookDryRun(summaryPath);
  testWebhookSkip();
  testIndex();
  console.log('\nAll daily ops report v1.1 checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
