# Phase 4 addendum: what changed in building and testing

`PROTOTYPE_4_SPEC.md` is the designer's intent, as given, and is never edited. This file records only what an AI working on the project needs in order not to get it wrong: where the build now differs from the spec, why, what was tried and rejected, and choices made for items the spec left open. Where this file and the spec disagree, this file wins.

An entry belongs here only if, without it, the AI would do something differently or worse. Test measurements, process stories, implementation details the code already shows, and repeats of the spec stay out; batch reports and git history keep those.

## Status

- Build order: all nine stages are built (the order gained a stage, see Pilots). Of the pilot spec's later stages, removing the old pilot, upgrade picks on the decision core with its guards, and the ceiling pilot are done; still to do: calibrating the designer-like pilot.
- Done when: met, except the pilot items above. Each type's performance cost was measured at its stage; all were close to the basic goo's.
- The designer is reworking how fights feel. Recorded-fight calibration of pilots waits until they are happy with it.
- **Fight pacing comes next,** at the designer's request, before the next phase's weapons (the spec's "Next phase, planned"). It goes through a Fight Director, described in `docs/dev/FIGHT_DIRECTOR_SPEC.md`. Why: fights feel repetitive, and late fights leave running as the only answer, which is likely why Mine Layer wins nearly every fight. Each change the director makes to fights is recorded here as it is built.

## Shared traits

- **One grey for every type:** every type is the same satin grey (`Color` in each type's file, all alike), darker than the asteroids, with a faint rim of light (`materials/goo.tres`). Types are told apart by silhouette alone.
  - Rejected: a pale tint per type; glossy.
  - Since types share a colour, maps give each type a palette colour of their own (run review spec).
- **Declared threats:** every type declares its body and its attacks' danger shapes once their tells are visible (`EnemyBehavior.DeclareThreats`, required by the build). This was added with the pilot rebuild.
- **What each type chases:** this is set by `Targets` in each type's file (the nearest of drones and the VNP, or the VNP only), which replaced `TargetsDronesFirst`. Every Grey Goo type goes for the VNP only.
  - **Why:** basic goos spent almost all their time chasing drones, which fly close round the VNP, so they never seemed to come for it.
- **No flocking while chasing:** `ChaseFlocking` is 0 for every Grey Goo type, so each heads straight at its target. They still keep apart, and idle groups still wander together.
  - **Why:** flocking pulled chasers well off their line.
- **Speed:** every type that chases moves at 90% of the VNP's starting speed (`Speed`, all alike), at the designer's choice.
  - Newly spawned enemies get faster each minute (`RampEnemySpeedPerMinute` in the fight plan's difficulty curve, `director/curves/`), and each escalation step adds more (`EscalationEnemySpeedPerStep` in `tuning/difficulty.tres`). Each enemy keeps the speed it spawned with.
  - The ramp is set so that enemies spawned near the time limit reach about the VNP's starting speed. Movement speed upgrades keep the player ahead, and escalation then takes enemies past it. Rejected: a steeper ramp.
  - Exceptions: the Burster, a little faster; the Devourer, which never chases; and the asteroid mimic, slow, as a lumbering rock.
  - Wander speed and acceleration are unchanged.
  - Escalation speeding enemies up changes phase 1, where escalation left speed alone.
- **Turning and health:** every type but the Devourer accelerates and turns sharply (`Acceleration`, `TurnSpeed`) and has 30% less health than before, at the designer's request.
  - **Why:** goos slid sideways through turns, taking nearly three seconds to reverse.
- **Shared tuning that isn't in a type's own file:** Grey Goo's health and damage ramps are in the fight plan's difficulty curve (`RampGreyGoo…`, `director/curves/`), and its escalation in `tuning/difficulty.tres` (`EscalationGreyGoo…`). Spawn weights and `MaxAlive` are in the Grey Goo faction's file, `enemies/grey_goo/grey_goo.tres`; when groups and waves come, and how large waves grow, is in the fight's plan, in `director/plans/` (the Fight Director's). Shared fields come from `GreyGooDefinition`, including `HoldDistance` and `HoldBlendDistance`: 0 closes in all the way.
- **Attack ranges:** they are measured from the enemy's edge for every type: `LaunchRange`, `VolleyRange`, `Reach` and `FireRange`.

## Basic goo

- **Targets and flocking:** it now goes for the VNP only (see Shared traits), which changes phase 3's targeting for the Goo. It still drains and absorbs drones that touch it.
- **ID:** it is `basic_goo`. Fight logs from before phase 4 call it `goo`.
- **Split push:** halves are pushed apart at `SplitPushSpeed`. Before the speed change, the push used its chase speed.
- **Splitting into other types:**
  - The pick covers every Grey Goo type in the fight except the basic goo, found from the types themselves, so a new type joins it with no change.
  - The other type is fresh at the fight's current difficulty. The other half's share of what the goo had grown is simply lost, which was a choice: the spec didn't say.
  - A mimic made this way is awake. A Devourer made this way goes about its business.

## Lancer

- **Aim:** its aim is fixed as it starts forming, so stepping off the line dodges it. The spec didn't say; this choice gives the warning the spec asks for.
- **Launch:** each launch carries `LaunchBeyond` past the point it aimed at, at the designer's request: farther, and twice as fast as first built.
  - Rejected: a fixed distance from its start, which barely reached a target at the edge of its range.
- **Touch during a launch:** a launching Lancer doesn't also hurt by touch; the launch is the hit.
- **Holds its distance:** between launches it keeps `HoldDistance` from the VNP, inside its launch range, instead of drifting straight at it. It grips harder than the other types (`Acceleration`).
- **Gliding:** it glides on at its chase speed after a launch, so since the speed change it glides farther. Watch this in playtesting.

## Quiller

- **What breaks spikes:** only Orbital Shards with blocking and weapons marked `Explosive` in their file break spikes. That is Mine Layer and Seeker Missiles now, and any later weapon only if its file says so. This replaces the spec's "any damage that touches it". Shots, Shockwave rings and ramming drones pass through spikes. Burster blobs follow the same rule.
- **Volleys:** a volley aims at the VNP only; drones are hit only when a spike passes through them. This replaces the spec's spikes shared among the VNP and drones.
- **Firing:** spikes leave from every side and curl in on target points spread round the VNP (`OutTurnDegrees`). Each flies `SpikeBeyond` past its point, then arcs home.
  - Rejected: a forward spread; a reach set from the target's distance plus a margin; the same reach for every spike.
- **Coming home:** spikes turn back hard (`ReturnTurnDegrees`), and near home they head straight in.
  - Rejected: a gentler return, which let spikes loop round their Quiller.
- **Holds at range:** between volleys it keeps `HoldDistance` from the VNP, inside its volley range, rather than closing in to touch. It also moves faster than the basic goo and the Lancer.
- **Losing spikes:** each spike broken costs its Quiller `SpikeLossHealth`, credited to whatever broke it.
- **For pilots:** while forming, it declares a small circle at each target point, not a line, since its spikes curve.
- **Performance:** spikes need the spatial grid. Rejected: finding each by scanning the enemies, which was far too slow in a crowd.

## Spiker

- **Look and size:** it is a four-armed caltrop, drawn a third smaller (`Size`); its reach and hit size are unchanged.
  - Rejected: an urchin, which looked too like the Quiller.
- **Moving and spikes:** it moves after the VNP only and hurts it by touch. Its spikes favour drones: the nearest drone in reach that no other spike is after, and the VNP only when no drone is in reach.
  - Rejected: spikes at every drone and the VNP at once; spikes at the VNP only, which rarely caught drones.
- **Spikes per thrust:** each thrust sends 2 spikes (`MinSpikes` and `MaxSpikes`), set by the designer after a while at 1.
- **Aim:** it rolls an accuracy as it spawns, raised by the difficulty ramp, like the Lancer. A spike at the VNP has its line fixed as forming starts, so it can be dodged. A spike at a drone is aimed as the thrust starts and swings toward the drone as it goes (`TrackDegrees`). Its cooldown is `ThrustCooldown`.
- **Impaling:** any drone the tip meets first is impaled, whether or not it was the one aimed at. The spec didn't say; this was the choice. A drone already on a spike can't be caught by another.
- **Holding drones:** a drone held by an enemy that stops holding it is released a moment later, so no drone is ever left stuck.

## Burster

- **Out of fights for now,** at the designer's request: too aggravating to fight, and too like the Quiller. It is out of Grey Goo's weight lists (`enemies/grey_goo/grey_goo.tres`), so fights don't know the type: it never spawns, and a basic goo's split can't become one. Its files and its tier stay, so it comes back by listing it in both weight lists again. While it is out, the scenario harness can't spawn it either. This changes the spec's six other types to five.

- **Speed:** a little faster than the other types, which since the speed change all move alike.
- **Tell:** it throbs, faster the more it is hurt (`HurtThrobFactor`).
- **Bursting:** only a Burster that is killed bursts; one cleared away far behind does not. Breaking a blob costs no one health, since its Burster is gone.
- **Exploding blobs:** a blob explodes as it pops on the VNP or a drone, hurting the VNP and every drone within `BlobBlastRadius`, at the designer's request. It never hurts enemies and can't break other fragile shots. A blob that runs out of life fizzles.
- **Blob speed:** blobs fly exactly at the VNP's starting speed (`BlobSpeed`), at the designer's choice. A VNP fleeing straight keeps its distance; one that turns or stops gets caught.
  - Rejected: slower than the VNP, which pilots simply outran, and later made the Burster most of the damage taken; a little faster than the VNP.

## Devourer

- **It never chases,** at the designer's request: not when the VNP is close, not when it is shot, not in a wave (`EnemyBehavior.Chases`). This settles the spec's open item on its targets. Its aggro ranges and targeting are unused, and it isn't drawn darker when idle.
- **Finding material:** `SeekRange` is just inside `ActiveRadius` in `asteroids.tres`, since beyond that nothing is kept findable. It searches near first, so it still heads for the nearest material.
- **No ripples:** it hurts only by touch and by draining drones. Ripples were built as the spec described and then removed at the designer's request. The spec's ripple tell no longer applies.
- **No sound while consuming,** at the designer's choice.
- **Producing:** this was added by the designer and isn't in the spec. While the VNP is within `AttackRange` of its edge, it produces a basic goo once its cooldown is done and it has `ProduceMass` stored. The goo is pushed toward the VNP at `ProducePushSpeed`, then behaves as any basic goo, new, at the fight's current difficulty.
- **Storing, then attacking:** it stores what it consumes as growth. Each goo costs `ProduceMass` of the store, and it shrinks by the growth that mass gave it.
  - It never shrinks below its starting size, and loses health by the same share, so producing never kills it.
  - Spent mass no longer counts toward its debris.
  - At `MaxBulk` it stops consuming until producing makes room.
  - Rejected: producing on a cooldown wherever the VNP was, without shrinking.
- **Adding enemies mid-update:** an enemy can't be added during the enemies' own update; use `EnemySwarm.QueueSpawn`, as splits do.
- **Never in waves,** at the designer's request: its wave weight is 0. This replaces the spec's "waves can include every type that spawns in groups".

## Asteroid mimic

- **Asleep:** a sleeping mimic isn't an enemy but an asteroid marked as a mimic (`Harvestables`), so neither a player nor a pilot can tell. Which fields hold mimics comes from a generator of its own, seeded from the field's position, so placing them doesn't change the fight's random numbers.
- **Waking:** only the VNP coming within `WakeRange` wakes it; drones near it don't, at the designer's request. Harvesting it still wakes it, as the spec says. A Devourer touching one wakes it unhurt rather than eating it.
  - **Why it has its own range:** `AggroRange` once did both waking and noticing targets, so an awake mimic barely noticed anything. Awake, its aggro ranges work like the basic goo's.
- **When it appears:** the waking mimic is added at the start of the next update.
- **Spawning:** never in groups or waves; its weights are 0. It is listed in Grey Goo's weights (`enemies/grey_goo/grey_goo.tres`) only so the fight knows the type, and its `GroupSize` is unused.

## Spawning

- **Fights follow plans,** at the designer's request: the Fight Director (`docs/dev/FIGHT_DIRECTOR_SPEC.md`) picks one of Grey Goo's plans for each fight by its seed, each with its own waves, difficulty curve and kill target. Why: fights all paced the same way felt repetitive, and late fights left running as the only answer.
  - **What it changes:** phase 1's waves, which are now one plan's (Classic's): the others time, place and make up their waves differently, and some add small unannounced waves between the announced ones. Phase 1's ramp during the fight, which is now each plan's. Phase 1's single kill target, which a plan can raise.
  - **Leaning on numbers:** plans other than Classic have no stat ramps before the time limit, so each enemy stays one drones can beat and pressure comes from how many there are. Why: with the stat ramps, drones were lost at the same rate but couldn't be rebuilt late, since tougher enemies meant fewer kills and less debris; raising drone health didn't help. Escalation past the limit is still stat-based for every plan.
- **Fights start with enemies in view,** at the designer's request: every plan's starting groups are placed on screen as well as off, each beyond its aggro range by the plan's `StartClearance`, so none notices the VNP at once. This changes phase 1's starting groups, which were off screen.
- **Idle enemies roam freely,** at the designer's request: they are no longer pulled back to where they spawned (`WanderHomePull` in `enemy_spawning.tres`, 0). This changes phase 1's idle groups, which wandered near home. A plan can also make them roam farther each way (`WanderLegScale`); Dense doubles it.
- **Enemies flock with their own group only,** at the designer's request: alignment and cohesion pull an enemy toward its own spawned group (a split's halves keep theirs), not every nearby enemy of its type. Why: once idle enemies stopped being pulled home, groups of a type drifted into each other and merged into big blobs, which group aggro then sent at the VNP all at once; in the designer's Dense run a pack of about twelve basic goos reached them seven seconds in. This changes phase 1's flocking.
- **Group aggro,** at the designer's request: when an idle enemy notices the VNP by sight, every idle enemy within `AlertRadius` (`enemy_spawning.tres`) notices too, and chases while the VNP is within its `LoseAggroRange`. One step only, so a single contact can't wake a whole field. This changes phase 1's enemies, each of which noticed only for itself.
- **Waves move in varied formations,** at the designer's request: as packs that hold together, loosely from many directions, or varied wave to wave, by each plan's wave pieces (`Formation`; see the Fight Director spec). This changes phase 1's waves, whose groups all arrived as tight clumps; Classic keeps that.
- **Fragile enemies in numbers:** a plan's groups and each waves piece have a `Strength`, multiplying the health and debris of the enemies they spawn (not their damage); a split's halves keep it. The Horde plan uses it for many fragile goos. A fragile goo is smaller, by area as its health (its size times the square root of its strength), at the designer's choice, so fodder reads as weaker at a glance.
- **The basic enemy:** its weights are 0, but its files and its entry in Grey Goo's weights (`enemies/grey_goo/grey_goo.tres`) stay. The scenario harness can still spawn it with `spawn=basic_enemy:N`.

## Pilots

- **Rebuilt before the Lancer,** as `docs/dev/PILOT_SPEC.md` describes. This added a stage 2, "Pilots that plan", to the build order; the types moved to stages 3 to 9. This answers the spec's open item on how pilots handle enemy types.
- **The pilot spec's later stages run between the types, where they fit:**
  - upgrade picks on the decision core, with the guards that keep it general (done);
  - calibration against the designer's recorded Grey Goo fights;
  - the ceiling pilot (done);
  - removing the old pilot (done).
- **Done when** gains: "Pilots react to each type's attacks through its threat declaration, with no pilot code for the type."
- **The planner replaced the first pilot on results alone,** at the designer's choice. It outlives the designer, so batches read the game as easier than it plays. Comparisons between options still hold.
- **Fitting pilot weights to individual recorded moments is deferred,** at the designer's choice. Calibrating settings against recorded fights replaces it.

## Weapons and other systems changed in phase 4

- **Seeker Missiles explode:** at the designer's request, every enemy within `BlastRadius` is hit (`seeker_missiles.tres`). A missile that hits a fragile shot explodes there.
  - A new upgrade, Seeker Missiles area of effect, widens the blast (`weapons/seeker_missiles/upgrades/area.tres`). The pool then had 40 upgrades; Harvest repair made it 41.
  - Enemies caught in the blast take `SplashDamage` of the missile's damage; the enemy hit takes the full amount. This was set to half at the designer's choice, because Grey Goo chases in tight packs and the blast had grown to nearly half the missiles' damage.
- **Seeker Missiles spread their targets:** a volley goes for up to as many different enemies in range as it has missiles, nearest first, one each. Missiles share an enemy only when there are fewer enemies than missiles. Each leaves on the side of its target, so missiles don't cross.
  - A missile whose target dies goes for the nearest enemy no other missile is after, or the nearest at all.
  - This changes phase 2's targeting.
- **The `Explosive` flag:** each VNP Weapon's file has an `Explosive` flag, which decides whether it breaks fragile enemy shots.
- **Sounds pause with the fight:** while a menu pauses the fight, the fight's own sounds pause and resume; menu and result sounds still play. This changes phase 2's "Sounds while paused".
- **The debug line is for development only,** at the designer's choice. The line at the top of the screen (fps, seed, health, counts) shows in the editor and in debug exports, never in a release build (`#if DEBUG` in `Fight.cs`). The result screen still shows the seed.
- **The damage breakdown collapses,** at the designer's request: each damage source is one line, and clicking it shows or hides its split by enemy type. Every source starts collapsed. A source shows "+" while collapsed and "-" while open, since the game's font has no triangle characters. This changes phase 3's breakdown, which always showed the split. Only the mouse opens them; like scrolling the panels, it has no controller or keyboard control yet.
- **Result panels scroll:** the damage breakdown and upgrade list grow until they would leave the screen, then scroll by mouse wheel or scroll bar. The damage breakdown stops above the weapon icons. Scrolling by controller or keyboard isn't built.

- **The camera's zoom and lens are separate,** after the designer found that field of view did little but zoom, and that a wide one stopped enemies appearing. `ViewHeight` (`camera.tres`) sets how much of the arena the screen shows. `Fov` now changes only how strong the perspective looks: the camera works out its own height to keep `ViewHeight`. This changes phase 1's camera, which had a set height that `Fov` zoomed from.
  - **Why wide lenses broke spawning:** past about 150 degrees at the 15 degree tilt, the top of the screen looked above the horizon, so "just off screen" no longer existed. Well before that, enemies spawned beyond `DespawnDistance` and were removed straight away.
  - **Guards:** `Fov` and `Tilt` are clamped, with an error, so the top of the screen stays at least 20 degrees below the horizon. A zoom wide enough to spawn enemies past `DespawnDistance` is reported as an error when the fight starts.
  - **The scene's Camera node:** its own `fov` is overwritten every frame, so only `camera.tres` counts.

- **A gentler experience curve,** at the designer's request: each level's increase is `ExperienceIncreaseDecay` (`levels.tres`) times the one before, so the early curve is nearly as it was and late levels grow dearer ever more slowly. This changes phase 1's experience per level, which grew by the same amount each level without end.
  - **Why:** the designer found the early curve good but late levelling slow; their experience came in at a steady rate, so ever-dearer levels meant gaps growing past half a minute.
  - **Rejected:** a cap on what one level costs (900), which made every level from 11 the same; the designer preferred a gentler increase. Variant `linear_levels` restores the old curve.
- **New drones without the menu:** once every other upgrade is used up and New drones is all a level-up could offer, it is taken without opening the menu, and the drones it built rise over the VNP as "+N" in the Drones card colour, at the designer's request. Why: in a long endless run the designer's last ~45 level-ups each opened the menu with New drones as the only card.
- **Harvest repair,** at the designer's request: harvesting repairs the VNP, by `RepairPerMass` health per unit of mass harvested by anything (its beam, its drones, Orbital Shards), up to its maximum. There is no other healing.
  - **Why:** a pilot that never harvested survived as long as one that played properly, seven levels behind with almost no drones, because running avoids damage and nothing else heals. Repair makes harvesting how the VNP stays alive.
  - **The base** (`RepairPerMass` in `vnp.tres`) was chosen with arrivals ahead, below, by a tuning pass that compared normal play against a pilot that never harvested. Repair alone barely separated them; with arrivals ahead it lets normal play survive as well as before while running gets clearly worse.
  - **The upgrade,** Harvest repair (`tuning/upgrades/harvesting/repair.tres`), adds a flat amount per pick, up to 4, filed under Stats like the other harvesting upgrades. Flat rather than a percentage, since the base was 0 when it was added.
- **Arrivals ahead of the VNP,** at the designer's request: the Swarm, Pincer, Rising and Horde plans send a share of their groups, and their front and pincer waves, toward a moving VNP's heading (`AheadShare`; see the Fight Director spec), so running in a straight line leads into fresh enemies. Classic doesn't, so it keeps the old pacing to compare against.
  - **Why, and what it did:** together with repair, a pilot that never harvested fell from 65 alive at 5:00 to 46 of 80, while normal play held at 68 against 65. Arrivals ahead without repair cost normal play too (51 of 80), since pilots move nearly all the time.

## Settled from the spec's open list

- **The Devourer and drones,** decided by the designer: it absorbs drones, draining those that touch it and growing from them, as the spec's default had it.
- **Drones and growth for every type,** decided by the designer: only the basic goo splits, and drones a Grey Goo type destroys without absorbing them (a Lancer hit, a Quiller spike) add nothing to its growth. A type grows only from drones it absorbs (and the Devourer from what it consumes). This was already the built default.

## Still open

These are from the spec's Not yet decided list. The built behaviour is the spec's default unless noted.

- **The Burster's tiny blobs:** they are as the spec describes, plus the explosion above.
