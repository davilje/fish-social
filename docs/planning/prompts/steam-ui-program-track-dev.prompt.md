# Steam UI 程序实现任务线

请作为 Unity 程序 Agent，按 `docs/planning/specs/Steam桌面宠物UI需求拆分.md` 实现以下程序 UI 项：

- `STEAM-UI-PROG-01`：桌面宠物主视图状态绑定与视图切换
- `STEAM-UI-PROG-02`：鱼塘场景、HUD 和 pond 状态绑定
- `STEAM-UI-PROG-03`：同塘玩家宠物对象生命周期与 Socket 状态刷新
- `STEAM-UI-PROG-04`：产品区域右键菜单与命令分发
- `STEAM-UI-PROG-05`：统一弹窗框架、层级和不离塘规则
- `STEAM-UI-PROG-06`：好友/聊天、背包、图鉴、设置弹窗交互
- `STEAM-UI-PROG-07`：通知、托盘、最小化恢复和重连反馈

程序 UI 负责：

- Unity Prefab/场景挂载
- 输入、状态机和数据绑定
- API/Socket 事件接入
- 弹窗生命周期
- 错误、加载和空数据状态

不负责：

- 猫咪、池塘、图标、字体和菜单皮肤等美术资源生产
- 修改 Node 鱼塘、生态、库存和 Steam 权威逻辑
- 修改 `mobile/`

美术资源通过资源清单和 Prefab 接口接入；没有正式资源时使用明确占位，不伪造业务数据。每个 `STEAM-UI-PROG-*` 单独提交和验收。
