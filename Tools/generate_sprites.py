"""Generates the placeholder pixel art in Assets/Resources/Sprites.

Run with: python3 Tools/generate_sprites.py   (needs Pillow)
Replace any PNG with real art of the same size; character sheets follow the
RPG Maker layout: 3 columns (step, idle, step) x 4 rows (down, left, right, up),
each frame 32x32.
"""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Sprites")
T = 32


def img(w=T, h=T):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def rect(d, x0, y0, x1, y1, c):
    d.rectangle([x0, y0, x1, y1], fill=c)


def save(im, name):
    im.save(os.path.join(OUT, name + ".png"))


# ---------------------------------------------------------------- tiles

def floor():
    im, d = img()
    base, dark, light = (122, 84, 54), (92, 62, 40), (142, 100, 66)
    rect(d, 0, 0, 31, 31, base)
    for i, y in enumerate(range(0, 32, 8)):
        rect(d, 0, y, 31, y, dark)
        rect(d, 0, y + 1, 31, y + 1, light)
        seam = (5 + i * 11) % 32
        rect(d, seam, y, seam, y + 7, dark)
    for x, y in [(3, 4), (20, 13), (12, 21), (27, 28), (8, 29)]:
        d.point((x, y), fill=dark)
    save(im, "floor")


def wall():
    im, d = img()
    base, dark, light = (70, 46, 32), (48, 31, 22), (88, 60, 42)
    rect(d, 0, 0, 31, 31, base)
    for x in range(0, 32, 8):
        rect(d, x, 0, x, 25, dark)
        rect(d, x + 1, 0, x + 1, 25, light)
    rect(d, 0, 26, 31, 27, (36, 23, 16))
    rect(d, 0, 28, 31, 31, (58, 38, 27))
    rect(d, 0, 0, 31, 1, (40, 26, 18))
    save(im, "wall")


def wall_top():
    im, d = img()
    rect(d, 0, 0, 31, 31, (30, 22, 18))
    rect(d, 2, 2, 29, 29, (38, 28, 23))
    save(im, "wall_top")


def rug():
    im, d = img()
    a, b, c = (140, 42, 46), (170, 66, 58), (214, 168, 92)
    rect(d, 0, 0, 31, 31, a)
    for y in range(0, 32, 4):
        for x in range(0, 32, 4):
            if (x // 4 + y // 4) % 2 == 0:
                rect(d, x + 1, y + 1, x + 2, y + 2, b)
    rect(d, 0, 15, 31, 16, c)
    rect(d, 15, 0, 16, 31, c)
    save(im, "rug")


# ---------------------------------------------------------------- objects

def bed():
    im, d = img()
    rect(d, 3, 1, 28, 31, (86, 56, 36))          # frame
    rect(d, 4, 2, 27, 30, (232, 228, 214))       # sheet
    rect(d, 7, 3, 24, 9, (250, 248, 240))        # pillow
    rect(d, 7, 9, 24, 9, (210, 204, 190))
    rect(d, 4, 13, 27, 30, (64, 96, 140))        # blanket
    rect(d, 4, 13, 27, 14, (88, 124, 170))
    for y in range(17, 30, 4):
        rect(d, 4, y, 27, y, (54, 82, 122))
    save(im, "bed")


def window():
    im, d = img()
    frame, glass, fog = (52, 34, 24), (120, 140, 150), (176, 190, 196)
    rect(d, 5, 3, 26, 23, frame)
    rect(d, 7, 5, 24, 21, glass)
    for y in range(5, 22):
        for x in range(7, 25):
            if (x * 3 + y * 5) % 11 < 4:
                d.point((x, y), fill=fog)
    rect(d, 15, 5, 16, 21, frame)
    rect(d, 7, 12, 24, 13, frame)
    rect(d, 4, 23, 27, 24, (96, 66, 44))
    save(im, "window")


def mirror():
    im, d = img()
    d.ellipse([8, 2, 23, 25], fill=(150, 110, 52))
    d.ellipse([10, 4, 21, 23], fill=(150, 178, 190))
    rect(d, 12, 7, 13, 12, (214, 230, 236))
    d.point((14, 6), fill=(214, 230, 236))
    save(im, "mirror")


def door():
    im, d = img()
    rect(d, 4, 0, 27, 27, (40, 26, 18))
    rect(d, 6, 2, 25, 27, (112, 74, 44))
    rect(d, 8, 4, 23, 13, (96, 62, 36))
    rect(d, 8, 16, 23, 25, (96, 62, 36))
    rect(d, 21, 14, 22, 15, (214, 180, 90))
    save(im, "door")


def table():
    im, d = img()
    rect(d, 3, 10, 28, 26, (68, 44, 28))          # shadow/legs
    rect(d, 2, 6, 29, 22, (132, 90, 56))          # top
    rect(d, 2, 6, 29, 7, (156, 110, 70))
    rect(d, 2, 22, 29, 24, (96, 62, 38))
    rect(d, 4, 25, 6, 30, (80, 52, 32))
    rect(d, 25, 25, 27, 30, (80, 52, 32))
    rect(d, 10, 9, 20, 17, (238, 232, 214))       # letter
    rect(d, 10, 9, 20, 9, (210, 200, 180))
    for y in (11, 13, 15):
        rect(d, 12, y, 18, y, (90, 80, 72))
    save(im, "table")


def jar():
    im, d = img()
    d.ellipse([6, 8, 25, 30], fill=(128, 70, 40))
    d.ellipse([8, 10, 21, 26], fill=(150, 86, 50))
    rect(d, 10, 4, 21, 9, (110, 60, 34))
    rect(d, 11, 4, 20, 5, (46, 74, 96))          # water
    rect(d, 8, 17, 23, 17, (176, 120, 70))
    save(im, "jar")


def cat():
    im, d = img()
    body, dark, light = (214, 132, 54), (168, 92, 34), (240, 196, 140)
    d.ellipse([9, 14, 24, 30], fill=body)        # body
    d.ellipse([10, 6, 21, 17], fill=body)        # head
    d.polygon([(10, 9), (11, 3), (14, 7)], fill=body)
    d.polygon([(21, 9), (20, 3), (17, 7)], fill=body)
    d.point((12, 6), fill=dark); d.point((19, 6), fill=dark)
    rect(d, 13, 10, 13, 11, (30, 24, 20)); rect(d, 18, 10, 18, 11, (30, 24, 20))
    d.point((15, 13), fill=(200, 90, 90)); d.point((16, 13), fill=(200, 90, 90))
    rect(d, 13, 19, 18, 26, light)
    for y in (17, 21, 25):
        rect(d, 9, y, 11, y, dark)
    rect(d, 24, 24, 28, 26, body); rect(d, 27, 20, 28, 24, body)  # tail
    save(im, "cat")


# ---------------------------------------------------------------- characters

def character(name, hair, skin, shirt, pants, long_hair=False, eye=(30, 24, 22)):
    sheet = Image.new("RGBA", (T * 3, T * 4), (0, 0, 0, 0))
    hair_dark = tuple(max(0, c - 40) for c in hair)
    shirt_dark = tuple(max(0, c - 35) for c in shirt)
    for row, facing in enumerate(["down", "left", "right", "up"]):
        for col in range(3):
            im, d = img()
            step = {0: -1, 1: 0, 2: 1}[col]
            bob = 0 if col == 1 else 1

            # shadow
            d.ellipse([9, 28, 22, 31], fill=(0, 0, 0, 70))

            # legs
            if facing in ("down", "up"):
                rect(d, 12, 22, 14, 28 - (1 if step < 0 else 0), pants)
                rect(d, 17, 22, 19, 28 - (1 if step > 0 else 0), pants)
                rect(d, 12, 28 - (1 if step < 0 else 0), 14, 29 - (1 if step < 0 else 0), (40, 30, 26))
                rect(d, 17, 28 - (1 if step > 0 else 0), 19, 29 - (1 if step > 0 else 0), (40, 30, 26))
            else:
                rect(d, 14 + step, 22, 16 + step, 28, pants)
                rect(d, 15 - step, 22, 17 - step, 28, tuple(max(0, c - 25) for c in pants))
                rect(d, 14 + step, 28, 17 + step, 29, (40, 30, 26))

            # long hair falls behind the body
            if long_hair and facing != "up":
                rect(d, 9, 9 + bob, 22, 22 + bob, hair_dark)

            # torso
            rect(d, 10, 14 + bob, 21, 23, shirt)
            rect(d, 10, 22, 21, 23, shirt_dark)
            # arms
            if facing in ("down", "up"):
                rect(d, 8, 15 + bob, 9, 21 + bob - step, shirt_dark)
                rect(d, 22, 15 + bob, 23, 21 + bob + step, shirt_dark)
                rect(d, 8, 22 + bob - step, 9, 22 + bob - step, skin)
                rect(d, 22, 22 + bob + step, 23, 22 + bob + step, skin)
            else:
                ax = 15 + step * 2
                rect(d, ax, 15 + bob, ax + 2, 21 + bob, shirt_dark)
                rect(d, ax, 22 + bob, ax + 2, 22 + bob, skin)

            # head
            hy = 3 + bob
            rect(d, 10, hy + 1, 21, hy + 11, skin)
            rect(d, 11, hy, 20, hy, skin)
            rect(d, 11, hy + 12, 20, hy + 12, skin)

            if facing == "down":
                rect(d, 9, hy - 1, 22, hy + 3, hair)
                rect(d, 9, hy + 4, 10, hy + 8, hair)
                rect(d, 21, hy + 4, 22, hy + 8, hair)
                rect(d, 13, hy + 4, 15, hy + 4, hair)          # fringe
                rect(d, 13, hy + 6, 13, hy + 8, eye)
                rect(d, 18, hy + 6, 18, hy + 8, eye)
                rect(d, 15, hy + 10, 16, hy + 10, (190, 120, 110))
                if long_hair:
                    rect(d, 9, hy + 4, 10, hy + 14, hair)
                    rect(d, 21, hy + 4, 22, hy + 14, hair)
            elif facing == "up":
                rect(d, 9, hy - 1, 22, hy + 11, hair)
                rect(d, 10, hy + 9, 21, hy + 10, hair_dark)
                if long_hair:
                    rect(d, 9, hy + 11, 22, hy + 17, hair)
            else:
                flip = facing == "right"
                rect(d, 9, hy - 1, 22, hy + 3, hair)
                back = (9, 13) if flip else (18, 22)
                rect(d, back[0], hy + 3, back[1], hy + 9, hair)
                ex = 19 if flip else 12
                rect(d, ex, hy + 6, ex, hy + 8, eye)
                if long_hair:
                    rect(d, back[0], hy + 9, back[1], hy + 16, hair)

            sheet.paste(im, (col * T, row * T))
    save(sheet, name)


# ---------------------------------------------------------------- UI

def ui_frame(name, fill):
    im, d = img(24, 24)
    rect(d, 0, 0, 23, 23, (58, 44, 36))
    rect(d, 1, 1, 22, 22, (120, 98, 80))
    rect(d, 2, 2, 21, 21, (58, 44, 36))
    rect(d, 3, 3, 20, 20, fill)
    for x, y in [(4, 4), (19, 4), (4, 19), (19, 19)]:
        d.point((x, y), fill=(120, 98, 80))
    save(im, name)


def ui_arrow():
    im, d = img(16, 16)
    d.polygon([(2, 4), (13, 4), (7, 12)], fill=(255, 255, 255))
    save(im, "ui_arrow")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for f in (floor, wall, wall_top, rug, bed, window, mirror, door, table, jar, cat):
        f()
    # The amnesiac protagonist: messy dark hair, plain white shirt.
    character("player", hair=(44, 36, 34), skin=(232, 196, 160), shirt=(226, 224, 216), pants=(60, 66, 84))
    # Mali: long red hair (a nod to the inspiration), dark green top.
    character("mali", hair=(176, 58, 60), skin=(240, 210, 190), shirt=(44, 72, 62), pants=(36, 44, 40), long_hair=True)
    ui_frame("ui_frame", (230, 218, 194))
    ui_frame("ui_card", (217, 203, 180))
    ui_arrow()
    print("Sprites written to", os.path.abspath(OUT))
