"""
Compares a batch's pilot fights with the designer's own fights at the same fight times, as calibration needs:
health, drones, kills and level at each minute, and how many are still alive. Comparing per minute over whole
fights misleads, since fights that last longer reach the fastest part of endless mode (see the run review spec).

    python tools/compare_to_designer.py playtests/batches/<batch folder>

Prints a table, then one number, the gap: the average difference from the designer across those measures and
times, each as a fraction of the designer's value. Smaller is closer. Calibration tries to bring it down.

Uses every designer fight log that has a timeline, in playtests/human/ (each fight in its own folder, or loose
from before). Editor-only testing tool: nothing here is part of the game.
"""
import glob
import json
import os
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TIMES = [60, 120, 180, 240, 300]
MEASURES = ['health', 'drones', 'kills', 'level']


def at(log, t):
    """The timeline sample at fight time t, or None if the fight had ended."""
    if log['outcome']['time'] < t:
        return None
    for sample in log['timeline']:
        if sample['time'] >= t:
            return sample
    return None


def profile(logs):
    """For each time: how many fights were alive, and the median of each measure among those alive."""
    rows = {}
    for t in TIMES:
        alive = [s for s in (at(l, t) for l in logs) if s is not None and s['health'] > 0]
        rows[t] = {'alive': len(alive) / len(logs) if logs else 0}
        for m in MEASURES:
            rows[t][m] = statistics.median(s[m] for s in alive) if alive else None
    return rows


def designer_logs():
    folder = os.path.join(ROOT, 'playtests', 'human')
    paths = glob.glob(os.path.join(folder, '*.json')) + glob.glob(os.path.join(folder, '*', '*.json'))
    logs = []
    for p in sorted(paths):
        log = json.load(open(p, encoding='utf-8'))
        # Very short fights, such as one restarted at once, say nothing about how the designer plays.
        if log.get('timeline') and log['outcome']['time'] > 60:
            logs.append(log)
    return logs


def gap(pilot, designer):
    """The average relative difference across every measure and time both have, plus being alive."""
    diffs = []
    for t in TIMES:
        d, p = designer[t], pilot[t]
        diffs.append(abs(p['alive'] - d['alive']))
        for m in MEASURES:
            if d[m] is not None and p[m] is not None and d[m] > 0:
                diffs.append(abs(p[m] - d[m]) / d[m])
    return sum(diffs) / len(diffs) if diffs else None


def main():
    if len(sys.argv) != 2:
        sys.exit('Usage: python tools/compare_to_designer.py <batch folder>')
    pilots = [json.load(open(p, encoding='utf-8')) for p in glob.glob(os.path.join(sys.argv[1], 'logs', '*.json'))]
    human = designer_logs()
    if not pilots or not human:
        sys.exit(f'Need pilot fights and designer fights: found {len(pilots)} and {len(human)}.')
    p, d = profile(pilots), profile(human)
    print(f'{len(pilots)} pilot fights against {len(human)} designer fights. Medians among those still alive.')
    print(f'{"time":>5} | {"alive":>13} | ' + ' | '.join(f'{m:>13}' for m in MEASURES))
    print(f'{"":>5} | {"pilot  design":>13} | ' + ' | '.join(f'{"pilot  design":>13}' for _ in MEASURES))
    fmt = lambda v: '   -' if v is None else f'{v:4.0f}'
    for t in TIMES:
        cells = [f'{p[t]["alive"] * 100:4.0f}%  {d[t]["alive"] * 100:4.0f}%'] + \
                [f'{fmt(p[t][m])}    {fmt(d[t][m])}' for m in MEASURES]
        print(f'{t // 60}:00 | ' + ' | '.join(f'{c:>13}' for c in cells))
    g = gap(p, d)
    print(f'GAP: {g:.3f}' if g is not None else 'GAP: -')


if __name__ == '__main__':
    main()
