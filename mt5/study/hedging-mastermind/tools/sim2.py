# Ladder v2 research: + TREND confirmation (EMA50 vs EMA200 on M1 closes), + PAIR-OFF (close the worst + the best leg when their sum >= 0),
# + RESCUE (after R legs the basket closes at >= 0). $5k, lots 0.01 per $1,060, spread 0.25, commission 0.001 %/side, 1:500, hedged 50 %, stop-out 50 %.
import sys
sys.path.insert(0, '.'); from sim import load_mgc, load_csv
def run(bars, step=4.0, maxl=10, tp=0.3, trend=False, pair=False, rescue=0, bal0=5000.0, spread=0.25, comm=0.001, oz=100, per=1060.0):
    bal = bal0; legs = []; pend = None; L = 0.01; e50 = e200 = None; k50, k200 = 2 / 51, 2 / 201; prevc = None
    nb = won = lost = so = pairs = 0; low = bal0; held = 0; t0 = None; longest = 0
    def pl(d, p, px): return (px - p) * d * L * oz - spread * L * oz - 2 * comm / 100 * px * L * oz
    for t, o, h, l, c in bars:
        tr = 0 if e200 is None else (1 if (e50 > e200 and c > e200) else -1 if (e50 < e200 and c < e200) else 0)
        path = (o, l, h, c) if c >= o else (o, h, l, c); gap = prevc is not None and abs(o - prevc) > 1.0; prevc = c
        for kx, px in enumerate(path):
            if not legs:
                if pend is None: pend = px; L = max(0.01, round(bal / per * 0.01, 2)); t0 = t
                up, dn = px >= pend + step / 2, px <= pend - step / 2
                if trend: up, dn = up and tr > 0, dn and tr < 0
                if up: legs.append((1, px if gap and kx == 0 else pend + step / 2))
                elif dn: legs.append((-1, px if gap and kx == 0 else pend - step / 2))
                if not legs and trend and tr == 0: pend = px   # no trend: the straddle follows the price
                continue
            hi = max(p for d, p in legs); lo = min(p for d, p in legs); net = sum(d for d, p in legs)
            if len(legs) < maxl and px >= hi + step:
                if not trend or tr >= 0 or net < 0: legs.append((1, px if gap and kx == 0 else hi + step))
            elif len(legs) < maxl and px <= lo - step:
                if not trend or tr <= 0 or net > 0: legs.append((-1, px if gap and kx == 0 else lo - step))
            f = sum(pl(d, p, px) for d, p in legs); low = min(low, bal + f)
            nbuy = sum(1 for d, p in legs if d > 0); ns = len(legs) - nbuy; mg = (abs(nbuy - ns) + 2 * min(nbuy, ns) * 0.5) * L * oz * px / 500
            if mg > 0 and (bal + f) / mg * 100 < 50:
                bal += f; so += 1; nb += 1; lost += 1; legs = []; pend = None
                if bal < 50: return dict(blown=True)
                continue
            if f >= tp / 100 * bal or (rescue and len(legs) >= rescue and f >= 0):
                bal += max(f, 0) if f < tp / 100 * bal else tp / 100 * bal; nb += 1; won += 1; longest = max(longest, t - t0); legs = []; pend = None; continue
            if pair and len(legs) >= 3:
                v = sorted((pl(d, p, px), i) for i, (d, p) in enumerate(legs))
                if v[0][0] + v[-1][0] >= 0 and v[0][0] < 0:
                    bal += v[0][0] + v[-1][0]; pairs += 1; legs = [x for i, x in enumerate(legs) if i not in (v[0][1], v[-1][1])]
        e50 = c if e50 is None else c * k50 + e50 * (1 - k50); e200 = c if e200 is None else c * k200 + e200 * (1 - k200)
    f = sum(pl(d, p, bars[-1][4]) for d, p in legs)
    return dict(blown=False, final=round(bal + f), baskets=nb, won=won, stopouts=so, pairs=pairs, low=round(low), open=round(f), longest_h=round(longest / 36e9, 1) if isinstance(longest, int) else 0)
if __name__ == '__main__':
    from multiprocessing import Pool
    name = sys.argv[1]
    B = [b for b in load_csv('/tmp/claude-0/exp3/08-candles-XAUUSD-M1.csv') if b[0] >= '2026-11-02'] if name == 'nov' else load_mgc('/home/user/keystone--ninjatrader/data/MGC', sys.argv[2].split(','))
    cfg = [dict(step=st, maxl=10, tp=tp, trend=tr, pair=pa, rescue=rs) for st in (4.0, 6.0) for tp in (0.2, 0.35) for tr in (False, True) for pa in (False, True) for rs in (0, 6)]
    def one(c): r = run(B, **c); r.update(c); return r
    with Pool(4) as p: out = p.map(one, cfg)
    import json; json.dump(out, open('v2_' + name + '.json', 'w'))
    for r in sorted(out, key=lambda r: -r.get('final', 0))[:16]: print(r)
    print('blown', sum(1 for r in out if r['blown']), 'of', len(out))
