import type Database from 'better-sqlite3';
import { FISH_BITE_CHECK_MS } from '@fish-social/shared';

export interface BiteCheckIntervalMigrationResult {
  updated: boolean;
}

/** 无条件将 FISH_BITE_CHECK_MS 校正为当前常量值 */
export function migrateBiteCheckInterval(
  database: Database.Database,
): BiteCheckIntervalMigrationResult {
  const row = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get('FISH_BITE_CHECK_MS') as { config_value: string } | undefined;

  const current = row ? Number(row.config_value) : NaN;
  if (Number.isFinite(current) && current === FISH_BITE_CHECK_MS) {
    return { updated: false };
  }

  const now = Date.now();
  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value, updated_at = excluded.updated_at`,
    )
    .run('FISH_BITE_CHECK_MS', String(FISH_BITE_CHECK_MS), now);

  return { updated: true };
}
