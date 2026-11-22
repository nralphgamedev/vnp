"""
Runs a batch of automated fights, headless and several at once, then writes a report.

Run it from PowerShell through tools/batch.cmd, or with python from the vnp folder:

    tools\\batch.cmd                                   a standard batch: average pilot, Greedy and Random builds,
                                                      each fight starting with random weapons
    tools\\batch.cmd --size quick                      a few fights, as a smoke test
    tools\\batch.cmd --builds greedy --weapons all     Greedy with every starting VNP Weapon in turn
    tools\\batch.cmd --weapons default --drone-weapons default   the game's own starting weapons
    tools\\batch.cmd --drone-weapons all --seeds 10    every starting Drone Weapon, 10 seeds each
    tools\\batch.cmd --rendered --seeds 2              watch the fights in a window, one at a time, muted
    tools\\batch.cmd --variants base,gentle_levels     compare the tuning with a variant, on the same fights

Every combination of pilot, build, starting VNP Weapon, starting Drone Weapon and tuning variant is played on
seeds 1 to N, so two batches of the same size play the same fights. By default each fight starts with a VNP Weapon
and a Drone Weapon picked at random from the starting weapon cheat's lists. The pick follows the seed, so seed 7
starts with the same weapons in every batch, and for every pilot, build and variant in one, which keeps
comparisons fair. A tuning variant is a file in tools/variants/
listing changed tuning values (see scripts/cheats/playtesting/TuningVariant.cs); "base" is the tuning as it is.
With more than one variant, the report compares them side by side. A fight still going at the endless cap is stopped and counted
as having survived to the cap. Sizes, the cap and how many fights run at once are in
scripts/cheats/playtesting/batch_settings.tres.

Each batch gets a folder in playtests/batches/, holding batch.json (what was run and how each fight went), the
fight logs in logs/, each fight's console output in output/, and the report, report.md, with the same report as a page with charts, report.html. The report can be written
again from the logs with tools/batch_report.py.

Editor-only testing tool: nothing here is part of the game.
"""
import argparse
import concurrent.futures
import datetime
import json
import random
import os
import re
import subprocess
import sys
import time

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GODOT = os.environ.get('GODOT', r'F:\GameDev\godot\bin\godot.windows.editor.x86_64.mono.console.exe')
SETTINGS = os.path.join(PROJECT, 'scripts', 'cheats', 'playtesting', 'batch_settings.tres')
CHEATS = os.path.join(PROJECT, 'scripts', 'cheats', 'cheats.tres')

# Lines a headless run prints that are not problems: sounds that played are reported as leaked when it quits,
# because its dummy audio driver never finishes them.
KNOWN_NOISE = re.compile(r'ObjectDB instances were leaked at exit|resources still in use at exit|ObjectDB::cleanup|'
                         r'ResourceCache::clear|^\s*at: ')
PROBLEM = re.compile(r'ERROR|Exception|Fatal error', re.IGNORECASE)


def read_tres_values(path):
    """The simple Key = value lines of a .tres file's [resource] section."""
    values = {}
    in_resource = False
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line.startswith('['):
                in_resource = line == '[resource]'
                continue
            if in_resource and ' = ' in line:
                key, value = line.split(' = ', 1)
                values[key] = value
    return values


def starting_choices():
    """The VNP Weapon and Drone Weapon IDs the starting weapon cheat offers, from cheats.tres, in order."""
    text = open(CHEATS, encoding='utf-8').read()
    paths = dict(re.findall(r'\[ext_resource type="Resource" path="res://([^"]+)" id="([^"]+)"\]', text))
    paths = {rid: path for path, rid in paths.items()}

    def ids(prop):
        match = re.search(r'^' + prop + r' = .*$', text, re.M)
        if not match:
            return []
        result = []
        for rid in re.findall(r'ExtResource\("([^"]+)"\)', match.group(0))[1:]:  # the first is the array's type
            definition = open(os.path.join(PROJECT, paths[rid]), encoding='utf-8').read()
            result.append(re.search(r'^Id = "([^"]+)"', definition, re.M).group(1))
        return result

    return ids('StartingWeaponChoices'), ids('StartingDroneWeaponChoices')


def expand(value, choices, default):
    """A comma-separated list, where 'all' means every choice, 'default' the game's own starting weapon, and
    'random' one picked for each fight by its seed."""
    if value == 'all':
        return choices
    return [v.strip() for v in value.split(',') if v.strip()] or [default]


def main():
    parser = argparse.ArgumentParser(description='Run a batch of automated fights and write a report.')
    parser.add_argument('--size', choices=['quick', 'standard', 'long'], default='standard',
                        help='how many seeds per combination, from batch_settings.tres (default: standard)')
    parser.add_argument('--seeds', type=int, help='seeds per combination, in place of the size')
    parser.add_argument('--pilots', default='average', help='skill levels, comma-separated (default: average)')
    parser.add_argument('--builds', default='greedy,random', help='builds, comma-separated (default: greedy,random)')
    parser.add_argument('--weapons', default='random',
                        help='starting VNP Weapons, comma-separated, "all", "default" for the game\'s own, or '
                             '"random" for one picked by each fight\'s seed (default: random)')
    parser.add_argument('--drone-weapons', default='random',
                        help='starting Drone Weapons, comma-separated, "all", "default" for the game\'s own, or '
                             '"random" for one picked by each fight\'s seed (default: random)')
    parser.add_argument('--variants', default='base',
                        help='tuning variants from tools/variants/, comma-separated; "base" is the tuning as it is')
    parser.add_argument('--cap', type=float, help='endless cap in seconds of fight time, in place of the setting')
    parser.add_argument('--at-once', type=int, help='fights run at once, in place of the setting')
    parser.add_argument('--rendered', action='store_true',
                        help='run the fights in a window at normal speed, with effects, instead of headless')
    parser.add_argument('--name', default='', help='a short name added to the batch folder\'s name')
    args = parser.parse_args()

    settings = read_tres_values(SETTINGS)
    seeds = args.seeds or int(settings[{'quick': 'QuickSeeds', 'standard': 'StandardSeeds', 'long': 'LongSeeds'}[args.size]])
    cap = args.cap or float(settings['EndlessCapSeconds'])
    weapon_choices, drone_choices = starting_choices()
    pilots = [p.strip() for p in args.pilots.split(',') if p.strip()]
    builds = [b.strip() for b in args.builds.split(',') if b.strip()]
    weapons = expand(args.weapons, weapon_choices, 'default')
    drone_weapons = expand(args.drone_weapons, drone_choices, 'default')
    for w in weapons:
        if w not in ('default', 'random') and w not in weapon_choices:
            sys.exit(f'Unknown VNP Weapon {w}; choices: {", ".join(weapon_choices)}')
    for d in drone_weapons:
        if d not in ('default', 'random') and d not in drone_choices:
            sys.exit(f'Unknown Drone Weapon {d}; choices: {", ".join(drone_choices)}')
    for p in pilots:
        if not os.path.exists(os.path.join(PROJECT, 'pilots', 'skill_levels', p + '.tres')):
            sys.exit(f'No skill level {p} in pilots/skill_levels/')
    for b in builds:
        if not os.path.exists(os.path.join(PROJECT, 'pilots', 'builds', b + '.tres')):
            sys.exit(f'No build {b} in pilots/builds/')

    variants = [v.strip() for v in args.variants.split(',') if v.strip()] or ['base']
    for v in variants:
        if v != 'base' and not os.path.exists(os.path.join(PROJECT, 'tools', 'variants', v + '.txt')):
            sys.exit(f'No tuning variant {v} in tools/variants/')
    combos = [(p, b, w, d, v) for v in variants for p in pilots for b in builds for w in weapons for d in drone_weapons]
    fights = [(combo, seed) for combo in combos for seed in range(1, seeds + 1)]
    if args.rendered:
        at_once = args.at_once or int(settings.get('RenderedAtOnce', 1)) or 1
    else:
        at_once = args.at_once or int(settings.get('FightsAtOnce', 0)) or max(1, (os.cpu_count() or 2) - 1)
    mute = args.rendered and len(fights) > 1  # Headless fights never have sound.

    stamp = datetime.datetime.now().strftime('%Y-%m-%d_%H-%M-%S')
    batch_id = stamp + ('_' + re.sub(r'[^A-Za-z0-9_-]+', '-', args.name) if args.name else '')
    folder = os.path.join(PROJECT, 'playtests', 'batches', batch_id)
    os.makedirs(os.path.join(folder, 'logs'))
    os.makedirs(os.path.join(folder, 'output'))
    # Keep the logs out of Godot's import.
    open(os.path.join(PROJECT, 'playtests', '.gdignore'), 'a').close()

    print(f'Batch {batch_id}: {len(combos)} combination(s) x {seeds} seed(s) = {len(fights)} fights, '
          f'{at_once} at once, {"rendered" if args.rendered else "headless"}, cap {cap:.0f}s')
    print('Building the game...')
    build = subprocess.run(['dotnet', 'build', 'VNP.sln', '-nologo', '-v', 'q'], cwd=PROJECT,
                           capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout[-3000:])
        sys.exit('The build failed; fix it first.')

    def run(fight):
        (pilot, build_id, weapon, drone, variant), seed = fight
        # A random start is picked by the seed alone, so the same seed starts the same way in every combination.
        start_weapon = random.Random(seed * 7919 + 1).choice(weapon_choices) if weapon == 'random' else weapon
        start_drone = random.Random(seed * 7919 + 2).choice(drone_choices) if drone == 'random' else drone
        name = f'{variant}_{pilot}_{build_id}_{weapon}_{drone}_s{seed}'
        cmd = [GODOT] + ([] if args.rendered else ['--headless']) + [
            '--path', PROJECT, '--fixed-fps', '60', 'res://scripts/cheats/scenario.tscn', '--',
            f'seed={seed}', f'pilot={pilot}', f'build={build_id}', f'maxtime={cap}',
            # The frame limit only guards against a fight that never stops; the cap ends fights in fight time,
            # and pauses for level-up offers also count as frames.
            f'frames={int(cap * 60 * 3)}', 'report=1000000',
            f'log=res://playtests/batches/{batch_id}/logs', f'logname={name}', 'close=yes']
        if start_weapon != 'default':
            cmd.append(f'weapon={start_weapon}')
        if start_drone != 'default':
            cmd.append(f'drone_weapon={start_drone}')
        if mute:
            cmd.append('mute=yes')
        if variant != 'base':
            cmd.append(f'variant={variant}')
        started = time.time()
        try:
            result = subprocess.run(cmd, cwd=PROJECT, capture_output=True, text=True, encoding='utf-8',
                                    errors='replace', timeout=max(600, cap * 4))
            output, code = result.stdout + result.stderr, result.returncode
        except subprocess.TimeoutExpired as timeout:
            output = (timeout.stdout or b'').decode('utf-8', 'replace') if isinstance(timeout.stdout, bytes) else (timeout.stdout or '')
            code = 'timeout'
        with open(os.path.join(folder, 'output', name + '.txt'), 'w', encoding='utf-8') as f:
            f.write(output)
        problems = [line for line in output.splitlines() if PROBLEM.search(line) and not KNOWN_NOISE.search(line)]
        logged = os.path.exists(os.path.join(folder, 'logs', name + '.json'))
        status = 'ok' if code == 0 and logged and not problems else (
            'timed out' if code == 'timeout' else 'crashed' if code != 0 else 'no log' if not logged else 'errors')
        return {'name': name, 'variant': variant, 'pilot': pilot, 'build': build_id, 'weapon': weapon, 'droneWeapon': drone,
                'startWeapon': start_weapon, 'startDroneWeapon': start_drone,
                'seed': seed, 'status': status, 'exitCode': code, 'problems': problems[:5],
                'seconds': round(time.time() - started, 1)}

    results = []
    started = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=at_once) as pool:
        for i, result in enumerate(pool.map(run, fights), 1):
            results.append(result)
            mark = '' if result['status'] == 'ok' else f'  <- {result["status"]}'
            print(f'  [{i}/{len(fights)}] {result["name"]}  {result["seconds"]:.0f}s{mark}', flush=True)

    batch = {
        'id': batch_id, 'started': stamp, 'seconds': round(time.time() - started, 1),
        'size': args.size if not args.seeds else f'{seeds} seeds', 'seeds': seeds, 'cap': cap,
        'rendered': args.rendered, 'atOnce': at_once, 'pilots': pilots, 'builds': builds,
        'weapons': weapons, 'droneWeapons': drone_weapons, 'variants': variants, 'fights': results,
    }
    with open(os.path.join(folder, 'batch.json'), 'w', encoding='utf-8') as f:
        json.dump(batch, f, indent=1)

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import batch_report
    # The run review's maps: overview maps of a few fights per combination and strips of a few flagged moments
    # per detector, drawn in one run of the game's map drawer.
    logs_by_name = batch_report.load(folder)[1]
    chosen = batch_report.choose_maps(batch, logs_by_name)
    if chosen:
        args = []
        for name, (overview, strips) in chosen.items():
            picks = ','.join(str(i) for i in sorted(strips)) if strips else 'none'
            args.append(f'log={os.path.join(folder, "logs", name + ".json")}@{picks}')
        overview_names = {n for n, (o, _) in chosen.items() if o}
        # Overviews and strips go in one run per kind, since the drawer's overview switch applies to all its logs.
        for with_overview in (True, False):
            group = [a for a, n in zip(args, chosen) if (n in overview_names) == with_overview]
            if not group:
                continue
            cmd = [GODOT, '--headless', '--path', PROJECT, 'res://scripts/cheats/playtesting/draw_maps.tscn', '--',
                   f'out={os.path.join(folder, "maps")}'] + ([] if with_overview else ['overview=no']) + group
            drawn = subprocess.run(cmd, cwd=PROJECT, capture_output=True, text=True, encoding='utf-8', errors='replace')
            print(f'Drew {drawn.stdout.count("MAP: ")} maps.')
    report = batch_report.write_report(folder)

    failed = [r for r in results if r['status'] != 'ok']
    print(f'\nDone in {batch["seconds"] / 60:.1f} minutes: {len(results) - len(failed)} of {len(results)} fights ok.')
    for r in failed:
        print(f'  {r["name"]}: {r["status"]} {r["problems"][:1]}')
    print(f'Report: {report}')
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
