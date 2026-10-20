import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


FOLDER = Path(__file__).resolve().parent
ROOT = FOLDER.parents[3]


def read_json(file):
    return json.loads(file.read_text())


def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def rgba(file):
    image = Image.open(file).convert("RGBA")
    assert image.size == (640, 512), file
    return np.array(image)


def main():
    manifest = read_json(FOLDER / "manifest.json")
    plan = read_json(FOLDER / "plan.json")
    assert manifest["name"] == "大肥鱼"
    assert manifest["characterId"] == "dafeiyu"
    assert len(manifest["frames"]) == 63
    assert manifest["uniqueDrawings"] == 44
    assert len({frame["drawing"] for frame in manifest["frames"]}) == 44
    assert manifest["rodOcclusionModel"] == "head-layer-order-v1"
    frames = {frame["id"]: frame for frame in manifest["frames"]}
    originals = {frame["id"]: frame for frame in plan["frames"]}
    assert frames.keys() == originals.keys()
    imports = read_json(FOLDER / "drawing-imports.json")["drawings"]
    assert len(imports) == 38
    for drawing in imports:
        assert drawing["sha256"] == digest(FOLDER / "painted" / (drawing["id"] + ".png"))
    partition_pixels = 0
    props_checked = 0
    for frame in manifest["frames"]:
        original = originals[frame["id"]]
        assert frame["renderLayerOrder"] == original["renderLayerOrder"]
        assert frame["durationMs"] == original["durationMs"]
        folder = FOLDER / frame["layerFolder"]
        character = rgba(folder / "character.png")
        assert np.array_equal(character, rgba(FOLDER / "registered" / (frame["drawing"] + ".png")))
        layers = [rgba(folder / (layer + ".png")) for layer in manifest["bodyLayers"]]
        owners = np.sum([layer[:, :, 3] > 0 for layer in layers], axis=0)
        assert np.array_equal(owners, (character[:, :, 3] > 0).astype(int)), frame["id"]
        merged = np.sum(layers, axis=0)
        occupied = character[:, :, 3] > 0
        assert np.array_equal(merged[occupied], character[occupied]), frame["id"]
        partition_pixels += int(np.count_nonzero(occupied))
        for layer in manifest["sharedLayers"]:
            actual = folder / (layer + ".png")
            source = ROOT / "docs/art/cat-skins" / original["layers"][layer]
            assert digest(actual) == digest(source) == frame["originalPropHashes"][layer], actual
            props_checked += 1
        order = frame["renderLayerOrder"]
        if order.index("body") < order.index("rod") < order.index("hands"):
            assert order.index("far_hand") < order.index("rod") < order.index("head") < order.index("hands"), frame["id"]
        if frame["drawing"] in ["sit-00", "sit-01"]:
            assert frame["showSeat"] is False
        assert (folder / "composite.png").is_file()
    runtime = read_json(FOLDER / "pack/dafeiyu/manifest.json")
    orange = read_json(ROOT / "fish-social-unity/Assets/StreamingAssets/Fishing/orange/manifest.json")
    assert runtime["skinId"] == "dafeiyu" and runtime["skinName"] == "大肥鱼"
    assert runtime["characterArtworkManifestSha256"] == digest(FOLDER / "manifest.json")
    assert runtime["rodOcclusionModel"] == orange["rodOcclusionModel"]
    assert runtime["clips"].keys() == orange["clips"].keys()
    slots = 0
    for clip, definition in runtime["clips"].items():
        for key in ["frames", "durationsMs", "sourceFrames", "playback"]:
            assert definition[key] == orange["clips"][clip][key], (clip, key)
        assert Image.open(FOLDER / "atlases" / (clip + ".png")).size == (320 * len(definition["frames"]), 256)
        for index, origin in enumerate(definition["sourceFrames"]):
            frame = frames[f'{origin["clip"]}-{origin["index"]:02d}']
            assert definition["frames"][index] == frame["renderLayerOrder"]
            assert definition["seatVisible"][index] == frame["showSeat"]
            for layer in manifest["bodyLayers"] + manifest["sharedLayers"]:
                part = "dafeiyu/body" if layer in manifest["bodyLayers"] else "shared"
                asset = FOLDER / "pack" / part / clip / str(index) / (layer + ".png")
                assert digest(asset) == digest(FOLDER / frame["layerFolder"] / (layer + ".png")), asset
            slots += 1
    assert slots == 74 and len(runtime["clips"]) == 16
    for boundary, previous, following in [
        ("sit-ready", "sit-03", "ready-00"),
        ("catch-reset", "catch-06", "reset_catch-00"),
        ("empty-reset", "retrieve_empty-05", "reset_empty-00"),
        ("reset-ready", "reset_catch-03", "ready-00"),
        ("reset-empty-ready", "reset_empty-03", "ready-00"),
    ]:
        assert frames[previous]["drawing"] == frames[following]["drawing"], boundary
    report = dict(name="大肥鱼", uniqueDrawings=44, newlyDrawn=38, retained=6,
                  sourceFrames=63, sourceActions=len({frame["clip"] for frame in manifest["frames"]}),
                  runtimeClips=16, runtimeSlots=slots, originalPropCopiesChecked=props_checked,
                  exactlyPartitionedPixels=partition_pixels, partition="passed", result="passed",
                  scope="Asset data and layer consistency; visual acceptance and Unity playback remain separate.")
    (FOLDER / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
