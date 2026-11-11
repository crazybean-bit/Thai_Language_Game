"""Cut ground tiles out of a PixelLab sheet (a 4x4 grid of 64x64 variations) and make them
repeat without seams:

    python Tools/import_tiles.py grass sheet.png 1,1 3,3 1,0 2,2
    python Tools/import_tiles.py sand sheet.png 2,0 3,3 1,1 0,2 --base 8a7a5e

Each col,row picks one variation; col,row,dx,dy shifts the square taken from it by that many
pixels (handy for two tiles from one good variation). A 40x40 square from its middle is folded into a 32x32 tile:
the 8 pixels past each edge are dithered into the opposite edge, so the tile wraps cleanly.
--base fills transparent pixels with that colour (lightly mottled), for sheets that came
out with the background removed. Tiles go to Tools/art/tiles/<name>_<i>.png, which
generate_sprites.py copies over its placeholders, and straight into Assets/Resources/Sprites.
"""
import os
import random
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Tools", "art", "tiles")
OUT = os.path.join(ROOT, "Assets", "Resources", "Sprites")
CELL, TILE, OVERLAP = 64, 32, 8


def mottled(base, size, seed):
    rnd = random.Random(seed)
    r, g, b = (int(base[i:i + 2], 16) for i in (0, 2, 4))
    img = Image.new("RGBA", (size, size))
    for y in range(size):
        for x in range(size):
            k = 1 + (rnd.random() - 0.5) * 0.08
            img.putpixel((x, y), (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), 255))
    return img


def fold(src, seed):
    """src is TILE + OVERLAP square; returns a TILE square that wraps."""
    rnd = random.Random(seed)
    px = src.load()
    n = TILE + OVERLAP
    # Columns first: x < OVERLAP takes from x + TILE, more often the closer to the edge.
    cols = Image.new("RGBA", (TILE, n))
    for y in range(n):
        for x in range(TILE):
            use_far = x < OVERLAP and rnd.random() > (x + 0.5) / OVERLAP
            cols.putpixel((x, y), px[x + TILE, y] if use_far else px[x, y])
    cpx = cols.load()
    out = Image.new("RGBA", (TILE, TILE))
    for y in range(TILE):
        for x in range(TILE):
            use_far = y < OVERLAP and rnd.random() > (y + 0.5) / OVERLAP
            out.putpixel((x, y), cpx[x, y + TILE] if use_far else cpx[x, y])
    return out


def main(argv):
    base = None
    if "--base" in argv:
        i = argv.index("--base")
        base = argv[i + 1].lstrip("#")
        argv = argv[:i] + argv[i + 2:]
    if len(argv) < 3:
        sys.exit(__doc__)
    name, sheet_path, cells = argv[0], argv[1], argv[2:]
    sheet = Image.open(sheet_path).convert("RGBA")
    os.makedirs(ART, exist_ok=True)
    n = TILE + OVERLAP
    off = (CELL - n) // 2
    for i, cell in enumerate(cells):
        c, r, dx, dy = ([int(v) for v in cell.split(",")] + [0, 0])[:4]
        x, y = c * CELL + off + dx, r * CELL + off + dy
        crop = sheet.crop((x, y, x + n, y + n))
        if base:
            under = mottled(base, n, i)
            under.alpha_composite(crop)
            crop = under
        tile = fold(crop, i)
        if not base and tile.getchannel("A").getextrema()[0] < 255:
            print(f"warning: {name}_{i} ({cell}) has see-through pixels; pass --base or pick another")
        for d in (ART, OUT):
            tile.save(os.path.join(d, f"{name}_{i}.png"))
        print(f"{name}_{i} <- {cell}")


if __name__ == "__main__":
    main(sys.argv[1:])
