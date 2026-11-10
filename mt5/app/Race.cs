// SpasiX — 🏁 RACE (user's request): EVERY strategy runs at the same time in the background, each on its OWN simulated account fed by the
// same prices (the live MT5 quotes and candles, or the BACKTEST's replay), each with its own 🤖 engine, iterations, hedges and trades.
// Nothing here can reach MT5: a ShadowBroker trades a SimAccount in memory only. Every finished iteration is saved per day
// (DataDir\race\<window>\<yyyy-MM-dd>.csv + -trades.csv) so days, weeks and months can be compared to find the most consistent strategy.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    // a simulated account on someone else's prices: the market's quotes / candles / symbols, its own positions, orders and deals
    public sealed class ShadowBroker : IBroker
    {
        readonly Mt5State market, state; public readonly SimAccount Acct; readonly Func<string, bool> send; int seq;
        readonly Dictionary<string, Tuple<DateTime, double, double>> seen = new Dictionary<string, Tuple<DateTime, double, double>>(StringComparer.OrdinalIgnoreCase);
        public ShadowBroker(Mt5State market, Func<string, bool> send, SimAccountSettings s)
        {
            this.market = market; this.send = send; state = new Mt5State(market); Acct = new SimAccount(state); state.BridgeVersion = 5;
            lock (state.Gate) { var now = market.ServerNow > DateTime.MinValue ? market.ServerNow : DateTime.Now; Acct.Reset(s, now); state.Account.Kind = "RACE"; state.Account.Name = "RACE"; Mirror(); }
        }
        public Mt5State State { get { return state; } } public bool IsSim { get { return true; } } public string Problem { get { return ""; } } public int Port { get { return 0; } }
        public bool Send(string line) { return send != null && send(line); }
        void Mirror()
        {
            state.ServerNow = market.ServerNow; state.ServerOffsetSec = market.ServerOffsetSec; state.Linked = true; state.AllowInput = state.AlgoTerminal = state.AlgoEa = state.AccountTrade = state.AccountExpert = true; state.CanKnown = true; state.MaxLots = 100;
        }
        public SimSpec SpecFor(string sym)
        {
            SimSpec sp; if (Acct.Specs.TryGetValue(sym, out sp)) return sp;
            Mt5Symbol ms; if (!market.Symbols.TryGetValue(sym, out ms)) return null;
            sp = SimSpec.From(ms, market.ServerOffsetSec, -7, Acct.Set.Currency); sp.MarginLeverage = Acct.Set.Leverage; Acct.Specs[sym] = sp; return sp;
        }
        void Price(string sym)
        {
            Mt5Quote q; if (!market.Quotes.TryGetValue(sym, out q) || q.Bid <= 0) return; var sp = SpecFor(sym); if (sp == null) return;
            Tuple<DateTime, double, double> last; if (seen.TryGetValue(sym, out last) && last.Item1 == q.Time && last.Item2 == q.Bid && last.Item3 == q.Ask) return;
            seen[sym] = Tuple.Create(q.Time, q.Bid, q.Ask); Acct.OnPrice(sym, q.Time, q.Bid, q.Ask + (Acct.Set.Widened ? Acct.Set.MarkupPts * sp.Point : 0));
        }
        // every step: the market's newest prices → this account (fills, stops, targets, P/L)
        public void Sync()
        {
            lock (state.Gate)
            {
                Mirror();
                foreach (var sym in state.Positions.Select(p => p.Symbol).Concat(state.Orders.Select(o => o.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()) Price(sym);
                Acct.Mark();
            }
        }
        public string Command(string cmd, params object[] a)
        {
            string id = "R" + (++seq);
            lock (state.Gate)
            {
                Tuple<bool, string> r;
                try
                {
                    if (cmd == "ORDER") { var sym = Convert.ToString(a[0]); seen.Remove(sym); Price(sym); }
                    var now = market.ServerNow > DateTime.MinValue ? market.ServerNow : DateTime.Now; r = Acct.Do(cmd, a, now > Acct.Now ? now : Acct.Now);
                }
                catch (Exception ex) { r = Tuple.Create(false, cmd + ": " + ex.Message); }
                state.AddResult(id, r.Item1, r.Item2);
            }
            return id;
        }
    }

    // one strategy in the race: its settings, its simulated account, its engine and the numbers of the day
    public sealed class RaceLane
    {
        public string Key = "", Name = "", Problem = ""; public AutoSettings Set; public ShadowBroker Br; public HedgeAuto Eng = new HedgeAuto();
        public int Saved, Blowups; public double StartBalance, PeakEq, MaxDdEq;   // the account's deepest drop from its highest equity (open P/L included)
        public double Equity { get { lock (Br.State.Gate) return Br.State.Account.Equity; } }
        public void Step()
        {
            if (Br == null) return; Br.Sync(); if (Eng.Running) Eng.Step(Br);
            double eq = Equity; if (eq > PeakEq) PeakEq = eq; MaxDdEq = Math.Max(MaxDdEq, PeakEq - eq);
        }
    }

    // the settings of each lane (pure: tested) — the 09d forward-test lanes SLV / FX10 / NFB are EXPERIMENTAL, simulation only, never live options
    // 09j: the 🏁 RACE scoreboard row (pure: tested) — lane · today $ · total $ · iterations · won / lost · worst · open now · vs MY SETTINGS
    public sealed class RaceScore { public string Key = "", Name = ""; public double Today, Total, Worst, Open = double.NaN, Vs = double.NaN; public int Iterations, Won, Lost; public bool Leader; }
    public static class RaceBoard
    {
        public static List<RaceScore> Rank(IEnumerable<RaceScore> rows)
        {
            var l = rows.OrderByDescending(r => r.Total).ThenBy(r => r.Key, StringComparer.Ordinal).ToList(); var mine = l.FirstOrDefault(r => r.Key == "MINE");
            foreach (var r in l) { r.Vs = mine == null ? double.NaN : Math.Round(r.Total - mine.Total, 2); r.Leader = false; }
            if (l.Count > 0) l[0].Leader = l.Count == 1 || l[0].Total > l[1].Total + 0.005; return l;   // a tie (e.g. all at $0) has no leader
        }
    }

    public static class RaceLanes
    {
        // ▶ COMPARE WITH MY SETTINGS (09j): the ready-made set — the baseline, the recommended protections, ➕ ADD ON BREAK + 🛡 PROTECT, TAH
        public static readonly string[] Keys = { "MINE", "REC", "CMP", "RNG", "LKL", "LKF", "BH1", "BH5", "BH15", "BH30", "BH60", "A", "AH", "B", "C", "D", "F", "BOX", "SLV", "FX10", "NFB", "TST", "RNA", "RLK", "RBE", "TAH", "AOB", "AOP", "RUN", "DEF" };   // every lane (the RACE tab's list; 09l: DEF)
        public static readonly string[] Compare = { "MINE", "RUN", "REC", "AOP", "TAH" };   // 09k: 🦅 RUNNER next to MINE
        // 09l: when MY SETTINGS already are 🦅 RUNNER, 🛡 DEFENDER (DEF) takes RUN's place — never RUNNER twice
        public static string[] CompareFor(AutoSettings mine) { return mine != null && mine.IsRunner ? new[] { "MINE", "DEF", "REC", "AOP", "TAH" } : Compare; }
        static string RadarText { get { return "50 %"; } }
        public static readonly string[] Experimental = { "SLV", "FX10", "NFB", "TST", "RNA", "RLK", "RBE", "TAH", "AOB", "AOP", "RUN" };
        public static bool IsExperimental(string k) { return Array.IndexOf(Experimental, k) >= 0; }
        public static string Tip(string k)
        {
            if (k == "SLV") return "🧪 EXPERIMENTAL — not proven live. 🔒 SAFE LIVE on its own simulated account (same-size hedges, 🧹 compress always, −10 % per iteration, −5 % a day, breakeven close, DANGER EXIT). " + AutoSettings.SafeLiveEvidence;
            if (k == "FX10") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS, but every hedge at a FIXED $10 of price from the last leg's fill (no swing, no max-distance / closer-hedge moves) — the R10 variant of the research, here only to compare on new data. Gold: $10 = 1,000 points.";
            if (k == "RNA") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS + ⚠ RADAR: no more add-ons above DANGER " + RadarText + " (lost to 🧹 COMPRESS ALWAYS in the 6-year test) — simulation only.";
            if (k == "RLK") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS + ⚠ RADAR: lock at net 0 above DANGER " + RadarText + " (the lock's own unlock on the break; lost to 🧹 COMPRESS ALWAYS in the 6-year test) — simulation only.";
            if (k == "RBE") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS + ⚠ RADAR: close at the next breakeven above DANGER " + RadarText + " (lost to 🧹 COMPRESS ALWAYS in the 6-year test) — simulation only.";
            if (k == "TAH") return "🧪 EXPERIMENTAL — unproven. MY SETTINGS, but a new iteration only WITH the H1 EMA 20's 3-hour slope (BUY when it rises, SELL when it falls). The opposite rule did about as well in the research — unproven.";
            if (k == "AOB") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS + ➕ ADD ON BREAK: while the basket loses, ONE add (1× the entry) in the net's direction on a trend ≥ 70 that way + a finished M1 close beyond the last swing, its stop at the last leg; the 🎯 exit guard closes all at the target. " + TrendDetect.AddBreakEvidence;
            if (k == "REC") return "★ MY SETTINGS + the RECOMMENDED protections: 🧹 COMPRESS HEDGES ALWAYS (the best of the 6-year replays: no wipe-out) + 🔐 at the breakeven line CLOSE ALL.";
            if (k == "AOP") return "🧪 EXPERIMENTAL — not proven. ➕ ADD ON BREAK + 🛡 PROTECT THE ADD on MY SETTINGS: the add is protected at once (entry − 1 × ATR M1); reached → lock at net 0, a 15-minute cool-down, then restart with the entry size on the first break.";
            if (k == "DEF") return "🛡 DEFENDER, the default, on its own simulated account (sized from the race balance, not from MY SETTINGS).";
            if (k == "RUN") return "🧪 EXPERIMENTAL — " + AutoSettings.RunnerText + " Here on its own simulated account, sized from the race balance.";
            if (k == "TST") return "🧪 EXPERIMENTAL — not proven. 🚀 TREND STACK on MY SETTINGS: the short-term trend detector stacks lots on the trend's side at each new swing break (sized to reach the target in 1.5 × ATR M5, capped), a flip closes the stacks, a strong trend banks the other side's winners — simulation only.";
            if (k == "NFB") return "🧪 EXPERIMENTAL — not proven. MY SETTINGS, but NO FLIP INSIDE A BOX: while the price is still inside the high–low of the last 30 finished candles no hedge opens — the hedge waits just beyond the box (the R2-W variant of the research), here only to compare on new data.";
            return "";
        }
        public static AutoSettings For(string k, AutoSettings mine, double bal)
        {
            bool mineBased = k == "MINE" || k == "CMP" || k == "RNG" || k == "LKL" || k == "LKF" || k == "FX10" || k == "NFB" || k == "TST" || k == "RNA" || k == "RLK" || k == "RBE" || k == "TAH" || k == "AOB" || k == "AOP" || k == "REC" || k.StartsWith("BH");
            var set = mineBased ? mine.Copy() : k == "SLV" ? AutoSettings.Model("SAFELIVE", mine, bal) : k == "RUN" ? AutoSettings.Model("RUNNER", mine, bal) : k == "DEF" ? AutoSettings.Model("DEFENDER", mine, bal) : AutoSettings.Model(k, mine);
            if (k.StartsWith("BH")) { set.EntryMode = "AGGBRK"; set.Tf = int.Parse(k.Substring(2), CultureInfo.InvariantCulture); set.AggRun = Math.Max(2, set.AggRun); set.FirstDir = "AUTO"; set.NextStart = "BREAK"; }   // BREAK OF HIGH: an aggression run, the first opposite close, its high (buy) / low (sell) broken — your hedging untouched
            if (k == "CMP") set.CompressMode = "ALWAYS"; else if (k == "RNG") { set.RangeOnly = true; set.LockOn = false; } else if (k == "LKL") { set.LockOn = true; set.Unlock = "LEAD"; set.RangeOnly = true; } else if (k == "LKF") { set.LockOn = true; set.Unlock = "LIFT"; set.RangeOnly = true; }
            else if (k == "FX10") { set.HedgeMode = "PRICE"; set.HedgePrice = 10; set.HedgeCap = 0; set.RangeOnly = false; set.QuickHedge = false; set.CandleHedge = false; set.KeyHedge = false; set.TrendHedge = false; set.DdTightEvery = 0; set.BoxBars = 0; }
            else if (k == "NFB") { set.BoxBars = 30; }
            else if (k == "TST") { set.TrendStack = true; }
            else if (k == "RNA") set.RadarNoAdd = true; else if (k == "RLK") set.RadarLock = true; else if (k == "RBE") set.RadarBe = true; else if (k == "TAH") set.SlopeFilter = true; else if (k == "REC") { set.CompressMode = "ALWAYS"; set.BeLock = true; set.BeClose = true; }
            else if (k == "AOB") { set.AddOnBreak = true; set.ProtectAdd = "OFF"; } else if (k == "AOP") { set.AddOnBreak = true; set.ProtectAdd = "ON"; }
            set.Symbol = mine.Symbol; set.TrackManual = false;   // a lane never adopts anything: its account is its own
            return set;
        }
    }

    // one saved iteration of one strategy (the history)
    public sealed class RaceRow { public string Key = "", First = "", Reason = ""; public DateTime Day, Start, End; public int N, Buys, Sells, AddOns; public double MaxGross, MaxDd, Result; public double Secs { get { return (End - Start).TotalSeconds; } } }
    public sealed class RaceStat { public string Key = ""; public int Days, GreenDays, Iterations, Wins; public double Net, BestDay, WorstDay = double.MaxValue, WorstDd, AvgSecs, MaxLots, Score; public double PerDay { get { return Days > 0 ? Net / Days : 0; } } }

    public static class RaceStore
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static void Save(string folder, string key, AutoIteration x, IEnumerable<AutoTrade> trades)
        {
            try
            {
                Directory.CreateDirectory(folder); var day = x.Start.ToString("yyyy-MM-dd", IC); var f = Path.Combine(folder, day + ".csv"); var t = Path.Combine(folder, day + "-trades.csv");
                if (!File.Exists(f)) File.WriteAllText(f, "strategy,n,start,end,first,buys,sells,addons,maxlots,maxdd,result,reason\n");
                File.AppendAllText(f, string.Join(",", key, x.N.ToString(IC), x.Start.Ticks.ToString(IC), x.End.Ticks.ToString(IC), x.FirstDir, x.Buys.ToString(IC), x.Sells.ToString(IC), x.AddOns.ToString(IC), x.MaxGross.ToString("0.##", IC), x.MaxDd.ToString("0.00", IC), x.Result.ToString("0.00", IC), (x.Reason ?? "").Replace(",", " ")) + "\n");
                if (!File.Exists(t)) File.WriteAllText(t, "strategy,iteration,time,side,lots,price,why\n");
                File.AppendAllLines(t, trades.Where(tr => tr.Iter == x.N).Select(tr => string.Join(",", key, tr.Iter.ToString(IC), tr.Time.Ticks.ToString(IC), tr.Side, tr.Lots.ToString("0.##", IC), tr.Price.ToString("R", IC), (tr.Why ?? "").Replace(",", " "))));
            }
            catch { }
        }
        public static List<RaceRow> Load(string folder, DateTime from)
        {
            var res = new List<RaceRow>(); if (!Directory.Exists(folder)) return res;
            foreach (var f in Directory.GetFiles(folder, "*.csv").Where(x => !x.EndsWith("-trades.csv")))
            {
                DateTime d; if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd", IC, DateTimeStyles.None, out d) || d < from.Date) continue;
                try
                {
                    foreach (var l in File.ReadAllLines(f).Skip(1))
                    {
                        var a = l.Split(','); if (a.Length < 12) continue;
                        res.Add(new RaceRow { Key = a[0], Day = d, N = int.Parse(a[1], IC), Start = new DateTime(long.Parse(a[2], IC)), End = new DateTime(long.Parse(a[3], IC)), First = a[4], Buys = int.Parse(a[5], IC), Sells = int.Parse(a[6], IC), AddOns = int.Parse(a[7], IC), MaxGross = double.Parse(a[8], IC), MaxDd = double.Parse(a[9], IC), Result = double.Parse(a[10], IC), Reason = a[11] });
                    }
                }
                catch { }
            }
            return res;
        }
        // per strategy over the chosen days: net, days in profit, the worst day, the deepest basket, the time in the trade; Score = the
        // average day divided by how much the days swing (+1 so one quiet day does not win) — the most CONSISTENT first
        public static List<RaceStat> Stats(List<RaceRow> rows)
        {
            var res = new List<RaceStat>();
            foreach (var g in rows.GroupBy(r => r.Key))
            {
                var days = g.GroupBy(r => r.Day).Select(d => d.Sum(r => r.Result)).ToList(); double avg = days.Average(), sd = Math.Sqrt(days.Sum(v => (v - avg) * (v - avg)) / days.Count);
                res.Add(new RaceStat { Key = g.Key, Days = days.Count, GreenDays = days.Count(v => v > 0), Iterations = g.Count(), Wins = g.Count(r => r.Result > 0), Net = g.Sum(r => r.Result), BestDay = days.Max(), WorstDay = days.Min(), WorstDd = g.Min(r => r.MaxDd), AvgSecs = g.Average(r => r.Secs), MaxLots = g.Max(r => r.MaxGross), Score = avg / (sd + 1) });
            }
            return res.OrderByDescending(s => s.Score).ToList();
        }
    }
}
