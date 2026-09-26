// Round-trip test of the lab's saved-data store (disk + memory), run through the real lab class
// compiled against the stand-in stubs. Uses reflection because the members are private.
using System; using System.Collections.Generic; using System.Linq; using System.Reflection;
using NinjaTrader.NinjaScript; using NinjaTrader.NinjaScript.AddOns;
public static class SavedDataTest
{
    public static int Main()
    {
        Environment.SetEnvironmentVariable("HOME", System.IO.Path.GetTempPath());
        var labType = typeof(KeystoneArc5MResearchLab);
        var itemType = labType.GetNestedType("HistoricalRequestWorkItem", BindingFlags.NonPublic);
        object item = Activator.CreateInstance(itemType, true);
        itemType.GetField("Key").SetValue(item, "MNQ");
        itemType.GetField("Instrument").SetValue(item, new NinjaTrader.Cbi.Instrument { FullName = "MNQ 03-26" });
        itemType.GetField("Minutes").SetValue(item, 1);
        itemType.GetField("Start").SetValue(item, new DateTime(2025, 1, 2, 17, 57, 0));
        itemType.GetField("End").SetValue(item, new DateTime(2025, 1, 10, 15, 55, 0));
        itemType.GetField("TradingHoursSource").SetValue(item, "CME US Index Futures ETH");
        var bars = new List<KeystoneArcBar>();
        for (int i = 0; i < 5000; i++) bars.Add(new KeystoneArcBar { Symbol = "MNQ", Time = new DateTime(2025, 1, 2, 18, 0, 0).AddMinutes(i * 2.3), Open = 20000 + i, High = 20001 + i, Low = 19999 + i, Close = 20000.25 + i, Volume = i });
        labType.GetMethod("StoreSavedBars", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { item, (object)bars });
        // clear memory so the disk file is read
        var mem = (System.Collections.IDictionary)labType.GetField("savedBarsMemory", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        bool inMemory = mem.Count == 1; mem.Clear();
        var lab = (KeystoneArc5MResearchLab)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(labType);
        var loaded = (List<KeystoneArcBar>)labType.GetMethod("LoadSavedBars", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lab, new[] { item });
        bool same = loaded != null && loaded.Count == bars.Count && loaded.Zip(bars, (a, b) => a.Time == b.Time && a.Open == b.Open && a.High == b.High && a.Low == b.Low && a.Close == b.Close && a.Volume == b.Volume && a.Symbol == "MNQ").All(x => x);
        Console.WriteLine((inMemory ? "PASS" : "FAIL") + "  stored in memory");
        Console.WriteLine((same ? "PASS" : "FAIL") + "  disk round trip returns identical bars (" + (loaded == null ? 0 : loaded.Count) + ")");
        itemType.GetField("End").SetValue(item, new DateTime(2025, 3, 10, 15, 55, 0));
        mem.Clear();
        labType.GetMethod("StoreSavedBars", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { item, (object)bars });
        bool partialSkipped = mem.Count == 0;
        Console.WriteLine((partialSkipped ? "PASS" : "FAIL") + "  a receipt that does not cover the range is not saved");
        bool ok = inMemory && same && partialSkipped;
        Console.WriteLine(ok ? "SAVED DATA TEST PASSED" : "SAVED DATA TEST FAILED");
        return ok ? 0 : 1;
    }
}
