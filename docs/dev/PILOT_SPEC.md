# VNP Combat Prototype: Pilots That Plan

The automated playtesting spec (`docs/dev/AUTOMATED_PLAYTESTING_SPEC.md`) built the pilots that fly the VNP in batches. They move by a set of hand-weighted wants, such as harvest, fight, back off, keep clear of contact enemies, and dodge, each tuned by watching fights and fixing what looked wrong. That worked for two enemy types, but it doesn't scale:

- **New enemies need new pilot code.** Each new enemy behavior needs pilot code of its own.
- **Conflicting wants average out.** Wants that pull different ways add up to weak, in-between moves, which was behind several problems fixed by hand: creeping, indecision, and closing in on enemies to reach rocks.

Phase 4 adds six Grey Goo types with attacks of their own, and the full game will have dozens of enemy types and hundreds of weapons and upgrades.

This spec rebuilds how pilots decide, so that a new enemy or weapon describes itself as data and pilots handle it without new pilot code. It sets two ways to train pilots: to play like the designer, and to play as well as possible. And it builds the general parts as a library that isn't tied to pilots or to VNP. Everything in the automated playtesting and run review specs still applies unless this file changes it.

## What this is for

- Can pilots handle a new enemy type, with attacks of its own, with no pilot code for it?
- How close does a pilot come to the designer's play when its settings are calibrated against the designer's recorded fights?
- How well can the game be played, and how far is that from how the designer plays it? The gap shows difficulty headroom, and the builds that win far more often in the best hands show where the balance breaks.
- Can the same pilot fly rival and escort VNPs at a chosen difficulty, fairly?
- Can the general parts drive other kinds of behavior, in this game and others?

## Terms

- **Decision core:** the general way every choice is made: options scored by adding weighted scoring terms, with skill settings, a record of each choice and an explanation.
- **Option:** one thing a decision can choose, such as a goal, a path or an upgrade.
- **Scoring term:** one thing an option is judged on, such as predicted damage, progress toward the goal or keeping drones safe, with a weight.
- **Threat declaration:** what an enemy publishes about itself for pilots: its body and the reach of its touch, its velocity, its traits, and the danger shapes of its attacks.
- **Danger shape:** an area an attack is about to hit, as a circle, line, cone or ring, with when it lands, what it endangers (the VNP, drones, or both) and how much it hurts.
- **Candidate path:** one way the VNP could move over the next moment or two: a direction, possibly turning. Paths are the options of the movement decision.
- **Goal:** what the pilot is trying to do: harvest, fight, regroup, explore or lead chasers.
- **Training target:** what a pilot's weights are tuned toward: like the designer, or as strong as possible.

## How a pilot works

Three layers, each replaceable without the others. Goals and movement are both decisions made by the decision core.

### Perception: everything declares itself

- **Enemies:** every enemy type implements a required hook that publishes its threat declaration. A type without one doesn't compile, as with weapons' `CollectPlaced`.
- **Danger shapes appear with their tells:** an attack's danger shape exists only once its tell is visible. A Lancer's line appears as it starts forming its spike, and a Quiller's cone as it forms its volley, then each spike's path back. Until then the pilot knows nothing of it.
- **Hidden enemies stay hidden:** a sleeping asteroid mimic publishes nothing, so pilots treat it as an asteroid.
- **Shots in flight** declare themselves the same way: enemy projectiles, Quiller spikes and Burster blobs, through the shared projectile system where they use it.
- **Weapons** publish where they are effective: their reach, and objects that work in place, such as mines (replacing `IPilotLure`) or the shards' orbit.
- **Material** publishes its worth, as harvest scoring does now.
- **Fair sight:** a pilot reads only declarations within its awareness and on screen, the same view it has now, and notices each danger shape with a chance set by its skill, after its reaction time.
- **Game code:** the hook is not a testing aid. Rival and escort VNPs will need it in exported games, so it is not inside `#if TOOLS` or `#if RECORDING`.

### Goals: what it is trying to do

- **Picking the goal:** the goal is a decision among harvest, fight, regroup, explore and lead chasers.
- **Existing judgements carry over** as its scoring terms:
  - whether it can afford a fight;
  - regrouping with few drones;
  - harvest worth and clusters;
  - pressure that rises at once and falls slowly;
  - remembered material.
- **What the goal decides:** which scoring terms count in the movement decision, and how much.

### Movement: scoring short paths

- **Prediction:** each decision, the pilot samples candidate paths over a short look-ahead and predicts where each threat will be along each one: bodies from their velocity, danger shapes from their declared timing.
- **Scoring:** a path's score adds up its scoring terms:
  - predicted damage to the VNP;
  - danger to drones: the swarm near a drone eater or inside a danger shape;
  - progress toward the goal, such as a rock, a weapon's preferred distance or a lure spot;
  - a cost for reversing or turning sharply, against indecision.
- **Following it:** the pilot follows the best path until its next decision. As now, it moves at full speed except when easing in to harvest.
- **Weapon profiles stay:** preferred distance, keeping moving and leading chasers become scoring terms, blended by investment as now.
- **Cost:** a decision looks only at threats near the VNP, through the spatial grids, so its cost doesn't grow with the fight's total number of enemies. Several pilots flying at once must stay a small share of game logic's budget (`docs/dev/PERFORMANCE_SPEC.md`).

### Upgrade picks

Builds move onto the decision core too: the upgrades offered are the options, and a build's weights are scoring terms on them. This makes upgrade picks the core's second user (see A generic library). Fitting a build to the designer's picks then uses the same training tools as movement.

### Skill

- **Settings, not behavior:** a skill level is a set of settings, not hand-written behavior:
  - reaction time;
  - awareness;
  - how reliably it notices a danger shape;
  - how much noise there is in its predictions and choices;
  - how far ahead it looks;
  - how many paths it tries;
  - how much its movement wanders.
- **Skill levels:** novice, average and expert become points on these settings, and rival and escort VNPs can be given any difficulty the same way.

### Explaining and reviewing

- **`explain=`:** prints, for each decision, the chosen goal and the best few paths with each one's scoring terms, so a choice can be traced to the term that decided it.
- **Maps:** recordings and strips draw danger shapes and the path the pilot chose, inside `#if RECORDING`, so a review sees what the pilot saw.

### Repeatable

The pilot uses the fight's random numbers and nothing else, so a pilot fight still repeats from its seed, and the stage check's repeatability test covers it.

## Two training targets

The same pilot is trained two ways, for different jobs.

### Like the designer

For balance testing. Batches only stand in for the designer's playtests if pilots play like them.

- **Skill settings:** human-like reaction, noticing and prediction noise.
- **Calibrated against the designer's recorded Grey Goo fights:** automatic calibration (`calibrate_pilot.py`, run review spec) adjusts a few of the expert's settings, such as reaction, how reliably it notices attacks and caution, until its health, drones, kills and level over the fight match the designer's at the same fight times. It needs only a few fights.
- **Until then:** batches read difficulty as easier than it plays for the designer, since the pilot outlives them. Comparisons between options, such as tuning changes, builds or enemy types, still come out in the right order, since the same pilot plays both sides.
- **Fitting weights to recorded moments, deferred:** the plan was to fit the scoring weights to each recorded moment, as a choice among candidate paths, the way `calibrate_build.py` fits the Designer build to the designer's picks. At the designer's choice, it is no longer required for phase 4: it is the costliest pilot stage, needing many recorded fights and a general fitting tool, while calibration gets batches near enough. It stays an option if batch numbers ever need to match the designer's play closely. (Why weights rather than a network, if it is done: a few dozen weights can be fitted from a handful of fights; a neural network couldn't be.)
- **Batches use it by default:** the expert.

### As strong as possible: the ceiling pilot

For finding what is broken, and how much headroom there is.

- **Skill settings:** perfect noticing of what it can see, with the longest look-ahead and the most paths that stay within budget.
- **Weights:** tuned by automatic calibration for results (wins, survival and level) rather than closeness to the designer.
- **Still fair:** it sees only what a player could see. It is a perfect player, not a cheating one, so its results say what a player could do.
- **Reports:** batch reports put its results beside the designer-like pilot's. The gap is how much better the game can be played than the designer plays it. Builds and upgrades that win far more often with it show where the balance breaks.

### Learning beyond this

A pilot trained by reinforcement learning, which only tries to win, may later help find exploits. It would read the same declarations and choose among the same options, so nothing here stands in its way. It is out of scope while tuning changes often, since each change would mean training it again.

## A generic library

At the designer's request, the general parts are a library that isn't tied to pilots or to VNP, called **Arbiter**: it weighs options and picks one, which is what every user of it does. (It was PilotKit while planned; the designer asked for a name that doesn't say pilot.) It should be able to drive other kinds of behavior in this game, and agents in other games. It is not machine learning: it chooses by scoring terms written by hand, and training means fitting their weights.

| Part | What it is | Who could use it |
|---|---|---|
| **Decision core** | Options scored by weighted terms, with skill settings, a record of each choice (the options, each term's value, and what was chosen) and explaining | Any agent making choices: a pilot's goal, path or upgrade, a drone's target, an enemy's attack |
| **Training tools** | Fitting weights to recorded choices; tuning weights by batch results | Any decision built on the core whose choices are recorded |
| **Motion planning** | Candidate paths on a plane, threat declarations and danger shapes, predicting threats along a path, built on the decision core | Any top-down real-time game |

- **What stays VNP's:** in `pilots/`:
  - its goals: harvesting, regrouping and drones;
  - its scoring terms;
  - the hooks its enemies and weapons implement;
  - the conversion between Godot's types and .NET's.
- **Training tools are Python:** they run outside the game, on the records the core writes, like the batch and calibration scripts.
- **Free of Godot and VNP:** the core and motion planning use plain .NET types (`System.Numerics` for vectors) and take their random numbers from the game. So they could serve another Godot C# project, or another .NET engine. Unreal's C++ could reuse the design, not the code.
- **Where it lives:** in this repository, in a folder of its own with its own project file. `VNP.csproj` references it and leaves its folder out of its own compile, so the build enforces the boundary: the library can't use VNP's code or Godot's. A `.gdignore` keeps Godot from importing it.
- **A second user proves it general:** until something other than movement uses the core, it is only a pilot library that claims to be general. Upgrade picks are the second user, since a fitting tool for them already exists. Drones' choices could follow later.
- **Packaging comes later:** packaging it for other projects (a repository of its own, a NuGet package, or a Godot addon) waits until a second project needs it.

### Staying general

Every change to the library has to keep it usable outside pilots and outside VNP, so a change made for one VNP need doesn't quietly tie it to VNP. Guards are added once the library exists and has its second user (stage 4), and settled with the designer then. Starting ideas:

- **The build boundary:** the library can't reference VNP's code or Godot (above).
- **A sample that isn't VNP:** a small program with a different kind of agent, such as a creature choosing what to eat and when to flee. The stage check builds and runs it, so a change that only makes sense for VNP's pilots breaks it.
- **No game words:** the stage check fails if the library's code names VNP's things, such as the VNP, drones, harvesting, Goos or asteroids.
- **Its purpose, written down:** a README in the library says what belongs in it and what goes in the game's code. CLAUDE.md and a skill step ask for each library change to be checked against it.

## What happens to the current pilot

`pilots/Pilot.cs` splits three ways:

- **Moved, not rewritten: the judgements.** These become the goal decision's scoring terms:
  - affording a fight, with threat and pressure that falls slowly (`PressureFall`);
  - low health;
  - regrouping;
  - harvest worth, with clusters and danger near material;
  - remembered material and exploring;
  - blending weapon profiles;
  - fair sight: what is on screen, within awareness.

  The settings calibrated for them (such as `StrengthPerDrone`, `HarvestAtCap` and the regroup thresholds) keep their values.
- **Replaced by path scoring: the code that turns wants into a direction.**
  - `KeepClearOfTouch`
  - `Dodge`
  - `FightMove`
  - `CircleTarget`
  - `EnemyAhead`
  - `LeadAcrossSpots`, with `IPilotLure`
  - the direction sums in `HarvestMove` and `Decide`

  Their settings, such as `TouchAvoidWeight` and the dodge chance, become scoring weights or noticing settings.
- **Unchanged at first: upgrade picks.** `ChooseUpgrade` and the builds stay as they are until they move onto the decision core (stage 4, now done).

**During the rebuild:** the old pilot stays as it is, chosen by a setting, as the baseline the new one must match on the same seeds.

**Once the new pilot matches it (stage 7):**
- The old movement code is deleted, along with the tuning variants that only switch off parts of it that no longer exist, such as `no_touch_avoidance` and `old_seeking`.
- Variants that change settings which survive are kept.
- Git history keeps all of it.
- The automated playtesting spec keeps its notes on the old pilot, as the record of what was learned. Most of that, such as how the designer plays and what failed, feeds the scoring terms.

## Settled during the build

The prototype now does the following; any of it can still change.

- **Arbiter (stage 1):**
  - **Where it lives:** `arbiter/`, with `Arbiter.csproj`, a README saying what belongs in it, and a `.gdignore`.
  - **How it builds:** `VNP.csproj` references it and leaves its folder out of its own compile, and it is in `VNP.sln`, so all three of the game's builds build it.
  - **Checked on its own:** `arbiter/checks/` is a small program, with no game, that checks its geometry and perception. The stage check runs it, which also proves the library builds without VNP or Godot.
  - **The decision core and motion planning** moved to stage 2, to be built with the goals and terms that use them rather than ahead of them.
- **Perception (stage 1):** Arbiter describes what an agent perceives as a `Situation`, which the game clears and fills before each decision:
  - **Bodies:** position, velocity, size, the game's ID and kind, and tags whose meaning the game decides (VNP's are in `ThreatTags`: hurts by touch, drains drones, shoots).
  - **Hazards:** an area (a circle, capsule, cone or ring, moving at a velocity), when it lands, how long it lasts, how much it hurts, whether per second, and whom: the agent, its allies, or both.
  - **Zones:** where the agent's own effects work: a reach around it, a band such as an orbit, or a trap left in place.

  Areas give a distance that is negative inside, so a planner can ask both whether a path is hit and how close it comes.
- **The hooks (stage 1):** two hooks, both required by the build (a type or weapon without one doesn't compile), plus one on the projectiles:
  - **Enemies:** `EnemyBehavior.DeclareThreats`.
    - **Grey Goo:** declares its body, tagged as hurting by touch, with a hazard to the VNP that lasts as long as it does. A type that drains drones adds the same area draining drones each second. Its touch cooldown isn't declared, since a player can't see it.
    - **The basic enemy:** declares only its body, tagged as one that shoots. It gives no warning before firing, so its shots are declared as they fly.
  - **Shots:** `Projectiles.DeclareThreats` declares every enemy shot as a moving point, harmful until it fizzles, to the VNP or a drone.
  - **Weapons:** `VnpWeapon.DeclareZones`.
    - the Pulse Laser, Scatter Shot, Seeker Missiles and Shockwave declare their reach;
    - Orbital Shards declare the band their shards sweep;
    - Mine Layer declares each mine as a trap the width of its blast.

  The first pilot read mines through `IPilotLure` until its code was removed (stage 7).
- **The decision core (stage 2):** `Arbiter.Decision`.
  - **Terms:** named terms, each worked out for every option and added up by its weight. Weights can change from one choice to the next. A term at weight 0 isn't worked out.
  - **Noise:** optional, drawn from the game's randomness through `IRandom`, so fights stay repeatable.
  - **Records and explaining:** every option's value for every term is kept, for explaining now and fitting later. `Explain` prints the best few options with the terms that made them.
- **Path planning (stage 2):** `Arbiter.PathPlanner`.
  - **Candidates:** a direction at each of a few strengths (full, half and a fifth) and letting go, held over the look-ahead. Each is simulated with the agent's own acceleration, deceleration and control lag, which for VNP is the stick's swing.
  - **Assessment:** against the hazards the agent noticed, each path gets three values:
    - **harm:** a hit counts once; harm per second counts for the time inside;
    - **closeness:** each hazard counts from 0 at the keep-clear distance to 1 touching;
    - **danger to allies:** taken as riding within an ally radius of the path.
  - **Noticing:** `Situation.Overlook` drops fleeting hazards (shots, attacks being readied) by a notice chance; lasting ones are always noticed.
  - **Cost:** it looks only at its **attention**'s worth of the nearest hazards, and checks each only against the paths it could come near. A body's touch and drain share one area, so they are measured once.
- **The planning pilot (stage 2):** `pilots/Pilot.Planning.cs`, flown by every skill level (it was chosen by a `Planner` setting until the first pilot's code was removed).
  - **Judgements:** the first pilot's, kept in the same order: affording a fight, regrouping, withdrawing, the harvest target, committing, holding still.
  - **Moving:** instead of adding wants up, each want is a term on whole paths:
    - harm, as a share of its health now, so the same hit matters more at low health;
    - closeness to hazards;
    - danger to its drones, by how much it relies on them;
    - getting within beam reach of its harvest target, so a path that overshoots scores worse;
    - keeping its fighting distance from where the enemy will be, with ApproachEnemies for closing in;
    - circling, or holding still;
    - backing off;
    - leading chasers across declared traps;
    - exploring;
    - keeping moving round a rock, for Mine Layer;
    - a small cost for turning sharply.

    Committing and holding still at a rock set the weights as the first pilot's overrides did.
  - **New settings in the skill levels:** `LookAheadSeconds`, `PathDirections`, `Attention`, `HarmWeight`, `DroneDangerWeight`, `TurnCost`, `OrbitShare` and `CommitWeight`. Its notice chance is the existing `DodgeChance`.
  - **Attention for enemies and rocks too:** it keeps track of the nearest enemies and considers the nearest rocks, up to its attention, and weighs danger for that many rocks at most.
  - **`explain`:** each line starts `PLANNER` and the goal, then the judgements, then the best three paths with their terms.
- **Cost (stage 2):**
  - **The harvest choice, for both pilots:** it now works out each rock's best possible score first, which danger can only lower. It then weighs danger from the best down, stopping once no rock left could win. Both pilots' fights traced identically before and after. The first pilot's slowest 1% in the tenfold crowd fell from about 7 ms to 0.7 ms.
  - **The planning pilot:** about 0.2 ms for its slowest 1% in the late fight, and 1.65 ms in the tenfold crowd. Before attention, it was 15 to 27 ms there.
- **First comparison (stage 2):** 50 expert fights each, on the same seeds, with the Designer build.

  | | First pilot | Planner |
  |---|---|---|
  | **Grey Goo only:** won | 40% | 52% |
  | **Grey Goo only:** destroyed by the 8:00 cap | 15 | 0 |
  | **Grey Goo only:** touched a contact enemy, a minute | 0.50 | 0.07 |
  | **Grey Goo only:** indecision, a minute | 0.46 | 0.15 |
  | **The phase 3 mix:** won | 68% | 92% |
  | **The phase 3 mix:** alive at 5:00 | 22 of 50 | 49 of 50 |
  | **The phase 3 mix:** median time destroyed | 4:43 | 6:03 |
  | **Gap to the designer's 18 fights** (phase 3 mix) | 0.168 | 0.308 |

  - **Results:** the planner beats the first pilot.
  - **Gap:** it is farther from the designer, since it outlives them: 98% alive at 5:00 against their 39%, with more health from 3:00. Its drones still collapse after 3:00, like the first pilot's.
  - **Detectors that worsened:** creeping, from 0.01 to 0.09 a minute, and drones not rebuilt, from 0.20 to 0.26.
  - **Tuning it down didn't help:** minding harm less (`HarmWeight` 4) or noticing less (`DodgeChance` 0.6) barely changed its survival. Its advantage is structural: it predicts where Goos and shots will be.
- **The planner replaced the first pilot (stage 2):** at the designer's choice, on results alone, against the spec's rule of results and the calibration gap both.
  - **Why the gap could wait:** the gap measures closeness to the designer's fights on the phase 3 mix, which phase 4 no longer plays. And the first pilot couldn't react to the Lancer without the per-type code this rebuild avoids.
  - **The gap moves to stage 5:** calibrating the designer-like pilot against the designer's recorded Grey Goo fights.
  - **Meanwhile:** batches run a pilot that survives better than the designer, so balance reads easier than it is.
  - **What changed:** `Planner` was turned on in every skill level, with variant `first_pilot` flying the first pilot for comparison, until its code was removed (stage 7).
- **The first pilot's code removed (stage 7):** at the designer's go-ahead, once the planner had replaced it.
  - **Removed:** the first pilot's `Decide`, which added its wants up into one direction, and the steering only it used: `Dodge`, `KeepClearOfTouch`, `FightMove`, `CircleTarget`, `LeadAcrossSpots` with `IPilotLure` (and Mine Layer's `CollectLureSpots`). Also the `Planner` switch and the two settings only the first pilot read, `DodgeWeight` and `TouchLookAhead`. The planning pilot notices shots by `DodgeChance` and predicts contact enemies along its paths instead.
  - **Kept:** every judgement the planner makes, in `Pilot.cs`: affording a fight, pressure, regrouping and withdrawing, the weapon profiles' blend, the harvest choice with clusters and danger, committing and holding still, remembering material and exploring. Also `EnemyAhead` and the upgrade picks.
  - **Variants:** `first_pilot` was deleted. `no_low_health_care` lost its `TouchLookAhead` lines and keeps `LowHealthBelow`. The others change settings the planner still reads, so they stay. `calibrate_pilot.py` no longer searches `DodgeWeight`.
  - **Checked:** four 2½-minute expert fights, one starting with Mine Layer, traced identically before and after at every checkpoint, with the first pilot's one random draw at the start (which way it circled) kept for the comparison.
  - **Seeds:** that draw was then removed, so a seed now plays out differently from the same seed before this change, as after any tuning change. Replay commands in older batch reports no longer match.
  - **Cost:** `Pilot.cs` went from 921 lines to 565.
- **Upgrade picks on the decision core (stage 4):** the decision core's second user.
  - **The decision:** `Pilot.ChooseUpgrade` is an `Arbiter.Decision` named "upgrade", the offered upgrades its options, with three terms: `liking` (the build's weight for the upgrade, its own or its category's, at weight 1), `owned` (1 if it improves something already owned, weighted by `FavourOwned`) and `new weapon` (1 if it gives a new weapon, weighted by `NewWeaponWeight`). The noise is the build's `Randomness` plus the skill level's `PickNoise`, drawn through the decision as before. A reroll still follows `RerollBelow`, on the chosen option's total.
  - **Picks unchanged:** four expert and novice fights with the Designer, Greedy, Weapon focus and Random builds, one with a reroll, traced identically before and after.
  - **Explaining:** with `explain=`, the harness prints each pick as a `PICK` line: every option with its total and the terms that made it. Arbiter's explanations now include the noise each option got (`NoiseOf`), so the terms add up to the total.
  - **Training tools begin:** `arbiter/training/fit_choices.py` fits a decision's term weights to recorded choices, as softmax choices over the same weighted sums the core scores, with terms optionally pulled toward a group's weight. It is plain Python, free of VNP's words. `calibrate_build.py` now describes each of the designer's level-up choices in the upgrade decision's terms (`liking:<upgrade>`, grouped by category, `owned`, `new weapon`) and fits them with it, giving exactly the weights the old fit gave.
  - **Guards for staying general,** chosen by the designer from the starting ideas (see Staying general):
    - **the build boundary,** as before: Arbiter can't reference VNP's code or Godot;
    - **no game words:** the stage check fails if Arbiter's code or training tools name VNP's things (the VNP, drones, Goo types, asteroids, debris, harvesting, VNP's weapon names, pilots, Godot). Generic words that VNP also uses, such as mine or weapon, are allowed, and the README may name VNP as where it came from;
    - **its purpose, written down:** the README says what belongs in it, `CLAUDE.md` asks for each change to be checked against it, and the check-stage review asks the same.
    - **Not chosen:** a sample agent that isn't VNP's, built and run by the stage check. It stays an option if the guards above miss something.
- **The ceiling pilot (stage 6):** `pilots/skill_levels/ceiling.tres`, flown with the Greedy build (the open item's default).
  - **Skill settings:** the expert's, with perfect noticing (`DodgeChance` 1), no wander, no pick noise of its own, and the longest look-ahead and most paths within budget: 1.5 s, 24 directions, attention 96. Its reaction time stays the expert's, so it plays at a person's speed. At 2 s and 32 directions its slowest 1% of decisions took 2.9 ms in the late fight, which with the rest of game logic would pass the 4 ms budget; at 1.5 s and 24, 1.4 ms.
  - **Tuned for results:** `calibrate_pilot.py --target results` scores a candidate by its shortfall: 1 less half the win rate and half the average share of the cap survived. It searches the planner's own weights as well as the judgements. Run with 100 seeds a candidate, confirming on seeds 201 on, over two passes, it kept one change, `HarvestAtCap` 0.9 to 1.0. Every other setting was as good as it gets a step either way, so the expert's settings already sit near the best for results; what holds the ceiling pilot back is behaviour, not settings. The search now remembers what it has measured, since fights repeat exactly, so a later pass doesn't play the same candidate again.
  - **Results:** on 400 fresh seeds (301 to 700), Greedy build, it won 92% against the expert's 90%, and was alive at 5:00 in 357 fights against 319. The headroom over the designer-like pilot is small in wins and clear in survival.
  - **Reports:** a batch with more than one pilot (`--pilots expert,ceiling`) adds a Pilots compared section: each pilot's results, each starting weapon's win rate for each pilot with the gap, and upgrades by lift (how much more often fights that took one were won) for each pilot, the ones whose lift grows most with the better pilot first.
  - **What it showed so far:** Mine Layer wins nearly every fight with either pilot (99 to 100%), so its strength isn't a pilot effect; Orbital Shards is the weakest start with either (72 to 75%). An apparent Orbital Shards gap in the first, smaller comparison (77% against 64%) was chance: on the same seeds each pilot won some the other lost, 16 against 8, and on another 50 seeds the ceiling pilot did slightly better.
  - **A behaviour to fix, for both pilots:** reviewing a ceiling pilot loss with the overview maps (seed 74, Orbital Shards) showed it losing its drones at 1:10 and then being chased for four minutes by Goos about 8 units away, never able to stop and rebuild. Across 1,000 expert fights, the drones-not-rebuilt detector fires 0.37 times a minute against the designer's 0.03, and drones left fighting 0.78 against 0.23. Since enemies chase at 90% of the VNP's speed, regrouping by moving away rarely shakes them off. Not changed yet.
- **Recorded and drawn (stage 1):** every picture of a recording holds the hazards and zones, as a pilot would perceive them around the VNP.
  - **On strips:** hazards are drawn yellow if they hurt the VNP and pink if they hurt drones, dimmer if not yet landed, each with a line to where it moves in the next second. Weapons' reach shows as faint rings.
  - **Size:** this added about half to a recording in a dense fight, about 830 KB for a 5-minute fight instead of 560 KB.
  - **Checked:** the full recording check fails if bodies that hurt by touch, drains, enemy shots, or any weapon's zones go missing.

## Tuning values

| Value | Where |
|---|---|
| Each skill level's reaction, awareness, noticing, prediction noise, look-ahead, number of paths and wander | its file in `pilots/skill_levels/`, the ceiling pilot's included |
| Each scoring term's weight, for each training target | the skill level files: the expert's for the designer-like target, the ceiling pilot's for the other |
| Each build's weights for upgrade picks | its file in `pilots/builds/`, as now |
| Each attack's danger shape: size, timing and damage | taken from the attack's own values in the enemy type's file, so they can't disagree |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **The guards for staying general:** settled with the designer once the library has its second user (see Staying general).
- **The ceiling pilot's upgrade picks:** the Greedy build for now. A build learned from batch results (which upgrades win most, given what is already owned) may come later.
- **How many designer fights calibration needs:** a few; calibrate on what exists, and confirm a change on a second set of seeds as the calibration tools already do, so too little data shows as changes that don't hold up.
- **Wave timing:** deferred in the run review spec. It fits in the goal decision when wanted.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Suggested build order

Build one stage at a time, and stop after each so the designer can try it.

1. **The library and perception:**
   - Arbiter, in its own project, with its perception types;
   - threat declarations and danger shapes;
   - the enemy hook for the basic enemy and the basic goo;
   - enemy shots;
   - weapons' effective areas;
   - danger shapes drawn on strips.
2. **Goals and path scoring (phase 4's stage 2):**
   - the decision core and motion planning in Arbiter, built with the goals and terms that use them;
   - the current wants rebuilt as goals and scoring terms, with `explain`;
   - built beside the current pilot, chosen by a setting, and compared in batches on the same seeds;
   - it replaces the current pilot only once it matches or beats it on results and on the calibration gap. (Done, on results alone, at the designer's choice: see Settled during the build.)
3. **The first declared attack:** the Lancer (phase 4's stage 3). This tests the design: the pilot dodges a forming Lancer with no code for it.
4. **The second user, and staying general:** (done, see Settled during the build)
   - upgrade picks moved onto the decision core;
   - `calibrate_build.py` fitting the Designer build's weights to picks made on the core;
   - the guards for staying general, as settled with the designer.
5. **Calibrating to the designer's Grey Goo fights,** for the designer-like target, with automatic calibration, once the designer has played a few recorded Grey Goo fights. (Fitting weights to recorded moments was planned here; deferred at the designer's choice, see Like the designer.)
6. **The ceiling pilot,** and reports with both targets side by side. (Done, see Settled during the build.)
7. **Removing the old pilot's code.** (Done, see Settled during the build.)

## Out of scope

- Reinforcement learning and neural networks.
- Packaging the library for other projects.
- Moving drones, enemies or other behavior onto the decision core, beyond upgrade picks. Possible later, one user at a time.
- How rival and autonomous VNPs behave on the galactic map.

## Done when

- A new enemy type's attacks are handled by pilots through its declaration alone.
- The new pilot matches or beats the old one in batches, and has replaced it.
- The decision core has two users in VNP, movement and upgrade picks, and its guards for staying general are in place.
- Arbiter builds on its own, without VNP's code or Godot's.
- The designer-like pilot is calibrated against the designer's recorded Grey Goo fights, and the ceiling pilot's results are reported beside it.
- A pilot fight repeats from its seed, and several pilots stay within the performance budget.
