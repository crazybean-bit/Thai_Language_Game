"""Tiny pixel-art helpers used by generate_sprites.py.

A Canvas is an RGBA numpy array. Shapes are drawn with plain fills, then
outline() traces a dark border around everything, which does most of the work
of making simple shapes read as proper pixel art.
"""
import numpy as np
from PIL import Image


def rgb(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def shade(c, amount):
    """Lighten (amount > 0) or darken (amount < 0) a colour, shifting hue slightly
    toward warm when lighter and cool when darker, the way pixel artists do."""
    r, g, b, a = c
    if amount >= 0:
        r = r + (255 - r) * amount
        g = g + (240 - g) * amount * 0.9
        b = b + (200 - b) * amount * 0.7
    else:
        k = -amount
        r = r * (1 - k)
        g = g * (1 - k * 0.95)
        b = b * (1 - k * 0.8) + 20 * k
    return (int(max(0, min(255, r))), int(max(0, min(255, g))), int(max(0, min(255, b))), a)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = np.zeros((h, w, 4), dtype=np.uint8)

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            if len(c) == 4 and c[3] < 255:
                base = self.px[y, x].astype(float)
                a = c[3] / 255
                if base[3] == 0:
                    self.px[y, x] = c
                else:
                    self.px[y, x, :3] = (np.array(c[:3]) * a + base[:3] * (1 - a)).astype(np.uint8)
            else:
                self.px[y, x] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return tuple(int(v) for v in self.px[y, x])
        return (0, 0, 0, 0)

    def filled(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and self.px[y, x, 3] > 0

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def ellipse(self, cx, cy, rx, ry, c, clip=None):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                dx = (x + 0.5 - cx) / rx
                dy = (y + 0.5 - cy) / ry
                if dx * dx + dy * dy <= 1.0 and (clip is None or clip(x, y)):
                    self.put(x, y, c)

    def poly(self, pts, c):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            for x in range(int(min(xs)), int(max(xs)) + 1):
                if _inside(x + 0.5, y + 0.5, pts):
                    self.put(x, y, c)

    def outline(self, c, only_outer=True):
        """Draw c on every empty pixel that touches a filled one (4-neighbour)."""
        mask = self.px[:, :, 3] > 0
        add = []
        for y in range(self.h):
            for x in range(self.w):
                if mask[y, x]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and mask[ny, nx]:
                        add.append((x, y))
                        break
        for x, y in add:
            self.px[y, x] = c

    def soft_outline(self, darken=0.62, fallback=None):
        """Outline in a dark version of whatever colour it borders, instead of flat
        black. Muted, coloured outlines are a big part of the softer modern pixel look."""
        mask = self.px[:, :, 3] > 200
        add = []
        for y in range(self.h):
            for x in range(self.w):
                if mask[y, x]:
                    continue
                cols = []
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and mask[ny, nx]:
                        cols.append(self.px[ny, nx, :3].astype(int))
                if cols:
                    avg = sum(cols) // len(cols)
                    c = shade((int(avg[0]), int(avg[1]), int(avg[2]), 255), -darken)
                    add.append((x, y, fallback or c))
        for x, y, c in add:
            self.px[y, x] = c

    def recolor_edges(self, c):
        """Darken filled pixels that sit on the shape's border (an inner outline)."""
        mask = self.px[:, :, 3] > 0
        edge = []
        for y in range(self.h):
            for x in range(self.w):
                if not mask[y, x]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < self.w and 0 <= ny < self.h) or not mask[ny, nx]:
                        edge.append((x, y))
                        break
        for x, y in edge:
            self.px[y, x] = c

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                p = other.px[y, x]
                if p[3] > 0:
                    self.put(ox + x, oy + y, tuple(int(v) for v in p))

    def image(self):
        return Image.fromarray(self.px, "RGBA")

    def save(self, path):
        self.image().save(path)


def _inside(x, y, pts):
    n = len(pts)
    inside = False
    j = n - 1
    for i in range(n):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-9) + xi:
            inside = not inside
        j = i
    return inside


def noise(x, y, seed=0):
    """Deterministic per-pixel noise in [0, 1)."""
    n = (x * 374761393 + y * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65536.0
