# pass 1: every frame -> P&L, OHLC, chart time, price-axis fit, order-line prices (no labels)
import sys, os, re, json, glob
from multiprocessing import Pool
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract import tess, num
from PIL import Image
import numpy as np
def one(path):
    try:
        im=Image.open(path).convert('RGB'); a=np.array(im).astype(int); out={'f':int(os.path.basename(path)[:5])}
        t=tess(im.crop((1050,240,1240,335)),psm=6)
        for k,key in (('Floating','float'),('Today','today'),('Week','week')):
            m=re.search(k+r'[^\d\-]*(-?[\d\.,]+)',t); out[key]=num(m.group(1)) if m else None
        t=tess(im.crop((0,93,420,107))); v=[num(x) for x in re.findall(r'\d{4}[\.,]\d\d',t)]; out['ohlc']=v[-4:]
        t=tess(im.crop((1236,95,1280,560)),psm=6,tsv=True,wl='0123456789.'); pts=[]
        for ln in t.splitlines()[1:]:
            f=ln.split('\t')
            if len(f)<12: continue
            if re.fullmatch(r'\d{4}\.\d\d',f[11].strip()): pts.append((95+(int(f[7])+int(f[9])/2)/3.0,float(f[11])))
        fit=None
        if len(pts)>=4:
            ys=np.array([p[0] for p in pts]); ps=np.array([p[1] for p in pts]); b,c0=np.polyfit(ys,ps,1)
            keep=np.abs(ps-(b*ys+c0))<0.6
            if keep.sum()>=4: b,c0=np.polyfit(ys[keep],ps[keep],1)
            if b<0: fit=(float(b),float(c0))
        out['fit']=fit
        bg=np.median(a[300,220:1230],axis=0); lines=[]
        for y in range(110,552):
            d=np.abs(a[y,220:1230]-bg).sum(1); n=(d>60).sum(); tr=np.abs(np.diff((d>40).astype(int))).sum()
            if n>200 and tr>120: lines.append(y)
        grp=[]
        for y in lines:
            if grp and y-grp[-1][-1]<=2: grp[-1].append(y)
            else: grp.append([y])
        out['lines']=[(float(np.mean(g)), round(fit[0]*np.mean(g)+fit[1],2) if fit else None) for g in grp]
        t=tess(im.crop((700,555,1240,571))); m=re.findall(r'(\d{1,2})\s*No\w\s*(\d\d):(\d\d)',t); out['time']=m[-1] if m else None
        out['row']=tess(im.crop((0,622,1270,640))).strip()
        return out
    except Exception as e: return {'f':int(os.path.basename(path)[:5]),'err':str(e)}
if __name__=='__main__':
    files=sorted(glob.glob(sys.argv[1]+'/*.jpg')); done=set()
    if os.path.exists(sys.argv[2]):
        for l in open(sys.argv[2]): done.add(json.loads(l)['f'])
    todo=[f for f in files if int(os.path.basename(f)[:5]) not in done]
    with Pool(4) as p, open(sys.argv[2],'a') as o:
        for r in p.imap(one,todo,chunksize=8): o.write(json.dumps(r)+'\n'); o.flush()
