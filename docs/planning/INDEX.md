# 策划文档索引

## 版本与代码对照

| 策划版本 | App 版本 (`app.json`) | 全景文档 | 状态 |
|----------|----------------------|----------|------|
| 0.1.0 | 0.1.0 | [v0.1.0-功能全景.md](./product/v0.1.0-功能全景.md) | **当前基线** |

## 总表

| 文档 | 说明 | 更新 |
|------|------|------|
| [项目开发需求计划表.xlsx](./项目开发需求计划表.xlsx) | **主总表**：开发计划 + 数据平台 + Bug修复 + 统计摘要 | `npm run planning:master-xlsx` |
| [发布说明](./releases/) | 各版本发布说明 HTML | `npm run planning:release`（含 xlsx 更新） |

## 文档清单

### 产品全景

| 文档 | 说明 |
|------|------|
| [v0.1.0-功能全景.md](./product/v0.1.0-功能全景.md) | v1 + v2 + v2.1 + v2.2 完整功能：地图、鱼塘、生态、社交、机器人、管理工具 |
| [钓鱼世界与鱼塘场景优化策略.md](./product/钓鱼世界与鱼塘场景优化策略.md) | **参考** REF-SCENE-1：批判与分档改法（非开发需求） |
| [Unity移植工程路径蓝图.md](./product/Unity移植工程路径蓝图.md) | **参考** REF-UNITY-1：架构切线与 Phase 0–5（非开发需求） |

### 工作流与模板

| 文档 | 说明 |
|------|------|
| [WORKFLOW.md](./WORKFLOW.md) | 策划创建、评审、发布流程 |
| [功能规格模板.md](./templates/功能规格模板.md) | 新功能 PRD 模板 |
| [版本变更记录模板.md](./templates/版本变更记录模板.md) | 单版本策划变更说明模板 |
| [CHANGELOG.md](./CHANGELOG.md) | 策划目录变更历史 |

### 专项策划（`specs/`）

钓鱼 v2 分阶段文档索引见 [specs/README.md](./specs/README.md)，开发交接见 [钓鱼系统v2-开发交接.md](./specs/钓鱼系统v2-开发交接.md)。

| 文档 | 状态 | 说明 |
|------|------|------|
| [钓鱼玩法扩展-v0.7-Epic.md](./specs/钓鱼玩法扩展-v0.7-Epic.md) | **已确认** | **v0.7 入口**：R1+R2 已交付；R3 候选待开题 |
| [鱼塘分级与玩家成长.md](./specs/鱼塘分级与玩家成长.md) | **已实现** | FEAT-PROG-01：七类塘、双熟练度、每2h扣费、新手完成态、卖价、数值表 |
| [Steam桌面端-新手引导本地教学关.md](./specs/Steam桌面端-新手引导本地教学关.md) | **已实现** | STEAM-DESKTOP-11：Overlay 本地教学关（5s 必上钩） |
| [钓具与鱼饵配置.md](./specs/钓具与鱼饵配置.md) | **已实现** | FEAT-GEAR-01：竿弱加成与断竿买新、饵按次扣金、船仅商店 |
| [Steam桌面端-08A2世界地图分区与进塘扣费.md](./specs/Steam桌面端-08A2世界地图分区与进塘扣费.md) | **已实现** | STEAM-DESKTOP-08A2：六区锁态、巨物暂闭、进塘扣费确认 |
| [禁止钓鱼塘巡警事件.md](./specs/禁止钓鱼塘巡警事件.md) | **已实现** | FEAT-RISK-01：巡警气泡 10s、免罚短禁/超时罚款日禁 |
| [钓位点位线索文字泡.md](./specs/钓位点位线索文字泡.md) | **已实现** | FEAT-SPOT-01：坐席后聊天泡；表驱动随机；habitat/activity |
| [钓位标签与线索库-v2.md](./specs/钓位标签与线索库-v2.md) | **已实现** | FEAT-SPOT-02：22 类标签；420 点位；标签过滤线索；体长 XP 0.85 |
| [钓位标签地形微调.md](./specs/钓位标签地形微调.md) | **已确认** | FEAT-SPOT-03：pond_spot_tags 按 Tile 地形人工微调 |
| [线索文案策划审校.md](./specs/线索文案策划审校.md) | **已确认** | FEAT-SPOT-04：spot_clue_texts 扩写；habitat/activity 禁区 |
| [钓位鱼情联动线索.md](./specs/钓位鱼情联动线索.md) | **已确认** | FEAT-SPOT-05：钓位鱼情 tier → activitySignal 联动 |
| [Steam桌面端-玩法Debug菜单.md](./specs/Steam桌面端-玩法Debug菜单.md) | **已实现** | STEAM-DESKTOP-12：开发版玩法 Debug 菜单（升级/出警/+2h 等） |
| [回鱼机制.md](./specs/回鱼机制.md) | **已实现** | FEAT-RETURN-01：回塘增重、准入、金≈卖价×0.7 |
| [双价塘与自动回鱼.md](./specs/双价塘与自动回鱼.md) | **已实现** | FEAT-RETURN-02：进塘双价；达标自动回鱼 |
| [BUG修复-日额度满后仍可开钓.md](./specs/BUG修复-日额度满后仍可开钓.md) | **已实现** | BUG-23：满 8h/扣满 4 次不可开钓，可落座 |
| [打窝机制.md](./specs/打窝机制.md) | **已实现** | FEAT-GROUND-01：Overlay 并列循环、50 层非线性、增鱼尺寸 |
| [钓鱼相册与成就.md](./specs/钓鱼相册与成就.md) | **已实现** | FEAT-ALBUM-01：个人中心大改、相册墙、成就 |
| [v0.2.4-开发交接.md](./specs/v0.2.4-开发交接.md) | **已确认** | Debug 合并面板 + 商店 §8.2 |

| [Unity移植-分阶段需求清单.md](./specs/Unity移植-分阶段需求清单.md) | **已定稿** | **UNITY-EPIC**：Unity 客户端 P0～P5 产品规划、阶段出口与验收 |
| [数值重构v2-成长咬钩与文案.md](./specs/数值重构v2-成长咬钩与文案.md) | **已确认**（§6.3 待开发） | 14日成长、乘法钓点、30s、§6.3 Debug |
| [UI体验修复-社交商店图鉴.md](./specs/UI体验修复-社交商店图鉴.md) | **已确认**（§8 补丁待开发） | 纪念照、商店/图鉴、Debug、好友动态 + §8 补丁 |
| [A0-数值重构.md](./specs/A0-数值重构.md) | **已确认** | P0：成长曲线、指数咬钩、迁移 |
| [A1-飘字广播.md](./specs/A1-飘字广播.md) | **已确认** | P0：咬钩/脱钩飘字（依赖 A0） |
| [A2-Debug面板.md](./specs/A2-Debug面板.md) | **已确认** | P0：Admin 钓鱼概率（依赖 A0） |
| [B0-商店基础.md](./specs/B0-商店基础.md) | **已确认** | P1：鱼饵/渔具商店 |
| [B1-鱼饵偏好.md](./specs/B1-鱼饵偏好.md) | **已确认** | P1：饵 × diet 偏好 |
| [C-调优与状态机.md](./specs/C-调优与状态机.md) | **已确认** | P2：调优/C6 状态机等按需 |
| [状态机需求描述.md](./specs/状态机需求描述.md) | 已定稿 | C6 引用，A/B 期不实施 |
| [钓鱼系统v2-生态与玩法重构.md](./specs/钓鱼系统v2-生态与玩法重构.md) | superseded | 已拆为 A0~C 子文档 |
| [他人主页优化.md](./specs/他人主页优化.md) | 已确认（待验收） | 他人资料展示简介/收藏品/动态 |
| [BUG修复-资料与鱼塘UI.md](./specs/BUG修复-资料与鱼塘UI.md) | **已实现** | 弹窗闪屏、首页头像、编辑按钮、鱼塘侧栏合并 |
| [服务器维护-端口占用.md](./specs/服务器维护-端口占用.md) | **已确认** | 端口占用排查、npm 维护脚本、优雅退出 |
| [BUG修复-四项体验问题.md](./specs/BUG修复-四项体验问题.md) | 已 superseded | 部分条目已被上表文档替代 |
| [BUG修复-进塘首帧状态与演示降级.md](./specs/BUG修复-进塘首帧状态与演示降级.md) | **已实现** | BUG-18：进塘旧态 / 静默 DEMO / 收杆额度 ack |
| [BUG修复-每日额度单一口径重构.md](./specs/BUG修复-每日额度单一口径重构.md) | **已实现** | BUG-19：base/session 拆分；checkpoint 不前移锚点 |
| [BUG修复-进塘与钓鱼剩余展示回归.md](./specs/BUG修复-进塘与钓鱼剩余展示回归.md) | **已实现** | BUG-20：钓鱼中剩余走动；未选钓点防满额 8h |
| [Steam桌面Overlay场景布局管线.md](./specs/Steam桌面Overlay场景布局管线.md) | **已确认** | STEAM-DESKTOP-ART-02：Unity Canvas Prefab → Overlay 像素布局（塘内场景，不含 HUD） |
| [Steam桌面Overlay分塘底图与HUD同步.md](./specs/Steam桌面Overlay分塘底图与HUD同步.md) | **已确认** | STEAM-DESKTOP-ART-03：分塘底图、猫咪六姿势序列帧、Unity HUD Prefab 同步 Overlay |
| [Steam桌面端Web功能对齐设计.md](./specs/Steam桌面端Web功能对齐设计.md) | **已实现** | STEAM-DESKTOP-07E：主窗口页签；菜单唤起时主窗口高于 Overlay |
| [Steam桌面端-09AOverlay玩家右键菜单.md](./specs/Steam桌面端-09AOverlay玩家右键菜单.md) | **已实现** | STEAM-DESKTOP-09A：Overlay 单玩家右键社交菜单 |
| [Steam桌面端-09BOverlay悬停状态与钓鱼时长.md](./specs/Steam桌面端-09BOverlay悬停状态与钓鱼时长.md) | **已实现** | STEAM-DESKTOP-09B：悬停仅时长 Tooltip + IPC 字段 |
| [Steam桌面端-09COverlay鱼塘聊天气泡与输入.md](./specs/Steam桌面端-09COverlay鱼塘聊天气泡与输入.md) | **已实现** | STEAM-DESKTOP-09C：Overlay 公屏聊天气泡+输入 |
| [Steam桌面端-09DOverlay布局与角色表现优化.md](./specs/Steam桌面端-09DOverlay布局与角色表现优化.md) | **已实现** | STEAM-DESKTOP-09D：960×560、64px、默认状态/圆环、左上收纳菜单 |

## 数据归档

专项报告 xlsx（三层数据体系、架构修复、埋点清单、切页修复）已合并至总表，原文件移至 `reports_archive/` 存档。

| 文档 | 说明 |
|------|------|
| [挂机断线排查-v0.4.3.md](../reports/挂机断线排查-v0.4.3.md) | 挂机断线排查 SOP（独立文档保留） |

## 源码快速索引

| 模块 | 路径 |
|------|------|
| 共享常量与鱼种 | `shared/` |
| 服务端 | `server/src/` |
| 客户端 | `mobile/app/`、`mobile/components/` |
| 项目说明 | 根目录 `README.md` |
