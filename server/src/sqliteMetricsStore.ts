import type Database from 'better-sqlite3';
import { timedDbQuery } from './db.js';
import type { MetricInsertRow, MetricRawRow, MetricsStore } from './metricsStore.js';

const CATCH_EVENT_TYPES = ['catch_accept', 'pending_catch_accept'] as const;

export class SqliteMetricsStore implements MetricsStore {
  private readonly insertStmt;
  private readonly insertIgnoreStmt;
  private readonly rawSinceStmt;
  private readonly timelineStmt;
  private readonly distinctCatchStmt;

  constructor(private readonly db: Database.Database) {
    this.insertStmt = db.prepare(`
      INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, correlation_id, dedup_key, created_at)
      VALUES (@id, @eventType, @playerId, @pondId, @payload, @correlationId, @dedupKey, @createdAt)
    `);
    this.insertIgnoreStmt = db.prepare(`
      INSERT OR IGNORE INTO fishing_metrics (id, event_type, player_id, pond_id, payload, correlation_id, dedup_key, created_at)
      VALUES (@id, @eventType, @playerId, @pondId, @payload, @correlationId, @dedupKey, @createdAt)
    `);
    this.rawSinceStmt = db.prepare(`
      SELECT id, event_type, player_id, pond_id, payload, correlation_id, created_at
      FROM fishing_metrics
      WHERE created_at >= ?
    `);
    this.timelineStmt = db.prepare(`
      SELECT id, event_type, pond_id, payload, correlation_id, created_at
      FROM fishing_metrics
      WHERE player_id = ? AND created_at >= ?
      ORDER BY created_at ASC
      LIMIT ?
    `);
    this.distinctCatchStmt = db.prepare(`
      SELECT COUNT(DISTINCT COALESCE(dedup_key, id)) AS cnt
      FROM fishing_metrics
      WHERE event_type IN ('catch_accept', 'pending_catch_accept') AND created_at >= ?
    `);
  }

  insertBatch(rows: MetricInsertRow[]): { inserted: number; skipped: number } {
    let inserted = 0;
    let skipped = 0;
    const tx = this.db.transaction((batch: MetricInsertRow[]) => {
      for (const row of batch) {
        const stmt = row.dedupKey ? this.insertIgnoreStmt : this.insertStmt;
        const info = stmt.run({
          id: row.id,
          eventType: row.eventType,
          playerId: row.playerId,
          pondId: row.pondId,
          payload: row.payload,
          correlationId: row.correlationId ?? null,
          dedupKey: row.dedupKey ?? null,
          createdAt: row.createdAt,
        });
        if (info.changes > 0) inserted += 1;
        else skipped += 1;
      }
    });
    tx(rows);
    return { inserted, skipped };
  }

  queryRawSince(since: number): MetricRawRow[] {
    return timedDbQuery('metrics_raw_since', () => this.rawSinceStmt.all(since) as MetricRawRow[], {
      meta: { since },
    });
  }

  queryPlayerTimeline(playerId: string, since: number, limit: number): MetricRawRow[] {
    return timedDbQuery(
      'metrics_player_timeline',
      () => this.timelineStmt.all(playerId, since, limit) as MetricRawRow[],
      { meta: { playerId, since, limit } },
    );
  }

  countDistinctCatchEvents(since: number): number {
    const row = this.distinctCatchStmt.get(since) as { cnt: number } | undefined;
    return row?.cnt ?? 0;
  }
}

export { CATCH_EVENT_TYPES };
