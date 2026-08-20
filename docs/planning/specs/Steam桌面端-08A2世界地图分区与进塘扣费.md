# STEAM-DESKTOP-08A2：世界地图分区与进塘扣费

## 元信息

| 字段 | 内容 |
|------|------|
| 编号 | `STEAM-DESKTOP-08A2` |
| 类型 | 功能 |
| 状态 | **已确认** |
| 设计时间 | **2026-08-21** |
| 目标版本 | v0.7 |
| 优先级 | P0 |
| 依赖 | `STEAM-DESKTOP-08A`（已实现）、`FEAT-PROG-01` |
| 关联 | Overlay `menu_map` |

## 1. 目标

在 08A 扁平 20 点地图上，按六类分区展示与锁态；进收费塘前做**扣费说明确认**（非买时长包）；巨物可见暂闭。

### 非目标

- 新手塘上地图
- 扣费结算逻辑本身（服务端 PROG-01；本票只做确认 UI 与展示）
- ART 最终六区插画（可先色块占位）

## 2. 功能

1. 数据源改为导出 `ponds.json`（含 `pondCategory`、`mapZoneId`、`feePer2h`、`isOpen`、`showOnWorldMap`、坐标）。
2. 地图**不显示** `novice`；显示 advanced / veteran / wilderness / reservoir / forbidden / giant。
3. 标记区分类型；未解锁灰锁；巨物 `isOpen=false` 显示「暂未开放」、不可进入。
4. 进收费塘：确认弹窗展示每 2h 费用、今日扣费次数/剩余；确认后再 `join`/`SwitchPond`。
5. 免票塘可直接进（仍受引导/等级门禁）。
6. 引导未完成：不可进公开塘（或整图引导遮罩，与 PROG 一致）。

## 3. 验收

- [ ] 六类分区可辨；新手不上图
- [ ] 锁态与巨物暂闭正确
- [ ] 收费塘必经确认文案；免票无购票流程
- [ ] 与服务端拒绝原因展示一致（满员/未解锁/金币等）

## 4. 预估代码

- `DesktopWorldMapPanel.cs`、`WorldMapPonds.json`（生成）、详情面板
