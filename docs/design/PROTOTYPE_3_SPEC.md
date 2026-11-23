# VNP Combat Prototype, Phase 3: Adaptation

Phase 1 (`docs/design/PROTOTYPE_1_SPEC.md`) built one complete fight. Phase 2 (`docs/design/PROTOTYPE_2_SPEC.md`) added feedback and more weapons and upgrades. VNP's core idea is that the player constantly adapts to the enemy, and with only one enemy type nothing has tested that yet. Phase 3 adds a second enemy type that calls for a different build, mixed into the same fight as the first, along with ways to see what worked.

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
- **Drones avoid danger:** drones steer clear of every enemy they aren't attacking, so a drone fighting one Goo doesn't drift into the next. The push is gentle, not perfect: a drone chasing its target past another enemy is slowed and turned rather than stopped, so drones in a packed cluster can still be caught. Ramming drones avoid too: a charge still carries straight through its own target, but is turned away from others. Drones also don't go in to harvest material beside a Goo, and leave material a Goo comes up beside.
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

**Built** as part of automated playtesting's stage 1, from the fight statistics. The breakdown is a panel on the left of the result screen, mirroring the upgrade list on the right. It lists each source, most damage first, in its weapon's colour, with its total and share. Under each source is its split by enemy type, in each enemy's colour, with each enemy's share of that source's damage. Then come drones lost (destroyed and absorbed), and shots blocked once Orbital Shards blocking has stopped any. The result screen also shows the fight's seed. There is no "other" source yet; damage with no known source would be listed as Other.

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
| How far from each enemy's edge drones that aren't attacking it steer clear (`DroneAvoidDistance`) | each enemy's file |
| How hard drones steer clear (`AvoidStrength`), and whether they skip material beside a Goo (`AvoidHarvestNearDroneEaters`) | `drones.tres` |
| How fast a Goo drains a touching drone's health (`DroneDrainPerSecond`) | the Goo's file; it grows over the fight with the Goo's damage values in `difficulty.tres` |
| Each enemy type's group size | each enemy's file, now including the basic enemy's, which moved there from `enemy_spawning.tres` |
| How much each absorbed drone makes a Goo grow and toughen, and how many it absorbs before splitting | the Goo's file |
| The Goo's debris mass, and how much each absorbed drone adds to it | the Goo's file |
| How quickly the Goo grows stronger during the fight and per escalation step | `difficulty.tres`, beside the basic enemy's values |
| The weight of each enemy type when a group spawns, and in waves | `enemy_spawning.tres` |
| Each asteroid amount's number of fields and lone asteroids (`FieldScale`, `LoneScale`), and its weight | its file in `tuning/asteroid_amounts/`, listed in `asteroids.tres` |
| The Orbital Shards harvesting conversion rate (`HarvestPerDamage`) | `weapons/orbital_shards/orbital_shards.tres`, with the shards' other numbers |

## Not yet decided

Nothing is open. The items that were open here were settled by the designer when the phase closed (see Settled during the build).

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

These were open when this file was written, or came up during the build. The prototype now does the following; any of them can still change.

- **Settled when the phase closed:** the designer kept every default that was listed as not yet decided:
  - **Splitting:** a Goo splits after absorbing a set number of drones, into two Goos that share its size and health.
  - **The Goo's targets:** the nearest drone first, and the VNP only when no drone is in range.
  - **Mixing:** each spawned group is one type, picked by weight. Waves have both.
  - **Kill target:** every kill counts as one, whichever type it is. Goos made by splitting count as separate kills when destroyed; the split itself is not a kill.
  - **Overkill:** left out of the damage breakdown.
- **One Drone Weapon choice per fight:** at the designer's request, once a Drone Weapon (Projectile, Beam or Ram) is chosen at a level up, no Drone Weapon is offered again for the rest of the fight. The Drone Weapon the fight starts with doesn't count, including one picked with the starting weapon cheat, so switches are offered until one is taken. `OneDroneWeaponChoicePerFight` in `levels.tres` turns the rule off. This changes phase 2's upgrade pool, where drones could switch whenever another Drone Weapon was offered; its table is updated. In 20 automated fights with the Random build, none took more than one Drone Weapon, or was offered one after choosing.

- **Drones avoiding danger:** added at the designer's request after their playtests found drones did fine against a few Goos but, against many, locked onto one and flew into the others. The designer chose basic, imperfect avoidance, of all enemies, Ram included. Over 720 automated fights, 240 with each of Projectile, Beam and Ram drones, half of them the same fights without it (variant `no_drone_avoidance`): drones absorbed per minute fell from 11.2 to 7.9, about twice as many drones were alive at 2:00, and the pilot won 10 to 18 points more often. Drones destroyed by basic enemies rose, because drones lived longer. It cost no measurable frame time.
- **Asteroid amounts (stage 3):** `AsteroidAmountDefinition`, in `tuning/`, with Sparse, Normal and Rich in `tuning/asteroid_amounts/`, listed in `Amounts` in `asteroids.tres`.
  - **What an amount sets:** it scales the numbers in `asteroids.tres` rather than replacing them: `FieldScale` multiplies how many fields there are and `LoneScale` how many lone asteroids, both around the gateway and in the cells filled in as the VNP explores, so the arena stays as sparse or as rich however far it goes. Normal scales by 1, Sparse well below and Rich well above (their files hold the numbers). Field sizes and asteroid masses are unchanged. Fields keep their spacing, so a very rich amount can ask for more fields than fit; at Rich nearly all are placed.
  - **The pick:** each fight picks one by `Weight`, all equal for now (the spec left the weights open; equal weights give each amount the same exposure while its effect is measured). Adding amounts left a fight with the Normal amount playing exactly as before.
  - **Shown:** the result screen has an Asteroids line above the seed.
  - **The cheat page:** the starting choice menu ends with CHOOSE THE ASTEROID AMOUNT: Random first, keeping the fight's own pick, then each amount. Choosing one places the asteroids again for it, before the fight starts. `ChooseAsteroidAmount` in `scripts/cheats/in_game_cheats.tres` turns the page on and off, separately from `ChooseStartingWeapon`.
  - **First measurement,** with Sparse at half and Rich at about one and a half times Normal: 50 expert fights on each amount (Designer build, random starting weapons, the same seeds):

    | Amount | Wins | Level at 3:00 | Final level | Asteroid experience | Drones built | Alive at 5:00 |
    |---|---|---|---|---|---|---|
    | Sparse | 64% | 11.5 | 13.4 | 3,500 | 62 | 28 |
    | Normal | 76% | 14.7 | 17.8 | 5,300 | 100 | 22 |
    | Rich | 92% | 16.4 | 20.7 | 7,500 | 124 | 32 |

    The amount clearly sets how fast the VNP grows: about three levels between each amount by 3:00, and twice the drones from Sparse to Rich, with debris experience following the same way. Wins follow. Alive at 5:00 doesn't line up (Sparse above Normal); with 50 fights each, a difference of that size can be chance. Rich costs some frame time from the extra asteroids: game logic averaged 4.1 ms a frame against 2.8 for Normal and 1.9 for Sparse, well within a frame.
  - **Wider apart:** at the designer's request, Sparse was made sparser and Rich richer, to about a third and about twice Normal, about 60 to 80 asteroids around the gateway for Sparse, 140 to 200 for Normal and 320 to 440 for Rich. Over 50 expert fights each on the same seeds: Sparse won 58%, level 10.2 at 3:00 and 11.8 at the end, 54 drones built; Rich won 86%, level 17.0 at 3:00 and 22.7 at the end, 139 drones built. Rich's game logic stayed well within a frame.
- **Orbital Shards harvesting (stage 4):** `weapons/orbital_shards/upgrades/harvesting.tres`, unlocking the new weapon feature `Harvesting`, as blocking does. The pool has 39 upgrades.
  - **How it harvests:** each shard harvests an asteroid or piece of debris it touches once per the weapon's cooldown, as it hits an enemy, which in practice is once each time it passes. Each pass draws out its damage times `HarvestPerDamage` in mass. So damage upgrades harvest faster, and speed and count upgrades pass more often, while a rock still lasts several passes. The harvest goes through the same path as a beam's: fight experience, and progress toward temporary drones, from asteroids or debris as usual. Harvest rate upgrades don't change it, since they speed up beams.
  - **Where the rate lives:** the spec put the conversion rate in the upgrade's file; it is in the weapon's file instead (`HarvestPerDamage` in `orbital_shards.tres`), beside the shards' damage it converts, since an unlock upgrade carries no numbers of its own. Its starting value gives each rock in the shards' path about a quarter of the VNP's own beam rate at starting damage; it is a guess to tune by playing.
  - **Feedback:** each pass that harvests throws a small burst in the fight experience colour at the shard, and the material glows with the usual harvest tint. In a screenshot the burst is small beside the VNP's own beam on the same rock, so the clearest sign is rocks inside the orbit glowing; no new sound.
  - **One feature per weapon:** at the designer's request, a weapon's features are a choice: once Orbital Shards blocking or harvesting is taken, the other is no longer offered for the rest of the fight. This applies to every weapon's features, so future ones work the same way. `OneFeaturePerWeapon` in `levels.tres` turns it off. The card says so: under its description, in its category's colour, "Other Orbital Shards mods will not be offered this fight" (shown when the weapon has other features in the pool; cards call weapon features mods). At the same time, Drone Weapon cards got the same kind of note for the one-Drone-Weapon rule: "Other drone weapons will not be offered this fight." Each note shows only while its rule is on. A mod's card is also tagged VNP WEAPON MOD rather than VNP WEAPON at the top, in the same colour. This changes phase 2's blocking, which could be taken alongside anything. In 15 expert fights starting with Orbital Shards, none was offered a second feature after taking one.
  - **First sign of its strength:** in one automated expert fight starting with Orbital Shards, taking it at the start gave about half again as much asteroid experience, twice the drones built, and three levels more than the same fight without it. One fight is only a sign.
- **Absorbing:** a Goo drains a touching drone's health, and absorbs it when its health runs out (see The Goo). The first build absorbed drones instantly on touch, which felt bad in playtesting. A short contact time before absorbing was also considered; draining was chosen because it shows the drone being eaten, and a drone that escapes stays damaged.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

Stages 1 and 2 are built, stage 2 as part of automated playtesting's fight statistics (`docs/dev/AUTOMATED_PLAYTESTING_SPEC.md`). The phase was paused for automated playtesting and run review (`docs/dev/RUN_REVIEW_SPEC.md`), and resumed with stages 3 and 4, now built.

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

**The designer's verdict, after playing every stage:** the phase is done.

**What the phase found,** against its questions:

- **Does the second enemy type call for different upgrade picks?** Somewhat. Ram drones aren't always a good pick against Goos, though still fairly effective. One Goo type is too few to tell: the designer wants the rest of the GDD's Goo types (such as the asteroid mimic, spiker, burster, devourer, lancer and quiller) fleshed out to get a real feel for the faction.
- **With both types in one fight, does the player balance the build rather than specialize?** Not settled, for the same reason.
- **Does the damage breakdown show clear differences?** Yes: it is a good addition and serves its purpose.
- **How much does the amount of asteroids change a fight's difficulty?** A lot: from Sparse to Rich, the VNP grows about three levels faster by 3:00 at each step, and builds about twice the drones (see Asteroid amounts, under Settled during the build).
- **Does performance hold with the new enemy in large numbers?** Yes: in a stress test with twelve groups of Goos absorbing and splitting on 80 extra drones, alongside basic enemies, game logic averaged under 1 ms a frame, with the worst frame within one frame's time.

- **The Goo against drones:** it plays differently. The Goos themselves don't feel threatening to the VNP at all, as the GDD intends, but approaching a larger group of them takes special care, since that is what gets the drones killed.
- **Orbital Shards harvesting:** it feels fine at its current conversion rate, for now.
