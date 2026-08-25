/**
 * FEAT-FISH-CN-01：旧外来种 ID 映射 / 删除补偿；塘内实体清空以便重 seed。
 * 不 import @fish-social/shared，避免 db 启动时拉整包数值表。
 */
import type Database from 'better-sqlite3';

const MIGRATION_KEY = 'FISH_CN_01_SPECIES_V1';

const REMAP: Record<string, string> = {
  bass: 'black_bass',
  trout: 'rainbow_trout',
  perch: 'black_bass',
  sturgeon: 'chinese_sturgeon',
};

const DELETED = [
  'tuna',
  'marlin',
  'salmon',
  'cod',
  'herring',
  'snapper',
  'mackerel',
  'pike',
];

function tableExists(database: Database.Database, name: string): boolean {
  const row = database
    .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name = ?`)
    .get(name) as { name: string } | undefined;
  return Boolean(row);
}

function remapColumn(database: Database.Database, table: string, column: string): void {
  if (!tableExists(database, table)) return;
  const update = database.prepare(`UPDATE ${table} SET ${column} = ? WHERE ${column} = ?`);
  for (const [from, to] of Object.entries(REMAP)) {
    update.run(to, from);
  }
}

function estimateSellGold(quality: string, sizeM: number): number {
  const base: Record<string, number> = {
    gray: 80,
    green: 160,
    blue: 360,
    purple: 900,
    red: 2200,
    orange: 5500,
    gold: 14000,
  };
  const ref: Record<string, number> = {
    gray: 0.2,
    green: 0.35,
    blue: 0.6,
    purple: 1.0,
    red: 1.8,
    orange: 3.0,
    gold: 5.0,
  };
  const minSell: Record<string, number> = {
    gray: 40,
    green: 80,
    blue: 160,
    purple: 400,
    red: 900,
    orange: 2200,
    gold: 6000,
  };
  const q = quality in base ? quality : 'gray';
  const ratio = Math.max(0.01, sizeM / (ref[q] ?? 0.2));
  const raw = (base[q] ?? 80) * Math.pow(ratio, 1.15);
  return Math.max(Math.floor(raw), minSell[q] ?? 40);
}

export function migrateFishCn01Species(database: Database.Database): {
  remapped: number;
  compensated: number;
  pondFishCleared: number;
} {
  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') {
    return { remapped: 0, compensated: 0, pondFishCleared: 0 };
  }

  remapColumn(database, 'inventory', 'species_id');
  remapColumn(database, 'pond_fish', 'species_id');
  remapColumn(database, 'fish_codex', 'species_id');
  remapColumn(database, 'pending_catch_locks', 'species_id');
  remapColumn(database, 'player_album_candidates', 'species_id');
  remapColumn(database, 'player_album_pins', 'species_id');

  let compensated = 0;
  if (tableExists(database, 'inventory')) {
    const placeholders = DELETED.map(() => '?').join(',');
    const rows = database
      .prepare(
        `SELECT id, player_id, quality, size_m FROM inventory WHERE species_id IN (${placeholders})`,
      )
      .all(...DELETED) as Array<{
      id: string;
      player_id: string;
      quality: string;
      size_m: number;
    }>;
    const del = database.prepare('DELETE FROM inventory WHERE id = ? AND player_id = ?');
    const addCoins = database.prepare(
      'UPDATE players SET coins = coins + ? WHERE player_id = ?',
    );
    const tx = database.transaction(() => {
      for (const row of rows) {
        addCoins.run(estimateSellGold(row.quality, row.size_m), row.player_id);
        del.run(row.id, row.player_id);
        compensated += 1;
      }
    });
    tx();
  }

  for (const table of [
    'fish_codex',
    'pending_catch_locks',
    'player_album_candidates',
    'player_album_pins',
  ]) {
    if (!tableExists(database, table)) continue;
    const placeholders = DELETED.map(() => '?').join(',');
    database
      .prepare(`DELETE FROM ${table} WHERE species_id IN (${placeholders})`)
      .run(...DELETED);
  }

  let pondFishCleared = 0;
  if (tableExists(database, 'pond_fish')) {
    const count = database.prepare('SELECT COUNT(*) as c FROM pond_fish').get() as { c: number };
    pondFishCleared = count.c;
    database.exec('DELETE FROM pond_fish');
  }

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return { remapped: Object.keys(REMAP).length, compensated, pondFishCleared };
}
