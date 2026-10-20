import argparse
import hashlib
import json
import sys
import tempfile
from pathlib import Path

from PIL import Image


class FramesRecovered(Exception):
    pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--authoring-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args()
    authoring = arguments.authoring_root.resolve()
    destination = arguments.output.resolve()
    sys.path.insert(0, str(authoring))
    import build_animation_studio as studio

    original = studio.groundbait_and_leave_clips
    previous = json.loads((authoring / 'output/animation-studio/animation.json').read_text())
    records = []

    def recover(clips):
        clips.update(original(clips))
        for clip, frames in clips.items():
            for index, frame in enumerate(frames):
                source_frame = previous['clips'][clip]['frames'][index]
                with Image.open(authoring / 'output/animation-studio' / source_frame['sourceCharacter']) as character:
                    if frame['characterSource'].tobytes() != character.convert('RGBA').tobytes():
                        raise ValueError(f'Character geometry changed during recovery: {clip}:{index}')
                filename = destination / f'{clip}-{index:02d}.png'
                filename.parent.mkdir(parents=True, exist_ok=True)
                frame['layers']['rod'].save(filename)
                records.append({'id': f'{clip}-{index:02d}', 'clip': clip, 'index': index,
                                'file': filename.name, 'sha256': hashlib.sha256(filename.read_bytes()).hexdigest(),
                                'renderLayerOrder': studio.render_layer_order(frame),
                                'rig': frame['rig']})
        raise FramesRecovered()

    studio.groundbait_and_leave_clips = recover
    assets = authoring / 'output/animation-studio/assets'
    with tempfile.TemporaryDirectory(prefix='fish-complete-rods-') as scratch:
        studio.OUTPUT = Path(scratch)
        sys.argv = ['recover_complete_rods', str(assets / 'bait-pose-sheet.png')]
        for option, name in [('retrieve', 'retrieve-pose-sheet'), ('catch', 'catch-pose-sheet'),
                             ('additional', 'additional-pose-sheet'), ('empty', 'empty-disappointed-pose-sheet'),
                             ('unarmed', 'unarmed-sit-pose-sheet')]:
            sys.argv.extend([f'--{option}-sheet', str(assets / (name + '.png'))])
        try:
            studio.main()
        except FramesRecovered:
            pass
    if len(records) != 63:
        raise ValueError(f'Expected 63 source rod frames, got {len(records)}')
    report = {'version': 1, 'model': 'head-layer-order-v1', 'canvas': [640, 512],
              'method': 'Replay original authored rod transforms before head-alpha subtraction; no repainting.',
              'characterGeometryUnchanged': True, 'frames': records}
    (destination / 'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'sourceFrames': len(records), 'characterGeometryUnchanged': True}))


if __name__ == '__main__':
    main()
