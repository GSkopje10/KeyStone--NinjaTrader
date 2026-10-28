// SpasiX — the live DEMO: your MT5's REAL prices, candles and symbols, with a SIMULATED account (the balance you choose, RAW or STANDARD, the
// commission, the leverage) — every button works like live, nothing reaches MT5. The account (positions, orders, deals) is saved on this PC and
// comes back next time; RESET starts a new one.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KeystoneMT5
{
    public sealed class PaperBroker : IBroker
    {
        readonly Mt5Bridge live; readonly Mt5State state; public readonly SimAccount Acct; int cmdSeq; DateTime savedAt = DateTime.MinValue; bool dirty;
        readonly Dictionary<string, Mt5Quote> seenQuote = new Dictionary<string, Mt5Quote>(StringComparer.OrdinalIgnoreCase);
        public static string FilePath { get { return Path.Combine(MainWindow.Dir, "demo-account.txt"); } }
        // each broker its own DEMO account: demo-account-LOGIN@SERVER.txt; the first broker keeps demo-account.txt (as before)
        string file;
        string MyFile { get { if (file != null) return file; var k = live.Key ?? ""; if (k == "" || live == Shell.Bridge && !File.Exists(KeyFile(k)) && File.Exists(FilePath)) return file = FilePath; return file = KeyFile(k); } }
        static string KeyFile(string k) { foreach (var c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_'); return Path.Combine(MainWindow.Dir, "demo-account-" + k + ".txt"); }
        public PaperBroker(Mt5Bridge live) { this.live = live; state = new Mt5State(live.State); Acct = new SimAccount(state); state.BridgeVersion = 5; }
        public Mt5State State { get { return state; } } public bool IsSim { get { return true; } } public string Problem { get { return live.Problem; } } public int Port { get { return live.Port; } }
        public bool Ready { get; private set; }

        // a saved account on this PC? (then it opens straight away)
        public bool LoadSaved()
        {
            try { if (!File.Exists(MyFile)) return false; lock (state.Gate) { Ready = Acct.Load(File.ReadAllLines(MyFile)); } return Ready; } catch { return false; }
        }
        public void Start(SimAccountSettings s)
        {
            lock (state.Gate) { var now = live.State.ServerNow > DateTime.MinValue ? live.State.ServerNow : DateTime.Now; Acct.Reset(s, now); state.Account.Kind = "DEMO"; state.Account.Name = "DEMO"; Ready = true; dirty = true; }
            Save();
        }
        public void Save()
        {
            if (!Ready) return; List<string> l; lock (state.Gate) l = Acct.Save();
            try { Directory.CreateDirectory(MainWindow.Dir); File.WriteAllLines(MyFile, l); savedAt = DateTime.Now; dirty = false; } catch { }
        }
        // every refresh: the live market → the simulated account (fills, stops, targets, P/L), the link's state mirrored
        public void Sync()
        {
            lock (state.Gate)
            {
                var m = live.State; state.Linked = m.Linked; state.LastLine = m.LastLine; state.LinkedAt = m.LinkedAt; state.ServerNow = m.ServerNow; state.ServerOffsetSec = m.ServerOffsetSec; state.Company = m.Company;
                state.AllowInput = state.AlgoTerminal = state.AlgoEa = state.AccountTrade = state.AccountExpert = true; state.CanKnown = true; state.MaxLots = 100; state.BridgeVersion = Math.Max(5, m.BridgeVersion);
                if (!Ready) return;
                var need = new HashSet<string>(state.Positions.Select(p => p.Symbol).Concat(state.Orders.Select(o => o.Symbol)), StringComparer.OrdinalIgnoreCase);
                foreach (var sym in need)
                {
                    Mt5Quote q; if (!m.Quotes.TryGetValue(sym, out q) || q.Bid <= 0) continue; Mt5Quote last; if (seenQuote.TryGetValue(sym, out last) && ReferenceEquals(last, q)) continue;
                    seenQuote[sym] = q; var sp = SpecFor(sym); if (sp == null) continue;
                    int before = state.Deals.Count; Acct.OnPrice(sym, q.Time, q.Bid, q.Ask + (Acct.Set.Widened ? Acct.Set.MarkupPts * sp.Point : 0)); if (state.Deals.Count != before) dirty = true;
                }
                Acct.Mark();
            }
            if (dirty || (DateTime.Now - savedAt).TotalSeconds > 30) Save();
        }
        // the spec of a symbol from MT5 (contract, tick value, swap, …), once
        public SimSpec SpecFor(string sym)
        {
            SimSpec sp; if (Acct.Specs.TryGetValue(sym, out sp)) return sp;
            Mt5Symbol ms; if (!live.State.Symbols.TryGetValue(sym, out ms)) return null;
            int ny = -7; try { var et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); ny = (int)Math.Round(et.GetUtcOffset(DateTime.UtcNow).TotalHours - live.State.ServerOffsetSec / 3600.0); } catch { }
            sp = SimSpec.From(ms, live.State.ServerOffsetSec, ny, Acct.Set.Currency); sp.MarginLeverage = live.State.Account.Leverage; Acct.Specs[sym] = sp; return sp;
        }
        public bool Send(string line) { return live.Send(line); }   // candles / symbols / history: from the real MT5
        public string Command(string cmd, params object[] a)
        {
            string id = "D" + (++cmdSeq);
            lock (state.Gate)
            {
                Tuple<bool, string> r;
                try
                {
                    if (!Ready) r = Tuple.Create(false, "set up the DEMO account first");
                    else
                    {
                        if (cmd == "ORDER") { var sym = Convert.ToString(a[0]); var sp = SpecFor(sym); Mt5Quote q; if (sp != null && live.State.Quotes.TryGetValue(sym, out q)) Acct.OnPrice(sym, q.Time, q.Bid, q.Ask + (Acct.Set.Widened ? Acct.Set.MarkupPts * sp.Point : 0)); }
                        var now = live.State.ServerNow > DateTime.MinValue ? live.State.ServerNow : DateTime.Now; r = Acct.Do(cmd, a, now > Acct.Now ? now : Acct.Now);
                    }
                }
                catch (Exception ex) { r = Tuple.Create(false, cmd + ": " + ex.Message); }
                state.AddResult(id, r.Item1, r.Item2); dirty = true;
            }
            return id;
        }
    }
}
