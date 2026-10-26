"""Build the cropped and resized assets for the cozy-journal UI prototype.

Run from the repo root:  python docs/设计/界面/原型/手账/prepare_assets.py
Source of truth stays in fish-social-unity; this only derives small web copies.
"""
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
COZY = ROOT / "fish-social-unity/Assets/Resources/Desktop/Theme/Cozy"
PREVIEWS = ROOT / "fish-social-unity/Library/CozyRedesignPreviews"
OUT = Path(__file__).resolve().parent / "assets"

# Unity sprite rects (x, y from bottom, w, h) from items-atlas.png.meta
ATLAS = {
    "rod-bamboo": (27, 729, 325, 296),
    "rod-light": (385, 729, 320, 296),
    "rod-wood": (740, 732, 336, 290),
    "rod-long": (1102, 728, 330, 294),
    "bait-basic": (27, 409, 320, 262),
    "bait-wheat": (385, 409, 321, 263),
    "bait-berry": (745, 411, 321, 260),
    "bait-shrimp": (1160, 405, 263, 269),
    "bait-mixed": (21, 59, 332, 273),
    "bait-pellet": (435, 55, 272, 282),
    "boat-wood": (736, 59, 350, 238),
    "boat-blue": (1086, 73, 358, 239),
}

FISH = [
    "koi", "carp", "crucian", "grass_carp", "black_bass", "mandarin", "silver_carp",
    "bream", "catfish", "snakehead", "golden_line", "yellow_croaker", "black_carp",
    "taimen", "chinese_sturgeon",
]

CURRENT = {
    "PanelShop.png": "shop.jpg",
    "PanelEquipment.png": "equipment.jpg",
    "PanelWorldMap.png": "worldmap.jpg",
    "PanelSettings.png": "settings.jpg",
    "PanelAchievements.png": "achievements.jpg",
    "PanelProfileHub.png": "profile.jpg",
    "PanelLeaderboard.png": "leaderboard.jpg",
    "PanelSocialFeed.png": "feed.jpg",
}


def trim(image, threshold=12):
    alpha = image.split()[-1].point(lambda value: 255 if value > threshold else 0)
    box = alpha.getbbox()
    return image.crop(box) if box else image


def fit(image, longest):
    scale = longest / max(image.size)
    if scale >= 1:
        return image
    return image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)


def main():
    (OUT / "items").mkdir(parents=True, exist_ok=True)
    (OUT / "fish").mkdir(parents=True, exist_ok=True)
    (OUT / "current").mkdir(parents=True, exist_ok=True)

    atlas = Image.open(COZY / "items/items-atlas.png").convert("RGBA")
    for name, (x, y, w, h) in ATLAS.items():
        top = atlas.height - y - h
        sprite = trim(atlas.crop((x, top, x + w, top + h)))
        fit(sprite, 320).save(OUT / "items" / (name + ".png"), optimize=True)

    for name in FISH:
        fish = trim(Image.open(COZY / "items/fish" / (name + ".png")).convert("RGBA"))
        fit(fish, 520).save(OUT / "fish" / (name + ".png"), optimize=True)

    for source, target in CURRENT.items():
        path = PREVIEWS / source
        if not path.exists():
            print("skip current", source)
            continue
        Image.open(path).convert("RGB").save(OUT / "current" / target, quality=88)

    pose = trim(Image.open(COZY / "characters/poses/orange-ready.png").convert("RGBA"))
    fit(pose, 420).save(OUT / "cat-fishing.png", optimize=True)

    for name in ("home-sakura", "world-map", "pond-afternoon"):
        image = Image.open(COZY / "scenes" / (name + ".png")).convert("RGB")
        fit(image, 1920).save(OUT / (name + ".jpg"), quality=90)

    shutil.copy(COZY / "textures/paper.png", OUT / "paper.png")
    print("ok", OUT)


if __name__ == "__main__":
    main()
