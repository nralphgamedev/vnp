# VNP Combat Prototype, Phase 4: Grey Goo

Phase 3 (`docs/design/PROTOTYPE_3_SPEC.md`) added a second enemy type, the Goo, mixed into the same fight as the basic enemy. Its main question was whether a second enemy type calls for different upgrade picks, and the answer was "somewhat": one Goo type was too few to tell. Phase 4 adds the GDD's other Grey Goo types, so the faction can be judged as a whole.

This phase focuses only on the Grey Goo faction. Fights spawn Grey Goo and nothing else, so the basic enemy is no longer used (see Spawning).

Everything in the phase 1, 2, and 3 specs still applies unless this file changes it. Where they disagree, the newest file wins.

## What this phase is for

- Does a fight against Grey Goo alone call for different builds than the fights in phases 1 to 3?
- Does each type pressure something different (the VNP, the drones, defenses, the economy, harvesting), and can the player tell the types apart at a glance?
- Do the existing weapons and upgrades give a good answer to each type, or is a counter missing?
- With splits producing other types, do fights against Grey Goo change as they go on?
- Does performance hold, within the requirement in CLAUDE.md, with every type in large numbers?

## The Grey Goo types

The phase 3 Goo becomes the **basic goo**, the most common type. Phase 4 adds six more. Each is its own enemy type, with its own ID, folder, and definition.

**Folders.** The Grey Goo types are related, so group them under a parent folder: `enemies/grey_goo/`, with code they share there and each type in its own folder below it (`basic_goo/`, `lancer/`, `quiller/`, `spiker/`, `burster/`, `devourer/`, `asteroid_mimic/`). This moves the phase 3 Goo from `enemies/goo/` and renames its ID to `basic_goo`. Follow CLAUDE.md's rules for moving files.

**Shared traits.** Unless its section says otherwise, every Grey Goo type:

- looks like Grey Goo: one satin grey shared by every type, darker than the asteroids, with a faint light at its edges, and a silhouette of its own, so each type can be told apart from the others and from asteroids;
- grows stronger over the fight and with each escalation step, as the basic goo does;
- leaves debris when destroyed;
- counts as one kill toward the kill target;
- damages the VNP by touching it, once per touch with a cooldown, as the basic goo does. Only the basic goo, the Devourer and an awake asteroid mimic drain and absorb drones that touch them; the other types reach drones only through their attacks;
- declares its threats for pilots: its body, its traits, and the danger shapes of its attacks once their tells are visible (see Pilots);
- is steered clear of by drones that aren't attacking it, as every enemy is;
- shows in the damage breakdown as its own enemy type.

### Basic goo (existing)

Unchanged from phase 3, except for splitting and what it goes after:

- **Only the VNP (changed by the designer):** it goes after the VNP only, rather than the nearest drone first. It still drains and absorbs drones that touch it.

- **Splitting into other types:** each time a basic goo splits, there is about a one in three chance that one of the two halves is one of the other six types instead of another basic goo. The type is picked completely at random, each equally likely. The one in three chance was set by the designer and is a tuning value.
- **The other type is fresh:** a half that becomes another type is a new one of that type, at the fight's current difficulty, rather than taking a share of what the basic goo had grown. The basic goo half keeps its share as usual.

### Lancer

- Takes a short time to form itself into a spike, then shoots toward the VNP. The forming is clearly visible, so the player has a moment to see it coming and get out of the way.
- It aims a little ahead of a moving VNP. How well it aims rises with the fight's difficulty, in the same way the basic enemy's accuracy rose with time spent in the fight: early on it leads poorly and strays, and later it leads well.
- It damages the VNP if it hits, and hits any drones in its path as well, damaging them without absorbing them. It carries on through what it hits, damaging each target once per launch.
- After each launch, it turns back into a blob, then forms a spike and launches again.
- Between launches, it drifts after the VNP.

### Quiller

- Forms spikes and fires them in a spread, like a shotgun, so one volley can hit the VNP and several drones at once. Its many spikes are shared out among several targets, the VNP and the drones within its range, rather than all aimed at one.
- Each spike then arcs back to the main blob and is reabsorbed. It can hit things on the way back as well.
- Destroying a spike before it returns weakens the blob: the Quiller loses a set amount of health for each spike it doesn't get back.
- A spike is destroyed by Orbital Shards blocking or by any damage that touches it, though weapons don't aim at spikes. That is: Orbital Shards, Shockwave rings, mine blasts, ramming drones, and the VNP's and drones' shots that fly into it. Beams only hit what they aim at, so they don't hit spikes.
- **Only blocking and explosives (changed by the designer):** a spike cannot be damaged by weapons unless the weapon says it can: Orbital Shards with blocking, and explosive weapons (Mine Layer and Seeker Missiles now, and any explosive weapon later). Shockwave rings, ramming drones and the VNP's and drones' shots pass through spikes. The same goes for the Burster's tiny blobs.
- **Stays at range (changed by the designer):** between volleys it keeps a set distance from the VNP, inside its volley range, rather than closing in to touch it.

### Spiker

- Attacks by pushing out many small spikes at once, each of which can impale a drone, so one attack can catch several drones. It then draws the impaled drones back into its mass and absorbs them. An absorbed drone counts as absorbed in the damage breakdown, as with the basic goo.
- An impaled drone stops fighting while it is drawn in.
- Each drone it absorbs makes it larger and tougher, as with the basic goo, but it never splits.
- Its spikes go out toward every drone within its reach, and toward the VNP if it is within reach too. They damage the VNP but cannot impale it.
- **Moves after the VNP, spikes at drones (changed by the designer):** it moves after the VNP only, hurting it by touch, while its spikes favour drones: the nearest drones in reach first, and the VNP only when no drone is in reach.
- If the Spiker is destroyed before it draws its impaled drones in, they are freed.

### Burster

- It moves faster than the other Grey Goo types, a little faster since it is one of the weaker types.
- When destroyed, it splits into tiny blobs that arc toward the VNP.
- The tiny blobs damage what they hit and can be destroyed on the way, by the same things that destroy a Quiller's spikes.
- **Explosive blobs (changed by the designer):** a blob explodes as it pops, hurting the VNP and every drone near it, never enemies.

### Devourer

- Slowly consumes anything harvestable, both asteroids and debris, growing as it does. It never splits, so it can become a very large blob, and it competes with the player for harvestable material.
- Its growth has a limit, but a large one.
- The more it has consumed, the more its debris is worth, so destroying a grown Devourer wins back some of what it took.
- **Producing (added by the designer):** once it has consumed enough mass, it produces a basic goo, pushed out toward the player at a raised speed, after which the basic goo moves as usual. Producing has a cooldown and a mass requirement, and costs the Devourer the mass it spends.
- **Stores, then attacks (added by the designer):** it stores its mass as it harvests, growing, and produces only when the player comes close, so it spends its mass only when attacking.

### Asteroid mimic

- Looks like an asteroid and takes an asteroid's place in a field. It isn't spawned around the VNP like the other types; mimics are placed in fields instead.
- **Where mimics are:** mimics come in groups. Some asteroid fields, clusters and rings alike, hold several mimics among their asteroids, while every other field is normal and holds none. This applies both around the gateway and as the VNP explores, so a fight rich in asteroids, with more fields, has more chances of a field with mimics.
- It stays inactive until the VNP comes within its wake range, then wakes; drones coming near don't wake it (changed by the designer). A mimic made by a basic goo's split appears already awake.
- If a drone or the VNP tries to harvest it first, it wakes, takes a little damage from the attempt, and gives no harvest material.
- Waking is clear and sudden, with its own effect and sound, so the player sees what happened.
- **Once awake:** it acts like a basic goo, draining and absorbing drones, but never splits. It can also fire a single projectile at the nearest of the VNP or a drone. As an enemy shot, Orbital Shards blocking can stop it.

## Spawning

- In this phase, fights spawn only Grey Goo. The basic enemy from phase 1 no longer spawns, but its files stay, so it can be brought back by giving it a spawn weight again.
- Each spawned group is one Grey Goo type, picked by weight. The basic goo has the highest weight, so it stays the most common type.
- The Lancer, Quiller, Spiker, Burster, and Devourer spawn in groups like the basic goo, each with its own weight and group size. Asteroid mimics are placed in asteroid fields instead, as described above.
- Waves can include every type that spawns in groups, except the Devourer: at the designer's request, neither Devourers nor asteroid mimics ever come in waves (their wave weights in `enemy_spawning.tres` are 0).

With the basic enemy gone, the only enemy shots are the Quiller's spikes and the mimic's projectile. Whether Orbital Shards blocking is still worth taking is part of what this phase tests.

## Look and feedback

Each type needs a clear tell for what it is about to do:

- **Lancer:** a visible charge-up as it forms its spike, so the launch can be dodged.
- **Quiller:** its spikes visibly arc back to the blob, so the player can see that destroying them matters.
- **Spiker:** the spikes shooting out, and impaled drones visibly drawn in.
- **Burster:** something about its look that warns it will burst, such as a swollen, bubbling body.
- **Devourer:** its size, and material visibly drawn in from what it consumes.
- **Asteroid mimic:** no tell while asleep, since that is its point, but a clear wake effect.

New placeholder sounds for each type's attack, the Burster bursting, the Devourer consuming, and a mimic waking. As always, they go in `res://placeholder/` and are listed in `PLACEHOLDERS.md`.

## Pilots

Batches only tell something about a Grey Goo type if pilots react to it: dodging a forming Lancer, shooting down returning spikes. The full game will have dozens of enemy types, each with its own thematic attacks and behaviors, so pilots need a way to handle enemy types that grows with them rather than code for each one.

So pilots are rebuilt before the Lancer (stage 2), as `docs/dev/PILOT_SPEC.md` describes:

- **Declared threats:** every enemy type declares its body, its traits, and the danger shapes of its attacks once their tells are visible.
- **Short paths:** the pilot scores short paths through those threats, so a new type needs no pilot code.
- **Fair sight:** pilots still see only what a player sees, so a sleeping asteroid mimic is an asteroid to them.

**Two pilots for comparison:** batches can then compare two pilots, one trained to play like the designer and one trained to play as well as possible. The gap shows the difficulty headroom, and the builds that win far more often in the best hands show where the balance breaks.

## Tuning values

As in the earlier specs, this file names each value and where it lives rather than its number.

| Value | Where |
|---|---|
| What every Grey Goo type shares: contact damage and its cooldown, how fast it drains drones (`DroneDrainPerSecond`, 0 for a type that doesn't drain), growth per absorbed drone (`BulkPerDrone`), debris per absorbed drone (`DebrisMassPerDrone`), and its wobble | each type's own file, from `GreyGooDefinition` in `enemies/grey_goo/` |
| The basic goo's split: drones absorbed before it splits, how long it bulges, how fast the halves are pushed apart (`SplitPushSpeed`), and the chance the second half is another type (`OtherTypeChance`, about one in three, set by the designer) | `enemies/grey_goo/basic_goo/basic_goo.tres` |
| How quickly Grey Goo grows stronger during the fight and per escalation step, shared by every type (`RampGreyGooHealthPerMinute`, `RampGreyGooDamagePerMinute`, `EscalationGreyGooHealthPerStep`, `EscalationGreyGooDamagePerStep`) | `tuning/difficulty.tres` |
| Each type's spawn weight, for groups and for waves; the basic enemy's is 0 | `tuning/enemy_spawning.tres` |
| How far a Grey Goo type keeps from its target while chasing (`HoldDistance`, from its edge, 0 to close in all the way) and how gently it blends closing in and backing off (`HoldBlendDistance`): set for the Lancer and the Quiller | each type's file, from `GreyGooDefinition` |
| Seeker Missiles' explosion: how far it reaches (`BlastRadius`), widened by its area of effect upgrade | `weapons/seeker_missiles/seeker_missiles.tres` |
| The Lancer's launch: the range it starts forming at, how long it forms, its speed, how far it carries on past the point it aimed at (`LaunchBeyond`), its damage, how long it recovers, its cooldown between launches | `enemies/grey_goo/lancer/lancer.tres` |
| The Lancer's aim: how rare accurate ones are, how far ahead it leads, and how much it strays, from least to most accurate | `lancer.tres`; the difficulty ramp's accuracy in `difficulty.tres` raises it, as for the basic enemy |
| The Lancer's look while forming: how far it stretches and how much paler it glows | `lancer.tres` |
| The Quiller's volley: the range it fires within, how long it forms, spikes per volley, how far each strays, its cooldown | `enemies/grey_goo/quiller/quiller.tres` |
| Which weapons break the Quiller's spikes and the Burster's blobs: `Explosive` in each VNP Weapon's file (on for Mine Layer and Seeker Missiles); Orbital Shards break them only with blocking | each weapon's file in `weapons/` |
| The Quiller's spikes: speed and turn rate going out (`OutTurnDegrees`), how far past its target point each flies (`SpikeBeyond`), speed and turn rate coming back, damage, size, and the health it loses for each one broken | `quiller.tres` |
| The Quiller's look while forming: how much it swells and how much paler it glows | `quiller.tres` |
| The Spiker's thrust: its reach from its edge, how long it forms, how many spikes one thrust has (`MinSpikes` to `MaxSpikes`), its cooldown (`ThrustCooldown`) | `enemies/grey_goo/spiker/spiker.tres` |
| The Spiker's aim: how rare accurate ones are, how far ahead of its target it leads, and how much it strays, from least to most accurate, and how fast a spike at a drone swings toward it (`TrackDegrees`) | `spiker.tres`; the difficulty ramp's accuracy in `difficulty.tres` raises it, as for the Lancer |
| The Spiker's spikes: how fast they shoot out, how far past the target's point they reach (`SpikeBeyond`), how fast an impaled drone is drawn in (`PullSpeed`), damage to the VNP, width | `spiker.tres` |
| The Spiker's growth per drone it absorbs, and debris per drone (`BulkPerDrone`, `DebrisMassPerDrone`), and its look while forming | `spiker.tres` |
| The Burster's speed, and its burst: how many blobs, their speed, turn rate toward the VNP, life, damage, size, and how far each one's explosion reaches (`BlobBlastRadius`) | `enemies/grey_goo/burster/burster.tres` |
| The Burster's throb: how much it swells, how fast, and how much faster when nearly destroyed (`HurtThrobFactor`) | `burster.tres` |
| The Devourer's consuming: how far it looks for material, how fast it consumes, growth per mass, its growth limit (`MaxBulk`), and the share of what it consumed added to its debris | `enemies/grey_goo/devourer/devourer.tres` |
| The Devourer's producing: what it produces (`Produces`, the basic goo), how near the VNP must be (`AttackRange`, from its edge), the stored mass each costs (`ProduceMass`), its cooldown (`ProduceCooldown`), and how fast the produced goo is pushed toward the VNP (`ProducePushSpeed`) | `devourer.tres` |
| Where asteroid mimics hide: the chance a field holds them, and how many it holds | `enemies/grey_goo/asteroid_mimic/asteroid_mimic.tres` |
| The asteroid mimic's waking: how close the VNP wakes it (`WakeRange`), and the share of its health a harvesting attempt costs it (`WakeDamageFraction`). Awake, its aggro ranges work as every enemy's | `asteroid_mimic.tres` |
| The asteroid mimic's shot: range, cooldown, damage, speed, how long it flies, colour, size, and how far it leads | `asteroid_mimic.tres` |
| How much faster newly spawned enemies get each minute of the fight, every type (`RampEnemySpeedPerMinute`) | `tuning/difficulty.tres` |
| How much faster newly spawned enemies are at each escalation step, every type (`EscalationEnemySpeedPerStep`) | `tuning/difficulty.tres` |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Drones and growth for every type:** whether drones a Grey Goo type destroys without absorbing them, such as with a Lancer hit or a Quiller spike, should add to its mass and health, and whether every type, not only the basic goo, should split once it reaches a threshold. For now, drones destroyed without being absorbed add nothing, and only the basic goo splits. This also decides how much each goo's debris is worth, since a goo's mass sets the value of its debris: the more a goo's mass can grow, the more destroying it gives back.
- **The Burster's tiny blobs:** they head for the VNP and pop on whatever they hit first, the VNP or a drone, exploding as they do (see Explosive blobs, under Settled during the build). They leave no debris and don't count as kills. (Built this way in stage 6.)
- **The Devourer and drones:** it drains and absorbs drones that touch it, like the basic goo, and grows from them as well as from what it consumes. (Built this way in stage 7.)


For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

The prototype now does the following; any of it can still change.

- **The Grey Goo folder (stage 1):**
  - **Shared code:** `enemies/grey_goo/` holds `GreyGooDefinition` and `GreyGoo`, what every type shares:
    - heading straight for its target;
    - touch damage to the VNP, with its cooldown;
    - draining and absorbing drones, for a type whose `DroneDrainPerSecond` is above 0;
    - growing larger and tougher with each absorbed drone;
    - more debris for what it absorbed;
    - the wobbling, glossy blob, with the swell on absorbing.

    A type overrides only its own part.
  - **The basic goo:** the phase 3 Goo is now the basic goo, in `enemies/grey_goo/basic_goo/` (`BasicGoo`, `BasicGooDefinition`, `basic_goo.tres`), with the ID `basic_goo` and the name Basic Goo. Its own part is splitting.
  - **Nothing changed in play:** two automated fights traced identically before and after the move, with the basic enemy still spawning: one an ordinary fight, one with many Goos absorbing and splitting.
- **Grey Goo difficulty:** the Goo's difficulty values in `difficulty.tres` are now the Grey Goo's, shared by every type, and renamed to match. Their numbers are unchanged.
- **Grey Goo only:** the basic enemy's spawn weights are 0 in `enemy_spawning.tres`, for groups and waves. Its files and its entry stay, so a weight brings it back, and the scenario harness can still spawn it (`spawn=basic_enemy:N`).
- **Pilots that plan (stage 2):** built as `docs/dev/PILOT_SPEC.md` describes, and now flying every pilot.
  - **Results:** over the same 50 seeds, fights of Grey Goo only were won 52% of the time instead of 40%, and none of its pilots were destroyed by the 8:00 cap, against 15. Touching a Goo fell from 0.50 to 0.07 times a minute.
  - **Closer to the designer later:** it outlives the designer's own fights by far more than the first pilot did. At the designer's choice, closing that gap waits for calibrating it against their recorded Grey Goo fights; until then, batches read the game as easier than it plays, though comparisons between options still hold.
- **The Lancer (stage 3):** `enemies/grey_goo/lancer/`, `Lancer` on the shared Grey Goo code. It drains no drones and doesn't grow.
  - **Its cycle:**
    - **Drifting:** a teardrop-shaped blob, drifting after the VNP with its point turned toward it once it has noticed the VNP. Before that it wanders and faces where it goes, as other enemies do; at first it faced the VNP even while idle, which the designer noticed.
    - **Forming:** once the VNP is within its launch range and its cooldown is done, it stops and forms into a spike over `FormSeconds`, stretching and glowing paler, with a rising whine. The whine stops when it launches, or if it is destroyed while forming; at first it played on after the Lancer died, which the designer noticed.
    - **Launching:** it shoots along its line with a whoosh, carrying on through what it meets and past the point it aimed at by `LaunchBeyond`, and damages the VNP and each drone in its path once (`LaunchDamage`, which grows with the Grey Goo damage ramp).

      **Changed after playtesting:** at the designer's request, it flies farther beyond its target and twice as fast. Before, each launch covered a fixed distance from where it started, which overshot a target at the edge of its launch range by only a few units.
    - **Recovering:** it turns back into a blob, then drifts until its next launch. A group's first launches are spread out.
  - **Aim:** its aim is set as it starts forming, so moving off the line dodges it. (The spec didn't say whether it keeps tracking while forming; set at the start gives the player the warning the spec asks for.) It leads ahead of a moving VNP by its accuracy, which it rolls as it spawns and the difficulty ramp raises, as the basic enemy does: sloppy ones aim nearer where the VNP is and stray more.
  - **Touch:** it damages the VNP by touch like every Grey Goo type, except during a launch, when the launch itself is what hits.
  - **Spawning:** in groups and waves, by its weight in `enemy_spawning.tres`, less often than the basic goo.
  - **Seen by pilots:** while forming and launching, it declares its line, landing when the launch does. In a 90-second test against four groups of Lancers, the planning pilot took no damage from them and lost 2 drones to them. The first pilot, with no code for Lancers either, took 42 damage and lost 6 drones.
  - **Cost:** about 2.6 µs an enemy alone, against 2.4 for the basic goo (about 1,000 each); every type mixed, about 3.3.
  - **Shared code it added:**
    - attack fields on enemies (`AttackPhase`, `AttackTimer`, `AttackFrom`, `AttackDirection`, `AttackDamage`), for the next types' attacks;
    - `Spins` and `TouchHurts` on Grey Goo;
    - `DroneSwarm.HitAlong`, which damages drones along a path once each;
    - the `Launches` threat tag.
  - **The full recording check** now also requires an attack declared before it lands.
- **The Quiller (stage 4):** `enemies/grey_goo/quiller/`, `Quiller` on the shared Grey Goo code. It drains no drones and doesn't grow.
  - **Its volley:**
    - **Drifting:** an urchin-like blob, quills round its middle, drifting after the VNP.
    - **Forming:** once the VNP is within its volley range and its cooldown is done, it stops, swells and glows paler. Each spike's target point is set as it starts: where the VNP is, each point straying a little round the Quiller, so the volley spreads.
    - **Firing:** with a rattle, the spikes leave from every side, evenly round it, and curl in on their target points (`OutTurnDegrees`), so one launched away from its target sweeps round in an arc. Each turns back as soon as it hits the VNP or a drone (damaging it), or once it has flown `SpikeBeyond` past its target point, then arcs home, turning at a limited rate, and is taken back in. On the way back it can hit again.
    - It forms its next volley only once all of the last are back or broken, and its cooldown is done.
  - **Breaking spikes:** any damage that touches a spike breaks it: Orbital Shards (blocking or not), Shockwave rings, mine blasts, charging Ram drones, and the VNP's and drones' shots and Seeker Missiles, which are used up doing it. Beams don't, since they only hit what they aim at. Each spike broken before it gets home costs its Quiller `SpikeLossHealth`, credited to whatever broke it, with a glassy tink. A spike whose Quiller is gone fizzles out.
  - **Only blocking and explosives (after playtesting):** at the designer's request, only Orbital Shards with blocking and weapons marked `Explosive` in their file (Mine Layer's blasts and Seeker Missiles) break fragile shots, the Quiller's spikes and the Burster's blobs alike; a shot of the VNP or drones now flies through them, and Shockwave rings and ramming drones pass over them. A new weapon breaks them only if its file marks it `Explosive`. This changes the breaking described above.
  - **From every side (after playtesting):** at the designer's request, spikes leave from every side and curl in on their targets, rather than firing toward them in a spread, and each flies a set distance past its target point. (Before that, each spike's reach was its target's distance plus a margin, also at the designer's choice; at first every spike had the same reach, which fell short of targets at the edge of the volley range.)
  - **Only the VNP, and faster (after playtesting):** at the designer's request, its volleys aim only at the VNP, and drones are hit only when a spike passes through them. This changes the spec above, which shared its spikes among the VNP and drones in range. It also moves faster, now quicker than the basic goo and the Lancer (`Speed` and `WanderSpeed` in `quiller.tres`).
  - **Seen by pilots while forming:** since spikes curve, the Quiller declares a small circle at each target point, landing about when its spike should arrive, rather than a line.
  - **Fragile shots:** spikes fly through a new shared system, `FragileShots` (`scripts/units/`), for enemy shots the player's weapons can break; the Burster's blobs are meant to use it too.
  - **Seen by pilots:** while forming, it declares each spike's target point (see below); spikes in flight declare themselves. Against four groups of Quillers for a minute, the planning pilot took 10 to 25 damage from them, the first pilot 25 to 60. They destroyed 15 to 20 drones a minute in those tests, a sign they may be strong against drones.
  - **Cost:** about 2.7 µs an enemy for 1,000 Quillers, and about 0.3 ms for their spikes. The spikes needed care for a crowd: a spatial grid, positions found once a frame, and counts kept as spikes come and go. Looking each up by scanning the enemies at first cost 13 ms a frame.
  - **Spawning:** in groups and waves, by its weight in `enemy_spawning.tres`, less often than the basic goo.
  - **Recorded:** spikes are recorded as enemy shots, and their hazards with the rest.
- **The Spiker (stage 5):** `enemies/grey_goo/spiker/`, `Spiker` on the shared Grey Goo code. It drains no drones by touch, but grows larger and tougher with each drone its spikes draw in, and never splits.
  - **Its thrust:**
    - **Drifting:** a blob drawn out into four broad arms, like a caltrop, drifting after its target.
    - **Forming:** once a drone or the VNP is within its reach of its edge and its cooldown is done, it stops, swells and glows paler, and picks its target: the nearest drone in reach that no other spike is after, or the VNP if in reach and no drone is. (`MinSpikes` and `MaxSpikes` set how many spikes a thrust has, picked at random between them; both are 2 at the designer's choice, after a while at 1.) A spike at the VNP has its line set now, so moving off it dodges.
    - **Thrusting:** with a stab, the spikes shoot out, a little past each target. A spike at a drone is aimed again as it goes, ahead of where the drone is heading (`AimLead`), and swings toward it as it extends (`TrackDegrees`), so it rarely misses; if its drone is gone or already caught, it goes for another. The first drone a spike's tip meets is impaled: it stops fighting and harvesting, and is drawn in at `PullSpeed`, then absorbed, counted as absorbed in the damage breakdown. A spike that crosses the VNP damages it once and carries on, since the VNP can't be impaled. An empty spike comes straight back.
    - It forms again once every spike is back in, and its cooldown is done.
  - **Freed:** if a Spiker is destroyed while drones are on its spikes, they are freed, with a small flash, and fight on. A drone on a spike can still be destroyed by other attacks.
  - **Changed after playtesting:** at the designer's request, it thrusts one spike at a time (at first one or two), aimed well at drones, rather than a spike at every drone and the VNP in reach at once; and it looks like a caltrop rather than an urchin, which was too like the Quiller. In four groups for a minute, its spikes caught the drone they were aimed at 95 to 100% of the time with Projectile and Beam drones, and about three times in four with Ram drones, which charge out of its reach. Before Spikers stopped going for the same drone, two-thirds of its misses were spikes whose drone another Spiker had already caught.
  - **Which drone is impaled:** the spec didn't say whether a spike can catch a drone other than the one it was aimed at. Any drone its tip meets first is impaled, aimed at or not, so drones flying across its line are caught too. A drone already on a spike can't be caught by another.
  - **Held drones, shared code:** `DroneSwarm` now has drones held by an enemy (`Hold`, `Release`, `AbsorbHeld`, `FindImpalable`): a held drone does nothing, and its holder moves it each frame. If the holder stops holding it, it is let go a moment later, so a drone can't be left stuck. Enemy types can also draw parts of their own beside their bodies (`EnemyBehavior.BeginDraw`, `EndDraw`), as the Spiker draws its spikes.
  - **Seen by pilots:** while forming, it declares each spike's line, landing as the thrust starts; while thrusting, what is left of each spike's way out. Both endanger the VNP and drones. Against four groups of Spikers for a minute, they drew in about 20 drones; with the first version, the planning pilot took no damage from them, the first pilot 6.
  - **Aim by accuracy (after the config review):** at the designer's request, it leads its target and strays by an accuracy it rolls as it spawns, which the difficulty ramp raises, as the Lancer does (`SharpshooterRarity`, `AimLeadMin` to `AimLeadMax`, `AimSpreadMin` to `AimSpreadMax`), in place of one `AimLead` and `AimSpreadDegrees`. A spike at the VNP leads it toward where it will be when the spike gets there, with its line set as the Spiker starts forming, so moving off it dodges; a spike at a drone is aimed as the thrust starts, and still swings toward it (`TrackDegrees`). A spike reaches no farther than its reach allows, however far ahead it aims. Its cooldown is now `ThrustCooldown`, named like the other types' cooldowns.
  - **Moves after the VNP, spikes at drones:** at the designer's request, it moves after the VNP only (`Targets`), so it reaches the VNP to hurt it by touch, while its spikes keep favouring drones, as above. (For a short while its spikes aimed only at the VNP too; drones were then caught only when a spike ran into them on the way.)
  - **Cost:** about 2.7 µs an enemy for 1,000 Spikers, like the other types.
  - **Spawning:** in groups and waves, by its weight in `enemy_spawning.tres`, less often than the basic goo.
- **The Burster (stage 6):** `enemies/grey_goo/burster/`, `Burster` on the shared Grey Goo code. It drains no drones and doesn't grow.
  - **Alive:** a swollen blob covered in bubbles, the fastest Grey Goo type (`Speed` in `burster.tres`; at the designer's request, a little faster than the other types, which since the speed change all move at the same speed). It throbs, swelling and shrinking, faster the more it is hurt, up to `HurtThrobFactor` times as fast when nearly destroyed: the tell that it is about to burst. Otherwise it only damages the VNP by touch, as every Grey Goo type does.
  - **Bursting:** when destroyed, with a wet pop, it throws `BlobCount` tiny blobs out evenly all round it, which curl in on the VNP at their turn rate. A blob pops on the first thing it hits, the VNP or a drone, or fizzles out after `BlobLife`. Only a Burster killed bursts; one left far behind and cleared away doesn't.
  - **Breaking blobs:** blobs fly as fragile shots, so everything that breaks a Quiller's spikes breaks them too. Breaking one costs no one health, since its Burster is gone.
  - **Faster than the VNP:** at first the blobs were slower than the VNP, and the planning pilot outran every one: in a minute against four groups, 31 of its 90 blobs fizzled out chasing it, and it took no damage from Bursters. Now they are faster, so they are dodged by sidestepping, which their turn rate allows, rather than by flying away.
  - **Seen by pilots:** blobs declare themselves as they fly, harmful until they fizzle. The burst itself gives no warning before the Burster dies, but the player causes it. Against four groups of Bursters for a minute, the planning pilot took 18 to 36 damage from them, the first pilot 70 to 75. Blobs hit drones often, but rarely destroy one.
  - **Fragile shots, shared code:** `FragileShots.FireHoming` fires a shot that homes on the VNP and pops on its first hit, drawn as a little blob rather than a quill.
  - **Cost:** about 2.6 µs an enemy for 1,000 Bursters, and under 0.2 ms for their blobs while Shockwave killed them quickly.
  - **Spawning:** in groups and waves, by its weight in `enemy_spawning.tres`, less often than the basic goo.
- **The Devourer (stage 7):** `enemies/grey_goo/devourer/`, `Devourer` on the shared Grey Goo code. It drains and absorbs drones like the basic goo, and never splits.
  - **Look:** a broad, slow blob whose rim rolls in waves.
  - **Consuming:** it heads for the nearest asteroid or debris within `SeekRange` and draws mass out of whatever it touches, with motes of the material flowing into it. It makes no sound while it does (its gulp was removed at the designer's choice). None of it becomes fight experience, so it competes with the player for material. A new hook lets an enemy go about its own business instead of wandering (`EnemyBehavior.Roam`).
  - **It never chases (after playtesting):** at the designer's request, it never goes after the VNP or drones, even when they are close, when it is shot, or when it comes in a wave (`EnemyBehavior.Chases`); they are hurt only by touching it. Its aggro ranges are unused, and it isn't drawn darker as idle enemies are. This settles the open question of its targets: at first it chased the VNP and drones that came within a short aggro range. At the same time, its `SeekRange` was raised a lot, to just inside the distance from the VNP where material is kept findable (`ActiveRadius` in `asteroids.tres`), beyond which a longer range finds nothing more. It searches close by first, then farther, so it still heads for the nearest.
  - **Growing:** each unit of mass consumed adds `BulkPerMass`, and each drone absorbed `BulkPerDrone`, larger and tougher together, up to `MaxBulk`, a large limit. Growth with a limit is now shared Grey Goo code (`GreyGoo.Grow`), with no limit for the other types.
  - **No ripples (after playtesting):** at the designer's request, its rippling surface was taken out altogether. It was built first as the spec described: every so often a lump swelled at a random point of its edge as a warning, then lashed out and back, damaging drones and the VNP it caught. Now it hurts only by touch and by draining drones, like the basic goo.
  - **Debris:** a share of the mass it consumed is added to its debris (`DebrisShareOfConsumed`), as one large piece.
  - **Producing basic goos (after playtesting):** at the designer's request, while the VNP is within `AttackRange` of its edge, once `ProduceCooldown` has passed and it has `ProduceMass` stored, it produces a basic goo at its edge nearest the VNP and pushes it out toward the VNP at `ProducePushSpeed`, with a swell, the split effect and the split sound. The goo then slows to its own speed at its own acceleration and behaves as any basic goo: awake, it chases the VNP or drones if they are within its aggro range, and wanders otherwise. It is a new basic goo at the fight's current difficulty, and counts as a kill when destroyed.
    - **Stores, then attacks:** at the designer's request, it stores what it consumes as its growth, and spends it only while the VNP is near: each goo costs `ProduceMass` of the store, the Devourer shrinks by the growth that mass gave it (`ProduceMass` times `BulkPerMass`), never below its starting size, losing health by the same share, so a hurt Devourer stays as hurt and producing never destroys it; and the mass no longer counts toward its debris. So a Devourer left alone grows big, and one the VNP stays near shrinks as it fires out goos. In a test, Devourers grew to about three times their starting bulk before the VNP came near, then each goo took one starting bulk off. Once full, at `MaxBulk`, it stops consuming until producing makes room, so its store and its size always agree. (At first it produced on a cooldown wherever the VNP was, and didn't shrink.)
    - **Added after the update:** a Devourer produces during the enemies' own update, so the goo is queued and added once the update is done (`EnemySwarm.QueueSpawn`), as splits are.
  - **Seen by pilots:** its body, like every Grey Goo type.
  - **Tested:** eight roaming Devourers placed among the gateway's asteroids consumed 117 mass in 30 seconds, using up 25 pieces, and grew.
  - **Cost:** about 2.7 µs an enemy for 1,000 Devourers chasing, and about 2.5 for 1,000 roaming a rich field, consuming as they go.
  - **Spawning:** in groups of one, by its weight in `enemy_spawning.tres`, less often than the other types. Never in waves, at the designer's request (its wave weight is 0).
- **The asteroid mimic (stage 8):** `enemies/grey_goo/asteroid_mimic/`, `AsteroidMimic` on the shared Grey Goo code for when it is awake.
  - **Asleep, it is an asteroid:** a sleeping mimic isn't an enemy at all but one of a field's asteroids, marked as a mimic (`Harvestables`). It looks, moves with its field, records and is seen by pilots exactly as an asteroid, so neither a player nor a pilot can tell. As the spec intends, it has no tell.
  - **Where:** as each field is placed, around the gateway and as the VNP explores, it holds mimics with the mimic's `FieldChance`, between `PerFieldMin` and `PerFieldMax` of them; the rest hold none. They are picked by a generator of their own, seeded from where the field is, so the fight's own random numbers are untouched. In two rich fights, about a quarter of the fields held mimics, around 220 in all, of which the pilot woke 4 to 7 in three minutes.
  - **Waking:** when the VNP comes within its `WakeRange` (at the designer's request, drones coming near no longer wake it), or the VNP or a drone tries to harvest it, the rock bursts with a crack and a hiss, and the goo inside is left, already awake. A harvesting attempt also costs it `WakeDamageFraction` of its health and gives nothing. A Devourer touching one wakes it too, unhurt, rather than eating it. The waking mimic appears at the start of the next update, so an enemy is never added in the middle of the enemies' own update.
  - **Awake:** a lumpy rock in the goo's grey, darker than the asteroids. It drains and absorbs drones and grows like the basic goo, but never splits, and once every `FireCooldown` fires a single shot at the nearest of the VNP or a drone within `FireRange`. Its shot is an ordinary enemy shot, so Orbital Shards blocking stops it and it declares itself to pilots.
  - **Never spawned:** its weights in `enemy_spawning.tres` are 0, so it never comes in groups or waves; it is listed there so the fight knows the type. `EnemySwarm.SpawnAwake` adds one where it wakes.
  - **Cost:** about 2.5 µs an enemy for 1,000 awake mimics; checking sleeping ones for the VNP and drones costs nothing measurable.
- **Splitting into other types (stage 9):** when a basic goo splits, with its `OtherTypeChance` the second half is one of the other six Grey Goo types instead, each equally likely: a new one at the fight's current difficulty, awake, carrying on with the half's push apart. The basic goo half keeps its own share as usual; the other half's share of what it had grown is simply gone. A mimic made this way appears awake, as the spec asks, since every type made by a split does; a Devourer goes about its business, since it never chases.
  - **Which types:** every Grey Goo type in the fight other than the basic goo, found from the types themselves, so a new Grey Goo type joins the pick without a change here.
  - **Tested:** in three fights of twelve groups of basic goos with plenty of drones to absorb, 24 of 57 splits gave another type, 42%, as near one in three as so few splits can show, and every one of the six types appeared.
- **Less shine:** at the designer's request, the goo material is satin rather than glossy, with a fainter rim of light and glow (`materials/goo.tres`, made by the placeholder baker). It covers every Grey Goo type, and the Quiller's and Spiker's spikes and the Burster's blobs.
- **One grey for every type:** at the designer's request, every Grey Goo type is the same grey (`Color` in each type's file, all alike), noticeably darker than the asteroids, so the types are told apart by silhouette alone. Until then each type had a pale tint of its own (blue for the Quiller, cream for the Lancer and Spiker, green for the Burster), all paler than the asteroids, following phase 3's pale grey, chosen because a dull grey Goo was easy to mistake for an asteroid; the glossy rim light still sets them apart from rock. Since enemy types are coloured by their `Color` in the damage breakdown and on review maps, Grey Goo types now share a colour there too, and are told apart by name, and on maps by their markings and key.
- **A smaller Spiker:** at the designer's request, it is drawn a third smaller (`Size` in `spiker.tres`); how far it reaches and how big a target it is are unchanged.
- **Result panels scroll:** at the designer's request, after a long fight's damage breakdown ran off the bottom of the screen, the damage breakdown and the upgrade list (on the result screen and the pause menu) grow with their lines only until they would leave the screen, then scroll, by the mouse wheel or their scroll bar. The damage breakdown also stops above the weapon icons at the bottom left, which it used to run behind. Scrolling with a controller or the keyboard isn't built yet.
- **Sounds pause with the fight:** at the designer's request, while a menu pauses the fight (a level-up offer, the pause menu, the result screen, the starting weapon menu), the fight's own sounds pause too and carry on where they left off once it closes. Menu and result sounds still play. Before, every sound played on through a pause, so a Lancer's wind-up could finish while an offer was open. This changes phase 2's "Sounds while paused".
- **Escalation speeds enemies up:** at the designer's request, each escalation step past the time limit also makes newly spawned enemies faster, in steps like their health, damage and accuracy (`EscalationEnemySpeedPerStep` in `difficulty.tres`).
  - **Every type, chasing and wandering:** each enemy keeps the speed it spawned with, as it keeps its strength. Halves of a split keep their parent's.
  - **Before the limit:** nothing changes.
  - **Outrunning them:** enemies whose speed climbs past the VNP's can no longer be outrun. Since enemies near the VNP's speed (below), they pass it during the fight, before escalation begins.

  This changes phase 1's escalation, which made enemies tougher, hit harder, spawn faster and widened every enemy's aggro range, but left their speed alone.
- **Enemies near the VNP's speed:** at the designer's request, during the rework of how fights feel, every enemy that chases moves just behind the VNP's starting speed (`Speed` in each type's file, all alike, at 90% of the VNP's), rather than at a speed of its own well below it. Before, chase speeds ran from the basic enemy's, about two thirds of the VNP's, down to the Spiker's, about a quarter.
  - **Faster through the fight:** newly spawned enemies get faster each minute (`RampEnemySpeedPerMinute` in `difficulty.tres`), like their health and damage, and escalation past the limit stacks on top. Each keeps the speed it spawned with. At the designer's choice, enemies spawned near the time limit reach about the VNP's starting speed, so Movement speed upgrades keep the player ahead of them, and escalation past the limit then takes them beyond it. (At first the ramp was steeper, putting enemies spawned near the limit well past the VNP's starting speed.)
  - **Not the Devourer:** it never chases, so it keeps its slow speed as it heads for material.
  - **Not the asteroid mimic:** at the designer's request, after the config review, it keeps its own slow speed, as a lumbering rock.
  - **The Burster:** a little faster than the rest (see the Burster).
  - **Unchanged:** how fast they wander, and how quickly they reach full speed (`Acceleration`), so a standing enemy takes a moment to get going. The halves of a basic goo's split are pushed apart at the old speed (`SplitPushSpeed` in `basic_goo.tres`), which used its chase speed before. A Lancer still glides on at its chase speed after a launch, so it now glides further.
- **Quills come straight home:** at the designer's request, after quills looped round their Quiller on the way back, they turn back faster (`ReturnTurnDegrees` in `quiller.tres`), and once nearer home than twice their turning radius they head straight in, so a quill can no longer circle its Quiller.
- **The Lancer holds its distance:** at the designer's request, between launches it keeps `HoldDistance` from the VNP (from its edge, inside its launch range), closing in when farther and backing off when nearer, blended over `HoldBlendDistance`, so it waits in range ready to launch; before, it drifted straight at the VNP. It also grips harder than the other types (`Acceleration` in `lancer.tres`), so it slides less as it turns.
- **The Quiller stays at range too:** at the designer's request, it holds the same way between volleys, inside its volley range, rather than closing in to hurt the VNP by touch. Holding is now shared Grey Goo code (`HoldDistance` and `HoldBlendDistance` in `GreyGooDefinition`, `GreyGoo.Hold`): 0, for every other type, closes in all the way.
- **Explosive blobs:** at the designer's request, a Burster's blob explodes as it pops on the VNP or a drone: the VNP, if within `BlobBlastRadius`, and every drone within it take the blob's damage, with a small blast ring and a quieter mine blast sound. It never hurts enemies, and still can't break other fragile shots. A blob that runs out of life fizzles, as before. Pilots see each blob as a hazard the size of its explosion. In a 40-second pilot fight against four groups of Bursters, 63 blobs exploded: 21 caught the VNP, and several caught two drones at once.
- **Seeker Missiles explode:** at the designer's request, a missile now explodes where it hits, damaging every enemy within `BlastRadius` (in `seeker_missiles.tres`) as well as the one it hit, with a blast ring like a mine's; one that flies into a fragile enemy shot explodes there. A new upgrade, Seeker Missiles area of effect (`weapons/seeker_missiles/upgrades/area.tres`), widens it, named and sized like Mine Layer's. The upgrade pool has 40 upgrades. Before, a missile damaged only the enemy it hit.
- **Basic goos go for the VNP, and chase in straight lines:** the designer noticed basic goos not really heading for the VNP. Measured over a minute against an expert pilot, a chasing basic goo was after a drone over 99% of the time, by phase 3's rule (the nearest drone, and the VNP only with no drone in range), and drones fly close around the VNP. And while chasing, flocking with nearby goos pulled each 10 to 20 degrees off its line on average. At the designer's choice:
  - **What each enemy goes after** is now `Targets` in its file: whichever is nearest, drones first, or the VNP only, in place of the yes-or-no `TargetsDronesFirst`. Every Grey Goo type but the Devourer goes for the VNP only, so they close in on it rather than drifting toward a nearer drone; the Devourer never chases, so what it targets doesn't matter. An awake asteroid mimic's shot still goes at the nearest of the VNP or a drone in range.
  - **Chasing, they stop flocking:** `ChaseFlocking` in each enemy's file sets how much alignment and cohesion count while chasing. It is 0 for every Grey Goo type, so each heads straight for its target, still keeping apart from the rest; the basic enemy keeps 1, as before. Idle groups still wander together.

  This changes phase 3's targeting for the Goo, which the designer had kept when that phase closed.
- **Sharper turns, less health:** at the designer's request, after noticing goos sliding around as they turned, every Grey Goo type but the Devourer changes direction faster and turns faster (`Acceleration` and `TurnSpeed` in each type's file, all alike, like the basic enemy's acceleration), and has 30% less health (`Health`). At the old acceleration, a goo at full speed took nearly three seconds to reverse, drifting sideways through every turn; it now takes about one. A Lancer gliding on after a launch, and a goo pushed out by a split or a Devourer, slow down sooner too.
- **The config review:** at the designer's request, every enemy's values were checked against the code, type by type. All were in use; the changes from it:
  - **The asteroid mimic's waking has its own range:** `AggroRange` did two jobs, waking it while asleep and noticing targets once awake, so an awake mimic barely noticed anything. Waking now uses `WakeRange`; awake, its aggro ranges are like the basic goo's. Its `GroupSize` is unused, since it never spawns in groups, and its file says so, and that a `ShotSize` of 0 is the fight's usual enemy shot size.
  - **Attack ranges from the edge:** the Lancer's `LaunchRange` and the Quiller's `VolleyRange` are now measured from the enemy's edge, like the Spiker's `Reach` and the mimic's `FireRange`, with each lowered by its type's radius so play is unchanged.
  - **The Spiker, the Burster and the Devourer:** as described in their entries above.
  - **Left as they are:** values a type doesn't use because of what it is, since every enemy shares them: the Devourer's aggro ranges and targeting (it never chases), and the drone-absorbing values of the Lancer, Quiller and Burster (0, or with no effect, since they absorb nothing).
- **Older fight logs:** these name the Goo `goo`. No report or tool groups results by enemy ID across logs, so nothing needed converting.
- **First measurement:** 50 expert fights with the Designer build and random starting weapons and asteroid amounts, on the same seeds, once with the old mix and once with Grey Goo only:

  | | Old mix | Grey Goo only |
  |---|---|---|
  | Won (reached the kill target) | 68% | 40% |
  | Median time to win | 3:00 | 3:52 |
  | Alive at 5:00 | 22 of 50 | 50 of 50 |
  | Destroyed by the 8:00 cap | 50 | 15, at a median 7:35 |
  | Level at 4:00 | 16.5 | 15.8 |

  The basic goo alone is little threat to the VNP, as the GDD expects: every pilot reached 5:00 alive. But it dies more slowly than the basic enemy did, so the kill target is reached less often and later. The stages ahead add types that threaten the VNP; the kill target and Grey Goo's strength are for the designer to judge by playing.

## Suggested build order

Build one stage at a time, and stop after each so the designer can play it.

1. **The Grey Goo folder and Grey Goo only:** move the phase 3 Goo into `enemies/grey_goo/basic_goo/` as the basic goo, set up the shared Grey Goo code, and stop the basic enemy spawning.
2. **Pilots that plan:** the goals and path scoring of `docs/dev/PILOT_SPEC.md` (its stages 1 and 2), replacing the current pilot once it matches or beats it in batches.
3. **Lancer:** the first type whose attack is declared; pilots should dodge a forming Lancer with no code for it.
4. **Quiller.**
5. **Spiker.**
6. **Burster.**
7. **Devourer.**
8. **Asteroid mimic.**
9. **Splitting into other types.**

Each type comes with its look, effects, and sounds, its spawn weight, and its threat declaration for pilots.

The pilot spec's later stages run between the types, where they fit:
- upgrade picks on the decision core, and the guards that keep it general;
- calibrating the designer-like pilot against the designer's recorded Grey Goo fights, once the designer has played a few (fitting its weights to individual recorded moments is no longer required: deferred at the designer's choice, as a later option);
- the ceiling pilot;
- removing the old pilot.

They don't hold up the types.

## Performance

Several types multiply: basic goos split, Bursters burst, and Quillers fill the air with spikes. Check against the performance requirement in CLAUDE.md after each stage, in a late fight with every type present.

Track each new enemy type's own performance cost as it is built, so the cost of every type can be seen on its own rather than only as part of the whole fight. When a stage adds a type, measure it alone in large numbers as well as in the full mix, and record what it costs, so a slow type is found at the stage that added it.

## Out of scope

Everything out of scope in phases 1 to 3, plus: other factions, new VNP Weapons, weapon modifiers beyond those already built, playing several fights in a row, Level Up Rewards, weapon combos, escort VNPs, music, and final art and audio.

## Stretch goals

Add these only after this phase feels good and the designer asks for them.

- Any upgrades that counter Grey Goo directly, if playtesting shows the existing pool doesn't offer a good answer to one of the types.

Next phase, planned: Tesla Coil, Cannonball, and the weapon modifiers from the GDD. After that: three fights in a row, with Level Up Rewards between them.

## Done when

- Fights spawn only Grey Goo, and every type behaves as described and can be told apart from the others at a glance.
- Basic goo splits produce other types about one time in three.
- The damage breakdown shows each type separately.
- Pilots react to each type's attacks through its threat declaration, with no pilot code for the type.
- The performance requirement in CLAUDE.md holds in a late fight with every type present.
- Each type's own performance cost has been measured and recorded at the stage that added it.
- Every new placeholder asset is in `res://placeholder/` and listed in `PLACEHOLDERS.md`.
