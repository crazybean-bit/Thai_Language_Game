"""Shrink a PixelLab prop down to the game's grid and save it in Tools/art/props.

PixelLab props are generated at about twice the game's size (it keeps them crisp). This
crops the empty border, scales the prop to the width you ask for (32 px = one tile) and
hardens the alpha so edges stay pixel-sharp. Tools/generate_sprites.py then copies
everything in Tools/art/props over the hand-drawn placeholders of the same name.

    python3 Tools/import_prop.py boat path/to/longtail.png 64
    python3 Tools/import_prop.py spirit_house_fallen path/to/spirit_house.png      # 32 wide
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROPS = os.path.join(ROOT, "Tools", "art", "props")
SPRITES = os.path.join(ROOT, "Assets", "Resources", "Sprites")


def shrink(src, width):
    img = Image.open(src).convert("RGBA")
    img = img.crop(img.getbbox())
    height = max(1, round(img.height * width / img.width))
    small = img.resize((width, height), Image.BOX)
    px = small.load()
    for y in range(small.height):
        for x in range(small.width):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255 if a >= 110 else 0)
    return small


def main():
    if len(sys.argv) not in (3, 4):
        sys.exit(__doc__)
    name, src = sys.argv[1], sys.argv[2]
    width = int(sys.argv[3]) if len(sys.argv) == 4 else 32
    small = shrink(src, width)
    os.makedirs(PROPS, exist_ok=True)
    small.save(os.path.join(PROPS, name + ".png"))
    small.save(os.path.join(SPRITES, name + ".png"))
    print(f"{name}: {small.width}x{small.height}")


if __name__ == "__main__":
    main()
