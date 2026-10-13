"""Apply the shared actor cluster and Overlay HUD zones.

Actor parts are rewritten from each spot's foot point plus one template.
Re-running uses the stored foot, so positions do not drift.
HUD sprites, labels, and slices are kept; only zone rectangles move.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Offsets from the cat foot, y down, taken from pond-calm spot 1.
CLUSTER = {
    "actor-pet": (-35, -101, 128, 128),
    "actor-hit": (-30, -75, 57, 76),
    "actor-seat": (-44, -21, 85, 50),
    "actor-ring": (-26, -129, 48, 48),
    "actor-ring-bg": (-26, -129, 48, 48),
    "actor-status": (-22, -125, 40, 40),
    "actor-name": (-46, 26, 88, 20),
    "actor-bubble": (-42, -104, 80, 20),
    "actor-hint": (-44, -104, 84, 20),
}

FOOT_X = 138.5 / 512.0
FOOT_Y = 404.0 / 512.0
NAME_KEEP_OUT = 488

HUD_ZONES = {
    "menu_rail": {"x": 908, "y": 16, "w": 40, "h": 400},
    "cap_status": {"x": 752, "y": 488, "w": 148, "h": 60},
    "txt_status": {"y": 4, "w": 136, "h": 16},
    "txt_pond": {"y": 22, "w": 136, "h": 16},
    "txt_spot": {"y": 40, "w": 136, "h": 16},
    "txt_error": {"x": 12, "y": 12, "w": 936, "h": 28, "label": ""},
    "dock_fishing": {"x": 371, "y": 508, "w": 218, "h": 40},
    "txt_groundbait": {"x": 354, "y": 442, "w": 104, "h": 58},
    "btn_pan_left": {"x": 12, "y": 212, "w": 36, "h": 64},
    "btn_pan_right": {"x": 912, "y": 212, "w": 36, "h": 64},
    "dock_chat": {"x": 12, "y": 500, "w": 288, "h": 48},
    "btn_debug_police": {"x": 761, "y": 420},
    "btn_debug_gameplay": {"x": 815, "y": 420},
}


def layout_dirs() -> list[Path]:
    found = []
    for path in ROOT.rglob("layouts"):
        if path.is_dir() and (path / "pond-calm.json").is_file():
            found.append(path)
    return found


def hud_files() -> list[Path]:
    found = []
    for path in ROOT.rglob("overlay-hud.json"):
        if "hud" in path.parts:
            found.append(path)
    return found


def apply_pond(path: Path) -> int:
    data = json.loads(path.read_text(encoding="utf-8"))
    objects = data.get("objects") or []
    pets = {}
    for item in objects:
        if item.get("kind") == "actor-pet" and item.get("spotId"):
            pets[item["spotId"]] = item

    feet = {}
    warnings = 0
    for item in objects:
        if item.get("kind") != "spot" or not item.get("spotId"):
            continue
        spot_id = item["spotId"]
        stored = item.get("foot")
        if isinstance(stored, list) and len(stored) >= 2:
            foot = (float(stored[0]), float(stored[1]))
        else:
            pet = pets.get(spot_id)
            if pet is None:
                continue
            foot = (
                float(pet["x"]) + float(pet["w"]) * FOOT_X,
                float(pet["y"]) + float(pet["h"]) * FOOT_Y,
            )
        feet[spot_id] = foot
        item["foot"] = [round(foot[0], 1), round(foot[1], 1)]
        if foot[1] + 46 > NAME_KEEP_OUT:
            warnings += 1

    for item in objects:
        kind = item.get("kind")
        spec = CLUSTER.get(kind)
        foot = feet.get(item.get("spotId"))
        if spec is None or foot is None:
            continue
        ox, oy, width, height = spec
        item["x"] = round(foot[0] + ox)
        item["y"] = round(foot[1] + oy)
        item["w"] = width
        item["h"] = height
        item["anchor"] = "top-left"

    path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    return warnings


def apply_hud(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    for widget in data.get("widgets") or []:
        zone = HUD_ZONES.get(widget.get("id"))
        if zone:
            widget.update(zone)
    path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def main() -> None:
    template = {
        "version": 1,
        "anchor": "foot",
        "keepOutBottom": NAME_KEEP_OUT,
        "parts": {
            kind: {"x": ox, "y": oy, "w": width, "h": height}
            for kind, (ox, oy, width, height) in CLUSTER.items()
        },
    }
    ponds = 0
    warnings = 0
    for folder in layout_dirs():
        (folder / "actor-cluster.json").write_text(
            json.dumps(template, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        for path in sorted(folder.glob("pond-*.json")):
            warnings += apply_pond(path)
            ponds += 1
        print(f"ponds {ponds} so far in {folder}")
    for path in hud_files():
        apply_hud(path)
        print(f"hud {path}")
    print(f"rewrote {ponds} pond files, name keep-out warnings {warnings}")


if __name__ == "__main__":
    main()
