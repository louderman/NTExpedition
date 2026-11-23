"""Before/after asset preview for the crate lid fix (Pillow only, no Unity).

Renders the SS14 crate art the way the scene now composes it and writes a labelled PNG:

  BEFORE  : Crates/Steel/base.png                          (what HomeShip.unity used to show)
  AFTER   : base + closed.png  (crate shut, m_SortingOrder 1 lid over the body)
  AFTER   : base + open.png    (crate open, StorageCrate.SetOpen(true) swaps the lid state)
  REF     : Crates/Steel/icon.png  (inventory icon; should equal base+closed)

Also asserts the composition identities and prints alpha/bbox numbers.
Output: tools/crate-lid-fix-preview.png  (asset preview, not a runtime screenshot)
"""
from PIL import Image, ImageDraw

ROOT = "/home/dan/Documents/gamedesignlab/NTExpedition"
CRATES = f"{ROOT}/Assets/Sprites/SS14/ShipKit/Crates/Steel"
OUT = f"{ROOT}/tools/crate-lid-fix-preview.png"
S = 8          # zoom
GAP = 3        # cells of padding between panels
BG = (24, 26, 32, 255)


def load(state):
    return Image.open(f"{CRATES}/{state}.png").convert("RGBA")


base, closed, opened, icon = (load(s) for s in ("base", "closed", "open", "icon"))


def over(*layers):
    out = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    for l in layers:
        out.alpha_composite(l)
    return out


before = over(base)
after_closed = over(base, closed)
after_open = over(base, opened)

panels = [
    ("before: base only", before),
    ("after: base+closed", after_closed),
    ("after: base+open", after_open),
    ("reference: icon", icon),
]

cell = 32 * S
gap = GAP * S
pad = 12
W = pad * 2 + len(panels) * cell + (len(panels) - 1) * gap
H = pad * 2 + cell + 28
img = Image.new("RGBA", (W, H), BG)
d = ImageDraw.Draw(img)

for i, (label, p) in enumerate(panels):
    x = pad + i * (cell + gap)
    y = pad
    # checkerboard so transparency is visible
    for cy in range(0, cell, S * 4):
        for cx in range(0, cell, S * 4):
            if ((cx // (S * 4)) + (cy // (S * 4))) % 2 == 0:
                d.rectangle([x + cx, y + cy, x + cx + S * 4 - 1, y + cy + S * 4 - 1], fill=(44, 48, 56, 255))
    img.alpha_composite(p.resize((cell, cell), Image.NEAREST), (x, y))
    d.text((x, y + cell + 6), label, fill=(210, 214, 220, 255))

img.save(OUT)
print("wrote", OUT, img.size)

bc, bo, ic, bl = (p.split()[3] for p in (after_closed, after_open, icon, before))
print("before  (base)        opaque=", sum(1 for v in bl.getdata() if v > 0), "bbox=", bl.getbbox())
print("after   (base+closed) opaque=", sum(1 for v in bc.getdata() if v > 0), "bbox=", bc.getbbox())
print("after   (base+open)   opaque=", sum(1 for v in bo.getdata() if v > 0), "bbox=", bo.getbbox())
print("ref icon              opaque=", sum(1 for v in ic.getdata() if v > 0), "bbox=", ic.getbbox())

# The lid layer must add pixels above the body and must change between closed and open.
blp, bcp, bop = bl.load(), bc.load(), bo.load()
lid_only_closed = sum(1 for y in range(32) for x in range(32)
                      if bcp[x, y] > 0 and blp[x, y] == 0)
lid_rows_closed = sorted({y for y in range(32) for x in range(32)
                          if bcp[x, y] > 0 and blp[x, y] == 0})
lid_rows_open = sorted({y for y in range(32) for x in range(32)
                        if bop[x, y] > 0 and blp[x, y] == 0})
changed = sum(1 for y in range(32) for x in range(32) if bcp[x, y] != bop[x, y])
print("lid-only pixels (closed) =", lid_only_closed, "rows:", (lid_rows_closed[0], lid_rows_closed[-1]) if lid_rows_closed else None)
print("lid-only rows (open) start at y =", lid_rows_open[0] if lid_rows_open else None)
print("pixels differing closed vs open =", changed)
print("base+closed == icon ?", list(bc.getdata()) == list(ic.getdata()))

assert lid_only_closed > 0, "closed lid adds no pixels -> fix would be a no-op"
assert changed > 0, "closed and open lid states are identical"
assert bc.getbbox() != bl.getbbox(), "composite bbox unchanged vs base-only"
print("RESULT: PASS (before state is lidless, after state has a lid that responds to open/close)")
