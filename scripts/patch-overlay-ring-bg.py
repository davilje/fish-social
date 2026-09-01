#!/usr/bin/env python3
"""Insert actor-ring-bg objects next to actor-ring in pond layout JSON."""
import json
from pathlib import Path

ROOTS = [
    Path("desktop-overlay/OverlayResources/layouts"),
    Path("fish-social-unity/FishSocialOverlay/OverlayResources/layouts"),
]


def dump_layout(data):
    objs = data.get("objects") or []
    header = {
        "version": data.get("version", 1),
        "pondId": data.get("pondId"),
        "canvas": data.get("canvas"),
    }
    if data.get("pond") is not None:
        header["pond"] = data["pond"]
    head = json.dumps(header, ensure_ascii=False, separators=(",", ":"))
    # {"version":1,...,"pond":{...}}  ->  {"version":1,...,"pond":{...},"objects":[
    if head.endswith("}"):
        head = head[:-1] + ',"objects":['
    lines = [head]
    for i, obj in enumerate(objs):
        comma = "," if i < len(objs) - 1 else ""
        lines.append("    " + json.dumps(obj, ensure_ascii=False, separators=(",", ":")) + comma)
    lines.append("]}")
    return "\n".join(lines) + "\n"


def patch_file(path: Path):
    data = json.loads(path.read_text(encoding="utf-8"))
    objs = data.get("objects") or []
    have_bg = {
        o.get("spotId")
        for o in objs
        if isinstance(o, dict) and o.get("kind") == "actor-ring-bg"
    }
    new_objs = []
    inserted = 0
    for o in objs:
        if isinstance(o, dict) and o.get("kind") == "actor-ring":
            sid = o.get("spotId")
            if sid and sid not in have_bg:
                oid = o.get("id") or ""
                bg_id = oid[:-5] + "-ring-bg" if oid.endswith("-ring") else sid + "-ring-bg"
                new_objs.append(
                    {
                        "id": bg_id,
                        "kind": "actor-ring-bg",
                        "spotId": sid,
                        "x": o.get("x"),
                        "y": o.get("y"),
                        "w": o.get("w"),
                        "h": o.get("h"),
                        "z": 11,
                        "anchor": o.get("anchor") or "top-left",
                        "sprite": "ring-bg.png",
                    }
                )
                have_bg.add(sid)
                inserted += 1
        new_objs.append(o)
    if inserted == 0:
        return 0
    data["objects"] = new_objs
    path.write_text(dump_layout(data), encoding="utf-8")
    return inserted


def main():
    total_files = 0
    total_inserted = 0
    for root in ROOTS:
        if not root.is_dir():
            continue
        for path in sorted(root.glob("pond-*.json")):
            n = patch_file(path)
            if n:
                total_files += 1
                total_inserted += n
                print("%s +%s" % (path, n))
    print("files %s inserted %s" % (total_files, total_inserted))


if __name__ == "__main__":
    main()
