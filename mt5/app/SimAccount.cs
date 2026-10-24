// SpasiX — the SIMULATED ACCOUNT behind the BACKTEST (history) and the live DEMO (real prices from your MT5): a HEDGING account on any number of
// symbols. It answers the same commands as the bridge (ORDER / CLOSE / CLOSEALL / MODIFY / MODORD / CANCEL / CANCELALL / BE): market orders at
// the ask / bid (+ slippage), limits at their price, stops at the market, stops / targets on every price, commission per lot (RAW: half in, half
// out; STANDARD: none — the spread is wider instead), swap at the server's midnight in MT5's own swap mode (points / money / interest, triple
// day from the symbol), margin with the leverage and a stop-out. Nothing here touches the real account.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class SimAccountSettings
    {
        public double Balance = 10000, CommissionPerLot = 7, StopOutPct = 50, MarkupPts = 0, SwapLong = 0, SwapShort = 0; public int Leverage = 500, SlipPts = 0;
        public string Currency = "USD", Kind = "RAW";   // RAW = commission + the raw spread, STANDARD = no commission + the spread widened by MarkupPts
        public double Commission { get { return Kind == "STANDARD" ? 0 : CommissionPerLot; } }
    }

    public sealed class SimAccount
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public readonly Mt5State State; public SimAccountSettings Set = new SimAccountSettings();
        public readonly Dictionary<string, SimSpec> Specs = new Dictionary<string, SimSpec>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, double[]> px = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);   // bid, ask
        public long NextTicket = 50000001, NextDeal = 70000001; public double Balance; public DateTime Now = DateTime.MinValue;
        public readonly List<string> Notes = new List<string>();
        public SimAccount(Mt5State state) { State = state; }

        public void Reset(SimAccountSettings s, DateTime t)
        {
            Set = s; Balance = s.Balance; Now = t; px.Clear(); Notes.Clear();
            State.Positions = new List<Mt5Position>(); State.Orders = new List<Mt5Order>(); State.Deals = new List<Mt5Deal>();
            State.Account = new Mt5Account { Login = 0, Name = "SIMULATED", Server = "SpasiX simulator", Currency = s.Currency, Balance = Balance, Equity = Balance, FreeMargin = Balance, Leverage = s.Leverage, Mode = "HEDGE", Connected = true, Kind = "SIM" };
            State.Deals.Add(new Mt5Deal { Ticket = NextDeal++, Time = t, Type = "BALANCE", Profit = Balance, Comment = "start balance" }); State.DealsVersion++; State.DealsAt = DateTime.Now; State.DealsFrom = t.Date;
        }
        public bool HasPrice(string sym) { return px.ContainsKey(sym); }
        public double Bid(string sym) { double[] p; return px.TryGetValue(sym, out p) ? p[0] : 0; }
        public double Ask(string sym) { double[] p; return px.TryGetValue(sym, out p) ? p[1] : 0; }
        SimSpec SpecOf(string sym) { SimSpec s; if (!Specs.TryGetValue(sym, out s)) throw new Exception("no spec for " + sym); return s; }

        // a new price of one symbol (ask = what this account pays)
        public void OnPrice(string sym, DateTime t, double bid, double ask)
        {
            if (Now > DateTime.MinValue && t.Date > Now.Date) Rollover(Now);
            if (t > Now) Now = t;
            double[] p; if (!px.TryGetValue(sym, out p)) px[sym] = p = new double[2]; p[0] = bid; p[1] = Math.Max(bid, ask);
            Triggers(sym, t); Mark(); StopOut(t);
        }
        // swap for every position held over the server's midnight (its symbol's triple day ×3; none on Saturday / Sunday nights)
        void Rollover(DateTime day)
        {
            if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) return;
            foreach (var p in State.Positions)
            {
                SimSpec s; if (!Specs.TryGetValue(p.Symbol, out s)) continue; int mult = day.DayOfWeek == s.Swap3Day ? 3 : 1;
                p.Swap += Math.Round(SwapPerLot(s, p.Dir, p.Current > 0 ? p.Current : p.Open) * p.Lots * mult, 2);
            }
        }
        public double SwapPerLot(SimSpec s, int dir, double price)
        {
            if (Set.SwapLong != 0 || Set.SwapShort != 0) return dir > 0 ? Set.SwapLong : Set.SwapShort;   // your own numbers ($ per lot)
            double v = dir > 0 ? s.SwapLong : s.SwapShort;
            switch (s.SwapMode)
            {
                case 1: return v * s.TickValue * (s.Point / s.TickSize);           // points
                case 2: case 3: case 4: return v;                                   // money per lot
                case 5: case 6: return price * s.Contract * v / 100.0 / 360.0;     // annual interest %
                case 7: case 8: return v * s.TickValue * (s.Point / s.TickSize);   // reopen (≈ points)
            }
            return 0;
        }
        void Triggers(string sym, DateTime t)
        {
            double bid = Bid(sym), ask = Ask(sym); var spec = SpecOf(sym);
            foreach (var o in State.Orders.Where(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                bool fire = o.Type == "BUY LIMIT" ? ask <= o.Price : o.Type == "SELL LIMIT" ? bid >= o.Price : o.Type == "BUY STOP" ? ask >= o.Price : o.Type == "SELL STOP" ? bid <= o.Price : false;
                if (!fire) continue;
                State.Orders.Remove(o); int dir = o.Type.StartsWith("BUY") ? 1 : -1;
                double fill = o.Type.EndsWith("LIMIT") ? o.Price : (dir > 0 ? ask : bid) + dir * Set.SlipPts * spec.Point;
                Open(t, sym, dir, o.Lots, Round(spec, fill), o.Sl, o.Tp, "#" + o.Ticket + " " + o.Type.ToLowerInvariant(), o.Ticket);
                Notes.Add("⏳ " + o.Type + " #" + o.Ticket + " " + sym + " filled @ " + fill.ToString("F" + spec.Digits, IC));
            }
            foreach (var p in State.Positions.Where(x => string.Equals(x.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (p.Dir > 0) { if (p.Sl > 0 && bid <= p.Sl) Close(t, p, p.Lots, bid, "SL"); else if (p.Tp > 0 && bid >= p.Tp) Close(t, p, p.Lots, p.Tp, "TP"); }
                else { if (p.Sl > 0 && ask >= p.Sl) Close(t, p, p.Lots, ask, "SL"); else if (p.Tp > 0 && ask <= p.Tp) Close(t, p, p.Lots, p.Tp, "TP"); }
            }
        }
        static double Round(SimSpec s, double v) { return s.Point > 0 ? Math.Round(v / s.Point) * s.Point : v; }
        static double Pl(SimSpec s, int dir, double open, double close, double lots) { return (close - open) * dir * lots * s.TickValue / s.TickSize; }
        // the margin of ONE lot at this price: MT5's own number (scaled by the price and by your leverage vs the account's then) or contract × price ÷ leverage
        public double MarginPerLot(SimSpec s, double price)
        {
            if (s.MarginPerLot > 0 && s.MarginRefPrice > 0)
            {
                var n = s.Name.ToUpperInvariant(); bool usdBase = n.Length >= 6 && n.StartsWith("USD") && n.Substring(0, 6).All(char.IsLetter);
                double m = s.MarginPerLot * (usdBase ? 1 : price / s.MarginRefPrice); if (s.MarginLeverage > 0 && Set.Leverage > 0) m *= (double)s.MarginLeverage / Set.Leverage; return m;
            }
            var nn = s.Name.ToUpperInvariant(); bool ub = nn.Length >= 6 && nn.StartsWith("USD") && nn.Substring(0, 6).All(char.IsLetter);
            return s.Contract * (ub ? 1 : price) / Math.Max(1, Set.Leverage);
        }
        // the margin of a symbol's positions, hedging like MT5: the uncovered lots at full margin, the covered (hedged) lots at the hedged margin
        // (MT5's SYMBOL_MARGIN_HEDGED: 0 = a hedged pair needs no margin, e.g. many indices); "larger leg" symbols: only the bigger side counts
        public double MarginOfSymbol(SimSpec s, double buyLots, double sellLots, double price)
        {
            double per = MarginPerLot(s, price); if (s.MarginUseLeg) return Math.Max(buyLots, sellLots) * per;
            double hedged = Math.Min(buyLots, sellLots), open = Math.Abs(buyLots - sellLots); double ratio = s.HedgedRatio < 0 ? 0.5 : s.HedgedRatio;
            return open * per + 2 * hedged * per * ratio;
        }
        double TotalMargin(string addSym = null, int addDir = 0, double addLots = 0, double addPrice = 0)
        {
            double mg = 0; var syms = State.Positions.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); if (addSym != null && !syms.Contains(addSym, StringComparer.OrdinalIgnoreCase)) syms.Add(addSym);
            foreach (var sym in syms)
            {
                SimSpec s; if (!Specs.TryGetValue(sym, out s)) continue; var ps = State.Positions.Where(p => string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)).ToList();
                double b = ps.Where(p => p.Dir > 0).Sum(p => p.Lots), se = ps.Where(p => p.Dir < 0).Sum(p => p.Lots), px0 = Bid(sym) > 0 ? Bid(sym) : (ps.Count > 0 ? ps[0].Open : addPrice);
                if (string.Equals(sym, addSym, StringComparison.OrdinalIgnoreCase)) { if (addDir > 0) b += addLots; else se += addLots; if (px0 <= 0) px0 = addPrice; }
                mg += MarginOfSymbol(s, b, se, px0);
            }
            return mg;
        }
        public void Mark()
        {
            double fl = 0, mg = 0;
            foreach (var p in State.Positions)
            {
                SimSpec s; if (!Specs.TryGetValue(p.Symbol, out s)) continue; double b = Bid(p.Symbol), a = Ask(p.Symbol);
                if (b > 0) { p.Current = p.Dir > 0 ? b : a; p.Profit = Math.Round(Pl(s, p.Dir, p.Open, p.Current, p.Lots), 2); }
                fl += p.Profit + p.Swap;
            }
            mg = TotalMargin();
            var ac = State.Account; ac.Balance = Math.Round(Balance, 2); ac.Equity = Math.Round(Balance + fl, 2); ac.Margin = Math.Round(mg, 2); ac.FreeMargin = Math.Round(ac.Equity - mg, 2); ac.MarginLevel = mg > 0 ? ac.Equity / mg * 100 : 0;
        }
        void StopOut(DateTime t)
        {
            for (int guard = 0; guard < 200; guard++)
            {
                var a = State.Account; if (a.Margin <= 0 || a.MarginLevel >= Set.StopOutPct || State.Positions.Count == 0) return;
                var worst = State.Positions.OrderBy(p => p.Profit + p.Swap).First(); Close(t, worst, worst.Lots, worst.Current, "STOPOUT");
                Notes.Add("⚠ STOP OUT: margin level under " + Set.StopOutPct.ToString("0", IC) + " % — #" + worst.Ticket + " closed"); Mark();
            }
        }
        Mt5Position Open(DateTime t, string sym, int dir, double lots, double fill, double sl, double tp, string comment, long ticket = 0)
        {
            double comm = -Math.Round(Set.Commission / 2 * lots, 2); Balance += comm;
            var p = new Mt5Position { Ticket = ticket > 0 ? ticket : NextTicket++, Symbol = SpecOf(sym).Name, Side = dir > 0 ? "BUY" : "SELL", Lots = lots, Open = fill, Sl = sl, Tp = tp, Current = dir > 0 ? Bid(sym) : Ask(sym), Time = t, Comment = comment, Commission = comm };
            State.Positions.Add(p);
            State.Deals.Add(new Mt5Deal { Ticket = NextDeal++, Position = p.Ticket, Time = t, Symbol = p.Symbol, Type = p.Side, Entry = "IN", Lots = lots, Price = fill, Commission = comm, Comment = comment }); State.DealsVersion++;
            Mark(); return p;
        }
        void Close(DateTime t, Mt5Position p, double lots, double price, string reason)
        {
            var s = SpecOf(p.Symbol); lots = Math.Min(p.Lots, lots); double frac = p.Lots > 0 ? lots / p.Lots : 1;
            double pl = Math.Round(Pl(s, p.Dir, p.Open, price, lots), 2), comm = -Math.Round(Set.Commission / 2 * lots, 2), sw = Math.Round(p.Swap * frac, 2);
            Balance += pl + comm + sw; p.Swap -= sw;
            State.Deals.Add(new Mt5Deal { Ticket = NextDeal++, Position = p.Ticket, Time = t, Symbol = p.Symbol, Type = p.Dir > 0 ? "SELL" : "BUY", Entry = "OUT", Lots = lots, Price = price, Profit = pl, Commission = comm, Swap = sw, Reason = reason }); State.DealsVersion++;
            if (lots >= p.Lots - 1e-9) State.Positions.Remove(p); else p.Lots = Math.Round(p.Lots - lots, 2);
            if (reason == "SL" || reason == "TP") Notes.Add((reason == "TP" ? "🎯 TARGET" : "🛑 STOP") + " #" + p.Ticket + " " + p.Symbol + " " + (pl + comm >= 0 ? "+" : "−") + "$" + Math.Abs(pl + comm).ToString("N2", IC));
            Mark();
        }

        // ---- the commands
        static double N(object o) { return Convert.ToDouble(o, IC); }
        static long Tk(object o) { return Convert.ToInt64(o, IC); }
        static double Lots(SimSpec s, double v) { double st = s.VolStep > 0 ? s.VolStep : 0.01; v = Math.Floor(v / st + 1e-9) * st; return Math.Round(Math.Max(s.VolMin, Math.Min(s.VolMax, v)), 2); }
        static string F(SimSpec s, double v) { return v.ToString("F" + s.Digits, IC); }
        static Tuple<bool, string> Ok(string s) { return Tuple.Create(true, s); }
        static Tuple<bool, string> No(string s) { return Tuple.Create(false, s); }
        static bool StopsOk(int dir, double px, double sl, double tp) { return dir > 0 ? (sl <= 0 || sl < px) && (tp <= 0 || tp > px) : (sl <= 0 || sl > px) && (tp <= 0 || tp < px); }
        public Tuple<bool, string> Do(string cmd, object[] a, DateTime t)
        {
            switch (cmd)
            {
                case "ORDER":
                    {
                        string sym = Convert.ToString(a[0], IC), kind = Convert.ToString(a[1], IC); SimSpec s; if (!Specs.TryGetValue(sym, out s)) return No(kind + " " + sym + ": this symbol is not loaded");
                        if (!HasPrice(sym)) return No(kind + " " + sym + ": no price yet");
                        double lots = Lots(s, N(a[2])), price = N(a[3]), sl = Round(s, N(a[4])), tp = Round(s, N(a[5])); int dir = kind.StartsWith("BUY") ? 1 : -1; double bid = Bid(sym), ask = Ask(sym);
                        if (N(a[2]) <= 0) return No(kind + ": the lots are 0");
                        if (kind == "BUY" || kind == "SELL")
                        {
                            double fill = Round(s, (dir > 0 ? ask : bid) + dir * Set.SlipPts * s.Point);
                            if (!StopsOk(dir, dir > 0 ? bid : ask, sl, tp)) return No(kind + " " + lots.ToString("0.00", IC) + ": invalid stops (a stop on the losing side, a target on the winning side)");
                            double need = TotalMargin(sym, dir, lots, fill) - State.Account.Margin; if (need > 0 && need > State.Account.FreeMargin) return No(kind + " " + lots.ToString("0.00", IC) + ": not enough money (margin " + need.ToString("N2", IC) + ")");
                            var p = Open(t, sym, dir, lots, fill, sl, tp, "SpasiX"); return Ok(kind + " " + lots.ToString("0.00", IC) + " " + s.Name + " @ " + F(s, fill) + ": done (#" + p.Ticket + ")");
                        }
                        price = Round(s, price);
                        bool okPx = kind == "BUY LIMIT" ? price < ask : kind == "SELL LIMIT" ? price > bid : kind == "BUY STOP" ? price > ask : kind == "SELL STOP" ? price < bid : false;
                        if (!okPx) return No(kind + " @ " + F(s, price) + ": invalid price (a limit below / above the market, a stop beyond it)");
                        if (!StopsOk(dir, price, sl, tp)) return No(kind + ": invalid stops");
                        var o = new Mt5Order { Ticket = NextTicket++, Symbol = s.Name, Type = kind, Lots = lots, Price = price, Sl = sl, Tp = tp, Time = t }; State.Orders.Add(o);
                        return Ok(kind + " " + lots.ToString("0.00", IC) + " " + s.Name + " @ " + F(s, price) + ": placed (#" + o.Ticket + ")");
                    }
                case "CLOSE":
                    {
                        var p = State.Positions.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (p == null) return No("close #" + a[0] + ": no such position");
                        var s = SpecOf(p.Symbol); double lots = N(a[1]); lots = lots > 0 && lots < p.Lots ? Lots(s, lots) : p.Lots; Close(t, p, lots, p.Dir > 0 ? Bid(p.Symbol) : Ask(p.Symbol), "CLIENT"); return Ok("close #" + p.Ticket + " " + lots.ToString("0.00", IC) + ": done");
                    }
                case "CLOSEALL":
                    {
                        string sym = Convert.ToString(a[0], IC), which = Convert.ToString(a[1], IC); int n = 0;
                        foreach (var p in State.Positions.ToList())
                        {
                            if (sym != "" && !string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)) continue; double pl = p.Profit + p.Swap;
                            if ((which == "BUY" && p.Dir < 0) || (which == "SELL" && p.Dir > 0) || (which == "PROFIT" && pl <= 0) || (which == "LOSS" && pl >= 0)) continue;
                            Close(t, p, p.Lots, p.Dir > 0 ? Bid(p.Symbol) : Ask(p.Symbol), "CLIENT"); n++;
                        }
                        return Ok("closed " + n);
                    }
                case "MODIFY":
                    {
                        var p = State.Positions.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (p == null) return No("S/L T/P #" + a[0] + ": no such position");
                        var s = SpecOf(p.Symbol); double sl = Round(s, N(a[1])), tp = Round(s, N(a[2])); if (!StopsOk(p.Dir, p.Dir > 0 ? Bid(p.Symbol) : Ask(p.Symbol), sl, tp)) return No("S/L T/P #" + p.Ticket + ": invalid stops");
                        p.Sl = sl; p.Tp = tp; return Ok("S/L T/P #" + p.Ticket + ": done");
                    }
                case "MODORD":
                    {
                        var o = State.Orders.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (o == null) return No("order #" + a[0] + ": no such order");
                        var s = SpecOf(o.Symbol); double price = Round(s, N(a[1])), sl = Round(s, N(a[2])), tp = Round(s, N(a[3])); double bid = Bid(o.Symbol), ask = Ask(o.Symbol);
                        bool okPx = o.Type == "BUY LIMIT" ? price < ask : o.Type == "SELL LIMIT" ? price > bid : o.Type == "BUY STOP" ? price > ask : price < bid;
                        if (!okPx) return No("order #" + o.Ticket + " @ " + F(s, price) + ": invalid price");
                        int dir = o.Type.StartsWith("BUY") ? 1 : -1; if (!StopsOk(dir, price, sl, tp)) return No("order #" + o.Ticket + ": invalid stops");
                        o.Price = price; o.Sl = sl; o.Tp = tp; return Ok("order #" + o.Ticket + " → " + F(s, price) + ": done");
                    }
                case "CANCEL": { var o = State.Orders.FirstOrDefault(x => x.Ticket == Tk(a[0])); if (o == null) return No("cancel #" + a[0] + ": no such order"); State.Orders.Remove(o); return Ok("cancel #" + o.Ticket + ": done"); }
                case "CANCELALL": { string sym = Convert.ToString(a[0], IC); int n = State.Orders.RemoveAll(o => sym == "" || string.Equals(o.Symbol, sym, StringComparison.OrdinalIgnoreCase)); return Ok("cancelled " + n + " order(s)"); }
                case "BE":
                    {
                        string sym = Convert.ToString(a[0], IC), which = Convert.ToString(a[1], IC); int off = (int)N(a[2]); int done = 0, skip = 0;
                        foreach (var p in State.Positions)
                        {
                            if (sym != "" && !string.Equals(p.Symbol, sym, StringComparison.OrdinalIgnoreCase)) continue;
                            if ((which == "BUY" && p.Dir < 0) || (which == "SELL" && p.Dir > 0)) continue; var s = SpecOf(p.Symbol);
                            double be = Round(s, p.Open + p.Dir * off * s.Point); bool inProfit = p.Dir > 0 ? p.Current > be + s.Point : p.Current < be - s.Point; bool better = p.Sl == 0 || (p.Dir > 0 ? be > p.Sl : be < p.Sl);
                            if (!inProfit || !better) { skip++; continue; }
                            p.Sl = be; done++;
                        }
                        return Ok("breakeven on " + done + " • " + skip + " not in profit / already better");
                    }
            }
            return No("unknown command " + cmd);
        }

        // ---- saving the account (the live DEMO keeps its positions between sessions)
        public List<string> Save()
        {
            var l = new List<string>();
            l.Add("SET|" + Set.Balance.ToString("R", IC) + "|" + Set.CommissionPerLot.ToString("R", IC) + "|" + Set.Leverage + "|" + Set.StopOutPct.ToString("R", IC) + "|" + Set.Kind + "|" + Set.MarkupPts.ToString("R", IC) + "|" + Set.SlipPts + "|" + Set.Currency);
            l.Add("BAL|" + Balance.ToString("R", IC) + "|" + NextTicket + "|" + NextDeal + "|" + Now.Ticks);
            foreach (var p in State.Positions) l.Add("P|" + p.Ticket + "|" + p.Symbol + "|" + p.Side + "|" + p.Lots.ToString("R", IC) + "|" + p.Open.ToString("R", IC) + "|" + p.Sl.ToString("R", IC) + "|" + p.Tp.ToString("R", IC) + "|" + p.Time.Ticks + "|" + p.Swap.ToString("R", IC) + "|" + p.Commission.ToString("R", IC));
            foreach (var o in State.Orders) l.Add("O|" + o.Ticket + "|" + o.Symbol + "|" + o.Type + "|" + o.Lots.ToString("R", IC) + "|" + o.Price.ToString("R", IC) + "|" + o.Sl.ToString("R", IC) + "|" + o.Tp.ToString("R", IC) + "|" + o.Time.Ticks);
            foreach (var d in State.Deals) l.Add("D|" + d.Ticket + "|" + d.Position + "|" + d.Time.Ticks + "|" + d.Symbol + "|" + d.Type + "|" + d.Entry + "|" + d.Lots.ToString("R", IC) + "|" + d.Price.ToString("R", IC) + "|" + d.Profit.ToString("R", IC) + "|" + d.Commission.ToString("R", IC) + "|" + d.Swap.ToString("R", IC) + "|" + d.Reason);
            return l;
        }
        public bool Load(IEnumerable<string> lines)
        {
            Func<string, double> D = x => { double v; return double.TryParse(x, NumberStyles.Float, IC, out v) ? v : 0; };
            Func<string, long> L = x => { long v; return long.TryParse(x, NumberStyles.Integer, IC, out v) ? v : 0; };
            bool any = false; var pos = new List<Mt5Position>(); var ord = new List<Mt5Order>(); var deals = new List<Mt5Deal>();
            foreach (var ln in lines)
            {
                var a = ln.Split('|');
                if (a[0] == "SET" && a.Length >= 9) { Set = new SimAccountSettings { Balance = D(a[1]), CommissionPerLot = D(a[2]), Leverage = (int)L(a[3]), StopOutPct = D(a[4]), Kind = a[5], MarkupPts = D(a[6]), SlipPts = (int)L(a[7]), Currency = a[8] }; any = true; }
                else if (a[0] == "BAL" && a.Length >= 5) { Balance = D(a[1]); NextTicket = L(a[2]); NextDeal = L(a[3]); Now = new DateTime(L(a[4])); }
                else if (a[0] == "P" && a.Length >= 11) pos.Add(new Mt5Position { Ticket = L(a[1]), Symbol = a[2], Side = a[3], Lots = D(a[4]), Open = D(a[5]), Sl = D(a[6]), Tp = D(a[7]), Time = new DateTime(L(a[8])), Swap = D(a[9]), Commission = D(a[10]), Current = D(a[5]) });
                else if (a[0] == "O" && a.Length >= 9) ord.Add(new Mt5Order { Ticket = L(a[1]), Symbol = a[2], Type = a[3], Lots = D(a[4]), Price = D(a[5]), Sl = D(a[6]), Tp = D(a[7]), Time = new DateTime(L(a[8])) });
                else if (a[0] == "D" && a.Length >= 13) deals.Add(new Mt5Deal { Ticket = L(a[1]), Position = L(a[2]), Time = new DateTime(L(a[3])), Symbol = a[4], Type = a[5], Entry = a[6], Lots = D(a[7]), Price = D(a[8]), Profit = D(a[9]), Commission = D(a[10]), Swap = D(a[11]), Reason = a[12] });
            }
            if (!any) return false;
            State.Positions = pos; State.Orders = ord; State.Deals = deals; State.DealsVersion++; State.DealsAt = DateTime.Now;
            State.Account = new Mt5Account { Name = "DEMO", Server = "SpasiX simulator", Currency = Set.Currency, Balance = Balance, Equity = Balance, FreeMargin = Balance, Leverage = Set.Leverage, Mode = "HEDGE", Connected = true, Kind = "DEMO" };
            return true;
        }
    }
}
