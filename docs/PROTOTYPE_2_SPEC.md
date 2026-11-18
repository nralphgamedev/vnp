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

## New weapons

Each new weapon comes with its own firing and impact effects and sounds.

### VNP Weapons

| Weapon | How it works | Upgrades |
|---|---|---|
| Pulse Laser (existing) | A straight beam that fires at the nearest enemy and damages only it | Damage, cooldown |
| Orbital Shards (existing) | Shapes that circle the VNP and damage what they touch | Damage, area of effect, speed, count, blocking |
| Scatter Shot | Fires a cone of small projectiles toward the nearest enemy | Damage, cooldown, projectile count |
| Seeker Missiles | Fires slow missiles that home in on enemies | Damage, cooldown, missiles per volley |
| Shockwave | Sends out a ring from the VNP at intervals, damaging enemies it passes through | Damage, cooldown, area of effect |
| Mine Layer | Drops mines behind the VNP as it moves. A mine explodes when an enemy touches it | Damage, cooldown, area of effect |

### Drone Weapons

- **Projectile (existing):** drones fire single projectiles.
- **Beam (existing):** drones attack with a short-range beam.
- **Ram (new):** drones charge enemies that come in range, damage them on impact, pull back, and charge again. They ram even while harvesting. A ramming drone keeps harvesting as long as the material stays within its beam's reach, and stops harvesting if ramming carries it out of reach.

### Stats

- **Armor (new):** reduces the damage the VNP takes.

## Upgrade pool

The pool grows from 12 upgrades to 35. Each upgrade step should be large enough that the player notices it.

| Upgrade | Effect | Condition | Limit |
|---|---|---|---|
| Orbital Shards | New VNP Weapon | A weapon slot is free | Once |
| Scatter Shot | New VNP Weapon | A weapon slot is free | Once |
| Seeker Missiles | New VNP Weapon | A weapon slot is free | Once |
| Shockwave | New VNP Weapon | A weapon slot is free | Once |
| Mine Layer | New VNP Weapon | A weapon slot is free | Once |
| Pulse Laser damage, cooldown | Raises damage, shortens cooldown | | 4 each |
| Orbital Shards damage, area of effect, speed | As phase 1 | Orbital Shards equipped | 4 each |
| Orbital Shards count | Adds more shards | Orbital Shards equipped | 4 |
| Orbital Shards blocking | The shards also block enemy projectiles they touch. A blocked projectile pops | Orbital Shards equipped | Once |
| Scatter Shot damage, cooldown, projectile count | More projectiles widen or fill the cone | Scatter Shot equipped | 4 each |
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
| Shockwave ring speed and radius | placeholder |
| Mine lifetime, most mines out at once, and blast radius | placeholder |
| Ram charge speed, damage, and pull-back distance | placeholder |
| Armor per upgrade | placeholder |
| Shards added per Orbital Shards count upgrade | placeholder |
| Extra fight experience per Debris reward upgrade | placeholder |
| Upgrade amounts for every new upgrade | placeholder, large enough to notice |
| Upgrade limits | 4 for each limited upgrade, to start |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Drone damage and attack rate:** they apply to whichever Drone Weapon the drones are using and carry over when the drones switch.
- **Ramming damage to drones:** drones take no damage from ramming itself, only from enemy attacks.
- **Mines:** mines are dropped only while the VNP is moving. Each lasts a set time, and if a new mine would go over the most allowed at once, the oldest one disappears.
- **Seeker Missiles targets:** each missile heads for the nearest enemy when fired and picks a new nearest enemy if its target is destroyed first.
- **Full weapon slots:** once every weapon slot is filled, new VNP Weapons are no longer offered.
- **How offers are drawn:** evenly at random from every upgrade whose condition is met and whose limit isn't reached.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

These were open when this file was written. The prototype now does the following; any of them can still change.

- **When tuning changes take effect:** each tuning file is read when the game starts. A change applies from the next run, not during a running fight.
- **Tuning files:** one file per system in `res://tuning/`: `vnp`, `drones`, `harvesting`, `asteroids`, `enemy_spawning`, `difficulty`, `fight`, `levels`, `camera`, `look` (colours shared by several systems, the gateway indicator, and offer card colours), and `feedback` (hit flash, damage numbers, screen shake, low health, and the amounts and size of every effect). Weapons, Drone Weapons, enemy types, and upgrades are definitions in folders beside them, organized by what they affect: each weapon and Drone Weapon has its own folder holding its definition and, in `upgrades/`, its upgrades (`weapons/pulse_laser/`, `weapons/orbital_shards/`, `drone_weapons/projectile/`, `drone_weapons/beam/`). The other upgrades are in `upgrades/drones/`, `upgrades/vnp_stats/`, and `upgrades/harvesting/`, and enemy types in `enemies/`. A new category gets its own folder when it is built. The upgrade pool is a list in `levels.tres`, so other lists, such as future Level Up Rewards, can point at the same files. An upgrade can be taken out of the pool without deleting its file.
- **How upgrades are described as data:** each upgrade uses one of a fixed set of effects written in code: equip a weapon, scale a weapon stat, switch the Drone Weapon, scale a player stat, add to a player stat, or build drones. Each effect has its own built-in condition, such as "weapon equipped" or "drones below the cap". An upgrade that reuses an effect is a new file. An upgrade that needs new behaviour, such as Orbital Shards blocking, needs a new effect added in code.
- **Card text:** each upgrade's card text is in its file. `{percent}`, `{amount}`, and `{count}` (the Orbital Shards shard count) are filled in from the numbers, so the text follows the tuning.
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
- **HUD icons:** weapon slot boxes and the Drone Weapon box sit at the bottom left, above the level. To keep the screen clean during play, they show only while a level-up offer or the pause menu is open, drawn above it. Each weapon's icon is a file named after its ID; a weapon without one shows its first letter.
- **Effect performance:** effects are drawn in batches, like the units, and capped at 3,000 particles (`MaxParticles`). In a stress test with about 900 enemies and 350 kills, they added about 0.06 ms per frame.
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
| VNP Weapon | Orange, `VnpWeaponCardColor` | New VNP Weapons and upgrades to a VNP Weapon | Orbital Shards; Pulse Laser damage and cooldown; Orbital Shards damage, area of effect and speed |
| Drone Weapon | Blue, `DroneWeaponCardColor` | New Drone Weapons and upgrades to how drones attack | Beam |
| Stats | Green, `StatsCardColor` | The VNP's own stats, and harvesting | Health, Movement speed, Harvest rate |
| Drones | Pink, `DronesCardColor` | How many drones there are | Drone cap, New drones |

**Filing a new upgrade.** Upgrades to a weapon go with that weapon's kind, so its card matches the weapon it improves. The upgrades still to come in this phase are filed as follows:

- **VNP Weapon:** the four new VNP Weapons and their upgrades, plus Orbital Shards count and blocking.
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
