# Calibration journal

The record of unattended calibration runs on `calibration/` branches (see CLAUDE.md, "Unattended work on calibration branches", and the run review spec's Automatic calibration). A run can be cut off at any moment, by a usage limit, a full context or a closed terminal, so every run starts by reading this file and carries on from the last entry.

## How a run works

- **A chunk** is one candidate or change, a batch kept under about a minute, its result against the designer's fights, the stage check, an entry here, and a commit (and a push of the branch).
- **The measure:** `python tools/compare_to_designer.py <batch folder>` compares the expert pilot's fights with the designer's at the same fight times, and prints a GAP: smaller is closer. A candidate is kept only if it lowers the gap by more than chance, judged by running the same seeds with and without it as variants.
- **Stopping rule:** stop and wait for the designer after 6 chunks in a row that bring no improvement, or for any reason in CLAUDE.md's list. The stop's reason is the last entry, so the designer sees it first.
- **Entries** are newest last, each dated, saying what was tried, what was found, what is kept, and what comes next.

## Entries

### 2026-11-22: setup

Branch `calibration/expert` created at the designer's request. Rules added to CLAUDE.md; the `pre-push` guard installed; this journal and `tools/compare_to_designer.py` added. Next: a test chunk measuring the expert pilot as it is, to check a chunk works end to end.

### 2026-11-22: test chunk, the expert pilot as it is

40 expert fights (Designer build, random starts) against the designer's 9 fights with timelines, some played on earlier tuning. GAP 0.210. The pilots match the designer on health to 3:00 and on drones to 2:00; from 3:00 they fall behind on kills and level, and by 4:00 their drones have collapsed (median 3 against the designer's 19). This is the late-fight gap the first review found. Kept: nothing changed; this sets the baseline. Next: the designer is playing a few more fights first, then calibration starts, aiming at the late fight.
