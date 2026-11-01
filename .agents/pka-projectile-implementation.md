# PKA projectile visual — implementation state

Task: kanban `t_78ea5118` (implementation phase; verification + Telegram delivery are card `t_f7c290d6`).
Author: coder profile. Nothing committed or pushed. No desktop GUI was used.
Upstream reference read read-only: `/home/dan/Documents/SS1/frontier-station-14` (HEAD `fc74013522ef2ee1c5049c986748c9f0866fb4c3`).

## 1. Verdict

NTExpedition's PKA already renders the exact Frontier Station projectile art, and this card verified it
against upstream and locked it down. The sprite is `Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi`
state `chronobolt` (32x32 canvas, 1 direction, 1 static frame), imported byte-identically at 32 PPU with
Point filtering and no mipmaps, wired through the real `RangedWeapon.ProjectileSprite` → `KineticProjectile`
path, and rotated onto the shot direction with the same convention Frontier uses. Licence CC BY-SA 3.0 is
recorded with the asset. No blocker.

## 2. What is in place (each line re-verified this session, not copied from the trace)

| Item | Evidence |
| --- | --- |
| Imported asset | `Assets/ThirdParty/FrontierStation14/ProtoKineticAccelerator/pka-projectile-chronobolt.png`, `md5sum` `475a90a2725779b8c57876ab95abdce8` — identical to the upstream `chronobolt.png` (`md5sum` run on both files this session). Only the filename differs. |
| Import settings | `pka-projectile-chronobolt.png.meta`: `spriteMode: 1`, `spritePixelsToUnits: 32`, `filterMode: 0` (Point), `enableMipMap: 0`, `alphaIsTransparency: 1`, `alignment: 0` (centre), sprite rect `x 13 y 0 w 7 h 31`, `textureCompression: 0` on the default platform |
| Prefab wiring | `Assets/Resources/WorldItems/ProtoKineticAccelerator.prefab:175` `projectileSprite: {guid: 10c712af83be545dbb89f2719d5d9bbb}`, that guid is `pka-projectile-chronobolt.png.meta` (`grep` on the meta) |
| Editor generator | `Assets/Editor/InventorySandboxSetup.cs:160,179,207` loads the same `pka-projectile-chronobolt.png` path into `projectileSprite`, so a regeneration of the prefab keeps the exact art |
| Runtime render | `Assets/Scripts/Combat/KineticProjectile.cs:19-27` — one `SpriteRenderer`, no scale override, `sortingOrder 18`, sprite assigned from `RangedWeapon.ProjectileSprite` |
| Static frame / direction | upstream `magic.rsi/meta.json:107-109`: the state entry is exactly `{"name": "chronobolt"}` — no `directions`, no `delays` → 1 direction, no animation. NTExpedition matches with a single static frame. |
| Scale | art is 5x29 px; at 32 PPU that is 0.156 x 0.906 world units — identical to Frontier, where 32 px = 1 tile |
| Licence / notices | upstream `magic.rsi/meta.json:1-4` (`CC-BY-SA-3.0`, copyright = tgstation commit `906fb0682bab6a0975b45036001c54f021f58ae7`); local `NOTICE.md` names the projectile source path, licence and ShareAlike/no-NonCommercial status; `source-meta-projectile-magic.json` is byte-identical to the upstream `meta.json` (`diff` run this session) |

## 3. Orientation — derived from engine source, with citations

The +90° in `KineticProjectile.Launch` is a convention conversion, not a guess about the artwork:

1. Frontier sets the projectile's rotation to the direction's world angle:
   `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs:443`
   `TransformSystem.SetWorldRotation(uid, direction.ToWorldAngle() + projectile.Angle);`
   with `Content.Shared/Projectiles/ProjectileComponent.cs:16` `public Angle Angle;` defaulting to zero.
2. Robust's world angle is **south-based**: `RobustToolbox/Robust.Shared.Maths/Angle.cs:45-48`
   `FromWorldVec(dir) => new Angle(dir) + Math.PI / 2`, and `Angle.cs:50-57,66-69` documents
   `ToWorldVec()` for "in-world calculations where an angle of zero is usually considered 'south' (0,-1)".
   Unity's `Atan2(y, x)` is east-based, hence the +90° offset.
3. The one-direction chronobolt state resolves to the South RSI row, and South is drawn with **no** extra
   direction matrix: `RobustToolbox/Robust.Client/GameObjects/Components/Renderable/SpriteComponent.cs:1631-1655`
   (`dir == RsiDirection.South || noRot ⇒ LocalMatrix`) — so the authored art is drawn as-is, rotated by the
   entity's rotation. `BulletKinetic` sets `noRot: false`
   (`Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml:514-538`).
4. Cross-checks of the "art is authored facing south" convention: SS14's other 1-direction projectile layers
   are authored the same way — `arrows.rsi/tip.png` puts the arrowhead point on the canvas bottom row and
   `rod.png` is a vertical shaft (both decoded pixel-by-pixel this session).
5. Net effect: rotation `Atan2(travel) + 90°` in Unity == Frontier's `direction.ToWorldAngle()`, so the bolt's
   point leads the shot and the forked plume trails, exactly as upstream.

## 4. Changes made by this card (2, both inside files created by card `t_7d2d6785`)

1. `Assets/Scripts/Combat/KineticProjectile.cs` — comment only. The old comment justified +90° by "the artwork
   points down"; replaced it with the actual verified reason (§3) so the offset cannot be "fixed" away by a
   later reader. Behaviour, numbers and structure are untouched.
2. `Assets/Scripts/Combat/ProtoKineticAcceleratorSpawner.cs` — the existing runtime assertion now also pins
   asset identity: the projectile texture name must contain `chronobolt` and `mipmapCount == 1` (plus the
   previous 32x32 / 32 PPU / Point checks). This makes the executable test fail if a guessed or substituted
   projectile visual is ever wired in. No gameplay assertions were weakened; every other check is unchanged.

## 5. Compile check (run this session)

`dotnet build Assembly-CSharp.csproj` → **0 errors**, and no warnings attributed to either edited file
(68 warnings total, all pre-existing `CS0649` unassigned-serialized-field warnings elsewhere in the project).
`git status --porcelain` shows the same working-tree scope as before this card: the 4 pre-existing modified
files plus the untracked PKA/asset paths; no new files were introduced by the build (Unity/dotnet output goes
to `Temp/`).

## 6. Deliberate non-changes (with evidence, so the verifier does not re-open them)

1. **Sprite rect is the art plus a 1 px margin, not the full 32x32 canvas.** Pivot therefore sits 0.5 px off the
   canvas centre (0.0156 world units); the rendered art size, scale and orientation are unaffected because the
   art pixels are 1:1 inside the sprite. The same convention is used by the already-shipped
   `pka-icon.png.meta` (rect `0,8,32,16`). Sub-pixel, orientation-neutral — deliberately not re-imported.
2. **Standalone/WebGL platform entries show `textureCompression: 1`.** They are `overridden: 0`, so they do not
   override the default (Uncompressed) platform setting, and `pka-icon.png.meta` shows the identical pattern
   from Unity itself. Not a fidelity risk.
3. **Frontier's `shader: unshaded` needs no port work here.** The projectile layer carries only a shader flag,
   not extra art, and `Assets/Scenes/HomeShip.unity` contains exactly one Light2D (`m_LightType: 4` = global,
   white, intensity 1), so lit and unlit sprites are indistinguishable in this scene. No material or shader
   asset was invented.

## 7. Evidence already on disk (from the earlier crashed run on this same working tree)

`/home/dan/Documents/gamedesignlab/NTExpedition-evidence/t_64c3ad22/` (written 12:10-12:15, after the 12:09-12:10
sprite import): `build.log`, `pka-runtime.log` ending in `PKA_COMBAT_PASS` with
`PASS: PKA uses Frontier's exact 32px chronobolt with point filtering at native scale`,
`PASS: primary fire launches one visible bolt accurately toward cursor`, plus
`bat-regression.log` and `inventory-regression.log`, and `screenshots/pka-firing.png`. In that screenshot the
bolt is horizontal with its point leading the leftward shot and its forked plume behind, i.e. the exact
chronobolt art with correct orientation.

Those results predate the comment/assertion edits above, so card `t_f7c290d6` still owes a fresh isolated
build, a fresh `--pka-self-test` run (the assertion text it will log changed) and the Telegram delivery.

## 8. Explicitly out of scope

Frontier's secondary projectile visuals — `BulletImpactEffectKinetic` (`Effects/arcs.rsi` state `punch`) and the
`PointLight` from `NFBulletKineticLowPower` — were not added: this card's remit is the projectile visual, and
those are separate assets with their own licence/notice obligations.
