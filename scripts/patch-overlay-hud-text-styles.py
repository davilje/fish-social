#!/usr/bin/env python3
"""Add STEAM-DESKTOP-14C text style fields to overlay-hud.json exports."""

from __future__ import annotations

import json
import sys
from pathlib import Path

BUTTON_STYLE = {
    "fontFile": "arial.ttf",
    "fontSize": 12,
    "fontColor": "#FFFFFFFF",
    "fontWeight": "normal",
    "contentAlign": "center",
}

TEXT_LEFT = {
    "fontFile": "arial.ttf",
    "fontSize": 12,
    "fontColor": "#FFFFFFFF",
    "fontWeight": "normal",
    "textAlign": "left",
}

TEXT_CENTER = {
    **TEXT_LEFT,
    "textAlign": "center",
}

TEXT_WIDGETS = {
    "txt_status": TEXT_LEFT,
    "txt_pond": TEXT_LEFT,
    "txt_spot": TEXT_LEFT,
    "txt_error": TEXT_CENTER,
    "txt_groundbait": TEXT_CENTER,
    "chat_preview": TEXT_LEFT,
    "chat_placeholder": TEXT_LEFT,
}


def patch(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    for widget in data.get("widgets", []):
        kind = widget.get("kind")
        widget_id = widget.get("id")
        if kind == "button":
            widget.update(BUTTON_STYLE)
        elif kind == "text" and widget_id in TEXT_WIDGETS:
            widget.update(TEXT_WIDGETS[widget_id])

    lines = [
        "    " + json.dumps(widget, ensure_ascii=False, separators=(",", ":"))
        for widget in data["widgets"]
    ]
    body = '{\n"width":960,"height":560,"widgets":[\n' + ",\n".join(lines) + "\n]}\n"
    path.write_text(body, encoding="utf-8")


def main(argv: list[str]) -> int:
    repo = Path(__file__).resolve().parents[1]
    targets = [
        repo / "desktop-overlay" / "OverlayResources" / "hud" / "overlay-hud.json",
        repo
        / "fish-social-unity"
        / "FishSocialOverlay"
        / "OverlayResources"
        / "hud"
        / "overlay-hud.json",
    ]
    for target in targets:
        if target.exists():
            patch(target)
            print("patched", target)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
