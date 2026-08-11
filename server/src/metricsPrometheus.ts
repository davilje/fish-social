import promClient from 'prom-client';

// Collect default metrics (Node.js event loop lag, GC, etc.)
const register = new promClient.Registry();
promClient.collectDefaultMetrics({ register });

// --- HTTP RED Metrics ---
export const httpRequestCounter = new promClient.Counter({
  name: 'fish_http_requests_total',
  help: 'Total HTTP requests',
  labelNames: ['method', 'path', 'status'],
  registers: [register],
});

export const httpRequestDurationHistogram = new promClient.Histogram({
  name: 'fish_http_request_duration_ms',
  help: 'HTTP request duration in ms',
  labelNames: ['method', 'path'],
  buckets: [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000],
  registers: [register],
});

// --- Socket Metrics ---
export const socketEventCounter = new promClient.Counter({
  name: 'fish_socket_events_total',
  help: 'Total Socket.io events',
  labelNames: ['event', 'pondId'],
  registers: [register],
});

export const socketConnectionsGauge = new promClient.Gauge({
  name: 'fish_socket_connections_active',
  help: 'Active socket connections',
  registers: [register],
});

// --- DB Metrics ---
export const dbQueryDurationHistogram = new promClient.Histogram({
  name: 'fish_db_query_duration_ms',
  help: 'Database query duration in ms',
  labelNames: ['queryName'],
  buckets: [1, 5, 10, 25, 50, 100, 250, 500],
  registers: [register],
});

export const slowQueryCounter = new promClient.Counter({
  name: 'fish_slow_queries_total',
  help: 'Total slow database queries',
  labelNames: ['queryName'],
  registers: [register],
});

// --- Bite Detection Metrics ---
export const biteCheckDurationHistogram = new promClient.Histogram({
  name: 'fish_bite_check_duration_ms',
  help: 'Bite check loop duration in ms',
  buckets: [5, 10, 25, 50, 100, 250],
  registers: [register],
});

export const biteEventsCounter = new promClient.Counter({
  name: 'fish_bite_events_total',
  help: 'Total bite events',
  labelNames: ['eventType', 'pondId'],
  registers: [register],
});

// --- Ecology Metrics ---
export const ecologyTickDurationHistogram = new promClient.Histogram({
  name: 'fish_ecology_tick_duration_ms',
  help: 'Ecology tick duration in ms',
  buckets: [5, 10, 25, 50, 100, 250, 500],
  registers: [register],
});

/** OBS-LOG-1: fanout metric (info log gated by FANOUT_LOG_INFO) */
export const socketBroadcastFanoutCounter = new promClient.Counter({
  name: 'fish_socket_broadcast_fanout_total',
  help: 'Socket room broadcast fanout emissions',
  labelNames: ['channel', 'pondId'],
  registers: [register],
});

// --- Business Metrics ---
export const activeConnectionsGauge = new promClient.Gauge({
  name: 'fish_active_connections',
  help: 'Active socket connections (aggregated)',
  registers: [register],
});

export const pondOccupancyGauge = new promClient.Gauge({
  name: 'fish_pond_occupancy',
  help: 'Number of users per pond',
  labelNames: ['pondId'],
  registers: [register],
});

export const metricsQueueDepthGauge = new promClient.Gauge({
  name: 'fish_metrics_queue_depth',
  help: 'Pending metrics queue depth',
  registers: [register],
});

export const onlinePlayersGauge = new promClient.Gauge({
  name: 'fish_online_players',
  help: 'Number of online players',
  registers: [register],
});

export const botCountGauge = new promClient.Gauge({
  name: 'fish_bot_count',
  help: 'Number of active bots',
  registers: [register],
});

// --- Loop Duration Metrics ---
export const fishingPhaseTickDurationHistogram = new promClient.Histogram({
  name: 'fish_fishing_phase_tick_duration_ms',
  help: 'Fishing phase tick duration in ms',
  buckets: [5, 10, 25, 50, 100, 250],
  registers: [register],
});

export function getMetricsRegister(): promClient.Registry {
  return register;
}

export async function getMetricsContent(): Promise<string> {
  return register.metrics();
}
