# VNP Combat Prototype, Phase 2: Feedback and Content

Phase 1 (`docs/PROTOTYPE_SPEC.md`) built one complete fight. It works and is almost fun, but two things hold it back: hits, kills, harvesting, and level ups give little feedback, and there aren't enough weapons and upgrades for builds to differ from fight to fight. Phase 2 adds both.

Everything in the phase 1 spec still applies unless this file changes it. Where the two disagree, this file wins.

## What this phase is for

- Does clear visual and sound feedback make the fight feel good to play?
- Can each weapon be told apart by how it looks and sounds, even in a crowded fight?
- Does each upgrade make a change the player can see or feel when they take it?
- With more weapons and upgrades, do fights with different picks play differently?
- Does performance hold with all the new effects on screen during late-fight waves?

## Placeholder assets

Every visual and audio asset made in the prototype is temporary and will be replaced before release. To make that easy to track:

- All placeholder visual and audio assets go in `res://placeholder/`, in subfolders by type (for example `meshes/`, `materials/`, `shaders/`, `textures/`, `effects/`, `audio/`).
- Each asset is saved as its own file in that folder, not embedded inside a gameplay scene, so replacing one means swapping one file.
- `res://placeholder/PLACEHOLDERS.md` lists every placeholder asset: what it is, what uses it, and where it came from. A downloaded asset also lists its license.
- A placeholder visual that is created in code rather than saved as a file is listed in `PLACEHOLDERS.md` too, along with the script that makes it.
- Written text (upgrade names and descriptions, menu labels, and so on) is not a placeholder asset. It stays where it is.

## Tuning and content data

Modding may be supported in the future, so content is described as data rather than written as special-case code.

- Tuning values and content definitions are custom Resources (C# classes that inherit from `Resource`, saved as `.tres` files), kept together in `res://tuning/`.
- Split them by system, such as enemies, weapons, drones, harvesting, upgrades, and feedback, rather than one large file.
- Each weapon, upgrade, and enemy type is a definition with an ID and its values. Code reads these definitions, so a new weapon, upgrade, or enemy that reuses existing behaviors can be added as a new definition without new code.
- Resources hold data only: values and IDs, no logic or embedded scripts. This keeps them easy to convert to JSON later if mod support is added.
- No mod loading is built in this phase.

## Feedback

### Visual

- **Hits:** anything that takes damage (enemies, drones, the VNP) flashes briefly.
- **Damage numbers:** small numbers rise from damaged enemies. They can be turned off.
- **Deaths:** a destroyed enemy bursts apart before its debris appears. A destroyed drone pops.
- **Weapons:** every weapon has its own firing effect and impact effect, so each one can be recognized in a crowded fight.
- **VNP damage:** a small, short screen shake and a flash on the VNP. Red screen edges are already used for the time limit, so low health uses a different signal: the health bar pulses and the VNP flashes periodically.
- **Harvesting:** small particles flow from the material to whatever is harvesting it, the material shrinks as its mass is drawn out, and it pops when used up.
- **New drones:** a brief flash where a temporary drone is built.
- **Level up:** a pulse ring from the VNP as the offer appears.
- **Offer cards:** colored by category (VNP Weapon, Drone Weapon, stats, drones) so an offer can be read at a glance. A card for a weapon the player doesn't have yet is marked as new.
- **Upgrades:** where possible, an upgrade changes how its weapon looks, such as faster shards or a wider shockwave, so the player sees what they gained.

### Screen shake

Small and short. It happens when the VNP takes damage, when a wave arrives, and at each time limit flash. It never happens on ordinary enemy kills, which are too frequent. Its strength can be tuned and it can be turned off.

### HUD additions

- **Weapon slots:** one box for each of the VNP's weapon slots. An occupied slot shows a simple icon for its weapon, and an empty slot is shown empty, so the player can see at a glance how many slots they have and how many are free.
- **Drone Weapon:** an icon showing which Drone Weapon the drones are using (Projectile, Beam, or Ram), which changes when the drones switch.
- These icons are placeholder visual assets, so they go in `res://placeholder/` like the rest.

### Audio

- Placeholder sounds for: each weapon firing, hits, enemy death, drone death, the VNP taking damage, low health, harvesting (a quiet loop while any beam is active), material used up, a drone being built, level up, choosing an upgrade, moving and selecting in menus, the wave warning, each time limit flash, and the win, loss, and retreat results.
- Many sounds play at once in a fight. Limit how many copies of the same sound can play at the same time, and vary the pitch slightly so repeated sounds don't grate.
- Placeholder sounds can be generated by a script (simple synthesized tones and noise bursts) or taken from a free CC0 sound pack. Either way, they go in `res://placeholder/audio/` and are listed in `PLACEHOLDERS.md`.
- No music in this phase.

## Screens

These change or add to the screens in the phase 1 spec.

### Pause menu and settings

- The pause menu has two options: Resume and Settings. This replaces phase 1's pause menu, which could only resume. The fight upgrades taken so far are listed on the right of the screen.
- Settings opens its own page, grouped by category. Sound is the only category so far, with Master and Effects volumes from 0 to 100% in steps of 10%. Further categories can be added to the same page later.
- Settings are kept between runs in a small settings file in Godot's user folder (`user://settings.cfg`). It holds only the player's preferences, nothing about a fight. Until the player changes a volume, it starts from `MasterVolume` and `EffectsVolume` in `audio.tres`.
- Controls, played one-handed: W/S or the stick picks a row, A/D changes a volume, and Space or A picks. Esc or Start resumes from the main page and goes back from Settings. The mouse works too: click a row to pick it, or click a bar to set its volume.
- Moving and changing a volume play the menu move sound, at the new volume, so the change can be heard. Picking plays the menu select sound.

### Upgrade list

- The pause menu and the result screen both list the upgrades taken in a panel on the right of the screen, one per line.
- The upgrades are grouped by what they affected:
  - each VNP Weapon under its own name, in slot order, with the upgrade that equipped it shown as Equipped;
  - then Drone Weapon, Stats, and Drones.
- Each heading takes its category's card colour.
- Each line shows how many times the upgrade was taken (such as ×2) and the total it has added so far:
  - Percentage upgrades multiply and end in %: two +25% damage upgrades show as +56%, and two -15% cooldown upgrades as -28%.
  - Flat upgrades add up and are plain numbers: two +25 health upgrades show as +50.
  - Upgrades that equip or switch a weapon have neither a count nor a number, since they can only be taken once.

### Starting weapon choice (testing)

- A testing aid, not part of the design. While `ChooseStartingWeapon` is on in `vnp.tres`, each fight starts paused on a menu to choose the starting VNP Weapon, with one card per weapon in `StartingWeaponChoices`: Pulse Laser, Orbital Shards, Scatter Shot, Seeker Missiles, Shockwave, and Mine Layer. A/D or the stick picks, Space or A chooses, and the mouse works too.
- The chosen weapon takes the first slot in place of the Pulse Laser. Its upgrades are offered as usual, and the other weapons can still be taken for the second slot. The Pulse Laser then has its own equip upgrade, offered for a free slot like the other new weapons. In a normal fight it is already equipped, so that upgrade is never offered.
- With `ChooseStartingWeapon` off, the fight starts with `StartingWeapons` (the Pulse Laser), as the phase 1 spec describes.

### Endless mode

- After a win, the result screen offers two buttons side by side: New fight, and Continue (endless). A line under them reads "Endless mode: keep fighting for as long as you last. No additional rewards." A/D or the stick picks, Space or A chooses, and the mouse works too.
- Continuing picks the same fight back up where it stopped, with the same VNP, drones, upgrades, and difficulty. Enemies, waves, escalation, and level ups carry on. The kill target no longer ends the fight; the HUD shows the kill count with "Endless" in place of the target.
- An endless run ends by retreating at the gateway or by the VNP being destroyed. Either way it is still shown as a VICTORY, with "Endless run complete". Being destroyed is not called out, since nothing was lost. The stats add when the kill target was reached and the kills made in endless mode. Space or A then starts a new fight.

## New weapons

Each new weapon comes with its own firing and impact effects and sounds.

### VNP Weapons

| Weapon | How it works | Upgrades |
|---|---|---|
| Pulse Laser (existing) | A straight beam that fires at the nearest enemy and damages only it | Damage, cooldown |
| Orbital Shards (existing) | Shapes that circle the VNP and damage what they touch | Damage, area of effect, speed, count, blocking |
| Scatter Shot | Fires a cone of small projectiles toward the nearest enemy | Damage, cooldown, projectile count, range |
| Seeker Missiles | Fires slow missiles that home in on enemies | Damage, cooldown, missiles per volley |
| Shockwave | Sends out a ring from the VNP at intervals, while an enemy is within reach, damaging enemies it passes through | Damage, cooldown, area of effect |
| Mine Layer | Drops mines behind the VNP as it moves. A mine explodes when an enemy touches it | Damage, cooldown, area of effect |

### Drone Weapons

- **Projectile (existing):** drones fire single projectiles.
- **Beam (existing):** drones attack with a short-range beam.
- **Ram (new):** drones charge enemies that come in range, damage them on impact, pull back, and charge again. They ram even while harvesting. A ramming drone keeps harvesting as long as the material stays within its beam's reach, and stops harvesting if ramming carries it out of reach.

### Stats

- **Armor (new):** reduces the damage the VNP takes.

## Upgrade pool

The pool grows from 12 upgrades to 37. Each upgrade step should be large enough that the player notices it.

| Upgrade | Effect | Condition | Limit |
|---|---|---|---|
| Pulse Laser | New VNP Weapon. Only offered when a fight started without it, through the starting weapon choice | A weapon slot is free and the Pulse Laser is not equipped | Once |
| Orbital Shards | New VNP Weapon | A weapon slot is free | Once |
| Scatter Shot | New VNP Weapon | A weapon slot is free | Once |
| Seeker Missiles | New VNP Weapon | A weapon slot is free | Once |
| Shockwave | New VNP Weapon | A weapon slot is free | Once |
| Mine Layer | New VNP Weapon | A weapon slot is free | Once |
| Pulse Laser damage, cooldown | Raises damage, shortens cooldown | Pulse Laser equipped (it always is, unless the fight started with another weapon) | 4 each |
| Orbital Shards damage, area of effect, speed | As phase 1 | Orbital Shards equipped | 4 each |
| Orbital Shards count | Adds more shards | Orbital Shards equipped | 4 |
| Orbital Shards blocking | The shards also block enemy projectiles they touch. A blocked projectile pops | Orbital Shards equipped | Once |
| Scatter Shot damage, cooldown, projectile count | More projectiles widen or fill the cone | Scatter Shot equipped | 4 each |
| Scatter Shot range | Fires at enemies further away, and the pellets fly further | Scatter Shot equipped | 4 |
| Seeker Missiles damage, cooldown, missiles per volley | More missiles fire at once | Seeker Missiles equipped | 4 each |
| Shockwave damage, cooldown, area of effect | A larger area of effect makes the ring reach farther | Shockwave equipped | 4 each |
| Mine Layer damage, cooldown, area of effect | A shorter cooldown drops mines more often; a larger area of effect widens the blast | Mine Layer equipped | 4 each |
| Beam | Switches the drones to Beam | Drones not using Beam | Once |
| Ram | Switches the drones to Ram | Drones not using Ram | Once |
| Drone damage | Raises the damage of the drones' weapon | | 4 |
| Drone attack rate | The drones attack more often | | 4 |
| Health | Raises the VNP's health | | 4 |
| Armor | Reduces the damage the VNP takes | | 4 |
| Movement speed | Raises the VNP's movement speed | | 4 |
| Harvest rate | Fight experience is gathered faster | | 4 |
| Debris reward | Harvesting enemy debris gives more fight experience | | 4 |
| Drone cap | Raises the cap for the rest of the fight | | 4 |
| New drones | Adds temporary drones right away, up to the cap | Drones below the cap | None |

The weapon rows that list several upgrades count each one separately.

## Tuning values

Add these to the existing tuning values, in the same place.

| Value | Starting point |
|---|---|
| Weapon slots | 2 |
| Hit flash length | placeholder, short |
| Screen shake strength, and on or off | placeholder, small; on |
| Damage numbers on or off | on |
| Volume: master, effects, and each sound category | placeholder |
| Most copies of one sound playing at once | placeholder |
| Pitch variation on repeated sounds | placeholder, slight |
| Each new weapon's damage, cooldown, and range | placeholder |
| Scatter Shot projectile count and cone width | placeholder |
| Seeker Missiles speed, turn rate, and missiles per volley | placeholder |
| Shockwave ring speed and radius, and how close an enemy must be for it to send a ring | placeholder: speed 28, radius 14, trigger range 14 |
| Mine lifetime, most mines out at once, and blast radius | placeholder: 12 seconds, 10 mines, radius 5 |
| How close an enemy must come to set off a mine, how fast the VNP must move to drop mines, and how far behind it they drop | placeholder: 1.6, 3, and 2 units |
| Ram charge speed, damage, and pull-back distance | placeholder |
| Armor per upgrade | placeholder |
| Shards added per Orbital Shards count upgrade | placeholder |
| Extra fight experience per Debris reward upgrade | placeholder |
| Enemy accuracy: how far the sloppiest and the sharpest enemies lead their target and how far their shots stray, and how rare sharpshooters are | placeholder: lead 0.3 to 0.9, spread 12° to 1°, rarity 2.5 |
| Enemy accuracy added per minute during the fight and per escalation step | placeholder: 0.06 and 0.15 |
| Upgrade amounts for every new upgrade | placeholder, large enough to notice |
| Upgrade limits | 4 for each limited upgrade, to start |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Drone damage and attack rate:** they apply to whichever Drone Weapon the drones are using and carry over when the drones switch.
- **Ramming damage to drones:** drones take no damage from ramming itself, only from enemy attacks.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

These were open when this file was written. The prototype now does the following; any of them can still change.

- **Mines:** mines are dropped only while the VNP is moving. Each lasts a set time, and if a new mine would go over the most allowed at once, the oldest one disappears.
- **Seeker Missiles targets:** each missile heads for the nearest enemy when fired and picks a new nearest enemy if its target is destroyed first.
- **Full weapon slots:** once every weapon slot is filled, new VNP Weapons are no longer offered.
- **How offers are drawn:** evenly at random from every upgrade whose condition is met and whose limit isn't reached.
- **When tuning changes take effect:** each tuning file is read when the game starts. A change applies from the next run, not during a running fight.
- **Tuning files:** one file per system in `res://tuning/`: `vnp`, `drones`, `harvesting`, `asteroids`, `enemy_spawning`, `difficulty`, `fight`, `levels`, `camera`, `look` (colours shared by several systems, the gateway indicator, and offer card colours), `feedback` (hit flash, damage numbers, screen shake, low health, and the amounts and size of every effect), and `audio` (volumes, repeated sounds, and the list of every sound). Weapons, Drone Weapons, enemy types, and upgrades are definitions in folders beside them, organized by what they affect: each weapon and Drone Weapon has its own folder holding its definition and, in `upgrades/`, its upgrades (`weapons/pulse_laser/`, `weapons/orbital_shards/`, `weapons/scatter_shot/`, `weapons/seeker_missiles/`, `weapons/shockwave/`, `weapons/mine_layer/`, `drone_weapons/projectile/`, `drone_weapons/beam/`). The other upgrades are in `upgrades/drones/`, `upgrades/vnp_stats/`, and `upgrades/harvesting/`, and enemy types in `enemies/`. A new category gets its own folder when it is built. The upgrade pool is a list in `levels.tres`, so other lists, such as future Level Up Rewards, can point at the same files. An upgrade can be taken out of the pool without deleting its file.
- **How upgrades are described as data:** each upgrade uses one of a fixed set of effects written in code: equip a weapon, scale a weapon stat, add to a weapon stat, switch the Drone Weapon, scale a player stat, add to a player stat, or build drones. Adding to a weapon stat was added for the count upgrades (Scatter Shot projectiles, Seeker Missiles per volley, and Orbital Shards count to come), which add a flat number. Each effect has its own built-in condition, such as "weapon equipped" or "drones below the cap". An upgrade that reuses an effect is a new file. An upgrade that needs new behaviour, such as Orbital Shards blocking, needs a new effect added in code.
- **Card text:** each upgrade's card text is in its file. `{percent}`, `{amount}`, and `{count}` (the weapon's own count: Orbital Shards' shards, Scatter Shot's projectiles, or Seeker Missiles per volley) are filled in from the numbers, so the text follows the tuning. `{drones}` is how many drones New drones would build right now. Near the cap that is fewer than its amount: at a cap of 30 with 28 drones, the card says "Gain 2 drones right away." The upgrade list shows the drones it actually built.
- **Placeholder files:** the meshes, materials, and models in `res://placeholder/` are made by `res://placeholder/code/PlaceholderBaker.cs`. Change a shape or colour there and bake again, rather than editing the files. The asteroid rocks are still made in code when the fight starts, because their shape follows tuning values.
- **Offer card colours:** see Offer card colours below.
- **Damage numbers:** hits on the same enemy within a quarter of a second are added into one number, so fast beams don't fill the screen with ones. A number pops slightly larger, rises, and fades. At most 80 are shown at once.
- **Enemy deaths:** the enemy bursts into pieces with sparks and a flash ring, and its debris appears a quarter of a second later (`DebrisDelay`).
- **Weapon effects:** the Pulse Laser flashes where it fires and sparks where it hits. Orbital Shards draw a trail along their orbit, behind each shard, drawn around wherever the VNP is now, so it stays fixed to the shard as the VNP moves (`ShardTrailTime` sets its length), and spark on each hit. Drone projectiles flash when fired and spark on impact, and the drone Beam sparks on each damage tick. Enemy shots also spark when they hit the VNP or a drone.
- **Upgrades that show:** Pulse Laser damage upgrades widen its beam and enlarge its flashes. Orbital Shards damage upgrades lengthen the trails and enlarge the hit sparks. Speed, area of effect, and cooldown upgrades already show as faster, larger, or more frequent attacks.
- **Harvesting:** particles flow along every harvest beam from the material to the VNP or drone, and used-up material pops into pieces with a ring. Drone harvest beams start just below the drones, so a beam seems to come from under the drone rather than out of its middle. Every harvest beam, the VNP's and the drones', angles down into the middle of the material, so its end is hidden inside.
- **Debris look:** debris is a thick broken plate of hull that always tumbles slowly (20 to 60 degrees a second, `DebrisTumbleSpeedMin` and `Max` in `harvesting.tres`), so it no longer looks like a star. It lies roughly flat and its tumble axis stays within 30 degrees of upright, so it wobbles and spins but never turns edge-on, where it would be hard to see.
- **Debris reward:** after playtesting, debris mass was raised from 1–2 to 1.5–2.5 (`DebrisMassMin` and `DebrisMassMax` in the enemy's file), so each kill gives about a third more fight experience and drone-building progress.
- **New drones:** a white ring and a few sparks where each drone is built, as well as its existing grow-in from a white spark.
- **Level up:** the fight pauses for the offer, so the pulse ring is drawn on the screen, over the dimmed fight and under the cards, spreading out from the VNP.
- **Screen shake:** the camera jitters sideways and settles over `ShakeTime`. A weaker shake does not cut short a stronger one.
- **Low health:** below 30% health (`LowHealthFraction`), the VNP flashes red every 0.8 seconds, and its health bar swells and glows red.
- **HUD icons:** weapon slot boxes and the Drone Weapon box sit at the bottom left, above the level. To keep the screen clean during play, they show only while a level-up offer, the pause menu, or the result screen is up, drawn above it. Each weapon's icon is a file named after its ID; a weapon without one shows its first letter.
- **Effect performance:** effects are drawn in batches, like the units, and capped at 3,000 particles (`MaxParticles`). In a stress test with about 900 enemies and 350 kills, they added about 0.06 ms per frame.
- **Placeholder sounds:** every sound is synthesised from simple tones and noise by `res://placeholder/code/PlaceholderSounds.cs` (run by the baker) and saved as a .wav in `res://placeholder/audio/`. None are downloaded.
- **Sound list and volumes:** `audio.tres` lists every sound with its file, category, volume, copy limit, and whether it varies in pitch or loops. Volumes run from 0 to 1. A sound plays at its own volume times its category volume, times the player's master and effects volumes from the pause menu's settings. The categories are weapons, hits, deaths, player (VNP damage and low health), harvesting, drones, alerts (wave warning and time limit), menus, and results. Effects covers every sound and is kept separate from master so music can be added later.
- **Repeated sounds:** each sound has its own copy limit (`MaxCopies`, or `DefaultMaxCopies` if unset). While that many copies are playing, it is skipped rather than cutting one off. The same sound is also not restarted within `MinRepeatSeconds` (0.04), so a burst of hits in one moment plays once rather than stacking into one loud sound. Sounds marked `VaryPitch` play up to 6% higher or lower each time (`PitchVariation`). Tunes, alarms, and menu sounds do not vary.
- **Which events play sounds:** the Pulse Laser plays a sound when it fires, and Orbital Shards play one on each hit, since they never fire. Scatter Shot plays a short blast when it fires. Seeker Missiles play a whoosh when a volley launches and a small explosion when a missile hits. Shockwave plays a low boom as each ring goes out. Mine Layer plays a soft click when a mine is dropped and a blast when one goes off. Drone projectiles play one when fired, and the drone Beam on each damage tick. Enemy shots play one when fired. Both enemies and drones being hit play the hit sound, with drones quieter. The low-health heartbeat, two soft low thumps, plays with each low-health flash. The menu move sound plays when the level-up highlight moves. The menu select sound plays on a reroll and when the pause menu opens or closes.
- **Harvesting loop:** it plays while any harvest beam, the VNP's or a drone's, is working. It fades in and out over `LoopFadeSeconds` (0.25), so short gaps between beams don't click.
- **Sounds while paused:** sounds keep playing while the fight is paused, so menu sounds work during level-up offers and the pause menu.
- **Scatter Shot:** fires when an enemy is within 16 units, aiming at the nearest. It fires 5 pellets (4 damage each) every 1.1 seconds, spread evenly across a 30° cone. Each extra pellet from the count upgrade (+2 a time) widens the cone by 4°, up to 60°, after which more pellets fill it more densely. Pellets are pale gold, use the projectile system (so they hit the first enemy in their path, with the usual hit sparks), and fly a little past the range. Firing throws a wide flash of sparks across the cone. Damage upgrades make the pellets larger and whiter. The range upgrade (+20% a time, added after playtesting) lets it fire at enemies further away, and carries the pellets further to match.
- **Seeker Missiles:** fires a volley of 3 missiles (14 damage each) every 2.2 seconds when an enemy is within 30 units. The volley fans out over 70° at launch, then each missile turns toward its target at up to 200° a second at a speed of 18, so it visibly curves in. Each missile heads for the nearest enemy when fired. If that enemy is destroyed first, it picks the nearest enemy to itself within range, and flies straight on if there is none. A missile hits the first enemy in its path, even if it was not its target, and fizzles out after 3.5 seconds. Missiles are pink, leave a smoke trail, and explode with sparks and a ring. Damage upgrades make the missiles and explosions larger. The count upgrade adds 1 missile per volley.
- **Shockwave:** every 2.5 seconds, if an enemy is within its reach, a ring goes out from where the VNP is. It spreads at 28 units a second to a radius of 14, damaging each enemy once (10 damage) as it passes. The ring stays centred where it went out, so the VNP can move on inside it, and more than one ring can be spreading at a time. It is a thin periwinkle ring that fades as it nears full reach, with hit sparks on each enemy and a low boom. The area of effect upgrade makes it reach further, and the ring visibly grows. Damage upgrades make it brighter.
- **Mine Layer:** while the VNP is moving faster than 3 units a second, it drops a mine 2 units behind it every 0.9 seconds, with a soft click. A mine goes off when an enemy touches it (within 1.6 units of its edge), dealing 22 damage to every enemy within a blast radius of 5, with an explosion, a ring that spreads to exactly the blast radius, and a blast sound. Each mine lasts 12 seconds, and at most 10 are out at once. A mine past that limit, or one that runs out, fizzles. Mines are green spiked discs that turn slowly and blink, blinking faster in their last two seconds. The area of effect upgrade widens the blast. Damage upgrades make the mines and blasts bigger.
- **Enemy accuracy:** after playtesting found enemies too accurate all the time, each enemy now rolls its own accuracy when it spawns, from 0 (sloppy) to 1 (a sharpshooter), and keeps it. The roll leans low (`SharpshooterRarity` 2.5), so most enemies are sloppy and about 1 in 12 is sharp. Accuracy sets how far ahead an enemy leads a moving target (from `AimLeadMin` 0.3 to `AimLeadMax` 0.9) and how far each shot randomly strays (from `AimSpreadMax` 12° to `AimSpreadMin` 1°). These replace the single `AimLead` and are in the enemy's file.
- **Accuracy rises with difficulty:** enemies spawned later are more accurate. `RampEnemyAccuracyPerMinute` (0.06) and `EscalationEnemyAccuracyPerStep` (0.15) in `difficulty.tres` add to the roll, capped at 1. In a simulation against a VNP circling at full speed, enemies hit about 25% of the time at the start, 34% at the time limit, 42% after the first escalation step, and 52% after three. Before this change they hit about 44% of the time throughout.
- **Weapon colours:** the Pulse Laser is cyan, Orbital Shards white, Scatter Shot pale gold, Seeker Missiles pink, Shockwave periwinkle, and Mine Layer green, so each is recognisable in a crowded fight. Enemy shots stay orange.
- **Model colours:** the VNP, gateway, and ground grid colours, and the grid's line spacing, are in their material files rather than in tuning. `GatewayColor` in `look.tres` colours only the gateway arrow and text on screen.

## Offer card colours

Every offer card is coloured by its upgrade's category, so an offer can be read at a glance.

**How it works.** Each upgrade's file in `res://tuning/` has a `Category` field. The card takes that category's colour from `look.tres`. The colour is used for:

- the card's border;
- a light tint on the card's background;
- the text of its top line.

The highlighted card has a thicker, full-strength border and a stronger tint. The top line names the category. It says NEW for a weapon the player doesn't have yet, and for a limited upgrade it shows which time this would be, such as `STATS   2/4`.

| Category | Colour (`look.tres`) | What belongs in it | Upgrades now |
|---|---|---|---|
| VNP Weapon | Orange, `VnpWeaponCardColor` | New VNP Weapons and upgrades to a VNP Weapon | Pulse Laser, Orbital Shards, Scatter Shot, Seeker Missiles, Shockwave, Mine Layer; Pulse Laser damage and cooldown; Orbital Shards damage, area of effect and speed; Scatter Shot damage, cooldown, projectile count and range; Seeker Missiles damage, cooldown and missiles per volley; Shockwave and Mine Layer damage, cooldown and area of effect |
| Drone Weapon | Blue, `DroneWeaponCardColor` | New Drone Weapons and upgrades to how drones attack | Beam |
| Stats | Green, `StatsCardColor` | The VNP's own stats, and harvesting | Health, Movement speed, Harvest rate |
| Drones | Pink, `DronesCardColor` | How many drones there are | Drone cap, New drones |

**Filing a new upgrade.** Upgrades to a weapon go with that weapon's kind, so its card matches the weapon it improves. The upgrades still to come in this phase are filed as follows:

- **VNP Weapon:** Orbital Shards count and blocking.
- **Drone Weapon:** Ram, Drone damage and Drone attack rate.
- **Stats:** Armor and Debris reward.

To change a card's colour, edit the colour in `look.tres`. To move an upgrade to another category, change `Category` in its file.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

1. **Placeholder folder and tuning data.** Move every existing placeholder visual into `res://placeholder/`, save any that are embedded in scenes as their own files, and write `PLACEHOLDERS.md`. If the tuning values aren't already custom Resources in `res://tuning/` as described under Tuning and content data, move them there and turn the existing weapons, upgrades, and enemy into definitions. Make sure every reference still works. Nothing should look or play differently.
2. **Visual feedback.** Hits, damage numbers, deaths, weapon effects for the existing weapons, VNP damage and low health, harvesting, new drones, level up, offer cards, screen shake, and the HUD additions.
3. **Audio.** Placeholder sounds for everything listed under Audio, the limit on copies of one sound, pitch variation, and volumes.
4. **Scatter Shot and Seeker Missiles,** with their upgrades, effects, and sounds.
5. **Shockwave and Mine Layer,** with their upgrades, effects, and sounds.
6. **Ram, the drone upgrades, Armor, Orbital Shards count and blocking, and Debris reward,** completing the upgrade pool.

## Performance

Hundreds of units can be fighting at once, and this phase adds an effect or sound to most of what they do. Keep effects cheap, reuse effect objects instead of creating new ones each time, and check the frame rate during a late-fight wave after each stage.

## Out of scope

Everything out of scope in phase 1, plus: new enemy types, playing several fights in a row, weapon combos, escort VNPs, music, and final art and audio.

## Stretch goals

Add these only after this phase feels good and the designer asks for them.

- A second enemy type whose behavior calls for a different upgrade choice, such as an enemy that absorbs drones.
- Three fights in a row, with one permanent upgrade chosen between each.
- One weapon combo, such as Orbital Shards and Shockwave combining into shards that periodically blast outward.
- One escort VNP with its own flocking drones.

## Done when

- Hits, kills, harvesting, level ups, and the VNP taking damage each have clear visual and sound feedback.
- Every weapon can be told apart by how it looks and sounds.
- Fights with different weapon picks play noticeably differently.
- The frame rate holds during late-fight waves with all effects on.
- Every placeholder visual and audio asset is in `res://placeholder/` and listed in `PLACEHOLDERS.md`.
