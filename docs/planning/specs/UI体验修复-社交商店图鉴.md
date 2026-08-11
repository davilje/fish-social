# UI 体验修复 — 社交 / 商店 / 图鉴 / Debug

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | UI 体验修复（纪念照、商店、图鉴、Debug、好友动态） |
| 阶段代号 | UI-1 |
| 优先级 | P0 |
| 预计工期 | 1~1.5 周 |
| 依赖 | A0~B1 已上线（商店/图鉴后端可复用） |
| 状态 | **已实现** |
| 目标版本 | v0.2.3 |

---

## 1. 背景与目标

### 1.1 背景

v0.2.x 功能已落地，但多处 UI 与产品预期不符：

| 问题域 | 现状 | 用户反馈 |
|--------|------|----------|
| 纪念照 | `PostCard` 固定高度 + `cover` 裁切 | 社交动态中图片尺寸不适配 |
| 商店 | 仅鱼塘有入口；`ShopModal` 单列列表，部分环境看不到商品 | 需全局入口 + 背包式双栏布局 |
| 图鉴 | 底部抽屉、列表拥挤；仅鱼塘 📖 入口 | 需背包式布局 + 全局入口 + 用户数据打通 |
| Debug | 仅世界地图顶栏 + 社交设置弱入口 | 任意界面可打开 |
| 关注栏 | `friends-feed` 含**全部公开帖** | 应仅好友（+自己）动态 |

### 1.2 目标

1. 纪念照在动态流中**完整可见、比例正确**，不裁切、不拉伸。
2. 商店/图鉴采用与 **背包 `BackpackModal` 一致** 的双栏模态规范，并在**世界地图、社交中心、鱼塘**均可打开。
3. 图鉴进度与玩家账号绑定，钓获后自动更新，UI 展示完整物种信息。
4. Debug/Admin 有**独立全局入口**（顶栏按钮），不依赖进入特定页面。
5. 「关注」Tab 改名为 **「好友动态」**，仅展示自己 + 好友帖子。

### 1.3 非目标

- 不重做社交中心整体架构
- 不新增付费货币
- 不实现 C6 钓鱼阶段动画
- 不改登录/认证流程

---

## 2. 参考规范：背包模态（`BackpackModal`）

后续商店、图鉴**必须对齐**以下结构：

```
Modal overlay（居中，rgba 遮罩）
└── panel（width 100%, maxWidth 720/860, maxHeight 90%）
    ├── header：标题 + 副信息（金币等）+ ✕
    └── body（桌面横排 / 移动竖排）
        ├── displayPane（左/上）：选中项大图 + 详情 + 操作按钮
        └── gridPane（右/下）：4 列网格 ScrollView + 选中高亮
```

**响应式**：`useResponsive().isDesktop` 控制 `bodyDesktop` 横排。

---

## 3. 功能规格

### 3.1 纪念照尺寸适配（P0）

**涉及**：`PostCard.tsx`（社交中心、他人资料弹窗均复用）

**规则**：
- `resizeMode` 改为 **`contain`**（完整显示，不裁切）
- 容器使用 **`aspectRatio: 4 / 3`**（纪念照资源多为横图），`width: '100%'`
- `maxHeight`：移动端 240px，桌面 320px
- 背景色 `colors.primaryLight` 作为 letterbox 留白
- 图片加载失败：隐藏图片区，保留文字鱼获信息

**验收**：
- [ ] 横图纪念照上下或左右留白，主体完整可见
- [ ] 竖屏手机动态列表无图片溢出卡片
- [ ] 他人资料弹窗内动态纪念照表现一致

---

### 3.2 商店 UI 重构 + 全局入口（P0）

#### 3.2.1 入口

| 位置 | 组件 | 说明 |
|------|------|------|
| 世界地图 `index.tsx` | `ShopButton` | 与背包、社交并列 |
| 社交中心 `social.tsx` | `ShopButton` | 顶栏右侧 |
| 鱼塘 `pond/[id].tsx` | 保留，统一为 `ShopButton`（替换 `SupplyButton`） |

点击后打开 `ShopModal`；各页面各自持有 `shopOpen` 状态 + `useShop(playerId)`。

#### 3.2.2 `ShopModal` 布局（对齐背包）

```
header：🛒 补给站 | 💰 金币 | 当前饵/竿 | ✕
tabs：鱼饵 | 渔具（保留）
body：
  displayPane：选中商品大图（icon 放大）、名称、价格、加成、库存/拥有状态、购买/装备按钮
  gridPane：4 列网格
    - 鱼饵格：icon + 名称 + 库存角标；已装备边框高亮
    - 渔具格：icon + 名称；未拥有半透明
```

**修复「看不到商品」**：
- `panel` 必须设 `width: '100%'`、`maxWidth: 860`、`alignSelf: 'center'`
- `body` 设 `minHeight: 360`（桌面）
- `gridPane` 使用 `flex: 1` + `ScrollView`，禁止仅靠 `maxHeight` 导致高度塌陷

**B1 偏好标签**：显示在 displayPane 详情区，不在网格格内堆叠。

**验收**：
- [ ] 世界地图可打开商店并看到全部鱼饵/渔具
- [ ] Web 与移动端商品列表均可滚动、可选中
- [ ] 双栏布局与背包视觉一致（圆角、配色、格子比例）
- [ ] 购买/装备流程与现 B0 API 不变

---

### 3.3 图鉴 UI 重构 + 用户数据（P0）

#### 3.3.1 数据（已有，需验收打通）

| 层 | 说明 |
|----|------|
| DB | `fish_codex(player_id, species_id, total_caught, max_size_m, first_caught_at, last_caught_at)` |
| 写入 | `recordCodexCatch` 于 `accept_catch` 成功时 |
| 读取 | `GET /api/player/codex?playerId=` |
| 实时 | ~~Socket `codex_unlocked` 首次解锁提示~~ → **改 §8.1：钓获弹窗「新」角标**（废弃独立 Alert） |

**策划要求**：开发需确认 sell/share 不误删图鉴记录；图鉴只增不减（除删号）。

**可选增强**：`GET /api/players/:id` 响应增加 `codexSummary: { unlocked: number, total: 20 }`（P1，本 spec 不强制）。

#### 3.3.2 客户端 API

新建 `mobile/lib/codexApi.ts` 封装 `getCodex(playerId)`，**禁止** `CodexModal` 内裸 `fetch`。

#### 3.3.3 全局入口

| 位置 | 组件 |
|------|------|
| 世界地图 | `CodexButton` |
| 社交中心 | `CodexButton` |
| 鱼塘 | `CodexButton`（替换 inline 📖） |

#### 3.3.4 `CodexModal` 布局（对齐背包）

```
header：📖 钓鱼图鉴 | 已解锁 X/20 | ✕
body：
  displayPane：
    - 未选中：提示「选择右侧鱼种查看详情」
    - 已选中未钓到：??? + 剪影 icon + 「尚未钓到」
    - 已钓到：大 icon、名称、食性、咬钩权重、脱钩率、记录（次数/最大尺寸/首次时间）、推荐鱼饵 Top3
  gridPane：4 列，20 鱼种
    - 已解锁：彩色 icon + 名称
    - 未解锁：灰色 🔒 + 「???」
    - 选中高亮边框
```

**废弃**：底部 sheet（`justifyContent: 'flex-end'`）、列表与详情同屏拥挤布局。

**验收**：
- [ ] 钓获新品种后图鉴格子解锁，数据持久化重启仍在
- [ ] 三处入口均可打开图鉴
- [ ] 布局与背包双栏一致，移动端可上下滚动
- [ ] 推荐鱼饵与 B1 `baitBiteBonus` 一致

---

### 3.4 Debug 全局入口（P0）

**组件**：复用 `AdminDebugButton`（跳转 `/admin`）

**展示位置**（顶栏 `right` 区域，与背包并列）：

| 页面 | 是否展示 |
|------|----------|
| 世界地图 `index.tsx` | ✅ 已有 |
| 鱼塘 `pond/[id].tsx` | ✅ 新增 |
| 社交中心 `social.tsx` | ✅ 新增（顶栏，不仅设置页） |
| 个人资料 `profile.tsx` | ✅ 新增 |
| 登录页 | ❌ |

**`_layout.tsx`**：补充注册 `admin` Stack Screen（可选，避免路由警告）。

**验收**：
- [ ] 从鱼塘、社交、资料页均可一键进 Admin
- [ ] 按钮样式与世界地图一致（compact 移动端仅 ⚙）

---

### 3.5 好友动态过滤（P0）

#### 3.5.1 产品定义

Tab 重命名：**「关注」→「好友动态」**

**可见范围**：
- 自己的全部帖子（任意 `visibility`）
- **互为好友**的玩家的帖子，且 `visibility ∈ { public, friends }`
- **不包含**：非好友的 `public` 帖子

#### 3.5.2 服务端 SQL（`posts.ts`）

```sql
SELECT p.* FROM social_posts p
WHERE p.player_id = @viewer
   OR (
     EXISTS (
       SELECT 1 FROM friend_links f
       WHERE f.player_id = @viewer AND f.friend_id = p.player_id
     )
     AND p.visibility IN ('public', 'friends')
   )
ORDER BY p.created_at DESC
LIMIT 500
```

**与动态墙差异**：
- 动态墙：仅 `visibility = 'public'`
- 好友动态：自己 + 好友，无陌生人公开帖

**验收**：
- [ ] 非好友发的公开帖不出现在好友动态
- [ ] 好友发 `friends` 可见帖，己方可见
- [ ] 自己发 `friends` 可见帖，自己可见
- [ ] Tab 文案为「好友动态」

---

## 4. 涉及文件（开发预估）

| 区域 | 文件 |
|------|------|
| mobile | `PostCard.tsx`、`ShopModal.tsx`、`CodexModal.tsx`、新建 `ShopButton.tsx`、`CodexButton.tsx`、`lib/codexApi.ts`；改 `index.tsx`、`social.tsx`、`pond/[id].tsx`、`profile.tsx`、`_layout.tsx` |
| server | `posts.ts`（好友动态 SQL） |
| 可选 | `players.ts`（codexSummary） |

---

## 5. 验收总则

- [ ] §3.1~3.5 全部子项勾选
- [x] §8.1~8.2 补丁项勾选
- [ ] 无新增 P1 范围外功能
- [ ] 商店/图鉴/背包三模态视觉一致（策划截图对比）

---

## 6. 风险

| 风险 | 缓解 |
|------|------|
| 三页面重复模态状态代码 | 可抽 `useShop`/`CodexModal` 复用，不强制抽 `GameHeader` |
| 好友动态过空（新玩家无好友） | 空状态文案：「添加好友后这里会显示他们的鱼获」 |
| 图鉴 20 种移动端网格过高 | gridPane 独立滚动，displayPane 固定最小高度 |

---

## 8. 补丁需求（v0.2.3.1）

> 基于 v0.2.3 实现验收反馈，**仅补以下两项**，不扩大范围。

### 8.1 图鉴解锁：并入钓获弹窗「新」角标（P0）

#### 8.1.1 问题

- 当前：`accept_catch` 成功后 socket 发 `codex_unlocked`，鱼塘页 `showAlert('图鉴解锁', …)` **顶部弹窗**。
- 体验割裂：玩家正在看 `CatchFishModal`，Alert 与弹窗时序冲突；图鉴解锁感弱。

#### 8.1.2 产品行为

| 时机 | 行为 |
|------|------|
| 鱼上钩弹窗展示时 | 若本次鱼种为玩家**首次钓获**，在弹窗鱼 icon 区域显示 **「新」角标** |
| 非首次同种鱼 | 不显示角标 |
| 空军/脱钩弹窗 | 不显示 |
| 点击「获得」后 | **不再**弹出 `Alert` / 系统提示「图鉴解锁」 |

**角标视觉**：
- 位置：鱼 icon 圆形容器**右上角**（absolute）
- 文案：**新**（单字）
- 样式：红底 `#F44336`、白字、圆角 8px、字号 11、加粗；可选轻微 scale 入场动画（200ms）
- 副文案（可选一行小字）：`首次收录图鉴` 放在品质名下方，字号 12、灰色

#### 8.1.3 数据流（服务端权威）

**判定时机**：在发出 `fish_bite` / 创建 `PendingFishCatch` 时（**早于** `accept_catch`），只读查询图鉴：

```ts
isCodexNew = (getCodexEntry(playerId, speciesId)?.totalCaught ?? 0) === 0
```

**扩展 `PendingFishCatch`**：

```ts
interface PendingFishCatch {
  // ...现有字段
  /** 本次钓获是否为该鱼种首次收录图鉴 */
  isCodexNew?: boolean;
}
```

- `fish_bite` payload 携带 `isCodexNew`
- `accept_catch` 仍调用 `recordCodexCatch`（写入逻辑不变）
- **废弃**客户端对 `codex_unlocked` 的 `Alert`；事件可保留供图鉴页静默刷新（`CodexModal` 打开时 reload）

**Demo 模式**：本地 pending 无服务端时，客户端可用内存 Set 记录本 session 已钓过种；或 `isCodexNew` 默认 false。

#### 8.1.4 涉及文件

| 区域 | 文件 |
|------|------|
| shared | `types.ts`（`PendingFishCatch.isCodexNew`） |
| server | `fishingSession.ts` / `inventory.ts`（构造 pending 时查询）；`index.ts`（payload） |
| mobile | `CatchFishModal.tsx`（角标 UI）；`pond/[id].tsx`（**删除** `onCodexUnlocked` Alert）；`usePondSocket.ts`（可选：转发 isCodexNew） |

#### 8.1.5 验收

- [x] 首次钓到某鱼种：钓获弹窗 icon 右上角有「新」，无顶部 Alert
- [x] 第二次钓同种：无「新」角标
- [x] 领取后图鉴页该种已解锁、数据持久化
- [x] 脱钩/空军弹窗无角标

---

### 8.2 商店商品显示缺失修复（P0）

#### 8.2.1 问题

补给站双栏布局已上线，但**网格区/详情区商品信息不可见或为空**（鱼饵 4 种、渔具 4 种应始终展示）。

可能原因（开发需逐项排查）：
1. `useShop` 未在打开前 `refresh()`，或 API 失败导致 `baits`/`tackles` 空数组
2. `body` / `gridPane` 无 `flex:1`、`minHeight`，Web 下网格高度塌陷为 0
3. 格子内 `cellIcon`/`cellName` 颜色与背景对比不足或字号过小
4. `loading` 长期 true 挡住 body
5. 详情区 `selectedBait` 为 null 时仅显示空状态，网格也未渲染

#### 8.2.2 修复要求

**布局（对齐 `BackpackModal`）**：

```ts
body: { flex: 1, flexDirection: 'column', minHeight: 320 }
bodyDesktop: { flexDirection: 'row', minHeight: 360 }
gridPane: { flex: 1, minHeight: 200, padding: 16 }
gridPaneDesktop: { flex: 1, maxHeight: undefined }
gridScroll: { flex: 1 }  // 父级必须有确定高度
```

**商品数据**：
- 打开商店时**必须** `await refresh()`（各入口 `setShopOpen(true)` 前）
- API 失败：展示错误条 +「重试」；**兜底**使用 `shared` 的 `BAITS` / `TACKLES` 静态列表渲染（价格/加成只读），购买按钮 disabled 并提示「请检查网络」
- Tab 切换时自动选中当前 Tab 第一个商品（已有逻辑，需保证 `baits.length > 0` 时生效）

**格子与详情必显字段**：

| Tab | 网格格 | 详情区 |
|-----|--------|--------|
| 鱼饵 | `icon`（≥28px）、`name`、库存角标（付费饵） | 大 icon、名称、单价、globalBonus、食性偏好标签、库存、买/装备 |
| 渔具 | `icon`、`name`、未拥有半透明 | 大 icon、名称、价格、脱钩减免%、拥有状态、买/装备 |

**空目录态**：`鱼饵 · 0 种` 时显示「加载失败或目录为空」+ 重试，**禁止**空白面板。

#### 8.2.3 涉及文件

| 区域 | 文件 |
|------|------|
| mobile | `ShopModal.tsx`、`useShop.ts`、各页打开商店处（`index`/`social`/`pond`） |
| server | 确认 `GET /api/shop/baits`、`/tackle` 返回完整 4+4 条（`shop.ts`） |

#### 8.2.4 验收

- [x] 世界地图 / 社交 / 鱼塘打开商店：鱼饵 Tab 可见 4 种、渔具 Tab 可见 4 种（名称+图标）
- [x] 点击格子，左侧详情同步切换
- [x] Web Chrome 与移动端均无「空白商店」
- [x] 断网时显示错误与重试，静态兜底至少可见商品名

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-03 | 开发 | §8.1~8.2 补丁实现；gear FK 修复 |
| 2026-07-01 | 策划 | §8 补丁：图鉴「新」角标入钓获弹窗；商店商品显示修复 |
| 2026-06-30 | 开发 | §3.1~3.5 实现 |
| 2026-07-01 | 策划 | 初稿（纪念照、商店、图鉴、Debug、好友动态） |
