# VNP Combat Prototype: Automated Playtesting

The prototype's questions are answered by playing it, and playtesting takes the designer a lot of time. This spec adds a way for the game to play itself: computer pilots that fly the VNP and pick upgrades, run in batches of fast headless fights, with every fight recorded and summarised in a report. The designer's own playtests are recorded the same way, so their results become data too.

The aim is to take the numbers questions off the designer: balance, matchups, upgrade value, and performance. Playtesting by the designer then focuses on what only a person can judge: how the fight feels and reads.

This spec interrupts phase 3 (`docs/PROTOTYPE_3_SPEC.md`). Phase 3's damage breakdown (its stage 2) is built here, as part of fight statistics, and phase 3's remaining stages resume afterwards. Everything in the phase 1, 2 and 3 specs still applies unless this file changes it.

## What this is for

- Can a batch of automated fights show where a fight's balance is off, such as how hard it is to reach the 5:00 mark, before the designer plays it?
- Can a change in tuning be judged by running the same fights before and after it?
- Which weapons, Drone Weapons and upgrades do well against which enemy types?
- Which upgrades are picked and never help, and which builds always win?
- How close can a computer pilot come to playing like the designer, and how can it get closer?

## Terms

- **Pilot:** the logic that flies a VNP and chooses its upgrades, in place of a player. Its behavior comes from a pilot definition (how skilled it is) and a build (what it tries to pick).
- **Fight statistics:** what happened in one fight: damage by source and enemy type, drones lost, kills, level-ups, and so on. Gathered in the game itself. The result screen shows part of them.
- **Fight log:** one fight's statistics and timeline, saved as a file.
- **Batch:** a set of fights run one after another, or several at a time, with chosen pilots, builds, starting weapons, seeds and tuning.
- **Seed:** the number that sets every random choice in a fight. The same seed, pilot and tuning give the same fight.
- **Tuning variant:** a set of changed tuning values, applied on top of the tuning files for a batch, without editing them.
- **Tuning fingerprint:** a short hash of every tuning and content file, so two fights can be checked to have used the same tuning.

## Seeds

- All gameplay randomness already comes from one generator (`Rng`, in `scripts/core/Rng.cs`). Each fight now seeds it at the start. A normal fight picks a seed at random; a batch sets it.
- The seed is shown on the result screen and in the debug line at the top of the screen, and saved in the fight log.
- Effects and sound variation use their own randomness, so turning effects up or down never changes a fight.
- A batch fight runs at a fixed time step, so the same seed, pilot and tuning give the same fight, and a surprising result can be run again in a window to watch it.
- A fight played by a person, whose frame times vary, is not reproduced exactly by its seed. Exact replays are out of scope (see Out of scope).

## Fight statistics

Gathered during every fight, by the game. This is phase 3's damage breakdown, extended.

- **Damage** by source (each VNP Weapon, each Drone Weapon while in use, and any other source) and by enemy type. Only damage that took health off an enemy counts; the part past its remaining health is left out.
- **Drones:** started, built, destroyed, and absorbed, by the enemy type responsible.
- **Orbital Shards blocking:** enemy shots stopped.
- **Kills** by enemy type and the time of each, and the time the kill target was reached.
- **Level-ups:** the time of each, and the three upgrades offered, any rerolls, and the one taken.
- **The VNP:** damage taken by enemy type, and health over time.
- **Fight experience** harvested over time, split into asteroids and debris.
- **End:** outcome (win, loss, retreat), time, and for endless, time survived after the win.

The result screen shows the damage breakdown, drones lost and shots blocked, as phase 3's Damage breakdown section describes. The rest is for fight logs.

## Fight logs

- In the editor, every fight writes a fight log. It holds:
  - its settings: seed, pilot or player, build, starting weapons, tuning variant, tuning fingerprint, game version from git, and which playtest cheats were used (starting weapon choice, free rerolls). Once phase 3 adds asteroid amounts, the fight's amount too.
  - its statistics.
  - a timeline sampled every second: time, health, level, experience, drones, enemies alive by type, and kills.
  - frame times: the game logic's average, 95th percentile and worst, with the most enemies and drones alive at once.
- The game keeps the statistics, as totals and events. Sampling the timeline, timing frames and writing the file are the editor-only log writer's job.
- **The designer's playtests** in the editor write a fight log automatically, to `playtests/human/`. The result screen notes where it was saved. These logs are how pilots are calibrated (see Pilots). They also record the designer's movement input over time at a low rate, as the start of the data any future learning from human play would need.
- **Batch fights** write their logs into the batch's folder.
- The format is JSON, one file per fight, readable by scripts.
- Writing fight logs is a testing aid: it exists only in the editor, like the cheats. An exported game writes no logs.

## Pilots

A pilot flies a VNP the way a player does: it chooses a direction to move each moment, and picks from each level-up offer. It sees what a player could see, the fight around the VNP within its awareness, and never reads hidden information such as the next wave's timing.

- **Reuse:** pilots are game code, in their own feature folder, `pilots/`, not editor-only. The full game will have several VNPs flown by the computer, and the same pilot logic is meant to fly them:
  - rival VNPs, which the GDD says will behave much like the player;
  - escort VNPs, the player's own autonomous VNPs that go into fights alongside the player's VNP;
  - the player's other VNPs generally: on the galactic map the player can swap which VNP they control, and the one they leave becomes autonomous.

  So a pilot flies any VNP it is given. It must not assume it flies the player's VNP or that it is the only VNP in the fight. For now, pilots only fly the player's VNP in batches and scenarios, and nothing in a normal fight uses them.
- **Input:** the VNP takes its movement from a controller: the player's input, or a pilot. Nothing else about the VNP changes. A level-up offer flown by a pilot is answered by the pilot, which picks one of the offered upgrades or rerolls, through the same choices the menu gives a player.
- **Awareness:** a pilot sees within a radius around the VNP, no farther than a player would see on screen. It notices an incoming shot within that radius with a chance set by its dodging skill, and reacts only after its reaction time.
- **Movement** is a set of simple, readable wants, weighed against each other each moment. Underneath them is one judgement: whether the pilot can afford a fight, given how strong it is right now (its health, its drones, and its weapons and upgrades) against what it faces. Engaging enemies is normal; the pilot pulls back only when a fight risks wearing it down too far. Its caution (part of its skill level) sets where that line is.
  - Harvest: head for the most worthwhile material nearby, even with enemies around it, as long as the fights on the way are ones it can afford.
  - Fight: move so the weapons and drones can reach enemies, without getting surrounded.
  - Back off: when a fight turns into one it can't afford, pull away. This covers dense groups, Goos that would eat a drone-reliant build, incoming shots it notices, and too many enemies drawn in at once. Pulling away lets the pack thin out or string out, so the weapons and drones can take it on at its edge.
- **Never retreats:** a rule rather than a want. After a win, a pilot continues into endless mode and plays until it is destroyed. Moving away from danger and searching for material is fine.
- **Upgrades:** a build sets what a pilot prefers. Each build is data: a weight for upgrades, categories, weapons and Drone Weapons, and when to reroll. The pilot scores each offered upgrade by its build's weights and takes the highest, with a little randomness that shrinks as skill rises, so two fights with the same build don't always pick alike. Starting builds:
  - Random: picks at random, as a baseline.
  - Greedy: a simple sense of value that favours what it already has.
  - Drone swarm: drone cap, new drones, drone damage and attack rate.
  - Weapon focus: one VNP Weapon and its upgrades first.
  - Area: Shockwave, Mine Layer, and area of effect.
  - Tank: health, armor, Orbital Shards and blocking.
- **Skill levels** are data too: how often it reconsiders where to move (its reaction time), how far it is aware of things, how well it notices and dodges shots, how cautious or greedy it is, and how much its movement wanders. Starting levels: novice, average, and expert.
- **Calibration:** the average pilot is adjusted until its fight logs land near the designer's own on the same tuning (the same tuning fingerprint). The report compares win rate, level at 5:00, time to the kill target and drones lost, and calibration aims at the first two (see Not yet decided). The designer's fights that used free rerolls are left out, since pilots don't get them. The starting weapon choice is fine: the batch uses the same weapons.

## Batches

- **Running one:** a command runs a batch from the command line, headless, so it can run any time while other work goes on. Claude or the designer can start it. An editor button is a later stage, if it turns out easier to use. Batch fights start straight away with the batch's weapons, skipping the starting weapon menu, as the scenario harness (`scripts/cheats/scenario.tscn`) does now.
- **What a batch varies:** pilots, builds, starting VNP Weapon and Drone Weapon (as the starting weapon cheat chooses them), seeds, and tuning variants. Each combination runs on every seed.
- **Seeds:** a batch with N seeds uses seeds 1 to N, so two batches of the same size play the same fights.
- **Sizes**, each a number of seeds per combination in the batch settings:
  - Quick: a few fights, in about a minute, for a smoke test after a change.
  - Standard: enough fights for clear averages, in about ten minutes.
  - Long: as many fights as asked for, for overnight tuning sweeps.
- **Headless by default:** a batch runs headless, at a fixed time step, as fast as the computer allows: each frame advances the fight by the same small step, however long it took to compute. Nothing is drawn, so effects are off, which changes nothing in the fight since they use their own randomness. Several fights run at once as separate game processes, one per spare processor core.
- **Rendered, as an option:** a batch can instead run its fights in a window, at normal speed, with effects on, to watch how pilots play or to look for something that only shows on screen. They run one at a time unless the batch asks for more at once. They use the same fixed time step, so a rendered fight plays out exactly as the same fight run headless.
- **Sound:** headless fights never have sound. A rendered run that starts more than one fight is muted. A single rendered fight, such as rewatching one seed, keeps its sound.
- **Endless cap:** a fight stops at a set fight time, counted from the start of the fight, recorded as "survived to the cap", so one strong build can't hold up a batch.
- **Tuning variants:** a variant is a short text file listing changed values, one per line, by tuning file and value name (for example `levels.ExperienceForFirstLevel = 120`). It applies on top of the tuning files when each fight starts. Comparing variants runs the same seeds, pilots and builds on each.
- **Where results go:** each batch gets a folder in `playtests/batches/`, holding its settings, its fight logs and its report. `playtests/` is kept out of git (by `.gitignore`) and out of Godot's import (by a `.gdignore` file), so batches never clutter the project.

## Reports

Each batch writes a report, with the same content in two forms:

- **Markdown:** in the batch's folder, readable anywhere, including by Claude.
- **A page with charts:** published privately for the designer by Claude, on request, from a batch's folder. The batch command itself only writes files.

A report covers:

- **Outcomes:** win rate and time to the kill target, by pilot, build and starting weapons.
- **Level curve:** level reached at 1-minute marks and at 5:00, and how many fights reached 5:00 alive.
- **Losses:** what killed the VNP (damage taken by enemy type), and when.
- **Endless:** time survived after the win.
- **Matchups:** damage share of each weapon and Drone Weapon against each enemy type, and drones lost to each enemy type.
- **Upgrades:** how often each upgrade was offered and taken, and win rates with and without it.
- **Performance:** game-logic frame times from the fight logs (average, 95th percentile and worst), next to the most enemies and drones alive at once.
- **Comparison:** for a batch with tuning variants, each number side by side, with the difference.
- **Calibration:** pilots' numbers next to the designer's own fight logs on the same tuning.

## Keeping it out of builds

- **Editor-only:** the batch runner, fight log writing, tuning variants, the reports, and any editor button. Code in the game project goes in `scripts/cheats/playtesting/`, behind `#if TOOLS`, under the same rule as the playtest cheats. Scripts that only read logs and write reports run outside Godot and need not be in the Godot project.
- **In the game:** fight statistics (the result screen uses them) and pilots (rival VNPs will). Neither does anything in a normal fight unless asked to: fight statistics only count, and no VNP has a pilot.

## Tuning values

As in the phase specs, this file names each value and where it lives rather than its number.

| Value | Where |
|---|---|
| Each skill level's reaction time, awareness, dodging, caution, greed and wander | its pilot definition file in the pilots folder |
| Each build's preferences and reroll rule | its build file in the pilots folder |
| How heavily a pilot weighs harvesting, fighting and backing off, and how it judges whether it can afford a fight | the pilot definition files |
| Batch sizes, the endless cap, and how many fights run at once | the batch settings file in `scripts/cheats/playtesting/` |
| How often the timeline is sampled, and how often the designer's input is recorded | the fight log settings in `scripts/cheats/playtesting/` |

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Editor button:** batches run from the command line. A button in the editor is added only if the command turns out awkward.
- **How close is close enough:** calibration aims for the average pilot's win rate within 10 percentage points of the designer's, and its average level at 5:00 within 10% of theirs. Adjust once there is data.
- **The designer's input recording rate:** ten samples a second, enough to see how the designer moves without large files.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Suggested build order

Build one stage at a time, and stop after each so the designer can try it.

1. **Seeds, fight statistics and fight logs.** Seeded fights, the statistics, the result screen's damage breakdown (phase 3 stage 2), and fight logs written for the designer's playtests.
2. **A pilot.** The movement controller, one skill level (average) and the Random and Greedy builds. Shown flying in a window through the scenario harness, so the designer can watch it play.
3. **Batches and the Markdown report.** The batch command, sizes, parallel headless fights, the rendered option, muting, the endless cap, and the report. The check-stage skill gains a quick batch as a smoke test: it passes when every fight finishes without errors and writes its log. Its balance numbers are shown, not judged.
4. **Skill levels, builds and tuning variants,** with comparison reports.
5. **Charts and calibration.** The report page with charts, and calibrating the average pilot against the designer's fight logs.
6. **Editor button,** only if the designer wants it after using the command.

Phase 3 resumes after stage 5, with its stages 3 and 4. Each stage here is finished when it does what its line describes, the check-stage skill passes, and the designer has tried it.

## Out of scope

- Machine learning and other ways to make pilots play more like a person. This is a goal for later. The designer's recorded movement and fight logs start the data it would need, and pilots read the fight through the same view a learned pilot would, so one could be added without rebuilding the rest.
- Exact replays of the designer's fights, which would need the fight to run at a fixed time step during normal play.
- Rival VNPs in fights, and anything else from the full game.
- Judging how the fight feels or looks, which stays with the designer.

## Done when

- A batch of fights runs headless from one command, with chosen pilots, builds, weapons, seeds and tuning variants, and writes a report as Markdown and as a page with charts. The same batch can run rendered instead. Headless fights have no sound, and a rendered run of more than one fight is muted.
- The same seed, pilot and tuning give the same fight, and any batch fight can be watched again in a window.
- The result screen shows the damage breakdown, drones lost, and shots blocked.
- The designer's playtests write fight logs automatically, and reports compare pilots with them.
- The average pilot's results land near the designer's on the same tuning.
- None of the testing tools are in an exported game.
