import Database from 'better-sqlite3';
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { migrateFishEcologyEntry } from './migrations/fish_ecology_entry.js';

type TableDigest = { columns: string[]; count: number; sha256: string };
type Snapshot = Record<string, TableDigest>;
export type EcologySeed = (database: Database.Database, batchId: string, now: number) => number;
export type EcologyRebuildResult = {
  batchId: string; alreadyCompleted: boolean; archivedCount: number; seededCount: number;
  backupPath: string; restoredCopyPath: string; manifestPath: string;
};

const quote = (name: string) => `"${name.replaceAll('"', '""')}"`;
const mutableTables = new Set([
  'pond_fish', 'pond_state', 'spot_bite_weights', 'fish_entity_archive',
  'fish_ecology_rebuild_batches', 'fish_ecology_state_archive',
]);

function tableExists(database: Database.Database, name: string): boolean {
  return !!database.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name);
}

/** Hash original columns so additive migrations do not falsely look like historical edits. */
function snapshot(database: Database.Database, template?: Snapshot): Snapshot {
  const names = template ? Object.keys(template) : (database.prepare(
    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
  ).all() as Array<{ name: string }>).map(row => row.name);
  const result: Snapshot = {};
  for (const name of names) {
    const columns = template?.[name]?.columns ?? (database.prepare(`PRAGMA table_info(${quote(name)})`).all() as Array<{name: string}>).map(row => row.name);
    const hashes: string[] = [];
    for (const row of database.prepare(`SELECT ${columns.map(quote).join(',')} FROM ${quote(name)}`).iterate()) {
      hashes.push(createHash('sha256').update(JSON.stringify(row)).digest('hex'));
    }
    hashes.sort();
    result[name] = { columns, count: hashes.length, sha256: createHash('sha256').update(hashes.join('\n')).digest('hex') };
  }
  return result;
}

function assertIntegrity(database: Database.Database): void {
  const rows = database.pragma('integrity_check') as Array<{integrity_check: string}>;
  if (rows.length !== 1 || rows[0].integrity_check !== 'ok') throw new Error('SQLite integrity_check failed');
  if ((database.pragma('foreign_key_check') as unknown[]).length > 0) throw new Error('SQLite foreign_key_check failed');
}

function assertNoUnsettledFish(database: Database.Database, now: number): void {
  const count = (sql: string) => (database.prepare(sql).get() as { n: number }).n;
  if (tableExists(database, 'pending_catch_locks') && count('SELECT COUNT(*) n FROM pending_catch_locks') > 0) {
    throw new Error('Pending catches must be settled or expired by the server before rebuilding');
  }
  if (tableExists(database, 'fish_hook_locks')) {
    const row = database.prepare('SELECT COUNT(*) n FROM fish_hook_locks WHERE expires_at > ?').get(now) as {n: number};
    if (row.n > 0) throw new Error('Unexpired hook locks prevent rebuilding');
  }
  if (tableExists(database, 'player_pond_session')) {
    const columns = database.prepare('PRAGMA table_info(player_pond_session)').all() as Array<{name: string}>;
    if (columns.some(row => row.name === 'fishing_phase') && count(
      "SELECT COUNT(*) n FROM player_pond_session WHERE fishing_phase IS NOT NULL AND fishing_phase NOT IN ('idle','seated','sleeping')",
    ) > 0) throw new Error('Active fishing sessions must be stopped before rebuilding');
  }
  if (tableExists(database, 'groundbait_casts')) {
    const row = database.prepare("SELECT COUNT(*) n FROM groundbait_casts WHERE status='active' AND expires_at>? AND portions_consumed<portions_total").get(now) as {n: number};
    if (row.n > 0) throw new Error('Active groundbait casts must expire before rebuilding');
  }
}

/** Fail closed when open-handle detection is unavailable; callers must stop the server first. */
export function assertDatabaseOffline(dbPath: string): void {
  if (process.platform === 'win32') throw new Error('Offline rebuild requires an OS open-handle check; run this tool on macOS/Linux');
  const files = [dbPath, `${dbPath}-wal`, `${dbPath}-shm`].filter(file => fs.existsSync(file));
  const check = spawnSync('lsof', ['-t', '--', ...files], { encoding: 'utf8' });
  if (check.error || (check.status !== 0 && check.status !== 1)) throw new Error('Cannot verify database is offline: lsof is unavailable or failed');
  if (check.status === 1 && check.stderr.trim()) throw new Error(`Cannot verify database is offline: ${check.stderr.trim()}`);
  const otherPids = [...new Set(check.stdout.trim().split(/\s+/).filter(Boolean).map(Number))].filter(pid => pid !== process.pid);
  if (otherPids.length) throw new Error(`Database is open in another process (${otherPids.join(', ')}); stop it before rebuilding`);
}

export function inspectEcologyRebuild(dbPath: string, now = Date.now()): { fishCount: number; ponds: number } {
  const database = new Database(dbPath, { readonly: true, fileMustExist: true });
  try {
    assertIntegrity(database);
    assertNoUnsettledFish(database, now);
    return {
      fishCount: (database.prepare('SELECT COUNT(*) n FROM pond_fish').get() as {n: number}).n,
      ponds: (database.prepare('SELECT COUNT(DISTINCT pond_id) n FROM pond_fish').get() as {n: number}).n,
    };
  } finally { database.close(); }
}

/**
 * Explicit maintenance operation. Never imported by normal server boot, never uses its default DB.
 * Backup and restored-copy validation finish before any schema or fish mutation.
 */
export async function rebuildTestPondEcology(options: {
  dbPath: string; backupDir: string; batchId: string; environment: 'test';
  seed: EcologySeed; ruleVersion: string; now?: number;
}): Promise<EcologyRebuildResult> {
  if (options.environment !== 'test') throw new Error('This rebuild only supports an explicitly selected test environment');
  if (!path.isAbsolute(options.dbPath) || !path.isAbsolute(options.backupDir)) throw new Error('Use explicit absolute database and backup paths');
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$/.test(options.batchId)) throw new Error('Batch ID must contain 1-80 letters, digits, dots, underscores or hyphens');
  const dbPath = fs.realpathSync(options.dbPath);
  const now = options.now ?? Date.now();
  const lockPath = `${dbPath}.ecology-maintenance.lock`;
  const lock = fs.openSync(lockPath, 'wx', 0o600);
  let database: Database.Database | undefined;
  let result: EcologyRebuildResult | undefined;
  try {
    fs.writeFileSync(lock, JSON.stringify({ pid: process.pid, batchId: options.batchId, startedAt: now }));
    assertDatabaseOffline(dbPath);
    database = new Database(dbPath, { fileMustExist: true, timeout: 1000 });
    database.pragma('foreign_keys = ON');
    if (tableExists(database, 'fish_ecology_rebuild_batches')) {
      const old = database.prepare('SELECT * FROM fish_ecology_rebuild_batches WHERE batch_id=?').get(options.batchId) as {
        completed_at: number | null; backup_path: string; restored_copy_path: string; archived_count: number; seeded_count: number;
      } | undefined;
      if (old?.completed_at != null) return {
        batchId: options.batchId, alreadyCompleted: true, archivedCount: old.archived_count, seededCount: old.seeded_count,
        backupPath: old.backup_path, restoredCopyPath: old.restored_copy_path, manifestPath: `${old.backup_path}.manifest.json`,
      };
    }
    assertIntegrity(database);
    assertNoUnsettledFish(database, now);
    const before = snapshot(database);
    fs.mkdirSync(options.backupDir, { recursive: true });
    const attempt = `${options.batchId}-${now}-${process.pid}`;
    const backupPath = path.join(options.backupDir, `${attempt}.sqlite`);
    const restoredCopyPath = path.join(options.backupDir, `${attempt}.restore-check.sqlite`);
    const manifestPath = `${backupPath}.manifest.json`;
    if (fs.existsSync(backupPath) || fs.existsSync(restoredCopyPath)) throw new Error('Backup filename exists; refusing to overwrite');
    await database.backup(backupPath);
    fs.chmodSync(backupPath, 0o600);
    fs.copyFileSync(backupPath, restoredCopyPath, fs.constants.COPYFILE_EXCL);
    const restored = new Database(restoredCopyPath, { readonly: true, fileMustExist: true });
    try {
      assertIntegrity(restored);
      if (JSON.stringify(snapshot(restored)) !== JSON.stringify(before)) throw new Error('Restored backup does not match all source table contents');
    } finally { restored.close(); }
    fs.writeFileSync(manifestPath, JSON.stringify({ batchId: options.batchId, dbPath, backupPath, restoredCopyPath, restoredAt: now, ruleVersion: options.ruleVersion, before }, null, 2), { flag: 'wx', mode: 0o600 });
    assertDatabaseOffline(dbPath);
    const db = database;
    result = db.transaction(() => {
      if (JSON.stringify(snapshot(db)) !== JSON.stringify(before)) throw new Error('Database changed since backup; rebuild aborted');
      assertNoUnsettledFish(db, now);
      migrateFishEcologyEntry(db);
      db.exec(`CREATE TABLE IF NOT EXISTS fish_ecology_state_archive (
        batch_id TEXT NOT NULL, table_name TEXT NOT NULL, row_key TEXT NOT NULL, row_json TEXT NOT NULL,
        PRIMARY KEY(batch_id, table_name, row_key)
      )`);
      const oldFish = db.prepare('SELECT * FROM pond_fish').all() as Array<Record<string, unknown> & {id: string}>;
      const archive = db.prepare('INSERT INTO fish_entity_archive(archive_id,fish_id,entity_json,archived_at,reason,batch_id,catch_id) VALUES (?,?,?,?,?,?,NULL)');
      for (const fish of oldFish) archive.run(`${options.batchId}:${fish.id}`, fish.id,
        JSON.stringify({ schema: 'legacy-database-row', row: fish }), now, 'ecology_rebuild', options.batchId);
      const archiveState = db.prepare('INSERT INTO fish_ecology_state_archive(batch_id,table_name,row_key,row_json) VALUES (?,?,?,?)');
      for (const table of ['pond_state','spot_bite_weights']) {
        if (!tableExists(db, table)) continue;
        for (const row of db.prepare(`SELECT * FROM ${table}`).all()) {
          const json = JSON.stringify(row);
          archiveState.run(options.batchId, table, createHash('sha256').update(json).digest('hex'), json);
        }
      }
      db.prepare('INSERT INTO fish_ecology_rebuild_batches(batch_id,rule_version,started_at,backup_path,restored_copy_path,archived_count) VALUES (?,?,?,?,?,?)')
        .run(options.batchId, options.ruleVersion, now, backupPath, restoredCopyPath, oldFish.length);
      db.exec('DELETE FROM pond_fish; DELETE FROM pond_state; DELETE FROM spot_bite_weights;');
      const seededCount = options.seed(db, options.batchId, now);
      const actualCount = (db.prepare('SELECT COUNT(*) n FROM pond_fish').get() as {n: number}).n;
      if (seededCount !== actualCount || seededCount <= 0) throw new Error('Seed callback returned an invalid stock count');
      const newIds = new Set((db.prepare('SELECT id FROM pond_fish').all() as {id: string}[]).map(row => row.id));
      if (oldFish.some(fish => newIds.has(fish.id))) throw new Error('Rebuilt fish must have new identities');
      const protectedBefore = Object.fromEntries(Object.entries(before).filter(([table]) => !mutableTables.has(table)));
      if (JSON.stringify(snapshot(db, protectedBefore)) !== JSON.stringify(protectedBefore)) throw new Error('Rebuild modified protected history or player data');
      assertIntegrity(db);
      db.prepare('UPDATE fish_ecology_rebuild_batches SET completed_at=?, seeded_count=? WHERE batch_id=?').run(now, seededCount, options.batchId);
      return { batchId: options.batchId, alreadyCompleted: false, archivedCount: oldFish.length, seededCount, backupPath, restoredCopyPath, manifestPath };
    }).exclusive();
    return result;
  } finally {
    database?.close();
    fs.closeSync(lock);
    fs.unlinkSync(lockPath);
  }
}
