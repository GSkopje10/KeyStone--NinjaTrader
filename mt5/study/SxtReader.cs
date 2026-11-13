// SpasiX 13r — reader for the .sxt.gz real-tick files of 📦 EXPORT TICKS FOR CLAUDE (format: the export's 00-README.txt / mt5/app/TickLib.cs TickCodec.Spec).
// Standalone (no SpasiX types): var f = SxtReader.Read("XAUUSD-2026-10.sxt.gz"); foreach (var d in f.Days) foreach (var t in d.Ticks) … t.Time, t.Bid, t.Ask
using System; using System.Collections.Generic; using System.IO; using System.IO.Compression; using System.Text;
public static class SxtReader
{
    public struct Tick { public DateTime Time; public double Bid, Ask; }
    public sealed class Day { public DateTime Date; public List<Tick> Ticks = new List<Tick>(); }
    public sealed class File1 { public string Symbol = ""; public int Digits; public double Point; public List<Day> Days = new List<Day>(); }
    static ulong VarU(Stream s) { ulong r = 0; int sh = 0; while (true) { int b = s.ReadByte(); if (b < 0) throw new EndOfStreamException(); r |= (ulong)(b & 0x7f) << sh; if ((b & 0x80) == 0) return r; sh += 7; } }
    static long Unzig(ulong u) { return (long)(u >> 1) ^ -(long)(u & 1); }
    public static File1 Read(string path) { using (var fs = File.OpenRead(path)) return Read(fs); }
    public static File1 Read(Stream inp)
    {
        var f = new File1();
        using (var gz = new GZipStream(inp, CompressionMode.Decompress, true))
        using (var br = new BinaryReader(gz, Encoding.UTF8, true))
        {
            if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "SXT1") throw new InvalidDataException("not an SXT1 file");
            int n = (int)VarU(gz); f.Symbol = Encoding.UTF8.GetString(br.ReadBytes(n)); f.Digits = br.ReadInt32(); f.Point = br.ReadDouble();
            while (true)
            {
                long dt; try { dt = br.ReadInt64(); } catch (EndOfStreamException) { break; }
                var d = new Day { Date = new DateTime(dt) }; int cnt = (int)VarU(gz); long pb = 0, ps = 0, pt = 0;
                for (int i = 0; i < cnt; i++)
                {
                    ulong u = VarU(gz); pt += (long)(u >> 1); var tm = d.Date.AddMilliseconds(pt);
                    if ((u & 1) == 1) { double b = br.ReadDouble(), a = br.ReadDouble(); d.Ticks.Add(new Tick { Time = tm, Bid = b, Ask = a }); pb = (long)Math.Round(b / f.Point); ps = (long)Math.Round(a / f.Point) - pb; continue; }
                    pb += Unzig(VarU(gz)); ps += Unzig(VarU(gz));
                    d.Ticks.Add(new Tick { Time = tm, Bid = Math.Round(pb * f.Point, f.Digits), Ask = Math.Round((pb + ps) * f.Point, f.Digits) });
                }
                f.Days.Add(d);
            }
        }
        return f;
    }
}
