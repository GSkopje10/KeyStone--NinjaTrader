// Compile-only stand-ins for the NinjaTrader 8 API members src/KeyStone.cs uses.
using System;
using System.Collections.Generic;

namespace NinjaTrader.Cbi
{
    public enum ErrorCode { NoError, UserAbort, Panic, LogOnFailed, Unknown }
    public class MasterInstrument { public string Name { get; set; } public double TickSize { get; set; } public double PointValue { get; set; } public DateTime GetNextExpiry(DateTime d) { return d; } public double RoundToTickSize(double p) { return p; } }
    public enum AccountItem { CashValue, NetLiquidation, RealizedProfitLoss, UnrealizedProfitLoss, TotalCashBalance }
    public enum Currency { UsDollar }
    public enum MarketPosition { Flat, Long, Short }
    public class Position { public Instrument Instrument { get; set; } public MarketPosition MarketPosition { get; set; } public int Quantity { get; set; } public double AveragePrice { get; set; } }
    public class Account
    {
        public static readonly System.Collections.ObjectModel.Collection<Account> All = new System.Collections.ObjectModel.Collection<Account>();
        public string Name { get; set; }
        public object Connection { get; set; }
        public Dictionary<AccountItem, double> StubValues = new Dictionary<AccountItem, double>();
        public double Get(AccountItem item, Currency currency) { double v; return StubValues.TryGetValue(item, out v) ? v : 0; }
        public List<Position> Positions = new List<Position>();
        public List<Order> Orders = new List<Order>();
        public Order CreateOrder(Instrument instrument, OrderAction action, OrderType orderType, OrderEntry orderEntry, TimeInForce timeInForce, int quantity, double limitPrice, double stopPrice, string oco, string name, DateTime gtd, CustomOrder customOrder) { return new Order { Instrument = instrument, OrderAction = action, OrderType = orderType, Quantity = quantity, LimitPrice = limitPrice, StopPrice = stopPrice, Oco = oco, Name = name }; }
        public void Submit(IEnumerable<Order> orders) { }
        public void Change(IEnumerable<Order> orders) { }
        public void Cancel(IEnumerable<Order> orders) { }
        public void Flatten(ICollection<Instrument> instruments) { }
        public event EventHandler<OrderEventArgs> OrderUpdate;
        public void RaiseStub() { if (OrderUpdate != null) OrderUpdate(this, null); }
    }
    public enum OrderAction { Buy, BuyToCover, Sell, SellShort }
    public enum OrderType { Limit, Market, MIT, StopLimit, StopMarket, Unknown }
    public enum OrderEntry { Automated, Manual }
    public enum TimeInForce { Day, Gtc, Gtd, Ioc, Opg }
    public enum OrderState { Accepted, CancelPending, CancelSubmitted, Cancelled, ChangePending, ChangeSubmitted, Filled, Initialized, PartFilled, Rejected, Submitted, TriggerPending, Unknown, Working }
    public class CustomOrder { }
    public class Order
    {
        public Instrument Instrument { get; set; } public OrderAction OrderAction { get; set; } public OrderType OrderType { get; set; } public OrderState OrderState { get; set; }
        public int Quantity { get; set; } public int Filled { get; set; } public double AverageFillPrice { get; set; } public double LimitPrice { get; set; } public double StopPrice { get; set; }
        public double LimitPriceChanged { get; set; } public double StopPriceChanged { get; set; } public int QuantityChanged { get; set; } public string Oco { get; set; } public string Name { get; set; } public string OrderId { get; set; }
    }
    public class OrderEventArgs : EventArgs { public Order Order { get; set; } public OrderState OrderState { get; set; } public string Comment { get; set; } }
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
        public event EventHandler<BarsUpdateEventArgs> Update;
        public void RaiseUpdate(int a, int b) { if (Update != null) Update(this, new BarsUpdateEventArgs { MinIndex = a, MaxIndex = b }); }
        public void Dispose() { }
    }
    public class BarsUpdateEventArgs : EventArgs { public int MinIndex { get; set; } public int MaxIndex { get; set; } }
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

namespace NinjaTrader.Core
{
    public static class Globals { public static readonly System.DateTime MaxDate = new System.DateTime(2099, 12, 1); }
}
