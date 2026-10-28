using System; using System.Linq; using KeystoneMT5;
public static class Trace
{
    public static void Run(string data, DateTime from, DateTime to, HedgeSettings s)
    {
        var bars = Gold.Load(data, from.AddDays(-3), to); s.Trace = true; var r = HedgeLab.Run(bars, s, from, to);
        foreach (var l in r.Log) Console.WriteLine(l); Console.WriteLine(HedgeLab.Line(r));
    }
}
