---
name: check-stage
description: Verify the VNP Godot project after building or changing something, before handing it to the designer to play or before committing. Runs all three C# builds, an editor rescan for .uid files, a load of every .tres data file, a headless fight, a check that playtest cheats stay out of export builds, and a review of the spec, CLAUDE.md rules and PLACEHOLDERS.md against the change. Use this whenever a build stage or playtest fix is finished, when the user asks to check, verify, test or "make sure everything works", before any commit, and after moving or renaming files, baking placeholders, or editing .tres files, even if the user doesn't say "check".
---

# Check a stage

The designer playtests every stage, so a stage isn't ready to hand over until it builds, loads, runs, and its docs match what was built. This skill is that pass. The mechanical part is one script; the judgement part is reading the change against the project's rules.

Run it from the vnp project. Paths below are relative to this skill's folder.

## 1. Run the mechanical checks

```bash
bash scripts/check_stage.sh
```

It takes a few minutes (three builds and several Godot launches), so run it with a generous timeout. It prints a section per check and a SUMMARY at the end:

- **Builds:** Debug (what the editor runs), ExportDebug and ExportRelease. Export builds leave out `scripts/cheats/`, so a line that uses a cheat outside `#if TOOLS` fails only there. That's why all three are built.
- **Editor rescan:** Godot makes a `.uid` file for each new script. New ones are listed so they get committed with their scripts. A `.uid` whose script is gone means a file was moved without its `.uid`.
- **Data files:** every `.tres` loads, and every one outside `placeholder/` has its C# script. A missing script usually means a class was renamed or moved without updating `path=` in the files that use it. It also reports the upgrade pool size, sounds, and enemy spawn weights.
- **Headless fight:** 600 frames at a fixed 60 fps, through the scenario harness (step 2), with two groups of every enemy type spawned around the VNP and the VNP kept alive. A plain run would sit on the starting weapon cheat's paused menu and test nothing. It reports any errors or exceptions, and how long each frame's game logic took. The fight also writes a fight log, which `scripts/check_log.py` cross-checks: totals against their parts, kills counted three ways, drones in and out, levels against level-ups, and the timeline against the outcome. The same script checks any fight log, such as one from the designer's playtests: `python scripts/check_log.py <log.json>`.
- **Repeatability:** the same 90-second pilot fight is run twice at once with `trace=30`, and the two traces must match line for line. Any difference means something outside the seed now changes how a fight plays. Past causes were the camera shake and the window size changing where enemies spawn. The first differing line shows when the runs parted: compare what changed between it and the line before.
- **Clean quit:** a full pilot fight that wins, carries on into endless and ends must quit with exit code 0. Long fights that end once crashed the game as it quit (exit code 139); see `ShutdownCleanup.cs`.
- **Full recording check (with `--full`, before each commit):** `check_recording_full.py` plays a few recorded pilot fights into escalation that between them use every starting VNP Weapon, every Drone Weapon, every weapon mod and every asteroid amount, worked out from what exists, so new content is covered with no change. Each recording and log is checked field by field: pictures at the recording's spacing from start to end, every part of every picture, each weapon shown, drones' health and targets, drain on drone eaters only, enemy headings, asteroids and debris, mods in the loadout, and hits, drones lost, level-ups, waves and escalation steps adding up to the fight's own statistics. It takes about half a minute. Run `bash scripts/check_stage.sh --full` before committing; the plain check leaves it out.
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
bash scripts/check_stage.sh --scenario spawn=goo:4 drones=40 invulnerable frames=900
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

- **Spec:** does the current spec (named in CLAUDE.md) describe what was built, including any choices made for "not yet decided" items? Does it name tuning values and files rather than quoting their numbers? Does it list new tuning values in its tuning table?
- **Tuning:** are new numbers that affect play in a data file, not hard-coded in a script? A few fixed constants that only shape a look or a motion are fine if they're commented.
- **Placeholders:** is every new mesh, material, sound or icon in `res://placeholder/` and listed in `PLACEHOLDERS.md`, with what uses it? Is a code-made one listed with the script that makes it?
- **Files together:** are new files in their feature's folder, Definitions and Tunings named by kind, and moved files' `.uid` and `path=` references updated?
- **Cheats:** is any testing aid in `scripts/cheats/` behind `#if TOOLS`, with its settings in `in_game_cheats.tres`?
- **Older specs:** if the change contradicts an earlier phase's spec, does the current spec say so, or was the earlier text updated?

## 4. Report

Keep the report short, for a designer who is a programmer and about to play the stage:

- **Result:** whether the stage is ready to play, in one line.
- **Problems:** anything found and fixed during the check, and anything still open, with the file.
- **Doc gaps:** what step 3 turned up, and whether you fixed it.
- **Screenshot:** what it showed, if you took one.
- **Commit:** that it is ready to commit, once `--full` has passed. Never commit or push without the designer asking: CLAUDE.md requires asking first, every time.

Don't paste the whole script output. Pick out what matters.
