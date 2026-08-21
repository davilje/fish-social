# 开发提示词：禁止钓鱼塘巡警事件（FEAT-RISK-01）

你是 Fish Social **后端 + Unity Overlay**开发 Agent。按规格实现，勿扩需求。

## 必读

1. `docs/planning/specs/禁止钓鱼塘巡警事件.md`（**已实现** / **FEAT-RISK-01**）
2. `docs/planning/specs/鱼塘分级与玩家成长.md`（`forbidden` 塘）
3. `docs/planning/specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md`（气泡表现参考）
4. `desktop-overlay/MainWindow.xaml`（禁止塘 Overlay Debug 按钮挂载点）

## 顺序

1. 服务端：`forbidden` 塘钓鱼中按表概率触发巡警；10s 时限状态机。
2. 时限内离塘：不罚款，该 `pondId` 当日 2h 禁入。
3. 超时：扣金（不足归零）+ 当日禁钓该塘；再 join 拒绝并提示。
4. Overlay：约定文案气泡「巡警来了！快跑！」；主窗可见时系统警告。
5. **Debug**：禁止塘 Overlay「一键出警」→ 服务端同源强制触发；设置面板不放此按钮；非禁止塘或未钓鱼时提示失败原因。
6. 埋点：`forbidden_pond_fine` / `forbidden_pond_escaped`（登记埋点表）。
7. verify / 自检。

## 不做

- 警察 3D 模型、复杂 QTE、跨塘全局通缉
- Release 正式包默认暴露一键出警

## 验收

对照 spec §3；完成后回写计划表 **已实现** + `npm run planning:master-xlsx`；埋点变更走 `metrics-catalog-sync`。

## 派发

```text
@docs/planning/prompts/feat-risk-01-forbidden-police-dev.prompt.md 按此实现 FEAT-RISK-01
```

建议角色：`@backend-dev` + Unity Overlay。
