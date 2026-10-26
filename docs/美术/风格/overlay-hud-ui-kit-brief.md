# Overlay HUD UI 美术资源需求清单（gpt-image 移交）

| 字段 | 内容 |
|------|------|
| 目标 | Steam Overlay HUD（960×560）正式按钮/底板素材 |
| 关联 | `STEAM-DESKTOP-ART-03` / `STEAM-DESKTOP-14C`；控件源：`OverlayHudWidgetCatalog` |
| 交付目录（过审后） | `desktop-overlay/OverlayResources/hud/`（由 Unity OverlayHud Prefab 引用后 Export） |
| 生成工具 | gpt-image（或同级文生图），**必须 RGBA / 透明背景** |

---

## 1. 总原则（必读）

1. **不要把会变的汉字烤进 PNG**  
   运行时文字仍由 Overlay 改 Content：`开始钓鱼`↔`收杆`、打窝次数、`离席`↔`领取鱼获`、菜单文案、状态字等。  
   PNG 只做：**底板 + 图标 + 装饰**。
2. **透明背景**：棋盘格外区域 alpha=0；不要灰底、不要假透明。
3. **扁平卡通 UI**：猫咪 + 小鱼主题；干净描边、块面色、少渐变、无写实照片、无霓虹赛博、无水印文字。
4. **可复用组件优先**：同一底板/描边/高光只生成一套，再叠不同图标；禁止每个按钮整图硬编码成无法复用的成品。
5. **导出尺寸**：下表「逻辑 px」= Overlay 控件矩形；建议 **按 4× 出图** 再缩放到逻辑尺寸入库（抗锯齿更稳）。

### 风格锚点（拼进每条 prompt）

**前缀（共用）**
```text
Fish Social casual fishing game UI, flat 2D cartoon, cute cat and small fish motif,
soft pastel palette (teal water, warm cream, coral accent), clean vector-like shapes,
rounded rectangles, thick soft outlines, minimal shading, no gradients overkill,
```

**后缀（共用）**
```text
isolated UI asset on fully transparent background, PNG with alpha channel,
no text, no letters, no watermark, no drop shadow onto background, no 3D render, no photo
```

色板建议（可写进 prompt）：  
主色 `#2A6F8F` 水色 · 辅色 `#E8D5A3` 奶油 · 强调 `#E07A5F` 珊瑚 · 深底 `#0F1820` 半透面板 · 描边 `#1B3A4A`。

---

## 2. Overlay 需要贴图的控件（完整表）

| widgetId | 类型 | 逻辑尺寸 | 默认文案（仅参考，勿烤进图） | 素材策略 |
|----------|------|----------|------------------------------|----------|
| `btn_menu_toggle` | button | 70×32 | ≡ | 共用小按钮板 + 菜单图标 |
| `btn_menu_map` | button | 70×32 | 世界地图 | 共用板 + 地图图标 |
| `btn_menu_shop` | button | 70×32 | 商店与装备 | 共用板 + 商店图标 |
| `btn_menu_friends` | button | 70×32 | 好友与聊天 | 共用板 + 好友图标 |
| `btn_menu_catch` | button | 70×32 | 鱼获/背包 | 共用板 + 背包/鱼获图标 |
| `btn_menu_leaderboard` | button | 70×32 | 排行榜 | 共用板 + 奖杯图标 |
| `btn_menu_settings` | button | 70×32 | 设置 | 共用板 + 齿轮图标 |
| `btn_open_main` | button | 70×32 | 打开主界面 | 共用板 + 主界面图标 |
| `btn_exit_pond` | button | 70×32 | 退出鱼塘 | 共用板 + 退出图标 |
| `btn_fishing_toggle` | button | 70×32 | 开始钓鱼/收杆 | 共用板 + 鱼竿图标（**无字**） |
| `btn_groundbait` | button | 70×32 | 打窝 x/N | 共用板 + 饵料图标（**无字**） |
| `btn_catch_leave` | button | 70×32 | 离席/领取鱼获 | 共用板 + 离席图标（**无字**） |
| `btn_pan_left` | button | 36×64 | ◀ | 竖条板 + 左箭头（可镜像） |
| `btn_pan_right` | button | 36×64 | ▶ | 同上镜像 |
| `chat_toggle` | button | 38×28 | ▲/▼ | 小方板 + 三角（可旋转） |
| `chat_send` | button | 38×28 | 发送 | 小方板 + 纸飞机/鱼讯图标 |
| `cap_status` | panel | 134×88 | （状态文字运行时） | 状态胶囊底板 |
| `dock_chat` | panel | 291×36（折叠） | （预览文字运行时） | 聊天底栏九宫/拉伸底板 |
| `chat_input` | panel | 240×28 | （输入框） | 输入槽底板 |

**本期不生成正式美术（可占位）**  
`btn_debug_police` / `btn_debug_gameplay`（调试）、纯文字 `txt_*`、`menu_rail` / `dock_fishing`（布局组无图）。

---

## 3. 提取后的「可复用组件包」（交给 gpt-image 的真正清单）

只生成下列 **组件**；业务按钮 = `底板 + 图标`（Unity/Overlay 叠图或导出合成均可）。

### A. 共用底板（Chrome）

| 组件 ID | 逻辑尺寸 | 4× 出图 | 说明 |
|---------|----------|---------|------|
| `ui_btn_plate_sm` | 70×32 | 280×128 | 右侧菜单 / 钓鱼条通用圆角按钮板（常态） |
| `ui_btn_plate_sm_pressed` | 70×32 | 280×128 | 同上按下态（略压暗/内凹）— **可选** |
| `ui_btn_plate_sm_disabled` | 70×32 | 280×128 | 禁用态（降饱和）— **可选** |
| `ui_btn_plate_pan` | 36×64 | 144×256 | 场景平移竖条按钮板（左右共用，箭头另贴） |
| `ui_btn_plate_xs` | 38×28 | 152×112 | 聊天展开 / 发送 小方板 |
| `ui_panel_status` | 134×88 | 536×352 | 右下状态胶囊（半透明深色圆角+细描边，可带猫耳或小鱼角饰） |
| `ui_panel_chat_dock` | 291×36 | 1164×144 | 聊天栏折叠底板；需可竖向拉伸到 ~64px 展开（优先九宫或纯色板+圆角） |
| `ui_panel_chat_input` | 240×28 | 960×112 | 输入槽：内凹浅槽，透明外缘 |

### B. 图标（Icons，一律居中透明底）

建议统一 **画布 128×128**（或 256×256），主体约占 70%，便于缩放到 70×32 按钮内 ~20–24px 高。

| 组件 ID | 语义 | 主题隐喻（猫/鱼） |
|---------|------|-------------------|
| `ico_menu` | 菜单开关 | 三条鱼骨≡ 或 猫爪三道痕 |
| `ico_map` | 世界地图 | 小岛/水洼地图 + 小鱼标记 |
| `ico_shop` | 商店 | 鱼饵袋 / 猫爪钱袋 |
| `ico_friends` | 好友 | 两只小猫头并排 |
| `ico_catch` | 鱼获/背包 | 鱼篓或背包露鱼尾 |
| `ico_leaderboard` | 排行榜 | 小奖杯顶站小猫 |
| `ico_settings` | 设置 | 齿轮，中心嵌小鱼剪影 |
| `ico_open_main` | 打开主界面 | 窗户/门框里探出猫头 |
| `ico_exit_pond` | 退出鱼塘 | 门+朝外箭头，或猫拎鱼出门 |
| `ico_fishing` | 钓鱼/收杆 | 鱼竿+浮漂（**无文字**） |
| `ico_groundbait` | 打窝 | 饵料团/撒窝手势（**无数字**） |
| `ico_leave` | 离席 | 空座位/起身猫剪影 |
| `ico_arrow_left` | 左平移 | 扁平三角/鱼头朝左（右箭头用水平镜像） |
| `ico_chevron_up` | 聊天展开 | 扁平三角朝上（收起垂直翻转） |
| `ico_send` | 发送 | 纸飞机或吐泡小鱼 |

**镜像复用（不必单独出图）**  
- `ico_arrow_right` ← 镜像 `ico_arrow_left`  
- `ico_chevron_down` ← 翻转 `ico_chevron_up`

### C. 可选装饰（一套即可）

| 组件 ID | 用途 |
|---------|------|
| `deco_fish_dot` | 小鱼圆点，点缀面板角 |
| `deco_paw_corner` | 猫爪角饰，状态胶囊角 |

---

## 4. 控件 → 组件装配表

| widgetId | 装配 |
|----------|------|
| 菜单 7 键 + open/exit | `ui_btn_plate_sm` + 对应 `ico_*` |
| `btn_fishing_toggle` / `groundbait` / `catch_leave` | `ui_btn_plate_sm` + `ico_fishing` / `ico_groundbait` / `ico_leave` |
| `btn_pan_left` / `right` | `ui_btn_plate_pan` + `ico_arrow_left`（右镜像） |
| `chat_toggle` | `ui_btn_plate_xs` + `ico_chevron_up`（收起翻转） |
| `chat_send` | `ui_btn_plate_xs` + `ico_send` |
| `cap_status` | `ui_panel_status` |
| `dock_chat` | `ui_panel_chat_dock` |
| `chat_input` | `ui_panel_chat_input` |

文案层始终叠在最上面（程序字体），图标建议偏左或纯图标居中；**不要**在图标层写「地图」「商店」等字。

---

## 5. gpt-image 单条生成 Prompt（可直接粘贴）

### 5.0 推荐：一份提示词整包出图（精灵表）

一次生成完整 HUD UI Kit，再按格子裁成 `chrome/` + `icons/`。  
建议出图 **1536×1024** 或 **2048×2048**；背景必须全透明。

```text
Fish Social casual fishing game HUD UI kit sprite sheet, flat 2D cartoon, cute cat and small fish motif, soft pastel palette teal water #2A6F8F cream #E8D5A3 coral #E07A5F dark panel #0F1820 outline #1B3A4A, clean vector-like shapes, rounded rectangles, thick soft outlines, minimal shading, no gradients overkill.

Create ONE transparent PNG atlas with clearly separated tiles in a tidy grid, generous padding between tiles, no overlapping, no text labels, no letters, no numbers, no watermark.

Row 1 — chrome plates only (empty faces, no icons on them):
(1) wide small button plate aspect ~70:32
(2) same plate slightly pressed/darker
(3) tall vertical pan button plate aspect ~36:64
(4) tiny square button plate aspect ~38:28
(5) status capsule panel aspect ~134:88 semi-transparent dark rounded bubble, optional tiny cat-ear tips on top corners, empty interior
(6) wide chat dock bar aspect ~291:36 semi-transparent dark teal rounded bar, empty
(7) chat input slot aspect ~240:28 inset lighter field, empty

Row 2 — flat UI icons, centered, bold silhouette readable at small size, single-color fill with outline, each on its own tile:
menu as three fishbone stripes; island map with fish pin; bait pouch with paw; two cute cat heads; fish basket with tail; trophy with cat; gear with fish cutout; window with cat peeking; door with outward arrow; fishing rod and bobber; bait ball; empty fishing seat; bold chevron left; bold chevron up; paper-plane fish send icon.

Fully transparent background everywhere outside the tiles, PNG with alpha channel, isolated UI assets only, no 3D, no photo, no drop shadows onto the background, no readable text of any kind.
```

裁切对照（从左到右 / 上到下）：

| 格 | 入库名 |
|----|--------|
| R1-1 | `ui_btn_plate_sm.png` |
| R1-2 | `ui_btn_plate_sm_pressed.png`（可选） |
| R1-3 | `ui_btn_plate_pan.png` |
| R1-4 | `ui_btn_plate_xs.png` |
| R1-5 | `ui_panel_status.png` |
| R1-6 | `ui_panel_chat_dock.png` |
| R1-7 | `ui_panel_chat_input.png` |
| R2-1…15 | `ico_menu` … `ico_send`（顺序同 §3.B 表） |

右箭头 / 下三角：分别镜像 `ico_arrow_left`、翻转 `ico_chevron_up`，不必再生成。

---

### 5.1 备选：按单件拆分生成（仅在整包效果差时用）

每条 = **§1 前缀** + 下列中段 + **§1 后缀**。  
出图尺寸建议 **1024×1024**（图标）或 **1024×512**（横条底板），再裁切到「4× 出图」尺寸。

### 5.1 底板

**`ui_btn_plate_sm`**
```text
flat cartoon UI button plate, size aspect 70:32, rounded rectangle,
soft teal-cream fishing game chrome, subtle inner highlight, thick outline,
empty face for icon overlay, no icon, no glyph
```

**`ui_btn_plate_pan`**
```text
tall flat cartoon UI vertical button plate, aspect 36:64, rounded capsule,
semi-transparent dark teal glass, thick outline, empty for arrow icon
```

**`ui_btn_plate_xs`**
```text
tiny flat cartoon UI square button plate, aspect 38:28, rounded corners,
cream-teal chrome, empty for small glyph
```

**`ui_panel_status`**
```text
flat cartoon status capsule panel, aspect 134:88, rounded bubble,
semi-transparent dark navy fill, soft outline, optional tiny cat-ear tips on top corners,
empty interior for text, no readable text
```

**`ui_panel_chat_dock`**
```text
flat cartoon chat bar panel, wide aspect 291:36, rounded rectangle,
semi-transparent dark teal, soft outline, empty content area, stretch-friendly flat fill
```

**`ui_panel_chat_input`**
```text
flat cartoon text input slot, aspect 240:28, inset rounded field,
lighter inner fill, dark outline, empty, no placeholder text
```

### 5.2 图标（各一条）

模板：
```text
flat cartoon UI icon, [SEMANTIC], cute cat-and-fish fishing game motif,
centered, bold silhouette readable at 24px, single color fill with outline
```

| ID | `[SEMANTIC]` 替换 |
|----|-------------------|
| `ico_menu` | three fishbone stripes like hamburger menu |
| `ico_map` | tiny island map with a fish pin |
| `ico_shop` | bait pouch with paw print |
| `ico_friends` | two cute cat heads side by side |
| `ico_catch` | fish basket with fish tail sticking out |
| `ico_leaderboard` | small trophy with a cat on top |
| `ico_settings` | gear with tiny fish cutout center |
| `ico_open_main` | window frame with cat peeking out |
| `ico_exit_pond` | door with outward arrow and fish |
| `ico_fishing` | fishing rod and bobber |
| `ico_groundbait` | bait ball for groundbait |
| `ico_leave` | empty fishing seat silhouette |
| `ico_arrow_left` | bold chevron arrow pointing left |
| `ico_chevron_up` | bold chevron pointing up |
| `ico_send` | paper plane shaped like a small fish |

---

## 6. 文件命名与入库约定

```text
desktop-overlay/OverlayResources/hud/
  chrome/
    ui_btn_plate_sm.png
    ui_btn_plate_pan.png
    ui_btn_plate_xs.png
    ui_panel_status.png
    ui_panel_chat_dock.png
    ui_panel_chat_input.png
  icons/
    ico_menu.png
    ico_map.png
    ...
```

- 格式：**PNG-24 + alpha**（或 PNG-32）  
- 禁止 JPEG  
- Unity：Sprite、Pivot Center；按钮板可设 9-slice（左右 12px / 上下 8px 量一下圆角）  
- Overlay 导出后文件名需与 Prefab `spriteFile` 一致

---

## 7. 验收清单

- [ ] 所有组件透明背景，无灰底/白底  
- [ ] 同一套色板与描边粗细，图标可识别缩到 ~24px  
- [ ] 无任何汉字/拉丁文烤进图  
- [ ] 70×32 系按钮共用同一 `ui_btn_plate_sm`  
- [ ] 左右平移、聊天三角用镜像/翻转，不重复出两套风格不一致的图  
- [ ] 换上 Prefab → `Export Overlay HUD` → Overlay 矩形误差 ≤1px（14C）  
- [ ] 钓鱼/打窝/离席文字切换仍正常  

---

## 8. 工作量（给生成侧）

| 类别 | 张数（最少） |
|------|----------------|
| 底板 Chrome | 6（+2 态可选） |
| 图标 | 15（另 2 个由镜像得到） |
| 装饰可选 | 0–2 |
| **合计** | **约 21–25 张**（远少于「每按钮一整图」的 18+ 张不可复用稿） |

---

## 9. 移交一句话

> 请按本文 **§3 组件包** 生成带 alpha 的扁平卡通猫鱼主题 UI 零件；**§5** 为可粘贴 prompt；业务按钮只叠底板+图标，**禁止**把动态文案画进 PNG。
