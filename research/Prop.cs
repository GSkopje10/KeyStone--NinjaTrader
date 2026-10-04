// Research harness — prop account simulator (Lucid-style), replaying REAL days in their real order.
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PDay { public DateTime Day; public double Pnl, Worst, Best; public bool Traded; }   // $ for 1 contract

public sealed class PRules
{
    public string Name = "50K FLEX"; public double Cost = 150, Target = 3000, Dd = 2000, LockAt = 100; public bool Intraday = false;
    public double EvalConsistency = 50; public int MinEvalDays = 2;
    public int PayDays = 5; public double PayDayMin = 150, PayPct = 50, PayCap = 2000, PayMin = 500, Split = 90; public int MaxPayouts = 5;
    public bool LockAfterPayout = true; public double FundConsistency = 0;
    public string PayMode = "CYCLE";   // CYCLE (Flex: N qualifying days, % of profit up to a cap) • DAILY (LucidDaily: anything above the buffer, any day)
    public bool FundIntraday = false; public double PayWhenBal = 0;   // ask for a payout only once the balance is at least this (0 = as soon as allowed)
    public PRules Copy() { return (PRules)MemberwiseClone(); }
}

public sealed class PLife { public bool Passed; public int EvalDays, FundDays, Payouts; public double Cash, Spent; public bool Blown, Graduated; public int EndIndex; }

public sealed class PAcct
{
    public PRules R; public double Es, Fs, Bal, Hwm, Best, Cycle, CycleBest; public bool Funded, Locked, Dead, Passed, Graduated; public int Qual, EvalDays, FundDays, Payouts; public double Cash;
    public PAcct(PRules r, double es, double fs) { R = r; Es = es; Fs = fs; }
    // one day; returns cash paid out today
    public double Step(PDay d)
    {
        if (Dead || !d.Traded) return 0;
        var r = R; double sz = Funded ? Fs : Es, pnl = d.Pnl * sz, worst = Math.Min(0, d.Worst * sz);
        bool intra = Funded ? r.FundIntraday : r.Intraday;
        if (intra) Hwm = Math.Max(Hwm, Bal + Math.Max(0, d.Best * sz));
        double mll = Locked ? r.LockAt : Math.Min(Hwm - r.Dd, r.LockAt);
        if (Bal + worst <= mll || Bal + pnl <= mll) { Dead = true; if (Funded) FundDays++; else EvalDays++; return 0; }
        Bal += pnl; if (Funded) FundDays++; else EvalDays++;
        Hwm = Math.Max(Hwm, Bal);
        if (!Funded)
        {
            Best = Math.Max(Best, pnl);
            bool consistent = r.EvalConsistency <= 0 || Best <= r.EvalConsistency / 100.0 * Bal;
            if (Bal >= r.Target && EvalDays >= r.MinEvalDays && consistent) { Funded = true; Passed = true; Bal = 0; Hwm = 0; Locked = false; Qual = 0; Cycle = 0; CycleBest = 0; }
            return 0;
        }
        Cycle += pnl; CycleBest = Math.Max(CycleBest, pnl);
        if (pnl >= r.PayDayMin) Qual++;
        if (r.PayMode == "DAILY" || r.PayMode == "BUFFER")
        {
            // only what is above the buffer (start + drawdown + 100) can be withdrawn; BUFFER adds a cycle profit goal + consistency (LucidPro)
            double g2 = Math.Min(r.PayCap > 0 ? r.PayCap : double.MaxValue, Bal - (r.Dd + r.LockAt));
            bool ok = Cycle > 0 && g2 >= r.PayMin && Bal >= r.PayWhenBal;
            if (r.PayMode == "BUFFER") ok = ok && Cycle >= r.PayDayMin && (r.FundConsistency <= 0 || CycleBest <= r.FundConsistency / 100.0 * Cycle);
            if (ok) { Bal -= g2; Payouts++; Cycle = 0; CycleBest = 0; Locked = true; double c = g2 * r.Split / 100.0; Cash += c; if (r.MaxPayouts > 0 && Payouts >= r.MaxPayouts) { Graduated = true; Dead = true; } return c; }
            return 0;
        }
        if (Bal < r.PayWhenBal) return 0;
        bool fcons = r.FundConsistency <= 0 || CycleBest <= r.FundConsistency / 100.0 * Cycle;
        if (Qual >= r.PayDays && Cycle > 0 && Bal > 0 && fcons)
        {
            double g = Math.Min(r.PayCap, Bal * r.PayPct / 100.0);
            if (g >= r.PayMin)
            {
                Bal -= g; Payouts++; Qual = 0; Cycle = 0; CycleBest = 0; if (r.LockAfterPayout) Locked = true;
                double c = g * r.Split / 100.0; Cash += c;
                if (r.MaxPayouts > 0 && Payouts >= r.MaxPayouts) { Graduated = true; Dead = true; }
                return c;
            }
        }
        return 0;
    }
}

public static class Prop
{
    public sealed class Summary { public int N; public double Pass, AvgEvalDays, Payouts, Cash, Net, P0Pay, AvgLifeDays, Grad, DaysPerEval; }

    public static Summary Each(List<PDay> days, DateTime from, DateTime to, PRules r, double es, double fs, int maxDays = 500)
    {
        var s = new Summary(); var passDays = new List<int>(); int zeroPay = 0;
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].Day < from || days[i].Day >= to || !days[i].Traded) continue;
            var a = new PAcct(r, es, fs); int k = 0;
            for (; k < maxDays && !a.Dead; k++) a.Step(days[(i + k) % days.Count]);
            s.N++;
            if (a.Passed) { s.Pass++; passDays.Add(a.EvalDays); }
            s.Payouts += a.Payouts; s.Cash += a.Cash; s.Net += a.Cash - r.Cost; if (a.Payouts == 0) zeroPay++; s.AvgLifeDays += k; if (a.Graduated) s.Grad++;
        }
        if (s.N == 0) return s;
        s.Pass = 100 * s.Pass / s.N; s.AvgEvalDays = passDays.Count > 0 ? passDays.Average() : 0; s.Payouts /= s.N; s.Cash /= s.N; s.Net /= s.N; s.P0Pay = 100.0 * zeroPay / s.N; s.AvgLifeDays /= s.N; s.Grad = 100 * s.Grad / s.N;
        return s;
    }

    // A running business on the real calendar: `slots` accounts all copying the same signal; an account that dies (or
    // graduates) is replaced by a new evaluation the next day. Cash = payouts − evaluation fees.
    public sealed class Program { public double Spent, Cash, Net, WorstNet, PerMonth; public int Bought, Passed, Payouts, Months; public List<double> Monthly = new List<double>(); }
    public static Program Run(List<PDay> days, DateTime from, DateTime to, PRules r, double es, double fs, int slots, int maxFunded = 5)
    {
        var p = new Program(); var accts = new List<PAcct>(); double net = 0; var month = DateTime.MinValue; double mStart = 0;
        Func<PAcct> buy = () => { p.Bought++; p.Spent += r.Cost; net -= r.Cost; return new PAcct(r, es, fs); };
        for (int s = 0; s < slots; s++) accts.Add(buy());
        foreach (var d in days)
        {
            if (d.Day < from || d.Day >= to) continue;
            var m = new DateTime(d.Day.Year, d.Day.Month, 1);
            if (m != month) { if (month != DateTime.MinValue) p.Monthly.Add(net - mStart); month = m; mStart = net; }
            for (int s = 0; s < accts.Count; s++)
            {
                bool wasFunded = accts[s].Funded; double c = accts[s].Step(d); net += c; p.Cash += c;
                if (!wasFunded && accts[s].Funded) p.Passed++;
                if (c > 0) p.Payouts++;
                if (accts[s].Dead) accts[s] = buy();
            }
            p.WorstNet = Math.Min(p.WorstNet, net);
        }
        p.Monthly.Add(net - mStart); p.Months = p.Monthly.Count; p.Net = net; p.PerMonth = p.Months > 0 ? net / p.Months : 0;
        return p;
    }
}
