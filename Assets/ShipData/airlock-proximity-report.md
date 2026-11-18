# Proximity airlocks in HomeShip (interior passage doors only)

Task `t_daf91a57`. Project `/home/dan/Documents/gamedesignlab/NTExpedition`.
Report rewritten 2026-11-18 ~19:20 (+08); supersedes the 02:10 paused version and the
intermediate 19:02 version (which still described the superseded 1.5 m approach).

**Status: COMPLETE and verified in a real play-mode run on the final one-tile revision —
`result: PASS (0 failures)`, 96 PASS lines / 0 FAIL, exit code 0.** The two interior airlocks
start opening at **at most one tile (1.00 m)** from the doorway on approach from either side, hold
open while the player is in the doorway, close after the player leaves with hysteresis, and all
nine side/exterior/secret doors are untouched and still nonautomatic. Implementation is saved;
nothing was committed or pushed (per instructions).

---

## 1. What the doors do

`Assets/Scripts/World/ProximityAirlock.cs` (new, reusable, art-only):

- Opens when the nearest player sensor is within `openDistance` (**1.0 m = one tile**) **along the
  passage axis** and within the aperture half-width laterally. It never starts opening earlier than
  one tile from the doorway.
- Stays open while a sensor is inside the **hold** band: the same axis within `holdDistance`
  (2.25 m), laterally within aperture half-width + 0.35 m pad. Open 1.0 / hold 2.25 is the
  hysteresis that stops the door chattering.
- Once nothing is in the hold band it waits `closeDelay` (0.5 s) and only then closes.
- The panel only ever swaps between existing upstream sprite states (6 opening frames,
  6 closing frames, open sprite, closed sprite) with the upstream per-frame delays.
  Frame/jamb layers, sorting order, orientation and transforms are untouched.
- No collider is added by this component (`closedCollider` is prepared but left disabled); the
  ship has no hull colliders either, so passage is purely visual and the door never blocks the
  player.

### 1.1 Tile-to-world scale (measured, not assumed)

The task brief requires the opening trigger to be at most **one tile**. The saved scene's own
`Grid` component (`HomeShip.unity`, `m_CellSize: {x: 1, y: 1, z: 0}`) makes one tile exactly one
world unit, and the door geometry confirms it: `AirlockSalvage#128` sits at tile `(2.5, 1.5)` and
a scripted player at world `y = 2.50` measures `apertureDist = 1.00 m` in the run log — one tile
away, one metre away. So `openDistance = 1.0f` is exactly one tile.

## 2. Door coverage

Made automatic — exactly these two, both interior passage doors on the ship's vertical centreline:

| GameObject | Tile | Notes | Both sides exercised |
| --- | --- | --- | --- |
| `AirlockSalvage#128` | `(2.5, 1.5)` | passage between the cockpit and the rest of the ship | yes (phases 3, 5, 6 from +Y) |
| `AirlockEngineering#61` | `(2.5, -8.5)` | passage into engineering | yes (phases 1, 2 both directions, 6 from -Y) |

Both: `panel=closed_0`, 6 opening + 6 closing frames, open + closed terminal sprites, axis N/S,
`state=Closed`, `openDistance=1.0 m`, `holdDistance=2.25 m`, `closeDelay=0.5 s`, identity rotation
and identity ancestors (`lossyScale=(1.00,1.00)`, `zRotation=0.0`), so `passageAlongLocalY = true`
is correct. Both serialized values are `openDistance: 1` in the saved scene.

Left untouched — all nine non-automatic doors, verified unchanged in the saved scene *and* at the
end of the play-mode run:

- `AirlockExternalGlassShuttleArrivals#92 (-2.5,-3.5)`, `#93 (-2.5,-4.5)`, `#94 (-2.5,-5.5)`
- `AirlockExternalGlassShuttleArrivals#95 (7.5,-3.5)`, `#96 (7.5,-4.5)`, `#97 (7.5,-5.5)`
- `SolidReinforcedSecretDoor#62`, `#63`, `#64` along `y = -15.5`

The split is by geometry and topology, not by prototype name: the two automatic doors sit on the
centreline with identity-rotation parents, while the six hull doors are the side/exterior pairs at
`x = -2.5` and `x = 7.5` whose panels are rotated ±90°, and the secret doors are off-passage.
Wiring those with the N/S sensor law would be wrong, so they were not touched.

## 3. Runtime verification (the actual evidence)

Headless play mode on the saved scene: `Unity 6000.6.0f1 -batchmode -projectPath <proj>
-executeMethod ProximityAirlockVerify.RunRuntimeTest`, 420 s timeout and a 100 MB log ceiling.
The editor exits itself with the run's result code; this run (`generated: 2026-11-18 11:11:58Z`)
took **74.3 s of play time / 154,952 frames** (mean `deltaTime=0.00048 s` ≈ **2,086 fps**) and
returned **exit 0** (`airlock-runtime-report.code` = `0`).

`result: PASS (0 failures)` — **96 PASS lines, 0 FAIL lines**, 42 door-state transitions,
`Assets/ShipData/airlock-runtime-report.txt` (**38,934 B**, 367 lines).

**Approach threshold, measured in fixed 0.10 m steps** (71 samples, 2 settle frames each, in the
doorway's own measured distance):

- `PASS closed at every sample further than openDistance=1 m`
- `PASS never Closed at a sample inside openDistance=1 m`
- `PASS still closed at d=1.05 m (player y=-9.55), one step outside openDistance=1`
- `PASS already opening at d=0.95 m, one step inside openDistance=1`
- `PASS still Closed just beyond one tile: d=1.05 m (one tile + 0.05 m), player y=-9.55`
- `PASS opening at one tile: d=1.00 m (one tile=1.00 m), player y=-9.50`
- `PASS door tracked the live player at every step (worst |apertureDist - |snapped offset|| =
  0.0000 m over 71 samples)` — the law is applied in real world units, not a scaled local space.
- `PASS never began a close while the player was approaching`.

**Both-directions passage** (continuous walks at 1.6–2.4 u/s, phases 1–3): engineering
north→south and south→north, salvage cockpit→bridge — each `Closed -> Opening`, then `Open` with
the service-open sprite, and the player crossed with the door open.

**Hold / dwell:** standing inside the hold band at `d=1.63 m` for 1.5 s → `Open` throughout,
`0 Closing transitions`. Hovering across the hysteresis band → `0 Closing transitions`.

**Leaving:** from `d=3.10 m` (clear of the 2.25 m hold band) the door is still `Open` 0.25 s after
the player steps clear, begins closing once the doorway has been clear for longer than the 0.5 s
`closeDelay`, completes the close **0.36 s** after it begins, and then makes no further
transitions. Same result on both doors.

**No chatter:** rapid approach/retreat cycles each produce exactly
`Closed -> Opening -> Open -> Closing -> Closed`, with `no Closing->Opening re-open inside any
monotone retreat`.

**Regression:** at the end of the run `still exactly 2 automatic airlocks`, both classified as
interior passage doors, and each of the 9 hull-side airlocks reported untouched. 42 door state
transitions in total.

**Rendered evidence** (`Assets/ShipData/renders/airlock/`, captured by the harness *during* the
run from the live play-mode camera, not a static scene camera; contact sheet
`airlock_contact_sheet.png`): `00_start_closed`, `01_engineering_open`,
`02_engineering_open_from_south`, `03_salvage_closed`, `04_salvage_open`,
`05_salvage_after_chatter`, plus the final one-tile pairs
`06_salvage_closed_just_outside_one_tile` / `06_salvage_open_on_approach` and
`07_engineering_closed_just_outside_one_tile` / `07_engineering_open_on_approach`.
Each `*_closed*` panel shows the solid closed door sprite; each `*_open*` panel shows the open
doorway. These are appearance evidence — the interaction evidence is the transition log above.

## 4. Prerequisite change: ActorFlash null flood

Reproduced, then fixed in an earlier attempt because without it headless play mode was unusable:
`ActorFlash.PlayFlash()` threw `ArgumentNullException` every frame on a null
`MaterialPropertyBlock` (~524k lines / 528 MB in ~380 s). `Assets/Scripts/Actors/ActorFlash.cs`
now lazily initializes `weaponController` and `block` and returns early when there is still no
weapon controller. Flash behaviour is preserved; no combat refactor. The final run reported no
exception flood. This is a prerequisite fix for testing, not part of the requested feature.

## 5. Changed files

The one-tile revision (19:05 +08) and this reporting pass:

- `Assets/Scripts/World/ProximityAirlock.cs` — `openDistance` 1.5 → 1.0.
- `Assets/Editor/ProximityAirlockSetup.cs` — setup/default path also pins `openDistance = 1.0f`
  so the old value cannot return on a re-apply.
- `Assets/Scenes/HomeShip.unity` — re-saved by the setup run; both `ProximityAirlock` blocks carry
  `openDistance: 1`.
- `Assets/Editor/ProximityAirlockVerify.cs` — phase 6/7 rewritten for the one-tile threshold scan
  and the labelled evidence frames; exit-code file; abort reports never overwrite a real report.
- `Assets/ShipData/airlock-runtime-report.txt` — regenerated by the passing run (38,934 B).
- `Assets/ShipData/renders/airlock/*.png` — regenerated by the passing run (10 frames) +
  `airlock_contact_sheet.png`.
- `Assets/ShipData/airlock-proximity-report.md` — this report.

Carried over as-is from earlier attempts:

- `Assets/Scripts/Actors/ActorFlash.cs` — prerequisite null-init fix (01:46).
- `Assets/Editor/SalvageShuttleBake.cs` — earlier layout-task bake editor script.

No commits, pushes, branch changes, resets or stashes were made (`git log -1` is still
`e14456fb starter gun fx`). The evidence PNGs are screenshots, not ship art, and no scene
references them.

## 6. How the runtime test drives the player

The harness plays the scene in batch mode and drives the *scene's own* `PlayerMovement`
GameObject (1 `BoxCollider2D`, Dynamic rigidbody, `gravityScale=0`) by setting its transform
position and zeroing velocity each frame — effectively a scripted player doll, not synthetic door
state. It reads the door's public state, sprite name and measured distances, and logs every
transition with the player position and play-mode timestamp. It does not simulate input events,
and it teleports rather than walking the input pipeline.

## 7. Limitations / what is still manual

- **No human has walked it.** The evidence is a scripted doll at 0.1 m step resolution plus
  continuous 1.2–5 u/s walks; a real keyboard walk by Dan is not part of this run.
- The harness moves the player transform directly rather than through input, so
  input→movement integration is not exercised here (it is exercised by normal play).
- Single passing run of the final one-tile harness.
- The doors remain art-only: with no collider they never block, so "the door is closed" is a
  visual/state fact, not a physical barrier. Nothing was done to add hull colliders.
- Renders prove appearance, the transition log proves interaction; neither is a video clip.
- `airlock_contact_sheet.png` is assembled outside the project (ImageMagick) from the in-project
  renders.

## 8. Attribution

The opening/closing/open/closed sprites are the existing upstream `salvageshuttle` states already
imported for `t_282d2507`; no new art was imported for this task. Attribution for those assets is
recorded in `Assets/ShipData/SOURCE-ATTRIBUTIONS.md` and is unchanged.

## 9. State at the end of this run

- The passing run exited itself: `result: PASS (0 failures)`, editor exit code `0`, 74.3 s play
  time, runtime log 38,934 B. No Unity editor, compiler or play-mode process of this task is alive
  afterwards and `Temp/UnityLockfile` does not exist (only an idle Roslyn `VBCSCompiler` build
  server, which holds no project lock).
- `HomeShip.unity` mtime `19:05:23`, size `2,135,786` bytes, saved **out of Play mode** (it
  predates every play-mode run). It is readable: ASCII, no NUL bytes, and the two `ProximityAirlock`
  blocks carry their full field data including `openDistance: 1`.
- The player's serialized spawn is the authored `(4.14, -0.09)` (resolved from the scene YAML via
  the `PlayerMovement` component), not a test position; every test position used `x = 2.50`.
- All nine non-automatic doors are present in the scene and were re-checked at the end of the run.
