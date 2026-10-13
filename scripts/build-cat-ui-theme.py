"""Flat cartoon-cat UI kit for the desktop shell and Overlay HUD.

Plates are drawn on an 8px grid with a fixed 18px corner so nine-slice stays even.
Icons are silhouettes with no text.
"""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
# Locked HUD palette. Do not shift these hexes.
INK = (0x1B, 0x3A, 0x4A, 255)
CREAM = (0xE8, 0xD5, 0xA3, 255)
CORAL = (0xE0, 0x7A, 0x5F, 255)
TEAL = (0x2A, 0x6F, 0x8F, 255)
PANEL = (0x0F, 0x18, 0x20, 230)
SHINE = (255, 248, 236, 70)

DESTINATIONS = [
    ROOT / "desktop-overlay" / "OverlayResources" / "hud" / "theme",
    ROOT / "fish-social-unity" / "Assets" / "StreamingAssets" / "DesktopTheme",
]


def new(w: int, h: int) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image)


def plate(w: int, h: int, fill, edge) -> Image.Image:
    image, draw = new(w, h)
    radius = 16 if min(w, h) >= 44 else 12
    draw.rounded_rectangle((2, 2, w - 3, h - 3), radius, fill=fill, outline=edge, width=4)
    draw.rounded_rectangle((10, 6, w - 11, 12), 4, fill=SHINE)
    return image


def icon_cat(draw, cx, cy, scale=1.0) -> None:
    def p(dx, dy):
        return (cx + int(dx * scale), cy + int(dy * scale))

    draw.polygon([p(-16, -4), p(-8, -20), p(0, -4)], fill=CORAL, outline=INK)
    draw.polygon([p(0, -4), p(8, -20), p(16, -4)], fill=CORAL, outline=INK)
    draw.ellipse([p(-16, -8), p(16, 18)], fill=CREAM, outline=INK, width=3)
    draw.ellipse([p(-9, -2), p(-3, 4)], fill=INK)
    draw.ellipse([p(3, -2), p(9, 4)], fill=INK)
    draw.polygon([p(-2, 6), p(2, 6), p(0, 10)], fill=CORAL)


def icon_fish(draw, cx, cy) -> None:
    draw.polygon([(cx - 18, cy), (cx - 6, cy - 10), (cx - 6, cy + 10)], fill=CORAL, outline=INK)
    draw.ellipse((cx - 10, cy - 12, cx + 16, cy + 12), fill=TEAL, outline=INK, width=3)
    draw.ellipse((cx + 4, cy - 4, cx + 9, cy + 1), fill=INK)


def save(image: Image.Image, name: str) -> None:
    for folder in DESTINATIONS:
        folder.mkdir(parents=True, exist_ok=True)
        image.save(folder / name)


def build_icons() -> None:
    specs = {
        "icon-home.png": "home",
        "icon-pond.png": "pond",
        "icon-friends.png": "friends",
        "icon-shop.png": "shop",
        "icon-me.png": "me",
        "icon-settings.png": "settings",
        "icon-menu.png": "menu",
        "icon-leave.png": "leave",
        "icon-map.png": "map",
        "icon-bag.png": "bag",
        "icon-rank.png": "rank",
        "icon-chat.png": "chat",
        "icon-pan-left.png": "left",
        "icon-pan-right.png": "right",
    }
    for name, kind in specs.items():
        image, draw = new(64, 64)
        if kind == "me":
            icon_cat(draw, 32, 34)
        elif kind == "home":
            draw.polygon([(8, 30), (32, 10), (56, 30)], fill=CORAL, outline=INK)
            draw.rounded_rectangle((16, 28, 48, 54), 6, fill=CREAM, outline=INK, width=3)
            icon_cat(draw, 32, 42, 0.45)
        elif kind == "pond":
            icon_fish(draw, 34, 30)
            draw.arc((10, 40, 54, 56), 200, 340, fill=TEAL, width=3)
        elif kind == "friends":
            icon_cat(draw, 18, 38, 0.58)
            icon_cat(draw, 46, 34, 0.58)
        elif kind == "shop":
            draw.rounded_rectangle((14, 22, 50, 52), 8, fill=CREAM, outline=INK, width=3)
            draw.arc((22, 12, 42, 28), 200, 340, fill=INK, width=3)
            icon_fish(draw, 32, 38)
        elif kind == "settings":
            draw.ellipse((16, 16, 48, 48), outline=INK, width=4)
            draw.ellipse((26, 26, 38, 38), fill=CORAL, outline=INK)
            for box in ((28, 6, 36, 18), (28, 46, 36, 58), (6, 28, 18, 36), (46, 28, 58, 36)):
                draw.rounded_rectangle(box, 2, fill=TEAL, outline=INK)
        elif kind == "menu":
            for y in (18, 30, 42):
                draw.rounded_rectangle((14, y, 50, y + 6), 3, fill=CREAM, outline=INK)
        elif kind == "leave":
            draw.rounded_rectangle((14, 12, 36, 52), 6, fill=CREAM, outline=INK, width=3)
            draw.polygon([(32, 32), (52, 22), (52, 42)], fill=CORAL, outline=INK)
        elif kind == "map":
            draw.rounded_rectangle((12, 14, 52, 50), 8, fill=TEAL, outline=INK, width=3)
            draw.polygon([(32, 18), (40, 36), (32, 32), (24, 36)], fill=CORAL, outline=INK)
        elif kind == "bag":
            draw.rounded_rectangle((16, 22, 48, 54), 8, fill=CREAM, outline=INK, width=3)
            draw.arc((24, 10, 40, 28), 200, 340, fill=INK, width=3)
        elif kind == "rank":
            draw.polygon([(32, 8), (40, 26), (58, 28), (44, 40), (48, 56), (32, 46), (16, 56), (20, 40), (6, 28), (24, 26)], fill=CORAL, outline=INK)
        elif kind == "chat":
            draw.rounded_rectangle((8, 10, 56, 42), 12, fill=CREAM, outline=INK, width=3)
            draw.polygon([(16, 40), (18, 54), (30, 40)], fill=CREAM, outline=INK)
        elif kind == "left":
            draw.polygon([(40, 12), (16, 32), (40, 52)], fill=CREAM, outline=INK)
        elif kind == "right":
            draw.polygon([(24, 12), (48, 32), (24, 52)], fill=CREAM, outline=INK)
        save(image, name)


def build_plates() -> None:
    save(plate(96, 48, CORAL, INK), "btn-coral.png")
    save(plate(96, 48, CREAM, INK), "btn-cream.png")
    save(plate(96, 48, TEAL, INK), "btn-teal.png")
    dock, draw = new(128, 72)
    draw.rounded_rectangle((2, 2, 125, 69), 20, fill=PANEL, outline=INK, width=4)
    draw.rounded_rectangle((12, 8, 116, 16), 4, fill=SHINE)
    save(dock, "dock.png")
    cap, draw = new(160, 56)
    draw.rounded_rectangle((2, 2, 157, 53), 20, fill=PANEL, outline=INK, width=4)
    save(cap, "capsule.png")
    tab, draw = new(96, 40)
    draw.rounded_rectangle((2, 2, 93, 37), 12, fill=CREAM, outline=INK, width=3)
    save(tab, "tab.png")
    tab_on, draw = new(96, 40)
    draw.rounded_rectangle((2, 2, 93, 37), 12, fill=CORAL, outline=INK, width=3)
    save(tab_on, "tab-on.png")


def patch_hud() -> None:
    icons = {
        "btn_menu_map": "theme/icon-map.png",
        "btn_menu_shop": "theme/icon-shop.png",
        "btn_menu_friends": "theme/icon-friends.png",
        "btn_menu_catch": "theme/icon-bag.png",
        "btn_menu_leaderboard": "theme/icon-rank.png",
        "btn_menu_settings": "theme/icon-settings.png",
        "btn_menu_toggle": "theme/icon-menu.png",
        "btn_open_main": "theme/icon-home.png",
        "btn_exit_pond": "theme/icon-leave.png",
        "btn_pan_left": "theme/icon-pan-left.png",
        "btn_pan_right": "theme/icon-pan-right.png",
        "chat_toggle": "theme/icon-chat.png",
        "btn_fishing_toggle": "theme/btn-coral.png",
        "btn_groundbait": "theme/btn-cream.png",
        "btn_catch_leave": "theme/btn-teal.png",
        "dock_chat": "theme/dock.png",
        "cap_status": "theme/capsule.png",
    }
    slices = {
        "btn_fishing_toggle": [16, 16, 16, 16],
        "btn_groundbait": [16, 16, 16, 16],
        "btn_catch_leave": [16, 16, 16, 16],
        "dock_chat": [16, 16, 16, 16],
        "cap_status": [16, 16, 16, 16],
    }
    # 960×560. One 64px bar, 16px off the bottom. Widgets share that bar's vertical center.
    zones = {
        "menu_rail": {"x": 904, "y": 104, "w": 40, "h": 360},
        "dock_chat": {"x": 16, "y": 488, "w": 280, "h": 48},
        "dock_fishing": {"x": 371, "y": 492, "w": 218, "h": 40},
        "cap_status": {"x": 784, "y": 484, "w": 160, "h": 56},
        "btn_pan_left": {"x": 16, "y": 220, "w": 40, "h": 40},
        "btn_pan_right": {"x": 904, "y": 220, "w": 40, "h": 40},
        "txt_error": {"x": 16, "y": 16, "w": 928, "h": 24, "label": ""},
        "btn_groundbait": {"h": 40},
        "btn_fishing_toggle": {"h": 40},
        "btn_catch_leave": {"h": 40},
        "txt_status": {"x": 8, "y": 4, "w": 144, "h": 16},
        "txt_pond": {"x": 8, "y": 20, "w": 144, "h": 16},
        "txt_spot": {"x": 8, "y": 36, "w": 144, "h": 16},
    }
    ink = "#FF1B3A4A"
    cream = "#FFF6E7C1"
    for path in ROOT.rglob("overlay-hud.json"):
        if "hud" not in path.parts:
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        for widget in data.get("widgets") or []:
            wid = widget.get("id")
            if wid in icons:
                widget["sprite"] = icons[wid]
            if wid in slices:
                widget["spriteSlice"] = slices[wid]
            if wid in ("btn_fishing_toggle", "btn_groundbait", "btn_catch_leave"):
                widget["fontColor"] = cream if wid == "btn_fishing_toggle" else ink
            if wid in zones:
                widget.update(zones[wid])
        path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
        theme_src = DESTINATIONS[0]
        theme_dst = path.parent / "theme"
        if theme_dst.resolve() != theme_src.resolve():
            theme_dst.mkdir(parents=True, exist_ok=True)
            for png in theme_src.glob("*.png"):
                (theme_dst / png.name).write_bytes(png.read_bytes())
        print("hud", path)


def main() -> None:
    build_plates()
    build_icons()
    patch_hud()
    print("theme ready")


if __name__ == "__main__":
    main()
