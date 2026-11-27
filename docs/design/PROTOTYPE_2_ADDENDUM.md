# Phase 2 addendum: what changed in building and testing

`PROTOTYPE_2_SPEC.md` is the designer's intent, as given, and is never edited. This file records only what an AI working on the project needs in order not to get it wrong: where the build now differs from the spec, why, what was tried and rejected, and choices made for items the spec left open. Where this file and the spec disagree, this file wins, and later phases' specs and addenda win over both.

The spec's paths are from before the docs moved: `docs/PROTOTYPE_SPEC.md` is now `docs/design/PROTOTYPE_1_SPEC.md`.

## Tuning and content data

- **Files sit with their feature,** not all in `res://tuning/`, at the designer's request. Keeping all data in `res://tuning/` meant moving between folders to see one weapon.
  - Each weapon, Drone Weapon and enemy type has a folder with its code, definition and `upgrades/`.
  - Game-wide settings stay in `res://tuning/`, one file per system, with the upgrades that belong to no feature in `tuning/upgrades/`. Audio has its own folder, `audio/`.
  - CLAUDE.md has the full rule.
- **The upgrade pool** is a list in `levels.tres`. Taking an upgrade out of the list removes it from the pool without deleting its file.
- **Upgrade effects are a fixed set written in code:** equip a weapon, scale or add to a weapon stat, unlock a weapon feature, switch the Drone Weapon, scale or add to a player stat, and build drones. Each effect has a built-in condition.
  - An upgrade that reuses an effect is just a new file.
  - A new kind of upgrade needs a new effect in code.
  - Weapon features (blocking, and harvesting from phase 3) are written in their weapon's code and switched on by an unlock upgrade.
- **Card text** is in each upgrade's file, with `{percent}`, `{amount}`, `{count}` and `{drones}` filled in from the numbers.
- **When tuning applies:** each file is read at the start of a fight, so a change applies from the next fight.
- **Placeholder meshes, materials and models** are made by `res://placeholder/code/PlaceholderBaker.cs`; change the baker and bake again rather than editing the files. Sounds are synthesised by `PlaceholderSounds.cs`; none are downloaded.
- **Colours:** model colours are in their material files. `look.tres` holds colours used by several systems and the offer card colours.

## Feedback

- **Screen shake:** a weaker shake doesn't cut short a stronger one.
- **Damage numbers:** hits on one enemy close together merge into one number (`DamageNumberMergeSeconds`), and how many show at once is capped (`DamageNumberMax`).
  - Why: fast beams filled the screen with ones.
- **Debris:** it lies roughly flat and tumbles slowly, with its tumble axis kept near upright, so it never turns edge-on and becomes hard to see.
  - Rejected: a star-like look.
  - Debris mass was raised after playtesting (`DebrisMassMin`, `DebrisMassMax` in each enemy's file).
- **HUD icons:** the weapon slots and the Drone Weapon box show only while a level-up offer, the pause menu or the result screen is up, to keep play clean. A weapon without an icon file shows its first letter.
- **Effects are batched and capped** (`MaxParticles`), and reuse objects.
- **Repeated sounds:** each sound has its own copy limit (`MaxCopies`, else `DefaultMaxCopies`), and a sound over its limit is skipped rather than cutting another off. The same sound isn't restarted within `MinRepeatSeconds`, so a burst of hits plays once. Tunes, alarms and menu sounds don't vary in pitch.
- **The time limit alarm** is two low, round, falling tones, after playtesting found the first version too jarring. Rejected: held buzzing pulses a semitone apart.
- **Sounds while paused:** phase 4 changed this; see the phase 4 addendum.

## Screens

Added at the designer's request; the spec has none of this.

- **Pause menu:**
  - Two options, Resume and Settings, with the upgrades taken listed on the right.
  - Settings is its own page, grouped by category. Sound, with Master and Effects volumes in steps of 10%, is the only category so far.
  - Settings are saved in `user://settings.cfg`, which holds only preferences. It starts from `MasterVolume` and `EffectsVolume` in `audio.tres`.
  - One-handed controls: W/S picks a row, A/D changes a volume, Space or A picks, Esc or Start resumes or goes back. The mouse works too.
- **Upgrade list:** the pause menu and the result screen list the upgrades taken, one per line.
  - They are grouped by each VNP Weapon in slot order, with the upgrade that equipped it shown as Equipped, then Drone Weapon, Stats and Drones. Headings take their category's card colour.
  - Each line shows its count (×2) and its total. Percentages multiply and end in %, so two +25% show as +56%. Flat amounts add up.
  - Once-only upgrades (equip, switch, unlock) show neither a count nor a total.
- **Endless mode:** after a win, the result screen offers New fight or Continue (endless).
  - Continuing carries on the same fight: the same VNP, upgrades and difficulty clock, with the ramp and, from the limit on, escalation. Endless adds no difficulty of its own.
  - The kill target no longer ends the fight, and the HUD shows "Endless" in place of the target.
  - It ends by retreating or by being destroyed. Either way it is shown as a VICTORY, "Endless run complete", with the kill target time and the kills made in endless.
- **Playtest cheats:** these are editor-only testing aids, not design. CLAUDE.md has the rules for keeping them out of builds, and their settings are in `scripts/cheats/in_game_cheats.tres`.
  - **Starting weapon choice** (`ChooseStartingWeapon`): fights start on menus to pick the starting VNP Weapon and Drone Weapon. The chosen weapon replaces the Pulse Laser in the first slot.
    - The Pulse Laser then has an equip upgrade of its own, and the Projectile a switch upgrade of its own.
    - The chosen Drone Weapon's switch upgrade isn't offered.
    - With the cheat off, and always in an export, the fight starts with `StartingWeapons` in `vnp.tres`.
  - **Free rerolls:** holding Alt makes rerolling free.
  - **The scenario harness** (`scripts/cheats/scenario.tscn`, arguments in `ScenarioRunner.cs`) plays a fight set up from the command line. The stage check uses it, since a normal editor fight waits on the starting weapon menu.
  - **Names:** `InGameCheats`, `InGameCheatSettings` and `in_game_cheats.tres`. They were renamed from `PlaytestCheats` and `cheats.tres`, which were easy to confuse with the automated playtesting tools.

## New weapons

- **Scatter Shot:** it fires when an enemy is within range, at the nearest.
  - Each extra pellet widens the cone (`ConeWidenPerProjectile`) up to `MaxConeDegrees`; past that, more pellets fill it.
  - Pellets use the shared projectile system.
  - A range upgrade was added after playtesting: it fires at enemies farther away and carries the pellets farther.
- **Seeker Missiles:** they fire when an enemy is within range. The volley fans out (`LaunchSpread`), then each missile turns at `TurnRate`, so it visibly curves in.
  - A missile hits the first enemy in its path, target or not, and fizzles after `MissileLifetime`.
  - Phase 4 changed targeting and added an explosion; see the phase 4 addendum.
- **Shockwave:** a ring goes out every cooldown whether or not an enemy is near, after playtesting. Rejected: waiting for an enemy within reach.
  - The ring stays centred where it went out.
  - It damages each enemy once.
  - More than one ring can be out at a time.
- **Mine Layer:** it drops a mine `DropDistance` behind the VNP every cooldown, while the VNP moves faster than `MinMoveSpeed`.
  - A mine goes off when an enemy comes within `TriggerRadius` of its edge, damaging all within `BlastRadius`.
  - It lasts `MineLifetime`. Past `MaxMines`, the oldest fizzles.
- **Ram:** this replaces the spec's "pull back, and charge again". Drones charge through their target and arc back round, never stopping or turning on the spot.
  - **Charging:** a drone charges at `ChargeSpeed`, homing only a little (`ChargeTurnRate`), deals its damage on contact, and carries straight through. A charge that misses gives up after a moment.
  - **Arcing back:** it glides on, slowing (`ArcSlowdown`), and turns back in an arc (`ArcTurnRate`). While its cooldown runs, it circles wide enough to turn straight in, starts turning just before the cooldown ends, and charges from a little beyond its range. A target too close to line up on gets a short straight run first.
    - Why: after playtesting, drones spent much of their time ready to charge but unable to line up.
  - **Losing a target:** a drone keeps its momentum and arcs toward the new one. With no enemy left, it finishes its arc for at most half a circle's turn before going back to harvesting or flocking.
    - Why: drones snapped straight round when their target died.
    - Why the half-circle limit: the spot a drone wanted could lie inside its loop, which left it circling until the rock was gone.
  - **Attack rate:** Drone attack rate upgrades also tighten the arc by the same multiple, since otherwise the loop, not the cooldown, set how often a drone rammed.
  - **Overlap:** ramming drones don't keep apart from each other.
  - **Damage:** Ram's damage sits between Projectile and Beam.
- **Drones never take damage from ramming,** only from enemy attacks. This was the spec's default, kept.
- **Armor** is a percentage less damage per hit, compounding, so it stays useful as enemy damage grows.
- **Debris reward** gives a percentage more fight experience from debris, compounding. Asteroids and drone building are unchanged.
- **Orbital Shards blocking:** shards destroy enemy shots within their hit radius, plus a little for a shot's travel in a frame, so area of effect upgrades widen blocking too. Once it is taken, the shards turn mint (`BlockingColor`) to show they block.

## Enemies

Changes to phase 1's basic enemy, all in its file:

- **Holding a target:** an enemy keeps its target for `TargetHoldSeconds`, varied a little per enemy, before it looks again for the nearest. It looks again at once if its target dies or gets out of reach.
  - This changes phase 1's "chases whichever is nearest". Why: enemies jittered, flicking between ramming drones flying past.
- **Turning its sense of the target:** it turns at `TurnSpeed` rather than jumping, and keeps turning the same way while the target is nearly behind it.
- **Blending:** near `HoldDistance` it blends from closing in to circling over `HoldBlendDistance`. Shots still aim at where the target really is.
- **Accuracy:** each enemy rolls its accuracy as it spawns, from sloppy to sharpshooter, leaning low (`SharpshooterRarity`). Accuracy sets how far it leads (`AimLeadMin` to `AimLeadMax`) and how much it strays (`AimSpreadMax` to `AimSpreadMin`).
  - Why: after playtesting, enemies were too accurate all the time. Rejected: a single `AimLead`.
  - Accuracy rises with difficulty (`RampEnemyAccuracyPerMinute`, `EscalationEnemyAccuracyPerStep` in `difficulty.tres`, capped at 1).

## Upgrade pool

- **Size:** the pool is 38 at the end of phase 2, not 35. Phase 3 took it to 39 and phase 4 to 40.
  - Pulse Laser equip and Projectile switch, for fights started without them by the starting weapon cheat.
  - Scatter Shot range.
- **Pulse Laser upgrades** need the Pulse Laser equipped. It always is, unless the cheat started the fight with another weapon.
- **Drone Weapon switches:** offered only until one Drone Weapon has been chosen at a level up (phase 3).
- **Choices for the spec's open items,** all kept as listed by the designer:
  - Drone damage and attack rate apply to whichever Drone Weapon is in use and carry over a switch.
  - Mines drop only while moving, last a set time, and the oldest goes past the limit.
  - Once every slot is full, new VNP Weapons aren't offered.
  - Offers are drawn evenly from every upgrade whose condition is met and whose limit isn't reached.
  - Seeker Missiles targeting was later changed in phase 4.

## Offer card colours

The spec asks for cards coloured by category but gives no rule; this is the rule built.

- **Category:** each upgrade's file has a `Category`. The card takes that category's colour from `look.tres` for its border, a light tint, and its top line.
  - The highlighted card has a thicker border and a stronger tint.
  - The top line names the category. It says NEW for a weapon not yet owned, and shows the count, such as `STATS   2/4`, for limited upgrades.
  - Mod cards say VNP WEAPON MOD (phase 3).
- **The categories:**
  - **VNP Weapon** (orange): new VNP Weapons and their upgrades, mods included.
  - **Drone Weapon** (blue): new Drone Weapons, and upgrades to how drones attack (Drone damage and attack rate).
  - **Stats** (green): the VNP's own stats, and harvesting.
  - **Drones** (pink): drone cap and new drones.
- **Filing a new upgrade:** an upgrade to a weapon takes its weapon's kind. One that applies to whichever Drone Weapon is in use is a Drone Weapon upgrade.

## Tuning values

The spec's "starting point" column is replaced by where each value lives. Values whose files aren't obvious:

- Hit flash, damage numbers and screen shake are in `feedback.tres`.
- Volumes, copy limits and pitch variation are in `audio.tres`.
- Enemy accuracy, target holding and blending are in each enemy's file.
- Accuracy over the fight and per escalation step is in `difficulty.tres`.
- Weapon slots, 2 (designer-set), are `WeaponSlots` in `vnp.tres`.
- Upgrade limits, 4 to start (designer-set), are `Limit` in each upgrade's file.
