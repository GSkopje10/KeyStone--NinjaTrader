# the order-line labels, read once a minute (every 12th frame), 4 processes; resumable
import sys, os, json
from multiprocessing import Pool
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract import label
from PIL import Image
FR, P1, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
def one(r):
    try:
        im = Image.open('%s/%05d.jpg' % (FR, r['f'])).convert('RGB'); res = []
        for y, p in r['lines']:
            if p is None: continue
            lb = label(im, int(round(y))); res.append((y, p, lb))
        return {'f': r['f'], 'lab': res}
    except Exception as e: return {'f': r['f'], 'err': str(e)}
if __name__ == '__main__':
    rows = [json.loads(l) for l in open(P1)]; rows = [r for r in rows if r.get('fit') and not r.get('err')]; rows.sort(key=lambda r: r['f'])
    pick = [r for r in rows if r['f'] % 12 == 0]
    done = set()
    if os.path.exists(OUT): done = {json.loads(l)['f'] for l in open(OUT)}
    todo = [r for r in pick if r['f'] not in done]
    with Pool(4) as p, open(OUT, 'a') as o:
        for i, x in enumerate(p.imap(one, todo, chunksize=2)):
            o.write(json.dumps(x) + '\n'); o.flush()
            if i % 10 == 0: open(OUT + '.progress', 'w').write('%d / %d label frames (%d%%)\n' % (len(done) + i, len(pick), (len(done) + i) * 100 // max(1, len(pick))))
    open(OUT + '.progress', 'w').write('DONE %d label frames\n' % len(pick))
