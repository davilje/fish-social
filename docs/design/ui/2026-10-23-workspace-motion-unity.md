# 新版工作区动效同步到 Unity

日期：2026-10-23。状态：已实现，Windows/Steam 实机验收仍待完成。

基准为 `prototype/journal-workspace/` 的工作区交互，以及它共用的 `cozy-journal/app.js`、`pages.js`。正式入口继续使用 DesktopShellUi / PanelRouter。

## 覆盖范围

| 交互 | 网页基准 | Unity 对应实现 |
|---|---|---|
| 从首页打开页面 | 纸页 200ms、上移 16px、OutCubic；内容在纸页升起后揭开 | CozyMotion / CozyJournalFlipPose。遮住整块内容，包括普通文字、工作区卡片和索引；异步追加的内容也保持遮住。只启动一次内容入场 |
| 切换页面、关闭页面 | 旧内容 120ms 左退 24px，新内容 200ms 从右进入；关闭 140ms 下沉 12px | 共用路由保留原参数；取消与快速切页恢复可见性和点击状态 |
| 活页环、索引、卡片 | 环扣 80ms、索引 200ms 错峰 30ms、前八张卡 260ms 错峰 40ms | 保留共享实现，在纸页升起后统一开始；鱼塘仍按首次进入视口播放 |
| 左侧页签 | 固定纸张；旧内容 100ms 淡出，选中页签向左伸出 16px | 保留 CozyIndexMotion 和各页签控制器，纳入回归 |
| 排行榜、动态、图鉴与收藏详情选择 | 照片原地从 0.9 倍／0.3 透明度回到正常，220ms OutBack | 移除这些页面误套用的 24px 横移和 260ms 时长；商店、背包、世界地图仍按其网页的 260ms 横移动效 |
| 异步加载排行榜和动态 | 新卡片出现，已显示的标题和侧栏不重新消失 | ContentReady 更新初始数量，避免数据到达后重新启动整页入口动画 |
| 工作区按钮 | 悬停上移 2px、按下下移 2px；160ms CSS ease | CozyWorkspaceInteraction；取消通用按钮的压扁回弹叠加，固定点击几何 |
| 选择头像 | 仅头像图片旋转 −5°、放大 1.1 倍，200ms CSS ease | 图片网格变形；选择头像只更新选中态，不销毁并重建整组头像 |
| 展示位、鱼获选择 | 保留选中边框与填色，选择不触发整卡压扁或重播入口 | 工作区 Choice 反馈，分享缩略图与编辑选择器共用 |
| 分享、离塘结算、鱼获选择器 | 纸张 200ms 上移 16px并淡入，遮罩先出现且固定 | CozyWorkspaceDialogMotion；遮罩独立于纸张透明度，解决第 0 帧被透明父级一起裁掉的问题；关闭时恢复姿态 |
| 减少动效 | 保留不超过 120ms 淡入淡出，移除位移／缩放／环扣回弹和错峰 | 页面、弹窗、索引与卡片入口保留短淡入，按钮和头像不做空间变换 |

修改均通过正式共享组件和 presenter 接入；已有预制体加载后生效。没有切换到 JournalV2，也没有重新生成用户已认可的布局资源。

## 验证与查看

- [Unity 实际动效分帧](../../art/cozy-journal/production/workspace-motion/index.html)：个人资料、编辑资料、选择鱼获、发布鱼获、离塘结算，五个场景共 30 张帧图，覆盖 0–650ms。
- 专项回归 **111/111 通过**，包含 13 个正式页面预制体的内容遮挡、快速切页、取消、异步数据返回、短淡入、按钮和头像反馈、弹窗关闭与遮罩位置。
- 本轮全套 EditMode 回归：**498 通过、4 失败**。四项均为此前已记录的断言：选塘与世界地图要求旧费用文案、图鉴要求累计捕获文案、排行榜要求旧名次 PNG。此前排行榜照片动效的失败已通过按网页修正 220ms 原地缩放而解决。
- 全套之后补充了弹窗遮罩第 0 帧与减少动效的专项复验；最终专项结果和全套失败详情见 [验证记录](../../art/cozy-journal/production/workspace-motion/validation.json)。
- 分帧是编辑器实际渲染；不代替真实账号网络波动、Windows/Steam 或 DPI 实机验收。

重新导出：运行 `FishSocial.Desktop.Editor.CozyRedesignPreview.RenderWorkspaceMotion`，然后 `node scripts/art/build-cozy-workspace-motion-preview.cjs`。前者保留用户的减少动效设置；生成器只写预览输出。
