# Unity 小屋动画原生检查

2026-10-24，Unity 2021.3.29f1c1。截图来自已保存的正式 `DesktopShell.prefab`，使用离线橘猫配置和 1672 × 941 画布。

| 截图 | 内容 |
| --- | --- |
| [00-idle.png](00-idle.png) | 原始待机 |
| [01-roll.png](01-roll.png) | 点击角色后打滚 |
| [02-walk.png](02-walk.png) | 点击地面后行走 |
| [03-pour.png](03-pour.png) | 点击食盆后倒粮 |
| [04-eat.png](04-eat.png) | 前往食盆后进食，满粮 |
| [05-eat-second-loop.png](05-eat-second-loop.png) | 第二轮进食、粮量减少 |
| [06-eat-third-loop.png](06-eat-third-loop.png) | 第三轮进食、粮量继续减少 |
| [07-returned.png](07-returned.png) | 结束喂食、返回中央活动范围 |

技术结果：`editmode-home.xml` 55/55，`editmode-regression.xml` 157/157，保存与原生渲染检查见 `verification.txt`。重复生成入口为 Unity 菜单 `Fish Social → Home Room → Save and verify home animations`。

这些结果验证技术接入，不替代用户美术验收；其他八猫新动作未制作，大肥鱼不在本轮范围。没有进行 Windows Player 构建、Steam 登录或服务端联调。
