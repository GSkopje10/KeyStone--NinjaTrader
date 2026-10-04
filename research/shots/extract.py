# Reads each screenshot: platform, date, time-axis labels (x → minutes), red arrows (tip x → time), Rithmic data box.
import sys, re, json, os, glob, numpy as np
from PIL import Image
from rapidocr_onnxruntime import RapidOCR
from collections import deque
ocr = RapidOCR()
MON = {m: i+1 for i, m in enumerate(['jan','feb','mar','apr','may','jun','jul','aug','sep','oct','nov','dec'])}

def parse_date(texts):
    joined = ' '.join(t for t, _, _ in texts)
    m = re.search(r'D:\s*(\d{1,2})\.(\d{1,2})\.(\d{2})', joined)
    if m: return '20%s-%02d-%02d' % (m.group(3), int(m.group(2)), int(m.group(1))), 'RITHMIC'
    m = re.search(r'(\d{1,2})-([A-Za-z]{3})-(\d{2})', joined)
    if m and m.group(2).lower() in MON: return '20%s-%02d-%02d' % (m.group(3), MON[m.group(2).lower()], int(m.group(1))), 'NT'
    m = re.search(r'(\d{1,2})\.(\d{1,2})\.(20\d{2})', joined)
    if m: return '%s-%02d-%02d' % (m.group(3), int(m.group(2)), int(m.group(1))), 'NT'
    m = re.search(r'(\d{1,2})/(\d{1,2})/(20\d{2})', joined)
    if m: return '%s-%02d-%02d' % (m.group(3), int(m.group(1)), int(m.group(2))), 'NT'
    return None, None

def axis(texts, H):
    pts = []
    for t, x, y in texts:
        if y < H * 0.85: continue
        for m in re.finditer(r'(\d{2}):(\d{2})\s*(AM|PM)?', t):
            hh, mm, ap = int(m.group(1)), int(m.group(2)), m.group(3)
            if ap == 'PM' and hh < 12: hh += 12
            if ap == 'AM' and hh == 12: hh = 0
            if mm % 5 or hh > 23: continue
            if len(re.findall(r'\d{2}:\d{2}', t)) == 1: pts.append((x, hh * 60 + mm))
    if len(pts) < 3: return None
    xs = np.array([p[0] for p in pts]); ms = np.array([p[1] for p in pts])
    # robust line: drop OCR misreads (e.g. 09:00 → 00:60) by iterating
    for _ in range(3):
        k, b = np.polyfit(xs, ms, 1); res = np.abs(ms - (k * xs + b)); keep = res < max(10, np.median(res) * 3)
        if keep.all(): break
        xs, ms = xs[keep], ms[keep]
        if len(xs) < 3: return None
    return k, b

def arrows(a, plot_h):
    H, W = a.shape[:2]
    red = (a[:, :, 0] > 190) & (a[:, :, 1] < 70) & (a[:, :, 2] < 80)
    red[int(plot_h):, :] = False
    seen = np.zeros_like(red); out = []
    ys, xs = np.nonzero(red)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]: continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; pix = []
        while q:
            y, x = q.popleft(); pix.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < H and 0 <= nx < W and red[ny, nx] and not seen[ny, nx]: seen[ny, nx] = True; q.append((ny, nx))
        p = np.array(pix); h = np.ptp(p[:, 0]) + 1; w = np.ptp(p[:, 1]) + 1
        if len(p) < 25 or w < 10 or h < 8 or w > 140 or h > 140: continue
        fill = len(p) / float(w * h)
        if fill > 0.5: continue   # candles are solid
        # diagonal-ness: correlation of x and y
        c = np.corrcoef(p[:, 0], p[:, 1])[0, 1] if p[:, 0].std() > 0 and p[:, 1].std() > 0 else 0
        if abs(c) < 0.5: continue
        # the head (denser half along the main axis) holds the tip
        cen = p.mean(0); v = np.linalg.svd(p - cen)[2][0]; proj = (p - cen) @ v
        lo, hi = p[proj < 0], p[proj >= 0]
        head = lo if len(lo) > len(hi) else hi; s = -1 if len(lo) > len(hi) else 1
        tip = p[np.argmax(proj * s)]
        out.append({'x': int(tip[1]), 'y': int(tip[0]), 'n': len(p), 'w': int(w), 'h': int(h)})
    return out

rows = []
files = sorted(glob.glob(sys.argv[1] + '/**/*.*', recursive=True))
for f in files:
    if not re.search(r'\.(png|jpe?g)$', f, re.I): continue
    im = Image.open(f).convert('RGB'); a = np.asarray(im).astype(int); H, W = a.shape[:2]
    res, _ = ocr(f)
    texts = [(r[1], sum(p[0] for p in r[0]) / 4, sum(p[1] for p in r[0]) / 4) for r in (res or [])]
    date, plat = parse_date(texts)
    ax = axis(texts, H)
    axis_y = min([y for t, x, y in texts if y >= H * 0.85 and re.search(r'\d{2}:\d{2}', t)] or [H * 0.95])
    arr = arrows(a, axis_y - 8)
    for r in arr:
        if ax: m = ax[0] * r['x'] + ax[1]; r['time'] = '%02d:%02d' % (int(m) // 60, int(m) % 60)
    box = {}
    for t, x, y in texts:
        for k in ('T', 'O', 'H', 'L', 'C'):
            mm = re.match(k + r':\s*([\d.,:APM]+)', t)
            if mm and k not in box: box[k] = mm.group(1)
    rows.append({'file': os.path.relpath(f, sys.argv[1]), 'date': date, 'platform': plat, 'axis': bool(ax), 'min_per_px': round(ax[0], 4) if ax else None, 'arrows': arr, 'box': box})
    print(json.dumps(rows[-1]), flush=True)
