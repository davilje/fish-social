import json
import math
import subprocess
import unittest
from pathlib import Path

from PIL import Image

from fishing_line_geometry import SWING_CLIPS, quadratic_points

ROOT = Path(__file__).resolve().parents[1]
FISHING = ROOT / 'desktop-overlay/OverlayResources/fishing'
MOTION = json.loads((FISHING / 'motion-samples.json').read_text())
RIG = json.loads((ROOT / 'docs/美术/钓鱼动作/source-rig.json').read_text())


class SwingLines(unittest.TestCase):
    def test_six_cast_keys_keep_character_layers_and_timing(self):
        cast = MOTION['Clips']['cast']
        self.assertEqual(cast['DurationsMs'], [140, 70, 70, 70, 70, 180])
        self.assertEqual(sum(f['DurationMs'] for f in cast['Frames']), 600)
        self.assertEqual({f['Source'] for f in cast['Frames']}, set(range(6)))
        for manifest_path in FISHING.glob('*/manifest.json'):
            manifest = json.loads(manifest_path.read_text())
            self.assertEqual(len(manifest['clips']['cast']['frames']), 6)
            for layer in manifest['bodyLayers']:
                folder = manifest_path.parent / 'body/cast'
                for new, original in ((2, 1), (3, 4)):
                    self.assertEqual((folder/str(new)/(layer+'.png')).read_bytes(),
                                     (folder/str(original)/(layer+'.png')).read_bytes())
            for target in ('fish-social-unity/FishSocialOverlay/OverlayResources/fishing', 'fish-social-unity/Assets/StreamingAssets/Fishing'):
                self.assertEqual(manifest_path.read_bytes(), (ROOT/target/manifest_path.relative_to(FISHING)).read_bytes())
        markers = json.loads((FISHING/'presentation-markers.json').read_text())
        self.assertEqual([(m['marker'], m['sourceIndex'], m['sourceMs']) for m in markers['markers'] if m['clip']=='cast'],
                         [('cast.release', 2, 210), ('float.contact', 5, 420)])
        for index in (2, 3):
            self.assertLess(RIG['clips']['cast'][index]['end'][1], 100)
        # New prop silhouettes must be distinct, not duplicate timing slots.
        self.assertEqual(len({(FISHING/'shared/cast'/str(i)/'rod.png').read_bytes() for i in range(6)}), 6)

    def test_unchanged_other_actions_and_first_two_poses(self):
        before = json.loads(subprocess.check_output(['git', 'show', 'HEAD:desktop-overlay/OverlayResources/fishing/motion-samples.json'], cwd=ROOT))
        for clip, data in MOTION['Clips'].items():
            if clip not in SWING_CLIPS:
                self.assertEqual(data, before['Clips'][clip])
            elif clip != 'cast':
                self.assertEqual(data['DurationsMs'], before['Clips'][clip]['DurationsMs'])
                self.assertEqual([frame for frame in data['Frames'] if frame['Source'] < 2],
                                 [frame for frame in before['Clips'][clip]['Frames'] if frame['Source'] < 2])

    def test_endpoints_follow_tackle_and_rod_without_body_changes(self):
        for clip in SWING_CLIPS:
            for frame in MOTION['Clips'][clip]['Frames']:
                if frame['Source'] < 2:
                    continue
                line = frame['Line']
                rig = RIG['clips'][clip][frame['Source']]
                for layer in (0, 1, 2, 6):
                    self.assertEqual(frame['Transforms'][layer], [1, 0, 0, 1, 0, 0])
                for axis in range(2):
                    transform = frame['Transforms'][3]
                    self.assertAlmostEqual(line[axis], transform[axis] * rig['tip'][0] + transform[axis + 2] * rig['tip'][1] + transform[axis + 4])
                    tackle = frame['Transforms'][5 if clip == 'reel' else 7]
                    self.assertAlmostEqual(line[axis + 4], tackle[axis] * rig['end'][0] + tackle[axis + 2] * rig['end'][1] + tackle[axis + 4])
                if clip == 'reel_empty':
                    self.assertEqual(frame['Transforms'][7], frame['Transforms'][8])

    def test_curvature_is_bounded_and_settled_line_does_not_arch_up(self):
        for clip in SWING_CLIPS:
            for frame in MOTION['Clips'][clip]['Frames']:
                if frame['Source'] < 2:
                    continue
                line = frame['Line']
                chord = math.dist(line[:2], line[4:])
                points = quadratic_points(line)
                length = sum(math.dist(first, second) for first, second in zip(points, points[1:]))
                self.assertLessEqual(length, chord * 1.04)
                if frame is MOTION['Clips'][clip]['Frames'][-1]:
                    self.assertGreaterEqual(line[3], (line[1] + line[5]) / 2 - 1e-8)

    def test_shared_resources_match_all_three_clients(self):
        for clip in SWING_CLIPS:
            for index in range(2, len(RIG['clips'][clip])):
                for layer in ('line', 'bait'):
                    relative = Path('shared') / clip / str(index) / (layer + '.png')
                    content = (FISHING / relative).read_bytes()
                    for target in ('fish-social-unity/FishSocialOverlay/OverlayResources/fishing', 'fish-social-unity/Assets/StreamingAssets/Fishing'):
                        self.assertEqual(content, (ROOT / target / relative).read_bytes())
                    with Image.open(FISHING / relative) as image:
                        self.assertEqual(image.size, (640, 512))


if __name__ == '__main__':
    unittest.main()
