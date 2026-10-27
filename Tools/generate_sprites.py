"""Generates the placeholder pixel art in Assets/Resources/Sprites.

Run with: python3 Tools/generate_sprites.py   (needs Pillow and numpy)

The style aims for the dark, muted, softly outlined look of games like
7 Days to End with You: desaturated colours, outlines tinted by the colour they
border, tall walls, and cluttered rooms. Replace any PNG with real art of the
same name and the game picks it up. Sizes:
  - floor/wall tiles: 32x32
  - objects: 32 wide, 32-64 tall (bottom edge sits on the object's tile)
  - character sheets: RPG Maker layout, 3 columns (step, idle, step) x 4 rows
    (down, left, right, up); frames here are 32x48, any size works
  - portraits: any size, shown beside the dialogue box
"""
import os
import shutil
from pixel import Canvas, rgb, shade, noise

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Sprites")

# Shared muted palette
WOOD = rgb("6a4a38")
WOOD_DARK = rgb("4a3328")
WOOD_LIGHT = rgb("85604a")
PAPER = rgb("ddd3bf")
BRASS = rgb("b8955a")


def save(c, name):
    c.save(os.path.join(OUT, name + ".png"))


def grain(c, x0, y0, x1, y1, base, seed, strength=0.12, stretch=4):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            n = noise(x // stretch, y, seed)
            c.put(x, y, shade(base, (n - 0.5) * strength))


def drop_shadow(c, cx, cy, rx, ry=1.8, alpha=80):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1 and not c.filled(x, y):
                c.put(x, y, (10, 6, 8, alpha))


# ============================================================== tiles

def floor(variant):
    c = Canvas(32, 32)
    woods = [rgb("5f4232"), rgb("583d2f"), rgb("664737"), rgb("5c4030")]
    for plank in range(4):
        y0 = plank * 8
        base = woods[(plank + variant * 3) % 4]
        grain(c, 0, y0, 31, y0 + 7, base, plank + variant * 11, 0.14, 5)
        c.hline(0, 31, y0 + 1, shade(base, 0.05))
        c.hline(0, 31, y0, shade(base, -0.42))
        if noise(plank, variant, 3) > 0.45:
            seam = int(noise(plank, variant, 7) * 24) + 4
            c.vline(seam, y0, y0 + 7, shade(base, -0.42))
            c.vline(seam + 1, y0 + 1, y0 + 7, shade(base, 0.05))
    save(c, f"floor_{variant}")


def wall_upper(variant):
    """Upper wall: dull patterned wallpaper under the ceiling shadow."""
    c = Canvas(32, 32)
    base = rgb("4d4038") if variant == 0 else rgb("4a3e36")
    for y in range(32):
        for x in range(32):
            col = base
            if x % 8 in (0, 1):
                col = shade(base, -0.12)
            # small diamond print
            dx, dy = abs((x % 8) - 4), abs((y % 10) - 5)
            if dx + dy == 2:
                col = shade(base, 0.1)
            c.put(x, y, shade(col, (noise(x, y, 5 + variant) - 0.5) * 0.06))
    for y in range(6):                                # ceiling shadow
        for x in range(32):
            c.put(x, y, (12, 8, 10, int(150 * (1 - y / 6))))
    save(c, f"wall_upper_{variant}")


def wall_lower(variant):
    """Lower wall: dark wood wainscot with a rail and baseboard."""
    c = Canvas(32, 32)
    for board in range(4):
        x0 = board * 8
        base = [rgb("4c3429"), rgb("473126"), rgb("50372b"), rgb("4a3227")][(board + variant) % 4]
        grain(c, x0, 0, x0 + 7, 31, base, board + variant * 5, 0.12, 1)
        c.vline(x0, 0, 31, shade(base, -0.4))
        c.vline(x0 + 1, 0, 31, shade(base, 0.08))
    c.rect(0, 0, 31, 2, rgb("6a4a38"))               # rail
    c.hline(0, 31, 0, rgb("85604a"))
    c.hline(0, 31, 3, rgb("2c1e18"))
    c.rect(0, 27, 31, 28, rgb("2c1e18"))             # baseboard
    c.rect(0, 29, 31, 31, rgb("3d2a21"))
    save(c, f"wall_lower_{variant}")


def wall_top():
    c = Canvas(32, 32)
    base = rgb("1f1817")
    for y in range(32):
        for x in range(32):
            c.put(x, y, shade(base, (noise(x // 2, y // 2, 3) - 0.5) * 0.15))
    save(c, "wall_top")


def ambient_occlusion():
    """Soft shadows laid over floor tiles that touch a wall."""
    for name, f in (("ao_top", lambda x, y: y), ("ao_left", lambda x, y: x), ("ao_right", lambda x, y: 31 - x)):
        c = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                d = f(x, y)
                if d < 10:
                    c.px[y, x] = (8, 4, 6, int(120 * (1 - d / 10) ** 1.6))
        save(c, name)


def rug(mask):
    """mask bits: 1 = top edge, 2 = right edge, 4 = bottom edge, 8 = left edge."""
    c = Canvas(32, 32)
    red, dark, gold, deep = rgb("7a2a2e"), rgb("5a1e24"), rgb("b08a4c"), rgb("3c141a")
    for y in range(32):
        for x in range(32):
            col = red
            dx, dy = abs((x % 8) - 4), abs((y % 8) - 4)
            if dx + dy == 2:
                col = gold if (x // 8 + y // 8) % 2 == 0 else shade(red, 0.12)
            elif dx + dy == 0:
                col = dark
            c.put(x, y, shade(col, (noise(x, y, 9) - 0.5) * 0.1))
    for y in range(32):
        for x in range(32):
            d = 99
            if mask & 1: d = min(d, y)
            if mask & 4: d = min(d, 31 - y)
            if mask & 8: d = min(d, x)
            if mask & 2: d = min(d, 31 - x)
            if d < 2: c.put(x, y, deep)
            elif d < 3: c.put(x, y, gold)
            elif d < 6: c.put(x, y, dark if ((x + y) // 2) % 2 == 0 else red)
            elif d < 7: c.put(x, y, gold)
    if mask & 8:
        for y in range(0, 32, 2): c.put(0, y, rgb("cbb894"))
    if mask & 2:
        for y in range(0, 32, 2): c.put(31, y, rgb("cbb894"))
    save(c, f"rug_{mask}")


# ============================================================== furniture (floor)

def bed():
    """Two tiles tall: the head of the bed is against the wall."""
    c = Canvas(32, 64)
    c.rect(2, 2, 29, 61, WOOD)
    grain(c, 2, 2, 29, 13, WOOD_LIGHT, 4, 0.1, 3)            # headboard
    c.hline(2, 29, 2, shade(WOOD_LIGHT, 0.15))
    c.rect(4, 14, 27, 60, rgb("cfc7b4"))                     # sheet
    c.rect(6, 16, 25, 24, rgb("e2dbcb"))                     # pillow
    c.hline(6, 25, 24, rgb("aaa190")); c.vline(25, 16, 24, rgb("b8af9d"))
    c.hline(8, 20, 17, rgb("efe9dc"))
    for y in range(29, 61):                                  # pha khao ma blanket
        for x in range(4, 28):
            col = rgb("3c5270")
            if (x // 3) % 2 == 0 and (y // 3) % 2 == 0: col = rgb("50688a")
            elif (x // 3) % 2 == 1 and (y // 3) % 2 == 1: col = rgb("2e405a")
            c.put(x, y, col)
    c.hline(4, 27, 29, rgb("6d84a6")); c.hline(4, 27, 30, rgb("cfc7b4"))
    for y in range(32, 60, 6):                               # folds
        c.hline(5, 9, y, rgb("2a3a52"))
    c.rect(2, 60, 29, 62, WOOD_DARK)
    c.soft_outline()
    save(c, "bed")


def bookshelf():
    c = Canvas(32, 64)
    c.rect(2, 4, 29, 61, WOOD_DARK)
    c.rect(4, 6, 27, 59, rgb("2a1d17"))
    c.hline(2, 29, 4, WOOD_LIGHT)
    book_cols = [rgb("6b2f2f"), rgb("3e5a4a"), rgb("8a7a4a"), rgb("3a4a66"), rgb("7a5a3a"),
                 rgb("5a3a5a"), rgb("a89070"), rgb("4a6a6a")]
    for shelf, y0 in enumerate((7, 21, 35, 49)):
        x = 4
        i = shelf * 5
        while x < 27:
            w = 2 + int(noise(i, shelf, 1) * 3)
            h = 9 + int(noise(i, shelf, 2) * 3)
            if noise(i, shelf, 3) > 0.85:          # a gap or a lying book
                c.rect(x, y0 + 11 - 2, min(26, x + 6), y0 + 11, book_cols[i % 8])
                x += 7
            else:
                col = book_cols[(i * 3 + shelf) % 8]
                c.rect(x, y0 + 12 - h, min(26, x + w - 1), y0 + 11, col)
                c.vline(x, y0 + 12 - h, y0 + 11, shade(col, 0.15))
                c.hline(x, min(26, x + w - 1), y0 + 14 - h, shade(col, 0.3))
                x += w
            i += 1
        c.rect(4, y0 + 12, 27, y0 + 13, WOOD)
        c.hline(4, 27, y0 + 12, WOOD_LIGHT)
    c.rect(2, 60, 29, 62, WOOD)
    c.soft_outline()
    save(c, "bookshelf")


def table():
    c = Canvas(32, 32)
    top = rgb("6e4c38")
    c.rect(4, 22, 6, 30, WOOD_DARK); c.rect(25, 22, 27, 30, WOOD_DARK)
    grain(c, 2, 9, 29, 21, top, 8, 0.1, 4)
    c.hline(2, 29, 9, shade(top, 0.18))
    c.rect(2, 21, 29, 23, WOOD_DARK)
    c.rect(8, 11, 17, 18, PAPER)                             # letter
    c.hline(8, 17, 18, rgb("a89c84")); c.vline(17, 11, 18, rgb("b8ac94"))
    for y in (13, 15):
        c.hline(10, 15, y, rgb("5a4e46"))
    # teal kettle, like the one in the reference screenshot
    kettle, kettle_d, kettle_l = rgb("3f7a78"), rgb("2c5654"), rgb("6aa6a0")
    c.ellipse(23.5, 13, 4.5, 4, kettle)
    c.rect(20, 13, 27, 17, kettle)
    c.vline(27, 12, 17, kettle_d); c.hline(20, 27, 17, kettle_d)
    c.rect(22, 7, 25, 8, kettle_d)                           # lid
    c.hline(21, 23, 11, kettle_l); c.put(21, 12, kettle_l)
    c.rect(16, 11, 19, 12, kettle)                           # spout
    c.put(16, 10, kettle)
    c.vline(28, 11, 15, kettle_d)                            # handle
    # candle
    c.rect(4, 13, 5, 18, rgb("e2d8c2"))
    c.put(4, 12, rgb("2a1e18")); c.put(4, 10, rgb("ffd27a")); c.put(4, 11, rgb("ff9a3a"))
    c.soft_outline()
    drop_shadow(c, 16, 30, 13)
    save(c, "table")


def stool():
    c = Canvas(32, 32)
    c.ellipse(16, 16, 8, 4, WOOD_LIGHT)
    c.ellipse(16, 16.5, 7, 3, WOOD)
    c.rect(10, 18, 11, 29, WOOD_DARK); c.rect(20, 18, 21, 29, WOOD_DARK)
    c.rect(15, 19, 16, 27, shade(WOOD_DARK, -0.15))
    c.hline(11, 20, 24, WOOD_DARK)
    c.soft_outline()
    drop_shadow(c, 16, 30, 8)
    save(c, "stool")


def jar():
    c = Canvas(32, 32)
    glaze, glaze_d, glaze_l = rgb("5e3424"), rgb("3e2219"), rgb("7d4a32")
    c.ellipse(16, 19, 10, 11.5, glaze)
    for y in range(32):
        for x in range(32):
            if c.filled(x, y):
                if x > 20: c.put(x, y, glaze_d)
                elif x < 11 and y < 22: c.put(x, y, glaze_l)
    c.rect(10, 5, 21, 8, glaze_d); c.hline(10, 21, 5, glaze_l)
    c.rect(11, 6, 20, 7, rgb("1c2a36"))
    for (x, y) in [(9, 18), (10, 16), (12, 15), (14, 16), (15, 18), (17, 19), (19, 18), (20, 16), (22, 15)]:
        c.put(x, y, rgb("b8903e")); c.put(x, y + 1, rgb("8a6a2c"))
    c.put(13, 11, rgb("a87a5a")); c.put(12, 12, rgb("a87a5a"))
    c.soft_outline()
    drop_shadow(c, 16, 31, 10)
    save(c, "jar")


def lamp():
    """A tall wooden stand with an oil lamp; the main light in the room."""
    c = Canvas(32, 48)
    c.rect(14, 22, 17, 44, WOOD_DARK)
    c.vline(14, 22, 44, WOOD)
    c.rect(10, 44, 21, 46, WOOD)
    c.rect(9, 20, 22, 21, WOOD_LIGHT)
    c.rect(12, 15, 19, 19, BRASS)
    c.hline(12, 19, 15, shade(BRASS, 0.3))
    c.ellipse(15.5, 9, 4.5, 6, (240, 225, 190, 170))
    c.rect(15, 7, 16, 11, rgb("ffb347"))
    c.put(15, 6, rgb("fff2b0")); c.put(16, 8, rgb("fff2b0"))
    c.rect(13, 2, 18, 3, shade(BRASS, -0.2))
    c.soft_outline()
    drop_shadow(c, 16, 46, 7)
    save(c, "lamp")


def plant():
    c = Canvas(32, 48)
    leaf, leaf_d, leaf_l = rgb("3d5e3a"), rgb("2a4228"), rgb("5a7e52")
    for (cx, cy, rx, ry) in [(10, 18, 5, 11), (22, 17, 5, 11), (16, 10, 4, 10), (7, 26, 5, 4), (25, 26, 5, 4), (16, 22, 4, 6)]:
        c.ellipse(cx, cy, rx, ry, leaf)
    for (cx, cy) in [(10, 18), (22, 17), (16, 10)]:
        c.vline(cx, cy - 7, cy + 8, leaf_d)
        c.put(cx - 1, cy - 4, leaf_l); c.put(cx - 1, cy - 3, leaf_l)
    c.poly([(9, 31), (23, 31), (21, 45), (11, 45)], rgb("7a4a34"))
    c.hline(9, 23, 31, rgb("94603f")); c.hline(10, 22, 32, rgb("5a3424"))
    c.soft_outline()
    drop_shadow(c, 16, 46, 7)
    save(c, "plant")


def cat():
    c = Canvas(32, 32)
    fur, fur_d, fur_l, white = rgb("c07a40"), rgb("8e552c"), rgb("d89a60"), rgb("e6dac8")
    c.rect(25, 22, 27, 28, fur); c.rect(26, 19, 28, 22, fur)
    c.ellipse(16, 23, 8, 8, fur)
    c.ellipse(16, 13, 6.5, 6, fur)
    c.poly([(9.5, 11), (10.5, 4), (14, 8)], fur)
    c.poly([(22.5, 11), (21.5, 4), (18, 8)], fur)
    c.put(11, 7, rgb("c88080")); c.put(21, 7, rgb("c88080"))
    c.ellipse(16, 25, 4, 5, white)
    for y in (18, 22, 26):
        c.hline(8, 10, y, fur_d); c.hline(22, 24, y, fur_d)
    c.hline(12, 20, 9, fur_l)
    for ex in (12, 19):
        c.rect(ex, 12, ex + 1, 14, rgb("c8b040")); c.vline(ex + 1, 12, 14, rgb("201818"))
    c.put(16, 15, rgb("a86060"))
    c.rect(12, 29, 14, 30, white); c.rect(18, 29, 20, 30, white)
    c.soft_outline()
    drop_shadow(c, 16, 31, 9)
    save(c, "cat")


# ============================================================== wall decor
# These sit on the lower wall tile and are 64 px tall, so they reach up the wall.

def window():
    c = Canvas(32, 64)
    frame, frame_l = rgb("3a2820"), rgb("5e4232")
    c.rect(4, 8, 27, 44, frame)
    for y in range(10, 43):
        for x in range(6, 26):
            t = (y - 10) / 32
            glass = (int(30 + 20 * t), int(42 + 26 * t), int(64 + 30 * t), 255)   # night sky
            if noise(x // 2, y // 2, 4) > 0.82:
                glass = shade(glass, 0.25)
            c.put(x, y, glass)
    for (x, y) in [(9, 13), (21, 17), (12, 24), (19, 30)]:
        c.put(x, y, rgb("c8d4e0"))                                  # stars or rain drops
    c.vline(15, 10, 42, frame); c.vline(16, 10, 42, frame_l)
    c.hline(6, 25, 26, frame); c.hline(6, 25, 25, frame_l)
    c.hline(4, 27, 8, frame_l)
    c.rect(3, 44, 28, 46, rgb("6a4a38")); c.hline(3, 28, 44, rgb("85604a"))
    for y in range(5, 48):                                          # curtains
        w = 3 if y < 40 else 4
        for x in range(1, 1 + w):
            c.put(x, y, shade(rgb("8a7458"), -0.18 * ((x + y // 3) % 2)))
        for x in range(31 - w, 31):
            c.put(x, y, shade(rgb("8a7458"), -0.18 * ((x + y // 3) % 2)))
    c.hline(0, 31, 4, rgb("2c1e18"))
    c.soft_outline()
    save(c, "window")


def mirror():
    c = Canvas(32, 64)
    c.ellipse(16, 24, 8.5, 13, rgb("7d5e2c"))
    c.ellipse(16, 24, 7.5, 12, BRASS)
    for y in range(64):
        for x in range(32):
            dx, dy = (x + 0.5 - 16) / 6, (y + 0.5 - 24) / 10.5
            if dx * dx + dy * dy <= 1:
                t = (y - 13) / 22
                c.put(x, y, (int(70 + 40 * t), int(86 + 36 * t), int(98 + 30 * t), 255))
    for i in range(6):
        c.put(12 + i // 2, 17 + i, rgb("c4d2d8"))
    c.soft_outline()
    save(c, "mirror")


def door():
    c = Canvas(32, 64)
    c.rect(4, 10, 27, 61, rgb("24180f"))
    wood = rgb("5a3c2a")
    grain(c, 6, 12, 25, 61, wood, 2, 0.14, 1)
    for (y0, y1) in ((15, 33), (37, 57)):
        c.rect(8, y0, 23, y1, shade(wood, -0.15))
        c.hline(8, 23, y0, shade(wood, -0.35)); c.vline(8, y0, y1, shade(wood, -0.35))
        c.hline(9, 23, y1, shade(wood, 0.1)); c.vline(23, y0 + 1, y1, shade(wood, 0.1))
    c.rect(21, 35, 22, 36, BRASS); c.put(21, 35, rgb("e8d090"))
    c.hline(6, 25, 61, rgb("8a7a5a"))
    c.soft_outline()
    save(c, "door")


def shrine():
    """A small wall shelf with an offering, a common sight in Thai homes."""
    c = Canvas(32, 64)
    c.rect(3, 30, 28, 32, rgb("6a2e1e")); c.hline(3, 28, 30, rgb("8e4a2e"))
    c.poly([(5, 33), (26, 33), (22, 37), (9, 37)], rgb("4a2016"))
    c.rect(13, 20, 18, 29, rgb("b08a38"))
    c.ellipse(15.5, 19, 2.5, 2.5, rgb("c8a048"))
    c.put(15, 16, rgb("c8a048"))
    c.vline(18, 22, 28, rgb("8a6a28"))
    c.rect(6, 26, 8, 29, rgb("e6e4d8")); c.put(7, 25, rgb("7aa060"))
    c.rect(23, 25, 25, 29, rgb("e6e4d8")); c.put(24, 24, rgb("7aa060"))
    c.vline(10, 23, 29, rgb("a89878")); c.put(10, 22, rgb("ff8a3a"))
    for i in range(4):                                             # incense smoke
        c.put(10 + (i % 2), 21 - i * 2, (200, 200, 200, 90))
    c.soft_outline()
    save(c, "shrine")


def calendar():
    c = Canvas(32, 64)
    c.vline(15, 6, 9, rgb("2a1e18"))
    c.rect(8, 10, 23, 30, PAPER)
    c.rect(8, 10, 23, 14, rgb("8a3a34"))
    for row in range(4):
        for col in range(5):
            x, y = 9 + col * 3, 16 + row * 3
            c.rect(x, y, x + 1, y + 1, rgb("6a5e54"))
    # one day circled, then the rest crossed out: a countdown?
    c.rect(18, 22, 19, 23, rgb("b03a3c"))
    for col in range(5):
        c.put(10 + col * 3, 17, rgb("b03a3c"))
    c.soft_outline()
    save(c, "calendar")


def painting():
    c = Canvas(32, 64)
    c.rect(4, 10, 27, 30, rgb("6a4a2a"))
    c.rect(6, 12, 25, 28, rgb("3a5040"))
    for x in range(6, 26):                                          # hills and a temple roof
        h = 22 + int(3 * noise(x // 3, 1, 8))
        c.vline(x, h, 28, rgb("2e4032"))
    c.poly([(14, 21), (17, 15), (20, 21)], rgb("8a4a2a"))
    c.rect(15, 21, 19, 24, rgb("c8b080"))
    c.rect(8, 14, 10, 15, rgb("d8c890"))                            # moon
    c.soft_outline()
    save(c, "painting")


def coat():
    c = Canvas(32, 64)
    c.rect(13, 8, 18, 9, BRASS)
    c.poly([(9, 14), (23, 14), (25, 40), (7, 40)], rgb("cfc8b8"))   # the white jacket from the reference
    c.poly([(14, 14), (18, 14), (16, 24)], rgb("8a8478"))
    c.vline(16, 24, 40, rgb("a8a294"))
    c.rect(6, 16, 8, 34, rgb("bdb6a6")); c.rect(24, 16, 26, 34, rgb("bdb6a6"))
    c.put(12, 30, rgb("8a8478")); c.put(20, 30, rgb("8a8478"))
    c.vline(15, 10, 13, rgb("6a5e50"))
    c.soft_outline()
    save(c, "coat")


# ============================================================== outdoors (the yard)

def sand(variant):
    c = Canvas(32, 32)
    base = rgb("8a7a5e")
    for y in range(32):
        for x in range(32):
            n = noise(x // 2, y // 2, 40 + variant)
            col = shade(base, (n - 0.5) * 0.16)
            if noise(x, y, 50 + variant) > 0.93:
                col = shade(base, 0.18)                       # shell grit
            c.put(x, y, col)
    for i in range(3):                                        # wet streaks left by the wave
        y = int(noise(i, variant, 61) * 28) + 2
        x0 = int(noise(i, variant, 62) * 16)
        c.hline(x0, x0 + 10, y, shade(base, -0.18))
    save(c, f"sand_{variant}")


def grass(variant):
    c = Canvas(32, 32)
    base = rgb("4a5a3a")
    for y in range(32):
        for x in range(32):
            n = noise(x // 2, y // 3, 70 + variant)
            col = shade(base, (n - 0.5) * 0.2)
            if noise(x, y, 80 + variant) > 0.9:
                col = shade(base, 0.16)
            elif noise(x, y, 81 + variant) > 0.92:
                col = rgb("6a5e48")                           # mud showing through
            c.put(x, y, col)
    save(c, f"grass_{variant}")


def sea(variant):
    c = Canvas(32, 32)
    base = rgb("2c4256")
    for y in range(32):
        for x in range(32):
            n = noise((x + variant * 5) // 3, y // 2, 90)
            c.put(x, y, shade(base, (n - 0.5) * 0.18))
    for i in range(4):
        y = 4 + i * 8 + (variant * 3) % 5
        x0 = int(noise(i, variant, 91) * 20)
        c.hline(x0, x0 + 6, y, shade(base, 0.22))
        c.hline(x0 + 2, x0 + 4, y - 1, shade(base, 0.32))
    save(c, f"sea_{variant}")


def fence():
    """Hedge and posts around the yard."""
    c = Canvas(32, 32)
    leaf = rgb("33452c")
    for y in range(32):
        for x in range(32):
            c.put(x, y, shade(leaf, (noise(x // 2, y // 2, 95) - 0.5) * 0.3))
    for (x, y) in [(5, 6), (20, 4), (12, 18), (26, 22), (3, 26)]:
        c.ellipse(x, y, 3, 2, shade(leaf, 0.15))
    save(c, "fence")


def roof(variant):
    """Rusty corrugated iron."""
    c = Canvas(32, 32)
    base = rgb("6a4a3c") if variant == 0 else rgb("634538")
    for y in range(32):
        for x in range(32):
            col = shade(base, 0.12) if x % 4 in (0, 1) else shade(base, -0.12)
            if noise(x // 3, y // 3, 100 + variant) > 0.78:
                col = shade(rgb("8a5530"), (noise(x, y, 101) - 0.5) * 0.2)   # rust
            c.put(x, y, col)
    save(c, f"roof_{variant}")


def house_wall(variant):
    """The cabin's outside: weathered horizontal planks on stilts."""
    c = Canvas(32, 32)
    for plank in range(4):
        y0 = plank * 8
        base = [rgb("6e5a48"), rgb("665342"), rgb("72604c"), rgb("6a5644")][(plank + variant) % 4]
        grain(c, 0, y0, 31, y0 + 7, base, plank + variant * 7 + 30, 0.14, 6)
        c.hline(0, 31, y0, shade(base, -0.4))
    c.rect(0, 0, 31, 3, (10, 8, 10, 120))                     # eave shadow
    for x in range(0, 32):                                    # tide line from the wave
        c.put(x, 22 + int(noise(x // 4, 0, 33) * 2), shade(rgb("4a4034"), -0.1))
    save(c, f"house_wall_{variant}")


def generator():
    c = Canvas(32, 32)
    body, dark = rgb("8a3a2a"), rgb("5a2418")
    c.rect(4, 12, 27, 27, rgb("3a3634"))                      # frame
    c.rect(6, 10, 25, 24, body)
    c.hline(6, 25, 10, shade(body, 0.25))
    c.rect(8, 14, 15, 21, rgb("2a2624"))                      # engine
    for y in range(15, 21, 2):
        c.hline(9, 14, y, rgb("4a4440"))
    c.rect(18, 13, 23, 16, rgb("c8b060"))                     # fuel cap and label
    c.ellipse(21, 12, 2, 1, rgb("3a3634"))
    c.rect(5, 26, 8, 29, rgb("1e1a1a")); c.rect(23, 26, 26, 29, rgb("1e1a1a"))
    c.rect(26, 18, 29, 25, rgb("b03a2a"))                     # red fuel can
    c.hline(26, 29, 18, rgb("d05a40"))
    for x in range(2, 30):                                    # wet sand piled on one side
        h = int(3 + noise(x // 3, 1, 34) * 3)
        for y in range(29 - h, 30):
            if x < 14:
                c.put(x, y, shade(rgb("7a6a50"), (noise(x, y, 35) - 0.5) * 0.2))
    c.soft_outline()
    drop_shadow(c, 16, 30, 12)
    save(c, "generator")


def spirit_house_fallen():
    """A san phra phum knocked off its post, lying in the mud."""
    c = Canvas(32, 32)
    c.rect(2, 24, 22, 26, rgb("8a7a6a"))                      # the post, on its side
    c.hline(2, 22, 24, rgb("a89a88"))
    c.poly([(14, 8), (28, 12), (26, 26), (12, 22)], rgb("c8a85a"))   # tilted little house
    c.poly([(12, 8), (22, 2), (30, 10)], rgb("a83a2a"))       # roof
    c.poly([(18, 4), (22, 2), (21, 0)], rgb("d8b860"))        # roof finial
    c.rect(17, 13, 22, 19, rgb("3a2a20"))                     # doorway
    for (x, y) in [(6, 20), (9, 22), (25, 27)]:               # scattered garlands
        c.ellipse(x, y, 1.5, 1.5, rgb("e8e0c8"))
    c.put(28, 28, rgb("d88040"))
    c.soft_outline()
    drop_shadow(c, 16, 29, 13)
    save(c, "spirit_house_fallen")


def boat():
    """An upturned longtail hull, two tiles wide."""
    c = Canvas(64, 32)
    hull, hull_d = rgb("4e6a78"), rgb("344a56")
    c.poly([(4, 24), (10, 12), (54, 10), (62, 22), (56, 28), (8, 28)], hull)
    for y in range(13, 27, 3):
        c.hline(9, 56, y, hull_d)
    c.poly([(54, 10), (62, 4), (63, 8), (60, 20)], rgb("6a8a96"))     # raised prow
    for (x, y) in [(56, 6), (58, 9)]:                         # ribbon on the prow
        c.put(x, y, rgb("d84a6a")); c.put(x + 1, y, rgb("e8c048"))
    c.rect(20, 18, 30, 20, rgb("8a6a4a"))                     # a patch of planking
    c.soft_outline()
    drop_shadow(c, 32, 30, 28)
    save(c, "boat")


def palm_fallen():
    c = Canvas(32, 48)
    trunk = rgb("7a6248")
    for i in range(30):                                       # trunk lying across the path
        y = 30 + int(i * 0.25)
        c.rect(1 + i, y, 1 + i, y + 4, shade(trunk, (i % 4 == 0) * -0.2))
    for (cx, cy, rx, ry) in [(6, 22, 6, 9), (12, 18, 8, 6), (3, 30, 4, 8)]:
        c.ellipse(cx, cy, rx, ry, rgb("4a6a34"))
    for x in range(2, 18, 3):
        c.vline(x, 14, 30, rgb("355024"))
    c.soft_outline()
    drop_shadow(c, 16, 44, 14)
    save(c, "palm_fallen")


def outdoors():
    for v in range(4):
        sand(v)
        grass(v)
    for v in range(2):
        sea(v)
        roof(v)
        house_wall(v)
    for f in (fence, generator, spirit_house_fallen, boat, palm_fallen, debris):
        f()


def debris():
    c = Canvas(32, 32)
    c.poly([(2, 22), (20, 16), (22, 19), (4, 25)], rgb("7a6a58"))   # boards
    c.poly([(10, 26), (28, 22), (29, 25), (11, 29)], rgb("6a5a48"))
    c.rect(18, 10, 26, 18, rgb("2e5a8a"))                     # a blue plastic chair
    c.rect(18, 18, 19, 23, rgb("244a72")); c.rect(25, 18, 26, 23, rgb("244a72"))
    c.ellipse(7, 27, 3, 1.5, rgb("c84a3a"))                   # one sandal
    c.soft_outline()
    drop_shadow(c, 16, 29, 13)
    save(c, "debris")


# ============================================================== characters

class Look:
    def __init__(self, hair, skin, top, bottom, shoes, eyes, long_hair=False, skirt=False, blush=False):
        self.hair, self.skin, self.top, self.bottom = hair, skin, top, bottom
        self.shoes, self.eyes = shoes, eyes
        self.long_hair, self.skirt, self.blush = long_hair, skirt, blush


def hair_strands(c, x0, x1, y0, y1, hair, seed):
    """Darker vertical strands and a few highlights, so hair isn't a flat blob."""
    dark, light = shade(hair, -0.25), shade(hair, 0.18)
    for x in range(x0, x1 + 1):
        if noise(x, seed, 21) > 0.62:
            for y in range(y0, y1 + 1):
                if c.get(x, y)[:3] == hair[:3]:
                    c.put(x, y, dark)
        elif noise(x, seed, 22) > 0.8:
            for y in range(y0, min(y1, y0 + 3) + 1):
                if c.get(x, y)[:3] == hair[:3]:
                    c.put(x, y, light)


def draw_frame(look, facing, step):
    """facing: down/left/up (right is a mirrored left). step: -1, 0, 1."""
    c = Canvas(32, 48)
    bob = 0 if step == 0 else 1
    hair, hair_d, hair_l = look.hair, shade(look.hair, -0.3), shade(look.hair, 0.22)
    skin, skin_d = look.skin, shade(look.skin, -0.16)
    top, top_d, top_l = look.top, shade(look.top, -0.25), shade(look.top, 0.12)
    bottom, bottom_d = look.bottom, shade(look.bottom, -0.25)
    hy = 15 + bob

    if look.long_hair:
        if facing == "down":
            c.rect(7, hy, 24, 34 + bob, hair_d)
        elif facing == "left":
            c.rect(14, hy, 24, 34 + bob, hair_d)

    # legs
    if facing in ("down", "up"):
        l_end = 44 - (2 if step == 1 else 0)
        r_end = 44 - (2 if step == -1 else 0)
        c.rect(12, 36, 15, l_end, bottom); c.vline(15, 36, l_end, bottom_d)
        c.rect(17, 36, 20, r_end, bottom); c.vline(20, 36, r_end, bottom_d)
        c.rect(12, l_end + 1, 15, l_end + 2, look.shoes)
        c.rect(17, r_end + 1, 20, r_end + 2, look.shoes)
    else:
        back, front = 2 * step, -2 * step
        c.rect(14 + back, 36, 17 + back, 44, bottom_d)
        c.rect(14 + back, 45, 17 + back, 46, shade(look.shoes, -0.2))
        c.rect(14 + front, 36, 17 + front, 44, bottom)
        c.rect(13 + front, 45, 17 + front, 46, look.shoes)

    # torso, with a soft fold shadow
    if facing in ("down", "up"):
        c.rect(10, 24 + bob, 21, 36, top)
        c.vline(21, 24 + bob, 36, top_d); c.vline(20, 27 + bob, 36, top_d)
        c.vline(10, 25 + bob, 36, top_l)
        c.hline(10, 21, 36, top_d)
        if facing == "down":
            c.vline(13, 28 + bob, 34, shade(top, -0.1))
    else:
        c.rect(11, 24 + bob, 20, 36, top)
        c.vline(20, 24 + bob, 36, top_d)
        c.hline(11, 20, 36, top_d)

    if look.skirt:
        for i, y in enumerate(range(33, 43)):
            w = 1 + i // 3
            if facing in ("down", "up"):
                c.rect(10 - w, y, 21 + w, y, bottom if y < 41 else bottom_d)
                c.put(21 + w, y, bottom_d)
                if y > 35 and (y + i) % 4 == 0:
                    c.put(14, y, bottom_d); c.put(18, y, bottom_d)
            else:
                c.rect(11 - w, y, 20 + w, y, bottom if y < 41 else bottom_d)

    # arms
    if facing in ("down", "up"):
        la, ra = 33 + bob - step, 33 + bob + step
        c.rect(8, 25 + bob, 9, la, top_d); c.rect(8, la + 1, 9, la + 2, skin)
        c.rect(22, 25 + bob, 23, ra, top_d); c.rect(22, ra + 1, 23, ra + 2, skin)
    else:
        ax = 14 - step * 2
        c.rect(ax, 26 + bob, ax + 2, 33 + bob, top_d)
        c.rect(ax, 34 + bob, ax + 2, 35 + bob, skin)

    # neck and head
    c.rect(14, 22 + bob, 17, 25 + bob, skin_d)
    c.ellipse(16, hy, 8.5, 8.5, skin)
    for y in range(hy, hy + 9):
        for x in range(19, 26):
            if c.get(x, y)[:3] == skin[:3] and x + y > hy + 30:
                c.put(x, y, skin_d)

    if facing == "down":
        c.ellipse(16, hy - 2, 9.5, 8, hair, clip=lambda x, y: y <= hy - 3)
        c.rect(7, hy - 4, 9, hy + 4, hair); c.rect(22, hy - 4, 24, hy + 4, hair)
        for x in range(9, 23):
            depth = 3 if (x // 2) % 3 == 0 else (2 if (x // 2) % 3 == 1 else 1)
            c.vline(x, hy - 3, hy - 3 + depth, hair)
        if look.long_hair:
            c.rect(7, hy + 4, 9, hy + 17, hair); c.rect(22, hy + 4, 24, hy + 17, hair)
        hair_strands(c, 7, 24, hy - 9, hy + 17, hair, 1)
        c.hline(11, 14, hy - 8, hair_l); c.hline(12, 15, hy - 7, hair_l)
        for ex in (11, 19):                                      # eyes: lash, iris, glint
            c.hline(ex - 1, ex + 1, hy + 1, rgb("231a1c"))
            c.rect(ex, hy + 2, ex + 1, hy + 3, look.eyes)
            c.put(ex + 1, hy + 3, shade(look.eyes, -0.4))
            c.put(ex, hy + 2, rgb("f4ece0"))
        c.hline(15, 16, hy + 6, shade(skin, -0.32))
        if look.blush:
            c.hline(9, 10, hy + 5, rgb("d89088")); c.hline(21, 22, hy + 5, rgb("d89088"))
    elif facing == "left":
        c.ellipse(16.5, hy - 2, 9.5, 8, hair, clip=lambda x, y: y <= hy - 3 or x >= 15)
        c.rect(15, hy - 3, 25, hy + 5, hair)
        for x in range(8, 15):
            depth = 2 if (x // 2) % 2 == 0 else 1
            c.vline(x, hy - 3, hy - 3 + depth, hair)
        if look.long_hair:
            c.rect(15, hy + 5, 24, hy + 17, hair)
        hair_strands(c, 8, 25, hy - 9, hy + 17, hair, 2)
        c.vline(15, hy - 2, hy + 5, hair_d)
        c.hline(12, 15, hy - 8, hair_l)
        c.hline(8, 10, hy + 1, rgb("231a1c"))
        c.rect(9, hy + 2, 10, hy + 3, look.eyes)
        c.put(9, hy + 2, rgb("f4ece0"))
        c.put(9, hy + 6, shade(skin, -0.32))
        if look.blush:
            c.hline(10, 11, hy + 5, rgb("d89088"))
    else:  # up
        c.ellipse(16, hy - 1, 9.5, 9.5, hair)
        c.rect(7, hy - 2, 24, hy + 5, hair)
        if look.long_hair:
            for y in range(hy + 5, hy + 20):
                inset = 0 if y < hy + 17 else (y - hy - 16)
                c.hline(7 + inset, 24 - inset, y, hair)
        hair_strands(c, 7, 24, hy - 8, hy + 20, hair, 3)
        c.hline(11, 15, hy - 8, hair_l)

    c.soft_outline(0.6)
    drop_shadow(c, 16, 46, 7, 1.6, 90)
    return c


def character(name, look):
    sheet = Canvas(96, 192)
    for row, facing in enumerate(["down", "left", "right", "up"]):
        for col, step in enumerate((-1, 0, 1)):
            if facing == "right":
                f = draw_frame(look, "left", step)
                f.px = f.px[:, ::-1].copy()
            else:
                f = draw_frame(look, facing, step)
            sheet.paste(f, col * 32, row * 48)
    save(sheet, name)


# ============================================================== portraits

def portrait_mali():
    """Bust portrait in the spirit of the reference screenshot: red hair, orange eyes,
    dark high-necked top. 120x150, shown scaled up beside the dialogue box."""
    c = Canvas(120, 150)
    hair, hair_d, hair_dd, hair_l = rgb("a8383c"), rgb("7a262a"), rgb("561a1e"), rgb("cc5a58")
    skin, skin_d, skin_dd = rgb("f0d8ca"), rgb("dab6a6"), rgb("c09888")
    top, top_d, top_l = rgb("2e3c36"), rgb("1e2824"), rgb("43564e")

    c.poly([(24, 44), (96, 44), (106, 146), (14, 146)], hair_dd)            # hair behind
    c.poly([(30, 108), (90, 108), (116, 149), (4, 149)], top)                # shoulders
    c.poly([(30, 108), (60, 108), (40, 149), (4, 149)], top_l)
    c.poly([(48, 112), (72, 112), (66, 126), (54, 126)], top_d)
    c.rect(51, 86, 69, 108, skin_d)                                          # neck
    c.rect(51, 86, 69, 92, skin_dd)
    c.rect(49, 98, 71, 112, top)                                             # high collar
    c.hline(49, 71, 98, top_l); c.vline(60, 99, 112, top_d)

    c.ellipse(60, 58, 25, 28, skin)                                          # face
    c.poly([(36, 64), (84, 64), (74, 84), (60, 92), (46, 84)], skin)
    for y in range(56, 92):
        for x in range(66, 88):
            if c.get(x, y)[:3] == skin[:3] and (x - 66) * 1.1 + (y - 56) > 22:
                c.put(x, y, skin_d)

    c.ellipse(60, 40, 33, 26, hair, clip=lambda x, y: y < 47)                # crown
    for x in range(29, 92):                                                  # fringe in clumps
        k = (x - 29) % 12
        tip = 46 + int(9 * (1 - abs(k - 6) / 6))
        c.vline(x, 36, tip, hair)
        if c.get(x, tip + 1)[:3] == skin[:3]:
            c.put(x, tip + 1, skin_d); c.put(x, tip + 2, skin_d)
    c.poly([(24, 36), (38, 36), (37, 90), (32, 140), (18, 140)], hair)       # side locks
    c.poly([(82, 36), (96, 36), (102, 140), (88, 140), (83, 90)], hair)
    for x in range(18, 103):                                                 # strands
        if noise(x, 7, 30) > 0.7:
            y_start = 30 + int(noise(x, 1, 31) * 50)
            y_end = y_start + 30 + int(noise(x, 2, 32) * 60)
            for y in range(y_start, min(141, y_end)):
                if c.get(x, y)[:3] == hair[:3]:
                    c.put(x, y, hair_d)
    for i in range(22):                                                      # glossy band
        y = 24 + int(((i - 11) ** 2) / 22)
        c.put(38 + i, y, hair_l); c.put(38 + i, y + 1, hair_l)

    for ex, flip in ((40, False), (68, True)):                               # eyes
        c.rect(ex, 58, ex + 11, 70, rgb("f6efe6"))
        c.rect(ex + 2, 58, ex + 10, 70, rgb("c46a34"))
        c.rect(ex + 3, 63, ex + 9, 69, rgb("8e4020"))
        c.rect(ex + 5, 64, ex + 7, 68, rgb("2a1a1c"))
        c.rect(ex + 3, 59, ex + 4, 61, rgb("ffffff"))
        c.put(ex + 8, 67, rgb("f2b88a"))
        c.hline(ex - 1, ex + 12, 57, rgb("2a1a1c"))                          # upper lash
        c.hline(ex, ex + 11, 56, rgb("2a1a1c"))
        c.put(ex + 12 if flip else ex - 1, 58, rgb("2a1a1c"))
        c.put(ex + 13 if flip else ex - 2, 56, rgb("2a1a1c"))
        c.hline(ex + 2, ex + 9, 71, skin_dd)                                 # lower lid
        c.hline(ex + 2, ex + 9, 51, hair_d)                                  # brows
    c.put(60, 76, skin_dd); c.put(61, 77, skin_dd)                           # nose
    c.hline(57, 63, 83, rgb("b47068"))                                       # mouth
    c.hline(36, 42, 75, rgb("e6a69c")); c.hline(78, 84, 75, rgb("e6a69c"))
    c.soft_outline(0.65)
    save(c, "portrait_mali")


# ============================================================== UI

def ui_frame(name, fill):
    c = Canvas(24, 24)
    c.rect(0, 0, 23, 23, rgb("3a2c24"))
    c.rect(1, 1, 22, 22, rgb("8a6f55"))
    c.rect(2, 2, 21, 21, rgb("3a2c24"))
    c.rect(3, 3, 20, 20, fill)
    for (x, y) in ((4, 4), (19, 4), (4, 19), (19, 19)):
        c.put(x, y, rgb("b8976a"))
    save(c, name)


def ui_arrow():
    c = Canvas(16, 16)
    c.poly([(2, 4), (14, 4), (8, 12)], rgb("ffffff"))
    save(c, "ui_arrow")


def ui_speaker():
    c = Canvas(16, 16)
    c.rect(2, 6, 4, 9, rgb("ffffff"))
    c.poly([(4, 6), (9, 2), (9, 13), (4, 9)], rgb("ffffff"))
    for (x, y) in ((11, 5), (12, 7), (12, 8), (11, 10)):
        c.put(x, y, rgb("ffffff"))
    save(c, "ui_speaker")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for old in os.listdir(OUT):
        if old.endswith(".png"):
            os.remove(os.path.join(OUT, old))
    for v in range(4):
        floor(v)
    for v in range(2):
        wall_upper(v)
        wall_lower(v)
    wall_top()
    ambient_occlusion()
    for m in range(16):
        rug(m)
    for f in (bed, bookshelf, table, stool, jar, lamp, plant, cat,
              window, mirror, door, shrine, calendar, painting, coat):
        f()
    outdoors()

    # The amnesiac protagonist: messy dark hair, plain off-white shirt.
    character("player", Look(hair=rgb("2a2224"), skin=rgb("e2c0a0"), top=rgb("cfc9bc"),
                             bottom=rgb("3a3e4e"), shoes=rgb("3e2c24"), eyes=rgb("5a4a3e")))
    # Mali: long red hair (a nod to the inspiration), dark green top and long skirt.
    character("mali", Look(hair=rgb("a8383c"), skin=rgb("f0d8ca"), top=rgb("2e3c36"),
                           bottom=rgb("232c28"), shoes=rgb("33262a"), eyes=rgb("c46a34"),
                           long_hair=True, skirt=True, blush=True))
    # Lung Daeng: placeholder until he has PixelLab art. Faded red cap, work shirt.
    character("lung_daeng", Look(hair=rgb("8a3a32"), skin=rgb("c89a78"), top=rgb("5a6470"),
                                 bottom=rgb("3c3a36"), shoes=rgb("2e2622"), eyes=rgb("3e3028")))
    portrait_mali()
    # Characters made in PixelLab (Tools/art/pixellab) replace the hand-drawn sheets above.
    import import_pixellab
    import_pixellab.build_all()
    # Finished portraits (Tools/art/portraits) replace the hand-drawn ones.
    portraits = os.path.join(os.path.dirname(os.path.abspath(__file__)), "art", "portraits")
    if os.path.isdir(portraits):
        for f in sorted(os.listdir(portraits)):
            if f.endswith(".png"):
                shutil.copy(os.path.join(portraits, f), os.path.join(OUT, f))

    ui_frame("ui_frame", rgb("e6dac2"))
    ui_frame("ui_card", rgb("d9cbb4"))
    ui_arrow()
    ui_speaker()
    print("Sprites written to", os.path.abspath(OUT))
