"""Insert two prop keys into the four-key cast, retaining approved character pixels.

Idempotent on both the legacy four-key pack and this six-key pack. Run before
export-fishing-swing-lines, export-fishing-water-contact and export-fishing-motion.
"""
import copy
import hashlib
import io
import json
import math
import uuid
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'desktop-overlay/OverlayResources/fishing'
TARGETS = (ART, ROOT / 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing',
           ROOT / 'fish-social-unity/Assets/StreamingAssets/Fishing')
RIG = ROOT / 'docs/美术/钓鱼动作/source-rig.json'
DURATIONS = [140, 70, 70, 70, 70, 180]
POSES = [0, 1, 1, 2, 2, 3]


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


def png(image):
    output = io.BytesIO()
    image.save(output, format='PNG')
    return output.getvalue()


def rotate_point(position, pivot, angle):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    x, y = position[0] - pivot[0], position[1] - pivot[1]
    return [pivot[0] + c*x - s*y, pivot[1] + s*x + c*y]


def unity_meta(path):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return
    # Deterministic, unique asset GUIDs; never replace an existing asset's GUID.
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'fish-social:' + str(path.relative_to(ROOT))).hex
    importer = 'folderAsset: yes\nDefaultImporter' if path.is_dir() else 'DefaultImporter'
    meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def main():
    manifest_paths = sorted(ART.glob('*/manifest.json'))
    manifests = {p.parent.name: json.loads(p.read_text()) for p in manifest_paths}
    count = len(manifests['orange']['clips']['cast']['frames'])
    assert count in (4, 6), 'Unexpected cast layout; do not overwrite unknown artwork'
    indexes = [0, 1, 2, 3] if count == 4 else [0, 1, 4, 5]
    assert all(len(m['clips']['cast']['frames']) == count for m in manifests.values())
    rig = json.loads(RIG.read_text())
    old_rig = [copy.deepcopy(rig['clips']['cast'][i]) for i in indexes]
    records = [copy.deepcopy(old_rig[i]) for i in POSES]
    for index, degrees, endpoint in ((2, 40, [140, 60]), (3, -40, [400, 90])):
        record = records[index]
        record['tip'] = rotate_point(record['tip'], record['grip'], degrees)
        record['angle'] += degrees
        record['end'] = endpoint
    rig['clips']['cast'] = records
    rig['castRevision'] = 'six-keys-overhead-v1'

    # Read all original files before replacing directories 2 and 3.
    originals = {}
    for name, manifest in manifests.items():
        for pose, source in enumerate(indexes):
            for layer in manifest['bodyLayers']:
                originals[name, pose, layer] = (ART / name / 'body/cast' / str(source) / (layer+'.png')).read_bytes()
    shared_layers = [layer for layer in manifests['orange']['layers'] if layer not in manifests['orange']['bodyLayers']]
    props = {(pose, layer): (ART / 'shared/cast' / str(source) / (layer+'.png')).read_bytes()
             for pose, source in enumerate(indexes) for layer in shared_layers}
    output = {}
    for index, pose in enumerate(POSES):
        for name, manifest in manifests.items():
            for layer in manifest['bodyLayers']:
                output[Path(name)/'body/cast'/str(index)/(layer+'.png')] = originals[name, pose, layer]
        for layer in shared_layers:
            data = props[pose, layer]
            if index in (2, 3) and layer in ('rod', 'bobber'):
                image = Image.open(io.BytesIO(data)).convert('RGBA')
                if layer == 'rod':
                    image = image.rotate(-40 if index == 2 else 40, resample=Image.Resampling.BICUBIC,
                                         center=records[index]['grip'])
                else:
                    dx, dy = [records[index]['end'][a] - old_rig[pose]['end'][a] for a in range(2)]
                    image = image.transform(image.size, Image.Transform.AFFINE, (1, 0, -dx, 0, 1, -dy), Image.Resampling.BICUBIC)
                data = png(image)
            output[Path('shared/cast')/str(index)/(layer+'.png')] = data

    for name, manifest in manifests.items():
        clip = manifest['clips']['cast']
        old_orders = [copy.deepcopy(clip['frames'][i]) for i in indexes]
        old_sources = [copy.deepcopy(clip['sourceFrames'][i]) for i in indexes]
        clip['frames'] = [old_orders[i] for i in POSES]
        clip['durationsMs'] = DURATIONS[:]
        clip['sourceFrames'] = [copy.deepcopy(old_sources[i]) for i in POSES]
        for index in (2, 3):
            clip['sourceFrames'][index]['propKey'] = 'overhead' if index == 2 else 'forward-release'
        clip['keyRevision'] = 'six-keys-overhead-v1'
        manifest['additionalPropKeyCount'] = 2
    write_json(RIG, rig)
    for target in TARGETS:
        for relative, data in output.items():
            path = target / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            if 'Assets/StreamingAssets' in str(target):
                unity_meta(path.parent)
                unity_meta(path)
        for name, manifest in manifests.items():
            write_json(target / name / 'manifest.json', manifest)
        inventory = json.loads((target / 'inventory.json').read_text())
        inventory['runtimeFrameCountPerSkin'] = sum(len(c['frames']) for c in manifests['orange']['clips'].values())
        inventory['additionalPropKeyCount'] = 2
        inventory['note'] = 'Eleven layered characters; cast includes two additional overhead prop keys; original body artwork and 63-pose source atlas retained.'
        write_json(target / 'inventory.json', inventory)

    # Refresh marker bounds and timings; release occurs overhead, splash only on landing.
    markers = json.loads((ART / 'presentation-markers.json').read_text())
    markers['sourceManifestSha256'] = hashlib.sha256((ART / 'orange/manifest.json').read_bytes()).hexdigest()
    for marker in markers['markers']:
        if marker['clip'] == 'cast':
            marker['sourceIndex'] = 2 if marker['marker'] == 'cast.release' else 5
            marker['sourceMs'] = sum(DURATIONS[:marker['sourceIndex']])
    markers['bounds']['cast'] = {}
    for layer in ('bobber', 'bait', 'fish'):
        markers['bounds']['cast'][layer] = [Image.open(ART/'shared/cast'/str(i)/(layer+'.png')).getchannel('A').getbbox() for i in range(6)]
    for target in TARGETS:
        write_json(target / 'presentation-markers.json', markers)
    print(f'Cast expanded to 6 keys for {len(manifests)} characters, {sum(DURATIONS)} ms, across 3 resource trees.')


if __name__ == '__main__':
    main()
