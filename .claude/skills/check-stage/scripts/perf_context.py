"""Explains a performance test's slow frames, for the stage check.

    python perf_context.py TEST_NAME PERF_OUTPUT PROCESS_SAMPLES HISTORY

PERF_OUTPUT is the harness's output from one performance test, with its "PERF SLOW:" lines (each slow frame's
clock time, its parts and its garbage collection). PROCESS_SAMPLES is what sample_processes.ps1 wrote meanwhile:
which other programs were busy, every half second. For each slow frame it prints the busiest programs in the
samples just around it, then a verdict on the likely cause. If the requirement wasn't met, it adds one line to
HISTORY, so failures can be compared across runs.
"""
import sys
from datetime import datetime, timedelta

WINDOW = timedelta(seconds=0.75)  # samples this close to a slow frame count as "around" it
GAME = ("godot",)                 # the game itself, shown but not counted as something else running


def parse_time(text):
    return datetime.strptime(text, "%Y-%m-%dT%H:%M:%S.%f")


def main():
    if len(sys.argv) != 5:
        sys.exit(__doc__)
    name, perf_path, samples_path, history_path = sys.argv[1:]
    perf = open(perf_path, encoding="utf-8", errors="replace").read().splitlines()
    met = any(line.startswith("PERF: requirement MET") for line in perf)

    slow = []
    for line in perf:
        if line.startswith("PERF SLOW: ") and " fight " in line:
            rest = line[len("PERF SLOW: "):]
            stamp, detail = rest.split(" ", 1)
            try:
                slow.append((parse_time(stamp), detail))
            except ValueError:
                pass

    samples = []
    try:
        for line in open(samples_path, encoding="utf-8", errors="replace"):
            parts = line.strip().split(" ", 2)
            if len(parts) == 3:
                try:
                    samples.append((parse_time(parts[0]), float(parts[1]), parts[2]))
                except ValueError:
                    pass
    except FileNotFoundError:
        pass

    gc_frames = 0
    other_busy = 0
    top_others = {}
    for when, detail in slow:
        near = [s for s in samples if abs(s[0] - when) <= WINDOW]
        busiest = {}
        for _, share, proc in near:
            busiest[proc] = max(busiest.get(proc, 0), share)
        ranked = sorted(busiest.items(), key=lambda kv: -kv[1])[:5]
        others = [(p, s) for p, s in ranked if not p.lower().startswith(GAME)]
        if "no garbage collection" not in detail:
            gc_frames += 1
        if others and others[0][1] >= 3:
            other_busy += 1
            top_others[others[0][0]] = top_others.get(others[0][0], 0) + 1
        shown = ", ".join(f"{p} {s:.1f}%" for p, s in ranked) if ranked else "no samples"
        print(f"PERF CONTEXT: {when:%H:%M:%S.%f}"[:-3] + f" {detail}; busy then: {shown}")

    if slow:
        if gc_frames == len(slow):
            verdict = "every slow frame had a garbage collection: the game's own allocations"
        elif other_busy == len(slow):
            verdict = "another program was busy at every slow frame: likely the machine, not the game"
        elif gc_frames == 0 and other_busy == 0:
            verdict = "no garbage collection and nothing else busy: cause still unknown"
        else:
            verdict = (f"mixed: {gc_frames} of {len(slow)} slow frames had a garbage collection, "
                       f"{other_busy} had another program busy")
        print(f"PERF CONTEXT: {len(slow)} slow frame(s); {verdict}"
              + (f" (most often: {max(top_others, key=top_others.get)})" if top_others else ""))
        if not samples:
            print("PERF CONTEXT: no process samples were taken, so other programs can't be judged")

    if not met:
        worst = max((float(d.split(" ms", 1)[0].split()[-1]) for _, d in slow), default=0)
        entry = (f"{datetime.now():%Y-%m-%d %H:%M} {name}: {len(slow)} slow frame(s), worst {worst:.1f} ms; "
                 f"{gc_frames} with garbage collection, {other_busy} with another program busy"
                 + (f" (most often {max(top_others, key=top_others.get)})" if top_others else ""))
        with open(history_path, "a", encoding="utf-8", newline="") as f:
            f.write(entry + "\n")
        print(f"PERF CONTEXT: added to {history_path}")


if __name__ == "__main__":
    main()
