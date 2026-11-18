# salvageshuttle: tile-connection root cause + collision + console — diagnosis (t_1c29e9bf)

Status: **partial**. Item 1's root cause is proven and the fix is applied to the generator
copy; items 2 (collision) and 3 (console) are **not done**. The bake was **not** re-run, so the
scene on disk still renders with the broken rotations.

## Item 1 — tiles/walls not connected correctly → ROOT CAUSE FOUND (proven)

The wall bake stacks 5 layers per reinforced-wall tile:

1. base `full_0` (the complete SS14 wall sprite), rot 0
2. four corner overlays, one per quadrant: SE, NE, NW, SW

Because SS14 renders RSI state art **per RsiDirection** (`SpriteComponent.GetLayerDrawMatrix`
+ `RsiDirectionExt.OffsetRsiDir`), each overlay layer picks a *cell index* inside its PNG and
applies its own rotation. The generator (`build_layout.py`, `layer_cell_rot`) picked:

    DIR_CELL = {0: 0, 90: 2, 180: 1, 270: 3}      # South 0, North 1, East 2, West 3
    return DIR_CELL[rdeg + offset_deg], -offset_deg   # <-- BUG

The **cell choice is correct** (matches SS14's `OffsetRsiDir`: South -> East for
CounterClockwise, South -> North for Flip, South -> West for Clockwise). The **rotation is
wrong**: the cell art is *already positioned in its own corner quadrant*, so any extra
rotation drags the pre-positioned piece into a neighbouring quadrant.

Observed cell art (pixel alpha, 32x32 cells of the 2x2 sheets `reinf_over{n}.png`, identical
across all 8 fill states):

    cell0 (South) -> piece in BR  == SE corner
    cell2 (East)  -> piece in TR  == NE corner
    cell1 (North) -> piece in TL  == NW corner
    cell3 (West)  -> piece in BL  == SW corner

and each cell holds exactly 256 opaque px = one quadrant. So the four cells of any fill state
tile the 32x32 exactly when placed at rotation 0.

### Proof (tools/compose_check.py, output tools/salvageshuttle-fix-evidence.txt)

Composite the 4 overlay layers of a fully-surrounded wall (`reinf_over7`) and compare with
`full.png` — SS14's own rendering of a fully-connected wall, which is placed with no rotation:

    A all-zero   rots=(0,0,0,0)            opaque=1024  alphaMismatch=0 (vs 1024 px full)
    B port bake  rots=(0,-90,-180,-270)    opaque= 256  alphaMismatch=768
    C sign flip  rots=(0,90,180,270)       opaque= 512  alphaMismatch=512

Candidate A reproduces SS14's `full` state **pixel-exactly** (0 mismatched pixels, full
coverage). The shipped bake (B) covers only 1/4 of each wall tile — the other 3/4 falls back
to the plain base layer, which is exactly the "two different wall types butting together /
corners don't match / wall broken around the console" look in Dan's screenshot. **One bug
explains all of Dan's item-1 visual symptoms.**

### Fix

`layer_cell_rot`: for Dir4 states return rotation `0` instead of `-offset_deg`
(keep the cell index). Applied to `tools/build_layout.fixed.py` (untracked copy of the
scratch generator `~/.hermes/profiles/coder/cache/scratch/build_layout.py`; scratch is pruned
after 24h idle, hence the copy).

### Remaining for item 1
1. Run `python3 tools/build_layout.fixed.py` -> regenerates `Assets/ShipData/salvageshuttle.layout.tsv`
   (expect the wall layer rows to show rot `0` for all four overlays; only the `_{cell}` suffix
   differs: `_0`, `_2`, `_1`, `_3`).
2. Re-run the Unity bake (`Assets/Editor/SalvageShuttleBake.cs`).
3. Fresh render + vision check against Dan's screenshot.
4. Confirm whether the SS14 source really has two wall types meeting where Dan saw them
   (base is `full_0` for every reinforced wall in the current bake -> after the fix the plated
   overlay should cover each tile; re-check before "fixing" anything the source does on purpose).
5. Rotated wall entities: the fix assumes the cell index alone encodes orientation; the current
   TSV has all wall entities at rot 0, so this is unverified for rotated walls.

## Item 2 — collision: NOT STARTED
No colliders added or derived from the layout in this run. Nothing to report but the plan:
derive solidity from the same layout data the bake uses (wall/door/blocked tiles) and verify
headlessly with per-tile physics overlap checks (wall tiles blocked, floor tiles free).

## Item 3 — console scene transition: NOT STARTED
Old shuttle console object, its transition mechanism, and destinations were not identified in
this run. Nothing changed in the scene.

## Housekeeping
- No commits, no pushes — everything left uncommitted per the standing rule.
- Untracked `airlock-telegram-receipts.txt` untouched. `tools/` is new and untracked.
