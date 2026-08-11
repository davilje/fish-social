import type { MetricInsertRow, MetricRawRow, MetricsStore } from './metricsStore.js';

type PgPool = {
  query(sql: string, params?: unknown[]): Promise<{ rows: Record<string, unknown>[]; rowCount?: number }>;
  end(): Promise<void>;
};

const CATCH_EVENT_TYPES = ['catch_accept', 'pending_catch_accept'];
const BATCH_CHUNK = 100;

let activeStore: PostgresMetricsStore | null = null;

function rowToRaw(row: Record<string, unknown>): MetricRawRow {
  const payload = row.payload;
  return {
    id: String(row.id),
    event_type: String(row.event_type),
    player_id: (row.player_id as string | null) ?? null,
    pond_id: (row.pond_id as string | null) ?? null,
    payload: typeof payload === 'string' ? payload : JSON.stringify(payload ?? {}),
    correlation_id: (row.correlation_id as string | null) ?? null,
    created_at: Number(row.created_at),
  };
}

export class PostgresMetricsStore implements MetricsStore {
  private pool: PgPool | null = null;
  private readonly initPromise: Promise<void>;
  private pendingWrite: Promise<void> = Promise.resolve();

  constructor(private readonly connectionUrl: string) {
    this.initPromise = this.initPool();
  }

  private async initPool(): Promise<void> {
    const pg = await import('pg');
    const pool = new pg.default.Pool({ connectionString: this.connectionUrl, max: 10 });
    await pool.query(`
      CREATE TABLE IF NOT EXISTS fishing_metrics (
        id TEXT PRIMARY KEY,
        event_type TEXT NOT NULL,
        player_id TEXT,
        pond_id TEXT,
        payload JSONB NOT NULL DEFAULT '{}',
        correlation_id TEXT,
        dedup_key TEXT,
        created_at BIGINT NOT NULL
      )
    `);
    await pool.query(`
      CREATE UNIQUE INDEX IF NOT EXISTS idx_fishing_metrics_dedup_key
      ON fishing_metrics(dedup_key) WHERE dedup_key IS NOT NULL
    `);
    await pool.query(`
      CREATE INDEX IF NOT EXISTS idx_fishing_metrics_player_time
      ON fishing_metrics(player_id, created_at DESC)
    `);
    this.pool = pool;
  }

  private async getPool(): Promise<PgPool> {
    await this.initPromise;
    if (!this.pool) throw new Error('Postgres metrics pool not ready');
    return this.pool;
  }

  /**
   * QUAL-03: enqueue a true multi-row INSERT (fire-and-forget with tracked pending).
   * Callers that need durability on shutdown must await closePostgresPool().
   */
  insertBatch(rows: MetricInsertRow[]): { inserted: number; skipped: number } {
    if (rows.length === 0) return { inserted: 0, skipped: 0 };
    this.pendingWrite = this.pendingWrite
      .then(() => this.insertBatchAsync(rows))
      .catch((err) => {
        console.warn('[metrics-pg] batch write failed:', err instanceof Error ? err.message : err);
      });
    return { inserted: rows.length, skipped: 0 };
  }

  private async insertChunk(
    pool: PgPool,
    rows: MetricInsertRow[],
    conflict: 'id' | 'dedup_key',
  ): Promise<void> {
    if (rows.length === 0) return;
    const values: string[] = [];
    const params: unknown[] = [];
    let i = 1;
    for (const row of rows) {
      values.push(
        `($${i++}, $${i++}, $${i++}, $${i++}, $${i++}::jsonb, $${i++}, $${i++}, $${i++})`,
      );
      params.push(
        row.id,
        row.eventType,
        row.playerId,
        row.pondId,
        row.payload,
        row.correlationId ?? null,
        row.dedupKey ?? null,
        row.createdAt,
      );
    }
    const onConflict =
      conflict === 'dedup_key'
        ? 'ON CONFLICT (dedup_key) DO NOTHING'
        : 'ON CONFLICT (id) DO NOTHING';
    await pool.query(
      `INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, correlation_id, dedup_key, created_at)
       VALUES ${values.join(', ')} ${onConflict}`,
      params,
    );
  }

  private async insertBatchAsync(rows: MetricInsertRow[]): Promise<void> {
    const pool = await this.getPool();
    const withDedup = rows.filter((r) => r.dedupKey);
    const withoutDedup = rows.filter((r) => !r.dedupKey);
    for (let start = 0; start < withDedup.length; start += BATCH_CHUNK) {
      await this.insertChunk(pool, withDedup.slice(start, start + BATCH_CHUNK), 'dedup_key');
    }
    for (let start = 0; start < withoutDedup.length; start += BATCH_CHUNK) {
      await this.insertChunk(pool, withoutDedup.slice(start, start + BATCH_CHUNK), 'id');
    }
  }

  /**
   * Sync reads are not supported on PG (pool is async). Prefer METRICS_READ_FROM=sqlite.
   * Startup asserts when METRICS_READ_FROM=postgres — see assertMetricsReadFromSupported.
   */
  queryRawSince(_since: number): MetricRawRow[] {
    console.warn('[metrics-pg] sync read not supported; use METRICS_READ_FROM=sqlite');
    return [];
  }

  queryPlayerTimeline(_playerId: string, _since: number, _limit: number): MetricRawRow[] {
    console.warn('[metrics-pg] sync read not supported; use METRICS_READ_FROM=sqlite');
    return [];
  }

  countDistinctCatchEvents(_since: number): number {
    return 0;
  }

  /** STAB-02 / QUAL-03: drain pending writes then end pool */
  async close(): Promise<void> {
    try {
      await this.initPromise;
    } catch {
      /* init may have failed */
    }
    try {
      await this.pendingWrite;
    } catch {
      /* already logged */
    }
    if (!this.pool) return;
    try {
      await this.pool.end();
    } finally {
      this.pool = null;
    }
  }
}

/**
 * QUAL-03: PG store has no sync query API. Refuse METRICS_READ_FROM=postgres at startup.
 */
export function assertMetricsReadFromSupported(): void {
  const readFrom = (process.env.METRICS_READ_FROM ?? 'sqlite').toLowerCase();
  if (readFrom === 'postgres' || readFrom === 'pg') {
    throw new Error(
      'METRICS_READ_FROM=postgres is not supported (PG metrics store has no sync reads). ' +
        'Use METRICS_READ_FROM=sqlite (dual-write still allowed via METRICS_DUAL_WRITE=true).',
    );
  }
}

export function createPostgresMetricsStoreIfConfigured(): MetricsStore | null {
  assertMetricsReadFromSupported();
  const url = process.env.METRICS_PG_URL;
  if (!url) return null;
  try {
    const store = new PostgresMetricsStore(url);
    activeStore = store;
    return store;
  } catch (err) {
    console.warn('[metrics] PostgresMetricsStore unavailable:', err instanceof Error ? err.message : err);
    return null;
  }
}

/** STAB-02: no-op when PG metrics not configured */
export async function closePostgresPool(): Promise<void> {
  if (!activeStore) return;
  try {
    await activeStore.close();
  } finally {
    activeStore = null;
  }
}

export { CATCH_EVENT_TYPES, rowToRaw };
