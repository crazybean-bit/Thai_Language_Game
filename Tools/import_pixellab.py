"""Turn PixelLab character exports into the game's 3x4 character sheets.

PixelLab's Character Creator exports either a zip (metadata.json plus PNG frames) or a
GIF of the 8 rotations. This script copies the frames we need into Tools/art/pixellab/<name>/
and builds Assets/Resources/Sprites/<name>.png from them:

    python Tools/import_pixellab.py player path/to/export.zip
    python Tools/import_pixellab.py mali path/to/Idle_rotations_8dir.gif
    python Tools/import_pixellab.py player path/to/folder   # south.png etc. plus *walking_south.gif etc.
    python Tools/import_pixellab.py --build          # rebuild every sheet from Tools/art/pixellab

Sheet layout matches CharacterSprite: rows down, left, right, up; columns step, stand, step.
With a walk animation the step columns use two of its frames; without one they use the
standing pose raised by a pixel, which reads as a small bob while walking. The full walk
cycle is also written to <name>_walk.png (same rows, one column per frame), which the game
plays instead of the three-frame cycle when it exists.
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


def import_folder(path):
    """Loose files as downloaded one by one: <direction>.png plus a walking GIF per direction."""
    frames = {}
    for d in GIF_ORDER:
        f = os.path.join(path, d + ".png")
        if os.path.exists(f):
            frames[d] = {"stand": Image.open(f).convert("RGBA")}
    if not frames:
        sys.exit("No south.png / west.png / ... rotations in that folder")
    for f in sorted(os.listdir(path)):
        low = f.lower()
        if not (low.endswith(".gif") and "walk" in low):
            continue
        d = low[:-4].rsplit("_", 1)[-1]
        if d in frames:
            gif = Image.open(os.path.join(path, f))
            frames[d]["walk"] = [g.convert("RGBA") for g in ImageSequence.Iterator(gif)]
    return frames


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


def grounded(frame, lift=0, size=None, feet=None):
    """Frame on a canvas of `size` (default: its own) with a soft floor shadow and the feet on
    the bottom row, optionally lifted. `feet` is the row the feet stand on in the source;
    passing one baseline for a whole walk cycle keeps its natural up-and-down bob."""
    w, h = size or frame.size
    bbox = frame.getbbox()
    if feet is None:
        feet = bbox[3] if bbox else frame.height
    out = Image.new("RGBA", (w, h))
    width = (bbox[2] - bbox[0]) if bbox else 16
    out.alpha_composite(shadow((w, h), w // 2, h - 2, max(6, width // 2 - 2), 2))
    # Animation canvases can be bigger than the rotation ones; keep the figure centred.
    out.alpha_composite(frame, ((w - frame.width) // 2, h - feet - lift))
    return out


def build(name):
    src = os.path.join(ART, name)
    rows, walk_rows = [], []
    for facing in DIRECTIONS:
        d = PIXELLAB_DIR[facing]
        stand = Image.open(os.path.join(src, d + ".png")).convert("RGBA")
        size = stand.size
        walk_dir = os.path.join(src, "walk", d)
        names = sorted(os.listdir(walk_dir)) if os.path.isdir(walk_dir) else []
        walk = [Image.open(os.path.join(walk_dir, n)).convert("RGBA") for n in names]
        if len(walk) >= 2:
            feet = max(f.getbbox()[3] for f in walk if f.getbbox())
            cycle = [grounded(f, size=size, feet=feet) for f in walk]
            # Frames a quarter cycle in are mid-stride; half a cycle apart is the other foot.
            a, b = len(cycle) // 8, len(cycle) // 8 + len(cycle) // 2
            rows.append([cycle[a], grounded(stand), cycle[b]])
            walk_rows.append(cycle)
        else:
            rows.append([grounded(stand, 1), grounded(stand), grounded(stand, 1)])
    fw, fh = rows[0][1].size
    write_sheet(rows, fw, fh, name + ".png")

    walk_path = os.path.join(OUT, name + "_walk.png")
    if len(walk_rows) == 4 and len({len(r) for r in walk_rows}) == 1:
        write_sheet(walk_rows, fw, fh, name + "_walk.png")
    elif os.path.exists(walk_path):
        os.remove(walk_path)
    print(f"{name}: {fw}x{fh} frames, walk cycle: {len(walk_rows[0]) if len(walk_rows) == 4 else 'none'}")


def write_sheet(rows, fw, fh, filename):
    sheet = Image.new("RGBA", (fw * len(rows[0]), fh * len(rows)))
    for r, row in enumerate(rows):
        for c, frame in enumerate(row):
            sheet.alpha_composite(frame, (c * fw, r * fh))
    sheet.save(os.path.join(OUT, filename))


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
        if os.path.isdir(path):
            frames = import_folder(path)
        elif path.lower().endswith(".gif"):
            frames = import_gif(path)
        else:
            frames = import_zip(path)
        save_frames(name, frames)
        build(name)
    else:
        sys.exit(__doc__)
