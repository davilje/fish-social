# STEAM-DESKTOP-08B：商店与装备

## 元信息

| 字段 | 内容 |
|---|---|
| 编号 | `STEAM-DESKTOP-08B` |
| 类型 | 功能 |
| 状态 | **已确认** |
| 目标版本 | v1.0-steam-desktop |
| 优先级 | P0 |
| 设计时间 | **2026-08-17** |
| 依赖 | 07E |

## 目标

将 Web 端鱼饵、渔具商店迁移为 Steam 主窗口的独立 `PanelShop.prefab`。

## 功能范围

- 鱼饵/渔具子页签。
- 商品图标、名称、说明、价格和库存。
- 金币余额。
- 购买、装备、已装备状态。
- 购买成功后刷新金币、库存和装备。
- 金币不足、库存不足、401/403、网络失败和重试提示。

## 入口与约束

- 主窗口独立页签 `PanelShop.prefab`；底栏和 Overlay 菜单增加 `menu_shop`，只切页，不在 Overlay 画商店。
- 沿用 07E：Prefab 为唯一页骨架；切页不 `leave_pond`、不重建 Socket/Overlay。

## 数据与边界

- 商品目录、金币、购买和装备结果以服务端为准。
- Unity 不自行扣金币、生成商品或计算最终价格。
- 不在 Overlay 内显示商店；不改商店服务端价格公式。

## 验收

- [ ] `PanelShop.prefab` 可从底部导航和 Overlay 菜单打开。
- [ ] 鱼饵和渔具购买、装备状态与 Web/服务端一致。
- [ ] 购买失败不会修改本地金币或库存。
- [ ] 切换商店不离开鱼塘、不重复创建 Socket/Overlay。

## 关联文件

- Web 基准：`mobile/components/ShopModal.tsx`、`mobile/lib/useShop.ts`
- Unity 数据层：`fish-social-unity/Assets/Scripts/Desktop/Auth/AuthenticatedApiClient.cs`
