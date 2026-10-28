// several MT5 accounts on ONE port at once: each terminal's connection goes to its own account's link (its own state and commands);
// the first account = the first link (as before); a reconnect comes back to its own link; an account switch inside a terminal moves it
using System; using System.Collections.Generic; using System.Linq; using System.Net.Sockets; using System.Text; using System.Threading; using KeystoneMT5;
public static class MultiTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    sealed class Term
    {
        public TcpClient C; NetworkStream ns; public readonly List<string> Got = new List<string>(); readonly StringBuilder sb = new StringBuilder();
        public Term(int port) { C = new TcpClient("127.0.0.1", port); ns = C.GetStream(); var t = new Thread(() => { var b = new byte[4096]; try { int k; while ((k = ns.Read(b, 0, b.Length)) > 0) lock (Got) { sb.Append(Encoding.UTF8.GetString(b, 0, k)); var s = sb.ToString(); int i; while ((i = s.IndexOf('\n')) >= 0) { Got.Add(s.Substring(0, i)); s = s.Substring(i + 1); } sb.Clear(); sb.Append(s); } } catch { } }) { IsBackground = true }; t.Start(); }
        public void Send(string s) { var b = Encoding.UTF8.GetBytes(s + "\n"); ns.Write(b, 0, b.Length); }
        public void Hello(string company, long login, string server, double bal, string kind) { Send("HELLO|5|" + company + "|5000"); Send("SYM|XAUUSD|2|100.00|0.01|0.01|100.00|0.01000000|1.000000|0.01000000|Gold vs US Dollar"); Send("ACC|" + login + "|Trader|" + server + "|USD|" + bal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + bal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|0|" + bal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|0|500|HEDGE|1|" + kind); }
    }
    static bool Wait(Func<bool> ok, int ms = 3000) { var t = DateTime.Now; while (!ok() && (DateTime.Now - t).TotalMilliseconds < ms) Thread.Sleep(30); return ok(); }

    public static int Main5()
    {
        var first = new Mt5Bridge(47998); first.Start(); var added = new List<Mt5Bridge>(); first.Hub.Added += l => { lock (added) added.Add(l); };
        var ic = new Term(47998); ic.Hello("Raw Trading Ltd", 5298760, "ICMarketsSC-MT5-2", 15708.3, "DEMO");
        Check(Wait(() => first.State.Account.Login == 5298760), "the first account is the first link (as before)", first.State.Account.Login + "");
        var ftmo = new Term(47998); ftmo.Hello("FTMO S.R.O.", 1510123, "FTMO-Server3", 100000, "REAL");
        Check(Wait(() => first.Hub.Links.Count == 2) && Wait(() => first.Hub.Links[1].State.Account.Login == 1510123), "a second terminal on the SAME port → its own link", string.Join(",", first.Hub.Links.Select(l => l.Key)));
        var two = first.Hub.Links[1];
        Check(first.State.Account.Login == 5298760 && Math.Abs(two.State.Account.Balance - 100000) < 1e-6 && two.State.Company == "FTMO S.R.O." && first.State.Company == "Raw Trading Ltd", "each link keeps its own account (no mixing)", first.State.Company + " / " + two.State.Company);
        Check(added.Count == 1 && added[0] == two, "the hub tells the app about the new account", added.Count + "");
        ic.Send("TICK|XAUUSD|4000.10|4000.30|1792800000000"); ftmo.Send("TICK|XAUUSD|4000.50|4000.90|1792800000000");
        Check(Wait(() => first.State.Quotes.ContainsKey("XAUUSD") && two.State.Quotes.ContainsKey("XAUUSD")) && Math.Abs(first.State.Quotes["XAUUSD"].Bid - 4000.10) < 1e-9 && Math.Abs(two.State.Quotes["XAUUSD"].Bid - 4000.50) < 1e-9, "each account has its own prices (its broker's)");
        ftmo.Send("POSS|1\nP|77|XAUUSD|BUY|1.00|4000.00|0|0|4000.5|50|0|1792795600|0|\nPOSEND");
        Check(Wait(() => two.State.Positions.Count == 1) && first.State.Positions.Count == 0, "positions stay on their own account", first.State.Positions.Count + " / " + two.State.Positions.Count);
        // a command goes ONLY to its own terminal
        var id = two.Command("ORDER", "XAUUSD", "BUY", 0.5, 0.0, 0.0, 0.0);
        Check(id != null && Wait(() => { lock (ftmo.Got) return ftmo.Got.Any(x => x.StartsWith("ORDER|" + id + "|XAUUSD|BUY|0.5")); }) && !ic.Got.Any(x => x.StartsWith("ORDER|")), "an order from the FTMO link reaches only the FTMO terminal", string.Join(" | ", ftmo.Got));
        first.Command("CLOSEALL", "XAUUSD", "ALL");
        Check(Wait(() => { lock (ic.Got) return ic.Got.Any(x => x.StartsWith("CLOSEALL|")); }) && !ftmo.Got.Any(x => x.StartsWith("CLOSEALL|")), "a close all from the IC link reaches only IC");
        // a terminal restarts: it comes back to its own link (same object, same windows)
        ftmo.C.Close(); Check(Wait(() => !two.State.Linked), "FTMO closed → only its link is off", first.State.Linked + "");
        var ftmo2 = new Term(47998); ftmo2.Hello("FTMO S.R.O.", 1510123, "FTMO-Server3", 100050, "REAL");
        Check(Wait(() => Math.Abs(two.State.Account.Balance - 100050) < 1e-6) && first.Hub.Links.Count == 2, "FTMO reconnects → back to its own link (no third)", first.Hub.Links.Count + "");
        // an account switch inside a terminal: the IC terminal logs into another account → a new link; the IC link goes off
        ic.Send("ACC|5300001|Trader|ICMarketsSC-MT5-2|USD|500|500|0|500|0|500|HEDGE|1|DEMO");
        Check(Wait(() => first.Hub.Links.Count == 3) && Wait(() => first.Hub.Links[2].State.Account.Login == 5300001) && first.Hub.Links[2].State.Company == "Raw Trading Ltd" && first.Hub.Links[2].State.Symbols.ContainsKey("XAUUSD") && Wait(() => !first.State.Linked), "an account switch in one terminal → that account's own link (its terminal, symbols)", first.Hub.Links.Count + "");
        first.Dispose();
        return fails;
    }
}
