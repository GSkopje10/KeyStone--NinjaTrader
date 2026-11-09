// 🎯 REVERSAL FVG SCANNER (BUILD 2026-11-11i): the detection = the research program (aggfvg/Program.cs) on synthetic bars, NO LOOKAHEAD
// (the bars after a signal scrambled → it stays), the forward tracking, and it never reaches an order path (an armed link sends nothing).
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.NinjaScript;

public sealed class RfBroker : IKeystoneBroker
{
    public List<string> Sent = new List<string>();
    public bool Connected { get { return true; } } public int Qty { get { return 0; } } public double AvgPrice { get { return 0; } } public double DayPnl { get { return 0; } } public double Tick { get { return 0.25; } }
    public List<KeystoneBrokerOrder> Working() { return new List<KeystoneBrokerOrder>(); } public List<KeystoneExec> Executions() { return new List<KeystoneExec>(); }
    public KeystoneBrokerOrder Submit(string kind, int signedQty, double price, string oco, bool automated) { Sent.Add("SUBMIT " + kind); return new KeystoneBrokerOrder { Id = "X" + Sent.Count, Kind = kind }; }
    public void ChangePrice(KeystoneBrokerOrder o, double price) { Sent.Add("CHANGE"); } public void Cancel(KeystoneBrokerOrder o) { Sent.Add("CANCEL"); } public void Flatten() { Sent.Add("FLATTEN"); }
}

public static class ReversalFvgTests
{
    static int failures;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail == "" ? "" : "  → " + detail)); if (!ok) failures++; }

    // the research program's detection, copied (B.T = the bar's OPEN time); its "i <= lastUsed" one-trade-at-a-time filter needs later fills, so it is left out
    sealed class B { public DateTime T; public double O, H, L, C, V; }
    static DateTime Sess(DateTime t) { return t.AddHours(6).Date; }
    static List<string> Reference(List<B> bars, int dir, double D)
    {
        var o = new List<string>();
        for (int i = 45; i < bars.Count; i++)
        {
            var c3 = bars[i]; var c1 = bars[i - 2]; if ((c3.T - c1.T).TotalMinutes != 2) continue;
            bool gap = dir > 0 ? c3.L - c1.H >= 2 : c1.L - c3.H >= 2; if (!gap) continue;
            int ext = -1; double ev = dir > 0 ? double.MaxValue : double.MinValue;
            for (int k = i - 15; k <= i - 1; k++) { if (Sess(bars[k].T) != Sess(c3.T)) continue; if (dir > 0 ? bars[k].L < ev : bars[k].H > ev) { ev = dir > 0 ? bars[k].L : bars[k].H; ext = k; } }
            if (ext < 0) continue; double op = dir > 0 ? double.MinValue : double.MaxValue;
            for (int k = Math.Max(0, ext - 40); k < ext; k++) { if (Sess(bars[k].T) != Sess(c3.T)) continue; op = dir > 0 ? Math.Max(op, bars[k].H) : Math.Min(op, bars[k].L); }
            if (op == double.MinValue || op == double.MaxValue || Math.Abs(op - ev) < D) continue;
            bool first = true; for (int k = ext + 2; k < i; k++) { if (dir > 0 ? bars[k].L - bars[k - 2].H >= 2 : bars[k - 2].L - bars[k].H >= 2) { first = false; break; } } if (!first) continue;
            var c2 = bars[i - 1]; double avgV = 0; int nv = 0; for (int k = i - 30; k < i - 2; k++) { avgV += bars[k].V; nv++; } avgV /= Math.Max(1, nv);
            bool star = c2.V >= 2 * avgV;
            double entry = dir > 0 ? c3.L : c3.H, stop = dir > 0 ? ev - 2 : ev + 2, risk = Math.Abs(entry - stop); if (risk < 4 || risk > 150) continue;
            o.Add(c3.T.AddMinutes(1).ToString("yyyy-MM-dd HH:mm") + "|" + dir + "|" + entry.ToString("0.00") + "|" + stop.ToString("0.00") + "|" + star);
        }
        return o;
    }
    static string KeyOf(KeystoneRevFvgSignal s) { return s.Time.ToString("yyyy-MM-dd HH:mm") + "|" + s.Dir + "|" + s.Entry.ToString("0.00") + "|" + s.Stop.ToString("0.00") + "|" + s.Star; }

    // synthetic MNQ sessions: a random walk with violent drops (≥ 100 pts in < 40 min) followed by sharp rebounds (gaps), and rallies + sell-offs
    static List<KeystoneArcBar> Synth(int seed, int days)
    {
        var rnd = new Random(seed); var l = new List<KeystoneArcBar>(); double p = 20000; var day0 = new DateTime(2025, 3, 3);
        for (int d = 0; d < days; d++)
        {
            var t0 = day0.AddDays(d).AddHours(-6); int phase = 0, left = 0;   // 18:00 → 17:00
            for (int m = 0; m < 23 * 60; m++)
            {
                if (left <= 0) { double u = rnd.NextDouble(); phase = u < 0.05 ? -1 : u < 0.10 ? 1 : u < 0.14 ? 2 : u < 0.18 ? -2 : 0; left = phase == 0 ? 20 + rnd.Next(60) : 8 + rnd.Next(14); }
                left--; double drift = phase == -1 ? -9 : phase == 1 ? 9 : phase == 2 ? 6 : phase == -2 ? -6 : 0;
                double o = p, c = o + drift + (rnd.NextDouble() - 0.5) * 8; if (Math.Abs(drift) >= 6 && rnd.NextDouble() < 0.4) { o += Math.Sign(drift) * (1 + rnd.NextDouble() * 4); c += Math.Sign(drift) * 2; }
                double h = Math.Max(o, c) + rnd.NextDouble() * 3, lo = Math.Min(o, c) - rnd.NextDouble() * 3;
                long v = 200 + rnd.Next(300) + (Math.Abs(drift) >= 6 ? rnd.Next(900) : 0);
                l.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(m + 1), Open = Math.Round(o * 4) / 4, High = Math.Round(h * 4) / 4, Low = Math.Round(lo * 4) / 4, Close = Math.Round(c * 4) / 4, Volume = v });
                p = c;
            }
        }
        foreach (var b in l) { b.High = Math.Max(b.High, Math.Max(b.Open, b.Close)); b.Low = Math.Min(b.Low, Math.Min(b.Open, b.Close)); }
        return l;
    }

    public static int Main()
    {
        // 1. the detection = the research program (both sides, D = 60 / 100 / 150, ★ included)
        int total = 0, stars = 0, mism = 0; string bad = "";
        for (int seed = 1; seed <= 6; seed++)
        {
            var bars = Synth(seed, 12); var rb = bars.Select(b => new B { T = b.Time.AddMinutes(-1), O = b.Open, H = b.High, L = b.Low, C = b.Close, V = b.Volume }).ToList();
            foreach (var D in KeystoneRevFvg.Moves) foreach (var dir in new[] { 1, -1 })
            {
                var mine = KeystoneRevFvg.Scan(bars, D, dir > 0, dir < 0, "MNQ").Select(KeyOf).ToList(); var refs = Reference(rb, dir, D);
                total += mine.Count; stars += mine.Count(x => x.EndsWith("True"));
                if (!mine.SequenceEqual(refs)) { mism++; bad += " seed" + seed + " D" + D + " dir" + dir + " (" + mine.Count + " vs " + refs.Count + ")"; }
            }
        }
        Check(mism == 0 && total >= 40 && stars >= 3, "🎯 detection = the research program on synthetic bars (" + total + " signals, " + stars + " ★, both sides, D 60 / 100 / 150)", bad);

        // 2. NO LOOKAHEAD: every bar after a signal scrambled → every signal up to it stays exactly the same
        int checks = 0, changed = 0; string bad2 = "";
        for (int seed = 11; seed <= 14; seed++)
        {
            var bars = Synth(seed, 8); var sg = KeystoneRevFvg.Scan(bars, 100, true, true, "MNQ");
            foreach (var s in sg.Take(12))
            {
                var rnd = new Random(seed * 31 + checks); var sc = bars.Select(b => new KeystoneArcBar { Symbol = b.Symbol, Time = b.Time, Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = b.Volume }).ToList();
                foreach (var b in sc.Where(b => b.Time > s.Time)) { double o = b.Open + (rnd.NextDouble() - 0.5) * 60, c = o + (rnd.NextDouble() - 0.5) * 40; b.Open = o; b.Close = c; b.High = Math.Max(o, c) + rnd.NextDouble() * 10; b.Low = Math.Min(o, c) - rnd.NextDouble() * 10; b.Volume = rnd.Next(5000); }
                var sg2 = KeystoneRevFvg.Scan(sc, 100, true, true, "MNQ");
                Func<IEnumerable<KeystoneRevFvgSignal>, string> key = l => string.Join(";", l.Where(y => y.Time <= s.Time).Select(y => y.Same()));
                checks++; if (key(sg) != key(sg2)) { changed++; bad2 += " " + seed + "@" + s.Time.ToString("MM-dd HH:mm"); }
            }
        }
        Check(checks >= 20 && changed == 0, "🎯 NO LOOKAHEAD: the bars after a signal scrambled → every signal up to it is the same (" + checks + " scrambles)", bad2);

        // 3. a hand-made BUY: a 120-point drop (high 20100 → low 19980 in 20 minutes), then a bullish gap of 4 pts with a big middle candle → ★
        {
            var t0 = new DateTime(2025, 3, 4, 9, 0, 0); var l = new List<KeystoneArcBar>(); double p = 20100;
            Action<double, double, double, double, long> add = (o, h, lo, c, v) => l.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t0.AddMinutes(l.Count + 1), Open = o, High = h, Low = lo, Close = c, Volume = v });
            for (int k = 0; k < 40; k++) add(p, p + 1, p - 1, p, 300);                    // quiet at 20100 (09:00 – 09:40)
            for (int k = 0; k < 20; k++) { add(p, p + 0.5, p - 6.5, p - 6, 400); p -= 6; }  // the drop to 19980
            add(p, p + 2, p - 0.5, p + 1, 300);                                            // c1: high 19982
            add(p + 1, p + 9, p + 1, p + 8, 1500);                                         // c2: the push (volume 5×)
            add(p + 8, p + 12, p + 6, p + 11, 400);                                        // c3: low 19986 → gap 4 pts
            var sg = KeystoneRevFvg.Scan(l, 100, true, false, "MNQ"); var s = sg.LastOrDefault();
            Check(sg.Count == 1 && s.Dir == 1 && s.Star && s.GapLo == 19982 && s.GapHi == 19986 && s.Entry == 19986 && Math.Abs(s.Stop - (19980 - 0.5 - 2)) < 1e-9 && Math.Abs(s.Move - (20101 - 19979.5)) < 1e-9,
                "🎯 a hand-made BUY: ≥ 100-pt drop → the first bullish gap; zone 19982–19986, entry 19986, stop = the low − 2, ★ (volume ≥ 2× the 30-bar average)", sg.Count + " " + (s == null ? "" : KeyOf(s) + " move " + s.Move));
            Check(KeystoneRevFvg.Scan(l, 150, true, false, "MNQ").Count == 0 && KeystoneRevFvg.Scan(l, 60, true, false, "MNQ").Count == 1 && KeystoneRevFvg.Scan(l, 100, false, true, "MNQ").Count == 0, "🎯 D = 150 needs a bigger move; D = 60 sees it; SELLS only → none");
            // forward: price comes back to 19986, then 1R / 2R / 3R … (risk 8.5): fill 1 tick through, then +3R
            var s0 = s; double r0 = s0.Risk;
            add(19997, 19998, 19985, 19990, 300);                                          // back into the zone, through the entry by a tick → filled
            Action<double> to = px => { var b = l[l.Count - 1]; add(b.Close, Math.Max(b.Close, px) + 0.25, Math.Min(b.Close, px), px, 300); };
            to(19986 + 1.2 * r0); to(19986 + 2.2 * r0); to(19986 + 3.2 * r0);
            KeystoneRevFvg.Track(s0, l, 0.25);
            Check(s0.Returned && s0.Outcome[0] == "WIN" && s0.Outcome[1] == "WIN" && s0.Outcome[2] == "WIN" && Math.Abs(s0.OutR[2] - (3 * r0 - 1) / r0) < 1e-9 && KeystoneRevFvg.Line(s0, DateTime.Now).Contains(",YES,"),
                "🎯 forward log: came back into the zone, 1R / 2R / 3R WIN (R after 1 pt of costs), written to the CSV line", string.Join("/", s0.Outcome) + " " + KeystoneRevFvg.Line(s0, DateTime.Now));
            var pp = KeystoneRevFvg.Proposal(s0, 3); Check(pp.Item1 == 19986 && Math.Abs(pp.Item2 - r0) < 1e-9 && Math.Abs(pp.Item3 - 3 * r0) < 1e-9 && KeystoneRevFvg.Words(s0, 2).Contains("3R"), "🎯 the proposal = the order panel's price / stop pts / target pts (values only)");

            // 4. the scanner never reaches an order path: an ARMED link + a trader, every scan / track / proposal → nothing sent
            var br = new RfBroker(); var link = new KeystoneAccountLink(br, "MNQ") { MaxContracts = 5 }; var tr = KeystoneReplayTrader.LiveStart(l, "MNQ", l[l.Count - 1].Time); tr.Slave = true;
            link.Arm(t0); link.Tick(tr, t0); int before = br.Sent.Count;
            for (int k = 50; k < l.Count; k++) { foreach (var x in KeystoneRevFvg.Scan(l.Take(k).ToList(), 100, true, true, "MNQ")) { KeystoneRevFvg.Track(x, l, 0.25); KeystoneRevFvg.Proposal(x, 2); } link.Tick(tr, t0.AddSeconds(k)); }
            Check(link.Armed && before == 0 && br.Sent.Count == 0 && tr.Position == 0 && tr.Orders.Count == 0, "🎯 the scanner never calls an order path: an ARMED link sends nothing (scan + track + proposal on every bar)", string.Join(" | ", br.Sent));
            Check(KeystoneRevFvg.Research.StartsWith("6 years MNQ: every setup −0.06 R/trade") && KeystoneRevFvg.Research.Contains("★ high-volume +0.18 R at 3R"), "🎯 the honest research line");
        }
        Console.WriteLine(failures == 0 ? "ALL REVERSAL FVG TESTS PASSED" : failures + " REVERSAL FVG TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
