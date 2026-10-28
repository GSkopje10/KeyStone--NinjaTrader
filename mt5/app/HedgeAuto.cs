// SpasiX — 🤖 AUTO: the hedge AUTOMATION (the user's strategy), the same code on the BACKTEST, the DEMO account and MT5 (demo or real).
// One ITERATION: open the ENTRY lots (BUY / SELL typed by you for the first one, or the automation's: the break of the recent high = BUY,
// the recent low = SELL). No stop: when the price breaks the recent LOW (the last leg was a buy) / HIGH (the last leg was a sell) of the last
// Lookback finished candles of the chosen timeframe, the ADD-ON lots open the other way (0.5 buy → 1.0 sell → 1.0 buy …), until the whole
// basket (every position on the symbol: profit + swap + commission) reaches +TARGET $ → everything closes. MAX DRAWDOWN $ (0 = none, until
// the account blows) closes the basket at −$X, then STOP or the NEXT iteration. The next iteration: after a PAUSE (seconds of the market's clock,
// then the last basket's direction at once) or at the very next BREAK of the recent high / low.
// Orders go through the caller's Send (the window's ARM / Algo Trading / PROP checks), one at a time: the next decision waits for MT5's answer
// and for the position to show up, so a slow bridge never doubles an order. The automation owns EVERY position on its symbol.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class AutoSettings
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Symbol = "XAUUSD"; public int Tf = 1, Lookback = 1;   // the recent high / low = the last Lookback FINISHED candles of Tf minutes
        public double EntryLots = 0.1, AddLots = 0.2, Target = 20, MaxDrawdown = 0, CommissionPerLot = 0;   // MaxDrawdown 0 = none; CommissionPerLot = round turn, taken off the basket when MT5 does not show it
        public string FirstDir = "AUTO";       // AUTO = the first break; BUY / SELL = at once
        public string NextStart = "BREAK";     // BREAK = the very next break of the recent high / low; PAUSE = PauseSec, then the last basket's direction at once
        public int PauseSec = 60; public string AfterDrawdown = "STOP";   // STOP / NEXT
        public bool Rolling;                   // BREAK: the add-on level follows each new candle (off = fixed when the leg opens)
        public string HedgeMode = "BREAK";     // when the add-on opens: BREAK = the recent low / high; PRICE = HedgePrice of price against the last leg; MONEY = −HedgeMoney $ since the last leg
        public double HedgePrice = 5, HedgeMoney = 100;
        public int MaxIterations = 0;          // 0 = no limit
        public string EntryMode = "BREAK";     // the entry setup: BREAK (recent high / low) • FVG (the retest of a fair value gap) • VWAP (a cross of the day's VWAP) • FLOW (a tick-volume surge candle, broken)
        public double FlowVol = 1.5, FlowShare = 0.7;   // FLOW: the candle's tick volume ≥ FlowVol × the last 20, buyers (est.) ≥ FlowShare (sellers for a SELL)
        public int PerDay = 0;                 // iterations a server day (0 = no limit)
        public string Windows = "";            // New York times new iterations may start, e.g. "08:30-11:00, 13:00-16:00" ("" = all the time)
        public int NyOffsetHours = -7;         // New York = the MT5 server's clock + this (IC Markets: −7)
        public string NewsMode = "IGNORE";     // IGNORE • AVOID (no new iteration NewsBefore … NewsAfter minutes around high-impact news) • ONLY (only then)
        public int NewsBefore = 15, NewsAfter = 15;
        public bool AddToWinner; public double WinnerLots = 0.1; public int MaxWinnerAdds = 0;   // ADD TO THE WINNER: on each new signal of the setup in the leading side's direction (0 = no limit)
        public AutoSettings Copy() { return (AutoSettings)MemberwiseClone(); }
        // the New York windows ("08:30-11:00, 13:00-16:00") as minutes of the day; null = all the time; a bad window = an empty list
        public List<Tuple<int, int>> WindowList()
        {
            if (string.IsNullOrWhiteSpace(Windows)) return null; var res = new List<Tuple<int, int>>();
            foreach (var part in Windows.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ab = part.Trim().Split('-'); TimeSpan a, b; if (ab.Length != 2 || !TimeSpan.TryParse(ab[0].Trim(), IC, out a) || !TimeSpan.TryParse(ab[1].Trim(), IC, out b)) return new List<Tuple<int, int>>();
                res.Add(Tuple.Create((int)a.TotalMinutes, (int)b.TotalMinutes));
            }
            return res;
        }
        public string Save() { return string.Join(";", "sym=" + Symbol, "tf=" + Tf, "look=" + Lookback, "entry=" + EntryLots.ToString(IC), "add=" + AddLots.ToString(IC), "target=" + Target.ToString(IC), "dd=" + MaxDrawdown.ToString(IC), "comm=" + CommissionPerLot.ToString(IC), "first=" + FirstDir, "next=" + NextStart, "pause=" + PauseSec, "afterdd=" + AfterDrawdown, "rolling=" + (Rolling ? 1 : 0), "maxit=" + MaxIterations, "hmode=" + HedgeMode, "hprice=" + HedgePrice.ToString(IC), "hmoney=" + HedgeMoney.ToString(IC), "emode=" + EntryMode, "fvol=" + FlowVol.ToString(IC), "fshare=" + FlowShare.ToString(IC), "perday=" + PerDay, "win=" + Windows.Replace(";", ","), "nyoff=" + NyOffsetHours, "news=" + NewsMode, "nb=" + NewsBefore, "na=" + NewsAfter, "win2=" + (AddToWinner ? 1 : 0), "wlots=" + WinnerLots.ToString(IC), "wmax=" + MaxWinnerAdds); }
        public static AutoSettings Load(string line)
        {
            var s = new AutoSettings(); if (string.IsNullOrEmpty(line)) return s; var d = new Dictionary<string, string>();
            foreach (var kv in line.Split(';')) { int i = kv.IndexOf('='); if (i > 0) d[kv.Substring(0, i)] = kv.Substring(i + 1); }
            Func<string, double, double> n = (k, def) => { string v; double x; return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, IC, out x) ? x : def; };
            Func<string, string, string> t = (k, def) => { string v; return d.TryGetValue(k, out v) && v != "" ? v : def; };
            s.Symbol = t("sym", s.Symbol); s.Tf = (int)n("tf", 1); s.Lookback = (int)n("look", 1); s.EntryLots = n("entry", 0.1); s.AddLots = n("add", 0.2); s.Target = n("target", 20); s.MaxDrawdown = n("dd", 0); s.CommissionPerLot = n("comm", 0);
            s.FirstDir = t("first", "AUTO"); s.NextStart = t("next", "BREAK"); s.PauseSec = (int)n("pause", 60); s.AfterDrawdown = t("afterdd", "STOP"); s.Rolling = n("rolling", 0) == 1; s.MaxIterations = (int)n("maxit", 0); s.HedgeMode = t("hmode", "BREAK"); s.HedgePrice = n("hprice", 5); s.HedgeMoney = n("hmoney", 100);
            s.EntryMode = t("emode", "BREAK"); s.FlowVol = n("fvol", 1.5); s.FlowShare = n("fshare", 0.7); s.PerDay = (int)n("perday", 0); s.Windows = t("win", ""); s.NyOffsetHours = (int)n("nyoff", -7); s.NewsMode = t("news", "IGNORE"); s.NewsBefore = (int)n("nb", 15); s.NewsAfter = (int)n("na", 15); s.AddToWinner = n("win2", 0) == 1; s.WinnerLots = n("wlots", 0.1); s.MaxWinnerAdds = (int)n("wmax", 0);
            return s;
        }
    }

    public sealed class AutoTrade { public int Iter; public DateTime Time; public string Side = "", Why = ""; public double Lots, Price, Level, Basket; }
    public sealed class AutoIteration
    {
        public int N; public DateTime Start, End; public string FirstDir = "", Reason = ""; public int Buys, Sells, AddOns, WinnerAdds;
        public double BuyLots, SellLots, MaxGross, MaxDd, Result, BalanceStart, BalanceEnd, Estimate, Peak; public bool Adopted;
        public bool Done { get { return End > DateTime.MinValue; } }
        public bool Won { get { return Done && Result > 0; } }
    }

    public sealed class HedgeAuto
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public AutoSettings Set = new AutoSettings();
        public bool Running { get; private set; }
        public string Phase { get; private set; } = "OFF";   // OFF / WAIT BREAK / PAUSE / IN ITERATION / CLOSING
        public string StopReason = "";
        public readonly List<AutoIteration> Iterations = new List<AutoIteration>();
        public readonly List<AutoTrade> Trades = new List<AutoTrade>();
        public readonly List<string> Log = new List<string>();
        public readonly List<Tuple<int, string>> Story = new List<Tuple<int, string>>();   // (iteration, what happened and WHY — the candle, the level, the prices, the $)
        public AutoIteration Current;
        public int Version;                                      // changes when anything above changes (the window repaints)
        public double Basket, HedgeLevel = double.NaN, BuyLevel = double.NaN, SellLevel = double.NaN;   // live numbers for the window
        public DateTime Now, PauseUntil = DateTime.MinValue;
        // the order path: (command, args, label) → the command's id, or null with the reason in `refused`
        public Func<string, object[], string, string> Send;
        public string Refused = "";
        // the news: (server time, minutes before, minutes after) → the high-impact event near that moment, or null (the window gives it)
        public Func<DateTime, int, int, string> NewsNear;
        public double Vwap = double.NaN; public string SignalText = "", HedgeWhy = "", Blocked = "";

        int lastDir; double prevBid = double.NaN;
        string pendId, pendKind; DateTime pendSent, pendOk; double pendGross; int pendDirAfter; double pendLots; string pendWhy; double pendLevel;
        DateTime closeSeen;
        // the entry setups, worked out once per finished candle
        DateTime sigBar = DateTime.MinValue, vwapDay = DateTime.MinValue, vwapLast = DateTime.MinValue; double cumPV, cumV;
        sealed class Fvg { public int Dir; public double Top, Bottom; public DateTime Time; }
        readonly List<Fvg> fvgs = new List<Fvg>(); int flowDir; double flowLevel = double.NaN; DateTime flowBar, flowUntil;
        static double BuyShare(Mt5Bar b) { double r = b.H - b.L; if (r <= 0) return 0.5; double pos = (b.C - b.L) / r, body = (b.C - b.O) / r; return Math.Max(0, Math.Min(1, 0.5 * pos + 0.5 * (0.5 + 0.5 * body))); }   // the chart's estimate (ChartFlow)

        void Tell(string s) { Story.Add(Tuple.Create(Current != null ? Current.N : 0, Now.ToString("dd MMM yy HH:mm:ss", IC) + "  " + s)); if (Story.Count > 5000) Story.RemoveRange(0, 1000); Version++; }
        string P(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.#####", IC); }
        string M(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N2", IC); }
        // the candle(s) behind the recent high / low, in words
        string Candles(Snap sn, bool high)
        {
            if (sn.Done.Count == 0) return "(no finished candles yet)"; var best = high ? sn.Done.OrderByDescending(x => x.H).First() : sn.Done.OrderBy(x => x.L).First();
            string one = Tf.Name(Set.Tf) + " candle " + best.Time.ToString("HH:mm", IC) + " (O " + P(best.O) + " H " + P(best.H) + " L " + P(best.L) + " C " + P(best.C) + ")";
            return sn.Done.Count == 1 ? "the previous " + one : "the " + (high ? "highest" : "lowest") + " of the last " + sn.Done.Count + " " + Tf.Name(Set.Tf) + " candles (" + sn.Done.Last().Time.ToString("HH:mm", IC) + "–" + sn.Done.First().Time.ToString("HH:mm", IC) + ") — the " + one;
        }
        // where the bid has to go for the basket to reach the target (net lots × $ per price), and the next add-on
        string Plan(Snap sn)
        {
            double net = sn.Pos.Sum(p => p.Dir * p.Lots), basket = sn.Pos.Count > 0 ? BasketOf(sn) : 0, tgt = sn.PerPrice > 0 && Math.Abs(net) > 1e-9 ? sn.Bid + (Set.Target - basket) / (net * sn.PerPrice) : double.NaN;
            string hm = Set.HedgeMode == "PRICE" ? Set.HedgePrice.ToString("0.#####", IC) + " against the last leg" : Set.HedgeMode == "MONEY" ? "−$" + Set.HedgeMoney.ToString("0.##", IC) + " since the last leg" : "the break of " + (lastDir > 0 ? "the recent low" : "the recent high");
            return "Now " + sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots).ToString("0.##", IC) + " bought / " + sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots).ToString("0.##", IC) + " sold (net " + (net >= 0 ? "+" : "") + net.ToString("0.##", IC) + "), basket " + M(basket)
                + ". TAKE PROFIT: the basket closes at " + M(Set.Target) + (double.IsNaN(tgt) ? "" : " — at a bid of ≈ " + P(tgt)) + ". NEXT ADD-ON " + (lastDir > 0 ? "SELL " : "BUY ") + Set.AddLots.ToString("0.##", IC) + " if the bid " + (lastDir > 0 ? "falls to " : "rises to ") + P(HedgeLevel) + " (" + hm + ")"
                + (Set.MaxDrawdown > 0 ? ". MAX DRAWDOWN: closes at −$" + Set.MaxDrawdown.ToString("0.##", IC) : "") + ".";
        }
        public void Note(string s) { Log.Add((Now > DateTime.MinValue ? Now : DateTime.Now).ToString("dd MMM HH:mm:ss", IC) + "  " + s); if (Log.Count > 3000) Log.RemoveRange(0, 1000); Version++; }

        // ---- start / stop
        // a problem that stops the start ("" = fine); adopt = the positions already open on the symbol become the current iteration
        public string Check(IBroker b, AutoSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.Symbol)) return "choose a symbol";
            if (s.EntryLots <= 0 || s.AddLots <= 0) return "the entry and add-on lots must be above 0";
            if (s.Target <= 0) return "the target per iteration must be above $0";
            if (!Tf.All.Contains(s.Tf)) return "the timeframe must be one of M1 … W1";
            if (s.Lookback < 1) return "the recent high / low needs at least 1 candle";
            if (s.HedgeMode == "PRICE" && s.HedgePrice <= 0) return "the add-on price distance must be above 0";
            if (s.HedgeMode == "MONEY" && s.HedgeMoney <= 0) return "the add-on $ amount must be above 0";
            if (s.AddToWinner && s.WinnerLots <= 0) return "the add-to-the-winner lots must be above 0";
            lock (b.State.Gate)
            {
                var st = b.State; if (!st.Symbols.ContainsKey(s.Symbol)) return s.Symbol + " is not in MT5's symbols (Market Watch)";
                if (!b.IsSim && st.Account.Mode == "NET") return "this MT5 account is NETTING — the strategy needs a HEDGING account (buys and sells side by side)";
                if (st.MaxLots > 0 && Math.Max(s.EntryLots, s.AddLots) > st.MaxLots + 1e-9) return "the bridge allows at most " + st.MaxLots.ToString("0.##", IC) + " lots an order (SpasiXBridge input MaxLotsPerOrder) — raise it in MT5 or use smaller lots";
            }
            return "";
        }
        public void Start(IBroker b, AutoSettings s, bool adopt)
        {
            Set = s.Copy(); Running = true; StopReason = ""; lastDir = 0; prevBid = double.NaN; pendId = null; PauseUntil = DateTime.MinValue; Current = null;
            Snap sn = Read(b); Now = sn.Now;
            Note("▶ START " + Set.Symbol + " " + Tf.Name(Set.Tf) + " • entry " + Set.EntryLots.ToString("0.##", IC) + " • add-on " + Set.AddLots.ToString("0.##", IC) + " • target $" + Set.Target.ToString("0.##", IC) + " • max drawdown " + (Set.MaxDrawdown > 0 ? "$" + Set.MaxDrawdown.ToString("0.##", IC) : "none") + " • first " + Set.FirstDir + " • next " + (Set.NextStart == "PAUSE" ? "pause " + Set.PauseSec + " s" : "the next break"));
            if (sn.Pos.Count > 0 && adopt)
            {
                var last = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).Last(); lastDir = last.Dir;
                Current = new AutoIteration { N = Iterations.Count + 1, Start = Now, FirstDir = sn.Pos.OrderBy(p => p.Time).First().Side, BalanceStart = sn.Balance, Adopted = true, Buys = sn.Pos.Count(p => p.Dir > 0), Sells = sn.Pos.Count(p => p.Dir < 0), BuyLots = sn.Pos.Where(p => p.Dir > 0).Sum(p => p.Lots), SellLots = sn.Pos.Where(p => p.Dir < 0).Sum(p => p.Lots) };
                Current.AddOns = Math.Max(0, sn.Pos.Count - 1); Iterations.Add(Current); Phase = "IN ITERATION"; SetHedgeLevel(sn, lastDir);
                Note("⤴ adopted the " + sn.Pos.Count + " position(s) open on " + Set.Symbol + " as iteration #" + Current.N);
            }
            else Wait(sn, Set.FirstDir == "BUY" || Set.FirstDir == "SELL" ? "FIRST" : "BREAK");
            Version++;
        }
        public void Stop(string why) { if (!Running) return; Running = false; Phase = "OFF"; StopReason = why; pendId = null; Note("■ STOP — " + why); }

        // ---- one look at the market (every tick of the BACKTEST, every refresh live)
        sealed class Snap { public DateTime Now; public double Bid = double.NaN, Ask = double.NaN, Balance; public List<Mt5Position> Pos = new List<Mt5Position>(); public List<Mt5Bar> Done = new List<Mt5Bar>(), Fin = new List<Mt5Bar>(); public bool Loaded; public Tuple<bool, string> Res; public double PerPrice; }   // PerPrice = $ per 1.0 of price per lot
        Snap Read(IBroker b)
        {
            var sn = new Snap();
            lock (b.State.Gate)
            {
                var st = b.State; sn.Now = st.ServerNow > DateTime.MinValue ? st.ServerNow : DateTime.Now; Mt5Quote q; if (st.Quotes.TryGetValue(Set.Symbol, out q)) { sn.Bid = q.Bid; sn.Ask = q.Ask; }
                sn.Balance = st.Account.Balance; Mt5Symbol ms; if (st.Symbols.TryGetValue(Set.Symbol, out ms) && ms.TickSize > 0) sn.PerPrice = ms.TickValue / ms.TickSize;
                foreach (var p in st.Positions) if (string.Equals(p.Symbol, Set.Symbol, StringComparison.OrdinalIgnoreCase)) sn.Pos.Add(p);
                var se = st.SeriesOf(Set.Symbol, Set.Tf); sn.Loaded = se.Bars.Count > 0;
                for (int i = se.Bars.Count - 1; i >= 0 && sn.Fin.Count < Math.Max(60, Set.Lookback); i--) if (se.Bars[i].Time.AddMinutes(Set.Tf) <= sn.Now) sn.Fin.Add(se.Bars[i]);
                sn.Done.AddRange(sn.Fin.Take(Set.Lookback));
                // VWAP of the server day: the finished candles not counted yet (typical price × tick volume)
                if (Set.EntryMode == "VWAP" && sn.Fin.Count > 0 && sn.Fin[0].Time > vwapLast)
                {
                    var add = new List<Mt5Bar>(); for (int i = se.Bars.Count - 1; i >= 0; i--) { var x = se.Bars[i]; if (x.Time <= vwapLast) break; if (x.Time.AddMinutes(Set.Tf) <= sn.Now) add.Add(x); }
                    for (int i = add.Count - 1; i >= 0; i--) { var x = add[i]; if (x.Time.Date != vwapDay) { vwapDay = x.Time.Date; cumPV = 0; cumV = 0; } double v = Math.Max(1, x.V); cumPV += (x.H + x.L + x.C) / 3 * v; cumV += v; vwapLast = x.Time; }
                    Vwap = cumV > 0 && vwapDay == sn.Now.Date ? cumPV / cumV : double.NaN;
                }
                if (!b.IsSim && !se.Loaded && (DateTime.Now - se.AskedAt).TotalSeconds > 5) { se.AskedAt = DateTime.Now; b.Send("BARS|" + Set.Symbol + "|" + Set.Tf + "|300"); }
                if (pendId != null) for (int i = st.Results.Count - 1; i >= 0; i--) if (st.Results[i].Item1 == pendId) { sn.Res = Tuple.Create(st.Results[i].Item2, st.Results[i].Item3); break; }
            }
            return sn;
        }
        double High(Snap sn) { return sn.Done.Count < Set.Lookback ? double.NaN : sn.Done.Max(x => x.H); }
        double Low(Snap sn) { return sn.Done.Count < Set.Lookback ? double.NaN : sn.Done.Min(x => x.L); }
        static double Gross(List<Mt5Position> p) { return p.Sum(x => x.Lots); }
        double BasketOf(Snap sn) { return sn.Pos.Sum(p => p.Profit + p.Swap + p.Commission) - (Set.CommissionPerLot > 0 ? Gross(sn.Pos) * Set.CommissionPerLot : 0); }

        public void Step(IBroker b)
        {
            if (!Running) return;
            var sn = Read(b); if (sn.Now < (b.IsSim ? Now : Now.AddMinutes(-1))) { Stop("the clock went back (the backtest was rewound or restarted)"); return; }
            Now = sn.Now; if (double.IsNaN(sn.Bid) || sn.Bid <= 0) return;
            Basket = sn.Pos.Count > 0 ? BasketOf(sn) : 0;
            BuyLevel = High(sn); SellLevel = Low(sn);
            try { Decide(b, sn); } finally { prevBid = sn.Bid; }
        }

        void Decide(IBroker b, Snap sn)
        {
            // an order on its way: wait for MT5's answer, then for the position list to show it
            if (pendId != null)
            {
                if (sn.Res == null) { if ((DateTime.Now - pendSent).TotalSeconds > 20 && !b.IsSim) Stop("MT5 did not answer the " + pendKind + " in 20 s — check the bridge / Algo Trading"); return; }
                if (!sn.Res.Item1)
                {
                    var no = sn.Res.Item2; pendId = null;
                    if (pendKind == "CLOSE") { Note("⚠ close refused: " + no + " — trying again"); closeSeen = DateTime.MinValue; return; }
                    Stop("MT5 refused the " + pendWhy + ": " + no); return;
                }
                if (pendOk == DateTime.MinValue) pendOk = DateTime.Now;
                bool shown = pendKind == "CLOSE" ? sn.Pos.Count == 0 : Math.Abs(Gross(sn.Pos) - pendGross) < 1e-6;
                if (!shown && (DateTime.Now - pendOk).TotalSeconds < 6) return;
                pendId = null;
                if (pendKind == "ORDER") Opened(sn);
            }

            if (Phase == "CLOSING")
            {
                if (sn.Pos.Count > 0) { if (pendId == null && (DateTime.Now - closeSeen).TotalSeconds > 2) CloseAll(sn, Current.Reason, true); return; }
                Finish(sn); return;
            }

            if (Phase == "IN ITERATION")
            {
                var it = Current;
                if (sn.Pos.Count == 0) { it.Reason = "CLOSED OUTSIDE"; Note("⚠ the positions on " + Set.Symbol + " were closed outside the automation (by hand or a stop-out)"); Finish(sn); return; }
                it.MaxDd = Math.Min(it.MaxDd, Basket); it.Peak = Math.Max(it.Peak, Basket); it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
                if (Basket >= Set.Target) { it.Reason = "TARGET"; Tell("TAKE PROFIT. WHY: the basket reached " + M(Basket) + " ≥ the target " + M(Set.Target) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "TARGET", false); return; }
                if (Set.MaxDrawdown > 0 && Basket <= -Set.MaxDrawdown) { it.Reason = "MAX DRAWDOWN"; Tell("MAX DRAWDOWN. WHY: the basket fell to " + M(Basket) + " ≤ −$" + Set.MaxDrawdown.ToString("0.##", IC) + " at a bid of " + P(sn.Bid) + " → CLOSE ALL " + sn.Pos.Count + " positions."); CloseAll(sn, "MAX DRAWDOWN", false); return; }
                if (Set.Rolling && Set.HedgeMode == "BREAK") SetHedgeLevel(sn, lastDir);
                // ADD TO THE WINNER: the setup signals again in the leading side's direction (the last leg's)
                if (Set.AddToWinner && (Set.MaxWinnerAdds == 0 || it.WinnerAdds < Set.MaxWinnerAdds))
                {
                    Signals(sn); int wd; double wl; string ww, ws;
                    if (Signal(sn, lastDir, out wd, out wl, out ww, out ws) && wd == lastDir)
                    {
                        Tell("ADD TO THE WINNER " + (wd > 0 ? "BUY " : "SELL ") + Set.WinnerLots.ToString("0.##", IC) + ". WHY: the leading side is " + (wd > 0 ? "BUY" : "SELL") + " and the setup " + Set.EntryMode + " signalled that way again: " + ws + ". The basket was " + M(Basket) + ".");
                        Order(sn, wd, Set.WinnerLots, "ADD TO THE WINNER — " + ww, wl); return;
                    }
                }
                if (!double.IsNaN(HedgeLevel) && (lastDir > 0 ? sn.Bid <= HedgeLevel : sn.Bid >= HedgeLevel))
                {
                    var leg = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault();
                    Tell("ADD-ON " + (lastDir > 0 ? "SELL " : "BUY ") + Set.AddLots.ToString("0.##", IC) + ". WHY: the last leg was a " + (lastDir > 0 ? "BUY" : "SELL") + (leg != null ? " " + leg.Lots.ToString("0.##", IC) + " @ " + P(leg.Open) + " (" + leg.Time.ToString("HH:mm:ss", IC) + ")" : "") + "; the bid " + (lastDir > 0 ? "fell to " : "rose to ") + P(sn.Bid) + ", past the add-on line " + P(HedgeLevel)
                        + (Set.HedgeMode == "PRICE" ? " = its fill " + (lastDir > 0 ? "− " : "+ ") + Set.HedgePrice.ToString("0.#####", IC) : Set.HedgeMode == "MONEY" ? " = where the basket lost $" + Set.HedgeMoney.ToString("0.##", IC) + " since that leg" : " = " + HedgeWhy + (Set.Rolling ? " (following the newest candles)" : " (fixed when the leg opened)"))
                        + ". The basket was " + M(Basket) + " (worst so far " + M(Current.MaxDd) + ") with " + (Current.Buys + Current.Sells) + " leg(s) open.");
                    Order(sn, -lastDir, Set.AddLots, (lastDir > 0 ? "ADD-ON SELL — " : "ADD-ON BUY — ") + (Set.HedgeMode == "PRICE" ? "the price moved " + Set.HedgePrice.ToString("0.#####", IC) + " against the last leg (" + HedgeLevel.ToString("0.#####", IC) + ")" : Set.HedgeMode == "MONEY" ? "−$" + Set.HedgeMoney.ToString("0.##", IC) + " since the last leg (" + HedgeLevel.ToString("0.#####", IC) + ")" : (lastDir > 0 ? "the recent low " : "the recent high ") + HedgeLevel.ToString("0.#####", IC) + " broke"), HedgeLevel);
                }
                return;
            }

            // waiting for the next iteration
            if (Set.MaxIterations > 0 && Iterations.Count(x => x.Done) >= Set.MaxIterations) { Stop("the " + Set.MaxIterations + " iterations are done"); return; }
            if (Phase == "PAUSE") { if (Now < PauseUntil) return; Phase = lastDir != 0 ? "AGAIN" : "WAIT BREAK"; }
            if (sn.Pos.Count > 0) { Note("⚠ positions on " + Set.Symbol + " that are not the automation's — waiting until they are closed"); return; }
            Signals(sn);
            var gate = Gate(); if (gate != Blocked) { Blocked = gate; if (gate != "") Tell("NOT TRADING NOW: " + gate + "."); else Tell("TRADING ALLOWED again" + (Set.Windows != "" ? " (inside " + Set.Windows + " New York)" : "") + "."); }
            if (gate != "") return;
            int dir = 0; string why = ""; double lvl = double.NaN; string story = "";
            if (Phase == "FIRST") { dir = Set.FirstDir == "BUY" ? 1 : -1; why = "FIRST " + Set.FirstDir + " (yours)"; story = "you chose " + Set.FirstDir + " as the first direction"; }
            else if (Phase == "AGAIN") { dir = lastDir; why = "AFTER THE PAUSE — the last basket's direction"; story = "the " + Set.PauseSec + " s pause after the last iteration ended; the last basket was " + (dir > 0 ? "long" : "short") + ", so " + (dir > 0 ? "BUY" : "SELL") + " again"; }
            else if (!Signal(sn, 0, out dir, out lvl, out why, out story)) return;
            Current = new AutoIteration { N = Iterations.Count + 1, Start = Now, FirstDir = dir > 0 ? "BUY" : "SELL", BalanceStart = sn.Balance }; Iterations.Add(Current); Phase = "IN ITERATION";
            Tell("ITERATION #" + Current.N + " — ENTRY " + (dir > 0 ? "BUY " : "SELL ") + Set.EntryLots.ToString("0.##", IC) + " (setup " + Set.EntryMode + "). WHY: " + story + ". Bid " + P(sn.Bid) + " / ask " + P(sn.Ask) + ", balance $" + sn.Balance.ToString("N2", IC) + ".");
            Order(sn, dir, Set.EntryLots, "ENTRY " + (dir > 0 ? "BUY" : "SELL") + " — " + why, lvl);
        }

        // the entry setup's signal now (only = 0 any side, +1 / −1 that side only — ADD TO THE WINNER): its direction, level, why and the story
        bool Signal(Snap sn, int only, out int dir, out double lvl, out string why, out string story)
        {
            dir = 0; lvl = double.NaN; why = ""; story = "";
            if (Set.EntryMode == "FVG")
            {
                var z = fvgs.FirstOrDefault(f => (only == 0 || f.Dir == only) && (f.Dir > 0 ? sn.Bid <= f.Top && sn.Bid >= f.Bottom : sn.Bid >= f.Bottom && sn.Bid <= f.Top)); if (z == null) return false;
                fvgs.Remove(z); dir = z.Dir; lvl = z.Dir > 0 ? z.Top : z.Bottom; why = (dir > 0 ? "FVG BUY — the retest of the bullish gap " : "FVG SELL — the retest of the bearish gap ") + P(z.Bottom) + "–" + P(z.Top);
                story = "a " + (dir > 0 ? "bullish" : "bearish") + " FAIR VALUE GAP " + P(z.Bottom) + "–" + P(z.Top) + " formed by the three " + Tf.Name(Set.Tf) + " candles ending " + z.Time.ToString("HH:mm", IC) + " (" + (dir > 0 ? "the 3rd candle's low above the 1st candle's high" : "the 3rd candle's high under the 1st candle's low") + "); the bid came back into it (" + P(prevBid) + " → " + P(sn.Bid) + ")";
            }
            else if (Set.EntryMode == "VWAP")
            {
                double v = Vwap; if (double.IsNaN(v) || double.IsNaN(prevBid)) return false;
                if (prevBid <= v && sn.Bid > v && only >= 0) dir = 1; else if (prevBid >= v && sn.Bid < v && only <= 0) dir = -1; else return false;
                lvl = v; why = "VWAP " + (dir > 0 ? "cross up " : "cross down ") + P(v);
                story = "the bid crossed the day's VWAP " + P(v) + " (" + Tf.Name(Set.Tf) + " typical price × tick volume since the server's midnight) " + (dir > 0 ? "upward" : "downward") + ": " + P(prevBid) + " → " + P(sn.Bid);
            }
            else if (Set.EntryMode == "FLOW")
            {
                if (flowDir == 0 || double.IsNaN(prevBid) || Now > flowUntil || (only != 0 && flowDir != only)) return false;
                if (flowDir > 0 && prevBid <= flowLevel && sn.Bid > flowLevel) dir = 1; else if (flowDir < 0 && prevBid >= flowLevel && sn.Bid < flowLevel) dir = -1; else return false;
                lvl = flowLevel; why = "ORDER FLOW " + (dir > 0 ? "BUY" : "SELL") + " — the surge candle " + flowBar.ToString("HH:mm", IC) + " broke"; story = SignalText + "; the bid broke its " + (dir > 0 ? "high " : "low ") + P(flowLevel) + " (" + P(prevBid) + " → " + P(sn.Bid) + ")"; flowDir = 0;
            }
            else
            {
                double hi = BuyLevel, lo = SellLevel; if (double.IsNaN(hi) || double.IsNaN(prevBid)) return false;
                if (prevBid <= hi && sn.Bid > hi && only >= 0) { dir = 1; lvl = hi; why = "BREAK of the recent high " + hi.ToString("0.#####", IC); }
                else if (prevBid >= lo && sn.Bid < lo && only <= 0) { dir = -1; lvl = lo; why = "BREAK of the recent low " + lo.ToString("0.#####", IC); }
                if (dir == 0) return false;
                story = "the bid moved from " + P(prevBid) + " to " + P(sn.Bid) + ", " + (dir > 0 ? "above " : "below ") + P(lvl) + " = " + Candles(sn, dir > 0) + " → the " + (dir > 0 ? "high" : "low") + " broke";
            }
            return true;
        }

        // FVG zones and the FLOW surge candle: once per new finished candle
        void Signals(Snap sn)
        {
            if (sn.Fin.Count == 0 || sn.Fin[0].Time == sigBar) return; sigBar = sn.Fin[0].Time;
            if (Set.EntryMode == "FVG" && sn.Fin.Count >= 3)
            {
                Mt5Bar c1 = sn.Fin[2], c3 = sn.Fin[0];
                if (c3.L > c1.H) { fvgs.Add(new Fvg { Dir = 1, Bottom = c1.H, Top = c3.L, Time = c3.Time }); SignalText = "bullish FVG " + P(c1.H) + "–" + P(c3.L) + " (" + c3.Time.ToString("HH:mm", IC) + ")"; }
                else if (c3.H < c1.L) { fvgs.Add(new Fvg { Dir = -1, Bottom = c3.H, Top = c1.L, Time = c3.Time }); SignalText = "bearish FVG " + P(c3.H) + "–" + P(c1.L) + " (" + c3.Time.ToString("HH:mm", IC) + ")"; }
                fvgs.RemoveAll(f => (f.Dir > 0 ? c3.C < f.Bottom : c3.C > f.Top) || f.Time < c3.Time.AddMinutes(-60 * Set.Tf)); while (fvgs.Count > 10) fvgs.RemoveAt(0);
            }
            if (Set.EntryMode == "FLOW" && sn.Fin.Count >= 21)
            {
                var c = sn.Fin[0]; double avg = sn.Fin.Skip(1).Take(20).Average(x => (double)x.V), sh = BuyShare(c);
                if (avg > 0 && c.V >= Set.FlowVol * avg && (sh >= Set.FlowShare || sh <= 1 - Set.FlowShare))
                {
                    flowDir = sh >= Set.FlowShare ? 1 : -1; flowLevel = flowDir > 0 ? c.H : c.L; flowBar = c.Time; flowUntil = c.Time.AddMinutes(Set.Tf * 6);
                    SignalText = "the " + Tf.Name(Set.Tf) + " candle " + c.Time.ToString("HH:mm", IC) + " had " + (c.V / avg).ToString("0.0", IC) + "× the tick volume of the last 20 and " + (100 * (flowDir > 0 ? sh : 1 - sh)).ToString("0", IC) + "% " + (flowDir > 0 ? "buyers" : "sellers") + " (estimated from where it closed in its range — MT5 CFDs have no real order book)";
                }
            }
        }
        // why no new iteration may start now ("" = it may): the iterations a day, the New York windows, the news
        string Gate()
        {
            if (Set.PerDay > 0) { int today = Iterations.Count(x => x.Start.Date == Now.Date); if (today >= Set.PerDay) return "the " + Set.PerDay + " iteration" + (Set.PerDay == 1 ? "" : "s") + " of today are done (server day " + Now.ToString("dd MMM", IC) + ")"; }
            var w = Set.WindowList();
            if (w != null)
            {
                if (w.Count == 0) return "the trading times \"" + Set.Windows + "\" cannot be read (use 08:30-11:00, 13:00-16:00)";
                var ny = Now.AddHours(Set.NyOffsetHours); int m = ny.Hour * 60 + ny.Minute;
                if (!w.Any(x => x.Item1 <= x.Item2 ? m >= x.Item1 && m < x.Item2 : m >= x.Item1 || m < x.Item2)) return "outside the trading times " + Set.Windows + " (New York " + ny.ToString("HH:mm", IC) + ")";
            }
            if (Set.NewsMode != "IGNORE" && NewsNear != null)
            {
                var n = NewsNear(Now, Set.NewsBefore, Set.NewsAfter);
                if (Set.NewsMode == "AVOID" && n != null) return "high-impact news " + n + " (" + Set.NewsBefore + " min before … " + Set.NewsAfter + " min after)";
                if (Set.NewsMode == "ONLY" && n == null) return "no high-impact news within " + Set.NewsBefore + " min before … " + Set.NewsAfter + " min after (news-only mode)";
            }
            return "";
        }

        void Order(Snap sn, int dir, double lots, string why, double level)
        {
            var label = (dir > 0 ? "BUY " : "SELL ") + lots.ToString("0.##", IC) + " " + Set.Symbol + " (🤖 #" + Current.N + " " + why + ")";
            Refused = ""; var id = Send == null ? null : Send("ORDER", new object[] { Set.Symbol, dir > 0 ? "BUY" : "SELL", lots, 0.0, 0.0, 0.0 }, label);
            if (id == null) { Stop("the order was not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed")); return; }
            pendId = id; pendKind = "ORDER"; pendSent = DateTime.Now; pendOk = DateTime.MinValue; pendGross = Gross(sn.Pos) + lots; pendDirAfter = dir; pendLots = lots; pendWhy = why; pendLevel = level;
            Note("→ " + label);
        }
        // the order is in: count it, the next add-on level
        void Opened(Snap sn)
        {
            var it = Current; int dir = pendDirAfter; var px = sn.Pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(p => p.Dir == dir);
            if (dir > 0) { it.Buys++; it.BuyLots += pendLots; } else { it.Sells++; it.SellLots += pendLots; }
            if (pendWhy.StartsWith("ADD TO THE WINNER")) it.WinnerAdds++; else if (it.Buys + it.Sells > 1) it.AddOns++;
            it.MaxGross = Math.Max(it.MaxGross, Gross(sn.Pos));
            Trades.Add(new AutoTrade { Iter = it.N, Time = Now, Side = dir > 0 ? "BUY" : "SELL", Lots = pendLots, Price = px != null ? px.Open : dir > 0 ? sn.Ask : sn.Bid, Level = pendLevel, Why = pendWhy, Basket = Basket });
            lastDir = dir; SetHedgeLevel(sn, dir);
            Tell("FILLED " + (dir > 0 ? "BUY " : "SELL ") + pendLots.ToString("0.##", IC) + " @ " + P(px != null ? px.Open : dir > 0 ? sn.Ask : sn.Bid) + ". " + Plan(sn)); Version++;
        }
        // the add-on line: the recent low under a buy / the recent high over a sell (at least the spread away from the price)
        void SetHedgeLevel(Snap sn, int dir)
        {
            double sp = !double.IsNaN(sn.Ask) && sn.Ask > sn.Bid ? sn.Ask - sn.Bid : 0;
            // PRICE: a fixed move from the last leg's fill; MONEY: the move that loses HedgeMoney $ on what the basket holds now (net lots × $ per price)
            if (Set.HedgeMode == "PRICE" || Set.HedgeMode == "MONEY")
            {
                var last = sn.Pos.Where(p => p.Dir == dir).OrderBy(p => p.Time).ThenBy(p => p.Ticket).LastOrDefault(); double from = last != null ? (dir > 0 ? last.Open - sp : last.Open + sp) : sn.Bid;   // the bid when the leg opened
                double net = Math.Abs(sn.Pos.Sum(p => p.Dir * p.Lots)); double dist = Set.HedgeMode == "PRICE" ? Set.HedgePrice : sn.PerPrice > 0 && net > 1e-9 ? Set.HedgeMoney / (net * sn.PerPrice) : double.NaN;
                HedgeLevel = double.IsNaN(dist) ? double.NaN : dir > 0 ? from - Math.Max(dist, sp) : from + Math.Max(dist, sp); return;
            }
            double lv = dir > 0 ? Low(sn) : High(sn);
            if (double.IsNaN(lv)) { HedgeLevel = double.NaN; HedgeWhy = ""; return; }
            HedgeLevel = Set.Rolling ? lv : dir > 0 ? Math.Min(lv, sn.Bid - sp) : Math.Max(lv, sn.Bid + sp);
            HedgeWhy = Candles(sn, dir < 0) + (Math.Abs(HedgeLevel - lv) > 1e-9 ? ", moved to " + P(HedgeLevel) + " (at least the spread from the price)" : "");
        }
        void CloseAll(Snap sn, string reason, bool again)
        {
            Current.Estimate = Basket; Phase = "CLOSING"; closeSeen = DateTime.Now;
            Refused = ""; var id = Send == null ? null : Send("CLOSEALL", new object[] { Set.Symbol, "ALL" }, "CLOSE ALL " + Set.Symbol + " (🤖 #" + Current.N + " " + reason + ", basket " + Basket.ToString("+0.00;−0.00", IC) + ")");
            if (id == null) { Note("⚠ close all not sent: " + (Refused != "" ? Refused : "trading is not armed / allowed") + " — trying again"); return; }
            pendId = id; pendKind = "CLOSE"; pendSent = DateTime.Now; pendOk = DateTime.MinValue;
            if (!again) Note("■ " + reason + " — closing the basket at " + Basket.ToString("+0.00;−0.00", IC));
        }
        void Finish(Snap sn)
        {
            var it = Current; it.End = Now; it.BalanceEnd = sn.Balance; it.Result = Math.Abs(sn.Balance - it.BalanceStart) > 1e-9 || it.Estimate == 0 ? sn.Balance - it.BalanceStart : it.Estimate;
            if (it.Reason == "") it.Reason = "CLOSED";
            Tell("CLOSED — iteration #" + it.N + " " + it.Reason + " " + M(it.Result) + " (balance $" + it.BalanceStart.ToString("N2", IC) + " → $" + sn.Balance.ToString("N2", IC) + "), " + it.Buys + " buys / " + it.Sells + " sells, " + it.AddOns + " add-ons, most lots open " + it.MaxGross.ToString("0.##", IC) + ", worst drawdown " + M(it.MaxDd) + ", " + (it.End - it.Start).ToString(@"hh\:mm\:ss", IC) + ".");
            Note((it.Result >= 0 ? "✔" : "✖") + " iteration #" + it.N + " " + it.Reason + " " + it.Result.ToString("+0.00;−0.00", IC) + " • " + it.Buys + " buys / " + it.Sells + " sells • " + it.AddOns + " add-ons • max drawdown " + it.MaxDd.ToString("0.00", IC));
            int dir = it.BuyLots > it.SellLots + 1e-9 ? 1 : it.SellLots > it.BuyLots + 1e-9 ? -1 : lastDir; lastDir = dir;
            Current = null; HedgeLevel = double.NaN; Version++;
            if (it.Reason == "MAX DRAWDOWN" && Set.AfterDrawdown == "STOP") { Stop("the max drawdown $" + Set.MaxDrawdown.ToString("0.##", IC) + " was hit"); return; }
            if (it.Reason == "CLOSED OUTSIDE") { Stop("the basket was closed outside the automation"); return; }
            if (Set.MaxIterations > 0 && Iterations.Count(x => x.Done) >= Set.MaxIterations) { Stop("the " + Set.MaxIterations + " iterations are done"); return; }
            Wait(sn, Set.NextStart == "PAUSE" ? "PAUSE" : "BREAK");
        }
        void Wait(Snap sn, string how)
        {
            if (how == "FIRST") { Phase = "FIRST"; return; }
            if (how == "PAUSE") { Phase = "PAUSE"; PauseUntil = Now.AddSeconds(Math.Max(0, Set.PauseSec)); Note("⏸ next iteration after " + Set.PauseSec + " s (" + PauseUntil.ToString("HH:mm:ss", IC) + ")"); return; }
            Phase = "WAIT BREAK"; Note("⏳ waiting for the next break of the recent high / low (" + Tf.Name(Set.Tf) + ", " + Set.Lookback + " candle" + (Set.Lookback == 1 ? "" : "s") + ")");
        }

        // ---- the numbers for the ANALYSIS
        public sealed class Stats { public int Done, Wins, Losses, Streak, BestStreak, WorstStreak, Buys, Sells, AddOns, MostAddOns; public double Net, Avg, WorstDd, BestIt, WorstIt; }
        public Stats Calc(IEnumerable<AutoIteration> list)
        {
            var d = list.Where(x => x.Done).ToList(); var s = new Stats { Done = d.Count, Wins = d.Count(x => x.Won), Losses = d.Count(x => !x.Won), Net = d.Sum(x => x.Result), Buys = d.Sum(x => x.Buys), Sells = d.Sum(x => x.Sells), AddOns = d.Sum(x => x.AddOns) };
            s.Avg = d.Count > 0 ? s.Net / d.Count : 0; s.WorstDd = d.Count > 0 ? d.Min(x => x.MaxDd) : 0; s.MostAddOns = d.Count > 0 ? d.Max(x => x.AddOns) : 0; s.BestIt = d.Count > 0 ? d.Max(x => x.Result) : 0; s.WorstIt = d.Count > 0 ? d.Min(x => x.Result) : 0;
            int w = 0, l = 0; foreach (var x in d) { if (x.Won) { w++; l = 0; } else { l++; w = 0; } s.BestStreak = Math.Max(s.BestStreak, w); s.WorstStreak = Math.Max(s.WorstStreak, l); }
            s.Streak = w > 0 ? w : -l; return s;
        }
    }
}
