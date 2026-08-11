# A2 — Debug / Admin 面板

## 元信息

| 字段 | 内容 |
|---|---|
| 阶段代号 | A2 |
| 优先级 | P0 |
| 预计工期 | 3~5 天 |
| 依赖 | A0 已上线（A1 无强依赖，可并行） |
| 产物版本 | v0.2.0-alpha3 |
| 状态 | **已实现** |

## 1. 目标

策划与开发可自助查看每个鱼塘、每个钓点、每条鱼的咬钩概率与脱钩率分布，用于数值调优与线上问题排查。

## 2. 范围

### In-Scope

- 新增 admin REST 接口 `GET /api/admin/ponds/:pondId/fishing-debug`
- Admin UI 增加「钓鱼概率」折叠面板
- 关键常量读取路径改造（读运行时配置 + 硬编码兜底），供 C 期热更

### Out-of-Scope

- 参数写操作（归 C1）
- 玩家侧可见的概率查询

## 3. API 设计

### 3.1 `GET /api/admin/ponds/:pondId/fishing-debug`

**鉴权**：需 admin key（沿用现有中间件）。

**响应结构**：

```ts
{
  pondId: string;
  updatedAt: number;
  constants: {
    biteLambda: number;
    checkMs: number;
    // 当前生效值（若 C 期实现热更，此处返回热更后的值）
  };
  spots: Array<{
    spotId: string;
    spotBite: number;
    tickBiteChance: number;   // 假设 basic 饵、basic 竿的 P_tick
    fishContributions: Array<{
      fishId: string;
      speciesId: string;
      quality: FishQuality;
      sizeM: number;
      biteWeight: number;
      effectiveWeight: number;   // biteWeight + spotBite
      escapeRate: number;         // 假设 basic 竿
      shareOfTotal: number;       // w_i / W
    }>;
    lockedFishIds: string[];    // pending 中被排除的鱼
  }>;
  activeFishers: Array<{
    userId: string;
    spotId: string;
    fishingPhase: string;       // A0 时期为 'idle' | 'fishing'；C6 上线后为完整枚举
    phaseEndsAt?: number;
    equippedBaitId: string;
    equippedTackleId: string;
  }>;
  summary: {
    totalFish: number;
    byQuality: Record<FishQuality, number>;
    avgTickBiteChance: number;
  };
}
```

**性能**：响应内存缓存 3 秒，key 为 `pondId`。

## 4. Admin UI

在 `mobile/app/admin.tsx` 鱼塘详情页新增「钓鱼概率」折叠面板：

- **顶部摘要卡片**：总鱼数、各品质占比、平均 tickBiteChance
- **分钓点表格**：每行 `spotId / spotBite / tickBiteChance / 鱼数`
- **展开单个钓点**：Top 10 鱼贡献列表（按 `shareOfTotal` 降序）
  - 列：品质、鱼种、尺寸、biteWeight、effectiveWeight、escapeRate、占比
- **正在钓鱼玩家列表**：userId、phase、剩余时间、装备
- **刷新按钮**：拉最新数据（绕过 3s 缓存）

## 5. 常量读取改造

`BITE_LAMBDA`、`QUALITY_ESCAPE_BONUS`、`FISH_BITE_CHECK_MS`、`hookDurationMs` 参数：

```ts
// A2：只做读取路径
function getBiteLambda(): number {
  return runtimeConfig.get('BITE_LAMBDA') ?? BITE_LAMBDA_DEFAULT;
}
```

写入路径留给 C1。

## 6. 涉及文件

| 区域 | 文件 |
|---|---|
| server | `admin.ts` 增加路由；`fishingSession.ts` 抽公共计算函数供 debug 复用；新建 `runtimeConfig.ts` |
| mobile | `admin.tsx` 新增面板；拆出 `AdminFishingDebugPanel.tsx` |
| shared | `fishing.ts` 导出 debug 响应类型 |

## 7. 验收标准

- [ ] 无 admin key 访问返回 401
- [ ] 面板显示的 `tickBiteChance` 与实际 tick 统计误差 < 1pp（挂机 30 分钟采样对比）
- [ ] 面板中 `escapeRate` 与后续实际脱钩率误差 < 2pp（1000 次咬钩采样）
- [ ] 面板加载耗时 < 500ms（80 条鱼 × 4 钓点场景）
- [ ] 3s 内重复调用命中缓存

## 8. 风险

| 风险 | 缓解 |
|---|---|
| 反复调用面板对服务器造成压力 | 3s 内存缓存 |
| 面板泄露鱼塘敏感数据 | 复用 admin key 鉴权 |
| 缓存导致策划以为改动没生效 | UI 显示 `updatedAt` 时间戳与「强制刷新」按钮 |

## 9. 变更记录

| 日期 | 作者 | 变更 |
|---|---|---|
| 2026-07-01 | 策划 | A2 初稿 |
