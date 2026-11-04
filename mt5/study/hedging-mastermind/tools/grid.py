import sys, json, datetime; sys.path.insert(0, '.'); import sim
from multiprocessing import Pool
name = sys.argv[1]; months = sys.argv[2].split(',')
B = sim.load_mgc('/home/user/keystone--ninjatrader/data/MGC', months) if name != 'nov3' else [b for b in sim.load_csv('/tmp/claude-0/exp3/08-candles-XAUUSD-M1.csv') if b[0] >= '2026-11-03']
def one(c):
    st, tp, sp, ml = c; r = sim.run(B, step=st, maxl=ml, target_pct=tp, stop_pct=sp); r.update(step=st, target=tp, stop=sp, maxlegs=ml)
    if 'at' in r and not isinstance(r['at'], str): r['at'] = str(datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=r['at'] // 10))[:10]
    return r
cfg = [(st, tp, sp, ml) for st in (2.3, 4.0, 6.0) for tp in (0.15, 0.35) for sp in (0, 5, 10) for ml in (6, 10)]
with Pool(4) as p: out = p.map(one, cfg)
json.dump(out, open('g_' + name + '.json', 'w')); print(name, 'done', len(out))
