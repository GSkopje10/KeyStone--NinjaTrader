// GROUP PLAN — the $150-a-day plan as the user would run it: 50 LUCID FLEX 50K evaluations, one trade a day per account
// (10 MNQ + 10 MGC at a session's open, closed at +$150, at the stop, or flat at 16:44), blown accounts replaced the next day.
// Plans compared: all 50 copying one trade • 5 groups of 10, each group its own session (blow-ups spread out) • 5 groups of 10 at
// the same session with different sides • every account its own random sides. Each plan with stops $2,000 / $1,000 / $500,
// payouts as the firm allows or only at a $4,000 balance, and the evaluation passed the same way or with FVG RETRACE.
// Everything per month: fees, evaluations bought, passed, blown, payouts, $ kept, the bank — and three accounts' day-by-day lives.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NinjaTrader.NinjaScript;

public static class GroupPlanStudy
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly DateTime Half = new DateTime(2024, 1, 1);
    static readonly string[] SideName = { "each account random", "random, copied", "BUY BOTH", "SELL BOTH", "BUY MNQ • SELL MGC", "SELL MNQ • BUY MGC", "FOLLOW the first 5 min", "FADE the first 5 min" };
    const double T = 150;

    sealed class G { public string Name; public int Ses, Side, Accts; }
    sealed class Plan { public string Name, Idea; public List<G> Gs = new List<G>(); public double Stop = 2000, PayFrom = 4000; public bool EvalFvgr; public int Base, Seed; public bool Random { get { return Gs.Any(g => g.Side <= 1); } } }
    sealed class R3 { public Plan P; public Out A, B, All; public string Band = "—"; public double Score { get { return Math.Min(A.Roi, B.Roi); } } }
    sealed class Lane { public OpenFvgStudy.Acct A; public G Grp; public int W, Id; public DateTime Started, FundedOn; public List<string[]> Log; public string Story = ""; }
    sealed class Mo { public double Fees, Kept; public int Bought, Passed, EBlown, FBlown, Retired, Payouts, Won, Lost, Flat; public int EvalAlive, FundAlive; }
    sealed class Out
    {
        public double Spent, Kept, Low, High; public int Bought, Passed, EBlown, FBlown, Retired, Payouts, Won, Lost, Flat, Straight; public DateTime FirstPay = DateTime.MinValue, FirstGreen = DateTime.MinValue, LowDay;
        public SortedDictionary<string, Mo> Month = new SortedDictionary<string, Mo>(); public Dictionary<string, double[]> Grp = new Dictionary<string, double[]>();   // fees, kept, passed, payouts, blown
        public List<Lane> Samples = new List<Lane>(); public double Net { get { return Kept - Spent; } } public double Roi { get { return Spent == 0 ? 0 : Kept / Spent; } }
        public int MonthsUp, MonthsDown; public double WorstMonth, BestMonth;
    }

    static Out Run(List<DailyTargetStudy.DayData> days, Plan p, KeystoneSimRules r, bool keepLogs)
    {
        var o = new Out(); var lanes = new List<Lane>(); int id = 0;
        foreach (var g in p.Gs) { if (g.Side == 0) for (int i = 0; i < g.Accts; i++) lanes.Add(new Lane { Grp = g, W = 1, Id = id++ }); else lanes.Add(new Lane { Grp = g, W = g.Accts, Id = id++ }); o.Grp[g.Name] = new double[5]; }
        if (keepLogs) foreach (var g in p.Gs.Take(3)) { var ln0 = lanes.First(l => l.Grp == g); ln0.Log = new List<string[]>(); o.Samples.Add(ln0); }
        double net = 0;
        for (int di = 0; di < days.Count; di++)
        {
            var dd = days[di]; string mk = dd.Day.ToString("yyyy-MM", IC); Mo mo; if (!o.Month.TryGetValue(mk, out mo)) { mo = new Mo(); o.Month[mk] = mo; }
            foreach (var ln in lanes)
            {
                var gs = o.Grp[ln.Grp.Name];
                if (ln.A == null)
                {
                    ln.A = new OpenFvgStudy.Acct { Id = ln.Id }; ln.Started = dd.Day; double fee = ln.W * r.EvalCost; o.Spent += fee; net -= fee; mo.Fees += fee; mo.Bought += ln.W; o.Bought += ln.W; gs[0] += fee;
                    if (keepLogs && ln.Log != null) ln.Log.Add(new[] { dd.Day.ToString("yyyy-MM-dd", IC), "NEW EVAL", "", "", "", "bought for $" + r.EvalCost });
                }
                var a = ln.A; double pnl; string how = "";
                if (!a.Funded && p.EvalFvgr) { if (dd.Fvgr == 0) continue; pnl = dd.Fvgr; how = "FVG RETRACE day"; }
                else
                {
                    int sd = ln.Grp.Side, c;
                    if (sd == 0) c = DailyTargetStudy.Hash(ln.Id + p.Seed * 1000, di); else if (sd == 1) c = DailyTargetStudy.Hash(ln.Grp.Ses * 31 + 7 + p.Seed * 1000, di); else if (sd <= 5) c = sd - 2; else c = dd.Follow[ln.Grp.Ses];
                    if (sd == 7 && c >= 0) c = c == 0 ? 1 : c == 1 ? 0 : c == 2 ? 3 : 2;
                    if (c < 0) continue;
                    var path = dd.P[ln.Grp.Ses][sd >= 6 ? 4 + c : c]; if (path == null) continue;
                    double dd0 = a.Funded ? r.FundDrawdown : r.EvalDrawdown, lk = a.Funded ? r.FundLockAt : r.EvalLockAt, floor = Math.Min(a.Hwm - dd0, lk);
                    double stop = Math.Min(p.Stop, a.Bal - floor); int h; double mfe; pnl = path.Query(T, stop, out h, out mfe);
                    if (h == 1) { o.Won += ln.W; mo.Won += ln.W; } else if (h == -1) { o.Lost += ln.W; mo.Lost += ln.W; if (mfe < 1) o.Straight += ln.W; } else { o.Flat += ln.W; mo.Flat += ln.W; }
                    how = (new[] { "BUY BOTH", "SELL BOTH", "BUY MNQ / SELL MGC", "SELL MNQ / BUY MGC" })[c] + (h == 1 ? " • +$150 target" : h == -1 ? " • stop" : " • flat 16:44");
                }
                double k0 = a.Kept; string ph = a.Funded ? "FUNDED" : "EVAL"; bool alive = DailyTargetStudy.PayStep(a, pnl, r, p.PayFrom);
                string ev = "";
                if (a.Kept > k0) { double g = (a.Kept - k0) * ln.W; o.Kept += g; net += g; mo.Kept += g; mo.Payouts += ln.W; o.Payouts += ln.W; gs[1] += g; gs[3] += ln.W; if (o.FirstPay == DateTime.MinValue) o.FirstPay = dd.Day; ev = "PAYOUT #" + a.Payouts + ": you keep $" + (a.Kept - k0).ToString("N0", IC); }
                if (!alive)
                {
                    if (a.End == "PASSED") { o.Passed += ln.W; mo.Passed += ln.W; gs[2] += ln.W; ev = "PASSED → FUNDED"; ln.A = new OpenFvgStudy.Acct { Id = ln.Id, Funded = true }; ln.FundedOn = dd.Day; }
                    else
                    {
                        if (a.End == "BLOWN") { if (a.Funded) { o.FBlown += ln.W; mo.FBlown += ln.W; ev = "FUNDED BLOWN"; } else { o.EBlown += ln.W; mo.EBlown += ln.W; ev = "EVAL BLOWN"; } gs[4] += ln.W; }
                        else { o.Retired += ln.W; mo.Retired += ln.W; ev = "RETIRED (5 payouts)"; }
                        ln.A = null;
                    }
                }
                if (keepLogs && ln.Log != null && ln.Log.Count < 400) ln.Log.Add(new[] { dd.Day.ToString("yyyy-MM-dd", IC), ph, how, Signed(pnl), Signed(a.Bal), ev });
            }
            if (net < o.Low) { o.Low = net; o.LowDay = dd.Day; } o.High = Math.Max(o.High, net); if (o.FirstGreen == DateTime.MinValue && net > 0) o.FirstGreen = dd.Day;
            if (di == days.Count - 1 || days[di + 1].Day.Month != dd.Day.Month) { mo.EvalAlive = lanes.Where(l => l.A != null && !l.A.Funded).Sum(l => l.W); mo.FundAlive = lanes.Where(l => l.A != null && l.A.Funded).Sum(l => l.W); }
        }
        foreach (var ln in o.Samples) ln.Story = ln.Log.Count(x => x[1] == "NEW EVAL") + " evaluations bought • " + ln.Log.Count(x => x[5] == "PASSED → FUNDED") + " passed • " + ln.Log.Count(x => x[5].StartsWith("PAYOUT")) + " payouts • " + ln.Log.Count(x => x[5].Contains("BLOWN")) + " blown" + (ln.Log.Count >= 400 ? " (first 400 days shown)" : "") + " • one account slot of " + (ln.W > 1 ? ln.W + " copying it" : "its group");
        foreach (var m in o.Month.Values) { double n = m.Kept - m.Fees; if (n > 0) o.MonthsUp++; else if (n < 0) o.MonthsDown++; o.WorstMonth = Math.Min(o.WorstMonth, n); o.BestMonth = Math.Max(o.BestMonth, n); }
        return o;
    }
    static string Signed(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N0", IC); }

    public static void Run(List<RDay> mnq, List<RDay> mgc, string outDir)
    {
        var t0 = DateTime.Now; var log = new StringBuilder(); Action<string> say = s => { Console.WriteLine(s); log.AppendLine(s); };
        var rules = KeystoneSimRules.Presets()[1].Copy(); var days = DailyTargetStudy.Prepare(mnq, mgc);
        var A = days.Where(d => d.Day < Half).ToList(); var B = days.Where(d => d.Day >= Half).ToList();
        int[] five = { 0, 1, 2, 4, 3 };   // 18:00, 20:00, 03:00, 08:00, 09:30
        Func<string, string, Func<List<G>>, Plan> mk = (n, idea, gs) => new Plan { Name = n, Idea = idea, Gs = gs() };
        var bases = new List<Plan>
        {
            mk("ALL 50 COPY • NY 09:30 • FOLLOW 5 MIN", "every account the same trade: the side MNQ and MGC moved in the first 5 minutes after 09:30", () => new List<G> { new G { Name = "NY 09:30 follow", Ses = 3, Side = 6, Accts = 50 } }),
            mk("ALL 50 COPY • NY 09:30 • RANDOM", "every account the same coin-flip sides at 09:30", () => new List<G> { new G { Name = "NY 09:30 random", Ses = 3, Side = 1, Accts = 50 } }),
            mk("5 GROUPS × 10 • ONE SESSION EACH • RANDOM", "group 1 trades 18:00, 2 → 20:00, 3 → 03:00, 4 → 08:00, 5 → 09:30; each group copies one coin flip", () => five.Select(si => new G { Name = DailyTargetStudy.SesName[si], Ses = si, Side = 1, Accts = 10 }).ToList()),
            mk("5 GROUPS × 10 • ONE SESSION EACH • FOLLOW 5 MIN", "the same 5 sessions, each group follows the first 5 minutes of its session", () => five.Select(si => new G { Name = DailyTargetStudy.SesName[si], Ses = si, Side = 6, Accts = 10 }).ToList()),
            mk("5 GROUPS × 10 • ONE SESSION EACH • EVERY ACCOUNT RANDOM", "the same 5 sessions, and inside each group every account flips its own coin", () => five.Select(si => new G { Name = DailyTargetStudy.SesName[si], Ses = si, Side = 0, Accts = 10 }).ToList()),
            mk("5 GROUPS × 10 • NY 09:30 • 5 DIFFERENT SIDES", "all at 09:30: BUY BOTH / SELL BOTH / BUY MNQ+SELL MGC / SELL MNQ+BUY MGC / FOLLOW 5 — never all on the same side", () => new List<G> { new G { Name = "09:30 BB", Ses = 3, Side = 2, Accts = 10 }, new G { Name = "09:30 SS", Ses = 3, Side = 3, Accts = 10 }, new G { Name = "09:30 BS", Ses = 3, Side = 4, Accts = 10 }, new G { Name = "09:30 SB", Ses = 3, Side = 5, Accts = 10 }, new G { Name = "09:30 FOLLOW", Ses = 3, Side = 6, Accts = 10 } }),
            mk("ALL 50 • NY 09:30 • EVERY ACCOUNT RANDOM", "all at 09:30, every account its own coin flip (about half win when the other half lose)", () => new List<G> { new G { Name = "NY 09:30 spread", Ses = 3, Side = 0, Accts = 50 } }),
        };
        var plans = new List<Plan>();
        for (int bi = 0; bi < bases.Count; bi++) foreach (double st in new[] { 2000.0, 1000, 500 }) foreach (double pf in new[] { 4000.0, 0 }) foreach (bool fv in new[] { false, true })
            plans.Add(new Plan { Name = bases[bi].Name, Idea = bases[bi].Idea, Gs = bases[bi].Gs, Stop = st, PayFrom = pf, EvalFvgr = fv, Base = bi });
        // random sides: 20 different coin sequences — the plan is shown with its MEDIAN sequence, and the 10%–90% range of luck
        Func<Plan, int, Plan> seeded = (p, sd) => new Plan { Name = p.Name, Idea = p.Idea, Gs = p.Gs, Stop = p.Stop, PayFrom = p.PayFrom, EvalFvgr = p.EvalFvgr, Base = p.Base, Seed = sd };
        var res = new List<R3>();
        foreach (var p in plans)
        {
            var runs = Enumerable.Range(0, p.Random ? 20 : 1).Select(sd => { var q = seeded(p, sd); return new R3 { P = q, A = Run(A, q, rules, false), B = Run(B, q, rules, false), All = Run(days, q, rules, false) }; }).OrderBy(x => x.Score).ToList();
            var med = runs[runs.Count / 2];
            if (runs.Count > 1)
            {
                var sc = runs.Select(x => x.Score).ToList(); var nt = runs.Select(x => x.All.Net).OrderBy(x => x).ToList(); var lw = runs.Select(x => x.All.Low).OrderBy(x => x).ToList();
                med.Band = "ROI " + sc[2].ToString("0.00", IC) + "–" + sc[17].ToString("0.00", IC) + "× • net $" + (nt[2] / 1000).ToString("0", IC) + "K–$" + (nt[17] / 1000).ToString("0", IC) + "K • low $" + (lw[2] / 1000).ToString("0", IC) + "K";
            }
            res.Add(med);
        }
        Func<Plan, string> setting = p => "stop $" + p.Stop.ToString("N0", IC) + " • " + (p.PayFrom > 0 ? "payout at $4K" : "firm payouts") + " • " + (p.EvalFvgr ? "pass with FVG RETRACE" : "pass with $150 a day");
        foreach (var x in res.OrderByDescending(x => x.Score).Take(15)) say("  " + x.P.Name + " • " + setting(x.P) + " • ROI A " + x.A.Roi.ToString("0.00") + " B " + x.B.Roi.ToString("0.00") + " • net " + x.All.Net.ToString("N0", IC) + " • low " + x.All.Low.ToString("N0", IC) + " • months up/down " + x.All.MonthsUp + "/" + x.All.MonthsDown);
        // the pick: steadiest (worse period ROI), then a bank that never needs more than $15K
        var best = res.Where(x => x.All.Low > -15000).OrderByDescending(x => x.Score).FirstOrDefault() ?? res.OrderByDescending(x => x.Score).First();
        // the pick again with logs: three accounts' lives (the first lanes)
        var bp = best.P; var lanesPlan = bp;
        var full = Run(days, lanesPlan, rules, true);
        say("PICK: " + bp.Name + " • " + setting(bp) + " • ROI A " + best.A.Roi.ToString("0.00") + " B " + best.B.Roi.ToString("0.00") + " • net " + full.Net.ToString("N0", IC));

        // ---- per-trade numbers for $150 (the why)
        var why = new List<string[]>();
        foreach (int si in new[] { 0, 1, 2, 4, 3, 5 }) foreach (double st in new[] { 2000.0, 1000, 500 })
        {
            int n = 0, win = 0, loss = 0; double sum = 0, lossSum = 0, flatSum = 0; int flats = 0;
            foreach (var dd in days) for (int c = 0; c < 4; c++) { var pth = dd.P[si][c]; if (pth == null) continue; int h; double mfe; double v = pth.Query(T, st, out h, out mfe); n++; sum += v; if (h == 1) win++; else if (h == -1) { loss++; lossSum += v; } else { flats++; flatSum += v; } }
            why.Add(new[] { DailyTargetStudy.SesName[si], "$" + st.ToString("N0", IC), (100.0 * win / n).ToString("0.0", IC) + "%", (100.0 * st / (st + T)).ToString("0.0", IC) + "%", (100.0 * loss / n).ToString("0.0", IC) + "%", (100.0 * flats / n).ToString("0.0", IC) + "%", (flats == 0 ? 0 : flatSum / flats).ToString("+0;−0", IC), (sum / n).ToString("+0.0;−0.0", IC), (sum / n * 21).ToString("+0;−0", IC) });
        }

        // ---- HTML
        var h2 = new StringBuilder(); Func<double, string> usd = v => (v < 0 ? "−$" : "$") + Math.Abs(v).ToString("N0", IC); Func<double, string> cls = v => v > 0 ? "pos" : v < 0 ? "neg" : "";
        Func<double, string> roi = v => v.ToString("0.00", IC) + "×";
        h2.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>$150 A Day Plan</title><style>");
        h2.Append(":root{--bg:#0b0f14;--card:#141a22;--card2:#1b2330;--line:#2a3442;--text:#e8eef5;--mute:#8d9aab;--pos:#3fb950;--neg:#f85149;--acc:#e3b341;--blue:#58a6ff}");
        h2.Append("@media (prefers-color-scheme: light){:root:not([data-theme=dark]){--bg:#f5f7fa;--card:#fff;--card2:#f0f3f7;--line:#d8dee6;--text:#1c2430;--mute:#5d6b7c;--pos:#1a7f37;--neg:#cf222e;--acc:#9a6700;--blue:#0969da}}");
        h2.Append("*{box-sizing:border-box}body{background:var(--bg);color:var(--text);font:14px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;margin:0;padding:28px 16px}main{max-width:1240px;margin:0 auto}h1{font-size:28px;margin:0 0 6px}h2{font-size:19px;margin:40px 0 10px}h3{font-size:15px;margin:22px 0 8px;color:var(--acc)}.mute{color:var(--mute)}.pos{color:var(--pos)}.neg{color:var(--neg)}");
        h2.Append(".cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px;margin:14px 0}.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}.card b{display:block;font-size:24px;line-height:1.2}.card span{color:var(--mute);font-size:12.5px}");
        h2.Append(".box{background:var(--card);border:1px solid var(--line);border-left:4px solid var(--acc);border-radius:10px;padding:14px 18px;margin:14px 0}.box.red{border-left-color:var(--neg)}.box.green{border-left-color:var(--pos)}");
        h2.Append(".wrap{overflow-x:auto;border-radius:10px;border:1px solid var(--line)}table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums;background:var(--card)}th,td{padding:6px 9px;text-align:right;white-space:nowrap;border-bottom:1px solid var(--line)}th{color:var(--mute);font-weight:600;background:var(--card2);position:sticky;top:0}td:first-child,th:first-child,.l{text-align:left}tr.pick td{background:rgba(227,179,65,.13)}");
        h2.Append(".bar{display:inline-block;height:10px;border-radius:3px;vertical-align:middle}.bp{background:var(--pos)}.bn{background:var(--neg)}details{margin:10px 0}summary{cursor:pointer;color:var(--blue);font-weight:600}.tag{display:inline-block;padding:1px 8px;border-radius:20px;font-size:12px;background:var(--card2);border:1px solid var(--line)}</style></head><body><main>");
        h2.Append("<h1>The $150-a-day plan • 50 prop accounts • 6 years</h1><p class='mute'>MNQ + MGC 1-minute data " + days[0].Day.ToString("yyyy-MM-dd", IC) + " → " + days[days.Count - 1].Day.ToString("yyyy-MM-dd", IC) + " (" + days.Count + " trading days) • LUCID FLEX 50K: $120 evaluation, pass at +$3,000 (best day ≤ 50%, 2 days minimum), $2,000 trailing floor (end of day) that stops at +$100, payouts after 5 days of ≥ $150: 50% of the balance up to $2,000, you keep 90%, 5 payouts then retired • one trade a day per account: 10 MNQ + 10 MGC at the session open, out at +$150, at the stop, or flat at 16:44 • $" + ((KeystoneMoveStudy.Cost("MNQ") + KeystoneMoveStudy.Cost("MGC")) * 10).ToString("0", IC) + " costs per trade • a blown account is replaced the next day. Historical simulation — no orders.</p>");

        var fa = full; double winRate = fa.Won + fa.Lost + fa.Flat == 0 ? 0 : 100.0 * fa.Won / (fa.Won + fa.Lost + fa.Flat);
        h2.Append("<div class='box green'><b>The plan this test picked:</b> " + bp.Name + " <span class='tag'>" + setting(bp) + "</span><br><span class='mute'>" + bp.Idea + ". Picked as the steadiest of " + plans.Count + " plans (best ROI in the worse of 2020–23 / 2024–26) among those that never needed more than $15,000 in the bank; plans with random sides are judged on their median of 20 coin sequences." + (best.Band != "—" ? " Luck range: " + best.Band + "." : "") + "</span></div>");
        h2.Append("<div class='cards'>");
        Action<string, string, string> card = (k, v, c) => h2.Append("<div class='card'><b class='" + c + "'>" + v + "</b><span>" + k + "</span></div>");
        card("net after all fees (6 years)", usd(fa.Net), cls(fa.Net)); card("ROI 2020–23 • 2024–26", roi(best.A.Roi) + " • " + roi(best.B.Roi), ""); card("payouts • you kept", fa.Payouts + " • " + usd(fa.Kept), "pos"); card("evaluation fees (" + fa.Bought + " bought)", usd(-fa.Spent), "neg");
        card("evaluations passed", fa.Passed + " (" + (fa.Bought == 0 ? 0 : 100.0 * fa.Passed / fa.Bought).ToString("0", IC) + "%)", ""); card("blown • eval / funded", fa.EBlown + " / " + fa.FBlown, "neg"); card("money needed: the lowest bank", usd(fa.Low) + " (" + fa.LowDay.ToString("MMM yyyy", IC) + ")", "neg"); card("first payout • bank green from", (fa.FirstPay == DateTime.MinValue ? "never" : fa.FirstPay.ToString("MMM yyyy", IC)) + " • " + (fa.FirstGreen == DateTime.MinValue ? "never" : fa.FirstGreen.ToString("MMM yyyy", IC)), "");
        card("months up / down", fa.MonthsUp + " / " + fa.MonthsDown, ""); card("best month • worst month", usd(fa.BestMonth) + " • " + usd(fa.WorstMonth), ""); card("trades: hit +$150", winRate.ToString("0.0", IC) + "% of " + (fa.Won + fa.Lost + fa.Flat).ToString("N0", IC), ""); card("straight to the stop (never $1 up)", fa.Straight.ToString("N0", IC), "");
        h2.Append("</div>");

        // why the 90% is not enough
        h2.Append("<h2>1 • Why winning 9 days out of 10 can still lose money</h2><div class='box red'>One win = <b>+$150</b>. One loss = <b>−$2,000</b>. One loss erases <b>13 wins</b> (2,000 ÷ 150 = 13.3). To break even you must win <b>2,000 ÷ 2,150 = 93.0%</b> of the days — not 90%. Every 1% below 93% costs about $21 per trade. With a $1,000 stop you need 87.0%; with a $500 stop, 76.9%. The table shows each session with sides picked at random (no edge): how often +$150 came first, the win rate needed, and the average $ per trade.</div>");
        h2.Append("<div class='wrap'><table><tr><th>SESSION</th><th>STOP</th><th>+$150 FIRST</th><th>NEEDED TO BREAK EVEN</th><th>STOPPED</th><th>FLAT AT 16:44</th><th>AVG FLAT $</th><th>AVG $ / TRADE</th><th>ONE ACCOUNT / MONTH</th></tr>");
        foreach (var w in why) { double wr = double.Parse(w[2].TrimEnd('%'), IC), ne = double.Parse(w[3].TrimEnd('%'), IC); h2.Append("<tr><td>" + w[0] + "</td><td>" + w[1] + "</td><td class='" + (wr >= ne ? "pos" : "neg") + "'><b>" + w[2] + "</b></td><td>" + w[3] + "</td><td>" + w[4] + "</td><td>" + w[5] + "</td><td>" + w[6] + "</td><td class='" + (w[7].StartsWith("+") ? "pos" : "neg") + "'>" + w[7] + "</td><td class='" + (w[8].StartsWith("+") ? "pos" : "neg") + "'>" + w[8] + "</td></tr>"); }
        h2.Append("</table></div><p class='mute'>So on your own money this plan is about break-even. With prop accounts it can still pay, because a blown evaluation costs only $120 while a funded account pays up to $2,000 at a time — section 2.</p>");

        // plans compared
        h2.Append("<h2>2 • Every plan compared (50 accounts each)</h2><p class='mute'>ROI = payouts kept ÷ fees paid. LOWEST BANK = the most money the plan was down at any point — what you need to have ready.</p>");
        Action<string, IEnumerable<R3>> tbl = (title, rows) =>
        {
            h2.Append("<h3>" + title + "</h3><div class='wrap'><table><tr><th>PLAN</th><th class='l'>SETTINGS</th><th>ROI 20–23</th><th>ROI 24–26</th><th>BOUGHT</th><th>PASSED</th><th>PAYOUTS</th><th>KEPT</th><th>FEES</th><th>NET 6 YRS</th><th>LOWEST BANK</th><th>MONTHS +/−</th><th class='l'>LUCK RANGE (20 COIN SEQUENCES, 10%–90%)</th></tr>");
            foreach (var x in rows) { Out f = x.All; Plan p = x.P; h2.Append("<tr class='" + (p == bp ? "pick" : "") + "'><td>" + p.Name + "</td><td class='l'>" + setting(p) + "</td><td class='" + (x.A.Roi >= 1 ? "pos" : "neg") + "'>" + roi(x.A.Roi) + "</td><td class='" + (x.B.Roi >= 1 ? "pos" : "neg") + "'><b>" + roi(x.B.Roi) + "</b></td><td>" + f.Bought + "</td><td>" + f.Passed + "</td><td>" + f.Payouts + "</td><td class='pos'>" + usd(f.Kept) + "</td><td class='neg'>" + usd(-f.Spent) + "</td><td class='" + cls(f.Net) + "'><b>" + usd(f.Net) + "</b></td><td class='neg'>" + usd(f.Low) + "</td><td>" + f.MonthsUp + " / " + f.MonthsDown + "</td><td class='l mute'>" + x.Band + "</td></tr>"); }
            h2.Append("</table></div>");
        };
        tbl("The 7 plans with the pick's settings — copy all vs groups by session vs different sides", bases.Select((b0, bi) => res.First(x => x.P.Base == bi && x.P.Stop == bp.Stop && x.P.PayFrom == bp.PayFrom && x.P.EvalFvgr == bp.EvalFvgr)).ToList());
        tbl("The pick's plan with every stop, payout rule and way to pass", res.Where(x => x.P.Base == bp.Base));
        tbl("The 15 steadiest of all " + plans.Count, res.OrderByDescending(x => x.Score).Take(15));

        // the pick: groups, years, months
        h2.Append("<h2>3 • The pick, group by group</h2><div class='wrap'><table><tr><th>GROUP</th><th>ACCOUNTS</th><th>FEES</th><th>PASSED</th><th>PAYOUTS</th><th>BLOWN</th><th>KEPT</th><th>NET</th></tr>");
        foreach (var g in bp.Gs) { var v = fa.Grp[g.Name]; h2.Append("<tr><td>" + g.Name + " • " + SideName[g.Side] + "</td><td>" + g.Accts + "</td><td class='neg'>" + usd(-v[0]) + "</td><td>" + v[2] + "</td><td>" + v[3] + "</td><td>" + v[4] + "</td><td class='pos'>" + usd(v[1]) + "</td><td class='" + cls(v[1] - v[0]) + "'><b>" + usd(v[1] - v[0]) + "</b></td></tr>"); }
        h2.Append("</table></div>");
        h2.Append("<h2>4 • The pick, year by year</h2><div class='wrap'><table><tr><th>YEAR</th><th>EVALS BOUGHT</th><th>FEES</th><th>PASSED</th><th>EVAL BLOWN</th><th>FUNDED BLOWN</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th>BANK AT YEAR END</th></tr>");
        double bank = 0;
        foreach (var y in fa.Month.GroupBy(kv => kv.Key.Substring(0, 4)))
        {
            double f = y.Sum(kv => kv.Value.Fees), k = y.Sum(kv => kv.Value.Kept); bank += k - f;
            h2.Append("<tr><td>" + y.Key + "</td><td>" + y.Sum(kv => kv.Value.Bought) + "</td><td class='neg'>" + usd(-f) + "</td><td>" + y.Sum(kv => kv.Value.Passed) + "</td><td>" + y.Sum(kv => kv.Value.EBlown) + "</td><td>" + y.Sum(kv => kv.Value.FBlown) + "</td><td>" + y.Sum(kv => kv.Value.Payouts) + "</td><td class='pos'>" + usd(k) + "</td><td class='" + cls(k - f) + "'><b>" + usd(k - f) + "</b></td><td class='" + cls(bank) + "'>" + usd(bank) + "</td></tr>");
        }
        h2.Append("</table></div>");
        double maxAbs = Math.Max(1, fa.Month.Values.Max(m => Math.Abs(m.Kept - m.Fees)));
        h2.Append("<h2>5 • The pick, month by month</h2><div class='wrap'><table><tr><th>MONTH</th><th>BOUGHT</th><th>FEES</th><th>WON / LOST / FLAT</th><th>PASSED</th><th>BLOWN E / F</th><th>PAYOUTS</th><th>KEPT</th><th>NET</th><th class='l'></th><th>BANK</th><th>EVALS • FUNDED ALIVE</th></tr>");
        bank = 0;
        foreach (var kv in fa.Month)
        {
            var m = kv.Value; double n = m.Kept - m.Fees; bank += n; int wpx = (int)Math.Round(120 * Math.Abs(n) / maxAbs);
            h2.Append("<tr><td>" + kv.Key + "</td><td>" + m.Bought + "</td><td class='neg'>" + (m.Fees == 0 ? "" : usd(-m.Fees)) + "</td><td>" + m.Won + " / " + m.Lost + " / " + m.Flat + "</td><td>" + m.Passed + "</td><td>" + m.EBlown + " / " + m.FBlown + "</td><td>" + m.Payouts + "</td><td class='pos'>" + (m.Kept == 0 ? "" : usd(m.Kept)) + "</td><td class='" + cls(n) + "'><b>" + usd(n) + "</b></td><td class='l'><span class='bar " + (n >= 0 ? "bp" : "bn") + "' style='width:" + wpx + "px'></span></td><td class='" + cls(bank) + "'>" + usd(bank) + "</td><td>" + m.EvalAlive + " • " + m.FundAlive + "</td></tr>");
        }
        h2.Append("</table></div>");
        // three accounts' lives
        h2.Append("<h2>6 • Three accounts, day by day (check the math yourself)</h2><p class='mute'>The first account slot of three groups: each row is one trading day — the trade, the day's $, the balance after it, and what happened.</p>");
        foreach (var ln in full.Samples)
        {
            h2.Append("<details><summary>" + ln.Grp.Name + " • " + SideName[ln.Grp.Side] + " • " + ln.Story + "</summary><div class='wrap'><table><tr><th>DAY</th><th class='l'>PHASE</th><th class='l'>TRADE</th><th>DAY $</th><th>BALANCE</th><th class='l'>EVENT</th></tr>");
            foreach (var row in ln.Log) h2.Append("<tr><td>" + row[0] + "</td><td class='l'>" + row[1] + "</td><td class='l'>" + row[2] + "</td><td class='" + (row[3].StartsWith("+") ? "pos" : row[3].StartsWith("−") ? "neg" : "") + "'>" + row[3] + "</td><td>" + row[4] + "</td><td class='l'><b>" + row[5] + "</b></td></tr>");
            h2.Append("</table></div></details>");
        }
        h2.Append("<h2>7 • How it was tested</h2><ul class='mute'><li>Entry: market at the session's first minute (FOLLOW: after 5 minutes, each instrument in the direction it moved). Exit: the first minute the combined $ of both legs reaches +$150, or the stop — the stop is checked first when both happen in the same minute, both legs at their worst.</li><li>The stop is the smaller of the plan's stop and the room left above the account's floor, so a stop-out never goes below the floor except by a gap.</li><li>Groups of 10 copy one trade inside the group (they rise and fall together); different groups trade different sessions or sides, so they do not.</li><li>PASS WITH FVG RETRACE: in the evaluation the account takes every FVG RETRACE trade of the day on 10 MNQ (09:30–15:00, +$1,500 / −$1,500, as the app trades it).</li><li>Not included: slippage beyond one tick, payout denials, firm rule changes, activation fees, days the firm is closed. Real results will be lower.</li></ul></main></body></html>");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(System.IO.Path.Combine(outDir, "plan_150.html"), h2.ToString());
        File.WriteAllText(System.IO.Path.Combine(outDir, "plan_150_log.txt"), log.ToString());
        say("done in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
    }

}
