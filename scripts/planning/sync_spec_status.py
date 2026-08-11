"""Batch-sync spec status fields and README index from effective status map."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPECS_DIR = ROOT / "docs" / "planning" / "specs"
README = SPECS_DIR / "README.md"

# Target status per file (authoritative)
TARGET_STATUS: dict[str, str] = {
    "A0-数值重构.md": "已废弃",
    "A1-飘字广播.md": "已实现",
    "A2-Debug面板.md": "已实现",
    "B0-商店基础.md": "已实现",
    "B1-鱼饵偏好.md": "已实现",
    "C-调优与状态机.md": "已确认",
    "UI体验修复-社交商店图鉴.md": "已实现",
    "BUG修复-四项体验问题.md": "已实现",
    "BUG修复-资料与鱼塘UI.md": "已实现",
    "BUG修复-鱼塘钓鱼时长显示.md": "已实现",
    "BUG修复-挂机断线离位.md": "已实现",
    "BUG修复-切页误离塘与计时中断.md": "已实现",
    "BUG修复-会话计时广播回归.md": "已实现",
    "三层数据体系-可观测性补充-v0.6.md": "已实现",
    "数据平台-Phase2-稳定增长.md": "已确认",
    "Phase2-剩余事项设计与风险.md": "已确认",
    "Phase2-下一迭代开发计划.md": "已确认",
    "运营日报-v1.md": "已确认",
    "运营日报-剩余需求交接.md": "已确认",
    "数据平台-DP-D-BI与合规交接.md": "已确认",
    "BUG修复-tsx-watch启动挂死.md": "已实现",
    "分析与修复-钓鱼概率与饵文案.md": "已实现",
    "他人主页优化.md": "已实现",
    "数值重构v2-成长咬钩与文案.md": "已实现",
    "数值重构-品质尺寸咬钩脱钩.md": "已实现",
    "生态调优-咬钩间隔与鱼群恢复.md": "已实现",
    "钓点鱼群流动性与分区咬钩.md": "已实现",
    "咬钩产量调优-单鱼抽样与脱钩.md": "已实现",
    "状态机需求描述.md": "已定稿",
    "钓鱼系统v2-生态与玩法重构.md": "已废弃",
    "钓鱼系统v2-开发交接.md": "已实现",
    "服务器维护-端口占用.md": "已实现",
    "服务器架构缺陷与埋点设计-v0.4.4.md": "已实现",
    "v0.4.4-未完成项补完.md": "已实现",
    "v0.4.4-埋点缺口复核与补全.md": "已实现",
    "服务器架构优化路线图-v0.5.md": "已实现",
    "排查-挂机断线诊断阶段2-4.md": "已实现",
    "v0.2.4-开发交接.md": "已实现",
    "v0.2.5-开发交接.md": "已实现",
    "v0.2.6-开发交接.md": "已实现",
    "v0.3.0-开发交接.md": "已实现",
    "v0.3.1-开发交接.md": "已实现",
    "v0.3.2-开发交接.md": "已实现",
    "v0.4.0-开发交接.md": "已实现",
    "v0.4.1-开发交接.md": "已实现",
}

STATUS_NOTE: dict[str, str] = {
    "A0-数值重构.md": "§3.2~3.8 由 A0-v2 / v0.3.x 替代",
    "C-调优与状态机.md": "P2 子任务按需触发",
    "数值重构v2-成长咬钩与文案.md": "§6.3 已于 v0.2.4 落地；数值后续由 v0.3.x 迭代",
    "状态机需求描述.md": "C6 引用，非独立交付物",
    "钓鱼系统v2-开发交接.md": "交接完成",
}


def fmt_status(filename: str, status: str) -> str:
    note = STATUS_NOTE.get(filename)
    if note:
        return f"**{status}**（{note}）"
    return f"**{status}**"


def replace_table_status(text: str, new_status: str) -> tuple[str, bool]:
    pattern = r"(\|\s*状态\s*\|\s*)([^|\n]+)(\s*\|)"
    m = re.search(pattern, text)
    if not m:
        return text, False
    return text[: m.start(2)] + new_status + text[m.end(2) :], True


def replace_spec_status_row(text: str, new_status: str) -> tuple[str, bool]:
    pattern = r"(\|\s*Spec 状态\s*\|\s*)([^|\n]+)(\s*\|)"
    m = re.search(pattern, text)
    if not m:
        return text, False
    return text[: m.start(2)] + new_status + text[m.end(2) :], True


def replace_bold_status_line(text: str, new_status: str) -> tuple[str, bool]:
    pattern = r"(\*\*状态\*\*\s*[:：]\s*)([^\n]+)"
    m = re.search(pattern, text)
    if not m:
        return text, False
    return text[: m.start(2)] + new_status + text[m.end(2) :], True


def update_file(path: Path, status: str) -> list[str]:
    filename = path.name
    display = fmt_status(filename, status)
    text = path.read_text(encoding="utf-8")
    original = text
    changes: list[str] = []

    if filename == "生态调优-咬钩间隔与鱼群恢复.md":
        text = re.sub(
            r"\*\*版本\*\*：v0\.3\.2（D1–D6 已实现；D7–D8 待开发）",
            "**版本**：v0.3.2（D1–D8 已实现）",
            text,
        )
        text, ok = replace_bold_status_line(text, display)
        if ok:
            changes.append("status line")

    elif filename == "钓鱼系统v2-开发交接.md":
        text = text.replace(
            "全部子 spec 状态：**已确认**。",
            "全部子 spec 状态：**已实现**。",
        )
        text, ok = replace_spec_status_row(text, fmt_status(filename, status))
        if ok:
            changes.append("Spec 状态 table")

    elif filename == "状态机需求描述.md":
        if "| 状态 |" not in text:
            insert = (
                "\n| 字段 | 内容 |\n|------|------|\n"
                f"| 状态 | {display} |\n| 定位 | C6 实施引用；完整状态机与阶段动画规格 |\n"
            )
            parts = text.split("\n", 1)
            text = parts[0] + insert + ("\n" + parts[1] if len(parts) > 1 else "")
            changes.append("added meta table")
        else:
            text, ok = replace_table_status(text, display)
            if ok:
                changes.append("status table")

    elif filename.endswith("-开发交接.md"):
        text, ok = replace_bold_status_line(text, display)
        if ok:
            changes.append("**状态** line")
        text, ok2 = replace_table_status(text, display)
        if ok2:
            changes.append("status table")
        if not ok and not ok2 and "状态：**已实现**" not in text:
            # v0.2.4 style blockquote only — add to meta if missing
            if "| 状态 |" not in text and "## 元信息" in text:
                text = text.replace(
                    "## 元信息\n\n| 字段 | 内容 |",
                    f"## 元信息\n\n| 字段 | 内容 |\n| 状态 | {display} |",
                    1,
                )
                changes.append("meta table status")

    else:
        text, ok1 = replace_table_status(text, display)
        text, ok2 = replace_bold_status_line(text, display)
        if ok1:
            changes.append("status table")
        if ok2:
            changes.append("**状态** line")

    if text != original:
        path.write_text(text, encoding="utf-8")
    return changes


def main() -> None:
    updated = 0
    for filename, status in sorted(TARGET_STATUS.items()):
        path = SPECS_DIR / filename
        if not path.exists():
            print(f"SKIP missing: {filename}")
            continue
        changes = update_file(path, status)
        if changes:
            updated += 1
            print(f"OK {filename}: {', '.join(changes)}")
        else:
            print(f"-- {filename}: no change needed")

    print(f"\nUpdated {updated} spec files")
    print("README.md must be updated separately (see sync output)")


if __name__ == "__main__":
    main()
