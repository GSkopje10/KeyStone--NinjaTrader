// the bridge against a FAKE MT5: a TCP client sends what the KeystoneBridge EA sends; the app's state must match
using System; using System.Linq; using System.Net.Sockets; using System.Text; using System.Threading; using KeystoneMT5;
public static class BridgeTests
{
    static int fails;
    static void Check(bool ok, string name, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + detail)); if (!ok) fails++; }
    public static int Main()
    {
        var br = new Mt5Bridge(47999); br.Start(); Check(br.Problem == "", "the app listens on 127.0.0.1", br.Problem);
        using (var c = new TcpClient("127.0.0.1", 47999))
        {
            var ns = c.GetStream();
            Action<string> send = s => { var b = Encoding.UTF8.GetBytes(s + "\n"); ns.Write(b, 0, b.Length); };
            send("HELLO|1|Raw Trading Ltd|5000");
            send("SYM|XAUUSD|2|100.00|0.01|0.01|100.00|0.01000000|1.000000|0.01000000|Gold vs US Dollar");
            send("ACC|5298760|Spasic Goran|ICMarketsSC-MT5|USD|15708.30|5077.25|802.46|4274.79|632.71|500|HEDGE|1|REAL");
            send("TICK|XAUUSD|3997.03|3997.53|1792800000000");
            send("POSS|3\nP|3524004628|XAUUSD|SELL|0.10|3994.56|0.00|0.00|3997.53|-29.70|3.66|1792795645|0|\nP|3524004624|XAUUSD|SELL|0.10|3994.60|0.00|0.00|3997.53|-29.30|3.66|1792795635|0|\nP|1|XAUUSD|BUY|0.20|3990.00|3980.00|4010.00|3997.03|150.60|0.00|1792795600|7|grid a|b\nPOSEND");
            send("ORDS|1\nO|3524005232|XAUUSD|BUY STOP|1.00|4017.00|0.00|0.00|1792796224\nORDEND");
            Thread.Sleep(400);
            var s = br.State;
            Check(s.Alive && s.Company == "Raw Trading Ltd", "HELLO → linked", s.Company);
            Check(s.Account.Login == 5298760 && Math.Abs(s.Account.Equity - 5077.25) < 1e-9 && s.Account.Mode == "HEDGE" && s.Account.Kind == "REAL" && Math.Abs(s.Account.Floating - (5077.25 - 15708.30)) < 1e-9, "ACC → balance, equity, open P/L, hedging, real");
            Check(s.Symbols["xauusd"].Contract == 100 && s.Symbols["XAUUSD"].VolStep == 0.01, "SYM → contract 100, lot step 0.01");
            Check(Math.Abs(s.Quotes["XAUUSD"].Ask - 3997.53) < 1e-9 && s.Quotes["XAUUSD"].Spread(0.01) == 50, "TICK → bid / ask, spread 50 points");
            Check(s.Positions.Count == 3 && s.Positions.Count(p => p.Dir < 0) == 2 && s.Positions.Single(p => p.Dir > 0).Sl == 3980, "POSS … POSEND → 3 tickets, buys and sells together (hedging), S/L kept", s.Positions.Count.ToString());
            Check(s.Orders.Count == 1 && s.Orders[0].Type == "BUY STOP" && s.Orders[0].Price == 4017, "ORDS … ORDEND → the buy stop at 4017");
            send("POSS|0\nPOSEND"); Thread.Sleep(200);
            Check(s.Positions.Count == 0, "an empty block clears the positions");
            send("POSS|1\nP|9|XAUUSD|BUY|0.1"); Thread.Sleep(200);
            Check(s.Positions.Count == 0, "a half-sent block changes nothing until POSEND (and a short line is ignored)");
        }
        Thread.Sleep(300);
        Check(!br.State.Linked, "MT5 gone → not linked");
        br.Dispose();
        Console.WriteLine(fails == 0 ? "ALL MT5 BRIDGE TESTS PASSED" : fails + " MT5 BRIDGE TEST(S) FAILED");
        return fails == 0 ? 0 : 1;
    }
}
