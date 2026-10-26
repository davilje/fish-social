# DB 备份与恢复

## 备份

```bash
npm run db:backup
```

输出到 `data/backups/fish-social-YYYYMMDD-HHmm.db.gz`，保留最近 7 份。

## 恢复

```bash
# 1. 停止服务
# 2. 解压备份
gzip -d -k data/backups/fish-social-20260101-1200.db.gz
# 3. 覆盖 DB
cp data/backups/fish-social-20260101-1200.db data/fish-social.db
# 4. 重启服务
```
