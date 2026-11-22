# VNP (working title)

VNP is a top-down roguelite space conquest game built in Godot. The current goal is a combat prototype: one real-time fight, with nothing from the rest of the game.

The current spec is the source for what to build: @docs/PROTOTYPE_3_SPEC.md (phase 3, a second enemy type and ways to see what worked, resumed at stage 3 after the testing tools below).

It builds on the earlier specs:

- Phase 1, one complete fight: @docs/PROTOTYPE_SPEC.md
- Phase 2, feedback and more weapons and upgrades: @docs/PROTOTYPE_2_SPEC.md
- Automated playtesting, pilots and batches (stages 1 to 5 built; stage 6, an editor button, only if wanted): @docs/AUTOMATED_PLAYTESTING_SPEC.md
- Run review: detectors, maps, Claude's reviews and automatic calibration (all four stages built): @docs/RUN_REVIEW_SPEC.md

## Technical rules

- Godot 4.7.2, built from the engine source in `../godot` next to this project. Update this line if a different version is installed.
  - When unsure how a Godot API or C# binding behaves, check the source instead of guessing: `doc/classes/` for the API reference, `modules/mono/glue/GodotSharp/GodotSharp/` for the C# API (its `Core/` folder holds types such as `Vector2` and `Mathf`), `scene/` for how nodes behave, and `servers/` for rendering and physics.
  - Treat the engine source as read-only by default. Changes to it are possible, but only after explaining the change and getting the designer's approval. An engine change means rebuilding the editor and its C# glue, and the project then needs that custom build, so record any change in the docs.
- C#.
- This is a 3D project with all gameplay locked to a single flat plane viewed from above. Use 3D nodes, not 2D nodes. Calculate movement, flocking, ranges, and distances in two dimensions on that plane.
- Placeholder art is simple 3D geometric shapes. Beyond making units easy to tell apart, spend time on visuals only where the spec asks for it, such as feedback effects.
- Every placeholder visual and audio asset goes in `res://placeholder/`, saved as its own file rather than embedded in a gameplay scene, and is listed in `res://placeholder/PLACEHOLDERS.md` with what uses it and where it came from. Written text is not a placeholder asset and stays where it is.
- Keep every tuning value in a data file, never buried in scripts. The point of the prototype is to adjust these numbers by playing.
- Prefer to keep related files together, so one folder gives a full view of a feature: a feature's code, its Resource classes, its `.tres` data, and its upgrades usually share a folder, rather than being split into separate code and data trees. This is a preference, not a hard rule, so use judgement where a file serves several features. Placeholder assets are the exception (see above).
  - `res://tuning/` holds the tuning for systems that don't have their own folder yet, one `.tres` per system with its class beside it, along with the `Tuning` loader and the upgrades that belong to no single feature (`tuning/upgrades/`). When a system grows enough to need its own folder, for example when it gets a manager and several tuning classes, move it out, wherever it fits best: not necessarily at the top level, and related folders can be grouped under a parent (for example, `weapons/vnp_weapons/` and `weapons/drone_weapons/`). The `Tuning` loader names each file by its full path, so moving one means changing its line there.
  - When moving any file, move a script's `.uid` file with it, and update every `path=` that refers to the file, since `.tres` files refer to scripts and to each other by path. For example, `levels.tres` lists every upgrade by its path, so moving an upgrade without updating that path breaks the pool.
- Tuning values and content definitions (weapons, upgrades, enemy types) are data-only custom Resources. Modding may be supported later, so keep logic out of them.
  - Name the classes by kind: a `*Tuning` class holds one system's settings, with exactly one file, which code reaches by name (`Tuning.Asteroids`); a `*Definition` class is one of many content entries, each with an `Id`, such as weapons, upgrades, enemies, and sounds.
- Playtest cheats go in `scripts/cheats/`, which `VNP.csproj` compiles only in the editor's Debug configuration. Wrap the cheat code and every line that uses it in `#if TOOLS`, so a cheat can never reach an exported game. This covers any testing aid that must not ship, not only cheats in the narrow sense. Cheat settings go in `scripts/cheats/in_game_cheats.tres`, not in the game's tuning files. A cheat that needs a node creates it from code under `#if TOOLS` rather than placing it in a scene, since a scene node whose script is left out of a build would break. When an export preset is made, exclude `scripts/cheats/` from its files.

## Editing files on Windows

This project is developed on Windows, where a few editing habits cause damage:

- Files are UTF-8 with LF line endings (`.gitattributes` enforces LF). Some contain non-ASCII characters, such as the `─` dividers in scripts.
- Prefer the Edit tool. For an edit too large or repetitive for it, write a Python script to a file in the scratchpad and run that, rather than passing it through a Bash heredoc or `python -c`: apostrophes break heredocs, and escapes such as `\x00` change meaning as they pass through each layer of quoting, which once wrote a real zero byte into a script.
- In Python, always open files with `encoding="utf-8"` and `newline=""`. Without them Python uses the Windows code page, which cannot write characters like `─`. Opening a file for writing empties it before the write fails, so the file is left blank.
- Make scripted edits safe to run again: check that each piece of old text appears exactly once before replacing it, and stop if not. After a failed run, look at what was already applied before running it again. A check like "skip if the new text is already there" is fooled when the new text contains the old.
- If an edit goes wrong, restore only the file you broke, from git if it had no other changes, and say so.

## How we work

- The designer is a programmer by trade and will playtest every stage. Explain what you built in plain terms and say how to try it.
- Build one stage from the spec's build order at a time, then stop so it can be played. Do not start the next stage until asked.
- You cannot judge how the game feels. Feedback will arrive as descriptions of what happened on screen, such as "drones lag too far behind." Turn that into a change, and say which tuning value or behavior you adjusted.
- Stay inside the spec's scope. If something would be useful but is listed as out of scope or as a stretch goal, mention it rather than building it.
- When the spec is silent on a design question, ask. Where the spec marks something as not yet decided, pick the simplest behavior, say what you picked, and keep it easy to change.
- Keep the current spec up to date. Whenever something changes during prototyping, whether from playtest feedback, a new design decision, or a choice made for something marked not yet decided, update the spec in the same step so it always describes what is built. Move items that get settled out of the spec's not-yet-decided list and record what was chosen.
- The spec describes behavior, not tuning numbers. Numbers change constantly while prototyping and the tuning files already record them, so name the tuning value and file instead of quoting its number. Keep only numbers the designer set as part of the design, such as weapon slots or upgrade limits, and leave out test measurements, which go stale when a value changes.
- After a stage is approved, commit it to git so there is a working version to return to. Always ask before committing or pushing, except on a calibration branch under the rules in "Unattended work on calibration branches" below.

## Unattended work on calibration branches

Long calibration runs on their own, without the designer, on a git branch made for them. These rules apply only there. Everywhere else, the rules above stand: ask before every commit and push.

- **Where:** only on a branch whose name starts with `calibration/`, created by the designer's go-ahead before a run. Check the current branch (`git branch --show-current`) before any commit or push; if it isn't one of these, stop and ask.
- **Allowed without asking, on that branch only:** changing pilot code and settings (`pilots/`, weapon `pilot_profile.tres` files), tuning variants (`tools/variants/`), calibration and review tools (`tools/`, `scripts/cheats/playtesting/`), the journal, and notes in the specs; committing those changes; and pushing that branch to `origin`.
- **Never, even there:** push to `main`, merge into or from `main`, force-push, rewrite history, or delete a branch. Never change game code or tuning outside the pilots, since that changes the game being balanced: if calibration seems to need it, stop and ask. Never delete or rewrite the designer's fight logs in `playtests/human/`.
- **The guard:** while a run is unattended, the file `.git/vnp-unattended` exists, and the `pre-push` hook in `.git/hooks/` then refuses any push to `main`. Create the file when an unattended run starts and remove it when it ends. Never remove the hook or bypass it (`--no-verify`).
- **Work in chunks, and keep the journal:** usage limits, a full context or a closed terminal can cut a run off at any moment, and the remaining usage can't be checked from inside a session. So work in small chunks, each ending in a commit: one change or candidate, a batch kept under about a minute, its result, and a dated entry in `docs/calibration/JOURNAL.md` saying what was tried, what was found and what comes next. A run that starts reads the journal first and carries on from its last entry, rather than from memory.
- **Every chunk passes the stage check** (`check-stage`) before it is committed. If it fails and the failure can't be fixed within that chunk, revert the chunk's changes, record why in the journal, and stop.
- **Stop and wait for the designer** when: a change would need game code or tuning outside the pilots; the stage check fails and can't be fixed; a set number of chunks in a row (the journal's stopping rule) bring no improvement; or anything is unclear. Write the reason as the journal's last entry, so the designer sees it first.
- **What stays with the designer:** merging the branch into `main`, any change to the game itself, and judging how the game feels.
