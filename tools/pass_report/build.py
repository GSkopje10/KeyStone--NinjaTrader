#!/usr/bin/env python3
"""Builds docs/research/pass/pass_study.html from research/PassStudy.cs output (pass_study.json).
Usage: build.py <pass_study.json> <out.html>"""
import json, sys, html, statistics as st

src, out = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
M = d['meta']; R = d['rows']; C = d['chosen']; BEST = M['best']
PL, BR, DI, SE, FI = M['plans'], M['brackets'], M['dirs'], M['sessions'], M['firms']
E = html.escape
COST = M['cost']

def money(v, signed=True):
    if v is None: return '—'
    s = ('+' if v > 0 else '−' if v < 0 else '') if signed else ''
    return s + '$' + format(abs(round(v)), ',')
def pct(v, dec=1): return '—' if v is None else f"{v:.{dec}f}%"
def cls(v): return 'up' if v and v > 0 else 'down' if v and v < 0 else 'flat'
def pool(rows, per):
    a = sum(r[per]['accounts'] for r in rows); p = sum(r[per]['passed'] for r in rows); t = sum(r[per]['trades'] for r in rows)
    w = sum(r[per]['win'] * r[per]['trades'] / 100 for r in rows); pn = sum(r[per]['pnl'] for r in rows); sp = sum(r[per]['spent'] for r in rows)
    return {'accounts': a, 'passed': p, 'rate': 100 * p / a if a else 0, 'trades': t, 'win': 100 * w / t if t else 0, 'pnl': pn, 'spent': sp}
def name(r): return f"{PL[r['p']]} • {BR[r['b']]} • {DI[r['d']]} • {SE[r['s']]}"

lis, loos = M['luckIs'], M['luckOos']
def band(v): return (v[1], v[len(v) // 2], v[-2]) if v else (0, 0, 0)
li_lo, li_md, li_hi = band(lis); lo_lo, lo_md, lo_hi = band(loos)
best = R[BEST]; bi, bo = best['is'], best['oos']
all_is, all_oos = pool(R, 'is'), pool(R, 'oos')

def bar(v, mx, c='var(--cyan)'):
    w = 0 if mx <= 0 else max(2, 100 * v / mx)
    return f"<span class='bar'><i style='width:{w:.0f}%;background:{c}'></i></span>"

def family(title, labels, key, note):
    rows = []
    for i, lab in enumerate(labels):
        sel = [r for r in R if r[key] == i]
        if not sel: continue
        a, b = pool(sel, 'is'), pool(sel, 'oos')
        bestc = max(sel, key=lambda r: r['oos']['rate'] if r['oos']['accounts'] >= 100 else -1)
        rows.append((lab, a, b, bestc))
    mx = max(max(x[1]['rate'], x[2]['rate']) for x in rows)
    top = max(rows, key=lambda x: x[2]['rate'])
    t = [f"<h2>{E(title)}</h2><p class='note'>{note}</p><div class='wrap'><table><tr><th>{E(title.split(' • ')[0])}</th><th>PASS 2020–23</th><th>PASS 2024–26</th><th></th><th>EVALS 24–26</th><th>WIN % 24–26</th><th>TRADING P/L 24–26</th><th>BEST SINGLE SET-UP 2024–26</th></tr>"]
    for lab, a, b, bc in rows:
        hl = " class='best'" if (lab, a, b, bc) == top else ''
        t.append(f"<tr{hl}><td class='l'><b>{E(lab)}</b></td><td>{pct(a['rate'])}</td><td class='{'up' if b['rate'] > lo_hi else 'flat'}'><b>{pct(b['rate'])}</b></td><td>{bar(b['rate'], mx)}</td><td>{b['accounts']:,}</td><td>{pct(b['win'],0)}</td><td class='{cls(b['pnl'])}'>{money(b['pnl'])}</td><td class='l'>{pct(bc['oos']['rate'])} <span class='cell'>{E(name(bc))}</span></td></tr>")
    t.append("</table></div>")
    return "".join(t), rows

css = """
:root{--bg:#0b0f17;--panel:#121a26;--card:#182233;--line:#24324a;--text:#e6edf6;--muted:#8796ab;--green:#22c55e;--red:#ef4444;--gold:#f5b942;--cyan:#22d3ee;--violet:#a78bfa}
@media (prefers-color-scheme: light){:root:not([data-theme="dark"]){--bg:#f6f8fb;--panel:#ffffff;--card:#eef2f8;--line:#d9e0ea;--text:#152033;--muted:#5b6b82;--green:#15803d;--red:#c62828;--gold:#a16207;--cyan:#0e7490;--violet:#6d28d9}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 'Segoe UI',system-ui,sans-serif}
main{max-width:1180px;margin:0 auto;padding:22px 16px 70px}h1{font-size:26px;margin:0;color:var(--gold)}h2{font-size:18px;margin:26px 0 6px;color:var(--cyan)}
p{margin:6px 0}.sub{color:var(--muted)}.verdict{margin:16px 0;padding:16px 18px;border-radius:14px;background:var(--card);border-left:5px solid var(--gold)}.verdict b{color:var(--gold)}
.kpis{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:10px;margin:14px 0}.kpi{background:var(--card);border-radius:12px;padding:10px 14px}
.kpi span{display:block;color:var(--muted);font:700 11px 'Segoe UI';letter-spacing:.05em}.kpi b{font:700 22px Consolas,monospace}.kpi i{display:block;color:var(--muted);font-size:12px;font-style:normal}
.tabs{display:flex;flex-wrap:wrap;gap:6px;margin:18px 0 6px;position:sticky;top:0;background:var(--bg);padding:8px 0;z-index:2}
.tabs button{background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:10px;padding:7px 12px;font:600 13px 'Segoe UI';cursor:pointer}.tabs button.on{background:var(--gold);color:#111;border-color:var(--gold)}
section{display:none}section.on{display:block}
.wrap{overflow-x:auto;background:var(--panel);border-radius:12px;margin:8px 0}table{border-collapse:collapse;width:100%;font:12.5px Consolas,monospace}
th{background:var(--card);color:var(--muted);font:700 11px 'Segoe UI';text-align:left;padding:8px 10px;letter-spacing:.04em;white-space:nowrap}td{padding:6px 10px;border-top:1px solid var(--line);white-space:nowrap}
td.l{white-space:normal;min-width:220px;font-family:'Segoe UI'}.up{color:var(--green)}.down{color:var(--red)}.flat{color:var(--muted)}.best td{background:rgba(34,197,94,.10)}
.bar{display:inline-block;width:140px;height:10px;background:var(--card);border-radius:5px;overflow:hidden;vertical-align:middle}.bar i{display:block;height:100%;border-radius:5px}
.cell{display:block;font-size:11px;color:var(--muted)}.note{color:var(--muted);font-size:12.5px}ul{margin:6px 0 6px 18px;padding:0}li{margin:4px 0}
.firm{display:inline-block;min-width:170px;background:var(--card);border-radius:10px;padding:8px 12px;margin:0 8px 8px 0}.firm b{display:block;font-size:20px}
"""
h = [f"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Rotation Pass Study</title><style>{css}</style></head><body><main>"]
h.append("<h1>Passing evaluations with the MNQ + MGC rotation</h1>")
h.append(f"<p class='sub'>{M['days']:,} trading days with both instruments • {M['first']} → {M['last']} • batches of {M['batch']} evaluations (5 firms × 10, rotating firm by firm) • {M['qm']} MNQ + {M['qg']} MGC per trade • {len(R)} set-ups • funded accounts ignored</p>")

fam_plan, rows_plan = family("DAY PLAN • one trade a day vs more", PL, 'p', "Each row pools every side choice, target / stop and session with that day plan. ONE TRADE A DAY = each account trades once a day (it waits for tomorrow, win or lose). UNTIL THE DAY TARGET = it keeps trading until it is up its target that day. FULL RECOVERY = every next trade aims to end the day at +target (after −$500 it aims at +$2,000).")
fam_br, rows_br = family("TARGET / STOP • per rotation trade", BR, 'b', "The rotation's $ target and $ stop per trade (both legs together, the fees on top of the target). Pooled over the plans, sides and sessions.")
fam_dir, rows_dir = family("SIDES • which combined directions", DI, 'd', "MNQ side first. Pooled over the plans, targets / stops and sessions. Random rows are the baseline: a side picker only helps if it beats them.")
fam_ses, rows_ses = family("SESSION • when to trade (New York time)", SE, 's', "Pooled over the plans, targets / stops and sides. The rotation trades the accounts one after another inside the window; the day ends at its end.")

bp = max(rows_plan, key=lambda x: x[2]['rate']); bb = max(rows_br, key=lambda x: x[2]['rate']); bd = max(rows_dir, key=lambda x: x[2]['rate']); bs = max(rows_ses, key=lambda x: x[2]['rate'])
rnd_rows = [x for x in rows_dir if 'RANDOM' in x[0].upper()]
rnd_oos = st.mean(x[2]['rate'] for x in rnd_rows) if rnd_rows else 0
edge = bo['rate'] > lo_hi
top_oos = sorted([r for r in R if r['oos']['accounts'] >= 200], key=lambda r: -r['oos']['rate'])[:1]
spent_per_pass = (bo['spent'] / bo['passed']) if bo['passed'] else None

levers = sorted([(n, min(x[2]['rate'] for x in rr), max(x[2]['rate'] for x in rr), max(rr, key=lambda x: x[2]['rate'])[0]) for n, rr in (("the target / stop", rows_br), ("the session", rows_ses), ("the sides", rows_dir), ("the day plan", rows_plan))], key=lambda t: -(t[2] - t[1]))
h.append("<div class='verdict'><b>The short answer.</b> " +
    f"Across all {len(R)} set-ups, {pct(all_oos['rate'])} of the evaluations passed in 2024–26 ({all_oos['passed']:,} of {all_oos['accounts']:,}). "
    f"The set-up chosen on 2020–23 — <b>{E(name(best))}</b> — passed {pct(bi['rate'])} there and <b>{pct(bo['rate'])}</b> on the untouched 2024–26 data; "
    f"the same plan, target / stop and session with <b>random sides</b> passed {pct(lo_lo)}–{pct(lo_hi)} (median {pct(lo_md)}) over 30 seeds. "
    + ("That is <b>above the luck band</b>." if edge else "That is <b>inside the luck band</b>: the choice of sides did not add a dependable edge.") +
    " What moves the 2024–26 pass rate, biggest first: " + "; ".join(f"<b>{n}</b> ({pct(lo_,1)} → {pct(hi_,1)}, best {E(b_)})" for n, lo_, hi_, b_ in levers) + ".</div>")

h.append("<div class='kpis'>")
def kpi(cap, val, c, sub): h.append(f"<div class='kpi'><span>{E(cap)}</span><b class='{c}'>{E(val)}</b><i>{E(sub)}</i></div>")
kpi("CHOSEN SET-UP • 2024–26", pct(bo['rate']), 'up' if edge else 'flat', f"{bo['passed']:,} of {bo['accounts']:,} passed • chosen on 2020–23 ({pct(bi['rate'])})")
kpi("RANDOM SIDES • SAME SET-UP", f"{pct(lo_lo,0)}–{pct(lo_hi,0)}", 'flat', f"30 seeds, 2024–26 • median {pct(lo_md)}")
kpi("PASSES PER BATCH OF 50", f"{50 * bo['rate'] / 100:.1f}", 'up', f"2024–26 • a batch lasts {bo['batchDays']:.0f} trading days on average")
kpi("COST PER PASSED EVALUATION", money(spent_per_pass, False) if spent_per_pass else '—', 'flat', f"${COST:,.0f} an evaluation ÷ the pass rate")
kpi("BEST DAY PLAN 2024–26", bp[0], 'up', f"{pct(bp[2]['rate'])} pooled")
kpi("BEST TARGET / STOP 2024–26", bb[0], 'up', f"{pct(bb[2]['rate'])} pooled")
kpi("BEST SESSION 2024–26", bs[0].split(' ')[0] + ' ' + bs[0].split(' ')[1], 'up', f"{bs[0]} • {pct(bs[2]['rate'])} pooled")
kpi("ALL SET-UPS 2024–26", pct(all_oos['rate']), 'flat', f"{all_oos['accounts']:,} evaluations")
h.append("</div>")

tabs = [("conc", "CONCLUSION"), ("plan", "DAY PLAN"), ("br", "TARGET / STOP"), ("dir", "SIDES"), ("ses", "SESSIONS"), ("firm", "FIRMS"), ("batch", "BATCHES"), ("hour", "HOURS + SIDES TAKEN"), ("all", "ALL SET-UPS"), ("how", "METHOD")]
h.append("<div class='tabs'>" + "".join(f"<button data-t='{k}'>{E(v)}</button>" for k, v in tabs) + "</div>")

# CONCLUSION
pb = PL.index(bp[0]); bbi = BR.index(bb[0]); bsi = SE.index(bs[0])
combo = [r for r in R if r['p'] == pb and r['b'] == bbi and r['s'] == bsi]
cb = max(combo, key=lambda r: r['is']['rate']) if combo else best
c = ["<section id='conc'><h2>Going forward: the rotation used only to pass evaluations</h2><ul>"]
c.append(f"<li><b>Expect about {50 * bo['rate'] / 100:.0f} passes per 50 evaluations</b> with the chosen set-up ({pct(bo['rate'])} on 2024–26), i.e. about {money(spent_per_pass, False) if spent_per_pass else '—'} of evaluation fees per passed account. A batch of 50 is finished (all passed or blown) after about {bo['batchDays']:.0f} trading days.</li>")
c.append(f"<li><b>Day plan:</b> {E(bp[0])} passed the most in 2024–26 ({pct(bp[2]['rate'])} pooled, vs " + ", ".join(f"{E(x[0])} {pct(x[2]['rate'])}" for x in rows_plan if x is not bp) + ").</li>")
c.append(f"<li><b>Target / stop:</b> {E(bb[0])} passed the most ({pct(bb[2]['rate'])}); " + ", ".join(f"{E(x[0])} {pct(x[2]['rate'])}" for x in rows_br if x is not bb) + ".</li>")
c.append(f"<li><b>Session:</b> {E(bs[0])} passed the most ({pct(bs[2]['rate'])}); the weakest was {E(min(rows_ses, key=lambda x: x[2]['rate'])[0])} ({pct(min(x[2]['rate'] for x in rows_ses))}).</li>")
c.append(f"<li><b>Sides:</b> the best pooled side choice in 2024–26 was {E(bd[0])} ({pct(bd[2]['rate'])}) against random sides at {pct(rnd_oos)} — " + ("a real difference worth keeping, but re-check it every quarter." if bd[2]['rate'] > lo_hi else "within luck: pick the sides any way you like (random is as good) and spend the effort on the plan, the target / stop and the session.") + "</li>")
same = [x for x in rows_dir if x[0] in (DI[0], DI[1], DI[4])]; hedg = [x for x in rows_dir if x[0] in (DI[2], DI[3], DI[5])]
if same and hedg:
    sr = 100 * sum(x[2]['passed'] for x in same) / max(1, sum(x[2]['accounts'] for x in same)); hr = 100 * sum(x[2]['passed'] for x in hedg) / max(1, sum(x[2]['accounts'] for x in hedg))
    c.append(f"<li><b>Same direction vs hedged:</b> both legs the same way (BUY-BUY / SELL-SELL) passed {pct(sr)} in 2024–26, hedged (BUY MNQ + SELL MGC / the reverse) {pct(hr)}. MNQ and MGC often move together, so a hedged pair moves less and reaches +$1,500 less often before the stop — keep both legs the same way; which way (buy or sell) is close to a coin.</li>")
c.append(f"<li><b>Firms:</b> rotating A → B → C → D → E keeps every firm at one account in five; the pass rate per firm is the same within luck (same rules) — see FIRMS. Use it to spread the activity, not as an edge.</li>")
c.append(f"<li><b>Set-up to start with:</b> {E(name(cb))} — 2020–23 {pct(cb['is']['rate'])}, 2024–26 {pct(cb['oos']['rate'])} ({cb['oos']['passed']} of {cb['oos']['accounts']}).</li>")
c.append("<li><b>Keep honest:</b> the 2024–26 numbers are the ones to trust (the set-ups were not chosen on them). Results on history are not a promise; trade it small first and compare your real pass rate with these.</li></ul>")
# per-year of chosen
yrs = C.get('years', [])
if yrs:
    c.append("<h2>The chosen set-up, year by year</h2><div class='wrap'><table><tr><th>YEAR</th><th>EVALUATIONS</th><th>PASSED</th><th>PASS RATE</th><th></th></tr>")
    mx = max(100 * p / a for _, p, a in yrs if a) if yrs else 1
    for y, p, a in yrs: c.append(f"<tr><td>{y}</td><td>{a}</td><td>{p}</td><td><b>{pct(100 * p / a if a else 0)}</b></td><td>{bar(100 * p / a if a else 0, mx, 'var(--green)')}</td></tr>")
    c.append("</table></div>")
c.append("</section>"); h.append("".join(c))

h.append(f"<section id='plan'>{fam_plan}</section>")
h.append(f"<section id='br'>{fam_br}</section>")
h.append(f"<section id='dir'>{fam_dir}<p class='note'>Random sides with the chosen plan / target / session over 30 seeds: 2020–23 {pct(li_lo)}–{pct(li_hi)} (median {pct(li_md)}) • 2024–26 {pct(lo_lo)}–{pct(lo_hi)} (median {pct(lo_md)}). A side rule is only better than luck above that band.</p></section>")
h.append(f"<section id='ses'>{fam_ses}</section>")

# FIRMS
f = ["<section id='firm'><h2>Firms (rotating one after another, 10 evaluations each per batch)</h2><p class='note'>Made-up names to track the accounts; the same rules for all five (" + E(M['rules']) + "). The chosen set-up over the whole period:</p>"]
for i, fn in enumerate(FI):
    p, b = C['full']['firms'][i]; rate = 100 * p / (p + b) if p + b else 0
    f.append(f"<div class='firm'><span class='cell'>{E(fn)}</span><b class='up'>{pct(rate)}</b><span class='cell'>{p} passed • {b} blown</span></div>")
f.append("<h2>Every set-up pooled, per firm (2024–26)</h2><div class='wrap'><table><tr><th>FIRM</th><th>PASSED</th><th>BLOWN</th><th>PASS RATE</th></tr>")
for i, fn in enumerate(FI):
    p = sum(r['oos']['firms'][i][0] for r in R); b = sum(r['oos']['firms'][i][1] for r in R)
    f.append(f"<tr><td class='l'>{E(fn)}</td><td>{p:,}</td><td>{b:,}</td><td><b>{pct(100 * p / (p + b) if p + b else 0)}</b></td></tr>")
f.append("</table></div></section>"); h.append("".join(f))

# BATCHES
b = ["<section id='batch'><h2>Every batch of 50 (the chosen set-up, whole period)</h2><p class='note'>A batch starts with 50 new evaluations and ends the day the last one passes or blows; the next batch starts the day after. No evaluation is bought in between.</p><div class='wrap'><table><tr><th>#</th><th>FROM</th><th>TO</th><th>TRADING DAYS</th><th>PASSED</th><th>BLOWN</th><th>PASS RATE</th><th>TRADES</th><th>TRADING P/L</th><th>PASSED PER FIRM (A B C D E)</th></tr>"]
for i, x in enumerate(C['batches']):
    n = x['passed'] + x['blown']; rate = 100 * x['passed'] / n if n else 0
    b.append(f"<tr><td>{i + 1}</td><td>{x['s']}</td><td>{x['e']}{' (open)' if x['open'] else ''}</td><td>{x['days']}</td><td class='up'>{x['passed']}</td><td class='down'>{x['blown']}</td><td><b>{pct(rate, 0)}</b> {bar(rate, 60, 'var(--green)')}</td><td>{x['trades']}</td><td class='{cls(x['pnl'])}'>{money(x['pnl'])}</td><td>{' '.join(str(p) for p, _ in x['firms'])}</td></tr>")
b.append("</table></div></section>"); h.append("".join(b))

# HOURS + SIDES TAKEN
hs = ["<section id='hour'><h2>When the chosen set-up's trades won (entry hour, NY)</h2><div class='wrap'><table><tr><th>HOUR</th><th>TRADES</th><th>WON</th><th>WIN %</th><th>TRADING P/L</th></tr>"]
for hr, n, w, pnl in C['hours']: hs.append(f"<tr><td>{hr:02d}:00</td><td>{n}</td><td>{w}</td><td>{pct(100 * w / n if n else 0, 0)}</td><td class='{cls(pnl)}'>{money(pnl)}</td></tr>")
hs.append("</table></div><h2>The sides it took</h2><div class='wrap'><table><tr><th>SIDES</th><th>TRADES</th><th>WON</th><th>WIN %</th><th>TRADING P/L</th></tr>")
for sd, n, w, pnl in C['sides']: hs.append(f"<tr><td class='l'>{E(sd)}</td><td>{n}</td><td>{w}</td><td>{pct(100 * w / n if n else 0, 0)}</td><td class='{cls(pnl)}'>{money(pnl)}</td></tr>")
hs.append("</table></div></section>"); h.append("".join(hs))

# ALL SET-UPS
def rows_tbl(title, rows):
    t = [f"<h2>{E(title)}</h2><div class='wrap'><table><tr><th>SET-UP</th><th>PASS 2020–23</th><th>PASS 2024–26</th><th>EVALS 24–26</th><th>BATCH DAYS</th><th>DAYS TO PASS</th><th>WIN %</th><th>TRADING P/L 24–26</th></tr>"]
    for r in rows:
        o = r['oos']; t.append(f"<tr{' class=best' if r is best else ''}><td class='l'>{E(name(r))}</td><td>{pct(r['is']['rate'])}</td><td><b class='{'up' if o['rate'] > lo_hi else 'flat'}'>{pct(o['rate'])}</b></td><td>{o['accounts']:,}</td><td>{o['batchDays']:.0f}</td><td>{o['passDays']:.1f}</td><td>{pct(o['win'], 0)}</td><td class='{cls(o['pnl'])}'>{money(o['pnl'])}</td></tr>")
    t.append("</table></div>"); return "".join(t)
ok = [r for r in R if r['is']['accounts'] >= 100]
h.append("<section id='all'>" + rows_tbl("The 40 best set-ups by 2020–23 (how they were chosen) — and what they did in 2024–26", sorted(ok, key=lambda r: -r['is']['rate'])[:40])
         + rows_tbl("The 40 best by 2024–26 (hindsight — not a fair pick, for reference)", sorted(ok, key=lambda r: -r['oos']['rate'])[:40]) + "</section>")

h.append("<section id='how'><h2>How it was measured</h2><ul>"
         f"<li>Real 1-minute MNQ and MGC bars, {M['first']} → {M['last']} ({M['days']:,} days with both). The app's own engine (the PROP SIM's rotation): one trade at a time, the account in turn, MNQ and MGC together with one combined $ target and stop, the stop checked first with both legs at their worst of the minute, targets at a minute's close, commission + 1 tick per contract and side.</li>"
         f"<li>Firm rules for every evaluation: {E(M['rules'])}. An evaluation passes at the target with the consistency rule and the minimum days; it blows at the trailing drawdown. An account too close to its floor to carry a trade is counted as failed.</li>"
         f"<li>Batches: {M['batch']} evaluations bought on the batch's first day ({M['batch']} × ${COST:,.0f}); none bought until every one has passed or blown; then a new batch starts the next trading day. Funded accounts are not traded or counted.</li>"
         "<li>Firm rotation: account 1 = firm A, 2 = B, 3 = C, 4 = D, 5 = E, 6 = A …, and the rotation takes them in that order, so the same firm trades every fifth turn.</li>"
         f"<li>Every combination of {len(PL)} day plans × {len(BR)} targets / stops × {len(DI)} side rules × {len(SE)} sessions ({len(R)} set-ups) was run on 2020-10 → 2023-12 and, separately, on 2024-01 → {M['last']}. The set-up was chosen on the first period only; the second period is the honest test. Random sides on 30 seeds give the luck band.</li>"
         "<li>History is not a promise: fills, slippage on fast days, firm rule details and news days can make real results worse.</li></ul></section>")

h.append("""<script>
const bs=[...document.querySelectorAll('.tabs button')],ss=[...document.querySelectorAll('section')];
function show(k){bs.forEach(b=>b.classList.toggle('on',b.dataset.t===k));ss.forEach(s=>s.classList.toggle('on',s.id===k));try{localStorage.setItem('passTab',k)}catch(e){}}
bs.forEach(b=>b.onclick=()=>show(b.dataset.t));let k0='conc';try{k0=localStorage.getItem('passTab')||'conc'}catch(e){}show(document.getElementById(k0)?k0:'conc');
</script></main></body></html>""")
open(out, 'w', encoding='utf-8').write("".join(h))
print("written", out)
