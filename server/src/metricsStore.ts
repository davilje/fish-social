export type MetricInsertRow = {
  id: string;
  eventType: string;
  playerId: string | null;
  pondId: string | null;
  payload: string;
  correlationId?: string | null;
  dedupKey?: string | null;
  createdAt: number;
};

export type MetricRawRow = {
  id: string;
  event_type: string;
  player_id: string | null;
  pond_id: string | null;
  payload: string;
  correlation_id: string | null;
  created_at: number;
};

export interface MetricsStore {
  insertBatch(rows: MetricInsertRow[]): { inserted: number; skipped: number };
  queryRawSince(since: number): MetricRawRow[];
  queryPlayerTimeline(playerId: string, since: number, limit: number): MetricRawRow[];
  countDistinctCatchEvents(since: number): number;
}

export class DualWriteMetricsStore implements MetricsStore {
  constructor(
    private readonly primary: MetricsStore,
    private readonly secondary: MetricsStore,
  ) {}

  insertBatch(rows: MetricInsertRow[]): { inserted: number; skipped: number } {
    const result = this.primary.insertBatch(rows);
    try {
      this.secondary.insertBatch(rows);
    } catch (err) {
      console.warn('[metrics] secondary write failed:', err instanceof Error ? err.message : err);
    }
    return result;
  }

  queryRawSince(since: number): MetricRawRow[] {
    return this.primary.queryRawSince(since);
  }

  queryPlayerTimeline(playerId: string, since: number, limit: number): MetricRawRow[] {
    return this.primary.queryPlayerTimeline(playerId, since, limit);
  }

  countDistinctCatchEvents(since: number): number {
    return this.primary.countDistinctCatchEvents(since);
  }
}

let writeStore: MetricsStore | null = null;
let readStore: MetricsStore | null = null;

export function initMetricsStores(sqliteStore: MetricsStore, postgresStore?: MetricsStore | null): void {
  const dualWrite = process.env.METRICS_DUAL_WRITE === 'true' && postgresStore;
  writeStore = dualWrite ? new DualWriteMetricsStore(sqliteStore, postgresStore!) : sqliteStore;
  // QUAL-03: postgres read path is refused at startup (assertMetricsReadFromSupported)
  readStore = sqliteStore;
}

export function getMetricsWriteStore(): MetricsStore {
  if (!writeStore) throw new Error('MetricsStore not initialized');
  return writeStore;
}

export function getMetricsReadStore(): MetricsStore {
  if (!readStore) throw new Error('MetricsStore not initialized');
  return readStore;
}
