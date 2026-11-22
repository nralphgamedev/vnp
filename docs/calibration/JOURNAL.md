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

### 2026-11-22: baseline on the designer's current tuning

The designer played three fights on the current tuning: Seeker Missiles with Ram, Orbital Shards with Projectile, and Shockwave with Beam. The Designer build was refitted to 13 logs (238 choices).

- **Fingerprint fix:** the tuning fingerprint covered the pilots' own files, so refitting the Designer build made the designer's fights look as if they were on other tuning. It now leaves out `pilots/` and every `pilot_profile.tres`. The three fights' fingerprints were taken the old way and their logs aren't rewritten; the game's files are unchanged since they were played (only `pilots/builds/designer.tres` differs), so they are picked by time with `compare_to_designer.py --since=2026-11-22_01-40`. Fights from now on can use `--same-tuning`.
- **Baseline:** 40 expert fights against those three: GAP 0.171. Health and level match to 3:00 (pilots a level ahead at 3:00). Pilots hold fewer drones from 3:00 (18 against 30), make fewer kills (110 against 130 at 3:00), and fewer are alive at 4:00 (78% against all three). At 4:00 both are low on drones (2 against 5). Three designer fights is a small target: treat differences under about a tenth as chance.
- **Next:** calibration, aiming at drones and kills from 3:00 and survival at 4:00. Use `--since=2026-11-22_01-40` until the designer has played on the new fingerprint, then `--same-tuning`.

### 2026-11-22: chunk 1, fighting more readily (rejected)

`compare_to_designer.py` gained `--variant=NAME`, to score each variant of a comparison batch on its own. Candidate: the expert fights more readily (FightWeight 0.8 to 1.2, Caution 0.5 to 0.35), since the designer spends about twice as long fighting. 25 expert fights each, same seeds: base GAP 0.154, candidate 0.238. Its drones collapsed by 3:00 (3 against the designer's 30) and kills and survival fell. Rejected; the variant file was removed. Base on these seeds: 0.154 (0.171 on 40 seeds earlier, so differences of about 0.02 are chance). No improvement: 1 of 6. Next: the opposite direction, protecting drones from 3:00, such as more weight on drone strength in what it can afford (StrengthPerDrone) or backing off sooner when drones are falling.

### 2026-11-22: chunk 2, drones count for more in what it can afford (kept)

Candidate: the expert's StrengthPerDrone 0.08 to 0.14, so its drones count for more in what it can afford and losing them makes it back off sooner. 25 fights each: base 0.154, candidate 0.139, within chance; so confirmed on 50 seeds each (100 fights, about 2.3 minutes, longer than a chunk's usual batch, to tell a small gain from chance): base 0.165, candidate 0.135. Kills and level at 5:00 now close to the designer's (252 against 253, level 21 against 22); drones at 3:00 still short (21 against 30). Kept: `pilots/skill_levels/expert.tres` StrengthPerDrone 0.14. No-improvement count reset: 0 of 6. Next: drones from 3:00, such as harvesting more at the drone cap (HarvestAtCap) or the regroup thresholds, and survival at 4:00. New baseline about 0.135.

### 2026-11-22: chunks 3 and 4, more drone weight and earlier regrouping (both rejected)

Both tested on 50 seeds each against the base, which replays exactly (GAP 0.135, as in chunk 2).

- **Chunk 3, StrengthPerDrone 0.14 to 0.2:** GAP 0.125, a gain of 0.01, within chance, and fewer drones and kills at 5:00. Rejected; 0.14 stays.
- **Chunk 4, regrouping earlier (RegroupBelow 0.2 to 0.35, RegroupUntil 0.4 to 0.55):** GAP 0.147, worse. Rejected.

No improvement: 2 of 6. Drones at 3:00 remain the clearest gap (about 20 against 30). Next: what feeds drones rather than what protects them, such as harvesting more at the drone cap (HarvestAtCap) or a stronger pull toward concentrations (ConcentrationPull).

### 2026-11-22: chunks 5 and 6, feeding drones (both rejected)

The base replays exactly on the same seeds, so it isn't re-run: its 50-seed GAP is 0.135 (chunk 2). Two candidates in one batch, 50 seeds each:

- **Chunk 5, ConcentrationPull 1.2 to 1.6:** GAP 0.153, worse; drones at 3:00 fell to 14.
- **Chunk 6, HarvestAtCap 0.8 to 1.0:** GAP 0.184, worse; fewer kills and drones from 4:00.

No improvement: 4 of 6. Pulling the pilot toward material doesn't raise its drones; neither did fighting more (chunk 1). Next: the opposite of chunk 1, fighting a little less readily (Caution up, FightWeight down), and how fast pressure eases (PressureFall).

### 2026-11-22: chunks 7 and 8, fighting less and faster easing (both rejected)

Against the base's 50-seed GAP of 0.135:

- **Chunk 7, fighting a little less readily (Caution 0.5 to 0.65, FightWeight 0.8 to 0.6):** GAP 0.181, worse; drones at 3:00 collapsed to 6.
- **Chunk 8, pressure easing faster (PressureFall 0.3 to 0.6):** GAP 0.134, no real change.

### 2026-11-22: stopped, waiting for the designer

**Why:** the stopping rule, 6 chunks in a row with no improvement (chunks 3 to 8).

**Where it stands:** one change kept, the expert's StrengthPerDrone 0.08 to 0.14 (chunk 2), taking the GAP from 0.165 to 0.135 on 50 seeds. Against the designer's three fights on the current tuning, the expert now matches health and level throughout, and kills at 5:00 (252 against 253). The clearest remaining difference is drones at 3:00: about 21 against the designer's 30.

**What the rejected chunks say:** fighting more readily (chunk 1) and less readily (chunk 7) both made drones collapse by 3:00, and pulling toward material (chunks 5, 6), regrouping earlier (chunk 4) and more drone weight (chunk 3) didn't raise them. The expert's settings look close to as good as these behaviours allow: the drone gap probably comes from something the pilot doesn't do at all, rather than a setting.

**Suggested next steps for the designer to choose from:**
- Look at the designer's own fights between 2:30 and 3:30 with the run review's maps (overview and strips) to see how they keep their drones, then add that as a behaviour.
- More designer fights on the current tuning: three is a small target, and the drone gap at 3:00 rests on them.
- Merge this branch's kept change into `main`.

### 2026-11-22: unattended settings search (one change kept)

`tools/calibrate_pilot.py --since=2026-11-22_01-40 --passes 3` ran on its own for about 2 hours 10 minutes. It searched 17 of the expert's settings, each raised and lowered, on seeds 1 to 50. A change was kept only if it beat the current gap by 0.01 there and also won on seeds 101 to 150. The full log is `playtests/calibration/2026-11-22_03-25-37.md`.

- **Kept:** HarvestAtCap 0.8 to 0.9, harvesting a little more at the drone cap. GAP 0.135 to 0.120 on the search seeds, and 0.189 to 0.179 on the confirmation seeds. (Chunk 6's 1.0 was too far.)
- **Nothing else** beat 0.120 in pass 1, in pass 2 at the same step, or in pass 3 at half the step. The nearest were FightWeight 1.0 (0.115, a gain under 0.01) and PreferredEnemyDistance and Caution 0.55 (both 0.120, no change). The search ended after its 3 passes.
- **The same pilot scores 0.120 on one seed set and 0.179 on the other.** With only three designer fights to aim at, that spread is larger than any single setting's effect.

### 2026-11-22: chunk 9, no regrouping (rejected)

The designer's Shockwave fight, between 2:30 and 3:30, shows them staying in one area near the fighting and rebuilding drones there (2 to 10 to 18) without regrouping. Candidate: the expert never regroups (`tools/variants/no_regroup.txt`, RegroupBelow 0). 50 seeds on each set:

- **Search seeds:** base 0.120, no regroup 0.153. That's worse, though it held more drones at 4:00 (8 against 5).
- **Confirmation seeds:** base 0.179, no regroup 0.177, no real change. Fewer were alive at 4:00 (66% against 74%).

Rejected. Not regrouping on its own doesn't recover the drones at 3:00 (18 to 20 against the designer's 30). What the designer does there is more than staying put: they weave close beside enemies while their drones rebuild on the spot. The pilot has nothing that does that yet. No improvement since the search: 1 of 6.

### 2026-11-22: stopped, waiting for the designer

**Why:** the settings are searched out. Three full passes of the automatic search kept one change, and the one behaviour hypothesis that settings can express (no regrouping) failed. What remains needs a new pilot behaviour or more designer fights, and the designer should choose which.

**Where it stands:** the expert now has StrengthPerDrone 0.14 and HarvestAtCap 0.9. Its GAP against the designer's three current fights is 0.120 on seeds 1 to 50, down from 0.165 at the start. It matches health, level and alive fraction to 3:00. The remaining differences are drones at 3:00 (about 19 against 30) and kills (about 110 against 130).

**Suggested next steps:**
- A new behaviour, building drones while fighting: with few drones, keep close to the fight and harvest debris and rocks beside it, instead of leaving or just holding back. This is pilot code, allowed on this branch, but it is a design judgement worth agreeing first.
- More designer fights on the current tuning (now matched with `--same-tuning`). Three fights make seed luck larger than any setting's effect.
- Merge the two kept settings into `main`.
