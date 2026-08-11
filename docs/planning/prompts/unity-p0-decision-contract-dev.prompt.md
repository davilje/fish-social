# 开发提示词：Unity 移植 Phase 0 — 决策与契约冻结（UNITY-P0）

你是 Fish Social **策划/全栈协调** Agent（可写 `docs/` 与契约清单；**本阶段不改玩法 FSM**）。按规格完成 UNITY-P0。

## 必读

1. `docs/planning/specs/Unity移植-分阶段需求清单.md` §2（**UNITY-P0 已确认**）
2. `docs/planning/product/Unity移植工程路径蓝图.md`（**REF-UNITY-1**）
3. `shared/types.ts` · Socket/REST 面（蓝图 §1.4）

## 顺序

1. 写迁移决策记录（Unity+Node；`mobile/` 冻结策略；仓库 `unity/` 形态）  
2. 导出契约冻结清单 v0：Socket C2S/S2C + 主 REST 路径表  
3. 约定 protocol/DTO 版本号写法  
4. 勾选 P0 验收；总表/计划表 P0 → **已实现**（完成日后）  

## 状态

**已实现**（2026-07-26）。产出：

- `docs/planning/architecture/Unity迁移决策记录.md`
- `docs/planning/architecture/Unity契约冻结清单-v0.md`

## 非目标

不重写 server FSM、不建 Unity 场景、不换协议事件名。

## 验收

对照清单 §2；完成后 `planning-progress-sync` Checklist B（仅 UNITY-P0）。

## 派发

```text
@docs/planning/prompts/unity-p0-decision-contract-dev.prompt.md 按此实现 UNITY-P0
```

建议角色：主 Agent / `@backend-dev`（契约清单）+ 制作人确认决策。
