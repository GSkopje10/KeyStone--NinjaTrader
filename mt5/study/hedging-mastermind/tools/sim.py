# Draft "Hedging Mastermind" automation (day-1 rules) on M1 candles.
# START: when flat, a straddle: BUY STOP at price + step/2 and SELL STOP at price - step/2.
# LADDER: a new BUY when the price reaches the basket's HIGHEST leg + step, a new SELL at its LOWEST leg - step (never inside the range).
# SIZE: every leg the same lots. EXIT: the whole basket closes when the floating (after costs) >= target % of the balance.
# Optional: basket stop at -stop % of the balance. Costs: spread on every entry, commission % of the notional per side.
import gzip, struct, sys, os, glob, datetime, json
def rstr(b, i):
    n = 0; sh = 0
    while True:
        x = b[i]; i += 1; n |= (x & 0x7f) << sh; sh += 7
        if x < 128: break
    return b[i:i+n].decode(), i + n
def load_mgc(folder, months):
    bars = []
    for f in sorted(glob.glob(folder + '/*.bars.gz')):
        if os.path.basename(f)[:7] not in months: continue
        b = gzip.open(f).read(); s, i = rstr(b, 0)
        days, = struct.unpack_from('<i', b, i); i += 4 + days * 12
        n, = struct.unpack_from('<i', b, i); i += 4
        for k in range(n):
            t, o, h, l, c, v = struct.unpack_from('<q4dq', b, i); i += 48
            bars.append((t, o, h, l, c))
    bars.sort(); return bars
def load_csv(f):
    out = []
    for ln in open(f).read().splitlines()[1:]:
        a = ln.split(','); out.append((a[0], float(a[1]), float(a[2]), float(a[3]), float(a[4])))
    return out
def run(bars, step=2.3, maxl=0, lots=None, per=1060.0, lev=500, hedged=0.5, so=50, target_pct=0.2, stop_pct=0, bal0=5000.0, spread=0.25, comm_pct=0.001, oz=100):
    prevc = None; L = lots; stopouts = 0; bal = bal0; legs = []; pend = None; res = []; peak_dd = 0; worst_float = 0; maxlegs = 0; bstart = None; curve_min = bal0
    def fl(px):
        return sum((px - p) * d * L * oz for d, p in legs) - len(legs) * (spread * L * oz + 2 * comm_pct / 100 * p_ * L * oz)
    for t, o, h, l, c in bars:
        global p_
        p_ = o
        path = (o, l, h, c) if c >= o else (o, h, l, c)
        gap = prevc is not None and abs(o - prevc) > 1.0; prevc = c
        for k, px in enumerate(path):
            jump = gap and k == 0   # a gap from the last candle: stop orders fill AT the new price (slippage), not at their level
            if not legs and pend is None: pend = (px + step / 2, px - step / 2); bstart = t; L = lots if lots else max(0.01, round(bal / per * 0.01, 2))
            if not legs and pend is not None:
                if px >= pend[0]: legs.append((1, pend[0]))
                elif px <= pend[1]: legs.append((-1, pend[1]))
                if legs: pend = None
                continue
            hi = max(p for d, p in legs); lo = min(p for d, p in legs)
            if jump:
                if px >= hi + step and (maxl == 0 or len(legs) < maxl): legs.append((1, px + spread))
                if px <= lo - step and (maxl == 0 or len(legs) < maxl): legs.append((-1, px))
            else:
                while px >= hi + step and (maxl == 0 or len(legs) < maxl): hi += step; legs.append((1, hi))
                while px <= lo - step and (maxl == 0 or len(legs) < maxl): lo -= step; legs.append((-1, lo))
            maxlegs = max(maxlegs, len(legs))
            f = fl(px); worst_float = min(worst_float, f); curve_min = min(curve_min, bal + f)
            nb = sum(1 for d, p in legs if d > 0); ns = len(legs) - nb; mg = (abs(nb - ns) + 2 * min(nb, ns) * hedged) * L * oz * px / lev
            if mg > 0 and (bal + f) / mg * 100 < so:   # the broker's STOP-OUT: everything closes at the market
                bal += f; stopouts += 1; res.append((bstart, t, len(legs), round(f, 2))); legs = []; pend = None
                if bal <= 50: return dict(blown=True, at=t, baskets=len(res))
                continue
            if f >= target_pct / 100 * bal or (stop_pct > 0 and f <= -stop_pct / 100 * bal):
                if f > 0: f = target_pct / 100 * bal   # the take profit fills AT the target (no candle-end overshoot)
                bal += f; res.append((bstart, t, len(legs), round(f, 2))); legs = []; pend = None
                if bal <= 50: return dict(blown=True, at=t, baskets=len(res))
    f = fl(bars[-1][4]) if legs else 0
    wins = [r for r in res if r[3] > 0]
    return dict(final=round(bal, 2), open_float=round(f, 2), baskets=len(res), won=len(wins), worst_basket=min([r[3] for r in res] or [0]), worst_float=round(worst_float, 2), low_equity=round(curve_min, 2), max_legs=maxlegs, blown=bal + f <= 0, stopouts=stopouts, losses=len(res) - len(wins))
if __name__ == '__main__':
    mode = sys.argv[1]
    if mode == 'nov3':
        bars = load_csv('/tmp/claude-0/exp3/08-candles-XAUUSD-M1.csv'); bars = [b for b in bars if b[0] >= '2026-11-03']
    else:
        months = sys.argv[2].split(','); bars = load_mgc('/home/user/keystone--ninjatrader/data/MGC', months)
    out = []
    for step in (1.5, 2.3, 3.0, 4.0):
        for tp in (0.15, 0.25, 0.35):
            for sp in (0, 3.5, 7):
                r = run(bars, step=step, target_pct=tp, stop_pct=sp); r.update(step=step, target=tp, stop=sp); out.append(r)
    json.dump(out, open('res_' + mode + '.json', 'w'))
    for r in sorted(out, key=lambda r: -(r.get('final', 0) + r.get('open_float', 0)))[:12]: print(r)
