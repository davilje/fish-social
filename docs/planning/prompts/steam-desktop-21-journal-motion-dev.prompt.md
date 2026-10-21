# STEAM-DESKTOP-21 开发提示词

状态：已确认。设计：`docs/design/ui/2026-10-21-cozy-journal-motion.md`。规格：`docs/planning/specs/Steam桌面端-21猫咪钓鱼手账动效.md`。

## 范围

Unity 桌面主界面动效。入口是 `CozyMotion`。按钮回弹、导航浮漂、切页、金币滚动、提示滑入、地图浮标、角落小猫、设置里的「减少动效」。

## 不要改

- Overlay 画布与 `OverlayPondLayoutBaker`
- `mobile/`、`server/`、`shared/`
- 不把状态改成已实现，等用户确认验收

## 完成标准

对照规格第 2 节。Debug 包需要重新编译 `Assembly-CSharp.dll` 后完全退出再打开 `Builds\Windows64-Debug\FishSocialDesktop.exe`。
