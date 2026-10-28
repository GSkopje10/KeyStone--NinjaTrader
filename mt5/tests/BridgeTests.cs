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
            // candles: H1 history (server time, oldest first), then live ticks build the forming candle / open the next one
            send("HELLO|2|Raw Trading Ltd|5000");
            Func<int, string> u = h => ((long)(new DateTime(2026, 10, 23, h, 0, 0) - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
            send("BARS|XAUUSD|60|3\nB|" + u(4) + "|3990.00|3999.00|3985.00|3995.00|1200\nB|" + u(5) + "|3995.00|4001.00|3992.00|3996.00|900\nB|" + u(6) + "|3996.00|3998.00|3994.00|3997.00|300\nBARSEND|XAUUSD|60");
            Thread.Sleep(300);
            var se = s.SeriesOf("XAUUSD", 60);
            Check(s.BridgeVersion == 2 && se.Loaded && se.Bars.Count == 3 && se.Bars[2].Time == new DateTime(2026, 10, 23, 6, 0, 0) && se.Bars[0].H == 3999, "BARS … BARSEND → 3 H1 candles at their opening time (server time)", se.Bars.Count + " " + (se.Bars.Count > 0 ? se.Bars[se.Bars.Count - 1].Time.ToString("o") : ""));
            send("TICK|XAUUSD|3999.50|4000.00|" + ((long)(new DateTime(2026, 10, 23, 6, 30, 0) - new DateTime(1970, 1, 1)).TotalMilliseconds)); Thread.Sleep(200);
            Check(se.Bars.Count == 3 && se.Bars[2].H == 3999.5 && se.Bars[2].C == 3999.5, "a tick inside the forming hour moves its high / close");
            send("TICK|XAUUSD|3998.00|3998.50|" + ((long)(new DateTime(2026, 10, 23, 7, 0, 5) - new DateTime(1970, 1, 1)).TotalMilliseconds)); Thread.Sleep(200);
            Check(se.Bars.Count == 4 && se.Bars[3].Time == new DateTime(2026, 10, 23, 7, 0, 0) && se.Bars[3].O == 3998, "the first tick of the next hour opens a new candle at 07:00");
            send("BARS|XAUUSD|60|1\nB|" + u(7) + "|3997.50|3999.00|3997.00|3998.25|50\nBARSEND|XAUUSD|60"); Thread.Sleep(200);
            Check(se.Bars.Count == 4 && se.Bars[3].O == 3997.5 && se.Bars[3].C == 3998.25, "MT5's own candle replaces the one built from ticks (same time)");
            Check(KeystoneMT5.Tf.Start(new DateTime(2026, 10, 23, 13, 47, 0), 240) == new DateTime(2026, 10, 23, 12, 0, 0) && KeystoneMT5.Tf.Start(new DateTime(2026, 10, 23, 13, 47, 0), 15) == new DateTime(2026, 10, 23, 13, 45, 0), "H4 / M15 candle starts (from the server's midnight)");
            // v3: what the bridge allows, MT5's answers, the broker's symbols, the trading commands the app sends
            send("HELLO|3|Raw Trading Ltd|5000"); send("CAN|1|1|0|5.00|10800|" + u(9)); Thread.Sleep(200);
            Check(s.BridgeVersion == 3 && s.AllowInput && s.AlgoTerminal && !s.AlgoEa && !s.CanTrade && s.ServerOffsetSec == 10800, "CAN → trading needs all three switches (here the EA's Algo Trading is off → read only)");
            send("CAN|1|1|1|5.00|10800|" + u(9)); Thread.Sleep(200); Check(s.CanTrade, "all three on → the bridge allows trading");
            send("RES|C7|OK|10009|BUY 0.10 XAUUSD: done|3524009999"); send("RES|C8|FAIL|10016|S/L T/P #1: invalid stops|1"); Thread.Sleep(200);
            Check(s.Results.Count == 2 && s.Results[0].Item1 == "C7" && s.Results[0].Item2 && !s.Results[1].Item2 && s.Results[1].Item3.Contains("invalid stops"), "RES → MT5's answers (done / invalid stops)");
            send("ALLSYMS|XAUUSD,EURUSD,US30"); send("ALLSYMS|BTCUSD"); send("ALLSYMSEND"); Thread.Sleep(200);
            Check(s.AllSymbols.Count == 4 && s.AllSymbols[0] == "BTCUSD", "ALLSYMS … ALLSYMSEND → every broker symbol, sorted", string.Join(",", s.AllSymbols));
            var rd = new System.IO.StreamReader(ns); string id = br.Command("ORDER", "XAUUSD", "BUY", 0.10, 0.0, 3990.5, 4010.25);
            var line = rd.ReadLine(); Check(id != null && line == "ORDER|" + id + "|XAUUSD|BUY|0.1|0|3990.5|4010.25", "an ORDER command goes out with its id and invariant numbers", line);
            id = br.Command("CLOSEALL", "", "PROFIT"); line = rd.ReadLine(); Check(line == "CLOSEALL|" + id + "||PROFIT", "CLOSEALL (every symbol, the ones in profit)", line);
            send("POSS|0\nPOSEND"); Thread.Sleep(200);
            Check(s.Positions.Count == 0, "an empty block clears the positions");
            send("POSS|1\nP|9|XAUUSD|BUY|0.1"); Thread.Sleep(200);
            Check(s.Positions.Count == 0, "a half-sent block changes nothing until POSEND (and a short line is ignored)");
        }
        Thread.Sleep(300);
        Check(!br.State.Linked, "MT5 gone → not linked");
        br.Dispose();
        Console.WriteLine(fails == 0 ? "ALL MT5 BRIDGE TESTS PASSED" : fails + " MT5 BRIDGE TEST(S) FAILED");
        fails += SimTests.Main2();
        fails += HedgeLabTests.Main3();
        return fails == 0 ? 0 : 1;
    }
}
