// 🏷 the firm plan from the account's REAL balance: a Take Profit Trader 25K account is never read as the 50K one (user's catch: "BLOWN, −$25,000")
using System; using System.Linq; using NinjaTrader.NinjaScript;
public static class FirmSizeTests
{
    static int fails;
    static void Check(bool ok, string name, string d = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok ? "" : "  → " + d)); if (!ok) fails++; }
    public static int Main()
    {
        Check(KeystoneFirmRules.Guess("TAKEPROFIT111").Name == "TAKE PROFIT TRADER 50K", "no balance known yet: the name alone → the 50K plan (as before)");
        lock (KeystoneFirmRules.Balances) { KeystoneFirmRules.Balances["TAKEPROFIT222"] = 25000; KeystoneFirmRules.Balances["TAKEPROFIT333"] = 50120; KeystoneFirmRules.Balances["TAKEPROFIT444"] = 24100; }
        var g25 = KeystoneFirmRules.Guess("TAKEPROFIT222"); var g50 = KeystoneFirmRules.Guess("TAKEPROFIT333"); var gLoss = KeystoneFirmRules.Guess("TAKEPROFIT444");
        Check(g25.Name == "TAKE PROFIT TRADER 25K" && g25.Size == 25000 && g25.Dd == 1500, "a $25,000 account → TAKE PROFIT TRADER 25K (drawdown $1,500, liquidates at $23,500)", g25.Name);
        Check(g50.Name == "TAKE PROFIT TRADER 50K" && gLoss.Name == "TAKE PROFIT TRADER 25K", "a $50,120 account stays 50K; a 25K that lost $900 stays 25K", g50.Name + " / " + gLoss.Name);
        Check(KeystoneFirmRules.Resolve("TAKE PROFIT TRADER 50K", "TAKEPROFIT222").Name == "TAKE PROFIT TRADER 25K" && KeystoneFirmRules.Resolve("TAKE PROFIT TRADER 50K", "TAKEPROFIT333").Name == "TAKE PROFIT TRADER 50K",
            "a 50K plan saved for a $25,000 account is corrected to 25K; a real 50K keeps its saved plan");
        Check(KeystoneFirmRules.Resolve("MFFU RAPID EOD 50K", "TAKEPROFIT222").Name == "TAKE PROFIT TRADER 25K" && KeystoneFirmRules.Resolve("LUCID FLEX 50K", "TAKEPROFIT333").Name == "LUCID FLEX 50K",
            "another firm's 50K plan saved for a $25,000 TAKEPROFIT account → its real TPT 25K (a matching size keeps the saved plan)");
        Console.WriteLine(fails == 0 ? "ALL FIRM SIZE TESTS PASSED" : fails + " FIRM SIZE TEST(S) FAILED"); return fails;
    }
}
