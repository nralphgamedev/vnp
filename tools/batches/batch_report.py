"""
Writes a batch's report, report.md, from its fight logs. tools/batches/batch.py runs this at the end of every batch; run
it yourself to write a report again, for example after improving this script:

    python tools/batches/batch_report.py playtests/batches/<batch folder>

The report covers outcomes, the level curve, losses, endless survival, matchups between weapons and enemy types,
upgrades, and performance, each by combination of pilot, build and starting weapons where that matters.

Editor-only testing tool: nothing here is part of the game.
"""
import glob
import json
import re
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
    if fight.get('plan', 'random') != 'random':
        parts.append(fight['plan'] + ' plan')
    label = ' / '.join(parts)
    return f'[{fight.get("variant", "base")}] {label}' if show_variant else label


def review_setting(name, default):
    """A value from the run review settings, scripts/cheats/playtesting/review_settings.tres."""
    path = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                        'scripts', 'cheats', 'playtesting', 'review_settings.tres')
    try:
        m = re.search(rf'^{name} = ([\d.]+)', open(path, encoding='utf-8').read(), re.M)
        return type(default)(float(m.group(1))) if m else default
    except OSError:
        return default


def choose_maps(batch, logs):
    """Which fights get an overview map, and which of their strips are drawn: for each combination its best,
    worst and a typical fight; for each detector a few strips from different fights. Returns
    {fight name: (draw its overview, set of strip indices)}."""
    show_variant = len(batch.get('variants', ['base'])) > 1
    by_combo = {}
    for fight in batch['fights']:
        if fight['name'] in logs and logs[fight['name']].get('maps'):
            by_combo.setdefault(combo_label(fight, show_variant), []).append(fight)
    chosen = {}
    per_combo = review_setting('OverviewsPerCombination', 3)
    for group in by_combo.values():
        for name, _ in overview_picks(group, logs)[:per_combo]:
            chosen.setdefault(name, [False, set()])[0] = True
    per_detector = review_setting('StripsPerDetector', 3)
    detector_ids = set()
    for log in logs.values():
        for strip in (log.get('maps') or {}).get('strips', []):
            detector_ids.add(strip['reason'])
    # Fights in turn from each combination, so a detector's strips come from different combinations rather than
    # all from the first. The first review's strips were all from novice fights.
    queues = list(by_combo.values())
    interleaved = [q[i] for i in range(max((len(q) for q in queues), default=0)) for q in queues if i < len(q)]
    for d in sorted(detector_ids):
        picked = 0
        for fight in interleaved:
            log = logs.get(fight['name'])
            if not log or not log.get('maps'):
                continue
            strips = log['maps']['strips']
            index = next((i for i, s in enumerate(strips) if s['reason'] == d), None)
            if index is None:
                continue
            chosen.setdefault(fight['name'], [False, set()])[1].add(index)
            picked += 1
            if picked >= per_detector:
                break
    return {name: (overview, strips) for name, (overview, strips) in chosen.items()}


def overview_picks(group, logs):
    """A combination's best, worst and a typical fight (median final level), as (name, why)."""
    pairs = [(f, logs[f['name']]) for f in group if f['name'] in logs]
    if not pairs:
        return []

    def goodness(pair):
        o = pair[1]['outcome']
        return (1, -o['killTargetTime']) if o['killTargetTime'] is not None else (0, o['time'])
    best = max(pairs, key=goodness)
    worst = min(pairs, key=lambda pair: (pair[1]['outcome']['result'] != 'loss', pair[1]['outcome']['time']))
    by_level = sorted(pairs, key=lambda pair: pair[1]['outcome']['level'])
    typical = by_level[len(by_level) // 2]
    picks = []
    for pair, why in ((best, 'best'), (worst, 'worst'), (typical, 'typical')):
        if pair[0]['name'] not in [p[0] for p in picks]:
            picks.append((pair[0]['name'], why))
    return picks


def map_file(folder, name, kind, strip=None):
    """The relative path of a drawn map, or None if it wasn't drawn."""
    if kind == 'overview':
        file = f'{name}_overview.png'
    else:
        index, s = strip
        file = f'{name}_strip{index}_{s["reason"]}_{int(s["time"])}s.png'
    return f'maps/{file}' if os.path.exists(os.path.join(folder, 'maps', file)) else None


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

    # ───────────── Pilots compared ─────────────
    # The pilot spec's two training targets side by side: the gap between them is how much better the game can be
    # played than the designer-like pilot plays it, and what wins far more often in the best hands shows where the
    # balance breaks.
    pilots = list(dict.fromkeys(f['pilot'] for f in fights))
    if len(pilots) > 1:
        first, last = pilots[0], pilots[-1]
        by_pilot = {pl: [logs[f['name']] for f in fights if f['pilot'] == pl and f['name'] in logs] for pl in pilots}

        def won(l):
            return l['outcome']['killTargetTime'] is not None

        def rate(ls):
            return 100 * sum(1 for l in ls if won(l)) / len(ls) if ls else None

        out.append('\n## Pilots compared\n')
        out.append(f'Each pilot over all its fights, every build and variant added together. The gap is {last} '
                   f'against {first}, in points of win rate.\n')
        rows = []
        for pl in pilots:
            ls = by_pilot[pl]
            rows.append([pl, len(ls), f'{num(rate(ls), 0)}%', clock(median([l['outcome']['killTargetTime'] for l in ls if won(l)])),
                         f'{sum(1 for l in ls if (s := at(l, 300)) is not None and s["health"] > 0)} of {len(ls)}',
                         num(mean([l['outcome']['level'] for l in ls]))])
        out.append(table(['Pilot', 'Fights', 'Win rate', 'Median time to win', 'Alive at 5:00', 'Average final level'], rows))

        weapons = sorted({l['settings']['startingWeapons'][0] for ls in by_pilot.values() for l in ls if l['settings'].get('startingWeapons')})
        if len(weapons) > 1:
            out.append(f'\n**By starting VNP Weapon,** win rate for each pilot, widest gap first. A weapon whose gap '
                       f'is far above the others is one the better pilot gets much more out of.\n')
            rows = []
            for w in weapons:
                rates = {pl: rate([l for l in by_pilot[pl] if l['settings']['startingWeapons'][0] == w]) for pl in pilots}
                n = {pl: sum(1 for l in by_pilot[pl] if l['settings']['startingWeapons'][0] == w) for pl in pilots}
                gap = rates[last] - rates[first] if rates[last] is not None and rates[first] is not None else None
                rows.append((gap if gap is not None else -999, [w] + [f'{num(rates[pl], 0)}% ({n[pl]})' for pl in pilots]
                             + [f'{gap:+.0f}' if gap is not None else '-']))
            rows.sort(key=lambda r: -r[0])
            out.append(table(['Starting weapon'] + pilots + ['Gap'], [r for _, r in rows]))

        # Upgrades: how much more often fights that took one were won than fights that didn't, for each pilot. The
        # lift growing with the better pilot points at an upgrade that's strong in skilled hands.
        min_taken = 15
        upgrade_rows = []
        ids = sorted({u for ls in by_pilot.values() for l in ls for u in l['outcome'].get('upgradesTaken', [])})
        for u in ids:
            lifts, cells = {}, []
            for pl in pilots:
                with_u = [l for l in by_pilot[pl] if u in l['outcome'].get('upgradesTaken', [])]
                without = [l for l in by_pilot[pl] if u not in l['outcome'].get('upgradesTaken', [])]
                if len(with_u) >= min_taken and len(without) >= min_taken:
                    lifts[pl] = rate(with_u) - rate(without)
                    cells.append(f'{lifts[pl]:+.0f} ({len(with_u)})')
                else:
                    cells.append('-')
            if first in lifts and last in lifts:
                upgrade_rows.append((lifts[last] - lifts[first], [u] + cells + [f'{lifts[last] - lifts[first]:+.0f}']))
        if upgrade_rows:
            upgrade_rows.sort(key=lambda r: -r[0])
            out.append(f'\n**Upgrades,** by lift: how many points more often fights that took the upgrade were won '
                       f'than fights that didn\'t, for each pilot (in brackets, fights that took it; only upgrades '
                       f'taken and not taken in at least {min_taken} fights each). The difference is {last}\'s lift '
                       f'against {first}\'s; the top of the list is what wins more in better hands, the bottom what '
                       f'the better pilot needs less. Lift is a correlation, not a cause: an upgrade taken late is '
                       f'taken by fights that lasted.\n')
            shown = upgrade_rows[:8] + ([(0, ['...'] + [''] * (len(pilots) + 1))] if len(upgrade_rows) > 12 else []) \
                + (upgrade_rows[-4:] if len(upgrade_rows) > 12 else upgrade_rows[8:])
            out.append(table(['Upgrade'] + [f'Lift, {pl}' for pl in pilots] + ['Difference'], [r for _, r in shown]))
        out.append('')

    # ───────────── By plan ─────────────
    # The Fight Director's plans (docs/dev/FIGHT_DIRECTOR_SPEC.md): every fight records the plan it followed, whether
    # picked by its seed or set by the batch, so plans can be compared and cut or tuned.
    plan_groups = {}
    for f in fights:
        if f['name'] in logs:
            plan = logs[f['name']]['settings'].get('fightPlan')
            if plan:
                plan_groups.setdefault(plan, []).append(logs[f['name']])
    if len(plan_groups) > 1:
        out.append('\n## By plan\n')
        out.append('Every fight by the Fight Director\'s plan it followed (director/plans/), adding together every pilot, '
                   'build, weapon and variant. Plans can have different kill targets, so compare time to win with care. '
                   'Drones are the average alive among fights still going then.\n')
        rows = []
        for plan, ls in sorted(plan_groups.items(), key=lambda kv: kv[0]):
            wins = [l for l in ls if l['outcome']['killTargetTime'] is not None]
            alive5 = sum(1 for l in ls if (s := at(l, 300)) is not None and s['health'] > 0)
            drones = []
            for m in (120, 240):
                xs = [s['drones'] for l in ls if (s := at(l, m)) is not None and s['health'] > 0]
                drones.append(num(mean(xs)) if xs else '-')
            enemies4 = [sum(s['enemies'].values()) for l in ls if (s := at(l, 240)) is not None]
            rows.append([plan, len(ls), ls[0]['outcome'].get('killTarget', '-'), f'{len(wins)} ({pct(len(wins), len(ls))})',
                         clock(median([l['outcome']['killTargetTime'] for l in wins])),
                         f'{alive5} of {len(ls)}', drones[0], drones[1],
                         num(mean(enemies4)) if enemies4 else '-', num(mean([l['outcome']['level'] for l in ls]))])
        out.append(table(['Plan', 'Fights', 'Kill target', 'Wins', 'Median time to win', 'Alive at 5:00',
                          'Drones at 2:00', 'Drones at 4:00', 'Enemies at 4:00', 'Average final level'], rows))
        out.append('')

    # ───────────── By asteroid amount ─────────────
    # Phase 3 asks how much the amount of asteroids changes a fight's difficulty. Every fight records its amount,
    # whether it was picked by its seed or set by the batch.
    amount_groups = {}
    for f in fights:
        if f['name'] in logs:
            amount = logs[f['name']]['settings'].get('asteroidAmount')
            if amount:
                amount_groups.setdefault(amount, []).append(logs[f['name']])
    if len(amount_groups) > 1:
        out.append('\n## By asteroid amount\n')
        out.append('Every fight by its asteroid amount, adding together every pilot, build, weapon and variant. The '
                   'amount scales how many asteroid fields and lone asteroids there are (tuning/asteroid_amounts/). '
                   'Experience from asteroids and drones built are medians over whole fights.\n')
        rows = []
        for amount, ls in sorted(amount_groups.items(), key=lambda kv: -len(kv[1])):
            wins = [l for l in ls if l['outcome']['killTargetTime'] is not None]
            alive5 = sum(1 for l in ls if (s := at(l, 300)) is not None and s['health'] > 0)
            level3 = [s['level'] for l in ls if (s := at(l, 180)) is not None]
            asteroid_xp = [l['stats'].get('experience', {}).get('asteroids') for l in ls]
            built = [l['stats'].get('drones', {}).get('built') for l in ls]
            rows.append([amount, len(ls), f'{len(wins)} ({pct(len(wins), len(ls))})',
                         clock(median([l['outcome']['killTargetTime'] for l in wins])),
                         num(mean(level3)) if level3 else '-', num(mean([l['outcome']['level'] for l in ls])),
                         f'{alive5} of {len(ls)}',
                         num(median([x for x in asteroid_xp if x is not None])) if any(x is not None for x in asteroid_xp) else '-',
                         num(median([x for x in built if x is not None])) if any(x is not None for x in built) else '-'])
        out.append(table(['Asteroid amount', 'Fights', 'Wins', 'Median time to win', 'Level at 3:00',
                          'Average final level', 'Alive at 5:00', 'Asteroid experience', 'Drones built'], rows))
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
        if fight.get('plan', 'random') != 'random':
            parts.append(f'plan={fight["plan"]}')
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

    # ───────────── Review ─────────────
    reviewed = [(f, logs[f['name']]) for f in fights if f['name'] in logs and logs[f['name']].get('review')]
    if reviewed:
        out.append('\n## Review\n')
        out.append('What the run review\'s detectors noticed (docs/dev/RUN_REVIEW_SPEC.md): moments like those the designer '
                   'reported while watching pilots. Rates are per minute of fight. The designer\'s own rate comes from '
                   'their fight logs that have detectors, on any tuning, so read it as a guide.\n')
        detector_ids = list(reviewed[0][1]['review']['detectors'])
        names = {d: reviewed[0][1]['review']['detectors'][d]['name'] for d in detector_ids}

        def rates(log_list):
            minutes = sum(l['outcome']['time'] for l in log_list) / 60
            # Only logs that had the detector count: one from before it existed, or before it was renamed, says
            # nothing about it. With none, the rate is unknown, not zero.
            out = []
            for d in detector_ids:
                having = [l for l in log_list if d in l['review']['detectors']]
                minutes_with = sum(l['outcome']['time'] for l in having) / 60
                out.append(num(sum(l['review']['detectors'][d]['count'] for l in having) / minutes_with, 2)
                           if minutes_with else 'not recorded')
            return out
        rows = []
        for label, group in by_combo.items():
            ls = [logs[f['name']] for f in group if f['name'] in logs and logs[f['name']].get('review')]
            if ls:
                rows.append([label] + rates(ls))
        human = []
        human_folder = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'playtests', 'human')
        # Each of the designer's fights has its own folder; older logs sit loose in playtests/human/.
        for path in sorted(glob.glob(os.path.join(human_folder, '*.json')) + glob.glob(os.path.join(human_folder, '*', '*.json'))):
            log = json.load(open(path, encoding='utf-8'))
            if log.get('review') and log['outcome']['time'] > 30:
                human.append(log)
        rows.append([f'**designer** ({len(human)} fights)'] + (rates(human) if human else ['-'] * len(detector_ids)))
        out.append(table(['Combination'] + [names[d] for d in detector_ids], rows))

        out.append('\n### Flagged moments\n')
        out.append('A few moments for each detector, from different fights. The command replays the fight in a window; '
                   'the moment is at the clock time given.\n')
        rows = []
        for d in detector_ids:
            picked = 0
            for f, log in reviewed:
                moments = log['review']['detectors'][d]['moments']
                if not moments:
                    continue
                m = moments[0]
                strips = (log.get('maps') or {}).get('strips', [])
                index = next((i for i, s in enumerate(strips) if s['reason'] == d), None)
                drawn = map_file(folder, f['name'], 'strip', (index, strips[index])) if index is not None else None
                rows.append([names[d], f'{clock(m["time"])} (clock {m["clock"]})', f'seed {f["seed"]}, {combo_label(f)}',
                             m['detail'], f'[strip]({drawn})' if drawn else '-', watch_command(f)])
                picked += 1
                if picked >= 3:
                    break
        out.append(table(['Detector', 'When', 'Fight', 'What it saw', 'Map', 'Watch it'], rows) if rows else 'None fired.')

        out.append('\n### Overview maps\n')
        out.append('The whole of a few fights per combination, each on one map: the VNP\'s path coloured by what it was '
                   'doing, the events along it, and health, drones, level and speed over time.\n')
        rows = []
        for label, group in by_combo.items():
            for name, why in overview_picks(group, logs):
                drawn = map_file(folder, name, 'overview')
                if drawn:
                    seed = next(f['seed'] for f in group if f['name'] == name)
                    rows.append([label, why, f'seed {seed}: {describe(logs[name])}', f'[overview]({drawn})'])
        out.append(table(['Combination', '', 'Fight', 'Map'], rows) if rows else 'No overview maps were drawn.')

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
        sys.exit('Usage: python tools/batches/batch_report.py <batch folder>')
    print(write_report(sys.argv[1]))
