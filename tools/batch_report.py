"""
Writes a batch's report, report.md, from its fight logs. tools/batch.py runs this at the end of every batch; run
it yourself to write a report again, for example after improving this script:

    python tools/batch_report.py playtests/batches/<batch folder>

The report covers outcomes, the level curve, losses, endless survival, matchups between weapons and enemy types,
upgrades, and performance, each by combination of pilot, build and starting weapons where that matters.

Editor-only testing tool: nothing here is part of the game.
"""
import json
import os
import statistics
import sys


def load(folder):
    batch = json.load(open(os.path.join(folder, 'batch.json'), encoding='utf-8'))
    logs = {}
    for fight in batch['fights']:
        path = os.path.join(folder, 'logs', fight['name'] + '.json')
        if os.path.exists(path):
            logs[fight['name']] = json.load(open(path, encoding='utf-8'))
    return batch, logs


def clock(seconds):
    if seconds is None:
        return '-'
    seconds = int(round(seconds))
    return f'{seconds // 60}:{seconds % 60:02d}'


def mean(values):
    values = [v for v in values if v is not None]
    return statistics.mean(values) if values else None


def median(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def num(value, digits=1):
    return '-' if value is None else f'{value:.{digits}f}'


def pct(part, whole):
    return '-' if not whole else f'{100 * part / whole:.0f}%'


def table(headers, rows):
    lines = ['| ' + ' | '.join(headers) + ' |', '|' + '|'.join(['---'] * len(headers)) + '|']
    lines += ['| ' + ' | '.join(str(c) for c in row) + ' |' for row in rows]
    return '\n'.join(lines)


def at(log, seconds):
    """The timeline sample at a fight time, or None if the fight ended before it."""
    for sample in log['timeline']:
        if sample['time'] >= seconds:
            return sample
    return None


def combo_label(fight, show_variant=False):
    parts = [fight['pilot'], fight['build']]
    if fight['weapon'] != 'default':
        parts.append(fight['weapon'])
    if fight['droneWeapon'] != 'default':
        parts.append(fight['droneWeapon'] + ' drones')
    label = ' / '.join(parts)
    return f'[{fight.get("variant", "base")}] {label}' if show_variant else label


def write_report(folder):
    batch, logs = load(folder)
    fights = batch['fights']
    variants = batch.get('variants', ['base'])
    several_variants = len(variants) > 1
    by_combo = {}
    for fight in fights:
        by_combo.setdefault(combo_label(fight, several_variants), []).append(fight)
    limit = None
    out = []

    def logs_of(group):
        return [logs[f['name']] for f in group if f['name'] in logs]

    # ───────────── Header ─────────────
    all_logs = list(logs.values())
    versions = sorted({l['settings']['gitVersion'] for l in all_logs})
    fingerprints = sorted({l['settings']['tuningFingerprint'] for l in all_logs})
    if all_logs:
        limit = all_logs[0]['outcome'].get('killTarget')
    ok = sum(1 for f in fights if f['status'] == 'ok')
    out.append(f'# Batch {batch["id"]}')
    out.append('')
    out.append(f'- **Fights:** {len(fights)} ({ok} ok), {len(by_combo)} combination(s) x {batch["seeds"]} seed(s), '
               f'{"rendered" if batch["rendered"] else "headless"}, {batch["atOnce"]} at once, '
               f'took {batch["seconds"] / 60:.1f} minutes.')
    out.append(f'- **Endless cap:** {clock(batch["cap"])} of fight time; a fight still going then counts as '
               f'having survived to the cap.')
    out.append(f'- **Game version:** {", ".join(versions) or "-"}. **Tuning fingerprint:** {", ".join(fingerprints) or "-"}.')
    if several_variants:
        out.append(f'- **Tuning variants compared:** {", ".join(variants)} (from tools/variants/; base is the tuning '
                   f'as it is). Each tuning fingerprint stands for one variant.')
    elif len(fingerprints) > 1:
        out.append('- **Warning:** these fights used different tuning, so their numbers are not comparable.')
    out.append('')
    out.append('Times are fight time. The on-screen clock counts down from the time limit, so fight time 1:30 is '
               'clock 3:30.')

    # ───────────── Comparison of variants ─────────────
    if several_variants:
        out.append('\n## Comparison of tuning variants\n')
        out.append(f'The same combinations on the same seeds, with each tuning variant. Differences are against '
                   f'{variants[0]}. With few fights, small differences can be chance: a difference of a few wins in '
                   f'{batch["seeds"]} fights is within chance.\n')
        groups = {}
        for fight in fights:
            groups.setdefault(combo_label(fight), {}).setdefault(fight.get('variant', 'base'), []).append(fight)

        def summary(group):
            ls = logs_of(group)
            wins = [l for l in ls if l['outcome']['killTargetTime'] is not None]
            at3 = [s['level'] for s in (at(l, 180) for l in ls) if s is not None and s['health'] > 0]
            return {
                'fights': len(ls),
                'win rate': 100 * len(wins) / len(ls) if ls else None,
                'time to win': median([l['outcome']['killTargetTime'] for l in wins]),
                'level at 3:00': mean(at3),
                'alive at 5:00': sum(1 for l in ls if (s := at(l, 300)) is not None and s['health'] > 0),
                'final level': mean([l['outcome']['level'] for l in ls]),
                'time destroyed': median([l['outcome']['time'] for l in ls if l['outcome']['result'] == 'loss']),
            }

        def diff(value, base, fmt):
            if value is None or base is None:
                return ''
            delta = value - base
            return '' if abs(delta) < 1e-9 else f' ({"+" if delta > 0 else "-"}{fmt(abs(delta))})'

        rows = []
        for label, by_variant in groups.items():
            base = summary(by_variant.get(variants[0], []))
            for v in variants:
                s = summary(by_variant.get(v, []))
                rows.append([label, v, s['fights'],
                             f'{num(s["win rate"], 0)}%' + (diff(s['win rate'], base['win rate'], lambda d: f'{d:.0f}') if v != variants[0] else ''),
                             clock(s['time to win']) + (diff(s['time to win'], base['time to win'], clock) if v != variants[0] else ''),
                             num(s['level at 3:00']) + (diff(s['level at 3:00'], base['level at 3:00'], lambda d: f'{d:.1f}') if v != variants[0] else ''),
                             f'{s["alive at 5:00"]} of {s["fights"]}',
                             num(s['final level']) + (diff(s['final level'], base['final level'], lambda d: f'{d:.1f}') if v != variants[0] else ''),
                             clock(s['time destroyed'])])
        out.append(table(['Combination', 'Variant', 'Fights', 'Win rate', 'Median time to win', 'Level at 3:00',
                          'Alive at 5:00', 'Final level', 'Median time destroyed'], rows))

    # ───────────── Outcomes ─────────────
    out.append('\n## Outcomes\n')
    out.append('A win is reaching the kill target; the fight then carries on into endless mode until the VNP is '
               'destroyed or the cap is reached.\n')
    rows = []
    for label, group in by_combo.items():
        ls = logs_of(group)
        wins = [l for l in ls if l['outcome']['killTargetTime'] is not None]
        lost_before_win = [l for l in ls if l['outcome']['killTargetTime'] is None and l['outcome']['result'] == 'loss']
        capped = [l for l in ls if l['outcome']['result'] == 'capped']
        rows.append([label, len(ls), f'{len(wins)} ({pct(len(wins), len(ls))})',
                     clock(median([l['outcome']['killTargetTime'] for l in wins])),
                     len(lost_before_win), clock(median([l['outcome']['time'] for l in lost_before_win])),
                     num(mean([l['outcome']['level'] for l in ls])), len(capped)])
    out.append(table(['Combination', 'Fights', 'Wins', 'Median time to win', 'Destroyed before winning',
                      'Median time destroyed', 'Average final level', 'Survived to cap'], rows))

    # ───────────── By starting weapons ─────────────
    randomised = [f for f in fights if f['weapon'] == 'random' or f['droneWeapon'] == 'random']
    if randomised:
        out.append('\n## By starting weapons\n')
        out.append('Fights that started with random weapons, by what they started with. Each row adds together '
                   'every pilot, build and variant in the batch, and with few fights per row, small differences '
                   'can be chance.\n')
        for key, title in (('startWeapon', 'VNP Weapon'), ('startDroneWeapon', 'Drone Weapon')):
            groups = {}
            for f in randomised:
                if f['name'] in logs:
                    groups.setdefault(f.get(key, '-'), []).append(logs[f['name']])
            rows = []
            for start, ls in sorted(groups.items(), key=lambda kv: -len(kv[1])):
                wins = [l for l in ls if l['outcome']['killTargetTime'] is not None]
                alive5 = sum(1 for l in ls if (s := at(l, 300)) is not None and s['health'] > 0)
                rows.append([start, len(ls), f'{len(wins)} ({pct(len(wins), len(ls))})',
                             clock(median([l['outcome']['killTargetTime'] for l in wins])),
                             num(mean([l['outcome']['level'] for l in ls])), f'{alive5} of {len(ls)}'])
            out.append(table([f'Starting {title}', 'Fights', 'Wins', 'Median time to win', 'Average final level',
                              'Alive at 5:00'], rows))
            out.append('')

    # ───────────── Level curve ─────────────
    out.append('\n## Level curve\n')
    out.append('Average level reached by each minute of fight time, among fights still going then. The last '
               'column is how many fights reached 5:00 with the VNP alive.\n')
    marks = [60, 120, 180, 240, 300]
    rows = []
    for label, group in by_combo.items():
        ls = logs_of(group)
        row = [label]
        for m in marks:
            samples = [s for s in (at(l, m) for l in ls) if s is not None and s['health'] > 0]
            row.append(f'{num(mean([s["level"] for s in samples]))} ({len(samples)})' if samples else '-')
        alive_at_5 = sum(1 for l in ls if (s := at(l, 300)) is not None and s['health'] > 0)
        row.append(f'{alive_at_5} of {len(ls)}')
        rows.append(row)
    out.append(table(['Combination'] + [clock(m) for m in marks] + ['Alive at 5:00'], rows))
    out.append('\nThe number in brackets is how many fights were still going at that time.')

    # ───────────── Losses ─────────────
    out.append('\n## Losses\n')
    out.append('How the VNP was destroyed, and what damaged it, across every fight.\n')
    rows = []
    for label, group in by_combo.items():
        ls = logs_of(group)
        destroyed = [l for l in ls if l['outcome']['result'] == 'loss']
        taken = {}
        for l in ls:
            for enemy, amount in l['stats']['damageTaken'].items():
                taken[enemy] = taken.get(enemy, 0) + amount
        total = sum(taken.values())
        share = ', '.join(f'{e} {pct(a, total)}' for e, a in sorted(taken.items(), key=lambda x: -x[1]))
        rows.append([label, f'{len(destroyed)} of {len(ls)}', clock(median([l['outcome']['time'] for l in destroyed])),
                     share or '-'])
    out.append(table(['Combination', 'Destroyed', 'Median time destroyed', 'Damage taken from'], rows))

    # ───────────── Endless ─────────────
    out.append('\n## Endless\n')
    out.append('After winning, how long the VNP lasted in endless mode, until destroyed or the cap.\n')
    rows = []
    for label, group in by_combo.items():
        ls = [l for l in logs_of(group) if l['outcome']['killTargetTime'] is not None]
        survived = [l['outcome']['time'] - l['outcome']['killTargetTime'] for l in ls]
        reached_cap = sum(1 for l in ls if l['outcome']['result'] == 'capped')
        rows.append([label, len(ls), clock(median(survived)), clock(max(survived) if survived else None), reached_cap])
    out.append(table(['Combination', 'Wins', 'Median time after winning', 'Longest', 'Reached the cap'], rows))

    # ───────────── Matchups ─────────────
    out.append('\n## Matchups\n')
    out.append('Damage dealt by each source, and how it split between enemy types, across every fight. Only '
               'damage that took health off counts.\n')
    sources, enemy_types = {}, set()
    for l in all_logs:
        for d in l['stats']['damage']:
            entry = sources.setdefault(d['source'], {'kind': d['kind'], 'total': 0.0, 'by': {}, 'fights': 0})
            entry['total'] += d['total']
            entry['fights'] += 1
            for enemy, amount in d['byEnemy'].items():
                entry['by'][enemy] = entry['by'].get(enemy, 0) + amount
                enemy_types.add(enemy)
    enemy_types = sorted(enemy_types)
    grand = sum(s['total'] for s in sources.values())
    rows = []
    for source, entry in sorted(sources.items(), key=lambda x: -x[1]['total']):
        rows.append([source, 'VNP Weapon' if entry['kind'] == 'VnpWeapon' else 'Drone Weapon' if entry['kind'] == 'DroneWeapon' else entry['kind'],
                     entry['fights'], f'{entry["total"] / max(1, entry["fights"]):,.0f}', pct(entry['total'], grand)]
                    + [pct(entry['by'].get(e, 0), entry['total']) for e in enemy_types])
    out.append(table(['Source', 'Kind', 'Fights used', 'Damage per fight', 'Share of all damage']
                     + [f'vs {e}' for e in enemy_types], rows))
    lost = {}
    for l in all_logs:
        for kind in ('destroyed', 'absorbed'):
            for enemy, count in l['stats']['drones'][kind].items():
                lost.setdefault(enemy, {'destroyed': 0, 'absorbed': 0})[kind] += count
    if lost:
        out.append('\nDrones lost to each enemy type, per fight:\n')
        out.append(table(['Enemy type', 'Destroyed', 'Absorbed'],
                         [[e, num(c['destroyed'] / max(1, len(all_logs))), num(c['absorbed'] / max(1, len(all_logs)))]
                          for e, c in sorted(lost.items())]))

    # ───────────── Upgrades ─────────────
    out.append('\n## Upgrades\n')
    out.append('How often each upgrade was offered and taken, and the win rate of fights that took it against '
               'those that didn\'t. "Offered" counts fights where it appeared in any offer, rerolls included. With '
               'few fights, win rates swing a lot.\n')
    offered, taken = {}, {}
    for l in all_logs:
        seen = {u for lu in l['stats']['levelUps'] for offer in lu['offers'] for u in offer}
        took = set(l['outcome']['upgradesTaken'])
        for u in seen:
            offered[u] = offered.get(u, 0) + 1
        for u in took:
            taken[u] = taken.get(u, 0) + 1
    won = lambda l: l['outcome']['killTargetTime'] is not None
    rows = []
    for u in sorted(offered, key=lambda u: (-taken.get(u, 0), u)):
        with_it = [l for l in all_logs if u in l['outcome']['upgradesTaken']]
        without = [l for l in all_logs if u not in l['outcome']['upgradesTaken']]
        rows.append([u, offered[u], taken.get(u, 0), pct(taken.get(u, 0), offered[u]),
                     pct(sum(map(won, with_it)), len(with_it)), pct(sum(map(won, without)), len(without))])
    out.append(table(['Upgrade', 'Fights offered', 'Fights taken', 'Taken when offered', 'Win rate taken',
                      'Win rate not taken'], rows))

    # ───────────── Fights worth watching ─────────────
    out.append('\n## Fights worth watching\n')
    out.append('The best fight of each combination (the fastest win, or the longest-lasting if none won) and the '
               'worst (destroyed earliest). Each command replays the fight exactly in a window, from the vnp folder, '
               'as long as the game hasn\'t changed since this batch: the fight log of the replay records the game '
               'version, to compare with the one above. Add `explain=FROM-TO` (fight seconds) to see the pilot\'s '
               'reasoning over a stretch.\n')

    def watch_command(fight):
        parts = ['tools\\scenario.cmd -Window', f'pilot={fight["pilot"]}', f'build={fight["build"]}', f'seed={fight["seed"]}']
        start_weapon = fight.get('startWeapon', fight['weapon'])
        start_drone = fight.get('startDroneWeapon', fight['droneWeapon'])
        if start_weapon != 'default':
            parts.append(f'weapon={start_weapon}')
        if start_drone != 'default':
            parts.append(f'drone_weapon={start_drone}')
        if fight.get('variant', 'base') != 'base':
            parts.append(f'variant={fight["variant"]}')
        parts.append(f'maxtime={batch["cap"]:g}')
        parts.append(f'frames={int(batch["cap"] * 60 * 3)}')
        return '`' + ' '.join(parts) + '`'

    def describe(log):
        o = log['outcome']
        won = f'won at {clock(o["killTargetTime"])}, ' if o['killTargetTime'] is not None else ''
        end = 'survived to the cap' if o['result'] == 'capped' else f'destroyed at {clock(o["time"])}'
        return f'{won}{end}, level {o["level"]}, {o["kills"]} kills'

    rows = []
    for label, group in by_combo.items():
        pairs = [(f, logs[f['name']]) for f in group if f['name'] in logs]
        if not pairs:
            continue
        # Best: a win beats no win, then the sooner the better; with no win, lasting longer is better.
        def goodness(pair):
            o = pair[1]['outcome']
            return (1, -o['killTargetTime']) if o['killTargetTime'] is not None else (0, o['time'])
        best = max(pairs, key=goodness)
        worst = min(pairs, key=lambda pair: (pair[1]['outcome']['result'] != 'loss', pair[1]['outcome']['time']))
        rows.append([label, 'best', f'seed {best[0]["seed"]}: {describe(best[1])}', watch_command(best[0])])
        if worst[0]['name'] != best[0]['name']:
            rows.append([label, 'worst', f'seed {worst[0]["seed"]}: {describe(worst[1])}', watch_command(worst[0])])
    out.append(table(['Combination', '', 'Fight', 'Watch it'], rows))

    # ───────────── Performance ─────────────
    out.append('\n## Performance\n')
    out.append('Game logic per frame, from the fight logs: drawing is not included. A frame has 16.7 ms at 60 frames '
               'a second.\n')
    rows = []
    for label, group in by_combo.items():
        ls = logs_of(group)
        frames = [l['frames'] for l in ls if l.get('frames')]
        rows.append([label, num(mean([f['averageMs'] for f in frames]), 2), num(max((f['p95Ms'] for f in frames), default=None), 2),
                     num(max((f['worstMs'] for f in frames), default=None), 2),
                     max((f['mostEnemies'] for f in frames), default='-')])
    out.append(table(['Combination', 'Average ms', 'Worst 95th percentile ms', 'Worst frame ms', 'Most enemies at once'], rows))

    # ───────────── Problems ─────────────
    failed = [f for f in fights if f['status'] != 'ok']
    out.append('\n## Problems\n')
    if not failed:
        out.append('None: every fight finished without errors and wrote its log.')
    else:
        out.append(table(['Fight', 'Status', 'Exit code', 'First problem'],
                         [[f['name'], f['status'], f['exitCode'], (f['problems'] or [''])[0][:120]] for f in failed]))
        out.append('\nEach fight\'s console output is in output/.')

    path = os.path.join(folder, 'report.md')
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join(out) + '\n')
    # The same report as a page with charts, report.html, beside it.
    import batch_report_html
    batch_report_html.write_page(folder)
    return path


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit('Usage: python tools/batch_report.py <batch folder>')
    print(write_report(sys.argv[1]))
