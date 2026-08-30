"""Write seats/_default.png placeholder for STEAM-DESKTOP-14A."""
import pathlib
import struct
import zlib


def write_png(path, rgba_fn, width, height):
    rows = []
    for y in range(height):
        row = bytearray([0])
        for x in range(width):
            row.extend(rgba_fn(x, y, width, height))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    out = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    pathlib.Path(path).write_bytes(out)


def seat_pixel(x, y, w, h):
    # Simple chair: back + seat pad
    back_left, back_right = w * 0.28, w * 0.72
    back_top, back_bottom = h * 0.08, h * 0.52
    seat_top, seat_bottom = h * 0.48, h * 0.88
    if back_left <= x <= back_right and back_top <= y <= back_bottom:
        edge = min(x - back_left, back_right - x, y - back_top, back_bottom - y)
        if edge < 2:
            return (255, 255, 230, 220)
        return (114, 82, 48, 235)
    if w * 0.18 <= x <= w * 0.82 and seat_top <= y <= seat_bottom:
        edge = min(x - w * 0.18, w * 0.82 - x, y - seat_top, seat_bottom - y)
        if edge < 2:
            return (255, 255, 230, 220)
        return (184, 132, 72, 240)
    return (0, 0, 0, 0)


def main():
    root = pathlib.Path(__file__).resolve().parents[1] / "desktop-overlay" / "OverlayResources" / "seats"
    root.mkdir(parents=True, exist_ok=True)
    write_png(root / "_default.png", seat_pixel, 48, 32)
    print("wrote", root / "_default.png")


if __name__ == "__main__":
    main()
