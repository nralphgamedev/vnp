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

## Not yet decided

- **Whole plans or pieces:** whether a plan stays one file holding all of its choices, picked whole, or is put together from pieces of each kind picked separately. Whole plans are easier to tell apart while playing and to show on the galactic map; pieces give more variety from fewer files. Stage 1 made plans whole files; to be settled in stage 3.
- Which pieces to build first.
- **How far to lean on numbers:** measured before stage 3 (see Settled during the build), and tuned by playing.
- **Small waves' size and frequency,** and whether some plans have only small waves.
- **The kill target and numbers:** a plan that grows by numbers brings the kill target sooner. In the first comparison, waves and spawning that grew faster won at a median 3:06 to 3:15 against 3:58. So more pressure also meant an earlier win. Options: the kill target belongs to the plan, or scales with how many enemies the plan sends, or counts something other than kills.
- **How types are ranked from easy to hard** for growing and swarm waves: a tier in the faction's file for each type, or worked out from each type's stats.
- How strong easing off should be, and whether hunters giving up feels fair or strange.
- Whether pilots need to estimate the next wave, once wave timing varies.
- Announcing a fight's character to the player. It stays hidden for now, at the designer's choice, until the galactic map can show it.
- Knobs beyond the first set.

## Suggested build order

1. **The brief and the plan,** with the classic plan only. No fight changes, checked against traces. Plans are recorded in fight logs.
2. **Grey Goo as a faction:** a `FactionDefinition` holding the weights from `enemy_spawning.tres`. Still no fight changes.
3. **A few pieces of each kind,** picked at fight start by the faction's weights, with the harness options, `--plans` and reports by piece. The designer plays them and cuts or tunes them. They include:
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
