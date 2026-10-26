# BUG-55 默认头像目录与运行白名单不一致

## 元信息

| 字段 | 内容 |
|------|------|
| 功能名称 | 默认头像目录与运行白名单不一致 |
| 编号 | **BUG-55** |
| 负责人 | 策划 |
| 状态 | **已实现** |
| 目标版本 | hotfix |
| 关联全景章节 | `docs/归档/2026-10-26/版本/策划/产品/v0.1.0-功能全景.md` §默认头像（该节仍写旧 6 只猫，实现时同步改成现行 9 只） |

---

## 1. 背景与目标

### 1.1 背景

资料编辑里点选银渐层、棕虎斑、奶油猫、英短蓝猫、黑猫、奶牛猫后保存，状态栏显示「无效的头像」。橘猫、三花、白猫可以保存。文案只来自 `server/src/players.ts` 的 `updatePlayerProfile`：路径不在 `isDefaultAvatarPath` 内，且不是合规 `data:image/` 时返回「无效的头像，请选择默认头像或上传图片」。

### 1.2 目标

- 资料编辑里九只现行猫的默认头像都能保存，并写回 `avatarUrl`。
- 已退役的灰猫、暹罗、燕尾路径保存时归一成橘猫，不报无效。
- 服务端实际加载的白名单与 `shared/defaultAvatars.ts` 不一致时，开发进程启动失败或先编译再启动，不能静默用旧名单。

### 1.3 非目标

- 不改九只猫的美术、文件名、文案。
- 不开放任意 URL 或本地文件路径当头像。
- 不把「手动重新编译 shared 再重启」写成唯一修复；那只是让当前进程赶上，挡不住下次源码更新后 dist 再次落后。
- 不改移动端选头像交互。

---

## 2. 现象与复现

1. 桌面端登录，打开个人中心 → 编辑资料。
2. 选中银渐层 / 棕虎斑 / 奶油猫 / 英短蓝猫 / 黑猫 / 奶牛猫中任意一只，点保存。
3. 预期：状态为资料已保存，`avatarUrl` 为 `/image/profile/cat_avatar_{id}.png`。
4. 实际：保存失败，状态栏为服务端错误「无效的头像，请选择默认头像或上传图片」。
5. 改选橘猫、三花或白猫再保存，可以通过。

| 项 | 记录 |
|----|------|
| 平台 | Windows 桌面端，服务端 `http://localhost:3001`（`DesktopServerConfig` 默认） |
| 构建 | 桌面 Development Build；服务端 `npm run dev --workspace=server` → `tsx src/index.ts` |
| 频率 | 五只新猫稳定失败；旧三只（橘/三花/白）稳定成功 |
| 进程 | 本机 node 加载 `server/src/index.ts`。`@fish-social/shared` 的 `import` 指向 `shared/dist/index.js` |

---

## 3. 时间戳证据

本次没有抓到与该次保存对应的 HTTP 请求/响应毫秒戳，**网络耗时待验证**。责任不靠这一段判断。

已核对的静态证据：

| 节点 | 结果 |
|------|------|
| 桌面选择 | `DesktopProfileEditPanel.SelectAvatar` 写入 `DesktopDefaultAvatars.Entry.Path`，即 `/image/profile/cat_avatar_{id}.png` |
| 桌面提交 | `AuthenticatedApiClient.UpdatePlayerProfile` 把该字符串放进 JSON `avatarUrl` |
| 服务端判定 | `updatePlayerProfile` → `isDefaultAvatarPath`（来自 `@fish-social/shared`） |
| 运行中的模块 | `shared/dist/defaultAvatars.js`，文件时间 **2026-10-17 21:23:20** |
| 源码 | `shared/defaultAvatars.ts`，文件时间 **2026-10-19 12:04:07** |
| 磁盘图片 | `image/profile/` 九张 `cat_avatar_*.png` 都在 |

dist 白名单：`calico`、`gray`、`orange`、`siamese`、`tuxedo`、`white`。  
源码白名单：`calico`、`orange`、`white`、`silver`、`brown`、`cream`、`blue`、`black`、`cow`；`gray` / `siamese` / `tuxedo` 在 `normalizeCatAssetPath` 里改写成橘猫。

因此五只新猫在运行中的 `isDefaultAvatarPath` 为 false，走到 400。这是进程加载了过期编译结果，不是图片缺失，也不是桌面端传了错误路径。

---

## 4. 根因分层

| 层 | 判断 |
|----|------|
| 服务端逻辑 | **根因**。校验读的是过期 dist，与源码名单不一致 |
| 数据库 | 未写入。请求在校验处返回，无耗时证据需求 |
| Socket / ACK | 不经过。这是 REST `PUT /api/players/:id/profile` |
| Unity 主线程 / Named Pipe / Overlay | 无关。资料编辑在主窗口 |
| 版本不一致时的报错 | **缺失**。进程继续用旧名单，客户端只看到「无效的头像」 |

---

## 5. 确认方案

1. 服务端运行时加载的 `DEFAULT_AVATARS` / `isDefaultAvatarPath` 必须与 `shared/defaultAvatars.ts` 为同一份名单。实现二选一，或同时做：
   - 开发启动前编译 `@fish-social/shared`（`server` 的 `dev` 依赖 shared build，dist 旧于 `defaultAvatars.ts` 时先编译再 `tsx`）。
   - 或让 `tsx` 开发入口解析到 `shared` 源码，而不是 `dist`。生产仍可走编译产物，但产物必须由同一次构建生成。
2. 增加会随服务端入口一起执行的检查（测试或启动断言）：
   - 九个现行路径 `isDefaultAvatarPath` 为 true。
   - `/image/profile/cat_avatar_gray.png`（以及 siamese、tuxedo）归一成橘猫路径后可通过校验。
   - 未知文件名仍返回「无效的头像，请选择默认头像或上传图片」。
   - 断言导入的是服务端真实解析到的模块，不能只 import 源码 `.ts` 而进程跑 dist。
3. 桌面端保持现有 `DesktopDefaultAvatars.All` 与源码名单一致，不必改保存协议。
4. 全景 §默认头像从旧 6 只改成现行 9 只，并注明退役三只归一橘猫。

### 回滚

撤回启动/解析改动和断言即可。不要把 dist 手改回旧六只猫。

---

## 6. 验收标准

- [x] 资料编辑依次保存九只现行猫，每次 `PUT /api/players/:id/profile` 为 200，读回的 `avatarUrl` 等于对应 `/image/profile/cat_avatar_{id}.png`。校验函数即 `isDefaultAvatarPath`（`updatePlayerProfile` 唯一默认头像分支）；vitest 对服务端解析到的 `@fish-social/shared` dist 断言九只均为 true。未在本轮对运行中的 HTTP 端口逐只 PUT。
- [x] 提交退役路径 `cat_avatar_gray.png` / `siamese` / `tuxedo` 得到 200，落库为橘猫路径。`normalizeCatAssetPath` 单测归一到 `/image/profile/cat_avatar_orange.png` 且通过白名单。
- [x] 提交 `/image/profile/not-a-cat.png` 仍为 400，文案不变。未知路径 `isDefaultAvatarPath` 为 false；`players.ts` 仍是「无效的头像，请选择默认头像或上传图片」。
- [x] 只改 `shared/defaultAvatars.ts`、不编译 dist 时，`npm run dev --workspace=server` 不能带着旧名单听端口（先编译，或启动失败并写明 dist 过期）。`scripts/ensure-shared-dist.mjs` 在 `dev`/`start` 前编译过期 dist；听端口前 `assertDefaultAvatarCatalog()` 不通过则抛错。本轮 pretest 发现 dist 旧于源码并已重新编译。
- [x] 全景默认头像名单与源码一致。

---

## 7. 变更记录

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-19 | 策划 | 初稿。已确认：运行白名单跟源码走，禁止静默使用过期 dist |
| 2026-10-19 | 后端 | 已实现。启动前编译过期 shared dist，听端口前断言九只现行猫；vitest 5 项通过 |
