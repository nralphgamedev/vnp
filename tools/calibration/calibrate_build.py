"""
Fits the Designer build's upgrade weights to the designer's own choices, from their fight logs in
playtests/human/, and writes them to pilots/builds/designer.tres:

    python tools/calibration/calibrate_build.py            fit, write the build, and print what it fitted
    python tools/calibration/calibrate_build.py --dry-run  fit and print, without writing

Every level-up offer the designer answered is a choice among three upgrades (the offer after any reroll). Each is
described in the terms the pilot's upgrade decision scores on Arbiter's decision core (Pilot.ChooseUpgrade):
- liking: the build's weight for that upgrade, one unknown per upgrade ("liking:<id>");
- owned: 1 if it improves something already owned;
- new weapon: 1 if it gives a new weapon.
Arbiter's own training tool (arbiter/training/fit_choices.py) then fits the weights that best explain which one was
taken, as a softmax choice. Upgrades the designer was rarely offered are pulled toward their category's weight, so
a few picks don't swing them far. The weights are then scaled to the
pilot's own randomness (the build's Randomness plus the average skill level's PickNoise), so the pilot picks with
about the same spread as the designer did.

Logs from older game versions still count: the designer's preferences change less than the tuning does.

Editor-only testing tool: nothing here is part of the game.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BUILD = os.path.join(ROOT, 'pilots', 'builds', 'designer.tres')
sys.path.insert(0, os.path.join(ROOT, 'arbiter', 'training'))
from fit_choices import Choice, fit as fit_choices  # noqa: E402  Arbiter's training tool, free of VNP's words.


def upgrade_info():
    """Each upgrade's category, effect and weapon, from its file, by ID."""
    info = {}
    for path in glob.glob(os.path.join(ROOT, '**', 'upgrades', '**', '*.tres'), recursive=True):
        text = open(path, encoding='utf-8').read()
        m = re.search(r'^Id = "([^"]+)"', text, re.M)
        if not m:
            continue
        cat = re.search(r'^Category = (\d+)', text, re.M)
        eff = re.search(r'^Effect = (\d+)', text, re.M)
        weapon = re.search(r'^Weapon = ExtResource\("([^"]+)"\)', text, re.M)
        weapon_id = None
        if weapon:
            ext = re.search(r'path="res://([^"]+)" id="' + re.escape(weapon.group(1)) + '"', text)
            if ext:
                weapon_id = os.path.splitext(os.path.basename(ext.group(1)))[0]
        info[m.group(1)] = {'category': int(cat.group(1)) if cat else 0, 'effect': int(eff.group(1)) if eff else 0,
                            'weapon': weapon_id}
    return info


EQUIP, SWITCH_DRONE = 0, 2  # UpgradeEffect values for equipping a weapon and switching the Drone Weapon.
DRONE_ATTACK = {'drone_damage', 'drone_attack_rate'}


def designer_logs():
    """The designer's fight logs: each in its own folder in playtests/human/, or loose there from before."""
    folder = os.path.join(ROOT, 'playtests', 'human')
    return sorted(glob.glob(os.path.join(folder, '*.json')) + glob.glob(os.path.join(folder, '*', '*.json')))


def choices(info):
    """Every answered offer in the designer's logs: (the three IDs, the taken ID, which improve owned things)."""
    out = []
    for path in designer_logs():
        log = json.load(open(path, encoding='utf-8'))
        if log['settings'].get('player') != 'designer':
            continue
        owned = set(log['settings'].get('startingWeapons') or [])
        for lu in log['stats']['levelUps']:
            offer, taken = lu['offers'][-1], lu['taken']
            if taken and taken in offer:
                # As the pilot counts it: an upgrade to an equipped weapon, or to the drones' attack.
                improves = [u in DRONE_ATTACK or (info.get(u, {}).get('weapon') in owned and info[u]['effect'] != EQUIP)
                            for u in offer]
                out.append((offer, taken, improves))
            if taken in info and info[taken]['effect'] == EQUIP and info[taken]['weapon']:
                owned.add(info[taken]['weapon'])
    return out


def as_choices(data, info):
    """The designer's choices in the terms the pilot's upgrade decision scores, for Arbiter's fitter."""
    out = []
    for offer, taken, improves in data:
        options = []
        for i, u in enumerate(offer):
            terms = {f'liking:{u}': 1.0}
            if improves[i]:
                terms['owned'] = 1.0
            if info.get(u, {}).get('effect') in (EQUIP, SWITCH_DRONE):
                terms['new weapon'] = 1.0
            options.append(terms)
        out.append(Choice(options=options, taken=offer.index(taken)))
    return out


def fit(data, info):
    """Each upgrade's liking, each category's, and the owned and new weapon weights."""
    ids = sorted({u for offer, _, _ in data for u in offer})
    # Each upgrade's liking is pulled toward its category's, by the category's number.
    group_of = {f'liking:{u}': info.get(u, {}).get('category', 0) for u in ids}
    weights, groups = fit_choices(as_choices(data, info), group_of)
    w = {u: weights[f'liking:{u}'] for u in ids}
    cat = {c: groups.get(c, 0.0) for c in range(4)}
    return w, cat, weights.get('owned', 0.0), weights.get('new weapon', 0.0)


def main():
    dry = '--dry-run' in sys.argv
    info = upgrade_info()
    data = choices(info)
    if not data:
        sys.exit('No designer fight logs with level ups in playtests/human/.')
    w, cat, owned_bonus, new_bonus = fit(data, info)
    # A softmax spread of 1 in fitted units is about the spread of the pilot's uniform randomness of 3.5 units
    # times its width; scale so the pilot's randomness plays the designer's indecision.
    text = open(BUILD, encoding='utf-8', newline='').read()
    randomness = float(re.search(r'^Randomness = ([\d.]+)', text, re.M).group(1))
    noise = float(re.search(r'^PickNoise = ([\d.]+)', open(os.path.join(ROOT, 'pilots', 'skill_levels', 'average.tres'),
                                                            encoding='utf-8').read(), re.M).group(1))
    scale = (randomness + noise) / 3.5
    # Weights are kept positive by offsetting everything by the lowest; only differences matter to an argmax.
    raw = {u: v for u, v in w.items()}
    low = min(list(raw.values()) + list(cat.values()))
    weights = {u: round((v - low) * scale + 0.1, 2) for u, v in sorted(raw.items(), key=lambda x: -x[1])}
    cats = [round((cat[c] - low) * scale + 0.1, 2) for c in range(4)]
    owned = round(max(0.0, owned_bonus) * scale, 2)
    new = round(new_bonus * scale, 2)
    n_logs = len(designer_logs())
    print(f'{len(data)} choices from {n_logs} designer logs.')
    print(f'Category weights (VNP Weapon, Drone Weapon, Stats, Drones): {cats}')
    print(f'Bonus for improving what it owns: {owned}; for a new weapon: {new}')
    for u, v in weights.items():
        offered = sum(1 for o, _, _ in data if u in o)
        taken = sum(1 for _, t, _ in data if t == u)
        print(f'  {u:26s} {v:5.2f}   taken {taken} of {offered} offers')
    if dry:
        return
    entries = ', '.join(f'"{u}": {v}' for u, v in weights.items())

    def put(key, value):
        nonlocal text
        text, n = re.subn(rf'^{key} = .*$', f'{key} = {value}', text, flags=re.M)
        assert n == 1, key
    put('UpgradeWeights', f'Dictionary[String, float]({{{entries}}})')
    put('VnpWeaponWeight', cats[0])
    put('DroneWeaponWeight', cats[1])
    put('StatsWeight', cats[2])
    put('DronesWeight', cats[3])
    put('FavourOwned', owned)
    put('NewWeaponWeight', new)
    open(BUILD, 'w', encoding='utf-8', newline='').write(text)
    print(f'Wrote {os.path.relpath(BUILD, ROOT)}.')


if __name__ == '__main__':
    main()
