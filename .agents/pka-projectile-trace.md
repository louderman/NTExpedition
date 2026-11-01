# Frontier Station PKA projectile — asset, state, orientation, pixel settings, license

Task: kanban t_201a65fc (feeds t_78ea5118 implementation and t_f7c290d6 verification).
Author: coder profile. No project files were modified, staged, committed or pushed by this trace.
Local Frontier source inspected read-only: `/home/dan/Documents/SS1/frontier-station-14`
Tested/reported source revision: HEAD `fc74013522ef2ee1c5049c986748c9f0866fb4c3` ("Automatic Changelog (#4804)", Sun Oct 4 22:02:40 2026 +0000).
`git status` confirms every file cited below is unmodified in that clone (the only dirty paths are unrelated ammunition-sequencer work).
Remotes: origin `https://github.com/louderman/frontier-station-14.git`, upstream `https://github.com/new-frontiers-14/frontier-station-14.git`.

## 1. Verdict

The proto-kinetic accelerator that exists in this Frontier build is `NFWeaponPka`. It fires
`NFBulletKineticMediumPower`, whose resolved sprite is a single static frame of
`Objects/Weapons/Guns/Projectiles/magic.rsi` state **`chronobolt`** — a 32x32 px PNG whose visible
art is 5x29 px. It is **1 direction, 1 frame, no animation**, rotated as a whole to match travel
direction. License is **CC BY-SA 3.0** (ShareAlike, no NonCommercial clause), copyright recorded as a
tgstation commit URL. There is no blocker: the asset, state and license are all established with
direct evidence. NTExpedition's already-imported copy is **byte-identical** to upstream.

## 2. Exact prototype chain (file:line, all in the local clone)

1. `Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Tools/pka.yml:143-158` — `NFWeaponPka`
   `parent: [ NFBaseWeaponFramePkaNanotrasen, NFBaseWeaponPkaChamberKineticMedium ]`.
   This is the live player-facing PKA on Frontier: every spawner/lathe/loadout reference goes to
   `NFWeaponPka` (`Catalog/VendingMachines/Inventories/salvage.yml:12`, `_NF/Recipes/Lathes/weapons.yml:183-184`,
   `_NF/Recipes/Lathes/Packs/{science,salvage,blueprints}.yml`, `_NF/Loadouts/Jobs/Contractor/hands_items.yml:197`,
   `_NF/Entities/Markers/Spawners/Random/Items/supply_tables.yml:106`, `_NF/Catalog/Fills/NPCsLoot/npc_loot_explorers_packs.yml:70,129,200,251`).
   The WizDen `WeaponProtoKineticAccelerator` (`Resources/Prototypes/Entities/Objects/Weapons/Guns/Basic/pka.yml:1-23`)
   is `categories: [ HideSpawnMenu ]` on Frontier and its lathe recipe is commented out
   (`Recipes/Lathes/devices.yml:198-199`), so it is not the in-game PKA here.
2. `.../Tools/pka.yml:47-60` `NFBaseWeaponFramePkaNanotrasen` → `:2-43` `NFBaseWeaponFramePka`
   (`Gun` projectileSpeed 22.5, fireRate 0.5, SemiAuto, minAngle 32.7 / maxAngle 43.5; weapon sprite
   `_NF/Objects/Weapons/Guns/Basic/kinetic_accelerator.rsi`, states icon / icon-unshaded / animation-icon).
3. `.../Tools/pka.yml:93-108` `NFBaseWeaponPkaChamberKineticMedium`
   → `BasicEntityAmmoProvider proto: NFBulletKineticMediumPower, capacity: 1, count: 1`,
   `RechargeBasicEntityAmmo rechargeCooldown: 0.85`, gunshot `/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg`.
4. `Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Ammunition/Projectiles/energy.yml:141-152`
   `NFBulletKineticMediumPower` (`parent: NFBulletKineticLowPower`) — overrides **only** `Projectile`
   damage: `Blunt: 16.25`, `Structural: 30`, `impactEffect: BulletImpactEffectKinetic`. It does **not**
   touch the sprite.
5. `.../Ammunition/Projectiles/energy.yml:117-139` `NFBulletKineticLowPower` (`parent: BulletKinetic`) — adds
   `Ammo.muzzleFlash: HitscanEffect`, `TimedDespawn lifetime: 1.5`, `PointLight radius 2.5, color white, energy 0.5`,
   `GatheringProjectile`, `MiningGatheringSoft`.
6. `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml:514-538` `BulletKinetic` ("kinetic bolt")
   — this is where the sprite is defined:
   ```yaml
   - type: Sprite
     noRot: false                       # sprite follows entity rotation (it is NOT frozen)
     sprite: Objects/Weapons/Guns/Projectiles/magic.rsi
     layers:
     - state: chronobolt
       shader: unshaded
   ```
   plus `Projectile` (impactEffect `BulletImpactEffectKinetic`, Blunt 15 / Structural 30),
   `TimedDespawn lifetime: 0.22`, `GatheringProjectile`, `Reflective: [NonEnergy]`.
   (Inherited from `BaseBullet`, `projectiles.yml:47-96`: `Physics` Dynamic, projectile fixture AABB
   ±0.1 with mask Impassable/BulletImpassable, `FlyBySound`, `Clickable`.)
7. `Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Ammunition/Projectiles/projectiles.yml:257-262`
   `NFBulletKinetic` (`parent: BulletKinetic`) adds `MiningGatheringSoft` only — sprite unchanged.

**The only variant that changes the projectile sprite is `NFBulletKineticHighPower`**
(`energy.yml:154-163`: `state: chronobolt, shader: unshaded, color: "#ffffff", scale: 1.2,1.2`), used by
`NFWeaponPkaCannon` / `NFBaseWeaponPkaChamberKineticHigh` — *not* by `NFWeaponPka`. The shuttles'
`NFBulletKineticShuttle` recolours the same chronobolt art `#FF8888` (`_NF/.../projectiles.yml:264-280`).

## 3. The exact asset

| Fact | Value |
| --- | --- |
| Upstream file | `Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/chronobolt.png` |
| Metadata | `Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/meta.json` |
| RSI canvas | 32 x 32 px (meta `size: {x: 32, y: 32}`, `version: 1`) |
| State name | `chronobolt` (meta.json:107-109 — entry is exactly `{"name": "chronobolt"}`, no `delays`, no `directions`) |
| Directions | 1 (absent `directions` key ⇒ Dir1) |
| Frames | 1 static frame (no `delays` ⇒ no animation sequence at all) |
| Visible art | alpha bbox (14,2)-(19,31) ⇒ **5 x 29 px**, vertically oriented |
| PNG format | PNG 32x32, 8-bit colormap, non-interlaced, 263 bytes, `md5 475a90a2725779b8c57876ab95abdce8` |
| Art shape | single-pixel tip at the bottom row (y=2), widening 1→3→3→3→3→5 px, an interior fork at y=14-16 (`#.#.#`), then a longer tapered tail up to single pixels at y=29-30. The bolt's point is at the **bottom of the canvas** (south-facing), the plumed tail at the top. |
| Impact FX (same chain) | `impacts.yml:57-72` `BulletImpactEffectKinetic`: `Effects/arcs.rsi`, state `punch`, `shader: unshaded`, `drawdepth: Effects`, lifetime 0.2 |
| Muzzle flash (same chain) | `Ammo.muzzleFlash: HitscanEffect` (`hitscan.yml:1-17`) — its sprite layers carry only a shader + `map`, no sprite/state, so it contributes no visible sprite art |

No other asset matches: `grep -rn chronobolt` over the whole resource tree returns exactly
`projectiles.yml:527,549,581`, `_NF/.../energy.yml:161` (all one-directional, single-frame, same RSI) and
the state definition itself.

## 4. Orientation / directional behaviour (evidence, not inference)

* Upstream stores **no** directional frames — rotation is done by rotating the whole sprite.
* `BulletKinetic` sets `noRot: false` ⇒ the renderer rotates the sprite with the entity.
* On fire, the projectile's rotation is set to the shot vector's angle:
  `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs:443`
  `TransformSystem.SetWorldRotation(uid, direction.ToWorldAngle() + projectile.Angle);`
  with `Content.Shared/Projectiles/ProjectileComponent.cs:16` `public Angle Angle;` defaulting to zero, so
  rotation == travel-direction angle.
* For a Dir1 RSI state the engine adds **no** direction matrix:
  `RobustToolbox/Robust.Client/GameObjects/Components/Renderable/SpriteComponent.cs:1631-1655`
  (`GetLayerDrawMatrix`: `dir == RsiDirection.South || noRot ⇒ LocalMatrix`; the matrix table maps South
  and East to identity). So the art is drawn exactly as authored, rotated by the travel angle.
* Consequence for a port: because the tipped end faces down/south in the canvas, an engine that uses
  "angle of travel + Z rotation" must add **+90°** to point the bolt along the direction of travel.
  NTExpedition already does exactly this:
  `Assets/Scripts/Combat/KineticProjectile.cs:19-23` (`… * Mathf.Rad2Deg + 90f`, comment: "Frontier's
  chronobolt artwork points down in its source frame, so +90 degrees maps it to +X"), and the self-test
  asserts the resulting -90° for a leftward shot (`ProtoKineticAcceleratorSpawner.cs:112-115`).

## 5. Pixel settings and scale

Upstream (SS14/RSI): 1 tile = 32 px, and the sprite is a full 32x32 canvas ⇒ the entity occupies one tile
of canvas, while the drawn bolt itself covers only 5 x 29 px (≈0.16 x 0.91 tile). No `scale` is applied to
the projectile that `NFWeaponPka` uses (the 1.2x scale belongs to the unused HighPower variant).

NTExpedition's imported copy (`Assets/ThirdParty/FrontierStation14/ProtoKineticAccelerator/`):

* `pka-projectile-chronobolt.png` — `md5 475a90a2725779b8c57876ab95abdce8`, **identical to the upstream
  file** (verified with `md5sum` on both). Pixel content is unmodified; only the filename differs.
* `pka-projectile-chronobolt.png.meta` — guid `10c712af83be545dbb89f2719d5d9bbb`;
  `spriteMode: 1` (single sprite), `spritePixelsToUnits: 32` (matches the project's RSI convention: 32 px =
  1 world unit), `spritePivot {0.5, 0.5}` / `alignment: 0` (centre), `filterMode: 0` (**Point**),
  `mipmaps.enableMipMap: 0`, `sRGBTexture: 1`, `alphaIsTransparency: 1`, mipmaps/streaming off;
  sprite rect `x: 13, y: 0, width: 7, height: 31` (internal id `771053076569974968`,
  `pka-projectile-chronobolt_0`); platform entries: DefaultTexturePlatform uncompressed
  (`textureCompression: 0`), **Standalone and WebGL `textureCompression: 1` (Normal Quality)**.
* Resulting world size at 32 PPU: sprite rect 7x31 px ⇒ 0.219 x 0.969 world units; the visible artwork
  5x29 px ⇒ 0.156 x 0.906 units. Pivot sits at the rect centre (canvas px 16.5, 15.5) ≈ canvas centre, so
  rotation stays centre-origin, matching SS14.

Two cosmetic items worth a look during verification (**not** blockers, do not justify re-importing):
1. The rect (13..19 x 0..30) is 1 px wider than the alpha bbox on each side horizontally but sits 2 px
   above it vertically (true bbox rows 2..30). Sub-pixel; it does not change orientation or scale class.
2. The Standalone/WebGL platform entries keep Unity's default "Normal Quality" compression, whereas the
   project's own RSI import path (`Assets/Editor/RsiPreview.cs:152`) forces Uncompressed. If the bolt looks
   soft in a built player, that meta field is the cause — check it in the build rather than guessing.

## 6. License and attribution (exact, as recorded upstream)

`Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/meta.json:1-4`:

```json
{
  "version": 1,
  "license": "CC-BY-SA-3.0",
  "copyright": "Tgstation at https://github.com/tgstation/tgstation/commit/906fb0682bab6a0975b45036001c54f021f58ae7",
  "size": { "x": 32, "y": 32 },
```

* **License: Creative Commons Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)** —
  https://creativecommons.org/licenses/by-sa/3.0/. There is **no NonCommercial clause** in this asset's
  metadata (the earlier CC-BY-NC-SA authorisation for the Frontier baseball bat does not describe this
  file; it neither helps nor hurts — this asset must be handled under BY-SA).
* Obligations that follow from BY-SA 3.0: keep attribution to the recorded copyright holder, keep the
  licence name/link with the asset, indicate that changes were made, and license adaptations under the same
  licence (ShareAlike). Attribution must not imply endorsement.
* Provenance chain recorded: the copyright is a **tgstation commit URL** (`906fb0682bab…`), the same
  commit string used by several other tgstation-derived Frontier assets
  (`Resources/Textures/Objects/Magic/magicactions.rsi/meta.json:4`, `Resources/Audio/Magic/attributions.yml:9`,
  `Resources/Audio/Effects/attributions.yml:14`). The chronobolt PNG itself entered this repository in
  commit `ad7889e8a95b8cd09e25f0d0af97be3206097d96` "Magic staves + wands (#9070)" by
  `Kara <lunarautomaton6@gmail.com>`, Thu Jul 14 19:45:27 2022 -0700 (`git log -- resources/.../chronobolt.png`).
* **Where the licence lives:** for this asset, *only* in that `meta.json`. There is **no** `attributions.yml`
  anywhere under `Resources/Textures/Objects/**` (the repo's `attributions.yml` files exist only under
  `LobbyScreens/`, `Parallaxes/`, `Tiles/` and the `Audio/**` trees) — `grep` for `magic` across all
  `attributions.yml` returns nothing for this texture.
* Repo-level rule (`LEGAL.md`, "Copyright" section): "Assets have distinct licenses in attributions.yml and
  meta.json files which must be followed individually." Code is AGPLv3 (`LICENSE-AGPLv3.txt`,
  `COPYRIGHT-AGPLv3.txt`) with MIT for WizDen-derived code (`LICENSE-MIT.txt`) — so the **AGPL applies to
  the YAML/C# we copy, and BY-SA 3.0 to this sprite**.
* Related FX asset `Resources/Textures/Effects/arcs.rsi/meta.json` (used by `BulletImpactEffectKinetic`):
  `"license": "CC-BY-SA-3.0"`, `"copyright": "Taken from https://github.com/tgstation/tgstation/raw/c545428822f1ee0d402b812221518632dbe198cb/icons/effects/effects.dmi with offsets modified"`.

## 7. Current NTExpedition state (already done — do not redo)

* Imported asset present and byte-identical to upstream (see §5), with attribution material already
  written: `Assets/ThirdParty/FrontierStation14/ProtoKineticAccelerator/NOTICE.md`,
  `source-meta.json` (verbatim copy of the weapon RSI meta) and `source-meta-projectile-magic.json`
  (verbatim copy of `magic.rsi/meta.json`). NOTICE.md names the projectile source path and states
  CC BY-SA 3.0 and that the file is ShareAlike but not NonCommercial — both statements match the upstream
  metadata read above.
* Wired to gameplay: `Assets/Resources/WorldItems/ProtoKineticAccelerator.prefab:175`
  `projectileSprite: {guid: 10c712af83be545dbb89f2719d5d9bbb}` (the chronobolt sprite);
  `glowSprite` is `pka-icon-unshaded.png`, the held visual is `pka-icon.png`
  (`Assets/Scripts/Combat/RangedWeapon.cs:9-19`, `KineticProjectile.cs:19-27`, sorting order 18).
* Runtime assertions already exist in `Assets/Scripts/Combat/ProtoKineticAcceleratorSpawner.cs:90-94`
  (texture 32x32, pixelsPerUnit 32, filter Point) and `:112-115` (bolt renderer uses that sprite and is
  rotated to the shot direction). Whether those assertions pass in a fresh build belongs to t_78ea5118 /
  t_f7c290d6 — this trace did not run Unity.

## 8. Non-authorisations / open questions

* Upstream game timings (0.22 s `BulletKinetic` lifetime, 1.5 s NF medium-power lifetime, 22.5 projectile
  speed, 0.85 s recharge, Blunt 16.25) are **not** a licence to change NTExpedition's tuned gameplay
  (5 damage, 0.65 s cooldown, 8 range, 12 speed, infinite ammo). Card t_64c3ad22 requires those preserved.
* Nothing here blocks implementation. The two cosmetic meta items in §5 are the only things worth a
  verification glance; both are scale-neutral and orientation-neutral.
* Ambiguity that would have mattered — "which PKA" and "which projectile state" — is resolved: every
  Frontier PKA path that a player can obtain ends at the same one-directional, single-frame
  `magic.rsi/chronobolt` asset; only the cannon/sawn/shuttle variants differ, and none of those is
  `NFWeaponPka`.

## 9. Commands used (reproducible)

```
# chain
grep -rn "NFBulletKinetic\|BulletKinetic" Resources/Prototypes/
sed -n '505,565p' Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml
sed -n '110,165p' Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Ammunition/Projectiles/energy.yml
sed -n '136,193p' Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Tools/pka.yml
# asset + licence
cat Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/meta.json
md5sum Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/chronobolt.png
git log --oneline -- Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/chronobolt.png
grep -rn "906fb0682bab6a0975b45036001c54f021f58ae7" --include=*.json --include=*.yml --include=*.md .
# engine behaviour
grep -n "ShootProjectile" -A 18 Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs
sed -n '1622,1660p' RobustToolbox/Robust.Client/GameObjects/Components/Renderable/SpriteComponent.cs
# identity check against the imported copy
md5sum /home/dan/Documents/gamedesignlab/NTExpedition/Assets/ThirdParty/FrontierStation14/ProtoKineticAccelerator/pka-projectile-chronobolt.png
python3 /tmp/pka_trace/ascii_mask.py <chronobolt.png>     # alpha mask + per-row widths (tip direction)
```
