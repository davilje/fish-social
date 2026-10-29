import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw

from fishing_line_geometry import SWING_CLIPS, quadratic_points, swing_line


ROOT = Path(__file__).resolve().parents[1]
FISHING = ROOT / 'desktop-overlay/OverlayResources/fishing'
REVIEW = ROOT / 'docs/美术/钓鱼动作/line-review'
TARGETS = (FISHING, ROOT / 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing',
           ROOT / 'fish-social-unity/Assets/StreamingAssets/Fishing')


def stroke(points):
    image = Image.new('RGBA', (2560, 2048))
    ImageDraw.Draw(image).line([(round(horizontal * 4), round(vertical * 4))
                               for horizontal, vertical in points], fill='#3e3628', width=6)
    return image.resize((640, 512), Image.Resampling.LANCZOS)


def main():
    rig = json.loads((REVIEW.parent / 'source-rig.json').read_text())
    manifest = json.loads((FISHING / 'orange/manifest.json').read_text())
    for clip in SWING_CLIPS:
        records = rig['clips'][clip]
        durations = manifest['clips'][clip]['durationsMs']
        for index in range(2, len(records)):
            original = REVIEW / 'original' / clip / str(index)
            original.mkdir(parents=True, exist_ok=True)
            for layer in ('line', 'bait'):
                if not (original / (layer + '.png')).exists():
                    shutil.copy2(FISHING / 'shared' / clip / str(index) / (layer + '.png'), original)
            record = records[index]
            curve = swing_line(clip, records, durations, index, min(index + 1, len(records) - 1),
                               0, record['tip'], record['end'])
            layers = {'line': stroke(quadratic_points(curve))}
            if clip == 'reel_empty':
                endpoint = record['end']
                leader = stroke([endpoint, [endpoint[0], endpoint[1] + 35]])
                with Image.open(original / 'bait.png') as hook:
                    leader.alpha_composite(hook.convert('RGBA'))
                layers['bait'] = leader
            for target in TARGETS:
                for layer, image in layers.items():
                    image.save(target / 'shared' / clip / str(index) / (layer + '.png'))
    count = sum(len(rig['clips'][clip]) - 2 for clip in SWING_CLIPS)
    print(f'Updated {count} line keys and 4 fixed-length hook leaders across three resource trees.')


if __name__ == '__main__':
    main()
