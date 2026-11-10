// SpasiX — 🛡 PROTECT THE ADD (09h, the user's request with his DEMO #47: after BUY 0.08 / SELL 0.16 / BUY 0.16 he added BUY 0.65 @4118.97 by hand
// → net LONG 0.73). Whenever a BIGGER ADD is placed — by ➕ ADD ON BREAK, by a 💡 RECOVERY ASSIST click, or BY HAND (a new leg in the net's direction of
// at least BigX × the entry) — a protection level is armed at once (the add's entry − k × ATR(M1), or the last swing). The price reaches it →
// EQUALIZE: the opposite lots so the basket is net 0 (the loss stops growing; the 🎯 exit guard still watches the total), then a COOL-DOWN
// (no hedges / adds), then RESUME SMALL: the entry lots in the direction of the first finished M1 candle that breaks the cool-down's range.
// It never arms on the automation's normal hedges (they flip the net) nor on its own legs. Pure: the engine / the tab send its actions.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class ProtectAction { public string Kind = ""; public int Dir; public double Lots; public string Why = ""; }

    public sealed class AddProtector
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public string Phase = "";   // "" | ARMED | LOCKED (cool-down) | BREAK (waits for the first break after the cool-down)
        public int AddDir; public long AddTicket; public string Source = "";
        public double AddEntry, AddLots, Level = double.NaN, EntryLots, EstLockTotal = double.NaN, LockTotal = double.NaN, RangeHi = double.NaN, RangeLo = double.NaN, NetAtArm;
        public DateTime LockAt, CoolUntil; public int CoolMin = 15; public int Locks, Resumes;
        readonly HashSet<long> known = new HashSet<long>(), engine = new HashSet<long>(), flagged = new HashSet<long>();
        bool seen; int flagDir; DateTime flagUntil = DateTime.MinValue; DateTime lastBreakBar = DateTime.MinValue;
        public bool Blocks { get { return Phase == "LOCKED" || Phase == "BREAK"; } }

        // the default ON / OFF per account kind: AUTO = on for simulated accounts (🎮 DEMO, 🧪 BACKTEST, 🏁 RACE), OFF on MT5 accounts unless ticked ON
        public static bool On(string mode, bool simulated) { return mode == "ON" || (mode != "OFF" && simulated); }
        public static double LevelOf(int dir, double entry, double atr, double swing, string mode, double k)
        {
            if (mode == "SWING" && !double.IsNaN(swing) && (dir > 0 ? swing < entry : swing > entry)) return swing;
            return entry - dir * k * (double.IsNaN(atr) || atr <= 0 ? 1 : atr);
        }
        // an ADD = a leg in the direction the net already had, at least bigX × the entry (normal hedges flip the net → never an add)
        public static bool IsAdd(double netBefore, int legDir, double legLots, double entryLots, double bigX)
        {
            return Math.Abs(netBefore) > 0.005 && Math.Sign(netBefore) == legDir && entryLots > 0 && legLots >= bigX * entryLots - 1e-9;
        }
        // the basket's total once the price reaches the level and it is locked: the net's move to the level − the spread / commission of the lock
        public static double TotalAtLock(double total, double net, double level, double bid, double ask, double perPrice, double commPerLot)
        {
            double mark = net > 0 ? bid : ask;
            return total + net * perPrice * (level - mark) - Math.Abs(net) * ((ask - bid) * perPrice + commPerLot);
        }
        public void Engine(long ticket) { engine.Add(ticket); }   // the automation's own legs (hedges, locks, stacks …): never an add to protect
        public void Flag(long ticket) { flagged.Add(ticket); }    // ➕ ADD ON BREAK's fill: always protected, whatever its size
        public void FlagNext(int dir, DateTime until) { flagDir = dir; flagUntil = until; }   // 💡 a RECOVERY ASSIST click: the next leg that way
        public void Reset() { Phase = ""; Level = double.NaN; AddTicket = 0; EstLockTotal = LockTotal = RangeHi = RangeLo = double.NaN; }

        // every tick: new legs (arm), then the phase's action (null = nothing to send). m1 = finished M1 candles, oldest first.
        public ProtectAction Tick(IList<Mt5Position> pos, double bid, double ask, DateTime now, IList<Mt5Bar> m1, double total, double perPrice, double commPerLot,
            string mode, double k, double bigX, int coolMin)
        {
            CoolMin = coolMin;
            if (pos == null || pos.Count == 0) { Reset(); known.Clear(); engine.Clear(); flagged.Clear(); seen = true; return null; }
            if (!seen) { seen = true; foreach (var p in pos) known.Add(p.Ticket); }   // the first look (a restart): the open legs are not new adds
            var fresh = pos.Where(p => !known.Contains(p.Ticket)).OrderBy(p => p.Time).ThenBy(p => p.Ticket).ToList();
            double entry = pos.OrderBy(p => p.Time).ThenBy(p => p.Ticket).First().Lots;
            foreach (var p in fresh)
            {
                double netBefore = Math.Round(pos.Where(x => known.Contains(x.Ticket)).Sum(x => x.Dir * x.Lots), 2); known.Add(p.Ticket);
                bool flag = flagged.Contains(p.Ticket) || (flagDir == p.Dir && now <= flagUntil);
                if (flag && flagDir == p.Dir) flagUntil = DateTime.MinValue;
                if (engine.Contains(p.Ticket) && !flag) continue;
                if (Blocks) continue;   // locked: nothing re-arms until the resume
                if (flag || IsAdd(netBefore, p.Dir, p.Lots, entry, bigX))
                {
                    double atr = m1 != null && m1.Count >= 2 ? TrendDetect.Atr(m1, m1.Count - 1, 14) : double.NaN;
                    double sw = double.NaN; if (mode == "SWING" && m1 != null && m1.Count >= 30) { var tr = TrendDetect.Read(m1, double.NaN); sw = p.Dir > 0 ? tr.SwingLo : tr.SwingHi; }
                    Phase = "ARMED"; AddDir = p.Dir; AddTicket = p.Ticket; AddEntry = p.Open; AddLots = p.Lots; EntryLots = entry; Level = Math.Round(LevelOf(p.Dir, p.Open, atr, sw, mode, k), 2);
                    Source = flagged.Contains(p.Ticket) ? "➕ ADD ON BREAK" : flag ? "💡 RECOVERY" : "✋ your add";
                }
            }
            double net = Math.Round(pos.Sum(p => p.Dir * p.Lots), 2);
            if (Phase == "ARMED")
            {
                if (!pos.Any(p => p.Ticket == AddTicket) || Math.Sign(net) != AddDir) { Reset(); return null; }   // the add is gone / a hedge flipped the net: nothing to protect
                NetAtArm = net; EstLockTotal = TotalAtLock(total, net, Level, bid, ask, perPrice, commPerLot);
                if (AddDir > 0 ? bid <= Level : ask >= Level)
                    return new ProtectAction { Kind = "LOCK", Dir = -Math.Sign(net), Lots = Math.Abs(net), Why = "🛡 PROTECT: the " + (AddDir > 0 ? "bid " + bid.ToString("0.00", IC) + " ≤ " : "ask " + ask.ToString("0.00", IC) + " ≥ ") + Level.ToString("0.00", IC) + " (" + Source + " " + (AddDir > 0 ? "BUY " : "SELL ") + AddLots.ToString("0.##", IC) + " @ " + AddEntry.ToString("0.00", IC) + " failed) → " + (net > 0 ? "SELL " : "BUY ") + Math.Abs(net).ToString("0.##", IC) + " = net 0" };
                return null;
            }
            if (Phase == "LOCKED")
            {
                if (now < CoolUntil) return null;
                var r = (m1 ?? new List<Mt5Bar>()).Where(b => b.Time >= Floor(LockAt) && b.Time < CoolUntil).ToList(); if (r.Count == 0 && m1 != null && m1.Count > 0) r.Add(m1[m1.Count - 1]);
                if (r.Count == 0) return null;
                RangeHi = r.Max(b => b.H); RangeLo = r.Min(b => b.L); Phase = "BREAK"; lastBreakBar = DateTime.MinValue; return null;
            }
            if (Phase == "BREAK")
            {
                if (Math.Abs(net) > 0.005) { Reset(); return null; }   // someone changed the locked basket: the protection steps aside
                if (m1 == null || m1.Count == 0) return null; var last = m1[m1.Count - 1];
                if (last.Time < CoolUntil || last.Time == lastBreakBar) return null; lastBreakBar = last.Time;
                int d = last.C > RangeHi ? 1 : last.C < RangeLo ? -1 : 0; if (d == 0) return null;
                return new ProtectAction { Kind = "RESUME", Dir = d, Lots = EntryLots, Why = "🛡 RESUME SMALL: the M1 close " + last.C.ToString("0.00", IC) + " broke the cool-down's " + (d > 0 ? "high " + RangeHi.ToString("0.00", IC) : "low " + RangeLo.ToString("0.00", IC)) + " → " + (d > 0 ? "BUY " : "SELL ") + EntryLots.ToString("0.##", IC) + " (the entry size), the iteration goes on" };
            }
            return null;
        }
        // the caller sent the action
        public void Sent(ProtectAction a, DateTime now, double total)
        {
            if (a == null) return;
            if (a.Kind == "LOCK") { Phase = "LOCKED"; LockAt = now; CoolUntil = now.AddMinutes(CoolMin); LockTotal = total; Locks++; }
            else if (a.Kind == "RESUME") { Phase = ""; Resumes++; Level = double.NaN; AddTicket = 0; }
        }
        static DateTime Floor(DateTime t) { return new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute); }
        static string Usd(double v) { return (v < 0 ? "−$" : "+$") + Math.Abs(v).ToString("N0", IC); }
        public string Text(DateTime now)
        {
            if (Phase == "ARMED") return "🛡 add protected (" + Source + " " + (AddDir > 0 ? "BUY " : "SELL ") + AddLots.ToString("0.##", IC) + " @ " + AddEntry.ToString("0.00", IC) + "): if " + Level.ToString("0.00", IC) + " → lock at net 0 (≈ " + Usd(EstLockTotal) + "), wait " + CoolMin + " min, restart " + EntryLots.ToString("0.##", IC);
            if (Phase == "LOCKED") { var l = CoolUntil - now; return "🛡 LOCKED at net 0 (total " + Usd(LockTotal) + ") — cool-down " + (l.TotalSeconds > 0 ? ((int)l.TotalMinutes).ToString(IC) + ":" + l.Seconds.ToString("00", IC) : "0:00") + " left (no hedges / adds), then restart " + EntryLots.ToString("0.##", IC) + " on the first break"; }
            if (Phase == "BREAK") return "🛡 cool-down over — waits for a finished M1 close above " + RangeHi.ToString("0.00", IC) + " (BUY " + EntryLots.ToString("0.##", IC) + ") or below " + RangeLo.ToString("0.00", IC) + " (SELL " + EntryLots.ToString("0.##", IC) + ")";
            return "";
        }
    }
}
