// Compile-only stand-ins for the NinjaTrader 8 API members src/KeyStone.cs uses.
using System;
using System.Collections.Generic;

namespace NinjaTrader.Cbi
{
    public enum ErrorCode { NoError, UserAbort, Panic, LogOnFailed, Unknown }
    public class MasterInstrument { public string Name { get; set; } public double TickSize { get; set; } public double PointValue { get; set; } public DateTime GetNextExpiry(DateTime d) { return d; } }
    public class Instrument
    {
        public string FullName { get; set; }
        public MasterInstrument MasterInstrument { get; set; }
        public DateTime Expiry { get; set; }
        public static Instrument GetInstrument(string name) { return null; }
        public static Instrument GetInstrument(string name, bool create) { return null; }
    }
}

namespace NinjaTrader.Data
{
    using NinjaTrader.Cbi;
    public enum BarsPeriodType { Tick, Volume, Range, Second, Minute, Day, Week, Month, Year }
    public enum MergePolicy { DoNotMerge, MergeBackAdjusted, MergeNonBackAdjusted, UseGlobalSettings }
    public class BarsPeriod { public BarsPeriodType BarsPeriodType { get; set; } public int Value { get; set; } }
    public class TradingHours { public string Name { get; set; } public static TradingHours Get(string name) { return null; } }
    public class Bars
    {
        public int Count { get { return 0; } }
        public DateTime GetTime(int i) { return DateTime.MinValue; }
        public double GetOpen(int i) { return 0; }
        public double GetHigh(int i) { return 0; }
        public double GetLow(int i) { return 0; }
        public double GetClose(int i) { return 0; }
        public long GetVolume(int i) { return 0; }
        public TradingHours TradingHours { get; set; }
        public Instrument Instrument { get; set; }
        public BarsPeriod BarsPeriod { get; set; }
    }
    public class BarsRequest : IDisposable
    {
        public BarsRequest(Instrument instrument, DateTime from, DateTime to) { }
        public BarsRequest(Instrument instrument, int barsBack) { }
        public BarsPeriod BarsPeriod { get; set; }
        public TradingHours TradingHours { get; set; }
        public MergePolicy MergePolicy { get; set; }
        public Bars Bars { get { return null; } }
        public Instrument Instrument { get { return null; } }
        public void Request(Action<BarsRequest, ErrorCode, string> callback) { }
        public void Dispose() { }
    }
}

namespace NinjaTrader.Gui
{
    public enum DashStyleHelper { Solid, Dash, DashDot, DashDotDot, Dot }
    public class ControlCenter : NinjaTrader.Gui.Tools.NTWindow { public object FindFirst(string name) { return null; } }
}

namespace NinjaTrader.Gui.Tools
{
    public class SimpleFont { public SimpleFont() { } public SimpleFont(string family, double size) { } }
    public class NTMenuItem : System.Windows.Controls.MenuItem { }
    public class NTWindow : System.Windows.Window { }
}

namespace NinjaTrader.Gui.Chart
{
    public class ChartControl : System.Windows.Controls.UserControl { }
}

namespace NinjaTrader.Gui.ControlCenterNs { }

namespace NinjaTrader.NinjaScript
{
    public enum State { SetDefaults, Configure, Active, DataLoaded, Historical, Transition, Realtime, Terminated, Finalized }
    public enum Calculate { OnBarClose, OnEachTick, OnPriceChange }
    [AttributeUsage(AttributeTargets.Property)] public sealed class NinjaScriptPropertyAttribute : Attribute { }
    public abstract class NinjaScriptBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public State State { get; set; }
        protected virtual void OnStateChange() { }
    }
    public class ISeries<T> { public T this[int barsAgo] { get { return default(T); } } public int Count { get { return 0; } } }
    public class TimeSeries : ISeries<DateTime> { }
    public class PriceSeries : ISeries<double> { }
    public abstract class IndicatorRenderBase : NinjaScriptBase
    {
        public Calculate Calculate { get; set; }
        public bool IsOverlay { get; set; }
        public bool DisplayInDataBox { get; set; }
        public NinjaTrader.Data.BarsPeriod BarsPeriod { get { return null; } }
        public NinjaTrader.Data.Bars Bars { get { return null; } }
        public int CurrentBar { get { return 0; } }
        public NinjaTrader.Cbi.Instrument Instrument { get { return null; } }
        public TimeSeries Time { get { return null; } }
        public PriceSeries Open { get { return null; } }
        public PriceSeries High { get { return null; } }
        public PriceSeries Low { get { return null; } }
        public PriceSeries Close { get { return null; } }
        public double TickSize { get { return 0.25; } }
        protected virtual void OnBarUpdate() { }
    }
    public abstract class AddOnBase : NinjaScriptBase
    {
        protected virtual void OnWindowCreated(System.Windows.Window window) { }
        protected virtual void OnWindowDestroyed(System.Windows.Window window) { }
    }
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public abstract class Indicator : NinjaTrader.NinjaScript.IndicatorRenderBase { }
}

namespace NinjaTrader.NinjaScript.AddOns { }

namespace NinjaTrader.NinjaScript.DrawingTools
{
    using System.Windows.Media;
    public class DrawingTool { }
    public static class Draw
    {
        public static DrawingTool Rectangle(NinjaTrader.NinjaScript.NinjaScriptBase owner, string tag, bool isAutoScale, DateTime startTime, double startY, DateTime endTime, double endY, Brush brush, Brush areaBrush, int areaOpacity, bool drawOnPricePanel) { return null; }
        public static DrawingTool ArrowLine(NinjaTrader.NinjaScript.NinjaScriptBase owner, string tag, bool isAutoScale, int startBarsAgo, double startY, int endBarsAgo, double endY, Brush brush, NinjaTrader.Gui.DashStyleHelper dashStyle, int width, bool drawOnPricePanel) { return null; }
        public static DrawingTool Text(NinjaTrader.NinjaScript.NinjaScriptBase owner, string tag, bool isAutoScale, string text, DateTime time, double y, int yPixelOffset, Brush textBrush, NinjaTrader.Gui.Tools.SimpleFont font, System.Windows.TextAlignment alignment, Brush outlineBrush, Brush areaBrush, int areaOpacity) { return null; }
    }
}

namespace NinjaTrader.Gui.ControlCenterHost { }
