// SpasiX — the link to MetaTrader 5. The SpasiXBridge EA (in MT5) connects to this app on 127.0.0.1 and streams text lines:
//   HELLO|version|company|build                once, when it connects
//   SYM|name|digits|contract|volMin|volStep|volMax|point|tickValue|tickSize|description
//   ACC|login|name|server|currency|balance|equity|margin|free|level|leverage|HEDGE/NET|connected 1/0|DEMO/REAL/CONTEST
//   CAN|EnableTrading|terminal Algo Trading|EA Algo Trading|max lots|server − GMT s|server time [|account trade allowed|account expert allowed]
//   TICK|symbol|bid|ask|time ms
//   POSS|n  then n × P|ticket|symbol|BUY/SELL|lots|open|sl|tp|current|profit|swap|open time (s)|magic|comment  then POSEND
//   ORDS|n  then n × O|ticket|symbol|type|lots|price|sl|tp|setup time (s)  then ORDEND
//   BARS|symbol|minutes|n  then B|time|o|h|l|c|v  then BARSEND|symbol|minutes
//   HIST|symbol|from|to|n|error  then H|time|o|h|l|c|v|spread  then HISTEND|symbol|from|to       (1-minute history for the backtest)
//   DEALS|from|to|n  then D|ticket|time|symbol|type|entry|lots|price|profit|commission|swap|position|magic|reason|comment  then DEALEND
//   RES|id|OK/FAIL|retcode|text|ticket           the answer to one of the app's commands
// The app talks to a BROKER: the real one (this bridge) or the backtest's simulated one (Sim.cs) — the windows do not know which.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KeystoneMT5
{
    public sealed class Mt5Account
    {
        public long Login; public string Name = "", Server = "", Currency = "USD", Mode = "HEDGE", Kind = "REAL";
        public double Balance, Equity, Margin, FreeMargin, MarginLevel; public int Leverage; public bool Connected;
        public double Floating { get { return Equity - Balance; } }
    }
    public sealed class Mt5Symbol { public string Name = "", Description = ""; public int Digits = 2; public double Contract = 1, VolMin = 0.01, VolStep = 0.01, VolMax = 100, Point = 0.01, TickValue = 1, TickSize = 0.01; }
    public sealed class Mt5Quote { public string Symbol = ""; public double Bid, Ask; public DateTime Time; public double Spread(double point) { return point > 0 ? Math.Round((Ask - Bid) / point) : 0; } }
    public sealed class Mt5Position
    {
        public long Ticket; public string Symbol = "", Side = "BUY", Comment = ""; public double Lots, Open, Sl, Tp, Current, Profit, Swap, Commission; public DateTime Time; public long Magic;
        public int Dir { get { return Side == "BUY" ? 1 : -1; } }
    }
    public sealed class Mt5Order { public long Ticket; public string Symbol = "", Type = ""; public double Lots, Price, Sl, Tp; public DateTime Time; }
    // one deal of the account's history (an IN opens a position, an OUT closes it — partly or fully; BALANCE = a deposit / withdrawal)
    public sealed class Mt5Deal
    {
        public long Ticket, Position, Magic; public DateTime Time; public string Symbol = "", Type = "", Entry = "", Reason = "", Comment = "";
        public double Lots, Price, Profit, Commission, Swap;
        public double Net { get { return Profit + Commission + Swap; } }
        public bool IsTrade { get { return Type == "BUY" || Type == "SELL"; } }
        public bool IsExit { get { return IsTrade && (Entry == "OUT" || Entry == "OUTBY" || Entry == "INOUT"); } }
    }

    // what the app talks to: MT5 through the bridge, or the backtest
    public interface IBroker
    {
        Mt5State State { get; }
        bool Send(string line);
        string Command(string cmd, params object[] args);
        string Problem { get; }
        int Port { get; }
        bool IsSim { get; }
    }

    // what the app knows about the terminal right now (replaced piece by piece as lines arrive)
    public sealed class Mt5State
    {
        public readonly object Gate = new object();
        public string Company = ""; public int Build; public bool Linked; public DateTime LastLine = DateTime.MinValue, LinkedAt = DateTime.MinValue;
        public Mt5Account Account = new Mt5Account();
        public readonly Dictionary<string, Mt5Symbol> Symbols = new Dictionary<string, Mt5Symbol>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Mt5Quote> Quotes = new Dictionary<string, Mt5Quote>(StringComparer.OrdinalIgnoreCase);
        public List<Mt5Position> Positions = new List<Mt5Position>();
        public List<Mt5Order> Orders = new List<Mt5Order>();
        public int BridgeVersion;
        // what the bridge allows
        public bool AllowInput, AlgoTerminal, AlgoEa, AccountTrade = true, AccountExpert = true; public double MaxLots = 5; public int ServerOffsetSec = 7200; public DateTime ServerNow = DateTime.MinValue; public bool CanKnown;
        public bool CanTrade { get { return BridgeVersion >= 3 && AllowInput && AlgoTerminal && AlgoEa && AccountTrade && AccountExpert; } }
        public readonly List<string> AllSymbols = new List<string>(); List<string> allIn;
        // the answers to the app's commands (RES|id|OK/FAIL|retcode|text|ticket), newest last
        public readonly List<Tuple<string, bool, string>> Results = new List<Tuple<string, bool, string>>(); public int ResultsDropped;
        // candles per symbol + timeframe
        public readonly Dictionary<string, BarSeries> Series = new Dictionary<string, BarSeries>(StringComparer.OrdinalIgnoreCase);
        List<Mt5Bar> barsIn; string barsKey;
        public static string Key(string sym, int m) { return sym + "|" + m; }
        public BarSeries SeriesOf(string sym, int m) { BarSeries b; if (!Series.TryGetValue(Key(sym, m), out b)) Series[Key(sym, m)] = b = new BarSeries(sym, m); return b; }
        // 1-minute history answers (HIST … HISTEND), by "symbol|from": the bars + MT5's error (≠ 0 = still downloading)
        public readonly Dictionary<string, Tuple<List<Mt5Bar>, int>> Hist = new Dictionary<string, Tuple<List<Mt5Bar>, int>>(StringComparer.OrdinalIgnoreCase);
        List<Mt5Bar> histIn; string histKey; int histErr;
        // the deals (DEALS … DEALEND), the last answer replaces the list
        public List<Mt5Deal> Deals = new List<Mt5Deal>(); public DateTime DealsAt = DateTime.MinValue, DealsFrom = DateTime.MinValue; public int DealsVersion;
        List<Mt5Deal> dealsIn; DateTime dealsFromIn;
        public long Lines;
        List<Mt5Position> posIn; List<Mt5Order> ordIn;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, IC, out v) ? v : 0; }
        static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, IC, out v) ? v : 0; }
        public static DateTime Unix(long sec) { return sec <= 0 ? DateTime.MinValue : new DateTime(1970, 1, 1).AddSeconds(sec); }   // MT5 server time (as MT5 shows it)
        public static long Epoch(DateTime t) { return (long)(t - new DateTime(1970, 1, 1)).TotalSeconds; }
        public void AddResult(string id, bool ok, string text) { lock (Gate) { Results.Add(Tuple.Create(id, ok, text)); if (Results.Count > 300) { Results.RemoveRange(0, 100); ResultsDropped += 100; } } }

        // one line from the EA (unknown / short lines are ignored: a newer EA never breaks this app)
        public void Apply(string line)
        {
            if (string.IsNullOrEmpty(line)) return; var a = line.TrimEnd('\r').Split('|');
            lock (Gate)
            {
                Lines++; LastLine = DateTime.Now;
                switch (a[0])
                {
                    case "HELLO": if (a.Length >= 4) { BridgeVersion = (int)L(a[1]); Company = a[2]; Build = (int)L(a[3]); } Linked = true; LinkedAt = DateTime.Now; break;
                    case "BARS": if (a.Length >= 4) { barsKey = Key(a[1], (int)L(a[2])); barsIn = new List<Mt5Bar>(); } break;
                    case "B":
                        if (barsIn == null || a.Length < 7) return;
                        barsIn.Add(new Mt5Bar { Time = Unix(L(a[1])), O = D(a[2]), H = D(a[3]), L = D(a[4]), C = D(a[5]), V = L(a[6]) });
                        break;
                    case "BARSEND":
                        if (barsIn != null && a.Length >= 3 && barsKey == Key(a[1], (int)L(a[2]))) { var se = SeriesOf(a[1], (int)L(a[2])); if (barsIn.Count > 0) se.Merge(barsIn); }
                        barsIn = null; barsKey = null; break;
                    case "HIST": if (a.Length >= 6) { histKey = a[1] + "|" + a[2]; histErr = (int)L(a[5]); histIn = new List<Mt5Bar>(); } break;
                    case "H":
                        if (histIn == null || a.Length < 8) return;
                        histIn.Add(new Mt5Bar { Time = Unix(L(a[1])), O = D(a[2]), H = D(a[3]), L = D(a[4]), C = D(a[5]), V = L(a[6]), Spread = (int)L(a[7]) });
                        break;
                    case "HISTEND": if (histIn != null && a.Length >= 3 && histKey == a[1] + "|" + a[2]) Hist[histKey] = Tuple.Create(histIn, histErr); histIn = null; histKey = null; break;
                    case "DEALS": if (a.Length >= 4) { dealsIn = new List<Mt5Deal>(); dealsFromIn = Unix(L(a[1])); } break;
                    case "D":
                        if (dealsIn == null || a.Length < 15) return;
                        dealsIn.Add(new Mt5Deal { Ticket = L(a[1]), Time = Unix(L(a[2])), Symbol = a[3], Type = a[4], Entry = a[5], Lots = D(a[6]), Price = D(a[7]), Profit = D(a[8]), Commission = D(a[9]), Swap = D(a[10]), Position = L(a[11]), Magic = L(a[12]), Reason = a[13], Comment = a[14] });
                        break;
                    case "DEALEND": if (dealsIn != null) { Deals = dealsIn; DealsFrom = dealsFromIn; DealsAt = DateTime.Now; DealsVersion++; dealsIn = null; } break;
                    case "BYE": Linked = false; break;
                    case "CAN":
                        if (a.Length >= 7) { AllowInput = a[1] == "1"; AlgoTerminal = a[2] == "1"; AlgoEa = a[3] == "1"; MaxLots = D(a[4]); ServerOffsetSec = (int)L(a[5]); ServerNow = Unix(L(a[6])); CanKnown = true; }
                        if (a.Length >= 9) { AccountTrade = a[7] == "1"; AccountExpert = a[8] == "1"; }
                        break;
                    case "RES": if (a.Length >= 6) { Results.Add(Tuple.Create(a[1], a[2] == "OK", a[4])); if (Results.Count > 300) { Results.RemoveRange(0, 100); ResultsDropped += 100; } } break;
                    case "ALLSYMS": if (allIn == null) allIn = new List<string>(); if (a.Length >= 2) allIn.AddRange(a[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                    case "ALLSYMSEND": if (allIn != null) { AllSymbols.Clear(); AllSymbols.AddRange(allIn.OrderBy(x => x)); allIn = null; } break;
                    case "SYM":
                        if (a.Length < 11) return;
                        Symbols[a[1]] = new Mt5Symbol { Name = a[1], Digits = (int)L(a[2]), Contract = D(a[3]), VolMin = D(a[4]), VolStep = D(a[5]), VolMax = D(a[6]), Point = D(a[7]), TickValue = D(a[8]), TickSize = D(a[9]), Description = a[10] };
                        break;
                    case "ACC":
                        if (a.Length < 14) return;
                        Account = new Mt5Account { Login = L(a[1]), Name = a[2], Server = a[3], Currency = a[4], Balance = D(a[5]), Equity = D(a[6]), Margin = D(a[7]), FreeMargin = D(a[8]), MarginLevel = D(a[9]), Leverage = (int)L(a[10]), Mode = a[11], Connected = a[12] == "1", Kind = a[13] };
                        Linked = true; break;
                    case "TICK":
                        if (a.Length < 5) return;
                        var qt = new Mt5Quote { Symbol = a[1], Bid = D(a[2]), Ask = D(a[3]), Time = new DateTime(1970, 1, 1).AddMilliseconds(L(a[4])) };   // MT5 server time
                        Quotes[a[1]] = qt;
                        foreach (var se in Series.Values) if (string.Equals(se.Symbol, qt.Symbol, StringComparison.OrdinalIgnoreCase) && se.Loaded) se.OnTick(qt.Time, qt.Bid);
                        break;
                    case "POSS": posIn = new List<Mt5Position>(); break;
                    case "P":
                        if (posIn == null || a.Length < 14) return;
                        posIn.Add(new Mt5Position { Ticket = L(a[1]), Symbol = a[2], Side = a[3], Lots = D(a[4]), Open = D(a[5]), Sl = D(a[6]), Tp = D(a[7]), Current = D(a[8]), Profit = D(a[9]), Swap = D(a[10]), Time = Unix(L(a[11])), Magic = L(a[12]), Comment = a[13] });
                        break;
                    case "POSEND": if (posIn != null) { Positions = posIn; posIn = null; } break;
                    case "ORDS": ordIn = new List<Mt5Order>(); break;
                    case "O":
                        if (ordIn == null || a.Length < 9) return;
                        ordIn.Add(new Mt5Order { Ticket = L(a[1]), Symbol = a[2], Type = a[3], Lots = D(a[4]), Price = D(a[5]), Sl = D(a[6]), Tp = D(a[7]), Time = Unix(L(a[8])) });
                        break;
                    case "ORDEND": if (ordIn != null) { Orders = ordIn; ordIn = null; } break;
                }
            }
        }
        // a quiet link (no line for 3 s) = MT5 closed, the EA removed or the terminal frozen
        public bool Alive { get { return Linked && (DateTime.Now - LastLine).TotalSeconds < 3; } }
    }

    // the server the EA connects to (one terminal at a time; a new connection replaces the old one)
    public sealed class Mt5Bridge : IBroker, IDisposable
    {
        readonly Mt5State state = new Mt5State(); readonly int port; string problem = "";
        public Mt5State State { get { return state; } } public int Port { get { return port; } } public string Problem { get { return problem; } } public bool IsSim { get { return false; } }
        public event Action Changed;
        TcpListener listener; Thread thread; volatile bool stop; TcpClient client; readonly object sendGate = new object(); StreamWriter writer;
        public Mt5Bridge(int port) { this.port = port; }
        public void Start()
        {
            try { listener = new TcpListener(IPAddress.Loopback, port); listener.Start(); }
            catch (Exception ex) { problem = "port " + port + " is busy (" + ex.Message + ") — is SpasiX already open?"; return; }
            thread = new Thread(Run) { IsBackground = true, Name = "SpasiX bridge" }; thread.Start();
        }
        void Run()
        {
            while (!stop)
            {
                TcpClient c = null;
                try { c = listener.AcceptTcpClient(); } catch { if (stop) return; Thread.Sleep(200); continue; }
                try { if (client != null) client.Close(); } catch { }
                client = c; c.NoDelay = true;
                var th = new Thread(() => Serve(c)) { IsBackground = true, Name = "SpasiX terminal" }; th.Start();
            }
        }
        void Serve(TcpClient c)
        {
            try
            {
                var ns = c.GetStream(); var rd = new StreamReader(ns, new UTF8Encoding(false));
                lock (sendGate) writer = new StreamWriter(ns, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                string line; int n = 0;
                while (!stop && (line = rd.ReadLine()) != null) { state.Apply(line); if (++n % 4 == 0 || line == "ORDEND" || line.StartsWith("HELLO")) Fire(); }
            }
            catch { }
            lock (state.Gate) state.Linked = false; Fire();
        }
        void Fire() { var h = Changed; if (h != null) try { h(); } catch { } }
        public bool Send(string line) { lock (sendGate) { if (writer == null) return false; try { writer.WriteLine(line); return true; } catch { return false; } } }
        int cmdSeq;
        // a trading command with its own id (the answer comes back as RES|id|…): ORDER|id|sym|kind|lots|price|sl|tp, CLOSE|id|ticket|lots, …
        public string Command(string cmd, params object[] args)
        {
            string id = "C" + Interlocked.Increment(ref cmdSeq);
            return Send(Format(cmd, id, args)) ? id : null;
        }
        public static string Format(string cmd, string id, object[] args)
        {
            var parts = new List<string> { cmd, id }; foreach (var x in args) parts.Add(x is double ? ((double)x).ToString("0.########", CultureInfo.InvariantCulture) : Convert.ToString(x, CultureInfo.InvariantCulture));
            return string.Join("|", parts);
        }
        public void Dispose() { stop = true; try { if (listener != null) listener.Stop(); } catch { } try { if (client != null) client.Close(); } catch { } }
    }
}
