# Tools

Editor-only testing tools: nothing here is part of the game. Godot doesn't import this folder (`.gdignore`). Run everything from the `vnp` folder. The Python scripts need only Python.

## What you type

| Command | What it does |
|---|---|
| `.\tools\scenario.cmd [-Window] ARGS` | Plays one fight through the scenario harness, headless unless `-Window`. Its arguments are listed in `scripts/cheats/ScenarioRunner.cs`; for example `pilot=expert build=designer seed=30 maxtime=420`, `maps=yes strip=363:0.25`, `explain=120-130`. |
| `tools\batch.cmd ARGS` | Runs a batch of pilot fights in parallel and writes its report (`--help` lists the options). |
| `tools\maps.cmd [FIGHT] ARGS` | Draws run review maps of a fight: its folder, its log, or `latest` (the default) for the designer's most recent. With nothing else, the overview and every flagged moment; `strip=356` or `strip=350-358` draws any moment or stretch from the fight's recording. Notes in `maps/maps.ps1`. |

These launchers stay at the top so the commands don't change. Everything else is grouped by job:

## `scenario/`

- `scenario.ps1`: what `scenario.cmd` runs. It finds the Godot build and starts the harness scene.

## `maps/`

- `maps.ps1`: what `maps.cmd` runs. It finds the fight's log and starts the map drawer (`scripts/cheats/playtesting/draw_maps.tscn`).

## `batches/`

- `batch.py`: runs a batch: every combination of pilots, builds, weapons and tuning variants on a set of seeds, in parallel. Writes the batch folder in `playtests/batches/`.
- `batch_report.py`: writes `report.md` from a batch folder's logs. `batch.py` runs it after each batch.
- `batch_report_html.py`: writes `report.html`, the same report with charts.

## `calibration/`

- `compare_to_designer.py`: compares a batch's pilot fights with the designer's fights at the same fight times, and prints the GAP (smaller is closer).
- `calibrate_pilot.py`: the unattended search over a pilot's settings, scored by that gap (see the run review spec, Automatic calibration, and `docs/calibration/JOURNAL.md`).
- `calibrate_build.py`: fits the Designer build's upgrade weights to the designer's picks in their logs.

## `variants/`

Tuning variants, one text file per variant, applied on top of the tuning files with `variant=NAME` (harness) or `--variants base,NAME` (batches). See `scripts/cheats/playtesting/TuningVariant.cs` for the format.

## Adding a tool

Put it in the folder for its job, or a new folder if none fits. A Python script finds the project folder three levels up from itself (`os.path.dirname` three times over `__file__`). Only commands the designer types go at the top. Update this file, and any spec that names the tool.
