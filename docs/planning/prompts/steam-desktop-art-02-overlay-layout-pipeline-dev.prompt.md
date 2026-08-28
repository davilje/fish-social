# 开发提示词：Overlay 场景布局管线（STEAM-DESKTOP-ART-02）

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**（可协同美术搭 Prefab）。只实现本编号；不要扩到 07E 弹窗、ART-03 HUD 或服务端玩法。

## 必读

1. `docs/planning/specs/Steam桌面Overlay场景布局管线.md`（**已确认** / **STEAM-DESKTOP-ART-02**）
2. `docs/planning/specs/Steam原生桌面宠物Overlay.md`（07G：WPF Overlay，不加载 Unity）
3. `docs/planning/specs/Steam桌面宠物UI需求拆分.md`（ART-01 换图；ART-03 管 HUD；本需求只做塘内布局表）
4. `docs/planning/specs/Steam桌面Overlay分塘底图与HUD同步.md`（ART-03：分塘底图 / 序列帧 / HUD；与本 JSON 分文件）
5. `desktop-overlay/PondScenePresenter.cs`（现有 `MapToScene` 自动缩放）
6. `desktop-overlay/README.md`

## 顺序

1. 约定 **960×560** Canvas Prefab：钓位挂现有 `spotId`；锚点写进导出规则（建议猫脚底中心）。
2. 编辑器菜单从 Prefab 导出 Overlay 坐标系 JSON（左上原点、Y 向下）到 `OverlayResources/layouts/<pondId>.json`；失败要报错，禁止半份文件。
3. Overlay 按 `pondId` 读布局 JSON + 同名 PNG；静态层（背景装饰、岸）按矩形摆放。
4. 有布局表的塘：猫和钓位只用表内像素，**停用该塘 `MapToScene`**。Pipe 仍只传占用与 `petVisualState`，不传图、不传 Prefab。
5. 无 JSON 的塘回退现有自动缩放。构建把 JSON/PNG 拷到 Overlay 旁。
6. 自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：须 **用户确认** 后再把 spec 改为已实现并刷新计划表。不要自行把状态改成已实现。

- [ ] 有布局表的塘：猫站在 Prefab 导出的钓位像素上，不再被 `MapToScene` 整体缩放
- [ ] 无 JSON 的塘仍可用现有自动缩放，不崩
- [ ] Prefab 钓位 `spotId` 均为服务端已有 id；导出失败不写半份文件
- [ ] 不包含 HUD 控件；不与 `hud/overlay-hud.json` 混文件

## 禁止

- 第二 Unity Player、Overlay 内 uGUI、IPC 传图/传 Prefab
- 修改 `mobile/`、`server/`、`shared/` 业务与钓位权威
- 重写 07A～07E 登录、导航、弹窗、会话
- 把服务端 tile `x/y` 当作有布局表时的 Overlay 像素真相
- 把菜单 / 钓鱼按钮 / 聊天条放进本 Prefab 或本 JSON（那是 ART-03）

## 派发

```text
@docs/planning/prompts/steam-desktop-art-02-overlay-layout-pipeline-dev.prompt.md 按此实现 STEAM-DESKTOP-ART-02
```
