#!/usr/bin/env python3
"""Builds docs/research/rotation/rotation_entries.html from research/RotationEntries.cs output (rotation_entries.json).
Usage: build.py <rotation_entries.json> <out.html>"""
import json, sys, html

src, out = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
meta = d['meta']; S = [s['name'] for s in meta['sessions']]; SETS = meta['sets']; B = meta['brackets']
E = html.escape
def money(v, signed=True):
    if v is None: return '—'
    s = ('+' if v > 0 else '−' if v < 0 else '') if signed else ('−' if v < 0 else '')
    return s + '$' + format(abs(round(v)), ',')
def pct(v): return '—' if v is None else f"{v:.0f}%"
def cls(v): return 'up' if v and v > 0 else 'down' if v and v < 0 else 'flat'
def br(b): return f"+${B[b][0]:,} / −${B[b][1]:,}"

css = """
:root{--bg:#0b0f17;--panel:#121a26;--card:#182233;--line:#24324a;--text:#e6edf6;--muted:#8796ab;--green:#22c55e;--red:#ef4444;--gold:#f5b942;--cyan:#22d3ee;--violet:#a78bfa}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 'Segoe UI',system-ui,sans-serif}
main{max-width:1180px;margin:0 auto;padding:26px 16px 70px}h1{font-size:28px;margin:0;color:var(--gold)}h2{font-size:18px;margin:34px 0 8px;color:var(--cyan)}h3{font-size:15px;margin:18px 0 6px;color:var(--text)}
p{margin:6px 0}.sub{color:var(--muted)}.verdict{margin:18px 0;padding:16px 18px;border-radius:14px;background:var(--card);border-left:5px solid var(--gold)}
.verdict b{color:var(--gold)}.kpis{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:10px;margin:14px 0}
.kpi{background:var(--card);border-radius:12px;padding:10px 14px}.kpi span{display:block;color:var(--muted);font:700 11px 'Segoe UI';letter-spacing:.05em}.kpi b{font:700 22px Consolas,monospace}
.kpi i{display:block;color:var(--muted);font-size:12px;font-style:normal}
.wrap{overflow-x:auto;background:var(--panel);border-radius:12px;margin:8px 0}table{border-collapse:collapse;width:100%;font:12.5px Consolas,monospace}
th{background:var(--card);color:var(--muted);font:700 11px 'Segoe UI';text-align:left;padding:8px 10px;letter-spacing:.04em;white-space:nowrap}td{padding:6px 10px;border-top:1px solid var(--line);white-space:nowrap}
td.l{white-space:normal;min-width:260px;font-family:'Segoe UI'}.up{color:var(--green)}.down{color:var(--red)}.flat{color:var(--muted)}
.band{position:relative;height:14px;width:220px;background:#0f1622;border-radius:7px}.band i{position:absolute;top:2px;height:10px;border-radius:5px;background:rgba(135,150,171,.45)}
.band b{position:absolute;top:0;width:4px;height:14px;border-radius:2px;background:var(--gold)}.tag{display:inline-block;border-radius:8px;padding:0 8px;font:700 11px 'Segoe UI';color:#fff}
.t-luck{background:#64748b}.t-edge{background:#15803d}.cell{display:block;font-size:11px;color:var(--muted)}.best{outline:2px solid var(--green);outline-offset:-2px}
.note{color:var(--muted);font-size:12.5px}ul{margin:6px 0 6px 18px;padding:0}li{margin:3px 0}
"""
h = []
h.append(f"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Rotation Entries Research</title><style>{css}</style></head><body><main>")
h.append(f"<h1>MNQ + MGC rotation: which entries, which session, which target?</h1>")
h.append(f"<p class='sub'>{meta['days']} days with both instruments • {meta['from']} → {meta['to']} • {meta['entries']:,} entry points • {meta['configs']:,} rule set-ups tested • generated {E(meta.get('generated',''))}</p>")

luck = d.get('propLuck', []); oosL = [x for x in luck if x['period'].startswith('2024')]; isL = [x for x in luck if x['period'].startswith('2020')]
best_is = max(isL, key=lambda x: x['realPass']) if isL else None
best_oos = max(oosL, key=lambda x: x['realPass']) if oosL else None
wf = d['walkForward']; lk = d['luck']
G = d.get('rotationGrid', [])
tight = [g for g in G if g['b'] in (0, 3)]; even = [g for g in G if g['b'] == 2 and g['set'] == 1]; g900 = [g for g in G if g['b'] >= 3]
grid_txt = ""
if G:
    grid_txt = (f"What does change the result is the <b>size and the $ target / stop</b> — see the grid below — not the side picker: with random sides, an even <b>+$1,500 / −$1,500 on 5 + 5</b> passed "
                f"{min(g['pass'] for g in even):.0f}–{max(g['pass'] for g in even):.0f}% of the evaluations (median per cell) in both periods, while tight stops (−$300 / −$500, above all with 10 + 10) passed "
                f"{min(g['pass'] for g in tight):.0f}–{max(g['pass'] for g in tight):.0f}% and lost the most. With a $900 target, {sum(1 for g in g900 if g['net'] <= 0)} of {len(g900)} session × size × stop cells lost money.")
h.append("<div class='verdict'><b>The short answer.</b> No confirmation rule gave a dependable edge for choosing BUY-BUY / SELL-SELL / BUY-SELL / SELL-BUY. "
         f"Picked on 2020–23, the best rules passed up to <b>{best_is['realPass']:.0f}%</b> of evaluations there — the 40%+ you asked for — but on the untouched 2024–26 data they passed "
         f"<b>{min(x['realPass'] for x in oosL):.0f}–{max(x['realPass'] for x in oosL):.0f}%</b>, the same as flipping a coin for the sides at the same moments "
         f"(coin median {min(x['coinPassMed'] for x in oosL):.0f}–{max(x['coinPassMed'] for x in oosL):.0f}%). "
         f"Picking a new best rule every year (walk-forward) made <b>{money(wf['total'])}</b> over {wf['n']} trades. " + grid_txt + "</div>")
h.append("<div class='kpis'>")
def kpi(cap, val, c, sub): h.append(f"<div class='kpi'><span>{E(cap)}</span><b class='{c}'>{E(val)}</b><i>{E(sub)}</i></div>")
kpi("BEST 2020–23 PASS RATE", pct(best_is['realPass']), 'up', 'chosen on these years (in-sample)')
kpi("SAME RULE 2024–26", pct([x for x in oosL if x['label']==best_is['label']][0]['realPass']) if oosL else '—', 'flat', 'coin-flip sides: ' + pct([x for x in oosL if x['label']==best_is['label']][0]['coinPassMed']))
kpi("SEARCH LUCK TEST", f"p {lk['p']:.3f}", 'flat', f"best t {lk['bestT']:.2f} vs luck 95th {lk['p95']:.2f} (2020–23)")
kpi("WALK-FORWARD 2022–26", money(wf['total']), cls(wf['total']), f"{wf['n']} trades, target hit {wf['hit']:.0f}%")
h.append("</div>")

# ---- prop runs with coin bands
h.append("<h2>1 • The best rules in the prop simulation, against coin-flip sides</h2>")
h.append("<p class='note'>5 evaluations (GENERIC 50K: $3,000 target, $2,000 EOD drawdown, 50% consistency), ONE WIN A DAY per account, the next account 2 minutes after a trade. "
         "The coin band: the same entry minutes and sizes, the sides chosen by a coin (40 runs) — what the rule would do with no information. Gold mark = the rule, grey bar = 5th–95th of the coins.</p>")
h.append("<div class='wrap'><table><tr><th>RULE • SESSION • SIZE • TARGET / STOP</th><th>YEARS</th><th>PASS RATE</th><th>COIN BAND (PASS %)</th><th>BETTER THAN COINS?</th><th>NET $</th><th>COIN NET (MEDIAN)</th></tr>")
for x in luck:
    lo, hi, v = x['coinPass5'], x['coinPass95'], x['realPass']; scale = 60.0
    bar = f"<div class='band'><i style='left:{min(lo,scale)/scale*100:.1f}%;width:{max(1,(min(hi,scale)-min(lo,scale)))/scale*100:.1f}%'></i><b style='left:calc({min(v,scale)/scale*100:.1f}% - 2px)'></b></div>"
    sig = x['pPass'] <= 0.05
    tag = f"<span class='tag {'t-edge' if sig else 't-luck'}'>{'yes (p ' + format(x['pPass'],'.2f') + ')' if sig else 'no (p ' + format(x['pPass'],'.2f') + ')'}</span>"
    h.append(f"<tr><td class='l'>{E(x['label'])}</td><td>{E(x['period'])}</td><td class='{'up' if v>=40 else ''}'>{v:.0f}%</td><td>{bar}<span class='cell'>{lo:.0f}–{hi:.0f}% • median {x['coinPassMed']:.0f}%</span></td><td>{tag}</td><td class='{cls(x['realNet'])}'>{money(x['realNet'])}</td><td class='{cls(x['coinNetMed'])}'>{money(x['coinNetMed'])}</td></tr>")
h.append("</table></div><p class='note'>2020–23 is where the rules were picked, so beating the coins there is expected. 2024–26 is the real test: one of six rows clears p 0.05 there, about what six tries give by chance, and its money is still negative.</p>")

# ---- plain rotation grid
grid = d.get('rotationGrid', [])
if grid:
    h.append("<h2>2 • The plain rotation (random sides) by session, size and $ target — what actually moves the result</h2>")
    h.append("<p class='note' style='color:var(--text)'><b>Read this first:</b> the green cells are not a market edge — section 3 shows every side losing a little per trade after costs. They come from the firm's math: here an evaluation costs $100 and a funded account can pay up to $2,000 (90% to you), so a coin that reaches +$3,000 one time in four can still come out ahead, with wide swings (the range in brackets). Pricier evaluations, activation fees or stricter payouts change it — run the PROP SIM with your firms' own rules before trusting any cell.</p>")
    h.append("<p class='note'>The studio's RANDOM ×4 rotation (BB / SS / BS / SB by coin), each cell the median of 20 runs. Pass = evaluations passed of those finished; days at target = account-days that reached the day target; net = payouts − evaluation fees. The $1,500 targets pass in 2 good days, the $900 targets need 3+.</p>")
    for period in ['2024–26', '2020–23']:
        rows = [g for g in grid if g['period'] == period]
        if not rows: continue
        best = max(rows, key=lambda g: g['net'])
        h.append(f"<h3>{period}{' (the test years)' if period.startswith('2024') else ' (the years the rules were picked on)'}</h3><div class='wrap'><table><tr><th>SESSION</th><th>SIZE</th>" + ''.join(f"<th>{E(br(b))}<span class='cell'>break-even hit {d['breakEven'][b]:.0f}%</span></th>" for b in range(len(B))) + "</tr>")
        for s in range(len(S)):
            for st in [0, 1]:
                cells = []
                for b in range(len(B)):
                    g = next((g for g in rows if g['s'] == s and g['set'] == st and g['b'] == b), None)
                    if g is None: cells.append('<td>—</td>'); continue
                    cells.append(f"<td class='{'best' if g is best else ''}'><b class='{cls(g['net'])}'>{money(g['net'])}</b><span class='cell'>pass {g['pass']:.0f}% ({g['passLo']:.0f}–{g['passHi']:.0f}) • at target {g['atTarget']:.0f}%</span></td>")
                h.append(f"<tr><td class='l'>{E(S[s])}</td><td>{E(SETS[st])}</td>{''.join(cells)}</tr>")
        h.append("</table></div>")
        # 1500 vs 900
        def avg(f, bs): v = [f(g) for g in rows if g['b'] in bs]; return sum(v)/len(v) if v else 0
        h.append(f"<p class='note'><b>$1,500 vs $900 ({period}):</b> $1,500 targets — pass {avg(lambda g: g['pass'], [0,1,2]):.0f}% median, net {money(avg(lambda g: g['net'], [0,1,2]))} per run on average; "
                 f"$900 targets — pass {avg(lambda g: g['pass'], [3,4,5]):.0f}%, net {money(avg(lambda g: g['net'], [3,4,5]))}. Best cell: {E(S[best['s']])} • {E(SETS[best['set']])} • {E(br(best['b']))} ({money(best['net'])}, pass {best['pass']:.0f}%).</p>")

# ---- static side map
h.append("<h2>3 • Every session and every side, held to the $ target or stop (all years)</h2>")
h.append("<p class='note'>Each entry point of the session: the trade held to its $ target, $ stop or the session's end. Hit = target reached; avg = $ per trade after costs. A side is only useful if its average is above 0 in most years.</p>")
sm = d['staticMap']
for st in [1, 0]:
    for b in [2, 5]:
        h.append(f"<h3>{E(SETS[st])} • {E(br(b))}</h3><div class='wrap'><table><tr><th>SESSION</th>" + ''.join(f"<th>{p}</th>" for p in ['BB', 'SS', 'BS', 'SB']) + "</tr>")
        for s in range(len(S)):
            cells = []
            for side in ['BB', 'SS', 'BS', 'SB']:
                m = next((m for m in sm if m['s'] == s and m['set'] == st and m['b'] == b and m['side'] == side), None)
                if not m: cells.append('<td>—</td>'); continue
                yrs = m['years']; pos = sum(1 for v in yrs.values() if v > 0)
                cells.append(f"<td><b class='{cls(m['avg'])}'>{money(m['avg'])}</b> / trade<span class='cell'>hit {m['hit']:.0f}% • stop {m['stop']:.0f}% • {pos} of {len(yrs)} years up • n {m['n']:,}</span></td>")
            h.append(f"<tr><td class='l'>{E(S[s])}</td>{''.join(cells)}</tr>")
        h.append("</table></div>")

# ---- search
h.append("<h2>4 • The search for a confirmation</h2>")
h.append("<p class='note'>Rules known at the entry: follow / fade the last 5–120 minutes (each instrument, or only when both agree), one instrument leading the other, buy the stronger / sell the weaker, VWAP side, the overnight move, yesterday, the session so far. Every rule × session × entry time × size × target / stop, ranked on 2020–23 (≥ 120 trades), then judged on 2024–26.</p>")
h.append("<div class='wrap'><table><tr><th>RULE</th><th>2020–23 t</th><th>HIT</th><th>$ / TRADE</th><th>2024–26 t</th><th>HIT</th><th>$ / TRADE</th><th>TRADES</th></tr>")
for x in d['top'][:12]:
    h.append(f"<tr><td class='l'>{E(x['name'])}</td><td>{x['is']['t']:.2f}</td><td>{x['is']['hit']:.0f}%</td><td class='{cls(x['is']['avg'])}'>{money(x['is']['avg'])}</td><td class='{cls(x['oos']['t'])}'>{x['oos']['t']:.2f}</td><td>{x['oos']['hit']:.0f}%</td><td class='{cls(x['oos']['avg'])}'>{money(x['oos']['avg'])}</td><td>{x['oos']['n']}</td></tr>")
h.append("</table></div>")
h.append(f"<p class='note'><b>Luck test:</b> shuffling which day's confirmation goes with which day's outcome and redoing the whole search 60 times gives a best t of {lk['med']:.2f} (median) and {lk['p95']:.2f} (95th) by luck alone; the real best is {lk['bestT']:.2f} (p {lk['p']:.3f}). So something was there in 2020–23 — and it did not carry into 2024–26.</p>")
h.append("<div class='wrap'><table><tr><th>FAMILY</th><th>SET-UPS</th><th>POSITIVE 2020–23</th><th>…AND 2024–26</th><th>THEIR 2024–26 $ / TRADE</th></tr>")
for f in d['families']:
    h.append(f"<tr><td class='l'>{E(f['family'])}</td><td>{f['configs']:,}</td><td>{f['posIs']}</td><td>{f['posBoth']}</td><td class='{cls(f['oosAvgOfPos'])}'>{money(f['oosAvgOfPos'])}</td></tr>")
h.append("</table></div>")
h.append("<h3>Walk-forward: each year, the rule that was best on the years before</h3><div class='wrap'><table><tr><th>YEAR</th><th>RULE CHOSEN</th><th>TRADES</th><th>HIT</th><th>$</th></tr>")
for y in wf['years']:
    h.append(f"<tr><td>{y['year']}</td><td class='l'>{E(y['name'])}</td><td>{y['n']}</td><td>{y['hit']:.0f}%</td><td class='{cls(y['usd'])}'>{money(y['usd'])}</td></tr>")
h.append(f"<tr><td><b>ALL</b></td><td></td><td>{wf['n']}</td><td>{wf['hit']:.0f}%</td><td class='{cls(wf['total'])}'><b>{money(wf['total'])}</b></td></tr></table></div>")

# ---- in the software
h.append("<h2>5 • In Keystone Arc</h2><ul>"
         "<li><b>PROP FIRM SIMULATION ▸ 1 STRATEGY ▸ SIDES</b>: FOLLOW THE LAST 5 MIN (BOTH AGREE), FOLLOW / FADE EACH ITS OWN, and BOTH FOLLOW MNQ (NQ LEADS) are there next to the random and fixed sides, so you can watch them trade.</li>"
         "<li><b>The indicator</b>: with ✋ I TRADE the panel and both charts show the next signal (▲▲ BUY BOTH, ▼▼, ▲▼, ▼▲ — or WAITING when MNQ and MGC disagree), and ▶ TAKE IT enters both legs at once — for firms that do not allow automation. ⚡ AUTO takes it by itself.</li>"
         "<li><b>⚖ COMPARE SESSIONS</b> after any rotation run: the same days in every session, the winner in green — the quickest way to see section 2 on your own dates.</li>"
         "<li>None of the side rules is labelled as an edge, because none showed one on 2024–26.</li></ul>")
h.append("<h2>Method</h2><ul class='note'>"
         "<li>Real 1-minute bars only. A combined trade enters at the next minute's open + 1 tick per leg; each minute the stop is checked first with both legs at their worst of that minute (conservative), the target only at a minute's close; costs per contract (commission + 1 tick each way), 1 more tick on stops.</li>"
         "<li>Rules chosen on Oct 2020 – Dec 2023, judged on Jan 2024 – Oct 2026; shuffle null for the search; walk-forward by year; coin-flip bands for the prop results.</li>"
         "<li>Prop engine = the studio's PROP SIM (KeystonePropSim) with the GENERIC 50K rules; a simulation on history, not a promise.</li></ul>")
h.append("</main></body></html>")
open(out, 'w', encoding='utf-8').write('\n'.join(h))
print('wrote', out, sum(len(x) for x in h), 'chars')
