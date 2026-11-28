# Pilots addendum: what changed in building and testing

`PILOT_SPEC.md` is the plan as agreed with the designer, and isn't edited to record what the build did. This file records only what an AI working on the project needs in order not to get it wrong: where the build differs from the spec, why, what was tried and rejected, choices made for its open items, and where things live. Where this file and the spec disagree, this file wins.

An entry belongs here only if, without it, the AI would do something differently or worse. Measurements, tuning numbers that the files already hold, process stories and repeats of the spec stay out; batch reports and git history keep those.

## Status

- Stages 1, 2, 3, 4, 6 and 7 are built. Stage 5, calibrating the designer-like pilot to the designer's Grey Goo fights, waits until the designer is happy with how fights feel.

## Arbiter

- **Where it lives:** `arbiter/`, its own project with a README saying what belongs in it, and a `.gdignore`. `VNP.csproj` references it and leaves its folder out of its own compile; it is in `VNP.sln`, so every build builds it. `arbiter/checks/` is a small program, with no game, that checks it; the stage check runs it, which also proves it builds without VNP or Godot.
- **Perception:** a `Situation` the game clears and fills before each decision: bodies (with tags whose meaning the game decides; VNP's are in `ThreatTags`), hazards (an area, when it lands, how long, how much it hurts, whom) and zones (where the agent's own effects work). Areas give a distance that is negative inside.
- **The decision core:** `Arbiter.Decision`: named terms worked out for each option and added by weight; a term at weight 0 isn't worked out; noise drawn through `IRandom` so fights repeat; every option's value for every term kept, for `Explain` (which includes each option's noise, `NoiseOf`) and for fitting.
- **Path planning:** `Arbiter.PathPlanner`: candidate paths simulated with the agent's own acceleration and control lag, scored for harm, closeness to hazards and danger to allies. `Situation.Overlook` drops fleeting hazards by a notice chance; lasting ones are always noticed. Its cost is bounded by **attention**: only that many of the nearest hazards, each checked only against paths that could come near it.
- **Training tools:** `arbiter/training/fit_choices.py` fits a decision's term weights to recorded choices; `tools/calibration/calibrate_build.py` uses it for the Designer build.
- **Guards for staying general,** chosen by the designer from the spec's ideas: the build boundary; the stage check fails if Arbiter's code or training tools name VNP's things (generic words such as mine or weapon are allowed, and the README may name VNP); and its purpose written in the README, which CLAUDE.md and the check-stage review ask each change to fit. Not chosen: a sample agent that isn't VNP's.

## The hooks

- **Required by the build,** game code, not inside `#if`: `EnemyBehavior.DeclareThreats` and `VnpWeapon.DeclareZones`, plus `Projectiles.DeclareThreats` for shots in flight. An attack is declared from the moment a player could see it coming, and no sooner; a sleeping enemy declares nothing.
- **Strips draw what a pilot perceives:** hazards yellow if they hurt the VNP, pink if drones, dimmer before they land; weapons' reach as faint rings. The full recording check fails if bodies, drains, shots or zones go missing.

## The planning pilot

- **Where:** `pilots/Pilot.Planning.cs`, flown by every skill level; the first pilot's judgements are kept in `Pilot.cs` (affording a fight, pressure, regrouping and withdrawing, the weapon profiles' blend, the harvest choice, committing and holding still, remembering material and exploring) and feed its path terms.
- **It replaced the first pilot on results alone,** at the designer's choice, against the spec's rule of results and the calibration gap both, since the gap compared against phase 3 fights the game no longer plays and the first pilot couldn't handle the Lancer without per-type code. The gap moved to stage 5.
- **The first pilot's code is removed** (stage 7), with `IPilotLure`, `DodgeWeight`, `TouchLookAhead` and the `Planner` switch. Its one random draw went too, so seeds play differently from before.
- **Idle enemies,** from the designer's Dense fights: a player can tell idle enemies apart (they are drawn darker), so the pilot may too (`EnemySwarm.IsChasingOf`). It never picks a fight with an idle enemy, since going for one wakes it and, by group aggro, its neighbours; and idle enemies count for less threat (`IdleThreatShare` in the skill levels), so a calm field doesn't make it withdraw. Drones still attack idle enemies in their engage range; the designer chose to leave drone behaviour as it is.

## Upgrade picks on the decision core

- `Pilot.ChooseUpgrade` is a decision named "upgrade", with terms `liking` (the build's weight for it or its category), `owned` (`FavourOwned`) and `new weapon` (`NewWeaponWeight`); noise is the build's `Randomness` plus the skill level's `PickNoise`; a reroll follows `RerollBelow` on the chosen option's total.
- The harness's `explain=` prints each pick as a `PICK` line.

## The ceiling pilot

- `pilots/skill_levels/ceiling.tres`, flown with the Greedy build (the spec's open item's default): the expert's settings with perfect noticing, no wander or pick noise of its own, and the longest look-ahead and most paths within the performance budget. Longer look-ahead with more paths took its decisions over game logic's budget in a late fight, so that limits it, not results.
- **Tuned for results:** `calibrate_pilot.py --target results` searches its settings, the planner's weights included, by wins and survival. The expert's settings already sat near the best; what holds both pilots back is behaviour, not settings.
- **Reports:** a batch with more than one pilot gets a Pilots compared section.

## Settled from the spec's open list

- **The guards for staying general:** above.
- **The ceiling pilot's upgrade picks:** the Greedy build.

## Still open

- **Stage 5,** calibrating the designer-like pilot to the designer's Grey Goo fights, once fights feel right.
- **Drones not rebuilt when chased:** once a pilot loses its drones, enemies chasing at 90% of its speed keep up, so it can rarely stop to rebuild; this is behaviour both pilots share, which no setting fixes.
- **Wave timing:** pilots don't estimate the next wave from waves seen (deferred in the run review addendum).
- **How many designer fights calibration needs.**
