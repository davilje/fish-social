# STEAM-DESKTOP-ART-02：Overlay 场景布局管线

你是 Fish Social 的 **Unity 桌面 / Overlay 工程师**（可协同美术搭 Prefab）。只实现本编号；不要扩到 07E 弹窗或服务端玩法。

## 必读

1. `docs/planning/specs/Steam桌面Overlay场景布局管线.md`（**已确认** / **STEAM-DESKTOP-ART-02**）
2. `docs/planning/specs/Steam原生桌面宠物Overlay.md`（07G：WPF Overlay，不加载 Unity）
3. `docs/planning/specs/Steam桌面宠物UI需求拆分.md`（ART-01 换图；本需求是布局表）
4. `desktop-overlay/PondScenePresenter.cs`（现有 `MapToScene` 自动缩放）
5. `desktop-overlay/README.md`

## 顺序

1. 约定 `960×480` Canvas Prefab：钓位挂现有 `spotId`；锚点写进导出规则。
2. 编辑器菜单从 Prefab 导出 Overlay 坐标系 JSON（左上原点、Y 向下）；失败要报错，禁止半份文件。
3. Overlay 按 `pondId` 读 `OverlayResources/layouts/<pondId>.json` + 同名 PNG；静态层按矩形摆放。
4. 有布局表的塘：猫和钓位只用表内像素，**停用该塘 `MapToScene`**。Pipe 仍只传占用与 `petVisualState`，不传图、不传 Prefab。
5. 无 JSON 的塘回退现有自动缩放。构建把 JSON/PNG 拷到 Overlay 旁。
6. 自检 spec §5。

## 验收

对照 spec §验收。完成后按 Skill `planning-progress-sync` Checklist B：须 **用户确认** 后再把 spec 改为已实现并刷新计划表。不要自行把状态改成已实现。

## 禁止

- 第二 Unity Player、Overlay 内 uGUI、IPC 传图/传 Prefab
- 修改 `mobile/`、`server/`、`shared/` 业务与钓位权威
- 重写 07A～07E 登录、导航、弹窗、会话
- 把服务端 tile `x/y` 当作有布局表时的 Overlay 像素真相
