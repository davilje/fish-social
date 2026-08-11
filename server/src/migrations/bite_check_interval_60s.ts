import type Database from 'better-sqlite3';
import { FISH_BITE_CHECK_MS } from '@fish-social/shared';

const MIGRATION_KEY = 'BITE_CHECK_INTERVAL_60S_V1';

export interface BiteCheckInterval60sMigrationResult {
  updated: boolean;
}

/** v0.4.1：FISH_BITE_CHECK_MS = 60_000（1 分钟） */
export function migrateBiteCheckInterval60s(
  database: Database.Database,
): BiteCheckInterval60sMigrationResult {
  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return { updated: false };

  const now = Date.now();
  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value, updated_at = excluded.updated_at`,
    )
    .run('FISH_BITE_CHECK_MS', String(FISH_BITE_CHECK_MS), now);

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, now);

  return { updated: true };
}
