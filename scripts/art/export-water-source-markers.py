#!/usr/bin/env python3
"""Export approved source-layer bounds once; never scan pixels on the WPF UI thread.

Requires Pillow. Source positions, not expanded interpolation indexes, define cues.
Underwater bite/effort have no surface markers; reel must pass actual geometry contact.
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
        for layer in ('bobber', 'fish', 'bait', 'line'):
            path = frame / (layer + '.png')
            if not path.exists():
                continue
            image = Image.open(path).convert('RGBA')
            bounds = image.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
            if bounds:
                layers[layer] = {'bounds': list(bounds), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                if layer == 'line':
                    alpha = image.getchannel('A')
                    rows = []
                    for y in range(bounds[1], bounds[3]):
                        xs = [x for x in range(bounds[0], bounds[2]) if alpha.getpixel((x, y)) > 8]
                        if xs:
                            rows.append([y, round(sum(xs) / len(xs), 4)])
                    layers[layer]['rowCenters'] = rows
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
    {'clip': 'reel', 'source': 0, 'sourceMs': 0, 'cue': 'retrieve.contact', 'layer': 'line'},
    {'clip': 'reel', 'source': 0, 'sourceMs': 0, 'cue': 'fish.surface', 'layer': 'fish'},
    {'clip': 'reel_empty', 'source': 0, 'sourceMs': 0, 'cue': 'retrieve.contact', 'layer': 'bobber'},
]
payload = {
    'schemaVersion': 1,
    'sourceCanvas': [640, 512],
    'alphaThreshold': 8,
    'presentationMarkersSha256': hashlib.sha256(canonical_path.read_bytes()).hexdigest(),
    'markers': markers,
    'notes': 'Bite is authoritative onset on first published hooked/bite/effort frame. Bite/effort fish remain submerged. Reel source0 has fish.surface only when the actual published fish alpha bounds reach the water plane; no fish.slap markers.',
    'frames': frames,
}
for target in (ART, ROOT / 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing',
               ROOT / 'fish-social-unity/Assets/StreamingAssets/Fishing'):
    (target / 'source-water-markers.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n')
print(f'Exported {len(frames)} source-frame records; {len(markers)} water markers (2 canonical one-shots + 3 continuous + 2 water-only retrieve contacts + 1 real reel fish exit); 0 fish.slap markers.')
