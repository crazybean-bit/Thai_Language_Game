"""Generates the placeholder pixel art in Assets/Resources/Sprites.

Run with: python3 Tools/generate_sprites.py   (needs Pillow and numpy)

Replace any PNG with real art and the game picks it up. Sizes:
  - tiles and objects: 32x32 (1 tile)
  - character sheets: RPG Maker layout, 3 columns (step, idle, step) x 4 rows
    (down, left, right, up). Frames here are 32x48; any frame size works.
  - portraits: any size, shown beside the dialogue box.
"""
import os
from pixel import Canvas, rgb, shade, noise

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Sprites")
OUTLINE = rgb("2a1c1e")


def save(c, name):
    c.save(os.path.join(OUT, name + ".png"))


# ============================================================== tiles

def floor(variant):
    c = Canvas(32, 32)
    woods = [rgb("8a5a3a"), rgb("7f5234"), rgb("94623f"), rgb("865637")]
    for plank in range(4):
        y0 = plank * 8
        base = woods[(plank + variant * 3) % 4]
        seam = int(noise(plank, variant, 7) * 24) + 4
        for y in range(y0, y0 + 8):
            for x in range(32):
                grain = noise(x // 3, y, plank + variant * 11)
                col = shade(base, (grain - 0.5) * 0.12)
                if (y - y0) == 1:
                    col = shade(base, 0.06)
                if (y - y0) in (3, 5) and noise(x // 5, y, variant + plank) > 0.55:
                    col = shade(base, -0.12)
                c.put(x, y, col)
        c.hline(0, 31, y0, shade(base, -0.38))
        # only some planks end inside this tile, so boards read as long runs, not bricks
        if noise(plank, variant, 3) > 0.45:
            c.vline(seam, y0, y0 + 7, shade(base, -0.38))
            c.vline(seam + 1, y0 + 1, y0 + 7, shade(base, 0.06))
            c.put(seam - 2, y0 + 4, shade(base, -0.3))
    save(c, f"floor_{variant}")


def wall(variant):
    c = Canvas(32, 32)
    for board in range(4):
        x0 = board * 8
        base = [rgb("5e3b27"), rgb("573624"), rgb("63402b"), rgb("5a3926")][(board + variant) % 4]
        for y in range(32):
            for x in range(x0, x0 + 8):
                grain = noise(x, y // 4, board + variant * 5)
                c.put(x, y, shade(base, (grain - 0.5) * 0.14))
        c.vline(x0, 0, 31, shade(base, -0.4))
        c.vline(x0 + 1, 0, 31, shade(base, 0.12))
    c.rect(0, 0, 31, 1, rgb("24170f"))           # shadow under the ceiling
    c.rect(0, 2, 31, 2, rgb("3a2619"))
    c.rect(0, 19, 31, 19, rgb("7a5136"))         # rail
    c.rect(0, 20, 31, 21, rgb("3d2719"))
    for y in range(22, 28):                      # darker lower panel
        for x in range(32):
            c.put(x, y, shade(c.get(x, y), -0.2))
    c.rect(0, 28, 31, 29, rgb("2e1d13"))         # baseboard
    c.rect(0, 30, 31, 31, rgb("43291b"))
    save(c, f"wall_{variant}")


def wall_top():
    c = Canvas(32, 32)
    base = rgb("2b201d")
    for y in range(32):
        for x in range(32):
            c.put(x, y, shade(base, (noise(x // 2, y // 2, 3) - 0.5) * 0.15))
    c.rect(0, 0, 31, 0, rgb("3b2d28"))
    c.rect(0, 0, 0, 31, rgb("3b2d28"))
    save(c, "wall_top")


def rug(mask):
    """mask bits: 1 = top edge, 2 = right edge, 4 = bottom edge, 8 = left edge."""
    c = Canvas(32, 32)
    red, dark, gold, deep = rgb("9b2c2f"), rgb("6e1c22"), rgb("d6a34e"), rgb("4e1218")
    for y in range(32):
        for x in range(32):
            col = red
            # small diamond motif in the field
            dx, dy = abs((x % 8) - 4), abs((y % 8) - 4)
            if dx + dy == 2:
                col = gold if (x // 8 + y // 8) % 2 == 0 else shade(red, 0.15)
            elif dx + dy == 0:
                col = dark
            c.put(x, y, shade(col, (noise(x, y, 9) - 0.5) * 0.1))

    def border(edge_px):
        for (x, y, d) in edge_px:
            if d < 2:
                c.put(x, y, deep)
            elif d < 3:
                c.put(x, y, gold)
            elif d < 6:
                c.put(x, y, dark if ((x + y) // 2) % 2 == 0 else red)
            elif d < 7:
                c.put(x, y, gold)

    pts = []
    for y in range(32):
        for x in range(32):
            d = 99
            if mask & 1: d = min(d, y)
            if mask & 4: d = min(d, 31 - y)
            if mask & 8: d = min(d, x)
            if mask & 2: d = min(d, 31 - x)
            if d < 7:
                pts.append((x, y, d))
    border(pts)
    # fringe on left/right ends
    if mask & 8:
        for y in range(0, 32, 2):
            c.put(0, y, rgb("e8d3a8"))
    if mask & 2:
        for y in range(0, 32, 2):
            c.put(31, y, rgb("e8d3a8"))
    save(c, f"rug_{mask}")


# ============================================================== objects

def bed():
    c = Canvas(32, 32)
    wood, wood_d = rgb("6b4128"), rgb("4a2c1b")
    c.rect(3, 1, 28, 31, wood)
    c.rect(3, 1, 28, 4, shade(wood, 0.1))          # headboard
    c.rect(3, 1, 4, 31, wood_d)
    c.rect(27, 1, 28, 31, wood_d)
    c.rect(5, 5, 26, 30, rgb("e9e2d0"))            # sheet
    c.rect(7, 6, 24, 11, rgb("f7f3ea"))            # pillow
    c.hline(7, 24, 11, rgb("cfc6b3"))
    c.vline(24, 6, 11, rgb("d9d1bf"))
    # blanket: pha khao ma check pattern
    for y in range(14, 31):
        for x in range(5, 27):
            col = rgb("3d5f8e")
            if (x // 3) % 2 == 0 and (y // 3) % 2 == 0:
                col = rgb("5578a8")
            elif (x // 3) % 2 == 1 and (y // 3) % 2 == 1:
                col = rgb("2f4a72")
            c.put(x, y, col)
    c.hline(5, 26, 14, rgb("7b98c2"))
    c.hline(5, 26, 15, rgb("e9e2d0"))
    c.outline(OUTLINE)
    save(c, "bed")


def window():
    c = Canvas(32, 32)
    frame, frame_l = rgb("3b2618"), rgb("6b4a30")
    c.rect(4, 3, 27, 25, frame)
    for y in range(5, 24):
        for x in range(6, 26):
            t = (y - 5) / 18
            glass = (int(150 + 40 * t), int(165 + 35 * t), int(175 + 25 * t), 255)
            if noise(x // 2, y // 2, 4) > 0.7:
                glass = shade(glass, 0.15)
            c.put(x, y, glass)
    c.vline(15, 5, 23, frame); c.vline(16, 5, 23, frame_l)
    c.hline(6, 25, 14, frame); c.hline(6, 25, 13, frame_l)
    c.hline(4, 27, 3, frame_l)
    # fog streaks
    for x in range(7, 25, 3):
        c.put(x, 8 + x % 4, rgb("dfe7ea"))
    c.rect(3, 25, 28, 26, rgb("7a5536"))            # sill
    c.rect(3, 27, 28, 27, rgb("3b2618"))
    # curtains
    for y in range(2, 26):
        w = 3 if y < 20 else 4
        for x in range(1, 1 + w):
            c.put(x, y, shade(rgb("b9a27a"), -0.15 * ((x + y // 3) % 2)))
        for x in range(31 - w, 31):
            c.put(x, y, shade(rgb("b9a27a"), -0.15 * ((x + y // 3) % 2)))
    c.hline(0, 31, 1, rgb("4a3020"))
    save(c, "window")


def mirror():
    c = Canvas(32, 32)
    c.ellipse(16, 14, 8.5, 12, rgb("9c7432"))
    c.ellipse(16, 14, 7.5, 11, rgb("c9a14e"))
    for y in range(32):
        for x in range(32):
            dx, dy = (x + 0.5 - 16) / 6, (y + 0.5 - 14) / 9.5
            if dx * dx + dy * dy <= 1:
                t = (y - 4) / 20
                c.put(x, y, (int(110 + 60 * t), int(140 + 50 * t), int(160 + 40 * t), 255))
    for i in range(5):
        c.put(12 + i // 2, 8 + i, rgb("e6f0f4"))
    c.put(18, 18, rgb("d0e0e6")); c.put(19, 17, rgb("d0e0e6"))
    c.outline(OUTLINE)
    save(c, "mirror")


def door():
    c = Canvas(32, 32)
    c.rect(4, 0, 27, 29, rgb("2e1c12"))
    wood = rgb("7a4c2e")
    for y in range(2, 30):
        for x in range(6, 26):
            c.put(x, y, shade(wood, (noise(x, y // 3, 2) - 0.5) * 0.15))
    for (y0, y1) in ((4, 13), (16, 26)):
        c.rect(8, y0, 23, y1, shade(wood, -0.18))
        c.hline(8, 23, y0, shade(wood, -0.35))
        c.vline(8, y0, y1, shade(wood, -0.35))
        c.hline(9, 23, y1, shade(wood, 0.12))
        c.vline(23, y0 + 1, y1, shade(wood, 0.12))
    c.rect(21, 15, 22, 16, rgb("e3c06a"))
    c.put(21, 15, rgb("fff0b0"))
    c.hline(6, 25, 29, rgb("c9b38a"))               # light under the door
    save(c, "door")


def table():
    c = Canvas(32, 32)
    top, top_l, top_d = rgb("8b5a36"), rgb("a8734a"), rgb("5c3a22")
    c.rect(4, 22, 6, 30, top_d); c.rect(25, 22, 27, 30, top_d)
    c.rect(2, 8, 29, 21, top)
    c.hline(2, 29, 8, top_l)
    c.rect(2, 21, 29, 23, top_d)
    for x in range(3, 29, 6):
        c.put(x, 14, shade(top, -0.15))
    # letter
    c.rect(9, 10, 20, 18, rgb("efe6cf"))
    c.hline(9, 20, 18, rgb("c9bc9c"))
    c.vline(20, 10, 18, rgb("d6caab"))
    for y in (12, 14, 16):
        c.hline(11, 17 - (y == 16) * 3, y, rgb("6b5d52"))
    # candle
    c.rect(23, 9, 25, 14, rgb("f0e6d0"))
    c.put(24, 8, rgb("3a2a20"))
    c.put(24, 6, rgb("ffd27a")); c.put(24, 7, rgb("ff9a3a"))
    c.outline(OUTLINE)
    save(c, "table")


def jar():
    c = Canvas(32, 32)
    glaze, glaze_d, glaze_l = rgb("7b3f1f"), rgb("55291a"), rgb("a4592e")
    c.ellipse(16, 19, 10, 11.5, glaze)
    for y in range(32):
        for x in range(32):
            if c.filled(x, y):
                if x > 20: c.put(x, y, glaze_d)
                if x < 11 and y < 22: c.put(x, y, glaze_l)
    c.rect(10, 5, 21, 8, glaze_d)
    c.hline(10, 21, 5, glaze_l)
    c.rect(11, 6, 20, 7, rgb("23394a"))             # water
    # dragon swirl
    for i, (x, y) in enumerate([(9, 18), (10, 16), (12, 15), (14, 16), (15, 18), (17, 19), (19, 18), (20, 16), (22, 15)]):
        c.put(x, y, rgb("d9a441")); c.put(x, y + 1, rgb("a87a2c"))
    c.outline(OUTLINE)
    save(c, "jar")


def cat():
    c = Canvas(32, 32)
    fur, fur_d, fur_l, white = rgb("d98a3d"), rgb("a8612a"), rgb("f0b06a"), rgb("f4e6d2")
    c.rect(25, 22, 27, 28, fur); c.rect(26, 19, 28, 22, fur)          # tail
    c.ellipse(16, 23, 8, 8, fur)                                       # body
    c.ellipse(16, 13, 6.5, 6, fur)                                     # head
    c.poly([(9.5, 11), (10.5, 4), (14, 8)], fur)
    c.poly([(22.5, 11), (21.5, 4), (18, 8)], fur)
    c.put(11, 7, rgb("e88f8f")); c.put(21, 7, rgb("e88f8f"))
    c.ellipse(16, 25, 4, 5, white)
    for y in (18, 22, 26):
        c.hline(8, 10, y, fur_d); c.hline(22, 24, y, fur_d)
    c.hline(12, 20, 9, fur_l)
    c.rect(12, 12, 13, 14, rgb("2d2420")); c.put(12, 12, rgb("ffffff"))
    c.rect(19, 12, 20, 14, rgb("2d2420")); c.put(19, 12, rgb("ffffff"))
    c.put(16, 15, rgb("c66a6a"))
    c.rect(12, 29, 14, 30, white); c.rect(18, 29, 20, 30, white)
    c.outline(OUTLINE)
    shadow(c, 16, 30, 9)
    save(c, "cat")


def lamp():
    c = Canvas(32, 32)
    c.rect(11, 20, 20, 21, rgb("6b4128"))           # stand top
    c.rect(14, 22, 17, 29, rgb("4a2c1b"))
    c.rect(10, 29, 21, 30, rgb("6b4128"))
    c.rect(12, 15, 19, 19, rgb("b88a3a"))            # brass base
    c.hline(12, 19, 15, rgb("e3bf6a"))
    c.ellipse(16, 10, 4.5, 6, rgb("f6e7c4", 200))    # glass
    c.rect(15, 8, 16, 12, rgb("ffb347"))             # flame
    c.put(15, 7, rgb("fff2b0")); c.put(16, 9, rgb("fff2b0"))
    c.rect(13, 3, 18, 4, rgb("8a6a2a"))
    c.outline(OUTLINE)
    save(c, "lamp")


def shrine():
    """Small wall shelf with an offering, a common sight in Thai homes."""
    c = Canvas(32, 32)
    c.rect(3, 18, 28, 20, rgb("7a3a22"))
    c.hline(3, 28, 18, rgb("a35a32"))
    c.poly([(5, 21), (26, 21), (22, 25), (9, 25)], rgb("5a2a18"))
    c.rect(13, 8, 18, 17, rgb("d4a640"))             # small golden figure
    c.ellipse(15.5, 7, 2.5, 2.5, rgb("e6bd55"))
    c.put(15, 4, rgb("e6bd55"))
    c.vline(18, 10, 16, rgb("a87c26"))
    c.rect(6, 14, 8, 17, rgb("f2f2ea"))              # jasmine garland
    c.put(7, 13, rgb("9ecf7a"))
    c.rect(23, 13, 25, 17, rgb("f2f2ea"))
    c.put(24, 12, rgb("9ecf7a"))
    c.vline(10, 11, 17, rgb("c8b090")); c.put(10, 10, rgb("ff8a3a"))   # incense
    c.outline(OUTLINE)
    save(c, "shrine")


def plant():
    c = Canvas(32, 32)
    leaf, leaf_d, leaf_l = rgb("3f7a3a"), rgb("2a5628"), rgb("6aa85a")
    for (cx, cy, rx, ry) in [(11, 12, 5, 9), (21, 11, 5, 9), (16, 7, 4, 8), (8, 17, 5, 4), (24, 17, 5, 4)]:
        c.ellipse(cx, cy, rx, ry, leaf)
    for (x, y) in [(10, 8), (11, 10), (20, 6), (21, 8), (16, 3), (16, 5)]:
        c.put(x, y, leaf_l)
    c.vline(16, 9, 21, leaf_d)
    c.poly([(9, 21), (23, 21), (21, 31), (11, 31)], rgb("a0583a"))
    c.hline(9, 23, 21, rgb("c07650"))
    c.hline(10, 22, 22, rgb("7a3e28"))
    c.outline(OUTLINE)
    save(c, "plant")


def shadow(c, cx, cy, rx):
    for y in range(cy - 1, cy + 2):
        for x in range(cx - rx, cx + rx + 1):
            dx = (x - cx) / rx
            dy = (y - cy) / 1.6
            if dx * dx + dy * dy <= 1 and not c.filled(x, y):
                c.put(x, y, (0, 0, 0, 70))


# ============================================================== characters

class Look:
    def __init__(self, hair, skin, top, bottom, shoes, eyes, long_hair=False, skirt=False, blush=False):
        self.hair, self.skin, self.top, self.bottom = hair, skin, top, bottom
        self.shoes, self.eyes = shoes, eyes
        self.long_hair, self.skirt, self.blush = long_hair, skirt, blush


def draw_frame(look, facing, step):
    """facing: down/left/up (right is a mirrored left). step: -1, 0, 1."""
    c = Canvas(32, 48)
    bob = 0 if step == 0 else 1
    hair, hair_d, hair_l = look.hair, shade(look.hair, -0.3), shade(look.hair, 0.25)
    skin, skin_d = look.skin, shade(look.skin, -0.18)
    top, top_d, top_l = look.top, shade(look.top, -0.28), shade(look.top, 0.15)
    bottom, bottom_d = look.bottom, shade(look.bottom, -0.25)
    hy = 15 + bob   # head centre

    # ---- long hair behind everything
    if look.long_hair:
        if facing == "down":
            c.rect(7, hy, 24, 34 + bob, hair_d)
        elif facing == "left":
            c.rect(14, hy, 24, 34 + bob, hair_d)
        # up view draws hair over the body later

    # ---- legs
    if facing in ("down", "up"):
        l_end = 44 - (2 if step == 1 else 0)
        r_end = 44 - (2 if step == -1 else 0)
        c.rect(12, 36, 15, l_end, bottom); c.vline(15, 36, l_end, bottom_d)
        c.rect(17, 36, 20, r_end, bottom); c.vline(20, 36, r_end, bottom_d)
        c.rect(12, l_end + 1, 15, l_end + 2, look.shoes)
        c.rect(17, r_end + 1, 20, r_end + 2, look.shoes)
    else:
        back, front = (2 * step, -2 * step)
        c.rect(14 + back, 36, 17 + back, 44, bottom_d)
        c.rect(14 + back, 45, 17 + back, 46, shade(look.shoes, -0.2))
        c.rect(14 + front, 36, 17 + front, 44, bottom)
        c.rect(13 + front, 45, 17 + front, 46, look.shoes)

    # ---- torso
    if facing in ("down", "up"):
        c.rect(10, 24 + bob, 21, 36, top)
        c.vline(21, 24 + bob, 36, top_d); c.vline(20, 26 + bob, 36, top_d)
        c.vline(10, 24 + bob, 36, top_l)
        c.hline(10, 21, 36, top_d)
    else:
        c.rect(11, 24 + bob, 20, 36, top)
        c.vline(20, 24 + bob, 36, top_d)
        c.hline(11, 20, 36, top_d)

    if look.skirt:
        flare = [(11, 33), (10, 35), (9, 37), (9, 39), (8, 41)]
        for i, y in enumerate(range(33, 43)):
            w = 1 + i // 3
            if facing in ("down", "up"):
                c.rect(10 - w, y, 21 + w, y, bottom if y < 41 else bottom_d)
                c.put(21 + w, y, bottom_d)
            else:
                c.rect(11 - w, y, 20 + w, y, bottom if y < 41 else bottom_d)

    # ---- arms
    if facing in ("down", "up"):
        la = 33 + bob - step
        ra = 33 + bob + step
        c.rect(8, 25 + bob, 9, la, top_d); c.rect(8, la + 1, 9, la + 2, skin)
        c.rect(22, 25 + bob, 23, ra, top_d); c.rect(22, ra + 1, 23, ra + 2, skin)
    else:
        ax = 14 - step * 2
        c.rect(ax, 26 + bob, ax + 2, 33 + bob, top_d)
        c.rect(ax, 34 + bob, ax + 2, 35 + bob, skin)

    # ---- neck and head
    c.rect(14, 22 + bob, 17, 25 + bob, skin_d)
    c.ellipse(16, hy, 8.5, 8.5, skin)
    for y in range(hy, hy + 9):
        for x in range(20, 26):
            if c.filled(x, y) and c.get(x, y) == skin and noise(x, y) > -1 and x + y > hy + 31:
                c.put(x, y, skin_d)

    if facing == "down":
        c.ellipse(16, hy - 2, 9.5, 8, hair, clip=lambda x, y: y <= hy - 3)
        c.rect(7, hy - 4, 9, hy + 4, hair); c.rect(22, hy - 4, 24, hy + 4, hair)
        for x in range(9, 23):                          # jagged fringe
            depth = 2 if (x // 2) % 2 == 0 else 1
            c.vline(x, hy - 3, hy - 3 + depth, hair)
        c.hline(11, 14, hy - 8, hair_l); c.hline(12, 13, hy - 7, hair_l)
        if look.long_hair:
            c.rect(7, hy + 4, 9, hy + 17, hair); c.rect(22, hy + 4, 24, hy + 17, hair)
            c.vline(9, hy + 4, hy + 17, hair_d); c.vline(22, hy + 4, hy + 17, hair_d)
        # eyes
        for ex in (11, 19):
            c.rect(ex, hy + 1, ex + 1, hy + 3, rgb("2a1c1e"))
            c.rect(ex, hy + 2, ex + 1, hy + 3, look.eyes)
            c.put(ex, hy + 1, rgb("ffffff"))
        c.hline(15, 16, hy + 6, shade(skin, -0.35))
        if look.blush:
            c.hline(9, 10, hy + 5, rgb("e89a94")); c.hline(21, 22, hy + 5, rgb("e89a94"))
    elif facing == "left":
        c.ellipse(16.5, hy - 2, 9.5, 8, hair, clip=lambda x, y: y <= hy - 3 or x >= 15)
        c.rect(15, hy - 3, 25, hy + 5, hair)
        c.vline(15, hy - 3, hy + 5, hair_d)
        for x in range(8, 15):
            depth = 2 if (x // 2) % 2 == 0 else 1
            c.vline(x, hy - 3, hy - 3 + depth, hair)
        c.hline(12, 15, hy - 8, hair_l)
        if look.long_hair:
            c.rect(15, hy + 5, 24, hy + 17, hair)
            c.vline(15, hy + 5, hy + 17, hair_d)
        c.rect(9, hy + 1, 10, hy + 3, rgb("2a1c1e"))
        c.rect(9, hy + 2, 10, hy + 3, look.eyes)
        c.put(9, hy + 1, rgb("ffffff"))
        c.put(9, hy + 6, shade(skin, -0.35))
        if look.blush:
            c.hline(10, 11, hy + 5, rgb("e89a94"))
    else:  # up
        c.ellipse(16, hy - 1, 9.5, 9.5, hair)
        c.rect(7, hy - 2, 24, hy + 5, hair)
        c.hline(11, 15, hy - 8, hair_l)
        c.vline(16, hy - 6, hy + 4, hair_d)
        if look.long_hair:
            for y in range(hy + 5, hy + 20):
                inset = 0 if y < hy + 17 else (y - hy - 16)
                c.hline(7 + inset, 24 - inset, y, hair)
            c.vline(12, hy + 6, hy + 18, hair_d); c.vline(19, hy + 6, hy + 18, hair_d)

    c.outline(OUTLINE)
    shadow(c, 16, 46, 7)
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
    c = Canvas(80, 100)
    hair, hair_d, hair_l = rgb("b03a3c"), rgb("7e2629"), rgb("d8585a")
    skin, skin_d = rgb("f2d7c6"), rgb("dcb5a2")
    top, top_d = rgb("2f4a40"), rgb("1f332c")

    c.poly([(16, 30), (64, 30), (70, 96), (10, 96)], hair_d)          # hair behind
    c.poly([(22, 72), (58, 72), (76, 99), (4, 99)], top)              # shoulders
    c.poly([(30, 74), (50, 74), (45, 84), (35, 84)], top_d)
    c.rect(34, 56, 46, 72, skin_d)                                     # neck
    c.rect(33, 66, 47, 75, top)                                        # high collar
    c.hline(33, 47, 66, shade(top, 0.2))
    c.vline(40, 67, 75, top_d)

    c.ellipse(40, 39, 16.5, 19, skin)                                  # face
    c.poly([(24, 44), (56, 44), (49, 57), (40, 62), (31, 57)], skin)  # jaw
    for y in range(44, 62):
        for x in range(44, 58):
            if c.get(x, y) == skin and (x - 44) * 1.2 + (y - 44) > 13:
                c.put(x, y, skin_d)

    c.ellipse(40, 27, 22, 17, hair, clip=lambda x, y: y < 31)          # crown
    for x in range(19, 62):                                            # fringe
        tip = 31 + int(5 * abs(((x - 19) % 8) - 4) / 4)
        c.vline(x, 24, tip, hair)
        if c.get(x, tip + 1) == skin:
            c.put(x, tip + 1, skin_d)
    c.poly([(16, 24), (25, 24), (24, 60), (21, 94), (13, 94)], hair)  # side locks
    c.poly([(55, 24), (64, 24), (67, 94), (59, 94), (56, 60)], hair)
    for x in (18, 21, 61, 64):
        c.vline(x, 40, 92, hair_d)
    for i in range(14):                                                # shine
        c.put(25 + i, 16 + int(((i - 7) ** 2) / 14), hair_l)
        c.put(25 + i, 17 + int(((i - 7) ** 2) / 14), hair_l)

    for ex in (27, 46):                                                # eyes
        c.rect(ex, 39, ex + 7, 47, rgb("ffffff"))
        c.rect(ex + 1, 39, ex + 6, 47, rgb("c96a32"))
        c.rect(ex + 2, 42, ex + 5, 46, rgb("8a3e1c"))
        c.rect(ex + 3, 43, ex + 4, 45, rgb("2a1c1e"))
        c.rect(ex + 1, 40, ex + 2, 41, rgb("ffffff"))
        c.put(ex + 5, 45, rgb("f0b080"))
        c.hline(ex - 1, ex + 8, 38, rgb("2a1c1e"))
        c.hline(ex, ex + 7, 37, rgb("2a1c1e"))
        c.put(ex + (8 if ex > 40 else -1), 39, rgb("2a1c1e"))
        c.hline(ex + 1, ex + 6, 35, hair_d)                            # brows
    c.put(40, 52, skin_d); c.put(41, 53, skin_d)                       # nose
    c.hline(38, 42, 57, rgb("c27c74"))                                 # mouth
    c.hline(25, 29, 50, rgb("eda6a0")); c.hline(51, 55, 50, rgb("eda6a0"))
    c.outline(OUTLINE)
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


def glow():
    """Soft radial gradient used by the lighting overlay as a reference; also handy for effects."""
    c = Canvas(64, 64)
    for y in range(64):
        for x in range(64):
            d = ((x - 31.5) ** 2 + (y - 31.5) ** 2) ** 0.5 / 32
            a = max(0.0, 1 - d) ** 2
            c.px[y, x] = (255, 255, 255, int(a * 255))
    save(c, "glow")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for old in os.listdir(OUT):
        if old.endswith(".png"):
            os.remove(os.path.join(OUT, old))
    for v in range(4):
        floor(v)
    for v in range(2):
        wall(v)
    wall_top()
    for m in range(16):
        rug(m)
    for f in (bed, window, mirror, door, table, jar, cat, lamp, shrine, plant):
        f()

    # The amnesiac protagonist: messy dark hair, plain white shirt.
    character("player", Look(hair=rgb("2e2624"), skin=rgb("e8c4a0"), top=rgb("e6e2d8"),
                             bottom=rgb("3f475c"), shoes=rgb("4a3326"), eyes=rgb("5a4a3a")))
    # Mali: long red hair (a nod to the inspiration), dark green top and long skirt.
    character("mali", Look(hair=rgb("b03a3c"), skin=rgb("f2d7c6"), top=rgb("2f4a40"),
                           bottom=rgb("263830"), shoes=rgb("3a2a24"), eyes=rgb("c96a32"),
                           long_hair=True, skirt=True, blush=True))
    portrait_mali()

    ui_frame("ui_frame", rgb("e6dac2"))
    ui_frame("ui_card", rgb("d9cbb4"))
    ui_arrow()
    ui_speaker()
    glow()
    print("Sprites written to", os.path.abspath(OUT))
