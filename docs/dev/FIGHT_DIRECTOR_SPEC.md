# VNP Combat Prototype: The Fight Director

Every fight is paced the same way today:
- five groups at the start;
- a group every few seconds after that, a little faster each minute;
- a ring of hunting groups every 45 seconds, one group larger each time;
- enemies that chase at 90% of the VNP's starting speed and keep up.

Fights differ only in the asteroids and the enemy types drawn. Late in a fight, running is often the only answer, and the batches show it: Mine Layer, whose mines land behind a running VNP, wins 99 to 100% with every pilot.

This spec adds a **Fight Director**. It gives each fight its own character and paces the fight as it goes. It is built so other factions, and later the galactic map, can use it without a new director of their own. It changes how the game plays, so each change it makes to fights is also recorded in the current design spec's addendum.

## What this is for

- Do fights on different seeds feel different, in ways a player notices and adapts to?
- Can a fight build, peak and ease off, so the player sometimes has room to regroup and rebuild drones instead of running?
- Does the strongest starting weapon's lead shrink once running isn't always the answer?
- Can one director serve factions that behave very differently, with each faction described as data?
- Can the galactic map later choose or show a fight's character before the player enters it?

## Terms

- **Fight brief:** what a fight is asked to be: its faction, its difficulty and its seed. The game sets it for now. Later the galactic map sets it, from the sector being fought over.
- **Fight plan:** the pieces picked for one fight at its start, from the brief alone.
- **Piece:** one choice in a plan: how enemies arrive, when waves come, what a wave is made of, how a wave behaves, how the fight grows harder, or a knob.
- **Large wave:** an announced wave, with its warning, sound, banner and screen shake, as every wave is today.
- **Small wave:** an unannounced wave of a few groups, with no warning, banner or shake, coming more often than large waves.
- **Difficulty curve:** how a fight grows harder before the time limit: by stats (enemies' health, damage, speed and accuracy) and by numbers (how often groups spawn, how large groups and waves are, and small waves).
- **Knob:** a named multiplier that the plan carries for one fight, such as enemy speed or spawn rate. It does not change the tuning files.
- **Pacing:** the live part of the director, which raises and lowers pressure during the fight.
- **Intensity:** the director's measure of how hard-pressed the player is right now.
- **Classic plan:** the plan that reproduces today's fights exactly.

## Where the director sits

- **What it decides:** what arrives, when, where, and how hard.
- **Mechanics stay in `EnemySwarm`:** placing groups, flocking, simulating them and removing them.
- **What enemies do stays per type,** in each type's behaviour code. The director asks for "a pincer wave of Lancers, hunting". The swarm places it, and each Lancer behaves as a Lancer.
- **Spawning moves to the plan:**
  - Today's spawn timer, waves and ramp become the classic plan's pieces.
  - The asteroid amount pick becomes one of the plan's knobs.
- **Escalation past the time limit stays as it is,** on top of whatever the plan does, so the limit keeps its meaning: "retreat, die, or narrowly succeed".

## Factions as data

- **Each faction is a resource,** a `FactionDefinition`, holding:
  - its enemy types and their spawn weights, moved out of `tuning/enemy_spawning.tres`;
  - which pieces it can use, with a weight for each;
  - its limits, such as the most enemies alive and the fastest it may be paced.
- **Grey Goo is the first and only faction for now.**
- **A faction that needs something new adds a new kind of piece,** and any faction can use it after that. There is no director per faction, and no faction code in the director.
- **The basic enemy** stays a placeholder with no faction, as phase 4 left it. It is listed in Grey Goo's weights at 0, so it can be brought back by giving it a weight.

## Pieces

These are starting ideas for the designer to cut, change or add to. Each kind has today's behaviour as one of its pieces, and the classic plan is made of those.

### How enemies arrive

- **Scattered groups:** single groups off screen in random directions (today).
- **Sweeping flocks:** large flocks that cross the map along a line, which the player can meet or avoid.
- **A front:** groups arriving from one direction, so there is a safe side.
- **A ring:** groups evenly around the VNP (today's waves).
- **A pincer:** two groups at once, from opposite sides.
- **A trickle:** a steady stream of small groups and no big waves.
- **Ambushes:** groups waiting in asteroid fields, as the mimics do, so harvesting carries risk.

### When waves come

Added at the designer's request: a plan can have two kinds of wave. **Large waves** are the announced ones, spaced further apart in plans that also have small waves. **Small waves** are unannounced and more frequent, giving steady pressure between the large waves without the player bracing for each one. Each plan sets the timing of both: when the first of each comes, the gaps between them and how they grow, and whether it has small waves at all. The classic plan keeps today's timing and has none.

Timings for either kind:

- **Steady:** a fixed interval (today's large waves).
- **Speeding up:** the gaps shorten through the fight.
- **Irregular:** random gaps around an average, so the next wave can't be timed.
- **Surge and lull:** a burst of waves close together, then a quiet stretch.

Small waves, as first built (easy to change):

- **From one or two directions,** not a full ring, so they read as a push from somewhere rather than a smaller large wave.
- **Hunting at once,** like large waves.
- **Not numbered:** the "WAVE n" banner counts large waves only.
- **Recorded:** in fight logs, and on the run review's maps in a colour of their own.
- **Fair to pilots:** pilots see them only once on screen, as a player does.

### What a wave is made of

- **Mixed:** each group's type drawn by weight (today).
- **Themed:** a whole wave of one type.
- **Escort:** a core enemy, such as a Devourer, with guards around it.
- **Growing:** the mix gains tougher types as the fight goes on. The designer's idea: early waves are mostly easier types, and as the fight goes on a few harder ones are peppered in, rather than every wave drawing from the same mix.
- **Swarm:** many of the easiest type, coming in rapid succession, so the pressure is sheer numbers rather than any one dangerous enemy. This pairs with a surge in wave timing.

**Easier and harder types:** growing and swarm waves need to know which types are easy. Each faction would rank its types, such as a tier for each in its file (for Grey Goo, the basic goo easiest), so a wave can draw "mostly easy, a few hard" without naming types, and a new type only needs its tier.

### How a wave behaves

- **Hunt at once:** every member goes for the VNP from the moment it arrives (today).
- **Gather, then charge:** groups gather out of reach, then come in together.
- **Circle and close:** groups ring the VNP and tighten.
- **Sweep through:** groups pass through and carry on, rather than staying to hunt.

### How the fight grows harder

Added at the designer's request, after raising drones' health didn't stop them dying too much. Each plan owns its difficulty curve before the time limit, rather than every fight sharing `tuning/difficulty.tres`:

- **By stats:** how much health, damage, speed and accuracy enemies gain each minute.
- **By numbers:** how much faster groups spawn each minute, how much larger groups and waves grow, and small waves.

**Why numbers, mostly:** with today's ramps, a Grey Goo spawned at 5:00 has twice the health and half again the damage, and its drain on drones grows with its damage. A drone fight that is fair at 1:00 is lopsided by 4:00, whatever the drones' health. Growing by numbers keeps each enemy one drones can beat, so the pressure comes from how many there are, which reads better in a swarm game. New plans lean on numbers; plans can still differ, one swelling in numbers while another sends fewer, tougher enemies.

- **The classic plan keeps today's ramps,** so fights on it don't change.
- **Escalation past the time limit stays shared** and stat-based, in `difficulty.tres`, so the limit keeps its meaning: retreat, die, or narrowly succeed.
- **The brief's difficulty scales the plan's curve,** once the galactic map sets it.

### Knobs

- enemy speed curve (part of the difficulty curve, above)
- spawn rate (likewise)
- group size
- aggro range
- asteroid amount

Each is a named multiplier, read where its tuning value is read today. "Anything tunable" grows by adding named knobs, one documented knob at a time, rather than by changing arbitrary tuning fields. That keeps the director safe in exported builds, where the editor's tuning variants don't exist.

## Live pacing

A later stage, after plans work.

- **What it measures:** intensity, from what the player is going through:
  - damage taken recently;
  - enemies close by;
  - drones lost recently;
  - health.
- **How it paces:** it moves through building up, peaking and easing off, in the way Left 4 Dead's AI Director does. The plan sets how long each stretch lasts and how hard it is. Pacing then follows intensity: it holds a peak while the player copes, and starts easing off sooner when they're overwhelmed.
- **While easing off:**
  - no new arrivals come near the VNP;
  - hunting enemies can lose interest and drift away;
  - the player gets room to regroup and rebuild drones.
- **Limits:**
  - Pacing can't make a fight easy. Easing off gets shorter as the fight goes on.
  - Escalation past the time limit still applies.
  - Pacing never removes enemies already close to the VNP.

## Repeatable

- **The plan depends on the brief alone.** Its seed drives the director's own random generator, as the asteroid amount pick does today. So the galactic map can know a sector's plan before the fight starts, and the fight's other random numbers don't shift.
- **Pacing draws from that generator too,** never from the fight's random numbers.
- **The spawn mechanics keep drawing from the fight's random numbers** for where and what, as they do now.
- **The classic plan reproduces today's fights exactly,** checked against traces, so older batches stay comparable and can still be replayed.

## Pilots, reviews and tools

- **Pilots don't read the plan.** They see only what a player sees, and waves keep their warning. Estimating the next wave from waves already seen stays as the pilot spec left it.
- **Fight logs record** the brief, the plan and, once pacing exists, each change of pacing.
- **Batch reports** compare results by piece and by plan. Win rate by starting weapon within each piece shows whether a weapon's lead depends on how a fight is paced.
- **The scenario harness** gets `plan=` to force a whole plan and options to force single pieces, such as `arrival=pincer`. Batches get `--plans`.
- **Maps** mark arrivals by piece, and, once pacing exists, its stretches along the timeline.
- **Performance:** the director costs almost nothing a frame and allocates nothing a frame, per the performance spec.

## Reuse for the galactic map

- **The brief is where the map plugs in.** The map will set the faction and difficulty. Since the plan depends on the brief alone, the map can also show a sector's plan before the player commits to the fight.
- **Pieces are picked with Arbiter's decision core** (`Arbiter.Decision`), with terms such as:
  - the faction's weight for the piece;
  - fit to the brief's difficulty;
  - not repeating recent fights.

  The map's own choices, such as where rival factions push, could reuse the same core. As the pilot spec requires, game words stay out of Arbiter; the terms are VNP's code, described to Arbiter in its own terms.
- **Not repeating recent fights needs a memory that lasts across fights.** That comes with "three fights in a row". Until then, each fight is picked on its own.

## Settled during the build

The prototype now does the following; any of it can still change.

- **The brief, the director and the classic plan (stage 1):** in `director/`.
  - **The brief:** `FightBrief`, holding the requested seed, faction (stage 2) and asteroid amount, each left unset for the director to pick. `Fight.NextBrief` sets the next fight's, replacing `Fight.NextSeed` and `Fight.NextAsteroidAmount`; the scenario harness sets it. The difficulty joins it when the director first uses it.
  - **The director:** `FightDirector`, a plain class the fight makes as it enters the tree, before any of its parts, so the asteroids are placed from the plan's amount. Each frame the swarm calls `FightDirector.Update` where it used to spawn, and the director asks the swarm for groups (`EnemySwarm.SpawnScattered`) and waves (`EnemySwarm.SpawnRing`). It counts the waves, so the HUD, the wave warning and the run review's recorder read waves from it.
  - **Plans are files,** for now: `FightPlanDefinition`, in `director/plans/`, picked by the weights in the faction's file (stage 2). The classic plan (`classic.tres`) holds the spawning schedule moved unchanged from `tuning/enemy_spawning.tres`: starting groups, the spawn interval and extra distance, and the waves' timing and growth. What every faction and plan shares stays there: the spawn margin, the despawn distance and the wave warning and banner. Whether later plans stay whole files or are put together from pieces picked separately is open (see Not yet decided).
  - **The director's own randomness:** each kind of choice has a generator of its own, seeded from the fight's seed (`seed × 7919 + its stream`): the asteroid amount keeps the stream it had before the director (3), and the plan has 5. So a seed keeps its asteroid amount, and the plan depends on the brief alone.
  - **Recorded:** fight logs record the plan (`fightPlan` in their settings), and the debug line shows it. The player isn't told it.
  - **No fight changed:** four 3:20 fights (expert with Greedy and with Designer, novice with Random; Orbital Shards with Ram, Mine Layer with Beam on a Rich field, and two random starts) traced identically before and after at every checkpoint, so older batches still replay.
- **Grey Goo as a faction (stage 2):** `FactionDefinition` (`director/FactionDefinition.cs`), with Grey Goo's file beside its types, `enemies/grey_goo/grey_goo.tres`.
  - **What a faction holds:** its enemy types' group and wave weights and `MaxAlive`, moved unchanged from `tuning/enemy_spawning.tres`, and the plans its fights can follow, with their weights, moved from `director/fight_director.tres`. That file now names only `DefaultFaction`, the faction a brief that names none is against: every fight, until the galactic map chooses.
  - **Who reads it:** the swarm takes its types, weights and limit from the fight's faction; the asteroid mimic hidden in fields is the faction's mimic type, found per fight rather than once for the whole game; the scenario harness checks `spawn=` against the default faction's types. Fight logs record the faction (`faction`).
  - **The basic enemy** is listed in Grey Goo's weights at 0, as it was in `enemy_spawning.tres`, so fights still know the type and `spawn=basic_enemy:N` works. It belongs to no faction; the listing is only so it can be brought back, as phase 4 left it.
  - **No fight changed:** the same four fights as stage 1 traced identically.
- **Plans made of pieces (stage 3):**
  - **The shape,** at the designer's choice: named plans made of reusable pieces, a fight picking a whole plan by its faction's weights. A plan (`FightPlanDefinition`, `director/plans/`) holds its kill target, how groups arrive between waves, and three pieces: its large waves, its small waves (optional) and its difficulty curve. Pieces are files that plans share: `WavesDefinition` in `director/waves/` and `DifficultyCurveDefinition` in `director/curves/`.
  - **A waves piece** holds its timing, arrival and makeup, each a kind with its own settings (as upgrade effects are), and its size: groups in the first wave and groups added each wave.
    - **Timing built:** steady, speeding up (each gap a set share of the last, down to a least gap), irregular (gaps straying either way from an average, from a generator of the director's own) and surge (waves close together, then a quiet stretch).
    - **Arrival built:** ring, front and pincer, each spread across a set angle. The direction comes from the fight's random numbers, as the ring's did.
    - **Makeup built:** mixed; growing, where each group is from a harder tier with a chance that grows each minute up to a most, and from the easiest otherwise; and swarm, the easiest tier only. Growing and swarm use only easiest against the rest so far.
    - **Themed (built later):** every group of the wave is one type, `ThemedType`, whatever its weight; Dense's waves are Lancers.
    - **Not built yet:** sweeping flocks, trickle, ambushes, escort waves, and wave behaviours other than hunting at once.
  - **Small waves** are built as the spec describes, a second wave stream beside the large one: no warning, banner, sound or shake; numbered on their own; recorded in fight logs (`small_wave` events) and marked on maps as `s1`, `s2` in a paler orange. Each stream's irregular timing has a generator of its own (streams 6 and 7).
  - **Tiers** are in the faction's file (`Tiers`); a type not listed is tier 1. Grey Goo's, picked for now: basic goo 1; Lancer, Quiller, Burster and asteroid mimic 2; Spiker and Devourer 3, since they absorb drones.
  - **The difficulty curve moved** from `tuning/difficulty.tres` into the plans' curves: every per-minute ramp (spawn rate, speed, Grey Goo's health and damage, and the basic enemy's health, damage and accuracy). The classic curve has the values every fight had. The numbers curve, which the new plans use, has no stat ramps, the speed ramp the designer set, and groups spawning 0.75 more a minute, from the first comparison's `numbers_a`. Escalation stays in `difficulty.tres`.
  - **Kill target per plan,** at the designer's choice: `KillTarget` in the plan, 0 for `fight.tres`'s. The HUD, the result screen and fight logs show the fight's own.
  - **The first plans,** starting values for the designer to play and tune, all at equal weight in Grey Goo's file:
    - **Classic:** the fights as they were.
    - **Swarm:** surges of three swarm waves, 6 seconds apart, from a ring, every 75 seconds; no small waves.
    - **Pincer:** growing waves from two opposite sides every 55 seconds, two groups larger each time; small growing waves from one side about every 20 seconds.
    - **Rising:** growing ring waves whose gaps shorten from 60 seconds to 25; the same small waves as Pincer.

    Every new plan uses the numbers curve and a kill target of 160.
  - **Tools:** the harness's `plan=ID`; batches' `--plans` (a list, `all`, or `random`, the default, for each fight's own pick); a By plan section in reports; the plan in combination labels and in watch commands when a batch sets it. Variants that changed the ramps or the classic waves now change the classic plan's pieces, so they are batched with `--plans classic`.
  - **No classic fight changed:** the same four fights, run with `plan=classic` to 5:30, traced identically before and after.
  - **First batch:** 25 expert fights a plan, Designer build:

    | Plan | Kill target | Wins | Alive at 5:00 | Drones at 2:00 / 4:00 | Enemies at 4:00 |
    |---|---|---|---|---|---|
    | Classic | 120 | 68% | 17 of 25 | 8.4 / 3.4 | 44 |
    | Swarm | 160 | 84% | 7 of 25 | 9.6 / 4.4 | 120 |
    | Pincer | 160 | 88% | 25 of 25 | 4.8 / 2.4 | 69 |
    | Rising | 160 | 96% | 25 of 25 | 11.0 / 9.9 | 98 |

    The plans play differently: Swarm is the hardest to survive past the win, Pincer is the hardest on drones, and Rising, with the most enemies after Swarm but no stat ramps, the easiest.
- **Arrivals ahead (after stage 3):** at the designer's request, after pilots kept surviving by running: a plan's `AheadShare` sends that share of its between-wave groups toward the VNP's heading, within `AheadSpreadDegrees`, and a waves piece's `AheadShare` aims a front there, or a pincer ahead and behind. Only while the VNP moves faster than a third of its starting top speed. 0 draws nothing, so Classic, which keeps it at 0, is unchanged.
  - **Why:** a pilot that never harvested survived to 5:00 as often as one that played properly (60 of 80 each, across the four plans), since enemies chasing at 90% of its speed never catch a VNP that keeps moving.
  - **Tuned together with harvest repair** (the VNP's, `RepairPerMass` in `vnp.tres`; see the phase 4 addendum), 80 fights each, normal play against a pilot that never harvested (variant `never_harvest`):

    | | Alive at 5:00, normal | Never harvests | Wins, normal |
    |---|---|---|---|
    | Both off | 65 | 65 | 71 |
    | Repair 0.05, ahead 0.3 (fronts half the time) | 67 | 46 | 71 |
    | Repair 0.1, ahead 0.3 (fronts half the time) | 68 | 52 | 71 |
    | Repair 0.1, ahead 0.5 (every front) | 68 | 46 | 75 |

    Chosen: the last, on every plan but Classic. Without repair, half the groups ahead cost normal play too (51 of 80). Variant `no_repair_or_ahead` turns both off.
- **Choosing the plan while playing:** the playtest cheat's starting choice menu ends with a plan page (`ChoosePlan` in `in_game_cheats.tres`), Random first, then the default faction's plans. Picking one swaps the plan and its difficulty curve before anything spawns; fight logs record `planChosen`. The HUD's kill target follows it.
- **Wave formations,** at the designer's request: Grey Goo doesn't flock while chasing (`ChaseFlocking` 0), but a wave's group spawned in a tight clump and flew straight at the VNP, so it still read as a flock. The designer wanted waves to vary, sometimes flocking and sometimes not. A waves piece's `Formation`:
  - **Usual:** as before; Classic keeps it.
  - **Pack:** held together while chasing, by the type's alignment and cohesion.
  - **Loose:** spawned spread out (`LooseSpread`) and each enemy coming in from its own angle (up to `LooseAngleDegrees` off straight), straightening out within about 40 units of its target, so the group arrives from several directions.
  - **Varied:** each wave pack or loose, at random.

  Swarm's and Horde's small waves are packs, Pincer's loose, and Rising's, Horde's large waves and the shared small front waves varied.
- **Plans without large waves, and streams of single enemies:** for a continuous stream, as the designer described it: enemies one at a time, spaced out, faster as the fight goes on; more streams added from other directions at set points, each mirroring the first, with the rate eased when one is added; and all of them turning round the VNP, faster from 2:00, so enemies stream in one after another as a screw (the designer's revision: always turning, speeding up, rather than starting to turn at 2:00).
  - `LargeWaves` may be empty, leaving no announced waves; the HUD then never warns.
  - **A streams piece** (`StreamsDefinition`, `director/streams/`), a plan's `Streams`: one stream from `FirstSeconds`, spawning single enemies just off screen from its direction (with a little jitter) every gap, each hunting the VNP. The gap shortens by `RatePerMinute`, down to `MinIntervalSeconds`. At each of `AddStreamSeconds` another stream is added, the streams spread evenly round the VNP, the new one starting half a gap later so they take turns, and every stream's gap lengthens by `IntervalPerExtraStream` for each stream beyond the first. Every spawn point turns round the VNP the same way, at `StartRotationDegreesPerSecond` and from `RotateFromSeconds` at `RotationDegreesPerSecond`. Enemies are drawn by makeup and tier as waves' are, at their own `Strength`. At most 8 streams. Streams ignore `MaxAlive`, as waves do; the first stream's direction comes from a generator of the director's own (stream 8).
  - **Stream** (`director/plans/stream.tres`): streams only (`screw.tres`), from 3 seconds, an enemy every 1.4 seconds at first, 0.4 faster a minute; a second, third and fourth stream at 1:15, 2:30 and 3:45; turning at 3 degrees a second, and 12 from 2:00; growing makeup at 0.9 strength; few other groups; kill target 300.
  - **Tuning:** at 1.6 seconds and 0.8 strength it was easy (all 20 alive at 5:00, the win at 3:20, about 75 enemies at 4:00; full strength changed little); at 1.2 seconds and full strength, too hard (drones gone by 3:00, 242 enemies at 4:00). The settled values, 20 fights: 16 wins, a median win at 3:29, 17 alive at 5:00, drones 14.6 at 2:00 falling to 2.4 at 4:00, 140 enemies at 4:00; a pilot that never harvested 9 wins and 14 alive.
  - **The screw shows only while the VNP holds its ground:** every stream's enemies hunt the VNP, so a VNP that runs gathers all of them into one mass behind it, as a pilot with no drones left did in a screenshot at 3:31. The designer is fine with that: the spiral needn't hold while moving.
  - **Replaced:** a first version spawned groups as unannounced waves from a slowly drifting direction (a `Stream` wave arrival, removed with it).
- **A dense field, and the Dense plan:** at the designer's request, a plan with few waves and many enemies already there. A plan's `FieldExtraDistance` spreads its starting groups that far beyond the screen, rather than just off it, and `KeepAlive` (growing by `KeepAlivePerMinute`) has the director add a group every `RefillSeconds` while fewer are alive, spread the same way, so the field stays dense around the VNP however many are killed. Enemies past `DespawnDistance` are removed, so the field is kept up around the VNP rather than placed across the arena.
  - **Dense** (`director/plans/dense.tres`): 45 starting groups, on screen as well as off (about 110 enemies; 60 was a little much at first, the designer found), at least 110 alive and 15 more each minute (150 at first, lowered with the starting groups so the start stays lighter), at 0.8 strength, a fifth of the usual Devourers and 0.7 of the Spikers, wandering twice as far each way; small, unannounced, loose waves of Lancers only (`dense_lancers.tres`, themed makeup), at the designer's request: one group of 2 at 0:30, then a wave every 30 seconds, one group more every 6 waves (`WavesPerGroupAdded`), so the same size for a while; no announced waves, since that often the warning would come all the time. Every 15 seconds was tried first: 7 of 20 alive at 5:00, Lancers 61% of the damage. Before: announced Lancer waves of 6 groups and two more every 90 seconds (Lancers over half the damage, 11 of 20 alive at 5:00), then 3 groups and one more every 60 (12 of 20); kill target 270 (raised from 200 to 300 at the designer's request, after they reached 200 at 3:26, then lowered to 270). It started with 35 groups and 100 alive, and waves of every type every two minutes. At 300 it was rarely reached (median 5:39). Idle enemies only notice the VNP up close, so the field is dangerous to push through rather than hunting it down; fights are survivable, and the challenge is the kills.
- **Plans shaping their groups:** for Dense, at the designer's request.
  - **Starting on screen, in every plan:** starting groups are placed anywhere from `StartClearance` beyond their aggro range (and their group's spread) out to the plan's field or extra distance beyond the screen, so a fight starts among enemies that haven't noticed the VNP rather than with an empty screen. First built for Dense alone; the designer made it the rule for every plan.
  - **Type weights per plan:** `GroupWeightScales` multiplies the faction's group weights, by type, for the plan's groups between waves. Dense sets the Devourer to a fifth (half was tried first) and the Spiker to 0.7, at the designer's request.
  - **How far idle enemies roam:** `WanderLegScale` multiplies how long an idle enemy heads one way before picking a new direction (1.5 to 4 seconds). Dense doubles it.
  - **No pull home, for every plan:** idle enemies are no longer pulled back to where they spawned (`WanderHomePull` in `enemy_spawning.tres`, 0; 1 restores it), so idle groups roam freely, still keeping together. On its own this didn't make Dense threatening: with the new start, 20 Dense fights were all alive at 5:00, and so were 20 by a pilot that never harvested, since idle enemies drift only 2 to 4 units a second and notice the VNP within 24 to 30 units.
  - **Group aggro, for every plan** (`AlertRadius`, see the phase 4 addendum): on Dense, 20 fights, health at 4:00 fell from 85 to 70 and the median win came sooner (4:11 against 4:37), but 19 were still alive at 5:00, and 19 for a pilot that never harvested.
  - **A fix it needed:** idle enemies turn to face where they drift only above a share of their wander speed. Without the pull home they drift at about half of it, by `WanderWeight`, so the share was lowered from a half to a fifth; Lancers had stopped pointing where they wandered.
- **Fragile enemies, and the Horde plan:** at the designer's suggestion, for drones dying late: a plan's `GroupStrength` and a waves piece's `Strength` multiply the health and debris of what they spawn (1 as usual), so many fragile enemies can come without flooding the fight with experience. Their damage and drain are unchanged. A split's halves keep it; a split's other type is fresh, at 1.
  - **Smaller:** a fragile goo is drawn and touches at its size times the square root of its strength (`GreyGoo.Resize`), the designer's answer to telling fodder apart: at 0.4, about 0.63 of the size.
  - **Horde** (`director/plans/horde.tres`): between-wave groups at 0.4 strength, every 2.5 seconds, half of them ahead; large ring waves of growing makeup at 0.4, from 40 seconds and every 50, two groups larger each time, hard share from none to 0.4; small swarm waves of the easiest tier at 0.35 from ahead, every 15 seconds shortening to 8; its own curve, `horde`, with no stat ramps and spawning 0.6 faster a minute; kill target 450.
  - **First tuning:** at a kill target of 300 and a steeper ramp it won at 2:40 and most fights died by about 4:10, so the target rose and the ramp and small waves eased. Then, 20 fights each: won 15, a median win at 4:00, 12 alive at 5:00, and 15.5 drones on average at 4:00, the most of any plan (Classic 2.0, Pincer 1.9, Swarm 3.4, Rising 11.2 in the batch before). A pilot that never harvested: 5 alive at 5:00, 3.1 drones.

## Not yet decided

- Which pieces to build next, and which plans to keep.
- **Dense difficulty:** survivable as tuned (20 of 20 alive at 5:00 for normal play, 19 for a pilot that never harvests), so a pilot that never harvests is held back only in kills. Starting values for the designer to play.
- **Stream's experience:** Stream sends many enemies, each dropping nearly full debris, so it gives far more experience than other plans (the designer's Stream fights brought in 8,000 to 14,000 a minute, against 3,000 to 4,500, and one reached level 43). A separate debris setting for stream enemies, apart from `Strength`, is one answer; left open at the designer's choice.
- **Tiers beyond two:** growing and swarm waves use only easiest against the rest; whether growing waves should bring in tier 2 before tier 3.
- **How far to lean on numbers:** measured before stage 3 (see Settled during the build), and tuned by playing.
- **Small waves' size and frequency,** and whether some plans have only small waves.
- **The kill target and numbers:** a plan that grows by numbers brings the kill target sooner. In the first comparison, waves and spawning that grew faster won at a median 3:06 to 3:15 against 3:58. So more pressure also meant an earlier win. For now each plan sets its own (stage 3, the designer's choice). Still open: whether it should instead scale with how many enemies the plan sends, or count something other than kills.
- **How types are ranked from easy to hard:** a tier set by hand in the faction's file for now (stage 3); worked out from each type's stats is the alternative.
- How strong easing off should be, and whether hunters giving up feels fair or strange.
- Whether pilots need to estimate the next wave, once wave timing varies.
- Announcing a fight's character to the player. It stays hidden for now, at the designer's choice, until the galactic map can show it.
- Knobs beyond the first set.

## Suggested build order

1. **The brief and the plan,** with the classic plan only. No fight changes, checked against traces. Plans are recorded in fight logs.
2. **Grey Goo as a faction:** a `FactionDefinition` holding the weights from `enemy_spawning.tres`. Still no fight changes.
3. **A few pieces of each kind,** (built, see Settled during the build) picked at fight start by the faction's weights, with the harness options, `--plans` and reports by piece. The designer plays them and cuts or tunes them. They include:
   - the difficulty curve moving into the plan, with the classic plan keeping today's ramps;
   - small, unannounced waves beside the large ones, with each plan's timing for both.
4. **Live pacing.**
5. **Hooks for the galactic map,** when the map is built: the brief set from outside a fight.

## Out of scope

- the galactic map itself;
- fights in a row;
- other factions;
- new enemy types.

## Done when

- Fights on different seeds feel different to the designer, and they can tell the plans apart while playing.
- Fights ease off at times, and in batches pilots rebuild their drones more often.
- No starting weapon wins nearly every fight across all plans.
