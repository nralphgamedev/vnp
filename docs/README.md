# Docs

Two folders, split by who needs them.

## `design/`

The game itself: the phase specs, taken from the game design document (GDD), describing what the prototype is and how it plays. The designer reads and decides these. Each spec is kept exactly as the designer gave it; its addendum (`PROTOTYPE_N_ADDENDUM.md`) records, for AI, what building and testing changed, and wins where they disagree (CLAUDE.md, "Specs and addenda").

- `PROTOTYPE_1_SPEC.md`: phase 1, one complete fight.
- `PROTOTYPE_2_SPEC.md`: phase 2, feedback and more weapons and upgrades.
- `PROTOTYPE_3_SPEC.md`: phase 3, a second enemy type and ways to see what worked.
- `PROTOTYPE_4_SPEC.md`: phase 4, the rest of the Grey Goo types, in fights of Grey Goo only.

## `dev/`

Development only: the testing tools and how the game is checked, which design doesn't need to know about to play or decide the game.

Each spec is the plan as agreed, and its `_ADDENDUM.md` beside it records what the build did, where it differs and why; the addendum wins where they disagree. They work as the design specs and addenda do (CLAUDE.md, Specs and addenda). The performance spec has no addendum yet.

- `AUTOMATED_PLAYTESTING_SPEC.md`: pilots, batches, fight logs and reports.
- `RUN_REVIEW_SPEC.md`: detectors, recordings, maps and calibration.
- `PERFORMANCE_SPEC.md`: the performance requirement, how it is measured, its tests and what they found.
- `PILOT_SPEC.md`: pilots rebuilt to read the threats enemies declare and plan short paths, trained two ways (like the designer, and as strong as possible), with the general parts as Arbiter, a decision library meant to serve other behavior and other games.
- `FIGHT_DIRECTOR_SPEC.md`: the Fight Director, which gives each fight its own plan and paces it, with factions as data and the galactic map in mind. It changes how the game plays, so what it changes is recorded in the current design addendum too.
- `calibration/JOURNAL.md`: the record of unattended calibration runs.

## Where a new doc goes

Anything about how the game plays, looks or sounds goes in `design/`. Anything about tools, testing, pipelines or how the code is checked goes in `dev/`. A dev spec can change game code for testing's sake (such as hooks inside `#if RECORDING`), but if it changes how the game plays, that change belongs in the current design spec's addendum too.
