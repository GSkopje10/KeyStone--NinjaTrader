//+------------------------------------------------------------------+
//| SpasiXBridge.mq5 — connects MetaTrader 5 to the SpasiX MT5 app   |
//| app on this PC (127.0.0.1). READ ONLY — it sends the account,    |
//| the prices, the open positions / orders and candles. It contains |
//| no trading function: it cannot open, change or close anything.   |
//+------------------------------------------------------------------+
#property copyright "SpasiX"
#property version   "2.00"
#property description "SpasiX MT5 bridge (read only: account, prices, positions, candles)"

input string AppHost = "127.0.0.1";   // the SpasiX MT5 app (this PC)
input int    AppPort = 47123;         // its port
input string Symbols = "";            // symbols to stream, e.g. XAUUSD,US30 (empty = every symbol in Market Watch)

int      sock    = INVALID_HANDLE;
datetime lastTry = 0;
string   inbuf   = "";
int      beats   = 0;

int OnInit()
  {
   EventSetMillisecondTimer(200);
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
      else Comment("SpasiX: waiting for the SpasiX app — start SpasiXMT5.exe (error ", e, ")");
      return(false);
     }
   Comment("SpasiX: connected to the app");
   SendLine("HELLO|2|" + TerminalInfoString(TERMINAL_COMPANY) + "|" + IntegerToString(TerminalInfoInteger(TERMINAL_BUILD)));
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
     }
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
      case 60: return(PERIOD_H1); case 240: return(PERIOD_H4); case 1440: return(PERIOD_D1); case 10080: return(PERIOD_W1);
     }
   return(PERIOD_H1);
  }

// candles for the app's chart: BARS|symbol|minutes|n, n × B|open time|o|h|l|c|tick volume, BARSEND|symbol|minutes (oldest first)
void SendBars(string sym, int m, int count)
  {
   MqlRates r[]; ArraySetAsSeries(r, false);
   int got = CopyRates(sym, TfOf(m), 0, MathMin(MathMax(count, 1), 5000), r); if(got < 0) got = 0;   // 0 = MT5 is still downloading: the app asks again
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

// messages from the app (no trading command exists)
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
     }
  }

void OnTimer()
  {
   if(!Connect()) return;
   ReadApp();
   SendAll();
   if(++beats % 150 == 0) SendSymbols();   // every 30 s: new Market Watch symbols too
  }
//+------------------------------------------------------------------+
