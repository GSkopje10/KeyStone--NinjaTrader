# a fake MT5 + SpasiXBridge v4 for screenshots: account, 3 symbols, ticks, positions, one pending order, candles, history, deals
import socket, time, math, random, sys, threading
random.seed(7)
NOW = int(time.time()) + 3*3600   # "server time" = UTC+3
SY = {"XAUUSD": (2, 3997.0, 0.01, 100, 1.0, 0.01, "Gold vs US Dollar"), "USTEC": (2, 20150.0, 0.01, 1, 0.01, 0.01, "US Tech 100 Index"), "BTCUSD": (2, 67250.0, 0.01, 1, 0.01, 0.01, "Bitcoin vs US Dollar"), "EURUSD": (5, 1.0832, 0.00001, 100000, 1.0, 0.00001, "Euro vs US Dollar")}
px = {k: v[1] for k, v in SY.items()}
def price(sym, t):
    b = SY[sym][1]; h = (math.sin(t * 12.9898) * 43758.5453) % 1.0
    return b * (1 + 0.0030*math.sin(2*math.pi*t/(3600*9)) + 0.0016*math.sin(2*math.pi*t/5400 + 1) + 0.0007*math.sin(2*math.pi*t/1300) + 0.00025*math.sin(2*math.pi*t/240) + 0.00012*(h - 0.5))
def bars(sym, m, n, end):
    out = []; t0 = end - (end % (m*60)) - (n-1)*m*60
    for i in range(n):
        t = t0 + i*m*60; k = max(6, min(60, m*2)); xs = [price(sym, t + j*m*60.0/k) for j in range(k+1)]
        if t + m*60 > end: xs = [price(sym, t + j*(end - t)/k) for j in range(k+1)]
        out.append((t, xs[0], max(xs), min(xs), xs[-1], int(200 + 600*abs(math.sin(t/977.0)))))
    return out
def fmt(v, d): return ("%." + str(d) + "f") % v
s = socket.socket(); 
for k in range(60):
    try: s.connect(("127.0.0.1", 47123)); break
    except Exception: time.sleep(1)
f = s.makefile("rw", encoding="utf-8", newline="\n")
def send(x): f.write(x + "\n"); f.flush()
send("HELLO|5|Raw Trading Ltd|5120")
XS = {"XAUUSD": "-62.4|28.1|1|3|0|2|0|800.00|3997|1.0|0", "USTEC": "-9.8|2.1|1|5|0|4|0|40.30|20150|0|0", "BTCUSD": "-18|-12|6|5|0|2|0|134.50|67250|1.0|0", "EURUSD": "-6.9|1.2|1|3|0|0|0|216.60|1.0832|0.5|0"}
for k, v in SY.items(): send("SYM|%s|%d|%s|0.01|0.01|100|%s|%s|%s|%s" % (k, v[0], v[3], v[2], v[4], v[5], v[6])); send("SYMX|%s|%s" % (k, XS[k]))
pos = [(3524004628, "XAUUSD", "SELL", 0.20, 3994.56, 0, 3985.0), (3524004629, "XAUUSD", "SELL", 0.10, 3999.10, 4006.0, 0), (3524004630, "XAUUSD", "BUY", 0.30, 3990.20, 3984.0, 4008.0), (3524004640, "BTCUSD", "BUY", 0.05, 66900.0, 0, 0)]
orders = [(3524005232, "XAUUSD", "BUY LIMIT", 0.20, 3988.50, 3982.0, 4000.0)]
def reader():
    for line in f:
        a = line.strip().split("|")
        if a[0] == "BARS":
            sym, m, n = a[1], int(a[2]), int(a[3]); b = bars(sym, m, min(n, 2000), NOW + int(time.time()) - T0); d = SY[sym][0]
            send("BARS|%s|%d|%d\n" % (sym, m, len(b)) + "\n".join("B|%d|%s|%s|%s|%s|%d" % (t, fmt(o, d), fmt(h, d), fmt(l, d), fmt(c, d), v) for t, o, h, l, c, v in b) + "\nBARSEND|%s|%d" % (sym, m))
        elif a[0] == "HIST":
            sym, fr, to = a[1], int(a[2]), int(a[3]); d = SY.get(sym, SY["XAUUSD"])[0]
            wd = time.gmtime(fr).tm_wday
            if wd == 5: send("HIST|%s|%d|%d|0|0\nHISTEND|%s|%d|%d" % (sym, fr, to, sym, fr, to)); continue
            b = bars(sym if sym in SY else "XAUUSD", 1, 1440, to - 1)
            send("HIST|%s|%d|%d|%d|0\n" % (sym, fr, to, len(b)) + "\n".join("H|%d|%s|%s|%s|%s|%d|%d" % (t, fmt(o, d), fmt(h, d), fmt(l, d), fmt(c, d), v, 18 + (t // 60) % 9) for t, o, h, l, c, v in b) + "\nHISTEND|%s|%d|%d" % (sym, fr, to))
        elif a[0] == "HISTT":
            sym, fr, to = a[1], int(a[2]), int(a[3]); sy = sym if sym in SY else "XAUUSD"; d = SY[sy][0]
            if time.gmtime(fr).tm_wday >= 5: send("TT|%s|%d|%d|0|0\nTTEND|%s|%d|%d" % (sym, fr, to, sym, fr, to)); continue
            r = random.Random(fr); out = []; ms = 0
            while ms < (to - fr) * 1000:
                ms += int(r.expovariate(1 / 1400.0)) + 30; t = fr + ms / 1000.0; b = price(sy, t) + (r.random() - 0.5) * 4 * SY[sy][2] * (10 if d == 2 else 1)
                out.append("T|%d|%s|%s" % (ms, fmt(b, d), fmt(b + (18 + r.randint(0, 9)) * SY[sy][2], d)))
            send("TT|%s|%d|%d|%d|0\n" % (sym, fr, to, len(out)) + "\n".join(out) + "\nTTEND|%s|%d|%d" % (sym, fr, to))
        elif a[0] == "FIRST":
            send("FIRST|%s|%d|%d|%d|100000" % (a[1], 1262304000, 1420070400, 1420070400))
        elif a[0] == "DEPTH":
            send("DEPTH|%s|1104537600|100000|1:%d:100000,5:%d:100000,15:%d:100000,30:%d:100000,60:%d:100000,240:%d:30100,1440:1104537600:5400,10080:1104537600:1100,43200:1104537600:250" % (a[1], NOW - 100000*60*7//5, NOW - 500000*60*7//5, NOW - 1500000*60*7//5, NOW - 3000000*60*7//5, NOW - 6000000*60*7//5, NOW - 30100*240*60*7//5))
        elif a[0] == "DEALS":
            out = []; tk = 900000; base = NOW - 25*86400; r = random.Random(3)
            for day in range(24):
                t0 = base + day*86400
                if time.gmtime(t0).tm_wday >= 5: continue
                for j in range(r.randint(1, 4)):
                    side = r.choice(["BUY", "SELL"]); op = 3950 + r.random()*60; pl = round((r.random()-0.42)*180, 2); tk += 2
                    out.append("D|%d|%d|XAUUSD|%s|IN|0.10|%.2f|0.00|-0.35|0.00|%d|0||" % (tk, t0 + 3600*(8+j), side, op, tk))
                    out.append("D|%d|%d|XAUUSD|%s|OUT|0.10|%.2f|%.2f|-0.35|0.00|%d|0|%s|" % (tk+1, t0 + 3600*(8+j) + 1800, "SELL" if side == "BUY" else "BUY", op + pl/10, pl, tk, r.choice(["TP", "SL", "DESKTOP"])))
            send("DEALS|%s|%s|%d\n" % (a[1], a[2], len(out)) + "\n".join(out) + "\nDEALEND")
        elif a[0] in ("ORDER", "CLOSE", "CLOSEALL", "MODIFY", "MODORD", "CANCEL", "CANCELALL", "BE"):
            send("RES|%s|OK|10009|%s: done|1" % (a[1], a[0]))
T0 = int(time.time())
threading.Thread(target=reader, daemon=True).start()
while True:
    srv = NOW + int(time.time()) - T0
    for k in px: px[k] = price(k, srv)
    pl = 0; lines = ["POSS|%d" % len(pos)]
    for p in pos:
        cur = px[p[1]]; d = 1 if p[2] == "BUY" else -1; pr = (cur - p[4]) * d * p[3] * SY[p[1]][3]; pl += pr
        lines.append("P|%d|%s|%s|%.2f|%s|%s|%s|%s|%.2f|0.00|%d|0|" % (p[0], p[1], p[2], p[3], fmt(p[4], SY[p[1]][0]), fmt(p[5], SY[p[1]][0]), fmt(p[6], SY[p[1]][0]), fmt(cur, SY[p[1]][0]), pr, srv - 7200))
    lines.append("POSEND"); lines.append("ORDS|%d" % len(orders))
    for o in orders: lines.append("O|%d|%s|%s|%.2f|%s|%s|%s|%d" % (o[0], o[1], o[2], o[3], fmt(o[4], 2), fmt(o[5], 2), fmt(o[6], 2), srv - 600))
    lines.append("ORDEND")
    send("ACC|5298760|Goran Spasic|ICMarketsSC-MT5-2|USD|15708.30|%.2f|802.46|%.2f|633.00|500|HEDGE|1|%s" % (15708.30 + pl, 15708.30 + pl - 802.46, sys.argv[1] if len(sys.argv) > 1 else "DEMO"))
    send("CAN|1|1|1|5.00|10800|%d|1|1" % srv)
    for k, v in SY.items(): send("TICK|%s|%s|%s|%d" % (k, fmt(px[k], v[0]), fmt(px[k] + 25 * v[2], v[0]), srv * 1000))
    send("\n".join(lines))
    time.sleep(0.2)
