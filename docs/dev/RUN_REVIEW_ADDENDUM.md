# Run review addendum: what changed in building and testing

`RUN_REVIEW_SPEC.md` is the plan as agreed with the designer, and isn't edited to record what the build did. This file records only what an AI working on the project needs in order not to get it wrong: where the build differs from the spec, why, what was tried and rejected, choices made for its open items, and where things live. Where this file and the spec disagree, this file wins.

An entry belongs here only if, without it, the AI would do something differently or worse. Measurements, tuning numbers that the files already hold, process stories and repeats of the spec stay out; batch reports and git history keep those.

## Status

- All four stages are built. Calibration has stopped, and resumes against the designer's Grey Goo fights once fights feel right (the pilot spec's stage 5).

## Detectors

- **Where:** `scripts/cheats/playtesting/RunReview.cs`, run by the fight log every frame of every fight in the editor; thresholds in `review_settings.tres`.
- **They only read the fight** and draw no random numbers, so fights stay repeatable; the stage check's repeatability test runs one copy with its log and detectors to catch a detector that changes fights.
- **Each fires once,** then waits until its condition has been false for `RearmSeconds`. The burst detector fires again each time the drones lost within `BurstSeconds` reach `BurstWorsens` times the count that last fired it, so a long collapse also gets a strip of its worst part.
- **They print `REVIEW:` lines only in scenario and batch runs** (`RunReview.PrintMoments`), since printing during play costs frames.
- **Added after the first review:** drones left fighting, and drones not rebuilt.
- **Named by what they see, not by enemy type:** the Goo-touch detector became "Touched a contact enemy", firing on any enemy that damages by touch. Reports count a detector missing from an older log as zero.

## Recording and maps

- **Where:** `ReviewRecorder.cs` records; `MapDrawer.cs` draws PNGs with Godot's `Image`, headless, with a built-in pixel font. The drawer reads enemy types, weapons, detectors and their letters from the log, with no lists of its own, so new content appears with no change to it.
- **The designer's whole fight is recorded,** at the designer's request: the picture a strip shows, ten times a second, into `<log name>.recording.json.gz` beside the log (`RecordPlayerFights` turns it off). Why: a collapse the burst detector counted as one earlier moment had no strip, and a designer's fight can't be replayed to draw it. Pilot fights replay exactly, so they aren't recorded, except with `record=yes`.
- **Maps on request:** the designer's fights draw no maps when the log is written (`DrawPlayerMaps` turns that back on); `tools\maps.cmd` draws them for a fight's folder or `latest`, with strips of any moment or stretch (`strip=T`, `T:S`, `FROM-TO`, `FROM-TO:S`). A recorded fight's log keeps only its flagged strips' reasons and times (`fromRecording`), drawn from the recording. Each designer fight has its own folder in `playtests/human/`.
- **Exact moments:** a strip adds a picture at the moment its detector fired, and a picture of the VNP's destruction; a strip asked for is taken on its own schedule. Why: the picture marked as a moment could be up to a spacing before it, and a fatal touch was missed.
- **Writing off the main thread:** the main thread only gathers the log, copying lists that keep growing; turning it into text, writing and drawing happen on a background thread, and leaving or quitting waits for them. The log's code is built once as the fight is set up. Why: writing and drawing on the main thread hitched the game when a fight was won.
- **Map colours of their own:** each enemy type gets a bright colour from a fixed palette by its place in the log's list of types, leaving out colours maps already use. Why: once every Grey Goo type was the same grey, types couldn't be told apart on maps.
- **What the VNP was doing** is judged from what can be seen. It counts as harvesting while its beam works or for `HarvestingWithin` seconds after it or a weapon last harvested (`Harvest(..., byVnp)`). Why: Orbital Shards harvesting on the move looked like travelling.
- **The killing hit is recorded,** since the fight pauses in the frame the VNP is destroyed.
- **Events** include waves, small waves and escalation steps, marked on overviews and named in strips.
- **Pilot and batch logs are written compact;** the designer's stay indented for reading.

## Nothing left out of recordings

- **Required by the build:** `CollectPlaced` is abstract on `VnpWeapon` and on Drone Weapons' `DroneAttack`, so a new weapon doesn't compile until it says what it has out in the world. A weapon with nothing of its own writes an empty body saying why.
- **Shared systems are recorded whole:** every shot in the projectiles, marked with the weapon that fired it; each picture lists the loadout with its mods.
- **Checked:** the stage check plays every starting weapon recorded and fails if one leaves nothing; with `--full` it checks four recorded fights field by field against the fights' own statistics (`check_recording_full.py`). Every fight starts with groups of every enemy type, so every type and its shots are recorded, and one fight is kept alive so escalation is always recorded.
- **Out of exported builds:** the hooks only recordings read are inside `#if RECORDING`, defined only for the editor's Debug build, separate from `TOOLS` so a special build could record fights later.

## Calibration

- **Tools:** `tools/calibration/`: `compare_to_designer.py` gives one number, the gap, the average difference from the designer's fights at the same fight times; `calibrate_pilot.py` tries each setting raised and lowered on the same seeds and keeps a change only if it also wins on a second set of seeds (`--target results` scores wins and survival instead, for the ceiling pilot). It runs unattended on a `calibration/` branch under CLAUDE.md's rules, with every step in `docs/dev/calibration/JOURNAL.md`.
- **Compare at the same fight times,** not per minute over whole fights: the designer's higher rates came from long endless runs, where kills come fastest.
- **Iterating on the expert first,** at the designer's suggestion; average and novice are made from it later.
- **Not built:** detector rates as calibration targets. When calibration resumes, choose which detectors to target, since some measure style rather than faults (indecision fires on the designer's deliberate weaving).
- **Tried and dropped:** being bolder about danger near debris with more harvesting power, and preferring material the drones can already reach. Neither changed drones built, debris collected or drone collapse.

## Settled from the spec's open list

- **Which fights and moments a batch draws:** overviews of each combination's best, worst and typical fight; strips of a few moments per detector, taken from each combination in turn.
- **Calibration targets' weights:** equal.
- **Wave timing,** the designer's choice: pilots may estimate the next wave from waves seen this fight, knowing nothing until the second. Not built; deferred with calibration.
