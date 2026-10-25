"""Copy animation-studio layers into Overlay fishing resources.

Reads animation.json for per-frame layerOrder. Skips fully transparent PNGs.
Does not rebuild poses or bend the rod.
"""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from PIL import Image

LAYERS = ["cat", "rod", "hands", "line", "bobber", "fish", "bait"]
BODY = {"cat", "hands"}
DEFAULT = ["cat", "rod", "hands", "line", "bobber", "fish", "bait"]
ROD_BACK = ["rod", "cat", "hands", "line", "bobber", "fish", "bait"]
RETRIEVE = ["rod", "line", "fish", "cat", "hands", "bobber", "bait"]
HOLD_FISH = ["rod", "line", "cat", "fish", "hands", "bobber", "bait"]

# Runtime clip, playback, loopStart, (studio phase, start, count) segments.
# sit is the studio ready pose, so seated uses the same cat as the fishing clips.
CLIPS = [
    ("sit", "hold", None, [("ready", 0, 1)]),
    ("bait", "once", None, [("bait", 0, 4)]),
    ("cast", "once", None, [("cast", 0, 4)]),
    ("fishing", "hold", None, [("wait", 0, 1)]),
    ("hooked", "intro-loop", 4, [("bite", 0, 4), ("effort", 0, 6)]),
    ("reel", "once", None, [("retrieve", 0, 6)]),
    ("catch", "once", None, [("catch", 0, 7)]),
]

# Paw contact and ear-to-paw height of pets/orange/sit/0.png (512 square, Stretch.Uniform).
REFERENCE = 512
REFERENCE_FOOT = (139.5, 399)
REFERENCE_BODY = 279


def measure_cat(path: Path) -> tuple:
    with Image.open(path) as image:
        rgba = image.convert("RGBA")
        pixels = rgba.load()
        width, height = rgba.size
        top = None
        foot_y = None
        foot_mid = None
        for y in range(height):
            row = [x for x in range(width) if pixels[x, y][3] > 20]
            if not row:
                continue
            if top is None:
                top = y
            longest = 1
            run = 1
            best = (row[0], row[0])
            for index in range(1, len(row)):
                if row[index] == row[index - 1] + 1:
                    run += 1
                else:
                    if run > longest:
                        longest = run
                        best = (row[index - run], row[index - 1])
                    run = 1
            if run > longest:
                longest = run
                best = (row[-run], row[-1])
            if longest >= 12:
                foot_y = y
                foot_mid = (best[0] + best[1]) / 2.0
        if top is None or foot_y is None:
            raise SystemExit(f"could not measure cat feet in {path}")
        return int(round(foot_mid)), int(foot_y), int(foot_y - top)


def is_blank(path: Path) -> bool:
    with Image.open(path) as image:
        rgba = image.convert("RGBA")
        alpha = rgba.getextrema()[3]
        return alpha[1] == 0


def studio_frame(animation: dict, phase: str, index: int) -> dict:
    frames = animation["clips"][phase]["frames"]
    if index < 0 or index >= len(frames):
        raise SystemExit(f"{phase} frame {index} missing")
    frame = frames[index]
    order = frame.get("layerOrder") or animation["layerOrder"]
    if list(order) != list(LAYERS) and sorted(order) != sorted(LAYERS):
        raise SystemExit(f"{phase}:{index} layerOrder is not the seven layers: {order}")
    if len(order) != 7 or len(set(order)) != 7:
        raise SystemExit(f"{phase}:{index} layerOrder must list each layer once")
    return frame


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--dest", type=Path, required=True)
    args = parser.parse_args()
    source = args.source
    animation_path = source / "animation.json"
    if not animation_path.is_file():
        raise SystemExit(f"missing {animation_path}")

    animation = json.loads(animation_path.read_text(encoding="utf-8"))
    if args.dest.exists():
        shutil.rmtree(args.dest)

    manifest_clips = {}
    copied = 0
    skipped = 0
    for clip_name, playback, loop_start, segments in CLIPS:
        orders = []
        runtime_index = 0
        for phase, start, count in segments:
            for offset in range(count):
                frame = studio_frame(animation, phase, start + offset)
                order = list(frame["layerOrder"])
                orders.append(order)
                for layer in LAYERS:
                    src = source / frame["layers"][layer]
                    if not src.is_file():
                        raise SystemExit(f"missing layer file {src}")
                    if is_blank(src):
                        skipped += 1
                        continue
                    folder = "body" if layer in BODY else "shared"
                    if folder == "body":
                        dest = (
                            args.dest
                            / "orange"
                            / "body"
                            / clip_name
                            / str(runtime_index)
                            / f"{layer}.png"
                        )
                    else:
                        dest = (
                            args.dest
                            / "shared"
                            / clip_name
                            / str(runtime_index)
                            / f"{layer}.png"
                        )
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src, dest)
                    copied += 1
                runtime_index += 1
        entry = {"playback": playback, "frames": orders}
        if loop_start is not None:
            entry["loopStart"] = loop_start
        manifest_clips[clip_name] = entry

    expect(manifest_clips)
    ready_cat = source / "clips" / "ready" / "layers" / "cat" / "frame-00.png"
    foot_x, foot_y, body_height = measure_cat(ready_cat)
    manifest = {
        "version": 1,
        "canvas": [640, 512],
        "layers": LAYERS,
        "bodyLayers": ["cat", "hands"],
        "place": {
            "foot": [foot_x, foot_y],
            "bodyHeight": body_height,
            "reference": REFERENCE,
            "referenceFoot": [REFERENCE_FOOT[0], REFERENCE_FOOT[1]],
            "referenceBody": REFERENCE_BODY,
        },
        "clips": manifest_clips,
    }
    manifest_path = args.dest / "orange" / "manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"copied {copied} png, skipped {skipped} blank, manifest {manifest_path}")


def expect(clips: dict) -> None:
    def same(clip: str, index: int, order: list) -> None:
        got = clips[clip]["frames"][index]
        if got != order:
            raise SystemExit(f"{clip}[{index}] order {got} != {order}")

    same("sit", 0, DEFAULT)
    for index in range(4):
        same("bait", index, DEFAULT)
    same("cast", 0, ROD_BACK)
    same("cast", 1, ROD_BACK)
    same("cast", 2, DEFAULT)
    same("cast", 3, DEFAULT)
    same("fishing", 0, DEFAULT)
    for index in range(10):
        same("hooked", index, DEFAULT)
    if clips["hooked"]["loopStart"] != 4:
        raise SystemExit("hooked loopStart must be 4")
    for index in range(6):
        same("reel", index, RETRIEVE)
    same("catch", 0, RETRIEVE)
    for index in range(1, 7):
        same("catch", index, HOLD_FISH)


if __name__ == "__main__":
    main()
