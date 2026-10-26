# Bug：重新进游戏 Overlay 看不到已保存的猫咪形象

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | 进游戏即用已保存形象，不必再去个人主页点一次保存 |
| 编号 | **BUG-24** |
| 类型 | **Bug修复** |
| 负责人 | Unity 桌面 / Overlay；必要时服务端同步塘内 `avatarUrl` |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 优先级 | P0 |
| 设计时间 | **2026-09-01** |
| 完成时间 | **2026-09-01** |
| 上位需求 | `STEAM-DESKTOP-08E`、`STEAM-DESKTOP-ART-03` |
| 关联全景 | 个人资料 `avatarUrl`、塘内宠物 `petId` |

---

## 1. 现象与复现

### 1.1 操作与预期

1. 个人中心选一只默认猫（如暹罗 / 灰猫）并 **保存**，Overlay 上自己是这只猫。
2. **完全退出**客户端后再进游戏、进塘。
3. **预期**：Overlay 自己仍是上次保存的猫，无需再操作资料。
4. **实际**：Overlay 落到按 `playerId` 哈希的默认猫（或橘猫回退）；必须再打开个人主页 **再保存一次** 才恢复。

稳定复现：是（每次冷启动）。平台：Steam Unity + 原生 Overlay。

### 1.2 链路（待开发用日志核对，禁止未取证就定「服务器慢」）

```text
登录 / 拉 profile
  → 进塘 join / snapshot（CurrentUser.avatarUrl）
  → OverlayPondStateBuilder.ownPetId
  → IPC ownPetId / users[].petId
  → Overlay OverlayFrameCache pets/<petId>/
```

已知代码事实（根因分层，部分待验证）：

| 层 | 现状 | 判断 |
|----|------|------|
| 服务端资料 | `PUT /api/players/:id/profile` 会把 `avatarUrl` 写入 `players` | 保存后库里应有形象 |
| 进塘用户 | `createHumanPondUser` 用 `resolvePlayerAvatar(playerId)` 抄一份 | 若 join 时 `getPlayer` 已有 url，snapshot 应带上 |
| 改资料后塘内 | `updatePlayerProfile` **不**回写已在塘的 `user.avatarUrl` | 会话内只靠客户端缓存「看起来好了」 |
| Unity 缓存 | `DesktopProfileCache.Latest` 只在打开/保存个人中心时赋值 | **冷启动未拉 profile 则缓存空** |
| Overlay 解析 | `ResolvePetId(user, profile)`：**优先** `CurrentUser.avatarUrl`；空则 cache；再空则 **hash playerId** | cache 空 + 塘内 url 空/未反序列化 → 错猫 |

「再保存一次就好」：保存把 `DesktopProfileCache.Latest` 写上，Overlay 下一帧用 cache 的 url。**不是**形象没落库，是冷启动没把库里的形象喂给 Overlay。

---

## 2. 目标

- 冷启动进塘后，Overlay **自己的猫** = 个人资料已保存的默认头像对应猫种（`orange` / `calico` / `gray` / `siamese` / `tuxedo` / `white`）。
- 不必打开个人中心、不必再点保存。
- 本局内改形象并保存后，Overlay 仍立即更新（现网保存后能更新的路径要保住）。

### 2.1 非目标

- 不新增猫种、不改 64×64 规格。
- 不强制自定义 `data:image` 上传头像映射到新猫种（解析不到则沿用六套之一，须在 spec 实现里写清回退，且回退稳定、不依赖再保存）。
- 不改他人/bot 随机形象算法（bot 仍 `pickBotAvatar`）。

---

## 3. 功能范围

| # | 功能点 | 优先级 | 说明 |
|---|--------|--------|------|
| 1 | 启动拉资料 | P0 | 登录成功后立刻 `GetPlayerProfile`（或已有 bootstrap 接口）写入 `DesktopProfileCache.Latest`，早于首帧 Overlay state |
| 2 | Overlay 解析 | P0 | `ownPetId`：profile.avatarUrl 与 pond user.avatarUrl 都能用；**空才** hash。禁止「塘内空字符串盖掉已有 cache」 |
| 3 | 塘内同步 | P0 | `updatePlayerProfile` 成功后，该 `playerId` 在各塘的 `user.avatarUrl` 立刻更新并 dirty，snapshot 带新 url |
| 4 | 进塘抄资料 | P0 | `createHumanPondUser` / 断线恢复已有 `resolvePlayerAvatar`；确认 snapshot DTO / Unity 反序列化未丢 `avatarUrl` |

### 3.1 规则

- 权威：`players.avatar_url`（默认路径 `/image/profile/cat_avatar_*.png`）。
- Overlay `petId` = `DesktopDefaultAvatars.ResolvePetId(avatarUrl, playerId)`，与主窗口同一套。
- 未选过形象：hash 默认猫可接受，且与「选过并保存」可区分。

---

## 4. 技术影响

### 4.1 API / Socket

| 类型 | 名称 | 说明 |
|------|------|------|
| REST | `GET /api/players/:id/profile`（或现用 GetPlayerProfile） | 启动必拉一次 |
| REST | `PUT` profile | 成功后服务端同步塘内 avatar |
| Socket | `pond_snapshot` / user 增量 | 须含 `avatarUrl` |

无新接口也可做完。

### 4.2 涉及文件（预估）

- `fish-social-unity/.../DesktopAppBootstrap.cs`（或登录完成钩子）
- `DesktopProfileCache` / `DesktopProfileEditPanel.cs`（对照何时写 cache）
- `OverlayPondStateBuilder.cs` `ResolvePetId`
- `server/src/players.ts` + `pondUserManager.ts`（改资料回写塘内用户）
- 不改 `mobile/` 业务除非同一 bug 在手机复现且本单顺带对齐

---

## 5. 验收标准

- [x] 保存形象 → 杀进程再进塘：Overlay 自己是保存的那只猫，不打开个人中心
- [x] 从未保存形象：仍有稳定默认猫，不崩
- [x] 本局改形象保存：Overlay 在数秒内换成新猫，无需重进塘
- [x] 他人/bot 形象不因此全变成自己的猫
- [x] 不改 `shared/` 钓鱼公式
- [x] 用户验收通过（2026-09-01）

---

## 6. 风险与依赖

| 风险 | 缓解 |
|------|------|
| 启动竞态：Overlay 首包早于 profile | 首包可用 hash，profile 到达后强制刷新 ownPetId；或以进塘前等 profile |
| 自定义上传头像无 petId | 回退六套之一，写进实现注释；本单不新做上传映射表 |

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-09-01 | 策划 | 已确认：冷启动恢复已保存形象，不必再保存 |
| 2026-09-01 | Overlay | 已实现：登录预拉 profile；塘内 avatar 回写；空 url 不盖 cache |
| 2026-09-01 | 策划 | 用户验收通过 |
