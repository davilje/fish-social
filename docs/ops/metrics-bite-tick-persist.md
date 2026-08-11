# 埋点：咬钩 tick 明细开关（D-L2-15）

默认**不再**向 `fishing_metrics` 写入 `bite_tick_miss` / `bite_tick_hit`。  
空竿判定只累加内存计数；上钩写 `bite_hook`、脱钩写 `escape`，payload 带 `sessionHooks` / `sessionEscapes` / `sessionMissTicks` 等。

## 恢复 tick 明细（压测 / 调参）

```bash
# Windows PowerShell
$env:METRICS_BITE_TICK_PERSIST="1"
npm run server

# 或写入 .env
METRICS_BITE_TICK_PERSIST=1
```

设为 `0` / 未设置 = 默认关闭。

## 相关

- Spec：`docs/planning/specs/埋点优化-咬钩脱钩计数替代tick.md`
- 日聚合：`hook_count` / `escape_count`；旧列 `bite_tick_hit` 兼容填入上钩数
