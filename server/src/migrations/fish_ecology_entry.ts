import type Database from 'better-sqlite3';

/** Additive only: old birth data and all player-owned history retain their meaning. */
export function migrateFishEcologyEntry(database: Database.Database): void {
  database.transaction(() => {
    const addColumns = (table: string, definitions: Record<string, string>) => {
      const columns = database.prepare(`PRAGMA table_info(${table})`).all() as Array<{ name: string }>;
      if (columns.length === 0) return;
      const names = new Set(columns.map(column => column.name));
      for (const [name, definition] of Object.entries(definitions)) {
        if (!names.has(name)) database.exec(`ALTER TABLE ${table} ADD COLUMN ${name} ${definition}`);
      }
    };
    addColumns('pond_fish', {
      entered_at: 'INTEGER', entry_size_m: 'REAL', juvenile_entry_size_m: 'REAL',
      individual_cap_m: 'REAL', growth_bonus_minutes: 'REAL NOT NULL DEFAULT 0',
      growth_rule_version: 'TEXT', source: 'TEXT', stocking_batch_id: 'TEXT', out_of_pond_ms: 'REAL NOT NULL DEFAULT 0',
    });
    addColumns('pond_state', {
      last_daily_settlement_key: 'TEXT', initial_stocking_batch_id: 'TEXT',
    });
    for (const table of ['inventory', 'player_album_candidates', 'player_album_pins']) {
      addColumns(table, { pond_fish_id: 'TEXT', history_json: 'TEXT' });
    }
    addColumns('pending_catch_locks', { history_json: 'TEXT' });
    database.exec(`
      CREATE TABLE IF NOT EXISTS fish_lifecycle_events (
        event_id TEXT PRIMARY KEY,
        fish_id TEXT NOT NULL,
        pond_id TEXT NOT NULL,
        spot_id TEXT,
        kind TEXT NOT NULL,
        occurred_at INTEGER NOT NULL,
        size_m REAL,
        requested_bonus_minutes REAL NOT NULL DEFAULT 0 CHECK(requested_bonus_minutes >= 0),
        granted_bonus_minutes REAL NOT NULL DEFAULT 0 CHECK(granted_bonus_minutes >= 0),
        feed_portions INTEGER NOT NULL DEFAULT 0 CHECK(feed_portions >= 0),
        groundbait_cast_id TEXT,
        payload_json TEXT NOT NULL DEFAULT '{}'
      );
      CREATE INDEX IF NOT EXISTS idx_fish_lifecycle_time
        ON fish_lifecycle_events(fish_id, occurred_at);
      CREATE INDEX IF NOT EXISTS idx_fish_lifecycle_cast
        ON fish_lifecycle_events(groundbait_cast_id, fish_id);
      CREATE TABLE IF NOT EXISTS groundbait_casts (
        cast_id TEXT PRIMARY KEY,
        player_id TEXT NOT NULL,
        pond_id TEXT NOT NULL,
        spot_id TEXT NOT NULL,
        groundbait_id TEXT NOT NULL,
        created_at INTEGER NOT NULL,
        expires_at INTEGER NOT NULL,
        portions_total INTEGER NOT NULL DEFAULT 12 CHECK(portions_total >= 0),
        portions_consumed INTEGER NOT NULL DEFAULT 0 CHECK(portions_consumed >= 0 AND portions_consumed <= portions_total),
        status TEXT NOT NULL DEFAULT 'active'
      );
      CREATE INDEX IF NOT EXISTS idx_groundbait_cast_location
        ON groundbait_casts(pond_id, spot_id, status, expires_at);
      CREATE TABLE IF NOT EXISTS fish_hook_locks (
        fish_id TEXT PRIMARY KEY,
        catch_id TEXT NOT NULL UNIQUE,
        user_id TEXT,
        locked_at INTEGER NOT NULL,
        expires_at INTEGER NOT NULL
      );
      CREATE TABLE IF NOT EXISTS fish_entity_archive (
        archive_id TEXT PRIMARY KEY,
        fish_id TEXT NOT NULL,
        entity_json TEXT NOT NULL,
        archived_at INTEGER NOT NULL,
        reason TEXT NOT NULL,
        batch_id TEXT,
        catch_id TEXT
      );
      CREATE INDEX IF NOT EXISTS idx_fish_entity_archive_identity
        ON fish_entity_archive(fish_id, archived_at DESC);
      CREATE INDEX IF NOT EXISTS idx_fish_entity_archive_catch
        ON fish_entity_archive(catch_id);
      CREATE TABLE IF NOT EXISTS fish_ecology_rebuild_batches (
        batch_id TEXT PRIMARY KEY,
        rule_version TEXT NOT NULL,
        started_at INTEGER NOT NULL,
        completed_at INTEGER,
        backup_path TEXT NOT NULL,
        restored_copy_path TEXT NOT NULL,
        archived_count INTEGER NOT NULL DEFAULT 0,
        seeded_count INTEGER NOT NULL DEFAULT 0
      );
    `);
    addColumns('fish_hook_locks', { context_json: 'TEXT' });
  })();
}
