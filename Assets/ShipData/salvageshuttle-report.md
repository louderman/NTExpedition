# SalvageShuttle → HomeShip replacement report

**Task**: kanban `t_282d2507` — replace HomeShip's placeholder box with the layout from
`/home/dan/Downloads/salvageshuttle.yml`. **Visual only** (user: *"we only need the sprites placed, I do
not care about functionality at all right now"*).
**Project**: `/home/dan/Documents/gamedesignlab/NTExpedition` (Unity 6000.6.0f1, URP 17.6, 2D).
**Executed**: 2026-11-18. Branch `dan`, HEAD `e14456fbf9af263d3e12249589af2fd98fc5c75e` (starter gun fx — untouched).

## Result

HomeShip's box visuals are replaced by **643 placed sprites** forming the salvageshuttle layout.
Both placeholder Tilemaps (`wall`, `floor`) were cleared (5 tiles); no GameObject was deleted.
Scene saved to `Assets/Scenes/HomeShip.unity`.

## Gates and evidence

| Gate | What ran | Evidence |
|---|---|---|
| 1 decode | `build_layout.py` (pre-resolves all SS14 logic in Python) | exit 0; 5 plain tiles + 63 PNGs copied; **180 tiles, 133 entities → 643 placements**; `sprite errors: none`; wrote `salvageshuttle.layout.tsv` |
| 2 bake | `Unity -batchmode -nographics -executeMethod SalvageShuttleBake.ImportAndBake -quit` | **exit 0**; log: `[Salvage] sliced 63 textures`, `[Salvage] cleared 5 placeholder tiles; 313 entities, 643 sprite layers placed; 0 problems`, `ImportAndBake complete`. Scene on disk 267 866 B → 2 131 808 B |
| 3 verify | `verify_scene.py` (independent re-parse of the saved scene vs the TSV) | 643 TSV rows = **643 SpriteRenderers under `SalvageShuttle`**; **expected-but-missing 0**; **in-scene-but-not-expected 0**; null-sprite renderers 0; `SalvageShuttle` node at localPos (0,0), parent `Grid` |
| 3 render | `Unity -batchmode -executeMethod SalvageShuttleBake.RenderOnly` (URP `SingleCameraRequest`, DISPLAY=:1) | exit 0; log `wrote Assets/ShipData/renders/salvage_full.png` / `salvage_interior.png`; `RenderOnly complete` |
| 4 report | this file + attribution artifacts | see below |

### Coverage accounting vs the decoded source map

- **Tiles (180 non-space cells)**: Plating 106, FloorAsteroidTile 31, FloorGrayConcrete 20,
  FloorConcrete 14, Lattice 6, FloorOldConcrete 3. `Space` cells are intentionally empty (space).
- **Entities (133 from 29 prototypes)**: WallReinforced 48, Catwalk 26, WallReinforcedDiagonal 10,
  Grille 7, ReinforcedWindow 7, AirlockExternalGlassShuttleArrivals 6, Thruster 4, Chair 3,
  SolidReinforcedSecretDoor 3, DebugThruster 3, GrilleDiagonal 2, Gyroscope 2, ReinforcedWindowDiagonal 2,
  + single-instance machinery/props (consoles, canisters, closets, generator, SMES, substation, etc.).
- **Sprite layers 643** across **313 distinct labels** (max 5 repeats of one proto), each with
  absolute position, rotation and sorting order from the TSV.
- Pre-resolved in Python: RSI cells (direction order South 0, North 1, East 2, West 3, then SE/SW/NE/NW),
  `noRot` handling, connected wall/window/catwalk smooth states and corner states, 4-variant tile
  sheets, rotations and sorting layers. Unity side only slices textures and places renderers.
- Coordinates kept **absolute** (no re-centring), PPU 32, Grid cellSize 1×1, SS14 world ↔ Unity 1:1.

## Renders (genuine, from the saved scene)

- `Assets/ShipData/renders/salvage_full.png` — 720×1080 full-ship: hull plating, windowed cockpit with
  console + pilot seat, beige corridor, cross corridor with window/airlock modules, catwalk strip with
  hazard markings, airlocks, machinery row (generator/SMES/substation/canisters), three stern thrusters.
- `Assets/ShipData/renders/salvage_interior.png` — 900×720 closer interior: cockpit consoles, pilot chair,
  airlock doors, wall monitor + server rack, medical crate, chairs, cylindrical tanks, blast doors.
- Neither render shows magenta/missing-texture placeholders or white placeholder boxes.
- Renders were produced with an offscreen URP camera request; the **saved scene is byte-identical before
  and after the render pass** (`sha256 40fad51ba8b7a08a6c9f3f7b1a6c5263956a117a297ec2892c12c9e708d15eda`).
- In-scene world-space UI labels that pre-exist in HomeShip (e.g. "Repair supplies [E]") are visible in
  the interior render; they are existing project UI and were not added or modified.

## Preservation and safety

- **No commits, no push, no branch change, no reset/stash.** Untracked work left untracked.
- An editor was **not** running (`pgrep -i unity` = none, no `Temp/UnityLockfile`), so the bake ran
  against the live project rather than an isolated copy.
- `HomeShip.unity` backed up before the bake: `~/.hermes/profiles/coder/cache/scratch/HomeShip.unity.orig-before-bake` (267 866 B).
- No GameObjects deleted: the box *was* the two purely-visual Tilemaps `wall` (14×10) and `floor` (13×9)
  under `Grid`, which hold no colliders; their tiles were cleared. Player, camera, ShuttleConsole and
  Computer nodes are still present in the saved scene.
- **No colliders were added** and no gameplay systems (atmospherics, power, doors, docking, inventory)
  were added.
- The existing spawn still lands inside the interior (Player tile (4,-1) = FloorAsteroidTile), so opening
  the scene shows the ship; no spawn move was needed.
- Only tooling file touched outside `Assets/ShipData/renders`: `Assets/Editor/SalvageShuttleBake.cs`
  (untracked). Its `Render()` was changed to use `RenderPipeline.SubmitRenderRequest` with URP's
  `SingleCameraRequest`, because `Camera.Render()` is not supported under URP; a plain `cam.Render()`
  fallback remains for the built-in pipeline.

## Exclusions (documented, not invented)

- The source declares `entityCount 134`; **133 visual entities** were placed. The remaining entity is a
  non-visual/system entity with no icon (typical for map-internal markers) and is documented here as
  excluded rather than guessed at.
- Hidden container/closet inventory contents were not rendered as props (non-visual by nature).
- `Space` tiles are empty by design; no atmospherics overlays, wiring, or power nets are represented.

## Licenses and attribution

- `Assets/ShipData/SOURCE-ATTRIBUTIONS.md` — every imported sprite, its upstream file, license, copyright
  and the SHA-256 of the imported pixels. **72 table rows, 0 unresolved**: 66 × `CC-BY-SA-3.0`,
  2 × `CC-BY-SA-4.0` (Thruster, Gyroscope), 2 × **`CC-BY-NC-SA-3.0`** (concrete.png / grayconcrete.png —
  goonstation, reported as-is, not laundered).
- `Assets/ShipData/import-checksums.txt` — 73 source/import checksums.
- Upstream attribution files copied **alongside** the sprites (appended, not rewritten):
  17 × `source-meta.json` per RSI, plus `Tiles/source-attributions.yml` and
  `Tiles/Concrete/source-attributions.yml`.
- Source pixels preserved verbatim — nothing recoloured or repainted.
- Upstream: `/home/dan/Documents/SS1/frontier-station-14` @ `fc74013522ef2ee1c5049c986748c9f0866fb4c3`.

## Files

| Path | State |
|---|---|
| `Assets/Scenes/HomeShip.unity` | **modified** — layout placed, box tilemaps cleared |
| `Assets/ShipData/salvageshuttle.layout.tsv` | added — 643 placements (label, sprite, cell, x, y, rot, sortingOrder) |
| `Assets/ShipData/renders/salvage_full.png`, `salvage_interior.png` | added — renders |
| `Assets/ShipData/SOURCE-ATTRIBUTIONS.md`, `import-checksums.txt` | added — license/checksum trail |
| `Assets/Editor/SalvageShuttleBake.cs` | added (untracked) — slicer/placer/renderer tool |
| `Assets/Sprites/SS14/SalvageShuttle/**` | added — 75 files, per-RSI metas + attributions |
| `Assets/ShipData/salvageshuttle-recreation-spec.md` | existing spec (unchanged) |
