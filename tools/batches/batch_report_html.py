"""
Writes a batch's report as a page with charts, report.html, beside report.md. batch_report.py runs this after
writing report.md; run it yourself to write the page again:

    python tools/batches/batch_report_html.py playtests/batches/<batch folder>

The page holds the same content as report.md, with charts at the top of the sections they illustrate: outcomes,
fights alive over time, level over time, what destroyed the VNP, and damage by source. It is one file with
everything inside it, so it opens straight from the folder in a browser, and Claude can publish it as a private
page for the designer.

Editor-only testing tool: nothing here is part of the game.
"""
import html
import json
import os
import re
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from batch_report import load, combo_label  # noqa: E402

TIME_LIMIT = 300.0  # The fight's 5:00 time limit, marked on the time charts.
STEP = 5            # Seconds between points on the time charts.
MAX_SERIES = 8      # The palette's eight colours; more combinations than this are left off the line charts.


def summarise(batch, logs):
    """Everything the charts need, worked out from the logs, so the page carries no raw logs."""
    variants = batch.get('variants', ['base'])
    show_variant = len(variants) > 1
    groups = {}
    for fight in batch['fights']:
        groups.setdefault(combo_label(fight, show_variant), []).append(fight)
    cap = batch['cap']
    times = list(range(0, int(cap) + 1, STEP))
    combos, sources_total = [], {}
    for label, group in groups.items():
        ls = [logs[f['name']] for f in group if f['name'] in logs]
        outcomes = {'won': 0, 'destroyed': 0, 'capped': 0}
        for l in ls:
            o = l['outcome']
            if o['killTargetTime'] is not None:
                outcomes['won'] += 1
            elif o['result'] == 'loss':
                outcomes['destroyed'] += 1
            else:
                outcomes['capped'] += 1

        def alive_at(l, t):
            o = l['outcome']
            return t <= o['time'] and not (o['result'] == 'loss' and t >= o['time'])

        def level_at(l, t):
            best = None
            for s in l['timeline']:
                if s['time'] > t:
                    break
                best = s
            return best['level'] if best else 1

        alive, level = [], []
        for t in times:
            living = [l for l in ls if alive_at(l, t)]
            alive.append(round(100 * len(living) / len(ls), 1) if ls else None)
            # A median of a handful of survivors jumps about, so the line stops when few are left.
            level.append(statistics.median(level_at(l, t) for l in living)
                         if len(living) >= max(3, len(ls) // 10) else None)

        losses = [l for l in ls if l['outcome']['result'] == 'loss']
        taken = {}
        for l in losses:
            total = sum(l['stats']['damageTaken'].values()) or 1
            for enemy, amount in l['stats']['damageTaken'].items():
                taken[enemy] = taken.get(enemy, 0) + 100 * amount / total / len(losses)
        dealt = {}
        for l in ls:
            total = l['stats']['totalDamage'] or 1
            for d in l['stats']['damage']:
                share = 100 * d['total'] / total / len(ls)
                dealt[d['source']] = dealt.get(d['source'], 0) + share
                sources_total[d['source']] = sources_total.get(d['source'], 0) + share
        win_times = [l['outcome']['killTargetTime'] for l in ls if l['outcome']['killTargetTime'] is not None]
        combos.append({'label': label, 'fights': len(ls), 'outcomes': outcomes, 'alive': alive, 'level': level,
                       'losses': len(losses), 'taken': taken, 'dealt': dealt,
                       'medianWin': statistics.median(win_times) if win_times else None})
    # Sources in order of how much damage they did across the batch, so each keeps its colour on every row.
    sources = sorted(sources_total, key=lambda s: -sources_total[s])
    enemies = sorted({e for c in combos for e in c['taken']})
    return {'times': times, 'cap': cap, 'timeLimit': TIME_LIMIT, 'combos': combos, 'sources': sources,
            'enemies': enemies}


# ───────────── Markdown to HTML, for the report's own text and tables ─────────────

def inline(text):
    text = html.escape(text, quote=False)
    text = re.sub(r'`([^`]+)`', r'<code>\1</code>', text)
    text = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', text)
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<a href="\2">\1</a>', text)
    return text


def markdown_sections(md):
    """The report split into (heading, html) sections. The first section is the header, before any ##."""
    sections, heading, lines = [], None, []
    for line in md.split('\n'):
        if line.startswith('## '):
            sections.append((heading, lines))
            heading, lines = line[3:].strip(), []
        elif line.startswith('# '):
            continue
        else:
            lines.append(line)
    sections.append((heading, lines))

    def render(lines):
        out, i = [], 0
        while i < len(lines):
            line = lines[i]
            if line.startswith('|'):
                rows = []
                while i < len(lines) and lines[i].startswith('|'):
                    rows.append([c.strip() for c in lines[i].strip().strip('|').split('|')])
                    i += 1
                head, body = rows[0], [r for r in rows[2:]]
                cells = ''.join(f'<th>{inline(c)}</th>' for c in head)
                trs = ''.join('<tr>' + ''.join(f'<td>{inline(c)}</td>' for c in r) + '</tr>' for r in body)
                out.append(f'<div class="table-wrap"><table><thead><tr>{cells}</tr></thead><tbody>{trs}</tbody></table></div>')
                continue
            if line.startswith('- '):
                items = []
                while i < len(lines) and lines[i].startswith('- '):
                    items.append(f'<li>{inline(lines[i][2:])}</li>')
                    i += 1
                out.append('<ul>' + ''.join(items) + '</ul>')
                continue
            if line.startswith('```'):
                i += 1
                code = []
                while i < len(lines) and not lines[i].startswith('```'):
                    code.append(html.escape(lines[i]))
                    i += 1
                i += 1
                out.append('<pre><code>' + '\n'.join(code) + '</code></pre>')
                continue
            if line.startswith('### '):
                out.append(f'<h3>{inline(line[4:])}</h3>')
            elif line.strip():
                out.append(f'<p>{inline(line)}</p>')
            i += 1
        return '\n'.join(out)

    return [(h, render(ls)) for h, ls in sections]


# Which charts go at the top of which report section.
CHARTS = {
    'Outcomes': [('outcomes', 'How each fight ended',
                  'Won means it reached the kill target; it then fought on in endless mode until destroyed or the cap.')],
    'Level curve': [('alive', 'Fights still alive',
                     'Share of fights in which the VNP was still alive, by fight time. The dashed line is the 5:00 time limit.'),
                    ('level', 'Level over time',
                     'Median level of the fights still alive. A line stops when fewer than a tenth of its fights are left.')],
    'Losses': [('taken', 'What destroyed the VNP',
                'Share of the damage the VNP took, by enemy type, in fights it lost.')],
    'Matchups': [('dealt', 'Damage by source',
                  'Share of all damage dealt, by weapon and Drone Weapon, averaged over the fights.')],
}


def write_page(folder):
    batch, logs = load(folder)
    md = open(os.path.join(folder, 'report.md'), encoding='utf-8').read()
    data = summarise(batch, logs)
    note = ''
    if len(data['combos']) > MAX_SERIES:
        note = (f'<p class="note">This batch has {len(data["combos"])} combinations; the time charts show the first '
                f'{MAX_SERIES}. The tables below cover them all.</p>')
    body = []
    for heading, content in markdown_sections(md):
        if heading is None:
            body.append(f'<section class="intro">{content}</section>')
            continue
        charts = ''.join(
            f'<figure class="chart" data-chart="{key}"><figcaption><span class="chart-title">{html.escape(title)}</span>'
            f'<span class="chart-note">{html.escape(caption)}</span></figcaption><div class="plot"></div></figure>'
            for key, title, caption in CHARTS.get(heading, []))
        if heading == 'Level curve' and charts:
            charts = note + charts
        slug = re.sub(r'[^a-z0-9]+', '-', heading.lower()).strip('-')
        body.append(f'<section id="{slug}"><h2>{html.escape(heading)}</h2>{charts}{content}</section>')

    page = TEMPLATE.replace('{{TITLE}}', html.escape(f'Batch {batch["id"]}')) \
        .replace('{{ID}}', html.escape(batch['id'])) \
        .replace('{{BODY}}', '\n'.join(body)) \
        .replace('{{DATA}}', json.dumps(data, separators=(',', ':')).replace('</', '<\\/'))
    path = os.path.join(folder, 'report.html')
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(page)
    return path


TEMPLATE = r'''<title>{{TITLE}}</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@500;600&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400&display=swap">
<style>
/* Layout: one reading column of report sections; each chart sits at the top of the section it illustrates. */
:root {
  --bg: #f6f7f9; --surface: #ffffff; --ink: #14161a; --ink-2: #4c5160; --ink-3: #7a8091;
  --rule: #e1e4ea; --grid: #eceef2; --accent: #2a78d6;
  --s1: #2a78d6; --s2: #eb6834; --s3: #1baf7a; --s4: #eda100; --s5: #e87ba4; --s6: #008300; --s7: #6250d6; --s8: #e34948;
  --font-head: "Space Grotesk", "Segoe UI", system-ui, sans-serif;
  --font-body: "Inter", "Segoe UI", system-ui, sans-serif;
  --font-mono: "JetBrains Mono", Consolas, "Courier New", monospace;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --bg: #121417; --surface: #1a1d21; --ink: #eef0f3; --ink-2: #b4b9c4; --ink-3: #868c99;
  --rule: #2c3037; --grid: #24282e; --accent: #3987e5;
  --s1: #3987e5; --s2: #d95926; --s3: #199e70; --s4: #c98500; --s5: #d55181; --s6: #0ca30c; --s7: #9085e9; --s8: #e66767;
  color-scheme: dark; } }
:root[data-theme="dark"] {
  --bg: #121417; --surface: #1a1d21; --ink: #eef0f3; --ink-2: #b4b9c4; --ink-3: #868c99;
  --rule: #2c3037; --grid: #24282e; --accent: #3987e5;
  --s1: #3987e5; --s2: #d95926; --s3: #199e70; --s4: #c98500; --s5: #d55181; --s6: #0ca30c; --s7: #9085e9; --s8: #e66767;
  color-scheme: dark; }
body { background: var(--bg); color: var(--ink); font: 15px/1.55 var(--font-body);
  padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px)); }
main { max-width: 960px; margin: 0 auto; padding-block: 32px 64px; display: flex; flex-direction: column; gap: 40px; }
header { display: flex; flex-direction: column; gap: 4px; }
.eyebrow { font: 500 12px/1 var(--font-body); letter-spacing: .08em; text-transform: uppercase; color: var(--ink-3); }
h1 { font: 600 30px/1.15 var(--font-head); margin: 0; text-wrap: balance; }
h2 { font: 600 21px/1.2 var(--font-head); margin: 0 0 12px; padding-bottom: 8px; border-bottom: 1px solid var(--rule); text-wrap: balance; }
h3 { font: 600 16px/1.3 var(--font-head); margin: 20px 0 8px; }
section { min-width: 0; }
p, ul { margin: 0 0 10px; max-width: 72ch; }
ul { padding-left: 20px; }
li { margin-bottom: 4px; }
code { font: 13px var(--font-mono); background: var(--grid); padding: 1px 5px; border-radius: 4px; }
pre { background: var(--surface); border: 1px solid var(--rule); border-radius: 6px; padding: 10px 12px; overflow-x: auto; }
pre code { background: none; padding: 0; }
.table-wrap { overflow-x: auto; margin: 8px 0 14px; }
table { border-collapse: collapse; font-size: 13.5px; font-variant-numeric: tabular-nums; min-width: 100%; }
th { text-align: left; font-weight: 600; color: var(--ink-2); padding: 6px 10px; border-bottom: 1px solid var(--rule); white-space: nowrap; }
td { padding: 6px 10px; border-bottom: 1px solid var(--grid); vertical-align: top; }
td:not(:first-child) { white-space: nowrap; }
.chart { margin: 0 0 20px; background: var(--surface); border: 1px solid var(--rule); border-radius: 8px; padding: 14px 16px 10px; }
figcaption { display: flex; flex-direction: column; gap: 2px; margin-bottom: 8px; }
.chart-title { font: 600 15px/1.3 var(--font-head); }
.chart-note { font-size: 13px; color: var(--ink-2); }
.note { font-size: 13px; color: var(--ink-2); }
.plot { position: relative; }
.plot svg { display: block; width: 100%; height: auto; overflow: visible; }
.plot text { fill: var(--ink-2); font: 11.5px var(--font-body); font-variant-numeric: tabular-nums; }
.plot .axis-line { stroke: var(--rule); }
.plot .gridline { stroke: var(--grid); }
.plot .limit { stroke: var(--ink-3); stroke-dasharray: 4 4; }
.legend { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 8px; font-size: 12.5px; color: var(--ink-2); }
.legend span { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
.tip { position: absolute; pointer-events: none; background: var(--surface); color: var(--ink); border: 1px solid var(--rule);
  border-radius: 6px; padding: 7px 9px; font-size: 12.5px; line-height: 1.45; box-shadow: 0 4px 14px rgb(0 0 0 / .12);
  white-space: nowrap; font-variant-numeric: tabular-nums; z-index: 5; }
.tip b { font-weight: 600; }
.tip .row { display: flex; align-items: center; gap: 6px; }
.tip i { width: 8px; height: 8px; border-radius: 2px; display: inline-block; }
</style>
<main>
<header><span class="eyebrow">VNP automated playtesting</span><h1>Batch {{ID}}</h1></header>
{{BODY}}
</main>
<script>
const DATA = {{DATA}};
const NS = 'http://www.w3.org/2000/svg';
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const series = i => `var(--s${i % 8 + 1})`;
const clock = s => s == null ? '-' : `${Math.floor(s / 60)}:${String(Math.round(s % 60)).padStart(2, '0')}`;
const esc = s => String(s).replace(/[&<>]/g, c => ({'&': '&amp;', '<': '&lt;', '>': '&gt;'}[c]));
function el(tag, attrs, parent) {
  const e = document.createElementNS(NS, tag);
  for (const k in attrs) e.setAttribute(k, attrs[k]);
  if (parent) parent.appendChild(e);
  return e;
}
function legend(plot, items) {
  const d = document.createElement('div'); d.className = 'legend';
  d.innerHTML = items.map(([name, color]) => `<span><i style="background:${color}"></i>${esc(name)}</span>`).join('');
  plot.appendChild(d);
}
function tipFor(plot) {
  const t = document.createElement('div'); t.className = 'tip'; t.hidden = true; plot.appendChild(t);
  return {
    show(html, x, y) {
      t.innerHTML = html; t.hidden = false;
      const w = plot.clientWidth, tw = t.offsetWidth;
      t.style.left = Math.min(Math.max(0, x + 14), w - tw) + 'px';
      t.style.top = Math.max(0, y - t.offsetHeight - 10) + 'px';
    },
    hide() { t.hidden = true; }
  };
}

// Horizontal stacked bars, one row per combination, each part a share out of 100.
function stacked(plot, rows, parts, fmtTip) {
  const W = 900, labelW = Math.min(330, Math.max(...rows.map(r => r.label.length)) * 6.6 + 12), rowH = 26, gap = 10;
  const H = rows.length * (rowH + gap) + 26;
  const svg = el('svg', {viewBox: `0 0 ${W} ${H}`, role: 'img'}, plot);
  const x0 = labelW, x1 = W - 8, sx = v => x0 + (x1 - x0) * v / 100;
  for (const v of [0, 25, 50, 75, 100]) {
    el('line', {x1: sx(v), x2: sx(v), y1: 0, y2: H - 22, class: 'gridline'}, svg);
    const t = el('text', {x: sx(v), y: H - 6, 'text-anchor': v === 0 ? 'start' : v === 100 ? 'end' : 'middle'}, svg);
    t.textContent = v + '%';
  }
  const tip = tipFor(plot);
  rows.forEach((r, i) => {
    const y = i * (rowH + gap);
    const lt = el('text', {x: 0, y: y + rowH / 2 + 4}, svg); lt.textContent = r.label;
    let acc = 0;
    parts.forEach((p, j) => {
      const v = r.values[j] || 0;
      if (v <= 0) return;
      const w = Math.max(0, sx(acc + v) - sx(acc) - 2);
      const rect = el('rect', {x: sx(acc) + (acc > 0 ? 1 : 0), y, width: w, height: rowH, rx: 3, fill: p.color}, svg);
      rect.addEventListener('mousemove', e => {
        const b = plot.getBoundingClientRect();
        tip.show(fmtTip(r, j), e.clientX - b.left, e.clientY - b.top);
      });
      rect.addEventListener('mouseleave', () => tip.hide());
      acc += v;
    });
  });
  legend(plot, parts.map(p => [p.name, p.color]));
}

// Lines over fight time, one per combination, with a crosshair tooltip.
function lines(plot, list, key, yMax, yFmt, yTicks) {
  const W = 900, H = 300, L = 40, R = 10, T = 8, B = 26;
  const times = DATA.times, cap = DATA.cap;
  const sx = t => L + (W - L - R) * t / cap, sy = v => T + (H - T - B) * (1 - v / yMax);
  const svg = el('svg', {viewBox: `0 0 ${W} ${H}`, role: 'img'}, plot);
  for (const v of yTicks) {
    el('line', {x1: L, x2: W - R, y1: sy(v), y2: sy(v), class: 'gridline'}, svg);
    const t = el('text', {x: L - 6, y: sy(v) + 4, 'text-anchor': 'end'}, svg); t.textContent = yFmt(v);
  }
  for (let m = 0; m <= cap; m += 60) {
    const t = el('text', {x: sx(m), y: H - 6, 'text-anchor': m === 0 ? 'start' : 'middle'}, svg); t.textContent = clock(m);
  }
  el('line', {x1: L, x2: W - R, y1: sy(0), y2: sy(0), class: 'axis-line'}, svg);
  el('line', {x1: sx(DATA.timeLimit), x2: sx(DATA.timeLimit), y1: T, y2: H - B, class: 'limit'}, svg);
  list.forEach((c, i) => {
    let d = '', pen = false;
    c[key].forEach((v, k) => {
      if (v == null) { pen = false; return; }
      d += (pen ? 'L' : 'M') + sx(times[k]).toFixed(1) + ',' + sy(v).toFixed(1); pen = true;
    });
    el('path', {d, fill: 'none', stroke: series(i), 'stroke-width': 2, 'stroke-linejoin': 'round'}, svg);
  });
  const cross = el('line', {y1: T, y2: H - B, stroke: css('--ink-3'), 'stroke-width': 1, visibility: 'hidden'}, svg);
  const dots = list.map((c, i) => el('circle', {r: 4, fill: series(i), stroke: 'var(--surface)', 'stroke-width': 2, visibility: 'hidden'}, svg));
  const hit = el('rect', {x: L, y: T, width: W - L - R, height: H - T - B, fill: 'transparent'}, svg);
  const tip = tipFor(plot);
  hit.addEventListener('mousemove', e => {
    const b = svg.getBoundingClientRect(), scale = W / b.width;
    const t = ((e.clientX - b.left) * scale - L) / (W - L - R) * cap;
    const k = Math.max(0, Math.min(times.length - 1, Math.round(t / (times[1] - times[0]))));
    const x = sx(times[k]);
    cross.setAttribute('x1', x); cross.setAttribute('x2', x); cross.setAttribute('visibility', 'visible');
    const rows = [];
    list.forEach((c, i) => {
      const v = c[key][k];
      if (v == null) { dots[i].setAttribute('visibility', 'hidden'); return; }
      dots[i].setAttribute('cx', x); dots[i].setAttribute('cy', sy(v)); dots[i].setAttribute('visibility', 'visible');
      rows.push([v, `<div class="row"><i style="background:${series(i)}"></i>${esc(c.label)}: <b>${yFmt(v)}</b></div>`]);
    });
    rows.sort((a, b) => b[0] - a[0]);
    const pb = plot.getBoundingClientRect();
    tip.show(`<div><b>${clock(times[k])}</b> fight time</div>` + (rows.map(r => r[1]).join('') || 'No line here'),
      e.clientX - pb.left, e.clientY - pb.top);
  });
  hit.addEventListener('mouseleave', () => { tip.hide(); cross.setAttribute('visibility', 'hidden'); dots.forEach(d => d.setAttribute('visibility', 'hidden')); });
  legend(plot, list.map((c, i) => [c.label, series(i)]));
}

const draw = {
  outcomes(plot) {
    const parts = [{name: 'Won', color: 'var(--s1)'}, {name: 'Destroyed before winning', color: 'var(--s2)'},
                   {name: 'Alive at the cap without winning', color: 'var(--s3)'}];
    const keys = ['won', 'destroyed', 'capped'];
    const rows = DATA.combos.map(c => ({label: c.label, c, values: keys.map(k => c.fights ? 100 * c.outcomes[k] / c.fights : 0)}));
    stacked(plot, rows, parts, (r, j) => `<b>${esc(r.label)}</b><br>${parts[j].name}: <b>${r.c.outcomes[keys[j]]} of ${r.c.fights}</b> (${r.values[j].toFixed(0)}%)` +
      (j === 0 && r.c.medianWin != null ? `<br>Median time to win: ${clock(r.c.medianWin)}` : ''));
  },
  alive(plot) { lines(plot, DATA.combos.slice(0, 8), 'alive', 100, v => Math.round(v) + '%', [0, 25, 50, 75, 100]); },
  level(plot) {
    const top = Math.max(5, ...DATA.combos.flatMap(c => c.level.filter(v => v != null)));
    const yMax = Math.ceil(top / 5) * 5, ticks = [];
    for (let v = 0; v <= yMax; v += 5) ticks.push(v);
    lines(plot, DATA.combos.slice(0, 8), 'level', yMax, v => String(Math.round(v * 10) / 10), ticks);
  },
  taken(plot) {
    const parts = DATA.enemies.map((e, i) => ({name: e.replace(/_/g, ' '), color: series(i)}));
    const rows = DATA.combos.filter(c => c.losses > 0).map(c => ({label: c.label, c, values: DATA.enemies.map(e => c.taken[e] || 0)}));
    if (!rows.length) { plot.innerHTML = '<p class="note">No fight in this batch was lost.</p>'; return; }
    stacked(plot, rows, parts, (r, j) => `<b>${esc(r.label)}</b><br>${parts[j].name}: <b>${r.values[j].toFixed(0)}%</b> of damage taken<br>over ${r.c.losses} lost fight(s)`);
  },
  dealt(plot) {
    // Eight colours: past seven sources, the rest are added together as Other.
    let names = DATA.sources.slice(0, DATA.sources.length > 8 ? 7 : 8);
    const other = DATA.sources.slice(names.length);
    const parts = names.map((s, i) => ({name: s.replace(/_/g, ' '), color: series(i)}));
    if (other.length) parts.push({name: 'Other', color: 'var(--ink-3)'});
    const rows = DATA.combos.map(c => {
      const values = names.map(s => c.dealt[s] || 0);
      if (other.length) values.push(other.reduce((a, s) => a + (c.dealt[s] || 0), 0));
      return {label: c.label, c, values};
    });
    stacked(plot, rows, parts, (r, j) => `<b>${esc(r.label)}</b><br>${parts[j].name}: <b>${r.values[j].toFixed(1)}%</b> of damage dealt`);
  },
};
for (const fig of document.querySelectorAll('[data-chart]')) draw[fig.dataset.chart](fig.querySelector('.plot'));
</script>
'''

if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit('Usage: python tools/batches/batch_report_html.py <batch folder>')
    print(write_page(sys.argv[1]))
