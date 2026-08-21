import sqlite3
from pathlib import Path

db_path = Path(__file__).resolve().parents[2] / "data" / "fish-social.db"
print("db:", db_path)
con = sqlite3.connect(str(db_path))
con.row_factory = sqlite3.Row

print("=== players ===")
for row in con.execute("SELECT player_id, nickname, coins, created_at FROM players ORDER BY created_at DESC"):
    print(dict(row))

print("=== player_fishing_progress ===")
try:
    for row in con.execute("SELECT * FROM player_fishing_progress"):
        print(dict(row))
except sqlite3.OperationalError as e:
    print("missing table:", e)

print("=== steam_accounts ===")
try:
    for row in con.execute(
        "SELECT steam_id64, player_id, revoked_at FROM steam_accounts ORDER BY created_at DESC"
    ):
        print(dict(row))
except sqlite3.OperationalError as e:
    print("missing table:", e)

con.close()
