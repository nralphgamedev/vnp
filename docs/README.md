# Docs

Two folders, split by who needs them.

## `design/`

The game itself: the phase specs, taken from the game design document (GDD), describing what the prototype is and how it plays. The designer reads and decides these.

- `PROTOTYPE_1_SPEC.md`: phase 1, one complete fight.
- `PROTOTYPE_2_SPEC.md`: phase 2, feedback and more weapons and upgrades.
- `PROTOTYPE_3_SPEC.md`: phase 3, a second enemy type and ways to see what worked.

## `dev/`

Development only: the testing tools and how the game is checked, which design doesn't need to know about to play or decide the game.

- `AUTOMATED_PLAYTESTING_SPEC.md`: pilots, batches, fight logs and reports.
- `RUN_REVIEW_SPEC.md`: detectors, recordings, maps and calibration.
- `calibration/JOURNAL.md`: the record of unattended calibration runs.

## Where a new doc goes

Anything about how the game plays, looks or sounds goes in `design/`. Anything about tools, testing, pipelines or how the code is checked goes in `dev/`. A dev spec can change game code for testing's sake (such as hooks inside `#if RECORDING`), but if it changes how the game plays, that change belongs in the current design spec too.
