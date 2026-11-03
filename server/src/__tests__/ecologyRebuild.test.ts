import Database from 'better-sqlite3';
import { afterEach, describe, expect, it } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { migrateFishEcologyEntry } from '../migrations/fish_ecology_entry.js';
import { rebuildTestPondEcology } from '../ecologyRebuild.js';
import { seedEcologyRebuild } from '../ecologySeed.js';
import { getFishGrowthT80Days, getPondEcologyDayKey } from '@fish-social/shared';

const temporary: string[] = [];
afterEach(() => { for (const dir of temporary.splice(0)) fs.rmSync(dir, { recursive: true, force: true }); });

function fixture() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'fish-ecology-l1-'));
  temporary.push(dir);
  const dbPath = path.join(dir, 'test.sqlite');
  const db = new Database(dbPath);
  db.exec(`
    CREATE TABLE players(player_id TEXT PRIMARY KEY, coins INTEGER, nickname TEXT);
    INSERT INTO players VALUES ('player',321,'before');
    CREATE TABLE pond_fish(id TEXT PRIMARY KEY,pond_id TEXT,species_id TEXT,quality TEXT,size_m REAL,born_at INTEGER NOT NULL,birth_size_m REAL);
    INSERT INTO pond_fish VALUES ('old-fish','pond-calm','carp','gray',.17,12345,.05);
    CREATE TABLE pond_state(pond_id TEXT PRIMARY KEY,last_supplement_at INTEGER);
    INSERT INTO pond_state VALUES ('pond-calm',456);
    CREATE TABLE spot_bite_weights(pond_id TEXT,spot_id TEXT,weight REAL);
    INSERT INTO spot_bite_weights VALUES ('pond-calm','spot',2.5);
    CREATE TABLE inventory(id TEXT PRIMARY KEY,player_id TEXT,size_m REAL);
    INSERT INTO inventory VALUES ('owned','player',40);
    CREATE TABLE player_album_candidates(candidate_id TEXT PRIMARY KEY,size_m REAL);
    INSERT INTO player_album_candidates VALUES ('photo',40);
    CREATE TABLE player_album_pins(pin_id TEXT PRIMARY KEY,size_m REAL);
    INSERT INTO player_album_pins VALUES ('pin',40);
    CREATE TABLE pending_catch_locks(user_id TEXT PRIMARY KEY,pond_fish_id TEXT);
    CREATE TABLE player_pond_session(player_id TEXT PRIMARY KEY,fishing_phase TEXT);
    CREATE TABLE arbitrary_history(id TEXT PRIMARY KEY,payload TEXT);
    INSERT INTO arbitrary_history VALUES ('history','preserve exactly');
  `);
  db.close();
  return { dir, dbPath, backupDir: path.join(dir, 'backups') };
}

function seed(db: Database.Database): number {
  db.exec("INSERT INTO pond_fish(id,pond_id,species_id,quality,size_m,born_at,entered_at,entry_size_m,juvenile_entry_size_m,individual_cap_m,growth_rule_version,source) VALUES ('new-fish','pond-calm','carp','gray',.08,0,100,.04,.04,.2,'pond-entry-v1','pre_opening_seed')");
  return 1;
}

const options = (f: ReturnType<typeof fixture>) => ({ ...f, batchId: 'test-batch', environment: 'test' as const, ruleVersion: 'pond-entry-v1', now: 2_000_000, seed });

describe('fish ecology entry additive migration', () => {
  it('preserves old birth and owned history; initializes new entry fields as unknown; is idempotent', () => {
    const f = fixture();
    const db = new Database(f.dbPath);
    migrateFishEcologyEntry(db);
    migrateFishEcologyEntry(db);
    const fish = db.prepare('SELECT * FROM pond_fish').get() as Record<string, unknown>;
    expect(fish).toMatchObject({ id: 'old-fish', born_at: 12345, birth_size_m: .05, entered_at: null, entry_size_m: null,
      individual_cap_m: null, growth_bonus_minutes: 0, out_of_pond_ms: 0 });
    expect(db.prepare('SELECT * FROM inventory').get()).toMatchObject({ id: 'owned', size_m: 40, history_json: null, pond_fish_id: null });
    expect(db.prepare('SELECT * FROM player_album_pins').get()).toMatchObject({ pin_id: 'pin', size_m: 40, history_json: null });
    expect(db.prepare('SELECT COUNT(*) n FROM fish_ecology_rebuild_batches').get()).toEqual({ n: 0 });
    db.close();
  });
});

describe('explicit offline L1 rebuild', () => {
  it('validates a complete restored backup before archiving active fish; preserves history and repeats safely', async () => {
    const f = fixture();
    const result = await rebuildTestPondEcology(options(f));
    expect(result).toMatchObject({ alreadyCompleted: false, archivedCount: 1, seededCount: 1 });
    const backup = new Database(result.restoredCopyPath, { readonly: true });
    expect(backup.prepare('SELECT id FROM pond_fish').get()).toEqual({ id: 'old-fish' });
    expect(backup.prepare('SELECT payload FROM arbitrary_history').get()).toEqual({ payload: 'preserve exactly' });
    expect(backup.pragma('integrity_check', { simple: true })).toBe('ok');
    backup.close();
    const db = new Database(f.dbPath);
    expect(db.prepare('SELECT id,born_at FROM pond_fish').get()).toEqual({ id: 'new-fish', born_at: 0 });
    const archived = db.prepare('SELECT fish_id,reason,entity_json FROM fish_entity_archive').get() as {fish_id: string;reason: string;entity_json: string};
    expect(archived).toMatchObject({ fish_id: 'old-fish', reason: 'ecology_rebuild' });
    expect(JSON.parse(archived.entity_json).row).toMatchObject({ born_at: 12345, birth_size_m: .05, size_m: .17 });
    expect(db.prepare('SELECT coins,nickname FROM players').get()).toEqual({ coins: 321, nickname: 'before' });
    expect(db.prepare('SELECT size_m FROM inventory').get()).toEqual({ size_m: 40 });
    expect(db.prepare('SELECT COUNT(*) n FROM fish_ecology_state_archive').get()).toEqual({ n: 2 });
    db.close();
    expect(fs.existsSync(result.manifestPath)).toBe(true);
    expect(fs.existsSync(`${f.dbPath}.ecology-maintenance.lock`)).toBe(false);
    const repeat = await rebuildTestPondEcology({ ...options(f), seed: () => { throw new Error('must not reseed'); } });
    expect(repeat).toMatchObject({ alreadyCompleted: true, backupPath: result.backupPath, seededCount: 1 });
  });

  it('rolls back fish, schema, and history when seed fails or modifies player data', async () => {
    const cases: Array<[(db: Database.Database) => number, string]> = [
      [(_db: Database.Database) => { throw new Error('seed failed'); }, 'seed failed'],
      [(db: Database.Database) => { seed(db); db.exec('UPDATE players SET coins=999'); return 1; }, 'modified protected history or player data'],
    ];
    for (const [badSeed, message] of cases) {
      const f = fixture();
      await expect(rebuildTestPondEcology({ ...options(f), seed: badSeed })).rejects.toThrow(message);
      const db = new Database(f.dbPath);
      expect(db.prepare('SELECT id FROM pond_fish').get()).toEqual({ id: 'old-fish' });
      expect(db.prepare('SELECT coins FROM players').get()).toEqual({ coins: 321 });
      expect(db.prepare("SELECT name FROM sqlite_master WHERE name='fish_ecology_rebuild_batches'").get()).toBeUndefined();
      db.close();
      expect(fs.readdirSync(f.backupDir).some(name => name.endsWith('.restore-check.sqlite'))).toBe(true);
      expect(fs.existsSync(`${f.dbPath}.ecology-maintenance.lock`)).toBe(false);
    }
  });

  it('refuses pending catches without creating backup or changing data', async () => {
    const f = fixture();
    const db = new Database(f.dbPath);
    db.exec("INSERT INTO pending_catch_locks VALUES ('user','old-fish')"); db.close();
    await expect(rebuildTestPondEcology(options(f))).rejects.toThrow('Pending catches');
    expect(fs.existsSync(f.backupDir)).toBe(false);
    expect(fs.existsSync(`${f.dbPath}.ecology-maintenance.lock`)).toBe(false);
  });

  it('refuses live hook locks and active groundbait', async () => {
    for (const sql of [
      "INSERT INTO fish_hook_locks(fish_id,catch_id,user_id,locked_at,expires_at) VALUES ('old-fish','catch','user',0,3000000)",
      "INSERT INTO groundbait_casts(cast_id,player_id,pond_id,spot_id,groundbait_id,created_at,expires_at) VALUES ('cast','player','pond-calm','spot','bait',0,3000000)",
    ]) {
      const f = fixture(); const db = new Database(f.dbPath);
      migrateFishEcologyEntry(db); db.exec(sql); db.close();
      await expect(rebuildTestPondEcology(options(f))).rejects.toThrow(/hook locks|groundbait/);
      expect(fs.existsSync(f.backupDir)).toBe(false);
    }
  });

  it('refuses another process holding the target database', async () => {
    const f = fixture();
    const require = createRequire(import.meta.url);
    const child = spawn(process.execPath, ['-e', "const D=require(process.argv[1]); const db=new D(process.argv[2]); console.log('ready'); setInterval(()=>{},1000)", require.resolve('better-sqlite3'), f.dbPath], { stdio: ['ignore','pipe','pipe'] });
    try {
      await new Promise<void>((resolve, reject) => { child.stdout!.once('data', () => resolve()); child.once('error', reject); child.once('exit', code => reject(new Error(`DB holder exited ${code}`))); });
      await expect(rebuildTestPondEcology(options(f))).rejects.toThrow('Database is open in another process');
      expect(fs.existsSync(f.backupDir)).toBe(false);
    } finally {
      const exited = new Promise<void>(resolve => child.once('exit', () => resolve())); child.kill(); await exited;
    }
  });

  it('rebuilds all configured formal ponds with 100 independently aged fish and keeps the novice stock special', async () => {
    const f = fixture();
    const db = new Database(f.dbPath);
    db.exec(`ALTER TABLE pond_fish ADD COLUMN spot_id TEXT;
      ALTER TABLE pond_fish ADD COLUMN generation INTEGER DEFAULT 0;
      ALTER TABLE pond_fish ADD COLUMN bite_weight REAL;
      ALTER TABLE pond_fish ADD COLUMN bite_multiplier REAL;
      ALTER TABLE pond_fish ADD COLUMN escape_multiplier REAL;
      ALTER TABLE pond_state ADD COLUMN depleted_until INTEGER;
      ALTER TABLE pond_state ADD COLUMN last_weight_refresh INTEGER DEFAULT 0;
      ALTER TABLE pond_state ADD COLUMN last_migration_at INTEGER DEFAULT 0;
      ALTER TABLE pond_state ADD COLUMN last_simulated_at INTEGER DEFAULT 0;`);
    db.close();
    const now = Date.UTC(2026, 10, 4, 0);
    const result = await rebuildTestPondEcology({ ...options(f), now, seed: seedEcologyRebuild });
    expect(result.seededCount).toBe(4001);
    const rebuilt = new Database(f.dbPath);
    const counts = rebuilt.prepare('SELECT pond_id,COUNT(*) n FROM pond_fish GROUP BY pond_id').all() as {pond_id: string;n: number}[];
    expect(counts.filter(row => row.pond_id !== 'pond-novice')).toHaveLength(40);
    expect(counts.filter(row => row.pond_id !== 'pond-novice').every(row => row.n === 100)).toBe(true);
    expect(counts.find(row => row.pond_id === 'pond-novice')?.n).toBe(1);
    const fish = rebuilt.prepare('SELECT * FROM pond_fish').all() as Array<Record<string, any>>;
    expect(new Set(fish.map(row => row.entered_at)).size).toBeGreaterThan(3900);
    for (const row of fish) {
      expect(row.born_at).toBe(0);
      expect(row.birth_size_m).toBeNull();
      expect(row.entered_at).toBeGreaterThanOrEqual(now - 14 * 86400000);
      expect(row.entered_at).toBeLessThanOrEqual(now);
      expect(row.entry_size_m).toBeGreaterThanOrEqual(.034);
      expect(row.entry_size_m).toBeLessThanOrEqual(.046);
      expect(row.out_of_pond_ms).toBe(0);
      expect(row.stocking_batch_id).toBe('test-batch');
      const k = Math.log((row.individual_cap_m-row.entry_size_m)/(.2*row.individual_cap_m))/getFishGrowthT80Days(row.quality);
      const expected = row.individual_cap_m-(row.individual_cap_m-row.entry_size_m)*Math.exp(-k*(now-row.entered_at)/86400000);
      expect(row.size_m).toBeCloseTo(expected, 3);
    }
    expect(rebuilt.prepare('SELECT DISTINCT last_daily_settlement_key FROM pond_state').all()).toEqual([{last_daily_settlement_key:getPondEcologyDayKey(now)}]);
    expect(rebuilt.prepare('SELECT COUNT(*) n FROM fish_lifecycle_events').get()).toEqual({n:0});
    expect(rebuilt.prepare('SELECT coins FROM players').get()).toEqual({coins:321});
    rebuilt.close();
  });
});
