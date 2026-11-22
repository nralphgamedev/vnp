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
- **The designer's playtests** in the editor write a fight log automatically, to `playtests/human/`. The result screen notes where it was saved. These logs are how pilots are calibrated (see Pilots). They also record the designer's movement input over time at a low rate, as the start of the data any future learning from human play would need. Pilot fights record their pilot's movement the same way, so the two can be compared.
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
  - Back off: when a fight turns into one it can't afford, pull away. Pulling away lets the pack thin out or string out, so the weapons and drones can take it on at its edge.
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

## Planned: weapon profiles for pilots

Agreed with the designer during stage 2, to build in stage 4, measured with batches.

- **General behaviors, written once:** a pilot has a small fixed set: keep a distance, keep moving, circle its target, lead chasers across a spot (such as its own mines), and stay close to enemies.
- **A profile per weapon, as data:** a pilot profile in each weapon's folder weights those behaviors for that weapon. Several behaviors per weapon is simply several entries. Mine Layer might weight keeping moving and leading chasers across its mines; Scatter Shot might weight a close distance.
- **Blending, not new behavior files:** each decision, the pilot adds up the profiles of everything equipped, each weighted by how invested it is in that weapon (upgrades taken). So weapon combinations blend without new files.
- **Combination overrides only where needed:** a small profile for a pair of weapons, if blending can't give what a combination needs.
- **Skill:** a skill value scales how well a pilot uses its weapon profiles, so a novice plays generically and an expert uses its weapons well.
- **Mine Layer is the first testbed.** Its value depends most on positioning, and a designer playtest showed the pilot standing still while harvesting, dropping no mines.

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **Editor button:** batches run from the command line. A button in the editor is added only if the command turns out awkward.
- **How close is close enough:** calibration aims for the average pilot's win rate within 10 percentage points of the designer's, and its average level at 5:00 within 10% of theirs. Adjust once there is data.
- **The designer's input recording rate:** ten samples a second, enough to see how the designer moves without large files.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Settled during the build

The prototype now does the following; any of it can still change.

- **Where a fight is seeded:** as the fight scene enters the tree, before any of its parts are ready, since the asteroids are placed as soon as they are. `Fight.NextSeed` sets the next fight's seed, for the scenario harness and batches.
- **Damage sources:** every hit carries what dealt it: the VNP Weapon, the Drone Weapon in use, or the enemy type, including shots in flight. Each Drone Weapon counts separately, so damage done before a switch stays with the weapon that did it.
- **Fight logs:** written when the fight ends, again if an endless run then ends, and as "unfinished" if the fight is left by starting a new one or closing the game. Each is named by its start time and seed. The result screen shows where it went, in small text at the bottom left, in the editor only. Logging is switched off with `WriteLogs` in `fight_log.tres`.
- **Frame times in fight logs:** the time from the start of the fight's update each frame to its end, measured by the log itself, so it is the game's own work without drawing or waiting for the screen. It works the same headless and in a window. Godot's built-in frame monitor was tried first, but it measures the whole frame, including waiting for the screen, so it read about 16.7 ms in every windowed fight.
- **Timeline and input sampling:** samples keep to a fixed cadence in fight time, so a log of a 3-minute fight has a sample every second and ten input samples a second, not slightly fewer.
- **Damage the VNP took:** like damage to enemies, only the health a hit actually took off counts, so the last hit's excess doesn't inflate what killed it.
- **Checking a log:** `check_log.py` in the check-stage skill cross-checks a fight log's numbers against each other. Run on the designer's first logged fights, it found a pause bug in frame timing (a level-up offer's open time counted as one long frame), sampling that slipped late, and overkill counted in damage taken. All three are fixed.
- **Pilot (stage 2):** `pilots/Pilot.cs`, with skill levels in `pilots/skill_levels/` (average so far) and builds in `pilots/builds/` (Random and Greedy so far). The VNP takes its movement from a controller when one is set, and from the player's input otherwise.
  - **Deciding:** every reaction time, varied a little. It sees enemies, enemy shots and material within its awareness radius and on screen, the same area a player sees, using the steady, window-independent view that spawning uses. At first its sight was capped at how far the screen shows in its shortest direction (about 36 units, up and down), while the screen shows about 68 sideways. A ring of asteroids at the side of the designer's screen was invisible to the pilot, which then spent 13 seconds neither fighting nor harvesting. Its awareness radius now covers the whole screen.
  - **Threat:** each enemy adds threat by closeness, falling off to nothing at `ThreatRange`. This range is separate from how far it sees: when it measured closeness against its sight, seeing the whole screen made every enemy feel twice as close, and the pilot backed off from fights it used to take. An enemy that absorbs drones counts for more the closer the drones are to their cap.
  - **Affording a fight:** what it can afford is its affordable threat at full health, scaled by health, drones, level and caution. It starts backing off at three-quarters of that, and fully by one and a quarter.
  - **Fighting:** it keeps its preferred distance from the nearest enemy and circles it. While its drones matter, it doesn't approach drone eaters. It wants to fight less the fewer drones it has, down to `FightWithoutDrones` of its fight weight with none. At the designer's request, a pilot that has lost its drones looks for asteroids rather than fighting with its VNP weapons alone. At a quarter of the weight, wins halved, so it starts at half.
  - **Harvesting:** it scores material by its worth and distance. Its worth is its mass plus `ClusterWeight` of the asteroids within `ClusterRadius` and `DebrisClusterWeight` of the debris there, so it prefers clusters, as the designer does. Debris counts for less, at the designer's request. Debris piles up where enemies died, usually in the middle of a fight, and counting it as fully as asteroids drew pilots into fights and cost them their drones. At a little under a third of the asteroid weight, it made no measurable difference over 12 seeds. It harvests at full weight while its drones are below their cap, and at `HarvestAtCap` of it once they are at the cap. The designer still harvests at cap for the fight experience, and doesn't ignore a concentration of material unless it is too dangerous, so `HarvestAtCap` starts high. A concentration pulls harder: harvest weight rises by up to `ConcentrationPull` for material whose worth reaches `ConcentrationMass`, so a rich field can draw it away from a fight it could take. Danger near the material scores it down first. Upgrades change what harvesting is worth to it, as they do to the designer: Debris reward upgrades make debris worth more, by `DebrisRewardInfluence` of their effect, and Harvest rate upgrades raise how much it wants to harvest, by `HarvestRateInfluence` of theirs (half, to start). It discounts material with enemies near it by caution, sticks with its target, and holds still once its beam reaches. Once its target is within beam reach plus `HarvestCommitDistance`, and it isn't backing off, it commits and closes in. Before, the pull toward a nearby rock could balance against keeping fighting distance, and the designer saw it hover just out of reach in the middle of a concentration. It keeps harvesting while backing off. At first, backing off switched harvesting off, so a pilot at low health, which can never afford a fight again, only fled. In one test it went over 500 units from the gateway and gained nothing more.
  - **Remembering and exploring:** it remembers the material it has seen, and forgets material that should be in sight but isn't, because it was used up. With nothing to fight or harvest, it returns to remembered material or explores. The chance of returning grows with what the remembered material is worth: its mass, counting half at `ReturnHalfDistance` away. It picks where to go in proportion to that worth. `ExploreUrge` sets how readily it explores instead. This was added at the designer's request, after the first pilot wandered off into empty space. The designer asked for returns in proportion to mass; distance was added because picking by mass alone sent the pilot across the map. A very high `ReturnHalfDistance` picks by mass alone. Over 8 seeds, pilots with and without returning did equally well; batches will tell with more fights.
  - **Randomness:** the pilot uses the fight's own random numbers, so a pilot fight repeats from its seed.
- **Pilots and level-ups:** the level-up menu asks the pilot when one flies the VNP. It waits the pilot's `UpgradeDecisionSeconds`, so a watched fight can be followed, then picks or rerolls. The build scores each option: its own or its category's weight, plus `FavourOwned` for an upgrade to something already equipped, plus `NewWeaponWeight` for a new weapon, plus randomness. It rerolls once per offer if nothing reaches `RerollBelow`.
- **Speed:** a pilot's wants decide only which way it goes. It moves at full speed, as a player holds the stick right over, except while easing in on the material it is about to harvest, so it doesn't overshoot, and when its wants balance out with no clear direction. Two earlier versions slowed it wrongly:
  - **Wants as speed everywhere:** a pilot whose drones were at cap, harvesting at reduced weight, crept between fights at 40% speed.
  - **Wants as speed in a fight:** wants pulling different ways, such as rocks ahead and enemies behind, partly cancelled. The designer saw it slow down with asteroids in view but far off. Measured on that fight, it was below 80% of top speed for 65% of the fight, and averaged under a fifth of top speed in the stretch the designer saw.

  Now it is below 80% of top speed for 42% of that fight, mostly while turning and speeding back up, and stands still 4% of the time. Over 12 seeds: 9 wins instead of 6, level 13.2 instead of 12.8, more asteroid experience, and many more drones alive at 2:00.
- **Seeing the whole screen and concentrations:** replaying the designer's watched fight with these changes, the pilot harvested steadily where it had sat idle, and won at 2:33 at level 15 where it had lost at 3:08. Over 12 seeds: 8 wins instead of 6, sooner (2:36 instead of 2:56), and fewer drones lost; level slightly lower (12.4 instead of 12.9).
- **The clock:** the HUD clock counts down from the time limit, so a moment the designer reports by the clock, such as 3:30, is 1:30 of fight time in fight logs and traces.
- **Effect of the designer's playing style:** over 12 seeds, the drone-aware harvesting, fighting and asteroid clusters together left the average pilot slightly weaker than before: 8 wins instead of 10, though sooner when it won, and level 12.7 instead of 13.2. They are kept, because the aim is a pilot that plays like the designer, not the strongest one. Calibration in stage 5 will tune it against the designer's fight logs.
- **Repeatable fights:** two causes were found and fixed while testing pilots.
  - **The screen shake moved spawn points.** Where enemies spawn off screen, and what a pilot sees, were measured from the camera after its screen shake, which uses its own unseeded randomness. Taking the shake back out afterwards wasn't enough, because floating-point rounding still differed between runs, and the fight's chaos amplified a difference of 0.0001 into a different fight.
  - **The window size moved spawn points.** Off-screen distance was measured from the window's actual size, so a fight differed between headless and a window, or between windows of different sizes.

  The camera now works out what is on screen from its own steady position and settings, at the project's reference 16:9 shape (`viewport_width` and `viewport_height` in the project settings), without the shake and independently of the window. The shake only moves what is drawn. Same-seed fights now match exactly, run several at once, headless or in a window. The calculation was checked against Godot's own projection and agrees to within 0.0001 units. The trade-off: in a window much wider than 16:9, enemies can spawn just inside the sides of the screen, within the spawn margin. This is left for later; the designer is noting it in the GDD.
- **Repeatability test:** the check-stage skill runs the same 90-second pilot fight twice at once and compares their traces line for line, so a change that breaks repeatability is caught before it is committed. Deliberately giving the pilot unseeded randomness made it fail at the first checkpoint, half a second in.
- **Checking a fight repeats:** `trace=N` in the scenario harness prints the fight's exact state every N frames: where the VNP, enemies and drones are, and how many random numbers have been drawn (`Rng.Draws`). Two runs that should match print the same lines, and the first line that differs shows where they parted.
- **Endless after a win:** the scenario harness, not the pilot, chooses to continue into endless mode on the result screen, since that choice belongs to the screen, not to flying. Rival and escort VNPs will have no such screen.
- **Running it on Windows:** `tools/scenario.cmd` runs the scenario harness from PowerShell or a Command Prompt, for example `.\tools\scenario.cmd -Window pilot=average build=greedy frames=20000`. It is headless unless `-Window` is given. It starts `tools/scenario.ps1`, bypassing PowerShell's script execution policy for that run only, so nothing on the computer needs changing. `tools/` is kept out of Godot's import.
- **The fight pauses in the harness:** the scenario harness keeps itself running while the game is paused, and the fight under it copied that, so in scenario runs the fight never paused: not during level-up offers, and not at the result screen. Pilots had half a second of play while choosing each upgrade, which affected every pilot result until it was found. The designer noticed the fight still playing behind the result screen. The fight now pauses as it does in a normal game. The designer's own playtests were never affected.
- **Pilots and the designer's upgrade picks:** the Greedy build takes no harvesting upgrades, by design: it favours what it already has and new weapons. The designer's logs show them taking harvesting upgrades often (harvest rate four times and debris reward twice in one round). A build based on the designer's own picks is planned for stage 4, and calibrated in stage 5, rather than changing Greedy.
- **Watching a scenario:** in a window, the scenario harness stays open when it finishes, on the result screen if the fight ended, or paused if it reached its frame limit, until the window is closed. The designer asked for this so the final screen can be read. `close=yes` quits instead. Headless runs, and runs taking a screenshot, always quit.
- **Quitting cleanly:** long fights that had ended sometimes crashed as the game quit, every time for some seeds. Godot's C# layer disposes every Godot array still in use at shutdown, but arrays that were already garbage, waiting for .NET to clean them up, slipped past it, and .NET cleaning them up after Godot had gone crashed the game. `ShutdownCleanup` now has .NET clean up first, while Godot is still running: the fight does it when the window is asked to close, and the scenario harness before it quits. This affected the real game as well as testing. The check-stage skill now runs a full pilot fight that ends and requires it to quit cleanly. Removing the fix made that check fail with the same crash.
- **Harvesting into danger:** while backing off, material's pull drops with the danger around it, divided by 1 + backing off × danger × `HarvestDangerWhenBackingOff`. The designer watched a pilot with no drones fly straight into a big cluster of Goos at the end of a fight, and be destroyed in half a second as many touched it at once. Its reasoning (see explain below) showed it was backing off fully, under pressure two to fourteen times what it could afford, yet heading for rich debris inside the cluster: the harvest pull beat backing off. On that seed it now survives a minute longer. Over 12 seeds results were otherwise similar, with Goos dealing 14% of the damage the VNP took.
- **Explaining a pilot's choices:** `explain=FROM-TO` in the scenario harness prints the pilot's reasoning at each decision between two fight times in seconds. It shows the threat it felt against what it could afford, whether it was backing off, what it was fighting, what it was harvesting and how dangerous that was, and its drones and health. When the designer reports something odd at a time on the clock, replaying that seed with `explain` shows why it happened. The pilot keeps this as a line of text it updates each decision; nothing in the game reads it.
- **What the scenario prints:** a status line every 30 seconds of fight time (`report=N` frames changes it), timed by fight time so pauses don't shift it, and one at the end. Each level-up prints when it happened, what was offered and what was taken. Each reroll prints the offer before and after. Times show as fight time and as the on-screen clock, which counts down. At the designer's request, this replaced a status line every second.
- **Batches (stage 3):** `tools/batch.cmd` runs one from PowerShell (`tools/batch.py` underneath, which needs only Python), for example `tools\batch.cmd --builds greedy --weapons all`.
  - **What it runs:** every combination of `--pilots`, `--builds`, `--weapons` and `--drone-weapons` (each a comma-separated list, `all` for every choice in the starting weapon cheat's lists, or `default` for the game's own) on seeds 1 to N. `--size quick|standard|long` or `--seeds N` sets N.
  - **How it runs them:** each fight is a scenario harness run, headless, several at once (one per processor core, leaving one spare). `--rendered` runs them in a window instead, one at a time, muted when there is more than one. A fight still going at the endless cap (`--cap`, or `EndlessCapSeconds`) is stopped and logged as "capped".
  - **What it writes:** each batch gets a folder in `playtests/batches/` with `batch.json` (what ran and how each fight went), the fight logs in `logs/`, each fight's console output in `output/`, and `report.md`. `tools/batch_report.py` writes the report again from the logs.
  - **Variants in reports:** with more than one variant, every combination is labelled with its variant, and the "Fights worth watching" commands include it.
  - **Watching fights from a batch:** the report's "Fights worth watching" section picks, for each combination, the best fight (the fastest win, or the longest-lasting if none won) and the worst (destroyed earliest), with the command that replays each exactly in a window. Added at the designer's request. A replay matches the batch fight as long as the game hasn't changed since; a replayed worst fight matched to the kill.
  - **Speed:** batches run far faster than planned. A standard batch of 40 fights took 45 seconds on 19 cores, so the standard size was raised from 20 to 50 seeds per combination.
  - **First standard batch:** the average pilot won half its fights with either build (10 of 20 each), and none of the 40 fights reached 5:00 alive. That bears on the designer's question about reaching the 5:00 mark, once pilots are calibrated against the designer.
- **Tuning variants (stage 4):** a variant is a text file in `tools/variants/` (`NAME.txt`), one change per line: `levels.ExperienceForFirstLevel = 120` for a game-wide tuning file, or `enemies/goo/goo.tres:Health = 80` for any `.tres` by its path. Lines starting with `#` are notes. The scenario harness's `variant=NAME` applies one in memory before the fight is made, holding the changed files so Godot's cache can't reload them unchanged. A value is read as the same kind as the value it replaces, and every line that can't be applied is reported as an error. The fight log records the variant, and folds its text into the tuning fingerprint. `--variants base,NAME` in a batch plays the same fights with each, and the report's comparison section puts them side by side, with differences against the first. Because a variant can change any `.tres`, it can also switch pilot behavior on and off, as `no_weapon_profiles` does.
- **First variant results:** `gentle_levels` (120 for the first level, then 50 more per level, instead of 150 and 75) put pilots 3 to 4 levels higher at 3:00, and won 12 to 20 points more often. But it didn't get them to 5:00: still only 2 to 4 fights in 50 alive at 5:00. Something other than the experience curve ends fights before 5:00.
- **Skill levels (stage 4):** novice and expert sit either side of average. A novice reacts in about half a second, notices less (a smaller awareness radius), rarely dodges, wanders more, is less cautious, underestimates Goos, and picks upgrades more randomly. An expert reacts in about an eighth of a second, dodges nearly every shot it sees, barely wanders, and rates Goos higher. Everything else matches average. Over 50 seeds with Greedy: novice won 60%, average 72%, expert 88%, and 0, 4 and 6 fights were alive at 5:00.
- **Builds (stage 4):**
  - **Drone swarm:** drone cap, new drones, drone damage and attack rate.
  - **Weapon focus:** the starting weapon and its upgrades, avoiding new weapons.
  - **Area:** Shockwave, Mine Layer and area of effect.
  - **Tank:** health, armor, and Orbital Shards with blocking.
  - **Designer:** weighted by the designer's own picks in their first four logged fights: drone cap, debris reward, drone attack rate, harvest rate and drone damage high; health, armor, movement speed, new drones and Ram low; new weapons taken when a slot is free. Four fights is a small sample, so it follows the clear preferences rather than exact rates.

  Over 50 seeds each, with the average pilot: Area won 84%, Greedy 72%, Random and Designer 62%, Drone swarm and Weapon focus 48%, and Tank 30%. Designer reached the highest final level (14.4).
- **Weapon profiles (stage 4):** built as planned (see Planned: weapon profiles for pilots).
  - **Profiles:** `WeaponPilotProfile`, in `pilots/`, has `PreferredDistance`, `KeepMoving` and `LeadChasers`. Each VNP Weapon's definition names its `pilot_profile.tres`. Preferred distances come from each weapon's reach: Orbital Shards 6, Scatter Shot 9, Shockwave 10, Mine Layer 12, Pulse Laser 15, Seeker Missiles 18. Only Mine Layer keeps moving and leads chasers so far.
  - **Blending:** each decision, the pilot blends the equipped weapons' profiles, weighted by investment (1 for having the weapon, plus 1 per upgrade taken for it), scaled by its skill level's `TacticSkill` (novice 0.3, average 0.7, expert 1.0).
  - **Keep moving:** the pilot circles the rock it is harvesting instead of holding still, at a fifth of top speed, just above the speed Mine Layer needs to drop mines. At up to 45% of top speed, circling lost the beam often enough to cost about a tenth of the fight experience by 2:00.
  - **Lead chasers:** with enemies close, the pilot steers toward a lure spot ahead of it as it moves away from them, so they follow it across. A weapon offers lure spots through `IPilotLure`; Mine Layer's are its mines. `LeadWeight` in the skill level sets how strongly.
  - **Results:** over 50 seeds starting with Mine Layer, against the same fights with profiles off (variant `no_weapon_profiles`):
    - **Designer:** mines' share of its damage rose from 42% to 47%; it lasted longer (destroyed at a median 4:12 instead of 3:48) and finished 0.8 levels higher; the win rate was the same.
    - **Greedy:** it already did 54% of its damage with mines, gained little, and finished a level lower.
    - **Both:** a little less fight experience by 2:00.
  - **Mine Layer is strong to start with:** Greedy won all 100 of its fights starting with it.
- **The page with charts (stage 5):** every batch writes `report.html` beside `report.md`, made by `tools/batch_report_html.py` (run it on a batch folder to write it again). It has the same text and tables as the Markdown, with charts at the top of the sections they illustrate: how each fight ended (Outcomes), the share of fights still alive and the median level of those alive over fight time, with the 5:00 limit marked (Level curve), what destroyed the VNP by enemy type (Losses), and damage dealt by source (Matchups). Hovering a bar or line shows its numbers. It is one file with nothing to install, so it opens from the folder in a browser; Claude publishes it as a private page on request. The time charts show at most eight combinations, the palette's eight colours; the tables cover the rest.
- **Calibrating the Designer build (stage 5):** `tools/calibrate_build.py` fits the Designer build's upgrade weights to the designer's choices in their fight logs, and writes them to `pilots/builds/designer.tres` (`--dry-run` only prints them). Each answered level-up offer is a choice among three. The fit finds a weight per upgrade, plus a bonus for upgrades to what is already owned and one for new weapons, that best explain the picks. Upgrades seldom offered are pulled toward their category's weight. The weights are scaled to the pilot's own randomness, so it picks with about the designer's spread. Logs from older versions count, since preferences change less than tuning. After the first fit, the pilot's take rate when offered was within about 10 points of the designer's for most upgrades.
- **Batches start with random weapons (stage 5):** at the designer's request, `--weapons` and `--drone-weapons` now default to `random`: each fight starts with a VNP Weapon and a Drone Weapon picked from the starting weapon cheat's lists. The pick follows the seed alone, so a seed starts the same way in every batch, and for every pilot, build and variant within one, which keeps comparisons fair. The report adds a section, By starting weapons, with results for each starting weapon. `--weapons default --drone-weapons default` gives the game's own starting weapons. The designer's own fights still start on the starting weapon menu.
- **First calibration fight (Orbital Shards with Beam drones):** with the designer's picks, the pilot did no better: it lost its drones by about 2:00, where the designer kept around 20 throughout, took damage from the first seconds, and harvested about a third less. Picks were not the gap; flying is. The designer pointed to the drones: fine against a few Goos, but against many they lock onto one and fly into others. Drones now avoid danger (see the phase 3 spec), which roughly doubled pilots' drones at 2:00.
- **Second calibration fight (Seeker Missiles with Beam drones):** with drone avoidance and the refitted Designer build, pilots on the same start won 19 of 25 and kept level with the designer for the first minute and a half. After that the designer pulled ahead, with about twice the drones; much of that came from being offered Drone cap and Debris reward more often, which one fight can't separate from style. A variant valuing debris more (`debris_keen`) changed nothing.
- **Keeping clear of Goos:** across both calibration fights the designer took almost no damage from Goos, while pilots took about a fifth of theirs from them. Pilots now steer clear of enemies that damage by touch (`TouchAvoidDistance` and `TouchAvoidWeight` in the skill levels; variant `no_touch_avoidance` turns it off). It cut Goos' share of the damage pilots took from 22% to 15%, but their total damage taken stayed the same, so the win rate didn't move. It is kept, since it matches how the designer plays.
- **How the designer moves:** comparing movement, the designer stands still a third to nearly half of the time, against 5% for pilots, turns sharply less often, and takes damage at a little over half the pilots' rate.
- **Holding still:** the designer holds still mostly while harvesting, and in a fight while there isn't much danger. Pilots now do the same: below `HoldStillPressure` (in the skill levels) they stop circling and leave the fight to the drones and weapons, closing in only on an enemy well beyond their preferred distance, and blending back into circling by half as much again. While harvesting at that pressure they keep holding at the rock; a weapon that needs movement, such as Mine Layer, still circles it. Variant `no_hold_still` turns it off. Tried at two pressures over 25 fights each, the higher one did better than not holding still: destroyed at a median 3:53 instead of 3:21, 1.5 levels higher, and less damage taken per minute. Pilots still stand still far less than the designer (15% of the time against a third or more), so they spend more of the fight travelling.
- **Harness options added for batches:** `maxtime=SECONDS` stops a fight at that fight time and logs it as "capped". `logname=` names the log, so fights started in the same second with the same seed don't overwrite each other. `mute=yes` silences it.
- **Scenario harness:** `pilot=ID` and `build=ID` have a pilot fly the VNP. Its status lines include the VNP's distance from the gateway. `seed=N` repeats a fight. By default it takes the first option of each level-up offer half a second after it opens, so its fights carry on through level-ups (`upgrades=none` leaves the fight paused on the offer). `log=FOLDER` writes the fight log there, and `log=none` writes none. By default, at the designer's request, a pilot fight is logged to `playtests/pilot/`, and a fight without a pilot isn't logged. Either way, scenario fights never land among the designer's own logs. Fights started by it are logged as played by "scenario", with the pilot and build in the settings.
- **Result screen fix:** a level-up offer that opened in the same moment the fight ended no longer shows through behind the result screen.

## Suggested build order

Build one stage at a time, and stop after each so the designer can try it.

1. **Seeds, fight statistics and fight logs.** Seeded fights, the statistics, the result screen's damage breakdown (phase 3 stage 2), and fight logs written for the designer's playtests.
2. **A pilot.** The movement controller, one skill level (average) and the Random and Greedy builds. Shown flying in a window through the scenario harness, so the designer can watch it play.
3. **Batches and the Markdown report.** The batch command, sizes, parallel headless fights, the rendered option, muting, the endless cap, and the report. The check-stage skill gains a quick batch as a smoke test: it passes when every fight finishes without errors and writes its log. Its balance numbers are shown, not judged.
4. **Skill levels, builds, weapon profiles and tuning variants,** with comparison reports: the novice and expert levels, the remaining builds plus one based on the designer's own picks, weapon profiles starting with Mine Layer (see Planned: weapon profiles for pilots), and tuning variants.
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
