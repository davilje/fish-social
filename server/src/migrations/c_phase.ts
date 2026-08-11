import type Database from 'better-sqlite3';

export interface CPhaseMigrationResult {
  tablesCreated: string[];
}

export function migrateCPhase(database: Database.Database): CPhaseMigrationResult {
  const tablesCreated: string[] = [];

  database.exec(`
    CREATE TABLE IF NOT EXISTS game_config (
      config_key TEXT PRIMARY KEY,
      config_value TEXT NOT NULL,
      updated_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS config_change_requests (
      id TEXT PRIMARY KEY,
      config_key TEXT NOT NULL,
      proposed_value TEXT NOT NULL,
      status TEXT NOT NULL DEFAULT 'pending',
      submitted_by TEXT NOT NULL,
      approved_by TEXT,
      created_at INTEGER NOT NULL,
      resolved_at INTEGER
    );

    CREATE TABLE IF NOT EXISTS config_audit_log (
      id TEXT PRIMARY KEY,
      config_key TEXT NOT NULL,
      old_value TEXT,
      new_value TEXT NOT NULL,
      changed_by TEXT NOT NULL,
      approved_by TEXT,
      created_at INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS fishing_metrics (
      id TEXT PRIMARY KEY,
      event_type TEXT NOT NULL,
      player_id TEXT,
      pond_id TEXT,
      payload TEXT NOT NULL DEFAULT '{}',
      created_at INTEGER NOT NULL
    );

    CREATE INDEX IF NOT EXISTS idx_fishing_metrics_type_time
      ON fishing_metrics(event_type, created_at DESC);

    CREATE TABLE IF NOT EXISTS fish_codex (
      player_id TEXT NOT NULL,
      species_id TEXT NOT NULL,
      total_caught INTEGER NOT NULL DEFAULT 0,
      max_size_m REAL NOT NULL DEFAULT 0,
      first_caught_at INTEGER,
      last_caught_at INTEGER,
      PRIMARY KEY (player_id, species_id),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
  `);

  tablesCreated.push(
    'game_config',
    'config_change_requests',
    'config_audit_log',
    'fishing_metrics',
    'fish_codex',
  );

  const gearCols = database
    .prepare(`PRAGMA table_info(player_gear)`)
    .all() as Array<{ name: string }>;
  if (!gearCols.some((c) => c.name === 'tackle_durability')) {
    database.exec(`ALTER TABLE player_gear ADD COLUMN tackle_durability TEXT NOT NULL DEFAULT '{}'`);
    tablesCreated.push('player_gear.tackle_durability');
  }

  const defaults: Array<[string, string]> = [
    ['C3_SINK_ENABLED', 'true'],
    ['C4_GENETICS_ENABLED', 'true'],
    ['C6_SKIP_CASTING_ON_REBATE', 'true'],
    ['BOT_CATCH_SHARE_CAP', '0.4'],
  ];
  const insert = database.prepare(`
    INSERT OR IGNORE INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
  `);
  const now = Date.now();
  for (const [k, v] of defaults) insert.run(k, v, now);

  return { tablesCreated };
}
