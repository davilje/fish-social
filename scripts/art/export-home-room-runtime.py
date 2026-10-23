#!/usr/bin/env python3
"""Export screen-sized room textures from retained masters; never redraw the art."""
import argparse
import hashlib
import json
import math
import shutil
from pathlib import Path

from PIL import Image


def export(repo: Path):
    source = repo / "docs/design/ui/prototype/cozy-journal/assets/room-v4"
    target = repo / "fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room"
    target.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((source / "manifest.json").read_text())
    rows = []
    for key, entry in manifest["assets"].items():
        placements = [p for p in manifest["placements"] if p["asset"] == key]
        image = Image.open(source / entry["file"]).convert("RGBA")
        before = image.size
        w = max(p["width"] for p in placements)
        h = max(p["height"] for p in placements)
        if all(p.get("fit") == "fill" for p in placements):
            size = (min(before[0], math.ceil(w * 2)), min(before[1], math.ceil(h * 2)))
        else:
            ratio = min(1, w * 2 / before[0], h * 2 / before[1])
            size = (max(1, math.ceil(before[0] * ratio)), max(1, math.ceil(before[1] * ratio)))
        # Premultiplied-alpha resampling prevents dark/white fringes on transparent edges.
        image = image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
        path = target / entry["file"]
        image.save(path, optimize=True)
        row = {"id": key, "file": entry["file"], "source": list(before), "runtime": list(size),
               "maxLayoutBox": [w, h], "rgbaBytesBefore": before[0] * before[1] * 4,
               "rgbaBytesAfter": size[0] * size[1] * 4, "fileBytes": path.stat().st_size,
               "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
        rows.append(row)
        entry.update(width=size[0], height=size[1], masterSize=list(before), runtimeDensity=2)
    Image.open(source / "room-base.png").save(target / "room-base.png", optimize=True)
    prototype = source.parent
    for pose in ["standing", "sitting"]:
        image = Image.open(prototype / ("room-cat-" + pose + ".png")).convert("RGBA")
        image.crop(image.getbbox()).save(target / ("cat-" + pose + ".png"), optimize=True)
    # Reuse every registered cat's existing sleep pose, trimming unused transparent canvas.
    poses = target / "poses"
    poses.mkdir(exist_ok=True)
    sleep = []
    for path in sorted((target.parent / "journal/characters").glob("*/sleep.png")):
        image = Image.open(path).convert("RGBA")
        cropped = image.crop(image.getbbox())
        name = path.parent.name + "-sleep.png"
        cropped.save(poses / name, optimize=True)
        sleep.append({"skin": path.parent.name, "file": "poses/" + name,
                      "width": cropped.width, "height": cropped.height, "source": str(path.relative_to(repo))})
    manifest["runtimeExport"] = {"density": 2, "masterFolder": str(source.relative_to(repo)),
                                 "note": "2x maximum layout size; transparent padding trimmed; rug resampled to its existing floor projection"}
    manifest["sleepPoses"] = sleep
    (target / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    before = sum(r["rgbaBytesBefore"] for r in rows)
    after = sum(r["rgbaBytesAfter"] for r in rows)
    report = {"density": 2, "count": len(rows), "rgbaBytesBefore": before, "rgbaBytesAfter": after,
              "reductionPercent": round(100 * (1 - after / before), 1), "assets": rows, "sleepPoses": sleep}
    (target / "texture-budget.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    web = source.parent / "room-runtime"
    web.mkdir(exist_ok=True)
    for path in target.rglob("*"):
        if path.suffix not in [".png", ".json"]:
            continue
        dest = web / path.relative_to(target)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, dest)
    Image.open(target / "room-base.png").convert("RGB").save(web / "room-base.jpg", quality=92, optimize=True)
    print(json.dumps({k: report[k] for k in ["count", "rgbaBytesBefore", "rgbaBytesAfter", "reductionPercent"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    export(parser.parse_args().repo)
