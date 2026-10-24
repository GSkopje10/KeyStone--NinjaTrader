//+------------------------------------------------------------------+
//| SpasiXBridge.mq5 — connects MetaTrader 5 to the SpasiX app on    |
//| this PC (127.0.0.1): the account, prices, positions / orders and |
//| candles, history, swaps and deals go to the app. TRADING only when the  |
//| input EnableTrading = true here (+ MT5's Algo Trading) AND the   |
//| ARM button in SpasiX is on. Every command is answered (RES).     |
//+------------------------------------------------------------------+
#property copyright "SpasiX"
#property version   "5.00"
#property description "SpasiX bridge: prices, positions, candles • trading only with EnableTrading + Algo Trading + ARM in the app"
#include <Trade\Trade.mqh>

input string AppHost = "127.0.0.1";   // the SpasiX MT5 app (this PC)
input int    AppPort = 47123;         // its port
input string Symbols = "";            // symbols to stream, e.g. XAUUSD,US30 (empty = every symbol in Market Watch)
input bool   EnableTrading = true;    // SpasiX may send orders once you ARM it in the app (false = read only)
input double MaxLotsPerOrder = 5.0;   // the most lots one order may have
input int    MaxDeviationPoints = 30; // the most slippage accepted on market orders (points)

CTrade trade;

int      sock    = INVALID_HANDLE;
datetime lastTry = 0;
string   inbuf   = "";
int      beats   = 0;

int OnInit()
  {
   EventSetMillisecondTimer(100);
   trade.SetDeviationInPoints(MaxDeviationPoints); trade.SetExpertMagicNumber(770077); trade.SetAsyncMode(false);
   Comment("SpasiX: starting…");
   return(INIT_SUCCEEDED);
  }

void OnDeinit(const int reason)
  {
   EventKillTimer();
   if(sock != INVALID_HANDLE) { SendLine("BYE"); SocketClose(sock); sock = INVALID_HANDLE; }
   Comment("");
  }

// one text line to the app (false = the connection dropped)
bool SendLine(string s)
  {
   if(sock == INVALID_HANDLE) return(false);
   uchar arr[];
   int n = StringToCharArray(s + "\n", arr, 0, WHOLE_ARRAY, CP_UTF8) - 1;   // without the closing 0
   if(n <= 0) return(true);
   if(SocketSend(sock, arr, n) < 0) { SocketClose(sock); sock = INVALID_HANDLE; return(false); }
   return(true);
  }

string Clean(string s) { StringReplace(s, "|", "/"); StringReplace(s, "\n", " "); StringReplace(s, "\r", " "); return(s); }

// the symbols to stream
int SymbolList(string &list[])
  {
   ArrayResize(list, 0);
   if(StringLen(Symbols) > 0)
     {
      string parts[]; int n = StringSplit(Symbols, ',', parts);
      for(int i = 0; i < n; i++) { string p = parts[i]; StringTrimLeft(p); StringTrimRight(p); if(StringLen(p) == 0) continue; SymbolSelect(p, true); int k = ArraySize(list); ArrayResize(list, k + 1); list[k] = p; }
     }
   else
     {
      int total = SymbolsTotal(true);
      for(int i = 0; i < total; i++) { int k = ArraySize(list); ArrayResize(list, k + 1); list[k] = SymbolName(i, true); }
     }
   return(ArraySize(list));
  }

bool Connect()
  {
   if(sock != INVALID_HANDLE && SocketIsConnected(sock)) return(true);
   if(sock != INVALID_HANDLE) { SocketClose(sock); sock = INVALID_HANDLE; }
   if(TimeLocal() - lastTry < 2) return(false);
   lastTry = TimeLocal();
   sock = SocketCreate();
   if(sock == INVALID_HANDLE) { Comment("SpasiX: cannot create a socket (error ", GetLastError(), ")"); return(false); }
   if(!SocketConnect(sock, AppHost, AppPort, 1000))
     {
      int e = GetLastError(); SocketClose(sock); sock = INVALID_HANDLE;
      if(e == 4014) Comment("SpasiX: add 127.0.0.1 in Tools > Options > Expert Advisors > 'Allow WebRequest for listed URL'");
      else Comment("SpasiX: waiting for the SpasiX app — start SpasiX.exe (error ", e, ")");
      return(false);
     }
   Comment("SpasiX: connected to the app • trading ", EnableTrading ? "possible after ARM in the app" : "OFF (EnableTrading = false)");
   SendLine("HELLO|5|" + TerminalInfoString(TERMINAL_COMPANY) + "|" + IntegerToString(TerminalInfoInteger(TERMINAL_BUILD)));
   SendSymbols();
   return(true);
  }

void SendSymbols()
  {
   string list[]; int n = SymbolList(list);
   for(int i = 0; i < n; i++)
     {
      string s = list[i];
      SendLine("SYM|" + s + "|" + IntegerToString(SymbolInfoInteger(s, SYMBOL_DIGITS)) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_TRADE_CONTRACT_SIZE), 2) + "|"
               + DoubleToString(SymbolInfoDouble(s, SYMBOL_VOLUME_MIN), 2) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_VOLUME_STEP), 2) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_VOLUME_MAX), 2) + "|"
               + DoubleToString(SymbolInfoDouble(s, SYMBOL_POINT), 8) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_TRADE_TICK_VALUE), 6) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_TRADE_TICK_SIZE), 8) + "|"
               + Clean(SymbolInfoString(s, SYMBOL_DESCRIPTION)));
      SendLine("SYMX|" + s + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_SWAP_LONG), 4) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_SWAP_SHORT), 4) + "|" + IntegerToString(SymbolInfoInteger(s, SYMBOL_SWAP_MODE)) + "|"
               + IntegerToString(SymbolInfoInteger(s, SYMBOL_SWAP_ROLLOVER3DAYS)) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_MARGIN_INITIAL), 2) + "|" + IntegerToString(SymbolInfoInteger(s, SYMBOL_TRADE_CALC_MODE)) + "|" + IntegerToString(SymbolInfoInteger(s, SYMBOL_TRADE_STOPS_LEVEL)) + "|" + MarginInfo(s));
     }
  }

// MT5's own margin for 1 lot (OrderCalcMargin at the ask), the price it used, the hedged margin (contract size / value per hedged lot), "larger leg"
string MarginInfo(string s)
  {
   double ask = SymbolInfoDouble(s, SYMBOL_ASK), m = 0; if(ask <= 0) ask = SymbolInfoDouble(s, SYMBOL_BID);
   if(ask > 0 && !OrderCalcMargin(ORDER_TYPE_BUY, s, 1.0, ask, m)) m = 0;
   return DoubleToString(m, 2) + "|" + DoubleToString(ask, (int)SymbolInfoInteger(s, SYMBOL_DIGITS)) + "|" + DoubleToString(SymbolInfoDouble(s, SYMBOL_MARGIN_HEDGED), 2) + "|" + (SymbolInfoInteger(s, SYMBOL_MARGIN_HEDGED_USE_LEG) ? "1" : "0");
  }

void SendAll()
  {
   // the account
   string acc = "ACC|" + IntegerToString(AccountInfoInteger(ACCOUNT_LOGIN)) + "|" + Clean(AccountInfoString(ACCOUNT_NAME)) + "|" + Clean(AccountInfoString(ACCOUNT_SERVER)) + "|" + AccountInfoString(ACCOUNT_CURRENCY) + "|"
                + DoubleToString(AccountInfoDouble(ACCOUNT_BALANCE), 2) + "|" + DoubleToString(AccountInfoDouble(ACCOUNT_EQUITY), 2) + "|" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN), 2) + "|"
                + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_FREE), 2) + "|" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_LEVEL), 2) + "|" + IntegerToString(AccountInfoInteger(ACCOUNT_LEVERAGE)) + "|"
                + (AccountInfoInteger(ACCOUNT_MARGIN_MODE) == ACCOUNT_MARGIN_MODE_RETAIL_HEDGING ? "HEDGE" : "NET") + "|" + (TerminalInfoInteger(TERMINAL_CONNECTED) ? "1" : "0") + "|"
                + (AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO ? "DEMO" : AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_CONTEST ? "CONTEST" : "REAL");
   if(!SendLine(acc)) return;
   SendLine("CAN|" + (EnableTrading ? "1" : "0") + "|" + (TerminalInfoInteger(TERMINAL_TRADE_ALLOWED) ? "1" : "0") + "|" + (MQLInfoInteger(MQL_TRADE_ALLOWED) ? "1" : "0") + "|" + DoubleToString(MaxLotsPerOrder, 2)
            + "|" + IntegerToString((long)(TimeTradeServer() - TimeGMT())) + "|" + IntegerToString((long)TimeTradeServer())
            + "|" + (AccountInfoInteger(ACCOUNT_TRADE_ALLOWED) ? "1" : "0") + "|" + (AccountInfoInteger(ACCOUNT_TRADE_EXPERT) ? "1" : "0"));
   // the prices
   string list[]; int n = SymbolList(list); string batch = "";
   for(int i = 0; i < n; i++)
     {
      MqlTick t; if(!SymbolInfoTick(list[i], t)) continue; int d = (int)SymbolInfoInteger(list[i], SYMBOL_DIGITS);
      batch += "TICK|" + list[i] + "|" + DoubleToString(t.bid, d) + "|" + DoubleToString(t.ask, d) + "|" + IntegerToString(t.time_msc) + "\n";
     }
   // the open positions (every ticket: hedging = buys and sells side by side)
   int pt = PositionsTotal(); batch += "POSS|" + IntegerToString(pt) + "\n";
   for(int i = 0; i < pt; i++)
     {
      ulong tk = PositionGetTicket(i); if(tk == 0) continue; string sym = PositionGetString(POSITION_SYMBOL); int d = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
      batch += "P|" + IntegerToString((long)tk) + "|" + sym + "|" + (PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? "BUY" : "SELL") + "|" + DoubleToString(PositionGetDouble(POSITION_VOLUME), 2) + "|"
               + DoubleToString(PositionGetDouble(POSITION_PRICE_OPEN), d) + "|" + DoubleToString(PositionGetDouble(POSITION_SL), d) + "|" + DoubleToString(PositionGetDouble(POSITION_TP), d) + "|"
               + DoubleToString(PositionGetDouble(POSITION_PRICE_CURRENT), d) + "|" + DoubleToString(PositionGetDouble(POSITION_PROFIT), 2) + "|" + DoubleToString(PositionGetDouble(POSITION_SWAP), 2) + "|"
               + IntegerToString((long)PositionGetInteger(POSITION_TIME)) + "|" + IntegerToString(PositionGetInteger(POSITION_MAGIC)) + "|" + Clean(PositionGetString(POSITION_COMMENT)) + "\n";
     }
   batch += "POSEND\n";
   // the pending orders
   int ot = OrdersTotal(); batch += "ORDS|" + IntegerToString(ot) + "\n";
   for(int i = 0; i < ot; i++)
     {
      ulong tk = OrderGetTicket(i); if(tk == 0) continue; string sym = OrderGetString(ORDER_SYMBOL); int d = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
      ENUM_ORDER_TYPE ty = (ENUM_ORDER_TYPE)OrderGetInteger(ORDER_TYPE);
      string tn = ty == ORDER_TYPE_BUY_LIMIT ? "BUY LIMIT" : ty == ORDER_TYPE_SELL_LIMIT ? "SELL LIMIT" : ty == ORDER_TYPE_BUY_STOP ? "BUY STOP" : ty == ORDER_TYPE_SELL_STOP ? "SELL STOP" : ty == ORDER_TYPE_BUY_STOP_LIMIT ? "BUY STOP LIMIT" : ty == ORDER_TYPE_SELL_STOP_LIMIT ? "SELL STOP LIMIT" : "OTHER";
      batch += "O|" + IntegerToString((long)tk) + "|" + sym + "|" + tn + "|" + DoubleToString(OrderGetDouble(ORDER_VOLUME_CURRENT), 2) + "|" + DoubleToString(OrderGetDouble(ORDER_PRICE_OPEN), d) + "|"
               + DoubleToString(OrderGetDouble(ORDER_SL), d) + "|" + DoubleToString(OrderGetDouble(ORDER_TP), d) + "|" + IntegerToString((long)OrderGetInteger(ORDER_TIME_SETUP)) + "\n";
     }
   batch += "ORDEND";
   SendLine(batch);
  }

// MT5 timeframe of a number of minutes
ENUM_TIMEFRAMES TfOf(int m)
  {
   switch(m)
     {
      case 1: return(PERIOD_M1); case 5: return(PERIOD_M5); case 15: return(PERIOD_M15); case 30: return(PERIOD_M30);
      case 60: return(PERIOD_H1); case 240: return(PERIOD_H4); case 1440: return(PERIOD_D1); case 10080: return(PERIOD_W1); case 43200: return(PERIOD_MN1);
     }
   return(PERIOD_H1);
  }

// candles for the app's chart: BARS|symbol|minutes|n, n × B|open time|o|h|l|c|tick volume, BARSEND|symbol|minutes (oldest first)
void SendBars(string sym, int m, int count)
  {
   MqlRates r[]; ArraySetAsSeries(r, false);
   int got = CopyRates(sym, TfOf(m), 0, MathMin(MathMax(count, 1), 100000), r); if(got < 0) got = 0;   // 0 = MT5 is still downloading: the app asks again
   int d = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   string b = "BARS|" + sym + "|" + IntegerToString(m) + "|" + IntegerToString(got) + "\n";
   for(int i = 0; i < got; i++)
     {
      b += "B|" + IntegerToString((long)r[i].time) + "|" + DoubleToString(r[i].open, d) + "|" + DoubleToString(r[i].high, d) + "|" + DoubleToString(r[i].low, d) + "|" + DoubleToString(r[i].close, d) + "|" + IntegerToString((long)r[i].tick_volume) + "\n";
      if(StringLen(b) > 60000) { if(!SendLine(StringSubstr(b, 0, StringLen(b) - 1))) return; b = ""; }
     }
   b += "BARSEND|" + sym + "|" + IntegerToString(m);
   SendLine(b);
  }

// one day (or any range) of 1-minute candles with the spread, for the app's BACKTEST:
// HIST|symbol|from|to|n|error, n × H|open time|o|h|l|c|tick volume|spread (points), HISTEND|symbol|from|to   (server time; error ≠ 0 = MT5 is still downloading)
void SendHist(string sym, long from, long to)
  {
   SymbolSelect(sym, true); ResetLastError();
   MqlRates r[]; ArraySetAsSeries(r, false);
   int got = CopyRates(sym, PERIOD_M1, (datetime)from, (datetime)(to - 1), r); int err = got < 0 ? GetLastError() : 0; if(got < 0) got = 0;
   if(got == 0 && err == 0)
     {
      // nothing came back: before the first 1-minute candle MT5 holds HERE, or past "Max bars in chart" = not loaded (4401), not "no trading that day"
      long here = SeriesInfoInteger(sym, PERIOD_M1, SERIES_FIRSTDATE); long mx = TerminalInfoInteger(TERMINAL_MAXBARS);
      if((here > 0 && from < here) || (mx > 0 && (long)TimeCurrent() - from > mx * 60 * 7 / 5)) err = 4401;
     }
   int d = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   string b = "HIST|" + sym + "|" + IntegerToString(from) + "|" + IntegerToString(to) + "|" + IntegerToString(got) + "|" + IntegerToString(err) + "\n";
   for(int i = 0; i < got; i++)
     {
      if((long)r[i].time < from || (long)r[i].time >= to) continue;
      b += "H|" + IntegerToString((long)r[i].time) + "|" + DoubleToString(r[i].open, d) + "|" + DoubleToString(r[i].high, d) + "|" + DoubleToString(r[i].low, d) + "|" + DoubleToString(r[i].close, d) + "|" + IntegerToString((long)r[i].tick_volume) + "|" + IntegerToString(r[i].spread) + "\n";
      if(StringLen(b) > 60000) { if(!SendLine(StringSubstr(b, 0, StringLen(b) - 1))) return; b = ""; }
     }
   b += "HISTEND|" + sym + "|" + IntegerToString(from) + "|" + IntegerToString(to);
   SendLine(b);
  }

// MT5's REAL TICKS of one day for the BACKTEST (every bid / ask change, in milliseconds from the day's start):
// TT|symbol|from|to|n|error, n × T|ms|bid|ask, TTEND|symbol|from|to   (error ≠ 0 or n = 0 on a weekday = MT5 is still downloading them)
void SendTicks(string sym, long from, long to)
  {
   SymbolSelect(sym, true); ResetLastError();
   MqlTick t[]; int got = CopyTicksRange(sym, t, COPY_TICKS_ALL, (ulong)from * 1000, (ulong)to * 1000 - 1); int err = got < 0 ? GetLastError() : 0; if(got < 0) got = 0;
   int d = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   string b = "TT|" + sym + "|" + IntegerToString(from) + "|" + IntegerToString(to) + "|" + IntegerToString(got) + "|" + IntegerToString(err) + "\n";
   double lb = 0, la = 0;
   for(int i = 0; i < got; i++)
     {
      if(t[i].bid <= 0 || t[i].ask <= 0) continue; if(t[i].bid == lb && t[i].ask == la) continue; lb = t[i].bid; la = t[i].ask;
      b += "T|" + IntegerToString((long)(t[i].time_msc - (ulong)from * 1000)) + "|" + DoubleToString(t[i].bid, d) + "|" + DoubleToString(t[i].ask, d) + "\n";
      if(StringLen(b) > 60000) { if(!SendLine(StringSubstr(b, 0, StringLen(b) - 1))) return; b = ""; }
     }
   b += "TTEND|" + sym + "|" + IntegerToString(from) + "|" + IntegerToString(to);
   SendLine(b);
  }

// how far back MT5's history goes for a symbol: FIRST|symbol|first date on the broker's server|first date on this PC|first 1-minute candle here (epochs, 0 = unknown)|max bars in chart
void SendFirst(string sym)
  {
   SymbolSelect(sym, true);
   long sv = SeriesInfoInteger(sym, PERIOD_M1, SERIES_SERVER_FIRSTDATE), tm = SeriesInfoInteger(sym, PERIOD_M1, SERIES_TERMINAL_FIRSTDATE), m1 = SeriesInfoInteger(sym, PERIOD_M1, SERIES_FIRSTDATE);
   long mx = TerminalInfoInteger(TERMINAL_MAXBARS);   // Tools > Options > Charts > Max bars in chart: MT5 never hands out more 1-minute candles than this
   SendLine("FIRST|" + sym + "|" + IntegerToString(sv) + "|" + IntegerToString(tm) + "|" + IntegerToString(m1) + "|" + IntegerToString(mx));
  }

// how deep each timeframe's history goes HERE (M1 … MN1): DEPTH|symbol|broker's first date|max bars in chart|minutes:first candle:candles,…
// (MT5 builds every timeframe from the broker's 1-minute history; "Max bars in chart" caps how many candles a program can read)
void SendDepth(string sym)
  {
   SymbolSelect(sym, true);
   int mins[] = {1, 5, 15, 30, 60, 240, 1440, 10080, 43200};
   string b = "DEPTH|" + sym + "|" + IntegerToString(SeriesInfoInteger(sym, PERIOD_M1, SERIES_SERVER_FIRSTDATE)) + "|" + IntegerToString(TerminalInfoInteger(TERMINAL_MAXBARS)) + "|";
   for(int i = 0; i < ArraySize(mins); i++)
     {
      ENUM_TIMEFRAMES tf = TfOf(mins[i]); long first = SeriesInfoInteger(sym, tf, SERIES_FIRSTDATE); int n = Bars(sym, tf);
      b += (i > 0 ? "," : "") + IntegerToString(mins[i]) + ":" + IntegerToString(first) + ":" + IntegerToString(n);
     }
   SendLine(b);
  }

// the account's deals (for the app's TRACKER): DEALS|from|to|n, n × D|ticket|time|symbol|type|entry|lots|price|profit|commission + fee|swap|position|magic|reason|comment, DEALEND
void SendDeals(long from, long to)
  {
   if(!HistorySelect((datetime)from, (datetime)to)) { SendLine("DEALS|" + IntegerToString(from) + "|" + IntegerToString(to) + "|0\nDEALEND"); return; }
   int n = HistoryDealsTotal();
   string b = "DEALS|" + IntegerToString(from) + "|" + IntegerToString(to) + "|" + IntegerToString(n) + "\n";
   for(int i = 0; i < n; i++)
     {
      ulong tk = HistoryDealGetTicket(i); if(tk == 0) continue;
      string sym = HistoryDealGetString(tk, DEAL_SYMBOL); int d = sym != "" ? (int)SymbolInfoInteger(sym, SYMBOL_DIGITS) : 2; if(d <= 0) d = 5;
      long ty = HistoryDealGetInteger(tk, DEAL_TYPE), en = HistoryDealGetInteger(tk, DEAL_ENTRY), rs = HistoryDealGetInteger(tk, DEAL_REASON);
      string tn = ty == DEAL_TYPE_BUY ? "BUY" : ty == DEAL_TYPE_SELL ? "SELL" : ty == DEAL_TYPE_BALANCE ? "BALANCE" : ty == DEAL_TYPE_CREDIT ? "CREDIT" : "OTHER";
      string enn = en == DEAL_ENTRY_IN ? "IN" : en == DEAL_ENTRY_OUT ? "OUT" : en == DEAL_ENTRY_INOUT ? "INOUT" : en == DEAL_ENTRY_OUT_BY ? "OUTBY" : "";
      string rn = rs == DEAL_REASON_SL ? "SL" : rs == DEAL_REASON_TP ? "TP" : rs == DEAL_REASON_SO ? "STOPOUT" : rs == DEAL_REASON_EXPERT ? "EA" : rs == DEAL_REASON_CLIENT ? "DESKTOP" : rs == DEAL_REASON_MOBILE ? "MOBILE" : rs == DEAL_REASON_WEB ? "WEB" : "";
      b += "D|" + IntegerToString((long)tk) + "|" + IntegerToString((long)HistoryDealGetInteger(tk, DEAL_TIME)) + "|" + sym + "|" + tn + "|" + enn + "|" + DoubleToString(HistoryDealGetDouble(tk, DEAL_VOLUME), 2) + "|"
           + DoubleToString(HistoryDealGetDouble(tk, DEAL_PRICE), d) + "|" + DoubleToString(HistoryDealGetDouble(tk, DEAL_PROFIT), 2) + "|" + DoubleToString(HistoryDealGetDouble(tk, DEAL_COMMISSION) + HistoryDealGetDouble(tk, DEAL_FEE), 2) + "|"
           + DoubleToString(HistoryDealGetDouble(tk, DEAL_SWAP), 2) + "|" + IntegerToString((long)HistoryDealGetInteger(tk, DEAL_POSITION_ID)) + "|" + IntegerToString(HistoryDealGetInteger(tk, DEAL_MAGIC)) + "|" + rn + "|" + Clean(HistoryDealGetString(tk, DEAL_COMMENT)) + "\n";
      if(StringLen(b) > 60000) { if(!SendLine(StringSubstr(b, 0, StringLen(b) - 1))) return; b = ""; }
     }
   b += "DEALEND";
   SendLine(b);
  }

// ---- trading (only with EnableTrading = true and the app ARMED; the app never sends a command while disarmed)
void Res(string id, bool ok, string text, long ticket)
  {
   SendLine("RES|" + id + "|" + (ok ? "OK" : "FAIL") + "|" + IntegerToString((long)trade.ResultRetcode()) + "|" + Clean(text) + "|" + IntegerToString(ticket));
  }
bool CanTrade(string id)
  {
   if(!EnableTrading) { Res(id, false, "trading is OFF in the bridge: chart ▸ Expert list ▸ SpasiXBridge ▸ Properties ▸ Inputs ▸ EnableTrading = true", 0); return(false); }
   if(!TerminalInfoInteger(TERMINAL_TRADE_ALLOWED)) { Res(id, false, "turn ON Algo Trading in the MT5 toolbar", 0); return(false); }
   if(!MQLInfoInteger(MQL_TRADE_ALLOWED)) { Res(id, false, "tick 'Allow Algo Trading' in the bridge's properties (Common tab)", 0); return(false); }
   return(true);
  }
double Lots(string sym, double lots)
  {
   double step = SymbolInfoDouble(sym, SYMBOL_VOLUME_STEP), mn = SymbolInfoDouble(sym, SYMBOL_VOLUME_MIN), mx = MathMin(SymbolInfoDouble(sym, SYMBOL_VOLUME_MAX), MaxLotsPerOrder);
   if(step <= 0) step = 0.01; lots = MathFloor(lots / step + 1e-9) * step; if(lots < mn) lots = mn; if(lots > mx) lots = mx;
   return(NormalizeDouble(lots, 2));
  }
double Px(string sym, double p) { if(p <= 0) return(0); return(NormalizeDouble(p, (int)SymbolInfoInteger(sym, SYMBOL_DIGITS))); }
string Why(bool ok) { return(ok ? "done" : trade.ResultRetcodeDescription()); }

void DoOrder(string id, string sym, string kind, double lots, double price, double sl, double tp)
  {
   if(!CanTrade(id)) return;
   if(lots > MaxLotsPerOrder + 1e-9) { Res(id, false, "more than MaxLotsPerOrder (" + DoubleToString(MaxLotsPerOrder, 2) + ")", 0); return; }
   SymbolSelect(sym, true); trade.SetTypeFillingBySymbol(sym);
   double v = Lots(sym, lots); sl = Px(sym, sl); tp = Px(sym, tp); price = Px(sym, price); bool ok = false; string c = "SpasiX";
   if(kind == "BUY") ok = trade.Buy(v, sym, 0, sl, tp, c);
   else if(kind == "SELL") ok = trade.Sell(v, sym, 0, sl, tp, c);
   else if(kind == "BUY LIMIT") ok = trade.BuyLimit(v, price, sym, sl, tp, ORDER_TIME_GTC, 0, c);
   else if(kind == "SELL LIMIT") ok = trade.SellLimit(v, price, sym, sl, tp, ORDER_TIME_GTC, 0, c);
   else if(kind == "BUY STOP") ok = trade.BuyStop(v, price, sym, sl, tp, ORDER_TIME_GTC, 0, c);
   else if(kind == "SELL STOP") ok = trade.SellStop(v, price, sym, sl, tp, ORDER_TIME_GTC, 0, c);
   else { Res(id, false, "unknown order type " + kind, 0); return; }
   long tk = (long)(trade.ResultOrder() > 0 ? trade.ResultOrder() : trade.ResultDeal());
   Res(id, ok && (trade.ResultRetcode() == TRADE_RETCODE_DONE || trade.ResultRetcode() == TRADE_RETCODE_PLACED || trade.ResultRetcode() == TRADE_RETCODE_DONE_PARTIAL), kind + " " + DoubleToString(v, 2) + " " + sym + ": " + Why(ok), tk);
  }
bool CloseOne(ulong tk, double lots)
  {
   if(!PositionSelectByTicket(tk)) return(false);
   string sym = PositionGetString(POSITION_SYMBOL); trade.SetTypeFillingBySymbol(sym);
   double v = PositionGetDouble(POSITION_VOLUME);
   if(lots > 0 && lots < v - 1e-9) return(trade.PositionClosePartial(tk, Lots(sym, lots)));
   return(trade.PositionClose(tk));
  }
// which: ALL / BUY / SELL / PROFIT / LOSS • sym empty = every symbol
void DoCloseAll(string id, string sym, string which)
  {
   if(!CanTrade(id)) return;
   int done = 0, fail = 0;
   for(int i = PositionsTotal() - 1; i >= 0; i--)
     {
      ulong tk = PositionGetTicket(i); if(tk == 0) continue;
      if(StringLen(sym) > 0 && PositionGetString(POSITION_SYMBOL) != sym) continue;
      long ty = PositionGetInteger(POSITION_TYPE); double pl = PositionGetDouble(POSITION_PROFIT) + PositionGetDouble(POSITION_SWAP);
      if(which == "BUY" && ty != POSITION_TYPE_BUY) continue; if(which == "SELL" && ty != POSITION_TYPE_SELL) continue;
      if(which == "PROFIT" && pl <= 0) continue; if(which == "LOSS" && pl >= 0) continue;
      if(CloseOne(tk, 0)) done++; else fail++;
     }
   Res(id, fail == 0, "closed " + IntegerToString(done) + (fail > 0 ? " • " + IntegerToString(fail) + " failed: " + trade.ResultRetcodeDescription() : ""), done);
  }
// breakeven: the stop to the entry ± offset points on every position of sym / which that is in profit by more than that
void DoBreakeven(string id, string sym, string which, int offPts)
  {
   if(!CanTrade(id)) return;
   int done = 0, skip = 0, fail = 0;
   for(int i = PositionsTotal() - 1; i >= 0; i--)
     {
      ulong tk = PositionGetTicket(i); if(tk == 0) continue; string s0 = PositionGetString(POSITION_SYMBOL);
      if(StringLen(sym) > 0 && s0 != sym) continue; long ty = PositionGetInteger(POSITION_TYPE);
      if(which == "BUY" && ty != POSITION_TYPE_BUY) continue; if(which == "SELL" && ty != POSITION_TYPE_SELL) continue;
      double pt = SymbolInfoDouble(s0, SYMBOL_POINT), op = PositionGetDouble(POSITION_PRICE_OPEN), cur = PositionGetDouble(POSITION_PRICE_CURRENT), tp = PositionGetDouble(POSITION_TP), sl = PositionGetDouble(POSITION_SL);
      double be = ty == POSITION_TYPE_BUY ? op + offPts * pt : op - offPts * pt;
      bool inProfit = ty == POSITION_TYPE_BUY ? cur > be + pt : cur < be - pt;
      bool better = sl == 0 || (ty == POSITION_TYPE_BUY ? be > sl : be < sl);
      if(!inProfit || !better) { skip++; continue; }
      if(trade.PositionModify(tk, Px(s0, be), tp)) done++; else fail++;
     }
   Res(id, fail == 0, "breakeven on " + IntegerToString(done) + " • " + IntegerToString(skip) + " not in profit / already better" + (fail > 0 ? " • " + IntegerToString(fail) + " failed: " + trade.ResultRetcodeDescription() : ""), done);
  }
void DoCancelAll(string id, string sym)
  {
   if(!CanTrade(id)) return;
   int done = 0, fail = 0;
   for(int i = OrdersTotal() - 1; i >= 0; i--) { ulong tk = OrderGetTicket(i); if(tk == 0) continue; if(StringLen(sym) > 0 && OrderGetString(ORDER_SYMBOL) != sym) continue; if(trade.OrderDelete(tk)) done++; else fail++; }
   Res(id, fail == 0, "cancelled " + IntegerToString(done) + " order(s)" + (fail > 0 ? " • " + IntegerToString(fail) + " failed" : ""), done);
  }

// every symbol of the broker (for the app's + button) and Market Watch changes
void SendAllSymbols()
  {
   int total = SymbolsTotal(false); string b = "";
   for(int i = 0; i < total; i++) { b += (StringLen(b) > 0 ? "," : "") + SymbolName(i, false); if(StringLen(b) > 30000) { SendLine("ALLSYMS|" + b); b = ""; } }
   SendLine("ALLSYMS|" + b); SendLine("ALLSYMSEND");
  }

// messages from the app
void ReadApp()
  {
   if(sock == INVALID_HANDLE) return;
   uint len = SocketIsReadable(sock); if(len == 0) return;
   uchar b[]; int n = SocketRead(sock, b, len, 10); if(n <= 0) return;
   inbuf += CharArrayToString(b, 0, n, CP_UTF8);
   int p;
   while((p = StringFind(inbuf, "\n")) >= 0)
     {
      string line = StringSubstr(inbuf, 0, p); inbuf = StringSubstr(inbuf, p + 1);
      if(line == "PING") SendLine("PONG");
      else if(line == "SYMBOLS") SendSymbols();
      else if(StringFind(line, "BARS|") == 0) { string p2[]; if(StringSplit(line, '|', p2) >= 4) SendBars(p2[1], (int)StringToInteger(p2[2]), (int)StringToInteger(p2[3])); }
      else if(line == "ALLSYMS") SendAllSymbols();
      else if(StringFind(line, "HIST|") == 0) { string p3[]; if(StringSplit(line, '|', p3) >= 4) SendHist(p3[1], StringToInteger(p3[2]), StringToInteger(p3[3])); }
      else if(StringFind(line, "HISTT|") == 0) { string p5[]; if(StringSplit(line, '|', p5) >= 4) SendTicks(p5[1], StringToInteger(p5[2]), StringToInteger(p5[3])); }
      else if(StringFind(line, "FIRST|") == 0) SendFirst(StringSubstr(line, 6));
      else if(StringFind(line, "DEPTH|") == 0) SendDepth(StringSubstr(line, 6));
      else if(StringFind(line, "DEALS|") == 0) { string p4[]; if(StringSplit(line, '|', p4) >= 3) SendDeals(StringToInteger(p4[1]), StringToInteger(p4[2])); }
      else if(StringFind(line, "WATCH|") == 0) { SymbolSelect(StringSubstr(line, 6), true); SendSymbols(); }
      else
        {
         string a[]; int n = StringSplit(line, '|', a); if(n < 2) continue; string cmd = a[0], id = a[1];
         if(cmd == "ORDER" && n >= 8) DoOrder(id, a[2], a[3], StringToDouble(a[4]), StringToDouble(a[5]), StringToDouble(a[6]), StringToDouble(a[7]));
         else if(cmd == "CLOSE" && n >= 4) { if(!CanTrade(id)) continue; bool ok = CloseOne((ulong)StringToInteger(a[2]), StringToDouble(a[3])); Res(id, ok, "close #" + a[2] + ": " + Why(ok), StringToInteger(a[2])); }
         else if(cmd == "CLOSEALL" && n >= 4) DoCloseAll(id, a[2], a[3]);
         else if(cmd == "MODIFY" && n >= 5) { if(!CanTrade(id)) continue; ulong tk = (ulong)StringToInteger(a[2]); string sym = PositionSelectByTicket(tk) ? PositionGetString(POSITION_SYMBOL) : ""; bool ok = sym != "" && trade.PositionModify(tk, Px(sym, StringToDouble(a[3])), Px(sym, StringToDouble(a[4]))); Res(id, ok, "S/L T/P #" + a[2] + ": " + (sym == "" ? "no such position" : Why(ok)), (long)tk); }
         else if(cmd == "MODORD" && n >= 6) { if(!CanTrade(id)) continue; ulong tk = (ulong)StringToInteger(a[2]); string sym = OrderSelect(tk) ? OrderGetString(ORDER_SYMBOL) : ""; bool ok = sym != "" && trade.OrderModify(tk, Px(sym, StringToDouble(a[3])), Px(sym, StringToDouble(a[4])), Px(sym, StringToDouble(a[5])), ORDER_TIME_GTC, 0); Res(id, ok, "order #" + a[2] + ": " + (sym == "" ? "no such order" : Why(ok)), (long)tk); }
         else if(cmd == "CANCEL" && n >= 3) { if(!CanTrade(id)) continue; bool ok = trade.OrderDelete((ulong)StringToInteger(a[2])); Res(id, ok, "cancel #" + a[2] + ": " + Why(ok), StringToInteger(a[2])); }
         else if(cmd == "CANCELALL" && n >= 3) DoCancelAll(id, a[2]);
         else if(cmd == "BE" && n >= 5) DoBreakeven(id, a[2], a[3], (int)StringToInteger(a[4]));
        }
     }
  }

void OnTimer()
  {
   if(!Connect()) return;
   ReadApp();                                // commands every 100 ms
   beats++;
   if(beats % 2 == 0) SendAll();              // the state every 200 ms
   if(beats % 300 == 0) SendSymbols();        // every 30 s: new Market Watch symbols too
  }
//+------------------------------------------------------------------+
