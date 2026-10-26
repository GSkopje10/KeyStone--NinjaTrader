// SpasiX — the chart, drawn directly (smooth at any zoom): candles in one of the THEMES, the time axis in the chosen TIME ZONE, the PRICE (its own
// colour, a soft pulse on every new price, the candle's COUNTDOWN), INDICATORS (EMA 9 / 21 / 50 / 200, VWAP, Bollinger, fair-value gaps, NY opening
// range, yesterday's high / low, the sessions, SETUPS = a sweep of a swing high / low that closes back and breaks — with their honest record),
// DRAWINGS (horizontal line / ray, vertical line, trend line / ray, rectangle, Fibonacci, text, long / short position, measure; colours, widths,
// handles, magnet), YOUR TICKETS (buys mint, sells red, pending orders amber; every line lights up and flashes once under the mouse; ✕ closes;
// drag a line = its stop / target / new price; many tickets fold into one label per side), a NEWS banner and a crosshair.
// Mouse: wheel = zoom • drag the chart = move in time / up-down • drag the TIME axis = stretch / squeeze • drag the PRICE axis = stretch the price •
// double-click = back to now • right-click = the menu (the host builds it, knowing what is under the mouse).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KeystoneMT5
{
    public sealed class ChartTheme
    {
        public string Name, Group = "SpasiX"; public Color Bg, Plot, Grid, Up, Down, UpWick, DownWick, Axis, Text, Accent, Price; public bool HollowUp, Glow, Light, Dotted;
        public bool Bodies; public Color UpBody, DownBody, Edge;   // own body / edge colours (MT5 classic: black / white bodies with a lime edge)
        static Color C(string h) { return (Color)ColorConverter.ConvertFromString(h); }
        static ChartTheme T(string name, string group, string bg, string plot, string grid, string up, string down, string axis, string text, string accent, string price, bool light = false) { return new ChartTheme { Name = name, Group = group, Bg = C(bg), Plot = C(plot), Grid = C(grid), Up = C(up), Down = C(down), UpWick = C(up), DownWick = C(down), Axis = C(axis), Text = C(text), Accent = C(accent), Price = C(price), Light = light }; }
        // the trading colours (entries, stops / targets, pending orders): the same on every theme unless the chart settings change them
        public static readonly Color DefBuy = C("#2EE59D"), DefSell = C("#FF4D6D"), DefPend = C("#FFB020");
        public Color Buy = DefBuy, Sell = DefSell, Pend = DefPend; public bool NoGrid; public double WickW = 1, BodyW = 0.7;
        public ChartTheme Copy() { return (ChartTheme)MemberwiseClone(); }
        // saved as text: name=value;… (colours #AARRGGBB, switches 0/1, widths)
        public static readonly string[] ColorKeys = { "Bg", "Plot", "Grid", "UpBody", "DownBody", "UpWick", "DownWick", "Edge", "Axis", "Text", "Price", "Accent", "Buy", "Sell", "Pend" };
        static readonly string[] BoolKeys = { "HollowUp", "Glow", "Light", "Dotted", "NoGrid" };
        public Color Get(string k) { return (Color)typeof(ChartTheme).GetField(k).GetValue(this); }
        public void Set(string k, Color c) { typeof(ChartTheme).GetField(k).SetValue(this, c); if (k == "UpBody") Up = c; if (k == "DownBody") Down = c; }
        // every theme as editable bodies (a theme without its own bodies paints them in Up / Down, without an outline)
        public ChartTheme Editable() { var t = Copy(); if (!t.Bodies) { t.Bodies = true; t.UpBody = t.Up; t.DownBody = t.Down; t.Edge = Colors.Transparent; } return t; }
        public string ToText()
        {
            var l = new List<string> { "Name=" + (Name ?? "").Replace(";", ",").Replace("=", "-") };
            foreach (var k in ColorKeys) l.Add(k + "=" + Get(k).ToString());
            foreach (var k in BoolKeys) l.Add(k + "=" + ((bool)typeof(ChartTheme).GetField(k).GetValue(this) ? "1" : "0"));
            l.Add("WickW=" + WickW.ToString(System.Globalization.CultureInfo.InvariantCulture)); l.Add("BodyW=" + BodyW.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", l);
        }
        public static ChartTheme FromText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null; var t = All[0].Editable(); t.Group = "Mine";
            foreach (var part in s.Split(';'))
            {
                int e = part.IndexOf('='); if (e <= 0) continue; string k = part.Substring(0, e), v = part.Substring(e + 1);
                try
                {
                    if (k == "Name") t.Name = v; else if (ColorKeys.Contains(k)) t.Set(k, C(v)); else if (BoolKeys.Contains(k)) typeof(ChartTheme).GetField(k).SetValue(t, v == "1");
                    else if (k == "WickW") t.WickW = Math.Max(1, Math.Min(4, double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
                    else if (k == "BodyW") t.BodyW = Math.Max(0.3, Math.Min(0.95, double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
                }
                catch { }
            }
            return t;
        }
        static ChartTheme W(ChartTheme t, Action<ChartTheme> a) { a(t); return t; }
        // the looks: each one different (background, candles, shape); themes are saved BY NAME, so the order can change
        public static readonly ChartTheme[] All = {
            // SpasiX
            new ChartTheme { Name = "SpasiX Night", Bg = C("#080C14"), Plot = C("#0B101B"), Grid = C("#151E2E"), Up = C("#4DD8FF"), Down = C("#7A5CFF"), UpWick = C("#4DD8FF"), DownWick = C("#7A5CFF"), Axis = C("#8A94A8"), Text = C("#E8ECF4"), Accent = C("#FFC94D"), Price = C("#FFFFFF") },
            new ChartTheme { Name = "Classic Pro", Bg = C("#0D1117"), Plot = C("#0F141B"), Grid = C("#1B222C"), Up = C("#26A69A"), Down = C("#EF5350"), UpWick = C("#26A69A"), DownWick = C("#EF5350"), Axis = C("#8B949E"), Text = C("#E6EDF3"), Accent = C("#58A6FF"), Price = C("#FFFFFF") },
            new ChartTheme { Name = "Gold Rush", Bg = C("#0E0B06"), Plot = C("#120E07"), Grid = C("#251D10"), Up = C("#FFC94D"), Down = C("#9B5DE5"), UpWick = C("#FFC94D"), DownWick = C("#9B5DE5"), Axis = C("#B8A57A"), Text = C("#FFF3D6"), Accent = C("#FFC94D"), Price = C("#FFFFFF") },
            new ChartTheme { Name = "Hollow Pro", Bg = C("#0A0D12"), Plot = C("#0B0F15"), Grid = C("#181E28"), Up = C("#E8ECF4"), Down = C("#5C6B85"), UpWick = C("#C9D1DE"), DownWick = C("#5C6B85"), Axis = C("#8A94A8"), Text = C("#E8ECF4"), Accent = C("#FFC94D"), Price = C("#FFC94D"), HollowUp = true },
            new ChartTheme { Name = "Ocean Deep", Bg = C("#03121C"), Plot = C("#041824"), Grid = C("#0B2A3B"), Up = C("#2EC4B6"), Down = C("#FF6B6B"), UpWick = C("#2EC4B6"), DownWick = C("#FF6B6B"), Axis = C("#7FA7BC"), Text = C("#E0F2FA"), Accent = C("#90E0EF"), Price = C("#FFFFFF") },
            new ChartTheme { Name = "Paper Light", Bg = C("#F4F6FA"), Plot = C("#FFFFFF"), Grid = C("#E6EAF0"), Up = C("#089981"), Down = C("#F23645"), UpWick = C("#089981"), DownWick = C("#F23645"), Axis = C("#5C6370"), Text = C("#151A22"), Accent = C("#2962FF"), Price = C("#151A22"), Light = true },
            // ♛ YOUR PICKS: built around Midnight Soft, Royal Velvet and Navy & Gold (deep blue / indigo / violet, soft or gold candles)
            T("Midnight Velvet", "♛ Your picks", "#0F1020", "#13142A", "#1E2038", "#6FC3B8", "#C98BB9", "#8F97B0", "#DCE2F0", "#E8C77A", "#FFFFFF"),
            T("Midnight Gold", "♛ Your picks", "#0E1320", "#111827", "#1B2335", "#E3C27A", "#7C93B8", "#8F97B0", "#DCE2F0", "#E3C27A", "#FFFFFF"),
            T("Navy Velvet", "♛ Your picks", "#101A33", "#14203D", "#1F2D4D", "#F2C46D", "#9B7BEA", "#8F97B0", "#DCE2F0", "#F2C46D", "#FFFFFF"),
            T("Indigo Night", "♛ Your picks", "#12122A", "#161634", "#232349", "#7FD1C7", "#E58F9E", "#8F97B0", "#DCE2F0", "#FFC94D", "#FFFFFF"),
            T("Sapphire Soft", "♛ Your picks", "#0C1A2E", "#0F2036", "#1A2E4A", "#8FD3C0", "#E8A0A0", "#8F97B0", "#DCE2F0", "#7FB2FF", "#FFFFFF"),
            T("Royal Navy", "♛ Your picks", "#0B1530", "#0F1A3A", "#1A2850", "#FFD27A", "#E86A8A", "#8F97B0", "#DCE2F0", "#FFD27A", "#FFFFFF"),
            T("Velvet Ice", "♛ Your picks", "#120E24", "#17112D", "#261D44", "#9FE3F0", "#B78CFF", "#8F97B0", "#DCE2F0", "#E8C77A", "#FFFFFF"),
            T("Midnight Ember", "♛ Your picks", "#121620", "#151A26", "#212838", "#7FC8A9", "#E89A6B", "#8F97B0", "#DCE2F0", "#E8C77A", "#FFFFFF"),
            T("Navy Pearl", "♛ Your picks", "#13203A", "#172644", "#24365A", "#F0E6D2", "#6E8FC9", "#8F97B0", "#DCE2F0", "#F2C46D", "#FFFFFF"),
            T("Plum Navy", "♛ Your picks", "#16142C", "#1B1834", "#2A2650", "#E8C77A", "#5FB3D9", "#8F97B0", "#DCE2F0", "#E8C77A", "#FFFFFF"),
            T("Twilight Soft", "♛ Your picks", "#181A2E", "#1C1F36", "#292D4A", "#9AD0C2", "#D7A2C8", "#8F97B0", "#DCE2F0", "#C9B37A", "#FFFFFF"),
            T("Deep Lagoon", "♛ Your picks", "#0B1A24", "#0E202C", "#18303E", "#7FD8C6", "#F0A3A3", "#8F97B0", "#DCE2F0", "#E8C77A", "#FFFFFF"),
            T("Dusk Navy", "♛ Your picks", "#1E2A44", "#24324F", "#33425F", "#F2C46D", "#8FB4FF", "#8F97B0", "#DCE2F0", "#F2C46D", "#FFFFFF"),
            T("Velvet Dusk", "♛ Your picks", "#241D3A", "#2A2244", "#3A3158", "#F0D08A", "#A98BFF", "#8F97B0", "#DCE2F0", "#F0D08A", "#FFFFFF"),
            // MetaTrader 5 itself
            new ChartTheme { Name = "MT5", Group = "MetaTrader", Bg = C("#000000"), Plot = C("#000000"), Grid = C("#2A2A2A"), Up = C("#00A000"), Down = C("#E00000"), UpWick = C("#00A000"), DownWick = C("#E00000"), Axis = C("#FFFFFF"), Text = C("#FFFFFF"), Accent = C("#3A3A3A"), Price = C("#C8C8C8"), Dotted = true },
            new ChartTheme { Name = "MT5 Classic (green on black)", Group = "MetaTrader", Bg = C("#000000"), Plot = C("#000000"), Grid = C("#3A4450"), Up = C("#00FF00"), Down = C("#00FF00"), UpWick = C("#00FF00"), DownWick = C("#00FF00"), Axis = C("#FFFFFF"), Text = C("#FFFFFF"), Accent = C("#3A3A3A"), Price = C("#778899"), Dotted = true, Bodies = true, UpBody = C("#000000"), DownBody = C("#FFFFFF"), Edge = C("#00FF00") },
            new ChartTheme { Name = "MT5 Light (black on white)", Group = "MetaTrader", Bg = C("#FFFFFF"), Plot = C("#FFFFFF"), Grid = C("#C0C0C0"), Up = C("#000000"), Down = C("#000000"), UpWick = C("#000000"), DownWick = C("#000000"), Axis = C("#000000"), Text = C("#000000"), Accent = C("#C0C0C0"), Price = C("#778899"), Light = true, Dotted = true, Bodies = true, UpBody = C("#FFFFFF"), DownBody = C("#000000"), Edge = C("#000000") },
            // Ⓜ MT5 LUXE: the MT5 black chart with the dotted grid, made modern and luxurious (deeper blacks, jewel candles, gold details)
            W(T("Onyx Emerald", "Ⓜ MT5 Luxe", "#000000", "#030504", "#1C2420", "#19C37D", "#E5484D", "#B9C2BD", "#F2F5F3", "#D4AF37", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Black Gold", "Ⓜ MT5 Luxe", "#000000", "#050402", "#2A2416", "#E8B84A", "#6E6A62", "#C9B98F", "#FFF6E0", "#E8B84A", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Black Tie", "Ⓜ MT5 Luxe", "#000000", "#000000", "#2A2A2A", "#C9A54A", "#C9A54A", "#BDBDBD", "#F5F5F5", "#C9A54A", "#FFFFFF"), t => { t.Dotted = true; t.Bodies = true; t.UpBody = C("#000000"); t.DownBody = C("#EDEDED"); t.Edge = C("#C9A54A"); }),
            W(T("Obsidian Ice", "Ⓜ MT5 Luxe", "#000000", "#02050A", "#16202C", "#00E5FF", "#FF3D71", "#A9B8C8", "#EEF6FF", "#00E5FF", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Midnight Bars", "Ⓜ MT5 Luxe", "#01030A", "#020612", "#1A2236", "#00C853", "#FF1744", "#AEB6C8", "#F0F3FA", "#5AA9FF", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Royal Black", "Ⓜ MT5 Luxe", "#000000", "#04020A", "#221A33", "#F2C46D", "#8E6BD8", "#BDB3D6", "#F6F0FF", "#F2C46D", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Carbon Rose", "Ⓜ MT5 Luxe", "#050505", "#080707", "#2A2324", "#F4B59A", "#7D8794", "#C9BDBA", "#FAF2EF", "#F4B59A", "#FFFFFF"), t => { t.Dotted = true; }),
            W(T("Graphite Pro", "Ⓜ MT5 Luxe", "#0B0C0E", "#0E1013", "#262A30", "#2EE59D", "#FF4D6D", "#9AA1AC", "#EEF1F5", "#FFC94D", "#FFFFFF"), t => { t.Dotted = true; }),
            // ✦ NEON: deep dark backgrounds with glowing candles
            new ChartTheme { Name = "Neon Pulse", Group = "✦ Neon", Bg = C("#07040F"), Plot = C("#0B0618"), Grid = C("#1C1233"), Up = C("#00F5D4"), Down = C("#F72585"), UpWick = C("#00F5D4"), DownWick = C("#F72585"), Axis = C("#9D8FC4"), Text = C("#F1EAFF"), Accent = C("#B5179E"), Price = C("#FFFFFF"), Glow = true },
            W(T("Neon Tokyo", "✦ Neon", "#06030C", "#0A0514", "#1E1230", "#00F0FF", "#FF2E97", "#9C8FC4", "#F3EDFF", "#FF2E97", "#FFFFFF"), t => { t.Glow = true; }),
            W(T("Neon Lime", "✦ Neon", "#030804", "#050C07", "#12241A", "#B6FF3B", "#FF3BD4", "#8FB79A", "#EFFFF2", "#B6FF3B", "#FFFFFF"), t => { t.Glow = true; }),
            W(T("Ultraviolet", "✦ Neon", "#07041A", "#0B0624", "#211744", "#FFB000", "#A35BFF", "#A99BD6", "#F4EEFF", "#A35BFF", "#FFFFFF"), t => { t.Glow = true; }),
            W(T("Neon Ice", "✦ Neon", "#02080F", "#030C16", "#0E2233", "#7DF9FF", "#FF7A59", "#8FB2C9", "#EAF8FF", "#7DF9FF", "#FFFFFF"), t => { t.Glow = true; }),
            W(T("Synthwave", "✦ Neon", "#100620", "#160A2C", "#2C1650", "#FFD319", "#FF2975", "#B49BDA", "#FFF0FA", "#FF2975", "#FFFFFF"), t => { t.Glow = true; }),
            W(T("Neon Mint & Gold", "✦ Neon", "#040A0A", "#061010", "#12292A", "#3DFFC5", "#FFC94D", "#8FBDB4", "#ECFFFA", "#3DFFC5", "#FFFFFF"), t => { t.Glow = true; }),
            // ☕ PAPER & INK: sepia / parchment backgrounds, toned down (not white), with ink-coloured candles
            T("Sepia Paper", "☕ Paper & ink", "#F3ECDD", "#F7F1E3", "#E2D8C3", "#4E7D5B", "#B5523B", "#6E6352", "#3A3226", "#A08B62", "#3A3226", true),
            T("Sepia Dusk", "☕ Paper & ink", "#D9CBB0", "#E0D4BC", "#C8B898", "#3E6B4C", "#A8432F", "#5A4B36", "#24190E", "#9A6A1A", "#24190E", true),
            T("Antique Ledger", "☕ Paper & ink", "#D3C4A5", "#DACDB1", "#C0AF8D", "#1D3A6B", "#8E2B2B", "#55472F", "#1E160C", "#8E6410", "#1E160C", true),
            T("Parchment Gold", "☕ Paper & ink", "#CDBD9C", "#D5C6A8", "#BBA984", "#8A5E00", "#2E3A55", "#4E412B", "#1A130A", "#8A5E00", "#1A130A", true),
            T("Tobacco Leaf", "☕ Paper & ink", "#B9A380", "#C2AE8D", "#A58E69", "#2F5A3A", "#8A2E22", "#3F3220", "#160F06", "#6E4A10", "#160F06", true),
            W(T("Linen & Ink", "☕ Paper & ink", "#DED4C0", "#E5DCCB", "#CBBFA7", "#1B1B1B", "#B0302A", "#4A4234", "#141210", "#B0302A", "#141210", true), t => { t.Bodies = true; t.UpBody = C("#E5DCCB"); t.DownBody = C("#1B1B1B"); t.Edge = C("#1B1B1B"); t.UpWick = C("#1B1B1B"); t.DownWick = C("#1B1B1B"); }),
            T("Stone Gold", "☕ Paper & ink", "#C9C4B8", "#D4D0C5", "#B8B2A4", "#9A6A00", "#2F4F7A", "#3E3A32", "#1C1A16", "#9A6A00", "#1C1A16", true),
            T("Twilight Paper", "☕ Paper & ink", "#BDBACB", "#C9C6D6", "#AAA6BB", "#A06E08", "#4A3F9A", "#37344A", "#15122A", "#A06E08", "#15122A", true),
            // ☾ easy on the eyes (soft contrast for long sessions)
            T("Midnight Soft", "♛ Your picks", "#10141C", "#121822", "#1B2230", "#5FB3B3", "#D88A8A", "#7E8A9C", "#C9D2DE", "#6C7A92", "#DDE4EE"),
            T("Nordic Frost", "Easy on the eyes", "#1F2530", "#232A36", "#2C3442", "#88C0D0", "#BF616A", "#8F9BB0", "#D8DEE9", "#5E81AC", "#ECEFF4"),
            T("Solarized Dark", "Easy on the eyes", "#002B36", "#03313D", "#0A3D49", "#2AA198", "#DC322F", "#839496", "#EEE8D5", "#268BD2", "#FDF6E3"),
            T("Warm Dusk", "Easy on the eyes", "#17130F", "#1B1611", "#272019", "#C9A86A", "#B5655B", "#9C8F7E", "#E6DCCB", "#8A6F47", "#F2E8D6"),
            T("Forest Calm", "Easy on the eyes", "#0F1512", "#121A16", "#1B2620", "#7FB77E", "#D98B6A", "#839486", "#D3DED5", "#4F6B57", "#E8F0E8"),
            // ✧ LUXE COLOUR: rich coloured backgrounds
            T("Royal Velvet", "♛ Your picks", "#0E0A1A", "#130E22", "#211934", "#E8C77A", "#8E6BD8", "#A79CC4", "#F1EAFB", "#E8C77A", "#FFFFFF"),
            T("Navy & Gold", "♛ Your picks", "#0F1B33", "#13213D", "#1E2E50", "#FFC94D", "#5AA9FF", "#9DB0D3", "#EDF2FF", "#FFC94D", "#FFFFFF"),
            T("Rose Gold", "✧ Luxe colour", "#2B2326", "#33292D", "#45373C", "#F4C27A", "#E58FA8", "#C9B3B9", "#FAF0F2", "#F4C27A", "#FFFFFF"),
            T("Aurora Gold", "✧ Luxe colour", "#1C2230", "#222A3A", "#2D364A", "#FFC94D", "#8E7CFF", "#A9B3C6", "#EEF1F7", "#FFC94D", "#FFFFFF"),
            T("Champagne Slate", "✧ Luxe colour", "#2A303D", "#313848", "#3C4557", "#F2C46D", "#7FA3D6", "#B3BCCB", "#F2F4F8", "#F2C46D", "#FFFFFF"),
            T("Quantum Blue", "✧ Luxe colour", "#13213A", "#172843", "#233759", "#4DD8FF", "#FF6B8B", "#9DB2D4", "#EAF2FF", "#FFC94D", "#FFFFFF"),
            T("Dracula", "✧ Luxe colour", "#22232E", "#282A36", "#343746", "#50FA7B", "#FF5555", "#9EA3C0", "#F8F8F2", "#BD93F9", "#F8F8F2"),
            T("Amber Terminal", "✧ Luxe colour", "#0B0904", "#0E0B05", "#231B0A", "#FFB000", "#7A5200", "#B38A33", "#FFD27A", "#FFB000", "#FFE3A3") };
    }
    public sealed class Drawing
    {
        public string Kind = "HLINE"; public DateTime T1, T2; public double P1, P2, P3; public string Color = "#FFC94D", Text = "", Fill = "";   // Fill (rectangle / FVG background): "" light, STRONG, NONE or #RRGGBB
        public double Width = 1.6; public bool Dash;
        public Drawing Clone() { return (Drawing)MemberwiseClone(); }
    }
    public sealed class ChartHit { public Rect R; public string Kind, Key; public long Ticket; public Action Click; }
    public sealed class Indicator { public string Key, Name, Color, Tip; public Indicator(string k, string n, string c, string t) { Key = k; Name = n; Color = c; Tip = t; } }

    public sealed partial class ChartView : FrameworkElement
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static SolidColorBrush Br(Color c, byte a = 255) { var x = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); x.Freeze(); return x; }
        static SolidColorBrush Hex(string h, byte a = 255) { return Br((Color)ColorConverter.ConvertFromString(h), a); }
        static Pen P(Brush b, double w, params double[] dash) { if (b != null && !b.IsFrozen) b = b.CloneCurrentValue(); var p = new Pen(b, w);   // a frozen pen freezes its brush: never the live interface brushes (the looks recolour those)
             if (dash.Length > 0) p.DashStyle = new DashStyle(dash, 0); p.Freeze(); return p; }
        static readonly Typeface Face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), Bold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), Mono = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        // the trading colours (the same on every theme, never a candle colour)
        Color BuyC { get { return Theme.Buy; } } Color SellC { get { return Theme.Sell; } } Color PendC { get { return Theme.Pend; } }
        SolidColorBrush bB, sB, pB; Color bC, sC, pC;   // cached until the theme's colour changes
        SolidColorBrush BuyB { get { if (bB == null || bC != BuyC) { bC = BuyC; bB = Br(bC); } return bB; } }
        SolidColorBrush SellB { get { if (sB == null || sC != SellC) { sC = SellC; sB = Br(sC); } return sB; } }
        SolidColorBrush PendB { get { if (pB == null || pC != PendC) { pC = PendC; pB = Br(pC); } return pB; } }
        static readonly SolidColorBrush GoldB = Hex("#FFC94D"), CyanB = Hex("#4DD8FF"), VioletB = Hex("#A98BFF"), WhiteB = Hex("#FFFFFF");
        public static readonly Indicator[] Indicators = {
            new Indicator("EMA9", "EMA 9", "#FFE066", "fast average (9 candles)"), new Indicator("EMA21", "EMA 21", "#4DD8FF", "the 21-candle average"), new Indicator("EMA50", "EMA 50", "#A98BFF", "the 50-candle average"),
            new Indicator("EMA200", "EMA 200", "#FF8A3D", "the trend (200 candles)"), new Indicator("VWAP", "VWAP", "#E879F9", "the day's volume-weighted average price (from the server's midnight)"), new Indicator("BB", "Bollinger 20/2", "#90A4AE", "20-candle average ± 2 deviations"),
            new Indicator("FVG", "Fair-value gaps", "#2EE59D", "3-candle gaps not filled yet"), new Indicator("ORB", "NY opening range", "#A98BFF", "New York 09:30–09:45 high / low"), new Indicator("PDHL", "Yesterday high / low", "#42A5F5", "the previous day's high and low"),
            new Indicator("SESS_SYD", "Session • Sydney (ASX)", "#26C6DA", "ASX 10:00–16:00 Sydney time • pre-open from 07:00"),
            new Indicator("SESS_TKY", "Session • Tokyo (TSE)", "#A98BFF", "TSE 09:00–15:30 Tokyo time (lunch 11:30–12:30) • orders from 08:00 • Japan data 08:50 = 20:00 / 19:50 New York"),
            new Indicator("SESS_CHN", "Session • Shanghai gold (SGE) + Hong Kong", "#FF5C7A", "SGE gold day session 09:00–15:30 Beijing (= 21:00 New York in summer) • Hong Kong / China stocks 09:30 • SGE night session 20:00–02:30"),
            new Indicator("SESS_LON", "Session • London + Frankfurt", "#FFC94D", "London (LSE) 08:00–16:30 London time • Frankfurt / Europe pre-market from 07:00 • LBMA gold auctions 10:30 and 15:00"),
            new Indicator("SESS_NY", "Session • New York", "#2EE59D", "pre-market 04:00 • COMEX gold 08:20 • US data 08:30 • NYSE 09:30–16:00 New York time"),
            new Indicator("SESS_CME", "Session • CME break + Globex reopen", "#90A4AE", "CME Globex closes 17:00 and reopens 18:00 New York time (gold, Nasdaq futures)"),
            new Indicator("VOL", "Volume", "#8FA3C0", "MT5 tick volume per candle, coloured by the estimated buyers / sellers"),
            new Indicator("VPROF", "Volume profile", "#4DD8FF", "volume by price for the visible candles: POC (most traded price), value area 70 % (VAH / VAL)"),
            new Indicator("DELTA", "Order flow (est.) — delta + CVD", "#2EE59D", "buyers − sellers per candle and its running total, estimated from MT5 tick volume and where each candle closed in its range"),
            new Indicator("FIGHT", "Bulls vs bears", "#FF9F43", "the fight of the last 20 candles: who pushes, absorption (big volume, small candle) and aggressive candles marked"),
            new Indicator("GAPS", "Daily + weekly opening gaps", "#FFB020", "the open of a day / week away from the last close: a zone until it is filled, its size in price and points • 📊 statistics in this menu"),
            new Indicator("SETUPS", "SpasiX setups", "#FFC94D", "a sweep of a swing high / low that closes back inside and breaks the sweep candle — arrows, the live setup blinking, its record (2R) on the chart") };
        public static string IndColor(string key) { var i = Indicators.FirstOrDefault(x => x.Key == key); return i != null ? i.Color : "#FFC94D"; }

        // ---- what the host gives the chart
        public BarSeries Series; public int Digits = 2; public double Point = 0.01, Bid, Ask, TickValue = 1, TickSize = 0.01, Lots = 0.1; public string Symbol = "", Currency = "USD", Status = "";
        public List<Mt5Position> Positions = new List<Mt5Position>(); public List<Mt5Order> Orders = new List<Mt5Order>();
        public object Gate = new object(); public ChartTheme Theme = ChartTheme.All[0];
        public Func<DateTime, DateTime> ToView = t => t; public string ZoneName = "SERVER"; public DateTime ServerNow = DateTime.MinValue, QuoteTime = DateTime.MinValue;
        public int NyOffsetHours = -7; public int ServerOffsetSec = 10800;   // server time = UTC + this (MT5's, sent by the bridge)   // NY time = server time + this (IC Markets: server = New York + 7)
        public HashSet<string> Ind = new HashSet<string>(); public int FvgSide;   // FVG: 0 = all, 1 = bullish only, −1 = bearish only
        public List<Drawing> Drawings = new List<Drawing>(); public string Tool = ""; public bool Magnet; public string DrawColor = "#FFC94D", DrawFill = "";
        public string News = ""; public bool NewsHot, TradeOn, Sim, ShowGrid; public double PickPrice = double.NaN; public string PickKind = ""; public string TicketMode = "AUTO"; public double EscapePx = double.NaN, VolStep = 0.01;   // AUTO / EACH / LINES / CLEAN (no lines, only a small ✕ per ticket on the right)
        // ---- what the chart tells the host
        public Action<long, double> CloseTicket;              // ticket, lots (0 = all)
        public Action<long, double, double> ModifyTicket;     // ticket, sl, tp
        public Action<long, double, double, double> ModifyOrder; // ticket, price, sl, tp
        public Action<long> CancelOrder; public Action<int> CloseSide;
        public Action<double> PricePicked;
        public Action<Point, double, DateTime, string, long, Drawing> RightClick;    // where, price, time, what is there (POS / SL / TP / ORD / DRAW / ""), ticket, drawing
        public Action<string> IndOff;   // ✕ / right-click on an indicator of the legend
        public Action DrawingsChanged, ToolDone; public Action<string> Toast; public Func<string, string> AskText;

        double barW = 8, rightOffset = 6, mHi = double.NaN, mLo = double.NaN; bool auto = true;
        Point? mouse; Point dragFrom; double dragOff, dragHi, dragLo, dragBarW; string drag = null;
        readonly List<ChartHit> hits = new List<ChartHit>();
        long dragTicket; string dragWhat = ""; double dragPrice = double.NaN; Drawing dragDraw, newDraw, measure, selected, dragOrig; string dragPart = ""; double dragDP;
        double hi, lo, plotW, plotH; int nBars;
        const double AxisW = 88, TimeH = 26, TopPad = 8;
        // animation: the hovered line flashes once, every new price pulses, new tickets blink, the live setup blinks
        string hoverKey = ""; DateTime hoverAt = DateTime.MinValue, pulseAt = DateTime.MinValue; double lastBid; readonly Dictionary<long, DateTime> seen = new Dictionary<long, DateTime>(); bool firstTickets = true;
        readonly DispatcherTimer anim; bool liveSetup;

        public ChartView()
        {
            ClipToBounds = true; Focusable = true; FocusVisualStyle = null;
            anim = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
            anim.Tick += delegate { if (IsVisible && Animating()) InvalidateVisual(); };
            IsVisibleChanged += delegate { if (IsVisible) anim.Start(); else anim.Stop(); };
        }
        bool Animating()
        {
            var now = DateTime.Now; if ((now - hoverAt).TotalMilliseconds < 700 || (now - pulseAt).TotalMilliseconds < 750 || NewsHot || liveSetup) return true;
            foreach (var kv in seen) if ((now - kv.Value).TotalMilliseconds < 1800) return true; return false;
        }
        public void ResetView() { auto = true; rightOffset = 6; InvalidateVisual(); }
        // switching the timeframe keeps the picture still (user's request): the same time at the right edge and the same time span across the chart
        // (the candles only regroup); applied when the new timeframe's candles are there
        DateTime keepRight = DateTime.MinValue; int keepTf; bool keepOn; double keepMpp, keepBarW = -1;   // the wanted span survives the zoom limits (M1 → H4 → M1 comes back the same)
        public bool ViewSpan(out DateTime right, out double minPerPx)
        {
            right = DateTime.MinValue; minPerPx = 0; if (Series == null || Series.Bars.Count == 0 || barW <= 0) return false;
            right = TimeAt(Series.Bars.Count - 1 + rightOffset); minPerPx = barW == keepBarW && keepMpp > 0 ? keepMpp : Series.Minutes / barW; return true;
        }
        public void KeepView(DateTime right, double minPerPx, int tfMinutes)
        {
            keepRight = right; keepTf = tfMinutes; keepOn = true; barW = Math.Max(1.2, Math.Min(80, tfMinutes / Math.Max(1e-9, minPerPx))); keepMpp = minPerPx; keepBarW = barW; auto = true; InvalidateVisual();
        }
        void ApplyKeep(List<Mt5Bar> bars)
        {
            if (!keepOn || Series == null || Series.Minutes != keepTf || bars.Count == 0) return; keepOn = false; int n = bars.Count;
            int lo = 0, hi = n - 1; if (keepRight < bars[0].Time) { rightOffset = -(n - 5); return; }
            while (lo < hi) { int m = (lo + hi + 1) / 2; if (bars[m].Time <= keepRight) lo = m; else hi = m - 1; }
            double frac = (keepRight - bars[lo].Time).TotalMinutes / Math.Max(1, Series.Minutes);
            rightOffset = Math.Min(plotW > 0 ? plotW / barW * 0.8 : 6, Math.Max(-(n - 5), lo + frac - (n - 1)));
        }
        public void Deselect() { selected = null; measure = null; InvalidateVisual(); }

        FormattedText T(string s, double size, Brush b, bool mono = false, bool bold = false) { return new FormattedText(s, IC, FlowDirection.LeftToRight, mono ? Mono : bold ? Bold : Face, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip); }
        string F(double v) { return v.ToString("F" + Digits, IC); }
        string Money(double v) { return (v >= 0 ? "+" : "−") + (Currency == "USD" ? "$" : "") + Math.Abs(v).ToString("N2", IC) + (Currency == "USD" ? "" : " " + Currency); }
        double Value(double priceMove, double lots) { return TickSize > 0 ? priceMove / TickSize * TickValue * lots : 0; }
        static double Nice(double raw) { if (raw <= 0) return 1; double e = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / e; return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * e; }
        double Y(double p) { return TopPad + (hi - p) / (hi - lo) * plotH; }
        double PriceAt(double y) { return hi - (y - TopPad) / plotH * (hi - lo); }
        double X(double i) { return plotW - (nBars - 1 - i + rightOffset + 0.5) * barW; }
        double IndexAt(double x) { return nBars - 1 + rightOffset + 0.5 - (plotW - x) / barW - 0.5; }
        double IndexOf(DateTime t)
        {
            var b = Series.Bars; int n = b.Count; if (n == 0) return 0; double m = Math.Max(1, Series.Minutes);
            if (t >= b[n - 1].Time) return n - 1 + (t - b[n - 1].Time).TotalMinutes / m; if (t <= b[0].Time) return (t - b[0].Time).TotalMinutes / m;
            int l = 0, h = n - 1; while (l < h) { int mid = (l + h) / 2; if (b[mid].Time >= t) h = mid; else l = mid + 1; }
            return l - Math.Min(1, (b[l].Time - t).TotalMinutes / m);
        }
        DateTime TimeAt(double idx) { var b = Series.Bars; int n = b.Count; if (n == 0) return DateTime.MinValue; int i = (int)Math.Round(Math.Max(0, Math.Min(n - 1, idx))); return b[i].Time.AddMinutes((idx - i) * Series.Minutes); }
        double Snap(Point m, double price)
        {
            if (!Magnet || Series == null) return price; int i = (int)Math.Round(IndexAt(m.X)); if (i < 0 || i >= Series.Bars.Count) return price; var b = Series.Bars[i];
            double best = price, bd = 14; foreach (var v in new[] { b.O, b.H, b.L, b.C }) { double d = Math.Abs(Y(v) - m.Y); if (d < bd) { bd = d; best = v; } } return best;
        }

        protected override void OnRender(DrawingContext dc) { lock (Gate) Draw(dc); }

        void Draw(DrawingContext dc)
        {
            hits.Clear();
            double W = ActualWidth, H = ActualHeight; if (W < 60 || H < 60) return; var th = Theme;
            plotW = W - AxisW; plotH = H - TimeH - TopPad;
            var axisB = Br(th.Axis); var textB = Br(th.Text); var gridP = th.Dotted ? P(Br(th.Grid), 1, 1, 3) : P(Br(th.Grid), 1); var tagBg = Br(th.Plot, 238);
            dc.DrawRectangle(Br(th.Bg), null, new Rect(0, 0, W, H)); dc.DrawRectangle(Br(th.Plot), null, new Rect(0, TopPad, plotW, plotH));
            var bars = Series != null ? Series.Bars : null;
            if (bars == null || bars.Count == 0) { var msg = T(Status != "" ? Status : "⟳", 15, axisB); dc.DrawText(msg, new Point(Math.Max(8, (plotW - msg.Width) / 2), H / 2 - 10)); return; }
            ApplyKeep(bars); int n = bars.Count; nBars = n;
            int i1 = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(n - 1 + rightOffset + 0.5))), i0 = Math.Max(0, Math.Min(i1, (int)Math.Floor(n - 1 + rightOffset + 0.5 - plotW / barW)));
            hi = double.MinValue; lo = double.MaxValue;
            for (int i = i0; i <= i1; i++) { hi = Math.Max(hi, bars[i].H); lo = Math.Min(lo, bars[i].L); }
            if (Bid > 0 && rightOffset > -2) { hi = Math.Max(hi, Bid); lo = Math.Min(lo, Bid); }
            if (hi <= lo) hi = lo + Point * 10; double pad = (hi - lo) * 0.08; hi += pad; lo -= pad;
            if (!auto && !double.IsNaN(mHi)) { hi = mHi; lo = mLo; } else { mHi = hi; mLo = lo; }
            if (Bid > 0 && Bid != lastBid) { if (lastBid > 0) pulseAt = DateTime.Now; lastBid = Bid; }
            // grid, price axis
            double step = Nice((hi - lo) / Math.Max(3, plotH / 70));
            for (double p = Math.Ceiling(lo / step) * step; p <= hi; p += step) { double y = Y(p); if (!th.NoGrid && ShowGrid) dc.DrawLine(gridP, new Point(0, y), new Point(plotW, y)); var t = T(F(p), 11, axisB, true); dc.DrawText(t, new Point(plotW + 7, y - t.Height / 2)); }
            // time axis (in the chosen time zone)
            int every = Math.Max(1, (int)Math.Ceiling(95 / barW)); int tf = Series.Minutes;
            for (int i = i0; i <= i1; i++)
            {
                if (i % every != 0) continue; double x = X(i); if (x < 0 || x > plotW) continue;
                if (!th.NoGrid && ShowGrid) dc.DrawLine(gridP, new Point(x, TopPad), new Point(x, TopPad + plotH));
                var bt = ToView(bars[i].Time); bool newDay = i > 0 && ToView(bars[i - 1].Time).Date != bt.Date;
                var t = T(tf >= 1440 ? bt.ToString("dd MMM", IC) : newDay || tf >= 240 ? bt.ToString("dd MMM HH:mm", IC) : bt.ToString("HH:mm", IC), 11, newDay ? textB : axisB); dc.DrawText(t, new Point(x - t.Width / 2, TopPad + plotH + 5));
            }
            dc.PushClip(new RectangleGeometry(new Rect(0, TopPad, plotW, plotH)));
            if (tf <= 60) DrawMarketSessions(dc, bars, i0, i1);
            if (Ind.Contains("VPROF")) DrawVolumeProfile(dc, bars, i0, i1);
            DrawIndicators(dc, bars, i0, i1);
            // candles
            double bw = Math.Max(1, barW * th.BodyW); var upB = Br(th.Up); var dnB = Br(th.Down); var upW = P(Br(th.UpWick), th.WickW); var dnW = P(Br(th.DownWick), th.WickW); var upHollow = P(upB, 1.2);
            var edgeP = th.Bodies && th.Edge.A > 0 ? P(Br(th.Edge), 1) : null; var upBody = th.Bodies ? Br(th.UpBody) : upB; var dnBody = th.Bodies ? Br(th.DownBody) : dnB;
            for (int i = i0; i <= i1; i++)
            {
                var b = bars[i]; double x = X(i); if (x < -barW || x > plotW + barW) continue; bool up = b.C >= b.O;
                double yo = Y(b.O), yc = Y(b.C), top = Math.Min(yo, yc), hgt = Math.Max(1, Math.Abs(yc - yo));
                if (th.Glow && barW >= 4) dc.DrawRectangle(Br(up ? th.Up : th.Down, 36), null, new Rect(x - bw / 2 - 2, top - 2, bw + 4, hgt + 4));
                dc.DrawLine(up ? upW : dnW, new Point(x, Y(b.H)), new Point(x, top)); dc.DrawLine(up ? upW : dnW, new Point(x, top + hgt), new Point(x, Y(b.L)));
                if (up && th.HollowUp && bw >= 3) dc.DrawRectangle(Br(th.Plot), upHollow, new Rect(x - bw / 2, top, bw, hgt));
                else if (th.Bodies && bw >= 3) dc.DrawRectangle(up ? upBody : dnBody, edgeP, new Rect(x - bw / 2, top, bw, hgt));
                else dc.DrawRectangle(up ? upB : dnB, null, new Rect(x - bw / 2, top, bw, hgt));
            }
            if (Ind.Contains("GAPS")) DrawGaps(dc, bars, i0, i1);
            if (Ind.Contains("VOL") || Ind.Contains("DELTA")) DrawFlow(dc, bars, i0, i1);
            if (Ind.Contains("FIGHT")) DrawFight(dc, bars, i0, i1);
            liveSetup = false; if (Ind.Contains("SETUPS")) DrawSetups(dc, bars, i0, i1);
            DrawDrawings(dc);
            dc.Pop();
            DrawTrading(dc, W);
            if (!double.IsNaN(EscapePx)) Level(dc, Y(EscapePx), Br(Color.FromRgb(0xF2, 0xC4, 0x6D)), "⛑ ESCAPE @ " + F(EscapePx) + " — every " + Symbol + " position closes if the price comes back here", true, 8, null);
            DrawIfClosed(dc);
            DrawPrice(dc, bars, th, textB, tagBg, tf);
            // the picked price for a limit / stop order
            if (!double.IsNaN(PickPrice)) Level(dc, Y(PickPrice), PendB, "🎯 " + PickKind + " @ " + F(PickPrice) + " — press PLACE in the panel", true, 8, null);
            else if (PickKind != "" && mouse.HasValue && mouse.Value.X < plotW) Level(dc, mouse.Value.Y, PendB, "🎯 click = " + PickKind + " @ " + F(Math.Round(PriceAt(mouse.Value.Y) / Point) * Point), true, 8, null);
            // NEWS banner
            if (News != "")
            {
                bool fl = NewsHot && DateTime.Now.Millisecond < 500; var nb = NewsHot ? SellB : PendB; var nt = T("📰 " + News, 12.5, fl ? Br(th.Bg) : textB, false, true);
                double nx = Math.Max(6, (plotW - nt.Width) / 2 - 10); dc.DrawRoundedRectangle(fl ? nb : tagBg, P(nb, 1.4), new Rect(nx, TopPad + 6, nt.Width + 20, 24), 12, 12); dc.DrawText(nt, new Point(nx + 10, TopPad + 9));
            }
            if (measure != null) DrawMeasure(dc, measure);
            // crosshair (only inside the chart) + the candle's numbers + what the mouse points at
            if (mouse.HasValue && (drag == null || drag == "NEWDRAW") && mouse.Value.X < plotW && mouse.Value.Y > TopPad && mouse.Value.Y < TopPad + plotH)
            {
                var m = mouse.Value; var cp = P(Br(th.Axis, 140), 1, 3, 3); dc.DrawLine(cp, new Point(0, m.Y), new Point(plotW, m.Y)); dc.DrawLine(cp, new Point(m.X, TopPad), new Point(m.X, TopPad + plotH));
                dc.DrawRoundedRectangle(axisB, null, new Rect(plotW + 2, m.Y - 9, AxisW - 4, 18), 3, 3); dc.DrawText(T(F(PriceAt(m.Y)), 11, Br(th.Bg), true), new Point(plotW + 7, m.Y - 8));
                int ci = (int)Math.Round(IndexAt(m.X));
                if (ci >= 0 && ci < n)
                {
                    var cb = bars[ci]; var tt = T(ToView(cb.Time).ToString(tf >= 1440 ? "ddd dd MMM yyyy" : "ddd dd MMM HH:mm", IC) + " " + ZoneName, 11, Br(th.Bg));
                    dc.DrawRoundedRectangle(axisB, null, new Rect(m.X - tt.Width / 2 - 5, TopPad + plotH + 3, tt.Width + 10, 18), 3, 3); dc.DrawText(tt, new Point(m.X - tt.Width / 2, TopPad + plotH + 4));
                    bool up = cb.C >= cb.O; var ib = up ? upB : dnB;
                    var info = T("O " + F(cb.O) + "   H " + F(cb.H) + "   L " + F(cb.L) + "   C " + F(cb.C) + "   " + (up ? "+" : "") + F(cb.C - cb.O) + "   vol " + cb.V.ToString("N0", IC), 12, textB, true);
                    double iy = TopPad + (News != "" ? 36 : 6); dc.DrawRoundedRectangle(tagBg, P(Br(up ? th.Up : th.Down, 120), 1), new Rect(8, iy, info.Width + 16, 22), 6, 6); dc.DrawText(info, new Point(16, iy + 3));
                }
                HoverCard(dc, m, textB, tagBg);
            }
            DrawIndLegend(dc, tagBg);
            if (Tool != "FVG") fvgFirst = -1;
            if (Tool == "FVG" && Series != null && Series.Bars.Count > 0)
            {
                // a small numbered badge over the candle: 1 = the candle under the mouse (first click), then 3; the gap is previewed only when it exists
                var fb = Series.Bars; Action<int, string, Color> badge = (ix, txt, c) => { if (ix < 0 || ix >= fb.Count) return; double bx = X(ix), by = Y(fb[ix].H) - 22; var ft = T(txt, 11, Br(th.Bg), false, true); var r = new Rect(bx - 9, by, 18, 18); dc.DrawEllipse(Br(c), null, new Point(bx, by + 9), 9, 9); dc.DrawText(ft, new Point(bx - ft.Width / 2, by + 9 - ft.Height / 2)); };
                int hov = mouse.HasValue && mouse.Value.X < plotW ? (int)Math.Round(IndexAt(mouse.Value.X)) : -1; if (hov >= fb.Count) hov = fb.Count - 1;
                if (fvgFirst >= 0 && fvgFirst < fb.Count)
                {
                    badge(fvgFirst, "1", th.Accent);
                    if (hov >= 0 && hov != fvgFirst)
                    {
                        int ia = Math.Min(fvgFirst, hov), ib = Math.Max(fvgFirst, hov); var g = ib - ia >= 2 ? FvgBetween(ia, ib) : null;
                        badge(hov, "3", g != null ? (g.P3 > 0 ? BuyC : SellC) : th.Axis);
                        if (g != null) { double gt = Y(Math.Max(g.P1, g.P2)), gb = Y(Math.Min(g.P1, g.P2)); var gc = g.P3 > 0 ? BuyC : SellC; dc.DrawRectangle(Br(gc, 40), P(Br(gc, 200), 1, 4, 3), new Rect(X(ia), gt, Math.Max(2, plotW - X(ia)), Math.Max(1, gb - gt))); }
                    }
                }
                else if (hov >= 0) badge(hov, "1", th.Accent);
            }
            if (Tool != "") { var tt = T("✏ " + ToolName(Tool) + " — " + (Tool == "FVG" ? (fvgFirst < 0 ? "click candle 1" : "click candle 3 — drawn only if there is a gap (green bullish / red bearish)") : OneClick(Tool) ? "click on the chart" : "press and drag on the chart") + (Magnet ? " • 🧲 magnet on" : "") + " • Esc = cancel", 11.5, GoldB, false, true); dc.DrawRoundedRectangle(tagBg, P(GoldB, 1), new Rect(8, TopPad + plotH - 28, tt.Width + 16, 22), 6, 6); dc.DrawText(tt, new Point(16, TopPad + plotH - 25)); }
            else if (!auto) dc.DrawText(T("PRICE SCALE: MANUAL • double-click = automatic", 10.5, axisB), new Point(8, TopPad + plotH - 18));
            if (Sim) { var wm = T("REPLAY", 11, Br(th.Accent, 180), false, true); dc.DrawText(wm, new Point(plotW - wm.Width - 10, TopPad + plotH - 18)); }
            dc.DrawRectangle(null, P(Br(th.Accent, 90), 1.2), new Rect(0.5, TopPad + 0.5, plotW - 1, plotH - 1));
        }
        static bool OneClick(string t) { return t == "HLINE" || t == "VLINE" || t == "HRAY" || t == "TEXT"; }
        public static string ToolName(string t)
        {
            switch (t) { case "HLINE": return "HORIZONTAL LINE"; case "HRAY": return "HORIZONTAL RAY"; case "VLINE": return "VERTICAL LINE"; case "TREND": return "TREND LINE"; case "RAY": return "TREND RAY"; case "RECT": return "RECTANGLE"; case "FVG": return "FAIR-VALUE GAP"; case "FIB": return "FIBONACCI"; case "TEXT": return "TEXT"; case "LONG": return "LONG POSITION (risk / reward)"; case "SHORT": return "SHORT POSITION (risk / reward)"; case "MEASURE": return "MEASURE"; }
            return t;
        }

        // ---- the price: its own colour, the ask faint, a pulse on every new price, the countdown (or CLOSED)
        void DrawPrice(DrawingContext dc, List<Mt5Bar> bars, ChartTheme th, Brush textB, Brush tagBg, int tf)
        {
            if (Bid <= 0) return; var pc = Br(th.Price); var last = bars[bars.Count - 1];
            if (Ask > 0) { double ya = Y(Ask); if (ya > TopPad && ya < TopPad + plotH) { dc.DrawLine(P(Br(th.Price, 70), 1, 2, 3), new Point(0, ya), new Point(plotW, ya)); var at = T("ASK " + F(Ask), 9.5, Br(th.Axis), true); dc.DrawText(at, new Point(plotW + 7, Math.Max(TopPad, ya - 21))); } }
            double yb = Y(Bid); if (yb > TopPad && yb < TopPad + plotH) dc.DrawLine(P(Br(th.Price, 170), 1, 6, 3), new Point(0, yb), new Point(plotW, yb));
            double ty = Math.Max(TopPad, Math.Min(TopPad + plotH - 36, yb - 10)); var tag = new Rect(plotW + 2, ty, AxisW - 4, 20);
            double k = (DateTime.Now - pulseAt).TotalMilliseconds / 700.0;
            if (k >= 0 && k < 1) { var rr = tag; rr.Inflate(2 + 7 * k, 2 + 5 * k); dc.DrawRoundedRectangle(null, P(Br(th.Price, (byte)(150 * (1 - k))), 1.6), rr, 6, 6); }
            dc.DrawRoundedRectangle(pc, null, tag, 4, 4); dc.DrawText(T(F(Bid), 12, Br(th.Bg), true), new Point(plotW + 7, ty + 2));
            if (ServerNow > DateTime.MinValue)
            {
                bool closed = !Sim && QuoteTime > DateTime.MinValue && (ServerNow - QuoteTime).TotalMinutes > 3;
                var left = Tf.Left(ServerNow, tf);   // from the clock, not the last candle (a candle not received yet left it at 00:00)
                string cd = closed ? "CLOSED" : left.TotalDays >= 1 ? ((int)left.TotalDays) + "d " + left.Hours + "h " + left.Minutes.ToString("00") + "m" : left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
                dc.DrawRoundedRectangle(tagBg, P(pc, 1), new Rect(plotW + 2, ty + 20, AxisW - 4, 16), 3, 3); dc.DrawText(T("⏱ " + cd, 10.5, textB, true), new Point(plotW + 7, ty + 21));
            }
        }

        // ---- indicators (from the candles on screen and before them)
        void DrawIndicators(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            int n = bars.Count;
            foreach (var e in new[] { Tuple.Create("EMA9", 9), Tuple.Create("EMA21", 21), Tuple.Create("EMA50", 50), Tuple.Create("EMA200", 200) })
            {
                if (!Ind.Contains(e.Item1) || n < 3) continue; double k = 2.0 / (e.Item2 + 1), v = bars[0].C; var g = new StreamGeometry();
                using (var c = g.Open()) { bool started = false; for (int i = 0; i <= i1 && i < n; i++) { v = i == 0 ? bars[0].C : bars[i].C * k + v * (1 - k); if (i < i0 - 1) continue; var pt = new Point(X(i), Y(v)); if (!started) { c.BeginFigure(pt, false, false); started = true; } else c.LineTo(pt, true, false); } }
                g.Freeze(); dc.DrawGeometry(null, P(Hex(IndColor(e.Item1)), e.Item1 == "EMA200" ? 2.2 : 1.6), g);
            }
            if (Ind.Contains("BB") && n > 21)
            {
                var up = new StreamGeometry(); var dn = new StreamGeometry(); var mid = new StreamGeometry(); var col = Hex(IndColor("BB"));
                using (var cu = up.Open()) using (var cd = dn.Open()) using (var cm = mid.Open())
                {
                    bool st = false;
                    for (int i = Math.Max(19, i0 - 1); i <= i1; i++)
                    {
                        double s = 0, s2 = 0; for (int j = i - 19; j <= i; j++) { s += bars[j].C; s2 += bars[j].C * bars[j].C; } double m = s / 20, sd = Math.Sqrt(Math.Max(0, s2 / 20 - m * m));
                        var a = new Point(X(i), Y(m + 2 * sd)); var b = new Point(X(i), Y(m - 2 * sd)); var c0 = new Point(X(i), Y(m));
                        if (!st) { cu.BeginFigure(a, false, false); cd.BeginFigure(b, false, false); cm.BeginFigure(c0, false, false); st = true; } else { cu.LineTo(a, true, false); cd.LineTo(b, true, false); cm.LineTo(c0, true, false); }
                    }
                }
                up.Freeze(); dn.Freeze(); mid.Freeze(); dc.DrawGeometry(null, P(col, 1.2), up); dc.DrawGeometry(null, P(col, 1.2), dn); dc.DrawGeometry(null, P(Hex(IndColor("BB"), 140), 1, 4, 3), mid);
            }
            if (Ind.Contains("VWAP"))
            {
                // the day's VWAP from the server's midnight (the forex day: New York 17:00)
                var g = new StreamGeometry(); double pv = 0, vv = 0; DateTime day = DateTime.MinValue;
                using (var c = g.Open()) { bool started = false; for (int i = 0; i <= i1 && i < n; i++) { var b = bars[i]; if (b.Time.Date != day) { day = b.Time.Date; pv = vv = 0; if (i >= i0) started = false; } double tp = (b.H + b.L + b.C) / 3, w = Math.Max(1, b.V); pv += tp * w; vv += w; if (i < i0) continue; var pt = new Point(X(i), Y(pv / vv)); if (!started) { c.BeginFigure(pt, false, false); started = true; } else c.LineTo(pt, true, false); } }
                g.Freeze(); dc.DrawGeometry(null, P(Hex(IndColor("VWAP")), 2.2), g);
            }
            if (Ind.Contains("FVG"))
            {
                // fair-value gaps (3 candles whose wicks do not overlap) that price has not filled yet — the last 12 of each side
                var gaps = new List<Tuple<int, double, double, int>>();
                for (int i = Math.Max(2, i0 - 300); i <= i1; i++)
                {
                    if (bars[i].L > bars[i - 2].H) gaps.Add(Tuple.Create(i - 1, bars[i - 2].H, bars[i].L, 1)); else if (bars[i].H < bars[i - 2].L) gaps.Add(Tuple.Create(i - 1, bars[i].H, bars[i - 2].L, -1));
                }
                int nb = 0, ns = 0;
                foreach (var gp in gaps.AsEnumerable().Reverse())
                {
                    int end = n - 1; for (int j = gp.Item1 + 2; j < n; j++) if (gp.Item4 > 0 ? bars[j].L <= gp.Item2 : bars[j].H >= gp.Item3) { end = j; break; }
                    if (end < n - 1) continue; if (FvgSide > 0 && gp.Item4 < 0 || FvgSide < 0 && gp.Item4 > 0) continue; if (gp.Item4 > 0 && ++nb > 12) continue; if (gp.Item4 < 0 && ++ns > 12) continue;
                    var cc = gp.Item4 > 0 ? BuyC : SellC; double x1 = X(gp.Item1), y1 = Y(gp.Item3), y2 = Y(gp.Item2);
                    dc.DrawRectangle(Br(cc, 30), P(Br(cc, 110), 0.8), new Rect(x1, Math.Min(y1, y2), Math.Max(2, plotW - x1), Math.Max(1, Math.Abs(y2 - y1))));
                    dc.DrawText(T("FVG", 9, Br(cc, 200), false, true), new Point(x1 + 3, Math.Min(y1, y2) + 1));
                }
            }
            if (Ind.Contains("ORB") && Series.Minutes < 60)
            {
                // the New York opening range 09:30–09:45 of the sessions on screen (server = New York + 7 h → 16:30–16:45)
                var days = bars.Skip(i0).Take(i1 - i0 + 1).Select(b => b.Time.Date).Distinct().ToList();
                foreach (var d in days)
                {
                    var s0 = d.AddHours(9.5 - NyOffsetHours); var s1 = s0.AddMinutes(15); var inR = bars.Where(b => b.Time >= s0 && b.Time < s1).ToList(); if (inR.Count == 0) continue;
                    double h = inR.Max(b => b.H), l = inR.Min(b => b.L); double x1 = X(IndexOf(s0)), x2 = X(IndexOf(d.AddHours(16 - NyOffsetHours)));
                    dc.DrawRectangle(Hex(IndColor("ORB"), 24), P(Hex(IndColor("ORB"), 170), 1, 4, 3), new Rect(x1, Y(h), Math.Max(2, x2 - x1), Math.Max(1, Y(l) - Y(h))));
                    dc.DrawText(T("NY ORB " + F(h) + " / " + F(l), 10, VioletB, false, true), new Point(x1 + 3, Y(h) - 14));
                }
            }
            if (Ind.Contains("PDHL") && Series.Minutes < 1440)
            {
                var lastDay = bars[n - 1].Time.Date; int k0 = n - 1; while (k0 > 0 && bars[k0].Time.Date == lastDay) k0--;
                if (k0 >= 0 && bars[k0].Time.Date < lastDay)
                {
                    var pd = bars[k0].Time.Date; double h = double.MinValue, l = double.MaxValue; for (int j = k0; j >= 0 && bars[j].Time.Date == pd; j--) { h = Math.Max(h, bars[j].H); l = Math.Min(l, bars[j].L); }
                    Level(dc, Y(h), Hex("#42A5F5"), "PDH " + F(h), true, plotW - 150, null); Level(dc, Y(l), Hex("#AB47BC"), "PDL " + F(l), true, plotW - 150, null);
                }
            }
        }
        // the sessions (NY time): a box over each session's high / low
        // ---- SETUPS: a sweep of a swing high (low) that closes back below (above) it, confirmed when a later close breaks the sweep candle
        sealed class Setup { public int Sweep, Signal, Dir, End; public double Entry, Stop, Target; public int Result; }   // Result 1 = 2R first, −1 = stop first, 0 = open
        List<Setup> setups = new List<Setup>(); string setupKey = "";
        List<Setup> FindSetups(List<Mt5Bar> b)
        {
            int n = b.Count - 1;   // the forming candle is not used
            string key = n + "|" + (n > 0 ? b[n - 1].Time.Ticks + "|" + b[n - 1].C : "") + "|" + (n > 0 ? b[0].Time.Ticks : 0);
            if (key == setupKey) { UpdateResults(b); return setups; }
            setupKey = key; var res = new List<Setup>(); const int L = 3;
            var highs = new List<int>(); var lows = new List<int>(); var usedH = new HashSet<int>(); var usedL = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int c = i - L; if (c >= L)
                {
                    bool sh = true, sl = true; for (int k = 1; k <= L; k++) { if (b[c].H <= b[c - k].H || b[c].H < b[c + k].H) sh = false; if (b[c].L >= b[c - k].L || b[c].L > b[c + k].L) sl = false; }
                    if (sh) highs.Add(c); if (sl) lows.Add(c);
                }
                // a sweep on candle i of the newest swing in the last 60 candles that is still untouched
                int hs = highs.LastOrDefault(h => h < i - L + 1 && h >= i - 60 && !usedH.Contains(h)); if (hs == 0) hs = -1;   // a swing is never candle 0
                if (hs >= 0 && b[i].H > b[hs].H)
                {
                    usedH.Add(hs);
                    if (b[i].C < b[hs].H && b[i].C < b[i].O)
                        for (int j = i + 1; j <= Math.Min(n - 1, i + 3); j++) if (b[j].C < b[i].L) { double st = Math.Max(b[i].H, b.Skip(i).Take(j - i + 1).Max(x => x.H)) + Point * 5; res.Add(new Setup { Sweep = i, Signal = j, Dir = -1, Entry = b[j].C, Stop = st, Target = b[j].C - 2 * (st - b[j].C) }); break; }
                }
                int ls = lows.LastOrDefault(h => h < i - L + 1 && h >= i - 60 && !usedL.Contains(h)); if (ls == 0) ls = -1;
                if (ls >= 0 && b[i].L < b[ls].L)
                {
                    usedL.Add(ls);
                    if (b[i].C > b[ls].L && b[i].C > b[i].O)
                        for (int j = i + 1; j <= Math.Min(n - 1, i + 3); j++) if (b[j].C > b[i].H) { double st = Math.Min(b[i].L, b.Skip(i).Take(j - i + 1).Min(x => x.L)) - Point * 5; res.Add(new Setup { Sweep = i, Signal = j, Dir = 1, Entry = b[j].C, Stop = st, Target = b[j].C + 2 * (b[j].C - st) }); break; }
                }
            }
            setups = res.GroupBy(s => s.Signal).Select(g => g.First()).ToList(); UpdateResults(b); return setups;
        }
        void UpdateResults(List<Mt5Bar> b)
        {
            foreach (var s in setups)
            {
                s.Result = 0; s.End = b.Count - 1;
                for (int k = s.Signal + 1; k < b.Count; k++)
                {
                    bool stop = s.Dir > 0 ? b[k].L <= s.Stop : b[k].H >= s.Stop, tgt = s.Dir > 0 ? b[k].H >= s.Target : b[k].L <= s.Target;
                    if (stop) { s.Result = -1; s.End = k; break; } if (tgt) { s.Result = 1; s.End = k; break; }
                }
            }
        }
        void DrawSetups(DrawingContext dc, List<Mt5Bar> bars, int i0, int i1)
        {
            var list = FindSetups(bars); var gold = Hex(IndColor("SETUPS")); bool blink = DateTime.Now.Millisecond < 500;
            foreach (var s in list)
            {
                if (s.Signal < i0 - 2 || s.Signal > i1 + 1) continue; var b = bars[s.Signal]; double x = X(s.Signal); var col = s.Dir > 0 ? BuyB : SellB;
                bool live = s.Result == 0 && s.Signal >= bars.Count - 25; if (live) liveSetup = true;
                double y = s.Dir > 0 ? Y(b.L) + 14 : Y(b.H) - 14; double sz = Math.Max(6, Math.Min(11, barW * 0.9));
                var tri = new StreamGeometry(); using (var c = tri.Open()) { if (s.Dir > 0) { c.BeginFigure(new Point(x, y - sz), true, true); c.LineTo(new Point(x - sz, y + sz * 0.6), true, false); c.LineTo(new Point(x + sz, y + sz * 0.6), true, false); } else { c.BeginFigure(new Point(x, y + sz), true, true); c.LineTo(new Point(x - sz, y - sz * 0.6), true, false); c.LineTo(new Point(x + sz, y - sz * 0.6), true, false); } } tri.Freeze();
                if (live && blink) { dc.DrawEllipse(Br(col.Color, 60), null, new Point(x, y), sz * 2.2, sz * 2.2); }
                dc.DrawGeometry(live ? (blink ? WhiteB : col) : Br(col.Color, s.Result == 0 ? (byte)220 : (byte)150), P(Br(Theme.Plot), 1), tri);
                if (s.Result != 0) { var mk = T(s.Result > 0 ? "✓" : "✕", 10, s.Result > 0 ? BuyB : SellB, false, true); dc.DrawText(mk, new Point(x - mk.Width / 2, s.Dir > 0 ? y + sz : y - sz - mk.Height)); }
                if (live || (selected == null && mouse.HasValue && Math.Abs(mouse.Value.X - x) < barW * 1.5))
                {
                    double x2 = X(Math.Max(s.End, s.Signal + 12)); var pe = P(gold, 1.2, 4, 3);
                    dc.DrawRectangle(Br(BuyC, 22), null, new Rect(x, Math.Min(Y(s.Entry), Y(s.Dir > 0 ? s.Target : s.Entry)), Math.Max(4, x2 - x), Math.Abs(Y(s.Target) - Y(s.Entry))));
                    dc.DrawRectangle(Br(SellC, 22), null, new Rect(x, Math.Min(Y(s.Entry), Y(s.Stop)), Math.Max(4, x2 - x), Math.Abs(Y(s.Stop) - Y(s.Entry))));
                    dc.DrawLine(pe, new Point(x, Y(s.Entry)), new Point(x2, Y(s.Entry)));
                    var lt = T((live ? "⚡ LIVE SETUP " : "SETUP ") + (s.Dir > 0 ? "BUY" : "SELL") + " • entry " + F(s.Entry) + " • stop " + F(s.Stop) + " • target 2R " + F(s.Target), 10.5, live && blink ? WhiteB : gold, false, true);
                    double ly = s.Dir > 0 ? Y(s.Target) - 16 : Y(s.Target) + 2; dc.DrawRoundedRectangle(Br(Theme.Plot, 220), null, new Rect(x - 2, ly - 1, lt.Width + 8, lt.Height + 2), 4, 4); dc.DrawText(lt, new Point(x + 2, ly));
                }
            }
            // the record of every setup on the loaded candles (honest: stop first when both are in one candle)
            int w = list.Count(s => s.Result > 0), l = list.Count(s => s.Result < 0), open = list.Count(s => s.Result == 0);
            string rec = "⚡ SETUPS (sweep → close back → break) • " + list.Count + " on " + bars.Count + " candles" + (w + l > 0 ? " • 2R first: " + w + " / " + (w + l) + " (" + (100.0 * w / (w + l)).ToString("0", IC) + " %) • expectancy " + ((2.0 * w - l) / (w + l)).ToString("+0.00;−0.00", IC) + " R" : "") + (open > 0 ? " • " + open + " open" : "");
            var rt = T(rec, 10.5, gold, false, true); double ry = TopPad + plotH - (Tool != "" ? 54 : 26) - (auto ? 0 : 16);
            dc.DrawRoundedRectangle(Br(Theme.Plot, 225), P(Br(gold.Color, 90), 1), new Rect(8, ry, rt.Width + 14, 20), 6, 6); dc.DrawText(rt, new Point(15, ry + 2));
        }

        // ---- drawings
        static readonly double[] FibLevels = { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1, 1.272, 1.618 };
        void DrawDrawings(DrawingContext dc)
        {
            var all = Drawings.ToList(); if (newDraw != null) all.Add(newDraw);
            for (int di = 0; di < all.Count; di++)
            {
                var d = all[di]; var col = Hex(d.Color); bool hov = hoverKey == "DRAW:" + Drawings.IndexOf(d) && d != selected;
                double wdt = d.Width + (d == selected || hov ? 0.9 : 0); var pen = d.Dash ? P(col, wdt, 5, 4) : P(col, wdt);
                double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2);
                if (hov && d.Kind != "TEXT") { var glow = P(Hex(d.Color, 50), d.Width + 6); DrawShape(dc, d, glow, null, x1, y1, x2, y2); }
                DrawShape(dc, d, pen, col, x1, y1, x2, y2);
                if (d == selected) foreach (var pt in Handles(d)) dc.DrawEllipse(Br(Theme.Plot), P(col, 2), pt, 5, 5);
            }
        }
        Brush FillOf(Drawing d) { var f = d.Fill ?? ""; if (f == "NONE") return null; if (f == "STRONG") return Hex(d.Color, 80); if (f.StartsWith("#")) { try { return Hex(f, 70); } catch { } } return Hex(d.Color, 30); }
        // the FVG tool: the clicked candle as the middle of a 3-candle gap (or its neighbours): bullish = the 3rd low above the 1st high, bearish = the 3rd high below the 1st low
        int fvgFirst = -1;
        Drawing FvgBetween(int ia, int ib)
        {
            var bars = Series.Bars; var a = bars[ia]; var c = bars[ib]; string tf = Tf.Name(Series.Minutes);
            if (c.L > a.H) return new Drawing { Kind = "FVG", T1 = a.Time, T2 = c.Time, P1 = c.L, P2 = a.H, P3 = 1, Color = "#2EE59D", Width = 1, Text = tf };
            if (c.H < a.L) return new Drawing { Kind = "FVG", T1 = a.Time, T2 = c.Time, P1 = a.L, P2 = c.H, P3 = -1, Color = "#FF4D6D", Width = 1, Text = tf };
            return null;
        }
        Drawing FvgAt(int k)
        {
            var bars = Series.Bars;
            foreach (int m in new[] { k, k - 1, k + 1 })
            {
                if (m < 1 || m + 1 >= bars.Count) continue; var a = bars[m - 1]; var c = bars[m + 1];
                if (c.L > a.H) return new Drawing { Kind = "FVG", T1 = a.Time, T2 = c.Time, P1 = c.L, P2 = a.H, P3 = 1, Color = "#" + BuyC.ToString().Substring(3), Width = 1 };
                if (c.H < a.L) return new Drawing { Kind = "FVG", T1 = a.Time, T2 = c.Time, P1 = a.L, P2 = c.H, P3 = -1, Color = "#" + SellC.ToString().Substring(3), Width = 1 };
            }
            return null;
        }
        void DrawShape(DrawingContext dc, Drawing d, Pen pen, SolidColorBrush col, double x1, double y1, double x2, double y2)
        {
            switch (d.Kind)
            {
                case "HLINE": dc.DrawLine(pen, new Point(0, y1), new Point(plotW, y1)); if (col != null) { var t = T(F(d.P1) + (d.Text != "" ? "  " + d.Text : ""), 10.5, col, true); dc.DrawText(t, new Point(plotW - t.Width - 8, y1 - 15)); } break;
                case "HRAY": dc.DrawLine(pen, new Point(x1, y1), new Point(plotW, y1)); if (col != null) { dc.DrawEllipse(col, null, new Point(x1, y1), 2.5, 2.5); var t = T(F(d.P1), 10.5, col, true); dc.DrawText(t, new Point(plotW - t.Width - 8, y1 - 15)); } break;
                case "VLINE": dc.DrawLine(pen, new Point(x1, TopPad), new Point(x1, TopPad + plotH)); break;
                case "TREND": dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2)); break;
                case "RAY":
                    {
                        double dx = x2 - x1, dy = y2 - y1; if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) { dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2)); break; }
                        double kx = Math.Abs(dx) > 1e-6 ? ((dx > 0 ? plotW + 50 : -50) - x1) / dx : 1e6, ky = Math.Abs(dy) > 1e-6 ? ((dy > 0 ? TopPad + plotH + 50 : TopPad - 50) - y1) / dy : 1e6; double k = Math.Max(1, Math.Min(kx, ky));
                        dc.DrawLine(pen, new Point(x1, y1), new Point(x1 + dx * k, y1 + dy * k)); break;
                    }
                case "RECT": dc.DrawRectangle(col != null ? FillOf(d) : null, pen, new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1))); break;
                case "FVG":
                    {
                        // from its first candle to the candle that fills it (traded through to the far side), else to the right edge; the 50 % line dashed
                        double top = Math.Max(d.P1, d.P2), bot = Math.Min(d.P1, d.P2); bool bull = d.P3 > 0; int i1 = (int)Math.Round(IndexOf(d.T1)); var bars = Series != null ? Series.Bars : null; double xe = plotW; bool filled = false;
                        if (bars != null) for (int i = Math.Max(0, i1 + 3); i < bars.Count; i++) if (bull ? bars[i].L <= bot : bars[i].H >= top) { xe = X(i); filled = true; break; }
                        double yt = Y(top), yb = Y(bot), xl = x1; var r = new Rect(Math.Min(xl, xe), yt, Math.Max(1, Math.Abs(xe - xl)), Math.Max(1, yb - yt));
                        dc.DrawRectangle(col != null ? FillOf(d) : null, pen, r);
                        if (col != null) { double ym = Y((top + bot) / 2); dc.DrawLine(P(Hex(d.Color, 150), 1, 4, 3), new Point(r.Left, ym), new Point(r.Right, ym)); var t = T((bull ? "▲ BULLISH FVG" : "▼ BEARISH FVG") + (d.Text != "" ? " • " + d.Text : "") + " • " + Math.Abs((top - bot) / Point).ToString("N0", IC) + " pts" + (filled ? " • filled" : ""), 10, col, true); if (r.Width > t.Width + 8) dc.DrawText(t, new Point(r.Left + 4, yt - t.Height - 1)); }
                        break;
                    }
                case "FIB":
                    {
                        double left = Math.Min(x1, x2);
                        foreach (var lv in FibLevels)
                        {
                            double p = d.P2 + (d.P1 - d.P2) * lv, y = Y(p); bool key = lv == 0.5 || lv == 0.618 || lv == 0 || lv == 1;
                            dc.DrawLine(key ? pen : P(Hex(d.Color, 150), 1), new Point(left, y), new Point(plotW, y));
                            if (col != null) dc.DrawText(T(lv.ToString("0.###", IC) + "  " + F(p), 10, Hex(d.Color, key ? (byte)255 : (byte)180), true), new Point(left + 4, y - 13));
                        }
                        if (col != null) dc.DrawLine(P(Hex(d.Color, 90), 1, 3, 3), new Point(x1, y1), new Point(x2, y2));
                        break;
                    }
                case "TEXT":
                    {
                        var t = T(d.Text == "" ? "text" : d.Text, 12.5 + d.Width, col ?? GoldB, false, true);
                        if (col != null) dc.DrawRoundedRectangle(Br(Theme.Plot, 200), P(Hex(d.Color, 120), 1), new Rect(x1 - 4, y1 - t.Height / 2 - 2, t.Width + 8, t.Height + 4), 4, 4);
                        dc.DrawText(t, new Point(x1, y1 - t.Height / 2)); break;
                    }
                case "LONG":
                case "SHORT":
                    {
                        int dir = d.Kind == "LONG" ? 1 : -1; double xr = Math.Max(x2, x1 + 30), yt = Y(d.P3);
                        if (col != null)
                        {
                            dc.DrawRectangle(Br(BuyC, 40), P(Br(BuyC, 160), 1), new Rect(x1, Math.Min(y1, yt), xr - x1, Math.Abs(yt - y1)));
                            dc.DrawRectangle(Br(SellC, 40), P(Br(SellC, 160), 1), new Rect(x1, Math.Min(y1, y2), xr - x1, Math.Abs(y2 - y1)));
                            double risk = Math.Abs(d.P1 - d.P2), rew = Math.Abs(d.P3 - d.P1), rr = risk > 0 ? rew / risk : 0;
                            var tt = T("TARGET " + F(d.P3) + "  " + Money(Value(rew, Lots)) + "  (" + rr.ToString("0.0#", IC) + " R)", 10.5, BuyB, false, true); dc.DrawText(tt, new Point(x1 + 4, dir > 0 ? yt - 15 : yt + 2));
                            var st = T("STOP " + F(d.P2) + "  " + Money(-Value(risk, Lots)) + "  • " + Lots.ToString("0.00", IC) + " lots", 10.5, SellB, false, true); dc.DrawText(st, new Point(x1 + 4, dir > 0 ? y2 + 2 : y2 - 15));
                            var et = T((dir > 0 ? "▲ LONG " : "▼ SHORT ") + F(d.P1), 10.5, Br(Theme.Text), false, true); dc.DrawText(et, new Point(xr - et.Width - 4, y1 - (dir > 0 ? -2 : 15)));
                        }
                        dc.DrawLine(pen, new Point(x1, y1), new Point(xr, y1)); break;
                    }
            }
        }
        List<Point> Handles(Drawing d)
        {
            double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2);
            switch (d.Kind)
            {
                case "HLINE": return new List<Point> { new Point(plotW / 2, y1) };
                case "VLINE": return new List<Point> { new Point(x1, TopPad + plotH / 2) };
                case "HRAY": case "TEXT": return new List<Point> { new Point(x1, y1) };
                case "LONG": case "SHORT": { double xr = Math.Max(x2, x1 + 30); return new List<Point> { new Point(x1, y1), new Point(x1, y2), new Point(x1, Y(d.P3)), new Point(xr, y1) }; }
                case "RECT": case "FIB": case "MEASURE": { double xm = (x1 + x2) / 2, ym = (y1 + y2) / 2; return new List<Point> { new Point(x1, y1), new Point(x2, y2), new Point(x1, y2), new Point(x2, y1), new Point(xm, y1), new Point(xm, y2), new Point(x1, ym), new Point(x2, ym) }; }   // 4 corners + 4 middles
                case "TREND": case "RAY": return new List<Point> { new Point(x1, y1), new Point(x2, y2), new Point((x1 + x2) / 2, (y1 + y2) / 2) };   // the middle moves the whole line
            }
            return new List<Point> { new Point(x1, y1), new Point(x2, y2) };
        }
        // which drawing, which part ("P1" / "P2" / "P3" / "W" = the width of a position box / "BODY")
        // the indicators on this chart, top left under the candle box: a chip each (its colour, its name, ✕) — ✕ or a right-click removes it
        void DrawIndLegend(DrawingContext dc, Brush tagBg)
        {
            if (IndOff == null) return;
            foreach (var it in LegendLayout())
            {
                var ind = it.Item1; var r = it.Item2; var col = Hex(ind.Color); var nm = T(ind.Name, 10.5, Br(Theme.Text), true); bool hov = mouse.HasValue && r.Contains(mouse.Value);
                dc.DrawRoundedRectangle(hov ? Br(col.Color, 60) : tagBg, P(Br(col.Color, (byte)(hov ? 230 : 120)), 1), r, 6, 6);
                dc.DrawRoundedRectangle(col, null, new Rect(r.X + 6, r.Y + 8, 9, 3), 1.5, 1.5); dc.DrawText(nm, new Point(r.X + 19, r.Y + 2));
                var xr = new Rect(r.Right - 17, r.Y + 2, 15, 15); bool hx = mouse.HasValue && xr.Contains(mouse.Value);
                dc.DrawRoundedRectangle(hx ? Br(Hex("#FF4D6D").Color, 230) : Br(Hex("#FF4D6D").Color, 70), null, xr, 4, 4); dc.DrawText(T("✕", 9.5, Br(Theme.Text), true), new Point(xr.X + 3.5, xr.Y + 1));
                var key = ind.Key; hits.Add(new ChartHit { R = r, Kind = "IND", Key = "IND:" + key, Click = () => { if (IndOff != null) IndOff(key); } });
            }
        }
        // where the chips go: left, under the candle box, wrapping before the bulls-vs-bears box; LegendH = how tall (the session labels go under it)
        double LegendH { get { var l = LegendLayout(); return l.Count == 0 ? 0 : l.Max(x => x.Item2.Bottom) - (TopPad + (News != "" ? 36 : 6) + 28) + 4; } }
        List<Tuple<Indicator, Rect>> LegendLayout()
        {
            var res = new List<Tuple<Indicator, Rect>>(); if (Ind.Count == 0 || IndOff == null) return res;
            double maxX = Ind.Contains("FIGHT") ? plotW - 290 - (Ind.Contains("VPROF") ? plotW * 0.16 + 8 : 10) - 10 : plotW * 0.7; maxX = Math.Max(260, maxX);
            double x = 8, y0 = TopPad + (News != "" ? 36 : 6) + 28, y = y0;
            foreach (var ind in Indicators.Where(i => Ind.Contains(i.Key)))
            {
                double w = T(ind.Name, 10.5, Br(Theme.Text), true).Width + 40; if (x + w > maxX && x > 8) { x = 8; y += 22; }
                res.Add(Tuple.Create(ind, new Rect(x, y, w, 19))); x += w + 6;
            }
            return res;
        }
        // what drawing is under the mouse: the SELECTED one first (its handles, then its body), then any handle, then the thin lines and the edges,
        // then the inside of a zone — so a click on a trend line's end is never taken by the rectangle under it
        Drawing DrawingAt(Point m, out string part)
        {
            part = "";
            if (selected != null && Drawings.Contains(selected)) { var hp = HandleAt(selected, m); if (hp != null) { part = hp; return selected; } if (BodyHit(selected, m, false) || BodyHit(selected, m, true)) { part = "BODY"; return selected; } }
            for (int k = Drawings.Count - 1; k >= 0; k--) { var hp = HandleAt(Drawings[k], m); if (hp != null) { part = hp; return Drawings[k]; } }
            foreach (var area in new[] { false, true })
                for (int k = Drawings.Count - 1; k >= 0; k--) if (BodyHit(Drawings[k], m, area)) { part = "BODY"; return Drawings[k]; }
            return null;
        }
        string HandleAt(Drawing d, Point m)
        {
            if (d.Kind == "HLINE" || d.Kind == "VLINE" || d.Kind == "HRAY" || d.Kind == "TEXT" || d.Kind == "FVG") return null;
            var hs = Handles(d); string[] names = d.Kind == "LONG" || d.Kind == "SHORT" ? new[] { "P1", "P2", "P3", "W" } : d.Kind == "RECT" || d.Kind == "FIB" || d.Kind == "MEASURE" ? new[] { "P1", "P2", "X1Y2", "X2Y1", "Y1", "Y2", "X1", "X2" } : new[] { "P1", "P2", "BODY" };
            for (int h = 0; h < hs.Count; h++) if ((hs[h] - m).Length <= 8) return names[Math.Min(h, names.Length - 1)];
            return null;
        }
        // area = false: the line itself / a zone's edge (within 5 px); area = true: inside a zone
        bool BodyHit(Drawing d, Point m, bool area)
        {
            double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2);
            double l = Math.Min(x1, x2), r = Math.Max(x1, x2), t = Math.Min(y1, y2), b = Math.Max(y1, y2);
            switch (d.Kind)
            {
                case "HLINE": return !area && Math.Abs(m.Y - y1) <= 5;
                case "HRAY": return !area && Math.Abs(m.Y - y1) <= 5 && m.X >= x1 - 5;
                case "VLINE": return !area && Math.Abs(m.X - x1) <= 5;
                case "RECT":
                    {
                        bool inside = m.X >= l - 4 && m.X <= r + 4 && m.Y >= t - 4 && m.Y <= b + 4; if (!inside) return false;
                        bool edge = Math.Abs(m.X - l) <= 5 || Math.Abs(m.X - r) <= 5 || Math.Abs(m.Y - t) <= 5 || Math.Abs(m.Y - b) <= 5; return area ? !edge : edge;
                    }
                case "FIB": return area && m.X >= l - 4 && m.Y >= t - 4 && m.Y <= b + 4;
                case "FVG": return area && m.X >= l - 4 && m.Y >= t - 4 && m.Y <= b + 4;
                case "TEXT": { if (area) return false; var tx = T(d.Text == "" ? "text" : d.Text, 12.5 + d.Width, GoldB, false, true); return m.X >= x1 - 4 && m.X <= x1 + tx.Width + 4 && Math.Abs(m.Y - y1) <= tx.Height / 2 + 3; }
                case "LONG": case "SHORT": { if (!area) return false; double xr = Math.Max(x2, x1 + 30), yt = Y(d.P3); return m.X >= x1 && m.X <= xr && m.Y >= Math.Min(Math.Min(y1, y2), yt) && m.Y <= Math.Max(Math.Max(y1, y2), yt); }
                case "TREND": case "RAY":
                    {
                        if (area) return false; double ex = x2, ey = y2; if (d.Kind == "RAY") { ex = x1 + (x2 - x1) * 50; ey = y1 + (y2 - y1) * 50; }
                        double len2 = (ex - x1) * (ex - x1) + (ey - y1) * (ey - y1); double u = len2 < 1 ? 0 : Math.Max(0, Math.Min(1, ((m.X - x1) * (ex - x1) + (m.Y - y1) * (ey - y1)) / len2)); double px = x1 + u * (ex - x1), py = y1 + u * (ey - y1); return Math.Abs(m.X - px) + Math.Abs(m.Y - py) <= 7;
                    }
            }
            return false;
        }
        public bool HasSelected { get { return selected != null; } }
        public void DeleteSelected() { if (selected != null) { Drawings.Remove(selected); selected = null; if (DrawingsChanged != null) DrawingsChanged(); InvalidateVisual(); } }
        void DrawMeasure(DrawingContext dc, Drawing d)
        {
            double x1 = X(IndexOf(d.T1)), x2 = X(IndexOf(d.T2)), y1 = Y(d.P1), y2 = Y(d.P2); bool up = d.P2 >= d.P1; var col = up ? BuyB : SellB;
            dc.DrawRectangle(Br(col.Color, 36), P(col, 1, 4, 3), new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)));
            double dp = d.P2 - d.P1; int nb = (int)Math.Round(Math.Abs(IndexOf(d.T2) - IndexOf(d.T1))); var span = d.T2 - d.T1;
            var t = T((dp >= 0 ? "+" : "−") + F(Math.Abs(dp)) + "  (" + (d.P1 != 0 ? 100 * dp / d.P1 : 0).ToString("+0.00;−0.00", IC) + " %)  •  " + Math.Abs(dp / Point).ToString("N0", IC) + " points  •  " + nb + " candles  •  " + (Math.Abs(span.TotalHours) >= 1 ? ((int)Math.Abs(span.TotalHours)) + " h " + Math.Abs(span.Minutes) + " min" : Math.Abs(span.TotalMinutes).ToString("0", IC) + " min") + "  •  " + Money(Value(Math.Abs(dp), Lots) * (dp >= 0 ? 1 : -1)) + " for " + Lots.ToString("0.00", IC) + " lots", 11.5, Br(Theme.Text), false, true);
            double tx = Math.Min(plotW - t.Width - 14, Math.Max(4, (x1 + x2) / 2 - t.Width / 2)), ty = up ? Math.Min(y1, y2) - 26 : Math.Max(y1, y2) + 4;
            dc.DrawRoundedRectangle(Br(col.Color, 220), null, new Rect(tx, ty, t.Width + 12, 22), 6, 6); dc.DrawText(T(t.Text, 11.5, Br(Theme.Bg), false, true), new Point(tx + 6, ty + 3));
        }

        // ---- your tickets and orders, like MT5: a dashed line per ticket (buys mint, sells red), stops red, targets mint, pending orders amber with their
        // own stop / target; a slim label on the LEFT ("SELL 0.10 at 3996.56   +$25.90  ✕"); every line drags (a ticket → its stop / target, an order →
        // its price with its bracket, a stop / target → its price) and shows the $ it means while you drag
        sealed class TagRow { public double Y, Price; public string Text, Money, Kind, Key; public Color Col; public long Ticket; public int Dir; public bool Faint; }
        void DrawTrading(DrawingContext dc, double W)
        {
            var mine = Positions.Where(p => string.Equals(p.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            var ords = Orders.Where(o => string.Equals(o.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            var now = DateTime.Now;
            foreach (var p in mine) if (!seen.ContainsKey(p.Ticket)) seen[p.Ticket] = firstTickets ? DateTime.MinValue : now;
            foreach (var o in ords) if (!seen.ContainsKey(o.Ticket)) seen[o.Ticket] = firstTickets ? DateTime.MinValue : now;
            if (mine.Count > 0 || ords.Count > 0 || Series != null) firstTickets = false;
            var tags = new List<TagRow>();
            foreach (var side in new[] { 1, -1 })
            {
                var ps = mine.Where(p => p.Dir == side).ToList(); bool quiet = TicketMode == "LINES"; var col = side > 0 ? BuyC : SellC;
                // (the Σ line of all the tickets on a side is gone: it was confusing next to the tickets, user's request)
                foreach (var p in ps)
                {
                    bool dragged = drag == "TICKET" && dragTicket == p.Ticket;
                    double sl = dragged && dragWhat == "SL" ? dragPrice : p.Sl, tp = dragged && dragWhat == "TP" ? dragPrice : p.Tp;
                    if (dragged && dragWhat == "NEW") { if (StopSide(p.Dir, dragPrice)) sl = dragPrice; else tp = dragPrice; }
                    double pl = p.Profit + p.Swap;
                    tags.Add(new TagRow { Y = Y(p.Open), Price = p.Open, Text = (p.Dir > 0 ? "BUY " : "SELL ") + p.Lots.ToString("0.00", IC) + " at " + F(p.Open), Money = Money(pl), Kind = "POS", Key = "POS:" + p.Ticket, Col = col, Ticket = p.Ticket, Dir = p.Dir, Faint = quiet });
                    if (sl > 0) tags.Add(new TagRow { Y = Y(sl), Price = sl, Text = "S/L " + F(sl), Money = Money(Value((sl - p.Open) * p.Dir, p.Lots)), Kind = "SL", Key = "SL:" + p.Ticket, Col = SellC, Ticket = p.Ticket, Dir = p.Dir, Faint = quiet && !dragged });
                    if (tp > 0) tags.Add(new TagRow { Y = Y(tp), Price = tp, Text = "T/P " + F(tp), Money = Money(Value((tp - p.Open) * p.Dir, p.Lots)), Kind = "TP", Key = "TP:" + p.Ticket, Col = BuyC, Ticket = p.Ticket, Dir = p.Dir, Faint = quiet && !dragged });
                }
            }
            foreach (var o in ords)
            {
                bool dr = drag == "ORDER" && dragTicket == o.Ticket; double d = dr && dragWhat == "PX" ? dragPrice - o.Price : 0;
                double px = o.Price + d, osl = dr && dragWhat == "SL" ? dragPrice : o.Sl > 0 ? o.Sl + d : 0, otp = dr && dragWhat == "TP" ? dragPrice : o.Tp > 0 ? o.Tp + d : 0; int dir = o.Type.StartsWith("BUY") ? 1 : -1;
                tags.Add(new TagRow { Y = Y(px), Price = px, Text = o.Type + " " + o.Lots.ToString("0.00", IC) + " at " + F(px), Money = "", Kind = "ORD", Key = "ORD:" + o.Ticket, Col = PendC, Ticket = o.Ticket, Dir = dir });
                if (osl > 0) tags.Add(new TagRow { Y = Y(osl), Price = osl, Text = "S/L " + F(osl), Money = Money(Value((osl - px) * dir, o.Lots)), Kind = "OSL", Key = "OSL:" + o.Ticket, Col = SellC, Ticket = o.Ticket, Dir = dir });
                if (otp > 0) tags.Add(new TagRow { Y = Y(otp), Price = otp, Text = "T/P " + F(otp), Money = Money(Value((otp - px) * dir, o.Lots)), Kind = "OTP", Key = "OTP:" + o.Ticket, Col = BuyC, Ticket = o.Ticket, Dir = dir });
            }
            bool clean = TicketMode == "CLEAN";
            // the lines: dashed like MT5; the hovered / dragged one glows and gets thicker
            foreach (var t in tags)
            {
                if (t.Y < TopPad || t.Y > TopPad + plotH) continue; bool hov = HoverIs(t) || ((drag == "TICKET" || drag == "ORDER") && dragTicket == t.Ticket && t.Key.StartsWith(dragWhat == "NEW" ? "POS" : dragWhat == "PX" ? "ORD" : (t.Kind.StartsWith("O") && t.Kind != "ORD" ? "O" : "") + dragWhat));
                if (clean && !hov) continue;   // CLEAN: no lines (the one you point at / drag still shows)
                if (hov) dc.DrawLine(P(Br(t.Col, 50), 7), new Point(0, t.Y), new Point(plotW, t.Y));
                byte a = t.Faint ? (byte)90 : (byte)200; var pen = t.Kind == "POS" ? P(Br(t.Col, hov ? (byte)255 : a), hov ? 1.9 : 1.3, 8, 3) : P(Br(t.Col, hov ? (byte)255 : a), hov ? 1.8 : 1, 2, 3);
                dc.DrawLine(pen, new Point(0, t.Y), new Point(plotW, t.Y));
            }
            // CLEAN: only a small ✕ on the right for every ticket / order (point at it = its card and its line, click = close / cancel at once like the label's ✕)
            if (clean)
            {
                double ly = double.MinValue;
                foreach (var t in tags.Where(t => (t.Kind == "POS" || t.Kind == "ORD") && t.Y >= TopPad - 30 && t.Y <= TopPad + plotH + 30).OrderBy(t => t.Y))
                {
                    double y = Math.Max(t.Y, ly + 17); ly = y; if (y > TopPad + plotH - 6) continue;
                    var xr = new Rect(plotW - 22, y - 7.5, 16, 15); bool xh = hoverKey == "X:" + t.Key;
                    if (Math.Abs(y - t.Y) > 2) dc.DrawLine(P(Br(t.Col, 120), 1), new Point(xr.X, y), new Point(xr.X - 8, t.Y));
                    dc.DrawRoundedRectangle(xh ? Br(SellC) : Br(t.Kind == "POS" ? t.Col : PendC, 170), xh ? P(WhiteB, 1) : null, xr, 4, 4); dc.DrawText(T("✕", 9.5, WhiteB, true, true), new Point(xr.X + 3.4, xr.Y + 0.6));
                    long tk = t.Ticket; string kind = t.Kind;
                    hits.Add(new ChartHit { R = xr, Kind = "X", Key = "X:" + t.Key, Ticket = tk, Click = () => { if (kind == "POS" && CloseTicket != null) CloseTicket(tk, 0); else if (kind == "ORD" && CancelOrder != null) CancelOrder(tk); } });
                    // the drag handle: a small bordered box with the price; only here the mouse becomes a hand and drags (a ticket → its stop / target, an order → its price)
                    var hf = T("⇕ " + F(t.Price), 9.5, Br(t.Col), true); bool hh = hoverKey == t.Key; var hr = new Rect(xr.X - hf.Width - 14, y - 7.5, hf.Width + 10, 15);
                    dc.DrawRoundedRectangle(hh ? Br(t.Col, 70) : Br(Theme.Plot, 230), P(Br(t.Col, hh ? (byte)255 : (byte)170), 1), hr, 4, 4); dc.DrawText(hf, new Point(hr.X + 5, y - 6.6));
                    hits.Add(new ChartHit { R = hr, Kind = kind, Key = t.Key, Ticket = tk });
                    if (HoverIs(t) && kind == "POS") PartButtons(dc, t, hr.X - 3, y);
                }
                return;
            }
            // the labels on the LEFT, stacked without overlap (with many tickets on a side: lines only — point at one for its card)
            var vis = tags.Where(t => !t.Faint && t.Y >= TopPad - 30 && t.Y <= TopPad + plotH + 30).OrderBy(t => t.Y).ToList(); double lastY = double.MinValue;
            foreach (var t in vis)
            {
                double y = Math.Max(t.Y, lastY + 17); lastY = y; if (y > TopPad + plotH - 6) continue;
                bool hov = HoverIs(t); DateTime born; double flash = seen.TryGetValue(t.Ticket, out born) ? (now - born).TotalMilliseconds : 99999;
                double hk = (now - hoverAt).TotalMilliseconds; bool blink = (flash < 1800 && ((int)(flash / 300)) % 2 == 0) || (hov && hk < 450 && ((int)(hk / 150)) % 2 == 0);
                var ft = T(t.Text, 10.5, blink ? Br(Theme.Bg) : hov ? WhiteB : Br(t.Col), true);
                FormattedText mt = t.Money != "" ? T(t.Money, 10.5, blink ? Br(Theme.Bg) : t.Money.StartsWith("+") ? BuyB : t.Money.StartsWith("−") ? SellB : Br(Theme.Text), true) : null;
                double w = ft.Width + (mt != null ? mt.Width + 12 : 0) + 32, x = plotW - w - 6;   // on the RIGHT, next to the prices: the candles stay free
                if (Math.Abs(y - t.Y) > 2) dc.DrawLine(P(Br(t.Col, 120), 1), new Point(x, y), new Point(x - 10, t.Y));
                var r = new Rect(x, y - 8.5, w, 17);
                dc.DrawRoundedRectangle(blink ? Br(t.Col, 220) : hov ? Br(t.Col, 70) : Br(Theme.Plot, 215), hov || blink ? P(Br(t.Col), 1.2) : null, r, 4, 4);
                dc.DrawText(ft, new Point(x + 5, y - 7)); if (mt != null) dc.DrawText(mt, new Point(x + 5 + ft.Width + 12, y - 7));
                var xr = new Rect(x + w - 21, y - 8.5, 17, 17); bool xh = hoverKey == "X:" + t.Key; bool posX = t.Kind == "POS";
                dc.DrawRoundedRectangle(xh ? Br(SellC) : posX ? Br(SellC, 190) : Br(t.Col, 70), xh ? P(WhiteB, 1) : null, xr, 5, 5); dc.DrawText(T("✕", 10.5, WhiteB, true, true), new Point(xr.X + 3.6, xr.Y + 0.8));
                long tk = t.Ticket; string kind = t.Kind;
                var pos = mine.FirstOrDefault(p => p.Ticket == tk); var ord = ords.FirstOrDefault(o => o.Ticket == tk);
                hits.Add(new ChartHit { R = xr, Kind = "X", Key = "X:" + t.Key, Ticket = tk, Click = () =>
                {
                    if (kind == "SIDE" && CloseSide != null) CloseSide((int)tk);
                    else if (kind == "POS" && CloseTicket != null) CloseTicket(tk, 0);
                    else if (kind == "SL" && pos != null && ModifyTicket != null) ModifyTicket(tk, 0, pos.Tp);
                    else if (kind == "TP" && pos != null && ModifyTicket != null) ModifyTicket(tk, pos.Sl, 0);
                    else if (kind == "ORD" && CancelOrder != null) CancelOrder(tk);
                    else if (kind == "OSL" && ord != null && ModifyOrder != null) ModifyOrder(tk, ord.Price, 0, ord.Tp);
                    else if (kind == "OTP" && ord != null && ModifyOrder != null) ModifyOrder(tk, ord.Price, ord.Sl, 0);
                } });
                hits.Add(new ChartHit { R = new Rect(x, y - 8.5, w - 23, 17), Kind = kind, Key = t.Key, Ticket = tk });
                if (hov && kind == "POS") PartButtons(dc, t, x - 4, y);
            }
        }
        // ✂ CLOSE PART of a ticket at once (one click, like its ✕): 50 % / 75 % / 90 % of its lots (rounded to the lot step; at least one step stays open)
        void PartButtons(DrawingContext dc, TagRow t, double right, double y)
        {
            if (t.Kind != "POS" || !TradeOn) return; var pos = Positions.FirstOrDefault(q => q.Ticket == t.Ticket); if (pos == null) return; double step = VolStep > 0 ? VolStep : 0.01;
            double x = right;
            foreach (var pc in new[] { 90, 75, 50 })
            {
                double lots = Math.Floor(pos.Lots * pc / 100.0 / step + 1e-9) * step; if (lots < step || lots >= pos.Lots - step / 2) continue;
                var ft = T(pc + "%", 10, WhiteB, true, true); var r = new Rect(x - ft.Width - 14, y - 8.5, ft.Width + 10, 17); x = r.X - 3; string key = "PC" + pc + ":" + t.Key; bool h = hoverKey == key;
                double gain = (pos.Profit + pos.Swap) * lots / pos.Lots;
                dc.DrawRoundedRectangle(h ? Br(gain >= 0 ? BuyC : SellC) : Br(gain >= 0 ? BuyC : SellC, 150), h ? P(WhiteB, 1) : null, r, 4, 4); dc.DrawText(ft, new Point(r.X + 5, y - 7));
                long tk = t.Ticket; double l0 = Math.Round(lots, 6); hits.Add(new ChartHit { R = r, Kind = "X", Key = key, Ticket = tk, Click = () => { if (CloseTicket != null) CloseTicket(tk, l0); } });
            }
        }
        // IF ALL CLOSED NOW: what this instrument and the whole account would bank this second (hedges included), live in the chart's corner
        void DrawIfClosed(DrawingContext dc)
        {
            if (Positions == null || Positions.Count == 0) return;
            double mine = 0; int nm = 0; double all = 0; foreach (var q in Positions) { double v = q.Profit + q.Swap; all += v; if (string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)) { mine += v; nm++; } }
            var t1 = T("IF ALL CLOSED NOW", 9.5, Br(Theme.Axis), false, true); var t2 = T((nm > 0 ? Symbol + " " + Money(mine) + "   •   " : "") + "account " + Money(all), 13, all >= 0 ? BuyB : SellB, true, true);
            double w = Math.Max(t1.Width, t2.Width) + 20, x = plotW - w - 8, y = TopPad + 6; var col = all >= 0 ? BuyC : SellC;
            dc.DrawRoundedRectangle(Br(Theme.Plot, 235), P(Br(col, 200), 1.4), new Rect(x, y, w, 38), 8, 8); dc.DrawText(t1, new Point(x + 10, y + 4)); dc.DrawText(t2, new Point(x + 10, y + 17));
        }
        bool HoverIs(TagRow t) { return hoverKey == t.Key || hoverKey == "X:" + t.Key || (hoverKey.StartsWith("PC") && hoverKey.EndsWith(":" + t.Key)); }
        // a card next to the mouse with what it points at
        void HoverCard(DrawingContext dc, Point m, Brush textB, Brush tagBg)
        {
            if (hoverKey == "" || drag != null) return; string[] k = hoverKey.Replace("X:", "").Split(':'); if (k.Length < 2) return; long tk; long.TryParse(k[1], out tk);
            string txt = null; Color col = PendC;
            if (k[0] == "POS" || k[0] == "SL" || k[0] == "TP")
            {
                var p = Positions.FirstOrDefault(x => x.Ticket == tk); if (p == null) return; col = p.Dir > 0 ? BuyC : SellC;
                txt = "#" + p.Ticket + "  " + p.Side + " " + p.Lots.ToString("0.00", IC) + " @ " + F(p.Open) + "\nnow " + F(p.Current) + "   P/L " + Money(p.Profit) + (p.Swap != 0 ? "   swap " + Money(p.Swap) : "") + "\nopened " + ToView(p.Time).ToString("ddd dd MMM HH:mm", IC) + " " + ZoneName
                    + (p.Sl > 0 ? "\nS/L " + F(p.Sl) + " = " + Money(Value((p.Sl - p.Open) * p.Dir, p.Lots)) : "") + (p.Tp > 0 ? "\nT/P " + F(p.Tp) + " = " + Money(Value((p.Tp - p.Open) * p.Dir, p.Lots)) : "")
                    + (TradeOn ? "\n" + (hoverKey.StartsWith("X:") ? (k[0] == "POS" ? "✕ = close this ticket" : "✕ = remove the " + (k[0] == "SL" ? "stop" : "target")) : k[0] == "POS" ? "drag the line = set its stop / target • right-click = more" : "drag = move the " + (k[0] == "SL" ? "stop" : "target")) : "\n🔒 ARM in the panel to change it from the chart");
            }
            else if (k[0] == "ORD" || k[0] == "OSL" || k[0] == "OTP") { var o = Orders.FirstOrDefault(x => x.Ticket == tk); if (o == null) return; txt = "#" + o.Ticket + "  " + o.Type + " " + o.Lots.ToString("0.00", IC) + " @ " + F(o.Price) + (o.Sl > 0 ? "\nS/L " + F(o.Sl) : "") + (o.Tp > 0 ? "\nT/P " + F(o.Tp) : "") + (TradeOn ? (k[0] == "ORD" ? "\ndrag = move the order (its stop / target move with it) • ✕ = cancel" : "\ndrag = move its " + (k[0] == "OSL" ? "stop" : "target") + " • ✕ = remove it") : "\n🔒 ARM to change it"); }
            else if (k[0] == "SIDE") { int dir = (int)tk; var ps = Positions.Where(p => p.Dir == dir && string.Equals(p.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).ToList(); col = dir > 0 ? BuyC : SellC; txt = ps.Count + (dir > 0 ? " buys" : " sells") + " • best " + Money(ps.Count > 0 ? ps.Max(p => p.Profit + p.Swap) : 0) + " • worst " + Money(ps.Count > 0 ? ps.Min(p => p.Profit + p.Swap) : 0) + (TradeOn ? "\n✕ = close all " + (dir > 0 ? "buys" : "sells") + " (asks first)" : ""); }
            if (k[0] == "POS" && txt != null)
            {
                var p0 = Positions.FirstOrDefault(x => x.Ticket == tk);
                if (p0 != null) { double rest = Positions.Where(x => string.Equals(x.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Profit + x.Swap) - (p0.Profit + p0.Swap); txt += "\nclose it: banks " + Money(p0.Profit + p0.Swap) + " • the rest of " + Symbol + " shows " + Money(rest) + (TradeOn ? "\n✂ 50% / 75% / 90% next to it = close that part at once" : ""); }
            }
            if (k[0].StartsWith("PC") && k.Length >= 3)
            {
                int pc; int.TryParse(k[0].Substring(2), out pc); long t2; long.TryParse(k[2], out t2); var p = Positions.FirstOrDefault(x => x.Ticket == t2); if (p == null) return; col = p.Dir > 0 ? BuyC : SellC;
                double step = VolStep > 0 ? VolStep : 0.01, lots = Math.Floor(p.Lots * pc / 100.0 / step + 1e-9) * step, part = (p.Profit + p.Swap) * lots / p.Lots;
                double rest = Positions.Where(x => string.Equals(x.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Profit + x.Swap) - part;
                txt = "✂ close " + pc + "% of #" + p.Ticket + " now: " + lots.ToString("0.##", IC) + " of " + p.Lots.ToString("0.##", IC) + " lots\nbanks ≈ " + Money(part) + "   •   " + (p.Lots - lots).ToString("0.##", IC) + " lots stay open\nthe rest of " + Symbol + " would show " + Money(rest) + " open";
            }
            if (txt == null) return;
            var ft = T(txt, 11.5, textB); double x = m.X + 16, y = m.Y + 14; if (x + ft.Width + 20 > plotW) x = m.X - ft.Width - 26; if (y + ft.Height + 14 > TopPad + plotH) y = m.Y - ft.Height - 18;
            dc.DrawRoundedRectangle(tagBg, P(Br(col), 1.2), new Rect(x, y, ft.Width + 18, ft.Height + 12), 8, 8); dc.DrawText(ft, new Point(x + 9, y + 6));
        }

        // a horizontal level with its label
        void Level(DrawingContext dc, double y, SolidColorBrush br, string text, bool dashed, double labelX, Brush labelFg)
        {
            if (y < TopPad || y > TopPad + plotH) return;
            dc.DrawLine(dashed ? P(br, 1, 5, 4) : P(br, 1.8), new Point(0, y), new Point(plotW, y));
            var t = T(text, 11.5, labelFg ?? br, false, true); double lx = Math.Max(4, Math.Min(labelX, plotW - t.Width - 20)); dc.DrawRoundedRectangle(Br(Theme.Plot, 238), P(br, 1), new Rect(lx, y - 10, t.Width + 12, 20), 5, 5); dc.DrawText(t, new Point(lx + 6, y - 8));
        }

        // ---- what is under the mouse
        string HitAt(Point p, out long ticket, out string kind, out string key)
        {
            ticket = 0; kind = ""; key = "";
            foreach (var h in hits.AsEnumerable().Reverse()) if (h.R.Contains(p)) { ticket = h.Ticket; kind = h.Kind; key = h.Key; return "HIT"; }
            if (p.X > plotW) return "PRICEAXIS"; if (p.Y > TopPad + plotH) return "TIMEAXIS";
            // a ticket / S/L / T/P / order line within 4 px (not when the lines are hidden: then only the small handle on the right drags)
            if (TicketMode != "CLEAN")
            foreach (var ps in Positions.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)))
            {
                if (ps.Sl > 0 && Math.Abs(p.Y - Y(ps.Sl)) <= 6) { ticket = ps.Ticket; kind = "SL"; key = "SL:" + ps.Ticket; return "LINE"; }
                if (ps.Tp > 0 && Math.Abs(p.Y - Y(ps.Tp)) <= 6) { ticket = ps.Ticket; kind = "TP"; key = "TP:" + ps.Ticket; return "LINE"; }
            }
            if (TicketMode != "CLEAN")
            foreach (var o in Orders.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase)))
            {
                if (Math.Abs(p.Y - Y(o.Price)) <= 6) { ticket = o.Ticket; kind = "ORD"; key = "ORD:" + o.Ticket; return "LINE"; }
                if (o.Sl > 0 && Math.Abs(p.Y - Y(o.Sl)) <= 6) { ticket = o.Ticket; kind = "OSL"; key = "OSL:" + o.Ticket; return "LINE"; }
                if (o.Tp > 0 && Math.Abs(p.Y - Y(o.Tp)) <= 6) { ticket = o.Ticket; kind = "OTP"; key = "OTP:" + o.Ticket; return "LINE"; }
            }
            if (TicketMode != "CLEAN")
            foreach (var ps in Positions.Where(q => string.Equals(q.Symbol, Symbol, StringComparison.OrdinalIgnoreCase))) if (Math.Abs(p.Y - Y(ps.Open)) <= 5) { ticket = ps.Ticket; kind = "POS"; key = "POS:" + ps.Ticket; return "LINE"; }
            string part; var d = DrawingAt(p, out part); if (d != null) { key = "DRAW:" + Drawings.IndexOf(d); kind = part; return "DRAWING"; }
            return "PLOT";
        }

        // ---- mouse
        protected override void OnMouseWheel(MouseWheelEventArgs e) { barW = Math.Max(1.2, Math.Min(80, barW * (e.Delta > 0 ? 1.15 : 1 / 1.15))); InvalidateVisual(); e.Handled = true; }
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this); if (Series == null || Series.Bars.Count == 0) return; e.Handled = true;
            if (Tool != "") { Tool = ""; newDraw = null; if (ToolDone != null) ToolDone(); InvalidateVisual(); return; }
            long tk; string kind, key; var what = HitAt(p, out tk, out kind, out key); Drawing dr = null;
            if (what == "HIT" && kind == "IND") { if (IndOff != null) IndOff(key.Substring(4)); return; }   // right-click on an indicator chip = remove it
            if (what == "DRAWING") { string part; dr = DrawingAt(p, out part); selected = dr; kind = "DRAW"; }
            else if (what == "HIT" && kind == "X") { kind = key.Replace("X:", "").Split(':')[0]; }
            else if (what != "HIT" && what != "LINE") kind = "";
            InvalidateVisual();
            if (RightClick != null) RightClick(p, PriceAt(p.Y), TimeAt(IndexAt(p.X)), kind, tk, dr);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { bool had = Tool != "" || selected != null || measure != null || PickKind != ""; Tool = ""; newDraw = null; selected = null; measure = null; PickPrice = double.NaN; PickKind = ""; if (ToolDone != null) ToolDone(); InvalidateVisual(); if (had) e.Handled = true; }
            if (e.Key == Key.Delete && selected != null) { DeleteSelected(); e.Handled = true; }
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus(); var p = e.GetPosition(this); if (Series == null || Series.Bars.Count == 0) return;
            if (e.ClickCount == 2 && Tool == "") { string pt; var dd = DrawingAt(p, out pt); if (dd != null && dd.Kind == "TEXT" && AskText != null) { var s = AskText(dd.Text); if (s != null) { dd.Text = s; if (DrawingsChanged != null) DrawingsChanged(); } InvalidateVisual(); e.Handled = true; return; } ResetView(); e.Handled = true; return; }
            long tk; string kind, key; string what = HitAt(p, out tk, out kind, out key);
            if (what == "HIT") { var h = hits.LastOrDefault(x => x.R.Contains(p)); if (h != null && h.Click != null) { h.Click(); e.Handled = true; return; } }
            if (PricePicked != null && PickKind != "" && double.IsNaN(PickPrice) && p.X < plotW) { PickPrice = Math.Round(PriceAt(p.Y) / Point) * Point; PricePicked(PickPrice); InvalidateVisual(); e.Handled = true; return; }
            if (measure != null && Tool == "") { measure = null; InvalidateVisual(); }
            if (Tool != "" && p.X < plotW && p.Y < TopPad + plotH)
            {
                var t = TimeAt(IndexAt(p.X)); double pr = Snap(p, PriceAt(p.Y));
                newDraw = new Drawing { Kind = Tool, T1 = t, T2 = t, P1 = pr, P2 = pr, P3 = pr, Color = Tool == "LONG" || Tool == "SHORT" ? "#E8ECF4" : DrawColor, Width = 1.6, Fill = Tool == "RECT" ? DrawFill : "" };
                if (Tool == "FVG")
                {
                    // click the 1st candle, then the 3rd: bullish (3rd low above the 1st high) or bearish (3rd high below the 1st low) is found by itself
                    int ci = Math.Max(0, Math.Min(Series.Bars.Count - 1, (int)Math.Round(IndexAt(p.X))));
                    if (fvgFirst < 0) { fvgFirst = ci; InvalidateVisual(); e.Handled = true; return; }
                    int ia = Math.Min(fvgFirst, ci), ib = Math.Max(fvgFirst, ci); fvgFirst = -1;
                    var fv = ib - ia >= 2 ? FvgBetween(ia, ib) : null;
                    if (fv == null) { if (Toast != null) Toast(ib - ia < 2 ? "▤ pick the 1st and the 3rd candle (one candle between them)" : "▤ no gap: the 3rd candle overlaps the 1st — click the 1st candle again"); InvalidateVisual(); e.Handled = true; return; }
                    Drawings.Add(fv); selected = fv; newDraw = null; Tool = ""; if (ToolDone != null) ToolDone(); if (DrawingsChanged != null) DrawingsChanged(); InvalidateVisual(); e.Handled = true; return;
                }
                if (OneClick(Tool))
                {
                    if (Tool == "TEXT") { var s = AskText != null ? AskText("") : "text"; if (string.IsNullOrEmpty(s)) { newDraw = null; Tool = ""; if (ToolDone != null) ToolDone(); InvalidateVisual(); e.Handled = true; return; } newDraw.Text = s; newDraw.Width = 1; }
                    Drawings.Add(newDraw); selected = newDraw; newDraw = null; Tool = ""; if (ToolDone != null) ToolDone(); if (DrawingsChanged != null) DrawingsChanged(); InvalidateVisual(); e.Handled = true; return;
                }
                drag = "NEWDRAW"; dragFrom = p; CaptureMouse(); e.Handled = true; return;
            }
            dragFrom = p; dragOff = rightOffset; dragHi = mHi; dragLo = mLo; dragBarW = barW;
            if (what == "PRICEAXIS") drag = "AXIS";
            else if (what == "TIMEAXIS") drag = "TIME";
            else if (TradeOn && (what == "LINE" || what == "HIT") && (kind == "SL" || kind == "TP" || kind == "POS" || kind == "ORD" || kind == "OSL" || kind == "OTP"))
            { bool o = kind == "ORD" || kind == "OSL" || kind == "OTP"; drag = o ? "ORDER" : "TICKET"; dragTicket = tk; dragWhat = kind == "POS" ? "NEW" : kind == "ORD" ? "PX" : kind == "OSL" ? "SL" : kind == "OTP" ? "TP" : kind; dragPrice = Math.Round(PriceAt(p.Y) / Point) * Point; }
            else if (what == "DRAWING") { string part; dragDraw = DrawingAt(p, out part); dragPart = part; selected = dragDraw; dragOrig = dragDraw.Clone(); drag = "MOVEDRAW"; dragDP = IndexAt(p.X); }
            else { drag = "PAN"; selected = null; }
            CaptureMouse(); e.Handled = true; InvalidateVisual();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this); mouse = p;
            if (drag == null)
            {
                long tk = 0; string kind = "", key = ""; string what = Series == null || Series.Bars.Count == 0 ? "PLOT" : HitAt(p, out tk, out kind, out key);
                if (key != hoverKey) { hoverKey = key; hoverAt = DateTime.Now; }
                bool lineDrag = TradeOn && (what == "LINE" || (what == "HIT" && kind != "X"));
                Cursor = what == "HIT" && kind == "X" ? Cursors.Hand : what == "HIT" ? Cursors.Hand : what == "PRICEAXIS" ? Cursors.SizeNS : what == "TIMEAXIS" ? Cursors.SizeWE : lineDrag ? Cursors.Hand : what == "LINE" ? Cursors.Hand : what == "DRAWING" ? (kind == "BODY" ? Cursors.Hand : kind == "W" || kind == "X1" || kind == "X2" ? Cursors.SizeWE : kind == "Y1" || kind == "Y2" ? Cursors.SizeNS : Cursors.SizeAll) : Tool != "" || (PickKind != "" && double.IsNaN(PickPrice)) ? Cursors.Pen : Cursors.Hand;
            }
            else
            {
                double dy = p.Y - dragFrom.Y, rng = dragHi - dragLo;
                if (drag == "PAN")
                {
                    rightOffset = Math.Min(plotW / barW * 0.8, Math.Max(-((Series != null ? Series.Bars.Count : 0) - 5), dragOff - (p.X - dragFrom.X) / barW));
                    if (!double.IsNaN(dragHi) && (Math.Abs(dy) > 8 || !auto)) { auto = false; double sh = dy / plotH * rng; mHi = dragHi + sh; mLo = dragLo + sh; }
                }
                else if (drag == "AXIS" && !double.IsNaN(dragHi)) { auto = false; double k = Math.Exp(dy / 160.0), mid = (dragHi + dragLo) / 2; mHi = mid + rng * k / 2; mLo = mid - rng * k / 2; }
                else if (drag == "TIME") barW = Math.Max(1.2, Math.Min(80, dragBarW * Math.Exp((dragFrom.X - p.X) / 180.0)));   // drag LEFT = zoom in (wider candles), RIGHT = zoom out (more candles)
                else if (drag == "TICKET" || drag == "ORDER") { Cursor = Cursors.SizeNS; dragPrice = Math.Round(PriceAt(p.Y) / Point) * Point; }
                else if (drag == "NEWDRAW" && newDraw != null)
                {
                    newDraw.T2 = TimeAt(IndexAt(p.X)); double pr = Snap(p, PriceAt(p.Y));
                    if (newDraw.Kind == "LONG" || newDraw.Kind == "SHORT")
                    {
                        int dir = newDraw.Kind == "LONG" ? 1 : -1; double risk = Math.Max(Point * 2, Math.Abs(pr - newDraw.P1)); newDraw.P2 = newDraw.P1 - dir * risk; newDraw.P3 = newDraw.P1 + dir * 2 * risk;
                        if (newDraw.T2 <= newDraw.T1) newDraw.T2 = newDraw.T1.AddMinutes(Series.Minutes * 25);
                    }
                    else newDraw.P2 = pr;
                    if (newDraw.Kind == "MEASURE") measure = newDraw;
                }
                else if (drag == "MOVEDRAW" && dragDraw != null && dragOrig != null)
                {
                    Cursor = dragPart == "BODY" ? Cursors.SizeAll : dragPart == "W" || dragPart == "X1" || dragPart == "X2" ? Cursors.SizeWE : dragPart == "Y1" || dragPart == "Y2" ? Cursors.SizeNS : Cursors.Cross;
                    double di = IndexAt(p.X) - dragDP, dp = PriceAt(p.Y) - PriceAt(dragFrom.Y); var pr = Snap(p, PriceAt(p.Y)); var t = TimeAt(IndexAt(p.X));
                    if (dragPart == "P1") { if (dragDraw.Kind == "LONG" || dragDraw.Kind == "SHORT") { dragDraw.P1 = pr; } else { dragDraw.T1 = t; dragDraw.P1 = pr; } }
                    else if (dragPart == "P2") { if (dragDraw.Kind == "LONG" || dragDraw.Kind == "SHORT") dragDraw.P2 = pr; else { dragDraw.T2 = t; dragDraw.P2 = pr; } }
                    else if (dragPart == "P3") dragDraw.P3 = pr;
                    else if (dragPart == "X1Y2") { dragDraw.T1 = t; dragDraw.P2 = pr; }
                    else if (dragPart == "X2Y1") { dragDraw.T2 = t; dragDraw.P1 = pr; }
                    else if (dragPart == "Y1") dragDraw.P1 = pr;
                    else if (dragPart == "Y2") dragDraw.P2 = pr;
                    else if (dragPart == "X1") dragDraw.T1 = t;
                    else if (dragPart == "X2") dragDraw.T2 = t;
                    else if (dragPart == "W") dragDraw.T2 = t > dragDraw.T1 ? t : dragDraw.T1.AddMinutes(Series.Minutes * 3);
                    else
                    {
                        dragDraw.T1 = dragOrig.T1.AddMinutes(di * Series.Minutes); dragDraw.T2 = dragOrig.T2.AddMinutes(di * Series.Minutes);
                        dragDraw.P1 = dragOrig.P1 + dp; dragDraw.P2 = dragOrig.P2 + dp; dragDraw.P3 = dragOrig.P3 + dp;
                        if (dragDraw.Kind == "HLINE" || dragDraw.Kind == "HRAY") { dragDraw.P1 = Snap(p, dragOrig.P1 + dp); if (dragDraw.Kind == "HLINE") { dragDraw.T1 = dragOrig.T1; dragDraw.T2 = dragOrig.T2; } }
                    }
                }
            }
            InvalidateVisual();
        }
        // a price on the losing side of the market now (a buy: under the bid; a sell: over the ask) = a stop, else a target
        bool StopSide(int dir, double price) { return dir > 0 ? price < (Bid > 0 ? Bid : price + 1) : price > (Ask > 0 ? Ask : price - 1); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            var was = drag; drag = null; ReleaseMouseCapture();
            if (was == "TICKET")
            {
                var ps = Positions.FirstOrDefault(q => q.Ticket == dragTicket); double pr = dragPrice;
                if (ps != null && ModifyTicket != null && !double.IsNaN(pr) && Math.Abs(pr - (dragWhat == "SL" ? ps.Sl : dragWhat == "TP" ? ps.Tp : ps.Open)) > Point / 2)
                {
                    double sl = ps.Sl, tp = ps.Tp;
                    if (dragWhat == "SL") sl = pr; else if (dragWhat == "TP") tp = pr; else if (StopSide(ps.Dir, pr)) sl = pr; else tp = pr;   // from the entry line: ALWAYS — the losing side of the price = the stop (it can lock profit), the other side = the target
                    bool bad = ps.Dir > 0 ? (sl > 0 && sl >= Bid) || (tp > 0 && tp <= Bid) : (sl > 0 && sl <= Ask) || (tp > 0 && tp >= Ask);
                    if (bad) { if (Toast != null) Toast("a stop must be on the losing side of the price, a target on the winning side"); } else ModifyTicket(ps.Ticket, sl, tp);
                }
            }
            else if (was == "ORDER")
            {
                var o = Orders.FirstOrDefault(q => q.Ticket == dragTicket);
                if (o != null && ModifyOrder != null && !double.IsNaN(dragPrice))
                {
                    if (dragWhat == "PX" && Math.Abs(dragPrice - o.Price) > Point / 2) { double d = dragPrice - o.Price; ModifyOrder(o.Ticket, dragPrice, o.Sl > 0 ? o.Sl + d : 0, o.Tp > 0 ? o.Tp + d : 0); }   // the bracket moves with the order
                    else if (dragWhat == "SL" && Math.Abs(dragPrice - o.Sl) > Point / 2) ModifyOrder(o.Ticket, o.Price, dragPrice, o.Tp);
                    else if (dragWhat == "TP" && Math.Abs(dragPrice - o.Tp) > Point / 2) ModifyOrder(o.Ticket, o.Price, o.Sl, dragPrice);
                }
            }
            else if (was == "NEWDRAW" && newDraw != null)
            {
                if (newDraw.Kind == "MEASURE") { measure = newDraw; newDraw = null; Tool = ""; if (ToolDone != null) ToolDone(); }
                else
                {
                    if ((newDraw.Kind == "LONG" || newDraw.Kind == "SHORT") && newDraw.P2 == newDraw.P1) { int dir = newDraw.Kind == "LONG" ? 1 : -1; double r = Math.Max(Point * 20, (hi - lo) * 0.05); newDraw.P2 = newDraw.P1 - dir * r; newDraw.P3 = newDraw.P1 + dir * 2 * r; newDraw.T2 = newDraw.T1.AddMinutes(Series.Minutes * 25); }
                    Drawings.Add(newDraw); selected = newDraw; newDraw = null; Tool = ""; if (ToolDone != null) ToolDone(); if (DrawingsChanged != null) DrawingsChanged();
                }
            }
            else if (was == "MOVEDRAW" && DrawingsChanged != null) DrawingsChanged();
            dragPrice = double.NaN; dragOrig = null; InvalidateVisual();
        }
        protected override void OnMouseLeave(MouseEventArgs e) { mouse = null; if (drag == null) hoverKey = ""; InvalidateVisual(); }
    }
}
