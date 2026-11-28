# Fight Director addendum: what changed in building and testing

`FIGHT_DIRECTOR_SPEC.md` is the plan as agreed with the designer, and isn't edited to record what the build did. This file records only what an AI working on the project needs in order not to get it wrong: where the build differs from the spec, why, what was tried and rejected, choices made for its open items, and where things live. Where this file and the spec disagree, this file wins. The Fight Director changes how the game plays, so its changes to fights are also in the current design addendum.

An entry belongs here only if, without it, the AI would do something differently or worse. Measurements, tuning numbers that the files already hold, process stories and repeats of the spec stay out; batch reports and git history keep those.

## Status

- Stages 1 to 3 are built, with additions the designer asked for (below). Live pacing (stage 4) and the galactic map's hooks (stage 5) aren't.

## Where things live

- **Code:** `director/`: `FightBrief` (what a fight is asked to be; `Fight.NextBrief` sets the next one), `FightDirector` (made as the fight enters the tree; the swarm calls its `Update` where it used to spawn, and it asks the swarm to place groups, wave groups and stream enemies), `WaveStream` (one kind of wave as it plays out).
- **Data:** plans in `director/plans/`, their shared pieces in `director/waves/`, `director/curves/` and `director/streams/`. A faction's file sits with its types: Grey Goo's is `enemies/grey_goo/grey_goo.tres`, holding its spawn weights, tiers, plans and `MaxAlive`. `director/fight_director.tres` names only the default faction.
- **What every plan shares** stays in `tuning/enemy_spawning.tres` (spawn margin, despawn distance, the wave warning and banner, `WanderHomePull`, `AlertRadius`) and escalation in `tuning/difficulty.tres`.
- **Recorded:** fight logs carry `faction`, `fightPlan` and `planChosen`, and `small_wave` events; the debug line shows the plan. The player isn't told it.
- **Tools:** the harness's `plan=`, batches' `--plans`, the report's By plan section, the starting menu's plan page (`ChoosePlan`).

## Rules a change must keep

- **The director's randomness:** each kind of its choice has a generator of its own, seeded from the fight's seed (`seed × 7919 + stream`): 3 the asteroid amount (the stream it had before the director), 5 the plan, 6 large waves' timing, 7 small waves', 8 streams' first direction. A new kind of choice gets a new stream; never draw the director's own choices from `Rng`.
- **A setting at its "off" value draws nothing:** where a feature draws from the fight's random numbers (arrivals ahead, loose formations, varied formations), it draws only when on, so plans that don't use it play as before.
- **Classic** is the old pacing, kept to compare against: no small waves, no arrivals ahead, the old curve. Changes outside plans (harvest repair, starting on screen, idle enemies) apply to it too, so it no longer replays batches from before them.

## Plans

- **Named plans made of shared pieces,** at the designer's choice: a fight picks a whole plan by its faction's weights, so plans can be told apart while playing and shown on the galactic map. Rejected: pieces picked separately for each fight.
- **Kill target per plan,** at the designer's choice (`KillTarget`, 0 for `fight.tres`'s), since plans that send more enemies otherwise win sooner. Scaling it automatically stays open.
- **Large waves are optional,** for plans that are one continuous stream or field.
- **Every plan starts with enemies in view,** at the designer's request: starting groups go anywhere from `StartClearance` beyond their aggro range out to the plan's field or extra distance, so none notices the VNP at once.
- **The current plans,** to compare and tune, all at equal weight:
  - **Classic:** the old pacing.
  - **Swarm:** surges of packs of the easiest tier.
  - **Pincer:** waves from two sides, with small waves from one.
  - **Rising:** ring waves coming ever closer together, with small waves.
  - **Horde:** many fragile enemies (see Strength) and a high kill target.
  - **Stream:** streams of single enemies turning into a screw (see Streams).
  - **Dense:** a field of idle enemies kept topped up, with small Lancer waves, shaped by the designer: fewer Devourers and Spikers, small unannounced Lancer waves that grow every few waves. Announced waves were rejected for it, since at that frequency the warning would come all the time.

## Pieces as built

- **Waves:** timing steady, speeding up, irregular or surge; arrival ring, front or pincer; makeup mixed, growing, swarm or themed (one type, `ThemedType`); growth every wave or every few (`WavesPerGroupAdded`). Not built: sweeping flocks, trickle, ambushes, escort waves, and wave behaviours other than hunting at once.
- **Small waves** are a second wave stream: no warning, banner, sound or shake, numbered on their own, marked `s1`, `s2` on maps.
- **Formations** (`Formation`): usual, pack, loose or varied. Why: Grey Goo doesn't flock while chasing, but a wave's group spawned in a tight clump and flew straight in, so it read as a flock anyway, and the designer wanted waves to vary.
- **Tiers** are in the faction's file; a type not listed is tier 1. Growing and swarm waves use only the easiest tier against the rest. Grey Goo's Spiker and Devourer are tier 3 because they absorb drones.
- **The difficulty curve** moved from `difficulty.tres` into the plans' curves; escalation stays shared. New plans lean on numbers, with no stat ramps. Why: with stat ramps, drones were lost at the same rate but couldn't be rebuilt late, since tougher enemies meant fewer kills and less debris; raising drone health didn't help.
- **Streams** (`StreamsDefinition`): single enemies one after another from each stream's direction, more streams added at set times and spread evenly, every stream's gap lengthening for each extra stream so the total rate steps up gently, and all spawn points always turning round the VNP, faster from `RotateFromSeconds` (the designer's revision of "start turning at 2:00"). Every stream enemy hunts the VNP, so the spiral collapses into one mass when the VNP runs; the designer accepted that. Replaced: a first version made of unannounced waves from a drifting direction.
- **A field kept dense:** `FieldExtraDistance` and `KeepAlive` (with `RefillSeconds` and growth a minute) top the population up around the VNP. Enemies past `DespawnDistance` are removed, so a field can't be laid out across the arena. Lowering starting groups alone doesn't lighten a start while `KeepAlive` refills it within seconds; lower both.
- **Per-plan shaping:** `GroupWeightScales` (type weights for the plan's groups), `WanderLegScale` (how far idle enemies roam each way), `GroupStrength` and a waves piece's `Strength`.
- **Strength:** health and debris times strength, damage and drain unchanged, so many fragile enemies don't flood the fight with experience. A split's halves keep it; a split's other type is fresh. Fragile goos are drawn and touch at their size times the square root of their strength, the designer's answer to telling fodder apart.
- **Arrivals ahead** (`AheadShare`, on a plan's groups and a waves piece's fronts and pincers): toward a moving VNP's heading. Why: a pilot that never harvested survived as long as one that played properly, since enemies chasing at 90% of its speed never catch a VNP that keeps moving. It was tuned together with the VNP's harvest repair: without repair it cost normal play too. On every plan but Classic, the old pacing, and Dense, whose field doesn't hunt.

## Settled from the spec's open list

- **Which pieces first:** those above.
- **How types are ranked:** a tier set by hand in the faction's file.
- **Announcing a fight's character:** hidden, at the designer's choice, until the galactic map can show it.

## Still open

- **Stream's experience:** Stream sends many enemies, each dropping nearly full debris, so it levels the player far faster than other plans. A debris setting apart from `Strength` is one answer; left open at the designer's choice.
- **Kill targets:** each plan's is a starting value; whether it should instead scale with how many enemies a plan sends.
- **Tiers beyond two** in growing waves.
- **Which plans to keep,** after the designer has played them.
