import type Database from 'better-sqlite3';

export function migratePlayerPondSessionReturnFee(database: Database.Database): void {
  const cols = database.prepare(`PRAGMA table_info(player_pond_session)`).all() as Array<{ name: string }>;
  if (!cols.some((c) => c.name === 'return_fee_mode')) {
    database.exec(`ALTER TABLE player_pond_session ADD COLUMN return_fee_mode TEXT`);
  }
}
