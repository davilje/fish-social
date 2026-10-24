import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]

class WaterContactMetadataTests(unittest.TestCase):
    def test_deployed_geometry_matches_source(self):
        source = ROOT / 'desktop-overlay/OverlayResources/fishing/water-contact.json'
        deployed = ROOT / 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing/water-contact.json'
        self.assertEqual(source.read_bytes(), deployed.read_bytes())

    def test_every_exported_clip_has_bounds_for_all_frames(self):
        folder = ROOT / 'desktop-overlay/OverlayResources/fishing'
        data = json.loads((folder / 'water-contact.json').read_text())
        for path in folder.glob('*/manifest.json'):
            manifest = json.loads(path.read_text())
            for clip, spec in manifest['clips'].items():
                with self.subTest(cat=path.parent.name, clip=clip):
                    self.assertGreaterEqual(len(data[clip]), len(spec['frames']))
                    for frame in data[clip]:
                        self.assertGreaterEqual(frame['LineBottom'], frame['LineTop'])
                        self.assertLessEqual(frame['LineBottom'], 512)

    def test_geometry_is_measured_from_alpha_not_cat_body(self):
        data = json.loads((ROOT / 'desktop-overlay/OverlayResources/fishing/water-contact.json').read_text())
        self.assertEqual(data['fishing'][0]['BobberY'], 463.5)
        self.assertLess(data['hooked'][0]['FishTop'], data['fishing'][0]['BobberY'])
        self.assertLess(data['reel'][0]['LineBottom'], data['fishing'][0]['LineBottom'])
