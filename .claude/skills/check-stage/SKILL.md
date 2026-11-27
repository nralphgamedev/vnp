---
name: check-stage
description: Verify the VNP Godot project after building or changing something, before handing it to the designer to play or before committing. Runs all three C# builds, an editor rescan for .uid files, a load of every .tres data file, a headless fight, a check that playtest cheats stay out of export builds, and a review of the spec, CLAUDE.md rules and PLACEHOLDERS.md against the change. Use this whenever a build stage or playtest fix is finished, when the user asks to check, verify, test or "make sure everything works", before any commit that changes more than documents, and after moving or renaming files, baking placeholders, or editing .tres files, even if the user doesn't say "check".
---

# Check a stage

The designer playtests every stage, so a stage isn't ready to hand over until it builds, loads, runs, and its docs match what was built. This skill is that pass. The mechanical part is one script; the judgement part is reading the change against the project's rules.

Run it from the vnp project. Paths below are relative to this skill's folder.

## 1. Run the mechanical checks

```bash
bash scripts/check_stage.sh
```

With nothing to compile, the plain check takes about 20 seconds and `--full` about 40; code changes add build time, so run it with a generous timeout. Every check that plays headless fights (the headless fight, repeatability and clean quit, the quick batch, and both recording checks) starts at once and is judged in turn afterwards: each runs on a fixed time step, so sharing the computer can't change its result. The performance tests run alone after them, since they measure clock time. It prints a section per check and a SUMMARY at the end. The whole output is also saved, to `playtests/checks/check_<date>.txt` (the last 30 runs) and `playtests/checks/latest.txt`, so read the saved file when a check fails rather than running it again:

- **Builds:** Debug (what the editor runs), ExportDebug and ExportRelease. Export builds leave out `scripts/cheats/`, so a line that uses a cheat outside `#if TOOLS` fails only there. That's why all three are built.
- **Editor rescan:** Godot makes a `.uid` file for each new script. New ones are listed so they get committed with their scripts. A `.uid` whose script is gone means a file was moved without its `.uid`.
- **Data files:** every `.tres` loads, and every one outside `placeholder/` has its C# script. A missing script usually means a class was renamed or moved without updating `path=` in the files that use it. It also reports the upgrade pool size, sounds, and enemy spawn weights.
- **Headless fight:** 1,800 frames (30 seconds) at a fixed 60 fps, through the scenario harness (step 2), with two groups of every enemy type spawned around the VNP and the VNP kept alive. A plain run would sit on the starting weapon cheat's paused menu and test nothing. It reports any errors or exceptions, and how long each frame's game logic took. The fight also writes a fight log, which `scripts/check_log.py` cross-checks: totals against their parts, kills counted three ways, drones in and out, levels against level-ups, and the timeline against the outcome. The same script checks any fight log, such as one from the designer's playtests: `python scripts/check_log.py <log.json>`.
- **Repeatability:** the same 90-second pilot fight is run twice at once with `trace=30`, and the two traces must match line for line. Any difference means something outside the seed now changes how a fight plays. Past causes were the camera shake and the window size changing where enemies spawn. The first differing line shows when the runs parted: compare what changed between it and the line before.
- **Clean quit:** a full pilot fight that wins, carries on into endless and ends must quit with exit code 0. Long fights that end once crashed the game as it quit (exit code 139); see `ShutdownCleanup.cs`.
- **Performance requirement (with `--full`, just before each push; opens a window):** the stress test for CLAUDE.md's requirement, run twice through the scenario harness's `perf=`: `plain` judges the frame budget (the slowest 1% within 8.33 ms, 120 FPS) and hitches (none over 16.7 ms) on clock time with nothing extra measuring, and `detail` adds game logic's budget (its slowest 1% within 4 ms) and shows logic, render CPU and GPU times. Each reports its worst frames with their fight times, which is where to start when it fails.
  - **Explaining slow frames:** each test also reports .NET's garbage collections while it was judged, and marks a frame a collection paused (`[GC paused N]`). The main thread's allocation a frame is judged against `AllocationBudgetKb` in `performance.tres`; over it, `profile=yes` shows which part allocates.
  - Each frame over budget gets a `PERF SLOW` line with its clock time.
  - While a test runs, `sample_processes.ps1` samples which other programs are using the CPU, every half second, at idle priority. Afterwards `perf_context.py` prints, for each slow frame, the busiest programs around it (`PERF CONTEXT`), then a verdict on the cause: garbage collection, another program, or unknown.
  - A failure adds a line to `playtests/checks/perf_failures.txt`, so failures can be compared across runs. Then the late fight: an invulnerable expert plays a Rich field to 6:00, fast forwarded (`perfstart=360`), and a minute of escalated fight is measured with recording on, with `profile=yes` printing each part of the update; it catches costs that grow over a long fight, and takes about 100 seconds. The tenfold crowd, about 2,500 enemies, runs only with `--crowd` as well (`--full --crowd`), at the designer's choice, when performance work is under way: it is reported only, since it doesn't meet the logic budget yet, so it can't fail the check, and it took 40% of `--full`'s time. Budgets are in `scripts/cheats/playtesting/performance.tres`.
- **Full recording check (with `--full`, just before each push):** `check_recording_full.py` plays a few recorded pilot fights into escalation that between them use every starting VNP Weapon, every Drone Weapon, every weapon mod and every asteroid amount, worked out from what exists, so new content is covered with no change. Each fight starts with two groups of every enemy type around the VNP, so every type and its shots are recorded even when the fight's own spawning leaves it out. Each recording and log is checked field by field: pictures at the recording's spacing from start to end, every part of every picture, each weapon shown, drones' health and targets, drain on drone eaters only, enemy headings, asteroids and debris, mods in the loadout, and hits, drones lost, level-ups, waves and escalation steps adding up to the fight's own statistics. It takes about half a minute. Run `bash scripts/check_stage.sh --full` just before pushing; the plain check, enough before a commit, leaves it out.
- **Recording:** one short recorded pilot fight per starting VNP Weapon, each paired with a Drone Weapon in turn, through `check_recording.py`. Every weapon must show up in the whole-fight recording (a shot, or something from its `CollectPlaced`), unless it is listed, with why, as having nothing of its own (Ram). A new weapon missing from the recording fails here; emptying Shockwave's `CollectPlaced` made it fail.
- **Quick batch:** a few short pilot fights through `tools/batches/batch.py`, as a smoke test of the batch tools. Every fight must finish without errors and write its log, and the report must be written. Its balance numbers are shown, not judged.
- **Cheats:** no class from `scripts/cheats/` or its subfolders appears in either export DLL.
- **Working tree:** untracked files that look temporary, and placeholder model scenes changed only by new node IDs.

The script never commits, reverts or deletes project files, so act on what it reports:

- **Placeholder models changed only by node IDs:** a rebake gives the model scenes new random node IDs. Revert them with `git checkout -- placeholder/models/` unless the stage changed a model.
- **Temporary-looking files:** remove any test scripts or scenes you made during the stage. Anything else, ask about.
- **A problem:** fix it and run the script again. Don't report a stage as ready while the summary shows problems.

## 2. Set up the situation the stage is about

The general fight can miss what the stage changed: a Goo splitting needs drones to feed it, and a late-fight slowdown needs a crowd. The scenario harness (`scripts/cheats/ScenarioRunner.cs`, editor only) sets a situation up from arguments, plays it, and prints status lines and frame timings:

```bash
bash scripts/check_stage.sh --scenario spawn=basic_goo:4 drones=40 invulnerable frames=900
bash scripts/check_stage.sh --scenario --window spawn=all:2 shot=<scratchpad>/shot.png@300
```

Its arguments:

- `weapon=ID` and `drone_weapon=ID`: the weapons, any from the cheat menu's lists.
- `spawn=ID:GROUPS[:DISTANCE]`: enemy groups hunting the VNP. `all` spawns every type.
- `drones=N`: extra drones.
- `invulnerable`: keeps the VNP alive.
- `frames=N` and `report=N`: how long to run, and how often to print a status line.
- `shot=PATH[@FRAME]`: a screenshot.

ScenarioRunner.cs documents them all. Use it instead of writing a throwaway test. If a situation needs something it can't set up yet, add an argument to it, since the next stage will likely want it too.

- **Performance:** run headless and read the TIMING line. That's game logic only, so compare it with the 16.7 ms a frame has at 60 fps. Windowed timings include waiting for vsync, so they say little.
- **Looks:** when the stage added or changed anything visible (a unit, an effect, a UI screen), take a `shot=` with `--window`, read the image, and look at it critically. Can each kind of unit be told apart, by shape as well as colour? Does the new thing read clearly against the dark background, the grey asteroids and the other units?

You can't judge how the game feels, only how a still frame and some numbers look. Say so, and leave feel to the designer's playtest.

## 3. Check the change against the project's rules

Read the diff (`git diff` and the untracked files) with these questions. Each comes from CLAUDE.md or a past mistake:

- **Spec and addendum:** is every design spec (`docs/design/PROTOTYPE_N_SPEC.md`) unchanged? Only addenda change. Does the current addendum record what was built where it differs from the spec, with the reason, and any choices made for "not yet decided" items? Does it name tuning values and files rather than quoting their numbers? Does each new entry pass the addendum test (an AI would do worse without it), leaving out test measurements and what the code already shows?
- **Tuning:** are new numbers that affect play in a data file, not hard-coded in a script? A few fixed constants that only shape a look or a motion are fine if they're commented.
- **Placeholders:** is every new mesh, material, sound or icon in `res://placeholder/` and listed in `PLACEHOLDERS.md`, with what uses it? Is a code-made one listed with the script that makes it?
- **Files together:** are new files in their feature's folder, Definitions and Tunings named by kind, and moved files' `.uid` and `path=` references updated?
- **Cheats:** is any testing aid in `scripts/cheats/` behind `#if TOOLS`, with its settings in `in_game_cheats.tres`?
- **Older specs:** if the change contradicts an earlier phase's spec or addendum, does the current addendum say which phase it changes? Earlier specs are never edited.

## 4. Report

Keep the report short, for a designer who is a programmer and about to play the stage:

- **Result:** whether the stage is ready to play, in one line.
- **Problems:** anything found and fixed during the check, and anything still open, with the file.
- **Doc gaps:** what step 3 turned up, and whether you fixed it.
- **Screenshot:** what it showed, if you took one.
- **Commit:** that it is ready to commit, once the plain check has passed, and ready to push once `--full` has (a commit of only documents, Markdown files, needs no check). Never commit or push without the designer asking: CLAUDE.md requires asking first, every time.

Don't paste the whole script output. Pick out what matters.
