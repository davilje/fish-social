"""Draws the Cozy UI 9-slice surfaces (buttons/plate-*, panels/*) from design tokens.

Sizes and slice borders must stay in step with CatTheme.PlateBorder / PaperBorder /
CompactPaperBorder and CozySpriteImporter: every corner radius plus its shadow margin
has to fit inside the slice border, otherwise Unity stretches the curve.

Usage: python Tools/cozy_ui_kit.py [--preview out.png]
"""
import argparse
import os

from PIL import Image, ImageDraw, ImageFilter

SS = 8
THEME = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Desktop", "Theme", "Cozy")


def hex_rgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


# One 9-slice keeps one corner radius. These families are separate on purpose:
# track radius 11 for ~25px capsules, chip radius 16 for ~38px pills,
# plate radius 10 for buttons under 58px, plate-large radius 16 for taller buttons,
# compact radius 8, card radius 18, window radius 28.
SURFACES = {
    "buttons/plate-default.png": dict(size=(64, 63), slice=12, inset=(2, 1, 2, 2), radius=10, border=1.0,
                                      stroke="#E8D5BA", top="#FFFBF4", bottom="#FBEFDC",
                                      shadow=(1.4, 1.3, 0.24), highlight=0.75),
    "buttons/plate-hover.png": dict(size=(64, 63), slice=12, inset=(2, 1, 2, 2), radius=10, border=1.4,
                                    stroke="#F0A55B", top="#FFE7C9", bottom="#FDD09E",
                                    shadow=(1.4, 1.3, 0.24), highlight=0.6),
    "buttons/plate-selected.png": dict(size=(64, 63), slice=12, inset=(2, 1, 2, 2), radius=10, border=2.0,
                                       stroke="#EE963F", top="#FFFAF1", bottom="#FDF0DD",
                                       shadow=(1.4, 1.6, 0.30), highlight=0.6),
    "buttons/plate-pressed.png": dict(size=(64, 63), slice=12, inset=(2, 1, 2, 2), radius=10, border=1.4,
                                      stroke="#E2964E", top="#FBC88F", bottom="#F6BB7E",
                                      shadow=None, highlight=0.0),
    "buttons/plate-large-default.png": dict(size=(80, 72), slice=18, inset=(2, 1, 2, 2), radius=16, border=1.1,
                                            stroke="#E8D5BA", top="#FFFBF4", bottom="#FBEFDC",
                                            shadow=(1.6, 1.5, 0.22), highlight=0.7),
    "buttons/plate-large-hover.png": dict(size=(80, 72), slice=18, inset=(2, 1, 2, 2), radius=16, border=1.5,
                                          stroke="#F0A55B", top="#FFE7C9", bottom="#FDD09E",
                                          shadow=(1.6, 1.5, 0.22), highlight=0.55),
    "buttons/plate-large-selected.png": dict(size=(80, 72), slice=18, inset=(2, 1, 2, 2), radius=16, border=2.0,
                                             stroke="#EE963F", top="#FFFAF1", bottom="#FDF0DD",
                                             shadow=(1.6, 1.8, 0.28), highlight=0.55),
    "buttons/plate-large-pressed.png": dict(size=(80, 72), slice=18, inset=(2, 1, 2, 2), radius=16, border=1.5,
                                            stroke="#E2964E", top="#FBC88F", bottom="#F6BB7E",
                                            shadow=None, highlight=0.0),
    "panels/track.png": dict(size=(40, 26), slice=12, inset=(1, 1, 1, 1), radius=11, border=1.0,
                             stroke="#E4D3B8", top="#F6EBDA", bottom="#EADCC4",
                             shadow=None, highlight=0.4),
    "panels/track-fill.png": dict(size=(40, 26), slice=12, inset=(1, 1, 1, 1), radius=11, border=1.0,
                                  stroke="#2E9A4A", top="#8BE58A", bottom="#3CB85A",
                                  shadow=None, highlight=0.75),
    "panels/chip.png": dict(size=(48, 40), slice=18, inset=(1, 1, 1, 1), radius=16, border=1.2,
                            stroke="#1E8A45", top="#6BDC78", bottom="#2FBE55",
                            shadow=None, highlight=0.6),
    "panels/paper.png": dict(size=(128, 125), slice=22, inset=(3, 2, 3, 4), radius=18, border=1.2,
                             stroke="#EAD9C1", top="#FCF4E5", bottom="#F8EDDA",
                             shadow=(1.8, 2.2, 0.14), highlight=0.5),
    "panels/paper-compact.png": dict(size=(64, 63), slice=10, inset=(2, 1, 2, 2), radius=8, border=1.0,
                                     stroke="#EAD9C1", top="#FCF4E5", bottom="#F8EDDA",
                                     shadow=(1.2, 1.2, 0.12), highlight=0.45),
    "panels/window.png": dict(size=(160, 156), slice=32, inset=(3, 2, 3, 3), radius=28, border=1.3,
                              stroke="#F0E1CA", top="#FDF8F0", bottom="#FBF2E4",
                              shadow=(2.4, 2.8, 0.26), highlight=0.8),
}


def rounded_mask(size, box, radius):
    width, height = size
    mask = Image.new("L", (width * SS, height * SS), 0)
    left, top, right, bottom = box
    ImageDraw.Draw(mask).rounded_rectangle(
        (left * SS, top * SS, right * SS - 1, bottom * SS - 1), radius=radius * SS, fill=255)
    return mask.resize(size, Image.LANCZOS)


def gradient(size, top, bottom):
    width, height = size
    top_rgb, bottom_rgb = hex_rgb(top), hex_rgb(bottom)
    column = Image.new("RGB", (1, height))
    for y in range(height):
        t = y / max(1, height - 1)
        column.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(top_rgb, bottom_rgb)))
    return column.resize(size, Image.NEAREST)


def surface(spec):
    size = spec["size"]
    width, height = size
    left, top, right, bottom = spec["inset"]
    if max(top, bottom) + spec["radius"] > spec["slice"] or max(left, right) + spec["radius"] > spec["slice"]:
        raise ValueError("corner radius and margin exceed the slice border")
    box = (left, top, width - right, height - bottom)
    out = Image.new("RGBA", size, (0, 0, 0, 0))

    if spec["shadow"]:
        offset, blur, alpha = spec["shadow"]
        big = Image.new("L", (width * SS, height * SS), 0)
        ImageDraw.Draw(big).rounded_rectangle(
            (box[0] * SS, (box[1] + offset) * SS, box[2] * SS - 1, (box[3] + offset) * SS - 1),
            radius=spec["radius"] * SS, fill=int(255 * alpha))
        shadow_mask = big.filter(ImageFilter.GaussianBlur(blur * SS)).resize(size, Image.LANCZOS)
        shadow = Image.new("RGBA", size, hex_rgb("#7A4E28") + (0,))
        shadow.putalpha(shadow_mask)
        out = Image.alpha_composite(out, shadow)

    stroke = Image.new("RGBA", size, hex_rgb(spec["stroke"]) + (255,))
    stroke.putalpha(rounded_mask(size, box, spec["radius"]))
    out = Image.alpha_composite(out, stroke)

    inner = spec["border"]
    inner_box = (box[0] + inner, box[1] + inner, box[2] - inner, box[3] - inner)
    fill = gradient(size, spec["top"], spec["bottom"]).convert("RGBA")
    fill.putalpha(rounded_mask(size, inner_box, max(0.5, spec["radius"] - inner)))
    out = Image.alpha_composite(out, fill)

    if spec["highlight"] > 0:
        rim = rounded_mask(size, inner_box, spec["radius"] - inner)
        below = rounded_mask(size, (inner_box[0], inner_box[1] + 1.2, inner_box[2], inner_box[3]), spec["radius"] - inner)
        line = Image.composite(Image.new("L", size, 0), rim, below)
        highlight = Image.new("RGBA", size, (255, 255, 255, 0))
        highlight.putalpha(line.point(lambda v: int(v * spec["highlight"])))
        out = Image.alpha_composite(out, highlight)
    return out


def preview(images, path):
    scale = 3
    tiles = [image.resize((image.width * scale, image.height * scale), Image.NEAREST) for image in images]
    sheet = Image.new("RGBA", (sum(t.width + 24 for t in tiles) + 24, max(t.height for t in tiles) + 48),
                      hex_rgb("#7FA86E") + (255,))
    x = 24
    for tile in tiles:
        sheet.alpha_composite(tile, (x, 24))
        x += tile.width + 24
    sheet.save(path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview")
    args = parser.parse_args()
    images = []
    for relative, spec in SURFACES.items():
        image = surface(spec)
        image.save(os.path.join(THEME, *relative.split("/")))
        images.append(image)
        print("wrote", relative, image.size)
    if args.preview:
        preview(images, args.preview)


if __name__ == "__main__":
    main()
