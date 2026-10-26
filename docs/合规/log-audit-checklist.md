# Log Audit Compliance Checklist

## Data Classification

| Data Type | Classification | Retention | Masking Required |
|-----------|---------------|-----------|-----------------|
| playerId | Technical ID | 90d (error_logs) | No (required for debugging) |
| Nickname | PII | 30d (file logs) | Yes (LOG_MASK_USER_DATA=true) |
| Chat text | PII | Not persisted in logs | Yes (LOG_MASK_USER_DATA=true) |
| IP addresses | Technical | 30d (file logs) | No (server logs only) |
| Game events | Technical | 30d (file logs) | No |
| Error stacks | Technical | 90d (error_logs) | No (may contain paths) |

## Retention Policies

| Storage | Default Retention | Configuration |
|---------|------------------|--------------|
| Server log files | 30 days | LOG_RETENTION_DAYS |
| Loki logs | 14 days | Loki retention_period |
| error_logs table | 90 days | ERROR_LOG_RETENTION_DAYS |
| fishing_metrics | 90 days | Archive script |

## Controls

1. [x] File log retention cleanup script (scripts/cleanup-old-logs.mjs)
2. [x] Sensitive data masking in logger (maskSensitiveFields)
3. [x] Audit log for admin operations (audit_log table)
4. [x] Debug sampling has automatic TTL expiry (30 min default)
5. [x] Admin RBAC for controlling access to user data

## Review Schedule

- Quarterly review of log contents for PII exposure
- Monthly cleanup verification
- Annual policy review

---
Last updated: 2026-07-11
