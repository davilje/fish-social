# BUG 修复：演示模式误触发与自动打开浏览器

| 字段 | 内容 |
|------|------|
| 状态 | **已实现** |
| 优先级 | P1 |
| 涉及文件 | `mobile/lib/usePondSocket.ts` · `mobile/app/pond/[id].tsx` · `scripts/expo-web.mjs` |
| 完成日期 | 2026-07-11 |

---

## BUG-09：演示模式误触发与服务端未就绪无重连

### 现象

Web 客户端在 Socket 连接失败后进入演示模式。若 **服务端根本未 listen（见 BUG-11）**，重连也无法恢复。

> **关联**：本地「3001 红 / 始终未连接」的根因见 [`BUG修复-tsx-watch启动挂死.md`](./BUG修复-tsx-watch启动挂死.md)（BUG-11）。本文件 BUG-09/10 仅覆盖客户端与 Expo 开浏览器表象。

### 根因

1. `usePondSocket.ts` 中 `connect_error` 后 2s 定时器直接 `setDemoMode(true)`，无重连机制
2. `connectionProbe.connected` 和 return `connected` 均为 `connected || demoMode`，演示模式时返回 true 造成误导
3. Socket.io 连接未启用 `reconnection` 选项

### 修复

| 修改项 | 修改前 | 修改后 |
|--------|--------|--------|
| Demo 超时 | 2 秒 | **10 秒** |
| 自动重连 | 无 | `reconnection: true`, `reconnectionAttempts: 6`, `reconnectionDelay: 5000` |
| 定时器清理 | 多次 connect_error 叠加定时器 | 设置新定时器前 `clearTimeout(demoTimer)` |
| connected 显示 | `connected \|\| demoMode` | `connected`（真实状态），demo 模式由独立 `demoMode` 区分 |
| [id].tsx 连接探针 | 显示"已连接"或"断开" | **演示模式**下显示"演示模式" |
| Demo banner | 仅 error 非空时显示 | error 为空时也显示"演示模式：服务端未连接" |

---

## BUG-10：npm run dev 不自动打开浏览器

### 现象

`npm run dev` 启动后浏览器不会自动打开 `http://localhost:8082`。

### 根因

`scripts/expo-web.mjs` 中设置了 `BROWSER: 'none'` 阻止了 Expo 自动打开浏览器。

### 修复

注释掉 `BROWSER: 'none'` 环境变量设置，让 Expo 自动打开浏览器。

---

## 验收

- [x] 启动 dev 后，服务端未就绪时 Web 等待至少 10 秒才进入演示模式
- [x] Socket 自动重试 6 次，每次间隔 5 秒
- [x] 重试过程中服务端启动，客户端自动连接并退出演示模式
- [x] 演示模式显示"连接: 演示模式"而非"已连接"
- [x] 演示模式 banner 在 error 为空时也显示
- [x] `npm run dev` 后浏览器自动打开
- [x] `npm run build:shared` 编译通过
