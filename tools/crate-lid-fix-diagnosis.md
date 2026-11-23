# StorageCrate: missing lid sprite — diagnosis and fix

Date: 2026-11-19. Project: `/home/dan/Documents/gamedesignlab/NTExpedition` (Unity 6000.6.0f1).
Dan: "Can you fix the crate sprite? Its missing its lid."
Status: diagnosis + fix implemented locally, uncommitted. Unity playtest still pending (see end).

## Cause

The crate's world `SpriteRenderer` (`Assets/Scenes/HomeShip.unity`, GameObject `StorageCrate`,
fileID 224699926) referenced only the SS14 **`base`** state of `Structures/Storage/Crates/generic.rsi`:

```
9379    m_SortingOrder: 0
9381    m_Sprite: {fileID: -3966044189379602213, guid: 16c3306fa9d87e11ebc71de16fe30374, type: 3}  # Crates/Steel/base.png
```

`base` is only the crate **body** (the lidless box). In Space Station 14 / Frontier a crate is drawn as
**two sprite layers on one entity**, verified in the upstream prototype this project was ported from
(`/home/dan/Documents/SS1/frontier-station-14/Resources/Prototypes/Entities/Structures/Storage/Crates/base_structurecrates.yml`):

```yaml
- type: Sprite
  sprite: Structures/Storage/Crates/generic.rsi
  layers:
  - state: base
    map: ["enum.StorageVisualLayers.Base"]      # body, always visible
  - state: closed
    map: ["enum.StorageVisualLayers.Door"]      # lid
  - state: welded
    visible: false
- type: EntityStorageVisuals
  stateDoorOpen: open        # Door layer swaps to `open` when the crate is opened
  stateDoorClosed: closed
```

The Unity port had layer 0 (`base`) only: there is no lid GameObject/child, and `StorageCrate.cs` never
touched any sprite (its `SetOpen` only toggled the inventory UI). So the crate rendered as a lidless box
in every state — the reported "missing its lid".

## Red evidence (alpha/geometry, measured with Pillow)

Steel and Plastic crate art is identical in geometry (only the palette differs):

| state  | size   | opaque px | alpha bbox (x0,y0,x1,y1) | interpretation |
|--------|--------|-----------|--------------------------|----------------|
| base   | 32x32  | 448       | (2, 13, 30, 29)          | crate body (lower box) |
| closed | 32x32  | 330       | (2, 9, 30, 21)           | lid, shut — sits above/over the body |
| open   | 32x32  | 342       | (2, 1, 30, 15)           | lid, flipped back |
| icon   | 32x32  | 554       | (2, 9, 30, 29)           | inventory icon |
| welded | 32x32  | 57        | (3, 18, 29, 22)          | weld overlay, hidden by default |

Composition checks:

* `base + closed` = 554 opaque px, bbox (2, 9, 30, 29) — **exactly** the `icon` state's 554 px and bbox.
  Confirms the intended crate look is body + lid, and that `icon` is that composite pre-rendered.
* `base + open` = 778 opaque px, bbox (2, 1, 30, 29) — the flipped-open lid above the body.
* `base ∩ closed` = 224 px — the lid layer deliberately overlaps the body's top rows, so it is a real
  separate layer, not a transparent hole-filler that a single sprite was supposed to cover.
* Rendering `base` alone (the shipped state) leaves rows y=9..12 empty and y=13..28 body-only: the lid
  band is absent. That is the red state.

Both states are single full-cell 32x32 sprites, `alignment: Center`, `pivot: (0.5, 0.5)`, Point filter
(`filterMode: 0`), same import settings — so a lid layer at the crate's local origin overlays the body
exactly, with no scale/pivot compensation needed.

## Fix (minimal, no new art)

1. `Assets/Scenes/HomeShip.unity` — added one child GameObject `CrateLid` under the `StorageCrate`
   transform (fileID 224699932): Transform + `SpriteRenderer` with
   `Crates/Steel/closed.png` (guid 542ef38a5812995848b8a301b30cb36d, sprite fileID -3330984756735713963),
   `m_SortingOrder: 1` (one above `base`'s 0), same builtin sprite material as the body renderer.
   The `StorageCrate` MonoBehaviour gained `lidRenderer`, `closedLidSprite`, `openLidSprite` refs.
   New fileIDs 919100001/919100002/919100003 — verified unused elsewhere in the scene.
2. `Assets/Scripts/Inventory/StorageCrate.cs` — `SetOpen(bool)` now also swaps the lid layer between
   `closed` and `open`, matching SS14's `EntityStorageVisuals` (`stateDoorClosed: closed`,
   `stateDoorOpen: open`); `Awake` normalises the lid to `closed`.

Untouched: all PNGs, all `.meta` files (GUIDs/import settings preserved), Plastic kit (never referenced
by any scene/prefab — different variant, left as-is), colliders, gameplay/input, scene layout, the
`welded`/`sparking` layers (SS14 hides them by default and the port has no welder/spark state).

Regeneration: no existing mechanism rebuilds this crate. `Assets/Editor/InventorySandboxSetup.cs` only
creates the separate `InventorySandbox.unity` and builds players; `Assets/Editor/RsiAnimationGenerator.cs`
is a manual menu action on a selected texture and does not slice or rewrite scene sprites. So the scene
edit is durable.

## Verification

See `tools/crate-lid-fix-evidence.txt` (checks + results) and `tools/crate-lid-fix-compose_check.py`
(re-runnable render check producing the before/after asset preview).

Pending: Unity playtest (open HomeShip, press Play, click the crate) — not run here; no editor was
launched and no build was packaged, per task constraints.
