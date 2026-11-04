import sys, re, subprocess, json, os
from PIL import Image
import numpy as np
TMP=os.path.dirname(os.path.abspath(__file__))
def tess(img,psm=7,scale=3,tsv=False,wl=None):
    c=img.convert('L'); c=c.resize((max(1,c.width*scale),max(1,c.height*scale)),Image.LANCZOS)
    p=os.path.join(TMP,'_c%d.png'%os.getpid()); c.save(p)
    a=['tesseract',p,'-','--psm',str(psm)]
    if wl: a+=['-c','tessedit_char_whitelist='+wl]
    if tsv: a+=['tsv']
    return subprocess.run(a,capture_output=True,text=True).stdout
def num(s):
    s=s.replace(',','.').replace(' ','')
    m=re.search(r'-?\d+\.\d+',s); return float(m.group()) if m else None
R=re.compile(r'#?(25\d{5})\s*(buy stop|sell stop|buy limit|sell limit|buy|sell)\s*(\d+\.\d\d)')
RT=re.compile(r'#?(25\d{5})\s*(tp|sl)\b')
def label(im,y):
    for x1 in (120,135,150):
        for lo,hi in ((y-13,y+1),(y-15,y),(y-12,y+2),(y-16,y-1)):
            for sc in (3,4):
                t=tess(im.crop((8,max(95,lo),x1,hi)),psm=7,scale=sc,wl='#0123456789.buyselstoplimt ').strip()
                m=R.search(t)
                if m: return m.groups()
                m=RT.search(t)
                if m: return (m.group(1)+'-'+m.group(2), m.group(2), '0.00')
    return None
def frame(path):
    im=Image.open(path).convert('RGB'); a=np.array(im).astype(int); out={}
    # P&L
    t=tess(im.crop((1050,240,1240,335)),psm=6)
    for k,key in (('Floating','float'),('Today','today'),('Week','week')):
        m=re.search(k+r'[^\d\-]*(-?[\d\.,]+)',t); out[key]=num(m.group(1)) if m else None
    # OHLC of the current candle
    t=tess(im.crop((0,93,420,107)))
    v=[num(x) for x in re.findall(r'\d{4}[\.,]\d\d',t)]; out['ohlc']=v[-4:] if len(v)>=4 else v
    # price axis -> y mapping
    t=tess(im.crop((1236,95,1280,560)),psm=6,tsv=True,wl='0123456789.')
    pts=[]
    for ln in t.splitlines()[1:]:
        f=ln.split('\t')
        if len(f)<12: continue
        m=re.fullmatch(r'\d{4}\.\d\d',f[11].strip())
        if m: pts.append((95+(int(f[7])+int(f[9])/2)/3.0,float(m.group())))
    fit=None
    if len(pts)>=4:
        ys=np.array([p[0] for p in pts]); ps=np.array([p[1] for p in pts]); b,c0=np.polyfit(ys,ps,1)
        res=np.abs(ps-(b*ys+c0)); keep=res<max(0.5,np.median(res)*3)
        if keep.sum()>=4: b,c0=np.polyfit(ys[keep],ps[keep],1)
        if b<0: fit=(b,c0)
    # order lines (dashed: many on/off changes) + their labels
    bg=np.median(a[300,220:1230],axis=0); lines=[]
    for y in range(110,552):
        d=np.abs(a[y,220:1230]-bg).sum(1); n=(d>60).sum(); tr=np.abs(np.diff((d>40).astype(int))).sum()
        if n>200 and tr>120: lines.append(y)
    grp=[]
    for y in lines:
        if grp and y-grp[-1][-1]<=2: grp[-1].append(y)
        else: grp.append([y])
    orders=[]
    for g in grp:
        y=int(round(np.mean(g))); lb=label(im,y)
        orders.append({'ticket':lb[0] if lb else None,'type':lb[1] if lb else None,'lots':float(lb[2]) if lb else None,'y':y,'price':round(fit[0]*y+fit[1],2) if fit else None})
    out['orders']=orders
    # first order row and chart time
    out['row']=tess(im.crop((0,622,1270,640)),psm=7).strip()
    t=tess(im.crop((700,555,1240,571)),psm=7); m=re.findall(r'(\d{1,2})\s*No\w\s*(\d\d):(\d\d)',t); out['time']=m[-1] if m else None
    t=tess(im.crop((0,93,420,107))); 
    return out
if __name__=='__main__':
    for p in sys.argv[1:]: print(os.path.basename(p), json.dumps(frame(p)))
