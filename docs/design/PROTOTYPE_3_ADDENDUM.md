# Phase 3 addendum: what changed in building and testing

`PROTOTYPE_3_SPEC.md` is the designer's intent, as given, and is never edited. This file records only what an AI working on the project needs in order not to get it wrong: where the build now differs from the spec, why, what was tried and rejected, and choices made for items the spec left open. Where this file and the spec disagree, this file wins, and phase 4's spec and addendum win over both.

The spec's paths are from before the docs moved: `docs/PROTOTYPE_SPEC.md` is now `docs/design/PROTOTYPE_1_SPEC.md`, and `docs/PROTOTYPE_2_SPEC.md` is now `docs/design/PROTOTYPE_2_SPEC.md`.

## Status

- **The phase is done,** by the designer's verdict after playing every stage. It was paused after stage 1 for automated playtesting and run review, which built stage 2 (the damage breakdown) from their fight statistics, then resumed with stages 3 and 4.
- **What it found, in the designer's words:**
  - Ram drones aren't always a good pick against Goos, though still fairly effective.
  - One Goo type was too few to answer the main question, so the designer wanted the GDD's other Goo types. That became phase 4, in place of the planned three fights in a row.
  - The damage breakdown serves its purpose.
  - Goos don't feel threatening to the VNP, as the GDD intends, but approaching a larger group takes care, since that is what gets drones killed.
  - Orbital Shards harvesting feels fine at its current rate.

## The Goo

Phase 4 moved the Goo to `enemies/grey_goo/basic_goo/` as the basic goo and changed what it chases; see the phase 4 addendum.

- **Absorbing:** a Goo drains a touching drone's health and absorbs it at zero.
  - Why: the first build absorbed drones instantly on touch, which felt bad in playtesting. Draining shows the drone being eaten, and a drone that escapes stays damaged.
  - Rejected: a short contact time before absorbing.
  - The spec as committed already describes draining; it was committed together with stage 1.
- **Drones avoid danger:** added at the designer's request. Their playtests found drones fine against a few Goos, but against many they locked onto one and flew into the others.
  - Drones gently steer clear of every enemy they aren't attacking, Ram included (`DroneAvoidDistance` in each enemy's file, `AvoidStrength` in `drones.tres`). A charge still goes straight through its own target.
  - Drones don't go in to harvest material beside a Goo, and leave material a Goo comes up beside (`AvoidHarvestNearDroneEaters`).
  - The designer chose basic, imperfect avoidance: drones in a packed cluster can still be caught.
- **Choices for the spec's open items,** all kept as listed by the designer when the phase closed:
  - **Splitting:** after a set number of drones, into two that share its size and health.
  - **Targets:** the nearest drone first (since changed by phase 4).
  - **Mixing:** one type per group, by weight; waves have both.
  - **Kill target:** every kill counts as one, whichever type, and split halves count separately.
  - **Overkill:** left out of the breakdown.
  - **Asteroid amount:** random each fight.

## Damage breakdown

- **The panel:** it sits on the left of the result screen, mirroring the upgrade list on the right. Sources are listed most damage first in their weapon's colour, with total and share. Each has its split by enemy type in each enemy's colour, then drones lost, then shots blocked once blocking has stopped any. The result screen also shows the fight's seed.
- **Other:** there is no Other source yet; damage with no known source would be listed as Other.
- **Shared colours:** since phase 4, Grey Goo types share one colour here, told apart by name.

## Asteroid amount

- **Files:** `AsteroidAmountDefinition`, with Sparse, Normal and Rich in `tuning/asteroid_amounts/`, listed in `Amounts` in `asteroids.tres`.
- **What an amount does:** it scales `asteroids.tres` rather than replacing it. `FieldScale` multiplies the number of fields and `LoneScale` the lone asteroids, around the gateway and as the arena fills in. Field sizes and masses are unchanged. Normal is 1.
- **Spread:** at the designer's request, Sparse was made sparser and Rich richer than first built.
- **The pick:** each fight picks by `Weight`, all equal for now, from a generator of its own seeded from the fight's seed, so fights that already existed didn't change.
- **The cheat page:** the starting choice menu ends with an asteroid amount page (Random first), turned on by `ChooseAsteroidAmount` in `in_game_cheats.tres`, separately from the starting weapon cheat.

## Orbital Shards harvesting

- **Where the rate lives:** the conversion rate is `HarvestPerDamage` in `weapons/orbital_shards/orbital_shards.tres`, not in the upgrade's file as the spec says, since an unlock upgrade carries no numbers. The upgrade is `weapons/orbital_shards/upgrades/harvesting.tres`, unlocking the feature `Harvesting`.
- **How it harvests:** each shard harvests what it touches once per the weapon's cooldown, about once a pass, drawing its damage times `HarvestPerDamage`. Damage, speed and count upgrades raise it; Harvest rate upgrades don't, since they speed up beams.
- **Feedback:** a small burst at the shard and the material's harvest glow; no new sound.

## Upgrade rules added in phase 3

- **One Drone Weapon choice per fight,** at the designer's request: once a Drone Weapon is chosen at a level up, none is offered again that fight. The starting one, including a cheat pick, doesn't count. `OneDroneWeaponChoicePerFight` in `levels.tres` turns it off. This changes phase 2's pool.
- **One mod per weapon,** at the designer's request: once one of a weapon's features is taken (Orbital Shards blocking or harvesting), its others aren't offered that fight. This applies to every weapon's features, including future ones. `OneFeaturePerWeapon` in `levels.tres` turns it off. This changes phase 2, where blocking could be taken alongside anything.
- **Card notes:** cards say so, in their category's colour: "Other Orbital Shards mods will not be offered this fight", and "Other drone weapons will not be offered this fight". Each note shows only while its rule is on, and the mod note only when the weapon has other features in the pool. Cards call weapon features mods, and a mod's card is tagged VNP WEAPON MOD.
- **Pool size:** 39 by the end of phase 3.
