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
        "已确认",
        "STEAM-DESKTOP-07A",
        "功能",
        "桌面宠物主视图与鱼塘入口",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "显示自己的 2D 猫咪、钓鱼状态和鱼塘入口；复用已完成桌面壳",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-07B",
        "功能",
        "2D 鱼塘环境与自己的猫咪",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "显示池塘环境、钓位、自己的宠物和钓鱼表现",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-07C",
        "功能",
        "同塘玩家宠物与状态同步",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "渲染 pond_user_joined/left/updated；显示同塘玩家宠物、昵称和基础状态",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-07D",
        "功能",
        "桌面宠物右键菜单",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "在产品窗口/宠物区域提供鱼塘、好友、背包、图鉴、设置、托盘和退出入口",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-07E",
        "功能",
        "桌面宠物功能弹窗层",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "好友/聊天、背包、图鉴、设置弹窗；打开关闭不得触发 leave_pond",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-07F",
        "功能",
        "桌面宠物主流程与恢复验收",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "串联登录、进塘、挂机、通知、托盘、收鱼、断线恢复并完成 Windows 验收",
        "docs/planning/specs/Steam桌面宠物与多人鱼塘表现层.md",
        "2026-08-13",
        "",
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
        "验收中",
        "STEAM-DESKTOP-03",
        "功能",
        "Steam 好友、Lobby、邀请与鱼塘映射",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "Lobby 仅映射 pondId，房主/成员离开不关闭鱼塘；Node/DB 负责鱼塘权威、休眠与离线补算；实现已完成，双 Steam 账号验收待补",
        "docs/planning/specs/Steam好友Lobby邀请与鱼塘映射.md",
        "2026-08-11",
        "2026-08-12",
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
        "已定稿",
        "UNITY-EPIC",
        "产品规划",
        "Unity移植分阶段需求总表",
        "—",
        "unity-client",
        "P0",
        "Unity Windows 客户端迁移总规划；定义 P0～P5 阶段目标、用户场景、权限边界、协议复用和总体验收；P0～P2 已实现，P3～P5 待开发",
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "",
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
    [
        "已定稿",
        "UNITY-P3",
        "架构",
        "Unity移植P3·等距场景核心",
        "—",
        "unity-client",
        "P0",
<<<<<<< HEAD
        "Tile·相机·序列帧；承接REF-SCENE-1；当前为开发规格，尚未完成 Unity 实现",
=======
        "架构出口由 STEAM-DESKTOP-07B/07C 承接；Tile、相机、序列帧、多人排序和真实网络状态表现尚未完成",
>>>>>>> main
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "",
    ],
    [
        "已定稿",
        "UNITY-P4",
        "架构",
        "Unity移植P4·壳层功能迁入",
        "—",
        "unity-client",
        "P1",
<<<<<<< HEAD
        "地图·背包商店·社交·排行榜；当前为开发规格，尚未完成 Unity 主循环迁入",
=======
        "架构出口由 STEAM-DESKTOP-07A/07D/07E/07F 承接；Unity 主循环尚未完成 Expo 脱离验收",
>>>>>>> main
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "",
    ],
    [
        "已定稿",
        "UNITY-P5",
        "架构",
        "Unity移植P5·发布与运维对齐",
        "—",
        "unity-client",
        "P1",
<<<<<<< HEAD
        "Steam 构建·client-logs·退役 RN；当前为发布规格，尚未完成商店包验收",
=======
        "架构出口由 STEAM-DESKTOP-07F 及后续发布验收承接；Steam 可提交包、日志和回滚方案尚未完成",
>>>>>>> main
        "docs/planning/specs/Unity移植-分阶段需求清单.md",
        "2026-07-26",
        "",
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
    "UNITY-EPIC": "产品规划",
    "UNITY-P0": "架构",
    "UNITY-P1": "架构",
    "UNITY-P2": "架构",
    "UNITY-P3": "架构",
    "UNITY-P4": "架构",
    "UNITY-P5": "架构",
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
                if col == 11 and cell.value not in (None, ""):
                    cell.value = ""
                    changed += 1
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


def ensure_companion_sheets(workbook: openpyxl.Workbook) -> int:
    """Keep the workbook tabs self-describing and expose Unity phases separately."""
    changed = 0
    tab_guide = [
        ["页签", "对应内容", "使用说明"],
        ["开发计划", "全项目需求总表", "唯一总览；按编号、类型、阶段和状态查看全部需求"],
        ["数据平台-Phase 0", "数据平台第一阶段", "结构化日志、错误落库、基础可观测性"],
        ["数据平台-Phase 1+", "数据平台后续阶段", "集中日志、追踪、告警、BI 与合规"],
        ["架构修复", "架构与工程治理", "安全、持久化、定时任务、模块边界、部署等"],
        ["Bug修复", "缺陷修复", "已发现问题、回归修复与桌面端专项 Bug"],
        ["功能特性", "产品功能需求", "玩法、社交、商店、UI 与桌面端功能"],
        ["数值与生态", "数值和鱼塘生态", "咬钩、成长、鱼群、平衡与生态模拟"],
        ["排查工具", "诊断与排障工具", "断线、弱网、数据检查和运维诊断"],
        ["运营与发布", "运维与发布需求", "部署、发版、运营平台和发布流程"],
        ["产品规划", "产品规划需求", "Steam/Unity 产品定位、转型和总体规划"],
        ["参考与美术", "参考文档和美术资源", "参考蓝图、视觉资源和非执行性资料"],
        ["spec 文档清单", "需求文档索引", "spec 文件与主表编号、状态、时间的对应关系"],
        ["统计摘要", "计划统计", "需求总数、状态分布和当前待办概览"],
        ["Unity移植阶段", "UNITY-P0～P5 阶段状态", "只记录 Unity 客户端迁移阶段，不代表所有条目已在 Unity 内实现"],
    ]
    ws = workbook["页签说明"] if "页签说明" in workbook.sheetnames else workbook.create_sheet("页签说明")
    ws.delete_rows(1, ws.max_row)
    for row in tab_guide:
        ws.append(row)
    changed += 1

    unity_rows = [
        ["编号", "阶段", "目标", "当前状态", "完成时间", "判断依据"],
        ["UNITY-P0", "决策与契约冻结", "确认 Unity+Node 架构、协议基线、仓库形态和 RN 冻结策略", "已实现", "2026-07-26", "决策记录与契约冻结清单已完成"],
        ["UNITY-P1", "契约工程化", "OpenAPI、Socket 目录、C# DTO 与服务端权威边界", "已实现", "2026-08-12", "契约工程化验收已完成"],
        ["UNITY-P2", "网络薄客户端", "Unity 连接 Node，完成登录、进塘、钓鱼、收鱼、背包和重连", "已实现", "2026-08-12", "真实 Unity Windows Development Build 联调通过"],
        ["UNITY-P3", "等距 Tile 场景核心", "Tilemap、正交相机、角色序列帧、多人排序和真实网络状态表现", "已定稿", "", "规格已定稿；Unity 场景验收项未完成"],
        ["UNITY-P4", "壳层功能迁入", "地图、背包、商店、图鉴、社交和排行榜迁入 Unity 主循环", "已定稿", "", "规格已定稿；主循环仍未完成 Expo 脱离验收"],
        ["UNITY-P5", "发布与运维对齐", "Steam Windows 构建、日志、回滚和 RN 退役时间点", "已定稿", "", "规格已定稿；尚无可提交 Steam 的最小可靠包验收"],
    ]
    ws = workbook["Unity移植阶段"] if "Unity移植阶段" in workbook.sheetnames else workbook.create_sheet("Unity移植阶段")
    ws.delete_rows(1, ws.max_row)
    for row in unity_rows:
        ws.append(row)
    changed += 1
    return changed


def rebuild_detail_sheets(workbook: openpyxl.Workbook) -> int:
    """Rebuild every detail tab from the authoritative 开发计划 rows."""
    main = workbook["开发计划"]
    rows = []
    for values in main.iter_rows(min_row=2, values_only=True):
        if len(values) >= 11 and values[1]:
            rows.append(list(values[:11]))

    def classify(values: list[object]) -> str:
        return str(values[2] or "").strip()

    groups: dict[str, list[list[object]]] = {
        "数据平台-Phase 0": [r for r in rows if classify(r) == "数据平台" and "Phase 0" in str(r[5])],
        "数据平台-Phase 1+": [r for r in rows if classify(r) == "数据平台" and "Phase 0" not in str(r[5])],
        "架构修复": [r for r in rows if classify(r) == "架构"],
        "Bug修复": [r for r in rows if classify(r) == "Bug修复"],
        "功能特性": [r for r in rows if classify(r) in {"功能", "功能优化"}],
        "数值与生态": [r for r in rows if classify(r) == "数值"],
        "排查工具": [r for r in rows if classify(r) == "排查"],
        "运营与发布": [r for r in rows if classify(r) == "运维"],
        "产品规划": [r for r in rows if classify(r) == "产品规划"],
        "参考与美术": [r for r in rows if classify(r) in {"参考", "美术"}],
    }
    header = ["当前状态", "编号", "类型", "需求名称", "阶段", "优先级", "说明", "文档路径", "设计时间", "完成时间"]
    changed = 0
    for title, group in groups.items():
        ws = workbook[title] if title in workbook.sheetnames else workbook.create_sheet(title)
        ws.delete_rows(1, ws.max_row)
        ws.append(header)
        for values in sorted(group, key=lambda item: str(item[1])):
            ws.append([values[0], values[1], values[2], values[3], values[5], values[6], values[7], values[8], values[9], values[10]])
        changed += 1

    spec_ws = workbook["spec 文档清单"]
    spec_ws.delete_rows(1, spec_ws.max_row)
    spec_ws.append(["文件名", "状态", "主表编号", "说明", "设计时间", "完成时间"])
    specs: dict[str, list[list[object]]] = {}
    for row in rows:
        path = row[8]
        if not isinstance(path, str) or not path.endswith(".md"):
            continue
        specs.setdefault(path, []).append(row)
    for path, linked in sorted(specs.items()):
        statuses = sorted({str(item[0]) for item in linked})
        status = statuses[0] if len(statuses) == 1 else "多状态"
        designs = sorted({str(item[9]) for item in linked if item[9]})
        completions = sorted({str(item[10]) for item in linked if item[10]})
        spec_ws.append([
            Path(path).name,
            status,
            "、".join(str(item[1]) for item in linked),
            "由开发计划总表同步",
            designs[0] if designs else "",
            completions[-1] if completions else "",
        ])
    changed += 1

    summary = workbook["统计摘要"]
    summary.delete_rows(1, summary.max_row)
    status_counts: dict[str, int] = {}
    type_counts: dict[str, int] = {}
    for row in rows:
        status_counts[str(row[0])] = status_counts.get(str(row[0]), 0) + 1
        type_counts[str(row[2])] = type_counts.get(str(row[2]), 0) + 1
    summary.append(["指标", "数值", "说明"])
    summary.append(["总需求数", len(rows), "开发计划总表条目数"])
    for status, count in sorted(status_counts.items()):
        summary.append([status, count, "按当前状态统计"])
    summary.append([])
    summary.append(["类型", "数量", "按开发计划总表类型统计"])
    for task_type, count in sorted(type_counts.items()):
        summary.append([task_type, count, ""])
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
    changed += ensure_companion_sheets(workbook)
    changed += rebuild_detail_sheets(workbook)

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
