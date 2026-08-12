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
        "已确认",
        "STEAM-DESKTOP-EPIC",
        "产品规划",
        "Steam 桌面端独立游戏转型",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Steam 独立游戏定位；Unity Windows 桌面助手；Steam 身份/Lobby；Node 权威服务；空鱼塘离线补算",
        "docs/planning/specs/Steam桌面端独立游戏转型计划.md",
        "2026-08-11",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-01",
        "产品规划",
        "Steam 独立游戏定位与桌面助手信息架构",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "上班族挂机、好友交流、低打扰通知；定义窗口/托盘/后台/主循环",
        "docs/planning/specs/Steam桌面端产品定位与信息架构.md",
        "2026-08-11",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-02",
        "架构",
        "Steam 身份、账号绑定与安全会话",
        "—",
        "v1.0-steam-desktop",
        "P0",
        "Steam Ticket 验证；SteamID64↔playerId；JWT/Refresh；权威数据与审计",
        "docs/planning/specs/Steam身份账号绑定与安全会话.md",
        "2026-08-11",
        "",
    ],
    [
        "已确认",
        "STEAM-DESKTOP-03",
        "功能",
        "Steam 好友、Lobby、邀请与鱼塘映射",
        "—",
        "v1.0-steam-desktop",
        "P1",
        "好友邀请进入 Lobby；Lobby 保存 pondId；Node 仍负责鱼塘权威",
        "docs/planning/specs/Steam桌面端独立游戏转型计划.md",
        "2026-08-11",
        "",
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
]

# ENSURE 行状态优先于「总 spec」元信息同步（避免 phase2 总文档把子编号打回已确认）
ENSURE_PLAN_IDS = {str(row[1]) for row in ENSURE_PLAN_ROWS}


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
