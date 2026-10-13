"""Read-only, bounded validation of the staged animation pack; not a Unity test.

Run with Python providing Pillow and NumPy. The default pack is pack/ beside this script.
An incomplete pack exits 2 immediately, without polling or writing a report.
Only verification-report.json beside this script may be written by a full run.
"""

import argparse
import hashlib
import json
import math
import sys
import unittest
from datetime import datetime, timezone
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image


HERE = Path(__file__).resolve().parent
SOURCE_ROOT = HERE.parent / "output" / "animation-studio"
PACK_ROOT = HERE / "pack"
BASELINE_MANIFEST = None
SIZE = (640, 512)
LAYERS = ("body", "head", "far_hand", "rod", "line", "fish", "hands", "bobber", "bait")
BODY_LAYERS = ("body", "head", "far_hand", "hands")
GEAR_LAYERS = tuple(layer for layer in LAYERS if layer not in BODY_LAYERS)
SKINS = ("orange", "silver", "brown", "cream", "blue")
SOURCE_COUNTS = {
    "sit": 4, "ready": 1, "bait": 4, "cast": 4, "wait": 1, "bite": 4,
    "effort": 6, "retrieve": 6, "retrieve_empty": 6, "catch": 7,
    "reset_catch": 4, "reset_empty": 4,
}
SOURCE_MAP = {
    "idle": [("sit", 0)],
    "sit": [("sit", index) for index in range(4)],
    "ready": [("ready", 0)],
    "bait": [("bait", index) for index in range(4)],
    "cast": [("cast", index) for index in range(4)],
    "fishing": [("wait", 0)],
    "bite": [("bite", index) for index in range(4)],
    "effort": [("effort", index) for index in range(6)],
    "hooked": [("bite", index) for index in range(4)]
    + [("effort", index) for index in range(6)],
    "reel": [("retrieve", index) for index in range(6)],
    "reel_empty": [("retrieve_empty", index) for index in range(6)],
    "catch": [("catch", index) for index in range(7)],
    "reset_catch": [("reset_catch", index) for index in range(4)],
    "reset_empty": [("reset_empty", index) for index in range(4)],
}
INDEX_COUNT = sum(len(frames) for frames in SOURCE_MAP.values())
OLD_PLACE = {
    "foot": [264, 450], "bodyHeight": 257, "reference": 512,
    "referenceFoot": [138.5, 404], "referenceBody": 279,
}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_path(relative):
    path = SOURCE_ROOT / relative
    if not path.resolve().is_relative_to(SOURCE_ROOT.resolve()):
        raise AssertionError(f"Source path escapes source root: {relative}")
    return path


def layer_path(skin, clip, index, layer):
    if layer in BODY_LAYERS:
        return PACK_ROOT / skin / "body" / clip / str(index) / f"{layer}.png"
    return PACK_ROOT / "shared" / clip / str(index) / f"{layer}.png"


def expected_pngs():
    return {
        layer_path(skin, clip, index, layer)
        for skin in SKINS for clip, frames in SOURCE_MAP.items()
        for index in range(len(frames)) for layer in LAYERS
    }


def read_rgba(path, size=SIZE):
    with Image.open(path) as image:
        if image.format != "PNG" or image.mode != "RGBA" or image.size != size:
            raise AssertionError(
                f"{path}: expected PNG RGBA {size}, got "
                f"{image.format} {image.mode} {image.size}"
            )
        return image.copy()


@lru_cache(maxsize=4096)
def png_signature(path):
    image = read_rgba(path)
    alpha = np.asarray(image.getchannel("A"))
    if np.any(alpha[0]) or np.any(alpha[-1]) or np.any(alpha[:, 0]) or np.any(alpha[:, -1]):
        raise AssertionError(f"{path}: nontransparent canvas boundary")
    return hashlib.sha256(image.tobytes()).hexdigest()


class SourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = read_json(SOURCE_ROOT / "animation.json")

    def test_source_contract_and_atlas_mapping(self):
        source = self.source
        self.assertEqual(source["version"], 18)
        self.assertEqual(source["frameSize"], {"width": 640, "height": 512})
        self.assertEqual(source["renderLayerOrder"], list(LAYERS))
        self.assertEqual(source["phaseOrder"], list(SOURCE_COUNTS))
        self.assertEqual(set(source["clips"]), set(SOURCE_COUNTS))
        self.assertEqual(source["characterMaskModel"]["id"], "pose-contours-v1")
        self.assertEqual(source["characterMaskModel"]["parts"], list(BODY_LAYERS))
        self.assertEqual([skin["id"] for skin in source["skins"]], list(SKINS))
        keys = []
        atlas_indices = []
        for name, count in SOURCE_COUNTS.items():
            clip = source["clips"][name]
            self.assertEqual(clip["frameCount"], count)
            self.assertEqual(len(clip["frames"]), count)
            elapsed = 0
            for index, frame in enumerate(clip["frames"]):
                with self.subTest(clip=name, frame=index):
                    self.assertEqual(frame["index"], index)
                    self.assertEqual(frame["startMs"], elapsed)
                    self.assertIs(type(frame["durationMs"]), int)
                    self.assertGreater(frame["durationMs"], 0)
                    elapsed += frame["durationMs"]
                    self.assertEqual(frame["rig"]["maskModel"], "pose-contours-v1")
                    self.assertCountEqual(frame["renderLayerOrder"], LAYERS)
                    self.assertNotIn("cat", frame["renderLayerOrder"])
                    keys.append(f"{name}:{index}")
                    atlas_indices.append(frame["atlasIndex"])
            self.assertEqual(elapsed, clip["durationMs"])
        self.assertEqual(sum(SOURCE_COUNTS.values()), 51)
        self.assertEqual(atlas_indices, list(range(51)))
        self.assertEqual(len(SOURCE_MAP), 14)
        self.assertEqual(INDEX_COUNT, 62)
        for skin in source["skins"]:
            with self.subTest(skin=skin["id"]):
                self.assertEqual((skin["width"], skin["height"]), SIZE)
                self.assertEqual((skin["columns"], skin["rows"]), (6, 9))
                self.assertEqual(skin["maskModel"], "pose-contours-v1")
                self.assertEqual(skin["frameKeys"], keys)
                self.assertEqual(set(skin["layers"]), set(BODY_LAYERS))
                for layer in BODY_LAYERS:
                    self.assertEqual(skin["layers"][layer], f"skins/{skin['id']}/{layer}-sheet.png")

    def test_original_pixel_ownership_and_final_composites(self):
        for name, clip in self.source["clips"].items():
            for frame in clip["frames"]:
                with self.subTest(clip=name, frame=frame["index"]):
                    parts = [np.asarray(read_rgba(source_path(frame["layers"][layer])))
                             for layer in BODY_LAYERS]
                    owners = np.zeros((SIZE[1], SIZE[0]), dtype=np.uint8)
                    merged = np.zeros((SIZE[1], SIZE[0], 4), dtype=np.uint8)
                    for part in parts:
                        visible = part[:, :, 3] > 0
                        owners += visible
                        merged[visible] = part[visible]
                    self.assertFalse(np.any(owners > 1), "Character layers overlap")
                    original = np.asarray(read_rgba(source_path(frame["sourceCharacter"])))
                    np.testing.assert_array_equal(merged[:, :, 3], original[:, :, 3])
                    visible = original[:, :, 3] > 0
                    np.testing.assert_array_equal(merged[visible], original[visible])
                    composite = Image.new("RGBA", SIZE)
                    for layer in frame["renderLayerOrder"]:
                        composite.alpha_composite(read_rgba(source_path(frame["layers"][layer])))
                    self.assertEqual(composite.tobytes(), read_rgba(source_path(frame["composite"])).tobytes())


class RuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = read_json(SOURCE_ROOT / "animation.json")
        cls.manifests = {skin: read_json(PACK_ROOT / skin / "manifest.json") for skin in SKINS}

    def source_frame(self, clip, index):
        name, source_index = SOURCE_MAP[clip][index]
        return self.source["clips"][name]["frames"][source_index]

    def test_v2_schema_orders_and_optional_provenance(self):
        for skin, manifest in self.manifests.items():
            with self.subTest(skin=skin):
                self.assertEqual(manifest["version"], 2)
                self.assertEqual(manifest["canvas"], list(SIZE))
                self.assertEqual(manifest["layers"], list(LAYERS))
                self.assertEqual(manifest["bodyLayers"], list(BODY_LAYERS))
                self.assertEqual(set(manifest["clips"]), set(SOURCE_MAP))
                self.assertEqual(sum(len(clip["frames"]) for clip in manifest["clips"].values()), INDEX_COUNT)
                optional_metadata = {
                    "sourceVersion": 18, "sourceFrameCount": 51,
                    "maskModel": "pose-contours-v1", "skinId": skin,
                    "sourceManifestSha256": digest(SOURCE_ROOT / "animation.json"),
                    "skinName": next(item["name"] for item in self.source["skins"] if item["id"] == skin),
                    "skinKind": "approved-tint-variant-not-new-breed",
                }
                for key, expected in optional_metadata.items():
                    if key in manifest:
                        self.assertEqual(manifest[key], expected)
            for name, mapping in SOURCE_MAP.items():
                clip = manifest["clips"][name]
                with self.subTest(skin=skin, clip=name):
                    self.assertIsInstance(clip["frames"], list)
                    self.assertEqual(len(clip["frames"]), len(mapping))
                    for index, order in enumerate(clip["frames"]):
                        self.assertIsInstance(order, list)
                        self.assertTrue(all(isinstance(layer, str) for layer in order))
                        self.assertEqual(len(order), 9)
                        self.assertCountEqual(order, LAYERS)
                        self.assertNotIn("cat", order)
                        self.assertEqual(order, self.source_frame(name, index)["renderLayerOrder"])
                    if "sourceFrames" in clip:
                        expected = [{"clip": source_name, "index": source_index,
                                     "atlasIndex": self.source_frame(name, index)["atlasIndex"]}
                                    for index, (source_name, source_index) in enumerate(mapping)]
                        self.assertEqual(clip["sourceFrames"], expected)

    def test_durations_playback_and_zero_based_loops(self):
        for skin, manifest in self.manifests.items():
            for name, mapping in SOURCE_MAP.items():
                clip = manifest["clips"][name]
                with self.subTest(skin=skin, clip=name):
                    durations = clip["durationsMs"]
                    self.assertIsInstance(durations, list)
                    self.assertEqual(len(durations), len(mapping))
                    for duration in durations:
                        self.assertIs(type(duration), int)
                        self.assertGreater(duration, 0)
                    self.assertEqual(durations, [self.source_frame(name, index)["durationMs"]
                                                 for index in range(len(mapping))])
                    self.assertIn(clip["playback"], ("once", "hold", "intro-loop"))
                    if "loopStart" in clip:
                        self.assertIs(type(clip["loopStart"]), int)
                        self.assertGreaterEqual(clip["loopStart"], 0)
                        self.assertLess(clip["loopStart"], len(mapping))
                    if clip["playback"] == "intro-loop":
                        self.assertIn("loopStart", clip)
                    if name in ("effort", "hooked"):
                        self.assertEqual(clip["playback"], "intro-loop")
                        self.assertEqual(clip["loopStart"], 4 if name == "hooked" else 0)
                    elif name in ("idle", "ready", "fishing"):
                        self.assertEqual(clip["playback"], "hold")
                        self.assertEqual(len(mapping), 1)
                    else:
                        self.assertEqual(clip["playback"], "once")
                    if name in ("reel", "reel_empty"):
                        self.assertEqual(durations, [100] * 6)
                    if "nextClip" in clip:
                        self.assertIsInstance(clip["nextClip"], str)
                        self.assertIn(clip["nextClip"], SOURCE_MAP)
                        expected_next = {
                            "sit": "ready", "bait": "cast", "cast": "fishing",
                            "bite": "effort", "reel": "catch", "catch": "reset_catch",
                            "reel_empty": "reset_empty", "reset_catch": "ready",
                            "reset_empty": "ready",
                        }
                        if name in expected_next:
                            self.assertEqual(clip["nextClip"], expected_next[name])

    def test_constant_geometry_and_legacy_placement(self):
        baseline = read_json(BASELINE_MANIFEST)["place"] if BASELINE_MANIFEST else OLD_PLACE
        for skin, manifest in self.manifests.items():
            with self.subTest(skin=skin):
                self.assertEqual(manifest["canvas"], [640, 512])
                self.assertEqual(manifest["place"], baseline)
                for key in ("foot", "referenceFoot"):
                    self.assertEqual(len(manifest["place"][key]), 2)
                    for value in manifest["place"][key]:
                        self.assertIs(type(value) in (int, float), True)
                        self.assertTrue(math.isfinite(value))
                for key in ("bodyHeight", "reference", "referenceBody"):
                    value = manifest["place"][key]
                    self.assertIs(type(value) in (int, float), True)
                    self.assertTrue(math.isfinite(value))
                    self.assertGreater(value, 0)

    def test_complete_exclusive_path_inventory_size_and_alpha(self):
        expected = expected_pngs()
        actual = set(PACK_ROOT.rglob("*.png"))
        self.assertEqual(len(expected), INDEX_COUNT * (len(SKINS) * len(BODY_LAYERS) + len(GEAR_LAYERS)))
        self.assertEqual(actual, expected, "Missing, extra, or duplicate character/gear PNG paths")
        manifests = set(PACK_ROOT.rglob("manifest.json"))
        self.assertEqual(manifests, {PACK_ROOT / skin / "manifest.json" for skin in SKINS})
        self.assertFalse((PACK_ROOT / "shared" / "body").exists())
        for path in sorted(expected):
            with self.subTest(path=str(path.relative_to(PACK_ROOT))):
                self.assertTrue(path.resolve().is_relative_to(PACK_ROOT.resolve()))
                png_signature(path)

    def test_all_skin_slices_are_exact_source_atlas_pixels(self):
        for skin in self.source["skins"]:
            for layer in BODY_LAYERS:
                atlas = read_rgba(source_path(skin["layers"][layer]), (SIZE[0] * 6, SIZE[1] * 9))
                for clip, mapping in SOURCE_MAP.items():
                    for index in range(len(mapping)):
                        with self.subTest(skin=skin["id"], layer=layer, clip=clip, frame=index):
                            atlas_index = self.source_frame(clip, index)["atlasIndex"]
                            left, top = atlas_index % 6 * SIZE[0], atlas_index // 6 * SIZE[1]
                            expected = atlas.crop((left, top, left + SIZE[0], top + SIZE[1]))
                            self.assertEqual(png_signature(layer_path(skin["id"], clip, index, layer)),
                                             hashlib.sha256(expected.tobytes()).hexdigest())

    def test_shared_gear_and_orange_are_exact_source_pixels(self):
        for clip, mapping in SOURCE_MAP.items():
            for index in range(len(mapping)):
                frame = self.source_frame(clip, index)
                for layer in LAYERS:
                    with self.subTest(clip=clip, frame=index, layer=layer):
                        self.assertEqual(png_signature(layer_path("orange", clip, index, layer)),
                                         png_signature(source_path(frame["layers"][layer])))

    def test_four_character_layers_have_exclusive_ownership_all_skins(self):
        for skin in SKINS:
            for clip, mapping in SOURCE_MAP.items():
                for index in range(len(mapping)):
                    with self.subTest(skin=skin, clip=clip, frame=index):
                        owners = np.zeros((SIZE[1], SIZE[0]), dtype=np.uint8)
                        for layer in BODY_LAYERS:
                            alpha = np.asarray(read_rgba(layer_path(skin, clip, index, layer)).getchannel("A"))
                            owners += alpha > 0
                        self.assertFalse(np.any(owners > 1), "Character pixels counted in multiple layers")

    def test_orange_final_composites_follow_per_frame_source_order(self):
        for clip, mapping in SOURCE_MAP.items():
            for index in range(len(mapping)):
                with self.subTest(clip=clip, frame=index):
                    composite = Image.new("RGBA", SIZE)
                    order = self.manifests["orange"]["clips"][clip]["frames"][index]
                    self.assertEqual(len(order), len(set(order)))
                    self.assertNotIn("cat", order)
                    for layer in order:
                        composite.alpha_composite(read_rgba(layer_path("orange", clip, index, layer)))
                    expected = read_rgba(source_path(self.source_frame(clip, index)["composite"]))
                    self.assertEqual(composite.tobytes(), expected.tobytes())

    def test_handoffs_absent_gear_and_distinct_retrieval_orders(self):
        pairs = (("sit", 3, "ready", 0), ("reset_catch", 3, "ready", 0),
                 ("reset_empty", 3, "ready", 0), ("idle", 0, "sit", 0),
                 ("reset_catch", 0, "catch", 6), ("reset_empty", 0, "reel_empty", 5))
        for skin, manifest in self.manifests.items():
            for first, first_index, second, second_index in pairs:
                with self.subTest(skin=skin, handoff=f"{first}:{first_index}->{second}:{second_index}"):
                    self.assertEqual(manifest["clips"][first]["frames"][first_index],
                                     manifest["clips"][second]["frames"][second_index])
                    for layer in LAYERS:
                        self.assertEqual(png_signature(layer_path(skin, first, first_index, layer)),
                                         png_signature(layer_path(skin, second, second_index, layer)))
            for index in range(6):
                with self.subTest(skin=skin, clip="reel_empty", frame=index):
                    self.assertIsNone(read_rgba(layer_path(skin, "reel_empty", index, "fish")).getchannel("A").getbbox())
                    empty_order = manifest["clips"]["reel_empty"]["frames"][index]
                    success_order = manifest["clips"]["reel"]["frames"][index]
                    self.assertNotEqual(empty_order, success_order)
                    self.assertLess(empty_order.index("far_hand"), empty_order.index("rod"))
                    self.assertLess(empty_order.index("rod"), empty_order.index("hands"))
            for clip, indices in (("sit", range(3)), ("idle", range(1))):
                for index in indices:
                    for layer in ("rod", "line", "bobber", "bait"):
                        with self.subTest(skin=skin, clip=clip, frame=index, hidden=layer):
                            self.assertIsNone(read_rgba(layer_path(skin, clip, index, layer)).getchannel("A").getbbox())
            hooked = manifest["clips"]["hooked"]
            self.assertEqual(hooked["frames"][:4], manifest["clips"]["bite"]["frames"])
            self.assertEqual(hooked["frames"][4:], manifest["clips"]["effort"]["frames"])


def main(argv=None):
    global SOURCE_ROOT, PACK_ROOT, BASELINE_MANIFEST
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE_ROOT, help="Source animation-studio directory")
    parser.add_argument("--pack", type=Path, default=PACK_ROOT, help="Staged pack directory")
    parser.add_argument("--baseline-manifest", type=Path, help="Optional read-only legacy manifest for exact place comparison")
    parser.add_argument("--source-only", action="store_true", help="Check only source inputs; does not validate the runtime pack or write a report")
    arguments = parser.parse_args(argv)
    SOURCE_ROOT = arguments.source.resolve()
    PACK_ROOT = arguments.pack.resolve()
    BASELINE_MANIFEST = arguments.baseline_manifest
    if not (SOURCE_ROOT / "animation.json").is_file():
        print(f"Missing source manifest: {SOURCE_ROOT / 'animation.json'}", file=sys.stderr)
        return 2
    if not arguments.source_only:
        required = expected_pngs() | {PACK_ROOT / skin / "manifest.json" for skin in SKINS}
        missing = sorted(path for path in required if not path.is_file())
        if missing:
            print(f"WAITING: staged pack is not complete ({len(missing)} missing files). No polling or runtime validation performed.", file=sys.stderr)
            print(f"First missing file: {missing[0]}", file=sys.stderr)
            return 2
    inputs = [SOURCE_ROOT / "animation.json"]
    if not arguments.source_only:
        inputs.extend(PACK_ROOT / skin / "manifest.json" for skin in SKINS)
    if BASELINE_MANIFEST:
        inputs.append(BASELINE_MANIFEST)
    before = {str(path): digest(path) for path in inputs}
    png_signature.cache_clear()
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromTestCase(SourceTests)
    if not arguments.source_only:
        suite.addTests(loader.loadTestsFromTestCase(RuntimeTests))
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    unchanged = before == {str(path): digest(path) for path in inputs}
    passed = result.wasSuccessful() and unchanged
    if arguments.source_only:
        print("Source-only validation; staged pack and Unity runtime are NOT validated.")
    else:
        report = {
            "status": "passed" if passed else "failed",
            "scope": "Static manifests and read-only PNG comparisons; no Unity runtime execution or breed claims",
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "sourceRoot": str(SOURCE_ROOT), "packRoot": str(PACK_ROOT),
            "inputSha256": before, "inputManifestsUnchanged": unchanged,
            "sourceFrames": sum(SOURCE_COUNTS.values()),
            "runtimeClipsPerSkin": len(SOURCE_MAP), "indicesPerSkin": INDEX_COUNT,
            "skins": list(SKINS), "expectedUniquePngs": len(expected_pngs()),
            "testsRun": result.testsRun, "skipped": len(result.skipped),
            "failures": [{"test": str(test), "traceback": detail} for test, detail in result.failures],
            "errors": [{"test": str(test), "traceback": detail} for test, detail in result.errors],
        }
        report_path = HERE / "verification-report.json"
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Verification report: {report_path}")
    if not unchanged:
        print("Input manifests changed during validation; discard this result.", file=sys.stderr)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
