# Automated playtesting addendum: what changed in building and testing

`AUTOMATED_PLAYTESTING_SPEC.md` is the plan as agreed with the designer, and isn't edited to record what the build did. This file records only what an AI working on the project needs in order not to get it wrong: where the build differs from the spec, why, what was tried and rejected, choices made for its open items, and where things live. Where this file and the spec disagree, this file wins; the pilot spec and its addendum win over both on how pilots decide.

An entry belongs here only if, without it, the AI would do something differently or worse. Measurements, tuning numbers that the files already hold, process stories and repeats of the spec stay out; batch reports and git history keep those.

## Status

- Stages 1 to 5 are built. Stage 6, an editor button, only if the designer wants it.
- The performance requirement grew into its own spec, `PERFORMANCE_SPEC.md`.

## Seeds and repeatable fights

- **Where a fight is seeded:** as the fight scene enters the tree, before any of its parts are ready, since the asteroids are placed as soon as they are. `Fight.NextBrief` sets the next fight's seed and requests (see the Fight Director addendum).
- **Choices that mustn't shift a fight** (asteroid amount, the director's) use generators of their own seeded from the fight's seed; batches' random starting weapons follow the seed alone, so a seed starts the same way in every batch.
- **Gameplay measures the screen from the camera's steady view,** at the project's reference 16:9 shape, without the screen shake and independently of the window. Why: the shake's own randomness and the window's size both once moved spawn points, so fights differed between runs. Trade-off: in a window much wider than 16:9, enemies can spawn just inside the sides.
- **Checking:** `trace=N` prints the fight's state every N frames, with how many random numbers have been drawn, so the first differing line shows where two runs parted; the stage check runs the same fight twice and compares.

## Fight statistics and logs

- **Only health actually taken off counts,** for damage to enemies and to the VNP, so a big last hit doesn't inflate it.
- **Damage sources:** every hit carries what dealt it; each Drone Weapon counts separately.
- **Logs are written** when a fight ends, again if an endless run then ends, as "unfinished" if it is left, and as "capped" at a batch's cap. `WriteLogs` in `fight_log.tres` turns them off. `check_log.py` in the check-stage skill cross-checks a log's numbers.
- **Frame times are measured by the log itself,** from the start of the fight's update to its end. Rejected: Godot's frame monitor, which includes waiting for the screen.
- **Samples keep a fixed cadence in fight time.**
- **Damage by minute:** `damageByMinute` holds each source's running total every minute and at the end, for the report's Build paths section.
- **The HUD clock counts down,** so a moment the designer reports by the clock, such as 3:30, is 1:30 of fight time in logs and traces.

## The scenario harness

- **Arguments are checked,** at the designer's request: a mistyped option, a value of the wrong kind or an ID that doesn't exist is a `SCENARIO ERROR`, with the closest match, and exit code 2, without playing. Batches count it as failed.
- **Where pilot fights are logged:** `playtests/pilot/` by default; batch fights in their batch's folder; never among the designer's own logs.
- **Ending:** `maxtime=` stops a fight at that fight time, as "capped", and sets the frame limit to reach it; a fight watched in a window with neither plays until it ends. A window stays open on the final screen unless `close=yes`. `fast=yes`, in a window, turns off vsync and the frame cap, the same fight sooner.
- **The fight pauses in the harness as in a game.** Why: it once kept playing behind level-up offers and the result screen, which skewed every pilot result until it was found.
- **Quitting cleanly:** `ShutdownCleanup` has .NET clean up while Godot still runs, when the window is asked to close and before the harness quits. Why: arrays left for .NET to free after Godot had gone crashed the game on quitting. The stage check plays a full fight that ends and requires a clean quit.
- **Other options:** `take=` applies upgrades at the start; `logname=`, `mute=yes`, `asteroids=`, `plan=`, `variant=`, `explain=FROM-TO`.

## Batches and reports

- **Running:** `tools\batch.cmd` (`tools/batches/batch.py`), every combination of `--pilots`, `--builds`, `--weapons`, `--drone-weapons`, `--asteroids`, `--plans` and `--variants` on seeds from `--first-seed`. Weapons default to random by seed; pilots default to the expert, the one level kept calibrated. `--no-build` is for callers that just built the game.
- **Layout:** each batch gets a folder in `playtests/batches/` with `batch.json`, `logs/`, `output/`, `report.md` and `report.html` (the same content with charts, built from `report.md`). `tools/` is grouped by job; `tools/README.md` lists it.
- **Tuning variants:** a text file in `tools/variants/`, one change per line, by tuning file and value or by any `.tres` path; applied in memory, folded into the tuning fingerprint, and able to change any `.tres`, pilots included. The fingerprint leaves the pilots' own files out, so calibrating pilots doesn't make the designer's fights look like other tuning.
- **Report sections beyond the spec:** By plan, By asteroid amount, Pilots compared, Build paths (`tools/batches/build_paths.py`: each fight's build shape from the order of its level-up picks, Drone Weapon time, damage by minute; its cutoffs are constants there), Fights worth watching (best and worst per combination, with replay commands), and the run review's Review section.
- **Compare at the same fight times,** not per minute over whole fights: designer fights that run long into endless make per-minute rates misleading.

## Pilots: what the judgements learned

The planning pilot keeps the first pilot's judgements, each tuned while watching the designer play. The reasons, so they aren't undone:

- **Sight covers the whole screen,** using the camera's steady view, and **threat has a range of its own** (`ThreatRange`), separate from sight. Why: capping sight at the screen's short side hid a ring of asteroids at its side; measuring threat against sight made every enemy feel twice as close once sight grew.
- **It moves at full speed,** except easing in on material. Why: wants as speed made it creep between fights and slow down with material far off.
- **Harvesting:** clusters count (`ClusterWeight`), debris less (`DebrisClusterWeight`, since counting it fully drew pilots into fights), harvesting continues at the drone cap (`HarvestAtCap`, high, as the designer does), a rich concentration can pull it from a fight it could take (`ConcentrationPull`), and it commits to a rock near enough (`HarvestCommitDistance`) rather than hovering short of it. It doesn't commit with an enemy close ahead, and harvesting pulls less with any enemy close, not only the one it fights.
- **Danger and harvesting:** while backing off, material's pull drops with the danger around it (`HarvestDangerWhenBackingOff`). Why: a pilot fully backing off still flew into a cluster of Goos for rich debris.
- **Remembering and exploring,** at the designer's request: it returns to remembered material in proportion to its worth, counting distance, or explores (`ExploreUrge`). Picking by mass alone sent it across the map.
- **Regrouping** with few drones (`RegroupBelow`, `RegroupUntil`): it stops fighting and prefers rich clusters until its drones recover.
- **Not going after distant enemies** (`ApproachEnemies`), and an enemy on the way to material counts as danger at it. Why: it once flew through a group to reach a cluster beyond.
- **Pressure falls slowly** (`PressureFall`), so it doesn't start and stop backing off as enemies flick in and out of range.
- **Holding still** below a pressure (`HoldStillPressure`), as the designer does while harvesting and in light fights.
- **Care at low health** (`LowHealthBelow`, `LowHealthTouchScale`, and each weapon's low-health distance).
- **Drones matter:** with no drones it prefers finding asteroids to fighting with its weapons alone (`FightWithoutDrones`), at the designer's request.
- **Never retreats;** the harness, not the pilot, continues into endless mode.
- **Rejected:** keeping Goos out of the drones' engage range (cut drones lost but cost harvesting and levels); a variant taking the designer's notes literally (closing in little, dodging more, barely fighting without drones), which lost far more.

## Pilots: skill levels, builds and weapon profiles

- **Skill levels:** novice, average and expert, and the ceiling pilot. Only the expert is calibrated; average and novice keep older settings until skill levels matter, such as for rival VNPs.
- **Builds:** Random, Greedy (favours what it has and new weapons, so it takes no harvesting upgrades, by design), Drone swarm, Weapon focus, Area, Tank, and Designer, fitted to the designer's picks by `calibrate_build.py`.
- **Weapon profiles:** `WeaponPilotProfile` in each weapon's folder: a distance as a fraction of the weapon's reach as upgrades change it (`RangeFraction`; Mine Layer keeps a fixed `PreferredDistance`), keeping moving and leading chasers, blended by how invested the pilot is in each weapon. Why distances follow reach: the designer keeps more distance the more range a weapon has.

## Settled from the spec's open list

- **Editor button:** not built; the command is enough so far.
- **The designer's input recording rate:** ten samples a second.
- **How close is close enough:** no threshold set; automatic calibration measures a gap at the same fight times (see the run review addendum).
