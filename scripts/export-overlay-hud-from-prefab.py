#!/usr/bin/env python3
"""Generate overlay-hud.json from Unity OverlayHud.prefab RectTransforms."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PREFAB = ROOT / "fish-social-unity/Assets/Resources/Desktop/Prefabs/OverlayHud.prefab"
OUT = ROOT / "desktop-overlay/OverlayResources/hud/overlay-hud.json"
ROOT_RT = "6619995883893608768"

KIND_BY_ID = {
    "menu_rail": "group",
    "dock_fishing": "group",
    "cap_status": "panel",
    "dock_chat": "panel",
    "chat_input": "panel",
}
TEXT_IDS = {
    "txt_status",
    "txt_pond",
    "txt_spot",
    "txt_error",
    "txt_groundbait",
    "chat_preview",
    "chat_placeholder",
}


def parse_prefab(text: str):
    blocks = re.split(r"^--- !u!", text, flags=re.M)
    rts: dict[str, dict] = {}
    go_to_rt: dict[str, str] = {}
    widget_by_rt: dict[str, str] = {}
    visible_by_rt: dict[str, bool] = {}

    for block in blocks:
        if block.startswith("1 &"):
            gid = re.match(r"1 &(\d+)", block)
            nm = re.search(r"^\s*m_Name: (.+)$", block, re.M)
            if gid and nm:
                go_to_rt.setdefault(gid.group(1), None)
        elif block.startswith("224 &"):
            fid_m = re.match(r"224 &(\d+)", block)
            if not fid_m:
                continue
            fid = fid_m.group(1)
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
            father = re.search(r"m_Father: \{fileID: (\d+)\}", block)
            ap = re.search(r"m_AnchoredPosition: \{x: ([^,]+), y: ([^}]+)\}", block)
            sd = re.search(r"m_SizeDelta: \{x: ([^,]+), y: ([^}]+)\}", block)
            amin = re.search(r"m_AnchorMin: \{x: ([^,]+), y: ([^}]+)\}", block)
            amax = re.search(r"m_AnchorMax: \{x: ([^,]+), y: ([^}]+)\}", block)
            piv = re.search(r"m_Pivot: \{x: ([^,]+), y: ([^}]+)\}", block)
            rts[fid] = {
                "father": father.group(1) if father else "0",
                "ap": (float(ap.group(1)), float(ap.group(2))) if ap else (0.0, 0.0),
                "sd": (float(sd.group(1)), float(sd.group(2))) if sd else (0.0, 0.0),
                "amin": (float(amin.group(1)), float(amin.group(2))) if amin else (0.0, 0.0),
                "amax": (float(amax.group(1)), float(amax.group(2))) if amax else (0.0, 0.0),
                "piv": (float(piv.group(1)), float(piv.group(2))) if piv else (0.0, 1.0),
            }
            if go:
                go_to_rt[go.group(1)] = fid
        elif block.startswith("114 &") and "widgetId:" in block:
            wid = re.search(r"widgetId: (\S+)", block).group(1)
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block).group(1)
            vis = re.search(r"visibleDefault: (\d)", block)
            rt = go_to_rt.get(go)
            if rt:
                widget_by_rt[rt] = wid
                visible_by_rt[rt] = bool(int(vis.group(1))) if vis else True

    return rts, widget_by_rt, visible_by_rt


def is_top_left(rt: dict) -> bool:
    eps = 1e-3
    return (
        abs(rt["amin"][0]) < eps
        and abs(rt["amin"][1] - 1.0) < eps
        and abs(rt["amax"][0]) < eps
        and abs(rt["amax"][1] - 1.0) < eps
        and abs(rt["piv"][0]) < eps
        and abs(rt["piv"][1] - 1.0) < eps
    )


def resolve_size(rt: dict) -> tuple[float, float]:
    w, h = rt["sd"]
    if abs(w) <= 0.5:
        w = abs(w)
    if abs(h) <= 0.5:
        h = abs(h)
    return w, h


def rel_pos(rt_id: str, rts: dict[str, dict]) -> tuple[float, float]:
    rt = rts[rt_id]
    if is_top_left(rt):
        return rt["ap"][0], -rt["ap"][1]

    # bottom-left anchored child inside top-left parent (broken prefab layout)
    father = rt["father"]
    if father in rts and is_top_left(rts[father]):
        _, ph = resolve_size(rts[father])
        return rt["ap"][0], ph
    return rt["ap"][0], -rt["ap"][1]


def abs_pos(rt_id: str, rts: dict[str, dict], cache: dict[str, tuple[float, float]] | None = None):
    if cache is None:
        cache = {}
    if rt_id in cache:
        return cache[rt_id]
    rt = rts[rt_id]
    rx, ry = rel_pos(rt_id, rts)
    if rt["father"] in ("0", ROOT_RT):
        cache[rt_id] = (rx, ry)
        return cache[rt_id]
    px, py = abs_pos(rt["father"], rts, cache)
    cache[rt_id] = (px + rx, py + ry)
    return cache[rt_id]


def parent_widget(rt_id: str, rts: dict[str, dict], widget_by_rt: dict[str, str]) -> str | None:
    f = rts[rt_id]["father"]
    while f not in ("0", ROOT_RT):
        if f in widget_by_rt:
            return widget_by_rt[f]
        f = rts[f]["father"]
    return None


def kind_for(widget_id: str) -> str:
    if widget_id in KIND_BY_ID:
        return KIND_BY_ID[widget_id]
    if widget_id in TEXT_IDS:
        return "text"
    return "button"


def main() -> int:
    text = PREFAB.read_text(encoding="utf-8")
    rts, widget_by_rt, visible_by_rt = parse_prefab(text)
    if not widget_by_rt:
        raise SystemExit(f"No widgets parsed from {PREFAB}")

    # catalog defaults for zero-size fishing buttons in current prefab
    fishing_defaults = {
        "btn_fishing_toggle": (0, 0, 70, 32),
        "btn_groundbait": (74, 0, 70, 32),
        "btn_catch_leave": (148, 0, 70, 32),
    }

    rt_by_widget = {v: k for k, v in widget_by_rt.items()}
    widgets = []
    for rt_id, widget_id in widget_by_rt.items():
        rt = rts[rt_id]
        ax, ay = abs_pos(rt_id, rts)
        w, h = resolve_size(rt)
        parent_id = parent_widget(rt_id, rts, widget_by_rt)
        if parent_id:
            prt = rt_by_widget[parent_id]
            px, py = abs_pos(prt, rts)
            x, y = round(ax - px), round(ay - py)
        else:
            x, y = round(ax), round(ay)

        if widget_id in fishing_defaults and (w <= 0.5 or h <= 0.5):
            x, y, w, h = fishing_defaults[widget_id]

        w, h = round(w), round(h)
        entry = {
            "id": widget_id,
            "kind": kind_for(widget_id),
            "x": x,
            "y": y,
            "w": w,
            "h": h,
            "z": 100,
            "visibleDefault": visible_by_rt.get(rt_id, True),
        }
        if parent_id:
            entry["parentId"] = parent_id
        widgets.append(entry)

    order = [
        "menu_rail",
        "btn_menu_settings",
        "btn_menu_map",
        "btn_menu_shop",
        "btn_menu_friends",
        "btn_menu_catch",
        "btn_menu_leaderboard",
        "btn_debug_police",
        "btn_debug_gameplay",
        "btn_menu_toggle",
        "btn_open_main",
        "btn_exit_pond",
        "cap_status",
        "txt_status",
        "txt_pond",
        "txt_spot",
        "txt_error",
        "dock_fishing",
        "btn_fishing_toggle",
        "btn_groundbait",
        "btn_catch_leave",
        "dock_chat",
        "chat_preview",
        "chat_toggle",
        "chat_input",
        "chat_send",
        "chat_placeholder",
        "txt_groundbait",
    ]
    by_id = {w["id"]: w for w in widgets}
    widgets = [by_id[i] for i in order if i in by_id]

    doc = {"width": 960, "height": 560, "widgets": widgets}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=4), encoding="utf-8")
    print("Wrote", OUT)
    for w in widgets:
        pid = w.get("parentId", "")
        print(
            f"{w['id']:22} x={w['x']:4} y={w['y']:4} "
            f"w={w['w']:3} h={w['h']:3} parent={pid}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
