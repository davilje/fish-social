# STEAM-DESKTOP-08B：商店与装备

请只实现 `STEAM-DESKTOP-08B`。

## 必读

- `docs/planning/specs/Steam桌面端-08B商店与装备.md`
- `mobile/components/ShopModal.tsx`
- `mobile/lib/useShop.ts`
- `fish-social-unity/Assets/Scripts/Desktop/Auth/AuthenticatedApiClient.cs`

## 要求

- 创建 `PanelShop.prefab`，包含鱼饵/渔具页签、商品卡片、金币和购买/装备按钮。
- 所有金币、商品、购买和装备结果以服务端为准。
- 处理 loading、金币不足、库存不足、401/403、网络错误和重试。
- Overlay 菜单只增加 `menu_shop` 切主窗口页签，不在 Overlay 画商店。
- 从底栏和 Overlay 菜单打开商店时不离塘、不重复创建会话。

## 禁止

- 不在 Unity 本地扣金币或生成商品。
- 不在 Overlay 内实现商店页面。
