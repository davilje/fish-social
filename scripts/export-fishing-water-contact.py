"""Export visible layer bounds for native water contact; never rewrites PNG artwork."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHARED = ROOT / 'desktop-overlay/OverlayResources/fishing/shared'

def bounds(path):
    if not path.exists():
        return None
    with Image.open(path) as image:
        return image.getchannel('A').getbbox()

def build():
    result = {}
    for clip in sorted(SHARED.iterdir()):
        if not clip.is_dir():
            continue
        frames = []
        for folder in sorted(clip.iterdir(), key=lambda p: int(p.name)):
            line, bobber, fish = (bounds(folder / (name + '.png')) for name in ('line', 'bobber', 'fish'))
            frames.append(dict(LineTop=line[1] if line else 0, LineBottom=line[3] if line else 0,
                               BobberY=(bobber[1] + bobber[3]) / 2 if bobber else -1,
                               FishTop=fish[1] if fish else -1))
        result[clip.name] = frames
    return json.dumps(result, ensure_ascii=False, indent=2) + '\n'

if __name__ == '__main__':
    payload = build()
    for target in ['desktop-overlay/OverlayResources/fishing', 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing']:
        (ROOT / target / 'water-contact.json').write_text(payload)
