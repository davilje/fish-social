<!-- 来源: docs/planning/specs/后端优化-D-工程债与部署.md §D2 -->
<!-- 用途: @backend-dev — BE-OPT-D 切片 D2（PG Metrics） -->

你是 Fish Social **后端工程师**。只做 **BE-OPT-D / D2 = QUAL-03**。

## 必读

- `docs/planning/specs/后端优化-D-工程债与部署.md`
- `server/src/postgresMetricsStore.ts` · `metricsStore.ts` · `fishingMetrics.ts` init

## 交付

1. `insertBatch`：**真正批量**写入（多行 INSERT 或 COPY），禁止逐行 await 假批量  
2. `insertBatch` 与异步完成语义清晰；shutdown 能等待未完成写入（可与 C 的 pool.end 衔接）  
3. `METRICS_READ_FROM=postgres`：要么实现可用的同步/异步读，要么 **启动时拒绝** 该配置并打明确错误  

`.env.example` 更新相关说明。

## 不做

vitest 大补（D1）· Docker CORS（D3）· 多机 Redis

## 完成后

CHANGELOG 记 D2；整包未齐勿标 BE-OPT-D 已实现。
