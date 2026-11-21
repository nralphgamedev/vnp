# Placeholder assets

Every visual and audio asset in the prototype is temporary. This file lists all of them, so each can be found and replaced.

Most files here are made by `code/PlaceholderBaker.cs`, which holds their shapes, sizes and colours. To change one of those, edit the baker and bake again rather than editing the file. To bake, open `code/Bake.tscn` in Godot and press F6 (Run Current Scene). Final art can simply overwrite a file, as long as it keeps the same path and type. After that, stop baking that file, or remove it from the baker.

No asset was downloaded, so there are no licences to track.

## Meshes (`meshes/`)

All made by `code/PlaceholderBaker.cs`.

| File | What it is | Used by |
|---|---|---|
| `drone.tres` | Small kite-shaped slab | Drones (`DroneSwarm.cs`) |
| `enemy.tres` | Pincer-shaped slab | The basic enemy (`BasicEnemy.cs`) |
| `goo.tres` | Smooth, lumpy blob, radius about 1, smoothly shaded. Made by `PlaceholderShapes.Blob`. `Goo.cs` flattens it, wobbles it, swells it on absorbing a drone, and stretches it before a split. | The Goo (`Goo.cs`) |
| `orbital_shard.tres` | Long thin diamond | Orbital Shards (`OrbitalShards.cs`) |
| `debris.tres` | Broken plate of hull with torn edges, 0.5 thick. It tumbles, so its farthest point (about 1) is also in `Harvestables.DebrisExtent`. | Enemy debris (`Harvestables.cs`) |
| `projectile.tres` | 0.4 × 0.4 × 1.3 box | Drone, enemy and Scatter Shot projectiles (`Projectiles.cs`) |
| `unit_box.tres` | 1 × 1 × 1 box, stretched into beams | Pulse Laser beam, VNP harvest beam, drone harvest and attack beams, and effect sparks |
| `fragment.tres` | Small jagged triangle slab | Pieces of destroyed enemies and used-up material (`Effects.cs`) |
| `missile.tres` | Small dart with fins at the back | Seeker Missiles (`SeekerMissiles.cs`) |
| `mine.tres` | Squat disc with eight short spikes | Mine Layer (`MineLayer.cs`) |
| `shockwave_ring.tres` | Thin flat ring, outer radius 1 | Shockwave (`Shockwave.cs`) |
| `ring.tres` | Flat ring, outer radius 1 | Spreading rings in effects: deaths, hits, new drones, pops (`Effects.cs`) |
| `ground_sheet.tres` | 800 × 800 plane | Ground grid (`GroundGrid.cs`) |
| `vnp_hull.tres`, `vnp_pod.tres`, `vnp_core.tres` | Prism, box and sphere | VNP model |
| `gateway_ring.tres`, `gateway_disc.tres`, `gateway_pylon.tres`, `gateway_inner_ring.tres`, `gateway_marker.tres` | Tori, a disc and boxes, made for a gateway radius of 6 | Gateway model |

## Materials (`materials/`)

All made by `code/PlaceholderBaker.cs`, except where noted.

| File | What it is | Used by |
|---|---|---|
| `instanced_lit.tres` | Lit, takes each copy's colour | Drones, the basic enemy, asteroids, debris |
| `goo.tres` | Lit, glossy, with a soft rim of light at the edges and a faint glow; takes each copy's colour | The Goo (`Goo.cs`), so it reads as wet slime rather than rock |
| `instanced_unshaded.tres` | Unlit, takes each copy's colour | Projectiles, Orbital Shards, drone beams |
| `instanced_glow.tres` | Unlit and additive: each copy's colour adds to what is behind it, so fading to black fades out | Effect sparks, rings, trails and harvest particles (`Effects.cs`) |
| `vnp_hull.tres`, `vnp_core.tres` | Solid colours with a little glow | VNP model. `Vnp.cs` copies the hull material for the hit flash. |
| `gateway_solid.tres`, `gateway_faint.tres` | Gateway colour, solid and faint | Gateway model |
| `pulse_laser_beam.tres` | Unlit, see-through | Pulse Laser beam. The colour comes from `weapons/pulse_laser/pulse_laser.tres`. |
| `harvest_beam.tres` | Unlit | VNP harvest beam. The colour comes from `ExperienceColor` in `tuning/look.tres`. |
| `ground_grid.tres` | Grid shader with its colours and line spacing | Ground grid |

## Shaders (`shaders/`)

| File | What it is | Used by |
|---|---|---|
| `ground_grid.gdshader` | Grid lines fixed in the world. Written by hand. | `materials/ground_grid.tres` |

## Models (`models/`)

Scenes that put meshes and materials together. Both are made by `code/PlaceholderBaker.cs`.

| File | What it is | Used by |
|---|---|---|
| `vnp.tscn` | Arrowhead hull, two side pods and a glowing core | `Vnp.cs`, scaled by `Size` in `tuning/vnp.tres` |
| `gateway.tscn` | Outer ring, faint disc, four pylons, and a turning inner ring with three markers. `Gateway.cs` turns the node named `InnerRing`. | `Gateway.cs`, scaled to `GatewayRadius` in `tuning/fight.tres` |

## Environments (`environments/`)

| File | What it is | Used by |
|---|---|---|
| `space.tres` | Near-black background and soft blue ambient light. Written by hand. | `scenes/Fight.tscn` |

## Textures (`textures/`)

| File | What it is | Used by |
|---|---|---|
| `icon.svg` | Godot's default project icon | Project settings (window and file icon) |
| `icons/pulse_laser.svg`, `icons/orbital_shards.svg`, `icons/scatter_shot.svg`, `icons/seeker_missiles.svg`, `icons/shockwave.svg`, `icons/mine_layer.svg`, `icons/projectile.svg`, `icons/beam.svg`, `icons/ram.svg` | White weapon icons, tinted with the weapon's colour. Drawn by hand. Each is named after its weapon's ID, so a new weapon's icon is `icons/<id>.svg`; a weapon without one shows its first letter. | HUD weapon slots and Drone Weapon box (`WeaponSlots.cs`) |

## Audio (`audio/`)

All made by `code/PlaceholderSounds.cs`, which `code/PlaceholderBaker.cs` runs. Each sound is synthesised from simple tones and noise, and saved as 16-bit mono .wav at 44.1 kHz. To change one, change its recipe in `PlaceholderSounds.cs` and bake again.

Every sound is listed in `audio/audio.tres`, which sets its category, volume, copy limit, pitch variation, and whether it loops. A final sound can replace a file directly, as long as it keeps the same name.

| File | What it sounds like | Played when |
|---|---|---|
| `pulse_laser_fire.wav` | Short falling zap | The Pulse Laser fires |
| `shard_hit.wav` | Bright tink | An Orbital Shard hits an enemy |
| `drone_projectile_fire.wav` | Tiny blip | A drone fires a projectile |
| `drone_beam_tick.wav` | Soft buzz | A drone's Beam deals a tick of damage |
| `scatter_shot_fire.wav` | Short shotgun blast | Scatter Shot fires |
| `seeker_launch.wav` | Rising whoosh | Seeker Missiles launch a volley |
| `seeker_hit.wav` | Small explosion | A seeker missile hits |
| `shockwave_fire.wav` | Low boom with a rush of air | A Shockwave ring goes out |
| `mine_drop.wav` | Two soft rising clicks | A mine is dropped |
| `mine_explode.wav` | Bigger explosion | A mine goes off |
| `ram_hit.wav` | Short metallic clank | A ramming drone hits an enemy |
| `shard_block.wav` | Bright glassy tink | An Orbital Shard destroys an enemy shot |
| `goo_absorb.wav` | Wet gulp: a dropping low tone with a muffled slurp | A Goo absorbs a drone |
| `goo_split.wav` | A rising stretch, then a soft pop | A Goo splits in two |
| `goo_death.wav` | Heavy splat: a deep thud and low, wet noise | A Goo is destroyed |
| `enemy_fire.wav` | Low pew | An enemy fires |
| `hit.wav` | Thud with a click | An enemy or drone is hit (drones quieter) |
| `enemy_death.wav` | Rumbling burst with a low thump | An enemy is destroyed |
| `drone_death.wav` | Small pop | A drone is destroyed |
| `vnp_damage.wav` | Heavy low thud | The VNP takes damage |
| `low_health.wav` | Soft low heartbeat, two thumps | Each low-health flash |
| `harvest_loop.wav` | Soft pulsing hum, two seconds, loops seamlessly | While any harvest beam is working |
| `material_pop.wav` | Rising pop | An asteroid or piece of debris is used up |
| `drone_built.wav` | Rising chirp | A temporary drone is built |
| `wave_warning.wav` | Two soft rising sweeps | The wave warning appears |
| `time_limit.wav` | Two low, round falling tones | The time limit and each escalation step |
| `level_up.wav` | Rising four-note arpeggio | A level-up offer appears |
| `upgrade_choose.wav` | Two rising notes | An upgrade is chosen |
| `menu_move.wav` | Tick | The highlight moves in the level-up offer |
| `menu_select.wav` | Short note | A reroll, or the pause menu opening or closing |
| `win.wav` | Rising major arpeggio | The win result |
| `loss.wav` | Falling notes over a low tone | The loss result |
| `retreat.wav` | Rising sweep | The retreat result |

## Made in code at runtime

These are not saved as files. Each is listed with the script that makes it.

| What | Script | Notes |
|---|---|---|
| Asteroid rocks: lumpy low-poly spheres, one mesh per shape variant | `code/PlaceholderShapes.cs` (`Rock`), called by `scripts/units/Harvestables.cs` | Made from fixed seeds when the fight starts, because their shape follows `ShapeVariants` and `Bumpiness` in `tuning/asteroids.tres` |
| Unit colours, sizes and effect colours: drones, enemies, shots, beams, asteroids, debris, experience | `tuning/*.tres` and the weapon and enemy definitions | Kept as tuning values, so they can be adjusted with the rest |
| Hit flashes, the low-health flash, the drone build spark, drone wreckage, and the harvest tint | `DroneSwarm.cs`, `EnemySwarm.cs`, `Vnp.cs`, `Harvestables.cs` | Colour changes on the shapes above |
| HUD: health bar (and its low-health pulse), experience bar, clock, kill count, retreat prompt, wave banners, red edge flash and "!" | `HealthBar.cs`, `ExperienceBar.cs`, `FightHud.cs` | Drawn in code each frame |
| HUD weapon slot boxes and the Drone Weapon box | `WeaponSlots.cs` | Boxes drawn in code; the icons are files |
| Damage numbers | `DamageNumbers.cs` | Drawn in code with the default font |
| Level-up pulse ring | `LevelUpPulse.cs` | Drawn on the screen in code |
| Effects: how many sparks, pieces and rings, their sizes, speeds and timing | `scripts/effects/Effects.cs`, with the amounts in `tuning/feedback.tres` | Uses `fragment.tres`, `ring.tres`, `unit_box.tres` and the instanced materials |
| Offer card colours by category | `LevelUpMenu.cs` | Colours are in `tuning/look.tres` |
| Screen shake | `CameraRig.cs` | Strength and timing in `tuning/feedback.tres` |
| Gateway indicator arrow | `GatewayIndicator.cs` | Drawn in code each frame |
| Menu panels and labels: level-up cards, pause menu and its settings page, result screen, the upgrade list on the right, and the starting weapon menu | `Ui.cs`, `LevelUpMenu.cs`, `PauseMenu.cs`, `ResultScreen.cs`, `UpgradeList.cs`, `StartingWeaponMenu.cs` | Styles built in code |
| Font | Godot's built-in default font | No font file is used |
