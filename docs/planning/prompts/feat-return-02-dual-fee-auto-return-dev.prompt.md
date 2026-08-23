# 开发提示词：双价塘与自动回鱼（FEAT-RETURN-02）

你是 Fish Social **后端 + Unity 进塘 UI** 开发 Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/双价塘与自动回鱼.md`（**已实现** / **FEAT-RETURN-02**）
2. `docs/planning/specs/回鱼机制.md`（**已实现** / RETURN-01，复用管道）
3. `docs/planning/specs/Steam桌面端-08A2世界地图分区与进塘扣费.md`

## 顺序

1. `ponds` 表增 `feePer2hSellOnly` / `feePer2hAutoReturn`（及开关）；export。
2. 进塘确认 UI 二选一；join/session 绑定 `returnFeeMode`。
3. 扣费 tick 按模式取对应 feePer2h。
4. `auto_return`：捕获达标 → 自动走 RETURN-01 return 管道；`sell_only` 禁用回鱼。
5. 埋点 `return_fee_mode_selected` / `fish_auto_returned` → metrics-catalog-sync。
6. 自检脚本。

## 不做

- 改 RETURN-01 增重/卖价公式本体；进塘后切模式；Mobile

## 派发

```text
@docs/planning/prompts/feat-return-02-dual-fee-auto-return-dev.prompt.md 按此实现 FEAT-RETURN-02
```

建议角色：`@backend-dev` + Unity 进塘确认 UI。
