# Phase 1 addendum: what changed in building and testing

`PROTOTYPE_1_SPEC.md` is the designer's intent, as given, and is never edited. This file records only what an AI working on the project needs in order not to get it wrong: where the build now differs from the spec, why, what was rejected, and choices made for items the spec left open. Where this file and the spec disagree, this file wins, and later phases' specs and addenda win over both.

Most of phase 1 was changed again by later phases; this file covers what phase 1 itself changed. Phase 1's reasons were mostly not written down at the time. Where an entry says "after playtesting", that is all that is known.

## Names and files

- **Drones, not basic probes:** "basic probes" were renamed drones throughout the code and docs during phase 1. The Basic Probe Weapon is the Drone Weapon. Read "probe" in this spec as "drone".
- **Sector Conquer:** the spec's Out of scope list says "Zone Conquer"; the GDD's name is Sector Conquer.
- **File paths:** this file was `docs/PROTOTYPE_SPEC.md` and is now `docs/design/PROTOTYPE_1_SPEC.md`. Later specs' paths moved the same way, into `docs/design/`.
- **Tuning in one place:** this was replaced in phase 2 by data Resources kept in each feature's folder (see the phase 2 addendum). The code no longer has a single tuning script.

## Asteroid fields

- **Lone asteroids:** occasional lone asteroids sit between fields.
- **Filled in as the player explores:** fields start around the gateway. Farther out, fields and lone asteroids are placed as the player explores, so asteroids stay about as common however far the player goes. Once placed, they stay where they are and don't respawn. This settles the spec's open item on where fields sit.
- **Look:** asteroids are lumpy low-poly rocks with random orientation and stretch, and some tumble slowly.

## Harvesting

- **Harvest time:** mass draws out gradually at a harvest rate, so fight experience arrives while the beam works. The spec's fixed harvest time of about 1 to 10 seconds by mass is not used; the rates are in the tuning (`harvesting.tres`).
- **The VNP's beam:** it is much faster than a drone's, after playtesting.
- **Harvest range upgrade:** removed after playtesting. The pool kept twelve upgrades, with Orbital Shards speed added in its place.

## Drones

- **Fighting comes first:** drones attack whenever an enemy is in range, and harvest only when none is. This settles the spec's open item, and replaces the spec's "harvesting stays their priority".

## Enemies

- **Idle groups** wander near where they spawned, pulled gently home.
- **Aim:** shots lead a moving target (phase 2 later added per-enemy accuracy).
- **Targets:** inside its aggro range, an enemy chases whichever is nearest, the VNP or a drone. Phase 2 added holding a target; phase 3 and phase 4 changed what Goos go after.
- **Spawning:** enemies spawn in groups at a random spot a margin beyond what the camera shows, so they stay out of sight at any camera height. This settles the spec's open item on where enemies spawn.
- **Waves:** added after playtesting. Every so often several groups arrive at once, spaced round the VNP off screen, all hunting it. Wave enemies chase from any distance and never give up. Each wave is larger than the last, with a warning on screen a few seconds before. Wave timing and size are tuning values.

## Fight experience and level ups

- **The free reroll:** one per fight, not refreshed. This settles the spec's open item.
- **Upgrade limits:** most upgrades can be taken only so many times, 4 to start (designer-set). The card shows which time it would be, such as 2/4, and an upgrade at its limit is no longer offered. New drones has no limit, and isn't offered with drones at the cap.

## Winning, losing and retreating

- **Escalation:** each step past the time limit makes newly spawned enemies tougher and hit harder, makes them spawn faster, and widens every enemy's aggro range. Phase 4 also makes them faster (see the phase 4 addendum).

## Screens

- **HUD:** the clock counts down to the limit, then counts time past it. The HUD also shows kills toward the kill target and the retreat prompt on the gateway.
- **The pause menu** only resumes (phase 2 added Settings).
- **The result screen** also shows the level reached and the upgrades taken.

## Tuning values

The spec's starting points that the designer didn't set ("placeholder") live in the tuning files, not in this spec.
