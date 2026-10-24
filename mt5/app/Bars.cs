// SpasiX MT5 — candles: the history from MT5 (BARS … BARSEND) plus the live ticks building the forming candle.
// Times are MT5 SERVER time (what MT5 shows), a candle's time = its OPENING minute.
using System;
using System.Collections.Generic;

namespace KeystoneMT5
{
    public sealed class Mt5Bar { public DateTime Time; public double O, H, L, C; public long V; }

    public static class Tf
    {
        public static readonly int[] All = { 1, 5, 15, 30, 60, 240, 1440, 10080 };
        public static string Name(int m) { return m >= 10080 ? "W1" : m >= 1440 ? "D1" : m >= 60 ? "H" + (m / 60) : "M" + m; }
        // the opening time of the candle a moment belongs to (H4 / D1 from the server's midnight, W1 from Sunday like MT5)
        public static DateTime Start(DateTime t, int m)
        {
            if (m >= 10080) return t.Date.AddDays(-(int)t.DayOfWeek);
            if (m >= 1440) return t.Date;
            long mins = (long)(t - t.Date).TotalMinutes; return t.Date.AddMinutes(mins - mins % m);
        }
    }

    public sealed class BarSeries
    {
        public readonly string Symbol; public readonly int Minutes; public readonly List<Mt5Bar> Bars = new List<Mt5Bar>();
        public bool Loaded; public DateTime AskedAt = DateTime.MinValue, LoadedAt = DateTime.MinValue; public int Version;
        public BarSeries(string symbol, int minutes) { Symbol = symbol; Minutes = minutes; }
        // history from MT5: new candles are added, the ones with the same time replaced (MT5's own values win), always sorted
        public void Merge(List<Mt5Bar> incoming)
        {
            if (incoming == null || incoming.Count == 0) return;
            var byTime = new Dictionary<DateTime, int>(); for (int i = 0; i < Bars.Count; i++) byTime[Bars[i].Time] = i;
            bool tailOnly = Bars.Count > 0 && incoming[0].Time >= Bars[Bars.Count - 1].Time;
            foreach (var b in incoming) { int i; if (byTime.TryGetValue(b.Time, out i)) Bars[i] = b; else { Bars.Add(b); byTime[b.Time] = Bars.Count - 1; } }
            if (!tailOnly) Bars.Sort((a, b) => a.Time.CompareTo(b.Time));
            Loaded = true; LoadedAt = DateTime.Now; Version++;
        }
        // a live price: it moves the forming candle, or opens the next one
        public void OnTick(DateTime t, double price)
        {
            if (Bars.Count == 0 || price <= 0) return; var st = Tf.Start(t, Minutes); var last = Bars[Bars.Count - 1];
            if (st < last.Time) return;
            if (st == last.Time) { last.H = Math.Max(last.H, price); last.L = Math.Min(last.L, price); last.C = price; }
            else Bars.Add(new Mt5Bar { Time = st, O = price, H = price, L = price, C = price });
            Version++;
        }
    }
}
