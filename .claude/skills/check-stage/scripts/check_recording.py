"""
Checks that every VNP Weapon and Drone Weapon shows up in a whole-fight recording (the run review's recording of
the designer's fights), so a new weapon can't be left off the maps unnoticed.

    python check_recording.py GODOT_EXECUTABLE

Plays one short recorded pilot fight for each VNP Weapon in the starting weapon cheat's list, with enemies
spawned around it, pairing each with a Drone Weapon in turn so every Drone Weapon is covered too, several at
once. Then each fight's recording must show its weapons: an object it placed (CollectPlaced: shards, mines,
missiles, rings, beams) or a shot it fired through the projectiles. Prints PASS or FAIL lines, and exits 1 on
any failure.

A weapon that never has anything of its own out in the world must be listed in NOTHING_OF_ITS_OWN with why;
its CollectPlaced says the same in its code. Editor-only testing tool.
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
OUT = os.path.join(PROJECT, 'playtests', 'check_record')

# Weapons that put nothing of their own in the world, so a recording shows them only through what they move.
NOTHING_OF_ITS_OWN = {
    'ram': 'ramming is the drones themselves, which are recorded with their targets',
}


def choices(field):
    text = open(os.path.join(PROJECT, 'scripts', 'cheats', 'in_game_cheats.tres'), encoding='utf-8').read()
    paths = dict(re.findall(r'path="(res://[^"]+)" id="([^"]+)"', text))
    ids = {v: k for k, v in paths.items()}
    line = re.search(rf'^{field} = .*$', text, re.M).group(0)
    found = []
    for ref in re.findall(r'ExtResource\("([^"]+)"\)\)?', line.split('(', 1)[1]):
        path = ids.get(ref)
        if path and path.endswith('.tres') and 'Definition' not in path:
            m = re.search(r'^Id = "(.+)"', open(os.path.join(PROJECT, path[len('res://'):]), encoding='utf-8').read(), re.M)
            if m:
                found.append(m.group(1))
    return found


def run(godot, weapon, drone):
    folder = os.path.join(OUT, weapon)
    cmd = [godot, '--headless', '--path', PROJECT, '--fixed-fps', '60', 'res://scripts/cheats/scenario.tscn', '--',
           'pilot=expert', 'build=greedy', f'weapon={weapon}', f'drone_weapon={drone}', 'seed=1', 'spawn=all:2',
           'invulnerable', 'frames=900', 'record=yes', 'mute=yes', f'log=res://playtests/check_record/{weapon}']
    result = subprocess.run(cmd, cwd=PROJECT, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=300)
    return weapon, drone, folder, result.returncode


def shown(folder, weapon_ids):
    """Which of the weapon IDs placed an object or fired a shot anywhere in the fight's recording."""
    logs = [p for p in glob.glob(os.path.join(folder, '*.json'))]
    recordings = glob.glob(os.path.join(folder, '*.recording.json.gz'))
    if not logs or not recordings:
        return None
    log = json.load(open(logs[0], encoding='utf-8'))
    names = [w['id'] for w in log['maps'].get('weaponTypes', [])]
    frames = json.loads(gzip.open(recordings[0]).read())['frames']
    seen = set()
    for frame in frames:
        for p in frame.get('placed', []):
            seen.add(int(p[3]))
        for s in frame.get('shots', []):
            if len(s) > 4 and s[4] >= 0:
                seen.add(int(s[4]))
    return {w for w in weapon_ids if w in names and names.index(w) in seen}


def main():
    godot = sys.argv[1]
    weapons, drones = choices('StartingWeaponChoices'), choices('StartingDroneWeaponChoices')
    if not weapons or not drones:
        sys.exit('FAIL: could not read the starting weapon lists from in_game_cheats.tres')
    shutil.rmtree(OUT, ignore_errors=True)
    pairs = [(w, drones[i % len(drones)]) for i, w in enumerate(weapons)]
    failed = 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=len(pairs)) as pool:
        for weapon, drone, folder, code in pool.map(lambda p: run(godot, *p), pairs):
            if code != 0:
                print(f'FAIL: the recorded fight with {weapon} and {drone} exited with code {code}')
                failed += 1
                continue
            got = shown(folder, {weapon, drone})
            if got is None:
                print(f'FAIL: the fight with {weapon} and {drone} wrote no log or recording')
                failed += 1
                continue
            for w in (weapon, drone):
                if w in got:
                    print(f'PASS: {w} is in the recording')
                elif w in NOTHING_OF_ITS_OWN:
                    print(f'PASS: {w} has nothing of its own to record ({NOTHING_OF_ITS_OWN[w]})')
                else:
                    print(f'FAIL: {w} placed nothing and fired nothing in 15 seconds of recording; give it a '
                          f'CollectPlaced, or list it in NOTHING_OF_ITS_OWN in check_recording.py if it truly has nothing')
                    failed += 1
    shutil.rmtree(OUT, ignore_errors=True)
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
