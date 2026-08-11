import type Database from 'better-sqlite3';

export function migrateAuditLog(db: Database): { tableCreated: boolean } {
  const existing = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='audit_log'").get();
  if (existing) return { tableCreated: false };

  db.exec(
    "CREATE TABLE audit_log (" +
    "  id TEXT PRIMARY KEY," +
    "  who TEXT NOT NULL," +
    "  what TEXT NOT NULL," +
    "  target_player_id TEXT," +
    "  reason TEXT," +
    "  details TEXT," +
    "  created_at INTEGER NOT NULL" +
    ")"
  );
  db.exec("CREATE INDEX IF NOT EXISTS idx_audit_log_created ON audit_log(created_at DESC)");
  db.exec("CREATE INDEX IF NOT EXISTS idx_audit_log_target ON audit_log(target_player_id)");
  console.log('Migration: audit_log table created');
  return { tableCreated: true };
}
