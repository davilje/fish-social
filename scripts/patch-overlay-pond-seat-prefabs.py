#!/usr/bin/env python3
"""Patch all OverlayLayouts/<pondId>.prefab spots with OverlayPondActor seat parts,
and rewrite layouts/<pondId>.json with absolute actor-* coords (STEAM-DESKTOP-14A).

Does not require Unity (useful when the editor already has the project open).
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
LAYOUTS_DIR = REPO / "fish-social-unity" / "Assets" / "Desktop" / "OverlayLayouts"
JSON_DIRS = [
    REPO / "desktop-overlay" / "OverlayResources" / "layouts",
    REPO / "fish-social-unity" / "FishSocialOverlay" / "OverlayResources" / "layouts",
]

SEAT_HOST_W = 84.0
SEAT_HOST_H = 122.0
SPOT_SIZE = 24.0

# Relative top-left inside seat host (matches OverlayPondActorBaker).
PARTS = [
    ("seat", "actor-seat", 10.0, 82.0, 64.0, 32.0, 8, "seats/_default.png", (0.72, 0.55, 0.32, 0.85)),
    ("pet", "actor-pet", 10.0, 18.0, 64.0, 64.0, 10, None, (0.85, 0.55, 0.2, 0.55)),
    ("ring", "actor-ring", 4.0, 12.0, 76.0, 76.0, 12, "status/hook-ring.png", (0.91, 0.61, 0.25, 0.95)),
    ("status", "actor-status", 33.0, 0.0, 18.0, 18.0, 14, "status/fishing.png", (0.35, 0.66, 0.84, 0.95)),
    ("name", "actor-name", 2.0, 102.0, 80.0, 18.0, 16, None, (0.06, 0.09, 0.12, 0.75)),
]

LAYOUT_OBJ_GUID = "8f5d2b01c3e25a7f9d4b6082a3e1f5c2"
ACTOR_VIEW_GUID = "796169c93c28b8d43bad5812e79dd4a3"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"

FILE_ID_RE = re.compile(r"&(\d+)")
SPOT_KIND_RE = re.compile(r"^\s*kind:\s*spot\s*$", re.M)


def collect_ids(text: str) -> set[int]:
    return {int(m.group(1)) for m in FILE_ID_RE.finditer(text)}


def make_id(used: set[int], *parts: str) -> int:
    seed = "|".join(parts)
    h = int(hashlib.md5(seed.encode("utf-8")).hexdigest()[:15], 16)
    if h == 0:
        h = 1
    while h in used:
        h = (h + 1) & 0x7FFFFFFFFFFFFFFF
        if h == 0:
            h = 1
    used.add(h)
    return h


def find_spot_blocks(text: str) -> list[dict]:
    """Return unique spots: {spot_id, go_id, rt_id, image_id, marker_id, name}."""
    spots = []
    seen_go: set[int] = set()
    # Match layout markers whose OWN script is DesktopOverlayLayoutObject and kind=spot.
    marker_re = re.compile(
        r"--- !u!114 &(\d+)\nMonoBehaviour:\n"
        r"  m_ObjectHideFlags: 0\n"
        r"  m_CorrespondingSourceObject: \{fileID: 0\}\n"
        r"  m_PrefabInstance: \{fileID: 0\}\n"
        r"  m_PrefabAsset: \{fileID: 0\}\n"
        r"  m_GameObject: \{fileID: (\d+)\}\n"
        r"  m_Enabled: 1\n"
        r"  m_EditorHideFlags: 0\n"
        r"  m_Script: \{fileID: 11500000, guid: %s, type: 3\}\n"
        r"  m_Name: \n"
        r"  m_EditorClassIdentifier: \n"
        r"  objectId: (.*?)\n"
        r"  kind: (.*?)\n"
        r"  spotId: (.*?)\n"
        % LAYOUT_OBJ_GUID
    )
    for mm in marker_re.finditer(text):
        kind = mm.group(4).strip()
        if kind != "spot":
            continue
        go_id = int(mm.group(2))
        if go_id in seen_go:
            continue
        seen_go.add(go_id)
        marker_id = int(mm.group(1))
        spot_id = mm.group(5).strip() or mm.group(3).strip()

        go_m = re.search(
            r"--- !u!1 &%d\nGameObject:\n.*?m_Component:\n((?:  - component: \{fileID: \d+\}\n)+).*?m_Name: (.*?)\n"
            % go_id,
            text,
            re.S,
        )
        if not go_m:
            continue
        comps = [int(x) for x in re.findall(r"fileID: (\d+)", go_m.group(1))]
        if len(comps) < 2:
            continue
        spots.append(
            {
                "spot_id": spot_id,
                "go_id": go_id,
                "rt_id": comps[0],
                "image_id": comps[2] if len(comps) >= 3 else None,
                "marker_id": marker_id,
                "name": go_m.group(2).strip(),
            }
        )
    return spots


def has_actor_seat(text: str, spot_id: str) -> bool:
    return (
        f"m_Name: {spot_id}-seat" in text
        or f"objectId: {spot_id}-seat" in text
        or (f"spotId: {spot_id}" in text and "kind: actor-seat" in text)
    )


def expand_spot_rect(text: str, rt_id: int) -> tuple[str, float, float, float, float]:
    """Expand 24×24 host to seat host; preserve bottom-center. Returns text + top-left x,y,w,h."""
    pat = (
        r"(--- !u!224 &%d\nRectTransform:.*?m_AnchoredPosition: \{x: )([-\d.]+)(, y: )([-\d.]+)"
        r"(\}\n  m_SizeDelta: \{x: )([-\d.]+)(, y: )([-\d.]+)(\})"
    ) % rt_id
    m = re.search(pat, text, re.S)
    if not m:
        raise RuntimeError(f"RectTransform {rt_id} not found")
    ax = float(m.group(2))
    ay = float(m.group(4))  # Unity top-left Y is negative of canvas Y
    w = float(m.group(6))
    h = float(m.group(8))
    canvas_x = ax
    canvas_y = -ay
    if w <= SPOT_SIZE + 1 and h <= SPOT_SIZE + 1:
        bottom_cx = canvas_x + w * 0.5
        bottom_y = canvas_y + h
        new_x = bottom_cx - SEAT_HOST_W * 0.5
        new_y = bottom_y - SEAT_HOST_H
        new_ax = new_x
        new_ay = -new_y
        repl = (
            f"{m.group(1)}{new_ax:.4f}{m.group(3)}{new_ay:.4f}"
            f"{m.group(5)}{SEAT_HOST_W:.1f}{m.group(7)}{SEAT_HOST_H:.1f}{m.group(9)}"
        )
        text = text[: m.start()] + repl + text[m.end() :]
        return text, new_x, new_y, SEAT_HOST_W, SEAT_HOST_H
    return text, canvas_x, canvas_y, w, h


def set_image_transparent(text: str, image_id: int | None) -> str:
    if not image_id:
        return text
    pat = (
        r"(--- !u!114 &%d\nMonoBehaviour:.*?m_Script: \{fileID: 11500000, guid: %s, type: 3\}.*?"
        r"m_Color: \{r: )([-\d.]+)(, g: )([-\d.]+)(, b: )([-\d.]+)(, a: )([-\d.]+)(\})"
    ) % (image_id, IMAGE_GUID)
    m = re.search(pat, text, re.S)
    if not m:
        return text
    repl = f"{m.group(1)}1{m.group(3)}1{m.group(5)}1{m.group(7)}0{m.group(9)}"
    return text[: m.start()] + repl + text[m.end() :]


def ensure_marker_sprite(text: str, marker_id: int) -> str:
    pat = (
        r"(--- !u!114 &%d\nMonoBehaviour:.*?kind: spot\n\s*spotId:.*?\n\s*spriteFile: )(.*?)(\n)"
    ) % marker_id
    m = re.search(pat, text, re.S)
    if not m:
        return text
    current = m.group(2).strip()
    if current:
        return text
    repl = f"{m.group(1)}seats/_default.png{m.group(3)}"
    return text[: m.start()] + repl + text[m.end() :]


def add_child_refs(text: str, rt_id: int, child_rt_ids: list[int]) -> str:
    extra = "".join(f"  - {{fileID: {cid}}}\n" for cid in child_rt_ids)
    pat = r"(--- !u!224 &%d\nRectTransform:.*?m_Children:\n)((?:  - \{fileID: \d+\}\n)*)" % rt_id
    m = re.search(pat, text, re.S)
    if m:
        repl = m.group(1) + m.group(2) + extra
        return text[: m.start()] + repl + text[m.end() :]

    pat2 = r"(--- !u!224 &%d\nRectTransform:.*?m_Children: )\[\](\n)" % rt_id
    m2 = re.search(pat2, text, re.S)
    if not m2:
        raise RuntimeError(f"Cannot patch children for RT {rt_id}")
    block = text[m2.start() : m2.end()].replace(
        "m_Children: []",
        "m_Children:\n" + extra.rstrip("\n"),
    )
    return text[: m2.start()] + block + text[m2.end() :]


def add_view_component(text: str, go_id: int, used: set[int], spot_id: str, pond_id: str) -> str:
    if f"guid: {ACTOR_VIEW_GUID}" in text and f"m_GameObject: {{fileID: {go_id}}}" in text:
        # crude check: view already on this GO?
        view_pat = (
            r"--- !u!114 &(\d+)\nMonoBehaviour:.*?m_GameObject: \{fileID: %d\}.*?"
            r"guid: %s"
        ) % (go_id, ACTOR_VIEW_GUID)
        if re.search(view_pat, text, re.S):
            return text

    view_id = make_id(used, pond_id, spot_id, "view")
    # Add to component list
    go_pat = r"(--- !u!1 &%d\nGameObject:.*?m_Component:\n)((?:  - component: \{fileID: \d+\}\n)+)" % go_id
    m = re.search(go_pat, text, re.S)
    if not m:
        raise RuntimeError(f"GameObject {go_id} components not found")
    repl = m.group(1) + m.group(2) + f"  - component: {{fileID: {view_id}}}\n"
    text = text[: m.start()] + repl + text[m.end() :]
    block = f"""--- !u!114 &{view_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ACTOR_VIEW_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  spotId: {spot_id}
"""
    return text.rstrip() + "\n" + block


def emit_part(
    used: set[int],
    pond_id: str,
    spot_id: str,
    father_rt: int,
    root_order: int,
    suffix: str,
    kind: str,
    x: float,
    y: float,
    w: float,
    h: float,
    z: int,
    sprite_file: str | None,
    color: tuple[float, float, float, float],
) -> tuple[str, int]:
    go = make_id(used, pond_id, spot_id, suffix, "go")
    rt = make_id(used, pond_id, spot_id, suffix, "rt")
    cr = make_id(used, pond_id, spot_id, suffix, "cr")
    img = make_id(used, pond_id, spot_id, suffix, "img")
    marker = make_id(used, pond_id, spot_id, suffix, "marker")
    object_id = f"{spot_id}-{suffix}"
    r, g, b, a = color
    sprite_line = f"  spriteFile: {sprite_file}\n" if sprite_file else "  spriteFile: \n"
    fill_type = 3 if kind == "actor-ring" else 0
    fill_amount = 0.75 if kind == "actor-ring" else 1
    fill_origin = 2 if kind == "actor-ring" else 0
    block = f"""--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt}}}
  - component: {{fileID: {cr}}}
  - component: {{fileID: {img}}}
  - component: {{fileID: {marker}}}
  m_Layer: 0
  m_Name: {object_id}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {father_rt}}}
  m_RootOrder: {root_order}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: {x}, y: {-y}}}
  m_SizeDelta: {{x: {w}, y: {h}}}
  m_Pivot: {{x: 0, y: 1}}
--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: {fill_type}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: {fill_amount}
  m_FillClockwise: 1
  m_FillOrigin: {fill_origin}
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &{marker}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {LAYOUT_OBJ_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  objectId: {object_id}
  kind: {kind}
  spotId: {spot_id}
{sprite_line}  zIndex: {z}
  anchor: top-left
"""
    if kind == "actor-name":
        label_go = make_id(used, pond_id, spot_id, suffix, "label-go")
        label_rt = make_id(used, pond_id, spot_id, suffix, "label-rt")
        label_cr = make_id(used, pond_id, spot_id, suffix, "label-cr")
        label_tx = make_id(used, pond_id, spot_id, suffix, "label-tx")
        # patch children of name rt
        block = block.replace(
            f"--- !u!224 &{rt}\nRectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n",
            f"--- !u!224 &{rt}\nRectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {{fileID: {label_rt}}}\n",
        )
        block += f"""--- !u!1 &{label_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {label_rt}}}
  - component: {{fileID: {label_cr}}}
  - component: {{fileID: {label_tx}}}
  m_Layer: 0
  m_Name: Label
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{label_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {rt}}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{label_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{label_tx}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}}
    m_FontSize: 11
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 4
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 0
    m_HorizontalOverflow: 1
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "\\u6635\\u79F0"
"""
    return block, rt


def patch_prefab(path: Path) -> dict:
    pond_id = path.stem
    text = path.read_text(encoding="utf-8")
    used = collect_ids(text)
    spots = find_spot_blocks(text)
    if not spots:
        return {"pond": pond_id, "spots": 0, "updated": 0, "skipped": 0}

    updated = 0
    skipped = 0
    spot_geometry: dict[str, tuple[float, float, float, float]] = {}
    append_blocks: list[str] = []

    for spot in spots:
        spot_id = spot["spot_id"]
        if has_actor_seat(text, spot_id):
            skipped += 1
            # still capture geometry for JSON
            text2, x, y, w, h = expand_spot_rect(text, spot["rt_id"])
            # don't expand if already has seats and wasn't tiny - expand_spot_rect is idempotent for large
            text = text2
            spot_geometry[spot_id] = (x, y, w, h)
            continue

        text, x, y, w, h = expand_spot_rect(text, spot["rt_id"])
        spot_geometry[spot_id] = (x, y, w, h)
        text = set_image_transparent(text, spot["image_id"])
        text = ensure_marker_sprite(text, spot["marker_id"])
        text = add_view_component(text, spot["go_id"], used, spot_id, pond_id)

        child_rts: list[int] = []
        # existing children count ≈ root order start
        # Count existing children in RT
        rt_children_m = re.search(
            r"--- !u!224 &%d\nRectTransform:.*?m_Children:\n((?:  - \{fileID: \d+\}\n)*)" % spot["rt_id"],
            text,
            re.S,
        )
        existing_count = 0
        if rt_children_m:
            existing_count = len(re.findall(r"fileID: (\d+)", rt_children_m.group(1)))
        elif re.search(
            r"--- !u!224 &%d\nRectTransform:.*?m_Children: \[\]" % spot["rt_id"], text, re.S
        ):
            existing_count = 0

        for i, (suffix, kind, px, py, pw, ph, z, sprite, color) in enumerate(PARTS):
            block, rt = emit_part(
                used,
                pond_id,
                spot_id,
                spot["rt_id"],
                existing_count + i,
                suffix,
                kind,
                px,
                py,
                pw,
                ph,
                z,
                sprite,
                color,
            )
            append_blocks.append(block)
            child_rts.append(rt)

        text = add_child_refs(text, spot["rt_id"], child_rts)
        updated += 1

    if append_blocks:
        text = text.rstrip() + "\n" + "".join(append_blocks)
        if not text.endswith("\n"):
            text += "\n"
        path.write_text(text, encoding="utf-8")

    return {
        "pond": pond_id,
        "spots": len(spots),
        "updated": updated,
        "skipped": skipped,
        "geometry": spot_geometry,
    }


def patch_json(pond_id: str, geometry: dict[str, tuple[float, float, float, float]]) -> int:
    wrote = 0
    for json_dir in JSON_DIRS:
        path = json_dir / f"{pond_id}.json"
        if not path.exists():
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        objects = data.get("objects") or []
        # Remove old actor-* for spots we manage; rebuild from geometry.
        kept = []
        for obj in objects:
            kind = (obj.get("kind") or "").strip()
            if kind.startswith("actor-"):
                continue
            kept.append(obj)

        by_id = {o.get("id"): o for o in kept}
        for spot_id, (x, y, w, h) in geometry.items():
            spot = by_id.get(spot_id)
            if spot is None:
                # try find by spotId
                for o in kept:
                    if o.get("kind") == "spot" and o.get("spotId") == spot_id:
                        spot = o
                        break
            if spot is None:
                continue
            # Store bottom-center for spot host.
            spot["w"] = round(w)
            spot["h"] = round(h)
            spot["x"] = round(x + w * 0.5)
            spot["y"] = round(y + h)
            spot["anchor"] = "bottom-center"
            if not spot.get("sprite"):
                spot["sprite"] = "seats/_default.png"

            for suffix, kind, px, py, pw, ph, z, sprite, _color in PARTS:
                entry = {
                    "id": f"{spot_id}-{suffix}",
                    "kind": kind,
                    "spotId": spot_id,
                    "x": round(x + px),
                    "y": round(y + py),
                    "w": round(pw),
                    "h": round(ph),
                    "z": z,
                    "anchor": "top-left",
                }
                if sprite:
                    entry["sprite"] = sprite
                kept.append(entry)

        data["objects"] = kept
        # Compact JSON matching existing style
        lines = [
            "    " + json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
            for obj in kept
        ]
        body = (
            '{"version":1,"pondId":"%s","canvas":{"width":960,"height":560,"origin":"top-left"},"objects":[\n'
            % pond_id
            + ",\n".join(lines)
            + "\n]}\n"
        )
        path.write_text(body, encoding="utf-8")
        wrote += 1
    return wrote


def main() -> int:
    prefabs = sorted(LAYOUTS_DIR.glob("pond-*.prefab"))
    if not prefabs:
        print("No pond prefabs in", LAYOUTS_DIR, file=sys.stderr)
        return 1

    total_updated = 0
    total_skipped = 0
    for prefab in prefabs:
        result = patch_prefab(prefab)
        geom = result.get("geometry") or {}
        # For skipped spots we still need geometry from prefab/json — re-read via expand only
        if not geom and result["spots"]:
            # Re-run geometry extraction from current file
            text = prefab.read_text(encoding="utf-8")
            used = collect_ids(text)
            spots = find_spot_blocks(text)
            geom = {}
            for spot in spots:
                _, x, y, w, h = expand_spot_rect(text, spot["rt_id"])
                geom[spot["spot_id"]] = (x, y, w, h)
        jn = patch_json(result["pond"], geom) if geom else 0
        total_updated += result["updated"]
        total_skipped += result["skipped"]
        print(
            f"{result['pond']}: spots={result['spots']} nested={result['updated']} "
            f"already={result['skipped']} json_files={jn}"
        )

    print(f"Done. nested_new={total_updated} already_had_seats={total_skipped}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
