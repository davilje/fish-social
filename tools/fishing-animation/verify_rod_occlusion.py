"""Verify complete shared rods and character-specific occlusion across all breeds."""

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
STUDY = ROOT / "docs/art/cat-skins"
FISHING = ROOT / "fish-social-unity/Assets/StreamingAssets/Fishing"
OVERLAY = ROOT / "desktop-overlay/OverlayResources/fishing"
SKINS = ("orange", "calico", "silver", "brown", "cream", "blue", "black", "white", "cow")
MODEL = "head-layer-order-v1"


def read_json(filename):
    return json.loads(filename.read_text(encoding="utf-8"))


def digest(filename):
    return hashlib.sha256(filename.read_bytes()).hexdigest()


def rgba(filename):
    with Image.open(filename) as source:
        assert source.size == (640, 512), filename
        assert source.mode == "RGBA", filename
        return source.copy()


def main():
    recovered = read_json(STUDY / "rod-source/manifest.json")
    assert recovered["model"] == MODEL
    assert recovered["characterGeometryUnchanged"]
    source_by_id = {frame["id"]: frame for frame in recovered["frames"]}
    assert len(source_by_id) == 63
    source = read_json(FISHING / "source-animation-v19.json")
    source_hash = digest(FISHING / "source-animation-v19.json")
    assert (FISHING / "source-animation-v19.json").read_bytes() == (OVERLAY / "source-animation-v19.json").read_bytes()
    checked_frames = 0
    gripping_frames = 0
    occluded_pixels = 0
    exposed_pixels = 0
    for skin in SKINS:
        manifest_file = Path(skin) / "manifest.json"
        assert (FISHING / manifest_file).read_bytes() == (OVERLAY / manifest_file).read_bytes(), skin
        manifest = read_json(FISHING / manifest_file)
        assert manifest["rodOcclusionModel"] == MODEL, skin
        assert manifest["sourceManifestSha256"] == source_hash, skin
        assert manifest["rodSourceManifestSha256"] == digest(STUDY / "rod-source/manifest.json"), skin
        for clip, definition in manifest["clips"].items():
            for index, order in enumerate(definition["frames"]):
                label = f"{skin}/{clip}/{index}"
                origin = definition["sourceFrames"][index]
                source_id = f"{origin['clip']}-{origin['index']:02d}"
                complete = source_by_id[source_id]
                assert order == complete["renderLayerOrder"], label
                assert set(order) == set(manifest["layers"]) and len(order) == 9, label
                rod_relative = Path("shared") / clip / str(index) / "rod.png"
                assert digest(FISHING / rod_relative) == complete["sha256"], label
                assert (FISHING / rod_relative).read_bytes() == (OVERLAY / rod_relative).read_bytes(), label
                images = {}
                for layer in order:
                    relative = (Path(skin) / "body" if layer in manifest["bodyLayers"] else Path("shared")) / clip / str(index) / f"{layer}.png"
                    images[layer] = rgba(FISHING / relative)
                alphas = {layer: np.asarray(image)[:, :, 3] for layer, image in images.items()}
                ownership = sum((alphas[layer] > 0).astype(np.uint8) for layer in manifest["bodyLayers"])
                assert not np.any(ownership > 1), f"Overlapping character parts: {label}"
                if order.index("body") < order.index("rod"):
                    assert order.index("rod") < order.index("head"), label
                authored = source["clips"][origin["clip"]]["frames"][origin["index"]]
                if authored["rig"].get("gripMode") == "far-hand-rod-near-hand":
                    assert authored["rig"]["rodHeadOcclusionBaked"] is False, label
                    assert order.index("far_hand") < order.index("rod") < order.index("hands"), label
                    gripping_frames += 1
                if order.index("rod") < order.index("head"):
                    result = Image.new("RGBA", (640, 512))
                    for layer in order:
                        result.alpha_composite(images[layer])
                    exposed = alphas["rod"] > 0
                    for layer in order[order.index("rod") + 1:]:
                        exposed &= alphas[layer] == 0
                    exposed_pixels += int(np.count_nonzero(exposed))
                    opaque_head = alphas["head"] == 255
                    for layer in order[order.index("head") + 1:]:
                        opaque_head &= alphas[layer] == 0
                    assert np.array_equal(np.asarray(result)[opaque_head], np.asarray(images["head"])[opaque_head]), label
                    occluded_pixels += int(np.count_nonzero(opaque_head & (alphas["rod"] > 0)))
                checked_frames += 1
    assert checked_frames == 666
    assert gripping_frames > 0 and occluded_pixels > 0 and exposed_pixels > 0
    trial_root = ROOT / "docs/archive/2026-10-26/superseded/art/whale-maid/template-study-v1"
    trial = read_json(trial_root / "manifest.json")
    assert trial["rodOcclusionModel"] == MODEL
    for frame in trial["frames"]:
        expected = source_by_id[frame["id"]]
        assert frame["renderLayerOrder"] == expected["renderLayerOrder"]
        assert digest(trial_root / frame["layerFolder"] / "rod.png") == expected["sha256"]
        assert frame["partitionExact"] is True
    report = dict(model=MODEL, skins=len(SKINS), runtimeFrames=checked_frames,
                  grippingFrames=gripping_frames, opaqueHeadOcclusionPixels=occluded_pixels,
                  exposedRodPixels=exposed_pixels, whaleTrialFrames=len(trial["frames"]),
                  result="passed", scope="Layer assets and compositions; no Unity or Windows Player execution")
    (STUDY / "rod-occlusion-verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
