"""
Compares a batch's pilot fights with the designer's own fights at the same fight times, as calibration needs:
health, drones, kills and level at each minute, and how many are still alive. Comparing per minute over whole
fights misleads, since fights that last longer reach the fastest part of endless mode (see the run review spec).

    python tools/compare_to_designer.py playtests/batches/<batch folder>              all the designer's fights
    python tools/compare_to_designer.py playtests/batches/<batch folder> --same-tuning  only those on the batch's tuning
    ... --variant=NAME             only the batch's fights with that tuning variant ("base" for none), to score
                                   each variant of a comparison batch on its own
    ... --since=2026-11-22_01-40   only the designer's fights started at or after that time (from their names), for
                                   fights whose fingerprint was taken before pilots' files were left out of it

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


def designer_logs(fingerprints=None, since=None):
    folder = os.path.join(ROOT, 'playtests', 'human')
    paths = glob.glob(os.path.join(folder, '*.json')) + glob.glob(os.path.join(folder, '*', '*.json'))
    logs = []
    for p in sorted(paths):
        if since and os.path.basename(p) < since:
            continue
        log = json.load(open(p, encoding='utf-8'))
        # Very short fights, such as one restarted at once, say nothing about how the designer plays.
        if log.get('timeline') and log['outcome']['time'] > 60 and (
                fingerprints is None or log['settings'].get('tuningFingerprint') in fingerprints):
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
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if len(args) != 1:
        sys.exit('Usage: python tools/compare_to_designer.py <batch folder> [--same-tuning]')
    pilots = [json.load(open(p, encoding='utf-8')) for p in glob.glob(os.path.join(args[0], 'logs', '*.json'))]
    variant = next((a.split('=', 1)[1] for a in sys.argv if a.startswith('--variant=')), None)
    if variant is not None:
        pilots = [l for l in pilots if (l['settings'].get('tuningVariant') or 'base') == variant]
    # The batch's tuning fingerprints, without any variant's text folded in, for comparing on the same tuning.
    base = {l['settings']['tuningFingerprint'] for l in pilots if not l['settings'].get('tuningVariant')}
    since = next((a.split('=', 1)[1] for a in sys.argv if a.startswith('--since=')), None)
    human = designer_logs(base if '--same-tuning' in sys.argv else None, since)
    print('Designer fights:', ', '.join(f"{l['settings']['startingWeapons'][0]}/{l['settings']['startingDroneWeapon']}"
                                       for l in human))
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
