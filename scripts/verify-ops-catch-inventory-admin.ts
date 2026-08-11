/**
 * OPS-CATCH-1.1：Admin overview / 聚合 / 业务健康产量 = inventory（含 bot）
 * 运行: npm run verify:ops-catch-inventory-admin
 */
import fs from 'fs';
import os from 'os';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath, pathToFileURL } from 'url';
import Database from 'better-sqlite3';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function testSourceGuards(): void {
  console.log('\n=== TC: source guards ===');
  const overview = fs.readFileSync(path.join(ROOT, 'server/src/adminPlayersOverview.ts'), 'utf8');
  assert(overview.includes('FROM inventory'), 'overview counts inventory');
  assert(overview.includes('inventoryCatchByPlayer'), 'overview uses inventoryCatchByPlayer');
  assert(!overview.includes("'catch_accept','pending_catch_accept'"), 'overview not summing catch_accept');

  const agg = fs.readFileSync(path.join(ROOT, 'scripts/aggregate-daily-metrics.mjs'), 'utf8');
  assert(agg.includes('inventoryCatchByPlayer'), 'aggregate uses inventoryCatchByPlayer');
  assert(agg.includes('OPS-CATCH-1.1'), 'aggregate notes OPS-CATCH-1.1');

  const health = fs.readFileSync(path.join(ROOT, 'server/src/businessHealth.ts'), 'utf8');
  assert(health.includes('shanghaiDateKeyOffset'), 'health uses Shanghai dateKey');
  assert(health.includes("NOT LIKE 'bot-%'"), 'active players exclude bots');
  assert(health.includes("catchSource: 'inventory'"), 'health exposes catchSource');
}

async function testPipeline(): Promise<void> {
  console.log('\n=== TC: aggregate + overview + health ===');
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ops-catch11-'));
  const dbPath = path.join(tmpDir, 'test.db');
  const db = new Database(dbPath);
  db.exec(`
    CREATE TABLE fishing_metrics (
      id TEXT PRIMARY KEY, event_type TEXT NOT NULL, player_id TEXT, pond_id TEXT,
      payload TEXT NOT NULL DEFAULT '{}', created_at INTEGER NOT NULL
    );
    CREATE TABLE inventory (
      id TEXT PRIMARY KEY, player_id TEXT NOT NULL, species_id TEXT NOT NULL,
      quality TEXT NOT NULL, size_m REAL NOT NULL, caught_at INTEGER NOT NULL, pond_id TEXT
    );
    CREATE TABLE players (
      player_id TEXT PRIMARY KEY, nickname TEXT NOT NULL, coins INTEGER DEFAULT 0,
      share_visibility TEXT DEFAULT 'public', avatar_url TEXT, bio TEXT DEFAULT '',
      showcase_fish_ids TEXT DEFAULT '[]', created_at INTEGER NOT NULL
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
  `);

  const now = Date.now();
  const t = now - 60_000;
  db.prepare(`INSERT INTO players (player_id, nickname, created_at) VALUES (?,?,?)`).run(
    'bot-pool-001',
    '测Bot',
    now,
  );
  db.prepare(`INSERT INTO players (player_id, nickname, created_at) VALUES (?,?,?)`).run(
    'human-1',
    '真人',
    now,
  );
  const inv = db.prepare(
    `INSERT INTO inventory (id, player_id, species_id, quality, size_m, caught_at, pond_id) VALUES (?,?,?,?,?,?,?)`,
  );
  inv.run('inv-b1', 'bot-pool-001', 'carp', 'gray', 0.3, t, 'pond-calm');
  inv.run('inv-b2', 'bot-pool-001', 'carp', 'gray', 0.3, t + 1, 'pond-calm');
  inv.run('inv-h1', 'human-1', 'carp', 'green', 0.4, t, 'pond-mist');

  const met = db.prepare(
    `INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, created_at) VALUES (?,?,?,?,?,?)`,
  );
  met.run('m1', 'bite_hook', 'bot-pool-001', 'pond-calm', '{}', t);
  met.run('m2', 'bite_hook', 'human-1', 'pond-mist', '{}', t);
  met.run('m3', 'pending_catch_accept', 'human-1', 'pond-mist', '{"quality":"green"}', t);
  db.close();

  const dateKey = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date());
  const agg = spawnSync('node', ['scripts/aggregate-daily-metrics.mjs', `--date=${dateKey}`], {
    cwd: ROOT,
    env: { ...process.env, DB_PATH: dbPath },
    encoding: 'utf8',
  });
  if (agg.status !== 0) {
    console.error(agg.stdout, agg.stderr);
    throw new Error('aggregate-daily-metrics failed');
  }
  assert(true, 'aggregate exit 0');

  const db2 = new Database(dbPath, { readonly: true });
  const playerCatch = db2
    .prepare(`SELECT player_id, catch_count FROM daily_player_stats WHERE date_key = ?`)
    .all(dateKey) as Array<{ player_id: string; catch_count: number }>;
  const byPlayer = new Map(playerCatch.map((r) => [r.player_id, r.catch_count]));
  assert(byPlayer.get('bot-pool-001') === 2, 'player_stats bot catch=2 from inventory');
  assert(byPlayer.get('human-1') === 1, 'player_stats human catch=1 from inventory');

  const pondCatch = db2
    .prepare(`SELECT pond_id, catch_count, hook_count FROM daily_pond_stats WHERE date_key = ?`)
    .all(dateKey) as Array<{ pond_id: string; catch_count: number; hook_count: number }>;
  const calm = pondCatch.find((p) => p.pond_id === 'pond-calm');
  const mist = pondCatch.find((p) => p.pond_id === 'pond-mist');
  assert(calm?.catch_count === 2, 'pond-calm catch=2 (bot inventory)');
  assert(mist?.catch_count === 1, 'pond-mist catch=1');
  assert((calm?.hook_count ?? 0) + (mist?.hook_count ?? 0) === 2, 'hooks still from metrics');
  db2.close();

  process.env.DB_PATH = dbPath;
  const overviewMod = await import(
    pathToFileURL(path.join(ROOT, 'server/src/adminPlayersOverview.ts')).href
  );
  const overview = overviewMod.getPlayersOverview({ hours: 24, humansOnly: false });
  const bot = overview.rows.find((r: { playerId: string }) => r.playerId === 'bot-pool-001');
  const human = overview.rows.find((r: { playerId: string }) => r.playerId === 'human-1');
  assert(bot?.catchCount === 2, 'overview bot catchCount=2');
  assert(human?.catchCount === 1, 'overview human catchCount=1');

  const healthMod = await import(pathToFileURL(path.join(ROOT, 'server/src/businessHealth.ts')).href);
  const trend = healthMod.getBusinessHealthTrend(1);
  assert(trend.catchSource === 'inventory', 'health catchSource=inventory');
  const day = trend.daily[trend.daily.length - 1];
  assert(day?.totalCatch === 3, `health totalCatch=3 (got ${day?.totalCatch})`);
  assert(day?.dateKey === dateKey, `health dateKey Shanghai today (${day?.dateKey})`);

  try {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  } catch {
    /* ignore */
  }
}

async function main(): Promise<void> {
  console.log('verify-ops-catch-inventory-admin (OPS-CATCH-1.1)');
  testSourceGuards();
  await testPipeline();
  console.log('\nAll OPS-CATCH-1.1 checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
