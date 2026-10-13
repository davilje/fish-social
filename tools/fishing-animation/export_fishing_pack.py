import argparse
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image


LAYERS = ['body', 'head', 'far_hand', 'rod', 'line', 'fish', 'hands', 'bobber', 'bait']
BODY_LAYERS = ['body', 'head', 'far_hand', 'hands']
MAPPING = {
    'idle': [('sit', 0)],
    'sit': [('sit', index) for index in range(4)],
    'ready': [('ready', 0)],
    'bait': [('bait', index) for index in range(4)],
    'cast': [('cast', index) for index in range(4)],
    'fishing': [('wait', 0)],
    'bite': [('bite', index) for index in range(4)],
    'effort': [('effort', index) for index in range(6)],
    'hooked': [('bite', index) for index in range(4)] + [('effort', index) for index in range(6)],
    'reel': [('retrieve', index) for index in range(6)],
    'reel_empty': [('retrieve_empty', index) for index in range(6)],
    'catch': [('catch', index) for index in range(7)],
    'reset_catch': [('reset_catch', index) for index in range(4)],
    'reset_empty': [('reset_empty', index) for index in range(4)],
}
NEXT = {'sit': 'ready', 'bite': 'effort', 'reel': 'catch', 'catch': 'reset_catch',
        'reset_catch': 'ready', 'reel_empty': 'reset_empty', 'reset_empty': 'ready'}
DEFAULT_PLACE = {'foot': [264, 450], 'bodyHeight': 257, 'reference': 512,
                 'referenceFoot': [138.5, 404], 'referenceBody': 279}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def checked_image(path, size):
    image = Image.open(path).convert('RGBA')
    if image.size != size:
        image.close()
        raise ValueError(f'Incorrect canvas: {path}')
    return image


def export(source, output, previous_manifest=None):
    manifest_path = source / 'animation.json'
    manifest_bytes = manifest_path.read_bytes()
    source_data = json.loads(manifest_bytes)
    if source_data.get('version') != 18 or source_data.get('characterMaskModel', {}).get('id') != 'pose-contours-v1':
        raise ValueError('This converter requires the approved v18 pose-contours-v1 export')
    width = source_data['frameSize']['width']
    height = source_data['frameSize']['height']
    if [width, height] != [640, 512]:
        raise ValueError('Unexpected canvas size')
    if source_data['renderLayerOrder'] != LAYERS:
        raise ValueError('Unexpected render layer names')
    if sum(len(clip['frames']) for clip in source_data['clips'].values()) != 51:
        raise ValueError('Expected all 51 source frames')
    place = DEFAULT_PLACE
    if previous_manifest:
        place = json.loads(previous_manifest.read_text(encoding='utf-8')).get('place', place)
    clips = {}
    for clip_id, entries in MAPPING.items():
        frames = []
        durations = []
        provenance = []
        for phase, index in entries:
            frame = source_data['clips'][phase]['frames'][index]
            order = frame['renderLayerOrder']
            if len(order) != len(LAYERS) or set(order) != set(LAYERS):
                raise ValueError(f'Invalid render order: {phase}:{index}')
            frames.append(order)
            durations.append(frame['durationMs'])
            provenance.append({'clip': phase, 'index': index, 'atlasIndex': frame['atlasIndex']})
        playback = 'hold' if clip_id in ('idle', 'ready', 'fishing') else 'once'
        if clip_id in ('effort', 'hooked'):
            playback = 'intro-loop'
        record = {'playback': playback, 'frames': frames, 'durationsMs': durations,
                  'sourceFrames': provenance}
        if clip_id in NEXT:
            record['nextClip'] = NEXT[clip_id]
        if playback == 'intro-loop':
            record['loopStart'] = 4 if clip_id == 'hooked' else 0
        clips[clip_id] = record
    runtime_data = {
        'version': 2, 'sourceVersion': 18, 'canvas': [width, height],
        'layers': LAYERS, 'bodyLayers': BODY_LAYERS, 'place': place,
        'maskModel': 'pose-contours-v1', 'sourceFrameCount': 51,
        'sourceManifestSha256': hashlib.sha256(manifest_bytes).hexdigest(),
        'clips': clips, 'sourceToRuntime': {
            'wait': 'fishing', 'retrieve': 'reel', 'retrieve_empty': 'reel_empty',
            'hooked': 'bite + effort (loopStart=4)',
        },
        'flows': {
            'success': ['sit', 'ready', 'bait', 'cast', 'fishing', 'hooked', 'reel', 'catch', 'reset_catch', 'ready'],
            'empty': ['sit', 'ready', 'bait', 'cast', 'fishing', 'reel_empty', 'reset_empty', 'ready'],
        },
    }
    for clip_id, entries in MAPPING.items():
        for frame_index, (phase, index) in enumerate(entries):
            frame = source_data['clips'][phase]['frames'][index]
            for layer in LAYERS:
                if layer in BODY_LAYERS:
                    continue
                destination = output / 'shared' / clip_id / str(frame_index) / (layer + '.png')
                destination.parent.mkdir(parents=True, exist_ok=True)
                origin = source / frame['layers'][layer]
                with checked_image(origin, (width, height)):
                    shutil.copy2(origin, destination)
    skins = source_data['skins']
    if [skin['id'] for skin in skins] != ['orange', 'silver', 'brown', 'cream', 'blue']:
        raise ValueError('Unexpected approved skin set')
    for skin in skins:
        skin_id = skin['id']
        sheets = {}
        try:
            if skin_id != 'orange':
                for layer in BODY_LAYERS:
                    sheets[layer] = checked_image(source / skin['layers'][layer],
                        (width * skin['columns'], height * skin['rows']))
            for clip_id, entries in MAPPING.items():
                for frame_index, (phase, index) in enumerate(entries):
                    frame = source_data['clips'][phase]['frames'][index]
                    for layer in BODY_LAYERS:
                        destination = output / skin_id / 'body' / clip_id / str(frame_index) / (layer + '.png')
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        if skin_id == 'orange':
                            origin = source / frame['layers'][layer]
                            with checked_image(origin, (width, height)):
                                shutil.copy2(origin, destination)
                        else:
                            atlas_index = frame['atlasIndex']
                            left = atlas_index % skin['columns'] * width
                            top = atlas_index // skin['columns'] * height
                            tile = sheets[layer].crop((left, top, left + width, top + height))
                            tile.save(destination)
                            tile.close()
            skin_manifest = dict(runtime_data, skinId=skin_id, skinName=skin['name'],
                                 skinKind='approved-tint-variant-not-new-breed')
            write_json(output / skin_id / 'manifest.json', skin_manifest)
        finally:
            for sheet in sheets.values():
                sheet.close()
    write_json(output / 'source-animation-v18.json', source_data)
    write_json(output / 'inventory.json', {
        'sourceVersion': 18, 'runtimeVersion': 2, 'sourceFrameCount': 51,
        'runtimeClipCount': len(clips), 'runtimeFrameCountPerSkin': sum(len(entries) for entries in MAPPING.values()),
        'skins': [{'id': skin['id'], 'name': skin['name']} for skin in skins],
        'maskModel': 'pose-contours-v1', 'bodyLayers': BODY_LAYERS, 'layers': LAYERS,
        'note': 'Retained approved tint variants. No decorative hat/scarf layers; no invented breed patterns.',
    })
    print(json.dumps({'output': str(output), 'skins': len(skins), 'clips': len(clips),
                      'sourceFrames': 51, 'runtimeFramesPerSkin': sum(map(len, MAPPING.values()))}))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--previous-manifest', type=Path)
    arguments = parser.parse_args()
    export(arguments.source, arguments.output, arguments.previous_manifest)


if __name__ == '__main__':
    main()
