# Steam 桌面 Overlay：宠物标签、右键菜单与登录窗

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | Overlay 宠物名/状态标签、右键菜单修复、登录小窗可见 |
| 编号 | **STEAM-DESKTOP-13B** |
| 类型 | Bug修复 |
| 状态 | **已实现** |
| 目标版本 | v1.0-steam-desktop / hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-08-29** |
| 完成时间 | **2026-08-29** |
| 上位需求 | `STEAM-DESKTOP-09A`、`STEAM-DESKTOP-09D`、`STEAM-DESKTOP-07A` |

---

## 1. 现象与复现

| # | 现象 | 预期 |
|---|------|------|
| 1 | 宠物名/状态无底、waiting 文案含糊 | 状态在头上、名字在脚下，有底；waiting 显示「钓鱼中」 |
| 2 | 右键其他玩家猫弹出场景产品菜单 | 弹出 09A 社交菜单（资料/好友/私聊/点赞） |
| 3 | 改 HUD Prefab 后登录小窗消失 | 主窗口登录 480×320 仍可见 |

### 根因分层

| 问题 | 层 | 根因 |
|------|----|------|
| 右键 | Overlay UI | 场景 `Border.ContextMenu` 先于宠物菜单；需 `PreviewMouseRightButtonDown` + `ContextMenuOpening` 取消 |
| 登录窗 | Unity Prefab | `DesktopShell` 根 `localScale=(0,0,0)`，整棵 Canvas 缩没 |
| 标签 | Overlay 表现 | 未做徽章底与 waiting 文案映射 |

---

## 2. 非目标

- 不改 09A 对自己猫仍走场景产品菜单的规则。
- 不重做登录流程与鉴权。

---

## 3. 验收标准

- [x] 状态徽章在头顶，名字在脚下；waiting → 「钓鱼中」
- [x] 右键其他玩家 → 社交菜单；右键自己 → 场景菜单
- [x] `DesktopShell` 根 scale 为 (1,1,1)；加载时 scale=0 自动恢复
- [x] 用户验收通过（2026-08-29）

---

## 4. 涉及文件

- `desktop-overlay/OverlayPetActor.cs`、`PondScenePresenter.cs`、`MainWindow.xaml.cs`
- `fish-social-unity/.../DesktopShell.prefab`、`DesktopShellUi.cs`、`DesktopPrefabBaker.cs`

---

## 5. 变更记录

| 日期 | 作者 | 说明 |
|------|------|------|
| 2026-08-29 | 策划 | 验收通过 → **已实现** |
