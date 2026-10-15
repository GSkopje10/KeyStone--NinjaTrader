// COMBO STUDY — rotation with CONFIRMED entries: MNQ + MGC traded together on one combined $ target / stop, but entered only when
// an FVG setup confirms (the OPENING FVG rule of the app, run at each session open), not at random times.
//   session      ASIA 20:00–23:55 • LONDON 03:00–06:55 • NY OPEN 09:30–15:55 • NY PM 13:30–15:55 (flat at the end)
//   trigger      MNQ FVG fill • MGC FVG fill • BOTH AGREE (both instruments' first FVG the same way; enter at the later fill)
//   companion    NONE (one leg) • SAME side • OPPOSITE side (hedge) • OWN (the other instrument's own first FVG side)
//   bracket      combined $ target / stop for 10 MNQ + 10 MGC • entry at 10% / 50% of the zone
// Fills: the trigger leg at its limit (the app's engine), the companion at that minute's close; from the next minute the stop is
// checked first on both legs' worst prices of the minute, the target on the minute's close; flat at the session end. Costs both legs.
// Scored on 50 evaluations of LUCID FLEX 50K (rotating, pass first, then funded, then the next batch): payouts − fees.
// CHOSEN on 2020–23, JUDGED on 2024–26.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class ComboStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    public static readonly string[] Sess = { "ASIA 20:00", "LONDON 03:00", "NY OPEN 09:30", "NY PM 13:30" };
    static readonly int[][] SessHm = { new[] { 2000, 2355 }, new[] { 300, 655 }, new[] { 930, 1555 }, new[] { 1330, 1555 } };
    public static readonly string[] Trig = { "MNQ FVG", "MGC FVG", "BOTH AGREE" };
    public static readonly string[] Comp = { "NONE", "SAME", "OPPOSITE", "OWN FVG" };
    static readonly double[][] Br = { new[] { 1000.0, 1000 }, new[] { 1500.0, 1000 }, new[] { 1500.0, 1500 }, new[] { 2000.0, 1500 } };
    static readonly double[] Pcts = { 10, 50 };
    const int QM = 10, QG = 10;

    public sealed class Cfg { public int S, T, C, B, P; public string Name { get { return Sess[S] + " • " + Trig[T] + " • other: " + Comp[C] + " • +$" + Br[B][0].ToString("N0", IC) + " / −$" + Br[B][1].ToString("N0", IC) + " @ " + Pcts[P] + "%"; } } }
    public sealed class Res { public Cfg C; public List<OpenFvgStudy.T> Ts; public OpenFvgStudy.Stats All, A, B; public OpenFvgEdge.Odds PA, PB, PAll; }

    static int Slot(DateTime t) { var o = t.AddMinutes(-1); return R.S(o.Hour, o.Minute); }

    // one day, one config → the combined trade's $ (null = no trade)
    static double? Day(RDay m, RDay g, KeystoneOpenFvgDay fm, KeystoneOpenFvgDay fg, Cfg c, int exitSlot)
    {
        int trigDir = 0, comDir = 0, k0 = -1; double eM = double.NaN, eG = double.NaN;
        bool mOk = fm != null && fm.Filled, gOk = fg != null && fg.Filled;
        if (c.T == 0) { if (!mOk) return null; trigDir = fm.Dir; k0 = Slot(fm.FillTime); eM = fm.Entry; }
        else if (c.T == 1) { if (!gOk) return null; trigDir = fg.Dir; k0 = Slot(fg.FillTime); eG = fg.Entry; }
        else
        {
            if (!mOk || !gOk || fm.Dir != fg.Dir) return null; trigDir = fm.Dir;
            int km = Slot(fm.FillTime), kg = Slot(fg.FillTime); k0 = Math.Max(km, kg);
            eM = km >= kg ? fm.Entry : m.CloseAt(k0); eG = kg >= km ? fg.Entry : g.CloseAt(k0);
        }
        if (k0 < 0 || k0 >= exitSlot) return null;
        int dM, dG;
        if (c.T == 2) { dM = dG = trigDir; }
        else
        {
            int other = c.C == 0 ? 0 : c.C == 1 ? trigDir : c.C == 2 ? -trigDir : 0;
            if (c.C == 3) { var fo = c.T == 0 ? fg : fm; other = fo != null && fo.Dir != 0 && fo.SetupTime <= (c.T == 0 ? fm.FillTime : fg.FillTime) ? fo.Dir : 0; }
            if (c.T == 0) { dM = trigDir; dG = other; } else { dG = trigDir; dM = other; }
        }
        if (dM != 0 && double.IsNaN(eM)) eM = m.CloseAt(k0);
        if (dG != 0 && double.IsNaN(eG)) eG = g.CloseAt(k0);
        if ((dM != 0 && double.IsNaN(eM)) || (dG != 0 && double.IsNaN(eG))) return null;
        double pvM = 2 * QM, pvG = 10 * QG, tgt = Br[c.B][0], stp = Br[c.B][1];
        double cost = (dM != 0 ? KeystoneMoveStudy.Cost("MNQ") * QM : 0) + (dG != 0 ? KeystoneMoveStudy.Cost("MGC") * QG : 0);
        double last = 0;
        for (int k = k0 + 1; k <= exitSlot; k++)
        {
            bool hm = dM == 0 || m.Has(k), hg = dG == 0 || g.Has(k); if (!hm || !hg) continue;
            double wM = dM == 0 ? 0 : dM * ((dM > 0 ? m.L[k] : m.H[k]) - eM) * pvM, wG = dG == 0 ? 0 : dG * ((dG > 0 ? g.L[k] : g.H[k]) - eG) * pvG;
            if (wM + wG <= -stp) return -stp - cost;
            double cM = dM == 0 ? 0 : dM * (m.C[k] - eM) * pvM, cG = dG == 0 ? 0 : dG * (g.C[k] - eG) * pvG; last = cM + cG;
            if (last >= tgt) return last - cost;
        }
        return last - cost;
    }

    public static void Run(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var gm = mgc.ToDictionary(d => d.Day.Date, d => d);
        var pairs = mnq.Where(d => gm.ContainsKey(d.Day.Date)).OrderBy(d => d.Day).Select(d => Tuple.Create(d, gm[d.Day.Date])).ToList();
        var rules = KeystoneSimRules.Presets()[1].Copy(); var baseR = KeystoneOpeningRange.Presets().First(x => x.Mode == "FVG");
        say("COMBO • " + pairs[0].Item1.Day.ToString("yyyy-MM-dd") + " → " + pairs[pairs.Count - 1].Item1.Day.ToString("yyyy-MM-dd") + " • " + pairs.Count + " days with MNQ + MGC");
        // the FVG of each instrument, each session, each entry % (fills do not depend on the bracket)
        var fv = new KeystoneOpenFvgDay[pairs.Count, 4, 2, 2];   // day, session, instrument (0 MNQ / 1 MGC), pct
        for (int i = 0; i < pairs.Count; i++)
        {
            var bm = OpenFvgStudy.BarsOf(pairs[i].Item1); var bg = OpenFvgStudy.BarsOf(pairs[i].Item2); bg.ForEach(b => b.Symbol = "MGC");
            for (int s = 0; s < 4; s++) for (int p = 0; p < 2; p++)
            {
                var r = baseR.Copy(); r.OpenHhmm = SessHm[s][0]; r.ExitHhmm = SessHm[s][1]; r.FvgPct = Pcts[p]; r.StopUsd = 1e9; r.TargetUsd = 1e9; r.Symbols = "BOTH";
                fv[i, s, 0, p] = KeystoneOpenFvg.Run(bm, r, "MNQ"); fv[i, s, 1, p] = KeystoneOpenFvg.Run(bg, r, "MGC");
            }
        }
        say("FVGs found in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var res = new List<Res>();
        for (int s = 0; s < 4; s++) for (int tr = 0; tr < 3; tr++) for (int c = 0; c < 4; c++) for (int b = 0; b < Br.Length; b++) for (int p = 0; p < 2; p++)
        {
            if (tr == 2 && c != 0) continue;   // BOTH AGREE: both legs the same side already
            var cfg = new Cfg { S = s, T = tr, C = c, B = b, P = p }; int exitSlot = R.S(SessHm[s][1] / 100, SessHm[s][1] % 100);
            var ts = new List<OpenFvgStudy.T>();
            for (int i = 0; i < pairs.Count; i++)
            {
                var pnl = Day(pairs[i].Item1, pairs[i].Item2, fv[i, s, 0, p], fv[i, s, 1, p], cfg, exitSlot);
                var d = new KeystoneOpenFvgDay { Day = pairs[i].Item1.Day.Date }; if (pnl.HasValue) { d.Filled = true; d.Dir = 1; d.Pnl = pnl.Value; d.Reason = pnl.Value > 0 ? "WIN" : "LOSS"; }
                ts.Add(new OpenFvgStudy.T { Day = pairs[i].Item1.Day.Date, D = d });
            }
            var rr = new Res { C = cfg, Ts = ts, All = OpenFvgStudy.Of(ts), A = OpenFvgStudy.Of(ts.Where(x => x.Day < Half)), B = OpenFvgStudy.Of(ts.Where(x => x.Day >= Half)) };
            var tA = ts.Where(x => x.Day < Half).ToList(); var tB = ts.Where(x => x.Day >= Half).ToList();
            rr.PA = OpenFvgEdge.Prop(cfg.Name, tA, tA, rules, 50, false); rr.PB = OpenFvgEdge.Prop(cfg.Name, tB, tB, rules, 50, false); rr.PAll = OpenFvgEdge.Prop(cfg.Name, ts, ts, rules, 50, false);
            res.Add(rr);
        }
        say(res.Count + " combinations in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        // chosen on 2020–23 (prop net), judged on 2024–26
        var picks = res.Where(x => x.All.Fills >= 150).OrderByDescending(x => x.PA.Net).Take(25).ToList();
        foreach (var x in picks.Take(10)) say("  " + x.C.Name + " • A prop " + x.PA.Net.ToString("N0", IC) + " → B prop " + x.PB.Net.ToString("N0", IC) + " (pass " + (100 * x.PB.PassRate).ToString("0") + "%, paid " + (100 * x.PB.PayoutRate).ToString("0") + "%) • live A " + x.A.Pnl.ToString("N0", IC) + " B " + x.B.Pnl.ToString("N0", IC));
        var baseline = res.First(x => x.C.S == 2 && x.C.T == 0 && x.C.C == 0 && x.C.B == 3 && x.C.P == 0);   // OPENING FVG MNQ alone (+$2,000 / −$1,500 @ 10% on 10 MNQ)
        var winner = picks.Where(x => x.PB.Net > 0 && x.B.Pnl > 0).OrderByDescending(x => x.PA.Net).FirstOrDefault() ?? picks[0];

        // ---- HTML
        var h = new StringBuilder();
        Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC);
        Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> pct = v => v.ToString("0.0", IC) + "%";
        h.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Combo Rotation Study</title><style>");
        h.Append(":root{--bg:#0d1117;--card:#161b22;--line:#30363d;--text:#e6edf3;--mute:#8b949e;--pos:#3fb950;--neg:#f85149;--acc:#d29922;--pbg:rgba(63,185,80,.16);--nbg:rgba(248,81,73,.16)}");
        h.Append("@media (prefers-color-scheme: light){:root{--bg:#f6f8fa;--card:#fff;--line:#d0d7de;--text:#1f2328;--mute:#59636e;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--pbg:rgba(26,127,55,.12);--nbg:rgba(207,34,46,.12)}}");
        h.Append("body{background:var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:24px 16px}main{max-width:1250px;margin:0 auto}h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:30px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:16px 0 6px;color:var(--acc)}");
        h.Append(".sub{color:var(--mute)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:10px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}.card b{display:block;font-size:17px}.card small{color:var(--mute)}");
        h.Append(".wrap{overflow-x:auto;margin-bottom:6px}table{border-collapse:collapse;width:100%;background:var(--card);border:1px solid var(--line);font-variant-numeric:tabular-nums}th,td{padding:5px 8px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--mute);font-weight:600;font-size:12px}");
        h.Append(".pos{color:var(--pos)}.neg{color:var(--neg)}.hp{background:var(--pbg)}.hn{background:var(--nbg)}.best{outline:2px solid var(--acc);outline-offset:-2px}.note{background:var(--card);border-left:3px solid var(--acc);padding:10px 14px;border-radius:6px;margin:10px 0}td.w{white-space:normal;min-width:330px;text-align:left}</style></head><body><main>");
        h.Append("<h1>Rotation with confirmed entries — MNQ + MGC, combined $, entered only on an FVG setup</h1><div class='sub'>" + pairs[0].Item1.Day.ToString("MMM d, yyyy", IC) + " → " + pairs[pairs.Count - 1].Item1.Day.ToString("MMM d, yyyy", IC) + " • " + res.Count + " combinations • 10 MNQ + 10 MGC • score = 50 evaluations of LUCID FLEX 50K rotating, payouts − fees ($120 each; a blown evaluation costs only its fee) • chosen on 2020–23, judged on 2024–26</div>");
        Action<string, Res, string> card = (k, x, why) => h.Append("<div class='card'><small>" + k + "</small><b>" + x.C.Name + "</b><small>2020–23 prop <span class='" + cls(x.PA.Net) + "'>" + usd(x.PA.Net) + "</span> • 2024–26 prop <span class='" + cls(x.PB.Net) + "'>" + usd(x.PB.Net) + "</span><br>2024–26: pass " + pct(100 * x.PB.PassRate) + " • funded paid " + pct(100 * x.PB.PayoutRate) + " • P(payout / eval) " + pct(100 * x.PB.PPay) + "<br>trades " + x.All.Fills + " • won " + pct(x.All.WinPct) + " • live 6 years " + usd(x.All.Pnl) + "<br>" + why + "</small></div>");
        h.Append("<div class='cards'>");
        card("THE PICK (best on 2020–23 that also held on 2024–26)", winner, "");
        card("BASELINE • OPENING FVG on MNQ alone", baseline, "the rule we already have, for comparison");
        h.Append("</div>");
        h.Append("<div class='note'><b>How to read it.</b> " + res.Count + " combinations were tried on the same days, so the best of them is partly luck. That is why the pick is chosen on 2020–23 ONLY and then shown on 2024–26, days it never saw. Trust a rule that is green in both columns; one green half is a coin flip.</div>");

        h.Append("<h2>1 • The 25 best on 2020–23 — and what they did on 2024–26</h2><div class='wrap'><table><tr><th>RULE</th><th>TRADES</th><th>WON</th><th>PROP 2020–23</th><th>PROP 2024–26</th><th>PASS 24–26</th><th>PAID 24–26</th><th>P(PAYOUT/EVAL)</th><th>RUIN 50</th><th>LIVE 2020–23</th><th>LIVE 2024–26</th></tr>");
        foreach (var x in picks) h.Append("<tr class='" + (x == winner ? "best" : "") + "'><td class='w'>" + x.C.Name + "</td><td>" + x.All.Fills + "</td><td>" + pct(x.All.WinPct) + "</td><td class='" + cls(x.PA.Net) + "'>" + usd(x.PA.Net) + "</td><td class='" + cls(x.PB.Net) + " " + (x.PB.Net > 0 ? "hp" : "hn") + "'><b>" + usd(x.PB.Net) + "</b></td><td>" + pct(100 * x.PB.PassRate) + "</td><td>" + pct(100 * x.PB.PayoutRate) + "</td><td>" + pct(100 * x.PB.PPay) + "</td><td>" + pct(100 * x.PB.Ruin(50)) + "</td><td class='" + cls(x.A.Pnl) + "'>" + usd(x.A.Pnl) + "</td><td class='" + cls(x.B.Pnl) + "'>" + usd(x.B.Pnl) + "</td></tr>");
        h.Append("</table></div>");

        // by session / trigger / companion (each: the average over the other settings, 6 years prop)
        Action<string, Func<Res, int>, string[]> by = (title, key, names) =>
        {
            h.Append("<h3>" + title + " — average over all the other settings</h3><div class='wrap'><table><tr><th></th><th>COMBINATIONS</th><th>AVG TRADES</th><th>AVG WON</th><th>AVG PROP 2020–23</th><th>AVG PROP 2024–26</th><th>GREEN IN BOTH</th></tr>");
            for (int i = 0; i < names.Length; i++)
            {
                var g = res.Where(x => key(x) == i).ToList(); if (g.Count == 0) continue;
                h.Append("<tr><td>" + names[i] + "</td><td>" + g.Count + "</td><td>" + g.Average(x => x.All.Fills).ToString("0") + "</td><td>" + pct(g.Average(x => x.All.WinPct)) + "</td><td class='" + cls(g.Average(x => x.PA.Net)) + "'>" + usd(g.Average(x => x.PA.Net)) + "</td><td class='" + cls(g.Average(x => x.PB.Net)) + "'>" + usd(g.Average(x => x.PB.Net)) + "</td><td>" + g.Count(x => x.PA.Net > 0 && x.PB.Net > 0) + " of " + g.Count + "</td></tr>");
            }
            h.Append("</table></div>");
        };
        h.Append("<h2>2 • What matters: session, confirmation, the other instrument, the bracket</h2>");
        by("SESSION", x => x.C.S, Sess); by("CONFIRMATION (TRIGGER)", x => x.C.T, Trig); by("THE OTHER INSTRUMENT", x => x.C.C, Comp);
        by("COMBINED TARGET / STOP", x => x.C.B, Br.Select(b => "+$" + b[0].ToString("N0", IC) + " / −$" + b[1].ToString("N0", IC)).ToArray()); by("ENTRY % OF THE ZONE", x => x.C.P, Pcts.Select(p => p + "%").ToArray());

        // the best per session (chosen on 2020–23)
        h.Append("<h2>3 • The best rule in each session (chosen on 2020–23)</h2><div class='wrap'><table><tr><th>SESSION</th><th>RULE</th><th>PROP 2020–23</th><th>PROP 2024–26</th><th>PASS 24–26</th><th>PAID 24–26</th><th>TRADES</th></tr>");
        for (int s = 0; s < 4; s++) { var x = res.Where(y => y.C.S == s && y.All.Fills >= 100).OrderByDescending(y => y.PA.Net).FirstOrDefault(); if (x == null) continue; h.Append("<tr><td>" + Sess[s] + "</td><td class='w'>" + x.C.Name + "</td><td class='" + cls(x.PA.Net) + "'>" + usd(x.PA.Net) + "</td><td class='" + cls(x.PB.Net) + "'><b>" + usd(x.PB.Net) + "</b></td><td>" + pct(100 * x.PB.PassRate) + "</td><td>" + pct(100 * x.PB.PayoutRate) + "</td><td>" + x.All.Fills + "</td></tr>"); }
        h.Append("</table></div>");
        h.Append("<p class='sub'>Built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", IC) + " • research/ComboStudy.cs</p></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "combo_study.html"), h.ToString());
        File.WriteAllText(Path.Combine(outDir, "combo_study_log.txt"), log.ToString());
        say("PICK: " + winner.C.Name + " • B prop " + winner.PB.Net.ToString("N0", IC) + " • BASELINE B prop " + baseline.PB.Net.ToString("N0", IC));
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }
}
