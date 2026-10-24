"""Export subframe trajectories while retaining the approved PNGs and key timings."""
import argparse
import hashlib
import json
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
FISHING = ROOT / 'desktop-overlay/OverlayResources/fishing'
RIG = ROOT / 'docs/art/fishing-motion/source-rig.json'
CLIPS = ('cast', 'reel', 'reel_empty', 'groundbait')
LAYERS = ('body', 'head', 'far_hand', 'rod', 'line', 'fish', 'hands', 'bobber', 'bait')
IDENTITY = [1, 0, 0, 1, 0, 0]


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


def mix(first, second, amount):
    return [left + (right - left) * amount for left, right in zip(first, second)]


def rotate(pivot, degrees, target=None):
    cosine, sine = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    target = target or pivot
    return [cosine, sine, -sine, cosine,
            target[0] - cosine * pivot[0] + sine * pivot[1],
            target[1] - sine * pivot[0] - cosine * pivot[1]]


def point(matrix, position):
    return [matrix[0] * position[0] + matrix[2] * position[1] + matrix[4],
            matrix[1] * position[0] + matrix[3] * position[1] + matrix[5]]


def center(bounds):
    return [(bounds[0] + bounds[2]) / 2, (bounds[1] + bounds[3]) / 2]


def bounds_after(bounds, matrix):
    if bounds is None:
        return None
    corners = [point(matrix, [horizontal, vertical]) for horizontal in (bounds[0], bounds[2])
               for vertical in (bounds[1], bounds[3])]
    return [min(value[0] for value in corners), min(value[1] for value in corners),
            max(value[0] for value in corners), max(value[1] for value in corners)]


def import_rig(studio, manifest):
    data = json.loads(studio.read_text())
    result = {'version': 1, 'sourceSha256': hashlib.sha256(studio.read_bytes()).hexdigest(), 'clips': {}}
    for clip in CLIPS:
        records = []
        for source in manifest['clips'][clip]['sourceFrames']:
            rig = data['clips'][source['clip']]['frames'][source['index']]['rig']
            controls = rig.get('cubicRodControls') or []
            records.append({'grip': rig.get('grip'), 'tip': rig.get('rodTip') or (controls[-1] if controls else None),
                            'end': rig.get('lineEnd'), 'angle': rig.get('angleDegrees', 0),
                            'fishAngle': -(rig.get('fishPose') or {}).get('rotationDegrees', 0)})
        result['clips'][clip] = records
    write_json(RIG, result)


def build(manifest, rig):
    result = {'Version': 1, 'Method': 'layer-trajectory-samples-v1', 'Clips': {}}
    for clip in CLIPS:
        original = manifest['clips'][clip]
        records = rig['clips'][clip]
        assert len(records) == len(original['frames'])
        geometry = []
        for index in range(len(records)):
            bounds = {}
            for layer in LAYERS:
                if layer in manifest['bodyLayers']:
                    continue
                with Image.open(FISHING / 'shared' / clip / str(index) / (layer + '.png')) as image:
                    bounds[layer] = image.getchannel('A').getbbox()
            geometry.append(bounds)
        samples = []
        for index, duration in enumerate(original['durationsMs']):
            following = min(index + 1, len(records) - 1)
            first, second = records[index], records[following]
            moving = following != index and original['frames'][index] == original['frames'][following]
            steps = math.ceil(duration / 40) if moving else 1
            for step in range(steps):
                amount = step / steps
                transforms = {layer: IDENTITY[:] for layer in LAYERS}
                line_path = None
                target_tip = first['tip']
                target_end = first['end']
                if amount and first['grip'] and second['grip'] and first['tip']:
                    angle = (second['angle'] - first['angle'] + 180) % 360 - 180
                    transforms['rod'] = rotate(first['grip'], angle * amount)
                    target_tip = point(transforms['rod'], first['tip'])
                if amount and first['end'] and second['end']:
                    target_end = mix(first['end'], second['end'], amount)
                    transforms['bobber'][4:] = [target_end[axis] - first['end'][axis] for axis in range(2)]
                    transforms['fish'] = rotate(first['end'], (second['fishAngle'] - first['fishAngle']) * amount, target_end)
                    if first['tip'] and target_tip:
                        line_path = [*target_tip, (target_tip[0] + target_end[0]) / 2,
                                     min(target_tip[1], target_end[1]) - (24 if clip == 'cast' else 0), *target_end]
                if amount and clip == 'groundbait':
                    before, after = geometry[index]['bait'], geometry[following]['bait']
                    if before and after:
                        current, target = center(before), center(after)
                        transforms['bait'][4:] = [(target[axis] - current[axis]) * amount for axis in range(2)]
                transformed = {layer: bounds_after(bounds, transforms[layer]) for layer, bounds in geometry[index].items()}
                line, bobber, fish = (transformed[layer] for layer in ('line', 'bobber', 'fish'))
                samples.append({'Source': index, 'Position': index + amount,
                                'DurationMs': round((step + 1) * duration / steps) - round(step * duration / steps),
                                'Transforms': [transforms[layer] for layer in LAYERS],
                                'Line': line_path,
                                'Contact': {'LineTop': target_tip[1] if target_tip and line else line[1] if line else 0,
                                            'LineBottom': target_end[1] if target_end and line else line[3] if line else 0,
                                            'BobberY': center(bobber)[1] if bobber else -1,
                                            'FishTop': fish[1] if fish else -1}})
        result['Clips'][clip] = {'DurationsMs': original['durationsMs'], 'Frames': samples}
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--studio', type=Path)
    args = parser.parse_args()
    manifest = json.loads((FISHING / 'orange/manifest.json').read_text())
    if args.studio:
        import_rig(args.studio, manifest)
    result = build(manifest, json.loads(RIG.read_text()))
    for folder in (FISHING, ROOT / 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing'):
        write_json(folder / 'motion-samples.json', result)
    preview = {'motion': result, 'pets': {path.parent.name: json.loads(path.read_text())
                                         for path in sorted(FISHING.glob('*/manifest.json'))}}
    (RIG.parent / 'preview-data.js').write_text('window.fishingMotionPreview = ' + json.dumps(preview, ensure_ascii=False) + ';\n')
    print(json.dumps({clip: {'keys': len(value['DurationsMs']), 'samples': len(value['Frames']),
                            'durationMs': sum(frame['DurationMs'] for frame in value['Frames'])}
                      for clip, value in result['Clips'].items()}, indent=2))


if __name__ == '__main__':
    main()
