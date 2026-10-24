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
CLIPS = ('sit', 'bait', 'cast', 'fishing', 'bite', 'effort', 'hooked', 'reel',
         'reel_empty', 'catch', 'reset_catch', 'reset_empty', 'groundbait', 'leave')
GROUP_CLIPS = ('sit', 'bait', 'catch', 'reset_catch', 'reset_empty', 'leave')
FOOT = [264, 450]
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
        for index, source in enumerate(manifest['clips'][clip]['sourceFrames']):
            rig = data['clips'][source['clip']]['frames'][source['index']]['rig']
            controls = rig.get('cubicRodControls') or []
            tip = rig.get('rodTip') or (controls[-1] if controls else None)
            endpoint = rig.get('lineEnd')
            with Image.open(FISHING / 'shared' / clip / str(index) / 'line.png') as image:
                alpha = image.getchannel('A')
                bounds = alpha.getbbox()
                if bounds:
                    if not tip:
                        row = alpha.crop((0, bounds[1], 640, bounds[1] + 1)).getbbox()
                        tip = [(row[0] + row[2]) / 2, bounds[1]]
                    if not endpoint:
                        row = alpha.crop((0, bounds[3] - 1, 640, bounds[3])).getbbox()
                        endpoint = [(row[0] + row[2]) / 2, bounds[3] - 1]
            records.append({'grip': rig.get('grip'), 'tip': tip,
                            'end': endpoint, 'angle': rig.get('angleDegrees', 0),
                            'fishAngle': -(rig.get('fishPose') or {}).get('rotationDegrees', 0),
                            'head': rig.get('headAnchor'), 'fishHeld': rig.get('fishHeld', False)})
        result['clips'][clip] = records
    write_json(RIG, result)


def build(manifest, rig):
    result = {'Version': 1, 'Method': 'layer-trajectory-samples-v2',
              'WaterContact': {'ReferenceSurfaceY': 463.5, 'FishDepth': 64, 'GroundbaitDrop': 90}, 'Clips': {}}
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
            if index == len(records) - 1 and original['playback'] == 'intro-loop':
                following = original.get('loopStart', 0)
            first, second = records[index], records[following]
            moving = following != index and original['frames'][index] == original['frames'][following]
            steps = math.ceil(duration / 40) if moving or clip == 'fishing' else 1
            for step in range(steps):
                amount = step / steps
                transforms = {layer: IDENTITY[:] for layer in LAYERS}
                root = IDENTITY[:]
                line_path = None
                target_tip = first['tip']
                target_end = first['end']
                if amount and clip in GROUP_CLIPS and first['head'] and second['head']:
                    angles = [math.degrees(math.atan2(record['head'][1] - FOOT[1], record['head'][0] - FOOT[0]))
                              for record in (first, second)]
                    lean = max(-6, min(6, (angles[1] - angles[0] + 180) % 360 - 180))
                    root = rotate(FOOT, lean * amount)
                if amount and first['grip'] and second['grip'] and first['tip']:
                    angle = (second['angle'] - first['angle'] + 180) % 360 - 180
                    if clip in ('bite', 'effort', 'hooked') and second['tip']:
                        directions = [math.atan2(tip[1] - first['grip'][1], tip[0] - first['grip'][0])
                                      for tip in (first['tip'], second['tip'])]
                        angle = (math.degrees(directions[1] - directions[0]) + 180) % 360 - 180
                    transforms['rod'] = rotate(first['grip'], angle * amount)
                    target_tip = point(transforms['rod'], first['tip'])
                if amount and clip == 'fishing' and first['end']:
                    target_end = [first['end'][0] + 1.5 * math.sin(amount * math.tau), first['end'][1]]
                    transforms['bobber'][4] = target_end[0] - first['end'][0]
                elif amount and clip != 'bait' and not first['fishHeld'] and first['end'] and second['end']:
                    target_end = mix(first['end'], second['end'], amount)
                    transforms['bobber'][4:] = [target_end[axis] - first['end'][axis] for axis in range(2)]
                    transforms['fish'] = rotate(first['end'], (second['fishAngle'] - first['fishAngle']) * amount, target_end)
                if amount and geometry[index]['line'] and target_tip and target_end:
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
                                'Root': root,
                                'Line': line_path,
                                'Contact': {'LineTop': target_tip[1] if target_tip and line else line[1] if line else 0,
                                            'LineBottom': target_end[1] if target_end and line else line[3] if line else 0,
                                            'BobberY': center(bobber)[1] if bobber else -1,
                                            'FishTop': fish[1] if fish else -1}})
        result['Clips'][clip] = {'DurationsMs': original['durationsMs'], 'Frames': samples, 'Loop': clip == 'fishing'}
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
    preview = {'motion': result, 'contact': json.loads((FISHING / 'water-contact.json').read_text()),
               'sleepHeads': sorted(path.parts[-5] for path in FISHING.glob('*/body/fishing/0/head-sleep.png')),
               'pets': {path.parent.name: json.loads(path.read_text())
                                         for path in sorted(FISHING.glob('*/manifest.json'))}}
    (RIG.parent / 'preview-data.js').write_text('window.fishingMotionPreview = ' + json.dumps(preview, ensure_ascii=False) + ';\n')
    print(json.dumps({clip: {'keys': len(value['DurationsMs']), 'samples': len(value['Frames']),
                            'durationMs': sum(frame['DurationMs'] for frame in value['Frames'])}
                      for clip, value in result['Clips'].items()}, indent=2))


if __name__ == '__main__':
    main()
