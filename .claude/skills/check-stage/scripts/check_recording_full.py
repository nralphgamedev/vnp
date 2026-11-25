"""
The full recording check, run before each commit (check_stage.sh --full): plays a few recorded pilot fights that
between them use every starting VNP Weapon, every Drone Weapon, every weapon mod and every asteroid amount, into
escalation, then checks each recording and log field by field against the fight's own statistics.

    python check_recording_full.py GODOT_EXECUTABLE

The fights are worked out from what exists, so a new weapon, Drone Weapon, mod or asteroid amount is covered
with no change here: VNP Weapons two to a fight (the second taken as an upgrade), Drone Weapons and asteroid
amounts in turn, and each mod in a fight with its weapon (a weapon holds one mod). Each fight also starts with
SPAWN_GROUPS groups of every enemy type around the VNP, so every type, and the shots of those that fire, is
recorded even when the fight's own spawning leaves it out (phase 4 spawns Grey Goo only). Prints PASS or FAIL lines and
exits 1 on any failure. Takes a few minutes. Editor-only testing tool.
"""
import concurrent.futures
import glob
import gzip
import json
import os
import re
import shutil
import subprocess
import sys

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..', '..'))
OUT = os.path.join(PROJECT, 'playtests', 'check_recording_full')
FIGHT_SECONDS = 330  # past the 5:00 limit, so escalation is recorded when a fight lasts
# The fight kept alive, so escalation is always recorded however hard the enemies are; the others can lose.
ESCALATING = 'fight2'
SPAWN_GROUPS = 2     # groups of every enemy type spawned around the VNP as each fight starts
FAILS = 0


def check(ok, text):
    global FAILS
    print(('  PASS ' if ok else '  FAIL ') + text)
    FAILS += 0 if ok else 1


def tres_id(path):
    m = re.search(r'^Id = "(.+)"', open(path, encoding='utf-8').read(), re.M)
    return m.group(1) if m else None


def starting_choices(field):
    """The IDs in one of the starting weapon cheat's lists, in in_game_cheats.tres."""
    text = open(os.path.join(PROJECT, 'scripts', 'cheats', 'in_game_cheats.tres'), encoding='utf-8').read()
    paths = {ref: path for path, ref in re.findall(r'path="(res://[^"]+)" id="([^"]+)"', text)}
    line = re.search(rf'^{field} = .*$', text, re.M).group(0)
    found = []
    for ref in re.findall(r'ExtResource\("([^"]+)"\)', line.split('(', 1)[1]):
        path = paths.get(ref, '')
        if path.endswith('.tres'):
            found.append(tres_id(os.path.join(PROJECT, path[len('res://'):])))
    return [f for f in found if f]


def mods():
    """Every weapon mod (an UnlockWeaponFeature upgrade, Effect 7) as (weapon ID, upgrade ID)."""
    found = []
    for path in glob.glob(os.path.join(PROJECT, 'weapons', '*', 'upgrades', '*.tres')):
        text = open(path, encoding='utf-8').read()
        if re.search(r'^Effect = 7$', text, re.M):
            found.append((os.path.basename(os.path.dirname(os.path.dirname(path))), tres_id(path)))
    return sorted(found)


def plan():
    weapons = starting_choices('StartingWeaponChoices')
    drones = starting_choices('StartingDroneWeaponChoices')
    amounts = sorted(tres_id(p) for p in glob.glob(os.path.join(PROJECT, 'tuning', 'asteroid_amounts', '*.tres')))
    fights = [{'weapons': weapons[i:i + 2], 'mod': None} for i in range(0, len(weapons), 2)]
    for weapon, mod in mods():
        home = next((f for f in fights if weapon in f['weapons'] and f['mod'] is None), None)
        if home is None:
            home = {'weapons': [weapon], 'mod': None}
            fights.append(home)
        home['mod'] = mod
    for i, f in enumerate(fights):
        f['drone'] = drones[i % len(drones)]
        f['asteroids'] = amounts[i % len(amounts)]
        f['seed'] = 21 + i
        f['name'] = f'fight{i + 1}'
    return fights


def run(godot, f):
    take = f['weapons'][1:] + ([f['mod']] if f['mod'] else [])
    cmd = [godot, '--headless', '--path', PROJECT, '--fixed-fps', '60', 'res://scripts/cheats/scenario.tscn', '--',
           'pilot=expert', 'build=designer', f'seed={f["seed"]}', f'maxtime={FIGHT_SECONDS}', 'record=yes', 'mute=yes',
           f'weapon={f["weapons"][0]}', f'drone_weapon={f["drone"]}', f'asteroids={f["asteroids"]}', f'spawn=all:{SPAWN_GROUPS}',
           f'log=res://playtests/check_recording_full/{f["name"]}'] + ([f'take={",".join(take)}'] if take else [])         + (['invulnerable'] if f['name'] == ESCALATING else [])
    result = subprocess.run(cmd, cwd=PROJECT, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=900)
    return f, result.returncode


def verify(folder, f):
    log_path = [p for p in glob.glob(os.path.join(folder, '*.json'))][0]
    log = json.load(open(log_path, encoding='utf-8'))
    s, o, maps, stats = log['settings'], log['outcome'], log['maps'], log['stats']
    print(f'== {f["name"]}: {"+".join(f["weapons"])}, {f["drone"]} drones, {f["asteroids"]} asteroids'
          f'{", mod " + f["mod"] if f["mod"] else ""}: {o["result"]} at {o["time"]}s')
    check(maps.get('recording') is not None, 'the log names its recording')
    rec = json.loads(gzip.open(os.path.join(folder, maps['recording'])).read())
    frames = rec['frames']
    times = [x['time'] for x in frames]
    gaps = [b - a for a, b in zip(times, times[1:])]
    spacing = rec['spacing']
    check(max(gaps) < spacing * 2, f'{len(frames)} pictures every {spacing} s, largest step {max(gaps):.2f} s')
    check(times[0] < spacing * 2 and abs(times[-1] - o['time']) < spacing * 1.5,
          f'from the start ({times[0]} s) to the end ({times[-1]} s against {o["time"]} s)')

    keys = {'time', 'vnp', 'health', 'drones', 'level', 'enemies', 'droneDots', 'material', 'shots', 'placed', 'loadout', 'droneWeapon',
            'hazards', 'zones'}
    check(all(keys <= set(x) for x in frames), 'every picture has every part')
    check(all(len(e) == 8 for x in frames for e in x['enemies']), 'enemies: position, size, type, velocity, ID, drain')
    check(all(len(d) == 4 for x in frames for d in x['droneDots']), 'drones: position, health, target')
    check(all(len(e) == 5 for x in frames for e in x['shots']), 'shots: position, velocity, what fired it')
    check(all(len(e) == 7 for x in frames for e in x['placed']), 'placed objects: position, radius, weapon, shape, end')

    # What a pilot perceives (Arbiter): hazards declared by enemies and the projectiles, zones by the VNP's weapons.
    hazards = [h for x in frames for h in x['hazards']]
    check(all(len(h) == 13 for h in hazards), 'hazards: shape, position, velocity, extent, radius, inner, timing, harm, flags')
    check(all(len(z) == 8 for x in frames for z in x['zones']), 'zones: kind, shape, position, radius, inner, follows, weapon')
    check(any(int(h[12]) & 1 and h[10] < 0 for h in hazards), 'hazards: bodies that hurt the VNP by touch, for as long as they exist')
    check(any(int(h[12]) & 2 and int(h[12]) & 4 for h in hazards), 'hazards: bodies that drain drones each second')
    check(any(h[10] >= 0 and (h[3] or h[4]) for h in hazards), 'hazards: enemy shots, moving, until they fizzle')
    check(any(h[9] > 0 for h in hazards), 'hazards: attacks declared while being readied, before they land')
    zoned = {int(z[7]) for x in frames for z in x['zones']}

    names = [w['id'] for w in maps.get('weaponTypes', [])]
    for w in dict.fromkeys(s['startingWeapons'] + o['weaponsAtEnd']):
        check(w in names and names.index(w) in zoned, f'{w}: declares where it works (zones)')
    shown = {names[int(e[3])] for x in frames for e in x['placed']} | {names[int(e[4])] for x in frames for e in x['shots'] if e[4] >= 0}
    targets = any(d[3] for x in frames for d in x['droneDots'])
    for w in dict.fromkeys(s['startingWeapons'] + o['weaponsAtEnd'] + [s['startingDroneWeapon'], o['droneWeaponAtEnd']]):
        if w == 'ram':
            check(targets, 'ram: its drones are recorded with their targets (it places nothing of its own)')
        else:
            check(w in shown, f'{w}: shows in the recording')
    check(any(e[4] < 0 for x in frames for e in x['shots']), 'enemy shots recorded')
    health = [d[2] for x in frames for d in x['droneDots']]
    check(min(health) >= 0 and max(health) <= 1 and min(health) < 0.9, f'drone health within 0 to 1, and varying ({min(health):.2f} to {max(health):.2f})')
    matched = total = 0
    for x in frames:
        ids = {int(e[6]) for e in x['enemies']}
        for d in x['droneDots']:
            if d[3]:
                total += 1
                matched += int(d[3]) in ids
    check(total > 0 and matched / total > 0.8, f'drones\' targets found among the enemies in the picture ({matched}/{total})')
    drainers = {i for i, t in enumerate(maps['enemyTypes']) if t.get('absorbsDrones')}
    drains = [e[7] for x in frames for e in x['enemies'] if int(e[3]) in drainers]
    others = [e[7] for x in frames for e in x['enemies'] if int(e[3]) not in drainers]
    check(not drainers or (drains and min(drains) > 0 and max(others or [0]) == 0), 'drain on drone eaters only')
    check(any(abs(e[4]) + abs(e[5]) > 0.5 for x in frames for e in x['enemies']), 'enemy headings recorded')
    check(any(m[3] for x in frames for m in x['material']) and any(not m[3] for x in frames for m in x['material']),
          'asteroids and debris both recorded')
    if f['mod']:
        recorded = {m for x in frames for entry in x['loadout'] for m in entry[1]}
        check(len(recorded) == 1, f'the mod shows in the loadout ({sorted(recorded)})')

    events = maps['events']
    hits = sum(e['value'] for e in events if e['kind'] == 'hit')
    taken = sum(stats['damageTaken'].values())
    check(abs(hits - taken) < 1.0, f'hits add up to the damage taken ({hits:.1f} against {taken:.1f})')
    lost = sum(e['value'] for e in events if e['kind'] == 'drones_lost')
    lost_stats = sum(stats['drones']['destroyed'].values()) + sum(stats['drones']['absorbed'].values())
    check(lost == lost_stats, f'drones lost add up ({lost} against {lost_stats})')
    levels = sum(1 for e in events if e['kind'] == 'level')
    check(levels == o['level'] - 1, f'every level-up logged ({levels})')
    waves = [e for e in events if e['kind'] == 'wave']
    check(waves and [e['value'] for e in waves] == list(range(1, len(waves) + 1)) and all('groups' in e for e in waves),
          f'waves logged in order, with their size ({len(waves)})')
    steps = sum(1 for e in events if e['kind'] == 'escalation')
    check((o['time'] < 300) == (steps == 0), f'escalation steps logged ({steps})')
    check(all(st.get('fromRecording') and 'frames' not in st for st in maps['strips']),
          f'{len(maps["strips"])} flagged moments keep only their time')
    return o['time'] >= 300


def main():
    godot = sys.argv[1]
    fights = plan()
    shutil.rmtree(OUT, ignore_errors=True)
    described = [f'{"+".join(f["weapons"])} / {f["drone"]} / {f["asteroids"]}' + (f' / {f["mod"]}' if f['mod'] else '')
                 for f in fights]
    print(f'{len(fights)} recorded fights: ' + '; '.join(described))
    escalated = False
    with concurrent.futures.ThreadPoolExecutor(max_workers=len(fights)) as pool:
        for f, code in pool.map(lambda f: run(godot, f), fights):
            folder = os.path.join(OUT, f['name'])
            if code != 0 or not glob.glob(os.path.join(folder, '*.recording.json.gz')):
                print(f'== {f["name"]}')
                check(False, f'the fight ran and wrote its recording (exit code {code})')
                continue
            escalated |= verify(folder, f)
    print('== coverage')
    check(escalated, 'at least one fight lasted past 5:00, so escalation was recorded')
    shutil.rmtree(OUT, ignore_errors=True)
    print(f'\n{"ALL PASSED" if FAILS == 0 else f"{FAILS} FAILED"}')
    sys.exit(1 if FAILS else 0)


if __name__ == '__main__':
    main()
