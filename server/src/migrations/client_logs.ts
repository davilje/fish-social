import type Database from 'better-sqlite3';

export function migrateClientLogs(db: Database): { tableCreated: boolean } {
  const existing = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='client_logs'").get();
  if (existing) return { tableCreated: false };

  db.exec(
    "CREATE TABLE client_logs (" +
    "  id TEXT PRIMARY KEY," +
    "  player_id TEXT NOT NULL," +
    "  ts INTEGER NOT NULL," +
    "  level TEXT NOT NULL DEFAULT 'info'," +
    "  event_type TEXT NOT NULL DEFAULT ''," +
    "  fields TEXT NOT NULL DEFAULT '{}'," +
    "  created_at INTEGER NOT NULL" +
    ")"
  );
  db.exec("CREATE INDEX IF NOT EXISTS idx_client_logs_player ON client_logs(player_id)");
  db.exec("CREATE INDEX IF NOT EXISTS idx_client_logs_created ON client_logs(created_at DESC)");
  console.log('Migration: client_logs table created');
  return { tableCreated: true };
}
