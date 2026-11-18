Frontier Station 14 proto-kinetic accelerator sprite attribution

Assets: pka-icon.png, pka-icon-unshaded.png, pka-projectile-chronobolt.png, inhand-left.png, inhand-right.png, wielded-inhand-left.png, and wielded-inhand-right.png
Original states: Resources/Textures/_NF/Objects/Weapons/Guns/Basic/kinetic_accelerator.rsi/icon.png, icon-unshaded.png, inhand-left.png, inhand-right.png, wielded-inhand-left.png, and wielded-inhand-right.png
Projectile state: Resources/Textures/Objects/Weapons/Guns/Projectiles/magic.rsi/chronobolt.png
Source repository: https://github.com/new-frontiers-14/frontier-station-14
Source metadata: source-meta.json and source-meta-projectile-magic.json (unchanged copies of each asset's upstream meta.json)
License: Creative Commons Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
https://creativecommons.org/licenses/by-sa/3.0/

Copyright and provenance are preserved exactly from the source asset metadata:
"Based on sprites by RiceMar1244 from https://github.com/space-wizards/space-station-14/pull/27526 which are based on tgstation at https://github.com/tgstation/tgstation/commit/8b7f8ba6a3327c7381967c550f185dffafd11a57 | inhand, wield, and belt equip sprites by RiceMar1244 | Modified by erhardsteinhauer (discord/github)"
Projectile copyright/provenance: "Tgstation at https://github.com/tgstation/tgstation/commit/906fb0682bab6a0975b45036001c54f021f58ae7"

Changes in NTExpedition: icon.png, icon-unshaded.png, and chronobolt.png were renamed for clear local use. The in-hand and wielded sheets retain their upstream names. Pixel content is unchanged. Unity import metadata and runtime presentation are project-specific adaptations. These files and adaptations remain available under CC BY-SA 3.0. These specific assets are ShareAlike but are not marked NonCommercial by their upstream metadata.

== PKA firing FX (muzzle flash + gunshot), added 2026-11-16 ==

Muzzle flash sprite
Asset: pka-muzzle-bullet.png
Original state: Resources/Textures/Objects/Weapons/Guns/Projectiles/projectiles.rsi/muzzle_bullet.png
Source repository: https://github.com/new-frontiers-14/frontier-station-14
Source revision: fc74013522ef2ee1c5049c986748c9f0866fb4c3
Source metadata: source-meta-muzzle-bullet.json (unchanged copy of the RSI's upstream meta.json)
License: Creative Commons Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
https://creativecommons.org/licenses/by-sa/3.0/

Copyright and provenance preserved exactly from the source asset metadata:
"Taken and modified from cev-eris at https://github.com/discordia-space/CEV-Eris/tree/7ff8f28ea4de734f3cc3cb70f2d4e4b4263a988d"

Upstream use: MuzzleFlashEffect in Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml (lines 2-19) - TimedDespawn lifetime 0.4 s, unshaded, BelowMobs draw depth, rotated to the fire angle, spawned at the gun on every shot (SharedGunSystem.MuzzleFlash -> MuzzleFlashEvent -> client GunSystem.CreateEffect).
Changes in NTExpedition: file renamed for clear local use; pixel content unchanged
(sha256 23089b784fb600ef035cff2612b3b23b24ef079981580b6fd351c227a1b72626). Unity import metadata and the runtime one-shot sprite presentation (spawned at the muzzle, rotated to the fire angle with the same Frontier-to-Unity angle conversion as the chronobolt, unlit, below mobs, 0.4 s fade) are project-specific adaptations.

Gunshot sound
Asset: pka-kinetic-accel.ogg
Original path: Resources/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg
Upstream use: Gun.soundGunshot - Resources/Prototypes/Entities/Objects/Weapons/Guns/Basic/base_pka.yml and Resources/Prototypes/_NF/Entities/Objects/Weapons/Guns/Tools/pka.yml
Source repository: https://github.com/new-frontiers-14/frontier-station-14
Source revision: fc74013522ef2ee1c5049c986748c9f0866fb4c3
Source metadata: source-meta-kinetic-accel.json (NTExpedition project record; upstream ships no per-file metadata for this sound - it is not listed in the directory's Resources/Audio/Weapons/Guns/Gunshots/attributions.yml and has no meta.json, so the repository-wide asset declaration applies)
License: Creative Commons Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
https://creativecommons.org/licenses/by-sa/3.0/
Repository-wide declaration (README, License section): "Most assets are licensed under CC-BY-SA 3.0 unless stated otherwise."
Changes in NTExpedition: file renamed for clear local use; audio content unchanged
(sha256 e4bbc2d65a8b9fc695e1256a797b3cfa58d66a80e325dddfbe97a8570c09aee1). Unity import settings and the runtime playback configuration (one-shot positional 3D source at the muzzle) are project-specific adaptations.

These files and adaptations remain available under CC BY-SA 3.0. Both additions are ShareAlike but are not marked NonCommercial by their upstream metadata or repository declarations.
