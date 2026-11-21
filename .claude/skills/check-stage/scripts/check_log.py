"""
Cross-checks one fight log for internal consistency: totals against their parts, kills counted three ways,
drones in and out, levels against level-ups, the timeline against the outcome, weapons against the upgrades
that equipped them, and input and frame times that look right. Prints FAIL lines for anything that doesn't add
up, then every check made.

    python check_log.py LOG.json
"""
import json
import sys

d = json.load(open(sys.argv[1], encoding='utf-8'))
s, o, st, tl = d['settings'], d['outcome'], d['stats'], d['timeline']
problems, notes = [], []

def check(ok, what):
    (notes if ok else problems).append(('OK   ' if ok else 'FAIL ') + what)

def close(a, b, tol=0.6):
    return abs(a - b) <= tol

# Damage
src_sum = sum(x['total'] for x in st['damage'])
check(close(src_sum, st['totalDamage'], 0.5 * len(st['damage']) + 0.1),
      f"total damage {st['totalDamage']} = sum of sources {src_sum:.1f}")
for x in st['damage']:
    by = sum(x['byEnemy'].values())
    check(close(by, x['total'], 0.5 * len(x['byEnemy']) + 0.1), f"{x['source']} total {x['total']} = sum by enemy {by:.1f}")

# Kills
kills_list = st['kills']
by_enemy = sum(st['killsByEnemy'].values())
check(o['kills'] == len(kills_list) == by_enemy == tl[-1]['kills'],
      f"kills: outcome {o['kills']}, list {len(kills_list)}, by enemy {by_enemy}, last timeline {tl[-1]['kills']}")
times = [k[0] for k in kills_list]
check(times == sorted(times) and (not times or times[-1] <= o['time'] + 0.1), "kill times in order and within the fight")
if o['killTargetTime'] is not None:
    nth = kills_list[o['killTarget'] - 1][0] if len(kills_list) >= o['killTarget'] else None
    check(nth is not None and close(nth, o['killTargetTime'], 0.15),
          f"kill target time {o['killTargetTime']} = time of kill #{o['killTarget']} ({nth})")
check((o['killTargetTime'] is not None) == (o['endless'] or o['result'] == 'win'),
      f"kill target time present exactly when won/endless (endless={o['endless']}, result={o['result']})")

# Drones
dr = st['drones']
lost = sum(dr['destroyed'].values()) + sum(dr['absorbed'].values())
expected_alive = dr['started'] + dr['built'] - lost
check(expected_alive == tl[-1]['drones'],
      f"drones: started {dr['started']} + built {dr['built']} - lost {lost} = {expected_alive}, last timeline {tl[-1]['drones']}")

# Levels and upgrades
lu = st['levelUps']
taken = [l['taken'] for l in lu if l['taken']]
check(o['level'] == 1 + len(lu), f"level {o['level']} = 1 + level-ups {len(lu)}")
check(taken == o['upgradesTaken'], f"upgrades taken match level-up picks ({len(taken)} vs {len(o['upgradesTaken'])})")
for l in lu:
    if l['taken']:
        check(l['taken'] in l['offers'][-1], f"L{l['level']} pick {l['taken']} was in its final offer")
    check(l['rerolls'] == len(l['offers']) - 1, f"L{l['level']} rerolls {l['rerolls']} = offers shown - 1")
    check(len(set(l['offers'][-1])) == len(l['offers'][-1]), f"L{l['level']} offer has no duplicates")
levels = [l['level'] for l in lu]
check(levels == list(range(2, 2 + len(lu))), f"level-ups numbered 2..{1 + len(lu)}")
lt = [l['time'] for l in lu]
check(lt == sorted(lt), "level-up times in order")
# Level in the timeline should match the count of level-ups shown by then.
mismatch = [x['time'] for x in tl if x['level'] < 1 + sum(1 for t in lt if t <= x['time'] - 0.05)]
check(not mismatch, f"timeline level never behind the level-ups shown by then {mismatch[:5]}")

# Experience
xp = st['experience']['asteroids'] + st['experience']['debris']
check(close(xp, o['experience'], 0.2), f"experience {o['experience']} = asteroids + debris {xp:.1f}")

# Timeline
t = [x['time'] for x in tl]
gaps = [round(b - a, 2) for a, b in zip(t, t[1:])]
check(t == sorted(t), "timeline times in order")
check(all(g <= 1.05 for g in gaps[:-1]), f"timeline sampled about every second (largest gap {max(gaps) if gaps else 0})")
for key in ('experience', 'level', 'kills'):
    vals = [x[key] for x in tl]
    check(vals == sorted(vals), f"timeline {key} never goes down")
check(all(x['health'] <= x['maxHealth'] + 0.05 for x in tl), "health never above max health")
check(close(tl[-1]['time'], o['time'], 0.15), f"timeline ends at the outcome time ({tl[-1]['time']} vs {o['time']})")
check(close(tl[-1]['experience'], o['experience'], 0.2), "timeline's last experience matches the outcome")
if o['result'] == 'loss':
    check(tl[-1]['health'] == 0, "a loss ends at 0 health")
taken_dmg = sum(st['damageTaken'].values())
max_hp = max(x['maxHealth'] for x in tl)
check(taken_dmg >= tl[0]['health'] - tl[-1]['health'] - 0.5,
      f"damage taken {taken_dmg:.1f} covers the health lost {tl[0]['health'] - tl[-1]['health']:.1f}")

# Weapons
equips = [u for u in o['upgradesTaken'] if u in ('pulse_laser', 'orbital_shards', 'scatter_shot', 'seeker_missiles', 'shockwave', 'mine_layer')]
check(o['weaponsAtEnd'] == s['startingWeapons'] + equips,
      f"weapons at end {o['weaponsAtEnd']} = starting {s['startingWeapons']} + equipped {equips}")
switches = [u for u in o['upgradesTaken'] if u in ('projectile', 'beam', 'ram')]
expected_drone_weapon = switches[-1] if switches else s['startingDroneWeapon']
check(o['droneWeaponAtEnd'] == expected_drone_weapon, f"drone weapon at end {o['droneWeaponAtEnd']} = {expected_drone_weapon}")
drone_sources = [x['source'] for x in st['damage'] if x['kind'] == 'DroneWeapon']
check(all(src in [s['startingDroneWeapon']] + switches for src in drone_sources),
      f"drone damage only from drone weapons used ({drone_sources})")

# Settings and input
check(bool(s['startingWeapons']) and s['tuningFingerprint'] and s['gitVersion'] != 'unknown', "settings filled in")
if d.get('input'):
    inp = d['input']['samples']
    it = [x[0] for x in inp]
    check(it == sorted(it), "input samples in order")
    rate = len(inp) / max(1e-6, o['time'])
    check(abs(rate - d['input']['samplesPerSecond']) < 1.5,
          f"input rate {rate:.1f}/s near {d['input']['samplesPerSecond']}")
    check(all(abs(x[1]) <= 1.01 and abs(x[2]) <= 1.01 for x in inp), "input values within -1..1")
fr = d['frames']
check(0 < fr['averageMs'] < 16, f"frame times look like game work, not whole frames ({fr})")

print('\n'.join(problems) if problems else 'No problems found.')
print(f'({len(notes)} checks passed, {len(problems)} failed)')
for n in notes:
    print('  ' + n)
