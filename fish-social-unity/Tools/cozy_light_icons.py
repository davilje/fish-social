"""Derives icons-light/<name>.png (cream glyphs for coloured buttons) from icons/<name>.png.

Dark ink becomes opaque cream and the light inner details of the dark icon (paw pads,
speech dots) become holes, so the glyph stays readable on coral and blue plates.

Usage: python Tools/cozy_light_icons.py coin social
"""
import os
import sys

from PIL import Image

THEME = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Desktop", "Theme", "Cozy")
CREAM = (0xFF, 0xF7, 0xE9)
INK, PAPER = 110, 190


def light(name):
    source = Image.open(os.path.join(THEME, "icons", name + ".png")).convert("RGBA")
    out = Image.new("RGBA", source.size, CREAM + (0,))
    pixels, target = source.load(), out.load()
    for y in range(source.height):
        for x in range(source.width):
            r, g, b, a = pixels[x, y]
            luminance = 0.299 * r + 0.587 * g + 0.114 * b
            ink = min(1.0, max(0.0, (PAPER - luminance) / (PAPER - INK)))
            target[x, y] = CREAM + (round(a * ink),)
    out.save(os.path.join(THEME, "icons-light", name + ".png"))
    print("wrote icons-light/" + name + ".png")


if __name__ == "__main__":
    for icon in sys.argv[1:]:
        light(icon)
