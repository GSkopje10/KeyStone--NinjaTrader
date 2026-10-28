// SpasiX — SEVERAL MT5 ACCOUNTS at once: each terminal's SpasiXBridge connects to the same port (the hub, Bridge.cs) and every account gets its
// own link — and its own LIVE / PROP tab named after it (the firm + login, or the name you give it on the HOME screen). Each tab is a whole
// SpasiX: its own ARM, chart, positions, 🤖 AUTO, 📅 DAY, 🔍 DECODE, tracker, PROP rules — an order from one tab reaches only its own terminal.
using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace KeystoneMT5
{
    public sealed partial class MainWindow
    {
        public Mt5Bridge Link { get { return live; } }
        string shownTitle = "";

        // the account's name: yours (set on HOME) or the firm from MT5's server name + the login; "" while the account is not known yet
        public static string AccountLabel(Mt5Bridge l)
        {
            if (l == null || l.Key == "") return "";
            var own = SGet("acct.name." + l.Key, ""); if (own != "") return own;
            long login; string server; lock (l.State.Gate) { login = l.State.Account.Login; server = l.State.Account.Server ?? ""; }
            if (login == 0) { var k = l.Key.Split('@'); long.TryParse(k[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out login); server = k.Length > 1 ? k[1] : ""; }
            return Firm(server) + " " + login.ToString(CultureInfo.InvariantCulture);
        }
        // "ICMarketsSC-MT5-2" → "ICMarkets", "FTMO-Server3" → "FTMO", "FundedNext-Live" → "FundedNext"
        public static string Firm(string server)
        {
            var f = (server ?? "").Split(new[] { '-', ' ', '_', '.' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "MT5";
            foreach (var cut in new[] { "SC", "Server", "Live", "Demo", "Trade", "MT5" }) if (f.Length > cut.Length + 2 && f.EndsWith(cut, StringComparison.OrdinalIgnoreCase)) f = f.Substring(0, f.Length - cut.Length);
            return f;
        }
        public static void Rename(Window owner, Mt5Bridge l)
        {
            if (l == null || l.Key == "") return; var now = AccountLabel(l);
            var v = Sx.Prompt(owner, "✎ Name this account", "The name of " + l.Key.Replace("@", " @ ") + " on its tab and on HOME (empty = the firm + login):", SGet("acct.name." + l.Key, now));
            if (v == null) return; SPut("acct.name." + l.Key, v.Trim()); Shell.Repaint();
        }

        // ⛔ from HOME: every ARMED account's positions close at the market and its 🤖 AUTO stops; a disarmed account is left as it is (and listed)
        public bool EmergencyClose(out string line)
        {
            var a = AccountLabel(live); if (a == "") a = Mode;
            if (Virtual) { line = a + ": not an MT5 account"; return false; }
            if (auto.Running) auto.Stop("⛔ close everything (HOME)");
            int n; bool can; lock (broker.State.Gate) { n = broker.State.Positions.Count; can = broker.State.CanTrade; }
            if (n == 0) { line = a + ": nothing open"; return true; }
            if (!armed || !can) { line = a + ": NOT closed — " + (!can ? "MT5 does not allow trading" : "not ARMED") + " (" + n + " open)"; return false; }
            var id = broker.Command("CLOSEALL", "", "ALL"); if (id != null) sent[id] = "⛔ close everything (HOME)";
            line = a + (id != null ? ": closing " + n + " position" + (n == 1 ? "" : "s") : ": MT5 is not connected"); return id != null;
        }

        // the tab and the window title follow the account once it is known (or renamed)
        void AccountTitle()
        {
            if (!Shell.PerAccount(Mode)) return; var t = Shell.TitleOf(this); if (t == shownTitle) return; shownTitle = t;
            Title = "SpasiX • " + t; Shell.Repaint();
        }
    }
}
