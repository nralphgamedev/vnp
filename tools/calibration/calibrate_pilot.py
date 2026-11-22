"""
Searches a pilot's settings for the values that play most like the designer, unattended (the run review spec's
Automatic calibration). It runs batches itself and needs no one to pick candidates or read results.

    python tools/calibration/calibrate_pilot.py                        search the expert, writing what it finds
    python tools/calibration/calibrate_pilot.py --dry-run              search, but leave the skill level file unchanged
    python tools/calibration/calibrate_pilot.py --passes 2 --params Caution,FightWeight

How it works:
- **The measure** is tools/calibration/compare_to_designer.py's gap: the expert's fights against the designer's at the same
  fight times. Smaller is closer.
- **One setting at a time:** for each setting it plays the same seeds with the setting raised and lowered by a
  step (a factor, or a fixed amount for settings from 0 to 1), as two tuning variants in one batch.
- **Confirming:** a candidate that beats the current gap by at least --min-gain on the search seeds is played
  again, with the current settings, on a second, separate set of seeds. It is kept only if it also wins there, so
  the search doesn't fit to the luck of one set of seeds.
- **Kept changes** are written to the skill level file (unless --dry-run), and the search carries on from them.
  After a full pass with nothing kept, the steps halve; it stops when they get too small or after --passes.
- **The log:** every candidate and decision goes to playtests/calibration/<start time>.md as it happens, so a
  run cut off partway still says what it found. It doesn't commit: whoever runs it does, after reading the log.

Editor-only testing tool: nothing here is part of the game.
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import compare_to_designer  # noqa: E402

# Settings to search, and how: ('scale', factor) multiplies or divides; ('add', amount, low, high) adds or takes
# away within limits. These are the pilot's main judgement settings; others can be named with --params.
SEARCH = {
    'StrengthPerDrone': ('scale', 1.3),
    'AffordableThreat': ('scale', 1.25),
    'StrengthPerLevel': ('scale', 1.3),
    'Caution': ('add', 0.1, 0.0, 1.0),
    'FightWeight': ('scale', 1.25),
    'FightWithoutDrones': ('add', 0.15, 0.0, 1.0),
    'HarvestWeight': ('scale', 1.25),
    'HarvestAtCap': ('add', 0.1, 0.0, 1.0),
    'BackOffWeight': ('scale', 1.25),
    'DodgeWeight': ('scale', 1.25),
    'PreferredEnemyDistance': ('scale', 1.2),
    'HoldStillPressure': ('add', 0.1, 0.1, 1.5),
    'PressureFall': ('scale', 1.5),
    'DroneEaterThreat': ('scale', 1.25),
    'TouchAvoidWeight': ('scale', 1.3),
    'RegroupBelow': ('add', 0.05, 0.0, 0.6),
    'WithdrawMargin': ('scale', 1.4),
}


def number(value):
    """A value as a .tres or variant writes a decimal number: always with a decimal point."""
    text = f'{value:g}'
    return text if ('.' in text or 'e' in text) else text + '.0'


def read_value(level_file, name):
    m = re.search(rf'^{name} = (.+)$', open(level_file, encoding='utf-8').read(), re.M)
    return float(m.group(1)) if m else None


def write_value(level_file, name, value):
    s = open(level_file, encoding='utf-8', newline='').read()
    s, n = re.subn(rf'^{name} = .+$', f'{name} = {number(value)}', s, flags=re.M)
    assert n == 1, name
    open(level_file, 'w', encoding='utf-8', newline='').write(s)


def candidates(name, value, rule, step_scale):
    """The value raised and lowered by one step, as (label, value)."""
    if rule[0] == 'scale':
        factor = 1 + (rule[1] - 1) * step_scale
        return [('up', round(value * factor, 4)), ('down', round(value / factor, 4))]
    amount, low, high = rule[1] * step_scale, rule[2], rule[3]
    out = []
    if value + amount <= high:
        out.append(('up', round(value + amount, 4)))
    if value - amount >= low:
        out.append(('down', round(value - amount, 4)))
    return out


class Search:
    def __init__(self, args):
        self.args = args
        self.level = os.path.join(ROOT, 'pilots', 'skill_levels', args.pilot + '.tres')
        self.res_level = f'pilots/skill_levels/{args.pilot}.tres'
        stamp = datetime.datetime.now().strftime('%Y-%m-%d_%H-%M-%S')
        os.makedirs(os.path.join(ROOT, 'playtests', 'calibration'), exist_ok=True)
        self.log_path = os.path.join(ROOT, 'playtests', 'calibration', f'{stamp}.md')
        self.variants = []

    def log(self, text):
        print(text, flush=True)
        with open(self.log_path, 'a', encoding='utf-8') as f:
            f.write(text + '\n')

    def variant(self, name, settings):
        """Writes a tuning variant setting the pilot's values, and returns its name."""
        lines = [f'# Written by tools/calibration/calibrate_pilot.py; removed when the search ends.']
        lines += [f'{self.res_level}:{k} = {number(v)}' for k, v in settings.items()]
        path = os.path.join(ROOT, 'tools', 'variants', name + '.txt')
        open(path, 'w', encoding='utf-8', newline='').write('\n'.join(lines) + '\n')
        self.variants.append(path)
        return name

    def batch(self, variants, first_seed):
        """Plays the variants on the search seeds and returns {variant: gap}."""
        cmd = [sys.executable, os.path.join(ROOT, 'tools', 'batches', 'batch.py'), '--pilots', self.args.pilot,
               '--builds', self.args.build, '--seeds', str(self.args.seeds), '--first-seed', str(first_seed),
               '--variants', ','.join(variants), '--name', 'autocal']
        out = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
        m = re.search(r'Report: (.+?)[\\/]report\.md', out)
        if not m:
            raise RuntimeError('The batch wrote no report:\n' + out[-2000:])
        folder = m.group(1)
        logs = [json.load(open(os.path.join(folder, 'logs', f), encoding='utf-8'))
                for f in os.listdir(os.path.join(folder, 'logs'))]
        human = compare_to_designer.designer_logs(None, self.args.since)
        designer = compare_to_designer.profile(human)
        gaps = {}
        for v in variants:
            mine = [l for l in logs if (l['settings'].get('tuningVariant') or 'base') == v]
            gaps[v] = compare_to_designer.gap(compare_to_designer.profile(mine), designer)
        return gaps

    def run(self):
        a = self.args
        names = a.params.split(',') if a.params else list(SEARCH)
        values = {n: read_value(self.level, n) for n in names}
        names = [n for n in names if values[n] is not None and n in SEARCH]
        self.log(f'# Calibration search, {datetime.datetime.now():%Y-%m-%d %H:%M}\n')
        self.log(f'Pilot {a.pilot}, build {a.build}, {a.seeds} seeds per candidate (seeds {a.first_seed} on), '
                 f'confirming on seeds {a.confirm_seed} on; designer fights since {a.since or "any time"}; '
                 f'kept only with a gain of at least {a.min_gain}.\n')
        current = self.batch(['base'], a.first_seed)['base']
        self.log(f'Starting gap: {current:.3f}\n')
        step = 1.0
        for p in range(1, a.passes + 1):
            kept_this_pass = 0
            self.log(f'## Pass {p}, step x{step:g}\n')
            for name in names:
                tries = candidates(name, values[name], SEARCH[name], step)
                if not tries:
                    continue
                labels = [self.variant(f'autocal_{name}_{label}', {name: v}) for label, v in tries]
                gaps = self.batch(labels, a.first_seed)
                best_label, best_gap = min(gaps.items(), key=lambda kv: kv[1])
                best_value = dict(zip(labels, [v for _, v in tries]))[best_label]
                line = f'- **{name}** {values[name]:g}: ' + ', '.join(
                    f'{v:g} gives {gaps[l]:.3f}' for l, (_, v) in zip(labels, tries))
                if best_gap > current - a.min_gain:
                    self.log(line + f'; none beats {current:.3f} by {a.min_gain}.')
                    continue
                # Confirm on separate seeds, against the current settings there.
                confirm = self.batch(['base', best_label], a.confirm_seed)
                if confirm[best_label] < confirm['base']:
                    self.log(line + f'; {best_value:g} confirmed on other seeds ({confirm["base"]:.3f} to '
                             f'{confirm[best_label]:.3f}). **Kept.**')
                    values[name] = best_value
                    current = best_gap
                    kept_this_pass += 1
                    if not a.dry_run:
                        write_value(self.level, name, best_value)
                else:
                    self.log(line + f'; {best_value:g} not confirmed on other seeds ({confirm["base"]:.3f} against '
                             f'{confirm[best_label]:.3f}).')
            if kept_this_pass == 0:
                step /= 2
                if step < a.min_step:
                    self.log(f'\nNothing kept in pass {p} and the step is down to x{step:g}: stopping.')
                    break
        self.log(f'\nFinal gap on the search seeds: {current:.3f}. Settings: ' +
                 ', '.join(f'{n} {values[n]:g}' for n in names))

    def cleanup(self):
        for path in self.variants:
            if os.path.exists(path):
                os.remove(path)


def main():
    parser = argparse.ArgumentParser(description='Search a pilot\'s settings to play more like the designer.')
    parser.add_argument('--pilot', default='expert')
    parser.add_argument('--build', default='designer')
    parser.add_argument('--seeds', type=int, default=50)
    parser.add_argument('--first-seed', type=int, default=1)
    parser.add_argument('--confirm-seed', type=int, default=101, help='first seed of the confirmation set')
    parser.add_argument('--since', default=None, help='only the designer\'s fights from this time on, as in compare_to_designer.py')
    parser.add_argument('--params', default=None, help='comma-separated settings to search (default: all known)')
    parser.add_argument('--passes', type=int, default=3)
    parser.add_argument('--min-gain', type=float, default=0.01)
    parser.add_argument('--min-step', type=float, default=0.25)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    search = Search(args)
    try:
        search.run()
    finally:
        search.cleanup()
    print(f'Log: {search.log_path}')


if __name__ == '__main__':
    main()
