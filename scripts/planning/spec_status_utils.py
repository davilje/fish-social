"""Shared helpers for reading/writing spec status fields."""

from __future__ import annotations

import re
from pathlib import Path


def first_match(text: str, patterns: list[str]) -> str:
    for p in patterns:
        m = re.search(p, text, flags=re.MULTILINE)
        if m:
            return m.group(1).strip()
    return ""


def normalize_status(raw: str) -> str:
    s = (raw or "").strip().lower()
    if not s:
        return "未标注"
    if "已实现" in s:
        return "已实现"
    if "待开发" in s:
        return "待开发"
    if "已确认" in s or "已定稿" in s:
        return "已确认"
    if "待评审" in s or "评审中" in s:
        return "评审中"
    if "superseded" in s or "废弃" in s:
        return "已废弃"
    if "草案" in s or "提案" in s:
        return "规划中"
    return "未标注"


def read_spec_status(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    raw = first_match(
        text,
        [
            r"\*\*状态\*\*\s*[:：]\s*([^\n]+)",
            r"\|\s*Spec 状态\s*\|\s*([^|\n]+)\|",
            r"\|\s*状态\s*\|\s*([^|\n]+)\|",
        ],
    )
    return normalize_status(raw)


def fmt_status(status: str, note: str | None = None) -> str:
    if note:
        return f"**{status}**（{note}）"
    return f"**{status}**"


def replace_table_status(text: str, new_status: str) -> tuple[str, bool]:
    pattern = r"(\|\s*状态\s*\|\s*)([^|\n]+)(\s*\|)"
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


def set_spec_status(path: Path, status: str, note: str | None = None) -> bool:
    """Write status into a spec/handoff markdown file. Returns True if changed."""
    display = fmt_status(status, note)
    text = path.read_text(encoding="utf-8")
    original = text

    text, ok1 = replace_table_status(text, display)
    text, ok2 = replace_bold_status_line(text, display)

    if not ok1 and not ok2:
        if "## 元信息" in text:
            text = text.replace(
                "## 元信息\n\n| 字段 | 内容 |",
                f"## 元信息\n\n| 字段 | 内容 |\n| 状态 | {display} |",
                1,
            )
        elif path.name.endswith("-开发交接.md") and "**状态**" not in text:
            # Insert after title block for handoff files
            lines = text.splitlines()
            insert_at = 1
            for i, line in enumerate(lines[:8]):
                if line.startswith("**策划文档**"):
                    insert_at = i + 1
                    break
            lines.insert(insert_at, f"**状态**：{display}  ")
            text = "\n".join(lines)

    if text != original:
        path.write_text(text, encoding="utf-8")
        return True
    return ok1 or ok2


def extract_main_spec_from_handoff(handoff_path: Path) -> Path | None:
    text = handoff_path.read_text(encoding="utf-8")
    m = re.search(r"\*\*策划文档\*\*[：:]\s*`([^`]+)`", text)
    if not m:
        return None
    rel = m.group(1).strip()
    root = handoff_path.resolve().parents[3]  # specs -> planning -> docs -> repo
    candidate = (root / rel).resolve()
    return candidate if candidate.exists() else None
