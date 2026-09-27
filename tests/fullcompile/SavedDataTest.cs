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
        // One Full Globex session (18:00 → 16:55 next day) whose receipt stops at midnight must not be saved.
        itemType.GetField("Start").SetValue(item, new DateTime(2026, 9, 24, 18, 0, 0));
        itemType.GetField("End").SetValue(item, new DateTime(2026, 9, 25, 16, 55, 0));
        var night = new List<KeystoneArcBar>();
        for (DateTime t = new DateTime(2026, 9, 24, 18, 5, 0); t <= new DateTime(2026, 9, 25, 0, 0, 0); t = t.AddMinutes(5)) night.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = 1, High = 2, Low = 0.5, Close = 1.5 });
        mem.Clear();
        labType.GetMethod("StoreSavedBars", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { item, (object)night });
        bool dayPartialSkipped = mem.Count == 0;
        Console.WriteLine((dayPartialSkipped ? "PASS" : "FAIL") + "  a one-day receipt that stops at midnight is not saved");
        // The same midnight-truncated receipt queues one continuation 00:00 → 16:55 that is merged
        // into the series; a complete receipt and a long real gap queue nothing.
        var queueType = typeof(Queue<>).MakeGenericType(itemType);
        var queue = Activator.CreateInstance(queueType);
        labType.GetField("historicalRequestQueue", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, queue);
        labType.GetField("historicalRequestDetails", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lab, new List<string>());
        var cont = labType.GetMethod("QueueMidnightContinuation", BindingFlags.NonPublic | BindingFlags.Instance);
        cont.Invoke(lab, new[] { item, (object)night });
        int queued = (int)queueType.GetProperty("Count").GetValue(queue);
        object next = queued == 1 ? queueType.GetMethod("Dequeue").Invoke(queue, null) : null;
        bool continuation = next != null && (DateTime)itemType.GetField("Start").GetValue(next) == new DateTime(2026, 9, 25) && (DateTime)itemType.GetField("End").GetValue(next) == new DateTime(2026, 9, 25, 16, 55, 0) && (bool)itemType.GetField("Append").GetValue(next);
        Console.WriteLine((continuation ? "PASS" : "FAIL") + "  a Full Globex receipt that stops at midnight queues the 00:00 → 16:55 continuation");
        var fullDay = new List<KeystoneArcBar>(night);
        for (DateTime t = new DateTime(2026, 9, 25, 0, 5, 0); t <= new DateTime(2026, 9, 25, 16, 55, 0); t = t.AddMinutes(5)) fullDay.Add(new KeystoneArcBar { Symbol = "MNQ", Time = t, Open = 1, High = 2, Low = 0.5, Close = 1.5 });
        cont.Invoke(lab, new[] { item, (object)fullDay });
        itemType.GetField("Start").SetValue(item, new DateTime(2026, 9, 14, 18, 0, 0));
        cont.Invoke(lab, new[] { item, (object)bars });
        bool noExtra = (int)queueType.GetProperty("Count").GetValue(queue) == 0;
        Console.WriteLine((noExtra ? "PASS" : "FAIL") + "  a complete receipt or a long real gap queues no continuation");
        bool ok = inMemory && same && partialSkipped && dayPartialSkipped && continuation && noExtra;
        Console.WriteLine(ok ? "SAVED DATA TEST PASSED" : "SAVED DATA TEST FAILED");
        return ok ? 0 : 1;
    }
}
