# Salvage shuttle visual recreation — resolved spec (NTExpedition HomeShip)

Task: kanban `t_282d2507`. Source: `/home/dan/Downloads/salvageshuttle.yml`.
Live Unity project: `/home/dan/Documents/gamedesignlab/NTExpedition` (Unity 6000.6.0f1).
Frontier source of truth: `/home/dan/Documents/SS1/frontier-station-14` @ `fc74013522ef2ee1c5049c986748c9f0866fb4c3`
(this is exactly the commit recorded in `Assets/Sprites/SS14/ShipKit/import-manifest.json` → `sourceRevision`).

Status: research/decoding **complete and verified**. Scene baking + rendering **not yet done**.
Everything below was derived from source, not guessed; re-verify anything marked TODO.

## 0. Objective (user's words)

"we only need the sprites placed, I do not care about functionality at all right now. Replace Homeship's current box with it."
→ visual-only recreation: floors + walls + windows + doors + grilles + catwalk + props, correct
placements/rotations/connected-variant states/sorting. No gameplay systems, no colliders unless required
to preserve existing startup (disclose if added).

## 1. Decoded source data (already produced, in scratch)

Scripts (re-runnable, in `~/.hermes/profiles/coder/cache/scratch/`):
`decode_map.py`, `resolve_protos.py`, `assets_needed.py`, `tile_summary.py`, `scene_summary.py`,
`extract_specs2.py`, `ent_specs.txt`, `protos2.txt`, `assets.txt`, `tsummary.txt`, `scene_summary.txt`.

- `decode_map.py` → `out/tiles.csv` (180 rows, x,y,tile,flags,variant,rotmirror) and
  `out/entities.csv` (133 rows, uid,proto,x,y,rot).
  Map v7 chunk decode: 16x16 chunks, per tile `int32 tileId | byte flags | byte variant | byte rotmirror`
  (per `RobustToolbox/Robust.Shared/EntitySerialization/MapChunkSerializer.cs`). All tiles: flags=0,
  rotmirror=0. Variants only non-zero on the 4-variant concrete sheets.
- Tile counts: Plating 106, FloorAsteroidTile 31, FloorGrayConcrete 20, FloorConcrete 14, Lattice 6,
  FloorOldConcrete 3. Space = empty (not written).
- Entity counts (23 protos, 133 ents): WallReinforced 48, Catwalk 26, WallReinforcedDiagonal 10,
  Grille 7, ReinforcedWindow 7, AirlockExternalGlassShuttleArrivals 6, Thruster 4, Chair 3,
  SolidReinforcedSecretDoor 3, DebugThruster 3, GrilleDiagonal 2, Gyroscope 2, ReinforcedWindowDiagonal 2,
  AirlockEngineering 1, AirlockSalvage 1, ComputerShuttle 1, ChairPilotSeat 1, LockerSalvageSpecialist 1,
  NitrogenCanister 1, OxygenCanister 1, PortableGeneratorSuperPacman 1, SMESBasic 1, SubstationBasicEmpty 1.
- Tile bbox x[-3..7] y[-18..5]; entity bbox x[-2.5..7.5] y[-17.5..5.5].
- Excluded by design: Space tiles (nothing to draw); the grid entity itself; hidden inventory contents /
  runtime-managed layers (see §4).

## 2. Coordinate + scale mapping (decided)

- Unity project PPU = 32 (`spritePixelsToUnits: 32` in every ShipKit `.meta`); HomeShip `Grid` cellSize 1x1.
- SS14 world units ↔ Unity world units 1:1. Entity → `(x, y)` as decoded (SS14 tile (0,0) spans [0,1]²,
  Unity tile centre for cell (0,0) is (0.5,0.5) — identical).
- Floor tile (tx,ty) → Unity tilemap cell (tx,ty); tilemap `m_TileAnchor` is (0.5,0.5) → centre matches.
- No axis flip: both y-up.
- Keep the map's absolute coordinates (do not re-centre) so relative placements stay faithful.
  The old box occupies tiles x[-7..6] y[-5..4]; the new ship x[-3..7] y[-18..5] overlaps it → the box must go.

## 3. RSI direction / frame mapping (verified from RobustToolbox)

- `Robust.Shared/Graphics/RSI/RsiDirection.cs`: **South=0, North=1, East=2, West=3** (SE=4, SW=5, NE=6, NW=7).
- `SpriteComponent.GetDirection` (Robust.Client/…/SpriteComponent.cs ~1661): angle 0 → South, π/2 → East,
  π → North, 3π/2 → West (`Dir1` states always South).
- Existing ShipKit `.meta` files slice each PNG as **`<pngStem>_<cellIndex>`**, cells in reading order
  (left-to-right, top-to-bottom; `solid0_0` = rect x0,y32 for a 64×64 = top-left). So
  **cellIndex == RsiDirection** for 4-direction states.
  → South=cell 0, North=cell 1, East=cell 2, West=cell 3.
- Entity rotation → RsiDirection: 0→South(0), π/2→East(2), π→North(1), −π/2→West(3).
- `noRot: true` sprites (`Chair*`, `Locker*`, `*Canister`, `PortableGenerator*`, `SMESBasic`,
  `SubstationBasicEmpty`): frame still comes from rotation, but the image is **not** rotated
  (`GetLayerDrawMatrix` skips the rotation). For these protos place the raw unrotated frame.

### 3a. Corner smoothing (walls, windows, catwalk) — exact algorithm
`Content.Client/IconSmoothing/IconSmoothSystem.cs` (Frontier fork):
- Neighbours n, ne, e, se, s, sw, w, nw (8) each “match” if the entity there has an `IconSmooth` with the
  same `SmoothKey` (`walls`, or `catwalk` for catwalk; `AdditionalKeys` none here), `Enabled`, and passes
  `EntityIsSmoothOnEdge` (diagonal-mode entities only count on some edges — §3c).
- Corner fill bits: `CounterClockwise=1, Diagonal=2, Clockwise=4`
  - cornerNE = n?1 + ne?2 + e?4
  - cornerSE = e?1 + se?2 + s?4
  - cornerSW = s?1 + sw?2 + w?4
  - cornerNW = w?1 + nw?2 + n?4
- Rotation remap (`xform.LocalRotation.GetCardinalDir()`): South → (NE,NW,SW,SE) unchanged;
  North → (SW,SE,NE,NW); West → (SE,NE,NW,SW); East → (NW,SW,SE,NE) [i.e. the default branch].
  For all 58 wall/14 window entities in this map rotation is 0 → remap is identity.
- Each entity draws its **base layer** state (state named in the prototype, or RSI state `full` for walls/
  windows — TODO: confirm default-state rule; `full` is the first state in both metas) plus 4 added layers:
  | layer | RSSI state | dir offset (DirExt.OffsetRsiDir) | resulting cell for base dir South |
  |---|---|---|---|
  | SE | `{base}{cornerSE}` | None | dir South → **cell 0** |
  | NE | `{base}{cornerNE}` | CounterClockwise | South→East → **cell 2** |
  | NW | `{base}{cornerNW}` | Flip | South→North → **cell 1** |
  | SW | `{base}{cornerSW}` | Clockwise | South→West → **cell 3** |
  Layer draw order in SS14 = SE, then the 4 corner layers in enum order (SE,NE,NW,SW) after the base
  layer; replicate with increasing Unity `sortingOrder` inside the same tile.
- Wall corner base names: `WallReinforced` → `reinf_over` (`_NF/Structures/Walls/solid.rsi`);
  `ReinforcedWindow` → `rwindow` (`_NF/Structures/Windows/reinforced_window.rsi`);
  `Catwalk` → `catwalk_` (`Structures/catwalk.rsi`; base layer state `catwalk_0` per prototype-less state —
  TODO decide: draw corner layers only, no base layer for catwalk since the RSI has no `full`/`catwalk` state).

### 3b. Diagonal smoothing (`mode: DiagonalNF`) — walls/`state0..4`
`CalculateNewSpriteDiagonalNF`: neighbours checked are `(1,0)` then `(0,-1)` **rotated by entity rotation**;
bit0 = that neighbour matches, bit1 = second matches; if `value == 3` and the rotated `(1,-1)` neighbour
matches, `value = 4`. State = `{StateBase}{value}` → `state0..state4` (`_NF/Structures/Walls/reinforced_diagonal.rsi`
has exactly state0..4, one frame each).
- `ReinforcedWindowDiagonal` uses `mode: Diagonal` (not NF) + base `state` (`_NF/Structures/Windows/reinforced_window_diagonal.rsi`, state0/state1) — the non-Frontier `CalculateNewSpriteDiagonal` path must be read for its exact neighbour/state rule (TODO, 2 entities).
- Diagonal entities also act as *neighbours* only on some edges (`EntityIsSmoothOnEdge`): for a diagonal-mode
  target, `rot = -targetRotation; angle = new Angle(rot.RotateVec(-sourceDir)) + π/2;
  dir = angle.GetCardinalDir();` match iff `dir ∈ {South, SouthEast, East}`. Must be ported exactly
  (12 diagonal entities ⇒ affects surrounding wall corner bits).

### 3c. Static (non-smoothing) entities
`mode: NoSprite` (Grille, GrilleDiagonal, all 3 airlocks, SolidReinforcedSecretDoor): single fixed state,
no corner layers, but they still *count* as smoothing neighbours for walls/windows.

## 4. Per-proto sprite spec (resolved; layers to draw)

Draw depth enum: `Content.Shared/DrawDepth/DrawDepth.cs`, all relative to `DrawDepthTag.Default`
(`…/DrawDepthTag.cs`): FloorTiles −12, Walls −2, WallTops −1, Objects 0, SmallObjects +1,
WallMountedItems +2, Items +4, Mobs +6, Doors +8, Effects +11.
Unity `sortingOrder` scheme: `sortingOrder = drawDepthOffset * 10 + layerIndex` (layerIndex = order within entity).

| proto | layers to draw (state @ rsi) | draw depth |
|---|---|---|
| WallReinforced | base `full` + corners `reinf_over{0-7}` @ `_NF/Structures/Walls/solid.rsi` | Walls (−2) |
| WallReinforcedDiagonal | `state0..4` @ `_NF/Structures/Walls/reinforced_diagonal.rsi` | Walls |
| ReinforcedWindow | base `full` + corners `rwindow{0-7}` @ `_NF/Structures/Windows/reinforced_window.rsi` | WallTops (−1) |
| ReinforcedWindowDiagonal | `state0/1` @ `_NF/Structures/Windows/reinforced_window_diagonal.rsi` | WallTops |
| Grille | `grille` @ `Structures/Walls/grille.rsi` | Walls |
| GrilleDiagonal | `grille_diagonal` @ `Structures/Walls/grille.rsi` | Walls |
| Catwalk | corners `catwalk_{0-7}` @ `Structures/catwalk.rsi` | FloorTiles (−12) |
| AirlockExternalGlassShuttleArrivals | `closed` @ `Structures/Doors/Airlocks/Glass/shuttle.rsi` | Doors (+8) |
| AirlockEngineering | `closed` @ `Structures/Doors/Airlocks/Standard/engineering.rsi` | Doors |
| AirlockSalvage | `closed` @ `Structures/Doors/Airlocks/Standard/salvage.rsi` | Doors |
| SolidReinforcedSecretDoor | `closed` @ `_NF/Structures/Doors/secret_door_reinforced.rsi` | Walls (inherits BaseStructure; TODO confirm) |
| Thruster / DebugThruster | `base` @ `_NF/Structures/Shuttles/thruster.rsi` | Objects (default; TODO confirm) |
| Gyroscope | `base` @ `_NF/Structures/Shuttles/gyroscope.rsi` | Objects (default) |
| Chair | `chair` @ `Structures/Furniture/chairs.rsi` (noRot) | Objects (default) |
| ChairPilotSeat | `shuttle` @ same (noRot) | Objects (default) |
| ComputerShuttle | `computer`, `generic_keyboard`, `shuttle`, `generic_keys` @ `Structures/Machines/computers.rsi` | Objects |
| LockerSalvageSpecialist | `generic`, `generic_door` @ `Structures/Storage/closet.rsi` (noRot) | Objects (default) |
| NitrogenCanister / OxygenCanister | `nitrogen` / `oxygen` @ `_NF/Structures/Storage/canister.rsi` (noRot) | Objects (default) |
| PortableGeneratorSuperPacman | `portgen1` @ `Structures/Power/Generation/portable_generator.rsi` (noRot) | Objects (default) |
| SMESBasic | `smes` @ `Structures/Power/smes.rsi` (noRot) | Objects (default) |
| SubstationBasicEmpty | `substation` @ `Structures/Power/substation.rsi` (noRot) | Objects (default) |

Explicitly **excluded** (runtime/system-managed or invisible layers, documented as exclusions):
airlock `closed_unlit/bolted_unlit/emergency_unlit/welded/panel_open/electrified*` (system-driven),
thruster `thrust*` (off when idle; `visible: false`), gyroscope `thrust*`, locker `locked` (LockSystem),
`welded`/`paper` (invisible), generator `portgen_on_unlit` (invisible), SMES charge/input/output overlays
(runtime values; TODO optional: include `smes-oc0`/`smes-op1` as the default idle look),
substation `screen`/`full` (runtime), grille `electrified` sparks, computer `generic_panel_open` (panel shut).
Nothing inside containers/inventories is drawn (they are not in the map at all).

## 5. Asset import plan

Convention to follow (established by the ShipKit import): copy PNGs byte-identical from
`Resources/Textures/<rsi>/<state>.png`, keep per-RSI `source-meta.json` (license, copyright, 32×32,
state list + directions), record sha256 + source path in an `import-manifest.json`, slice each PNG in the
Unity importer as `<pngStem>_<cell>` (PPU 32, Point filter, uncompressed, no mipmaps, spriteMode Multiple).

New imports needed (source RSI → destination folder; **all under `Assets/Sprites/SS14/SalvageShuttle/`**):
- `_NF/Structures/Walls/solid.rsi` → `Walls/Reinforced/`: `full.png` + `solid0-7.png` + `reinf_over0-7.png`
  (17 files; mirrors the existing `ShipKit/Walls/Steel` precedent). license CC-BY-SA-3.0,
  copyright "Taken from https://github.com/ParadiseSS13/Paradise/ and modified by FoxxoTrystan".
  NOTE: `ShipKit/Walls/Steel/*` are the **upstream non-`_NF`** art (sha256 ≠ `_NF`), so they are NOT the
  pixels the game shows for `WallReinforced` — do not reuse them for the reinforced walls.
- `_NF/Structures/Walls/reinforced_diagonal.rsi` → `Walls/ReinforcedDiagonal/`: `state0-4.png` (5).
  CC-BY-SA-3.0, "Sprited by erhardsteinhauer (discord), edited by Whatstone (Discord) for SS14 New Frontier".
- `_NF/Structures/Windows/reinforced_window.rsi` → `Windows/Reinforced/`: `full.png` + `rwindow0-7.png` (9).
  Same Paradise/FoxxoTrystan licence line.
- `_NF/Structures/Windows/reinforced_window_diagonal.rsi` → `Windows/ReinforcedDiagonal/`: `state0,state1` (2).
  CC-BY-SA-3.0, "Made by brainfood1183 (github), transparency tweaked by Ubaser."
- `Structures/catwalk.rsi` → `Catwalk/`: `catwalk_0-7.png` (8). CC-BY-SA-3.0 tgstation c6e3401… "modified by Swept".
- `Structures/Walls/grille.rsi` → `Grilles/`: `grille.png`, `grille_diagonal.png` (2). CC-BY-SA-3.0 tgstation 17c603f….
- `Structures/Doors/Airlocks/Glass/shuttle.rsi` → `Doors/GlassShuttle/`: `closed.png` (1). CC-BY-SA-3.0 CEV-Eris 1451793… "modified by 20kdc & AJCM-git, glass by Peptide90".
- `Structures/Doors/Airlocks/Standard/engineering.rsi` → `Doors/Engineering/`: `closed.png`. CC-BY-SA-3.0 tgstation c6e3401….
- `Structures/Doors/Airlocks/Standard/salvage.rsi` → `Doors/Salvage/`: `closed.png`. CC-BY-SA-3.0 tgstation c6e3401… "salvage version made by BackeTako".
- `_NF/Structures/Doors/secret_door_reinforced.rsi` → `Doors/SecretDoorReinforced/`: `closed.png`.
  CC-BY-SA-3.0 "Made by Nimfar11 (GitHub) for Space Station 14".
- `_NF/Structures/Shuttles/thruster.rsi` → `Shuttles/Thruster/`: `base.png`. **CC-BY-SA-4.0** (only CC-BY-SA-4.0 source here),
  "base taken from https://github.com/Baystation12/Baystation12/tree/7274e5654c3cf82d4104dbf818ef00fdc0082aa8".
- `_NF/Structures/Shuttles/gyroscope.rsi` → `Shuttles/Gyroscope/`: `base.png`. CC-BY-SA-4.0 "by Shaeone".
- `_NF/Structures/Storage/canister.rsi` → `Storage/Canister/`: `nitrogen.png`, `oxygen.png`. CC-BY-SA-3.0 "Made by stagnation for Frontier".
- `Structures/Storage/closet.rsi` → `Storage/Closet/`: `generic.png`, `generic_door.png` (+optional `locked.png`).
  CC-BY-SA-3.0 "Taken from tgstation, …" (see assets.txt line for full string).
- `Structures/Power/Generation/portable_generator.rsi` → `Power/PortableGenerator/`: `portgen1.png`. CC-BY-SA-3.0 Baystation12 caa635e… "Adapted to SS14 and portgen3 sprites made by PJB3005".
- `Structures/Power/smes.rsi` → `Power/SMES/`: `smes.png`. CC-BY-SA-3.0 tgstation 9c7d509… "modified by Swept…".
- `Structures/Power/substation.rsi` → `Power/Substation/`: `substation.png`. CC-BY-SA-3.0 "Drawn by Ubaser."
- Floors: `Tiles/lattice.png`, `Tiles/Asteroid/asteroid_tile.png`, `Tiles/Planet/Concrete/{grayconcrete,concrete,oldconcrete}.png`
  → `Tiles/` (+`Tiles/Concrete/`). Licences per the existing `Assets/Sprites/SS14/Hull/source-attributions.yml`
  (lattice = tgstation 8abb195…; asteroid_tile = vgstation; concrete sheets: check `Resources/Textures/Tiles/…` meta licenses).
- Reuse (byte-identical to source ⇒ reference existing ShipKit assets): `ShipKit/Floors/plating.png`
  (`Tiles/plating.png`), chairs/`chair.png`+`shuttle.png` (`Structures/Furniture/chairs.rsi`),
  computers `computer/generic_keyboard/shuttle/generic_keys` (`Structures/Machines/computers.rsi`) —
  **verify sha256 equality before reusing**; if different, import into `SalvageShuttle/` instead.
- Append (never rewrite) a NOTICE: add the new attribution rows to `Assets/Sprites/SS14/SalvageShuttle/source-attributions.yml`.

## 6. Scene edit plan (next run)

1. Generate `Assets/ShipData/salvageshuttle.layout.json` from `out/tiles.csv` + `out/entities.csv` with all
   state/cell/sorting decisions already computed (Python; no SS14 logic left in C#).
2. Import PNGs + slice via a small Editor method under `Assets/Editor/` (mirror `RsiPreview.cs` slicing code,
   names `<stem>_<cell>`, PPU 32) — do not modify `RsiPreview.cs`.
3. Editor method (`-batchmode -executeMethod …`) that: repurposes the existing `Grid` GO
   (`Grid/floor` tilemap → ship floors; `Grid/wall` tilemap → cleared), creates `SalvageShuttle` root with
   category children, bakes every sprite layer as a `SpriteRenderer` at `(x,y)` with the computed cell
   (`SpriteRenderer.sprite` from `AssetDatabase.LoadAllAssetsAtPath`), sets sortingOrder, saves the scene.
4. Old box: clear all 44 tiles from the `wall` tilemap (kills its stale geometry **and** its
   `TilemapCollider2D` collision — disclose; the user asked for visual replacement only and the ship has no
   colliders, so the player can now leave the hull), clear/repaint `floor` tilemap. Do not delete GOs.
5. Spawn check: `Player` is at (4.14,−0.09) → tile (4,−1) = FloorAsteroidTile (inside the new hull) ✓;
   `ShuttleConsole` (ScenePortal) at (0,0) ✓ floor; supply stations at (2.64,−1.09) and (5.64,−1.09) ✓ floor.
   No spawn move is needed unless the render shows otherwise.
6. Screenshots: batchmode render needs a display — `X0`/`X1` sockets exist (`DISPLAY=:0`), Unity 6000.6.0f1 at
   `~/Unity/Hub/Editor/*/Editor/Unity`, entitlement licence present at `~/.config/unity3d/Unity/licenses/`.
   Use a temporary camera + `RenderTexture` → PNG (full ship + interior close-up), then remove it.
   **Preserve Dan's live unsaved editor state**: if an editor is open on this project, do the bake in an
   isolated copy and label it — check `pgrep -a -f "[Uu]nity"` first.
7. Verification: grep the saved `HomeShip.unity` for the new sprite GUIDs, count placed layers vs the source
   map (180 floor tiles, 58 wall tiles ×5 layers, 26 catwalk, 9 grille, 9 window, 11 doors, 4 thruster, 2 gyro,
   1 console, 4 chairs, 1 locker, 2 canisters, 1 generator, 1 SMES, 1 substation, 3 secret doors), then
   compare the render against the decoded layout.
8. Report: `Assets/ShipData/salvageshuttle-report.md` with changed files, coverage, exclusions, checksums.
