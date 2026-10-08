// Research harness — data layer. Loads the studio's saved 1-minute bars (Documents\KeystoneArcData\Studio
// layout, uploaded to data/) into one fixed grid per session: slot k = the minute that starts 18:00 + k (NY).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NinjaTrader.NinjaScript;

public sealed class RDay
{
    public DateTime Day; public string Sym; public int Bars; public bool Full;
    public float[] O = new float[R.Slots], H = new float[R.Slots], L = new float[R.Slots], C = new float[R.Slots]; public int[] V = new int[R.Slots];
    public bool Has(int k) { return k >= 0 && k < R.Slots && !float.IsNaN(C[k]); }
    // last known close at or before slot k (NaN if none)
    public double CloseAt(int k) { for (int i = Math.Min(k, R.Slots - 1); i >= 0; i--) if (!float.IsNaN(C[i])) return C[i]; return double.NaN; }
    public double OpenAt(int k) { for (int i = Math.Max(0, k); i < R.Slots; i++) if (!float.IsNaN(O[i])) return O[i]; return double.NaN; }
    public double Hi(int a, int b) { double h = double.NaN; for (int i = Math.Max(0, a); i <= Math.Min(b, R.Slots - 1); i++) if (!float.IsNaN(H[i]) && (double.IsNaN(h) || H[i] > h)) h = H[i]; return h; }
    public double Lo(int a, int b) { double l = double.NaN; for (int i = Math.Max(0, a); i <= Math.Min(b, R.Slots - 1); i++) if (!float.IsNaN(L[i]) && (double.IsNaN(l) || L[i] < l)) l = L[i]; return l; }
    public double Atr;      // average full-session range of the 14 previous full days (points)
    public double RthAtr;   // average 09:30–16:00 range of the 14 previous full days
    public RDay Prev;       // previous full session
}

public static partial class R
{
    public const int Slots = 1380;               // 18:00 → 17:00
    public static int S(int hh, int mm) { int m = hh * 60 + mm - 18 * 60; if (m < 0) m += 1440; return m; }
    public static string T(int slot) { int m = (slot + 18 * 60) % 1440; return (m / 60).ToString("00") + ":" + (m % 60).ToString("00"); }
    public static readonly DateTime OosStart = new DateTime(2024, 1, 1);

    public static List<RDay> Load(string folder, string sym) { return Load(folder, sym, false); }
    // every = every session with at least an hour of bars (not only full days; no ATR needed)
    public static List<RDay> Load(string folder, string sym, bool every)
    {
        string cache = Path.Combine(folder, sym + ".grid");
        var days = new List<RDay>();
        if (File.Exists(cache))
        {
            using (var br = new BinaryReader(File.OpenRead(cache)))
            {
                int n = br.ReadInt32();
                for (int d = 0; d < n; d++)
                {
                    var x = new RDay { Day = new DateTime(br.ReadInt64()), Sym = sym, Bars = br.ReadInt32() };
                    for (int k = 0; k < Slots; k++) { x.O[k] = br.ReadSingle(); x.H[k] = br.ReadSingle(); x.L[k] = br.ReadSingle(); x.C[k] = br.ReadSingle(); x.V[k] = br.ReadInt32(); }
                    days.Add(x);
                }
            }
        }
        else
        {
            var sessions = KeystoneStudioStore.Sessions(folder, sym);
            foreach (var kv in sessions)
            {
                if (kv.Key.DayOfWeek == DayOfWeek.Saturday || kv.Key.DayOfWeek == DayOfWeek.Sunday) continue;
                var bars = KeystoneStudioStore.Load(folder, sym, kv.Key, kv.Key);
                var x = new RDay { Day = kv.Key, Sym = sym };
                for (int k = 0; k < Slots; k++) { x.O[k] = x.H[k] = x.L[k] = x.C[k] = float.NaN; }
                foreach (var b in bars)
                {
                    var open = b.Time.AddMinutes(-1);
                    int m = open.Hour * 60 + open.Minute - 18 * 60; if (m < 0) m += 1440;
                    if (m < 0 || m >= Slots) continue;
                    x.O[m] = (float)b.Open; x.H[m] = (float)b.High; x.L[m] = (float)b.Low; x.C[m] = (float)b.Close; x.V[m] = (int)Math.Min(int.MaxValue, b.Volume); x.Bars++;
                }
                days.Add(x);
            }
            using (var bw = new BinaryWriter(File.Create(cache)))
            {
                bw.Write(days.Count);
                foreach (var x in days) { bw.Write(x.Day.Ticks); bw.Write(x.Bars); for (int k = 0; k < Slots; k++) { bw.Write(x.O[k]); bw.Write(x.H[k]); bw.Write(x.L[k]); bw.Write(x.C[k]); bw.Write(x.V[k]); } }
            }
        }
        if (every) { var ev = days.Where(x => x.Bars >= 60).OrderBy(x => x.Day).ToList(); for (int i = 0; i < ev.Count; i++) ev[i].Prev = i > 0 ? ev[i - 1] : null; return ev; }
        // full days only: 800+ minutes and the 09:30–16:00 cash session present
        int s930 = S(9, 30), s1559 = S(15, 59);
        foreach (var x in days) x.Full = x.Bars >= 800 && x.Has(s930) && x.Has(s1559 - 1);
        var full = days.Where(x => x.Full).ToList();
        for (int i = 0; i < full.Count; i++)
        {
            full[i].Prev = i > 0 ? full[i - 1] : null;
            if (i >= 14)
            {
                double a = 0, b2 = 0;
                for (int j = i - 14; j < i; j++) { a += full[j].Hi(0, Slots - 1) - full[j].Lo(0, Slots - 1); b2 += full[j].Hi(s930, s1559) - full[j].Lo(s930, s1559); }
                full[i].Atr = a / 14; full[i].RthAtr = b2 / 14;
            }
        }
        return full.Where(x => x.Atr > 0).ToList();
    }
}
