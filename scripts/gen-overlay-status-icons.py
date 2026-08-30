"""Write 20x20 fishing/hooked placeholder PNGs for STEAM-DESKTOP-14D."""
import pathlib
import struct
import zlib


def write_png(path, rgba_fn, size=20):
    rows = []
    for y in range(size):
        row = bytearray([0])
        for x in range(size):
            row.extend(rgba_fn(x, y, size))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    out = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    pathlib.Path(path).write_bytes(out)


def fishing(x, y, size):
    cx = cy = (size - 1) / 2.0
    dx, dy = x - cx, y - cy
    r2 = dx * dx + dy * dy
    if r2 > 8.6 * 8.6:
        return (0, 0, 0, 0)
    if r2 > 7.4 * 7.4:
        return (255, 255, 230, 230)
    if abs((x - 6) - (13 - y) * 0.9) < 1.2 and 4 <= x <= 14 and 4 <= y <= 13:
        return (255, 255, 255, 255)
    if (x - 13) ** 2 + (y - 5) ** 2 <= 2.4:
        return (255, 255, 255, 255)
    return (90, 168, 214, 230)


def hooked(x, y, size):
    cx = cy = (size - 1) / 2.0
    dx, dy = x - cx, y - cy
    r2 = dx * dx + dy * dy
    if r2 > 8.6 * 8.6:
        return (0, 0, 0, 0)
    if r2 > 7.4 * 7.4:
        return (255, 255, 230, 230)
    if abs(x - 9.5) < 1.1 and 4 <= y <= 13:
        return (255, 255, 255, 255)
    if 5 <= x <= 10 and 11 <= y <= 15:
        d = (x - 7.5) ** 2 + (y - 13) ** 2
        if 4.0 <= d <= 10.5:
            return (255, 255, 255, 255)
    return (232, 156, 64, 230)


def hook_ring(x, y, size):
    cx = cy = (size - 1) / 2.0
    dx, dy = x - cx, y - cy
    r = (dx * dx + dy * dy) ** 0.5
    outer, inner = size * 0.48, size * 0.38
    if r > outer or r < inner:
        return (0, 0, 0, 0)
    t = (r - inner) / max(0.001, outer - inner)
    if t < 0.18 or t > 0.82:
        return (255, 255, 230, 230)
    return (232, 156, 64, 235)


def main():
    root = pathlib.Path(__file__).resolve().parents[1] / "desktop-overlay" / "OverlayResources" / "status"
    root.mkdir(parents=True, exist_ok=True)
    write_png(root / "fishing.png", fishing)
    write_png(root / "hooked.png", hooked)
    write_png(root / "hook-ring.png", hook_ring, 80)
    print("wrote", root / "fishing.png", root / "hooked.png", root / "hook-ring.png")


if __name__ == "__main__":
    main()
