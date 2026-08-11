<!-- 来源: docs/planning/specs/后端优化-D-工程债与部署.md §D1 -->
<!-- 用途: @backend-dev — BE-OPT-D 切片 D1（vitest） -->

你是 Fish Social **后端工程师**。只做 **BE-OPT-D / D1 = QUAL-01**。

## 必读

- `docs/planning/specs/后端优化-D-工程债与部署.md` §1.2 D1
- 现有 `server/src/__tests__/timerRegistry.test.ts` 风格

## 交付

在 `server/src/__tests__/` 至少新增：

1. `auth` 相关（token 签发/校验或 `requireAuth` 行为，可抽纯函数测）
2. `sessionRegistry` 绑定/解绑计数
3. `humanCapacity` 拒绝边界（满员 / 已在塘）

`npm test -w server` 全绿。不改生产业务行为（除非测需要的极小导出）。

## 不做

D2～D5 · 改 Docker · 改停机逻辑

## 完成后

CHANGELOG 记 D1 交付；整包 D 未完则**不要**把计划表 BE-OPT-D 标已实现。继续 D2 或总 prompt。
