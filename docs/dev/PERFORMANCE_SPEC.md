# VNP Combat Prototype: Performance

VNP is built for very large crowds: hundreds of enemies and drones at once now, and perhaps ten times that later, in the way of Vampire Survivors. Each phase spec has asked for the frame rate to be checked, but until now there was no set requirement, no way to measure it beyond game logic's time, and no test that failed when it was missed. This spec gathers those: the requirement, the tools that measure it, the tests that guard it, what they have found, and what is still open.

The requirement itself is a technical rule in CLAUDE.md, so it applies to all work. This spec describes how it is measured and kept. Everything in the other specs still applies.

## What this is for

- Does the game hold at least 120 frames a second in the worst realistic case, and does a change that breaks it get caught before it is committed?
- When a frame is slow, can the cause be found quickly?
- How large can crowds grow before the game misses its budget, and what has to change to go further?

## Terms

- **Frame time:** how long one whole frame takes by the clock, including drawing, measured with vsync off and no frame cap.
- **Game logic:** the fight's own update each frame: every node's `_Process`, from the first to the last, without drawing.
- **Slowest 1%:** the frame time that only 1% of frames are slower than. It is judged instead of the average, which hides stutters, and instead of the single worst frame, which one stray frame can set.
- **Hitch:** a single frame long enough to be seen as a stutter.
- **Testing cost:** the time testing code (the scenario harness, the fight log, its recorder and detectors) spends in a frame. An exported game doesn't run it.

## The requirement

At the designer's request, at least 120 frames a second on the designer's machine in the worst realistic case. The designer's GPU is far above a typical player's, so a high target on their machine leaves room for weaker ones.

- The slowest 1% of frames take no more than 8.33 ms (120 frames a second), by the clock with nothing extra measuring.
- No frame takes more than 16.7 ms, outside loading and menus.
- Game logic's slowest 1% takes no more than 4 ms. Weaker computers suffer most from CPU work, so logic keeps headroom beyond the frame budget.

The budgets are in `scripts/cheats/playtesting/performance.tres` (`PerformanceSettings`). A change that breaks them is fixed before it is committed.

**What doesn't count:**

- **Loading:** the first `SkipSeconds` of a fight, while code compiles and the fight is built.
- **Menus:** paused frames, such as level-up offers and the result screen.
- **Testing code:** each frame is judged on the game's own time, its frame time less its testing cost. A frame over budget only because of testing work is reported, tagged `[testing]`, and doesn't fail. Testing code wraps its per-frame work in `TestingCost.Begin` and `End` (`scripts/cheats/playtesting/TestingCost.cs`).

## Measuring

All of it is in the scenario harness (`scripts/cheats/ScenarioRunner.cs`), editor only, inside `#if TOOLS`.

- **`perf=plain`:** in a window, with vsync off and no frame cap, times each whole frame by the clock with nothing else switched on. The requirement's frame budget and hitches are judged on it.
- **`perf=detail`:** the same, plus Godot's render timing (render CPU and GPU time) and game logic, timed from the first node's update to the last's by two marker nodes at the lowest and highest update priority. The logic budget is judged on it. Measuring the detail costs too little to see: averages of 2.33 ms with it against 2.35 ms without.
- **What they print:** frames counted; average, slowest 1% and worst of the game's own time; frames over budget and hitches; testing code's average cost and the frames tagged as testing; the worst frames with their fight times (and, in detail, their logic, render CPU and GPU times); and whether the requirement was met. While measuring, the harness holds its own lines and prints them afterwards, since printing is itself slow (see What it has found).
- **`perfstart=SECONDS`:** measures only from that fight time, such as 360 for a late, escalated fight. Up to a second before it, the fight plays faster by Godot's time scale (`FastForwardScale` in `performance.tres`), so a late fight is reached in a fraction of its fight time. That part isn't measured.
- **`profile=yes`:** times each part of the fight's update and prints each part's average and slowest 1% at the end: the fight, pilots, the VNP, harvestables, enemies, drones, projectiles, weapons, effects, audio, camera, ground and HUD. A marker node runs after each update priority's nodes (`UpdateOrder`), so the time between two markers is one part. It works headless, for logic only, and with `perfstart=`.
- **The designer's fight logs:** in a window, each fight log also records each frame's whole time by the clock (held to the screen's refresh while vsync is on, so it shows dropped frames rather than headroom) and render CPU and GPU times, as averages, the slowest 1% and the worst, beside game logic's.
- **Uncapped runs and `maxtime=`:** while measuring, `maxtime=` sets no frame limit of its own, since uncapped frames are many more than 60 a second.

## The tests

Run by the check-stage skill with `--full`, just before every push (before every commit until the designer moved it), in a window. The stage check runs fights on a fixed step of 1/60 of a second, so the measured fights repeat, and each frame carries a full step of fight.

| Test | What it is | What it guards | Fails the check |
|---|---|---|---|
| Stress | A Rich field, a full swarm of Beam drones, Orbital Shards with extra shards and Mine Layer for effects, eight groups of every enemy type plus six more of Goos, and an invulnerable expert pilot, for a minute; once with `perf=plain` and once with `perf=detail` | a heavy early crowd with every effect on | yes |
| Late fight | An invulnerable expert on a Rich field, played to 6:00 fast forwarded, then 6:00 to 7:00 measured with recording on, `perf=detail` and `profile=yes` | costs that grow over a long fight: escalation's crowds, and anything that piles up as the VNP explores | yes |
| Tenfold crowd | 360 groups of each enemy type, about 2,500 enemies, around an invulnerable expert for 30 seconds, `perf=detail` and `profile=yes`; only with `--full --crowd` | progress toward much larger crowds | no, reported only |

The stress test and late fight take about 20 seconds together, since their frames run uncapped; the tenfold crowd about half a minute. At the designer's choice, the tenfold crowd runs only when asked for (`--crowd`), when performance work is under way, since it can't fail the check and was 40% of `--full`'s time.

## What it has found

- **Printing:** the first stress measurement met the frame budget easily, but every level-up was a hitch of 11 to 16 ms, all of it game logic. A single print took 10 to 13 ms with the output going to a console, and the harness printed a line at each level-up, while the detectors printed a `REVIEW:` line each time one fired, in the designer's fights in the editor too. Now detectors print only in scenario and batch runs (`RunReview.PrintMoments`), and the harness holds its lines while measuring. CLAUDE.md now says to avoid printing during play.
- **Asteroids never let go:** the late fight failed the logic budget, its slowest 1% at 6.2 ms. Profiling showed harvestables at 4.2 ms of it, against 0.5 ms for 265 enemies: every asteroid ever placed was moved, indexed and drawn every frame, and the arena only grows as the VNP explores, so a fight got slower the farther it had travelled. Now only asteroids and debris within `ActiveRadius` of the VNP (in `asteroids.tres`) are kept up to date, found and drawn; the rest wait, checked a slice at a time over `ActivationFrames`. The radius is well beyond anything that looks at material, so fights don't change: a 5-minute Rich fight with shard harvesting, 600 units out, traced identically with every asteroid kept active (variant `all_asteroids_active`). Harvestables fell to 0.1 ms, the same 1,300 units from the gateway as 400.
- **Where the time goes now,** in the late fight: enemies about half of game logic, harvestables about a fifth, the pilot's decision frames the largest spikes, and everything else together a few hundredths of a millisecond each. Render CPU and GPU take about 0.1 ms each.
- **Enemies scale steadily:** about 2.2 µs each, with no hidden squared cost, from 160 enemies to 2,500. At 2,500, that is 5.5 ms, over the logic budget on its own.
- **The pilot's decisions grow with the crowd:** its slowest 1% is under 1 ms in a late fight and about 2.6 to 4.2 ms in the tenfold crowd.
- **Goos:** in phase 3's stress test, twelve groups of Goos absorbing and splitting on 80 extra drones, beside basic enemies, kept game logic under 1 ms a frame on average.

## Not yet decided

The designer has not settled these. Use the behavior listed, say that you did, and keep it easy to change.

- **The tenfold crowd as a requirement:** reported only, until crowds actually grow toward it. When it becomes a requirement, whether it meets the same 120 frames a second or a looser target for extreme crowds.
- **The enemy update, for larger crowds:** nothing yet. The options are cheaper updates for distant enemies (less often, or simpler), a faster data layout, and spreading the work across processor cores. To be chosen when the tenfold crowd becomes a requirement.
- **The pilot's cost:** most of the first pilot's slowest decisions in a crowd were its harvest choice, weighing every rock against every enemy. Weighing danger from the best rock down, and stopping once none left could win, took its slowest 1% in the tenfold crowd from about 7 ms to 0.7 ms with its fights unchanged. The planning pilot (`docs/dev/PILOT_SPEC.md`) bounds its cost by its attention: about 0.2 ms in the late fight and 1.65 ms in the tenfold crowd. With several pilots flying at once for rival and escort VNPs, that tenfold figure is the one to watch.
- **Two unexplained frames:** in free-running late fights, a frame or two of about 10 ms, with game logic, render CPU and GPU all under 1 ms, so the time went elsewhere, perhaps the operating system or .NET's garbage collector. Within the requirement, so not chased yet. Seen larger during phase 4's stage 6: two `--full` runs in a row each failed on a few such frames, one of 121 ms in the late fight and four of 17 to 32 ms in the stress test, each with the game's own work under 1 ms (logic, render CPU and GPU), at moments that didn't match anything in the fight; a third run, with no change, passed cleanly. Over the five runs of that stage, three failed this way, each time on a burst of up to four frames in a row, at a different moment and in a different test, while the same fight measured again showed nothing there. If they keep failing checks, the next step is finding what else is running on the machine at those moments.
- **Typical players' machines:** the requirement is set on the designer's machine, with headroom for weaker ones. No minimum machine has been chosen to measure on.
- **Not measured yet:** GPU-heavy scenes, such as many large effects at once; memory over a long fight; loading time. Each gets a test if a phase or playtest suggests it matters.
- **The designer's frame times:** the render timing in the designer's fight logs hasn't been checked on a real designer fight yet.

For design questions this file does not answer, ask the designer rather than inventing a rule.

## Keeping it out of builds

Every measuring tool is in `scripts/cheats/`, inside `#if TOOLS`, under CLAUDE.md's rule for playtest cheats. The asteroid change is game code: it is how the game works now, in exports too.

## Adding to it

- **A new phase:** its spec's Performance section names what to measure. If the stress test or late fight doesn't cover it, add a test here and to the stage check.
- **A new system with per-frame cost:** give it its own update priority in `UpdateOrder` and an entry in the harness's profile parts, so `profile=yes` shows it.
- **A compile symbol of its own:** none yet, since every measuring tool is inside `scripts/cheats/` and nothing outside it calls into them. Add a `PERF` symbol, separate from `TOOLS` and `RECORDING`, the first time measuring needs a hook on a game class (such as a counter inside the enemy update), or for a build sent to testers to measure on typical machines (see Typical players' machines, under Not yet decided). That build would also need the measuring code moved out of `scripts/cheats/`, which exports leave out, and a way to show or save results without the harness.
- **A slow frame:** start with `perf=detail` to see whether it is logic, render CPU or GPU, then `profile=yes` for logic, and `perfstart=` to reach the moment quickly.
