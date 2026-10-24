import type Database from 'better-sqlite3';

export function migrateFishingSleep(database: Database.Database): void {
  const columns = database.prepare('PRAGMA table_info(player_pond_session)').all() as Array<{ name: string }>;
  const additions = {
    fishing_asleep: 'INTEGER NOT NULL DEFAULT 0',
    fishing_sleep_at: 'INTEGER',
    fishing_attention_at: 'INTEGER',
    disconnected_from_phase: 'TEXT',
    stop_requested: 'INTEGER NOT NULL DEFAULT 0',
  };
  for (const [name, definition] of Object.entries(additions)) {
    if (!columns.some((column) => column.name === name)) {
      database.exec(`ALTER TABLE player_pond_session ADD COLUMN ${name} ${definition}`);
    }
  }
}
