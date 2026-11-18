"""Decide the correct per-layer Unity rotation for the wall corner overlays.

Ground truth: SS14 renders a wall whose 8 neighbours are all solid with the
`full` state (dirs=1) -- i.e. NO per-layer rotation. The bake instead places the
base `full` plus 4 corner-overlay pieces of state reinf_over7. So the correct
candidate rotation set is the one whose composite reproduces `full`.

Layers / cells per build_layout.py: SE->c0, NE->c2, NW->c1, SW->c3; cell art is
32x32 inside a 2x2 sheet (row-major).
"""
import os
from PIL import Image

BASE = "/home/dan/Documents/gamedesignlab/NTExpedition/Assets/Sprites/SS14/SalvageShuttle/Walls/Reinforced"
CELL = 32


def cell_img(stem, n, d):
    img = Image.open(os.path.join(BASE, f"{stem}{n}.png")).convert("RGBA")
    cols = img.size[0] // CELL
    return img.crop(((d % cols) * CELL, (d // cols) * CELL, (d % cols) * CELL + CELL, (d // cols) * CELL + CELL))


def composite(layers, rots):
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    for (stem, n, d), rot in zip(layers, rots):
        c = cell_img(stem, n, d)
        if rot:
            c = c.rotate(rot % 360, resample=Image.NEAREST, expand=False)
        out.alpha_composite(c)
    return out


def diff(a, b):
    pa, pb = a.load(), b.load()
    tot = n = big = 0
    for y in range(CELL):
        for x in range(CELL):
            ag = pa[x, y][3] > 8
            bg = pb[x, y][3] > 8
            if ag != bg:
                big += 1
                continue
            if ag:
                n += 1
                tot += sum(abs(pa[x, y][i] - pb[x, y][i]) for i in range(3)) / 3.0
    return big, (tot / n if n else 0.0)


full = Image.open(os.path.join(BASE, "full.png")).convert("RGBA")
if full.size != (CELL, CELL):
    full = full.crop((0, 0, CELL, CELL))
layers = [("reinf_over", 7, 0), ("reinf_over", 7, 2), ("reinf_over", 7, 1), ("reinf_over", 7, 3)]
cands = {
    "A all-zero": (0, 0, 0, 0),
    "B port -offset (CCW+)": (0, -90, -180, -270),
    "C port sign-flipped (CW+)": (0, 90, 180, 270),
}
for name, rots in cands.items():
    img = composite(layers, rots)
    big, md = diff(img, full)
    op = sum(1 for p in img.getdata() if p[3] > 8)
    print(f"{name:28} rots={str(rots):22} opaque={op:5} alphaMismatch={big:5} meanRGBdiff={md:.1f}")

# coverage sanity: how many opaque px does `full` have / one cell of state 7
print("full opaque:", sum(1 for p in full.getdata() if p[3] > 8))
for d in range(4):
    c = cell_img("reinf_over", 7, d)
    print(f"  reinf_over7 cell{d} opaque:", sum(1 for p in c.getdata() if p[3] > 8))
for st in ("reinf_over", "solid"):
    for n in (0, 3, 7):
        tot = 0
        for d in range(4):
            tot += sum(1 for p in cell_img(st, n, d).getdata() if p[3] > 8)
        print(f"  {st}{n} 4 cells total opaque:", tot)
