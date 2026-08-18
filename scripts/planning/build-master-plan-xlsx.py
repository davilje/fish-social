#!/usr/bin/env python3
"""Regenerate the planning workbook without locale-dependent text handling.

The previous generator was a large, hand-maintained Python literal that had
become mojibake and syntactically invalid.  The workbook is the durable
planning artifact, so this generator updates that workbook in place, syncing
statuses from the authoritative spec metadata while preserving all sheets and
rows.
"""

from __future__ import annotations

import re
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path

import openpyxl


ROOT = Path(__file__).resolve().parents[2]
DOCS_PLANNING = ROOT / "docs" / "planning"
SPEC_DIR = DOCS_PLANNING / "specs"
TODAY = date.today().isoformat()

# Rows that must exist on the first planning sheet (upsert by 编号).
# Columns: 当前状态, 编号, 类型, 需求名称, 层级, 版本/阶段, 优先级, 说明, 文档路径, 设计时间, 完成时间
ENSURE_PLAN_ROWS: list[list[object]] = [
    [
        "已实现",
        "BUG-18",
        "Bug修复",
        "进塘首帧状态与演示降级",
        "—",
        "hotfix",
        "P0",
        "进塘清空旧态；禁静默DEMO；收杆ack最终额度；快照门禁",
        "docs/planning/specs/BUG修复-进塘首帧状态与演示降级.md",
        "2026-08-10",
        "2026-08-10",
    ],
    [
        "已实现",
        "BUG-19",
        "Bug修复",
        "每日额度单一口径重构",
        "—",
        "hotfix",
        "P0",
        "拆分base/session锚点；checkpoint不前移展示锚点；客户端禁反推",
        "docs/planning/specs/BUG修复-每日额度单一口径重构.md",
        "2026-08-10",
        "2026-08-10",
    ],
    [
        "已实现",
        "BUG-20",
        "Bug修复",
        "进塘与钓鱼剩余展示回归",
        "—",
        "hotfix",
        "P0",
        "BUG-19后：冻结基线+墙钟走动；未选钓点join ack种子防满额8h",
        "docs/planning/specs/BUG修复-进塘与钓鱼剩余展示回归.md",
        "2026-08-10",
        "2026-08-10",
    ],
    [
        "已实现",
        "BUG-21",
        "Bug修复",
        "桌面端关闭后进程残留",
        "—",
        "hotfix",
        "P0",
        "关闭按钮真正退出；托盘显式隐藏；单实例互斥；清理托盘线程",
        "docs/planning/specs/BUG修复-桌面端关闭后进程残留.md",
        "2026-08-12",
        "2026-08-12",
    ],
    [
        "已实现",
        "BUG-22",
        "Bug修复",
        "Steam Lobby 创建权限拒绝与状态残留",
        "—",
        "hotfix",
        "P0",
        "细分 Lobby 权限错误；校验 Steam 绑定；创建失败回滚 CurrentLobbyId/pondId；不影响鱼塘生命周期",
        "docs/planning/specs/BUG修复-SteamLobby创建权限与状态残留.md",
        "2026-08-13",
        "2026-08-13",
    ],
    [
        "已废弃",
        "STEAM-DESKTOP-06",
        "功能优化",
        "Steam Lobby 生命周期与邀请反馈优化",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "本版本跳过；不进入开发排期，后续如有正式 Lobby 产品方案再重新立项",
        "docs/planning/specs/Steam Lobby生命周期与邀请反馈优化.md",
        "2026-08-13",
        "2026-08-13",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07A",
        "功能",
        "桌面宠物主视图与鱼塘入口",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "显示自己的 2D 猫咪、钓鱼状态和鱼塘入口；采用序列帧+状态机，复用已完成桌面壳",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "2026-08-14",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07B",
        "功能",
        "2D 鱼塘环境与自己的猫咪",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "显示池塘环境、钓位、自己的宠物和钓鱼表现",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "2026-08-14",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07C",
        "功能",
        "同塘玩家宠物与状态同步",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "在 Overlay 渲染 pond_user_joined/left/updated；128×128 统一序列帧；fishingPhase→petVisualState；IPC 不传图、不新开协议",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "2026-08-15",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07D",
        "功能",
        "桌面宠物右键菜单",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "在产品窗口/宠物区域提供鱼塘、好友、背包、图鉴、设置、托盘和退出入口",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "2026-08-16",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07E",
        "功能",
        "桌面宠物主窗口功能页签",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "好友/聊天、背包、图鉴、设置为主窗口页签；Overlay 菜单切页且主窗口高于 Overlay；不用功能弹窗",
        "docs/planning/specs/Steam桌面端Web功能对齐设计.md",
        "2026-08-13",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07F",
        "功能",
        "桌面宠物主流程与恢复验收",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "串联登录、进塘、挂机、通知、托盘、收鱼、断线恢复并完成 Windows 验收",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-07G",
        "功能",
        "原生桌面宠物 Overlay",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "独立 WPF/Win32 Overlay；不启动第二个 Unity Player；通过 Named Pipe 接收状态并发送命令",
        "docs/planning/specs/Steam原生桌面宠物Overlay.md",
        "2026-08-14",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08A",
        "功能",
        "世界地图与鱼塘选择",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "主窗口超大地图 Image：拖动/滚轮缩放/边界；pondId 坐标绑定；点击进塘并显示 Overlay",
        "docs/planning/specs/Steam桌面端-08A世界地图与鱼塘选择.md",
        "2026-08-17",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08B",
        "功能",
        "商店与装备",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "PanelShop：鱼饵/渔具购买与装备；金币库存以服务端为准；Overlay 只 menu_shop 切页",
        "docs/planning/specs/Steam桌面端-08B商店与装备.md",
        "2026-08-17",
        "2026-08-18",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-08C",
        "功能",
        "动态墙与好友动态",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "PanelSocialFeed：公共/好友动态、点赞、评论、删除评论；不塞进 PanelSocial",
        "docs/planning/specs/Steam桌面端-08C动态墙与好友动态.md",
        "2026-08-17",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-08D",
        "功能",
        "排行榜",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "PanelLeaderboard：日榜/周榜/鱼塘榜/稀有鱼榜与我的排名；分数以服务端为准",
        "docs/planning/specs/Steam桌面端-08D排行榜.md",
        "2026-08-17",
        "",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08E",
        "功能",
        "个人中心与资料编辑",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "PanelProfile/Edit：昵称头像展示鱼获；不改 SteamID、不迁移旧档",
        "docs/planning/specs/Steam桌面端-08E个人中心与资料编辑.md",
        "2026-08-17",
        "2026-08-18",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08F",
        "功能",
        "好友列表与申请 Prefab",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "独立 PanelFriends；修复接受/拒绝重叠不可点；不与 PanelSocial 双开好友 UI",
        "docs/planning/specs/Steam桌面端-08F好友列表与申请Prefab.md",
        "2026-08-17",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08G",
        "功能",
        "Overlay 钓鱼操作栏",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Overlay 底部选钓位/开始/收杆/领鱼获；Pipe 发命令；Unity/Node 保留权威",
        "docs/planning/specs/Steam桌面端-08GOverlay钓鱼操作栏.md",
        "2026-08-17",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08H",
        "工程",
        "全量 UI 预制体化与动态内容组件",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "壳层、功能页、列表行和 Grid item 全部使用合法 Prefab；运行时代码只绑定数据和事件",
        "docs/planning/specs/Steam桌面端-08H全量UI预制体化.md",
        "2026-08-17",
        "2026-08-17",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-08I",
        "Bug 修复 / 功能优化",
        "鱼塘退出与跨塘切换优化",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Overlay 点击钓位直接入座；新增离席/退出鱼塘；收杆领鱼离席后跨塘切换；修复隐藏主窗口时 5 FPS 导致的 ACK 延迟，增加 ACK 优先队列、持久状态序号、重复状态丢弃及三段链路时间戳验收",
        "docs/planning/specs/Steam桌面端-08I鱼塘退出与跨塘切换优化.md",
        "2026-08-18",
        "2026-08-18",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-ART-01",
        "美术",
        "桌面宠物与鱼塘视觉资源替换",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "提供猫咪宠物、鱼塘环境和基础视觉资源；其他 UI 先由程序使用通用资源",
        "docs/planning/specs/Steam桌面宠物UI需求拆分.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-ART-02",
        "美术",
        "Overlay 场景布局管线",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "Unity 960×480 Canvas Prefab 导出布局 JSON；Overlay 按像素表摆图与钓位，有表则停用 MapToScene",
        "docs/planning/specs/Steam桌面Overlay场景布局管线.md",
        "2026-08-16",
        "",
    ],
    # —— 2026-08-10 收尾验收：ARC / DP-C / OPS ——
    [
        "已实现",
        "ARC-06",
        "架构",
        "Docker 容器化部署",
        "—",
        "v0.8",
        "P0",
        "Dockerfile+compose+SQLite卷+/health；verify:deploy（Docker CLI 环境可SKIP）",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-11",
        "2026-08-10",
    ],
    [
        "已实现",
        "ARC-07",
        "架构",
        "Mobile JWT Token 管理",
        "—",
        "v0.8",
        "P0",
        "SecureStore+刷新+API Authorization+Socket auth.token；verify:deploy",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-11",
        "2026-08-10",
    ],
    [
        "已实现",
        "ARC-08",
        "架构",
        "gameState.ts 拆分",
        "—",
        "v0.8",
        "P1",
        "facade+pondSession/UserManager/Chat；import boundary；verify:engineering",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "ARC-09",
        "架构",
        "统一日志/指标 API",
        "—",
        "v0.8",
        "P1",
        "业务路径无裸logInfo/Warn；logStructuredEvent+metrics；verify:engineering",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "ARC-10",
        "架构",
        "安全加固补完",
        "—",
        "v0.8",
        "P1",
        "HTTP限流+Socket连接上限+dev-token仅localhost；verify:engineering",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "ARC-11",
        "架构",
        "单元测试 + CI",
        "—",
        "v0.8",
        "P1",
        "Vitest+GitHub Actions；BUG-08门禁改语义对齐isFishingActive；verify:engineering",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "BUG-08",
        "Bug修复",
        "F1 Modal 会话计时不冻结",
        "—",
        "v0.8",
        "P2",
        "Modal打开时基于fishingStartedAt本地插值；tick与服务端isFishingActive语义对齐；verify:engineering",
        "docs/planning/specs/phase2-开发计划.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "D-L3-02",
        "数据平台",
        "线上实测 vs 模拟对照",
        "—",
        "v0.8",
        "P1",
        "live-vs-sim.html/json+deviationPct；日批样本；verify:data-platform-dp-c",
        "docs/planning/specs/数据平台-Phase2-稳定增长.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "D-L3-09",
        "数据平台",
        "analytics 索引打通",
        "—",
        "v0.8",
        "P1",
        "manifest live-daily+index入口+日批持续生成+warehouse/latest；verify:data-platform-dp-c",
        "docs/planning/specs/数据平台-Phase2-稳定增长.md",
        "2026-07-12",
        "2026-08-10",
    ],
    [
        "已实现",
        "OPS-RELEASE-1",
        "运维",
        "发版与热更策略",
        "—",
        "hotfix",
        "P1",
        "单机Runbook：A配置/B发版/备份/迁移/冒烟/health/回滚；OTA暂不做",
        "docs/planning/specs/发版与热更策略.md",
        "2026-07-27",
        "2026-08-10",
    ],
    [
        "已文档化",
        "STEAM-DESKTOP-EPIC",
        "产品规划",
        "Steam 桌面端独立游戏转型",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "规划已完成；实际 Unity 表现层拆分为 STEAM-DESKTOP-07，已完成子需求不重复开发",
        "docs/planning/specs/Steam桌面端独立游戏转型计划.md",
        "2026-08-11",
        "2026-08-13",
    ],
    [
        "已文档化",
        "STEAM-DESKTOP-01",
        "产品规划",
        "2D 多人社交桌面宠物定位、鱼塘场景与信息架构",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "规划已完成；桌面宠物、多人鱼塘、右键菜单与弹窗实现转入 STEAM-DESKTOP-07",
        "docs/planning/specs/Steam桌面端产品定位与信息架构.md",
        "2026-08-11",
        "2026-08-13",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-02",
        "架构",
        "Steam 身份、账号绑定与安全会话",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Steam Ticket 验证；SteamID64↔playerId；JWT/Refresh；权威数据与审计",
        "docs/planning/specs/Steam身份账号绑定与安全会话.md",
        "2026-08-11",
        "2026-08-12",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-03",
        "功能",
        "Steam 好友、Lobby、邀请与鱼塘映射",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "核心功能验收完成：Lobby 创建/加入、邀请与 pondId 映射、权限拒绝、Lobby 失效后重新进塘、房主离开不删除鱼塘；双 Steam 联调因缺少第二测试账号跳过",
        "docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md",
        "2026-08-11",
        "2026-08-14",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04",
        "Unity",
        "Unity Windows 桌面端基础壳",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "窗口/托盘/主界面占位/通知接口；04A～04F Windows Development Build 与冒烟已完成",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-12",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04A",
        "Unity",
        "Unity 工程基线与 Windows 构建",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "目录约定、DesktopMain、Player Settings、gitignore、构建菜单",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-11",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04B",
        "Unity",
        "应用生命周期与窗口模式",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "普通窗口/全屏/无边框、配置保存、关闭进托盘",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-11",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04C",
        "Unity",
        "托盘与后台挂机容器",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Win32 托盘菜单；隐藏后降帧；会话生命周期占位接口",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-11",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04D",
        "Unity",
        "Unity 主界面与功能占位",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "运行时 UGUI：鱼塘/好友/鱼获/设置四入口可进出",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-11",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04E",
        "Unity",
        "桌面设置与通知接口",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "通知偏好持久化；鱼咬钩/好友邀请/连接错误模拟",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-11",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-04F",
        "Unity",
        "Unity Windows 性能与发布验证",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "Windows Development Build、窗口/托盘/设置/通知/后台降帧冒烟已完成",
        "docs/planning/specs/Unity Windows桌面端基础壳.md",
        "2026-08-11",
        "2026-08-12",
    ],
    [
        "已实现",
        "STEAM-DESKTOP-05",
        "架构",
        "空鱼塘休眠与生态离线补算",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "lastSimulatedAt；鱼群迁移/成长补算；幂等唤醒；活跃鱼塘才运行高频 Tick",
        "docs/planning/specs/空鱼塘休眠与生态离线补算.md",
        "2026-08-11",
        "2026-08-12",
    ],
    [
        "已实现",
        "UNITY-P1",
        "架构",
        "Unity 移植 P1·契约工程化",
        "—",
        "unity-client",
        "P0",
        "Unity/Node/shared Socket 契约、C# DTO、服务端权威边界与兼容策略；Debug：补齐 phase/重连状态同步",
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "2026-08-12",
    ],
    [
        "已实现",
        "UNITY-P2",
        "架构",
        "Unity 移植 P2·网络薄客户端",
        "—",
        "unity-client",
        "P0",
        "Steam JWT Socket 登录、进塘、钓鱼、咬钩、领取、背包更新与断线重连；Debug：修复 fish_bite 时序、重复收杆和空钓位 waiting",
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "2026-08-12",
    ],
]

# ENSURE 行状态优先于「总 spec」元信息同步（避免 phase2 总文档把子编号打回已确认）
ENSURE_PLAN_IDS = {str(row[1]) for row in ENSURE_PLAN_ROWS}
REMOVE_PLAN_IDS = {
    "STEAM-DESKTOP-07",
    "STEAM-DESKTOP-07A",
    "STEAM-DESKTOP-07B",
    "STEAM-DESKTOP-07C",
    "STEAM-DESKTOP-07D",
    "STEAM-DESKTOP-07E",
    "STEAM-DESKTOP-07F",
    "STEAM-DESKTOP-07G",
    "STEAM-DESKTOP-08A",
    "STEAM-DESKTOP-08B",
    "STEAM-DESKTOP-08C",
    "STEAM-DESKTOP-08D",
    "STEAM-DESKTOP-08E",
    "STEAM-DESKTOP-08F",
    "STEAM-DESKTOP-08G",
    "STEAM-DESKTOP-08H",
    "STEAM-DESKTOP-08I",
    "STEAM-UI-01",
    "STEAM-UI-02",
    "STEAM-UI-03",
    "STEAM-UI-04",
    "STEAM-UI-05",
    "STEAM-UI-06",
    "STEAM-UI-07",
    "STEAM-UI-PROG-01",
    "STEAM-UI-ART-01",
    "STEAM-UI-PROG-02",
    "STEAM-UI-ART-02",
    "STEAM-UI-PROG-03",
    "STEAM-UI-ART-03",
    "STEAM-UI-PROG-04",
    "STEAM-UI-ART-04",
    "STEAM-UI-PROG-05",
    "STEAM-UI-ART-05",
    "STEAM-UI-PROG-06",
    "STEAM-UI-ART-06",
    "STEAM-UI-PROG-07",
    "STEAM-UI-ART-07",
}
NORMALIZE_PLAN_TYPES = {
    "UNITY-P0": "架构",
    "UNITY-P1": "架构",
    "UNITY-P2": "架构",
    "UNITY-P3": "架构",
}


def is_metrics_workbook(path: Path) -> bool:
    name = path.name.lower()
    return "埋点" in path.name or "metrics" in name or "event" in name


def find_master_workbook() -> Path:
    candidates = [
        p
        for p in ROOT.glob("*.xlsx")
        if not is_metrics_workbook(p) and not p.name.startswith("~$")
    ]
    if not candidates:
        raise FileNotFoundError("No planning workbook (*.xlsx) found in repository root")
    preferred = [p for p in candidates if "计划表" in p.name or "plan" in p.name.lower()]
    return sorted(preferred or candidates, key=lambda p: p.stat().st_mtime, reverse=True)[0]


def read_spec_metadata(spec_path: Path) -> tuple[str | None, str | None]:
    if not spec_path.exists():
        return None, None
    text = spec_path.read_text(encoding="utf-8-sig")
    status_match = re.search(r"\|\s*状态\s*\|\s*\*\*(.+?)\*\*", text)
    done_match = re.search(r"\|\s*完成时间\s*\|\s*\*\*(\d{4}-\d{2}-\d{2})\*\*", text)
    return (
        status_match.group(1).strip() if status_match else None,
        done_match.group(1) if done_match else None,
    )


def find_spec_path(row: list[object]) -> Path | None:
    for value in row:
        if not isinstance(value, str):
            continue
        match = re.search(r"(docs/planning/specs/[^'\"]+\.md)", value)
        if match:
            return ROOT / match.group(1).replace("/", "\\")
    return None


def status_column(ws: openpyxl.worksheet.worksheet.Worksheet) -> int | None:
    for cell in ws[1]:
        if cell.value in {"当前状态", "当前状态"}:
            return cell.column
    # Existing workbooks may have mojibake headers.  Known planning sheets use
    # the conventional status positions below.
    return {1: 1, 2: 5, 3: 5, 4: 5, 5: 5, 6: 5, 7: 5, 8: 5, 9: 2}.get(ws._parent.worksheets.index(ws) + 1)


def ensure_plan_rows(workbook: openpyxl.Workbook) -> int:
    if not workbook.worksheets:
        return 0
    ws = workbook.worksheets[0]
    changed = 0
    id_to_row: dict[str, int] = {}
    for idx, row in enumerate(ws.iter_rows(min_row=2, values_only=True), start=2):
        if len(row) > 1 and isinstance(row[1], str) and row[1].strip():
            id_to_row[row[1].strip()] = idx

    rows_to_remove = [
        idx
        for idx, row in enumerate(ws.iter_rows(min_row=2, values_only=True), start=2)
        if len(row) > 1 and isinstance(row[1], str) and row[1].strip() in REMOVE_PLAN_IDS
    ]
    for existing in reversed(rows_to_remove):
        ws.delete_rows(existing, 1)
        changed += 1

    id_to_row = {}
    for idx, row in enumerate(ws.iter_rows(min_row=2, values_only=True), start=2):
        if len(row) > 1 and isinstance(row[1], str) and row[1].strip():
            id_to_row[row[1].strip()] = idx

    for plan_row in ENSURE_PLAN_ROWS:
        plan_id = str(plan_row[1])
        existing = id_to_row.get(plan_id)
        if existing is None:
            ws.append(plan_row)
            changed += 1
            continue
        # ENSURE 为验收权威：状态/说明/路径/日期一并回写
        for col, value in enumerate(plan_row, start=1):
            cell = ws.cell(existing, col)
            if value in (None, ""):
                continue
            if cell.value != value:
                cell.value = value
                changed += 1

    for row in ws.iter_rows(min_row=2):
        if len(row) <= 2:
            continue
        if row[2].value == "Unity":
            row[2].value = "架构"
            changed += 1
        elif row[2].value == "UI":
            row[2].value = "功能"
            changed += 1
    return changed


def update_workbook(path: Path) -> int:
    workbook = openpyxl.load_workbook(path)
    changed = 0

    for ws in workbook.worksheets:
        status_col = status_column(ws)
        if status_col is None:
            continue
        for row in ws.iter_rows(min_row=2):
            values = [cell.value for cell in row]
            plan_id = values[1] if len(values) > 1 and isinstance(values[1], str) else None
            if plan_id in NORMALIZE_PLAN_TYPES and len(row) > 2:
                type_cell = row[2]
                normalized_type = NORMALIZE_PLAN_TYPES[plan_id]
                if type_cell.value != normalized_type:
                    type_cell.value = normalized_type
                    changed += 1
            elif len(row) > 2 and row[2].value == "Unity":
                row[2].value = "架构"
                changed += 1
            elif len(row) > 2 and row[2].value == "UI":
                row[2].value = "功能"
                changed += 1
            # 子编号若在 ENSURE 中，不以总文档元信息覆盖状态
            if plan_id and plan_id in ENSURE_PLAN_IDS:
                continue
            spec_path = find_spec_path(values)
            if spec_path is None:
                continue
            status, completed = read_spec_metadata(spec_path)
            if status is None:
                continue
            cell = row[status_col - 1]
            if cell.value != status:
                cell.value = status
                changed += 1
            if status == "已实现" and completed:
                # Completion date is the cell immediately after design date in
                # all planning sheets; leave unrelated sheets untouched.
                for index, value in enumerate(values):
                    if isinstance(value, str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
                        target = row[index + 1] if index + 1 < len(row) else None
                        if target is not None and target.value in (None, ""):
                            target.value = completed
                            changed += 1
                        break

    # 最后应用 ENSURE，保证验收收口状态不被总 spec 打回
    changed += ensure_plan_rows(workbook)

    workbook.save(path)
    return changed


def sync_copy(master: Path) -> Path:
    DOCS_PLANNING.mkdir(parents=True, exist_ok=True)
    copy_path = DOCS_PLANNING / master.name
    shutil.copy2(master, copy_path)
    return copy_path


def regenerate_board() -> None:
    board_builder = ROOT / "scripts" / "planning" / "build-producer-progress-html.py"
    if board_builder.exists():
        subprocess.run([sys.executable, str(board_builder)], cwd=ROOT, check=True)


def main() -> None:
    master = find_master_workbook()
    changed = update_workbook(master)
    copy_path = sync_copy(master)
    regenerate_board()
    print(f"Updated: {master}")
    print(f"Synced copy: {copy_path}")
    print(f"Status/date cells changed: {changed}")


if __name__ == "__main__":
    main()
