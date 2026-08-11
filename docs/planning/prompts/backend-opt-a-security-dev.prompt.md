<!-- 来源: docs/planning/specs/后端优化-A-安全收口.md -->
<!-- 用途: @backend-dev — BE-OPT-A 安全收口 -->

你是 Fish Social **后端工程师**。实现 **BE-OPT-A**（SEC-01～SEC-06）。

## 必读

1. `docs/planning/specs/后端优化-A-安全收口.md`
2. `docs/planning/specs/后端优化-问题汇总与分批计划.md` §3.1
3. 现有 `server/src/auth.ts` · `socialRoutes.ts` · `createApp.ts` · `playerAnonymize.ts`

## 顺序

1. SEC-06 生产 pepper 硬失败 + `.env.example`
2. SEC-01 注册发 token 收紧（生产禁止任意 playerId 冒充）
3. SEC-02/03/04 读接口 `requireAuth` + 主体一致；DM 副作用
4. SEC-05 client-logs 鉴权 + 批次上限
5. 补 `verify:auth` 或 `verify:backend-opt-a`；检查 mobile 是否已带 Authorization

## 不做

FEAT-05 · 性能批次 B · Admin query key（属 C）· 千人多机

## 完成后

按 `.cursor/skills/planning-progress-sync/SKILL.md`：BE-OPT-A → **已实现** + 完成时间 → `npm run planning:master-xlsx`
