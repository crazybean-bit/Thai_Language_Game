"""Turn PixelLab character exports into the game's 3x4 character sheets.

PixelLab's Character Creator exports either a zip (metadata.json plus PNG frames) or a
GIF of the 8 rotations. This script copies the frames we need into Tools/art/pixellab/<name>/
and builds Assets/Resources/Sprites/<name>.png from them:

    python Tools/import_pixellab.py player path/to/export.zip
    python Tools/import_pixellab.py mali path/to/Idle_rotations_8dir.gif
    python Tools/import_pixellab.py --build          # rebuild every sheet from Tools/art/pixellab

Sheet layout matches CharacterSprite: rows down, left, right, up; columns step, stand, step.
With a walk animation the step columns use two of its frames; without one they use the
standing pose raised by a pixel, which reads as a small bob while walking.
"""
import io
import json
import os
import shutil
import sys
import zipfile

from PIL import Image, ImageDraw, ImageFilter, ImageSequence

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Tools", "art", "pixellab")
OUT = os.path.join(ROOT, "Assets", "Resources", "Sprites")

DIRECTIONS = ["down", "left", "right", "up"]
PIXELLAB_DIR = {"down": "south", "left": "west", "right": "east", "up": "north"}
# Order of the frames in PixelLab's "Idle_rotations_8dir" GIF.
GIF_ORDER = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]


def load_rgba(data):
    return Image.open(io.BytesIO(data) if isinstance(data, bytes) else data).convert("RGBA")


def save_frames(name, frames):
    """frames: {pixellab direction: {"stand": Image, "walk": [Image, ...]}}"""
    dst = os.path.join(ART, name)
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    for d, f in frames.items():
        os.makedirs(os.path.join(dst, "walk", d), exist_ok=True)
        f["stand"].save(os.path.join(dst, d + ".png"))
        for i, w in enumerate(f.get("walk", [])):
            w.save(os.path.join(dst, "walk", d, f"frame_{i:03d}.png"))


def import_gif(path):
    gif = Image.open(path)
    frames = [f.convert("RGBA") for f in ImageSequence.Iterator(gif)]
    if len(frames) != 8:
        sys.exit(f"Expected the 8-direction rotation GIF, got {len(frames)} frames")
    return {d: {"stand": frames[i]} for i, d in enumerate(GIF_ORDER)}


def import_zip(path):
    z = zipfile.ZipFile(path)
    names = z.namelist()
    meta_name = next((n for n in names if n.endswith("metadata.json")), None)
    if meta_name is None:
        sys.exit("No metadata.json in the zip; is this a PixelLab character export?")
    base = meta_name[: -len("metadata.json")]
    meta = json.loads(z.read(meta_name))
    state = meta["states"][0]["frames"]

    def read(rel):
        return load_rgba(z.read(base + rel))

    frames = {d: {"stand": read(p)} for d, p in state["rotations"].items()}
    # Animations are keyed by name; take the first whose name mentions walking.
    for anim_name, by_dir in state.get("animations", {}).items():
        if "walk" not in anim_name.lower():
            continue
        for d, paths in by_dir.items():
            if d in frames:
                frames[d]["walk"] = [read(p) for p in paths]
        break
    return frames


def shadow(size, cx, cy, rx, ry):
    layer = Image.new("RGBA", size)
    ImageDraw.Draw(layer).ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(10, 6, 8, 90))
    return layer.filter(ImageFilter.GaussianBlur(0.8))


def grounded(frame, lift=0):
    """Frame with a soft floor shadow, feet moved to the bottom row, optionally lifted."""
    bbox = frame.getbbox()
    w, h = frame.size
    out = Image.new("RGBA", (w, h))
    feet = bbox[3] if bbox else h
    out.alpha_composite(shadow((w, h), w // 2, h - 2, max(6, (bbox[2] - bbox[0]) // 2 - 2) if bbox else 8, 2))
    moved = Image.new("RGBA", (w, h))
    moved.alpha_composite(frame, (0, h - feet - lift))
    out.alpha_composite(moved)
    return out


def build(name):
    src = os.path.join(ART, name)
    columns = []
    for facing in DIRECTIONS:
        d = PIXELLAB_DIR[facing]
        stand = Image.open(os.path.join(src, d + ".png")).convert("RGBA")
        walk_dir = os.path.join(src, "walk", d)
        walk = sorted(os.listdir(walk_dir)) if os.path.isdir(walk_dir) else []
        if len(walk) >= 2:
            # Two frames half a cycle apart give the left- and right-foot steps.
            a = Image.open(os.path.join(walk_dir, walk[0])).convert("RGBA")
            b = Image.open(os.path.join(walk_dir, walk[len(walk) // 2])).convert("RGBA")
            row = [grounded(a), grounded(stand), grounded(b)]
        else:
            row = [grounded(stand, 1), grounded(stand), grounded(stand, 1)]
        columns.append(row)
    fw, fh = columns[0][1].size
    sheet = Image.new("RGBA", (fw * 3, fh * 4))
    for r, row in enumerate(columns):
        for c, frame in enumerate(row):
            sheet.alpha_composite(frame, (c * fw, r * fh))
    sheet.save(os.path.join(OUT, name + ".png"))
    print(f"{name}: {fw}x{fh} frames -> Assets/Resources/Sprites/{name}.png")


def build_all():
    if not os.path.isdir(ART):
        return
    for name in sorted(os.listdir(ART)):
        if os.path.isdir(os.path.join(ART, name)):
            build(name)


if __name__ == "__main__":
    if sys.argv[1:] == ["--build"]:
        build_all()
    elif len(sys.argv) == 3:
        name, path = sys.argv[1], sys.argv[2]
        frames = import_gif(path) if path.lower().endswith(".gif") else import_zip(path)
        save_frames(name, frames)
        build(name)
    else:
        sys.exit(__doc__)
