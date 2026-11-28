"""
Build paths: how each fight's loadout was built over time, from its fight log, for the batch report's Build paths
section. A loadout's final state hides the strategy: one weapon built up all fight and a second bought late ends the
same as two weapons mixed from the start, so this follows the order of level-up picks instead.

For each fight:
- **Investment in each VNP Weapon:** equipping it (or starting with it) counts EQUIP_WEIGHT, each of its upgrades
  (stat upgrades and mods) one, each at the time it was taken.
- **Drone Weapons:** which were used and for how long, from the starting one and each switch, and each one's share of
  the drones' damage.
- **Its shape,** by the rules below, from the investment and when the second weapon came.

Editor-only testing tool: nothing here is part of the game.
"""
import os
import re

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Equipping a weapon is a bigger commitment than one stat upgrade, so it counts as this many.
EQUIP_WEIGHT = 2
# Focused: the most-invested weapon has at least this share of all weapon investment.
FOCUS_SHARE = 2 / 3
# A second weapon first equipped at or after this fight time, in seconds, makes a "focus, then late second" build.
LATE_SECOND_SECONDS = 180.0

SHAPES = ['single weapon', 'focused', 'focus, then late second', 'mixed']

# UpgradeEffect in tuning/UpgradeDefinition.cs, by number.
EQUIP_WEAPON, SCALE_WEAPON_STAT, SWITCH_DRONE_WEAPON, ADD_WEAPON_STAT, UNLOCK_FEATURE = 0, 1, 2, 6, 7


def _upgrade_files():
    """Every upgrade file in the pool, from tuning/levels.tres."""
    text = open(os.path.join(PROJECT, 'tuning', 'levels.tres'), encoding='utf-8').read()
    return [p for p in re.findall(r'path="res://([^"]+)"', text) if '/upgrades/' in p]


def load_upgrades():
    """Each upgrade's ID mapped to what it does to the loadout: {'effect', 'weapon', 'drone'}, weapon and drone being
    the IDs of the weapon or Drone Weapon it belongs to, if any (from their files' names)."""
    upgrades = {}
    for rel in _upgrade_files():
        path = os.path.join(PROJECT, rel)
        if not os.path.exists(path):
            continue
        text = open(path, encoding='utf-8').read()
        uid = re.search(r'^Id = "(.+)"', text, re.M)
        effect = re.search(r'^Effect = (\d+)', text, re.M)
        if not uid:
            continue
        # Each external resource's id mapped to its path, to find the weapon or Drone Weapon the upgrade names.
        ext = {i: p for p, i in re.findall(r'\[ext_resource [^\]]*path="res://([^"]+)" id="([^"]+)"\]', text)}

        def target(field):
            m = re.search(rf'^{field} = ExtResource\("([^"]+)"\)', text, re.M)
            return os.path.splitext(os.path.basename(ext[m.group(1)]))[0] if m and m.group(1) in ext else None

        upgrades[uid.group(1)] = {'effect': int(effect.group(1)) if effect else -1,
                                  'weapon': target('Weapon'), 'drone': target('DroneWeapon')}
    return upgrades


def path_of(log, upgrades):
    """One fight's build path: investment per weapon with times, equip times, Drone Weapon time and damage shares,
    and its shape."""
    s, o = log['settings'], log['outcome']
    end = o['time']
    invest = {}      # weapon -> list of (time, amount)
    equipped = {}    # weapon -> time first equipped
    for w in s.get('startingWeapons') or []:
        equipped[w] = 0.0
        invest.setdefault(w, []).append((0.0, EQUIP_WEIGHT))
    drone_spans = []  # (drone weapon, from time)
    if s.get('startingDroneWeapon'):
        drone_spans.append((s['startingDroneWeapon'], 0.0))
    for lu in log['stats'].get('levelUps', []):
        u = upgrades.get(lu.get('taken'))
        if not u:
            continue
        t = lu['time']
        if u['effect'] == EQUIP_WEAPON and u['weapon']:
            equipped.setdefault(u['weapon'], t)
            invest.setdefault(u['weapon'], []).append((t, EQUIP_WEIGHT))
        elif u['effect'] in (SCALE_WEAPON_STAT, ADD_WEAPON_STAT, UNLOCK_FEATURE) and u['weapon']:
            invest.setdefault(u['weapon'], []).append((t, 1))
        elif u['effect'] == SWITCH_DRONE_WEAPON and u['drone']:
            drone_spans.append((u['drone'], t))

    totals = {w: sum(a for _, a in v) for w, v in invest.items()}
    ranked = sorted(totals, key=lambda w: (-totals[w], equipped.get(w, 0)))
    all_invest = sum(totals.values()) or 1
    first = ranked[0] if ranked else None
    second = sorted((w for w in equipped if w != first), key=lambda w: equipped[w])
    second_time = equipped[second[0]] if second else None
    if len(equipped) <= 1:
        shape = 'single weapon'
    elif second_time is not None and second_time >= LATE_SECOND_SECONDS and \
            min(equipped.values()) < LATE_SECOND_SECONDS:
        shape = 'focus, then late second'
    elif first is not None and totals[first] / all_invest >= FOCUS_SHARE:
        shape = 'focused'
    else:
        shape = 'mixed'

    drone_time = {}
    for i, (d, start) in enumerate(drone_spans):
        stop = drone_spans[i + 1][1] if i + 1 < len(drone_spans) else end
        drone_time[d] = drone_time.get(d, 0.0) + max(0.0, stop - start)
    drone_damage = {d['source']: d['total'] for d in log['stats'].get('damage', []) if d.get('kind') == 'DroneWeapon'}
    return {
        'shape': shape,
        'pair': ' + '.join(sorted(equipped)) if equipped else '-',
        'invest': totals,
        'investShare': {w: totals[w] / all_invest for w in totals},
        'equipped': equipped,
        'droneTime': drone_time,
        'droneDamage': drone_damage,
        'mainDrone': max(drone_time, key=drone_time.get) if drone_time else None,
        'end': end,
    }


def damage_by_minute(log):
    """Each source's damage within each minute, from the log's running totals: [(minute, {source: damage})].
    Empty for logs written before damage by minute was logged."""
    snaps = log.get('damageByMinute') or []
    out, before = [], {}
    for snap in snaps:
        minute = int(-(-snap['time'] // 60))  # The minute it ends, 1 for the first.
        now = snap['damage']
        out.append((minute, {k: v - before.get(k, 0.0) for k, v in now.items() if v - before.get(k, 0.0) > 0}))
        before = now
    return out
