// Keystone MT5 — the link to MetaTrader 5. The KeystoneBridge EA (in MT5) connects to this app on 127.0.0.1 and streams text lines:
//   HELLO|1|company|build                      once, when it connects
//   SYM|name|digits|contract|volMin|volStep|volMax|point|tickValue|tickSize|description
//   ACC|login|name|server|currency|balance|equity|margin|free|level|leverage|HEDGE/NET|connected 1/0|DEMO/REAL/CONTEST
//   TICK|symbol|bid|ask|time ms
//   POSS|n  then n × P|ticket|symbol|BUY/SELL|lots|open|sl|tp|current|profit|swap|open time (s)|magic|comment  then POSEND
//   ORDS|n  then n × O|ticket|symbol|type|lots|price|sl|tp|setup time (s)  then ORDEND
// STEP 1 is read only: the app sends nothing but PING / SYMBOLS.
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
        public long Ticket; public string Symbol = "", Side = "BUY", Comment = ""; public double Lots, Open, Sl, Tp, Current, Profit, Swap; public DateTime Time; public long Magic;
        public int Dir { get { return Side == "BUY" ? 1 : -1; } }
    }
    public sealed class Mt5Order { public long Ticket; public string Symbol = "", Type = ""; public double Lots, Price, Sl, Tp; public DateTime Time; }

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
        public long Lines;
        List<Mt5Position> posIn; List<Mt5Order> ordIn;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, IC, out v) ? v : 0; }
        static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, IC, out v) ? v : 0; }
        static DateTime Unix(long sec) { return sec <= 0 ? DateTime.MinValue : new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sec); }

        // one line from the EA (unknown / short lines are ignored: a newer EA never breaks this app)
        public void Apply(string line)
        {
            if (string.IsNullOrEmpty(line)) return; var a = line.TrimEnd('\r').Split('|');
            lock (Gate)
            {
                Lines++; LastLine = DateTime.Now;
                switch (a[0])
                {
                    case "HELLO": if (a.Length >= 4) { Company = a[2]; Build = (int)L(a[3]); } Linked = true; LinkedAt = DateTime.Now; break;
                    case "BYE": Linked = false; break;
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
                        Quotes[a[1]] = new Mt5Quote { Symbol = a[1], Bid = D(a[2]), Ask = D(a[3]), Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(L(a[4])) };
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
    public sealed class Mt5Bridge : IDisposable
    {
        public readonly Mt5State State = new Mt5State(); public readonly int Port; public string Problem = "";
        public event Action Changed;
        TcpListener listener; Thread thread; volatile bool stop; TcpClient client; readonly object sendGate = new object(); StreamWriter writer;
        public Mt5Bridge(int port) { Port = port; }
        public void Start()
        {
            try { listener = new TcpListener(IPAddress.Loopback, Port); listener.Start(); }
            catch (Exception ex) { Problem = "port " + Port + " is busy (" + ex.Message + ") — is Keystone MT5 already open?"; return; }
            thread = new Thread(Run) { IsBackground = true, Name = "KeystoneMT5 bridge" }; thread.Start();
        }
        void Run()
        {
            while (!stop)
            {
                TcpClient c = null;
                try { c = listener.AcceptTcpClient(); } catch { if (stop) return; Thread.Sleep(200); continue; }
                try { if (client != null) client.Close(); } catch { }
                client = c; c.NoDelay = true;
                var th = new Thread(() => Serve(c)) { IsBackground = true, Name = "KeystoneMT5 terminal" }; th.Start();
            }
        }
        void Serve(TcpClient c)
        {
            try
            {
                var ns = c.GetStream(); var rd = new StreamReader(ns, new UTF8Encoding(false));
                lock (sendGate) writer = new StreamWriter(ns, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                string line; int n = 0;
                while (!stop && (line = rd.ReadLine()) != null) { State.Apply(line); if (++n % 4 == 0 || line == "ORDEND" || line.StartsWith("HELLO")) Fire(); }
            }
            catch { }
            lock (State.Gate) State.Linked = false; Fire();
        }
        void Fire() { var h = Changed; if (h != null) try { h(); } catch { } }
        public bool Send(string line) { lock (sendGate) { if (writer == null) return false; try { writer.WriteLine(line); return true; } catch { return false; } } }
        public void Dispose() { stop = true; try { if (listener != null) listener.Stop(); } catch { } try { if (client != null) client.Close(); } catch { } }
    }
}
