# VNP Combat Prototype: Run Review

Automated playtesting (`docs/dev/AUTOMATED_PLAYTESTING_SPEC.md`) gave the game pilots that play it and batches that summarise hundreds of fights. Getting pilots to play like the designer then meant the designer watching pilot fights and reporting what looked wrong, with clock times, and Claude replaying those moments to see why. That fixed real problems, but each round cost the designer a watched fight, and the gains became too small to measure.

This spec takes over most of that watching. The game notices the kinds of moments the designer kept reporting, such as flying into a Goo, and records them in every fight, the designer's own included. Beyond those known moments, the goal is for Claude to review fights visually on its own and notice problems nobody has described yet. For that, every fight can be drawn as a top-down overview map of the whole fight, and any moment as a strip of small maps a second apart. Claude scans these, the way the designer watched, and confirms what it finds against the fight's numbers and the pilot's reasoning before reporting it. Automatic calibration, kept as an option in the automated playtesting spec, moves here, with the detector counts as extra targets.

It interrupts phase 3 (`docs/design/PROTOTYPE_3_SPEC.md`) again, at the designer's request, since it speeds up all tuning after it. Phase 3 resumes afterwards from its stage 3. Everything in the earlier specs still applies unless this file changes it.

What the build did, where it differs, and choices made for the open items are in `RUN_REVIEW_ADDENDUM.md`, which wins where the two disagree.

## What this is for

- Can the moments the designer reported while watching pilots be found without anyone watching?
- How often does each happen to pilots, and how often to the designer?
- Can Claude notice problems on its own from maps of whole fights and moments, as the designer did by watching, including ones no detector looks for?
- Can pilots be calibrated to the designer by these counts as well as by results, without the designer watching?

## Terms

- **Detector:** a rule that watches a fight and notices one kind of moment, such as the VNP touching a Goo. It works from what happens in the fight, not from the pilot's reasoning, so it judges the designer's fights and a pilot's in the same way.
- **Flagged moment:** a time when a detector fired, with what it saw.
- **Track:** the VNP's path through a whole fight, recorded a few times a second, with what it was doing and the events along the way.
- **Overview map:** one top-down picture of a whole fight, drawn from its track.
- **Snapshot:** the state of the fight around the VNP at one moment: the enemies, drones, material and shots near it.
- **Strip map:** a row of small top-down pictures of snapshots about a second apart, in one image, so a moment's motion reads at a glance.
- **Maps** are plain diagrams, not the game's own look.

## Detectors

Each detector fires on something that can be seen in the fight, so it works the same on the designer's fights and on a pilot's. Its thresholds are in the review settings file, not in code. A detector that keeps firing on the same thing counts it once, until the moment has clearly passed.

Starting detectors, each from something the designer reported:

| Detector | Fires when | From |
|---|---|---|
| Touched a contact enemy | the VNP touches an enemy that damages by touch, such as a Goo | flew into a Goo (clock 4:06) |
| Flew through a group | the VNP passes within a short distance of several enemies within a few seconds | flew through a group to a cluster beyond it (clock 4:36) |
| Closed in on a near enemy | the VNP moves toward an enemy that is already close, for more than a moment | moved even closer to an enemy that was already close |
| Indecision | the VNP's direction reverses several times within a few seconds with enemies near | juggled between regrouping and fighting (clock 2:25) |
| Creeping | the VNP moves below most of its top speed for a while, while not harvesting and not easing in to harvest | slowed down between fights (clock 4:52, and earlier) |
| Fighting without drones | with no drones, the VNP stays within weapon reach of enemies for a while instead of getting away | kept taking shots with no drones (clock 3:06 and 2:15) |
| Idle | the VNP neither harvests nor has an enemy within reach for a while | pilots ending fights far from the gateway, noticed by Claude |
| Drones lost in a burst | many drones are absorbed or destroyed within a few seconds, and again each time a burst worsens to `BurstWorsens` times the count that fired it | drones locking onto one Goo and flying into others |
| Drones left fighting | several drones are fighting an enemy near them while the VNP, farther off, moves away | Claude's first review of a batch (stage 3) |
| Drones not rebuilt | the drones stay at a few or none for a while after the first minute | Claude's first review of a batch (stage 3) |

Detectors are a list, so more can be added as new kinds of moments come up. They judge enemies by what they do (absorbing drones, damaging by touch), never by which enemy they are, so they keep working as enemy types are added. Each counts how often it fired, per minute of fight, and keeps its flagged moments.

## Overview maps

One image per fight, for scanning whole fights for anything odd.

- **Track:** every fight records the VNP's position a few times a second, with what it was doing, judged from what can be seen so that it works for the designer's fights too: harvesting (its beam is working, or it or a weapon such as Orbital Shards harvested within `HarvestingWithin` seconds), fighting (an enemy within its weapons' reach), moving away from enemies, or travelling. Along the way it records events: hits taken, drones lost, Goo touches, level-ups, the kill target, each wave and escalation step, and each detector firing.
- **The map:** the whole path on one top-down image, coloured by what the VNP was doing, with the gateway, markers for the events, and the time at regular marks along the path. Underneath, strips show health, drones, level and speed over the fight, on the same time scale, with the 5:00 limit marked. It shows the shape of a fight: long detours, circling one spot, going back and forth, lingering at the edge of a fight, wandering far from material.
- **Which fights:** a batch draws overview maps for a few fights per combination, such as its best, its worst and a typical one; any fight's map can also be drawn from its log afterwards.

## Strip maps

A few small top-down pictures of one moment, about a second apart, side by side in one image.

- **Each picture:** the area around the VNP, about what the screen shows. The VNP and its recent path stand out; enemies are coloured by type and drawn at their size; Goos show their reach; drones are small dots; material is sized by mass, asteroids and debris told apart; shots are short lines in their direction of travel. A label gives the fight time, the HUD clock, health, drones and level.
- **Flagged moments:** every flagged moment gets a strip, from a few seconds before it to a little after. The fight keeps the last few seconds of snapshots as it goes, so a strip of what led up to a moment can be saved when a detector fires, in the designer's fights as well as pilots'.
- **Any moment of a pilot fight:** a pilot fight repeats exactly from its seed, so the scenario harness can replay it headless and draw a strip around any fight time, such as something Claude spotted on an overview map. Since the time is known before the replay, the strip's pictures are taken on its own schedule: one exactly at the moment, at any spacing, or across a stretch of the fight.
- **The end of a fight:** a strip still being taken when the VNP is destroyed ends with a picture of that moment.
- **Drawn by the game:** maps are drawn by the game's editor-only code with Godot's own image drawing, which works headless and needs nothing installed. Batch scripts stay Python-only.
- **How many:** a batch draws strips for a limited number of each detector's moments, picked to cover different fights, so a big batch doesn't write thousands of pictures.

## At the scale of the full game

The full game will have dozens of enemy types and hundreds of weapons, weapon combinations and upgrades. Nothing in the review may depend on a fixed list of them.

- **Built:** weapons' objects on maps, through a hook every weapon must implement (see the addendum); detectors work from enemy traits; fight logs carry each enemy type's name, colour and traits from its definition, and each detector's name; maps draw each enemy type in a colour of its own from the drawer's palette, marked by shape for what they do (striped for damaging by touch, an outer ring for absorbing drones), with a key to the types in each picture, and take detector names and letters from the log. A new enemy type or detector appears on maps with no change to the drawer.
- **Planned, for when content grows:**
  - **Coverage:** batches can't play every combination, so random starts sample them. The report gains a coverage view: which weapons, combinations and upgrades a batch tried, and how often, so gaps are visible.
  - **Outliers:** the report flags combinations and upgrades whose win rate or detector rates sit far from the rest. Those flags pick what Claude looks at closely, since hundreds of combinations can't each be reviewed by eye.

## Claude's review of a batch

- **What it looks at:** the overview maps of a few fights per combination, and the strips of flagged moments, then strips of anything else that looks wrong on an overview.
- **Confirming:** a busy image can be misread, so before reporting anything Claude checks it against the fight's numbers and, for a pilot fight, replays it with `explain` to see the pilot's reasoning.
- **What it reports:** what it noticed, with the fight, the HUD clock and its maps, whether it was confirmed, and what might be causing it, in the same way the designer's notes were turned into changes. A pattern seen often enough becomes a new detector.

## Screenshots, on request

Real screenshots stay available for when how something looks on screen matters, such as effects hiding a Goo, which a map can't show. The scenario harness already saves one with `shot=`; taking several around a moment is added only if a review needs it. A screenshot needs a real window, so one opens on the designer's computer.

## The designer's fights

- Detectors run in the designer's own fights in the editor, and their counts, flagged moments and track go into the fight log, as with pilots, so the designer's fights get overview maps and strips of their flagged moments, drawn on request from the log and the recording.
- A designer's fight can't be replayed, so it records the picture around the VNP all fight long instead, into a compressed file beside its log, and any moment of it can be drawn afterwards, flagged or not.
- This shows where the designer's play differs from the pilots': for example, if pilots touch Goos five times a minute and the designer once.

## Reports

- **Fight logs:** each detector's count and rate, its flagged moments with fight time and clock time, their snapshots (for a recorded fight, the designer's, only the moments' times: their pictures come from the recording), and the fight's track and events, including waves and escalation steps.
- **Batch report:** a Review section, in both the Markdown and the page with charts:
  - each detector's rate per minute, by combination, beside the designer's own rate from their logs on the same tuning;
  - for each detector, its flagged moments worth looking at, each with its strip and the command to replay it;
  - the overview maps it drew.
- **Checking a change:** a pilot or tuning change can be judged by whether it lowers the detector rates it was meant to, without anyone watching.

## Automatic calibration

Moved here from the automated playtesting spec, where it was kept as an option.

- **Targets from the designer's logs:** win rate, level at 5:00, time to the kill target, damage taken per minute, drones lost to Goos per minute, time spent holding still, and now each detector's rate, on the designer's own starting weapons. As built, the search scores by health, drones, kills, level and survival at the same fight times (`compare_to_designer.py`'s gap); detector rates are not among its targets yet (see the addendum).
- **Search:** a script tries values for the expert pilot's settings (such as caution, dodging, harvest weight, `PressureFall` and the regroup thresholds) as tuning variants, plays each candidate on those starting weapons, and keeps the closest match to the targets.
- **Running it:** unattended, overnight or in the background. Batches run together while iterating stay under a minute.
- **Data:** it gets more reliable as the designer's logs grow. A handful of fights is too few to set targets.
- **The designer's part:** playing normally, so the logs grow, and watching a pilot only when the numbers or a map suggest something odd.
- **Two training targets (`docs/dev/PILOT_SPEC.md`; the ceiling pilot is built, with `--target results`):**
  - **Like the designer:** calibration toward the designer's results, as here, against their recorded Grey Goo fights. (Fitting the rebuilt pilot's weights to the recordings first is deferred, at the designer's choice.)
  - **As strong as possible:** the ceiling pilot, calibrated for results instead.

  Strips will also draw the danger shapes enemies declare, and the path the pilot chose.

## Keeping it out of builds

All of it is a testing aid: detectors, tracks, snapshots, recordings, maps and calibration. Code in the game project goes in `scripts/cheats/playtesting/`, behind `#if TOOLS`, under the same rule as the playtest cheats. The few hooks it needs on game classes outside that folder, such as each weapon's `CollectPlaced`, go inside `#if RECORDING`, defined only for the editor's Debug build (see the addendum). An exported game has none of it.

## Tuning values

| Value | Where |
|---|---|
| Each detector's thresholds, such as distances, counts and how long something must last | the review settings file in `scripts/cheats/playtesting/` |
| How often the track records the VNP's position | the review settings file |
| How many pictures a strip has, how far apart, and how far before and after a moment it reaches | the review settings file |
| How many overview maps and strips a batch draws | the review settings file |
| Whether the designer's fights are recorded, how often (`RecordPlayerFights`, `RecordSpacing`), and whether their maps are drawn when the log is written (`DrawPlayerMaps`) | the review settings file |
| How long after the VNP last harvested it still counts as harvesting (`HarvestingWithin`) | the review settings file |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Which fights and moments a batch draws:** overview maps of the best, worst and a typical fight per combination; strips of a few moments per detector, spread across different fights and combinations.
- **Calibration targets' weights:** every target counts equally until there is data to say otherwise.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Suggested build order

Build one stage at a time, and stop after each so the designer can try it.

1. **Detectors in fight logs,** for pilots and the designer, with counts, rates and flagged moments, and the batch report's rates beside the designer's.
2. **Overview and strip maps:** the track and snapshots in fight logs, overview maps, strips of flagged moments, strips of any moment of a pilot fight from the scenario harness, and the report listing them.
3. **Claude's review of a batch:** the first reviews, reported to the designer, with what was confirmed and any new detectors they suggest.
4. **Automatic calibration,** with the detector rates among its targets.

All four stages are built. Phase 3 resumed after this spec, with its stages 3 and 4. Each stage here is finished when it does what its line describes, the check-stage skill passes, and the designer has tried it.

## Out of scope

- A replay page the designer can scrub through. Possible later from the same tracks and snapshots.
- Judging how the fight feels or looks, which stays with the designer.
- Machine learning, as in the automated playtesting spec.

## Done when

- Every fight, the designer's and pilots', records how often each detector fired and when.
- A batch report shows each detector's rate beside the designer's, with overview maps and strips of flagged moments that Claude can read.
- Claude can review a batch from its maps and report problems it found on its own, confirmed against the numbers and the pilot's reasoning.
- Automatic calibration runs unattended and brings the average pilot's results and detector rates near the designer's. (Built and run unattended on the expert, to which the aim moved; its results came within a gap of 0.120 of the designer's three fights before the search ran out of settings. Detector rates as targets are deferred.)
- None of it is in an exported game.
