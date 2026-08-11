# Fish Social Monitoring

## Components

- **Prometheus**: Metrics collection at :3002/metrics (optional, METRICS_PROMETHEUS_ENABLED=true)
- **Loki**: Log aggregation (optional, LOKI_ENABLED=true)
- **Grafana**: Dashboard visualization (import JSON from grafana-dashboards/)
- **Alert Webhook**: Alert notifications via generic/dingtalk/wework webhook

## Quick Start

### Start monitoring stack
`ash
docker compose -f docker/docker-compose.monitoring.yml up -d
`

### Configure Server
`ash
# .env additions
METRICS_PROMETHEUS_ENABLED=true
METRICS_PROMETHEUS_PORT=3002
LOKI_ENABLED=true
LOKI_HOST=http://localhost:3100
ALERT_WEBHOOK_URL=https://hooks.example.com/alert
`

### Import Dashboards
1. Open Grafana at http://localhost:3000
2. Go to Dashboards > Import
3. Upload JSON from docs/monitoring/grafana-dashboards/

## Alert Rules

- Prometheus: docs/monitoring/alert-rules/prometheus.yml (5 rules)
- Loki: docs/monitoring/alert-rules/loki.yml (1 rule)

## Log Retention

- Server logs: 30 days (configurable via LOG_RETENTION_DAYS)
- Loki retention: 14 days (configure in Loki config)
- error_logs table: 90 days (configurable via ERROR_LOG_RETENTION_DAYS)
