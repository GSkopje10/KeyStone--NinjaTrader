# stitch: every frame's order lines get the label read (once a minute) at the same price nearest in time; a ticket lives from its first to its
# last sighting; fills = the label type changes; REALISED = the jumps of "Today". Output: the events CSV + the tickets table.
import sys, os, json, csv, bisect
P1, LAB, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
rows = sorted((json.loads(l) for l in open(P1)), key=lambda r: r['f']); rows = [r for r in rows if r.get('fit') and not r.get('err')]
labs = {}
for l in open(LAB):
    x = json.loads(l)
    if 'lab' in x: labs[x['f']] = [(p, lb) for y, p, lb in x['lab'] if lb]
lf = sorted(labs)
def vt(f): s = (f - 1) * 5; return '%d:%02d:%02d' % (s // 3600, s // 60 % 60, s % 60)
def find(f, p):
    i = bisect.bisect_left(lf, f); cand = []
    for j in (i - 1, i, i - 2, i + 1):
        if 0 <= j < len(lf) and abs(lf[j] - f) <= 24:
            for q, lb in labs[lf[j]]:
                if abs(q - p) <= 0.08: cand.append((abs(lf[j] - f) + (0 if lf[j] <= f else 0.5), lb))
    return min(cand)[1] if cand else None
raw = []
for r in rows:
    for y, p in r['lines']:
        if p is None: continue
        lb = find(r['f'], p)
        if lb and not lb[0].endswith(('-tp', '-sl')): raw.append((p, r['f'], lb[0], lb[1], float(lb[2])))
# one ORDER = one price (its line never moves): cluster the readings by price, the ticket = the most common reading in the cluster
raw.sort(); groups = []
for x in raw:
    if groups and x[0] - groups[-1][-1][0] <= 0.06: groups[-1].append(x)
    else: groups.append([x])
from collections import Counter
sight = {}
for g in groups:
    t = Counter(x[2] for x in g).most_common(1)[0][0]
    key = t if t not in sight else t + '@' + ('%.2f' % g[0][0])
    sight[key] = [(x[1], x[3], x[0], Counter(y[4] for y in g).most_common(1)[0][0]) for x in g]
byf = {r['f']: r for r in rows}
def ctx(f):
    r = byf.get(f) or min(rows, key=lambda x: abs(x['f'] - f)); c = r.get('ohlc') or []
    return {'video': vt(r['f']), 'chart': ('%s Nov %s:%s' % tuple(r['time'])) if r.get('time') else '', 'bid': c[-1] if c else '', 'floating': r.get('float'), 'today': r.get('today'), 'week': r.get('week'), 'frame': r['f']}
ev = []; tick = []
first_f, last_f = rows[0]['f'], rows[-1]['f']
for t, ss in sight.items():
    if len(ss) < 3: continue   # stray readings
    ss.sort(); typ = max(set(x[1] for x in ss[:6]), key=lambda k: sum(1 for x in ss[:6] if x[1] == k)); f0 = ss[0][0]; lots = ss[0][3]
    ev.append(dict(ctx(f0), event=('ALREADY OPEN ' if f0 <= first_f + 6 else 'NEW ') + typ.upper(), ticket=t, lots=lots, price=round(sum(x[2] for x in ss[:5]) / min(5, len(ss)), 2)))
    cur = typ; fill_f = None
    for i in range(1, len(ss) - 2):
        if ss[i][1] != cur and ss[i + 1][1] == ss[i][1] and ss[i + 2][1] == ss[i][1]:
            ev.append(dict(ctx(ss[i][0]), event='FILLED ' + cur.upper() + ' -> ' + ss[i][1].upper(), ticket=t, lots=lots, price=round(ss[i][2], 2))); cur = ss[i][1]; fill_f = ss[i][0]
    fl = ss[-1][0]
    if fl < last_f - 12: ev.append(dict(ctx(fl + 1), event=('REMOVED ' if ('stop' in cur or 'limit' in cur) else 'GONE (closed) ') + cur.upper(), ticket=t, lots=lots, price=round(ss[-1][2], 2)))
    tick.append({'ticket': t, 'first_type': typ, 'last_type': cur, 'lots': lots, 'price': round(ss[0][2], 2), 'first_seen': vt(f0), 'chart_first': ctx(f0)['chart'], 'filled_at': ctx(fill_f)['chart'] if fill_f else '', 'last_seen': vt(fl), 'chart_last': ctx(fl)['chart'], 'sightings': len(ss), 'floating_first': ctx(f0)['floating'], 'floating_last': ctx(fl)['floating']})
prev = None
for r in rows:
    td = r.get('today')
    if td is None: continue
    if prev is not None and abs(td - prev) > 0.5 and abs(td - prev) < 300000 and (td != 0 or prev != 0):
        nxt = [x.get('today') for x in rows if r['f'] < x['f'] <= r['f'] + 3]
        if all(n is None or abs(n - td) < 0.5 for n in nxt):   # holds for the next frames (not an OCR slip)
            ev.append(dict(ctx(r['f']), event='REALISED %+.2f' % (td - prev), ticket='', lots='', price=''))
            prev = td
        continue
    prev = td
ev.sort(key=lambda e: e['frame'])
keys = ['video', 'chart', 'event', 'ticket', 'lots', 'price', 'bid', 'floating', 'today', 'week', 'frame']
with open(OUT, 'w', newline='') as f:
    w = csv.DictWriter(f, keys, extrasaction='ignore'); w.writeheader(); [w.writerow(e) for e in ev]
with open(OUT.replace('.csv', '_tickets.csv'), 'w', newline='') as f:
    k2 = list(tick[0].keys()) if tick else ['ticket']; w = csv.DictWriter(f, k2); w.writeheader(); [w.writerow(x) for x in sorted(tick, key=lambda x: x['first_seen'])]
print(len(ev), 'events,', len(tick), 'tickets')
