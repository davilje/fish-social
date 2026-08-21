"""Reset FEAT-PROG-01 onboarding for human Steam player(s)."""
import sqlite3
import time
from pathlib import Path

DB = Path(__file__).resolve().parents[2] / "data" / "fish-social.db"
now = int(time.time() * 1000)

con = sqlite3.connect(str(DB))
con.row_factory = sqlite3.Row

# Prefer Steam-bound humans; also include steam_* player ids
targets = []
for row in con.execute(
    "SELECT player_id FROM steam_accounts WHERE revoked_at IS NULL"
):
    targets.append(row["player_id"])
for row in con.execute(
    "SELECT player_id FROM players WHERE player_id LIKE 'steam_%' OR player_id LIKE 'p_%'"
):
    if row["player_id"] not in targets:
        targets.append(row["player_id"])

# Exclude smoke/test bots
targets = [p for p in targets if not p.startswith("bot-") and "smoke" not in p]
if not targets:
    raise SystemExit("no human player found")

print("reset targets:", targets)

for player_id in targets:
    con.execute(
        """
        INSERT INTO player_fishing_progress
          (player_id, level, xp, onboarding_completed, onboarding_completed_at, updated_at)
        VALUES (?, 1, 0, 0, NULL, ?)
        ON CONFLICT(player_id) DO UPDATE SET
          onboarding_completed = 0,
          onboarding_completed_at = NULL,
          updated_at = excluded.updated_at
        """,
        (player_id, now),
    )
    con.execute(
        "DELETE FROM player_pond_proficiency WHERE player_id = ? AND pond_id = 'pond-novice'",
        (player_id,),
    )
    try:
        con.execute(
            "DELETE FROM player_pond_session WHERE player_id = ?",
            (player_id,),
        )
    except sqlite3.OperationalError:
        pass

con.commit()

print("=== after ===")
for row in con.execute(
    "SELECT player_id, level, xp, onboarding_completed, onboarding_completed_at FROM player_fishing_progress"
):
    print(dict(row))

con.close()
print("done — restart client (and server if running) then Steam login")
