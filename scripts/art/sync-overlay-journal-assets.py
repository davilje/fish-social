#!/usr/bin/env python3
"""Copy the desktop journal source PNGs unchanged; WPF scales surface texels for compact HUDs."""
import hashlib
import json
import shutil
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy'
TARGET = ROOT / 'desktop-overlay/OverlayResources/hud'
SURFACES = {'button-quiet-default': (0.25, 14), 'button-quiet-hover': (0.25, 14),
            'button-quiet-pressed': (0.25, 14), 'paper-aside': (0.5, 22), 'input-default': (0.5, 14)}
def main():
    assets = []
    files = [(SOURCE / f'journal/surfaces/{name}.png', f'cozy/journal/surfaces/{name}.png', scale, border)
             for name, (scale, border) in SURFACES.items()]
    files += [(p, 'cozy/icons/' + p.name, 1, 0) for p in sorted((SOURCE/'icons').glob('*.png'))]
    files += [(p, 'cozy/characters/avatars/' + p.name, 1, 0) for p in sorted((SOURCE/'characters/avatars').glob('*.png'))]
    for source, relative, scale, border in files:
        destination = TARGET / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        assets.append({'source':str(source.relative_to(ROOT)), 'file':relative,
                       'sha256':hashlib.sha256(source.read_bytes()).hexdigest(), 'renderScale':scale, 'slice':[border]*4})
    (TARGET/'journal-assets.json').write_text(json.dumps({'schema':1,'assets':assets},ensure_ascii=False,indent=2)+'\n')
    print(f'Synced {len(assets)} desktop journal assets unchanged.')
if __name__ == '__main__': main()
