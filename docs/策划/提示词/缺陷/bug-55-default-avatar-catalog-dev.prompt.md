# 开发提示词：默认头像白名单跟源码（BUG-55）

你负责 Fish Social **shared + server 启动链**。只做 **BUG-55**。  
桌面端已经提交 `/image/profile/cat_avatar_{id}.png`，不要改保存协议来绕过校验。

## 必读

1. `docs/策划/规格/缺陷/BUG修复-默认头像目录与运行白名单不一致.md`（**已确认** / **BUG-55**）
2. `shared/defaultAvatars.ts`（现行 9 只 + 退役三只归一橘猫）
3. `shared/dist/defaultAvatars.js`（2026-10-17 的旧 6 只，含 gray/siamese/tuxedo）
4. `server/src/players.ts` 的 `updatePlayerProfile`；`server/package.json` 的 `dev` 目前是 `tsx src/index.ts`，import 走 `shared/package.json` 的 `dist`

## 方案要点

1. 服务端进程实际调用的 `isDefaultAvatarPath` 必须与 `shared/defaultAvatars.ts` 一致。开发启动在 dist 旧于源码时先编译 shared，或让 tsx 开发入口直接解析源码。生产用编译产物，但必须是同一次构建。
2. 断言或测试要 import **服务端真实解析到的那个模块**。九个现行路径为 true；gray/siamese/tuxedo 归一橘猫后可通过；未知文件名仍 400，文案不变。
3. 不改九只猫的文件名和美术。不接受任意 URL。
4. 全景 `docs/归档/2026-10-26/版本/策划/产品/v0.1.0-功能全景.md` 默认头像从旧 6 只改成现行 9 只。

## 完成后

- [ ] spec §6 可勾选验收都有结果
- [ ] spec 状态改为「已实现」，`CHANGELOG.md` 记一条
- [ ] 计划表经 `npm run planning:master-xlsx` 把 BUG-55 写成已实现并填完成日期

## 派发

```text
@docs/策划/提示词/缺陷/bug-55-default-avatar-catalog-dev.prompt.md 按此实现 BUG-55
```

建议角色：`@backend-dev`
