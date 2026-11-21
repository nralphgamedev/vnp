# VNP Combat Prototype, Phase 3: Adaptation

Phase 1 (`docs/PROTOTYPE_SPEC.md`) built one complete fight. Phase 2 (`docs/PROTOTYPE_2_SPEC.md`) added feedback and more weapons and upgrades. VNP's core idea is that the player constantly adapts to the enemy, and with only one enemy type nothing has tested that yet. Phase 3 adds a second enemy type that calls for a different build, mixed into the same fight as the first, along with ways to see what worked.

Everything in the phase 1 and phase 2 specs still applies unless this file changes it. Where they disagree, the newest file wins.

## What this phase is for

- Does the second enemy type call for different upgrade picks than the first?
- With both types in one fight, does the player have to balance their build rather than specialize?
- Does the damage breakdown show clear differences in how weapons perform against each type?
- How much does the amount of asteroids change a fight's difficulty?
- Does performance hold with the new enemy in large numbers?

## The Goo

The Goo (ID `goo`) is a placeholder for the GDD's Grey Goo faction: blob-like enemies that absorb the player's drones. The existing enemy (`basic_enemy`) is unchanged. In the full game each faction will have several enemy types; for now, the two types are simply mixed together.

### Behavior

- **Movement:** slow, drifting flock movement, slower than the basic enemy. Idle groups wander near where they spawned, as the basic enemy does.
- **Targeting:** inside its aggro range, a Goo goes after the nearest drone. It goes after the VNP only when no drone is within its aggro range.
- **Absorbing drones:** a Goo drains the health of every drone touching it, at a steady rate. A drone drained to nothing is absorbed: it is gone and leaves no wreckage. A drone that gets away keeps the damage. This applies with every Drone Weapon, so a ramming drone takes some drain each time it passes through a Goo, and a drone that lingers is lost. After playtesting found instant absorbing on touch felt bad, this replaced it.
- **Growing:** each absorbed drone makes the Goo larger and tougher.
- **Splitting:** once a Goo has absorbed enough drones, it bulges for a moment, then splits into two Goos that share what it had grown: its bulk, health, and the drones it absorbed, half each. Drones it absorbs while bulging count toward the halves' next split. Feeding the Goo drones makes more of them. The full game will have several Goo types, each with its own split size, so the number of drones before a split belongs to each Goo type's file. There is no limit on how many Goos splitting can make, for now.
- **Attacking the VNP:** the Goo has no ranged attack. It damages the VNP by touching it, once per touch with a cooldown before it can do so again, and cannot absorb the VNP. As the GDD describes, it is more of a threat to drones than to the VNP.
- **How drones fight it:** drones measure their attack distance and range from an enemy's edge rather than its center, so they surround a large Goo at its edge, not inside it. They also stay a little farther back from a Goo than from the basic enemy. Ramming drones are the exception: they still charge through it, and are drained as they pass. A drone that strays into a Goo for any other reason, such as flocking or harvesting past it, is drained too.
- **Size:** a Goo's size is also its reach. As it grows, it touches, and so drains drones, from farther out.
- **Debris:** a destroyed Goo leaves debris like the basic enemy. The more drones it absorbed, the more mass its debris has, so destroying a Goo that grew wins back some of what it took.
- **Difficulty:** the Goo grows stronger over the fight and with each escalation step, as the basic enemy does.

### Spawning

- Both enemy types spawn in the same fight, under the existing spawn rules. Each spawned group is one type, picked at random by a weight in the tuning, so the mix can be adjusted.
- Waves include both types.
- Every kill counts toward the kill target, whichever type it is. Goos made by splitting count as separate kills. The split itself is not a kill.
- Each enemy type's group size is in its own file, the basic enemy's included.

### Look and feedback

- A blob that is clearly different from the basic enemy by its silhouette, not only its color: a lumpy, slowly wobbling grey mass. Its size shows how much it has grown. It is a pale, glossy grey with a soft light at its edges, because a dull grey blob was easy to mistake for an asteroid.
- A drone being drained flashes, and small motes in its colour flow from it into the Goo, so the player can see it being eaten.
- An absorbed drone is visibly pulled into the Goo rather than popping, so the player can tell an absorbed drone from a destroyed one.
- The Goo pulses when it absorbs a drone and visibly bulges before it splits.
- New placeholder sounds for absorbing a drone, splitting, and the Goo's death. Like every placeholder asset, they go in `res://placeholder/` and are listed in `PLACEHOLDERS.md`.

## Damage breakdown

The result screen shows how much damage each source did during the fight, so the player can see what worked.

- One line per source: each VNP Weapon, each Drone Weapon the drones used, and any other source. A Drone Weapon used for only part of the fight shows the damage it did while in use.
- Each line shows its total and its share of all damage dealt, split by enemy type, so the player can see which weapons worked against which enemy.
- Only damage that actually took health off an enemy counts. The part of a hit that goes past an enemy's remaining health is left out, so big hits on weak enemies don't inflate a weapon's number.
- Drones lost, split into destroyed and absorbed.
- How many enemy shots Orbital Shards blocking stopped, since blocking does no damage.
- The breakdown appears after every fight, including endless runs.

The upgrade list on the result screen stays. Fit the breakdown in where it reads clearly, on its own page or panel if needed.

## Asteroid amount

The GDD says the amount of asteroids varies between fights and is one of the settings that set a fight's difficulty. This phase tests how much it matters.

- Asteroid amounts are definitions: Sparse, Normal, and Rich to start. Each sets how many asteroid fields and lone asteroids a fight has. Normal matches the fights as they are now.
- Each fight picks one at random, by a weight for each amount.
- The result screen shows which amount the fight had.
- A new page in the playtest cheats' starting choice menu lets the designer pick the amount for a fight. Like the other cheats, it works only in the editor.

## Orbital Shards harvesting

A new upgrade from the GDD.

- **Effect:** the shards also damage asteroids and debris they touch, which harvests them faster. A conversion rate sets how much shard damage counts toward harvesting, so upgraded shards don't empty asteroids instantly. Material harvested by shards gives fight experience and builds drones in the same way as beam harvesting.
- **Condition:** Orbital Shards equipped. **Limit:** once. **Category:** VNP Weapon. It unlocks a weapon feature, like Orbital Shards blocking.
- **Feedback:** once it is taken, the shards show a small harvesting effect as they pass through material.

The upgrade pool grows from 38 upgrades to 39.

## Tuning values

As in the phase 2 spec, this file names each value and where it lives rather than its number.

| Value | Where |
|---|---|
| The Goo's health, contact damage and its cooldown, speed, aggro range, size, and group size | the Goo's file in `enemies/goo/` |
| How far back drones stay from a Goo's edge | the Goo's file |
| How fast a Goo drains a touching drone's health (`DroneDrainPerSecond`) | the Goo's file; it grows over the fight with the Goo's damage values in `difficulty.tres` |
| Each enemy type's group size | each enemy's file, now including the basic enemy's, which moved there from `enemy_spawning.tres` |
| How much each absorbed drone makes a Goo grow and toughen, and how many it absorbs before splitting | the Goo's file |
| The Goo's debris mass, and how much each absorbed drone adds to it | the Goo's file |
| How quickly the Goo grows stronger during the fight and per escalation step | `difficulty.tres`, beside the basic enemy's values |
| The weight of each enemy type when a group spawns, and in waves | `enemy_spawning.tres` |
| Each asteroid amount's number of fields and lone asteroids, and its weight | the asteroid tuning |
| The Orbital Shards harvesting conversion rate | the upgrade's file |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Splitting:** a Goo splits after absorbing a set number of drones, into two Goos that share its size and health.
- **The Goo's targets:** the nearest drone first, and the VNP only when no drone is in range.
- **Mixing:** each spawned group is one type, picked by weight. Waves have both.
- **Kill target:** every kill counts as one, whichever type it is.
- **Overkill:** left out of the damage breakdown.
- **Asteroid amount:** picked at random each fight from Sparse, Normal, and Rich.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

These were open when this file was written. The prototype now does the following; any of them can still change.

- **Absorbing:** a Goo drains a touching drone's health, and absorbs it when its health runs out (see The Goo). The first build absorbed drones instantly on touch, which felt bad in playtesting. A short contact time before absorbing was also considered; draining was chosen because it shows the drone being eaten, and a drone that escapes stays damaged.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

1. **The Goo:** movement, targeting, absorbing, growing, splitting, contact damage, debris, its look, effects, and sounds, and spawning mixed with the basic enemy.
2. **Damage breakdown** on the result screen, including drones lost and shots blocked.
3. **Asteroid amounts:** the random pick, the result screen line, and the cheat page.
4. **Orbital Shards harvesting.**

## Performance

Goos grow and split, so their numbers can climb quickly when they absorb many drones. Check the frame rate in a late fight where Goos have absorbed and split freely.

## Out of scope

Everything out of scope in phases 1 and 2, plus: factions, more than two enemy types, playing several fights in a row, Level Up Rewards, weapon combos, escort VNPs, music, and final art and audio.

## Stretch goals

Add these only after this phase feels good and the designer asks for them.

- A third enemy type, such as a Berserker-style enemy that is large, slow, and powerful, to test single-target weapons.
- An upgrade that counters the Goo directly, if playtesting shows the existing pool doesn't offer a good answer to it.

Three fights in a row, with Level Up Rewards between them, is planned as the next phase.

## Done when

- Both enemy types appear in the same fight, and the Goo absorbs, grows, and splits as described.
- A build that leans on drones plays noticeably differently against Goos than against the basic enemy.
- The result screen shows damage by source and by enemy type, drones lost, and the fight's asteroid amount.
- Orbital Shards harvesting works, and its conversion rate can be tuned.
- The frame rate holds in a late fight with many Goos.
- Every new placeholder asset is in `res://placeholder/` and listed in `PLACEHOLDERS.md`.
