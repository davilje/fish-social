#!/usr/bin/env python3
"""Generate a Unity Overlay layout Prefab from layouts/<pondId>.json (ART-02)."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
JSON_DIR = REPO / "desktop-overlay" / "OverlayResources" / "layouts"
OUT_DIR = REPO / "fish-social-unity" / "Assets" / "Desktop" / "OverlayLayouts"

VIEW_GUID = "7e4c1a90b2d14f6e8c3a5f7192d0e4b1"
OBJECT_GUID = "8f5d2b01c3e25a7f9d4b6082a3e1f5c2"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"
SCALER_GUID = "0cd44c1031e13a943bb63640046fad76"
RAYCASTER_GUID = "dc42784cf147c0c48a680349fa168899"
FONT = "{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}"


def fid(*parts: str) -> int:
    raw = hashlib.md5("|".join(parts).encode("utf-8")).hexdigest()
    return int(raw[:15], 16)


def top_left(obj: dict) -> tuple[float, float, float, float]:
    w = float(obj.get("w") or 24)
    h = float(obj.get("h") or 24)
    x = float(obj.get("x") or 0)
    y = float(obj.get("y") or 0)
    anchor = (obj.get("anchor") or "top-left").lower()
    if anchor == "bottom-center":
        return x - w * 0.5, y - h, w, h
    if anchor == "center":
        return x - w * 0.5, y - h * 0.5, w, h
    return x, y, w, h


def color_for(kind: str) -> str:
    if kind == "sprite":
        return "{r: 0.16, g: 0.42, b: 0.48, a: 1}"
    if kind == "waiting":
        return "{r: 0.2, g: 0.28, b: 0.22, a: 0.45}"
    if kind == "pet-size":
        return "{r: 0.85, g: 0.55, b: 0.2, a: 0.35}"
    return "{r: 0.95, g: 0.79, b: 0.41, a: 0.92}"


def emit_go(lines: list[str], go: int, comps: list[int], name: str) -> None:
    lines.append(f"--- !u!1 &{go}")
    lines.append("GameObject:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append("  serializedVersion: 6")
    lines.append("  m_Component:")
    for c in comps:
        lines.append(f"  - component: {{fileID: {c}}}")
    lines.append("  m_Layer: 0")
    lines.append(f"  m_Name: {name}")
    lines.append("  m_TagString: Untagged")
    lines.append("  m_Icon: {fileID: 0}")
    lines.append("  m_NavMeshLayer: 0")
    lines.append("  m_StaticEditorFlags: 0")
    lines.append("  m_IsActive: 1")


def emit_rt(
    lines: list[str],
    rt: int,
    go: int,
    children: list[int],
    father: int,
    order: int,
    ax: float,
    ay: float,
    w: float,
    h: float,
    pivot_zero: bool,
) -> None:
    lines.append(f"--- !u!224 &{rt}")
    lines.append("RectTransform:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
    lines.append("  m_LocalPosition: {x: 0, y: 0, z: 0}")
    lines.append("  m_LocalScale: {x: 1, y: 1, z: 1}")
    lines.append("  m_ConstrainProportionsScale: 0")
    lines.append("  m_Children:")
    if children:
        for c in children:
            lines.append(f"  - {{fileID: {c}}}")
    else:
        lines.append("    []")
    lines.append(f"  m_Father: {{fileID: {father}}}")
    lines.append(f"  m_RootOrder: {order}")
    lines.append("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}")
    if pivot_zero:
        lines.append("  m_AnchorMin: {x: 0, y: 0}")
        lines.append("  m_AnchorMax: {x: 0, y: 0}")
        lines.append("  m_AnchoredPosition: {x: 0, y: 0}")
        lines.append(f"  m_SizeDelta: {{x: {w:g}, y: {h:g}}}")
        lines.append("  m_Pivot: {x: 0, y: 0}")
    else:
        lines.append("  m_AnchorMin: {x: 0, y: 1}")
        lines.append("  m_AnchorMax: {x: 0, y: 1}")
        lines.append(f"  m_AnchoredPosition: {{x: {ax:g}, y: {-ay:g}}}")
        lines.append(f"  m_SizeDelta: {{x: {w:g}, y: {h:g}}}")
        lines.append("  m_Pivot: {x: 0, y: 1}")


def emit_stretch_rt(lines: list[str], rt: int, go: int, father: int) -> None:
    lines.append(f"--- !u!224 &{rt}")
    lines.append("RectTransform:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
    lines.append("  m_LocalPosition: {x: 0, y: 0, z: 0}")
    lines.append("  m_LocalScale: {x: 1, y: 1, z: 1}")
    lines.append("  m_ConstrainProportionsScale: 0")
    lines.append("  m_Children: []")
    lines.append(f"  m_Father: {{fileID: {father}}}")
    lines.append("  m_RootOrder: 0")
    lines.append("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}")
    lines.append("  m_AnchorMin: {x: 0, y: 0}")
    lines.append("  m_AnchorMax: {x: 1, y: 1}")
    lines.append("  m_AnchoredPosition: {x: 0, y: 0}")
    lines.append("  m_SizeDelta: {x: 0, y: 0}")
    lines.append("  m_Pivot: {x: 0.5, y: 0.5}")


def emit_canvas_renderer(lines: list[str], cr: int, go: int) -> None:
    lines.append(f"--- !u!222 &{cr}")
    lines.append("CanvasRenderer:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_CullTransparentMesh: 1")


def emit_image(lines: list[str], img: int, go: int, color: str) -> None:
    lines.append(f"--- !u!114 &{img}")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}")
    lines.append("  m_Name: ")
    lines.append("  m_EditorClassIdentifier: ")
    lines.append("  m_Material: {fileID: 0}")
    lines.append(f"  m_Color: {color}")
    lines.append("  m_RaycastTarget: 1")
    lines.append("  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}")
    lines.append("  m_Maskable: 1")
    lines.append("  m_OnCullStateChanged:")
    lines.append("    m_PersistentCalls:")
    lines.append("      m_Calls: []")
    lines.append("  m_Sprite: {fileID: 0}")
    lines.append("  m_Type: 0")
    lines.append("  m_PreserveAspect: 0")
    lines.append("  m_FillCenter: 1")
    lines.append("  m_FillMethod: 4")
    lines.append("  m_FillAmount: 1")
    lines.append("  m_FillClockwise: 1")
    lines.append("  m_FillOrigin: 0")
    lines.append("  m_UseSpriteMesh: 0")
    lines.append("  m_PixelsPerUnitMultiplier: 1")


def emit_text(lines: list[str], txt: int, go: int, text: str, size: int) -> None:
    lines.append(f"--- !u!114 &{txt}")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}")
    lines.append("  m_Name: ")
    lines.append("  m_EditorClassIdentifier: ")
    lines.append("  m_Material: {fileID: 0}")
    lines.append("  m_Color: {r: 1, g: 1, b: 1, a: 0.85}")
    lines.append("  m_RaycastTarget: 0")
    lines.append("  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}")
    lines.append("  m_Maskable: 1")
    lines.append("  m_OnCullStateChanged:")
    lines.append("    m_PersistentCalls:")
    lines.append("      m_Calls: []")
    lines.append("  m_FontData:")
    lines.append(f"    m_Font: {FONT}")
    lines.append(f"    m_FontSize: {size}")
    lines.append("    m_FontStyle: 0")
    lines.append("    m_BestFit: 0")
    lines.append("    m_MinSize: 4")
    lines.append("    m_MaxSize: 40")
    lines.append("    m_Alignment: 4")
    lines.append("    m_AlignByGeometry: 0")
    lines.append("    m_RichText: 0")
    lines.append("    m_HorizontalOverflow: 1")
    lines.append("    m_VerticalOverflow: 1")
    lines.append("    m_LineSpacing: 1")
    lines.append(f"  m_Text: {text}")


def emit_marker(lines: list[str], mb: int, go: int, obj: dict) -> None:
    kind = obj.get("kind") or "sprite"
    lines.append(f"--- !u!114 &{mb}")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {go}}}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {OBJECT_GUID}, type: 3}}")
    lines.append("  m_Name: ")
    lines.append("  m_EditorClassIdentifier: ")
    lines.append(f"  objectId: {obj.get('id') or ''}")
    lines.append(f"  kind: {kind}")
    lines.append(f"  spotId: {obj.get('spotId') or ''}")
    lines.append(f"  spriteFile: {obj.get('sprite') or ''}")
    lines.append(f"  zIndex: {int(obj.get('z') or 0)}")
    default_anchor = "bottom-center" if kind == "spot" else "top-left"
    lines.append(f"  anchor: {obj.get('anchor') or default_anchor}")


def generate(pond_id: str) -> Path:
    src = JSON_DIR / f"{pond_id}.json"
    doc = json.loads(src.read_text(encoding="utf-8"))
    objects = doc.get("objects") or []
    root_go = fid(pond_id, "go")
    root_rt = fid(pond_id, "rt")
    root_canvas = fid(pond_id, "canvas")
    root_scaler = fid(pond_id, "scaler")
    root_ray = fid(pond_id, "ray")
    root_view = fid(pond_id, "view")

    child_rts = []
    child_blocks: list[list[str]] = []
    for i, obj in enumerate(objects):
        oid = obj.get("id") or f"obj-{i}"
        go = fid(pond_id, oid, "go")
        rt = fid(pond_id, oid, "rt")
        cr = fid(pond_id, oid, "cr")
        img = fid(pond_id, oid, "img")
        mb = fid(pond_id, oid, "mb")
        label_go = fid(pond_id, oid, "label-go")
        label_rt = fid(pond_id, oid, "label-rt")
        label_cr = fid(pond_id, oid, "label-cr")
        label_txt = fid(pond_id, oid, "label-txt")
        child_rts.append(rt)
        ax, ay, w, h = top_left(obj)
        kind = obj.get("kind") or "sprite"
        block: list[str] = []
        emit_go(block, go, [rt, cr, img, mb], oid)
        emit_rt(block, rt, go, [label_rt], root_rt, i, ax, ay, w, h, False)
        emit_canvas_renderer(block, cr, go)
        emit_image(block, img, go, color_for(kind))
        emit_marker(block, mb, go, obj)
        emit_go(block, label_go, [label_rt, label_cr, label_txt], "Label")
        emit_stretch_rt(block, label_rt, label_go, rt)
        emit_canvas_renderer(block, label_cr, label_go)
        emit_text(block, label_txt, label_go, oid, 8 if kind == "spot" else 12)
        child_blocks.append(block)

    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    emit_go(lines, root_go, [root_rt, root_canvas, root_scaler, root_ray, root_view], pond_id)
    emit_rt(lines, root_rt, root_go, child_rts, 0, 0, 0, 0, 960, 560, True)
    lines += [
        f"--- !u!223 &{root_canvas}",
        "Canvas:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root_go}}}",
        "  m_Enabled: 1",
        "  serializedVersion: 3",
        "  m_RenderMode: 2",
        "  m_Camera: {fileID: 0}",
        "  m_PlaneDistance: 100",
        "  m_PixelPerfect: 0",
        "  m_ReceivesEvents: 1",
        "  m_OverrideSorting: 0",
        "  m_OverridePixelPerfect: 0",
        "  m_SortingBucketNormalizedSize: 0",
        "  m_AdditionalShaderChannelsFlag: 0",
        "  m_SortingLayerID: 0",
        "  m_SortingOrder: 0",
        "  m_TargetDisplay: 0",
        f"--- !u!114 &{root_scaler}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root_go}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {SCALER_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_UiScaleMode: 0",
        "  m_ReferencePixelsPerUnit: 100",
        "  m_ScaleFactor: 1",
        "  m_ReferenceResolution: {x: 960, y: 560}",
        "  m_ScreenMatchMode: 0",
        "  m_MatchWidthOrHeight: 0",
        "  m_PhysicalUnit: 3",
        "  m_FallbackScreenDPI: 96",
        "  m_DefaultSpriteDPI: 96",
        "  m_DynamicPixelsPerUnit: 1",
        "  m_PresetInfoIsWorld: 1",
        f"--- !u!114 &{root_ray}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root_go}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {RAYCASTER_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_IgnoreReversedGraphics: 1",
        "  m_BlockingObjects: 0",
        "  m_BlockingMask:",
        "    serializedVersion: 2",
        "    m_Bits: 4294967295",
        f"--- !u!114 &{root_view}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root_go}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {VIEW_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        f"  pondId: {pond_id}",
    ]
    for block in child_blocks:
        lines.extend(block)

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    prefab = OUT_DIR / f"{pond_id}.prefab"
    prefab.write_text("\n".join(lines) + "\n", encoding="utf-8")
    meta = OUT_DIR / f"{pond_id}.prefab.meta"
    guid = hashlib.md5(f"overlay-layout-{pond_id}".encode("utf-8")).hexdigest()
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "PrefabImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )
    return prefab


if __name__ == "__main__":
    pond = sys.argv[1] if len(sys.argv) > 1 else "pond-calm"
    path = generate(pond)
    print(path)
