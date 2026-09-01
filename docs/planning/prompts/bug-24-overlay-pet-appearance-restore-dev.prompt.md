# 开发提示词：Overlay 形象冷启动恢复（BUG-24）

你是 Fish Social 的 **Unity 桌面 / Overlay**工程师，必要时改服务端塘内 `avatarUrl`。按规格修，勿扩需求。

## 必读

1. `docs/planning/specs/BUG修复-Overlay形象进游戏不恢复.md`（**已确认** / **BUG-24**）
2. `OverlayPondStateBuilder.ResolvePetId`：塘内 url → `DesktopProfileCache` → hash `playerId`
3. 预估：`DesktopAppBootstrap` 登录后拉 profile；`players.ts` / `pondUserManager.ts` 改资料回写塘内用户

## 顺序

1. 登录成功即写入 `DesktopProfileCache.Latest`，早于或紧跟首帧 Overlay。
2. `ownPetId` 不得被空的塘内 `avatarUrl` 盖掉已有 cache。
3. `updatePlayerProfile` 后同步各塘该玩家 `avatarUrl` 并 dirty。
4. 冷启动杀进程验收：不必打开个人中心。

## 验收

对照 spec §验收；完成后按 Skill `planning-progress-sync`：
spec→**已实现** + `build-master-plan-xlsx.py` 完成时间 + `npm run planning:master-xlsx`。

- [x] 再进游戏 Overlay 即是已保存的猫
- [x] spec → **已实现** + `npm run planning:master-xlsx`

## 派发

```text
@docs/planning/prompts/bug-24-overlay-pet-appearance-restore-dev.prompt.md 按此实现 BUG-24
```
