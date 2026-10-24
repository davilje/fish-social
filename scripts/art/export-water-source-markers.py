#!/usr/bin/env python3
"""Export approved source-layer bounds once; never scan pixels on the WPF UI thread.

Requires Pillow. Source positions, not expanded interpolation indexes, define cues.
No fish.surface/slap markers are claimed by the currently approved underwater clips.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'desktop-overlay/OverlayResources/fishing'
frames = {}
for clip in sorted((ART / 'shared').iterdir()):
    if not clip.is_dir():
        continue
    for frame in sorted(clip.iterdir()):
        if not frame.is_dir() or not frame.name.isdigit():
            continue
        layers = {}
        for layer in ('bobber', 'fish', 'bait'):
            path = frame / (layer + '.png')
            if not path.exists():
                continue
            image = Image.open(path).convert('RGBA')
            bounds = image.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
            if bounds:
                layers[layer] = {'bounds': list(bounds), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        frames[f'{clip.name}/{frame.name}'] = layers
canonical_path = ART / 'presentation-markers.json'
canonical = json.loads(canonical_path.read_text())
markers = [
    {'clip': m['clip'], 'source': m['sourceIndex'], 'sourceMs': m['sourceMs'], 'cue': m['marker'], 'layer': m['contactLayer']}
    for m in canonical['markers'] if m['marker'] in ('float.contact', 'groundbait.contact')
]
markers += [
    {'clip': 'fishing', 'source': 0, 'cue': 'waiting.ripple', 'layer': 'bobber'},
    {'clip': 'hooked', 'source': 4, 'cue': 'fish.struggle', 'layer': 'fish'},
    {'clip': 'effort', 'source': 0, 'cue': 'fish.struggle', 'layer': 'fish'},
]
payload = {
    'schemaVersion': 1,
    'sourceCanvas': [640, 512],
    'alphaThreshold': 8,
    'presentationMarkersSha256': hashlib.sha256(canonical_path.read_bytes()).hexdigest(),
    'markers': markers,
    'notes': 'Bite is authoritative onset on first published hooked/bite/effort frame. Current approved fish clips remain submerged: no fish.surface/fish.slap source markers.',
    'frames': frames,
}
(ART / 'source-water-markers.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n')
print(f'Exported {len(frames)} source-frame records; {len(markers)} water markers (2 canonical one-shots + continuous); 0 fish.surface/slap markers.')
