// Keystone Arc 5M Research Lab
// Independent research-only NinjaTrader AddOn + chart study.
// It intentionally references no Apex, Nexus, Aurora, FVG, Account, Order, or execution APIs.

#region Using declarations
using System;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript
{
    // All Keystone Arc state is private to this module. Existing products cannot read or mutate it.
    public sealed class KeystoneArcBar
    {
        public DateTime Time;
        public string Symbol;
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public long Volume;
    }

    public sealed class KeystoneArcEvent
    {
        public string Id;
        public string Symbol;
        public string SetupClass;       // BH, FVG, or DT
        // LONG and SHORT are historical setup directions, not an order instruction.
        public string Direction = "LONG";
        public string StrengthTag;      // BASE, WICK, AGGR
        public DateTime ReferenceTime;
        public DateTime TriggerTime;
        public DateTime EntryTime;
        public double Entry;
        public double Stop;
        public double Target;
        // Prop scenarios use whole micros; a personal CFD/spot index model may use fractional
        // lots. The value is a historical sizing input only and never reaches a broker.
        public double Quantity;
        public double StopDistance;
        public string RiskModel;
        public DateTime ExitTime;
        public double ExitPrice = double.NaN;
        public string Outcome;          // WIN, LOSS, SESSION EXIT, OPEN
        public double GrossPnl;
        // Optional evaluation-stage result for the same historical path.  The standard Outcome
        // remains the funded/base parameter result; allocation selects this resolved result only
        // while the assigned virtual slot is still in its evaluation stage.
        public string EvaluationOutcome;
        public DateTime EvaluationExitTime;
        public double EvaluationExitPrice = double.NaN;
        public double EvaluationGrossPnl;
        public double EvaluationTarget = double.NaN;
        public double EvaluationStop = double.NaN;
        // Auditable path statistics from the exact 1-minute series when it is validated.
        // If the run falls back to setup bars, they remain conservative bar-range statistics.
        public double PeakAfterEntry = double.NaN;
        public double TroughAfterEntry = double.NaN;
        public int TargetTouched;
        public int StopTouched;
        public int SessionOrder;
        public double FvgLower = double.NaN;
        public double FvgUpper = double.NaN;
        public DateTime FvgFormedTime = DateTime.MinValue;
        public string AssignedVirtualAccount;
        public string SkipReason;
        public string ConfigurationKey;
        // Internal ACCEPTED state means detector-qualified and eligible for a virtual pool.
        // Every detector-qualified event begins eligible for the virtual pool. The review screen
        // is optional and can exclude a specific row; it is not a second approval requirement.
        public string ReviewState = "ACCEPTED"; // ACCEPTED (eligible), REJECTED, FLAGGED
        public string ReviewNote = string.Empty;
    }

    // One row is an auditable scenario, never a trading instruction.  It keeps setup discovery
    // separate from virtual-account allocation and the optional illustrative lifecycle.
    public sealed class KeystoneArcComparisonRow
    {
        public string Symbol;
        public int SetupMinutes;
        public int PoolSize;
        public string StartMode;
        public string DataMethod;
        public int Setups;
        public int Wins;
        public int Losses;
        public int SessionExits;
        public double AllOutcomeGross;
        public int Assigned;
        public int Skipped;
        public double AssignedGross;
        public int EvaluationPassed;
        public int Funded;
        public int Payouts;
        public double PayoutCash;
        public double EvaluationCost;
        public string Note;

        public override string ToString()
        {
            int resolved = Wins + Losses;
            string winRate = resolved == 0 ? "n/a" : (Wins * 100.0 / resolved).ToString("0.0", CultureInfo.InvariantCulture) + "%";
            if (string.Equals(StartMode, "LIVE ACCOUNT", StringComparison.OrdinalIgnoreCase))
                return Symbol.PadRight(4) + " | " + SetupMinutes.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "M | setups " + Setups.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                    " | W/L " + Wins.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "/" + Losses.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                    " | " + winRate.PadLeft(6) + " | final " + AssignedGross.ToString("C0", CultureInfo.InvariantCulture).PadLeft(10) + " | ONE LIVE ACCOUNT";
            return Symbol.PadRight(4) + " | " + SetupMinutes.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "M | setups " + Setups.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                " | W/L " + Wins.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "/" + Losses.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                " | " + winRate.PadLeft(6) + " | gross " + AllOutcomeGross.ToString("C0", CultureInfo.InvariantCulture).PadLeft(10) +
                " | pool " + PoolSize.ToString(CultureInfo.InvariantCulture).PadLeft(2) + " | " + StartMode;
        }
    }

    public sealed class KeystoneArcOptimizationRow
    {
        public string Scope;
        public int SetupMinutes;
        public double TargetDollars;
        public double StopDollars;
        public string TargetLabel;
        public string StopLabel;
        public int Detected;
        public int FinalTrades;
        public int Wins;
        public int Losses;
        public int SessionExits;
        public double FinalPnl;
        public double MaximumDrawdown;
        public int PositiveDays;
        public int TotalDays;
        public int EvaluationPasses;
        public int CurrentlyFunded;
        public int Payouts;
        public double PayoutCash;
        public double EvaluationCost;
    }

    // These rows are derived only from the immutable per-account daily snapshots below.  They
    // make simultaneous payout dates, replacement purchases, and carry-forward balances visible
    // without confusing cumulative payout totals with what happened on one date.
    public sealed class KeystoneArcFirstPayoutTiming
    {
        public bool HasPayout;
        public DateTime InitialSlotStart = DateTime.MinValue;
        public DateTime FirstPayoutDate = DateTime.MinValue;
        public DateTime LifecycleStartForFirstPayout = DateTime.MinValue;
        public int CalendarDaysFromInitial;
        public int RecordedSessionsFromInitial;
        public int CalendarDaysFromLifecycleStart;
        public int RecordedSessionsFromLifecycleStart;
    }

    // One row per virtual slot, including rows without a payout. This prevents a first payout
    // amount/date from being confused with lifetime withdrawals or the current stage balance.
    public sealed class KeystoneArcFirstReturnRow
    {
        public string Account;
        public DateTime InitialSlotStart = DateTime.MinValue;
        public bool HasPayout;
        public DateTime FirstPayoutDate = DateTime.MinValue;
        public double FirstPayoutGross;
        public double FirstPayoutCashAfterShare;
        public double EvaluationCostThroughFirstPayout;
        public double FullNetReturnAfterAllCosts;
        public int CalendarDaysFromInitial;
        public int RecordedSessionsFromInitial;
        public string StateAtFirstPayout;
    }

    public sealed class KeystoneArcPayoutCycleRow
    {
        public int CycleNumber;
        public DateTime Day;
        public int PayoutAccounts;
        public int PayoutCycles;
        public double GrossWithdrawals;
        public double NetCashAfterShare;
        public int EvaluationPurchases;
        public double EvaluationCostOnDate;
        public double NetCashAfterEvaluationCost;
        // Portfolio cumulative values at this payout date make the initial $120-per-evaluation
        // spend visible even when the purchase occurred before the payout date.
        public double CumulativePayoutCashThroughDate;
        public double CumulativeEvaluationCostThroughDate;
        public double CumulativeFullNetCashAfterAllCosts;
        public int FundedAtClose;
        public int FundedBelowPayoutGoal;
        public int EvaluationInProgress;
        public int ReplacementNextSession;
        public int TerminalBlown;
        public readonly List<string> PayoutContributors = new List<string>();
    }

    public sealed class KeystoneArcBalanceMatrixRow
    {
        public int CycleNumber;
        public DateTime Day;
        public string Account;
        public string Status;
        public double BalanceBefore;
        public double GrossPayout;
        public double CarryForwardBalance;
        public double DayPnl;
    }

    public sealed class KeystoneArcPayoutAuditDay
    {
        public string Account;
        public KeystoneArcAccountDay DaySnapshot;
        public int PayoutDelta;
        public double PayoutGrossDelta;
        public double PayoutCashDelta;
        public int EvaluationPurchaseDelta;
        public double EvaluationCostDelta;
    }

    // This is a reporting/guardrail summary of a virtual-pool scenario. It never represents a
    // brokerage balance or available cash. "Payout cash" is the modeled withdrawal after the
    // configured account share; the values exist so a capped-reinvestment test is reproducible.
    public sealed class KeystoneArcCapitalPolicySummary
    {
        public bool GateEnabled;
        public int InitialEvaluationPurchases;
        public double InitialEvaluationInvestment;
        public int ReplacementEvaluationPurchases;
        public double ReplacementEvaluationCost;
        public double PayoutCashAfterShare;
        public double PayoutCashAfterInitialInvestment;
        public double ReplacementCashAvailable;
        public double TotalEvaluationCost;
        public double FullNetCashAfterAllCosts;
        public int BenchedReplacementSlots;
        public int PendingReplacementSlots;
        public double CashRequiredForPendingReplacements;
        public double ReplacementCashShortfall;
        // These are date-audited portfolio milestones, not account balances.  They distinguish
        // a payout-funded replacement policy from a continuous-replenishment test where later
        // evaluation purchases can occur before any modeled payout is received.
        public bool FirstPayoutReached;
        public DateTime FirstPayoutDate = DateTime.MinValue;
        public double PayoutCashThroughFirstPayoutDate;
        public double InvestmentThroughFirstPayoutDate;
        public int EvaluationPurchasesThroughFirstPayoutDate;
        public int ReplacementPurchasesThroughFirstPayoutDate;
        public int ReplenishedSlotsThroughFirstPayoutDate;
        public double NetCashThroughFirstPayoutDate;
        public bool ProfitabilityReached;
        public DateTime ProfitabilityDate = DateTime.MinValue;
        public double PayoutCashThroughProfitability;
        public double InvestmentThroughProfitability;
        public int EvaluationPurchasesThroughProfitability;
        public int ReplacementPurchasesThroughProfitability;
        public double NetCashAtProfitability;
    }

    // Deterministic evidence notes drawn only from the loaded selected-range run. These are not
    // predictions, optimization claims, or financial advice; they make the report's observable
    // tradeoffs easier to inspect before a separate validation run is considered.
    internal sealed class KeystoneArcResearchFinding
    {
        public string Title;
        public string Detail;
        public Brush Accent;
    }

    internal sealed class KeystoneArcCapitalPolicyDay
    {
        public DateTime Day;
        public double PayoutCash;
        public int ReplacementPurchases;
        public readonly HashSet<string> ReplenishedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class KeystoneArcVirtualAccount
    {
        public string Name;
        // Optional modeled firm grouping. These labels are scenario data only; this AddOn never
        // connects to or manages a firm account.
        public string PropFirmCode = string.Empty;
        public int PropFirmSlot;
        public DateTime FundedSinceDate = DateTime.MinValue;
        public DateTime FirstPayoutDate = DateTime.MinValue;
        public DateTime FirstEvaluationPassDate = DateTime.MinValue;
        public DateTime LastPayoutDate = DateTime.MinValue;
        // A passed evaluation can wait here when its modeled firm has reached the selected funded
        // capacity. It remains a paid evaluation, not a failure or replacement purchase.
        public bool FundedCapPending;
        public DateTime ActiveDay = DateTime.MinValue;
        public double DayPnl;
        public double EvalDayCredit;
        public double EvalBestPositiveDay;
        public double EvaluationBalance;
        public double FundedBalance;
        public double TotalPnl;
        public double PeakPnl;
        // Starting balance is used only by the single-account range P/L view.  It is never a
        // real account balance and no account API is accessed.
        public double StartingBalance;
        public double EvaluationCost;
        public int Trades;
        public int Wins;
        public int Losses;
        public bool DayLocked;
        public bool EvaluationPassed;
        public bool Funded;
        public int ConsecutiveEvalQualifyingDays;
        public int PositiveDays;
        public int FundedPositiveDays;
        public int EvaluationPasses;
        public int FailedEvaluations;
        public int FailedFunded;
        // A blown slot cannot receive another setup. Evaluation-first slots remain blown until
        // the next session, when a replacement evaluation is explicitly purchased. Direct-funded
        // slots stay terminally blown for the rest of the study.
        public bool Blown;
        // A failed evaluation or funded account remains unavailable for the rest of that session.
        // The same virtual slot purchases one replacement evaluation at the next session open.
        public bool ReplacementPending;
        public bool ReplacementFromFunded;
        // When the optional capital gate is active, this slot stays benched after a failure until
        // modeled payout cash can fund the next evaluation without using new external cash.
        public bool ReplacementBudgetBlocked;
        public int EvaluationPurchases;
        public int Payouts;
        // Gross withdrawal reduces the illustrative funded balance; net cash applies the
        // user-entered account share. Keeping both prevents payout reports from conflating them.
        public double PayoutGrossWithdrawn;
        public double PayoutCash;
        // Starts are explicit audit values. Initial slots begin at the first requested session;
        // evaluation replacements begin only on the next session after their failure.
        public DateTime InitialLifecycleStart = DateTime.MinValue;
        public DateTime CurrentLifecycleStart = DateTime.MinValue;
        // The last date receiving an assigned event remains separate from the lifecycle start.
        // A direct-funded terminal slot stops here; later pool dates must not be shown as if the
        // account remained active after its blowout.
        public DateTime LastAssignedDate = DateTime.MinValue;
        public DateTime LastBlowoutDate = DateTime.MinValue;
        public DateTime TerminalLifecycleEnd = DateTime.MinValue;
        public DateTime FreeAt = DateTime.MinValue;
        public string LastState = "EVALUATION READY";
        // One immutable row per completed account-day. These rows make carry, qualification,
        // pass/fail, and payout progress inspectable instead of inferring them from the final balance.
        public readonly List<KeystoneArcAccountDay> DayHistory = new List<KeystoneArcAccountDay>();
    }

    public sealed class KeystoneArcAccountDay
    {
        public DateTime Day;
        public double DayPnl;
        public int TradesAfter;
        public int WinsAfter;
        public int LossesAfter;
        public double BalanceBefore;
        public double BalanceAfter;
        public double CostDelta;
        public double PayoutGrossDelta;
        public double PayoutCashDelta;
        public int EvaluationPurchaseDelta;
        public DateTime EvaluationPassDateAfter = DateTime.MinValue;
        public DateTime FirstPayoutDateAfter = DateTime.MinValue;
        public bool DayLocked;
        public bool EvalQualifyingDay;
        public bool FundedQualifyingDay;
        public double EvaluationBalanceAfter;
        public double FundedBalanceAfter;
        public int EvaluationQualifyingDaysAfter;
        public int ConsecutiveEvalQualifyingDaysAfter;
        public int FundedQualifyingDaysAfter;
        public int PayoutsAfter;
        public double PayoutGrossAfter;
        public double PayoutCashAfter;
        public int EvaluationPurchasesAfter;
        public int EvaluationPassesAfter;
        public double EvaluationCostAfter;
        public bool FundedAfter;
        public DateTime LifecycleStartAfter = DateTime.MinValue;
        public bool ReplacementPendingAfter;
        public bool FundedCapPendingAfter;
        public string PropFirmAfter;
        public int FundedSlotsInFirmAfter;
        public bool BlownAfter;
        public string StateAfterClose;
    }

    // Purely historical-model statistics. They do not predict future drawdown and never access
    // a broker or a real account balance.
    public sealed class KeystoneArcRiskSequenceStats
    {
        public int WorstNegativeSetupStreak;
        public double WorstNegativeSetupStreakPnl;
        public int WorstNegativeDayStreak;
        public double WorstNegativeDayStreakPnl;
        public double StartingBalance;
        public double EndingBalance;
        public double LowestBalance;
        public double MaximumDrawdown;
    }

    public sealed class KeystoneArcRunConfig
    {
        public string MnqName = "MNQ AUTO";
        public string MgcName = "MGC AUTO";
        public string Scope = "MNQ"; // MNQ, MGC, BOTH
        // Prop-only historical model. This AddOn never reads a broker account or submits an order.
        public string AccountPath = "PROP";
        public DateTime Start;
        public DateTime End;
        public int OneDayMode = 1;
        public int SetupMinutes = 5;
        // BH remains the default and existing research mode. ASIAN75 is a separate historical
        // 1-minute strategy path; it never changes the BH detector's entry rule.
        public string StrategyCode = "BH";
        public int EnableBh = 1;
        public int EnableFvg = 1;
        // BB/SS/SL/BS applies the selected MNQ and MGC side filters independently. It is
        // deliberately a research selector, so no broker or account path consumes it.
        public string DirectionMode = "BB";
        // ALL = the base BH rule. STRONGER applies only the enabled user-entered aggression
        // thresholds before the bullish reference. A zero threshold disables that criterion.
        public string BhAggressionFilter = "ALL";
        public int MnqStrongRedCandles = 3;
        public double MnqStrongDeclinePoints = 0;
        public int MgcStrongRedCandles = 0;
        public double MgcStrongDeclineDollars = 10;
        // ANY = at least one enabled criterion must pass; ALL = every enabled criterion must pass.
        public string BhStrongCombine = "ANY";
        public int MnqStart = 930;
        public int MgcStart = 800;
        public int EndTime = 1555;
        // INSTRUMENT_DEFAULT, ALL_ELIGIBLE, ASIAN, LONDON, NY_OPEN, NY_AFTER_0930, CUSTOM
        public string SessionMode = "NY_AFTER_0930";
        public int CustomStart = 930;
        public int Quantity = 10;
        public double TargetDollars = 1000;
        public double StopDollars = 500;
        public int StopFirstOnSameMinute = 1;
        public string StopMode = "STANDARD";
        public double MnqStopOffsetPoints = 5;
        public double MgcStopOffsetPoints = 1;
        // LIVE ACCOUNT is a manually entered personal-account price model. No IC Markets
        // contract specification is assumed: the user supplies both lot size and cash value.
        public double PersonalLotSize = 1.0;
        public double MnqCashPerPointPerLot = 1.0;
        public double MgcCashPerPointPerLot = 1.0;
        public double MnqTargetMove = 10.0;
        public double MgcTargetMove = 10.0;
        public double MnqStandardStopMove = 5.0;
        public double MgcStandardStopMove = 5.0;
        public double PersonalMaxRiskDollars = 500.0;
        public int BreakEvenEnabled;
        public double BreakEvenTriggerMove = 0.0;
        // Disabled if the requested 1-minute series cannot reproduce the selected setup-bar source.
        public int OutcomeModelEnabled = 1;
        // A small timestamp normalization is allowed only when aggregated OHLC values prove the bars are the same series.
        public int MnqOutcomeTimeOffsetMinutes = 0;
        public int MgcOutcomeTimeOffsetMinutes = 0;
        public string MnqOutcomeSource = "VERIFIED 1M";
        public string MgcOutcomeSource = "VERIFIED 1M";
        public int PoolSize = 10;
        // Copy pool is a separate historical allocation mode: each active modeled account
        // receives the same eligible BH event rather than rotating to the next free account.
        public int CopyTradingPool;
        // Default false preserves one setup per account per session. When enabled, an account
        // may receive additional BH setups in the same session until its configured daily
        // profit/loss lock, total drawdown, or lifecycle state stops it.
        public int AllowMultipleSetupsPerDay;
        public double DailyGoal = 1000;
        public double DailyLoss = 1000;
        public int EvaluationEnabled = 1;
        public double EvaluationTarget = 3000;
        public double EvaluationDailyCreditCap = 1500;
        public double EvaluationConsistencyPercent = 0;
        public double EvaluationFailure = 2000;
        // When enabled, these apply only until the evaluation passes. Funded slots continue to
        // use the main target/stop inputs chosen at the top of the configuration.
        public int EvaluationStageTradeRulesEnabled;
        public double EvaluationTradeTargetDollars = 500;
        public double EvaluationTradeStopDollars = 500;
        // Zero disables the respective daily-loss lock. Total drawdown is kept separate so a
        // user can model a firm with a daily limit, a total limit, both, or neither.
        public double EvaluationDailyLoss = 0;
        public double FundedDailyLoss = 0;
        public double FundedFailure = 2000;
        public int MinimumPositiveDays = 2;
        // A day counts toward the configurable evaluation or funded payout day rule only at or above this amount.
        public double MinimumQualifyingDayProfit = 150;
        public double PayoutThreshold = 4000;
        public int PayoutDaysRequired = 5;
        public double PayoutAmount = 2000;
        // Illustrative only: the account-holder share of a modeled withdrawal, entered as 90
        // for a 90/10 split or 80 for an 80/20 split. It does not alter the account balance.
        public double PayoutProfitSharePercent = 100;
        public double EvaluationCost = 120;
        // Optional historical-scenario capital rule. Initial evaluations are purchased at the
        // selected range start. Replacement evaluations wait on the bench until modeled payout
        // cash after account share can pay for them without new money beyond that initial spend.
        public int ReplacementsRequirePayoutFunding;
        // Optional capacity model: evaluation slots are grouped P1…Pn in blocks of the selected
        // size and each group can have no more than the selected number of funded slots. Passed
        // evaluations above the cap wait on the firm bench until a funded failure frees space.
        public int FirmFundedCapEnabled;
        public int EvaluationSlotsPerFirm = 10;
        public int MaxFundedPerFirm = 5;
        public double PropStartingBalance = 0;
        public double PersonalStartingBalance = 0;
        // Asian cycle backtest inputs. Each selected instrument enters at the exact configured
        // session opening minute, reverses after its own limit, and increments one micro per
        // completed leg. These are historical-model inputs only.
        public int AsianStartHhmm = 1800;
        public int AsianEndHhmm = 1555;
        public string AsianMnqInitialDirection = "LONG";
        public string AsianMgcInitialDirection = "LONG";
        // CASH = a fixed cash loss per completed leg; PRICE = a fixed price movement per leg.
        public string AsianRiskMode = "CASH";
        public double AsianReversalLossDollars = 75;
        public double AsianMnqReversalPriceMove = 37.5;
        public double AsianMgcReversalPriceMove = 7.5;
        public double AsianCycleTargetDollars = 350;
        // The cycle stop is a distinct combined MNQ + MGC loss boundary. The daily limit remains
        // a final backstop so a user can model both a smaller per-cycle stop and a wider daily cap.
        public double AsianCombinedStopLossDollars = 600;
        public double AsianDailyLossLimitDollars = 2000;
        // A per-instrument cap stops only that instrument after its realized reversal losses
        // reach the configured value. Zero disables this optional cap.
        public double AsianInstrumentStopLossDollars = 300;
        public double AsianMnqInstrumentStopLossDollars = 300;
        public double AsianMgcInstrumentStopLossDollars = 300;
        // When the combined marked cycle reaches this profit, a subsequent return to flat closes
        // the remaining positions. Zero disables the optional combined breakeven lock.
        public double AsianBreakEvenTriggerDollars = 0;
        public int AsianStartingQuantity = 1;
        // Reversals are counted after the initial x1 entry: 3 means x1 → x2 → x3 → x4.
        public int AsianMaxReversalsPerInstrument = 3;
        public int AsianMnqMaxReversals = 3;
        public int AsianMgcMaxReversals = 3;
        // Retained for snapshot compatibility; new runs calculate this as reversals + x1.
        public int AsianMaxTotalLegsPerInstrument = 4;

        public string Snapshot()
        {
            return string.Join("|", new[] {
                Scope, AccountPath ?? string.Empty, MnqName ?? string.Empty, MgcName ?? string.Empty, Start.ToString("yyyyMMdd"), End.ToString("yyyyMMdd"), OneDayMode.ToString(), SetupMinutes.ToString(), StrategyCode ?? string.Empty, EnableBh.ToString(), EnableFvg.ToString(), DirectionMode ?? string.Empty, BhAggressionFilter ?? string.Empty, MnqStrongRedCandles.ToString(), MnqStrongDeclinePoints.ToString("0.00", CultureInfo.InvariantCulture), MgcStrongRedCandles.ToString(), MgcStrongDeclineDollars.ToString("0.00", CultureInfo.InvariantCulture), BhStrongCombine ?? string.Empty, SessionMode, CustomStart.ToString(), MnqStart.ToString(), MgcStart.ToString(), EndTime.ToString(),
                Quantity.ToString(), TargetDollars.ToString("0.00", CultureInfo.InvariantCulture), StopDollars.ToString("0.00", CultureInfo.InvariantCulture), StopMode ?? string.Empty, MnqStopOffsetPoints.ToString("0.00", CultureInfo.InvariantCulture), MgcStopOffsetPoints.ToString("0.00", CultureInfo.InvariantCulture), PersonalLotSize.ToString("0.00", CultureInfo.InvariantCulture), MnqCashPerPointPerLot.ToString("0.00", CultureInfo.InvariantCulture), MgcCashPerPointPerLot.ToString("0.00", CultureInfo.InvariantCulture), MnqTargetMove.ToString("0.00", CultureInfo.InvariantCulture), MgcTargetMove.ToString("0.00", CultureInfo.InvariantCulture), MnqStandardStopMove.ToString("0.00", CultureInfo.InvariantCulture), MgcStandardStopMove.ToString("0.00", CultureInfo.InvariantCulture), PersonalMaxRiskDollars.ToString("0.00", CultureInfo.InvariantCulture), BreakEvenEnabled.ToString(), BreakEvenTriggerMove.ToString("0.00", CultureInfo.InvariantCulture), OutcomeModelEnabled.ToString(), MnqOutcomeTimeOffsetMinutes.ToString(), MgcOutcomeTimeOffsetMinutes.ToString(), MnqOutcomeSource ?? string.Empty, MgcOutcomeSource ?? string.Empty,
                PoolSize.ToString(), DailyGoal.ToString("0.00", CultureInfo.InvariantCulture), DailyLoss.ToString("0.00", CultureInfo.InvariantCulture),
                EvaluationEnabled.ToString(), EvaluationTarget.ToString("0.00", CultureInfo.InvariantCulture), EvaluationDailyCreditCap.ToString("0.00", CultureInfo.InvariantCulture), EvaluationConsistencyPercent.ToString("0.00", CultureInfo.InvariantCulture), EvaluationFailure.ToString("0.00", CultureInfo.InvariantCulture), EvaluationDailyLoss.ToString("0.00", CultureInfo.InvariantCulture), EvaluationStageTradeRulesEnabled.ToString(), EvaluationTradeTargetDollars.ToString("0.00", CultureInfo.InvariantCulture), EvaluationTradeStopDollars.ToString("0.00", CultureInfo.InvariantCulture), FundedDailyLoss.ToString("0.00", CultureInfo.InvariantCulture), FundedFailure.ToString("0.00", CultureInfo.InvariantCulture), MinimumPositiveDays.ToString(), MinimumQualifyingDayProfit.ToString("0.00", CultureInfo.InvariantCulture),
                PayoutThreshold.ToString("0.00", CultureInfo.InvariantCulture), PayoutDaysRequired.ToString(), PayoutAmount.ToString("0.00", CultureInfo.InvariantCulture), EvaluationCost.ToString("0.00", CultureInfo.InvariantCulture), ReplacementsRequirePayoutFunding.ToString(), FirmFundedCapEnabled.ToString(), EvaluationSlotsPerFirm.ToString(), MaxFundedPerFirm.ToString(), PropStartingBalance.ToString("0.00", CultureInfo.InvariantCulture), PersonalStartingBalance.ToString("0.00", CultureInfo.InvariantCulture), AsianStartHhmm.ToString(), AsianEndHhmm.ToString(), AsianMnqInitialDirection ?? string.Empty, AsianMgcInitialDirection ?? string.Empty, AsianRiskMode ?? string.Empty, AsianReversalLossDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianMnqReversalPriceMove.ToString("0.00", CultureInfo.InvariantCulture), AsianMgcReversalPriceMove.ToString("0.00", CultureInfo.InvariantCulture), AsianCycleTargetDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianCombinedStopLossDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianDailyLossLimitDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianInstrumentStopLossDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianMnqInstrumentStopLossDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianMgcInstrumentStopLossDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianBreakEvenTriggerDollars.ToString("0.00", CultureInfo.InvariantCulture), AsianStartingQuantity.ToString(), AsianMaxReversalsPerInstrument.ToString(), AsianMnqMaxReversals.ToString(), AsianMgcMaxReversals.ToString(), AsianMaxTotalLegsPerInstrument.ToString()
            });
        }
    }

    public static class KeystoneArcHub
    {
        private static readonly object Sync = new object();
        private static readonly List<KeystoneArcEvent> latestEvents = new List<KeystoneArcEvent>();
        private static readonly Dictionary<string, List<KeystoneArcEvent>> eventsByFiveMinuteBar = new Dictionary<string, List<KeystoneArcEvent>>();
        private static KeystoneArcRunConfig latestConfig = new KeystoneArcRunConfig();
        public static bool ShowChartMarks = true;
        public static bool ShowChartOutcomeLabels = true;
        public static bool ShowChartWins = true;
        public static bool ShowChartLosses = true;
        public static bool ShowChartSessionExits = false;
        public static bool ShowChartNoEntryData = false;
        public static bool ShowChartBh = true;
        public static bool ShowChartFvg = true;
        public static bool ShowChartDt = true;
        public static bool ShowChartFvgZones = false;
        public static int MaxChartMarks = 250;
        public static long ChartReviewRevision { get; private set; }

        public static void Publish(List<KeystoneArcEvent> events, KeystoneArcRunConfig config)
        {
            lock (Sync)
            {
                latestEvents.Clear();
                eventsByFiveMinuteBar.Clear();
                if (events != null)
                {
                    foreach (KeystoneArcEvent e in events)
                    {
                        KeystoneArcEvent copy = Clone(e);
                        latestEvents.Add(copy);
                        string key = EventKey(copy.Symbol, copy.TriggerTime);
                        List<KeystoneArcEvent> bucket;
                        if (!eventsByFiveMinuteBar.TryGetValue(key, out bucket)) { bucket = new List<KeystoneArcEvent>(); eventsByFiveMinuteBar[key] = bucket; }
                        bucket.Add(copy);
                    }
                }
                latestConfig = config ?? new KeystoneArcRunConfig();
                ChartReviewRevision++;
            }
        }

        public static List<KeystoneArcEvent> Snapshot(string symbol)
        {
            string root = (symbol ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            lock (Sync)
                return latestEvents.Where(x => string.Equals(x.Symbol, root, StringComparison.OrdinalIgnoreCase)).Where(ChartReviewIncludes).Select(Clone).ToList();
        }

        public static List<KeystoneArcEvent> SnapshotAt(string symbol, DateTime fiveMinuteTime)
        {
            lock (Sync)
            {
                List<KeystoneArcEvent> bucket;
                if (!eventsByFiveMinuteBar.TryGetValue(EventKey(symbol, fiveMinuteTime), out bucket)) return new List<KeystoneArcEvent>();
                return bucket.Where(ChartReviewIncludes).Select(Clone).ToList();
            }
        }

        private static string EventKey(string symbol, DateTime time)
        {
            string root = (symbol ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return root.ToUpperInvariant() + "|" + time.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        private static bool ChartReviewIncludes(KeystoneArcEvent e)
        {
            if (e == null) return false;
            if (e.SetupClass == "BH" && !ShowChartBh) return false;
            if (e.SetupClass == "FVG" && !ShowChartFvg) return false;
            if (e.SetupClass == "DT" && !ShowChartDt) return false;
            if (e.Outcome == "WIN") return ShowChartWins;
            if (e.Outcome.StartsWith("LOSS")) return ShowChartLosses;
            if (e.Outcome == "SESSION EXIT") return ShowChartSessionExits;
            if (e.Outcome == "NO ENTRY DATA") return ShowChartNoEntryData;
            return false;
        }

        private static KeystoneArcEvent Clone(KeystoneArcEvent x)
        {
            return new KeystoneArcEvent
            {
                Id = x.Id, Symbol = x.Symbol, SetupClass = x.SetupClass, Direction = x.Direction, StrengthTag = x.StrengthTag,
                ReferenceTime = x.ReferenceTime, TriggerTime = x.TriggerTime, EntryTime = x.EntryTime, Entry = x.Entry, Stop = x.Stop, Quantity = x.Quantity, StopDistance = x.StopDistance, RiskModel = x.RiskModel,
                Target = x.Target, ExitTime = x.ExitTime, ExitPrice = x.ExitPrice, Outcome = x.Outcome, GrossPnl = x.GrossPnl, PeakAfterEntry = x.PeakAfterEntry, TroughAfterEntry = x.TroughAfterEntry, TargetTouched = x.TargetTouched, StopTouched = x.StopTouched,
                SessionOrder = x.SessionOrder, FvgLower = x.FvgLower, FvgUpper = x.FvgUpper, FvgFormedTime = x.FvgFormedTime, AssignedVirtualAccount = x.AssignedVirtualAccount,
                SkipReason = x.SkipReason, ConfigurationKey = x.ConfigurationKey, ReviewState = x.ReviewState, ReviewNote = x.ReviewNote
            };
        }
    }

    public static class KeystoneArcEngine
    {
        private sealed class Asian75Position
        {
            public string Symbol;
            public int LegNumber;
            public int Quantity;
            public int Direction; // +1 long, -1 short
            public DateTime EntryTime;
            public double EntryPrice;
            public bool PendingEntry;
            public DateTime PendingAt;
            public int PendingDirection;
            public int PendingQuantity;
            public bool Halted;
            // Realized loss is held per instrument so an optional instrument cap can stop MNQ
            // without pretending that MGC must also stop. It is never used to synthesize a price.
            public double RealizedPnl;
        }

        private sealed class BullishFvg
        {
            public double Lower;
            public double Upper;
            public DateTime Formed;
            public bool Touched;
            public DateTime PendingReference;
            public double PendingHigh;
            public bool Invalid;
        }

        public static List<KeystoneArcBar> ToSetupBars(List<KeystoneArcBar> oneMinute, string symbol, int minutes)
        {
            var result = new List<KeystoneArcBar>();
            if (oneMinute == null) return result;
            minutes = minutes == 1 ? 1 : 5;
            foreach (var group in oneMinute.Where(x => x != null).OrderBy(x => x.Time)
                .GroupBy(x => new DateTime(x.Time.Year, x.Time.Month, x.Time.Day, x.Time.Hour, (x.Time.Minute / minutes) * minutes, 0)))
            {
                var items = group.ToList();
                if (items.Count == 0) continue;
                result.Add(new KeystoneArcBar
                {
                    Time = group.Key,
                    Symbol = symbol,
                    Open = items[0].Open,
                    High = items.Max(x => x.High),
                    Low = items.Min(x => x.Low),
                    Close = items[items.Count - 1].Close,
                    Volume = items.Sum(x => x.Volume)
                });
            }
            return result;
        }

        public static List<KeystoneArcEvent> DetectAndResolve(List<KeystoneArcBar> oneMinute, KeystoneArcRunConfig cfg)
        {
            return DetectAndResolve(oneMinute, null, cfg);
        }

        public static List<KeystoneArcEvent> DetectAndResolve(List<KeystoneArcBar> oneMinute, List<KeystoneArcBar> directSetupBars, KeystoneArcRunConfig cfg)
        {
            var events = new List<KeystoneArcEvent>();
            if (oneMinute == null || cfg == null) return events;
            if (string.Equals(cfg.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase))
                return DetectAsian75Reversal(oneMinute, cfg);
            foreach (string symbol in oneMinute.Select(x => x.Symbol).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var raw = oneMinute.Where(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Time).ToList();
                var bars = directSetupBars == null ? new List<KeystoneArcBar>() : directSetupBars.Where(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Time).ToList();
                if (bars.Count == 0) bars = ToSetupBars(raw, symbol, cfg.SetupMinutes);
                int order = 0;
                DateTime orderDay = DateTime.MinValue;
                var zones = new List<BullishFvg>();
                for (int i = 2; i < bars.Count; i++)
                {
                    KeystoneArcBar current = bars[i];
                    if (current.Time < cfg.Start) continue;
                    if (current.Time > cfg.End) break;
                    DateTime tradingDay = SessionGroupingDate(current.Time, cfg);
                    if (orderDay != tradingDay)
                    {
                        orderDay = tradingDay;
                        order = 0;
                    }
                    if (!InsideSession(current.Time, symbol, cfg))
                    {
                        if (cfg.EnableFvg == 1) DetectNewBullishFvg(bars, i, zones);
                        continue;
                    }

                    // Resolve a pending FVG reference only on the immediately following selected-timeframe bar.
                    // The retained FVG implementation is a bullish long setup. When this
                    // instrument is set to SELL, it is intentionally excluded until the separate
                    // bearish-FVG contract is enabled rather than falsely labeling it short.
                    var fvgEvent = cfg.EnableFvg == 1 && DirectionAllows(symbol, "LONG", cfg) ? ResolveFvgBreak(current, zones, cfg, symbol, bars, i) : null;
                    KeystoneArcEvent bhEvent = cfg.EnableBh == 1 ? DetectBhBreak(bars, i, cfg, symbol) : null;
                    if (bhEvent != null || fvgEvent != null)
                    {
                        order++;
                        KeystoneArcEvent e = fvgEvent ?? bhEvent;
                        if (bhEvent != null && fvgEvent != null) e.SetupClass = "DT";
                        e.SessionOrder = order;
                        e.StrengthTag = Strength(bars, i);
                        e.ConfigurationKey = cfg.Snapshot();
                        ResolveOutcome(e, raw, cfg, symbol);
                        ResolveEvaluationStageOutcome(e, raw, cfg, symbol);
                        events.Add(e);
                    }

                    if (cfg.EnableFvg == 1) { UpdateFvgTouchState(current, zones, cfg); DetectNewBullishFvg(bars, i, zones); }
                }
            }
            return events.OrderBy(x => x.TriggerTime).ThenBy(x => x.Symbol).ToList();
        }

        // Asian is not a pattern detector.  This receipt deliberately reports only whether the
        // exact configured opening minute exists for every selected instrument.  It never creates
        // a price, substitutes a nearby bar, or changes the historical request path.
        public static string AsianCycleReadinessReceipt(IEnumerable<KeystoneArcBar> source, KeystoneArcRunConfig cfg)
        {
            if (cfg == null) return "ASIAN CYCLE DIAGNOSTIC: configuration unavailable.";
            List<KeystoneArcBar> all = (source ?? Enumerable.Empty<KeystoneArcBar>()).Where(x => x != null).OrderBy(x => x.Time).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
            if (all.Count == 0) return "ASIAN CYCLE DIAGNOSTIC: no direct 1-minute bars were supplied to the cycle engine.";
            string[] symbols = AsianSelectedSymbols(cfg);
            DateTime first = SessionGroupingDate(cfg.Start, cfg).Date;
            DateTime last = SessionGroupingDate(cfg.End, cfg).Date;
            if (last < first) last = first;
            var lines = new List<string>();
            for (DateTime sessionDate = first; sessionDate <= last; sessionDate = sessionDate.AddDays(1))
            {
                DateTime start = AsianDateAtHhmm(sessionDate, cfg.AsianStartHhmm);
                DateTime end = AsianDateAtHhmm(sessionDate, cfg.AsianEndHhmm);
                if (end < start) end = end.AddDays(1);
                var states = new List<string>();
                bool ready = true;
                foreach (string symbol in symbols)
                {
                    List<KeystoneArcBar> symbolBars = all.Where(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).ToList();
                    List<KeystoneArcBar> bars = symbolBars.Where(x => x.Time >= start && x.Time <= end).ToList();
                    KeystoneArcBar opening = bars.FirstOrDefault(x => x.Time == start);
                    if (opening != null)
                    {
                        states.Add(symbol + " exact " + start.ToString("HH:mm") + " OPEN " + opening.Open.ToString("0.####", CultureInfo.InvariantCulture));
                        continue;
                    }
                    ready = false;
                    KeystoneArcBar before = symbolBars.Where(x => x.Time < start).OrderByDescending(x => x.Time).FirstOrDefault();
                    KeystoneArcBar after = symbolBars.Where(x => x.Time > start).OrderBy(x => x.Time).FirstOrDefault();
                    string range = bars.Count == 0 ? "no bars in configured window" : (bars.First().Time.ToString("HH:mm") + "→" + bars.Last().Time.ToString("HH:mm"));
                    states.Add(symbol + " MISSING exact " + start.ToString("HH:mm") + " • window " + range + " • previous " + (before == null ? "none" : before.Time.ToString("yyyy-MM-dd HH:mm")) + " • next " + (after == null ? "none" : after.Time.ToString("yyyy-MM-dd HH:mm")));
                }
                lines.Add(sessionDate.ToString("yyyy-MM-dd") + " • " + (ready ? "CYCLE READY" : "NO CYCLE — exact simultaneous opening bar required") + " • " + string.Join(" | ", states));
            }
            return "ASIAN CYCLE DIAGNOSTIC • entry " + cfg.AsianStartHhmm.ToString("0000") + " ET • selected scope " + cfg.Scope + " • exact opening-bar policy (no nearest-bar substitute)\n" + string.Join("\n", lines);
        }

        private static string[] AsianSelectedSymbols(KeystoneArcRunConfig cfg)
        {
            if (cfg != null && string.Equals(cfg.Scope, "MNQ", StringComparison.OrdinalIgnoreCase)) return new[] { "MNQ" };
            if (cfg != null && string.Equals(cfg.Scope, "MGC", StringComparison.OrdinalIgnoreCase)) return new[] { "MGC" };
            return new[] { "MNQ", "MGC" };
        }

        // Research-only Asian cycle backtest. It operates directly on validated 1-minute bars:
        // each selected instrument enters at the exact chosen opening minute, reverses at the
        // next 1M bar open after its own limit, and adds one micro. The combined target/stop
        // decision is deliberately 1M-close confirmed because OHLC data cannot establish
        // cross-instrument intrabar ordering.
        private static List<KeystoneArcEvent> DetectAsian75Reversal(List<KeystoneArcBar> oneMinute, KeystoneArcRunConfig cfg)
        {
            var output = new List<KeystoneArcEvent>();
            bool invalidRisk = AsianUsesPriceRisk(cfg)
                ? (cfg.AsianMnqReversalPriceMove <= 0 || cfg.AsianMgcReversalPriceMove <= 0)
                : cfg.AsianReversalLossDollars <= 0;
            if (invalidRisk || cfg.AsianCycleTargetDollars <= 0 || cfg.AsianDailyLossLimitDollars <= 0 || cfg.AsianStartingQuantity <= 0 || cfg.AsianMnqMaxReversals < 0 || cfg.AsianMgcMaxReversals < 0) return output;
            List<KeystoneArcBar> all = oneMinute.Where(x => x != null).OrderBy(x => x.Time).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (IGrouping<DateTime, KeystoneArcBar> day in all.GroupBy(x => SessionGroupingDate(x.Time, cfg)).OrderBy(x => x.Key))
            {
                DateTime sessionDate = day.Key.Date;
                DateTime start = AsianDateAtHhmm(sessionDate, cfg.AsianStartHhmm);
                DateTime end = AsianDateAtHhmm(sessionDate, cfg.AsianEndHhmm);
                if (end < start) end = end.AddDays(1);
                List<KeystoneArcBar> range = day.Where(x => x.Time >= start && x.Time <= end).OrderBy(x => x.Time).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
                if (range.Count == 0) continue;
                var positions = new Dictionary<string, Asian75Position>(StringComparer.OrdinalIgnoreCase);
                // BOTH is one shared cycle, not two independent optional cycles.  A selected
                // instrument lacking its exact configured opening 1M bar makes that session
                // unavailable rather than silently testing only the other instrument.
                bool openingComplete = true;
                foreach (string symbol in AsianSelectedSymbols(cfg))
                {
                    KeystoneArcBar opening = range.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase) && x.Time == start);
                    // No 17:59 carry-forward or nearest-bar proxy is permitted: the session is
                    // simply unavailable for an instrument without its exact 18:00 opening bar.
                    if (opening == null) { openingComplete = false; break; }
                    positions[symbol] = new Asian75Position { Symbol = symbol, LegNumber = 1, Quantity = cfg.AsianStartingQuantity, Direction = AsianInitialDirection(symbol, cfg), EntryTime = start, EntryPrice = opening.Open };
                }
                if (!openingComplete || positions.Count == 0) continue;
                var latestClose = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                bool closed = false;
                double combinedPeakMarked = double.MinValue;
                foreach (IGrouping<DateTime, KeystoneArcBar> tickGroup in range.GroupBy(x => x.Time).OrderBy(x => x.Key))
                {
                    DateTime time = tickGroup.Key;
                    var tick = tickGroup.ToDictionary(x => x.Symbol, x => x, StringComparer.OrdinalIgnoreCase);
                    foreach (KeystoneArcBar b in tickGroup) latestClose[b.Symbol] = b.Close;
                    // Stops observed in one bar hand off at the next 1M bar open. This avoids
                    // inventing a same-minute reversal price/path not present in OHLC history.
                    foreach (Asian75Position p in positions.Values.Where(x => x.PendingEntry && x.PendingAt == time).ToList())
                    {
                        KeystoneArcBar b;
                        if (!tick.TryGetValue(p.Symbol, out b)) continue;
                        p.PendingEntry = false; p.Direction = p.PendingDirection; p.Quantity = p.PendingQuantity; p.LegNumber++; p.EntryTime = time; p.EntryPrice = b.Open;
                    }
                    if (time > start)
                    {
                        foreach (Asian75Position p in positions.Values.Where(x => !x.Halted && !x.PendingEntry && x.EntryTime < time).ToList())
                        {
                            KeystoneArcBar b;
                            if (!tick.TryGetValue(p.Symbol, out b)) continue;
                            double stop = Asian75StopPrice(p, cfg);
                            bool hit = p.Direction > 0 ? b.Low <= stop : b.High >= stop;
                            if (!hit) continue;
                            double reversalLoss = -Asian75LegLoss(p, cfg);
                            p.RealizedPnl += reversalLoss;
                            int maxTotalLegs = Math.Max(1, AsianMaxReversalsForSymbol(p.Symbol, cfg) + 1);
                            double instrumentCap = AsianInstrumentCapForSymbol(p.Symbol, cfg);
                            bool instrumentStop = instrumentCap > 0 && p.RealizedPnl <= -Math.Abs(instrumentCap);
                            string lossNote = "REVERSAL LOSS • " + AsianRiskDescription(p, cfg);
                            if (instrumentStop) lossNote += " • INSTRUMENT LOSS CAP REACHED";
                            else if (p.LegNumber >= maxTotalLegs) lossNote += " • MAX REVERSALS REACHED";
                            output.Add(NewAsian75Event(p, sessionDate, stop, time, "LOSS", reversalLoss, cfg, lossNote));
                            if (instrumentStop || p.LegNumber >= maxTotalLegs) p.Halted = true;
                            else { p.PendingEntry = true; p.PendingAt = time.AddMinutes(1); p.PendingDirection = -p.Direction; p.PendingQuantity = p.Quantity + 1; }
                        }
                    }
                    double marked = output.Where(x => SessionGroupingDate(x.TriggerTime, cfg) == sessionDate).Sum(x => x.GrossPnl);
                    foreach (Asian75Position p in positions.Values.Where(x => !x.Halted && !x.PendingEntry))
                    {
                        double mark; if (latestClose.TryGetValue(p.Symbol, out mark)) marked += Asian75OpenPnl(p, mark);
                    }
                    combinedPeakMarked = Math.Max(combinedPeakMarked, marked);
                    if (marked >= cfg.AsianCycleTargetDollars)
                    {
                        CloseAsian75Positions(output, positions, latestClose, sessionDate, time, "WIN", "CYCLE TARGET • 1M CLOSE CONFIRMED", cfg);
                        closed = true;
                    }
                    else if (cfg.AsianCombinedStopLossDollars > 0 && marked <= -Math.Abs(cfg.AsianCombinedStopLossDollars))
                    {
                        KeystoneArcEvent terminalLoss = output.LastOrDefault(x => x.ExitTime == time && x.Outcome == "LOSS");
                        if (terminalLoss != null) terminalLoss.ReviewNote = (terminalLoss.ReviewNote ?? string.Empty) + " • COMBINED CYCLE STOP REACHED";
                        CloseAsian75Positions(output, positions, latestClose, sessionDate, time, "LOSS", "COMBINED CYCLE STOP • 1M CLOSE CONFIRMED", cfg);
                        closed = true;
                    }
                    else if (cfg.AsianBreakEvenTriggerDollars > 0 && combinedPeakMarked >= cfg.AsianBreakEvenTriggerDollars && marked <= 0)
                    {
                        // Minute OHLC cannot prove the exact intraminute flat print after the
                        // trigger. Exit at the known completed-bar close and preserve actual P/L.
                        CloseAsian75Positions(output, positions, latestClose, sessionDate, time, "BREAKEVEN GUARD", "BREAKEVEN GUARD • PROFIT TRIGGER REACHED; 1M CLOSE RETURNED TO FLAT/NEGATIVE", cfg);
                        closed = true;
                    }
                    else if (marked <= -Math.Abs(cfg.AsianDailyLossLimitDollars))
                    {
                        CloseAsian75Positions(output, positions, latestClose, sessionDate, time, "LOSS", "DAILY LOSS LIMIT • 1M CLOSE CONFIRMED", cfg);
                        closed = true;
                    }
                    if (closed) break;
                }
                if (!closed)
                {
                    DateTime closeTime = range.Max(x => x.Time);
                    CloseAsian75Positions(output, positions, latestClose, sessionDate, closeTime, "SESSION EXIT", "SESSION END • LAST AVAILABLE 1M CLOSE", cfg);
                }
            }
            int order = 0; DateTime orderDay = DateTime.MinValue;
            foreach (KeystoneArcEvent e in output.OrderBy(x => x.TriggerTime).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.EntryTime))
            {
                DateTime day = SessionGroupingDate(e.TriggerTime, cfg);
                if (day != orderDay) { orderDay = day; order = 0; }
                e.SessionOrder = ++order; e.ConfigurationKey = cfg.Snapshot();
            }
            return output.OrderBy(x => x.TriggerTime).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.EntryTime).ToList();
        }

        private static void CloseAsian75Positions(List<KeystoneArcEvent> output, Dictionary<string, Asian75Position> positions, Dictionary<string, double> latestClose, DateTime sessionDate, DateTime time, string outcome, string reason, KeystoneArcRunConfig cfg)
        {
            foreach (Asian75Position p in positions.Values.Where(x => !x.Halted && !x.PendingEntry).ToList())
            {
                double mark; if (!latestClose.TryGetValue(p.Symbol, out mark)) continue;
                output.Add(NewAsian75Event(p, sessionDate, mark, time, outcome, Asian75OpenPnl(p, mark), cfg, reason));
                p.Halted = true;
            }
            foreach (Asian75Position p in positions.Values.Where(x => x.PendingEntry).ToList()) p.Halted = true;
        }

        private static KeystoneArcEvent NewAsian75Event(Asian75Position p, DateTime sessionDate, double exit, DateTime exitTime, string outcome, double pnl, KeystoneArcRunConfig cfg, string note)
        {
            bool reversalLoss = (note ?? string.Empty).StartsWith("REVERSAL LOSS", StringComparison.OrdinalIgnoreCase);
            double cyclePnlThroughEvent = reversalLoss ? p.RealizedPnl : p.RealizedPnl + pnl;
            return new KeystoneArcEvent
            {
                Id = "KA|" + p.Symbol + "|ASIA75|L" + p.LegNumber + "|" + p.EntryTime.Ticks.ToString(CultureInfo.InvariantCulture),
                Symbol = p.Symbol,
                SetupClass = "ASIA75",
                Direction = p.Direction > 0 ? "LONG" : "SHORT",
                StrengthTag = "ASIA 75 • LEG " + p.LegNumber,
                ReferenceTime = AsianDateAtHhmm(sessionDate, cfg.AsianStartHhmm),
                TriggerTime = p.EntryTime,
                EntryTime = p.EntryTime,
                Entry = p.EntryPrice,
                Stop = Asian75StopPrice(p, cfg),
                // Target is combined across active MNQ/MGC legs, not a per-leg price level.
                // Reusing entry prevents a false chart price target from being drawn.
                Target = p.EntryPrice,
                Quantity = p.Quantity,
                StopDistance = Math.Abs(p.EntryPrice - Asian75StopPrice(p, cfg)),
                RiskModel = "ASIAN CYCLE • " + (AsianUsesPriceRisk(cfg) ? "PRICE-MOVE REVERSAL" : "FIXED-CASH REVERSAL") + " • " + AsianRiskDescription(p, cfg) + " • COMBINED $" + cfg.AsianCycleTargetDollars.ToString("0") + " TARGET • CYCLE STOP " + (cfg.AsianCombinedStopLossDollars <= 0 ? "OFF" : "$" + cfg.AsianCombinedStopLossDollars.ToString("0")) + " • INSTRUMENT CAP " + (AsianInstrumentCapForSymbol(p.Symbol, cfg) <= 0 ? "OFF" : "$" + AsianInstrumentCapForSymbol(p.Symbol, cfg).ToString("0")),
                ExitTime = exitTime,
                ExitPrice = exit,
                Outcome = outcome,
                GrossPnl = pnl,
                PeakAfterEntry = Math.Max(p.EntryPrice, exit),
                TroughAfterEntry = Math.Min(p.EntryPrice, exit),
                TargetTouched = outcome == "WIN" ? 1 : 0,
                StopTouched = note.StartsWith("REVERSAL LOSS", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                ReviewState = "ACCEPTED",
                ReviewNote = (note ?? string.Empty) + " • LEG " + p.LegNumber + " • CURRENT CONTRACTS x" + p.Quantity + " • LEG P/L " + pnl.ToString("C0") + " • CYCLE P/L THROUGH EVENT " + cyclePnlThroughEvent.ToString("C0")
            };
        }

        private static DateTime AsianDateAtHhmm(DateTime day, int hhmm)
        {
            int clean = Math.Max(0, Math.Min(2359, hhmm));
            return new DateTime(day.Year, day.Month, day.Day, clean / 100, clean % 100, 0);
        }

        private static int AsianInitialDirection(string symbol, KeystoneArcRunConfig cfg)
        {
            string direction = IsMgc(symbol) ? cfg.AsianMgcInitialDirection : cfg.AsianMnqInitialDirection;
            return string.Equals(direction, "SHORT", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        }

        private static bool AsianUsesPriceRisk(KeystoneArcRunConfig cfg)
        {
            return cfg != null && string.Equals(cfg.AsianRiskMode, "PRICE", StringComparison.OrdinalIgnoreCase);
        }

        private static double AsianPriceLossMove(string symbol, KeystoneArcRunConfig cfg)
        {
            return Math.Max(0.0001, IsMgc(symbol) ? cfg.AsianMgcReversalPriceMove : cfg.AsianMnqReversalPriceMove);
        }

        private static double Asian75LegLoss(Asian75Position p, KeystoneArcRunConfig cfg)
        {
            if (AsianUsesPriceRisk(cfg)) return AsianPriceLossMove(p.Symbol, cfg) * CashValuePerPriceMove(p.Symbol, cfg) * Math.Max(1, p.Quantity);
            return Math.Abs(cfg.AsianReversalLossDollars);
        }

        private static string AsianRiskDescription(Asian75Position p, KeystoneArcRunConfig cfg)
        {
            return AsianUsesPriceRisk(cfg)
                ? AsianPriceLossMove(p.Symbol, cfg).ToString("0.####", CultureInfo.InvariantCulture) + " PRICE MOVE • " + Asian75LegLoss(p, cfg).ToString("C0") + " AT x" + p.Quantity
                : Asian75LegLoss(p, cfg).ToString("C0") + " FIXED CASH";
        }

        private static int AsianMaxReversalsForSymbol(string symbol, KeystoneArcRunConfig cfg)
        {
            int specific = IsMgc(symbol) ? cfg.AsianMgcMaxReversals : cfg.AsianMnqMaxReversals;
            return Math.Max(0, specific);
        }

        private static double AsianInstrumentCapForSymbol(string symbol, KeystoneArcRunConfig cfg)
        {
            double specific = IsMgc(symbol) ? cfg.AsianMgcInstrumentStopLossDollars : cfg.AsianMnqInstrumentStopLossDollars;
            return Math.Max(0, specific > 0 ? specific : cfg.AsianInstrumentStopLossDollars);
        }

        private static double Asian75StopPrice(Asian75Position p, KeystoneArcRunConfig cfg)
        {
            double distance = AsianUsesPriceRisk(cfg)
                ? AsianPriceLossMove(p.Symbol, cfg)
                : Asian75LegLoss(p, cfg) / Math.Max(0.0001, CashValuePerPriceMove(p.Symbol, cfg) * p.Quantity);
            return p.Direction > 0 ? p.EntryPrice - distance : p.EntryPrice + distance;
        }

        private static double Asian75OpenPnl(Asian75Position p, double mark)
        {
            return (mark - p.EntryPrice) * CashValuePerPriceMove(p.Symbol, null) * p.Quantity * p.Direction;
        }

        private static KeystoneArcEvent DetectBhBreak(List<KeystoneArcBar> bars, int i, KeystoneArcRunConfig cfg, string symbol)
        {
            KeystoneArcBar bearish = bars[i - 2], reference = bars[i - 1], trigger = bars[i];
            // Every formation candle must occur after the chosen session begins. Pre-session
            // candles remain visible only as chart context, never as a qualifying setup leg.
            if (bearish.Time < cfg.Start || reference.Time < cfg.Start || trigger.Time < cfg.Start || trigger.Time > cfg.End) return null;
            if (!InsideSession(bearish.Time, symbol, cfg) || !InsideSession(reference.Time, symbol, cfg) || !InsideSession(trigger.Time, symbol, cfg)) return null;
            bool longSide = DirectionAllows(symbol, "LONG", cfg);
            bool shortSide = DirectionAllows(symbol, "SHORT", cfg);
            bool longCandidate = longSide && bearish.Close < bearish.Open && reference.Close > reference.Open && trigger.High >= reference.High;
            bool shortCandidate = shortSide && bearish.Close > bearish.Open && reference.Close < reference.Open && trigger.Low <= reference.Low;
            if (!longCandidate && !shortCandidate) return null;
            bool shortSignal = shortCandidate && !longCandidate;
            if (string.Equals(cfg.BhAggressionFilter, "STRONGER", StringComparison.OrdinalIgnoreCase) && !PassesStrongerBhAggression(bars, i, symbol, cfg, shortSignal)) return null;
            return NewEvent(symbol, shortSignal ? "BL" : "BH", reference.Time, trigger.Time, shortSignal ? reference.Low : reference.High, cfg, bars, i, shortSignal ? "SHORT" : "LONG");
        }

        private static bool DirectionAllows(string symbol, string direction, KeystoneArcRunConfig cfg)
        {
            string mode = (cfg == null ? "BB" : cfg.DirectionMode ?? "BB").ToUpperInvariant();
            if (mode.Length < 2) mode = "BB";
            bool mnq = !IsMgc(symbol);
            char selected = mnq ? mode[0] : mode.Length > 1 ? mode[1] : 'B';
            return string.Equals(direction, "SHORT", StringComparison.OrdinalIgnoreCase) ? selected == 'S' : selected == 'B';
        }

        private static bool PassesStrongerBhAggression(List<KeystoneArcBar> bars, int triggerIndex, string symbol, KeystoneArcRunConfig cfg, bool shortSignal)
        {
            // The stronger tag mirrors the selected side: red/down aggression precedes LONG;
            // green/up aggression precedes SHORT. A doji breaks either consecutive sequence.
            int lastImpulse = triggerIndex - 2;
            int firstImpulse = lastImpulse;
            while (firstImpulse > 0 && (shortSignal ? bars[firstImpulse - 1].Close > bars[firstImpulse - 1].Open : bars[firstImpulse - 1].Close < bars[firstImpulse - 1].Open)) firstImpulse--;
            int impulseCount = lastImpulse - firstImpulse + 1;
            double move = shortSignal ? bars[lastImpulse].Close - bars[firstImpulse].Open : bars[firstImpulse].Open - bars[lastImpulse].Close;
            bool mgc = IsMgc(symbol);
            int requiredRed = Math.Max(0, mgc ? cfg.MgcStrongRedCandles : cfg.MnqStrongRedCandles);
            double requiredDecline = Math.Max(0, mgc ? cfg.MgcStrongDeclineDollars : cfg.MnqStrongDeclinePoints);
            bool useRed = requiredRed > 0;
            bool useDecline = requiredDecline > 0;
            // Stronger mode cannot silently fall back to the base rule when every threshold is
            // disabled. Require at least one active filter before it can produce results.
            if (!useRed && !useDecline) return false;
            bool redPass = !useRed || impulseCount >= requiredRed;
            bool declinePass = !useDecline || move >= requiredDecline;
            return string.Equals(cfg.BhStrongCombine, "ALL", StringComparison.OrdinalIgnoreCase)
                ? redPass && declinePass
                : (useRed && redPass) || (useDecline && declinePass);
        }

        private static KeystoneArcEvent ResolveFvgBreak(KeystoneArcBar current, List<BullishFvg> zones, KeystoneArcRunConfig cfg, string symbol, List<KeystoneArcBar> setupBars, int triggerIndex)
        {
            KeystoneArcEvent result = null;
            foreach (BullishFvg zone in zones.Where(z => !z.Invalid && z.PendingReference != DateTime.MinValue).ToList())
            {
                if (current.Time <= zone.PendingReference) continue;
                // Optional FVG evidence obeys the same strict selected-session rule as BH:
                // its formation, bullish reference, and break must be inside the chosen range.
                if (zone.Formed < cfg.Start || zone.PendingReference < cfg.Start || current.Time < cfg.Start || current.Time > cfg.End) { zone.Invalid = true; continue; }
                // The FVG rule is an immediate next selected-timeframe-bar break after a bullish reference close.
                if (current.Time == zone.PendingReference.AddMinutes(cfg.SetupMinutes) && current.High >= zone.PendingHigh && result == null)
                {
                    result = NewEvent(symbol, "FVG", zone.PendingReference, current.Time, zone.PendingHigh, cfg, setupBars, triggerIndex);
                    result.FvgLower = zone.Lower;
                    result.FvgUpper = zone.Upper;
                    result.FvgFormedTime = zone.Formed;
                }
                zone.Invalid = true; // prevents repeated entries from one FVG touch/reference sequence
            }
            return result;
        }

        private static void UpdateFvgTouchState(KeystoneArcBar current, List<BullishFvg> zones, KeystoneArcRunConfig cfg)
        {
            foreach (BullishFvg zone in zones.Where(z => !z.Invalid && z.Formed < current.Time).ToList())
            {
                if (current.Close < zone.Lower) { zone.Invalid = true; continue; }
                if (zone.PendingReference != DateTime.MinValue) continue;
                bool touched = current.Low <= zone.Upper && current.High >= zone.Lower;
                if (!touched) continue;
                zone.Touched = true;
                if (current.Close > current.Open)
                {
                    zone.PendingReference = current.Time;
                    zone.PendingHigh = current.High;
                }
                else if (current.Close < current.Open)
                {
                    // Bearish touch requires the next completed 5m candle to close bullish.
                    zone.PendingReference = current.Time.AddMinutes(cfg.SetupMinutes);
                    zone.PendingHigh = double.NaN;
                }
            }

            foreach (BullishFvg zone in zones.Where(z => !z.Invalid && z.PendingReference != DateTime.MinValue && double.IsNaN(z.PendingHigh) && current.Time == z.PendingReference).ToList())
            {
                if (current.Close > current.Open) zone.PendingHigh = current.High;
                else zone.Invalid = true;
            }
        }

        private static void DetectNewBullishFvg(List<KeystoneArcBar> bars, int i, List<BullishFvg> zones)
        {
            if (i < 2) return;
            KeystoneArcBar first = bars[i - 2], third = bars[i];
            if (third.Low > first.High)
                zones.Add(new BullishFvg { Lower = first.High, Upper = third.Low, Formed = third.Time });
        }

        private static bool IsPersonalAccount(KeystoneArcRunConfig cfg)
        {
            // Keystone Arc is prop virtual-pool research only. This compatibility helper is kept
            // false so legacy snapshots cannot re-enable the removed personal-account model.
            return false;
        }

        private static double CashValuePerPriceMove(string symbol, KeystoneArcRunConfig cfg)
        {
            if (IsPersonalAccount(cfg)) return Math.Max(0.0001, IsMgc(symbol) ? cfg.MgcCashPerPointPerLot : cfg.MnqCashPerPointPerLot);
            return IsMgc(symbol) ? 10.0 : 2.0;
        }

        private static double EventPnlForMove(KeystoneArcEvent e, double priceMove, KeystoneArcRunConfig cfg)
        {
            return priceMove * CashValuePerPriceMove(e.Symbol, cfg) * Math.Max(0.0001, e.Quantity);
        }

        private static bool IsPersonalAutoRiskStop(KeystoneArcRunConfig cfg)
        {
            return IsPersonalAccount(cfg) && string.Equals(cfg.StopMode, "LIVE_LOW_AUTO_RISK", StringComparison.OrdinalIgnoreCase);
        }

        private static KeystoneArcEvent NewEvent(string symbol, string setup, DateTime reference, DateTime trigger, double entry, KeystoneArcRunConfig cfg, List<KeystoneArcBar> setupBars, int triggerIndex, string direction = "LONG")
        {
            bool personal = IsPersonalAccount(cfg);
            bool shortSide = string.Equals(direction, "SHORT", StringComparison.OrdinalIgnoreCase);
            double sign = shortSide ? -1.0 : 1.0;
            double pointValue = CashValuePerPriceMove(symbol, cfg);
            double qty = personal ? Math.Max(0.01, cfg.PersonalLotSize) : Math.Max(1, cfg.Quantity);
            double targetMove = personal ? Math.Max(0.0001, IsMgc(symbol) ? cfg.MgcTargetMove : cfg.MnqTargetMove) : cfg.TargetDollars / Math.Max(0.0001, pointValue * qty);
            double standardStopMove = personal ? Math.Max(0.0001, IsMgc(symbol) ? cfg.MgcStandardStopMove : cfg.MnqStandardStopMove) : cfg.StopDollars / Math.Max(0.0001, pointValue * qty);
            double stop = entry - sign * standardStopMove;
            string riskModel = personal ? "LIVE STANDARD • FIXED LOT" : "PROP STANDARD • FIXED MICRO SIZE";
            bool belowSetupLow = string.Equals(cfg.StopMode, "BELOW_SETUP_LOW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cfg.StopMode, "LIVE_LOW_FIXED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cfg.StopMode, "LIVE_LOW_AUTO_RISK", StringComparison.OrdinalIgnoreCase);
            if (belowSetupLow && setupBars != null && triggerIndex >= 2 && triggerIndex < setupBars.Count)
            {
                double extreme = shortSide
                    ? Math.Max(setupBars[triggerIndex - 2].High, Math.Max(setupBars[triggerIndex - 1].High, setupBars[triggerIndex].High))
                    : Math.Min(setupBars[triggerIndex - 2].Low, Math.Min(setupBars[triggerIndex - 1].Low, setupBars[triggerIndex].Low));
                double offset = IsMgc(symbol) ? Math.Max(0, cfg.MgcStopOffsetPoints) : Math.Max(0, cfg.MnqStopOffsetPoints);
                stop = extreme - sign * offset;
                double distance = Math.Max(0.0001, Math.Abs(entry - stop));
                if (IsPersonalAutoRiskStop(cfg))
                {
                    // Broker lot increments differ. Keep the calculation transparent at 0.01
                    // lots; the user can adjust the final rounded lot in the personal input.
                    qty = Math.Max(0.01, Math.Floor(Math.Max(0, cfg.PersonalMaxRiskDollars) / (distance * pointValue) * 100.0) / 100.0);
                    riskModel = shortSide ? "LIVE ABOVE 3-CANDLE HIGH • AUTO-RISK LOT" : "LIVE BELOW 3-CANDLE LOW • AUTO-RISK LOT";
                }
                else if (personal) riskModel = shortSide ? "LIVE ABOVE 3-CANDLE HIGH • FIXED LOT" : "LIVE BELOW 3-CANDLE LOW • FIXED LOT";
                else
                {
                    qty = Math.Max(1, Math.Floor(cfg.StopDollars / (distance * pointValue)));
                    riskModel = shortSide ? "PROP ABOVE 3-CANDLE HIGH • RISK-SIZED" : "PROP BELOW 3-CANDLE LOW • RISK-SIZED";
                }
            }
            return new KeystoneArcEvent
            {
                Id = "KA|" + symbol + "|" + setup + "|" + direction + "|" + trigger.Ticks.ToString(CultureInfo.InvariantCulture),
                Symbol = symbol,
                SetupClass = setup,
                Direction = shortSide ? "SHORT" : "LONG",
                ReferenceTime = reference,
                TriggerTime = trigger,
                EntryTime = trigger,
                Entry = entry,
                Target = entry + sign * targetMove,
                Stop = stop,
                Quantity = qty,
                StopDistance = Math.Abs(entry - stop),
                RiskModel = riskModel,
                Outcome = "OPEN"
            };
        }

        private static bool IsShort(KeystoneArcEvent e) { return e != null && string.Equals(e.Direction, "SHORT", StringComparison.OrdinalIgnoreCase); }
        private static double EventPnlAtExit(KeystoneArcEvent e, double exitPrice, double pointValue, double qty)
        {
            return (IsShort(e) ? e.Entry - exitPrice : exitPrice - e.Entry) * pointValue * qty;
        }
        private static bool UsesFixedDollarStop(KeystoneArcRunConfig cfg)
        {
            return !IsPersonalAccount(cfg) && string.Equals(cfg.StopMode, "STANDARD", StringComparison.OrdinalIgnoreCase);
        }

        private static void ResolveOutcome(KeystoneArcEvent e, List<KeystoneArcBar> raw, KeystoneArcRunConfig cfg, string symbol)
        {
            if (cfg.OutcomeModelEnabled == 0)
            {
                e.Outcome = "OUTCOME BLOCKED • SERIES MISMATCH";
                e.EntryTime = e.TriggerTime;
                e.ExitTime = e.TriggerTime;
                e.GrossPnl = 0;
                return;
            }
            int timeOffset = IsMgc(symbol) ? cfg.MgcOutcomeTimeOffsetMinutes : cfg.MnqOutcomeTimeOffsetMinutes;
            DateTime sessionEnd = new DateTime(e.TriggerTime.Year, e.TriggerTime.Month, e.TriggerTime.Day, cfg.EndTime / 100, cfg.EndTime % 100, 0);
            int triggerHhmm = e.TriggerTime.Hour * 100 + e.TriggerTime.Minute;
            if (cfg.EndTime < triggerHhmm) sessionEnd = sessionEnd.AddDays(1);
            if (sessionEnd > cfg.End) sessionEnd = cfg.End;
            sessionEnd = sessionEnd.AddMinutes(timeOffset);
            double pointValue = CashValuePerPriceMove(symbol, cfg);
            double qty = Math.Max(0.0001, e.Quantity > 0 ? e.Quantity : (IsPersonalAccount(cfg) ? cfg.PersonalLotSize : cfg.Quantity));
            int startIndex = FirstIndexAtOrAfter(raw, e.TriggerTime.AddMinutes(timeOffset));
            if (startIndex < 0) { e.Outcome = "NO ENTRY DATA"; e.GrossPnl = 0; return; }
            int entryIndex = -1;
            for (int i = startIndex; i < raw.Count; i++)
            {
                KeystoneArcBar candidate = raw[i];
                if (candidate.Time > sessionEnd) break;
                if (IsShort(e) ? candidate.Low <= e.Entry : candidate.High >= e.Entry) { entryIndex = i; break; }
            }
            if (entryIndex < 0) { e.Outcome = "NO ENTRY DATA"; e.GrossPnl = 0; return; }
            KeystoneArcBar entryBar = raw[entryIndex];
            e.EntryTime = entryBar.Time.AddMinutes(-timeOffset);
            e.PeakAfterEntry = entryBar.High;
            e.TroughAfterEntry = entryBar.Low;
            bool entryBarTarget = IsShort(e) ? entryBar.Low <= e.Target : entryBar.High >= e.Target;
            bool entryBarStop = IsShort(e) ? entryBar.High >= e.Stop : entryBar.Low <= e.Stop;
            e.TargetTouched = entryBarTarget ? 1 : 0;
            e.StopTouched = entryBarStop ? 1 : 0;
            bool breakEvenActive = IsPersonalAccount(cfg) && cfg.BreakEvenEnabled > 0 && cfg.BreakEvenTriggerMove > 0;
            bool breakEvenArmed = false;
            if (entryBarStop)
            {
                e.Outcome = entryBarTarget ? "LOSS SAME-MINUTE STOP-FIRST" : "LOSS ENTRY-MINUTE AMBIGUOUS STOP-FIRST";
                e.GrossPnl = UsesFixedDollarStop(cfg) ? -Math.Abs(cfg.StopDollars) : EventPnlAtExit(e, e.Stop, pointValue, qty);
                e.ExitTime = entryBar.Time.AddMinutes(-timeOffset);
                e.ExitPrice = e.Stop;
                return;
            }
            if (entryBarTarget)
            {
                e.Outcome = "WIN";
                e.GrossPnl = IsPersonalAccount(cfg) ? EventPnlAtExit(e, e.Target, pointValue, qty) : cfg.TargetDollars;
                e.ExitTime = entryBar.Time.AddMinutes(-timeOffset);
                e.ExitPrice = e.Target;
                return;
            }
            if (breakEvenActive && (IsShort(e) ? entryBar.Low <= e.Entry - cfg.BreakEvenTriggerMove : entryBar.High >= e.Entry + cfg.BreakEvenTriggerMove)) breakEvenArmed = true;
            KeystoneArcBar last = entryBar;
            for (int i = entryIndex + 1; i < raw.Count; i++)
            {
                KeystoneArcBar bar = raw[i];
                if (bar.Time > sessionEnd) break;
                last = bar;
                e.PeakAfterEntry = Math.Max(e.PeakAfterEntry, bar.High);
                e.TroughAfterEntry = Math.Min(e.TroughAfterEntry, bar.Low);
                bool hitTarget = IsShort(e) ? bar.Low <= e.Target : bar.High >= e.Target;
                bool hitStop = IsShort(e) ? bar.High >= e.Stop : bar.Low <= e.Stop;
                bool hitBreakEven = breakEvenArmed && (IsShort(e) ? bar.High >= e.Entry : bar.Low <= e.Entry);
                if (hitTarget) e.TargetTouched = 1;
                if (hitStop) e.StopTouched = 1;
                if (hitTarget && hitStop)
                {
                    e.Outcome = "LOSS SAME-MINUTE STOP-FIRST";
                    e.GrossPnl = UsesFixedDollarStop(cfg) ? -Math.Abs(cfg.StopDollars) : EventPnlAtExit(e, e.Stop, pointValue, qty);
                    e.ExitTime = bar.Time.AddMinutes(-timeOffset);
                    e.ExitPrice = e.Stop;
                    return;
                }
                if (hitStop)
                {
                    e.Outcome = "LOSS";
                    e.GrossPnl = UsesFixedDollarStop(cfg) ? -Math.Abs(cfg.StopDollars) : EventPnlAtExit(e, e.Stop, pointValue, qty);
                    e.ExitTime = bar.Time.AddMinutes(-timeOffset);
                    e.ExitPrice = e.Stop;
                    return;
                }
                if (hitTarget)
                {
                    e.Outcome = "WIN";
                    e.GrossPnl = IsPersonalAccount(cfg) ? EventPnlAtExit(e, e.Target, pointValue, qty) : cfg.TargetDollars;
                    e.ExitTime = bar.Time.AddMinutes(-timeOffset);
                    e.ExitPrice = e.Target;
                    return;
                }
                // Once the requested movement is reached, subsequent bars may exit at the entry
                // price. Within a one-minute bar, an original stop still wins the tie first.
                if (hitBreakEven)
                {
                    e.Outcome = "BREAKEVEN";
                    e.GrossPnl = 0;
                    e.ExitTime = bar.Time.AddMinutes(-timeOffset);
                    e.ExitPrice = e.Entry;
                    return;
                }
                if (breakEvenActive && (IsShort(e) ? bar.Low <= e.Entry - cfg.BreakEvenTriggerMove : bar.High >= e.Entry + cfg.BreakEvenTriggerMove)) breakEvenArmed = true;
            }
            e.Outcome = "SESSION EXIT";
            e.ExitTime = last.Time.AddMinutes(-timeOffset);
            e.ExitPrice = last.Close;
            e.GrossPnl = EventPnlAtExit(e, last.Close, pointValue, qty);
        }

        // Evaluation-stage terms are pre-resolved from the same direct 1-minute path as the base
        // model. The pool later chooses them only while an assigned account has not passed; this
        // avoids changing funded outcomes or pretending a different data series was used.
        private static void ResolveEvaluationStageOutcome(KeystoneArcEvent source, List<KeystoneArcBar> raw, KeystoneArcRunConfig cfg, string symbol)
        {
            if (source == null || cfg == null || cfg.EvaluationStageTradeRulesEnabled <= 0 || cfg.EvaluationTradeTargetDollars <= 0 || cfg.EvaluationTradeStopDollars <= 0) return;
            double pointValue = CashValuePerPriceMove(symbol, cfg);
            double qty = Math.Max(0.0001, source.Quantity > 0 ? source.Quantity : cfg.Quantity);
            double sign = IsShort(source) ? -1.0 : 1.0;
            var stage = new KeystoneArcEvent
            {
                Id = source.Id + "|EVAL",
                Symbol = source.Symbol,
                SetupClass = source.SetupClass,
                Direction = source.Direction,
                ReferenceTime = source.ReferenceTime,
                TriggerTime = source.TriggerTime,
                EntryTime = source.EntryTime,
                Entry = source.Entry,
                Quantity = qty,
                Target = source.Entry + sign * cfg.EvaluationTradeTargetDollars / Math.Max(0.0001, pointValue * qty),
                Stop = source.Entry - sign * cfg.EvaluationTradeStopDollars / Math.Max(0.0001, pointValue * qty),
                RiskModel = "EVALUATION STAGE • FIXED CASH TERMS",
                Outcome = "OPEN"
            };
            var stageCfg = new KeystoneArcRunConfig
            {
                OutcomeModelEnabled = cfg.OutcomeModelEnabled,
                End = cfg.End,
                EndTime = cfg.EndTime,
                Quantity = Math.Max(1, (int)Math.Round(qty, MidpointRounding.AwayFromZero)),
                TargetDollars = cfg.EvaluationTradeTargetDollars,
                StopDollars = cfg.EvaluationTradeStopDollars,
                StopMode = "STANDARD",
                MnqOutcomeTimeOffsetMinutes = cfg.MnqOutcomeTimeOffsetMinutes,
                MgcOutcomeTimeOffsetMinutes = cfg.MgcOutcomeTimeOffsetMinutes
            };
            ResolveOutcome(stage, raw, stageCfg, symbol);
            source.EvaluationOutcome = stage.Outcome;
            source.EvaluationExitTime = stage.ExitTime;
            source.EvaluationExitPrice = stage.ExitPrice;
            source.EvaluationGrossPnl = stage.GrossPnl;
            source.EvaluationTarget = stage.Target;
            source.EvaluationStop = stage.Stop;
        }

        private static void ApplyEvaluationStageOutcomeForAccount(KeystoneArcEvent e, KeystoneArcVirtualAccount account, KeystoneArcRunConfig cfg)
        {
            if (e == null || account == null || cfg == null || account.Funded || cfg.EvaluationEnabled <= 0 || cfg.EvaluationStageTradeRulesEnabled <= 0 || string.IsNullOrWhiteSpace(e.EvaluationOutcome)) return;
            e.Outcome = e.EvaluationOutcome;
            e.ExitTime = e.EvaluationExitTime;
            e.ExitPrice = e.EvaluationExitPrice;
            e.GrossPnl = e.EvaluationGrossPnl;
            e.Target = e.EvaluationTarget;
            e.Stop = e.EvaluationStop;
            e.StopDistance = Math.Abs(e.Entry - e.Stop);
            e.RiskModel = "EVALUATION STAGE • " + cfg.EvaluationTradeTargetDollars.ToString("C0") + " TARGET / " + cfg.EvaluationTradeStopDollars.ToString("C0") + " STOP";
        }

        private static int FirstIndexAtOrAfter(List<KeystoneArcBar> raw, DateTime time)
        {
            if (raw == null || raw.Count == 0) return -1;
            int low = 0, high = raw.Count - 1, result = -1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                if (raw[mid].Time >= time) { result = mid; high = mid - 1; }
                else low = mid + 1;
            }
            return result;
        }

        private static bool UsesFirmFundedCap(KeystoneArcRunConfig cfg)
        {
            return cfg != null && cfg.EvaluationEnabled > 0 && cfg.FirmFundedCapEnabled > 0 && cfg.EvaluationSlotsPerFirm > 0 && cfg.MaxFundedPerFirm > 0;
        }

        private static KeystoneArcVirtualAccount CreatePoolAccount(KeystoneArcRunConfig cfg, int accountIndex, DateTime initialStart, string readyState)
        {
            bool grouped = UsesFirmFundedCap(cfg);
            int slotsPerFirm = Math.Max(1, cfg == null ? 10 : cfg.EvaluationSlotsPerFirm);
            int firmIndex = ((Math.Max(1, accountIndex) - 1) / slotsPerFirm) + 1;
            int firmSlot = ((Math.Max(1, accountIndex) - 1) % slotsPerFirm) + 1;
            string firm = grouped ? "P" + firmIndex.ToString(CultureInfo.InvariantCulture) : string.Empty;
            return new KeystoneArcVirtualAccount
            {
                // Keep the original neutral KA-V naming whenever the optional firm-cap scenario
                // is off, so existing reports/regressions retain their historical identifiers.
                Name = grouped ? firm + "-EVAL" + firmSlot.ToString(CultureInfo.InvariantCulture) : "KA-V" + accountIndex.ToString("00", CultureInfo.InvariantCulture),
                PropFirmCode = firm,
                PropFirmSlot = grouped ? firmSlot : accountIndex,
                StartingBalance = cfg.PropStartingBalance,
                EvaluationCost = cfg.EvaluationEnabled <= 0 ? 0 : cfg.EvaluationCost,
                EvaluationPurchases = cfg.EvaluationEnabled > 0 ? 1 : 0,
                InitialLifecycleStart = initialStart,
                CurrentLifecycleStart = initialStart,
                LastState = readyState
            };
        }

        private static int FundedSlotsInFirm(IEnumerable<KeystoneArcVirtualAccount> pool, string firm)
        {
            return (pool ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Count(a => a != null && a.Funded && string.Equals(a.PropFirmCode, firm ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        // Promotion/deferment happens after every completed session. If several evaluations pass
        // on the same date, earlier pass time/slot has priority and later passes wait. A funded
        // blowout frees a place at the next session close; no pending evaluation is discarded.
        private static void EnforceFirmFundedCapacity(IEnumerable<KeystoneArcVirtualAccount> pool, KeystoneArcRunConfig cfg)
        {
            if (!UsesFirmFundedCap(cfg)) return;
            List<KeystoneArcVirtualAccount> all = (pool ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Where(a => a != null).ToList();
            foreach (IGrouping<string, KeystoneArcVirtualAccount> group in all.Where(a => !string.IsNullOrEmpty(a.PropFirmCode)).GroupBy(a => a.PropFirmCode, StringComparer.OrdinalIgnoreCase))
            {
                int cap = Math.Max(1, cfg.MaxFundedPerFirm);
                List<KeystoneArcVirtualAccount> funded = group.Where(a => a.Funded).OrderBy(a => a.FundedSinceDate == DateTime.MinValue ? DateTime.MinValue : a.FundedSinceDate).ThenBy(a => a.PropFirmSlot).ToList();
                for (int index = cap; index < funded.Count; index++)
                {
                    KeystoneArcVirtualAccount wait = funded[index];
                    wait.Funded = false;
                    wait.EvaluationPassed = true;
                    wait.FundedCapPending = true;
                    wait.FundedBalance = 0;
                    wait.FundedPositiveDays = 0;
                    wait.LastState = "EVAL PASSED • FIRM FUNDED CAP WAIT (ILLUSTRATIVE)";
                }
                int available = Math.Max(0, cap - FundedSlotsInFirm(group, group.Key));
                foreach (KeystoneArcVirtualAccount wait in group.Where(a => a.FundedCapPending && !a.Blown && !a.ReplacementPending).OrderBy(a => a.FundedSinceDate == DateTime.MinValue ? a.ActiveDay : a.FundedSinceDate).ThenBy(a => a.PropFirmSlot).ToList())
                {
                    if (available <= 0) break;
                    wait.FundedCapPending = false;
                    wait.Funded = true;
                    wait.EvaluationPassed = true;
                    wait.FundedBalance = 0;
                    wait.FundedPositiveDays = 0;
                    wait.FundedSinceDate = wait.ActiveDay == DateTime.MinValue ? DateTime.MinValue : wait.ActiveDay;
                    wait.LastState = "FUNDED • FIRM CAP OPENED (ILLUSTRATIVE)";
                    available--;
                }
                foreach (KeystoneArcVirtualAccount account in group)
                {
                    KeystoneArcAccountDay last = account.DayHistory.Count == 0 ? null : account.DayHistory[account.DayHistory.Count - 1];
                    if (last == null) continue;
                    // A later capacity opening must not rewrite the historical daily close that
                    // recorded this evaluation as waiting at the firm cap. The current account
                    // state below still changes to funded; the audit keeps both facts visible.
                    bool historicalCapWait = last.FundedCapPendingAfter || (!string.IsNullOrEmpty(last.StateAfterClose) && last.StateAfterClose.IndexOf("FIRM FUNDED CAP WAIT", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (historicalCapWait && !account.FundedCapPending) continue;
                    last.FundedAfter = account.Funded;
                    last.FundedCapPendingAfter = account.FundedCapPending;
                    last.PropFirmAfter = account.PropFirmCode;
                    last.FundedSlotsInFirmAfter = FundedSlotsInFirm(group, group.Key);
                    if (account.FundedCapPending) last.StateAfterClose = account.LastState;
                }
            }
        }

        public static List<KeystoneArcVirtualAccount> SimulatePool(List<KeystoneArcEvent> events, KeystoneArcRunConfig cfg)
        {
            if (cfg != null && string.Equals(cfg.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase))
                return SimulateAsian75CopyPool(events, cfg);
            if (cfg != null && cfg.CopyTradingPool > 0)
                return SimulateBhCopyPool(events, cfg);
            List<KeystoneArcVirtualAccount> accounts = new List<KeystoneArcVirtualAccount>();
            // Prop-only lab: every eligible event is evaluated through the virtual-pool allocator.
            bool personalPath = false;
            int poolCount = Math.Max(1, cfg.PoolSize);
            // Pool accounts are modeled as purchased at the beginning of the selected research
            // range—not at the first setup. This makes "days to first return" answer the user's
            // actual question: how long from starting the test/evaluation until cash was received.
            DateTime configuredInitialSlotStart = cfg.Start == DateTime.MinValue ? DateTime.MinValue : SessionGroupingDate(cfg.Start, cfg).Date;
            int accountIndex;
            for (accountIndex = 1; accountIndex <= poolCount; accountIndex++)
            {
                var account = CreatePoolAccount(cfg, accountIndex, configuredInitialSlotStart, "EVALUATION READY");
                if (cfg.EvaluationEnabled == -2)
                {
                    account.LastState = "SINGLE ACCOUNT • RANGE P/L ONLY";
                }
                else if (cfg.EvaluationEnabled < 0)
                {
                    account.LastState = "ONE-DAY ASSIGNMENT";
                }
                else if (cfg.EvaluationEnabled == 0)
                {
                    account.Funded = true;
                    account.EvaluationPassed = true;
                    account.FundedSinceDate = configuredInitialSlotStart;
                    account.LastState = "FUNDED READY (ILLUSTRATIVE)";
                }
                accounts.Add(account);
            }

            List<KeystoneArcEvent> orderedEvents = new List<KeystoneArcEvent>(events);
            orderedEvents.Sort(CompareEventsByEntryTime);

            int eventIndex;
            int nextAccountCursor = 0;
            for (eventIndex = 0; eventIndex < orderedEvents.Count; eventIndex++)
            {
                KeystoneArcEvent e = orderedEvents[eventIndex];
                DateTime when = e.EntryTime == DateTime.MinValue ? e.TriggerTime : e.EntryTime;
                int stateIndex;
                for (stateIndex = 0; stateIndex < accounts.Count; stateIndex++)
                {
                    if (accounts[stateIndex].InitialLifecycleStart == DateTime.MinValue)
                    {
                        accounts[stateIndex].InitialLifecycleStart = when.Date;
                        accounts[stateIndex].CurrentLifecycleStart = when.Date;
                    }
                    // A direct-funded terminal blowout is no longer an active lifecycle.  Do
                    // not manufacture daily zero-P/L snapshots through the rest of the range;
                    // this keeps its visible activity dates and report duration ending at blowout.
                    if (accounts[stateIndex].Blown && !accounts[stateIndex].ReplacementPending && cfg.EvaluationEnabled == 0) continue;
                    RollDay(accounts[stateIndex], when.Date, cfg, accounts);
                }

                // Each account has now closed the prior session. Re-open available firm capacity
                // before allocating the first event of this new session.
                EnforceFirmFundedCapacity(accounts, cfg);

                KeystoneArcVirtualAccount selected = null;
                int selectedIndex = -1;
                // Round-robin starts after the most recently assigned account. This deliberately spreads
                // sequential opportunities through the pool instead of repeatedly reusing A1 when it is free.
                for (stateIndex = 0; stateIndex < accounts.Count; stateIndex++)
                {
                    int candidateIndex = (nextAccountCursor + stateIndex) % accounts.Count;
                    KeystoneArcVirtualAccount candidate = accounts[candidateIndex];
                    if (!candidate.Blown && !candidate.ReplacementPending && !candidate.FundedCapPending && !candidate.DayLocked && candidate.FreeAt <= when) { selected = candidate; selectedIndex = candidateIndex; break; }
                }
                if (selected == null)
                {
                    List<KeystoneArcVirtualAccount> activeSlots = accounts.Where(x => !x.Blown && !x.ReplacementPending && !x.FundedCapPending).ToList();
                    e.SkipReason = activeSlots.Count > 0 && activeSlots.All(x => x.DayLocked)
                        ? "DAILY ACCOUNT LOCK • ALL ACTIVE ACCOUNTS LOCKED"
                        : (accounts.Any(x => x.FundedCapPending) ? "FIRM FUNDED CAP WAIT • PASSED EVAL BENCHED" : "NO FREE VIRTUAL ACCOUNT");
                    continue;
                }
                nextAccountCursor = (selectedIndex + 1) % accounts.Count;
                ApplyEvaluationStageOutcomeForAccount(e, selected, cfg);
                e.AssignedVirtualAccount = selected.Name;
                selected.LastAssignedDate = when.Date;
                selected.FreeAt = e.ExitTime == DateTime.MinValue ? when : e.ExitTime;
                selected.Trades++;
                selected.DayPnl += e.GrossPnl;
                selected.TotalPnl += e.GrossPnl;
                selected.PeakPnl = Math.Max(selected.PeakPnl, selected.TotalPnl);
                if (e.GrossPnl > 0) selected.Wins++; else if (e.GrossPnl < 0) selected.Losses++;
                if (cfg.EvaluationEnabled >= 0) ApplyIllustrativeLifecycle(selected, e.GrossPnl, cfg);
                if (cfg.EvaluationEnabled == 0 && selected.Blown && !selected.ReplacementPending)
                    FinalizeDay(selected, cfg);
                if (cfg.AllowMultipleSetupsPerDay <= 0)
                {
                    selected.DayLocked = true;
                    if (!selected.Blown && !selected.ReplacementPending) selected.LastState = "ONE SETUP / DAY LOCK";
                }
                else
                {
                    double profitLock = DailyProfitLock(selected, cfg);
                    double lossLock = DailyLossLock(selected, cfg);
                    // In one-day assignment mode there is no evaluation-credit ledger. Its stated
                    // rotation target is therefore measured from actual account DayPnl, exactly as
                    // it is for personal and funded scenarios. Only an active evaluation uses its
                    // capped evaluation-day credit for this lock.
                    double profitLockMeasure = cfg.EvaluationEnabled > 0 && !selected.Funded ? selected.EvalDayCredit : selected.DayPnl;
                    if (profitLock > 0 && profitLockMeasure >= profitLock) { selected.DayLocked = true; selected.LastState = "DAILY PROFIT LOCK"; }
                    if (lossLock > 0 && selected.DayPnl <= -Math.Abs(lossLock)) { selected.DayLocked = true; selected.LastState = "DAILY LOSS LOCK"; }
                }
            }
                for (accountIndex = 0; accountIndex < accounts.Count; accountIndex++)
                    if (!(accounts[accountIndex].Blown && !accounts[accountIndex].ReplacementPending && cfg.EvaluationEnabled == 0)) FinalizeDay(accounts[accountIndex], cfg);
            EnforceFirmFundedCapacity(accounts, cfg);
            return accounts;
        }

        // BH copy pool: every active slot receives each eligible event at the same time. This is
        // intentionally separate from rotation; it models a synchronized cohort, not an order
        // copier or live account action.
        private static List<KeystoneArcVirtualAccount> SimulateBhCopyPool(List<KeystoneArcEvent> events, KeystoneArcRunConfig cfg)
        {
            var accounts = new List<KeystoneArcVirtualAccount>();
            int count = Math.Max(1, cfg.PoolSize);
            DateTime initial = cfg.Start == DateTime.MinValue ? DateTime.MinValue : SessionGroupingDate(cfg.Start, cfg).Date;
            for (int i = 1; i <= count; i++)
            {
                KeystoneArcVirtualAccount account = CreatePoolAccount(cfg, i, initial, "COPY COHORT • EVALUATION READY");
                if (cfg.EvaluationEnabled == -2) account.LastState = "SINGLE ACCOUNT • COPY RANGE P/L ONLY";
                else if (cfg.EvaluationEnabled < 0) account.LastState = "ONE-DAY • COPY ASSIGNMENT";
                else if (cfg.EvaluationEnabled == 0) { account.Funded = true; account.EvaluationPassed = true; account.FundedSinceDate = initial; account.LastState = "COPY COHORT • FUNDED READY (ILLUSTRATIVE)"; }
                accounts.Add(account);
            }
            foreach (KeystoneArcEvent e in (events ?? new List<KeystoneArcEvent>()).OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime))
            {
                DateTime when = e.EntryTime == DateTime.MinValue ? e.TriggerTime : e.EntryTime;
                foreach (KeystoneArcVirtualAccount account in accounts)
                {
                    if (account.InitialLifecycleStart == DateTime.MinValue) { account.InitialLifecycleStart = when.Date; account.CurrentLifecycleStart = when.Date; }
                    if (account.Blown && !account.ReplacementPending && cfg.EvaluationEnabled == 0) continue;
                    RollDay(account, when.Date, cfg, accounts);
                }
                EnforceFirmFundedCapacity(accounts, cfg);
                List<KeystoneArcVirtualAccount> active = accounts.Where(x => !x.Blown && !x.ReplacementPending && !x.FundedCapPending && !x.DayLocked && x.FreeAt <= when).ToList();
                if (active.Count == 0)
                {
                    e.SkipReason = accounts.Any(x => x.ReplacementPending || x.ReplacementBudgetBlocked) ? "COPY COHORT • WAITING FOR REPLACEMENT" : "COPY COHORT • DAILY LOCK OR NO ACTIVE ACCOUNT";
                    continue;
                }
                // All active accounts are at the same lifecycle point under a synchronized copy
                // run, so the evaluation override (if selected) produces one shared outcome.
                ApplyEvaluationStageOutcomeForAccount(e, active[0], cfg);
                e.AssignedVirtualAccount = "COPY → " + active.Count + " ACTIVE ACCOUNT" + (active.Count == 1 ? string.Empty : "S");
                foreach (KeystoneArcVirtualAccount account in active)
                {
                    account.LastAssignedDate = when.Date;
                    account.FreeAt = e.ExitTime == DateTime.MinValue ? when : e.ExitTime;
                    account.Trades++; account.DayPnl += e.GrossPnl; account.TotalPnl += e.GrossPnl; account.PeakPnl = Math.Max(account.PeakPnl, account.TotalPnl);
                    if (e.GrossPnl > 0) account.Wins++; else if (e.GrossPnl < 0) account.Losses++;
                    if (cfg.EvaluationEnabled >= 0) ApplyIllustrativeLifecycle(account, e.GrossPnl, cfg);
                    if (cfg.EvaluationEnabled == 0 && account.Blown && !account.ReplacementPending) FinalizeDay(account, cfg);
                    if (cfg.AllowMultipleSetupsPerDay <= 0)
                    {
                        account.DayLocked = true;
                        if (!account.Blown && !account.ReplacementPending) account.LastState = "COPY COHORT • ONE SETUP / DAY LOCK";
                    }
                    else
                    {
                        double profitLock = DailyProfitLock(account, cfg), lossLock = DailyLossLock(account, cfg);
                        double lockMeasure = cfg.EvaluationEnabled > 0 && !account.Funded ? account.EvalDayCredit : account.DayPnl;
                        if (profitLock > 0 && lockMeasure >= profitLock) { account.DayLocked = true; account.LastState = "COPY COHORT • DAILY PROFIT LOCK"; }
                        if (lossLock > 0 && account.DayPnl <= -Math.Abs(lossLock)) { account.DayLocked = true; account.LastState = "COPY COHORT • DAILY LOSS LOCK"; }
                    }
                }
            }
            foreach (KeystoneArcVirtualAccount account in accounts)
                if (!(account.Blown && !account.ReplacementPending && cfg.EvaluationEnabled == 0)) FinalizeDay(account, cfg);
            EnforceFirmFundedCapacity(accounts, cfg);
            return accounts;
        }

        // Asian is copy trading, not rotation: one fully resolved 18:00 session result is copied
        // to every active virtual slot. Slots that are terminally blown (direct funded) or await a
        // next-session replacement are excluded without altering the historical cycle itself.
        private static List<KeystoneArcVirtualAccount> SimulateAsian75CopyPool(List<KeystoneArcEvent> events, KeystoneArcRunConfig cfg)
        {
            var accounts = new List<KeystoneArcVirtualAccount>();
            int count = Math.Max(1, cfg.PoolSize);
            DateTime initial = cfg.Start == DateTime.MinValue ? DateTime.MinValue : SessionGroupingDate(cfg.Start, cfg).Date;
            for (int i = 1; i <= count; i++)
            {
                var account = CreatePoolAccount(cfg, i, initial, "EVALUATION READY • ASIAN COPY");
                if (cfg.EvaluationEnabled == -2) account.LastState = "SINGLE ACCOUNT • ASIAN COPY RANGE P/L ONLY";
                else if (cfg.EvaluationEnabled < 0) account.LastState = "ONE-DAY • ASIAN COPY";
                else if (cfg.EvaluationEnabled == 0) { account.Funded = true; account.EvaluationPassed = true; account.FundedSinceDate = initial; account.LastState = "FUNDED READY • ASIAN COPY (ILLUSTRATIVE)"; }
                accounts.Add(account);
            }
            foreach (IGrouping<DateTime, KeystoneArcEvent> group in (events ?? new List<KeystoneArcEvent>()).Where(x => x != null).OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).GroupBy(x => SessionGroupingDate(x.TriggerTime, cfg)).OrderBy(x => x.Key))
            {
                DateTime day = group.Key;
                List<KeystoneArcEvent> cycle = group.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
                foreach (KeystoneArcVirtualAccount account in accounts)
                {
                    if (account.Blown && !account.ReplacementPending && cfg.EvaluationEnabled == 0) continue;
                    RollDay(account, day, cfg, accounts);
                }
                EnforceFirmFundedCapacity(accounts, cfg);
                List<KeystoneArcVirtualAccount> active = accounts.Where(x => !x.Blown && !x.ReplacementPending && !x.FundedCapPending && !x.DayLocked).ToList();
                if (active.Count == 0)
                {
                    foreach (KeystoneArcEvent e in cycle) e.SkipReason = "NO ACTIVE VIRTUAL COPY ACCOUNT";
                    continue;
                }
                double sessionPnl = cycle.Sum(x => x.GrossPnl);
                int wins = cycle.Count(x => x.GrossPnl > 0), losses = cycle.Count(x => x.GrossPnl < 0);
                string assigned = "COPY → " + active.Count + " ACTIVE ACCOUNT" + (active.Count == 1 ? string.Empty : "S");
                foreach (KeystoneArcEvent e in cycle) { e.AssignedVirtualAccount = assigned; e.SkipReason = string.Empty; }
                foreach (KeystoneArcVirtualAccount account in active)
                {
                    account.LastAssignedDate = day;
                    account.Trades += cycle.Count;
                    account.Wins += wins; account.Losses += losses;
                    account.DayPnl += sessionPnl;
                    account.TotalPnl += sessionPnl;
                    account.PeakPnl = Math.Max(account.PeakPnl, account.TotalPnl);
                    if (cfg.EvaluationEnabled >= 0) ApplyIllustrativeLifecycle(account, sessionPnl, cfg);
                    if (cfg.EvaluationEnabled == 0 && account.Blown && !account.ReplacementPending) FinalizeDay(account, cfg);
                    double profitLock = DailyProfitLock(account, cfg), lossLock = DailyLossLock(account, cfg);
                    double measure = cfg.EvaluationEnabled > 0 && !account.Funded ? account.EvalDayCredit : account.DayPnl;
                    if (profitLock > 0 && measure >= profitLock) { account.DayLocked = true; account.LastState = "DAILY PROFIT LOCK • ASIAN COPY"; }
                    if (lossLock > 0 && account.DayPnl <= -Math.Abs(lossLock)) { account.DayLocked = true; account.LastState = "DAILY LOSS LOCK • ASIAN COPY"; }
                }
            }
            foreach (KeystoneArcVirtualAccount account in accounts)
                if (!(account.Blown && !account.ReplacementPending && cfg.EvaluationEnabled == 0)) FinalizeDay(account, cfg);
            EnforceFirmFundedCapacity(accounts, cfg);
            return accounts;
        }

        private static List<KeystoneArcVirtualAccount> SimulateLiveAccount(List<KeystoneArcEvent> source, KeystoneArcRunConfig cfg)
        {
            var account = new KeystoneArcVirtualAccount { Name = "KA-LIVE", LastState = "LIVE ACCOUNT • HISTORICAL RANGE READY" };
            var events = (source ?? new List<KeystoneArcEvent>()).Where(x => x != null).OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
            for (int i = 0; i < events.Count; i++) { events[i].AssignedVirtualAccount = string.Empty; events[i].SkipReason = string.Empty; }

            var selected = new List<KeystoneArcEvent>();
            foreach (var dayGroup in events.GroupBy(x => SessionGroupingDate(x.TriggerTime, cfg)).OrderBy(x => x.Key))
            {
                // The live-account path is intentionally conservative: when BOTH is selected,
                // MNQ and MGC compete for the same single daily slot.  The earliest fully
                // resolved setup wins; the other instrument remains visible as evidence only.
                KeystoneArcEvent chosen = dayGroup
                    .Where(x => x.Outcome != "UNVERIFIED 1M" && x.Outcome != "NO ENTRY DATA" && !x.Outcome.StartsWith("OUTCOME BLOCKED", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime)
                    .ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                foreach (KeystoneArcEvent e in dayGroup.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime))
                {
                    if (object.ReferenceEquals(e, chosen))
                    {
                        e.AssignedVirtualAccount = account.Name;
                        selected.Add(e);
                    }
                    else if (e.Outcome == "UNVERIFIED 1M" || e.Outcome == "NO ENTRY DATA" || e.Outcome.StartsWith("OUTCOME BLOCKED", StringComparison.OrdinalIgnoreCase))
                        e.SkipReason = "LIVE OUTCOME UNAVAILABLE";
                    else
                        e.SkipReason = "LIVE MAX ONE SETUP PER SESSION DATE";
                }
            }

            double running = 0;
            foreach (KeystoneArcEvent e in selected.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime))
            {
                account.Trades++;
                account.TotalPnl += e.GrossPnl;
                running += e.GrossPnl;
                account.PeakPnl = Math.Max(account.PeakPnl, running);
                if (e.GrossPnl > 0) account.Wins++; else if (e.GrossPnl < 0) account.Losses++;
            }
            foreach (var day in events.GroupBy(x => SessionGroupingDate(x.TriggerTime, cfg)).OrderBy(x => x.Key))
            {
                List<KeystoneArcEvent> daySelected = selected.Where(x => SessionGroupingDate(x.TriggerTime, cfg) == day.Key).ToList();
                double dayPnl = daySelected.Sum(x => x.GrossPnl);
                account.DayHistory.Add(new KeystoneArcAccountDay
                {
                    Day = day.Key,
                    DayPnl = dayPnl,
                    DayLocked = daySelected.Count > 0,
                    EvaluationBalanceAfter = 0,
                    FundedBalanceAfter = 0,
                    StateAfterClose = daySelected.Count == 0 ? "LIVE DAY • NO RESOLVED TRADE" : "LIVE DAY • FIRST RESOLVED SETUP COMPLETE"
                });
            }
            account.ActiveDay = account.DayHistory.Count == 0 ? DateTime.MinValue : account.DayHistory[account.DayHistory.Count - 1].Day;
            account.DayPnl = account.DayHistory.Count == 0 ? 0 : account.DayHistory[account.DayHistory.Count - 1].DayPnl;
            account.LastState = "LIVE RANGE COMPLETE • ONE EARLIEST RESOLVED SETUP / SESSION DATE";
            return new List<KeystoneArcVirtualAccount> { account };
        }

        private static int CompareEventsByEntryTime(KeystoneArcEvent left, KeystoneArcEvent right)
        {
            DateTime leftTime = left.EntryTime == DateTime.MinValue ? left.TriggerTime : left.EntryTime;
            DateTime rightTime = right.EntryTime == DateTime.MinValue ? right.TriggerTime : right.EntryTime;
            return DateTime.Compare(leftTime, rightTime);
        }

        private static double DailyProfitLock(KeystoneArcVirtualAccount a, KeystoneArcRunConfig cfg)
        {
            // The regular DAILY PROFIT LOCK is the operational stop for every account.  The
            // evaluation daily-credit cap only limits what counts toward passing; it must not
            // silently let an evaluation keep trading beyond the stated daily target.
            if (cfg.EvaluationEnabled > 0 && !a.Funded)
            {
                double dailyTarget = Math.Max(0, cfg.DailyGoal);
                double creditCap = Math.Max(0, cfg.EvaluationDailyCreditCap);
                if (dailyTarget <= 0) return creditCap;
                return creditCap <= 0 ? dailyTarget : Math.Min(dailyTarget, creditCap);
            }
            return Math.Max(0, cfg.DailyGoal);
        }

        private static double DailyLossLock(KeystoneArcVirtualAccount a, KeystoneArcRunConfig cfg)
        {
            if (cfg.EvaluationEnabled > 0 && !a.Funded)
            {
                double regular = Math.Max(0, cfg.DailyLoss);
                double evalSpecific = Math.Max(0, cfg.EvaluationDailyLoss);
                return regular <= 0 ? evalSpecific : (evalSpecific <= 0 ? regular : Math.Min(regular, evalSpecific));
            }
            if (cfg.EvaluationEnabled >= 0 && a.Funded)
            {
                double regular = Math.Max(0, cfg.DailyLoss);
                double fundedSpecific = Math.Max(0, cfg.FundedDailyLoss);
                return regular <= 0 ? fundedSpecific : (fundedSpecific <= 0 ? regular : Math.Min(regular, fundedSpecific));
            }
            return Math.Max(0, cfg.DailyLoss);
        }

        private static void RollDay(KeystoneArcVirtualAccount a, DateTime day, KeystoneArcRunConfig cfg, IEnumerable<KeystoneArcVirtualAccount> pool)
        {
            if (a.ActiveDay == DateTime.MinValue) { a.ActiveDay = day; return; }
            if (a.ActiveDay == day) return;
            FinalizeDay(a, cfg);
            StartPendingReplacementAtNewSession(a, cfg, pool);
            a.ActiveDay = day;
            a.DayPnl = 0;
            a.EvalDayCredit = 0;
            a.DayLocked = false;
        }

        private static void StartPendingReplacementAtNewSession(KeystoneArcVirtualAccount a, KeystoneArcRunConfig cfg, IEnumerable<KeystoneArcVirtualAccount> pool)
        {
            if (a == null || !a.ReplacementPending || cfg.EvaluationEnabled <= 0 || string.Equals(cfg.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase)) return;
            if (cfg.ReplacementsRequirePayoutFunding > 0)
            {
                KeystoneArcCapitalPolicySummary capital = BuildCapitalPolicySummary(pool, cfg);
                // Replacements are released only when the modeled payout cash can fund the full
                // currently benched/pending replacement batch, rather than silently restarting
                // the first failed slot while the remaining failed slots are still unfunded.
                if (capital.ReplacementCashAvailable + 0.0001 < capital.CashRequiredForPendingReplacements)
                {
                    a.ReplacementBudgetBlocked = true;
                    a.LastState = "BENCHED • WAITING FOR PAYOUT-FUNDED REPLACEMENT (ILLUSTRATIVE)";
                    return;
                }
            }
            a.ReplacementPending = false;
            a.ReplacementFromFunded = false;
            a.ReplacementBudgetBlocked = false;
            a.FundedCapPending = false;
            a.Blown = false;
            a.Funded = false; a.EvaluationPassed = false;
            a.EvaluationBalance = 0; a.FundedBalance = 0;
            a.PositiveDays = 0; a.ConsecutiveEvalQualifyingDays = 0; a.FundedPositiveDays = 0; a.EvalDayCredit = 0; a.EvalBestPositiveDay = 0;
            a.EvaluationPurchases++;
            a.EvaluationCost += cfg.EvaluationCost;
            a.CurrentLifecycleStart = a.ActiveDay == DateTime.MinValue ? DateTime.MinValue : a.ActiveDay.AddDays(1);
            a.LastState = "NEW EVALUATION PURCHASED • REPLACEMENT (ILLUSTRATIVE)";
        }

        private static void FinalizeDay(KeystoneArcVirtualAccount a, KeystoneArcRunConfig cfg)
        {
            if (a.ActiveDay == DateTime.MinValue) return;
            int tradesBefore = a.Trades;
            int winsBefore = a.Wins;
            int lossesBefore = a.Losses;
            int purchasesBefore = a.EvaluationPurchases;
            double costBefore = a.EvaluationCost;
            int payoutsBefore = a.Payouts;
            double payoutGrossBefore = a.PayoutGrossWithdrawn;
            double payoutCashBefore = a.PayoutCash;
            double balanceBefore = a.Funded ? a.FundedBalance : a.EvaluationBalance;
            bool evalQualified = false;
            bool fundedQualified = false;
            if (cfg.EvaluationEnabled == -2)
            {
                a.LastState = a.Blown ? "SINGLE ACCOUNT • BLOWN" : "SINGLE ACCOUNT • RANGE P/L ONLY";
            }
            else if (string.Equals(cfg.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase))
            {
                a.LastState = a.Trades == 0 ? "LIVE ACCOUNT • UNUSED" :
                    (a.DayLocked ? (a.DayPnl >= Math.Max(0, cfg.DailyGoal) ? "DAILY PROFIT LOCK" : "DAILY LOSS LOCK") : "LIVE ACCOUNT • SESSION COMPLETE");
            }
            else if (cfg.EvaluationEnabled < 0)
            {
                a.LastState = a.Trades == 0 ? "ONE-DAY • UNUSED" :
                    (a.DayLocked ? (a.DayPnl >= Math.Max(0, cfg.DailyGoal) ? "DAILY PROFIT LOCK" : "DAILY LOSS LOCK") : "ONE-DAY • SESSION COMPLETE");
            }
            else if (a.ReplacementPending)
            {
                if (a.ReplacementBudgetBlocked)
                    a.LastState = "BENCHED • WAITING FOR PAYOUT-FUNDED REPLACEMENT (ILLUSTRATIVE)";
                else
                    a.LastState = a.ReplacementFromFunded
                        ? "FUNDED FAILURE • REPLACEMENT EVALUATION NEXT SESSION (ILLUSTRATIVE)"
                        : "EVALUATION FAILURE • REPLACEMENT EVALUATION NEXT SESSION (ILLUSTRATIVE)";
            }
            else if (a.Blown)
            {
                a.LastState = cfg.EvaluationEnabled == 0
                    ? "FUNDED BLOWN • DIRECT-FUNDED SLOT ENDED (ILLUSTRATIVE)"
                    : "ACCOUNT BLOWN • UNAVAILABLE";
            }
            else if (a.FundedCapPending)
            {
                a.LastState = "EVAL PASSED • FIRM FUNDED CAP WAIT (ILLUSTRATIVE)";
            }
            else if (!a.Funded)
            {
                // An evaluation pass is based on complete qualifying sessions.  The daily target
                // is the governing minimum when it is higher than the generic qualifying-day
                // field: for example, $1,000/day for three days requires three $1,000 days.
                double evalDayRequirement = Math.Max(Math.Max(0, cfg.MinimumQualifyingDayProfit), DailyProfitLock(a, cfg));
                evalQualified = a.EvalDayCredit >= evalDayRequirement;
                if (evalQualified)
                {
                    a.PositiveDays++;
                    a.ConsecutiveEvalQualifyingDays++;
                    a.EvalBestPositiveDay = Math.Max(a.EvalBestPositiveDay, a.EvalDayCredit);
                }
                else a.ConsecutiveEvalQualifyingDays = 0;
                bool consistencyOk = cfg.EvaluationConsistencyPercent <= 0 || a.EvaluationBalance <= 0 || a.EvalBestPositiveDay * 100.0 <= a.EvaluationBalance * cfg.EvaluationConsistencyPercent;
                if (a.EvaluationBalance >= cfg.EvaluationTarget && a.ConsecutiveEvalQualifyingDays >= cfg.MinimumPositiveDays && consistencyOk)
                {
                    a.Funded = true;
                    a.EvaluationPassed = true;
                    a.FundedCapPending = false;
                    a.FundedSinceDate = a.ActiveDay;
                    if (a.FirstEvaluationPassDate == DateTime.MinValue) a.FirstEvaluationPassDate = a.ActiveDay;
                    a.EvaluationPasses++;
                    a.FundedBalance = 0;
                    a.FundedPositiveDays = 0;
                    a.LastState = "FUNDED (ILLUSTRATIVE)";
                }
                else if (a.EvaluationBalance >= cfg.EvaluationTarget && a.ConsecutiveEvalQualifyingDays >= cfg.MinimumPositiveDays && !consistencyOk)
                    a.LastState = "EVAL TARGET MET • CONSISTENCY WAIT (ILLUSTRATIVE)";
                else if (a.LastState != "EVALUATION RESET (ILLUSTRATIVE)")
                    a.LastState = "EVALUATION IN PROGRESS (ILLUSTRATIVE)";
            }
            else
            {
                fundedQualified = a.DayPnl >= Math.Max(0, cfg.MinimumQualifyingDayProfit);
                if (fundedQualified) a.FundedPositiveDays++;
                if (a.FundedBalance >= cfg.PayoutThreshold && a.FundedPositiveDays >= cfg.PayoutDaysRequired)
                {
                    double balanceBeforeWithdrawal = a.FundedBalance;
                    double withdrawal = Math.Min(Math.Max(0, cfg.PayoutAmount), a.FundedBalance);
                    double netCash = withdrawal * Math.Max(0, Math.Min(100, cfg.PayoutProfitSharePercent)) / 100.0;
                    a.Payouts++;
                    a.LastPayoutDate = a.ActiveDay;
                    if (a.FirstPayoutDate == DateTime.MinValue) a.FirstPayoutDate = a.ActiveDay;
                    a.PayoutGrossWithdrawn += withdrawal;
                    a.PayoutCash += netCash;
                    a.FundedBalance -= withdrawal;
                    a.FundedPositiveDays = 0;
                    a.LastState = "PAYOUT RECORDED • GROSS " + withdrawal.ToString("C0") + " • NET SHARE " + netCash.ToString("C0") + " • BALANCE " + balanceBeforeWithdrawal.ToString("C0") + " → " + a.FundedBalance.ToString("C0") + " (ILLUSTRATIVE)";
                }
                else if (a.LastState != "FUNDED FAILURE → EVALUATION RESET (ILLUSTRATIVE)")
                    a.LastState = "FUNDED • PAYOUT PROGRESS (ILLUSTRATIVE)";
            }
            string closeState = a.LastState;
            if (a.DayLocked)
            {
                double profitLock = DailyProfitLock(a, cfg);
                double profitLockMeasure = cfg.EvaluationEnabled > 0 && !a.Funded ? a.EvalDayCredit : a.DayPnl;
                bool profitLocked = profitLockMeasure >= profitLock;
                string lockLabel = profitLocked ? "DAILY PROFIT LOCK" : "DAILY LOSS LOCK";
                closeState = closeState.StartsWith(lockLabel, StringComparison.OrdinalIgnoreCase) ? closeState : lockLabel + " • " + closeState;
            }
            a.DayHistory.Add(new KeystoneArcAccountDay
            {
                Day = a.ActiveDay,
                DayPnl = a.DayPnl,
                TradesAfter = Math.Max(0, a.Trades - tradesBefore),
                WinsAfter = Math.Max(0, a.Wins - winsBefore),
                LossesAfter = Math.Max(0, a.Losses - lossesBefore),
                BalanceBefore = balanceBefore,
                BalanceAfter = a.Funded ? a.FundedBalance : a.EvaluationBalance,
                CostDelta = a.EvaluationCost - costBefore,
                PayoutGrossDelta = a.PayoutGrossWithdrawn - payoutGrossBefore,
                PayoutCashDelta = a.PayoutCash - payoutCashBefore,
                EvaluationPurchaseDelta = Math.Max(0, a.EvaluationPurchases - purchasesBefore),
                EvaluationPassDateAfter = a.FirstEvaluationPassDate,
                FirstPayoutDateAfter = a.FirstPayoutDate,
                DayLocked = a.DayLocked,
                EvalQualifyingDay = evalQualified,
                FundedQualifyingDay = fundedQualified,
                EvaluationBalanceAfter = cfg.EvaluationEnabled == -2 ? a.StartingBalance + a.TotalPnl : a.EvaluationBalance,
                FundedBalanceAfter = cfg.EvaluationEnabled == -2 ? a.StartingBalance + a.TotalPnl : a.FundedBalance,
                EvaluationQualifyingDaysAfter = a.PositiveDays,
                ConsecutiveEvalQualifyingDaysAfter = a.ConsecutiveEvalQualifyingDays,
                FundedQualifyingDaysAfter = a.FundedPositiveDays,
                PayoutsAfter = a.Payouts,
                PayoutGrossAfter = a.PayoutGrossWithdrawn,
                PayoutCashAfter = a.PayoutCash,
                EvaluationPurchasesAfter = a.EvaluationPurchases,
                EvaluationPassesAfter = a.EvaluationPasses,
                EvaluationCostAfter = a.EvaluationCost,
                FundedAfter = a.Funded,
                LifecycleStartAfter = a.CurrentLifecycleStart,
                ReplacementPendingAfter = a.ReplacementPending,
                FundedCapPendingAfter = a.FundedCapPending,
                PropFirmAfter = a.PropFirmCode,
                FundedSlotsInFirmAfter = UsesFirmFundedCap(cfg) ? FundedSlotsInFirm(new[] { a }, a.PropFirmCode) : 0,
                BlownAfter = a.Blown,
                StateAfterClose = closeState
            });
            a.LastState = closeState;
        }

        private static void ApplyIllustrativeLifecycle(KeystoneArcVirtualAccount a, double pnl, KeystoneArcRunConfig cfg)
        {
            if (cfg.EvaluationEnabled < 0) return;
            if (cfg.EvaluationEnabled == 0)
            {
                a.Funded = true;
                a.FundedBalance += pnl;
                if (a.FundedBalance <= -Math.Abs(cfg.FundedFailure))
                {
                    a.FailedFunded++;
                    a.Funded = false;
                    a.EvaluationPassed = false;
                    a.FundedPositiveDays = 0;
                    a.Blown = true;
                    a.ReplacementPending = false;
                    a.ReplacementFromFunded = false;
                    a.LastBlowoutDate = a.ActiveDay;
                    a.TerminalLifecycleEnd = a.ActiveDay;
                    a.DayLocked = true;
                    a.LastState = "FUNDED BLOWN • DIRECT-FUNDED SLOT ENDED (ILLUSTRATIVE)";
                }
                return;
            }
            if (!a.Funded)
            {
                if (pnl > 0)
                {
                    double credit = Math.Min(pnl, Math.Max(0, cfg.EvaluationDailyCreditCap - a.EvalDayCredit));
                    a.EvalDayCredit += credit;
                    a.EvaluationBalance += credit;
                }
                else a.EvaluationBalance += pnl;
                if (a.EvaluationBalance <= -Math.Abs(cfg.EvaluationFailure))
                {
                    a.FailedEvaluations++;
                    a.PositiveDays = 0;
                    a.ConsecutiveEvalQualifyingDays = 0;
                    a.EvalDayCredit = 0;
                    a.EvalBestPositiveDay = 0;
                    a.ReplacementPending = true;
                    a.ReplacementFromFunded = false;
                    a.FundedCapPending = false;
                    a.Blown = true;
                    a.LastBlowoutDate = a.ActiveDay;
                    a.DayLocked = true;
                    a.LastState = "EVALUATION FAILURE • REPLACEMENT NEXT SESSION (ILLUSTRATIVE)";
                }
            }
            else
            {
                a.FundedBalance += pnl;
                if (a.FundedBalance <= -Math.Abs(cfg.FundedFailure))
                {
                    a.FailedFunded++;
                    a.Funded = false;
                    a.EvaluationPassed = false;
                    a.PositiveDays = 0;
                    a.ConsecutiveEvalQualifyingDays = 0;
                    a.FundedPositiveDays = 0;
                    a.EvalDayCredit = 0;
                    a.EvalBestPositiveDay = 0;
                    a.Blown = true;
                    a.ReplacementPending = cfg.EvaluationEnabled > 0;
                    a.ReplacementFromFunded = cfg.EvaluationEnabled > 0;
                    a.FundedCapPending = false;
                    a.LastBlowoutDate = a.ActiveDay;
                    if (cfg.EvaluationEnabled == 0) a.TerminalLifecycleEnd = a.ActiveDay;
                    a.DayLocked = true;
                    a.LastState = cfg.EvaluationEnabled > 0
                        ? "FUNDED BLOWN • REPLACEMENT EVALUATION NEXT SESSION (ILLUSTRATIVE)"
                        : "FUNDED BLOWN • DIRECT-FUNDED SLOT ENDED (ILLUSTRATIVE)";
                }
            }
        }

        // Cumulative values in AccountDay are deliberately converted to daily deltas here.  A
        // report must never sum PayoutsAfter or PayoutCashAfter directly because that would
        // overstate simultaneous payout-cycle totals.
        public static List<KeystoneArcPayoutAuditDay> BuildPayoutAuditDays(IEnumerable<KeystoneArcVirtualAccount> source)
        {
            var rows = new List<KeystoneArcPayoutAuditDay>();
            foreach (KeystoneArcVirtualAccount account in source ?? Enumerable.Empty<KeystoneArcVirtualAccount>())
            {
                if (account == null) continue;
                int priorPayouts = 0, priorPurchases = 0;
                double priorGross = 0, priorNet = 0, priorCost = 0;
                foreach (KeystoneArcAccountDay day in account.DayHistory.OrderBy(x => x.Day))
                {
                    if (day == null) continue;
                    rows.Add(new KeystoneArcPayoutAuditDay
                    {
                        Account = account.Name ?? string.Empty,
                        DaySnapshot = day,
                        PayoutDelta = Math.Max(0, day.PayoutsAfter - priorPayouts),
                        PayoutGrossDelta = day.PayoutGrossAfter - priorGross,
                        PayoutCashDelta = day.PayoutCashAfter - priorNet,
                        EvaluationPurchaseDelta = Math.Max(0, day.EvaluationPurchasesAfter - priorPurchases),
                        EvaluationCostDelta = day.EvaluationCostAfter - priorCost
                    });
                    priorPayouts = day.PayoutsAfter;
                    priorPurchases = day.EvaluationPurchasesAfter;
                    priorGross = day.PayoutGrossAfter;
                    priorNet = day.PayoutCashAfter;
                    priorCost = day.EvaluationCostAfter;
                }
            }
            return rows.OrderBy(x => x.DaySnapshot.Day).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static KeystoneArcCapitalPolicySummary BuildCapitalPolicySummary(IEnumerable<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            List<KeystoneArcVirtualAccount> accounts = (source ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Where(x => x != null).ToList();
            var result = new KeystoneArcCapitalPolicySummary
            {
                GateEnabled = cfg != null && cfg.EvaluationEnabled > 0 && cfg.ReplacementsRequirePayoutFunding > 0,
                InitialEvaluationPurchases = cfg != null && cfg.EvaluationEnabled > 0 ? accounts.Count : 0,
                PayoutCashAfterShare = accounts.Sum(x => x.PayoutCash),
                TotalEvaluationCost = accounts.Sum(x => x.EvaluationCost),
                BenchedReplacementSlots = accounts.Count(x => x.ReplacementPending && x.ReplacementBudgetBlocked),
                PendingReplacementSlots = accounts.Count(x => x.ReplacementPending)
            };
            // The selected initial pool is a separate cash commitment. The optional gate compares
            // subsequent replacement purchases only with modeled payout cash after the account
            // share, avoiding any claim that trading P/L is withdrawable cash.
            result.InitialEvaluationInvestment = result.InitialEvaluationPurchases * (cfg == null ? 0 : Math.Max(0, cfg.EvaluationCost));
            result.ReplacementEvaluationCost = Math.Max(0, result.TotalEvaluationCost - result.InitialEvaluationInvestment);
            result.ReplacementEvaluationPurchases = cfg == null || cfg.EvaluationCost <= 0 ? 0 : (int)Math.Round(result.ReplacementEvaluationCost / cfg.EvaluationCost, MidpointRounding.AwayFromZero);
            result.PayoutCashAfterInitialInvestment = result.PayoutCashAfterShare - result.InitialEvaluationInvestment;
            // The optional policy is intentionally stricter than merely matching one new fee:
            // modeled payout cash must recover the initial selected evaluation investment and
            // any already-paid replacement cost before it is available to buy another batch.
            // This is a virtual cash gate, not a claim about a firm's actual payout timing.
            result.ReplacementCashAvailable = Math.Max(0, result.PayoutCashAfterShare - result.InitialEvaluationInvestment - result.ReplacementEvaluationCost);
            result.CashRequiredForPendingReplacements = result.PendingReplacementSlots * (cfg == null ? 0 : Math.Max(0, cfg.EvaluationCost));
            result.ReplacementCashShortfall = Math.Max(0, result.CashRequiredForPendingReplacements - result.ReplacementCashAvailable);
            result.FullNetCashAfterAllCosts = result.PayoutCashAfterShare - result.TotalEvaluationCost;

            // Build a chronological cash trail from immutable daily snapshots.  Initial purchases
            // are present in the first daily snapshot for every active slot; later positive
            // purchase deltas are replacement evaluations.  This makes the cost through the
            // actual first payout and the later break-even date auditable instead of deducting
            // only the initial account batch in every scenario.
            List<KeystoneArcPayoutAuditDay> audit = BuildPayoutAuditDays(accounts);
            var byDay = new Dictionary<DateTime, KeystoneArcCapitalPolicyDay>();
            foreach (IGrouping<string, KeystoneArcPayoutAuditDay> accountRows in audit.GroupBy(x => x.Account ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                // All selected accounts are purchased at the start of the test, including slots
                // whose first assigned trade occurs later.  Therefore the first daily snapshot
                // for a slot establishes its initial purchase baseline and must not be added to
                // the later replenishment timeline.
                bool baseline = true;
                foreach (KeystoneArcPayoutAuditDay row in accountRows.OrderBy(x => x.DaySnapshot == null ? DateTime.MaxValue : x.DaySnapshot.Day))
                {
                    if (row == null || row.DaySnapshot == null) continue;
                    DateTime day = row.DaySnapshot.Day.Date;
                    KeystoneArcCapitalPolicyDay dayRow;
                    if (!byDay.TryGetValue(day, out dayRow))
                    {
                        dayRow = new KeystoneArcCapitalPolicyDay { Day = day };
                        byDay.Add(day, dayRow);
                    }
                    dayRow.PayoutCash += row.PayoutCashDelta;
                    if (!baseline && row.EvaluationPurchaseDelta > 0)
                    {
                        dayRow.ReplacementPurchases += row.EvaluationPurchaseDelta;
                        dayRow.ReplenishedSlots.Add(row.Account ?? string.Empty);
                    }
                    baseline = false;
                }
            }
            double cumulativePayout = 0;
            double cumulativeInvestment = result.InitialEvaluationInvestment;
            int cumulativePurchases = result.InitialEvaluationPurchases;
            int cumulativeReplacementPurchases = 0;
            var replenishedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeystoneArcCapitalPolicyDay dayRow in byDay.Values.OrderBy(x => x.Day))
            {
                // The cumulative cost derives from the daily purchase count, preserving the
                // configured fee even if an account was later blown or benched.
                int purchasesThisDay = dayRow.ReplacementPurchases;
                cumulativePurchases += purchasesThisDay;
                cumulativeReplacementPurchases += dayRow.ReplacementPurchases;
                cumulativeInvestment += purchasesThisDay * (cfg == null ? 0 : Math.Max(0, cfg.EvaluationCost));
                cumulativePayout += dayRow.PayoutCash;
                foreach (string name in dayRow.ReplenishedSlots) replenishedSlots.Add(name);

                if (!result.FirstPayoutReached && dayRow.PayoutCash > 0)
                {
                    result.FirstPayoutReached = true;
                    result.FirstPayoutDate = dayRow.Day;
                    result.PayoutCashThroughFirstPayoutDate = cumulativePayout;
                    result.InvestmentThroughFirstPayoutDate = cumulativeInvestment;
                    result.EvaluationPurchasesThroughFirstPayoutDate = cumulativePurchases;
                    result.ReplacementPurchasesThroughFirstPayoutDate = cumulativeReplacementPurchases;
                    result.ReplenishedSlotsThroughFirstPayoutDate = replenishedSlots.Count;
                    result.NetCashThroughFirstPayoutDate = cumulativePayout - cumulativeInvestment;
                }
                if (!result.ProfitabilityReached && cumulativePayout > 0 && cumulativePayout >= cumulativeInvestment)
                {
                    result.ProfitabilityReached = true;
                    result.ProfitabilityDate = dayRow.Day;
                    result.PayoutCashThroughProfitability = cumulativePayout;
                    result.InvestmentThroughProfitability = cumulativeInvestment;
                    result.EvaluationPurchasesThroughProfitability = cumulativePurchases;
                    result.ReplacementPurchasesThroughProfitability = cumulativeReplacementPurchases;
                    result.NetCashAtProfitability = cumulativePayout - cumulativeInvestment;
                }
            }
            // If the date range has no daily snapshots (for example no eligible setup), retain
            // truthful range totals without fabricating a first-cash or profitability date.
            if (!result.FirstPayoutReached) result.InvestmentThroughFirstPayoutDate = result.TotalEvaluationCost;
            if (!result.ProfitabilityReached)
            {
                result.InvestmentThroughProfitability = result.TotalEvaluationCost;
                result.PayoutCashThroughProfitability = result.PayoutCashAfterShare;
                result.NetCashAtProfitability = result.FullNetCashAfterAllCosts;
                result.EvaluationPurchasesThroughProfitability = cfg == null || cfg.EvaluationCost <= 0 ? 0 : (int)Math.Round(result.TotalEvaluationCost / cfg.EvaluationCost, MidpointRounding.AwayFromZero);
                result.ReplacementPurchasesThroughProfitability = result.ReplacementEvaluationPurchases;
            }
            return result;
        }

        public static KeystoneArcFirstPayoutTiming GetFirstPayoutTiming(KeystoneArcVirtualAccount account)
        {
            var result = new KeystoneArcFirstPayoutTiming();
            if (account == null || account.DayHistory.Count == 0) return result;
            List<KeystoneArcAccountDay> days = account.DayHistory.OrderBy(x => x.Day).ToList();
            result.InitialSlotStart = account.InitialLifecycleStart == DateTime.MinValue ? days[0].Day : account.InitialLifecycleStart;
            int priorPayouts = 0;
            DateTime lifeStart = result.InitialSlotStart;
            for (int i = 0; i < days.Count; i++)
            {
                KeystoneArcAccountDay day = days[i];
                if (day.LifecycleStartAfter != DateTime.MinValue) lifeStart = day.LifecycleStartAfter;
                if (day.PayoutsAfter > priorPayouts)
                {
                    result.HasPayout = true;
                    result.FirstPayoutDate = day.Day;
                    result.LifecycleStartForFirstPayout = lifeStart;
                    result.CalendarDaysFromInitial = Math.Max(0, (day.Day.Date - result.InitialSlotStart.Date).Days);
                    result.RecordedSessionsFromInitial = days.Count(x => x.Day >= result.InitialSlotStart && x.Day <= day.Day);
                    result.CalendarDaysFromLifecycleStart = Math.Max(0, (day.Day.Date - lifeStart.Date).Days);
                    result.RecordedSessionsFromLifecycleStart = days.Count(x => x.Day >= lifeStart && x.Day <= day.Day);
                    return result;
                }
                priorPayouts = day.PayoutsAfter;
            }
            return result;
        }

        public static List<KeystoneArcFirstReturnRow> BuildFirstReturnRows(IEnumerable<KeystoneArcVirtualAccount> source)
        {
            var rows = new List<KeystoneArcFirstReturnRow>();
            foreach (KeystoneArcVirtualAccount account in (source ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Where(x => x != null).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                List<KeystoneArcAccountDay> days = account.DayHistory.Where(x => x != null).OrderBy(x => x.Day).ToList();
                var row = new KeystoneArcFirstReturnRow
                {
                    Account = account.Name ?? string.Empty,
                    InitialSlotStart = account.InitialLifecycleStart == DateTime.MinValue ? (days.Count == 0 ? DateTime.MinValue : days[0].Day) : account.InitialLifecycleStart
                };
                int priorPayouts = 0;
                double priorGross = 0, priorCash = 0;
                for (int i = 0; i < days.Count; i++)
                {
                    KeystoneArcAccountDay day = days[i];
                    if (day.PayoutsAfter > priorPayouts)
                    {
                        row.HasPayout = true;
                        row.FirstPayoutDate = day.Day;
                        row.FirstPayoutGross = day.PayoutGrossAfter - priorGross;
                        row.FirstPayoutCashAfterShare = day.PayoutCashAfter - priorCash;
                        row.EvaluationCostThroughFirstPayout = day.EvaluationCostAfter;
                        // PayoutCashAfter is all cash recorded through the first return. Before
                        // that date it is zero; costs include every initial/replacement purchase.
                        row.FullNetReturnAfterAllCosts = day.PayoutCashAfter - day.EvaluationCostAfter;
                        row.CalendarDaysFromInitial = row.InitialSlotStart == DateTime.MinValue ? 0 : Math.Max(0, (day.Day.Date - row.InitialSlotStart.Date).Days);
                        row.RecordedSessionsFromInitial = row.InitialSlotStart == DateTime.MinValue ? 0 : days.Count(x => x.Day >= row.InitialSlotStart && x.Day <= day.Day);
                        row.StateAtFirstPayout = day.StateAfterClose ?? string.Empty;
                        break;
                    }
                    priorPayouts = day.PayoutsAfter;
                    priorGross = day.PayoutGrossAfter;
                    priorCash = day.PayoutCashAfter;
                }
                rows.Add(row);
            }
            return rows;
        }

        public static List<KeystoneArcPayoutCycleRow> BuildPayoutCycleRows(IEnumerable<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            List<KeystoneArcVirtualAccount> accounts = (source ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Where(x => x != null).ToList();
            List<KeystoneArcPayoutAuditDay> audit = BuildPayoutAuditDays(accounts);
            var rows = new List<KeystoneArcPayoutCycleRow>();
            foreach (IGrouping<DateTime, KeystoneArcPayoutAuditDay> group in audit.Where(x => x.PayoutDelta > 0).GroupBy(x => x.DaySnapshot.Day.Date).OrderBy(x => x.Key))
            {
                var row = new KeystoneArcPayoutCycleRow
                {
                    Day = group.Key,
                    PayoutAccounts = group.Count(x => x.PayoutDelta > 0),
                    PayoutCycles = group.Sum(x => x.PayoutDelta),
                    GrossWithdrawals = group.Sum(x => x.PayoutGrossDelta),
                    NetCashAfterShare = group.Sum(x => x.PayoutCashDelta),
                    EvaluationPurchases = audit.Where(x => x.DaySnapshot.Day.Date == group.Key).Sum(x => x.EvaluationPurchaseDelta),
                    EvaluationCostOnDate = audit.Where(x => x.DaySnapshot.Day.Date == group.Key).Sum(x => x.EvaluationCostDelta)
                };
                row.NetCashAfterEvaluationCost = row.NetCashAfterShare - row.EvaluationCostOnDate;
                row.CumulativePayoutCashThroughDate = audit.Where(x => x.DaySnapshot.Day.Date <= group.Key).Sum(x => x.PayoutCashDelta);
                row.CumulativeEvaluationCostThroughDate = audit.Where(x => x.DaySnapshot.Day.Date <= group.Key).Sum(x => x.EvaluationCostDelta);
                row.CumulativeFullNetCashAfterAllCosts = row.CumulativePayoutCashThroughDate - row.CumulativeEvaluationCostThroughDate;
                row.PayoutContributors.AddRange(group.OrderBy(x => x.Account, StringComparer.OrdinalIgnoreCase).Select(x => x.Account + " ×" + x.PayoutDelta));
                foreach (KeystoneArcVirtualAccount account in accounts)
                {
                    KeystoneArcAccountDay snapshot = account.DayHistory.Where(x => x.Day <= group.Key).OrderByDescending(x => x.Day).FirstOrDefault();
                    if (snapshot == null)
                    {
                        if (account.Blown && !account.ReplacementPending) row.TerminalBlown++;
                        else if (account.ReplacementPending) row.ReplacementNextSession++;
                        else if (account.Funded)
                        {
                            row.FundedAtClose++;
                            if (cfg != null && account.FundedBalance < cfg.PayoutThreshold) row.FundedBelowPayoutGoal++;
                        }
                        else row.EvaluationInProgress++;
                        continue;
                    }
                    if (snapshot.BlownAfter && !snapshot.ReplacementPendingAfter) row.TerminalBlown++;
                    else if (snapshot.ReplacementPendingAfter) row.ReplacementNextSession++;
                    else if (snapshot.FundedAfter)
                    {
                        row.FundedAtClose++;
                        if (cfg != null && snapshot.FundedBalanceAfter < cfg.PayoutThreshold) row.FundedBelowPayoutGoal++;
                    }
                    else row.EvaluationInProgress++;
                }
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++) rows[i].CycleNumber = i + 1;
            return rows;
        }

        public static List<KeystoneArcBalanceMatrixRow> BuildBalanceMatrixRows(IEnumerable<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            List<KeystoneArcVirtualAccount> accounts = (source ?? Enumerable.Empty<KeystoneArcVirtualAccount>()).Where(x => x != null).ToList();
            List<KeystoneArcPayoutAuditDay> audit = BuildPayoutAuditDays(accounts);
            var rows = new List<KeystoneArcBalanceMatrixRow>();
            foreach (KeystoneArcPayoutCycleRow cycle in BuildPayoutCycleRows(accounts, cfg))
            {
                foreach (KeystoneArcVirtualAccount account in accounts)
                {
                    KeystoneArcAccountDay snapshot = account.DayHistory.Where(x => x.Day <= cycle.Day).OrderByDescending(x => x.Day).FirstOrDefault();
                    if (snapshot == null)
                    {
                        string unstarted = account.Blown && !account.ReplacementPending ? "TERMINAL BLOWN" : (account.ReplacementPending ? "REPLACEMENT NEXT SESSION" : (account.Funded ? "FUNDED BELOW PAYOUT GOAL" : "NO RECORDED SESSION YET"));
                        rows.Add(new KeystoneArcBalanceMatrixRow { CycleNumber = cycle.CycleNumber, Day = cycle.Day, Account = account.Name, Status = unstarted, BalanceBefore = 0, GrossPayout = 0, CarryForwardBalance = 0, DayPnl = 0 });
                        continue;
                    }
                    KeystoneArcPayoutAuditDay transaction = audit.FirstOrDefault(x => string.Equals(x.Account, account.Name, StringComparison.OrdinalIgnoreCase) && x.DaySnapshot.Day.Date == cycle.Day.Date);
                    double gross = transaction == null ? 0 : transaction.PayoutGrossDelta;
                    double carry = snapshot.FundedAfter ? snapshot.FundedBalanceAfter : snapshot.EvaluationBalanceAfter;
                    string status = gross > 0 ? "PAID" : (snapshot.BlownAfter && !snapshot.ReplacementPendingAfter ? "TERMINAL BLOWN" : (snapshot.ReplacementPendingAfter ? "REPLACEMENT NEXT SESSION" : (snapshot.FundedAfter ? "FUNDED BELOW PAYOUT GOAL" : "EVALUATION IN PROGRESS")));
                    rows.Add(new KeystoneArcBalanceMatrixRow
                    {
                        CycleNumber = cycle.CycleNumber,
                        Day = cycle.Day,
                        Account = account.Name,
                        Status = status,
                        BalanceBefore = gross > 0 ? snapshot.FundedBalanceAfter + gross : carry,
                        GrossPayout = gross,
                        CarryForwardBalance = carry,
                        DayPnl = snapshot.DayPnl
                    });
                }
            }
            return rows.OrderBy(x => x.CycleNumber).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool InsideSession(DateTime t, string symbol, KeystoneArcRunConfig cfg)
        {
            int hhmm = t.Hour * 100 + t.Minute;
            string mode = (cfg.SessionMode ?? "INSTRUMENT_DEFAULT").ToUpperInvariant();
            if (mode == "FULL_GLOBEX" || mode == "ALL_ELIGIBLE") return InSessionWindow(hhmm, 1800, cfg.EndTime);
            if (mode == "ASIAN75") return InSessionWindow(hhmm, cfg.AsianStartHhmm, cfg.AsianEndHhmm);
            if (mode == "ASIA" || mode == "ASIAN") return InSessionWindow(hhmm, 1900, 300);
            if (mode == "LONDON") return InSessionWindow(hhmm, 300, 1130);
            if (mode == "NY_EARLY") return InSessionWindow(hhmm, 800, cfg.EndTime);
            if (mode == "NY_OPEN" || mode == "NY_AFTER_0930" || mode == "CUSTOM") return InSessionWindow(hhmm, cfg.CustomStart, cfg.EndTime);
            int start = IsMgc(symbol) ? cfg.MgcStart : cfg.MnqStart;
            return InSessionWindow(hhmm, start, cfg.EndTime);
        }

        private static bool InSessionWindow(int hhmm, int start, int end)
        {
            if (start <= end) return hhmm >= start && hhmm <= end;
            return hhmm >= start || hhmm <= end;
        }

        private static string Strength(List<KeystoneArcBar> bars, int i)
        {
            if (i < 3) return "BASE";
            double redBody = 0;
            int red = 0;
            for (int x = i - 2; x >= 0 && x >= i - 7; x--)
            {
                if (bars[x].Close >= bars[x].Open) break;
                red++;
                redBody += bars[x].Open - bars[x].Close;
            }
            double referenceBody = Math.Abs(bars[i - 1].Close - bars[i - 1].Open);
            if (red >= 3 || redBody > Math.Max(0.0001, referenceBody) * 3.0) return "AGGR";
            double wick = Math.Min(bars[i - 1].Open, bars[i - 1].Close) - bars[i - 1].Low;
            if (wick > Math.Max(0.0001, referenceBody) * 0.5) return "WICK";
            return "BASE";
        }

        private static bool IsMgc(string s) { return (s ?? string.Empty).StartsWith("MGC", StringComparison.OrdinalIgnoreCase); }
        // A selected session date starts at 18:00 Eastern and ends on the following calendar day before the next 18:00 open.
        public static DateTime TradingSessionDate(DateTime time)
        {
            return time.TimeOfDay < new TimeSpan(18, 0, 0) ? time.Date.AddDays(-1) : time.Date;
        }
        public static bool UsesOvernightSessionDate(KeystoneArcRunConfig cfg)
        {
            string mode = cfg == null ? string.Empty : (cfg.SessionMode ?? string.Empty).ToUpperInvariant();
            if (mode == "ASIAN75") return cfg.AsianStartHhmm > cfg.AsianEndHhmm;
            return mode == "FULL_GLOBEX" || mode == "ASIA" || mode == "ALL_ELIGIBLE" || mode == "ASIAN" || (mode == "CUSTOM" && cfg.CustomStart > cfg.EndTime);
        }
        public static DateTime SessionGroupingDate(DateTime time, KeystoneArcRunConfig cfg)
        {
            if (cfg != null && string.Equals(cfg.SessionMode, "ASIAN75", StringComparison.OrdinalIgnoreCase))
            {
                int hhmm = time.Hour * 100 + time.Minute;
                bool overnight = cfg.AsianStartHhmm > cfg.AsianEndHhmm;
                return overnight && hhmm < cfg.AsianStartHhmm ? time.Date.AddDays(-1) : time.Date;
            }
            return UsesOvernightSessionDate(cfg) ? TradingSessionDate(time) : time.Date;
        }
    }
}

namespace NinjaTrader.NinjaScript.Indicators
{
    using System.ComponentModel.DataAnnotations;
    // Separate chart-review study. It draws the same filtered ledger events published by the Keystone AddOn.
    public class KeystoneArc5MChartStudy : Indicator
    {
        [NinjaScriptProperty]
        [Range(0, 1)]
        [Display(Name = "Show completed outcomes (0=No, 1=Yes)", GroupName = "Keystone Arc", Order = 1)]
        public int ShowOutcomes { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "Keystone Arc 5M Chart Study";
                Description = "Research-only chart study fed by the isolated Keystone Arc event ledger; supports selected 1, 5, 15, 30, 60, or 240 minute setup bars.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                ShowOutcomes = 1;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsPeriod == null || BarsPeriod.BarsPeriodType != BarsPeriodType.Minute || (BarsPeriod.Value != 1 && BarsPeriod.Value != 5 && BarsPeriod.Value != 15 && BarsPeriod.Value != 30 && BarsPeriod.Value != 60 && BarsPeriod.Value != 240) || CurrentBar < 1) return;
            string symbol = Instrument == null ? string.Empty : Instrument.FullName;
            if (!KeystoneArcHub.ShowChartMarks || string.IsNullOrWhiteSpace(symbol)) return;
            List<KeystoneArcEvent> events = KeystoneArcHub.SnapshotAt(symbol, Time[0]);
            foreach (KeystoneArcEvent e in events)
            {
                Brush brush = e.SetupClass == "DT" ? Brushes.Turquoise : (e.SetupClass == "FVG" ? Brushes.MediumOrchid : Brushes.DodgerBlue);
                string tag = "KA_STUDY_" + e.Id.Replace("|", "_");
                if (KeystoneArcHub.ShowChartFvgZones && (e.SetupClass == "FVG" || e.SetupClass == "DT") && !double.IsNaN(e.FvgLower) && !double.IsNaN(e.FvgUpper) && e.FvgFormedTime != DateTime.MinValue)
                    Draw.Rectangle(this, tag + "_FVG", false, e.FvgFormedTime, e.FvgUpper, Time[0], e.FvgLower, Brushes.MediumOrchid, Brushes.MediumOrchid, 10, true);
                Draw.ArrowLine(this, tag + "_LINE", false, 1, e.Entry, 0, e.Entry, brush, NinjaTrader.Gui.DashStyleHelper.Solid, 3, true);
                string outcome = ShowOutcomes == 1 && KeystoneArcHub.ShowChartOutcomeLabels ? OutcomeTag(e) : string.Empty;
                Brush outcomeBrush = e.Outcome.StartsWith("WIN") ? Brushes.LimeGreen : (e.Outcome.StartsWith("LOSS") ? Brushes.OrangeRed : brush);
                Draw.Text(this, tag + "_TEXT", false, e.SetupClass + outcome, Time[0], e.Entry + TickSize * 4, 0, outcomeBrush,
                    new SimpleFont("Segoe UI Semibold", 11), System.Windows.TextAlignment.Left, outcomeBrush, Brushes.Black, 88);
            }
        }

        private static string OutcomeTag(KeystoneArcEvent e)
        {
            if (e == null) return string.Empty;
            if (e.Outcome == "WIN") return " • WIN";
            if (e.Outcome.StartsWith("LOSS")) return " • LOSS";
            if (e.Outcome == "SESSION EXIT") return " • EXIT";
            if (e.Outcome == "NO ENTRY DATA") return " • NO ENTRY";
            return string.Empty;
        }
    }
}

namespace NinjaTrader.NinjaScript.AddOns
{
    public sealed class KeystoneArc5MResearchLab : AddOnBase
    {
        private sealed class HistoricalRequestWorkItem
        {
            public string Key;
            public Instrument Instrument;
            public DateTime Start;
            public DateTime End;
            public int Minutes;
            public bool IsSetupRequest;
            // The 1-minute outcome request must use the same session template as the selected
            // setup bars.  A chart can have several MNQ/MGC tabs with different templates, so
            // resolving it independently for each request can manufacture an OHLC mismatch.
            public TradingHours TradingHours;
            public string TradingHoursSource;
            // A continuation is merged into the prior response for the same instrument/series.
            // This is used only when NinjaTrader returns an overnight request through midnight.
            public bool Append;
            // Retry uses an actual open chart contract or Default 24 x 7 only after a zero/error response.
            // It never splits the selected range into calendar fragments.
            public Instrument FallbackInstrument;
            public int FallbackStage;
            // Final old-history recovery: each item is an adjacent, date-compatible futures
            // contract interval. This is deliberately distinct from calendar fragmentation.
            public bool ContractRolloverSegment;
            public long Run;
        }

        private const string MenuCaption = "KEYSTONE ARC 5M RESEARCH LAB";
        private NTMenuItem newMenu;
        private NTMenuItem launchItem;
        private DispatcherTimer launcherTimer;
        private Window window;
        private Window evidenceWindow;
        private Window comparisonWindow;
        private TextBlock statusText, workflowText, summaryText, poolText, lifecycleText, eventText, setupStatsText, mathText, reviewDetailText, poolDetailText, savedDataText, sessionHintText, strategyRuleText, strategyWorkflowText, reviewLedgerText, reviewPositionText, poolResultBanner, startModeHintText, instrumentSourceText, resultScopeText, resultsHeadingText, poolAccountsHeadingText, poolDetailHeadingText, poolTimelineHeadingText, poolGrossWithdrawalMetric, poolNetCashMetric, poolEvaluationCostMetric, poolNetCashAfterCostMetric, poolInitialInvestmentMetric, poolPayoutAfterInitialMetric, poolCapitalAvailabilityMetric, poolFirmCapMetric, poolPayoutCycleMetric, poolFundedMetric, poolReplacementMetric, poolBlownMetric, rangeEligibleMetric, rangeAssignedMetric, rangePnlMetric, rangePurchasesMetric, rangeCostMetric, rangeNetCashAfterCostMetric, rangeInitialInvestmentMetric, rangePayoutAfterInitialMetric, rangeCapitalAvailabilityMetric, rangeBlowoutMetric, oneDayEligibleMetric, oneDayAssignedMetric, oneDayAccountsTradedMetric, oneDayProfitLockedMetric, oneDayLossLockedMetric, oneDayUnusedMetric, oneDaySkippedMetric, oneDayPnlMetric, oneDayRangeSummaryText, accountBalanceMetric, accountPayoutCashMetric, accountCostMetric, accountNetCashAfterCostMetric, accountPayoutCyclesMetric, accountBlowoutMetric, accountLatestBlowoutMetric, accountLifecycleMetric, accountPnlMetric, accountTradesMetric, accountFirstPayoutMetric, accountFirstPayoutDaysMetric, oneDayAccountBalanceMetric, oneDayAccountPnlMetric, oneDayAccountTradesMetric, oneDayAccountStateMetric, oneDayAccountWindowMetric, oneDayAccountLimitsMetric, firstReturnDashboardText, dailySessionScoreboardText, comparisonStatusText, comparisonSummaryText, comparisonMatrixText;
        private TextBlock evidenceStatusText, evidenceLegendText, evidenceZoomText, evidencePnlText, evidenceStudyText, evidenceMetricsText, comparisonBusyText;
        private TextBox mnqBox, mgcBox, startBox, endBox, targetBox, stopBox, mnqStopOffsetBox, mgcStopOffsetBox, dailyGoalBox, dailyLossBox, asianStartTimeBox, asianEndTimeBox, asianReversalLossBox, asianMnqPriceMoveBox, asianMgcPriceMoveBox, asianCycleTargetBox, asianCombinedStopBox, asianDailyLossBox, asianInstrumentStopBox, asianMnqInstrumentStopBox, asianMgcInstrumentStopBox, asianBreakEvenBox, asianStartingQuantityBox, asianMaxReversalsBox, asianMnqMaxReversalsBox, asianMgcMaxReversalsBox, mnqStrongRedBox, mnqStrongDeclineBox, mgcStrongRedBox, mgcStrongDeclineBox, evalTargetBox, evalTradeTargetBox, evalTradeStopBox, payoutThresholdBox, payoutAmountBox, payoutProfitShareBox, mnqStartBox, mgcStartBox, customStartBox, endTimeBox, quantityBox, personalLotBox, mnqCashValueBox, mgcCashValueBox, mnqTargetMoveBox, mgcTargetMoveBox, mnqStandardStopMoveBox, mgcStandardStopMoveBox, personalMaxRiskBox, breakEvenMoveBox, evalDailyCapBox, evalConsistencyBox, evalFailureBox, evalDailyLossBox, fundedDailyLossBox, fundedFailureBox, payoutDaysBox, minimumDaysBox, minimumQualifyingDayBox, evalCostBox, firmEvalSlotsBox, firmMaxFundedBox, propStartingBalanceBox, personalStartingBalanceBox, reviewNoteBox;
        private TextBox evidenceDateBox;
        private ComboBox strategyBox, scopeBox, accountPathBox, directionBox, bhFilterBox, bhStrongCombineBox, stopModeBox, poolBox, sessionBox, accountStartModeBox, poolStateFilterBox, payoutCycleMonthFilterBox, chartReviewScopeBox, evidenceInstrumentBox, evidenceTimeframeBox, evidenceScopeBox, evidenceBarsBox, evidenceSessionFilterBox, evidenceStrengthBox, timeframeBox, dateModeBox, comparisonInstrumentBox, comparisonTimeframeBox, comparisonPoolBox, comparisonModeBox, comparisonSessionBox, asianDirectionBox, asianMnqDirectionBox, asianMgcDirectionBox, asianRiskModeBox;
        private TabControl evidenceInstrumentTabs;
        private StackPanel evidenceDateStrip;
        private ScrollViewer evidenceTabsScroll;
        private CheckBox outcomesBox, chartMarksBox, showWinsBox, showLossesBox, showExitsBox, showNoEntryBox, oneDayShowTradedBox, oneDayShowProfitLocksBox, oneDayShowLossLocksBox, oneDayShowUnusedBox, chartReviewEnabledBox, chartReviewBhBox, chartReviewFvgBox, chartReviewDtBox, chartReviewWinsBox, chartReviewLossesBox, chartReviewExitsBox, chartReviewNoEntryBox, chartReviewFvgZonesBox, bhSetupBox, fvgSetupBox, breakEvenBox, evalStageTradeRulesBox, replacementFundingGateBox, firmFundedCapBox, copyTradingPoolBox, multipleSetupsPerDayBox;
        private CheckBox evidenceWinsBox, evidenceLossesBox, evidenceExitsBox, evidenceNoEntryBox;
        private Button confirmConfigurationButton, requestButton, runButton, cancelButton, openReviewButton, resetNewTestButton, refreshMathButton, runPoolButton, saveButton, exportButton, clearButton, publishChartReviewButton, comparisonRunButton, clearPoolButton, evidenceAfterPoolButton, researchPackageButton, comparisonBuildButton, comparisonOptimizeButton, comparisonClearButton, comparisonExportButton;
        private ListBox reviewList, poolAccountList, savedRunList, comparisonList, payoutAccountList, firstReturnAccountList;
        private StackPanel poolAccountCardStack, poolTimelineStack, walkthroughTimelineStack, firstReturnDashboardStack, dailySessionScoreboardStack, payoutCycleDashboardStack, researchFindingsStack;
        private WrapPanel oneDayAccountFilters;
        private WrapPanel evalOverridePanel;
        private readonly List<UIElement> evaluationTradeRuleControls = new List<UIElement>();
        private readonly List<UIElement> firmCapControls = new List<UIElement>();
        private UniformGrid poolLifecycleMetrics, poolCashPolicyMetrics, oneDayPoolMetrics, rangeLifecycleMetrics, accountLifecycleMetrics, oneDayAccountMetrics;
        private Border firstReturnCard, payoutCycleCard;
        private TabItem portfolioCashResultsTab, walkthroughResultsTab, firstReturnResultsTab, dailySessionResultsTab, payoutCycleResultsTab, researchFindingsResultsTab;
        private ComboBox walkthroughAccountBox, periodGranularityBox;
        private TextBlock walkthroughHeadingText, walkthroughSummaryText, payoutAccountDetailText, firstReturnDetailText;
        private bool walkthroughSelectionUpdating, payoutAccountUpdating, firstReturnAccountUpdating;
        private Border oneDayRangeSummaryCard;
        private RowDefinition firstReturnDashboardRow, payoutCycleDashboardRow;
        private ScrollViewer poolAccountScroll;
        private Canvas evidenceCanvas;
        private ScrollViewer evidenceScroll;
        private ScrollBar evidenceHorizontalScrollBar, evidenceVerticalScrollBar;
        private StackPanel evidenceControlsPanel;
        private Border evidenceDetailBorder, evidencePnlBorder;
        private Button evidenceControlsToggle;
        private bool evidenceControlsVisible;
        private bool asianScopeDefaultApplied;
        private double evidenceZoom = 1.0;
        private double evidenceHorizontalZoom = 1.0, evidenceVerticalZoom = 1.0;
        private DispatcherTimer evidenceSelectionTimer;
        private int evidenceSelectionPulse;
        private bool evidenceSelectionPulseOn;
        private bool evidenceCloseConfirmed;
        private bool evidencePanning;
        private Action pendingEvidenceSelectionAction;
        private Point evidencePanStart;
        private double evidencePanHorizontalStart, evidencePanVerticalStart;
            // 0 = free plot pan, 1 = bottom time-scale drag, 2 = right price-scale drag.
        private int evidenceScaleDragAxis;
        private Point evidenceScaleDragStart;
        private double evidenceScaleDragStartZoom;
        private double evidenceScaleDragStartHorizontalZoom, evidenceScaleDragStartVerticalZoom;
        private int evidenceScaleDragStartFirstBar;
        private double evidencePriceCenter = double.NaN;
        private double evidenceVisiblePriceRange;
        private double evidenceScrollPriceMinimum, evidenceScrollPriceMaximum;
        private bool evidenceNavigationUpdating;
        private double evidenceScaleDragStartPriceCenter;
        private int evidenceFirstVisibleBar;
        private bool evidenceCrosshairVisible;
        private Point evidenceCrosshairPoint;
        private string evidenceLastAnimatedRenderKey;
        // --- Interaction smoothness -------------------------------------------------
        // Every pan/zoom/hover tick used to call RenderEvidenceChart() synchronously,
        // which clears and rebuilds every candle, marker, and label from scratch. A
        // mouse reports far more move events than the screen can paint, so interaction
        // speed was capped by full-rebuild cost instead of the display refresh rate.
        // These fields coalesce any number of updates that arrive within one frame
        // into a single real render on CompositionTarget.Rendering (~60 fps), and let
        // a pure hover reposition a persistent crosshair overlay instead of rebuilding
        // the whole chart just to move a dashed line.
        private bool evidenceRenderQueued;
        private bool evidenceFullRenderNeeded = true;
        private double evidenceLayoutLeft, evidenceLayoutRight, evidenceLayoutTop, evidenceLayoutBottom;
        private double evidenceLayoutWidth, evidenceLayoutHeight;
        private double evidenceLayoutMinPrice, evidenceLayoutMaxPrice;
        private double evidenceLayoutCandleWidth;
        private int evidenceLayoutLastPossibleFirst;
        private TranslateTransform evidencePanOverscrollTransform;
        private List<KeystoneArcBar> evidenceLayoutBars;
        private System.Windows.Shapes.Line evidenceCrosshairHLine, evidenceCrosshairVLine;
        private TextBlock evidenceCrosshairPriceLabel, evidenceCrosshairTimeLabel;
        private TabControl workspaceTabs;
        private TabItem configureTab, reviewTab, resultsTab, savedRunsTab;
        private Border activityOverlay, comparisonBusyOverlay;
        private TextBlock activityText;
        private DispatcherTimer activityTimer;
        private bool operationBusy;
        private bool payoutCycleFilterUpdating;
        private string operationMessage = string.Empty;
        private int activityFrame;
        private Border confirmationOverlay;
        private TextBlock confirmationTitleText, confirmationBodyText;
        private Button confirmationContinueButton, confirmationSaveCloseButton, confirmationExportCloseButton;
        private Action confirmationAction;
        private bool closeConfirmed;
        private UIElement lifecycleControlsPanel, poolAccountsCard, poolDetailCard;
        private StackPanel lifecyclePolicySection, governanceSection;
        private readonly List<UIElement> evaluationOnlyControls = new List<UIElement>();
        private readonly List<UIElement> oneDayHiddenControls = new List<UIElement>();
        private readonly List<UIElement> propOnlyControls = new List<UIElement>();
        private readonly List<UIElement> personalOnlyControls = new List<UIElement>();
        private readonly List<UIElement> liveConfigurationControls = new List<UIElement>();
        private readonly List<UIElement> propConfigurationControls = new List<UIElement>();
        private readonly List<UIElement> lowStopControls = new List<UIElement>();
        private readonly List<UIElement> autoRiskLotControls = new List<UIElement>();
        private readonly List<UIElement> singleAccountControls = new List<UIElement>();
        private readonly List<UIElement> strongerBhControls = new List<UIElement>();
        private readonly List<UIElement> customSessionControls = new List<UIElement>();
        private readonly List<UIElement> bhStrategyControls = new List<UIElement>();
        private readonly List<UIElement> asianStrategyControls = new List<UIElement>();
        private readonly List<UIElement> asianCashRiskControls = new List<UIElement>();
        private readonly List<UIElement> asianPriceRiskControls = new List<UIElement>();
        private readonly List<Button> saveButtons = new List<Button>();
        private readonly List<Button> exportButtons = new List<Button>();
        private List<KeystoneArcBar> mnqBars = new List<KeystoneArcBar>();
        private List<KeystoneArcBar> mgcBars = new List<KeystoneArcBar>();
        private List<KeystoneArcBar> mnqSetupBars = new List<KeystoneArcBar>();
        private List<KeystoneArcBar> mgcSetupBars = new List<KeystoneArcBar>();
        private bool mnqSetupFromOpenChart, mgcSetupFromOpenChart, mnqSetupDerivedFromOpenOneMinute, mgcSetupDerivedFromOpenOneMinute, mnqOutcomeMatchesSetup = true, mgcOutcomeMatchesSetup = true;
        private bool mnqOutcomeFromOpenChart, mgcOutcomeFromOpenChart;
        private string mnqOutcomeValidation = "not checked", mgcOutcomeValidation = "not checked";
        private readonly List<KeystoneArcComparisonRow> comparisonRows = new List<KeystoneArcComparisonRow>();
        private readonly List<KeystoneArcOptimizationRow> optimizationRows = new List<KeystoneArcOptimizationRow>();
        private readonly Dictionary<string, List<KeystoneArcBar>> comparisonSetupCache = new Dictionary<string, List<KeystoneArcBar>>();
        private readonly Dictionary<string, string> comparisonSeriesStatus = new Dictionary<string, string>();
        private KeystoneArcRunConfig comparisonRunConfig;
        private int comparisonPendingRequests;
        private long comparisonGeneration;
        private readonly Queue<HistoricalRequestWorkItem> comparisonRequestQueue = new Queue<HistoricalRequestWorkItem>();
        private bool comparisonRequestActive;
        private BarsRequest comparisonRequest;
        private readonly Dictionary<string, Instrument> openChartInstruments = new Dictionary<string, Instrument>(StringComparer.OrdinalIgnoreCase);
        // Keep the exact Instrument objects resolved at Step 1. Do not round-trip a futures FullName through a master-symbol lookup before BarsRequest.
        private Instrument configuredMnqInstrument, configuredMgcInstrument;
        private readonly List<string> historicalRequestDetails = new List<string>();
        private bool isProcessing;
        private List<KeystoneArcEvent> events = new List<KeystoneArcEvent>();
        private List<KeystoneArcEvent> reviewRows = new List<KeystoneArcEvent>();
        private List<KeystoneArcVirtualAccount> accounts = new List<KeystoneArcVirtualAccount>();
        private List<string> savedFiles = new List<string>();
        private List<KeystoneArcBar> evidenceBars = new List<KeystoneArcBar>();
        private List<KeystoneArcEvent> evidencePreviewEvents = new List<KeystoneArcEvent>();
        private int evidencePreviewMinutes;
        private bool evidencePreviewOwnLedger;
        private KeystoneArcEvent selectedEvidenceEvent;
        private TextBlock evidenceDetailText;
        private TextBlock evidenceHoverText;
        private Border evidenceHoverBorder;
        private bool evidenceSelectionClickHandled;
        private readonly List<Button> evidenceDateButtons = new List<Button>();
        private int evidenceSelectedDateIndex = -1;
        private bool autoRunResearchAfterLoad;
        // A completed Asian cycle may legitimately have zero legs when an exact configured
        // opening bar is unavailable. Keep that distinct from a cycle still running.
        private bool researchRunCompleted;
        private bool unsavedResearch;
        private BarsRequest mnqRequest, mgcRequest, mnqSetupRequest, mgcSetupRequest, evidenceRequest;
        private long generation;
        private long evidenceGeneration;
        private string evidenceRequestKey;
        private DateTime evidenceRequestDay;
        private DateTime evidenceRequestSessionEnd;
        private Instrument evidenceRequestInstrument;
        private TradingHours evidenceRequestHours;
        private List<KeystoneArcBar> evidenceRequestFirstPart = new List<KeystoneArcBar>();
        private int pendingRequests;
        private int historicalRequestTotal, historicalRequestCompleted;
        private readonly Queue<HistoricalRequestWorkItem> historicalRequestQueue = new Queue<HistoricalRequestWorkItem>();
        private readonly HashSet<string> failedContractRolloverSeries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool historicalRequestActive;
        private string historicalDataReceipt = "DATA RECEIPT: no completed NinjaTrader historical request recorded for this lab run.";
        private bool configurationApproved;
        private string configurationApprovalKey = string.Empty;
        // A completed or active research configuration may not be submitted again.  This avoids
        // an accidental second historical load while the current run is still the active study.
        private bool researchSubmissionLocked;
        private KeystoneArcRunConfig config = new KeystoneArcRunConfig();
        // --- Visual theme ------------------------------------------------------------
        // A disciplined terminal palette: one true dark base with two elevation steps,
        // a single confident accent (Blue) for interactive/primary elements, a distinct
        // secondary accent (Cyan) used sparingly for section headers, and exactly three
        // semantic colors (Green/Red/Gold) reused everywhere something is good, bad, or
        // pending - rather than many competing bright hues at the same weight.
        private static readonly SolidColorBrush Bg = ColorBrush(9, 13, 20);
        private static readonly SolidColorBrush Panel = ColorBrush(16, 23, 34);
        private static readonly SolidColorBrush Card = ColorBrush(24, 34, 49);
        private static readonly SolidColorBrush Text = ColorBrush(236, 241, 249);
        private static readonly SolidColorBrush Muted = ColorBrush(124, 141, 163);
        private static readonly SolidColorBrush Blue = ColorBrush(74, 163, 255);
        private static readonly SolidColorBrush Orchid = ColorBrush(163, 137, 247);
        private static readonly SolidColorBrush Cyan = ColorBrush(45, 201, 190);
        private static readonly SolidColorBrush Green = ColorBrush(50, 197, 122);
        private static readonly SolidColorBrush Red = ColorBrush(240, 84, 97);
        private static readonly SolidColorBrush Gold = ColorBrush(235, 176, 43);
        private static readonly SolidColorBrush Orange = ColorBrush(240, 133, 58);
        // Evidence Chart uses a quieter slate palette. Candle bodies stay muted so wicks, exact entry lines,
        // and the small result markers remain readable without the chart looking like a wall of neon labels.
        private static readonly SolidColorBrush EvidenceBg = ColorBrush(11, 16, 25);
        private static readonly SolidColorBrush EvidenceGrid = ColorBrush(63, 78, 99);
        private static readonly SolidColorBrush CandleUp = ColorBrush(43, 150, 116);
        private static readonly SolidColorBrush CandleDown = ColorBrush(181, 76, 94);
        private static readonly SolidColorBrush CandleWick = ColorBrush(184, 200, 219);
        private static readonly SolidColorBrush EntryInk = ColorBrush(17, 55, 92);
        private static readonly SolidColorBrush EntryHalo = ColorBrush(199, 231, 255);
        private static readonly SolidColorBrush WinPurple = ColorBrush(56, 214, 130);
        private static readonly SolidColorBrush LossAmber = ColorBrush(255, 82, 96);
        private static readonly SolidColorBrush ExitIce = ColorBrush(141, 210, 250);

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults) Name = "KeystoneArc5MResearchLab";
        }

        protected override void OnWindowCreated(Window createdWindow)
        {
            CaptureOpenChartInstrument(createdWindow);
            ControlCenter center = createdWindow as ControlCenter;
            if (center == null || center.Dispatcher == null) return;
            if (launcherTimer != null) launcherTimer.Stop();
            int attempts = 0;
            launcherTimer = new DispatcherTimer(DispatcherPriority.Background, center.Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
            launcherTimer.Tick += delegate { attempts++; if (RegisterMenu(center) || attempts >= 15) { launcherTimer.Stop(); launcherTimer = null; } };
            launcherTimer.Start();
            RegisterMenu(center);
        }

        protected override void OnWindowDestroyed(Window destroyedWindow)
        {
            RemoveOpenChartInstrument(destroyedWindow);
            if (!(destroyedWindow is ControlCenter)) return;
            CancelRequests();
            CancelEvidenceRequest();
            if (launcherTimer != null) { launcherTimer.Stop(); launcherTimer = null; }
            try { if (newMenu != null && launchItem != null) newMenu.Items.Remove(launchItem); } catch { }
            newMenu = null; launchItem = null;
        }

        private void CaptureOpenChartInstrument(Window createdWindow)
        {
            try
            {
                // Do not depend on one private ChartWindow type name.  A few NinjaTrader builds
                // host a chart tab in a shell window whose type is not recognizably "Chart".
                // The scanner only records actual BarsArray instruments, so it is safe to use
                // on every new NinjaTrader window and makes MGC chart capture symmetrical with MNQ.
                foreach (object control in ChartControlsInWindow(createdWindow))
                {
                    Instrument direct = DirectChartInstrument(control);
                    string directRoot = InstrumentRoot(direct);
                    if ((directRoot == "MNQ" || directRoot == "MGC") && direct != null) openChartInstruments[directRoot] = direct;
                    IEnumerable rows = ReflectionValue(control, "BarsArray") as IEnumerable;
                    if (rows == null) continue;
                    foreach (object row in rows)
                    {
                        Instrument instrument = ReflectionValue(ReflectionValue(row, "Bars") ?? row, "Instrument") as Instrument;
                        string root = InstrumentRoot(instrument);
                        if ((root == "MNQ" || root == "MGC") && instrument != null) openChartInstruments[root] = instrument;
                    }
                }
            }
            catch { }
        }

        private void RemoveOpenChartInstrument(Window destroyedWindow)
        {
            try
            {
                if (!IsNinjaChartWindow(destroyedWindow)) return;
                Instrument instrument = ChartWindowInstrument(destroyedWindow);
                string root = InstrumentRoot(instrument);
                if (!string.IsNullOrWhiteSpace(root)) openChartInstruments.Remove(root);
            }
            catch { }
        }

        private void RefreshOpenChartInstruments()
        {
            try
            {
                if (Application.Current == null || Application.Current.Windows == null) return;
                foreach (Window candidate in Application.Current.Windows)
                {
                    foreach (object control in ChartControlsInWindow(candidate))
                    {
                        Instrument direct = DirectChartInstrument(control);
                        string directRoot = InstrumentRoot(direct);
                        if ((directRoot == "MNQ" || directRoot == "MGC") && direct != null) openChartInstruments[directRoot] = direct;
                        IEnumerable rows = ReflectionValue(control, "BarsArray") as IEnumerable;
                        if (rows == null) continue;
                        foreach (object row in rows)
                        {
                            object bars = ReflectionValue(row, "Bars") ?? row;
                            Instrument instrument = ReflectionValue(bars, "Instrument") as Instrument;
                            string root = InstrumentRoot(instrument);
                            if ((root == "MNQ" || root == "MGC") && instrument != null) openChartInstruments[root] = instrument;
                        }
                    }
                }
            }
            catch { }
        }

        private static Instrument DirectChartInstrument(object control)
        {
            try
            {
                Instrument direct = ReflectionValue(control, "Instrument") as Instrument;
                if (direct != null) return direct;
                return ReflectionValue(ReflectionValue(control, "Bars"), "Instrument") as Instrument;
            }
            catch { return null; }
        }

        private static Instrument ChartWindowInstrument(Window candidate)
        {
            try
            {
                if (!IsNinjaChartWindow(candidate)) return null;
                object control = candidate.GetType().GetProperty("ActiveChartControl") == null ? null : candidate.GetType().GetProperty("ActiveChartControl").GetValue(candidate, null);
                object chartBars = control == null || control.GetType().GetProperty("BarsArray") == null ? null : control.GetType().GetProperty("BarsArray").GetValue(control, null);
                IEnumerable all = chartBars as IEnumerable;
                if (all == null) return null;
                foreach (object row in all)
                {
                    object bars = ReflectionValue(row, "Bars") ?? row;
                    Instrument instrument = bars == null || bars.GetType().GetProperty("Instrument") == null ? null : bars.GetType().GetProperty("Instrument").GetValue(bars, null) as Instrument;
                    if (instrument != null) return instrument;
                }
            }
            catch { }
            return null;
        }

        // NinjaTrader has used more than one concrete chart window type across builds.
        // Accept actual NinjaTrader chart namespace variants rather than one exact class name.
        private static bool IsNinjaChartWindow(Window candidate)
        {
            if (candidate == null) return false;
            string typeName = candidate.GetType().FullName ?? string.Empty;
            if (typeName.IndexOf("NinjaTrader.Gui.Chart", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // A chart tab can be hosted by a NinjaTrader shell window whose concrete type does
            // not contain the Chart namespace.  Treat a window as a chart host only when it
            // exposes one of NinjaTrader's chart-control properties; ordinary AddOn windows do
            // not meet either condition.
            return candidate.GetType().GetProperty("ActiveChartControl") != null || candidate.GetType().GetProperty("ChartControl") != null;
        }

        // Chart windows can host several chart tabs. ActiveChartControl exposes only the selected tab,
        // so inspect the reflected, visual, and logical trees for tab-owned ChartControl instances.
        // In particular, NinjaTrader's tab host can keep a loaded historical ChartControl in its
        // visual tree without exposing it through Content, Items, or Children.  Missing that node
        // sends a known chart history through a dated-contract BarsRequest instead, which can return
        // zero bars for older years even though the exact chart already has the requested data.
        private static IEnumerable<object> ChartControlsInWindow(Window candidate)
        {
            var output = new List<object>();
            var seen = new HashSet<object>();
            var queue = new Queue<KeyValuePair<object, int>>();
            if (candidate != null) queue.Enqueue(new KeyValuePair<object, int>(candidate, 0));
            while (queue.Count > 0)
            {
                KeyValuePair<object, int> item = queue.Dequeue();
                object node = item.Key;
                if (node == null || !seen.Add(node)) continue;
                object active = ReflectionValue(node, "ActiveChartControl");
                object chart = ReflectionValue(node, "ChartControl");
                // A just-opened chart tab can expose Instrument before BarsArray is populated.
                // Keep that node in the traversal output so contract discovery does not fail for
                // MGC merely because its historical series has not been activated yet.
                if ((ReflectionValue(node, "BarsArray") is IEnumerable || ReflectionValue(node, "Instrument") is Instrument || ReflectionValue(ReflectionValue(node, "Bars"), "Instrument") is Instrument) && !output.Contains(node)) output.Add(node);
                if (active != null && (ReflectionValue(active, "BarsArray") is IEnumerable || ReflectionValue(active, "Instrument") is Instrument || ReflectionValue(ReflectionValue(active, "Bars"), "Instrument") is Instrument) && !output.Contains(active)) output.Add(active);
                if (chart != null && (ReflectionValue(chart, "BarsArray") is IEnumerable || ReflectionValue(chart, "Instrument") is Instrument || ReflectionValue(ReflectionValue(chart, "Bars"), "Instrument") is Instrument) && !output.Contains(chart)) output.Add(chart);
                if (item.Value >= 48) continue;
                object content = ReflectionValue(node, "Content");
                if (content != null && !(content is string)) queue.Enqueue(new KeyValuePair<object, int>(content, item.Value + 1));
                object child = ReflectionValue(node, "Child");
                if (child != null && !(child is string)) queue.Enqueue(new KeyValuePair<object, int>(child, item.Value + 1));
                IEnumerable items = ReflectionValue(node, "Items") as IEnumerable;
                if (items != null)
                    foreach (object itemChild in items)
                        if (itemChild != null) queue.Enqueue(new KeyValuePair<object, int>(itemChild, item.Value + 1));
                IEnumerable children = ReflectionValue(node, "Children") as IEnumerable;
                if (children != null)
                    foreach (object grandchild in children)
                        if (grandchild != null) queue.Enqueue(new KeyValuePair<object, int>(grandchild, item.Value + 1));
                DependencyObject dependency = node as DependencyObject;
                if (dependency == null) continue;
                int visualCount = 0;
                try { visualCount = VisualTreeHelper.GetChildrenCount(dependency); } catch { visualCount = 0; }
                for (int visualIndex = 0; visualIndex < visualCount; visualIndex++)
                {
                    DependencyObject visualChild = null;
                    try { visualChild = VisualTreeHelper.GetChild(dependency, visualIndex); } catch { }
                    if (visualChild != null) queue.Enqueue(new KeyValuePair<object, int>(visualChild, item.Value + 1));
                }
                // Some chart tabs are represented in the logical tree only.  This is a separate
                // source from the reflected Content/Items path above and is intentionally bounded
                // by the shared seen set.
                try
                {
                    foreach (object logicalChild in LogicalTreeHelper.GetChildren(dependency))
                        if (logicalChild != null && !(logicalChild is string)) queue.Enqueue(new KeyValuePair<object, int>(logicalChild, item.Value + 1));
                }
                catch { }
            }
            return output;
        }

        // Copies the exact bars already loaded in an open NinjaTrader chart when its root and timeframe match Step 1.
        // Reflection keeps this AddOn compatible across NinjaTrader chart-window API versions.
        private bool TryCopyOpenChartBars(string root, int minutes, DateTime start, DateTime end, out List<KeystoneArcBar> copied, out string detail)
        {
            copied = new List<KeystoneArcBar>(); detail = string.Empty;
            try
            {
                if (Application.Current == null || Application.Current.Windows == null) return false;
                // Asian cycles have no red/reference setup context: their first required bar is
                // the exact selected entry minute. Requiring a prior hour unnecessarily rejects
                // an otherwise complete open MGC 1-minute chart and forces a zero-bar request.
                DateTime includeFrom = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase)
                    ? start
                    : start.AddMinutes(-Math.Max(60, minutes * 3));
                foreach (Window candidate in Application.Current.Windows)
                {
                    // Do not require a ChartWindow type/property here.  In several NinjaTrader
                    // layouts the historical chart tab lives inside a generic shell window, so
                    // that gate reports “no matching chart” even while the exact bars are loaded.
                    // ChartControlsInWindow accepts only actual Bars-bearing nodes and therefore
                    // remains safe when scanning ordinary application windows as well.
                    foreach (object control in ChartControlsInWindow(candidate))
                    {
                        IEnumerable rows = ReflectionValue(control, "BarsArray") as IEnumerable;
                        var candidates = new List<object>();
                        // A Bars object may itself be encountered through the chart's visual tree.
                        // It is a valid direct source, not merely a container that owns a Bars field.
                        if (ReflectionValue(control, "Instrument") is Instrument && ReflectionValue(control, "BarsPeriod") != null) candidates.Add(control);
                        object directBars = ReflectionValue(control, "Bars");
                        if (directBars != null) candidates.Add(directBars);
                        if (rows != null) foreach (object row in rows) if (row != null) candidates.Add(row);
                        foreach (object row in candidates.Distinct())
                        {
                            object bars = ReflectionValue(row, "Bars") ?? row;
                            Instrument instrument = ReflectionValue(bars, "Instrument") as Instrument;
                            if (instrument == null || !string.Equals(InstrumentRoot(instrument), root, StringComparison.OrdinalIgnoreCase)) continue;
                            object period = ReflectionValue(bars, "BarsPeriod") ?? ReflectionValue(row, "BarsPeriod");
                            string type = Convert.ToString(ReflectionValue(period, "BarsPeriodType"));
                            int value = ReflectionInteger(ReflectionValue(period, "Value"), 0);
                            if (!string.Equals(type, "Minute", StringComparison.OrdinalIgnoreCase) || value != minutes) continue;
                            int count = ReflectionInteger(ReflectionValue(bars, "Count"), 0);
                            if (count <= 0) continue;
                            var local = new List<KeystoneArcBar>();
                            for (int index = 0; index < count; index++)
                            {
                                DateTime time = ReflectionDateTime(bars, "GetTime", index);
                                if (time == DateTime.MinValue || time < includeFrom || time > end) continue;
                                local.Add(new KeystoneArcBar
                                {
                                    Time = time, Symbol = root,
                                    Open = ReflectionDouble(bars, "GetOpen", index), High = ReflectionDouble(bars, "GetHigh", index),
                                    Low = ReflectionDouble(bars, "GetLow", index), Close = ReflectionDouble(bars, "GetClose", index),
                                    Volume = (long)ReflectionDouble(bars, "GetVolume", index)
                                });
                            }
                            local = local.OrderBy(x => x.Time).ToList();
                            if (local.Count == 0) continue;
                            // Full Globex begins at 18:00 ET, after the 17:00–18:00 CME
                            // maintenance break. BH asks for predecessor context, so includeFrom
                            // is normally one hour before the selected window. Do not reject a
                            // complete open chart merely because that predecessor window falls
                            // wholly inside the documented maintenance break. The selected range
                            // itself must still be covered exactly; this only avoids converting a
                            // visible 18:00→close chart into a zero-bar BarsRequest.
                            bool chartStartsAtConfiguredOpen = config != null
                                && string.Equals(config.SessionMode, "FULL_GLOBEX", StringComparison.OrdinalIgnoreCase)
                                && local.First().Time >= start
                                && local.First().Time <= start.AddMinutes(minutes);
                            bool leftCovered = local.First().Time <= includeFrom.AddMinutes(minutes) || chartStartsAtConfiguredOpen;
                            if (!leftCovered || local.Last().Time < end.AddMinutes(-minutes))
                            {
                                detail = "MATCHING OPEN CHART DOES NOT COVER THE REQUIRED PRE-RANGE CONTEXT + SELECTED RANGE • chart " + local.First().Time.ToString("yyyy-MM-dd HH:mm") + " → " + local.Last().Time.ToString("yyyy-MM-dd HH:mm");
                                continue;
                            }
                            copied = local;
                            detail = "OPEN CHART EXACT BARS • " + instrument.FullName + " • " + minutes + "M • " + local.Count + " bars • " + local.First().Time.ToString("yyyy-MM-dd HH:mm") + " → " + local.Last().Time.ToString("yyyy-MM-dd HH:mm") + (chartStartsAtConfiguredOpen ? " • STARTS AT CONFIGURED 18:00 GLOBEX OPEN; 17:00–18:00 PRE-CONTEXT IS A MAINTENANCE GAP" : string.Empty);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) { detail = "OPEN CHART READ ERROR • " + ex.Message; }
            return false;
        }

        // When the user has years of exact one-minute bars in an open NinjaTrader chart but has
        // not separately loaded the selected 5/15/30/60/240M chart, preserve that chart history
        // rather than forcing a provider BarsRequest.  Each emitted bar requires every one-minute
        // component and is later passed through the same strict OHLC reconciliation gate.
        private static List<KeystoneArcBar> AggregateExactOneMinuteSetupBars(List<KeystoneArcBar> oneMinute, int setupMinutes)
        {
            var result = new List<KeystoneArcBar>();
            if (oneMinute == null || setupMinutes <= 1) return result;
            List<KeystoneArcBar> ordered = oneMinute.Where(x => x != null && x.Time != DateTime.MinValue).OrderBy(x => x.Time).ToList();
            for (int index = 0; index + setupMinutes <= ordered.Count; index++)
            {
                KeystoneArcBar first = ordered[index];
                // Standard futures bars are opened on the overnight Globex session grid
                // (18:00, 18:05, ... for 5M; 18:00, 22:00, ... for 240M). Do not create
                // overlapping synthetic bars.
                int minuteOfDay = first.Time.Hour * 60 + first.Time.Minute;
                if (((minuteOfDay - (18 * 60) + (24 * 60)) % setupMinutes) != 0) continue;
                bool consecutive = true;
                for (int component = 1; component < setupMinutes; component++)
                    if (ordered[index + component].Time != first.Time.AddMinutes(component)) { consecutive = false; break; }
                if (!consecutive) continue;
                List<KeystoneArcBar> slice = ordered.Skip(index).Take(setupMinutes).ToList();
                result.Add(new KeystoneArcBar
                {
                    Time = first.Time,
                    Symbol = first.Symbol,
                    Open = first.Open,
                    High = slice.Max(x => x.High),
                    Low = slice.Min(x => x.Low),
                    Close = slice.Last().Close,
                    Volume = slice.Sum(x => x.Volume)
                });
            }
            return result;
        }

        private static object ReflectionValue(object target, string property)
        {
            try { return target == null ? null : (target.GetType().GetProperty(property) == null ? null : target.GetType().GetProperty(property).GetValue(target, null)); }
            catch { return null; }
        }
        private static int ReflectionInteger(object value, int fallback)
        {
            try { return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }
        private static double ReflectionDouble(object target, string method, int index)
        {
            try { object value = target == null || target.GetType().GetMethod(method, new[] { typeof(int) }) == null ? null : target.GetType().GetMethod(method, new[] { typeof(int) }).Invoke(target, new object[] { index }); return value == null ? 0.0 : Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }
        private static DateTime ReflectionDateTime(object target, string method, int index)
        {
            try { object value = target == null || target.GetType().GetMethod(method, new[] { typeof(int) }) == null ? null : target.GetType().GetMethod(method, new[] { typeof(int) }).Invoke(target, new object[] { index }); return value is DateTime ? (DateTime)value : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        private static string InstrumentRoot(Instrument instrument)
        {
            string full = instrument == null ? string.Empty : instrument.FullName;
            return (full ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

        private Instrument ResolveDynamicInstrument(string root, DateTime requestEnd, out string source)
        {
            RefreshOpenChartInstruments();
            // MGC's direct 1M history must not inherit the currently visible gold contract.
            // A chart such as MGC 12-26 can visually carry merged bars for an older range while
            // a direct 1M BarsRequest to that same far-dated contract returns zero bars. Resolve
            // the nearest valid metals delivery from the requested range first; MNQ retains its
            // existing verified chart-first branch below.
            if (string.Equals(root, "MGC", StringComparison.OrdinalIgnoreCase))
            {
                Instrument rangeContract = ResolveMgcRangeStartInstrument(requestEnd, out source);
                if (rangeContract != null) return rangeContract;
            }
            Instrument chartInstrument;
            chartInstrument = FindOpenChartInstrument(root, requestEnd);
            // Use the actual dated contract on an open chart first.  The previous continuous
            // first policy produced 0-bar BarsRequests on this user's NinjaTrader connection,
            // even for a one-day current test.  A compatible visible contract is the same data
            // source the user has already verified on the NinjaTrader chart.
            if (chartInstrument != null && IsChartInstrumentDateCompatible(chartInstrument, requestEnd))
            {
                openChartInstruments[root] = chartInstrument;
                source = "OPEN CHART CONTRACT • " + chartInstrument.FullName;
                return chartInstrument;
            }
            Instrument remembered;
            if (openChartInstruments.TryGetValue(root, out remembered) && remembered != null && IsChartInstrumentDateCompatible(remembered, requestEnd))
            {
                source = "OPEN CHART CONTRACT • " + remembered.FullName;
                return remembered;
            }
            // For a closed or historical chart, resolve the date-matched expiry contract from
            // NinjaTrader's own instrument database before attempting a continuous symbol.
            Instrument master = SafeGetInstrument(root);
            if (master != null && master.MasterInstrument != null)
            {
                try
                {
                    DateTime expiry = master.MasterInstrument.GetNextExpiry(requestEnd.Date);
                    Instrument resolved = expiry == DateTime.MinValue ? null : SafeGetInstrument(root + " " + expiry.ToString("MM-yy", CultureInfo.InvariantCulture));
                    if (resolved != null) { source = "AUTO EXPIRY • " + resolved.FullName; return resolved; }
                }
                catch { }
            }
            // Gold has a different active-contract sequence from equity indexes on some
            // NinjaTrader connections. Probe every standard month around the requested date.
            DateTime requestMonth = new DateTime(requestEnd.Year, requestEnd.Month, 1);
            for (int year = requestEnd.Year; year <= requestEnd.Year + 1; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    DateTime candidate = new DateTime(year, month, 1);
                    if (candidate < requestMonth || candidate > requestMonth.AddMonths(12)) continue;
                    Instrument resolved = SafeGetInstrument(root + " " + candidate.ToString("MM-yy", CultureInfo.InvariantCulture));
                    if (resolved != null) { source = "AUTO CONTRACT • " + resolved.FullName; return resolved; }
                }
            }
            // Last fallback only.  Some connections expose continuous futures for BarsRequest;
            // others resolve the object but return zero intraday bars.  It must never replace a
            // confirmed dated chart or a date-matched expiry contract.
            Instrument continuous = SafeGetInstrument(root + " ##-##");
            if (continuous != null)
            {
                source = "CONTINUOUS FALLBACK • " + continuous.FullName;
                return continuous;
            }
            // Final fallback only.  This lets a chart still supply a request when the connection
            // has no continuous symbol configured, while keeping the diagnostic explicit.
            if (chartInstrument != null)
            {
                source = "OPEN CHART FALLBACK • " + chartInstrument.FullName + " (date-mismatched; verify receipt)";
                return chartInstrument;
            }
            source = "UNRESOLVED • open a " + root + " chart and click REFRESH OPEN CHART INSTRUMENTS";
            return null;
        }

        private static Instrument ResolveMgcRangeStartInstrument(DateTime requestedDate, out string source)
        {
            source = string.Empty;
            DateTime month = new DateTime(requestedDate.Year, requestedDate.Month, 1);
            Instrument master = SafeGetInstrument("MGC");
            if (master != null && master.MasterInstrument != null)
            {
                try
                {
                    DateTime expiry = master.MasterInstrument.GetNextExpiry(requestedDate.Date);
                    // A front/next MGC delivery should be close to the selected range. Do not
                    // accept a current chart contract that is many months after a historical
                    // start merely because the provider reports it as the master default.
                    if (expiry != DateTime.MinValue && expiry.Date <= requestedDate.Date.AddMonths(4))
                    {
                        Instrument resolved = SafeGetInstrument("MGC " + expiry.ToString("MM-yy", CultureInfo.InvariantCulture));
                        if (resolved != null) { source = "AUTO MGC RANGE-START EXPIRY • " + resolved.FullName; return resolved; }
                    }
                }
                catch { }
            }
            // MGC futures use Feb/Apr/Jun/Aug/Oct/Dec delivery months. This internal fallback
            // needs no contract input and is only used when the master catalog cannot supply a
            // plausible nearby delivery for the selected historic range.
            int[] deliveryMonths = new[] { 2, 4, 6, 8, 10, 12 };
            for (int offset = 0; offset <= 4; offset++)
            {
                DateTime candidate = month.AddMonths(offset);
                if (!deliveryMonths.Contains(candidate.Month)) continue;
                Instrument resolved = SafeGetInstrument("MGC " + candidate.ToString("MM-yy", CultureInfo.InvariantCulture));
                if (resolved != null) { source = "AUTO MGC RANGE-START DELIVERY • " + resolved.FullName; return resolved; }
            }
            return null;
        }

        private static Instrument SafeGetInstrument(string name)
        {
            try { return string.IsNullOrWhiteSpace(name) ? null : Instrument.GetInstrument(name); }
            catch { return null; }
        }

        private static Instrument FindOpenChartInstrument(string root, DateTime requestedDate)
        {
            try
            {
                if (Application.Current == null || Application.Current.Windows == null) return null;
                var candidates = new List<Instrument>();
                foreach (Window candidate in Application.Current.Windows)
                {
                    foreach (object control in ChartControlsInWindow(candidate))
                    {
                        // Some NinjaTrader chart shells expose Instrument directly while their
                        // BarsArray is delayed until the tab is activated.  Checking the direct
                        // property first keeps an open MGC tab usable for the next request.
                        Instrument direct = ReflectionValue(control, "Instrument") as Instrument;
                        if (direct == null)
                        {
                            object directBars = ReflectionValue(control, "Bars");
                            direct = ReflectionValue(directBars, "Instrument") as Instrument;
                        }
                        if (direct != null && string.Equals(InstrumentRoot(direct), root, StringComparison.OrdinalIgnoreCase)) candidates.Add(direct);
                        IEnumerable rows = ReflectionValue(control, "BarsArray") as IEnumerable;
                        if (rows == null) continue;
                        foreach (object row in rows)
                        {
                            object bars = ReflectionValue(row, "Bars") ?? row;
                            Instrument instrument = ReflectionValue(bars, "Instrument") as Instrument;
                            if (instrument != null && string.Equals(InstrumentRoot(instrument), root, StringComparison.OrdinalIgnoreCase)) candidates.Add(instrument);
                        }
                    }
                }
                // Multiple dated MNQ/MGC chart tabs can be open.  Never let the first enumerated
                // (and possibly expired) chart hide a later chart that actually covers the test.
                Instrument compatible = candidates.FirstOrDefault(x => IsChartInstrumentDateCompatible(x, requestedDate));
                if (compatible != null) return compatible;
                return candidates.FirstOrDefault();
            }
            catch { }
            return null;
        }

        // A futures chart can use an instrument-specific trading-hours template.  Reuse that
        // template for BarsRequest when the matching instrument is already open, rather than
        // forcing both MNQ and MGC through the generic 24 x 7 template.
        private TradingHours ResolveRequestTradingHours(string root, int preferredMinutes, out string source)
        {
            source = "Default 24 x 7";
            TradingHours fallback = null;
            string fallbackSource = string.Empty;
            try
            {
                if (Application.Current != null && Application.Current.Windows != null)
                {
                    foreach (Window candidate in Application.Current.Windows)
                    {
                        foreach (object control in ChartControlsInWindow(candidate))
                        {
                            Instrument direct = DirectChartInstrument(control);
                            if (direct != null && string.Equals(InstrumentRoot(direct), root, StringComparison.OrdinalIgnoreCase))
                            {
                                TradingHours directHours = ReflectionValue(control, "TradingHours") as TradingHours;
                                if (directHours == null) directHours = ReflectionValue(ReflectionValue(control, "Bars"), "TradingHours") as TradingHours;
                                if (directHours != null && IsTradingHoursCompatibleWithRoot(root, directHours))
                                {
                                    int directMinutes = ChartControlMinutes(control);
                                    if (directMinutes == preferredMinutes)
                                    {
                                        source = "matching open " + root + " " + preferredMinutes + "M chart template";
                                        return directHours;
                                    }
                                    if (fallback == null) { fallback = directHours; fallbackSource = "open " + root + " chart template (timeframe not exposed)"; }
                                }
                            }
                            IEnumerable rows = ReflectionValue(control, "BarsArray") as IEnumerable;
                            if (rows == null) continue;
                            foreach (object row in rows)
                            {
                                object bars = ReflectionValue(row, "Bars") ?? row;
                                Instrument instrument = ReflectionValue(bars, "Instrument") as Instrument;
                                if (instrument == null || !string.Equals(InstrumentRoot(instrument), root, StringComparison.OrdinalIgnoreCase)) continue;
                                TradingHours chartHours = ReflectionValue(bars, "TradingHours") as TradingHours;
                                if (chartHours == null) chartHours = ReflectionValue(row, "TradingHours") as TradingHours;
                                if (chartHours != null && IsTradingHoursCompatibleWithRoot(root, chartHours))
                                {
                                    int chartMinutes = ChartBarsMinutes(bars, row);
                                    if (chartMinutes == preferredMinutes)
                                    {
                                        source = "matching open " + root + " " + preferredMinutes + "M chart template";
                                        return chartHours;
                                    }
                                    if (fallback == null) { fallback = chartHours; fallbackSource = "open " + root + " chart template (fallback)"; }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            if (fallback != null)
            {
                source = fallbackSource;
                return fallback;
            }
            // Restore the previously working fallback: a named template can be absent or can
            // resolve to an unrelated market template on different NinjaTrader installations.
            // In particular, an MGC request must never inherit an index-futures session merely
            // because no matching gold chart template was found.  A matching MGC chart template
            // above remains preferred; otherwise the documented generic request is retried by
            // the existing full-range fallback sequence.
            // MGC can have an entirely different session-template family from MNQ.  When a
            // compatible gold chart is not open, request the documented native metals template
            // before the generic fallback.  This avoids silently asking a COMEX contract through
            // an index-hours template and keeps the generic 24x7 retry available if the local
            // NinjaTrader installation does not provide the named template.
            if (string.Equals(root, "MGC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    TradingHours metals = TradingHours.Get("CME Globex Metals ETH");
                    if (metals != null)
                    {
                        source = "native MGC CME Globex Metals ETH template";
                        return metals;
                    }
                }
                catch { }
            }
            source = "Default 24 x 7 • no matching " + root + " chart/native template";
            return TradingHours.Get("Default 24 x 7");
        }

        private static bool IsTradingHoursCompatibleWithRoot(string root, TradingHours tradingHours)
        {
            if (tradingHours == null) return false;
            string name = Convert.ToString(ReflectionValue(tradingHours, "Name") ?? ReflectionValue(tradingHours, "DisplayName") ?? string.Empty);
            if (string.IsNullOrWhiteSpace(name)) return true;
            if (string.Equals(root, "MGC", StringComparison.OrdinalIgnoreCase) && name.IndexOf("Index", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (string.Equals(root, "MNQ", StringComparison.OrdinalIgnoreCase) && name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        private static int ChartControlMinutes(object control)
        {
            return ChartBarsMinutes(ReflectionValue(control, "Bars"), control);
        }

        private static int ChartBarsMinutes(object bars, object fallback)
        {
            try
            {
                object period = ReflectionValue(bars, "BarsPeriod") ?? ReflectionValue(fallback, "BarsPeriod");
                string type = Convert.ToString(ReflectionValue(period, "BarsPeriodType"));
                int value = ReflectionInteger(ReflectionValue(period, "Value"), 0);
                return string.Equals(type, "Minute", StringComparison.OrdinalIgnoreCase) ? value : 0;
            }
            catch { return 0; }
        }

        private static bool IsChartInstrumentDateCompatible(Instrument instrument, DateTime requestedDate)
        {
            try
            {
                string fullName = instrument == null ? string.Empty : (instrument.FullName ?? string.Empty).Trim();
                string[] tokens = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                DateTime parsedExpiry;
                if (tokens.Length >= 2 && DateTime.TryParseExact(tokens[tokens.Length - 1], "MM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedExpiry))
                {
                    // A visible chart can remain open after its dated futures contract has
                    // expired.  Do not let (for example) MGC 08-25 satisfy a Sep-25 BH request
                    // simply because it is only one month old; it can return zero provider bars
                    // while the user's merged historical chart has years of valid data.
                    DateTime requestMonth = new DateTime(requestedDate.Year, requestedDate.Month, 1);
                    return parsedExpiry >= requestMonth && parsedExpiry <= requestMonth.AddMonths(12);
                }
                System.Reflection.PropertyInfo expiryProperty = instrument.GetType().GetProperty("Expiry");
                object rawExpiry = expiryProperty == null ? null : expiryProperty.GetValue(instrument, null);
                if (!(rawExpiry is DateTime)) return true;
                DateTime expiry = (DateTime)rawExpiry;
                if (expiry == DateTime.MinValue) return true;
                return expiry >= requestedDate.AddMonths(-3) && expiry <= requestedDate.AddMonths(9);
            }
            catch { return true; }
        }

        private string BuildInstrumentSourceText()
        {
            string scope = scopeBox == null ? "MNQ" : Convert.ToString(scopeBox.SelectedItem ?? "MNQ");
            bool needMnq = scope == "MNQ" || scope == "BOTH";
            bool needMgc = scope == "MGC" || scope == "BOTH";
            Instrument mnq = null, mgc = null; string mnqSource = "NOT SELECTED", mgcSource = "NOT SELECTED";
            DateTime rangeStartDate = SelectedInputDateOrToday();
            DateTime mnqRequestedDate = rangeStartDate;
            // Display the same resolver anchor used by a BH date-range request.  This prevents
            // the status line from showing an old left-edge contract while the actual BH request
            // correctly uses the range-end dated contract.
            if (!IsAsian75Selected() && dateModeBox != null && string.Equals(Convert.ToString(dateModeBox.SelectedItem), "DATE RANGE", StringComparison.OrdinalIgnoreCase))
            {
                DateTime rangeEnd;
                if (endBox != null && DateTime.TryParseExact(endBox.Text == null ? string.Empty : endBox.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out rangeEnd)) mnqRequestedDate = rangeEnd.Date;
            }
            if (needMnq) mnq = ResolveDynamicInstrument("MNQ", mnqRequestedDate, out mnqSource);
            // MGC range history is intentionally anchored to the first requested date. Showing
            // the range end here previously made the label say MGC 12-21 for a Jan-2020 start,
            // which was confusing and did not describe the actual MGC request anchor.
            if (needMgc) mgc = ResolveDynamicInstrument("MGC", rangeStartDate, out mgcSource);
            var parts = new List<string>();
            if (needMnq) parts.Add("MNQ " + mnqRequestedDate.ToString("yyyy-MM-dd") + ": " + (mnq == null ? "not found" : mnqSource));
            if (needMgc) parts.Add("MGC " + rangeStartDate.ToString("yyyy-MM-dd") + ": " + (mgc == null ? "not found" : mgcSource));
            return "DATA SOURCE ANCHORS • " + string.Join(" | ", parts) + ". Matching open NinjaTrader chart tabs are preferred; their merge/rollover history is preserved. Click REFRESH OPEN CHART INSTRUMENTS after opening or selecting a chart tab.";
        }

        private DateTime SelectedInputDateOrToday()
        {
            DateTime value;
            return startBox != null && DateTime.TryParseExact(startBox.Text == null ? string.Empty : startBox.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? value.Date : DateTime.Today;
        }

        private void RefreshInstrumentSourceText()
        {
            if (instrumentSourceText != null) instrumentSourceText.Text = BuildInstrumentSourceText();
        }

        private bool RegisterMenu(ControlCenter center)
        {
            if (center == null || !center.IsLoaded) return false;
            newMenu = center.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;
            if (newMenu == null) return false;
            launchItem = newMenu.Items.OfType<NTMenuItem>().FirstOrDefault(x => string.Equals(Convert.ToString(x.Header), MenuCaption, StringComparison.Ordinal));
            if (launchItem != null) return true;
            launchItem = new NTMenuItem { Header = MenuCaption, Style = Application.Current == null ? null : Application.Current.TryFindResource("MainMenuItem") as Style };
            launchItem.Click += delegate { OpenWindow(); };
            newMenu.Items.Add(launchItem);
            return true;
        }

        private void OpenWindow()
        {
            if (window != null) { if (window.IsVisible) window.Activate(); return; }
            window = BuildWindow();
            window.Closing += delegate(object sender, CancelEventArgs args)
            {
                if (closeConfirmed) return;
                args.Cancel = true;
                if (operationBusy || isProcessing) { UpdateUi("FINISH OR CANCEL THE ACTIVE OPERATION BEFORE CLOSING", Gold); return; }
                if (unsavedResearch) ShowCloseConfirmation();
                else ShowSavedCloseConfirmation();
            };
            window.Closed += delegate { CancelRequests(); CancelEvidenceRequest(); try { if (evidenceWindow != null) evidenceWindow.Close(); } catch { } if (activityTimer != null) activityTimer.Stop(); window = null; closeConfirmed = false; };
            window.Show();
            UpdateUi("READY • VIRTUAL/HISTORICAL RESEARCH ONLY • NO ACCOUNT OR ORDER ACCESS", Blue);
            UpdateWorkflowState();
        }

        private Window BuildWindow()
        {
            saveButtons.Clear();
            exportButtons.Clear();
            var w = new Window { Title = MenuCaption, Width = 1160, Height = 790, MinWidth = 900, MinHeight = 620, Background = Bg, Foreground = Text, ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true };
            var host = new Grid();
            var root = new Grid { Margin = new Thickness(7) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(Header());
            workspaceTabs = new TabControl { Background = Bg, BorderBrush = Blue, BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 2), TabStripPlacement = Dock.Top };
            configureTab = new TabItem { Header = "1. CONFIGURE + DETECT", Content = SetupTab(), Background = Panel, Foreground = Text };
            reviewTab = new TabItem { Header = "2. VERIFY ENTRIES", Content = ReviewTab(), Background = Panel, Foreground = Text, IsEnabled = false };
            resultsTab = new TabItem { Header = "3. SIMULATION RESULTS", Content = ResultsTab(), Background = Panel, Foreground = Text, IsEnabled = false, VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            savedRunsTab = new TabItem { Header = "4. RUN ARCHIVE", Content = DataTab(), Background = Panel, Foreground = Text, IsEnabled = false };
            workspaceTabs.Items.Add(configureTab);
            workspaceTabs.Items.Add(reviewTab);
            workspaceTabs.Items.Add(resultsTab);
            workspaceTabs.Items.Add(savedRunsTab);
            Grid.SetRow(workspaceTabs, 1); root.Children.Add(workspaceTabs);
            var footer = Stack(); footer.Margin = new Thickness(0);
            workflowText = new TextBlock { Text = "NEXT: complete Step 1 configuration", Foreground = Gold, FontWeight = FontWeights.Bold, FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 0) };
            statusText = new TextBlock { Text = "READY", Foreground = Blue, FontWeight = FontWeights.Bold, FontSize = 9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 0) };
            footer.Children.Add(workflowText); footer.Children.Add(statusText);
            Grid.SetRow(footer, 2); root.Children.Add(footer);
            host.Children.Add(root);
            activityText = new TextBlock { Text = "WORKING", Foreground = Text, FontSize = 16, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20) };
            var activityCard = new Border { Background = Panel, BorderBrush = Cyan, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(24), Child = activityText, Width = 520, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            activityOverlay = new Border { Background = Bg, Child = activityCard, Visibility = Visibility.Collapsed };
            host.Children.Add(activityOverlay);
            confirmationTitleText = new TextBlock { Text = "CONFIRM", Foreground = Gold, FontSize = 17, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
            confirmationBodyText = new TextBlock { Text = string.Empty, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
            var confirmationButtons = new UniformGrid { Columns = 4, Margin = new Thickness(6) };
            var stayButton = Btn("KEEP", Blue); stayButton.ToolTip = "Keep working in this run"; stayButton.Click += delegate { HideConfirmation(); };
            confirmationSaveCloseButton = Btn("SAVE + EXIT", Orchid); confirmationSaveCloseButton.ToolTip = "Save snapshot, then close"; confirmationSaveCloseButton.Visibility = Visibility.Collapsed; confirmationSaveCloseButton.Click += delegate { SaveSnapshot(); closeConfirmed = true; window.Close(); };
            confirmationExportCloseButton = Btn("EXPORT + EXIT", Gold); confirmationExportCloseButton.ToolTip = "Export CSV, then close"; confirmationExportCloseButton.Visibility = Visibility.Collapsed; confirmationExportCloseButton.Click += delegate { ExportCsv(); closeConfirmed = true; window.Close(); };
            confirmationContinueButton = Btn("DISCARD", Red); confirmationContinueButton.ToolTip = "Close without saving"; confirmationContinueButton.Click += delegate { Action action = confirmationAction; HideConfirmation(); if (action != null) action(); };
            confirmationButtons.Children.Add(stayButton); confirmationButtons.Children.Add(confirmationSaveCloseButton); confirmationButtons.Children.Add(confirmationExportCloseButton); confirmationButtons.Children.Add(confirmationContinueButton);
            var confirmationStack = Stack(); confirmationStack.Children.Add(confirmationTitleText); confirmationStack.Children.Add(confirmationBodyText); confirmationStack.Children.Add(confirmationButtons);
            var confirmationCard = new Border { Background = Panel, BorderBrush = Gold, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(18), Child = confirmationStack, Width = 720, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            confirmationOverlay = new Border { Background = Bg, Child = confirmationCard, Visibility = Visibility.Collapsed };
            host.Children.Add(confirmationOverlay);
            activityTimer = new DispatcherTimer(DispatcherPriority.Background, w.Dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
            activityTimer.Tick += delegate { if (!operationBusy || activityText == null) return; activityFrame = (activityFrame + 1) % 4; activityText.Text = operationMessage + new string('.', activityFrame + 1) + "\n\nPlease wait. Buttons and tabs are locked until this finishes."; };
            w.Content = host;
            // ResultsTab creates lifecycle controls while the lab window is built. Apply their
            // initial visibility now, rather than waiting for a later selection event.
            RefreshLifecycleInputState();
            return w;
        }

        private void BeginBusy(string message)
        {
            operationBusy = true;
            operationMessage = message ?? "WORKING";
            activityFrame = 0;
            if (activityText != null) activityText.Text = operationMessage + ".\n\nPlease wait. Buttons and tabs are locked until this finishes.";
            if (activityOverlay != null) activityOverlay.Visibility = Visibility.Visible;
            if (activityTimer != null) activityTimer.Start();
            UpdateWorkflowState();
        }

        private void EndBusy()
        {
            operationBusy = false;
            operationMessage = string.Empty;
            if (activityTimer != null) activityTimer.Stop();
            if (activityOverlay != null) activityOverlay.Visibility = Visibility.Collapsed;
            UpdateWorkflowState();
        }

        private void ShowConfirmation(string title, string body, string continueText, Brush continueBrush, Action action)
        {
            confirmationAction = action;
            if (confirmationTitleText != null) confirmationTitleText.Text = title ?? "CONFIRM";
            if (confirmationBodyText != null) confirmationBodyText.Text = body ?? string.Empty;
            if (confirmationContinueButton != null) { confirmationContinueButton.Content = continueText ?? "CONTINUE"; confirmationContinueButton.Background = continueBrush ?? Red; }
            if (confirmationSaveCloseButton != null) confirmationSaveCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationExportCloseButton != null) confirmationExportCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationOverlay != null) confirmationOverlay.Visibility = Visibility.Visible;
        }

        private void ShowCloseConfirmation()
        {
            confirmationAction = delegate { CancelRequests(); closeConfirmed = true; window.Close(); };
            if (confirmationTitleText != null) confirmationTitleText.Text = "UNSAVED KEYSTONE RUN";
            if (confirmationBodyText != null) confirmationBodyText.Text = "Choose SAVE + EXIT to create a full snapshot, EXPORT + EXIT to write CSV files, DISCARD RUN to close without saving this in-memory run, or KEEP WORKING.";
            if (confirmationContinueButton != null) { confirmationContinueButton.Content = "DISCARD RUN"; confirmationContinueButton.ToolTip = "Close without saving this in-memory research run"; confirmationContinueButton.Background = Red; }
            if (confirmationSaveCloseButton != null) confirmationSaveCloseButton.Visibility = Visibility.Visible;
            if (confirmationExportCloseButton != null) confirmationExportCloseButton.Visibility = Visibility.Visible;
            if (confirmationOverlay != null) confirmationOverlay.Visibility = Visibility.Visible;
        }

        private void ShowSavedCloseConfirmation()
        {
            confirmationAction = delegate { closeConfirmed = true; if (window != null) window.Close(); };
            if (confirmationTitleText != null) confirmationTitleText.Text = "CLOSE KEYSTONE ARC?";
            if (confirmationBodyText != null) confirmationBodyText.Text = "This workspace has no unsaved research changes. Close the Keystone Arc window, or keep working.";
            if (confirmationContinueButton != null) { confirmationContinueButton.Content = "CLOSE LAB"; confirmationContinueButton.ToolTip = "Close the Keystone Arc workspace"; confirmationContinueButton.Background = Gold; }
            if (confirmationSaveCloseButton != null) confirmationSaveCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationExportCloseButton != null) confirmationExportCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationOverlay != null) confirmationOverlay.Visibility = Visibility.Visible;
        }

        private void HideConfirmation()
        {
            confirmationAction = null;
            if (confirmationSaveCloseButton != null) confirmationSaveCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationExportCloseButton != null) confirmationExportCloseButton.Visibility = Visibility.Collapsed;
            if (confirmationOverlay != null) confirmationOverlay.Visibility = Visibility.Collapsed;
        }

        private UIElement Header()
        {
            var g = new Grid { VerticalAlignment = VerticalAlignment.Top }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var stack = new StackPanel { Margin = new Thickness(0) };
            var title = Txt("KEYSTONE ARC", Text, 20, FontWeights.Bold); title.Margin = new Thickness(0, 0, 0, 0); stack.Children.Add(title);
            var subtitle = Txt("5M RESEARCH LAB • INDEPENDENT HISTORICAL DETECTION • VIRTUAL POOL • NO LIVE ORDERS", Cyan, 9, FontWeights.Bold); subtitle.Margin = new Thickness(0, 0, 0, 0); stack.Children.Add(subtitle);
            g.Children.Add(stack);
            var newTest = Btn("NEW TEST", Gold); newTest.Width = 106; newTest.Height = 30; newTest.Click += delegate { ConfirmResetForNewTest(); }; resetNewTestButton = newTest; Grid.SetColumn(newTest, 1); g.Children.Add(newTest);
            var closeAux = Btn("CLOSE CHARTS", Blue); closeAux.Width = 118; closeAux.Height = 30; closeAux.FontSize = 10; closeAux.ToolTip = "Close the Evidence Chart and Range Comparison windows; the Keystone workspace stays open"; closeAux.Click += delegate { CloseAuxiliaryWindows(); }; Grid.SetColumn(closeAux, 2); g.Children.Add(closeAux);
            var close = Btn("CLOSE LAB", Red); close.Width = 96; close.MinWidth = 96; close.MaxWidth = 96; close.Height = 30; close.FontSize = 10; close.ToolTip = "Close the entire Keystone Arc workspace"; close.Click += delegate { window.Close(); }; Grid.SetColumn(close, 3); g.Children.Add(close);
            return g;
        }

        private void CloseAuxiliaryWindows()
        {
            if (evidenceWindow != null && evidenceWindow.IsVisible) evidenceWindow.Close();
            if (comparisonWindow != null && comparisonWindow.IsVisible) comparisonWindow.Close();
            UpdateUi("AUXILIARY WINDOWS CLOSED • THE KEYSTONE WORKSPACE REMAINS OPEN", Green);
        }

        private UIElement SetupTab()
        {
            personalOnlyControls.Clear(); liveConfigurationControls.Clear(); propConfigurationControls.Clear();
            var root = Stack(); root.Margin = new Thickness(8);
            var heading = Stack(); heading.Margin = new Thickness(0, 0, 0, 6);
            heading.Children.Add(Txt("RESEARCH CONFIGURATION", Cyan, 16, FontWeights.Bold));
            heading.Children.Add(Txt("Complete the steps in order. The next step stays locked until the previous step is valid. Keystone only requests historical data; it cannot place an order or access an account.", Muted, 11, FontWeights.Normal));
            root.Children.Add(PanelCard(heading));

            var setupCards = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 8) };

            RefreshOpenChartInstruments();
            string defaultDay = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var data = Stack(); data.Children.Add(Txt("1. DATA, SETUPS & SESSION", Gold, 13, FontWeights.Bold));
            strategyBox = Select("BH • BREAK-HIGH LONG", "ASIAN 75 REVERSAL • COPY TRADING"); strategyBox.SelectedIndex = 0;
            scopeBox = Select("MNQ", "MGC", "BOTH"); scopeBox.SelectedIndex = 0;
            accountPathBox = Select("PROP • VIRTUAL POOL"); accountPathBox.SelectedIndex = 0; accountPathBox.Visibility = Visibility.Collapsed;
            // Keystone is intentionally one setup lab in this revision: long BH only.
            // Directional, stronger-filter, and FVG switches are retained only as dormant
            // compatibility fields for old snapshots; they are not exposed or read by a new run.
            directionBox = Select("BB • BUY MNQ + BUY MGC"); directionBox.SelectedIndex = 0; directionBox.Visibility = Visibility.Collapsed;
            bhFilterBox = Select("ALL VALID BH • BASE RULE"); bhFilterBox.SelectedIndex = 0; bhFilterBox.Visibility = Visibility.Collapsed;
            // These are fixed Eastern-Time research labels, not a claim that any window is
            // better. CUSTOM remains available for an exact user-defined start/end range.
            sessionBox = Select("NY OPEN 09:30-15:55", "NY EARLY 08:00-15:55", "ASIA 19:00-03:00", "LONDON 03:00-11:30", "FULL GLOBEX 18:00-15:55", "INSTRUMENT DEFAULT", "CUSTOM RANGE", "ASIAN CYCLE • CONFIGURED TIME"); sessionBox.SelectedIndex = 0;
            dateModeBox = Select("ONE DAY", "DATE RANGE"); dateModeBox.SelectedIndex = 0;
            timeframeBox = Select("1 MINUTE SETUPS", "5 MINUTE SETUPS", "15 MINUTE SETUPS", "30 MINUTE SETUPS", "60 MINUTE SETUPS", "240 MINUTE SETUPS"); timeframeBox.SelectedIndex = 1;
            sessionBox.SelectionChanged += delegate { RefreshSessionInputs(); InvalidateConfigurationApproval(); };
            strategyBox.SelectionChanged += delegate { RefreshStrategyInputState(); InvalidateConfigurationApproval(); };
            scopeBox.SelectionChanged += delegate { RefreshAsianDerivedInputs(); InvalidateConfigurationApproval(); };
            directionBox.SelectionChanged += delegate { InvalidateConfigurationApproval(); };
            // Prop-only lab: the account path is fixed to the illustrative virtual pool.
            bhFilterBox.SelectionChanged += delegate { RefreshBhAggressionInputState(); InvalidateConfigurationApproval(); };
            dateModeBox.SelectionChanged += delegate { RefreshDateInputs(); RefreshLifecycleInputState(); InvalidateConfigurationApproval(); };
            timeframeBox.SelectionChanged += delegate { InvalidateConfigurationApproval(); };
            startBox = Input(defaultDay); endBox = Input(defaultDay);
            mnqBox = Input("AUTO"); mnqBox.IsReadOnly = true; mgcBox = Input("AUTO"); mgcBox.IsReadOnly = true;
            WatchConfigurationInput(startBox); WatchConfigurationInput(endBox);
            customStartBox = Input("930"); endTimeBox = Input("1555");
            WatchConfigurationInput(customStartBox); WatchConfigurationInput(endTimeBox);
            mnqStrongRedBox = Input("3"); mnqStrongDeclineBox = Input("0"); mgcStrongRedBox = Input("0"); mgcStrongDeclineBox = Input("10");
            bhStrongCombineBox = Select("ANY ENABLED THRESHOLD", "ALL ENABLED THRESHOLDS"); bhStrongCombineBox.SelectedIndex = 0;
            WatchConfigurationInput(mnqStrongRedBox); WatchConfigurationInput(mnqStrongDeclineBox); WatchConfigurationInput(mgcStrongRedBox); WatchConfigurationInput(mgcStrongDeclineBox);
            bhStrongCombineBox.SelectionChanged += delegate { InvalidateConfigurationApproval(); };
            startBox.LostFocus += delegate { RefreshDateInputs(); };
            // BH is the entire default strategy, not an optional subordinate setup. Keep the
            // compatibility object hidden so an old run cannot surface a confusing checkbox.
            bhSetupBox = new CheckBox { IsChecked = true, Visibility = Visibility.Collapsed };
            fvgSetupBox = new CheckBox { Content = "FVG • RETEST + BREAK HIGH (OPTIONAL)", IsChecked = false, Foreground = Orchid, Margin = new Thickness(6), Visibility = Visibility.Collapsed };
            bhSetupBox.Checked += delegate { InvalidateConfigurationApproval(); }; bhSetupBox.Unchecked += delegate { InvalidateConfigurationApproval(); };
            fvgSetupBox.Checked += delegate { InvalidateConfigurationApproval(); }; fvgSetupBox.Unchecked += delegate { InvalidateConfigurationApproval(); };
            instrumentSourceText = Txt(BuildInstrumentSourceText(), Cyan, 10, FontWeights.Bold);
            UIElement mnqStrongRedRow = Row("MNQ STRONG • MIN RED CANDLES (0=OFF)", mnqStrongRedBox);
            UIElement mnqStrongDeclineRow = Row("MNQ STRONG • MIN DECLINE POINTS (0=OFF)", mnqStrongDeclineBox);
            UIElement mgcStrongRedRow = Row("MGC STRONG • MIN RED CANDLES (0=OFF)", mgcStrongRedBox);
            UIElement mgcStrongDeclineRow = Row("MGC STRONG • MIN DECLINE $ (0=OFF)", mgcStrongDeclineBox);
            UIElement strongCombineRow = Row("STRONG FILTER COMBINE", bhStrongCombineBox);
            strongerBhControls.Clear(); strongerBhControls.Add(mnqStrongRedRow); strongerBhControls.Add(mnqStrongDeclineRow); strongerBhControls.Add(mgcStrongRedRow); strongerBhControls.Add(mgcStrongDeclineRow); strongerBhControls.Add(strongCombineRow);
            UIElement customStartRow = Row("CUSTOM START HHMM", customStartBox);
            UIElement customEndRow = Row("CUSTOM END HHMM", endTimeBox);
            customSessionControls.Clear(); customSessionControls.Add(customStartRow); customSessionControls.Add(customEndRow);
            UIElement timeframeRow = Row("SETUP BAR TIMEFRAME", timeframeBox);
            bhStrategyControls.Clear(); bhStrategyControls.Add(timeframeRow);
            data.Children.Add(Row("STRATEGY", strategyBox)); data.Children.Add(Row("INSTRUMENTS", scopeBox)); data.Children.Add(Row("DATE MODE", dateModeBox)); data.Children.Add(timeframeRow); data.Children.Add(Row("SESSION", sessionBox)); data.Children.Add(customStartRow); data.Children.Add(customEndRow); data.Children.Add(Row("RUN DATE YYYY-MM-DD", startBox)); data.Children.Add(Row("RANGE END DATE", endBox)); data.Children.Add(instrumentSourceText);
            strategyRuleText = Txt("BH RULE: after the chosen session begins, a red candle is followed by a bullish reference candle; the immediately next bar breaks that bullish high. Every valid long BH setup is detected and eligible by default. No contract month is required.", Gold, 10, FontWeights.Bold);
            data.Children.Add(strategyRuleText);

            var model = Stack(); model.Children.Add(Txt("2. OUTCOME + RISK MODEL", Blue, 13, FontWeights.Bold));
            quantityBox = Input("10"); targetBox = Input("1500"); stopBox = Input("500"); mnqStopOffsetBox = Input("5"); mgcStopOffsetBox = Input("1"); dailyGoalBox = Input("1500"); dailyLossBox = Input("500");
            asianStartTimeBox = Input("1800"); asianEndTimeBox = Input("1555");
            asianDirectionBox = Select("LONG", "SHORT"); asianDirectionBox.SelectedIndex = 0;
            asianMnqDirectionBox = Select("LONG", "SHORT"); asianMnqDirectionBox.SelectedIndex = 0;
            asianMgcDirectionBox = Select("LONG", "SHORT"); asianMgcDirectionBox.SelectedIndex = 0;
            asianRiskModeBox = Select("FIXED CASH PER LEG", "PRICE MOVE PER LEG"); asianRiskModeBox.SelectedIndex = 0;
            asianReversalLossBox = Input("75"); asianMnqPriceMoveBox = Input("37.5"); asianMgcPriceMoveBox = Input("7.5");
            asianCycleTargetBox = Input("350"); asianCombinedStopBox = Input("0"); asianDailyLossBox = Input("600"); asianDailyLossBox.IsReadOnly = true; asianInstrumentStopBox = Input("0"); asianMnqInstrumentStopBox = Input("0"); asianMgcInstrumentStopBox = Input("0"); asianBreakEvenBox = Input("0"); asianStartingQuantityBox = Input("1"); asianMaxReversalsBox = Input("4"); asianMnqMaxReversalsBox = Input("4"); asianMgcMaxReversalsBox = Input("4");
            // Retained fields are not rendered and are not read in the prop-only configuration path.
            personalStartingBalanceBox = Input("0"); personalLotBox = Input("1.00"); mnqCashValueBox = Input("1.00"); mgcCashValueBox = Input("1.00");
            mnqTargetMoveBox = Input("10"); mgcTargetMoveBox = Input("10"); mnqStandardStopMoveBox = Input("5"); mgcStandardStopMoveBox = Input("5"); personalMaxRiskBox = Input("500"); breakEvenMoveBox = Input("0");
            breakEvenBox = new CheckBox { IsChecked = false, Visibility = Visibility.Collapsed };
            stopModeBox = Select("PROP STANDARD • FIXED MICRO SIZE", "PROP BELOW 3-CANDLE LOW • RISK-SIZED"); stopModeBox.SelectedIndex = 0;
            mnqStartBox = Input("930"); mgcStartBox = Input("800");
            WatchConfigurationInput(quantityBox); WatchConfigurationInput(targetBox); WatchConfigurationInput(stopBox); WatchConfigurationInput(mnqStopOffsetBox); WatchConfigurationInput(mgcStopOffsetBox); WatchConfigurationInput(dailyGoalBox); WatchConfigurationInput(dailyLossBox); WatchConfigurationInput(asianStartTimeBox); WatchConfigurationInput(asianEndTimeBox); WatchConfigurationInput(asianReversalLossBox); WatchConfigurationInput(asianMnqPriceMoveBox); WatchConfigurationInput(asianMgcPriceMoveBox); WatchConfigurationInput(asianCycleTargetBox); WatchConfigurationInput(asianCombinedStopBox); WatchConfigurationInput(asianDailyLossBox); WatchConfigurationInput(asianInstrumentStopBox); WatchConfigurationInput(asianMnqInstrumentStopBox); WatchConfigurationInput(asianMgcInstrumentStopBox); WatchConfigurationInput(asianBreakEvenBox); WatchConfigurationInput(asianStartingQuantityBox); WatchConfigurationInput(asianMaxReversalsBox); WatchConfigurationInput(asianMnqMaxReversalsBox); WatchConfigurationInput(asianMgcMaxReversalsBox); WatchConfigurationInput(personalStartingBalanceBox); WatchConfigurationInput(personalLotBox); WatchConfigurationInput(mnqCashValueBox); WatchConfigurationInput(mgcCashValueBox); WatchConfigurationInput(mnqTargetMoveBox); WatchConfigurationInput(mgcTargetMoveBox); WatchConfigurationInput(mnqStandardStopMoveBox); WatchConfigurationInput(mgcStandardStopMoveBox); WatchConfigurationInput(personalMaxRiskBox); WatchConfigurationInput(breakEvenMoveBox);
            asianReversalLossBox.TextChanged += delegate { RefreshAsianDerivedInputs(); };
            asianMaxReversalsBox.TextChanged += delegate { RefreshAsianDerivedInputs(); };
            stopModeBox.SelectionChanged += delegate { RefreshStopModelInputState(); InvalidateConfigurationApproval(); };
            breakEvenBox.Checked += delegate { RefreshStopModelInputState(); InvalidateConfigurationApproval(); }; breakEvenBox.Unchecked += delegate { RefreshStopModelInputState(); InvalidateConfigurationApproval(); };
            asianRiskModeBox.SelectionChanged += delegate { RefreshAsianRiskInputState(); InvalidateConfigurationApproval(); };
            asianDirectionBox.SelectionChanged += delegate { if (asianMnqDirectionBox != null) asianMnqDirectionBox.SelectedIndex = asianDirectionBox.SelectedIndex; if (asianMgcDirectionBox != null) asianMgcDirectionBox.SelectedIndex = asianDirectionBox.SelectedIndex; InvalidateConfigurationApproval(); };
            asianMnqDirectionBox.SelectionChanged += delegate { InvalidateConfigurationApproval(); };
            asianMgcDirectionBox.SelectionChanged += delegate { InvalidateConfigurationApproval(); };
            UIElement liveBalanceRow = Row("LIVE STARTING BALANCE $", personalStartingBalanceBox);
            UIElement liveLotRow = Row("PERSONAL LOT SIZE (1.00 = ONE LOT)", personalLotBox);
            UIElement mnqCashRow = Row("NAS100 $ / POINT / 1.00 LOT", mnqCashValueBox);
            UIElement mgcCashRow = Row("GOLD $ / $1 MOVE / 1.00 LOT", mgcCashValueBox);
            UIElement mnqTargetMoveRow = Row("NAS100 TARGET PRICE MOVE", mnqTargetMoveBox);
            UIElement mgcTargetMoveRow = Row("GOLD TARGET PRICE MOVE", mgcTargetMoveBox);
            UIElement mnqStandardStopRow = Row("NAS100 STANDARD STOP PRICE MOVE", mnqStandardStopMoveBox);
            UIElement mgcStandardStopRow = Row("GOLD STANDARD STOP PRICE MOVE", mgcStandardStopMoveBox);
            UIElement mnqLowOffsetRow = Row("NAS100 LOW STOP OFFSET POINTS", mnqStopOffsetBox);
            UIElement mgcLowOffsetRow = Row("GOLD LOW STOP OFFSET $", mgcStopOffsetBox);
            UIElement liveMaxRiskRow = Row("LIVE MAX RISK $ (AUTO-LOT MODE)", personalMaxRiskBox);
            UIElement breakEvenMoveRow = Row("BREAKEVEN TRIGGER PRICE MOVE", breakEvenMoveBox);
            UIElement propQuantityRow = Row("PROP MICRO CONTRACTS", quantityBox);
            UIElement propTargetRow = Row("PROP TARGET PER TRADE $", targetBox);
            UIElement propStopRow = Row("PROP MAX RISK PER TRADE $", stopBox);
            UIElement dailyGoalRow = Row("PROP DAILY PROFIT LOCK / ACCOUNT $", dailyGoalBox);
            UIElement dailyLossRow = Row("PROP DAILY LOSS LOCK / ACCOUNT $", dailyLossBox);
            UIElement asianStartTimeRow = Row("ASIAN ENTRY TIME HHMM (ET)", asianStartTimeBox);
            UIElement asianEndTimeRow = Row("ASIAN BACKTEST END HHMM (ET)", asianEndTimeBox);
            UIElement asianDirectionRow = Row("ASIAN START DIRECTION (MNQ + MGC)", asianDirectionBox);
            UIElement asianMnqDirectionRow = Row("MNQ INITIAL DIRECTION", asianMnqDirectionBox);
            UIElement asianMgcDirectionRow = Row("MGC INITIAL DIRECTION", asianMgcDirectionBox);
            UIElement asianRiskModeRow = Row("ASIAN REVERSAL RISK TYPE", asianRiskModeBox);
            UIElement asianReversalRow = Row("ASIAN FIXED CASH LOSS / LEG $", asianReversalLossBox);
            UIElement asianMnqPriceMoveRow = Row("MNQ REVERSAL PRICE MOVE / LEG", asianMnqPriceMoveBox);
            UIElement asianMgcPriceMoveRow = Row("MGC REVERSAL PRICE MOVE / LEG", asianMgcPriceMoveBox);
            UIElement asianCycleTargetRow = Row("ASIAN COMBINED DAILY PROFIT TARGET $", asianCycleTargetBox);
            UIElement asianCombinedStopRow = Row("ADVANCED COMBINED CYCLE STOP $ (0=OFF)", asianCombinedStopBox);
            UIElement asianDailyLossRow = Row("AUTO MAX COMBINED REVERSAL LOSS $", asianDailyLossBox);
            UIElement asianInstrumentStopRow = Row("LEGACY MAX LOSS / INSTRUMENT $", asianInstrumentStopBox);
            UIElement asianMnqInstrumentStopRow = Row("MNQ MAX LOSS / DAY $ (0=OFF)", asianMnqInstrumentStopBox);
            UIElement asianMgcInstrumentStopRow = Row("MGC MAX LOSS / DAY $ (0=OFF)", asianMgcInstrumentStopBox);
            UIElement asianBreakEvenRow = Row("ASIAN BREAKEVEN TRIGGER $ (0=OFF)", asianBreakEvenBox);
            UIElement asianStartQuantityRow = Row("ASIAN INITIAL MICRO CONTRACTS", asianStartingQuantityBox);
            UIElement asianMaxReversalsRow = Row("MAX REVERSAL LEGS / INSTRUMENT", asianMaxReversalsBox);
            UIElement asianMnqMaxReversalsRow = Row("MNQ MAX REVERSALS AFTER x1", asianMnqMaxReversalsBox);
            UIElement asianMgcMaxReversalsRow = Row("MGC MAX REVERSALS AFTER x1", asianMgcMaxReversalsBox);
            UIElement liveModeNote = Txt("LIVE ACCOUNT: enter the broker cash value and lot size yourself; Keystone never assumes an IC Markets contract. Exactly one earliest resolved setup across the selected instrument scope enters the final historical ledger per session date. All later setups remain visible as evidence only.", Cyan, 10, FontWeights.Bold);
            UIElement propModeNote = Txt("PROP: all eligible setups are sent in chronological order to the first available virtual account. Daily account locks and the selected evaluation/funded lifecycle are applied in Step 3.", Gold, 10, FontWeights.Bold);
            personalOnlyControls.Add(liveBalanceRow); personalOnlyControls.Add(liveLotRow); personalOnlyControls.Add(mnqCashRow); personalOnlyControls.Add(mgcCashRow); personalOnlyControls.Add(mnqTargetMoveRow); personalOnlyControls.Add(mgcTargetMoveRow); personalOnlyControls.Add(mnqStandardStopRow); personalOnlyControls.Add(mgcStandardStopRow); personalOnlyControls.Add(liveMaxRiskRow); personalOnlyControls.Add(breakEvenBox); personalOnlyControls.Add(breakEvenMoveRow); liveConfigurationControls.Add(liveModeNote);
            propConfigurationControls.Add(propQuantityRow); propConfigurationControls.Add(propTargetRow); propConfigurationControls.Add(propStopRow); propConfigurationControls.Add(dailyGoalRow); propConfigurationControls.Add(dailyLossRow); propConfigurationControls.Add(propModeNote);
            lowStopControls.Clear(); lowStopControls.Add(mnqLowOffsetRow); lowStopControls.Add(mgcLowOffsetRow);
            autoRiskLotControls.Clear(); autoRiskLotControls.Add(liveMaxRiskRow);
            UIElement stopModeRow = Row("STOP MODE", stopModeBox);
            UIElement bhModelNote = Txt("PROP MODEL: fixed micro quantity uses the selected cash target and risk. BELOW 3-CANDLE LOW calculates the stop from the red/reference/trigger group and risk-sizes the micro quantity. This is historical research only; no broker, firm, or order access exists.", Gold, 10, FontWeights.Bold);
            var asianModel = Stack(); asianModel.Margin = new Thickness(0, 4, 0, 2);
            asianModel.Children.Add(Txt("ASIAN REVERSAL BACKTEST • COPY TRADING", Orchid, 11, FontWeights.Bold));
            asianModel.Children.Add(asianStartTimeRow); asianModel.Children.Add(asianEndTimeRow); asianModel.Children.Add(asianDirectionRow); asianModel.Children.Add(asianStartQuantityRow); asianModel.Children.Add(asianReversalRow); asianModel.Children.Add(asianMaxReversalsRow); asianModel.Children.Add(asianCycleTargetRow); asianModel.Children.Add(asianDailyLossRow); asianModel.Children.Add(asianBreakEvenRow);
            asianCashRiskControls.Clear(); asianCashRiskControls.Add(asianReversalRow);
            asianPriceRiskControls.Clear(); asianPriceRiskControls.Add(asianMnqPriceMoveRow); asianPriceRiskControls.Add(asianMgcPriceMoveRow);
            // Retained only for compatibility with old snapshots; the current Asian tester uses
            // one shared cash stop and one shared reversal count for both instruments.
            asianInstrumentStopRow.Visibility = Visibility.Collapsed; asianMnqInstrumentStopRow.Visibility = Visibility.Collapsed; asianMgcInstrumentStopRow.Visibility = Visibility.Collapsed; asianMnqDirectionRow.Visibility = Visibility.Collapsed; asianMgcDirectionRow.Visibility = Visibility.Collapsed; asianRiskModeRow.Visibility = Visibility.Collapsed; asianCombinedStopRow.Visibility = Visibility.Collapsed; asianMnqPriceMoveRow.Visibility = Visibility.Collapsed; asianMgcPriceMoveRow.Visibility = Visibility.Collapsed; asianMnqMaxReversalsRow.Visibility = Visibility.Collapsed; asianMgcMaxReversalsRow.Visibility = Visibility.Collapsed;
            asianModel.Children.Add(Txt("ONE DAILY CYCLE: MNQ and MGC enter together at the exact selected 1-minute bar (18:00 by default). Every stop closes that leg at the fixed cash loss, reverses at the next 1-minute open, and adds one micro. The shared reversal count applies separately to MNQ and MGC. AUTO MAX COMBINED REVERSAL LOSS = reversal legs × fixed loss × 2 instruments; it is a visible protection ceiling, while the combined profit target closes the daily cycle. Each completed cycle is copied to active virtual accounts; no rotation is used.", Orchid, 10, FontWeights.Bold));
            asianStrategyControls.Clear(); asianStrategyControls.Add(asianModel);
            bhStrategyControls.Add(stopModeRow); bhStrategyControls.Add(propQuantityRow); bhStrategyControls.Add(propTargetRow); bhStrategyControls.Add(propStopRow); bhStrategyControls.Add(dailyGoalRow); bhStrategyControls.Add(dailyLossRow); bhStrategyControls.Add(mnqLowOffsetRow); bhStrategyControls.Add(mgcLowOffsetRow); bhStrategyControls.Add(propModeNote); bhStrategyControls.Add(bhModelNote);
            model.Children.Add(stopModeRow); model.Children.Add(propQuantityRow); model.Children.Add(propTargetRow); model.Children.Add(propStopRow); model.Children.Add(dailyGoalRow); model.Children.Add(dailyLossRow); model.Children.Add(mnqLowOffsetRow); model.Children.Add(mgcLowOffsetRow); model.Children.Add(propModeNote); model.Children.Add(bhModelNote); model.Children.Add(asianModel);
            sessionHintText = Txt("NY OPEN: begins at the first 09:30 ET setup bar and ends at 15:55 ET.", Cyan, 10, FontWeights.Bold); data.Children.Add(sessionHintText);

            setupCards.Children.Add(PanelCard(data)); setupCards.Children.Add(PanelCard(model)); root.Children.Add(setupCards);

            var workflow = Stack(); workflow.Children.Add(Txt("3. START RESEARCH", Orchid, 13, FontWeights.Bold));
            strategyWorkflowText = Txt("BH = red candle → bullish reference → next high break. Every detected setup is automatically included in the research ledger and virtual pool. The review screen is for chart inspection or excluding a specific setup only.", Text, 11, FontWeights.Normal); strategyWorkflowText.TextWrapping = TextWrapping.Wrap; workflow.Children.Add(strategyWorkflowText);
            confirmConfigurationButton = Btn("START RESEARCH • LOAD + DETECT", Green); confirmConfigurationButton.Height = 38; confirmConfigurationButton.Click += delegate { ConfirmAndStartResearch(); }; workflow.Children.Add(confirmConfigurationButton);
            summaryText = Txt("NEXT: choose the test settings above, then click START RESEARCH. Keystone locks the screen while it loads and detects.", Gold, 11, FontWeights.Bold); workflow.Children.Add(summaryText);
            eventText = Txt("No candidate ledger yet.", Text, 10, FontWeights.Normal); workflow.Children.Add(eventText);
            setupStatsText = Txt("RAW SETUP TOTALS: waiting for Step 3. This count is independent of virtual accounts and rotation.", Cyan, 10, FontWeights.Bold); setupStatsText.TextWrapping = TextWrapping.Wrap; workflow.Children.Add(setupStatsText);
            root.Children.Add(PanelCard(workflow));

            RefreshSessionInputs();
            RefreshDateInputs();
            RefreshBhAggressionInputState();
            RefreshResearchModeState();
            RefreshStopModelInputState();
            RefreshStrategyInputState();
            RefreshAsianDerivedInputs();
            return PanelCard(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = root, Margin = new Thickness(2) });
        }

        private bool IsAsian75Selected()
        {
            return strategyBox != null && Convert.ToString(strategyBox.SelectedItem ?? string.Empty).StartsWith("ASIAN 75", StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshStrategyInputState()
        {
            bool asian = IsAsian75Selected();
            for (int i = 0; i < bhStrategyControls.Count; i++) if (bhStrategyControls[i] != null) bhStrategyControls[i].Visibility = asian ? Visibility.Collapsed : Visibility.Visible;
            for (int i = 0; i < asianStrategyControls.Count; i++) if (asianStrategyControls[i] != null) asianStrategyControls[i].Visibility = asian ? Visibility.Visible : Visibility.Collapsed;
            // Asian defaults to the combined MNQ + MGC study, but a backtest may intentionally
            // isolate MNQ or MGC.  The scope selector therefore remains active after the first
            // Asian default is applied; the report and data-request path use that exact scope.
            if (scopeBox != null)
            {
                if (asian && !asianScopeDefaultApplied) { scopeBox.SelectedIndex = 2; asianScopeDefaultApplied = true; }
                if (!asian) asianScopeDefaultApplied = false;
                scopeBox.IsEnabled = true;
            }
            if (timeframeBox != null) { if (asian) timeframeBox.SelectedIndex = 0; timeframeBox.IsEnabled = !asian; }
            if (sessionBox != null)
            {
                if (asian && sessionBox.SelectedIndex != 7) sessionBox.SelectedIndex = 7;
                if (!asian && sessionBox.SelectedIndex == 7) sessionBox.SelectedIndex = 0;
                sessionBox.IsEnabled = !asian;
            }
            if (strategyRuleText != null)
            {
                strategyRuleText.Foreground = asian ? Orchid : Gold;
                strategyRuleText.Text = asian
                    ? "ASIAN CYCLE BACKTEST: default scope is BOTH, but MNQ or MGC may be selected alone. Every selected instrument uses validated direct 1-minute bars, enters at the selected exact minute (18:00 ET by default) in the selected direction, reverses on the next minute after its fixed loss, and adds one micro per reversal. The automatic loss ceiling uses selected instruments × reversal legs × cash loss; the combined profit target closes the daily cycle."
                    : "BH RULE: after the chosen session begins, a red candle is followed by a bullish reference candle; the immediately next bar breaks that bullish high. Every valid long BH setup is detected and eligible by default. No contract month is required.";
            }
            if (confirmConfigurationButton != null) confirmConfigurationButton.Content = asian ? "START BACKTEST • LOAD + RUN CYCLES" : "START RESEARCH • LOAD + DETECT";
            if (strategyWorkflowText != null)
            {
                strategyWorkflowText.Text = asian
                    ? "ASIAN = no setup search. At the exact selected 1-minute opening bar, every selected instrument enters in the selected direction. A fixed reversal loss closes that leg; the next one-minute open reverses direction and adds one micro. The combined target, daily loss, or session end closes the cycle. For BOTH, MNQ and MGC must both have the exact opening bar—Keystone never substitutes a nearby price or silently tests only one instrument."
                    : "BH = red candle → bullish reference → next high break. Every detected setup is automatically included in the research ledger and virtual pool. The review screen is for chart inspection or excluding a specific setup only.";
            }
            RefreshSessionInputs();
            RefreshStopModelInputState();
            RefreshAsianRiskInputState();
            RefreshInstrumentSourceText();
        }

        private void RefreshAsianRiskInputState()
        {
            bool price = asianRiskModeBox != null && Convert.ToString(asianRiskModeBox.SelectedItem).StartsWith("PRICE", StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < asianCashRiskControls.Count; i++) if (asianCashRiskControls[i] != null) asianCashRiskControls[i].Visibility = price ? Visibility.Collapsed : Visibility.Visible;
            for (int i = 0; i < asianPriceRiskControls.Count; i++) if (asianPriceRiskControls[i] != null) asianPriceRiskControls[i].Visibility = price ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshAsianDerivedInputs()
        {
            if (asianDailyLossBox == null) return;
            double loss = Number(asianReversalLossBox, 75);
            int reversals = Math.Max(1, Integer(asianMaxReversalsBox, 4));
            string scope = scopeBox == null ? "BOTH" : Convert.ToString(scopeBox.SelectedItem ?? "BOTH");
            int instrumentCount = string.Equals(scope, "BOTH", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            double combined = Math.Max(0, loss) * reversals * instrumentCount;
            asianDailyLossBox.Text = combined.ToString("0.##", CultureInfo.InvariantCulture);
            if (asianMnqMaxReversalsBox != null) asianMnqMaxReversalsBox.Text = reversals.ToString(CultureInfo.InvariantCulture);
            if (asianMgcMaxReversalsBox != null) asianMgcMaxReversalsBox.Text = reversals.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshDateInputs()
        {
            if (dateModeBox == null || startBox == null || endBox == null) return;
            bool oneDay = string.Equals(Convert.ToString(dateModeBox.SelectedItem), "ONE DAY", StringComparison.OrdinalIgnoreCase);
            if (oneDay) { endBox.Text = startBox.Text; endBox.IsEnabled = false; }
            else endBox.IsEnabled = true;
        }

        private void RefreshBhAggressionInputState()
        {
            bool stronger = bhFilterBox != null && string.Equals(Convert.ToString(bhFilterBox.SelectedItem), "STRONGER BH • USE THRESHOLDS BELOW", StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < strongerBhControls.Count; i++)
                if (strongerBhControls[i] != null) strongerBhControls[i].Visibility = stronger ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshResearchModeState()
        {
            if (accountPathBox != null) accountPathBox.SelectedIndex = 0;
            if (stopModeBox != null && Convert.ToString(stopModeBox.SelectedItem).StartsWith("LIVE", StringComparison.OrdinalIgnoreCase)) stopModeBox.SelectedIndex = 0;
            for (int i = 0; i < personalOnlyControls.Count; i++) if (personalOnlyControls[i] != null) personalOnlyControls[i].Visibility = Visibility.Collapsed;
            for (int i = 0; i < liveConfigurationControls.Count; i++) if (liveConfigurationControls[i] != null) liveConfigurationControls[i].Visibility = Visibility.Collapsed;
            for (int i = 0; i < propConfigurationControls.Count; i++) if (propConfigurationControls[i] != null) propConfigurationControls[i].Visibility = Visibility.Visible;
            RefreshStopModelInputState();
        }

        private void RefreshStopModelInputState()
        {
            string selected = stopModeBox == null ? string.Empty : Convert.ToString(stopModeBox.SelectedItem);
            bool low = selected.IndexOf("BELOW 3-CANDLE LOW", StringComparison.OrdinalIgnoreCase) >= 0 && !IsAsian75Selected();
            bool autoRiskLot = false;
            for (int i = 0; i < lowStopControls.Count; i++)
                if (lowStopControls[i] != null) lowStopControls[i].Visibility = low ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < autoRiskLotControls.Count; i++)
                if (autoRiskLotControls[i] != null) autoRiskLotControls[i].Visibility = Visibility.Visible;
                else if (autoRiskLotControls[i] != null) autoRiskLotControls[i].Visibility = Visibility.Collapsed;
            if (breakEvenMoveBox != null) breakEvenMoveBox.IsEnabled = breakEvenBox != null && breakEvenBox.IsChecked == true;
        }

        private void SetOneDay(DateTime day)
        {
            if (dateModeBox != null) dateModeBox.SelectedIndex = 0;
            if (startBox != null) startBox.Text = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (endBox != null) endBox.Text = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            RefreshDateInputs(); InvalidateConfigurationApproval();
        }

        private void RefreshSessionInputs()
        {
            if (sessionBox == null || customStartBox == null || endTimeBox == null) return;
            string display = Convert.ToString(sessionBox.SelectedItem ?? "NY OPEN 09:30-15:55");
            bool editable = display == "CUSTOM RANGE";
            if (display == "NY OPEN 09:30-15:55") { customStartBox.Text = "930"; endTimeBox.Text = "1555"; }
            if (display == "NY EARLY 08:00-15:55") { customStartBox.Text = "800"; endTimeBox.Text = "1555"; }
            if (display == "ASIA 19:00-03:00") { customStartBox.Text = "1900"; endTimeBox.Text = "300"; }
            if (display == "LONDON 03:00-11:30") { customStartBox.Text = "300"; endTimeBox.Text = "1130"; }
            if (display == "FULL GLOBEX 18:00-15:55") { customStartBox.Text = "1800"; endTimeBox.Text = "1555"; }
            if (display == "ASIAN CYCLE • CONFIGURED TIME") { customStartBox.Text = "1800"; endTimeBox.Text = "1555"; }
            customStartBox.IsEnabled = editable;
            endTimeBox.IsEnabled = editable;
            for (int i = 0; i < customSessionControls.Count; i++)
                if (customSessionControls[i] != null) customSessionControls[i].Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
            if (sessionHintText == null) return;
            if (display == "FULL GLOBEX 18:00-15:55") sessionHintText.Text = "FULL GLOBEX: fixed 18:00 ET → 15:55 ET next day. The 17:00-18:00 CME maintenance break is outside this research window.";
            else if (display == "ASIAN CYCLE • CONFIGURED TIME") sessionHintText.Text = "ASIAN CYCLE BACKTEST: set the exact entry and end HHMM in the Asian parameter panel. Each chosen time uses direct 1-minute data; no prior-close or nearest-bar substitute is used.";
            else if (display == "ASIA 19:00-03:00") sessionHintText.Text = "ASIA: fixed 19:00 ET → 03:00 ET next day. This is a named research label; it is not an exchange-defined quality rating.";
            else if (display == "LONDON 03:00-11:30") sessionHintText.Text = "LONDON: fixed 03:00 ET → 11:30 ET. Use CUSTOM around the U.S./UK daylight-saving transition weeks if exact London-local alignment is needed.";
            else if (display == "INSTRUMENT DEFAULT") sessionHintText.Text = "INSTRUMENT DEFAULT: MNQ starts 09:30; MGC starts 08:00; both end 15:55.";
            else if (display == "CUSTOM RANGE") sessionHintText.Text = "CUSTOM RANGE: enter an exact Eastern-Time start and end in HHMM. An end earlier than the start continues into the next calendar day.";
            else if (display == "NY EARLY 08:00-15:55") sessionHintText.Text = "NY EARLY: fixed 08:00 ET → 15:55 ET. It includes pre-open, NY open, and the regular U.S. session through the selected close.";
            else sessionHintText.Text = "NY OPEN: fixed 09:30 ET → 15:55 ET. The first 09:30 setup bar is included.";
        }

        private void InvalidateConfigurationApproval()
        {
            configurationApproved = false;
            configurationApprovalKey = string.Empty;
            // A real input change creates a new candidate configuration; it may be submitted
            // after the user confirms that the old in-memory research should be cleared.
            researchSubmissionLocked = false;
            UpdateWorkflowState();
        }

        private void WatchConfigurationInput(TextBox input)
        {
            if (input == null) return;
            input.LostFocus += delegate { InvalidateConfigurationApproval(); };
        }

        private string ConfigurationInputKey()
        {
            return string.Join("|", new[]
            {
                strategyBox == null ? string.Empty : Convert.ToString(strategyBox.SelectedItem),
                scopeBox == null ? string.Empty : Convert.ToString(scopeBox.SelectedItem),
                accountPathBox == null ? string.Empty : Convert.ToString(accountPathBox.SelectedItem),
                bhFilterBox == null ? string.Empty : Convert.ToString(bhFilterBox.SelectedItem),
                mnqStrongRedBox == null ? string.Empty : (mnqStrongRedBox.Text ?? string.Empty).Trim(),
                mnqStrongDeclineBox == null ? string.Empty : (mnqStrongDeclineBox.Text ?? string.Empty).Trim(),
                mgcStrongRedBox == null ? string.Empty : (mgcStrongRedBox.Text ?? string.Empty).Trim(),
                mgcStrongDeclineBox == null ? string.Empty : (mgcStrongDeclineBox.Text ?? string.Empty).Trim(),
                bhStrongCombineBox == null ? string.Empty : Convert.ToString(bhStrongCombineBox.SelectedItem),
                sessionBox == null ? string.Empty : Convert.ToString(sessionBox.SelectedItem),
                dateModeBox == null ? string.Empty : Convert.ToString(dateModeBox.SelectedItem),
                timeframeBox == null ? string.Empty : Convert.ToString(timeframeBox.SelectedItem),
                bhSetupBox == null ? "1" : (bhSetupBox.IsChecked == true ? "1" : "0"),
                fvgSetupBox == null ? "1" : (fvgSetupBox.IsChecked == true ? "1" : "0"),
                startBox == null ? string.Empty : (startBox.Text ?? string.Empty).Trim(),
                endBox == null ? string.Empty : (endBox.Text ?? string.Empty).Trim(),
                quantityBox == null ? string.Empty : (quantityBox.Text ?? string.Empty).Trim(),
                targetBox == null ? string.Empty : (targetBox.Text ?? string.Empty).Trim(),
                stopBox == null ? string.Empty : (stopBox.Text ?? string.Empty).Trim(),
                dailyGoalBox == null ? string.Empty : (dailyGoalBox.Text ?? string.Empty).Trim(),
                dailyLossBox == null ? string.Empty : (dailyLossBox.Text ?? string.Empty).Trim(),
                asianReversalLossBox == null ? string.Empty : (asianReversalLossBox.Text ?? string.Empty).Trim(),
                asianStartTimeBox == null ? string.Empty : (asianStartTimeBox.Text ?? string.Empty).Trim(),
                asianEndTimeBox == null ? string.Empty : (asianEndTimeBox.Text ?? string.Empty).Trim(),
                asianMnqDirectionBox == null ? string.Empty : Convert.ToString(asianMnqDirectionBox.SelectedItem),
                asianMgcDirectionBox == null ? string.Empty : Convert.ToString(asianMgcDirectionBox.SelectedItem),
                asianRiskModeBox == null ? string.Empty : Convert.ToString(asianRiskModeBox.SelectedItem),
                asianMnqPriceMoveBox == null ? string.Empty : (asianMnqPriceMoveBox.Text ?? string.Empty).Trim(),
                asianMgcPriceMoveBox == null ? string.Empty : (asianMgcPriceMoveBox.Text ?? string.Empty).Trim(),
                asianCycleTargetBox == null ? string.Empty : (asianCycleTargetBox.Text ?? string.Empty).Trim(),
                asianDailyLossBox == null ? string.Empty : (asianDailyLossBox.Text ?? string.Empty).Trim(),
                asianStartingQuantityBox == null ? string.Empty : (asianStartingQuantityBox.Text ?? string.Empty).Trim(),
                asianCombinedStopBox == null ? string.Empty : (asianCombinedStopBox.Text ?? string.Empty).Trim(),
                asianInstrumentStopBox == null ? string.Empty : (asianInstrumentStopBox.Text ?? string.Empty).Trim(),
                asianMnqInstrumentStopBox == null ? string.Empty : (asianMnqInstrumentStopBox.Text ?? string.Empty).Trim(),
                asianMgcInstrumentStopBox == null ? string.Empty : (asianMgcInstrumentStopBox.Text ?? string.Empty).Trim(),
                asianBreakEvenBox == null ? string.Empty : (asianBreakEvenBox.Text ?? string.Empty).Trim(),
                asianMaxReversalsBox == null ? string.Empty : (asianMaxReversalsBox.Text ?? string.Empty).Trim(),
                asianMnqMaxReversalsBox == null ? string.Empty : (asianMnqMaxReversalsBox.Text ?? string.Empty).Trim(),
                asianMgcMaxReversalsBox == null ? string.Empty : (asianMgcMaxReversalsBox.Text ?? string.Empty).Trim(),
                customStartBox == null ? string.Empty : (customStartBox.Text ?? string.Empty).Trim(),
                endTimeBox == null ? string.Empty : (endTimeBox.Text ?? string.Empty).Trim()
            });
        }

        private bool HasSelectedData()
        {
            bool requiresMnq = config.Scope == "MNQ" || config.Scope == "BOTH";
            bool requiresMgc = config.Scope == "MGC" || config.Scope == "BOTH";
            // Selected-timeframe bars are sufficient to build and audit the setup ledger.
            // One-minute data is a separate hard requirement for outcome/P&L/pool math.
            return (!requiresMnq || mnqSetupBars.Count > 0) && (!requiresMgc || mgcSetupBars.Count > 0) && (requiresMnq || requiresMgc);
        }

        private bool ConfigurationStillApproved()
        {
            return configurationApproved && !string.IsNullOrWhiteSpace(configurationApprovalKey) && string.Equals(configurationApprovalKey, ConfigurationInputKey(), StringComparison.Ordinal);
        }

        private void ConfirmConfiguration()
        {
            DateTime start, end;
            string priorKey = config.Snapshot();
            if (!ReadConfig(out start, out end)) return;
            RefreshLifecycleInputState();
            string currentKey = config.Snapshot();
            if (events.Count > 0 && !string.Equals(priorKey, currentKey, StringComparison.Ordinal))
            {
                if (MessageBox.Show("The confirmed configuration changed. Clear the current candidates, review decisions, and virtual pool before requesting a new range? Saved files will remain.", "Confirm new research configuration", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    UpdateUi("CONFIGURATION NOT CONFIRMED • CURRENT REVIEWED RUN WAS KEPT", Gold);
                    return;
                }
                ClearCurrentResearch(false);
            }
            configurationApproved = true;
            configurationApprovalKey = ConfigurationInputKey();
            UpdateUi("CONFIGURATION CONFIRMED • LOADING " + config.Scope + " HISTORY FOR " + SelectedTestDateLabel(), Green);
            UpdateWorkflowState();
        }

        // One visible action validates the configuration, loads NinjaTrader history, and then
        // builds the setup ledger.  Later tabs remain locked until their own prerequisite exists.
        private void ConfirmAndStartResearch()
        {
            if (operationBusy || isProcessing) return;
            if (researchSubmissionLocked)
            {
                UpdateUi("THIS CONFIGURATION IS ALREADY LOADED • USE NEW TEST OR CHANGE A SETTING BEFORE STARTING AGAIN", Gold);
                UpdateWorkflowState();
                return;
            }
            autoRunResearchAfterLoad = true;
            ConfirmConfiguration();
            if (!ConfigurationStillApproved()) { autoRunResearchAfterLoad = false; return; }
            researchSubmissionLocked = true;
            RequestHistory();
        }

        private void UpdateWorkflowState()
        {
            bool asianBacktest = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            bool approved = ConfigurationStillApproved();
            bool requestActive = pendingRequests > 0;
            bool dataReady = HasSelectedData();
            bool detected = events.Count > 0;
            bool eligible = events.Any(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            bool simulated = accounts.Count > 0;
            bool outcomesVerified = config.OutcomeModelEnabled == 1;
            bool busy = operationBusy || isProcessing;
            int activeTab = workspaceTabs == null ? -1 : workspaceTabs.SelectedIndex;
            if (confirmConfigurationButton != null) confirmConfigurationButton.IsEnabled = !busy && !researchSubmissionLocked;
            if (resetNewTestButton != null) resetNewTestButton.IsEnabled = !busy;
            if (requestButton != null) requestButton.IsEnabled = false;
            if (runButton != null) runButton.IsEnabled = false;
            if (cancelButton != null) cancelButton.IsEnabled = false;
            if (openReviewButton != null) openReviewButton.IsEnabled = false;
            if (configureTab != null) configureTab.IsEnabled = !busy || activeTab == 0;
            if (reviewTab != null) reviewTab.IsEnabled = detected && (!busy || activeTab == 1);
            // Scenario controls and evidence are useful after a successful setup-bar load even
            // before outcome resolution is available. Only the P/L-dependent actions remain
            // disabled until the direct 1M price path is verified.
            if (resultsTab != null) resultsTab.IsEnabled = detected && (!busy || activeTab == 2);
            if (savedRunsTab != null) savedRunsTab.IsEnabled = detected && (!busy || activeTab == 3);
            if (refreshMathButton != null) refreshMathButton.IsEnabled = eligible && outcomesVerified && !busy;
            if (runPoolButton != null) runPoolButton.IsEnabled = eligible && outcomesVerified && !busy;
            if (clearPoolButton != null) clearPoolButton.IsEnabled = accounts.Count > 0 && !busy;
            if (evidenceAfterPoolButton != null) evidenceAfterPoolButton.IsEnabled = detected && dataReady && !busy;
            if (comparisonRunButton != null) comparisonRunButton.IsEnabled = detected && dataReady && !busy;
            for (int i = 0; i < saveButtons.Count; i++) saveButtons[i].IsEnabled = detected && !busy;
            for (int i = 0; i < exportButtons.Count; i++) exportButtons[i].IsEnabled = detected && !busy;
            if (researchPackageButton != null) researchPackageButton.IsEnabled = simulated && !busy;
            if (workflowText == null) return;
            if (busy) workflowText.Text = "WORKING: " + (string.IsNullOrWhiteSpace(operationMessage) ? "processing" : operationMessage) + ". Wait for the completion message.";
            else if (!approved) workflowText.Text = "NEXT: choose the date, session, timeframe, and risk inputs, then click START RESEARCH.";
            else if (requestActive) workflowText.Text = "WORKING: NinjaTrader history is loading. The research ledger builds automatically when it finishes.";
            else if (!dataReady) workflowText.Text = "DATA DID NOT LOAD. The failed receipt is cleared on retry: correct the selected date or open-chart history if needed, then click START RESEARCH again. Use NEW TEST only to reset all settings.";
            else if (!detected) workflowText.Text = researchRunCompleted
                ? (asianBacktest ? "ASIAN BACKTEST COMPLETE: zero cycle legs were created. Read the exact-opening diagnostic above; no nearest-bar entry was substituted." : "RESEARCH COMPLETE: no detector-qualified setups were found in the selected data window.")
                : (asianBacktest ? "WORKING: running the configured MNQ/MGC daily-cycle backtest." : "WORKING: building the detector-qualified setup ledger.");
            else if (!eligible) workflowText.Text = asianBacktest ? "REVIEW: every resolved daily-cycle leg was excluded. Restore one or more rows to run the copy-account scenario." : "REVIEW: every detected setup was excluded. Restore at least one setup to run the virtual pool.";
            else if (!outcomesVerified) workflowText.Text = asianBacktest ? "ASIAN BACKTEST BARS READY: the direct 1-minute path was not verified, so cycle P/L and copy-account math remain disabled." : "SETUP REVIEW READY: setup bars loaded. Simulation settings and evidence charts are available; only outcome P/L and virtual-pool calculations remain disabled until the direct 1-minute price path is verified.";
            else if (!simulated) workflowText.Text = asianBacktest ? "NEXT: open Simulation Results, choose copy-account lifecycle assumptions, then click RUN VIRTUAL POOL." : "NEXT: open Simulation Results, choose accounts and lifecycle assumptions, then click RUN VIRTUAL POOL.";
            else workflowText.Text = "COMPLETE: inspect the virtual-account results, then save or export the research package.";
        }

        private UIElement HistoryTab()
        {
            var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var controls = new UniformGrid { Columns = 3, Margin = new Thickness(8) };
            requestButton = Btn("1. REQUEST SELECTED NINJATRADER DATA", Blue); requestButton.Click += delegate { RequestHistory(); };
            runButton = Btn("2. BUILD PENDING EVENT LEDGER", Green); runButton.IsEnabled = false; runButton.Click += delegate { RunResearch(); };
            cancelButton = Btn("CANCEL REQUEST", Red); cancelButton.Click += delegate { CancelRequests(); UpdateUi("CANCELLED", Gold); };
            controls.Children.Add(requestButton); controls.Children.Add(runButton); controls.Children.Add(cancelButton); grid.Children.Add(controls);
            summaryText = Txt("CHOOSE A RANGE IN RESEARCH SETUP", Gold, 13, FontWeights.Bold); summaryText.Margin = new Thickness(8); Grid.SetRow(summaryText, 1); grid.Children.Add(summaryText);
            eventText = Txt("This screen creates PENDING candidates only. Review and accept entries in 3. VERIFY ENTRIES before you run the pool.", Text, 11, FontWeights.Normal); eventText.FontFamily = new FontFamily("Consolas"); eventText.TextWrapping = TextWrapping.Wrap;
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = eventText, Margin = new Thickness(8) }; Grid.SetRow(scroll, 2); grid.Children.Add(scroll); return PanelCard(grid);
        }

        private UIElement ReviewTab()
        {
            var root = new Grid { Margin = new Thickness(6) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            var left = new Grid { Margin = new Thickness(2) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var verifyHeading = Txt("VERIFY DETECTED ENTRIES", Cyan, 15, FontWeights.Bold); left.Children.Add(verifyHeading);
            var verifyInfo = Txt("Every detector-qualified setup is already included automatically. This screen is a read-only ledger check; no acceptance action is required.", Muted, 11, FontWeights.Normal); Grid.SetRow(verifyInfo, 1); left.Children.Add(verifyInfo);
            reviewLedgerText = Txt("LEDGER: waiting for detection.", Gold, 10, FontWeights.Bold); Grid.SetRow(reviewLedgerText, 2); left.Children.Add(reviewLedgerText);
            reviewList = new ListBox { Background = Card, Foreground = Text, BorderBrush = Blue, BorderThickness = new Thickness(1), Margin = new Thickness(6), MinHeight = 0 };
            ScrollViewer.SetVerticalScrollBarVisibility(reviewList, ScrollBarVisibility.Visible);
            ScrollViewer.SetHorizontalScrollBarVisibility(reviewList, ScrollBarVisibility.Disabled);
            reviewList.SelectionChanged += delegate { UpdateReviewDetail(); };
            Grid.SetRow(reviewList, 3); left.Children.Add(reviewList);

            var right = Stack();
            right.Children.Add(Txt("SELECTED EVENT", Orchid, 15, FontWeights.Bold));
            reviewDetailText = Txt("Run detection to create an eligible setup ledger.", Text, 12, FontWeights.Normal); reviewDetailText.FontFamily = new FontFamily("Consolas"); right.Children.Add(PanelCard(reviewDetailText));
            var proceed = Btn("PROCEED TO SIMULATION SETTINGS", Green); proceed.Height = 42; proceed.Click += delegate { ProceedFromVerifyToSimulation(); }; right.Children.Add(proceed);
            right.Children.Add(Txt("NEXT: proceed to Simulation Results to inspect scenario settings and open chart setups. A virtual-pool calculation requires the direct 1-minute outcome path; the chart itself does not.", Gold, 11, FontWeights.Bold));
            var leftCard = PanelCard(left);
            var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = right, Margin = new Thickness(0) };
            var rightCard = PanelCard(rightScroll);
            root.Children.Add(leftCard);
            Grid.SetColumn(rightCard, 1);
            root.Children.Add(rightCard);
            return PanelCard(root);
        }

        private UIElement ResultsTab()
        {
            // Keep the operating dashboard inside the TabControl's available height.  The old
            // outer page ScrollViewer made the entire results page move whenever users tried to
            // browse account cards.  Only intentional internal regions may scroll now.
            var root = new Grid { Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = Stack();
            resultsHeadingText = Txt("PROP VIRTUAL-POOL RESULTS • ALL ELIGIBLE SETUPS", Cyan, 14, FontWeights.Bold); resultsHeadingText.Margin = new Thickness(4, 1, 4, 0); top.Children.Add(resultsHeadingText);
            var resultIntro = Txt("Historical virtual-pool model only • verified 1-minute outcomes are required for P/L and lifecycle math.", Muted, 10, FontWeights.Normal); resultIntro.Margin = new Thickness(4, 0, 4, 1); top.Children.Add(resultIntro);
            resultScopeText = Txt(BuildStudyScopeLabel(), Text, 12, FontWeights.Bold);
            top.Children.Add(new Border { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 2, 0, 1), Child = resultScopeText });
            var actions = new UniformGrid { Columns = 4, Margin = new Thickness(0, 2, 0, 2) };
            runPoolButton = Btn("RUN VIRTUAL POOL", Green); runPoolButton.IsEnabled = false; runPoolButton.Click += delegate { SimulatePool(); };
            clearPoolButton = Btn("CLEAR POOL RESULTS", Red); clearPoolButton.IsEnabled = false; clearPoolButton.Click += delegate { ClearPoolResults(); };
            evidenceAfterPoolButton = Btn("SHOW CHART SETUPS", Blue); evidenceAfterPoolButton.IsEnabled = false; evidenceAfterPoolButton.Click += delegate { OpenEvidenceChart(); };
            researchPackageButton = Btn("EXPORT RESEARCH PACKAGE", Orchid); researchPackageButton.IsEnabled = false; researchPackageButton.Click += delegate { ExportEvidencePackage(); }; exportButtons.Add(researchPackageButton);
            comparisonRunButton = Btn("COMPARE TIMEFRAMES", Cyan); comparisonRunButton.IsEnabled = false; comparisonRunButton.Click += delegate { OpenComparisonWorkbench(); };
            actions.Children.Add(runPoolButton); actions.Children.Add(evidenceAfterPoolButton); actions.Children.Add(comparisonRunButton); actions.Children.Add(researchPackageButton); top.Children.Add(actions);
            var clearRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 1) }; clearPoolButton.Height = 27; clearRow.Children.Add(clearPoolButton); top.Children.Add(clearRow);
            poolResultBanner = Txt("NEXT: choose settings, then RUN VIRTUAL POOL. Chart setups are available after detection.", Gold, 10, FontWeights.Bold); poolResultBanner.Margin = new Thickness(4, 0, 4, 1); lifecycleText = poolResultBanner; top.Children.Add(poolResultBanner);
            // Account detail always shows every assigned row. The full filters and unassigned-row audit live in the single exported package.
            showWinsBox = new CheckBox { IsChecked = true }; showLossesBox = new CheckBox { IsChecked = true }; showExitsBox = new CheckBox { IsChecked = true }; showNoEntryBox = new CheckBox { IsChecked = true };
            Grid.SetRow(top, 0); root.Children.Add(top);

            poolLifecycleMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 4, 0, 4) };
            poolGrossWithdrawalMetric = MetricTile(poolLifecycleMetrics, "GROSS WITHDRAWALS", "$0", "before the selected account-share split and before costs", Gold);
            poolNetCashAfterCostMetric = MetricTile(poolLifecycleMetrics, "FULL NET CASH AFTER ALL COSTS", "$0", "cash after share less every modeled evaluation/replacement cost; not trading P/L", Green);
            poolPayoutCycleMetric = MetricTile(poolLifecycleMetrics, "PAYOUT CYCLES", "0", "completed virtual withdrawal cycles", Cyan);
            poolBlownMetric = MetricTile(poolLifecycleMetrics, "BLOWN / ENDED", "0", "terminal slots; no future assignments", Red);
            Grid.SetRow(poolLifecycleMetrics, 1); root.Children.Add(poolLifecycleMetrics);

            oneDayPoolMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 4, 0, 4), Visibility = Visibility.Collapsed };
            oneDayEligibleMetric = MetricTile(oneDayPoolMetrics, "ELIGIBLE SETUPS", "0", "detected and accepted before account allocation", Cyan);
            oneDayAssignedMetric = MetricTile(oneDayPoolMetrics, "ASSIGNED TRADES", "0", "trades placed before account daily locks", Green);
            oneDayAccountsTradedMetric = MetricTile(oneDayPoolMetrics, "ACCOUNTS TRADED", "0 / 0", "accounts with at least one assigned trade", Blue);
            oneDayProfitLockedMetric = MetricTile(oneDayPoolMetrics, "DAILY PROFIT LOCKS", "0", "accounts that reached the selected daily profit lock", Green);
            oneDayLossLockedMetric = MetricTile(oneDayPoolMetrics, "DAILY LOSS LOCKS", "0", "accounts that reached the selected daily loss lock", Red);
            oneDayUnusedMetric = MetricTile(oneDayPoolMetrics, "UNUSED ACCOUNTS", "0", "no trade was assigned during this one-day study", Muted);
            oneDaySkippedMetric = MetricTile(oneDayPoolMetrics, "SKIPPED SETUPS", "0", "eligible setups after every account was busy or daily locked", Gold);
            oneDayPnlMetric = MetricTile(oneDayPoolMetrics, "ASSIGNED MODEL P/L", "$0", "sum of only the assigned one-day trade outcomes", Gold);
            Grid.SetRow(oneDayPoolMetrics, 2); root.Children.Add(oneDayPoolMetrics);

            var resultViews = new TabControl { Background = Panel, BorderBrush = Cyan, BorderThickness = new Thickness(1), Margin = new Thickness(0, 2, 0, 0), TabStripPlacement = Dock.Top, VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Grid.SetRow(resultViews, 3); root.Children.Add(resultViews);
            var body = new Grid { MinHeight = 0 };
            // Keep the default settings view short enough to read without a page scroll.  The
            // only expanding group is the explicit evaluation override, which has no effect
            // until its toggle is selected.
            var controls = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };
            var coreControls = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            var lifecyclePolicyControls = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            var governanceControls = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            evaluationOnlyControls.Clear(); oneDayHiddenControls.Clear(); propOnlyControls.Clear();
            poolBox = Select("1", "5", "10", "20", "40"); poolBox.SelectedItem = "20"; poolBox.SelectionChanged += delegate { RefreshLifecycleInputState(); }; var poolRow = PoolRow("ACCOUNTS", poolBox); propOnlyControls.Add(poolRow); coreControls.Children.Add(poolRow);
            accountStartModeBox = Select("EVALUATION FIRST", "DIRECT FUNDED"); accountStartModeBox.SelectedIndex = 0; accountStartModeBox.SelectionChanged += delegate { RefreshLifecycleInputState(); }; var startModeRow = PoolRow("START", accountStartModeBox); oneDayHiddenControls.Add(startModeRow); propOnlyControls.Add(startModeRow); coreControls.Children.Add(startModeRow);
            copyTradingPoolBox = new CheckBox { Content = "COPY EVERY SETUP TO ALL ACTIVE", IsChecked = false, Foreground = Cyan, Margin = new Thickness(6), ToolTip = "Off: rotate each eligible setup to the next free account. On: every active account takes the same eligible setup, locks/blows/passes together, and replacement evaluations restart together on the next session." };
            coreControls.Children.Add(PoolRow("ALLOCATION", copyTradingPoolBox));
            multipleSetupsPerDayBox = new CheckBox { Content = "ALLOW MULTIPLE SETUPS / DAY", IsChecked = false, Foreground = Gold, Margin = new Thickness(6), ToolTip = "Off (default): each rotating or BH copy account receives at most one setup per session. On: keep assigning additional BH setups to the same account(s) until the configured daily profit/loss lock, total drawdown, blowout, or other lifecycle state is reached. Asian 75 remains one configured cycle per session." };
            coreControls.Children.Add(PoolRow("DAILY ALLOCATION", multipleSetupsPerDayBox));
            propStartingBalanceBox = Input("0");
            UIElement singleStartBalanceRow = PoolRow("SINGLE ACCOUNT START BALANCE $", propStartingBalanceBox);
            singleAccountControls.Clear(); singleAccountControls.Add(singleStartBalanceRow);
            evalTargetBox = Input("3000"); evalDailyCapBox = Input("1500"); evalConsistencyBox = Input("0"); evalFailureBox = Input("2000"); evalDailyLossBox = Input("500"); fundedDailyLossBox = Input("0"); fundedFailureBox = Input("2000"); evalCostBox = Input("120"); minimumQualifyingDayBox = Input("150"); evalTradeTargetBox = Input("1500"); evalTradeStopBox = Input("500");
            payoutThresholdBox = Input("4000"); payoutDaysBox = Input("5"); payoutAmountBox = Input("2000"); payoutProfitShareBox = Input("100"); minimumDaysBox = Input("2");
            var evalTargetRow = PoolRow("EVAL TARGET $", evalTargetBox); var evalDailyCapRow = PoolRow("EVAL DAILY + $", evalDailyCapBox); var evalConsistencyRow = PoolRow("BEST DAY % (0=OFF)", evalConsistencyBox); var evalFailureRow = PoolRow("EVAL DRAWDOWN $", evalFailureBox); var evalDailyLossRow = PoolRow("EVAL DAILY - $", evalDailyLossBox); var evalCostRow = PoolRow("EVAL COST $", evalCostBox); var minimumDaysRow = PoolRow("EVAL PASS DAYS", minimumDaysBox);
            evalStageTradeRulesBox = new CheckBox { Content = "USE EVAL OVERRIDE", IsChecked = false, Foreground = Orchid, Margin = new Thickness(4), ToolTip = "Off: evaluation uses the main Step 1 trade and daily limits. On: evaluation-only target, drawdown, daily, and per-trade values replace them." }; evalStageTradeRulesBox.Checked += delegate { RefreshLifecycleInputState(); }; evalStageTradeRulesBox.Unchecked += delegate { RefreshLifecycleInputState(); };
            replacementFundingGateBox = new CheckBox { Content = "WAIT FOR PAYOUT BEFORE REBUY", IsChecked = false, Foreground = Green, Margin = new Thickness(6), ToolTip = "OFF (default): a blown evaluation or funded slot buys a new evaluation at the next session so later setups are not skipped. ON: failed slots are benched and later setups are skipped until modeled payout cash funds every pending replacement." };
            firmFundedCapBox = new CheckBox { Content = "FIRM FUNDED CAP", IsChecked = false, Foreground = Gold, Margin = new Thickness(6), ToolTip = "When on, evaluation slots are grouped by firm. Passed evaluations wait if that firm has reached its funded-account capacity." }; firmFundedCapBox.Checked += delegate { RefreshLifecycleInputState(); }; firmFundedCapBox.Unchecked += delegate { RefreshLifecycleInputState(); };
            var evalStageToggleRow = PoolRow("EVAL TERMS", evalStageTradeRulesBox); var evalTradeTargetRow = PoolRow("EVAL TRADE + $", evalTradeTargetBox); var evalTradeStopRow = PoolRow("EVAL TRADE - $", evalTradeStopBox); var replacementGateRow = PoolRow("REPLACEMENT", replacementFundingGateBox);
            firmEvalSlotsBox = Input("10"); firmMaxFundedBox = Input("5");
            var firmCapToggleRow = PoolRow("FIRM CAP MODEL", firmFundedCapBox);
            var firmCapPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            firmCapPanel.Children.Add(PoolRow("EVAL SLOTS / FIRM", firmEvalSlotsBox)); firmCapPanel.Children.Add(PoolRow("MAX FUNDED / FIRM", firmMaxFundedBox));
            evalOverridePanel = new WrapPanel { Margin = new Thickness(3, 0, 3, 2), Visibility = Visibility.Collapsed };
            var overrideTitle = Txt("EVAL OVERRIDE • replaces Step 1 limits only while this account is in EVALUATION", Orchid, 10, FontWeights.Bold); overrideTitle.Width = 800; overrideTitle.Margin = new Thickness(7, 4, 7, 1); overrideTitle.TextWrapping = TextWrapping.Wrap;
            evalOverridePanel.Children.Add(overrideTitle); evalOverridePanel.Children.Add(evalTargetRow); evalOverridePanel.Children.Add(evalFailureRow); evalOverridePanel.Children.Add(evalDailyCapRow); evalOverridePanel.Children.Add(evalDailyLossRow); evalOverridePanel.Children.Add(evalTradeTargetRow); evalOverridePanel.Children.Add(evalTradeStopRow);
            evaluationTradeRuleControls.Clear(); evaluationTradeRuleControls.Add(evalStageToggleRow); evaluationTradeRuleControls.Add(evalOverridePanel);
            firmCapControls.Clear(); firmCapControls.Add(firmCapPanel);
            evaluationOnlyControls.Add(evalCostRow); evaluationOnlyControls.Add(minimumDaysRow); evaluationOnlyControls.Add(evalStageToggleRow); evaluationOnlyControls.Add(evalOverridePanel); evaluationOnlyControls.Add(replacementGateRow); evaluationOnlyControls.Add(firmCapToggleRow); evaluationOnlyControls.Add(firmCapPanel);
            propOnlyControls.Add(evalCostRow); propOnlyControls.Add(minimumDaysRow); propOnlyControls.Add(evalStageToggleRow); propOnlyControls.Add(evalOverridePanel); propOnlyControls.Add(replacementGateRow); propOnlyControls.Add(firmCapToggleRow); propOnlyControls.Add(firmCapPanel);
            var payoutThresholdRow = PoolRow("PAYOUT BALANCE $", payoutThresholdBox); var payoutDaysRow = PoolRow("PAYOUT DAYS", payoutDaysBox); var payoutAmountRow = PoolRow("WITHDRAWAL $", payoutAmountBox); var payoutProfitShareRow = PoolRow("ACCOUNT SHARE %", payoutProfitShareBox); var qualifyingDayRow = PoolRow("QUALIFYING DAY $", minimumQualifyingDayBox); var fundedDailyLossRow = PoolRow("FUNDED DAILY LOSS $ (0=OFF)", fundedDailyLossBox); var fundedFailureRow = PoolRow("FUNDED TOTAL DRAWDOWN $", fundedFailureBox);
            oneDayHiddenControls.Add(evalCostRow); oneDayHiddenControls.Add(minimumDaysRow); oneDayHiddenControls.Add(evalStageToggleRow); oneDayHiddenControls.Add(evalOverridePanel); oneDayHiddenControls.Add(replacementGateRow); oneDayHiddenControls.Add(firmCapToggleRow); oneDayHiddenControls.Add(firmCapPanel); oneDayHiddenControls.Add(payoutThresholdRow); oneDayHiddenControls.Add(payoutDaysRow); oneDayHiddenControls.Add(payoutAmountRow); oneDayHiddenControls.Add(payoutProfitShareRow); oneDayHiddenControls.Add(qualifyingDayRow);
            propOnlyControls.Add(payoutThresholdRow); propOnlyControls.Add(payoutDaysRow); propOnlyControls.Add(payoutAmountRow); propOnlyControls.Add(payoutProfitShareRow); propOnlyControls.Add(qualifyingDayRow);
            coreControls.Children.Add(singleStartBalanceRow);
            lifecyclePolicyControls.Children.Add(evalCostRow); lifecyclePolicyControls.Children.Add(minimumDaysRow); lifecyclePolicyControls.Children.Add(payoutThresholdRow); lifecyclePolicyControls.Children.Add(payoutDaysRow); lifecyclePolicyControls.Children.Add(payoutAmountRow); lifecyclePolicyControls.Children.Add(payoutProfitShareRow); lifecyclePolicyControls.Children.Add(qualifyingDayRow);
            governanceControls.Children.Add(evalStageToggleRow); governanceControls.Children.Add(replacementGateRow); governanceControls.Children.Add(firmCapToggleRow); governanceControls.Children.Add(firmCapPanel); governanceControls.Children.Add(evalOverridePanel);
            startModeHintText = Txt("DEFAULT: evaluation uses the main Step 1 profit / loss values. Turn on EVAL OVERRIDE only to show evaluation-only target, drawdown, daily, and per-trade limits. DIRECT FUNDED hides evaluation inputs.", Gold, 10, FontWeights.Bold);
            var settingsStack = Stack();
            settingsStack.Children.Add(Txt("POOL SETTINGS • SCENARIO CONTROLS", Cyan, 13, FontWeights.Bold));
            settingsStack.Children.Add(startModeHintText);
            settingsStack.Children.Add(Txt("CORE", Cyan, 10, FontWeights.Bold)); settingsStack.Children.Add(coreControls);
            lifecyclePolicySection = Stack(); lifecyclePolicySection.Children.Add(Txt("PAYOUT / LIFECYCLE", Gold, 10, FontWeights.Bold)); lifecyclePolicySection.Children.Add(lifecyclePolicyControls); settingsStack.Children.Add(lifecyclePolicySection);
            governanceSection = Stack(); governanceSection.Children.Add(Txt("OPTIONAL GOVERNANCE", Orchid, 10, FontWeights.Bold)); governanceSection.Children.Add(governanceControls); settingsStack.Children.Add(governanceSection);
            settingsStack.Children.Add(controls);
            // This tab owns its compact vertical scrollbar when an evaluation/payout group is
            // expanded. It prevents controls at the bottom from being clipped while leaving the
            // pool/dashboard panes fixed and their independent account scroll untouched.
            var settingsScroll = new ScrollViewer { Content = settingsStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0) };
            lifecycleControlsPanel = PanelCard(settingsScroll);
            resultViews.Items.Add(new TabItem { Header = "POOL SETTINGS", Background = Blue, Foreground = Text, Content = lifecycleControlsPanel });

            // Keep the operating dashboard in view. The detailed initial-investment and
            // replacement-cash reconciliation remains one click away instead of consuming a
            // third row above the account pool.
            var cashPolicyPanel = new Grid { Margin = new Thickness(4) };
            cashPolicyPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cashPolicyPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cashPolicyPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cashPolicyPanel.Children.Add(Txt("PORTFOLIO CASH • INITIAL INVESTMENT AND REINVESTMENT POLICY", Cyan, 13, FontWeights.Bold));
            var cashPolicyHint = Txt("These cards reconcile the evaluation money modeled at the start of the selected range and the optional payout-funded replacement policy. They are scenario cash accounting, not trading P/L or broker-account cash.", Gold, 10, FontWeights.Bold); cashPolicyHint.TextWrapping = TextWrapping.Wrap; Grid.SetRow(cashPolicyHint, 1); cashPolicyPanel.Children.Add(cashPolicyHint);
            poolCashPolicyMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(2, 3, 2, 2) };
            poolNetCashMetric = MetricTile(poolCashPolicyMetrics, "CASH AFTER SHARE • PRE-COST", "$0", "gross withdrawals after selected account share, before evaluation/replacement costs", Green, 88, 21);
            poolEvaluationCostMetric = MetricTile(poolCashPolicyMetrics, "TOTAL EVAL / REPLACEMENT COST", "$0", "all modeled initial and replacement evaluation purchases", Orchid, 88, 21);
            poolInitialInvestmentMetric = MetricTile(poolCashPolicyMetrics, "INITIAL EVAL INVESTMENT", "$0", "initial selected evaluations only; excludes replacement purchases", Gold, 88, 21);
            poolPayoutAfterInitialMetric = MetricTile(poolCashPolicyMetrics, "PAYOUT CASH AFTER INITIAL INVESTMENT", "$0", "shown only as an initial-cost reconciliation when BENCH UNTIL PAYOUT is on", Cyan, 88, 21);
            poolCapitalAvailabilityMetric = MetricTile(poolCashPolicyMetrics, "REINVESTMENT CASH AVAILABLE", "$0", "shown only when BENCH UNTIL PAYOUT is on; it is not broker cash", Orchid, 88, 21);
            poolFirmCapMetric = MetricTile(poolCashPolicyMetrics, "FIRM FUNDED CAP", "OFF", "optional modeled firm grouping and funded capacity", Gold, 88, 21);
            poolFundedMetric = MetricTile(poolCashPolicyMetrics, "CURRENTLY FUNDED", "0", "active funded virtual accounts", Green, 88, 21);
            poolReplacementMetric = MetricTile(poolCashPolicyMetrics, "EVAL AVAILABLE / ACTIVE", "0 / 0", "open evaluation-stage slots / those with current-run assignments", Orchid, 88, 21);
            Grid.SetRow(poolCashPolicyMetrics, 2); cashPolicyPanel.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = poolCashPolicyMetrics });
            portfolioCashResultsTab = new TabItem { Header = "PORTFOLIO CASH", Background = Gold, Foreground = Bg, Content = PanelCard(cashPolicyPanel) };
            resultViews.Items.Add(portfolioCashResultsTab);

            var dashboard = new Grid { MinHeight = 0, Margin = new Thickness(2) }; dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.92, GridUnitType.Star) }); dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.12, GridUnitType.Star) }); dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.85, GridUnitType.Star) });
            var summaryPanel = new Grid(); summaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); summaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); summaryPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            summaryPanel.Children.Add(Txt("FINAL RANGE SUMMARY", Cyan, 13, FontWeights.Bold));
            rangeLifecycleMetrics = new UniformGrid { Columns = 2, Margin = new Thickness(3, 0, 3, 3) };
            rangeEligibleMetric = MetricTile(rangeLifecycleMetrics, "ELIGIBLE EVENTS", "0", "detector-qualified before allocation", Cyan, 68, 18);
            rangeAssignedMetric = MetricTile(rangeLifecycleMetrics, "ASSIGNED / COPIED LEGS", "0", "BH assigns; Asian copies each resolved leg", Green, 68, 18);
            rangePnlMetric = MetricTile(rangeLifecycleMetrics, "RAW EVENT / CYCLE P/L", "$0", "BH assigned outcomes; Asian one-account copy-cycle P/L", Gold, 68, 18);
            rangePurchasesMetric = MetricTile(rangeLifecycleMetrics, "EVALUATIONS BOUGHT", "0", "initial and replacement evaluations", Orchid, 68, 18);
            Grid.SetRow(rangeLifecycleMetrics, 1); summaryPanel.Children.Add(rangeLifecycleMetrics);
            oneDayRangeSummaryText = Txt("ONE-DAY SCORECARD: run the virtual pool to see eligible setups, assigned trades, daily locks, skipped setups, and assigned P/L.", Cyan, 12, FontWeights.Bold);
            oneDayRangeSummaryText.FontFamily = new FontFamily("Consolas"); oneDayRangeSummaryText.TextWrapping = TextWrapping.Wrap;
            oneDayRangeSummaryCard = new Border { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(5), Padding = new Thickness(9), Margin = new Thickness(3), Visibility = Visibility.Collapsed, Child = oneDayRangeSummaryText };
            Grid.SetRow(oneDayRangeSummaryCard, 1); summaryPanel.Children.Add(oneDayRangeSummaryCard);
            mathText = Txt("SELECT A CARD FOR THE FULL COLORED ACCOUNT DETAIL. PORTFOLIO CASH HOLDS COST AND INITIAL-INVESTMENT RECONCILIATION.", Cyan, 10, FontWeights.Bold); mathText.FontFamily = new FontFamily("Consolas"); mathText.TextWrapping = TextWrapping.Wrap;
            var summaryScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = mathText, Margin = new Thickness(4) }; Grid.SetRow(summaryScroll, 2); summaryPanel.Children.Add(summaryScroll); dashboard.Children.Add(PanelCard(summaryPanel));

            var accountsPanel = new Grid(); accountsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); accountsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); accountsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); accountsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var accountHeader = new Grid(); accountHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); accountHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            poolAccountsHeadingText = Txt("VIRTUAL ACCOUNTS • SCROLL CARDS / CLICK A CARD", Orchid, 13, FontWeights.Bold); accountHeader.Children.Add(poolAccountsHeadingText);
            var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(2) };
            poolStateFilterBox = Select("ALL", "FUNDED", "PAYOUT HISTORY", "EVALUATION", "FIRM CAP WAIT", "REPLACEMENT NEXT", "BENCHED • PAYOUT GATE", "BLOWN / ENDED"); poolStateFilterBox.SelectedIndex = 0; poolStateFilterBox.Width = 145; poolStateFilterBox.Height = 27; poolStateFilterBox.ToolTip = "Filter visible account cards; it never changes the pool calculation."; poolStateFilterBox.SelectionChanged += delegate { RenderPoolLedger(); };
            navigation.Children.Add(poolStateFilterBox); Grid.SetColumn(navigation, 1); accountHeader.Children.Add(navigation); accountsPanel.Children.Add(accountHeader);
            oneDayAccountFilters = new WrapPanel { Margin = new Thickness(4, 0, 4, 3), Visibility = Visibility.Collapsed };
            oneDayShowTradedBox = new CheckBox { Content = "TRADED / UNLOCKED", IsChecked = true, Foreground = Blue, Margin = new Thickness(4) };
            oneDayShowProfitLocksBox = new CheckBox { Content = "PROFIT LOCK", IsChecked = true, Foreground = Green, Margin = new Thickness(4) };
            oneDayShowLossLocksBox = new CheckBox { Content = "LOSS LOCK", IsChecked = true, Foreground = Red, Margin = new Thickness(4) };
            oneDayShowUnusedBox = new CheckBox { Content = "UNUSED", IsChecked = true, Foreground = Muted, Margin = new Thickness(4) };
            oneDayShowTradedBox.Checked += delegate { RenderPoolLedger(); }; oneDayShowTradedBox.Unchecked += delegate { RenderPoolLedger(); };
            oneDayShowProfitLocksBox.Checked += delegate { RenderPoolLedger(); }; oneDayShowProfitLocksBox.Unchecked += delegate { RenderPoolLedger(); };
            oneDayShowLossLocksBox.Checked += delegate { RenderPoolLedger(); }; oneDayShowLossLocksBox.Unchecked += delegate { RenderPoolLedger(); };
            oneDayShowUnusedBox.Checked += delegate { RenderPoolLedger(); }; oneDayShowUnusedBox.Unchecked += delegate { RenderPoolLedger(); };
            oneDayAccountFilters.Children.Add(Txt("ONE-DAY CARD FILTERS:", Gold, 10, FontWeights.Bold));
            oneDayAccountFilters.Children.Add(oneDayShowTradedBox); oneDayAccountFilters.Children.Add(oneDayShowProfitLocksBox); oneDayAccountFilters.Children.Add(oneDayShowLossLocksBox); oneDayAccountFilters.Children.Add(oneDayShowUnusedBox);
            Grid.SetRow(oneDayAccountFilters, 1); accountsPanel.Children.Add(oneDayAccountFilters);
            // The hidden selector retains a stable account index for the detail panel; visible
            // account rows are large, bordered buttons inside a real ScrollViewer.
            poolAccountList = new ListBox { Visibility = Visibility.Collapsed };
            poolAccountList.SelectionChanged += delegate { UpdatePoolDetail(); };
            poolAccountCardStack = new StackPanel { Margin = new Thickness(2) };
            poolAccountScroll = new ScrollViewer { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Content = poolAccountCardStack, Margin = new Thickness(4), MinHeight = 0 };
            MouseWheelEventHandler scrollPoolCards = delegate(object sender, MouseWheelEventArgs args)
            {
                if (poolAccountScroll == null) return;
                // A card is a Button, and on some NinjaTrader/WPF builds it consumes the wheel
                // before the ScrollViewer's normal handler runs.  Translate the event directly
                // to this list and mark it handled so the outer results window remains fixed.
                poolAccountScroll.ScrollToVerticalOffset(Math.Max(0, poolAccountScroll.VerticalOffset - args.Delta / 1.5));
                args.Handled = true;
            };
            // The outer results page can mark PreviewMouseWheel handled before a nested
            // ScrollViewer receives it on some NinjaTrader/WPF builds. Registering with
            // handledEventsToo keeps wheel movement attached to the account list itself.
            poolAccountScroll.AddHandler(Mouse.PreviewMouseWheelEvent, scrollPoolCards, true);
            poolAccountCardStack.AddHandler(Mouse.PreviewMouseWheelEvent, scrollPoolCards, true);
            Grid.SetRow(poolAccountScroll, 2); accountsPanel.Children.Add(poolAccountScroll);
            poolText = Txt("POOL-ONLY SCROLL: point at a card and use the mouse wheel; only this center list moves. COLOR KEY • BLUE = traded / open • GREEN = funded or profit lock • RED = loss lock or terminal blown • PURPLE = evaluation • GOLD = payout history • ORANGE = replacement next session.", Gold, 10, FontWeights.Bold); poolText.TextWrapping = TextWrapping.Wrap; Grid.SetRow(poolText, 3); accountsPanel.Children.Add(poolText);
            poolAccountsCard = PanelCard(accountsPanel); Grid.SetColumn(poolAccountsCard, 1); dashboard.Children.Add(poolAccountsCard);

            var detailPanel = new Grid(); detailPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); detailPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); detailPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            poolDetailHeadingText = Txt("SELECTED ACCOUNT • ASSIGNED EVENTS • LIFECYCLE", Green, 13, FontWeights.Bold); detailPanel.Children.Add(poolDetailHeadingText);
            accountLifecycleMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(2, 0, 2, 2) };
            accountBalanceMetric = MetricTile(accountLifecycleMetrics, "CURRENT / FINAL BALANCE", "$0", "selected account stage balance", Cyan, 53, 16);
            accountNetCashAfterCostMetric = MetricTile(accountLifecycleMetrics, "FULL NET CASH AFTER COSTS", "$0", "payout cash after share less all evaluation/replacement costs", Green, 53, 16);
            accountPayoutCyclesMetric = MetricTile(accountLifecycleMetrics, "PAYOUT CYCLES", "0", "completed historical withdrawal cycles", Gold, 53, 16);
            accountBlowoutMetric = MetricTile(accountLifecycleMetrics, "BLOWOUT EVENTS", "0", "evaluation plus funded drawdown failures", Red, 53, 16);
            accountTradesMetric = MetricTile(accountLifecycleMetrics, "TRADES / W-L", "0", "assigned completed trade record", Cyan, 53, 16);
            accountFirstPayoutMetric = MetricTile(accountLifecycleMetrics, "FIRST PAYOUT DATE", "—", "first dated modeled payout in this slot", Gold, 53, 14);
            accountFirstPayoutDaysMetric = MetricTile(accountLifecycleMetrics, "TIME TO FIRST PAYOUT", "—", "calendar days / recorded sessions from initial slot start", Gold, 53, 14);
            accountLifecycleMetric = MetricTile(accountLifecycleMetrics, "LIFECYCLE / BLOWOUT DATE", "—", "initial start through last assignment or terminal blowout", Orchid, 53, 13);
            Grid.SetRow(accountLifecycleMetrics, 1); detailPanel.Children.Add(accountLifecycleMetrics);
            oneDayAccountMetrics = new UniformGrid { Columns = 2, Margin = new Thickness(2, 0, 2, 2), Visibility = Visibility.Collapsed };
            oneDayAccountBalanceMetric = MetricTile(oneDayAccountMetrics, "ENDING ACCOUNT P/L", "$0", "final assigned one-day model P/L; no lifecycle balance is modeled", Cyan, 82, 21);
            oneDayAccountPnlMetric = MetricTile(oneDayAccountMetrics, "DAY P/L", "$0", "sum of this account’s assigned outcomes for the selected session", Gold, 82, 21);
            oneDayAccountTradesMetric = MetricTile(oneDayAccountMetrics, "TRADES / W-L", "0", "each account stops receiving trades after its selected daily lock", Blue, 82, 21);
            oneDayAccountStateMetric = MetricTile(oneDayAccountMetrics, "DAY STATUS", "UNUSED", "blue = traded/unlocked; green/red = selected daily lock", Muted, 82, 18);
            oneDayAccountWindowMetric = MetricTile(oneDayAccountMetrics, "ASSIGNMENT WINDOW", "—", "first to last assigned trade during the selected session", Cyan, 82, 18);
            oneDayAccountLimitsMetric = MetricTile(oneDayAccountMetrics, "DAILY LIMITS", "—", "profit and loss locks used by this one-day study", Gold, 82, 18);
            Grid.SetRow(oneDayAccountMetrics, 1); detailPanel.Children.Add(oneDayAccountMetrics);
            poolDetailText = Txt("Run the selected pool. Then select an account in the center list for its current balance, payout cash, costs, and concise trade audit. Open LIFECYCLE WALKTHROUGH for the full dated history.", Text, 10, FontWeights.Normal); poolDetailText.FontFamily = new FontFamily("Consolas"); poolDetailText.TextWrapping = TextWrapping.Wrap;
            var detailScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = poolDetailText, Margin = new Thickness(4) }; Grid.SetRow(detailScroll, 2); detailPanel.Children.Add(detailScroll);
            poolDetailCard = PanelCard(detailPanel); Grid.SetColumn(poolDetailCard, 2); dashboard.Children.Add(poolDetailCard);
            body.Children.Add(dashboard);
            resultViews.Items.Add(new TabItem { Header = "POOL DASHBOARD", Background = Cyan, Foreground = Bg, Content = body });
            var walkthroughPanel = new Grid { Margin = new Thickness(4) };
            walkthroughPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            walkthroughPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            walkthroughPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            walkthroughPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var walkthroughHeader = new Grid(); walkthroughHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); walkthroughHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            walkthroughHeadingText = Txt("LIFECYCLE WALKTHROUGH • SELECT AN ACCOUNT", Gold, 14, FontWeights.Bold); walkthroughHeader.Children.Add(walkthroughHeadingText);
            walkthroughAccountBox = Select("SELECT ACCOUNT"); walkthroughAccountBox.Width = 190; walkthroughAccountBox.SelectionChanged += delegate { if (!walkthroughSelectionUpdating && walkthroughAccountBox.SelectedIndex >= 0 && poolAccountList != null) { poolAccountList.SelectedIndex = walkthroughAccountBox.SelectedIndex; UpdatePoolDetail(); } }; Grid.SetColumn(walkthroughAccountBox, 1); walkthroughHeader.Children.Add(walkthroughAccountBox); walkthroughPanel.Children.Add(walkthroughHeader);
            walkthroughSummaryText = Txt("Choose a pool card or account name. This view shows the selected slot from initial evaluation through pass, payout, replacement, firm-cap wait, or terminal blowout.", Cyan, 11, FontWeights.Bold); walkthroughSummaryText.TextWrapping = TextWrapping.Wrap; walkthroughSummaryText.Margin = new Thickness(2, 3, 2, 3); Grid.SetRow(walkthroughSummaryText, 1); walkthroughPanel.Children.Add(walkthroughSummaryText);
            var walkthroughLegend = Txt("COLOR KEY • PURPLE evaluation / purchase • GREEN pass or funded • GOLD payout • ORANGE waiting / replacement • RED blowout • CYAN active audit. This is modeled historical scenario tracking only.", Gold, 10, FontWeights.Bold); walkthroughLegend.TextWrapping = TextWrapping.Wrap; Grid.SetRow(walkthroughLegend, 2); walkthroughPanel.Children.Add(walkthroughLegend);
            walkthroughTimelineStack = new StackPanel { Margin = new Thickness(5) };
            // Reuse the proven timeline renderer, but place its output in this full-size dedicated tab.
            poolTimelineStack = walkthroughTimelineStack;
            var walkthroughScroll = new ScrollViewer { Background = Card, BorderBrush = Gold, BorderThickness = new Thickness(1.5), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = true, Content = walkthroughTimelineStack, Margin = new Thickness(2) };
            walkthroughScroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs args) { walkthroughScroll.ScrollToVerticalOffset(Math.Max(0, walkthroughScroll.VerticalOffset - args.Delta / 3.0)); args.Handled = true; };
            Grid.SetRow(walkthroughScroll, 3); walkthroughPanel.Children.Add(walkthroughScroll);
            walkthroughResultsTab = new TabItem { Header = "LIFECYCLE WALKTHROUGH", Background = Gold, Foreground = Bg, Content = PanelCard(walkthroughPanel) };
            resultViews.Items.Add(walkthroughResultsTab);
            var firstReturnPanel = new Grid(); firstReturnPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); firstReturnPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); firstReturnPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            firstReturnDashboardText = Txt("FIRST RETURN • PAID ACCOUNTS ONLY", Gold, 13, FontWeights.Bold); firstReturnPanel.Children.Add(firstReturnDashboardText);
            var firstReturnHint = Txt("Only accounts that actually reached a first payout appear here. Select one to inspect start date, first payout, cost through return, full net, current balance, payout count, and daily/weekly/monthly facts. Unpaid accounts remain in Pool Dashboard and Lifecycle Walkthrough.", Cyan, 10, FontWeights.Bold); firstReturnHint.TextWrapping = TextWrapping.Wrap; Grid.SetRow(firstReturnHint, 1); firstReturnPanel.Children.Add(firstReturnHint);
            firstReturnDashboardStack = new StackPanel { Margin = new Thickness(2) };
            var firstReturnScroll = new ScrollViewer { Background = Card, BorderBrush = Gold, BorderThickness = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Content = firstReturnDashboardStack, Margin = new Thickness(3) }; Grid.SetRow(firstReturnScroll, 2); firstReturnPanel.Children.Add(firstReturnScroll);
            firstReturnCard = PanelCard(firstReturnPanel);
            firstReturnResultsTab = new TabItem { Header = "FIRST RETURN", Background = Gold, Foreground = Bg, Content = firstReturnCard };
            resultViews.Items.Add(firstReturnResultsTab);
            var scorePanel = new Grid(); scorePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); scorePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); scorePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var scoreHeader = new Grid(); scoreHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); scoreHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dailySessionScoreboardText = Txt("PERFORMANCE PERIODS • DAILY / WEEKLY / MONTHLY", Cyan, 13, FontWeights.Bold); scoreHeader.Children.Add(dailySessionScoreboardText);
            periodGranularityBox = Select("DAILY", "WEEKLY", "MONTHLY"); periodGranularityBox.SelectedIndex = 0; periodGranularityBox.Width = 125; periodGranularityBox.SelectionChanged += delegate { RenderDailySessionScoreboard(); }; Grid.SetColumn(periodGranularityBox, 1); scoreHeader.Children.Add(periodGranularityBox); scorePanel.Children.Add(scoreHeader);
            var scoreHint = Txt("Each colored card is an existing selected-session period. It shows accounts assigned, accounts at the selected daily profit / loss lock, payout accounts, P/L, and evaluation cost. Green marks the best positive period in the current view. It never invents an unrequested session or timeframe series.", Gold, 10, FontWeights.Bold); scoreHint.TextWrapping = TextWrapping.Wrap; Grid.SetRow(scoreHint, 1); scorePanel.Children.Add(scoreHint);
            dailySessionScoreboardStack = new StackPanel { Margin = new Thickness(2) };
            var scoreScroll = new ScrollViewer { Background = Card, BorderBrush = Green, BorderThickness = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = dailySessionScoreboardStack, Margin = new Thickness(3) }; Grid.SetRow(scoreScroll, 2); scorePanel.Children.Add(scoreScroll);
            var scoreCard = PanelCard(scorePanel);
            dailySessionResultsTab = new TabItem { Header = "PERFORMANCE PERIODS", Background = Green, Foreground = Bg, Content = scoreCard };
            resultViews.Items.Add(dailySessionResultsTab);
            var findingsPanel = new Grid { Margin = new Thickness(4) }; findingsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); findingsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); findingsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            findingsPanel.Children.Add(Txt("RESEARCH FINDINGS • EVIDENCE FROM THIS LOADED RUN", Orchid, 13, FontWeights.Bold));
            var findingsHint = Txt("These are transparent, deterministic comparisons from the current selected session, instrument scope, timeframe, target, stop, and date range. They are hypotheses for a separate validation run—not promises, future predictions, or a hidden re-run under different settings.", Gold, 10, FontWeights.Bold); findingsHint.TextWrapping = TextWrapping.Wrap; Grid.SetRow(findingsHint, 1); findingsPanel.Children.Add(findingsHint);
            researchFindingsStack = new StackPanel { Margin = new Thickness(2) };
            var findingsScroll = new ScrollViewer { Background = Card, BorderBrush = Orchid, BorderThickness = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = researchFindingsStack, Margin = new Thickness(3) }; Grid.SetRow(findingsScroll, 2); findingsPanel.Children.Add(findingsScroll);
            researchFindingsResultsTab = new TabItem { Header = "RESEARCH FINDINGS", Background = Orchid, Foreground = Text, Content = PanelCard(findingsPanel) };
            resultViews.Items.Add(researchFindingsResultsTab);
            var cyclePanel = new Grid(); cyclePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); cyclePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); cyclePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var cycleHeader = new Grid(); cycleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); cycleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cycleHeader.Children.Add(Txt("PAYOUT CYCLE DASHBOARD • DATED, SIMULTANEOUS PAYOUT EVENTS", Cyan, 13, FontWeights.Bold));
            payoutCycleMonthFilterBox = Select("ALL MONTHS"); payoutCycleMonthFilterBox.Width = 130; payoutCycleMonthFilterBox.SelectionChanged += delegate { if (!payoutCycleFilterUpdating) RenderPayoutCycleDashboard(); }; Grid.SetColumn(payoutCycleMonthFilterBox, 1); cycleHeader.Children.Add(payoutCycleMonthFilterBox); cyclePanel.Children.Add(cycleHeader);
            var cycleHint = Txt("Every row is one payout date. COST THROUGH PAYOUT DATE includes every initial evaluation (including the first $120 per slot) plus replacement purchases recorded up to that date; it is modeled scenario cost, not a trading loss. Month filter changes display only, never pool math.", Gold, 10, FontWeights.Bold); cycleHint.TextWrapping = TextWrapping.Wrap; Grid.SetRow(cycleHint, 1); cyclePanel.Children.Add(cycleHint);
            payoutCycleDashboardStack = new StackPanel { Margin = new Thickness(2) };
            payoutCycleDashboardStack.Children.Add(Txt("RUN THE VIRTUAL POOL TO BUILD DATED PAYOUT CYCLES.", Muted, 11, FontWeights.Bold));
            var cycleBody = new Grid { Margin = new Thickness(0, 2, 0, 0) }; cycleBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) }); cycleBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            var cycleScroll = new ScrollViewer { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = payoutCycleDashboardStack, Margin = new Thickness(3) }; cycleBody.Children.Add(cycleScroll);
            var payoutAccountPanel = new Grid { Margin = new Thickness(3) }; payoutAccountPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); payoutAccountPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.42, GridUnitType.Star) }); payoutAccountPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.58, GridUnitType.Star) });
            payoutAccountPanel.Children.Add(Txt("PAYOUT ACCOUNTS • SELECT ONE", Gold, 11, FontWeights.Bold));
            payoutAccountList = new ListBox { Background = Card, Foreground = Text, BorderBrush = Gold, BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 3, 0, 3), MinHeight = 100 }; ScrollViewer.SetVerticalScrollBarVisibility(payoutAccountList, ScrollBarVisibility.Auto); payoutAccountList.SelectionChanged += delegate { if (!payoutAccountUpdating) RenderPayoutAccountDetail(); }; Grid.SetRow(payoutAccountList, 1); payoutAccountPanel.Children.Add(payoutAccountList);
            payoutAccountDetailText = Txt("Only accounts with payout history are listed here.", Text, 10, FontWeights.Bold); payoutAccountDetailText.TextWrapping = TextWrapping.Wrap;
            var payoutAccountScroll = new ScrollViewer { Background = Panel, BorderBrush = Gold, BorderThickness = new Thickness(1.5), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = payoutAccountDetailText, Margin = new Thickness(0, 2, 0, 0) }; Grid.SetRow(payoutAccountScroll, 2); payoutAccountPanel.Children.Add(payoutAccountScroll);
            var payoutAccountCard = PanelCard(payoutAccountPanel); Grid.SetColumn(payoutAccountCard, 1); cycleBody.Children.Add(payoutAccountCard);
            Grid.SetRow(cycleBody, 2); cyclePanel.Children.Add(cycleBody);
            payoutCycleCard = PanelCard(cyclePanel);
            payoutCycleResultsTab = new TabItem { Header = "PAYOUT CYCLES", Background = Orchid, Foreground = Text, Content = payoutCycleCard };
            resultViews.Items.Add(payoutCycleResultsTab);
            // Pool Settings is always the safe default: select assumptions before inspecting results.
            resultViews.SelectedIndex = 0;
            Border resultsCard = PanelCard(root);
            resultsCard.VerticalAlignment = VerticalAlignment.Stretch;
            resultsCard.HorizontalAlignment = HorizontalAlignment.Stretch;
            return resultsCard;
        }

        private void OpenComparisonWorkbench()
        {
            if (events == null || events.Count == 0 || !HasSelectedData()) { UpdateUi("COMPARE RANGE REQUIRES A COMPLETED DATA LOAD AND DETECTED SETUPS", Gold); return; }
            if (comparisonWindow != null) { comparisonWindow.Activate(); return; }
            var w = new Window { Title = "KEYSTONE ARC • RANGE COMPARISON", Width = 1380, Height = 820, MinWidth = 960, MinHeight = 620, Background = Bg, Foreground = Text, ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true };
            var root = new Grid { Margin = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = Stack();
            header.Children.Add(Txt("PROP VIRTUAL-POOL • TIMEFRAME COMPARISON", Cyan, 18, FontWeights.Bold));
            header.Children.Add(Txt("Each 1M, 5M, 15M, 30M, 60M, and 240M setup series is loaded directly from NinjaTrader. One-minute bars are used only to verify intrabar entry/stop/target outcomes. Results keep raw outcomes, virtual-account assignment, and illustrative evaluation/funded lifecycle scenarios separate.", Muted, 11, FontWeights.Normal));
            header.Children.Add(new Border { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 5, 0, 4), Child = Txt(BuildStudyScopeLabel(), Text, 12, FontWeights.Bold) });
            comparisonStatusText = Txt("STEP 1 • click BUILD COMPARISON. Then read the raw timeframe table first: setup count, wins, losses, and model P/L for each MNQ/MGC timeframe.", Gold, 11, FontWeights.Bold); header.Children.Add(comparisonStatusText);
            var actions = new UniformGrid { Columns = 4, Margin = new Thickness(0, 6, 0, 3) };
            comparisonBuildButton = Btn("BUILD 1M–4H COMPARISON", Green); comparisonBuildButton.Click += delegate { StartComparisonRequests(); };
            comparisonOptimizeButton = Btn("TEST TARGET / STOP GRID", Blue); comparisonOptimizeButton.Click += delegate { StartTargetStopOptimization(); };
            comparisonExportButton = Btn("EXPORT FULL PACKAGE", Orchid); comparisonExportButton.Click += delegate { ExportEvidencePackage(); };
            comparisonClearButton = Btn("CLEAR COMPARISON", Red); comparisonClearButton.Click += delegate { comparisonRows.Clear(); optimizationRows.Clear(); comparisonSetupCache.Clear(); comparisonSeriesStatus.Clear(); RefreshComparisonView(); SetComparisonBusy(false, null); if (comparisonStatusText != null) comparisonStatusText.Text = "CLEARED • current detected run and its charts remain unchanged."; };
            actions.Children.Add(comparisonBuildButton); actions.Children.Add(comparisonOptimizeButton); actions.Children.Add(comparisonExportButton); actions.Children.Add(comparisonClearButton); header.Children.Add(actions);
            Grid.SetRow(header, 0); root.Children.Add(header);

            var filters = new UniformGrid { Columns = 5, Margin = new Thickness(0, 4, 0, 6) };
            comparisonInstrumentBox = Select("ALL", "MNQ", "MGC", "BOTH"); comparisonInstrumentBox.SelectedIndex = 0;
            comparisonTimeframeBox = Select("ALL", "1M", "5M", "15M", "30M", "60M", "240M"); comparisonTimeframeBox.SelectedIndex = 0;
            comparisonSessionBox = Select("CURRENT STUDY SESSION", "FULL GLOBEX", "NY EARLY 08:00-15:55", "NY OPEN 09:30-15:55"); comparisonSessionBox.SelectedIndex = 0;
            comparisonPoolBox = Select("ALL", "1", "5", "10", "20", "40"); comparisonPoolBox.SelectedIndex = 0;
            comparisonModeBox = Select("ALL", "SINGLE P/L", "EVALUATION FIRST", "DIRECT FUNDED"); comparisonModeBox.SelectedIndex = 0;
            comparisonInstrumentBox.SelectionChanged += delegate { RefreshComparisonView(); };
            comparisonTimeframeBox.SelectionChanged += delegate { RefreshComparisonView(); };
            comparisonPoolBox.SelectionChanged += delegate { RefreshComparisonView(); };
            comparisonModeBox.SelectionChanged += delegate { RefreshComparisonView(); };
            comparisonSessionBox.SelectionChanged += delegate { if (comparisonStatusText != null && comparisonSessionBox.SelectedIndex > 0) comparisonStatusText.Text = "SESSION VIEW FILTERS REQUIRE A STUDY LOADED WITH THAT SESSION'S DIRECT BARS. Run Step 1 with the chosen session, then BUILD COMPARISON for a like-for-like table."; RefreshComparisonView(); };
            UIElement comparisonPoolRow = Row("VIRTUAL ACCOUNTS", comparisonPoolBox);
            UIElement comparisonModeRow = Row("START MODE", comparisonModeBox);
            comparisonPoolRow.Visibility = Visibility.Visible;
            comparisonModeRow.Visibility = Visibility.Visible;
            filters.Children.Add(Row("INSTRUMENT", comparisonInstrumentBox)); filters.Children.Add(Row("TIMEFRAME", comparisonTimeframeBox)); filters.Children.Add(Row("SESSION DATA", comparisonSessionBox)); filters.Children.Add(comparisonPoolRow); filters.Children.Add(comparisonModeRow);
            Grid.SetRow(filters, 1); root.Children.Add(filters);

            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            var left = new Grid(); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            comparisonSummaryText = Txt("No comparison rows yet. The current detected timeframe remains available in the normal results screen.", Gold, 11, FontWeights.Bold); left.Children.Add(comparisonSummaryText);
            comparisonMatrixText = Txt("RAW TIMEFRAME TABLE • all outcomes before virtual accounts\n\nNo verified timeframe rows yet.", Text, 10, FontWeights.Normal); comparisonMatrixText.FontFamily = new FontFamily("Consolas"); comparisonMatrixText.TextWrapping = TextWrapping.NoWrap;
            var matrixScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = comparisonMatrixText, Margin = new Thickness(2, 5, 2, 2), MaxHeight = 190 };
            Grid.SetRow(matrixScroll, 1); left.Children.Add(PanelCard(matrixScroll));
            comparisonList = new ListBox { Background = Card, Foreground = Text, BorderBrush = Cyan, BorderThickness = new Thickness(1), Margin = new Thickness(2, 5, 2, 2) }; comparisonList.FontFamily = new FontFamily("Consolas"); ScrollViewer.SetVerticalScrollBarVisibility(comparisonList, ScrollBarVisibility.Visible); Grid.SetRow(comparisonList, 2); left.Children.Add(comparisonList);
            var detail = Txt("Select a comparison row to see its setup count, raw outcome P/L, virtual assignment result, and illustrative lifecycle totals.", Text, 11, FontWeights.Normal); detail.FontFamily = new FontFamily("Consolas");
            comparisonList.SelectionChanged += delegate { KeystoneArcComparisonRow row = comparisonList.SelectedItem as KeystoneArcComparisonRow; detail.Text = BuildComparisonDetail(row); };
            body.Children.Add(PanelCard(left));
            // Set the column on the card actually added to the grid. The old code set it on the
            // inner TextBlock, leaving both cards in column zero and hiding the raw timeframe
            // table and selectable scenario rows behind the detail panel.
            var detailCard = PanelCard(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = detail, Margin = new Thickness(4) });
            Grid.SetColumn(detailCard, 1); body.Children.Add(detailCard);
            Grid.SetRow(body, 2); root.Children.Add(body);
            comparisonBusyText = Txt("BUILDING DIRECT TIMEFRAME COMPARISON\n\nNinjaTrader requests are serialized. The table will appear here when validation finishes. Controls are locked until then.", Text, 16, FontWeights.Bold);
            var comparisonBusyCard = new Border { Background = Panel, BorderBrush = Cyan, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(28), Width = 560, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = comparisonBusyText };
            comparisonBusyOverlay = new Border { Background = Bg, Opacity = 0.96, Child = comparisonBusyCard, Visibility = Visibility.Collapsed };
            comparisonBusyOverlay.Name = "ComparisonBusyOverlay";
            Grid.SetRow(comparisonBusyOverlay, 2); root.Children.Add(comparisonBusyOverlay);
            w.Content = root; comparisonWindow = w;
            bool closeConfirmed = false;
            w.Closing += delegate(object sender, CancelEventArgs args) { if (closeConfirmed) return; if (MessageBox.Show("Close Range Comparison? The current research run remains open.", "Close comparison", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) args.Cancel = true; else closeConfirmed = true; };
            w.Closed += delegate { CancelComparisonRequests(); comparisonWindow = null; comparisonList = null; comparisonStatusText = null; comparisonSummaryText = null; comparisonMatrixText = null; comparisonInstrumentBox = null; comparisonTimeframeBox = null; comparisonSessionBox = null; comparisonPoolBox = null; comparisonModeBox = null; comparisonBuildButton = null; comparisonOptimizeButton = null; comparisonExportButton = null; comparisonClearButton = null; comparisonBusyText = null; comparisonBusyOverlay = null; };
            w.Show();
            RefreshComparisonView();
        }

        private void RefreshComparisonView()
        {
            if (comparisonList == null) return;
            string instrument = comparisonInstrumentBox == null ? "ALL" : Convert.ToString(comparisonInstrumentBox.SelectedItem);
            string timeframe = comparisonTimeframeBox == null ? "ALL" : Convert.ToString(comparisonTimeframeBox.SelectedItem);
            string pool = comparisonPoolBox == null ? "ALL" : Convert.ToString(comparisonPoolBox.SelectedItem);
            string mode = comparisonModeBox == null ? "ALL" : Convert.ToString(comparisonModeBox.SelectedItem);
            IEnumerable<KeystoneArcComparisonRow> rows = comparisonRows;
            if (instrument != "ALL") rows = rows.Where(x => x.Symbol == instrument);
            if (timeframe != "ALL") rows = rows.Where(x => x.SetupMinutes.ToString(CultureInfo.InvariantCulture) + "M" == timeframe);
            if (pool != "ALL") rows = rows.Where(x => x.PoolSize.ToString(CultureInfo.InvariantCulture) == pool);
            if (mode != "ALL") rows = rows.Where(x => x.StartMode == mode);
            List<KeystoneArcComparisonRow> shown = rows.OrderByDescending(x => x.AssignedGross).ThenBy(x => x.Symbol).ThenBy(x => x.SetupMinutes).ThenBy(x => x.PoolSize).ToList();
            comparisonList.Items.Clear();
            for (int i = 0; i < shown.Count; i++) comparisonList.Items.Add(shown[i]);
            if (shown.Count > 0) comparisonList.SelectedIndex = 0;
            if (comparisonMatrixText != null) comparisonMatrixText.Text = BuildComparisonMatrix(shown);
            if (comparisonSummaryText != null)
            {
                if (shown.Count == 0) comparisonSummaryText.Text = comparisonRows.Count == 0 ? "NO COMPARISON BUILT YET • click BUILD 1M–4H COMPARISON." : "NO ROWS MATCH THESE FILTERS.";
                else
                {
                    KeystoneArcComparisonRow best = shown[0];
                    comparisonSummaryText.Text = "FILTERED SCENARIOS " + shown.Count + " • BEST ASSIGNED MODEL GROSS (historical sample only, not a recommendation): " + best.Symbol + " • " + best.SetupMinutes + "M • " + best.AssignedGross.ToString("C0") + " • " + best.Assigned + " assigned / " + best.Skipped + " skipped";
                }
            }
        }

        private void StartTargetStopOptimization()
        {
            if (operationBusy || isProcessing) { if (comparisonStatusText != null) comparisonStatusText.Text = "WAIT FOR THE CURRENT OPERATION TO FINISH."; return; }
            if (config == null || config.OutcomeModelEnabled == 0 || !HasSelectedData()) { if (comparisonStatusText != null) comparisonStatusText.Text = "TARGET / STOP GRID REQUIRES THE VERIFIED 1-MINUTE OUTCOME PATH."; return; }
            KeystoneArcRunConfig cfg = CloneConfig(config);
            List<KeystoneArcBar> mnqOutcome = new List<KeystoneArcBar>(mnqBars), mgcOutcome = new List<KeystoneArcBar>(mgcBars), mnqSetups = new List<KeystoneArcBar>(mnqSetupBars), mgcSetups = new List<KeystoneArcBar>(mgcSetupBars);
            SetComparisonBusy(true, "TESTING TARGET / STOP GRID\n\nThis re-resolves the currently loaded setup timeframe under nine risk combinations and compares MNQ-only, MGC-only, and BOTH where available. The saved run is not changed.");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeystoneArcOptimizationRow> rows;
                string failure = string.Empty;
                try { rows = BuildTargetStopOptimizationRows(cfg, mnqOutcome, mgcOutcome, mnqSetups, mgcSetups); }
                catch (Exception ex) { rows = new List<KeystoneArcOptimizationRow>(); failure = ex.Message; }
                DispatchToLab(delegate
                {
                    optimizationRows.Clear(); optimizationRows.AddRange(rows); SetComparisonBusy(false, null);
                    if (comparisonMatrixText != null) comparisonMatrixText.Text = rows.Count == 0 ? "NO TARGET / STOP GRID RESULTS • " + failure : BuildOptimizationMatrix(rows, cfg.AccountPath == "PERSONAL");
                    if (comparisonSummaryText != null) comparisonSummaryText.Text = rows.Count == 0 ? "TARGET / STOP GRID FAILED • " + failure : "IN-SAMPLE GRID READY • " + rows.Count + " SCENARIOS • ranked by " + (cfg.AccountPath == "PERSONAL" ? "final live-account P/L" : "modeled account-share payout cash, then assigned P/L") + ". This is historical sensitivity analysis, not a recommendation.";
                    if (comparisonStatusText != null) comparisonStatusText.Text = rows.Count == 0 ? "TARGET / STOP GRID DID NOT PRODUCE RESULTS." : "TARGET / STOP GRID READY • current saved run and its selected parameters were not changed.";
                });
            });
        }

        private static List<KeystoneArcOptimizationRow> BuildTargetStopOptimizationRows(KeystoneArcRunConfig baseConfig, List<KeystoneArcBar> mnqOutcome, List<KeystoneArcBar> mgcOutcome, List<KeystoneArcBar> mnqSetups, List<KeystoneArcBar> mgcSetups)
        {
            var rows = new List<KeystoneArcOptimizationRow>();
            bool live = string.Equals(baseConfig.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase);
            double[] targets = live
                ? new[] { 0.65, 1.00, 1.35 }
                : new[] { Math.Max(1, baseConfig.TargetDollars * 0.65), baseConfig.TargetDollars, Math.Max(1, baseConfig.TargetDollars * 1.35) }.Select(x => Math.Max(1, Math.Round(x / 50.0) * 50.0)).Distinct().OrderBy(x => x).ToArray();
            bool liveStandardStop = live && string.Equals(baseConfig.StopMode, "STANDARD", StringComparison.OrdinalIgnoreCase);
            bool liveAutoRiskStop = live && string.Equals(baseConfig.StopMode, "LIVE_LOW_AUTO_RISK", StringComparison.OrdinalIgnoreCase);
            double[] stops = live
                ? ((liveStandardStop || liveAutoRiskStop) ? new[] { 0.70, 1.00, 1.30 } : new[] { 1.00 })
                : new[] { Math.Max(1, baseConfig.StopDollars * 0.70), baseConfig.StopDollars, Math.Max(1, baseConfig.StopDollars * 1.30) }.Select(x => Math.Max(1, Math.Round(x / 50.0) * 50.0)).Distinct().OrderBy(x => x).ToArray();
            string[] scopes = baseConfig.Scope == "BOTH" ? new[] { "MNQ", "MGC", "BOTH" } : new[] { baseConfig.Scope };
            DateTime first = KeystoneArcEngine.SessionGroupingDate(baseConfig.Start, baseConfig).Date, last = KeystoneArcEngine.SessionGroupingDate(baseConfig.End, baseConfig).Date;
            int totalDays = Math.Max(1, (int)(last - first).TotalDays + 1);
            foreach (double target in targets)
            {
                foreach (double stop in stops)
                {
                    KeystoneArcRunConfig scenario = CloneConfig(baseConfig);
                    if (live)
                    {
                        scenario.MnqTargetMove = Math.Max(0.0001, baseConfig.MnqTargetMove * target);
                        scenario.MgcTargetMove = Math.Max(0.0001, baseConfig.MgcTargetMove * target);
                        if (liveStandardStop)
                        {
                            scenario.MnqStandardStopMove = Math.Max(0.0001, baseConfig.MnqStandardStopMove * stop);
                            scenario.MgcStandardStopMove = Math.Max(0.0001, baseConfig.MgcStandardStopMove * stop);
                        }
                        if (liveAutoRiskStop) scenario.PersonalMaxRiskDollars = Math.Max(1, baseConfig.PersonalMaxRiskDollars * stop);
                    }
                    else { scenario.TargetDollars = target; scenario.StopDollars = stop; }
                    var bySymbol = new Dictionary<string, List<KeystoneArcEvent>>(StringComparer.OrdinalIgnoreCase);
                    if ((baseConfig.Scope == "MNQ" || baseConfig.Scope == "BOTH") && mnqSetups.Count >= 3) { KeystoneArcRunConfig c = CloneConfig(scenario); c.Scope = "MNQ"; bySymbol["MNQ"] = KeystoneArcEngine.DetectAndResolve(mnqOutcome, mnqSetups, c); }
                    if ((baseConfig.Scope == "MGC" || baseConfig.Scope == "BOTH") && mgcSetups.Count >= 3) { KeystoneArcRunConfig c = CloneConfig(scenario); c.Scope = "MGC"; bySymbol["MGC"] = KeystoneArcEngine.DetectAndResolve(mgcOutcome, mgcSetups, c); }
                    foreach (string scope in scopes)
                    {
                        var detected = new List<KeystoneArcEvent>();
                        if ((scope == "MNQ" || scope == "BOTH") && bySymbol.ContainsKey("MNQ")) detected.AddRange(CloneEvents(bySymbol["MNQ"]));
                        if ((scope == "MGC" || scope == "BOTH") && bySymbol.ContainsKey("MGC")) detected.AddRange(CloneEvents(bySymbol["MGC"]));
                        detected = detected.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
                        for (int i = 0; i < detected.Count; i++) detected[i].ReviewState = "ACCEPTED";
                        KeystoneArcRunConfig allocation = CloneConfig(scenario); allocation.Scope = scope;
                        List<KeystoneArcVirtualAccount> accounts = KeystoneArcEngine.SimulatePool(detected, allocation);
                        List<KeystoneArcEvent> finalRows = detected.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)).OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
                        rows.Add(new KeystoneArcOptimizationRow
                        {
                            Scope = scope, SetupMinutes = baseConfig.SetupMinutes, TargetDollars = live ? scenario.MgcTargetMove : target, StopDollars = live ? (liveAutoRiskStop ? scenario.PersonalMaxRiskDollars : (liveStandardStop ? scenario.MgcStandardStopMove : 0)) : stop,
                            TargetLabel = live ? "NQ +" + scenario.MnqTargetMove.ToString("0.####", CultureInfo.InvariantCulture) + " • GC +" + scenario.MgcTargetMove.ToString("0.####", CultureInfo.InvariantCulture) + " • " + scenario.PersonalLotSize.ToString("0.####", CultureInfo.InvariantCulture) + " lot" : target.ToString("C0", CultureInfo.InvariantCulture),
                            StopLabel = live ? (liveStandardStop ? "STD NQ -" + scenario.MnqStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture) + " • GC -" + scenario.MgcStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture) : (liveAutoRiskStop ? "3-candle low • auto-risk " + scenario.PersonalMaxRiskDollars.ToString("C0", CultureInfo.InvariantCulture) : "3-candle low • fixed lot")) : stop.ToString("C0", CultureInfo.InvariantCulture),
                            Detected = detected.Count, FinalTrades = finalRows.Count,
                            Wins = finalRows.Count(x => x.Outcome == "WIN"), Losses = finalRows.Count(x => x.Outcome.StartsWith("LOSS")), SessionExits = finalRows.Count(x => x.Outcome == "SESSION EXIT"), FinalPnl = finalRows.Sum(x => x.GrossPnl),
                            MaximumDrawdown = MaximumRunningDrawdown(finalRows), PositiveDays = finalRows.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, allocation)).Count(g => g.Sum(x => x.GrossPnl) > 0), TotalDays = totalDays,
                            EvaluationPasses = accounts.Sum(x => x.EvaluationPasses), CurrentlyFunded = accounts.Count(x => x.Funded), Payouts = accounts.Sum(x => x.Payouts), PayoutCash = accounts.Sum(x => x.PayoutCash), EvaluationCost = accounts.Sum(x => x.EvaluationCost)
                        });
                    }
                }
            }
            return rows;
        }

        private static double MaximumRunningDrawdown(IEnumerable<KeystoneArcEvent> source)
        {
            double balance = 0, peak = 0, drawdown = 0;
            foreach (KeystoneArcEvent e in (source ?? Enumerable.Empty<KeystoneArcEvent>()).OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime)) { balance += e.GrossPnl; peak = Math.Max(peak, balance); drawdown = Math.Max(drawdown, peak - balance); }
            return drawdown;
        }

        private static string BuildOptimizationMatrix(List<KeystoneArcOptimizationRow> rows, bool live)
        {
            var sb = new StringBuilder();
            sb.AppendLine(live ? "LIVE ACCOUNT • IN-SAMPLE TARGET / STOP SENSITIVITY" : "PROP VIRTUAL-POOL • IN-SAMPLE TARGET / STOP SENSITIVITY");
            sb.AppendLine("This table ranks only the loaded historical sample. It does not prove an optimal future setting.");
            sb.AppendLine(live ? "SCOPE | TARGET MOVE / LOT | STOP MODEL | DETECTED | FINAL | W/L/X | FINAL P/L | MAX DD | +DAYS/TOTAL" : "SCOPE | TARGET | STOP | FINAL | ASSIGNED P/L | EVAL PASSES | FUNDED | PAYOUTS | ACCOUNT CASH | EVAL COST");
            IEnumerable<KeystoneArcOptimizationRow> ordered = live ? rows.OrderByDescending(x => x.FinalPnl).ThenBy(x => x.MaximumDrawdown) : rows.OrderByDescending(x => x.PayoutCash - x.EvaluationCost).ThenByDescending(x => x.FinalPnl);
            foreach (KeystoneArcOptimizationRow row in ordered)
            {
                if (live) sb.AppendLine(row.Scope.PadRight(5) + " | " + (row.TargetLabel ?? string.Empty).PadRight(31) + " | " + (row.StopLabel ?? string.Empty).PadRight(30) + " | " + row.Detected.ToString().PadLeft(8) + " | " + row.FinalTrades.ToString().PadLeft(5) + " | " + (row.Wins + "/" + row.Losses + "/" + row.SessionExits).PadLeft(7) + " | " + row.FinalPnl.ToString("C0").PadLeft(9) + " | " + row.MaximumDrawdown.ToString("C0").PadLeft(7) + " | " + (row.PositiveDays + "/" + row.TotalDays));
                else sb.AppendLine(row.Scope.PadRight(5) + " | " + row.TargetDollars.ToString("C0").PadLeft(7) + " | " + row.StopDollars.ToString("C0").PadLeft(6) + " | " + row.FinalTrades.ToString().PadLeft(5) + " | " + row.FinalPnl.ToString("C0").PadLeft(12) + " | " + row.EvaluationPasses.ToString().PadLeft(11) + " | " + row.CurrentlyFunded.ToString().PadLeft(6) + " | " + row.Payouts.ToString().PadLeft(7) + " | " + row.PayoutCash.ToString("C0").PadLeft(12) + " | " + row.EvaluationCost.ToString("C0"));
            }
            return sb.ToString();
        }

        private string BuildComparisonMatrix(List<KeystoneArcComparisonRow> shown)
        {
            var sb = new StringBuilder();
            bool liveComparison = config != null && config.AccountPath == "PERSONAL";
            sb.AppendLine((liveComparison ? "FINAL LIVE-ACCOUNT TIMEFRAME RESULTS • ONE EARLIEST RESOLVED SETUP / SESSION DATE • SESSION " : "RAW TIMEFRAME RESULTS • BEFORE VIRTUAL ACCOUNTS • SESSION ") + ((comparisonRunConfig ?? config) == null ? "n/a" : (comparisonRunConfig ?? config).SessionMode));
            sb.AppendLine(liveComparison ? "INSTR |   TF | DETECTED | TRADED | SKIPPED | FINAL LIVE P/L" : "INSTR |   TF | SETUPS |  W |  L | EXIT |  WIN % | MODEL P/L");
            sb.AppendLine("------+------|--------|----|----|------|--------|------------");
            string[] symbols = config != null && config.Scope == "BOTH" ? new[] { "MNQ", "MGC", "BOTH" } : new[] { config == null ? "MNQ" : config.Scope };
            int[] frames = new[] { 1, 5, 15, 30, 60, 240 };
            for (int s = 0; s < symbols.Length; s++)
            {
                for (int f = 0; f < frames.Length; f++)
                {
                    KeystoneArcComparisonRow row = comparisonRows.Where(x => x.Symbol == symbols[s] && x.SetupMinutes == frames[f]).OrderBy(x => x.PoolSize).ThenBy(x => x.StartMode).FirstOrDefault();
                    if (row == null)
                    {
                        string diagnostic;
                        if (!comparisonSeriesStatus.TryGetValue(ComparisonCacheKey(symbols[s], frames[f]), out diagnostic)) diagnostic = "NOT REQUESTED";
                        sb.AppendLine(symbols[s].PadRight(5) + " | " + frames[f].ToString(CultureInfo.InvariantCulture).PadLeft(4) + "M | " + diagnostic);
                        continue;
                    }
                    if (row.StartMode == "SETUP ONLY")
                    {
                        sb.AppendLine(row.Symbol.PadRight(5) + " | " + (row.SetupMinutes + "M").PadLeft(4) + " | " + row.Setups.ToString(CultureInfo.InvariantCulture).PadLeft(6) + " | SETUP ONLY • 1M OUTCOME / P&L BLOCKED");
                        continue;
                    }
                    if (liveComparison)
                    {
                        sb.AppendLine(row.Symbol.PadRight(5) + " | " + (row.SetupMinutes + "M").PadLeft(4) + " | " + row.Setups.ToString(CultureInfo.InvariantCulture).PadLeft(8) + " | " + row.Assigned.ToString(CultureInfo.InvariantCulture).PadLeft(6) + " | " + row.Skipped.ToString(CultureInfo.InvariantCulture).PadLeft(7) + " | " + row.AssignedGross.ToString("C0", CultureInfo.InvariantCulture).PadLeft(14));
                        continue;
                    }
                    int resolved = row.Wins + row.Losses;
                    string winRate = resolved == 0 ? "n/a" : (row.Wins * 100.0 / resolved).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                    sb.AppendLine(row.Symbol.PadRight(5) + " | " + (row.SetupMinutes + "M").PadLeft(4) + " | " + row.Setups.ToString(CultureInfo.InvariantCulture).PadLeft(6) + " | " + row.Wins.ToString(CultureInfo.InvariantCulture).PadLeft(2) + " | " + row.Losses.ToString(CultureInfo.InvariantCulture).PadLeft(2) + " | " + row.SessionExits.ToString(CultureInfo.InvariantCulture).PadLeft(4) + " | " + winRate.PadLeft(6) + " | " + row.AllOutcomeGross.ToString("C0", CultureInfo.InvariantCulture).PadLeft(10));
                }
            }
            sb.Append(config != null && config.AccountPath == "PERSONAL" ? "\nLIVE ACCOUNT • each P/L uses one earliest resolved setup across the selected instrument scope on every session date. All later setups remain raw evidence only." : "\nRows below add virtual-account and illustrative lifecycle scenarios. The table above is the direct setup/outcome comparison for the selected date range.");
            return sb.ToString();
        }

        private string BuildComparisonDetail(KeystoneArcComparisonRow row)
        {
            if (row == null) return "Select a comparison row.";
            int resolved = row.Wins + row.Losses;
            if (row.StartMode == "LIVE ACCOUNT")
                return "LIVE ACCOUNT COMPARISON\n" + row.Symbol + " • " + row.SetupMinutes + " MINUTE SETUPS • ONE ACCOUNT\n\n" +
                    "DATA METHOD\n" + row.DataMethod + "\n\n" +
                    "FINAL LIVE LEDGER\n" +
                    "Detected setups " + row.Setups + " | Final first-per-instrument trades " + row.Assigned + " | Later / unavailable " + row.Skipped + " | Final live P/L " + row.AssignedGross.ToString("C0") + "\n\n" +
                    "RAW AUDIT ONLY\n" +
                    "Detected outcomes: " + row.Wins + " wins | " + row.Losses + " losses | " + row.SessionExits + " session exits | raw gross " + row.AllOutcomeGross.ToString("C0") + ". Later same-instrument outcomes are not added to final live P/L.\n\n" +
                    "NOTE\n" + row.Note;
            return "SCENARIO\n" + row.Symbol + " • " + row.SetupMinutes + " MINUTE SETUPS • " + (row.StartMode == "LIVE ACCOUNT" ? "ONE LIVE ACCOUNT" : row.PoolSize + " VIRTUAL ACCOUNTS • " + row.StartMode) + "\n\n" +
                "DATA METHOD\n" + row.DataMethod + "\n\n" +
                "ALL DETECTED OUTCOMES (before accounts)\n" +
                "Setups " + row.Setups + " | Wins " + row.Wins + " | Losses " + row.Losses + " | Win % " + (resolved == 0 ? "n/a" : (row.Wins * 100.0 / resolved).ToString("0.0") + "%") + " | Session exits " + row.SessionExits + " | Model gross " + row.AllOutcomeGross.ToString("C0") + "\n\n" +
                (row.StartMode == "LIVE ACCOUNT" ? "LIVE ACCOUNT RESULT\nOne historical account ledger; one earliest resolved setup per session date. No prop rotation, evaluation, funded, payout, or evaluation-cost math.\n\n" :
                "VIRTUAL ALLOCATION\n" +
                "Assigned " + row.Assigned + " | Skipped " + row.Skipped + " | Assigned model gross " + row.AssignedGross.ToString("C0") + "\n\n" +
                "ILLUSTRATIVE LIFECYCLE\n" +
                "Evaluation passes completed " + row.EvaluationPassed + " | Currently funded " + row.Funded + " | Payout cycles " + row.Payouts + " | Payout cash " + row.PayoutCash.ToString("C0") + " | Evaluation cost " + row.EvaluationCost.ToString("C0") + "\n\n") +
                "NOTE\n" + row.Note;
        }

        // Comparison intentionally runs only after a normal research load has established the
        // requested range, contracts, session template, target/stop model, and current detector.
        // Each setup timeframe is requested directly from NinjaTrader instead of resampling an
        // unrelated chart.  A comparison row is admitted only when its one-minute bars reproduce
        // its setup timeframe; a same-bar fallback would manufacture rankings and is prohibited.
        private void StartComparisonRequests()
        {
            if (operationBusy || isProcessing) { if (comparisonStatusText != null) comparisonStatusText.Text = "WAIT FOR THE CURRENT OPERATION TO FINISH."; return; }
            if (!HasSelectedData() || config == null) { if (comparisonStatusText != null) comparisonStatusText.Text = "LOAD THE CURRENT RANGE BEFORE BUILDING A COMPARISON."; return; }
            CancelComparisonRequests();
            comparisonRows.Clear(); comparisonSetupCache.Clear(); comparisonSeriesStatus.Clear();
            comparisonRunConfig = CloneConfig(config);
            string sessionChoice = comparisonSessionBox == null ? "CURRENT STUDY SESSION" : Convert.ToString(comparisonSessionBox.SelectedItem);
            if (sessionChoice == "FULL GLOBEX") comparisonRunConfig.SessionMode = "FULL_GLOBEX";
            else if (sessionChoice == "NY EARLY 08:00-15:55") comparisonRunConfig.SessionMode = "NY_EARLY";
            else if (sessionChoice == "NY OPEN 09:30-15:55") comparisonRunConfig.SessionMode = "NY_OPEN";
            if (sessionChoice != "CURRENT STUDY SESSION")
            {
                DateTime first = KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date;
                DateTime last = KeystoneArcEngine.SessionGroupingDate(config.End, config).Date;
                if (KeystoneArcEngine.UsesOvernightSessionDate(config) && !KeystoneArcEngine.UsesOvernightSessionDate(comparisonRunConfig)) { first = first.AddDays(1); last = last.AddDays(1); }
                DateTime selectedStart, selectedEnd; GetConfiguredSessionBounds(first, last, comparisonRunConfig, out selectedStart, out selectedEnd);
                // A comparison view may narrow a loaded study, but it cannot manufacture bars
                // outside the parent Step 1 request.
                if (selectedStart < config.Start || selectedEnd > config.End)
                {
                    if (comparisonStatusText != null) comparisonStatusText.Text = "SESSION FILTER NEEDS BARS OUTSIDE THIS STUDY. Run Step 1 with " + sessionChoice + " first; the current comparison was not changed.";
                    comparisonRunConfig = null;
                    return;
                }
                comparisonRunConfig.Start = selectedStart; comparisonRunConfig.End = selectedEnd;
            }
            long run = ++comparisonGeneration;
            SetComparisonBusy(true, "REQUESTING DIRECT 1M–4H DATA\n\nNinjaTrader requests MNQ/MGC one series at a time. Please wait; comparison controls stay locked until every requested timeframe has returned or been marked unavailable.");
            int[] frames = new[] { 1, 5, 15, 30, 60, 240 };
            string[] symbols = config.Scope == "BOTH" ? new[] { "MNQ", "MGC" } : new[] { config.Scope };
            for (int s = 0; s < symbols.Length; s++)
            {
                string symbol = symbols[s];
                Instrument instrument = symbol == "MNQ" ? configuredMnqInstrument : configuredMgcInstrument;
                if (instrument == null) { if (comparisonStatusText != null) comparisonStatusText.Text = "COMPARISON STOPPED • " + symbol + " HAS NO RESOLVED NINJATRADER INSTRUMENT."; return; }
                for (int f = 0; f < frames.Length; f++)
                {
                    int minutes = frames[f];
                    List<KeystoneArcBar> currentBars = symbol == "MNQ" ? mnqSetupBars : mgcSetupBars;
                    if (minutes == config.SetupMinutes && currentBars != null && currentBars.Count > 0)
                    {
                        comparisonSetupCache[ComparisonCacheKey(symbol, minutes)] = new List<KeystoneArcBar>(currentBars);
                        comparisonSeriesStatus[ComparisonCacheKey(symbol, minutes)] = "READY • current run's verified setup timeframe";
                        continue;
                    }
                    DateTime contextStart = comparisonRunConfig.Start.AddMinutes(-Math.Max(60, minutes * 3));
                    comparisonRequestQueue.Enqueue(new HistoricalRequestWorkItem { Key = symbol, Instrument = instrument, Start = contextStart, End = comparisonRunConfig.End, Minutes = minutes, IsSetupRequest = true, Run = run });
                }
            }
            comparisonPendingRequests = comparisonRequestQueue.Count;
            BeginBusy("REQUESTING DIRECT 1M–4H COMPARISON BARS");
            if (comparisonStatusText != null) comparisonStatusText.Text = "REQUESTING " + comparisonPendingRequests + " DIRECT NINJATRADER TIMEFRAME SERIES • " + comparisonRunConfig.SessionMode + " • MNQ/MGC REQUESTS ARE SERIALIZED.";
            if (comparisonPendingRequests == 0) FinishComparisonBuild(run); else StartNextComparisonRequest();
        }

        private static string ComparisonCacheKey(string symbol, int minutes)
        {
            return (symbol ?? string.Empty).ToUpperInvariant() + "|" + minutes.ToString(CultureInfo.InvariantCulture);
        }

        private void StartNextComparisonRequest()
        {
            if (comparisonRequestActive) return;
            if (comparisonRequestQueue.Count == 0) { FinishComparisonBuild(comparisonGeneration); return; }
            HistoricalRequestWorkItem item = comparisonRequestQueue.Dequeue();
            if (item == null || item.Run != comparisonGeneration) { StartNextComparisonRequest(); return; }
            comparisonRequestActive = true;
            try
            {
                string hoursSource;
                // Every comparison timeframe must use the same NinjaTrader session template as
                // the source run and its 1M outcome series. Resolving a separate template by
                // timeframe is what made comparison bars fail OHLC reconciliation.
                TradingHours hours = ResolveRequestTradingHours(item.Key, config.SetupMinutes, out hoursSource);
                comparisonRequest = new BarsRequest(item.Instrument, item.Start, item.End) { BarsPeriod = new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = item.Minutes }, TradingHours = hours, MergePolicy = MergePolicy.UseGlobalSettings };
                comparisonRequest.Request(delegate(BarsRequest done, ErrorCode error, string message) { CompleteComparisonRequest(item, done, error, message); });
            }
            catch (Exception ex)
            {
                comparisonRequestActive = false; comparisonPendingRequests = Math.Max(0, comparisonPendingRequests - 1);
                comparisonSeriesStatus[ComparisonCacheKey(item.Key, item.Minutes)] = "UNAVAILABLE • request start error: " + ex.Message;
                if (comparisonStatusText != null) comparisonStatusText.Text = "REQUEST ERROR • " + item.Key + " " + item.Minutes + "M • " + ex.Message + " • continuing with available series.";
                StartNextComparisonRequest();
            }
        }

        private void CompleteComparisonRequest(HistoricalRequestWorkItem item, BarsRequest request, ErrorCode error, string message)
        {
            if (item == null || item.Run != comparisonGeneration) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var bars = new List<KeystoneArcBar>();
                string failure = string.Empty;
                try
                {
                    if (error != ErrorCode.NoError || request == null || request.Bars == null) failure = error + " • " + message;
                    else for (int i = 0; i < request.Bars.Count; i++) bars.Add(new KeystoneArcBar { Time = request.Bars.GetTime(i), Symbol = item.Key, Open = request.Bars.GetOpen(i), High = request.Bars.GetHigh(i), Low = request.Bars.GetLow(i), Close = request.Bars.GetClose(i), Volume = request.Bars.GetVolume(i) });
                }
                catch (Exception ex) { failure = "bar conversion error • " + ex.Message; bars.Clear(); }
                DispatchToLab(delegate
                {
                    if (item.Run != comparisonGeneration) return;
                    comparisonRequestActive = false; comparisonPendingRequests = Math.Max(0, comparisonPendingRequests - 1);
                    string key = ComparisonCacheKey(item.Key, item.Minutes);
                    if (bars.Count > 0)
                    {
                        comparisonSetupCache[key] = bars;
                        comparisonSeriesStatus[key] = "READY • " + bars.Count + " direct bars • shared source session template";
                    }
                    else comparisonSeriesStatus[key] = "UNAVAILABLE • " + (string.IsNullOrWhiteSpace(failure) ? "NinjaTrader returned 0 bars" : failure);
                    if (!string.IsNullOrWhiteSpace(failure) && comparisonStatusText != null) comparisonStatusText.Text = "TIMEFRAME UNAVAILABLE • " + item.Key + " " + item.Minutes + "M • " + failure + " • remaining series continue.";
                    if (comparisonRequestQueue.Count == 0) FinishComparisonBuild(item.Run); else StartNextComparisonRequest();
                });
            });
        }

        private void FinishComparisonBuild(long run)
        {
            if (run != comparisonGeneration) return;
            comparisonRequestActive = false; comparisonPendingRequests = 0;
            if (comparisonSetupCache.Count == 0) { EndBusy(); SetComparisonBusy(false, null); if (comparisonStatusText != null) comparisonStatusText.Text = "NO DIRECT TIMEFRAME SERIES WERE AVAILABLE FOR THIS RANGE."; return; }
            if (comparisonStatusText != null) comparisonStatusText.Text = "DIRECT TIMEFRAME BARS LOADED • checking 1M only for exact entry/stop/target outcome safety before P/L or account math.";
            var cacheCopy = new Dictionary<string, List<KeystoneArcBar>>();
            foreach (var pair in comparisonSetupCache) cacheCopy[pair.Key] = new List<KeystoneArcBar>(pair.Value);
            KeystoneArcRunConfig cfgCopy = CloneConfig(comparisonRunConfig ?? config);
            List<KeystoneArcBar> mnqOutcome = new List<KeystoneArcBar>(mnqBars);
            List<KeystoneArcBar> mgcOutcome = new List<KeystoneArcBar>(mgcBars);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeystoneArcComparisonRow> rows;
                try { rows = BuildComparisonRows(cacheCopy, mnqOutcome, mgcOutcome, cfgCopy); }
                catch (Exception ex) { rows = new List<KeystoneArcComparisonRow> { new KeystoneArcComparisonRow { Symbol = "ERROR", Note = ex.Message } }; }
                DispatchToLab(delegate
                {
                    if (run != comparisonGeneration) return;
                    foreach (string key in cacheCopy.Keys)
                    {
                        string[] keyParts = key.Split('|');
                        int minutes;
                        if (keyParts.Length != 2 || !int.TryParse(keyParts[1], out minutes)) continue;
                        bool hasVerifiedRow = rows.Any(x => x.Symbol == keyParts[0] && x.SetupMinutes == minutes && x.StartMode != "SETUP ONLY");
                        if (!hasVerifiedRow) comparisonSeriesStatus[key] = "UNVERIFIED • direct timeframe bars did not reconcile exactly to the verified 1M outcome series";
                    }
                    comparisonRows.Clear(); comparisonRows.AddRange(rows); EndBusy(); SetComparisonBusy(false, null); RefreshComparisonView();
                    int verified = comparisonRows.Count(x => x.StartMode != "SETUP ONLY");
                    int setupOnly = comparisonRows.Count(x => x.StartMode == "SETUP ONLY");
                    if (comparisonStatusText != null) comparisonStatusText.Text = comparisonRows.Count == 0 ? "NO DIRECT TIMEFRAME SERIES RETURNED SETUP BARS." : "COMPARISON READY • " + verified + " VERIFIED P/L SCENARIOS • " + setupOnly + " DIRECT SETUP-ONLY ROWS • filters do not change the saved current run.";
                });
            });
        }

        private static List<KeystoneArcComparisonRow> BuildComparisonRows(Dictionary<string, List<KeystoneArcBar>> cache, List<KeystoneArcBar> mnqOutcome, List<KeystoneArcBar> mgcOutcome, KeystoneArcRunConfig baseConfig)
        {
            var results = new List<KeystoneArcComparisonRow>();
            var verifiedByFrame = new Dictionary<int, Dictionary<string, List<KeystoneArcEvent>>>();
            foreach (var pair in cache.OrderBy(x => x.Key))
            {
                string[] parts = pair.Key.Split('|'); if (parts.Length != 2) continue;
                string symbol = parts[0]; int minutes; if (!int.TryParse(parts[1], out minutes)) continue;
                List<KeystoneArcBar> setupBars = pair.Value == null ? new List<KeystoneArcBar>() : pair.Value.OrderBy(x => x.Time).ToList();
                if (setupBars.Count < 3) continue;
                List<KeystoneArcBar> oneMinute = symbol == "MNQ" ? mnqOutcome : mgcOutcome;
                int offset = 0;
                // A 1M row already is the direct outcome series. Treating it as an aggregate
                // of one-minute components makes its comparison availability depend on a
                // needless second validation step.
                bool validatedOneMinute = minutes == 1
                    ? setupBars.Count > 0
                    : ComparisonOutcomeMatches(oneMinute, setupBars, minutes, baseConfig.Start, baseConfig.End, out offset);
                KeystoneArcRunConfig detectionConfig = CloneConfig(baseConfig);
                if (!validatedOneMinute)
                {
                    // Direct setup bars are still useful evidence even when the requested 1M
                    // series cannot safely resolve intrabar target/stop order. Keep the setup
                    // count visible, but create no P/L, pool, lifecycle, or payout scenario.
                    detectionConfig.Scope = symbol; detectionConfig.SetupMinutes = minutes; detectionConfig.OutcomeModelEnabled = 0;
                    List<KeystoneArcEvent> setupOnly = KeystoneArcEngine.DetectAndResolve(setupBars, setupBars, detectionConfig);
                    results.Add(new KeystoneArcComparisonRow
                    {
                        Symbol = symbol, SetupMinutes = minutes, PoolSize = 0, StartMode = "SETUP ONLY", DataMethod = "direct " + minutes + "M setup bars • 1M outcome unverified",
                        Setups = setupOnly.Count, Wins = 0, Losses = 0, SessionExits = 0, AllOutcomeGross = 0, Assigned = 0, Skipped = 0, AssignedGross = 0,
                        EvaluationPassed = 0, Funded = 0, Payouts = 0, PayoutCash = 0, EvaluationCost = 0,
                        Note = "Setup count comes directly from NinjaTrader " + minutes + "M bars. The matching 1M series did not reconcile, so outcomes, P/L, allocation, lifecycle, and payout math are intentionally blocked."
                    });
                    continue;
                }
                detectionConfig.Scope = symbol; detectionConfig.SetupMinutes = minutes; detectionConfig.OutcomeModelEnabled = 1;
                if (symbol == "MNQ") { detectionConfig.MnqOutcomeTimeOffsetMinutes = offset; detectionConfig.MnqOutcomeSource = minutes == 1 ? "COMPARISON DIRECT SHARED 1M" : "COMPARISON VERIFIED 1M"; }
                else { detectionConfig.MgcOutcomeTimeOffsetMinutes = offset; detectionConfig.MgcOutcomeSource = minutes == 1 ? "COMPARISON DIRECT SHARED 1M" : "COMPARISON VERIFIED 1M"; }
                List<KeystoneArcBar> outcomeBars = minutes == 1 ? setupBars : oneMinute;
                List<KeystoneArcEvent> events = KeystoneArcEngine.DetectAndResolve(outcomeBars, setupBars, detectionConfig);
                for (int i = 0; i < events.Count; i++) { events[i].ReviewState = "ACCEPTED"; events[i].ReviewNote = "COMPARISON AUTO-INCLUDED"; }
                Dictionary<string, List<KeystoneArcEvent>> frameEvents;
                if (!verifiedByFrame.TryGetValue(minutes, out frameEvents)) { frameEvents = new Dictionary<string, List<KeystoneArcEvent>>(StringComparer.OrdinalIgnoreCase); verifiedByFrame[minutes] = frameEvents; }
                frameEvents[symbol] = CloneEvents(events);
                AppendComparisonScenarioRows(results, events, symbol, minutes, detectionConfig);
            }
            if (baseConfig.Scope == "BOTH")
            {
                foreach (var frame in verifiedByFrame.OrderBy(x => x.Key))
                {
                    List<KeystoneArcEvent> mnqEvents, mgcEvents;
                    if (!frame.Value.TryGetValue("MNQ", out mnqEvents) || !frame.Value.TryGetValue("MGC", out mgcEvents)) continue;
                    List<KeystoneArcEvent> combined = CloneEvents(mnqEvents); combined.AddRange(CloneEvents(mgcEvents)); combined = combined.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
                    KeystoneArcRunConfig combinedConfig = CloneConfig(baseConfig); combinedConfig.Scope = "BOTH"; combinedConfig.SetupMinutes = frame.Key; combinedConfig.OutcomeModelEnabled = 1;
                    AppendComparisonScenarioRows(results, combined, "BOTH", frame.Key, combinedConfig);
                }
            }
            return results;
        }

        private static void AppendComparisonScenarioRows(List<KeystoneArcComparisonRow> results, List<KeystoneArcEvent> events, string symbol, int minutes, KeystoneArcRunConfig detectionConfig)
        {
            foreach (int poolSize in new[] { 1, 5, 10, 20, 40 })
            {
                int[] modes = poolSize == 1 ? new[] { -2 } : new[] { 1, 0 };
                foreach (int evaluationEnabled in modes)
                {
                    KeystoneArcRunConfig scenario = CloneConfig(detectionConfig); scenario.Scope = symbol; scenario.PoolSize = poolSize; scenario.EvaluationEnabled = evaluationEnabled;
                    List<KeystoneArcEvent> assignedEvents = CloneEvents(events); List<KeystoneArcVirtualAccount> accounts = KeystoneArcEngine.SimulatePool(assignedEvents, scenario);
                    int wins = events.Count(x => x.Outcome == "WIN"), losses = events.Count(x => x.Outcome.StartsWith("LOSS")), exits = events.Count(x => x.Outcome == "SESSION EXIT");
                    results.Add(new KeystoneArcComparisonRow
                    {
                        Symbol = symbol, SetupMinutes = minutes, PoolSize = poolSize, StartMode = evaluationEnabled == -2 ? "SINGLE P/L" : (evaluationEnabled == 0 ? "DIRECT FUNDED" : "EVALUATION FIRST"),
                        DataMethod = "direct " + minutes + "M setup bars + verified 1M outcome timing", Setups = events.Count, Wins = wins, Losses = losses, SessionExits = exits,
                        AllOutcomeGross = events.Sum(x => x.GrossPnl), Assigned = assignedEvents.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)), Skipped = assignedEvents.Count(x => !string.IsNullOrWhiteSpace(x.SkipReason)), AssignedGross = assignedEvents.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)).Sum(x => x.GrossPnl),
                        EvaluationPassed = accounts.Sum(x => x.EvaluationPasses), Funded = accounts.Count(x => x.Funded), Payouts = accounts.Sum(x => x.Payouts), PayoutCash = accounts.Sum(x => x.PayoutCash), EvaluationCost = accounts.Sum(x => x.EvaluationCost),
                        Note = "Same selected date/session/risk model as the current run. This is a historical ranking for the loaded sample, not a recommendation or future-performance forecast."
                    });
                }
            }
        }

        private void SetComparisonBusy(bool busy, string message)
        {
            if (comparisonBusyOverlay != null) comparisonBusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(message) && comparisonBusyText != null) comparisonBusyText.Text = message;
            if (comparisonBuildButton != null) comparisonBuildButton.IsEnabled = !busy;
            if (comparisonOptimizeButton != null) comparisonOptimizeButton.IsEnabled = !busy;
            if (comparisonClearButton != null) comparisonClearButton.IsEnabled = !busy;
            if (comparisonExportButton != null) comparisonExportButton.IsEnabled = !busy && comparisonRows.Count > 0;
            if (comparisonInstrumentBox != null) comparisonInstrumentBox.IsEnabled = !busy;
            if (comparisonTimeframeBox != null) comparisonTimeframeBox.IsEnabled = !busy;
            if (comparisonSessionBox != null) comparisonSessionBox.IsEnabled = !busy;
            if (comparisonPoolBox != null) comparisonPoolBox.IsEnabled = !busy;
            if (comparisonModeBox != null) comparisonModeBox.IsEnabled = !busy;
        }

        private static bool ComparisonOutcomeMatches(List<KeystoneArcBar> oneMinute, List<KeystoneArcBar> setupBars, int minutes, DateTime start, DateTime end, out int offset)
        {
            offset = 0;
            if (oneMinute == null || oneMinute.Count == 0 || setupBars == null || setupBars.Count == 0) return false;
            const double tolerance = 0.0001;
            int bestMatched = -1, bestChecked = 0, bestMissing = int.MaxValue, bestOffset = 0;
            var offsets = new List<int> { 0 };
            for (int candidate = -1; candidate >= -Math.Max(0, minutes - 1); candidate--) offsets.Add(candidate);
            for (int candidate = 1; candidate <= 2; candidate++) offsets.Add(candidate);
            foreach (int candidate in offsets)
            {
                int checkedBars = 0, matchedBars = 0, missingBars = 0;
                foreach (KeystoneArcBar setup in setupBars.Where(x => x.Time >= start && x.Time <= end).Take(24))
                {
                    DateTime componentStart = setup.Time.AddMinutes(candidate);
                    List<KeystoneArcBar> members = oneMinute.Where(x => x.Time >= componentStart && x.Time < componentStart.AddMinutes(minutes)).OrderBy(x => x.Time).ToList();
                    if (members.Count < minutes) { missingBars++; continue; }
                    checkedBars++;
                    if (Math.Abs(members[0].Open - setup.Open) <= tolerance && Math.Abs(members.Max(x => x.High) - setup.High) <= tolerance && Math.Abs(members.Min(x => x.Low) - setup.Low) <= tolerance && Math.Abs(members[members.Count - 1].Close - setup.Close) <= tolerance) matchedBars++;
                }
                if (matchedBars > bestMatched || (matchedBars == bestMatched && missingBars < bestMissing)) { bestMatched = matchedBars; bestChecked = checkedBars; bestMissing = missingBars; bestOffset = candidate; }
            }
            bool passed = bestChecked >= 3 && bestMatched == bestChecked && bestMissing == 0;
            offset = passed ? bestOffset : 0;
            return passed;
        }

        private void CancelComparisonRequests()
        {
            ++comparisonGeneration; comparisonRequestQueue.Clear(); comparisonRequestActive = false; comparisonPendingRequests = 0;
            try { if (comparisonRequest != null) comparisonRequest.Dispose(); } catch { }
            comparisonRequest = null;
            if (operationBusy && operationMessage.StartsWith("REQUESTING DIRECT 1M–4H COMPARISON", StringComparison.OrdinalIgnoreCase)) EndBusy();
            SetComparisonBusy(false, null);
        }

        private void RefreshLifecycleInputState()
        {
            bool asianCopy = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            bool oneDay = config != null && config.Start != DateTime.MinValue
                ? config.OneDayMode == 1
                : (dateModeBox != null && string.Equals(Convert.ToString(dateModeBox.SelectedItem), "ONE DAY", StringComparison.OrdinalIgnoreCase));
            bool singleAccount = poolBox != null && string.Equals(Convert.ToString(poolBox.SelectedItem), "1", StringComparison.OrdinalIgnoreCase);
            bool evaluationFirst = !singleAccount && (accountStartModeBox == null || !string.Equals(Convert.ToString(accountStartModeBox.SelectedItem), "DIRECT FUNDED", StringComparison.OrdinalIgnoreCase));
            if (evalTargetBox != null) evalTargetBox.IsEnabled = evaluationFirst;
            if (evalDailyCapBox != null) evalDailyCapBox.IsEnabled = evaluationFirst;
            if (evalConsistencyBox != null) evalConsistencyBox.IsEnabled = evaluationFirst;
            if (evalFailureBox != null) evalFailureBox.IsEnabled = evaluationFirst;
            if (evalDailyLossBox != null) evalDailyLossBox.IsEnabled = evaluationFirst;
            if (evalCostBox != null) evalCostBox.IsEnabled = evaluationFirst;
            if (minimumDaysBox != null) minimumDaysBox.IsEnabled = evaluationFirst;
            if (fundedDailyLossBox != null) fundedDailyLossBox.IsEnabled = !oneDay && !singleAccount;
            if (fundedFailureBox != null) fundedFailureBox.IsEnabled = !oneDay && !singleAccount;
            for (int i = 0; i < oneDayHiddenControls.Count; i++) oneDayHiddenControls[i].Visibility = oneDay || singleAccount ? Visibility.Collapsed : Visibility.Visible;
            if (lifecyclePolicySection != null) lifecyclePolicySection.Visibility = oneDay ? Visibility.Collapsed : Visibility.Visible;
            if (governanceSection != null) governanceSection.Visibility = oneDay ? Visibility.Collapsed : Visibility.Visible;
            for (int i = 0; i < evaluationOnlyControls.Count; i++) evaluationOnlyControls[i].Visibility = evaluationFirst && !oneDay && !singleAccount ? Visibility.Visible : Visibility.Collapsed;
            bool stageTermsVisible = evaluationFirst && !oneDay && !singleAccount && !asianCopy;
            if (evalStageTradeRulesBox != null) evalStageTradeRulesBox.IsEnabled = stageTermsVisible;
            bool stageTermsEnabled = stageTermsVisible && evalStageTradeRulesBox != null && evalStageTradeRulesBox.IsChecked == true;
            // The toggle itself is still visible for BH evaluation studies. Asian is a separate
            // cycle-backtest whose leg exits are driven by its own reversal rules, not BH trade
            // target/stop values, so these BH-only caps are not shown there.
            for (int i = 0; i < evaluationTradeRuleControls.Count; i++)
            {
                if (i == 0) evaluationTradeRuleControls[i].Visibility = stageTermsVisible ? Visibility.Visible : Visibility.Collapsed;
                else evaluationTradeRuleControls[i].Visibility = stageTermsEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            if (evalOverridePanel != null) evalOverridePanel.Visibility = stageTermsEnabled ? Visibility.Visible : Visibility.Collapsed;
            bool firmCapVisible = evaluationFirst && !oneDay && !singleAccount && firmFundedCapBox != null && firmFundedCapBox.IsChecked == true;
            for (int i = 0; i < firmCapControls.Count; i++) firmCapControls[i].Visibility = firmCapVisible ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < singleAccountControls.Count; i++) singleAccountControls[i].Visibility = singleAccount ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < propOnlyControls.Count; i++) propOnlyControls[i].Visibility = Visibility.Visible;
            for (int i = 0; i < personalOnlyControls.Count; i++) personalOnlyControls[i].Visibility = Visibility.Collapsed;
            if (poolBox != null) poolBox.IsEnabled = true;
            if (lifecycleControlsPanel != null) lifecycleControlsPanel.Visibility = Visibility.Visible;
            if (poolAccountsCard != null) poolAccountsCard.Visibility = Visibility.Visible;
            if (poolDetailCard != null) poolDetailCard.Visibility = Visibility.Visible;
            if (runPoolButton != null) { runPoolButton.Visibility = Visibility.Visible; runPoolButton.Content = "RUN VIRTUAL POOL"; }
            if (clearPoolButton != null) clearPoolButton.Visibility = Visibility.Visible;
            if (accountStartModeBox != null) accountStartModeBox.IsEnabled = !singleAccount;
            if (resultsHeadingText != null) resultsHeadingText.Text = singleAccount ? (asianCopy ? "SINGLE ACCOUNT • ASIAN CYCLE RANGE P/L" : "SINGLE PROP ACCOUNT • RANGE P/L") : (asianCopy ? "ASIAN CYCLE • COPY-ACCOUNT RESULTS" : "PROP VIRTUAL-POOL RESULTS • ALL ELIGIBLE SETUPS");
            if (poolAccountsHeadingText != null) poolAccountsHeadingText.Text = singleAccount ? "SINGLE ACCOUNT" : (asianCopy ? "COPY-TRADING VIRTUAL ACCOUNTS" : "VIRTUAL ACCOUNTS");
            if (poolDetailHeadingText != null) poolDetailHeadingText.Text = singleAccount ? "SINGLE ACCOUNT • ASSIGNED EVENTS" : (asianCopy ? "SELECTED COPY ACCOUNT • DAILY CYCLE • LIFECYCLE" : "SELECTED ACCOUNT • ASSIGNED EVENTS • LIFECYCLE");
            if (poolResultBanner != null && singleAccount) poolResultBanner.Text = "SINGLE ACCOUNT MODE: starting balance plus assigned historical P/L only. Evaluation, funded, payout, replacement, and lifecycle controls are hidden.";
            else if (poolResultBanner != null && oneDay) poolResultBanner.Text = "ONE-DAY STUDY: virtual accounts assign every eligible setup. Evaluation, funded-stage, payout, and multi-day lifecycle settings are hidden because a one-day study cannot establish them.";
            else if (poolResultBanner != null && !oneDay && accounts.Count == 0) poolResultBanner.Text = config.OutcomeModelEnabled == 1
                ? (asianCopy ? "NEXT: choose the copy-account count and lifecycle settings, then click RUN VIRTUAL POOL. The resolved MNQ/MGC daily-cycle legs are ready for chart review." : "NEXT: choose the virtual account count and lifecycle settings, then click RUN VIRTUAL POOL. Chart setups are available now.")
                : "SETUP BARS ARE READY: inspect charts or scenario settings. RUN VIRTUAL POOL remains disabled until the direct 1-minute price path is verified.";
            if (startModeHintText != null)
                startModeHintText.Text = singleAccount
                    ? "SINGLE ACCOUNT: one virtual prop ledger for the selected range. It records starting balance, assigned P/L, wins/losses, and daily locks only; no payout or evaluation mechanics are applied."
                    : oneDay
                    ? "ONE-DAY STUDY: setup count, entry price, target/stop outcome, account assignment, and session statistics only."
                    : (evaluationFirst
                    ? "EVALUATION FIRST: by default, evals use the main profit/loss limits selected in Step 1. Turn on EVAL OVERRIDE only to reveal different evaluation daily-profit, daily-loss, trade-profit, and trade-loss values."
                    : "DIRECT FUNDED: evaluation inputs are hidden and ignored. All virtual accounts start funded; payout settings remain active.");
            RefreshResultsModePresentation(oneDay && !singleAccount);
        }

        private void RefreshResultsModePresentation(bool oneDayPool)
        {
            if (poolLifecycleMetrics != null) poolLifecycleMetrics.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (oneDayPoolMetrics != null) oneDayPoolMetrics.Visibility = oneDayPool ? Visibility.Visible : Visibility.Collapsed;
            if (rangeLifecycleMetrics != null) rangeLifecycleMetrics.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (oneDayRangeSummaryCard != null) oneDayRangeSummaryCard.Visibility = oneDayPool ? Visibility.Visible : Visibility.Collapsed;
            if (accountLifecycleMetrics != null) accountLifecycleMetrics.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (oneDayAccountMetrics != null) oneDayAccountMetrics.Visibility = oneDayPool ? Visibility.Visible : Visibility.Collapsed;
            if (poolStateFilterBox != null) poolStateFilterBox.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (oneDayAccountFilters != null) oneDayAccountFilters.Visibility = oneDayPool ? Visibility.Visible : Visibility.Collapsed;
            if (poolCashPolicyMetrics != null) poolCashPolicyMetrics.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (portfolioCashResultsTab != null) portfolioCashResultsTab.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (walkthroughResultsTab != null) walkthroughResultsTab.Visibility = Visibility.Visible;
            if (firstReturnCard != null) firstReturnCard.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (payoutCycleCard != null) payoutCycleCard.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (firstReturnResultsTab != null) firstReturnResultsTab.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (payoutCycleResultsTab != null) payoutCycleResultsTab.Visibility = oneDayPool ? Visibility.Collapsed : Visibility.Visible;
            if (dailySessionResultsTab != null) dailySessionResultsTab.Visibility = Visibility.Visible;
            if (poolTimelineHeadingText != null)
                poolTimelineHeadingText.Text = oneDayPool
                    ? "ONE-DAY ACCOUNT WALKTHROUGH • assignment times, W/L, P/L, and daily locks"
                    : "LIFECYCLE WALKTHROUGH • payout dates, blowouts, and replacements";
            if (walkthroughHeadingText != null)
                walkthroughHeadingText.Text = oneDayPool
                    ? "ACCOUNT WALKTHROUGH • ASSIGNMENTS, W/L, DAILY LOCKS"
                    : "LIFECYCLE WALKTHROUGH • EVAL → FUNDED → PAYOUT / REPLACEMENT";
        }

        private UIElement MathTab()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = Stack();
            top.Children.Add(Txt("MATH READER • SELECTED-RANGE DATA ONLY", Cyan, 14, FontWeights.Bold));
            top.Children.Add(Txt("All detector-qualified setups are included in rotation, lifecycle, and account comparison math by default. The Verify Entries screen can exclude a specific row. It is not live trading, replay, or a payout prediction.", Muted, 11, FontWeights.Normal));
            var refresh = Btn("REFRESH MATH FROM ELIGIBLE SETUPS", Blue); refresh.Click += delegate { BuildMathReaderAsync(); }; top.Children.Add(refresh);
            root.Children.Add(top);
            mathText = Txt("WAITING FOR A DETECTED DATE RANGE", Text, 11, FontWeights.Normal); mathText.FontFamily = new FontFamily("Consolas"); mathText.TextWrapping = TextWrapping.Wrap;
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = mathText, Margin = new Thickness(8) };
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);
            return PanelCard(root);
        }

        private UIElement PoolTab()
        {
            var root = new Grid { Margin = new Thickness(6) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.78, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            var controls = Stack(); controls.Children.Add(Txt("ROTATION CONTROLS", Cyan, 14, FontWeights.Bold));
            poolBox = Select("1", "5", "10", "20", "40"); poolBox.SelectedIndex = 2;
            controls.Children.Add(Row("POOL SIZE", poolBox));
            controls.Children.Add(Txt("Only ACCEPTED review rows are assigned. A valid signal is skipped when no account is free at its immediate entry time.", Muted, 11, FontWeights.Normal));
            var simulate = Btn("RUN ACCEPTED-EVENT ROTATION", Green); simulate.Click += delegate { SimulatePool(); }; controls.Children.Add(simulate);
            var openReview = Btn("GO TO ENTRY REVIEW", Blue); openReview.Click += delegate { if (workspaceTabs != null) workspaceTabs.SelectedIndex = 1; }; controls.Children.Add(openReview);

            var accountPanel = Stack(); accountPanel.Children.Add(Txt("VIRTUAL ACCOUNTS", Orchid, 14, FontWeights.Bold));
            poolAccountList = new ListBox { Background = Card, Foreground = Text, BorderBrush = Orchid, BorderThickness = new Thickness(1), MinHeight = 410, Margin = new Thickness(4) };
            poolAccountList.SelectionChanged += delegate { UpdatePoolDetail(); }; accountPanel.Children.Add(poolAccountList);
            poolText = Txt("No virtual pool run. Accept entries first.", Gold, 11, FontWeights.Bold); accountPanel.Children.Add(poolText);

            var detailPanel = Stack(); detailPanel.Children.Add(Txt("ACCOUNT LEDGER", Green, 14, FontWeights.Bold));
            poolDetailText = Txt("Choose an account after a rotation run to see every assigned trade, day lock, carry balance, skips, and lifecycle state.", Text, 11, FontWeights.Normal); poolDetailText.FontFamily = new FontFamily("Consolas");
            detailPanel.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = poolDetailText, Margin = new Thickness(4) });
            root.Children.Add(PanelCard(controls)); Grid.SetColumn(accountPanel, 1); root.Children.Add(PanelCard(accountPanel)); Grid.SetColumn(detailPanel, 2); root.Children.Add(PanelCard(detailPanel)); return PanelCard(root);
        }

        private UIElement EvaluationTab()
        {
            var root = new Grid { Margin = new Thickness(6) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            var left = Stack(); left.Children.Add(Txt("ILLUSTRATIVE LIFECYCLE ASSUMPTIONS", Orchid, 14, FontWeights.Bold));
            evalTargetBox = Input("3000"); evalDailyCapBox = Input("1500"); evalFailureBox = Input("2000"); minimumDaysBox = Input("2"); minimumQualifyingDayBox = Input("150"); payoutThresholdBox = Input("4000"); payoutDaysBox = Input("5"); payoutAmountBox = Input("2000"); evalCostBox = Input("120");
            left.Children.Add(Row("EVALUATION TARGET $", evalTargetBox)); left.Children.Add(Row("MAX CREDIT / DAY $", evalDailyCapBox)); left.Children.Add(Row("EVALUATION FAILURE $", evalFailureBox)); left.Children.Add(Row("MIN QUALIFYING DAY $", minimumQualifyingDayBox)); left.Children.Add(Row("MIN POSITIVE DAYS", minimumDaysBox)); left.Children.Add(Row("PAYOUT THRESHOLD $", payoutThresholdBox)); left.Children.Add(Row("PAYOUT DAYS REQUIRED", payoutDaysBox)); left.Children.Add(Row("PAYOUT AMOUNT $", payoutAmountBox)); left.Children.Add(Row("EVALUATION COST $", evalCostBox));
            left.Children.Add(Txt("These are scenario inputs only. They do not represent named firm rules, eligibility, or projected earnings.", Gold, 11, FontWeights.Bold));
            var right = Stack(); right.Children.Add(Txt("LIFECYCLE SUMMARY BY ACCOUNT", Cyan, 14, FontWeights.Bold)); lifecycleText = Txt("Run accepted-event rotation first. This tab will then show evaluation balance, funded balance, carry, failures, costs, and payout-cycle state for every account.", Text, 11, FontWeights.Normal); lifecycleText.FontFamily = new FontFamily("Consolas"); right.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = lifecycleText, Margin = new Thickness(4) });
            root.Children.Add(PanelCard(left)); Grid.SetColumn(right, 1); root.Children.Add(PanelCard(right)); return PanelCard(root);
        }

        private UIElement DataTab()
        {
            var root = new Grid { Margin = new Thickness(6) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = Stack(); top.Children.Add(Txt("RUN ARCHIVE", Cyan, 14, FontWeights.Bold)); top.Children.Add(Txt("This tab is a read-only archive of saved snapshots and completed evidence packages. Use EXPORT RESEARCH PACKAGE in Simulation Results for the single folder with index.html, ledgers, and chart files.", Muted, 11, FontWeights.Normal));
            var actions = new UniformGrid { Columns = 3, Margin = new Thickness(0, 8, 0, 8) };
            saveButton = Btn("SAVE SNAPSHOT", Blue); saveButton.IsEnabled = false; saveButton.Click += delegate { SaveSnapshot(); }; saveButtons.Add(saveButton);
            var refresh = Btn("REFRESH RUN ARCHIVE", Orchid); refresh.Click += delegate { RefreshSavedRuns(); };
            clearButton = Btn("CLEAR CURRENT LAB", Red); clearButton.Click += delegate { ClearLab(); };
            actions.Children.Add(saveButton); actions.Children.Add(refresh); actions.Children.Add(clearButton); top.Children.Add(actions); root.Children.Add(top);
            var bottom = new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            savedRunList = new ListBox { Background = Card, Foreground = Text, BorderBrush = Cyan, BorderThickness = new Thickness(1), Margin = new Thickness(4), MinHeight = 340 }; savedRunList.SelectionChanged += delegate { ShowSavedRunDetail(); };
            savedDataText = Txt("No saved runs listed yet.", Text, 11, FontWeights.Normal); savedDataText.FontFamily = new FontFamily("Consolas");
            var savedListCard = PanelCard(savedRunList);
            var savedDetailCard = PanelCard(savedDataText);
            bottom.Children.Add(savedListCard);
            Grid.SetColumn(savedDetailCard, 1);
            bottom.Children.Add(savedDetailCard);
            Grid.SetRow(bottom, 1); root.Children.Add(bottom); return PanelCard(root);
        }

        private void RequestHistory()
        {
            DateTime start, end;
            if (!ConfigurationStillApproved()) { UpdateUi("STEP 1 REQUIRED • CONFIRM THE CURRENT CONFIGURATION BEFORE REQUESTING DATA", Gold); UpdateWorkflowState(); return; }
            if (!ReadConfig(out start, out end)) return;
            CancelRequests(); CancelEvidenceRequest(); evidenceBars.Clear(); historicalRequestDetails.Clear(); failedContractRolloverSeries.Clear(); mnqBars = new List<KeystoneArcBar>(); mgcBars = new List<KeystoneArcBar>(); mnqSetupBars = new List<KeystoneArcBar>(); mgcSetupBars = new List<KeystoneArcBar>(); mnqSetupFromOpenChart = false; mgcSetupFromOpenChart = false; mnqSetupDerivedFromOpenOneMinute = false; mgcSetupDerivedFromOpenOneMinute = false; mnqOutcomeFromOpenChart = false; mgcOutcomeFromOpenChart = false; mnqOutcomeMatchesSetup = true; mgcOutcomeMatchesSetup = true; config.MnqOutcomeTimeOffsetMinutes = 0; config.MgcOutcomeTimeOffsetMinutes = 0; config.MnqOutcomeSource = "VERIFICATION PENDING"; config.MgcOutcomeSource = "VERIFICATION PENDING"; mnqOutcomeValidation = "awaiting 1-minute comparison"; mgcOutcomeValidation = "awaiting 1-minute comparison"; researchRunCompleted = false; events.Clear(); reviewRows.Clear(); accounts.Clear(); historicalDataReceipt = "DATA RECEIPT: historical request started; awaiting NinjaTrader BarsRequest completion.";
            // A new request must never leave totals, account cards, or a ledger from a previous
            // completed run on screen.  Otherwise a zero-bar receipt looks like a successful run
            // because the visible totals belong to a different date range.
            if (summaryText != null) summaryText.Text = "LOADING NEW HISTORY • PREVIOUS RUN TOTALS CLEARED";
            if (eventText != null) eventText.Text = "HISTORY REQUEST STARTED • CURRENT-RUN LEDGER IS EMPTY UNTIL THE DATA RECEIPT COMPLETES";
            if (mathText != null) mathText.Text = "WAITING FOR THE CURRENT NINJATRADER HISTORY RECEIPT";
            if (poolText != null) poolText.Text = "CURRENT-RUN POOL RESULTS CLEARED • WAITING FOR VERIFIED HISTORY";
            if (poolDetailText != null) poolDetailText.Text = "NO CURRENT-RUN ACCOUNT SELECTED";
            if (poolTimelineStack != null) poolTimelineStack.Children.Clear();
            if (poolAccountCardStack != null) poolAccountCardStack.Children.Clear();
            if (poolAccountList != null) poolAccountList.Items.Clear();
            UpdatePoolMetricTiles();
            isProcessing = false;
            long run = ++generation; pendingRequests = 0; historicalRequestTotal = 0; historicalRequestCompleted = 0; historicalRequestQueue.Clear(); historicalRequestActive = false;
            string scope = config.Scope;
            try
            {
                BeginBusy(config.SetupMinutes == 1
                    ? "REQUESTING " + scope + " DIRECT 1-MINUTE SETUP HISTORY"
                    : "REQUESTING " + scope + " " + config.SetupMinutes + "-MINUTE SETUP HISTORY + DIRECT 1-MINUTE PRICE PATH");
                if (scope == "MNQ" || scope == "BOTH") BeginInstrumentRequest("MNQ", configuredMnqInstrument, config.MnqName, start, end, run);
                if (scope == "MGC" || scope == "BOTH") BeginInstrumentRequest("MGC", configuredMgcInstrument, config.MgcName, start, end, run);
                StartNextHistoricalRequest();
                if (historicalRequestActive || historicalRequestQueue.Count > 0 || pendingRequests > 0)
                {
                    UpdateUi("LOADING SELECTED HISTORY • " + scope + " • TEST DATE(S) " + SelectedTestDateLabel() + " • MNQ AND MGC REQUESTS ARE ISOLATED • NO ORDERS", Gold);
                    UpdateWorkflowState();
                }
            }
            catch (Exception ex) { EndBusy(); UpdateUi("REQUEST FAILED • " + ex.Message, Red); UpdateWorkflowState(); }
        }

        private void BeginInstrumentRequest(string key, Instrument instrument, string contractName, DateTime start, DateTime end, long run)
        {
            if (instrument == null)
            {
                historicalRequestDetails.Add(key + " instrument unresolved • no request started.");
                if (key == "MNQ") { mnqBars.Clear(); mnqSetupBars.Clear(); }
                else { mgcBars.Clear(); mgcSetupBars.Clear(); }
                return;
            }
            List<KeystoneArcBar> exactOutcomeBars, exactChartBars; string outcomeChartDetail, chartDetail;
            // Resolve the selected setup timeframe's trading-hours template once, then carry
            // that exact template into both requests.  The previous code asked each request to
            // discover any chart with the same symbol; with multiple MNQ/MGC charts open, 1M and
            // setup requests could silently use different session templates.
            string commonHoursSource;
            TradingHours commonHours = ResolveRequestTradingHours(key, config.SetupMinutes, out commonHoursSource);
            bool outcomeChartFound = TryCopyOpenChartBars(key, 1, start, end, out exactOutcomeBars, out outcomeChartDetail);
            bool chartBarsFound = TryCopyOpenChartBars(key, config.SetupMinutes, start, end, out exactChartBars, out chartDetail);
            historicalRequestDetails.Add(key + " exact request instrument: " + instrument.FullName + " | " + (outcomeChartFound ? outcomeChartDetail : "direct 1M BarsRequest outcome bars") + " | " + (chartBarsFound ? chartDetail : "direct " + config.SetupMinutes + "M BarsRequest setup bars") + " | shared trading hours: " + commonHoursSource + " | merge policy: NinjaTrader global setting.");
            // A 1-minute study does not need two separate requests for the same 1-minute data.
            // One direct source is simultaneously the selected setup series and the precise
            // outcome series, so it cannot fail a needless cross-timeframe reconciliation.
            if (config.SetupMinutes == 1)
            {
                List<KeystoneArcBar> sharedBars = chartBarsFound ? exactChartBars : (outcomeChartFound ? exactOutcomeBars : null);
                bool sharedFromOpenChart = chartBarsFound || outcomeChartFound;
                historicalRequestDetails.Add(key + " 1M study: one direct 1M source is used for both setup detection and outcome resolution; no separate 1M-versus-timeframe comparison is requested.");
                if (sharedFromOpenChart)
                {
                    if (key == "MNQ") { mnqBars = new List<KeystoneArcBar>(sharedBars); mnqSetupBars = new List<KeystoneArcBar>(sharedBars); mnqOutcomeFromOpenChart = true; mnqSetupFromOpenChart = true; }
                    else { mgcBars = new List<KeystoneArcBar>(sharedBars); mgcSetupBars = new List<KeystoneArcBar>(sharedBars); mgcOutcomeFromOpenChart = true; mgcSetupFromOpenChart = true; }
                }
                else
                {
                    // Include context for the red/reference candles, then reuse this completed
                    // direct request for exact 1M entry/stop/target resolution.
                    EnqueueCalendarSafeHistoryRequests(key, instrument, start, end, 1, true, commonHours, commonHoursSource, run);
                }
                return;
            }
            BarsRequest outcomeRequest = null;
            if (outcomeChartFound)
            {
                if (key == "MNQ") { mnqBars = exactOutcomeBars; mnqOutcomeFromOpenChart = true; }
                else { mgcBars = exactOutcomeBars; mgcOutcomeFromOpenChart = true; }
            }
            else
            {
                EnqueueCalendarSafeHistoryRequests(key, instrument, start, end, 1, false, commonHours, commonHoursSource, run);
            }
            BarsRequest setupRequest = null;
            if (chartBarsFound)
            {
                if (key == "MNQ") { mnqSetupBars = exactChartBars; mnqSetupFromOpenChart = true; }
                else { mgcSetupBars = exactChartBars; mgcSetupFromOpenChart = true; }
            }
            else if (!string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase) && outcomeChartFound)
            {
                List<KeystoneArcBar> derivedSetupBars = AggregateExactOneMinuteSetupBars(exactOutcomeBars, config.SetupMinutes);
                if (derivedSetupBars.Count > 0)
                {
                    if (key == "MNQ") { mnqSetupBars = derivedSetupBars; mnqSetupFromOpenChart = true; mnqSetupDerivedFromOpenOneMinute = true; }
                    else { mgcSetupBars = derivedSetupBars; mgcSetupFromOpenChart = true; mgcSetupDerivedFromOpenOneMinute = true; }
                    historicalRequestDetails.Add(key + " selected " + config.SetupMinutes + "M setup bars derived from the exact open 1M chart because no matching " + config.SetupMinutes + "M chart was loaded • strict 1M↔setup OHLC validation remains required.");
                }
                else
                {
                    EnqueueCalendarSafeHistoryRequests(key, instrument, start, end, config.SetupMinutes, true, commonHours, commonHoursSource, run);
                }
            }
            else
            {
                // Bring two completed setup bars before the selected boundary so the first in-range
                // trigger can legitimately use the prior red and green reference bars.
                EnqueueCalendarSafeHistoryRequests(key, instrument, start, end, config.SetupMinutes, true, commonHours, commonHoursSource, run);
            }
            if (key == "MNQ") { mnqRequest = outcomeRequest; mnqSetupRequest = setupRequest; }
            else { mgcRequest = outcomeRequest; mgcSetupRequest = setupRequest; }
        }

        // Request the selected range as one direct BarsRequest per instrument and series.
        // Splitting a 9-month run into 266 calendar pieces caused some connections to return
        // zero bars for each partial overnight fragment.  A normal direct range request is the
        // same approach that successfully supplies the Evidence Chart's full session bars.
        // The setup request starts with context so the first selected setup can use its prior
        // red/bullish reference bars.
        private void EnqueueCalendarSafeHistoryRequests(string key, Instrument instrument, DateTime start, DateTime end, int minutes, bool isSetupRequest, TradingHours tradingHours, string tradingHoursSource, long run)
        {
            if (instrument == null || start > end) return;
            // The selected setup request needs predecessor bars for the red/reference pattern.
            // The direct 1M outcome request needs the same left context whenever a selected
            // N-minute bar is close-stamped: an 08:00 5M bar may aggregate 07:56–08:00.
            // Without this, the first otherwise valid setup bar makes a full date range fail
            // the global safety check even though the rest of the exact NinjaTrader series
            // reconciles.  Comparison requests already carried this context; the main path now
            // does too.
            // BH needs only the immediately prior red/reference setup bars. Request three
            // selected-timeframe bars (15 minutes for a 5M study, 3 minutes for 1M), not an
            // arbitrary preceding hour. The outcome path carries the same small context so an
            // opening-timestamped setup can still reconcile to 1M components.
            int context = isSetupRequest
                ? Math.Max(3, minutes * 3)
                : Math.Max(3, config == null ? 3 : config.SetupMinutes + 2);
            // A cycle backtest enters at the exact chosen minute and does not need BH-pattern
            // predecessor candles. Its request must not be rejected merely because no bar exists
            // before the selected entry window.
            bool asianCycle = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            if (asianCycle) context = 0;
            DateTime requestStart = start.AddMinutes(-context);
            // Full Globex opens at 18:00 ET after the 17:00–18:00 CME maintenance break. A
            // 19:00 Asia request can safely carry an 18:00 predecessor hour, but a Full Globex
            // request must never begin at 17:00: some NinjaTrader connections return zero bars
            // for the entire request when its start is inside that non-trading interval. The
            // first 18:00 bar simply has no in-session BH predecessor, as intended.
            if (!asianCycle && config != null && string.Equals(config.SessionMode, "FULL_GLOBEX", StringComparison.OrdinalIgnoreCase)
                && start.Hour == 18 && start.Minute == 0 && requestStart < start)
                requestStart = start;
            // MGC's currently visible chart can be a far-dated contract whose merged display
            // history is useful on screen but whose direct 1M BarsRequest has no data for the
            // selected old range. Its resolver is already range-start anchored; do not undo that
            // selection by falling back to the visible current MGC contract after a zero receipt.
            Instrument chartFallback = string.Equals(key, "MGC", StringComparison.OrdinalIgnoreCase) ? null : FindOpenChartInstrument(key, end.Date);
            historicalRequestQueue.Enqueue(new HistoricalRequestWorkItem { Key = key, Instrument = instrument, Start = requestStart, End = end, Minutes = minutes, IsSetupRequest = isSetupRequest, TradingHours = tradingHours, TradingHoursSource = tradingHoursSource, Append = false, FallbackInstrument = chartFallback, FallbackStage = 0, Run = run });
            historicalRequestTotal++;
            historicalRequestDetails.Add(key + " " + minutes + "M " + (isSetupRequest ? "setup" : "outcome") + " history queued as one direct selected-range request • " + requestStart.ToString("yyyy-MM-dd HH:mm") + " → " + end.ToString("yyyy-MM-dd HH:mm") + ".");
        }

        private void StartNextHistoricalRequest()
        {
            if (historicalRequestActive || pendingRequests > 0) return;
            if (historicalRequestQueue.Count == 0) { FinalizeRequestedDataState(); return; }
            HistoricalRequestWorkItem item = historicalRequestQueue.Dequeue();
            if (item == null || item.Run != generation) { StartNextHistoricalRequest(); return; }
            historicalRequestActive = true; pendingRequests = 1;
            try
            {
                BarsRequest request = StartRequest(item);
                if (item.Key == "MNQ") { if (item.IsSetupRequest) mnqSetupRequest = request; else mnqRequest = request; }
                else { if (item.IsSetupRequest) mgcSetupRequest = request; else mgcRequest = request; }
            }
            catch (Exception ex)
            {
                // NinjaTrader does not expose a universal fallback error enum member across builds.
                // A request-start exception is handled explicitly so it still clears the queue safely.
                DispatchToLab(delegate { CompleteRequestStartFailure(item.Key, item.IsSetupRequest, "REQUEST START ERROR • " + ex.Message, item.Run); });
            }
        }

        private BarsRequest StartRequest(HistoricalRequestWorkItem item)
        {
            if (item == null || item.Instrument == null) throw new InvalidOperationException("Instrument not found: " + (item == null ? "unknown" : item.Key));
            string hoursSource;
            TradingHours requestHours = item.TradingHours;
            hoursSource = item.TradingHoursSource;
            if (requestHours == null) requestHours = ResolveRequestTradingHours(item.Key, item.IsSetupRequest ? item.Minutes : config.SetupMinutes, out hoursSource);
            if (string.IsNullOrWhiteSpace(hoursSource)) hoursSource = "Default 24 x 7";
            historicalRequestDetails.Add(item.Key + " " + item.Minutes + "M " + (item.IsSetupRequest ? "setup" : "outcome") + (item.Append ? " continuation" : " request") + " • " + item.Instrument.FullName + " • " + item.Start.ToString("yyyy-MM-dd HH:mm") + " to " + item.End.ToString("yyyy-MM-dd HH:mm") + " • trading hours: " + hoursSource + ".");
            var r = new BarsRequest(item.Instrument, item.Start, item.End) { BarsPeriod = new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = item.Minutes }, TradingHours = requestHours, MergePolicy = MergePolicy.UseGlobalSettings };
            r.Request(delegate(BarsRequest done, ErrorCode error, string message) { CompleteRequest(item, done, error, message); });
            return r;
        }

        private void CompleteRequest(HistoricalRequestWorkItem item, BarsRequest request, ErrorCode error, string message)
        {
            string key = item == null ? string.Empty : item.Key;
            bool isSetupRequest = item != null && item.IsSetupRequest;
            long run = item == null ? 0 : item.Run;
            if (error != ErrorCode.NoError || request == null || request.Bars == null)
            {
                DispatchToLab(delegate { CompleteRequestOnLabThread(item, new List<KeystoneArcBar>(), error, message); });
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var list = new List<KeystoneArcBar>();
                try
                {
                    int total = request.Bars.Count;
                    for (int i = 0; i < total; i++) list.Add(new KeystoneArcBar { Time = request.Bars.GetTime(i), Symbol = key, Open = request.Bars.GetOpen(i), High = request.Bars.GetHigh(i), Low = request.Bars.GetLow(i), Close = request.Bars.GetClose(i), Volume = request.Bars.GetVolume(i) });
                }
                catch (Exception ex)
                {
                    DispatchToLab(delegate { CompleteRequestStartFailure(key, isSetupRequest, "BAR CONVERSION ERROR • " + ex.Message, run); });
                    return;
                }
                DispatchToLab(delegate { CompleteRequestOnLabThread(item, list, error, message); });
            });
        }

        private void CompleteRequestStartFailure(string key, bool isSetupRequest, string message, long run)
        {
            if (run != generation) return;
            pendingRequests = Math.Max(0, pendingRequests - 1); historicalRequestCompleted++;
            historicalRequestActive = false;
            if (key == "MNQ")
            {
                if (isSetupRequest) mnqSetupBars.Clear(); else mnqBars.Clear();
            }
            else
            {
                if (isSetupRequest) mgcSetupBars.Clear(); else mgcBars.Clear();
            }
            historicalRequestDetails.Add(key + " " + (isSetupRequest ? config.SetupMinutes + "M setup" : "1M outcome") + " request failed: " + message);
            UpdateUi("LOADING HISTORY " + historicalRequestCompleted + " / " + Math.Max(historicalRequestTotal, historicalRequestCompleted) + " • " + key + " REQUEST ERROR • " + message, Red);
            if (pendingRequests == 0) StartNextHistoricalRequest();
            else UpdateWorkflowState();
        }

        private bool QueueSingleHistoricalFallback(HistoricalRequestWorkItem item, string reason)
        {
            if (item == null || item.Run != generation) return false;
            Instrument nextInstrument = null;
            TradingHours nextHours = item.TradingHours;
            string nextHoursSource = item.TradingHoursSource;
            int nextStage = item.FallbackStage;
            if (item.FallbackStage == 0 && item.FallbackInstrument != null && !string.Equals(item.FallbackInstrument.FullName, item.Instrument == null ? string.Empty : item.Instrument.FullName, StringComparison.OrdinalIgnoreCase))
            {
                nextInstrument = item.FallbackInstrument;
                nextStage = 1;
                reason += " • retrying actual open chart contract " + nextInstrument.FullName;
            }
            else if (item.FallbackStage < 2 && (nextHoursSource ?? string.Empty).IndexOf("Default 24 x 7", StringComparison.OrdinalIgnoreCase) < 0)
            {
                nextInstrument = item.Instrument;
                nextHours = TradingHours.Get("Default 24 x 7");
                nextHoursSource = "Default 24 x 7 retry";
                nextStage = 2;
                reason += " • retrying same contract with Default 24 x 7";
            }
            else if (item.FallbackStage < 3)
            {
                Instrument continuous = SafeGetInstrument(item.Key + " ##-##");
                if (continuous != null && !string.Equals(continuous.FullName, item.Instrument == null ? string.Empty : item.Instrument.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    nextInstrument = continuous;
                    nextHours = item.TradingHours;
                    nextHoursSource = item.TradingHoursSource;
                    nextStage = 3;
                    reason += " • final retry using continuous fallback " + continuous.FullName;
                }
            }
            if (nextInstrument == null) return false;
            historicalRequestQueue.Enqueue(new HistoricalRequestWorkItem
            {
                Key = item.Key, Instrument = nextInstrument, Start = item.Start, End = item.End, Minutes = item.Minutes,
                IsSetupRequest = item.IsSetupRequest, TradingHours = nextHours, TradingHoursSource = nextHoursSource,
                Append = false, FallbackInstrument = item.FallbackInstrument, FallbackStage = nextStage, Run = item.Run
            });
            historicalRequestTotal++;
            historicalRequestDetails.Add(item.Key + " " + item.Minutes + "M " + (item.IsSetupRequest ? "setup" : "outcome") + " primary response had " + reason + ". One full-range fallback queued; no calendar fragments were created.");
            return true;
        }

        // A dated September contract cannot provide a January-to-September intraday receipt.
        // The normal path above deliberately requests the full range once and then tries the
        // compatible chart/template/continuous sources. Only after all of those return zero or
        // clearly partial history do we walk NinjaTrader's master expiry calendar. Each request
        // covers one *contract lifecycle* and adjacent receipts are merged by exact timestamp.
        // This is not the previously removed calendar-day splitting path.
        private bool QueueDateCompatibleRolloverSegments(HistoricalRequestWorkItem item, string reason)
        {
            if (item == null || item.Run != generation || item.ContractRolloverSegment || item.FallbackStage >= 4 || item.Start >= item.End) return false;
            Instrument master = SafeGetInstrument(item.Key);
            var segments = new List<HistoricalRequestWorkItem>();
            DateTime cursor = item.Start;
            int guard = 0;
            // MGC's tradable delivery sequence is Feb/Apr/Jun/Aug/Oct/Dec. Some NinjaTrader
            // master catalogs expose a generic quarterly expiry calendar for MGC, which produces
            // dated March/June/September/December recovery requests and leaves historical gold
            // range gaps. The automatic MGC delivery schedule below is therefore authoritative
            // for this recovery step only. MNQ continues using its unchanged master-calendar path.
            bool useMgcDeliverySchedule = string.Equals(item.Key, "MGC", StringComparison.OrdinalIgnoreCase);
            try
            {
                while (!useMgcDeliverySchedule && master != null && master.MasterInstrument != null && cursor < item.End && guard++ < 96)
                {
                    DateTime expiry = master.MasterInstrument.GetNextExpiry(cursor.Date);
                    if (expiry == DateTime.MinValue) break;
                    // Some instrument databases return the expiry month anchor before the current
                    // cursor. Advance until the returned contract can own a non-empty interval.
                    while (expiry.Date < cursor.Date && guard++ < 96)
                    {
                        // A few provider/master calendars return the opening day of the current
                        // contract month even when the probe is later in that month. Move the
                        // probe to the following month so the loop cannot remain on the stale
                        // expiry anchor (for example 01-Mar for a 03-Mar cursor).
                        DateTime nextProbe = expiry.Date.AddMonths(1);
                        DateTime nextExpiry = master.MasterInstrument.GetNextExpiry(nextProbe);
                        if (nextExpiry == DateTime.MinValue || nextExpiry.Date <= expiry.Date) break;
                        expiry = nextExpiry;
                    }
                    if (expiry == DateTime.MinValue || expiry.Date < cursor.Date) break;
                    Instrument contract = SafeGetInstrument(item.Key + " " + expiry.ToString("MM-yy", CultureInfo.InvariantCulture));
                    if (contract == null) break;
                    // Give the rollover boundary a small overlap. MergeRequestedBars removes the
                    // duplicate timestamps, and the overlap avoids a false gap when a provider
                    // rolls the merged series shortly before or after its published expiry date.
                    DateTime segmentEnd = expiry.Date.AddDays(2).AddMinutes(-1);
                    if (segmentEnd > item.End) segmentEnd = item.End;
                    if (segmentEnd < cursor) segmentEnd = cursor;
                    segments.Add(new HistoricalRequestWorkItem
                    {
                        Key = item.Key, Instrument = contract, Start = cursor, End = segmentEnd,
                        Minutes = item.Minutes, IsSetupRequest = item.IsSetupRequest,
                        TradingHours = item.TradingHours, TradingHoursSource = item.TradingHoursSource,
                        Append = segments.Count > 0, FallbackInstrument = null, FallbackStage = 4,
                        ContractRolloverSegment = true, Run = item.Run
                    });
                    if (segmentEnd >= item.End) break;
                    cursor = segmentEnd.AddMinutes(1);
                }
            }
            catch { segments.Clear(); }
            // Some NinjaTrader provider/catalog combinations expose the master instrument but
            // return no expiry calendar through GetNextExpiry from an AddOn. A multi-month range
            // must still not be sent only to the current dated contract (for example MNQ 12-26
            // for Mar→Sep). Build automatic delivery-month intervals only after every normal
            // full-range/chart/24x7/continuous request has failed; no contract input is exposed.
            // MGC short-range fix: the MGC delivery-month recovery previously required a range
            // longer than 31 days and at least two segments, so a short MGC study whose initial
            // contract resolution failed got no recovery at all, and a range that falls inside a
            // single delivery month (legitimately one segment) was discarded. For MGC only, run
            // the delivery-month recovery at any range length and accept a single segment. The
            // master-calendar / non-MGC path keeps its original >31-day and >=2-segment rules.
            int minimumRecoverySegments = useMgcDeliverySchedule ? 1 : 2;
            if (useMgcDeliverySchedule || (segments.Count < 2 && (item.End - item.Start).TotalDays > 31))
            {
                segments.Clear();
                try
                {
                    DateTime fallbackCursor = item.Start;
                    int fallbackGuard = 0;
                    while (fallbackCursor < item.End && fallbackGuard++ < 48)
                    {
                        DateTime expiry = NextAutomaticExpiryMonth(item.Key, fallbackCursor.Date);
                        if (expiry == DateTime.MinValue) break;
                        Instrument contract = SafeGetInstrument(item.Key + " " + expiry.ToString("MM-yy", CultureInfo.InvariantCulture));
                        if (contract == null) { fallbackCursor = new DateTime(expiry.Year, expiry.Month, 1).AddMonths(1); continue; }
                        DateTime segmentEnd = expiry.Date.AddDays(2).AddMinutes(-1);
                        if (segmentEnd > item.End) segmentEnd = item.End;
                        if (segmentEnd < fallbackCursor) { fallbackCursor = fallbackCursor.AddDays(1); continue; }
                        segments.Add(new HistoricalRequestWorkItem
                        {
                            Key = item.Key, Instrument = contract, Start = fallbackCursor, End = segmentEnd,
                            Minutes = item.Minutes, IsSetupRequest = item.IsSetupRequest,
                            TradingHours = item.TradingHours, TradingHoursSource = item.TradingHoursSource,
                            Append = segments.Count > 0, FallbackInstrument = null, FallbackStage = 4,
                            ContractRolloverSegment = true, Run = item.Run
                        });
                        if (segmentEnd >= item.End) break;
                        fallbackCursor = segmentEnd.AddMinutes(1);
                    }
                    if (segments.Count >= minimumRecoverySegments) reason += useMgcDeliverySchedule
                        ? " • using standard MGC Feb/Apr/Jun/Aug/Oct/Dec delivery-month recovery"
                        : " • master expiry calendar unavailable; using automatic " + item.Key + " delivery-month recovery";
                    else segments.Clear();
                }
                catch { segments.Clear(); }
            }
            if (segments.Count < minimumRecoverySegments) return false;
            foreach (HistoricalRequestWorkItem segment in segments) historicalRequestQueue.Enqueue(segment);
            historicalRequestTotal += segments.Count;
            historicalRequestDetails.Add(item.Key + " " + item.Minutes + "M " + (item.IsSetupRequest ? "setup" : "outcome") + " had " + reason + " after the full-range/chart/24x7/continuous path. Queued " + segments.Count + " adjacent date-compatible contract segments; each response will be merged and must still pass the existing strict 1-minute outcome validation.");
            return true;
        }

        private static DateTime NextAutomaticExpiryMonth(string root, DateTime day)
        {
            int[] months = string.Equals(root, "MGC", StringComparison.OrdinalIgnoreCase) ? new[] { 2, 4, 6, 8, 10, 12 } : new[] { 3, 6, 9, 12 };
            DateTime month = new DateTime(day.Year, day.Month, 1);
            for (int offset = 0; offset <= 18; offset++)
            {
                DateTime candidate = month.AddMonths(offset);
                if (!months.Contains(candidate.Month)) continue;
                return new DateTime(candidate.Year, candidate.Month, DateTime.DaysInMonth(candidate.Year, candidate.Month));
            }
            return DateTime.MinValue;
        }

        private static string ContractRolloverSeriesKey(HistoricalRequestWorkItem item)
        {
            return (item == null ? string.Empty : item.Run + "|" + item.Key + "|" + item.Minutes + "|" + (item.IsSetupRequest ? "SETUP" : "OUTCOME"));
        }

        // A non-zero BarsRequest is not necessarily a usable historical receipt.  Some provider/
        // contract combinations return only the most recent fragment of an older request.  Treat
        // a clearly missing left or right side as incomplete, retry the existing full-range
        // fallback order, and block the data rather than building a misleading partial study.
        private bool HasUsableRangeCoverage(HistoricalRequestWorkItem item, List<KeystoneArcBar> list, out string detail)
        {
            detail = string.Empty;
            if (item == null || list == null || list.Count == 0) { detail = "0 bars"; return false; }
            List<KeystoneArcBar> ordered = list.Where(x => x != null && x.Time != DateTime.MinValue).OrderBy(x => x.Time).ToList();
            if (ordered.Count == 0) { detail = "0 timestamped bars"; return false; }
            DateTime expectedStart = item.ContractRolloverSegment || config == null || config.Start == DateTime.MinValue ? item.Start : config.Start;
            DateTime expectedEnd = item.End;
            double requestedMinutes = Math.Max(1, (expectedEnd - expectedStart).TotalMinutes);
            // A rollover boundary can land across a market weekend/holiday. The final merged
            // receipt remains date-audited; this local tolerance merely avoids rejecting a
            // correctly scoped contract interval because its next tradable bar is Sunday evening.
            // Gold frequently starts/ends a requested historical range at a CME weekend or
            // holiday boundary. Give MGC the same bounded four-day tolerance already used for a
            // dated rollover segment, while MNQ retains its proven short full-range tolerance.
            TimeSpan tolerance = item.ContractRolloverSegment || string.Equals(item.Key, "MGC", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromDays(4)
                : TimeSpan.FromMinutes(Math.Max(90, Math.Min(480, requestedMinutes / 10.0)));
            DateTime first = ordered.First().Time;
            DateTime last = ordered.Last().Time;
            bool missingLeft = first > expectedStart.Add(tolerance);
            bool missingRight = last < expectedEnd.Subtract(tolerance);
            if (!missingLeft && !missingRight) return true;
            detail = "partial receipt " + first.ToString("yyyy-MM-dd HH:mm") + " → " + last.ToString("yyyy-MM-dd HH:mm") + "; expected approximately " + expectedStart.ToString("yyyy-MM-dd HH:mm") + " → " + expectedEnd.ToString("yyyy-MM-dd HH:mm");
            return false;
        }

        // MNQ's known-good direct loader must accept its provider's non-zero merged receipts as
        // returned. MGC is isolated because the screenshot shows its range-start dated contract
        // supplying only the first month of a multi-year selection. A non-zero but incomplete MGC
        // receipt is not usable evidence for a range study, so it advances only the existing MGC
        // fallback order: same-contract 24x7, continuous, then dated delivery segments.
        private bool RequiresMgcPartialRangeRecovery(HistoricalRequestWorkItem item, List<KeystoneArcBar> list, out string detail)
        {
            detail = string.Empty;
            if (item == null || !string.Equals(item.Key, "MGC", StringComparison.OrdinalIgnoreCase) || item.ContractRolloverSegment || list == null || list.Count == 0) return false;
            if (HasUsableRangeCoverage(item, list, out detail)) return false;
            detail = "MGC " + detail;
            return true;
        }

        private void CompleteRequestOnLabThread(HistoricalRequestWorkItem item, List<KeystoneArcBar> list, ErrorCode error, string message)
        {
            if (item == null) return;
            string key = item.Key; bool isSetupRequest = item.IsSetupRequest; long run = item.Run;
            if (run != generation) return;
            pendingRequests = Math.Max(0, pendingRequests - 1); historicalRequestCompleted++; historicalRequestActive = false;
            bool emptyReceipt = list == null || list.Count == 0;
            string failureReason = error != ErrorCode.NoError ? ("error " + error + " " + message) : "0 bars";
            string partialCoverage = string.Empty;
            bool incompleteMgcReceipt = error == ErrorCode.NoError && !emptyReceipt && RequiresMgcPartialRangeRecovery(item, list, out partialCoverage);
            if (incompleteMgcReceipt) failureReason = partialCoverage;
            // Keep a non-zero BarsRequest receipt exactly as NinjaTrader returned it. In
            // particular, do not reintroduce a broad coverage gate that can discard valid MNQ
            // merged history. MGC alone advances through its already-defined recovery sequence
            // when a non-zero response covers only a fragment of the requested range.
            bool requiresRecovery = error != ErrorCode.NoError || emptyReceipt || incompleteMgcReceipt;
            if (requiresRecovery && QueueSingleHistoricalFallback(item, failureReason))
            {
                UpdateUi("RETRYING " + key + " " + item.Minutes + "M HISTORY • controlled " + (incompleteMgcReceipt ? "partial-range" : "zero-bar") + " fallback", Gold);
                StartNextHistoricalRequest();
                return;
            }
            if (requiresRecovery && QueueDateCompatibleRolloverSegments(item, failureReason))
            {
                UpdateUi("RECOVERING " + key + " " + item.Minutes + "M HISTORY • automatic dated-contract segments", Gold);
                StartNextHistoricalRequest();
                return;
            }
            if (requiresRecovery)
            {
                if (key == "MNQ") { if (isSetupRequest) mnqSetupBars.Clear(); else mnqBars.Clear(); } else { if (isSetupRequest) mgcSetupBars.Clear(); else mgcBars.Clear(); }
                historicalRequestDetails.Add(key + " " + (isSetupRequest ? config.SetupMinutes + "M setup" : "1M outcome") + " request failed after controlled " + (incompleteMgcReceipt ? "partial-range" : "zero-bar") + " recovery • " + failureReason);
                UpdateUi("DATA ERROR " + key + " " + (isSetupRequest ? config.SetupMinutes + "M SETUP" : "1M OUTCOME") + " • " + failureReason, Red);
            }
            else if (key == "MNQ")
            {
                if (isSetupRequest) mnqSetupBars = MergeRequestedBars(item.Append ? mnqSetupBars : null, list); else mnqBars = MergeRequestedBars(item.Append ? mnqBars : null, list);
                historicalRequestDetails.Add("MNQ " + (isSetupRequest ? config.SetupMinutes + "M setup" : "1M outcome") + " response: " + (list == null ? 0 : list.Count) + " bars.");
            }
            else
            {
                if (isSetupRequest) mgcSetupBars = MergeRequestedBars(item.Append ? mgcSetupBars : null, list); else mgcBars = MergeRequestedBars(item.Append ? mgcBars : null, list);
                historicalRequestDetails.Add("MGC " + (isSetupRequest ? config.SetupMinutes + "M setup" : "1M outcome") + " response: " + (list == null ? 0 : list.Count) + " bars.");
            }
            UpdateUi("LOADING NINJATRADER HISTORY • " + historicalRequestCompleted + " / " + Math.Max(historicalRequestTotal, historicalRequestCompleted) + " request segments completed", Gold);
            if (pendingRequests == 0) StartNextHistoricalRequest();
            else UpdateWorkflowState();
        }

        private static List<KeystoneArcBar> MergeRequestedBars(List<KeystoneArcBar> existing, List<KeystoneArcBar> incoming)
        {
            if (existing == null || existing.Count == 0) return incoming == null ? new List<KeystoneArcBar>() : incoming.OrderBy(x => x.Time).ToList();
            if (incoming == null || incoming.Count == 0) return existing.OrderBy(x => x.Time).ToList();
            return existing.Concat(incoming).GroupBy(x => x.Time).Select(x => x.Last()).OrderBy(x => x.Time).ToList();
        }

        private void FinalizeRequestedDataState()
        {
            bool requiresMnq = config.Scope == "MNQ" || config.Scope == "BOTH";
            bool requiresMgc = config.Scope == "MGC" || config.Scope == "BOTH";
            // Never require a 1M series merely to expose setup marks.  It is required later,
            // and independently, to enable target/stop math.
            bool mnqOk = !requiresMnq || mnqSetupBars.Count > 0;
            bool mgcOk = !requiresMgc || mgcSetupBars.Count > 0;
            bool ready = mnqOk && mgcOk;
            if (ready)
            {
                if (requiresMnq)
                {
                    if (config.SetupMinutes == 1 && mnqSetupBars.Count > 0)
                    {
                        mnqBars = new List<KeystoneArcBar>(mnqSetupBars);
                        mnqOutcomeMatchesSetup = true;
                        config.MnqOutcomeTimeOffsetMinutes = 0;
                        mnqOutcomeValidation = "MNQ 1M study: the direct selected 1M setup series is the outcome series • VERIFIED";
                        config.MnqOutcomeSource = mnqSetupFromOpenChart ? "EXACT OPEN 1M CHART • SHARED SETUP/OUTCOME" : "DIRECT 1M BARSREQUEST • SHARED SETUP/OUTCOME";
                    }
                    else
                    {
                        mnqOutcomeMatchesSetup = ValidateOutcomeSeries("MNQ", mnqBars, mnqSetupBars, out mnqOutcomeValidation, out config.MnqOutcomeTimeOffsetMinutes);
                        if (!mnqOutcomeMatchesSetup && TryDeriveVerifiedSetupFromOutcome("MNQ", mnqBars, out mnqSetupBars, out mnqOutcomeValidation, out config.MnqOutcomeTimeOffsetMinutes))
                        {
                            mnqOutcomeMatchesSetup = true;
                            mnqSetupDerivedFromOpenOneMinute = true;
                            mnqSetupFromOpenChart = mnqOutcomeFromOpenChart;
                            historicalRequestDetails.Add("MNQ direct selected-timeframe receipt did not reconcile to its direct 1M receipt; verified " + config.SetupMinutes + "M setup bars were derived from the same direct 1M source instead. The original selected-timeframe receipt remains in the request diagnostics.");
                        }
                    }
                    if (mnqOutcomeMatchesSetup && config.SetupMinutes != 1) config.MnqOutcomeSource = mnqSetupDerivedFromOpenOneMinute ? (mnqOutcomeFromOpenChart ? "EXACT OPEN 1M • DERIVED SETUP BARS" : "DIRECT 1M • DERIVED SETUP BARS") : (mnqOutcomeFromOpenChart ? "EXACT OPEN 1M CHART" : "VERIFIED 1M • BARSREQUEST");
                    else
                    {
                        if (!mnqOutcomeMatchesSetup)
                        {
                            config.MnqOutcomeTimeOffsetMinutes = 0;
                            config.MnqOutcomeSource = "UNVERIFIED 1M • OUTCOME MATH DISABLED";
                            mnqOutcomeValidation += " • No setup-bar fallback is permitted: P/L and pool math remain disabled until exact 1M aggregation is proven.";
                        }
                    }
                }
                if (requiresMgc)
                {
                    if (config.SetupMinutes == 1 && mgcSetupBars.Count > 0)
                    {
                        mgcBars = new List<KeystoneArcBar>(mgcSetupBars);
                        mgcOutcomeMatchesSetup = true;
                        config.MgcOutcomeTimeOffsetMinutes = 0;
                        mgcOutcomeValidation = "MGC 1M study: the direct selected 1M setup series is the outcome series • VERIFIED";
                        config.MgcOutcomeSource = mgcSetupFromOpenChart ? "EXACT OPEN 1M CHART • SHARED SETUP/OUTCOME" : "DIRECT 1M BARSREQUEST • SHARED SETUP/OUTCOME";
                    }
                    else
                    {
                        mgcOutcomeMatchesSetup = ValidateOutcomeSeries("MGC", mgcBars, mgcSetupBars, out mgcOutcomeValidation, out config.MgcOutcomeTimeOffsetMinutes);
                        if (!mgcOutcomeMatchesSetup && TryDeriveVerifiedSetupFromOutcome("MGC", mgcBars, out mgcSetupBars, out mgcOutcomeValidation, out config.MgcOutcomeTimeOffsetMinutes))
                        {
                            mgcOutcomeMatchesSetup = true;
                            mgcSetupDerivedFromOpenOneMinute = true;
                            mgcSetupFromOpenChart = mgcOutcomeFromOpenChart;
                            historicalRequestDetails.Add("MGC direct selected-timeframe receipt did not reconcile to its direct 1M receipt; verified " + config.SetupMinutes + "M setup bars were derived from the same direct 1M source instead. The original selected-timeframe receipt remains in the request diagnostics.");
                        }
                    }
                    if (mgcOutcomeMatchesSetup && config.SetupMinutes != 1) config.MgcOutcomeSource = mgcSetupDerivedFromOpenOneMinute ? (mgcOutcomeFromOpenChart ? "EXACT OPEN 1M • DERIVED SETUP BARS" : "DIRECT 1M • DERIVED SETUP BARS") : (mgcOutcomeFromOpenChart ? "EXACT OPEN 1M CHART" : "VERIFIED 1M • BARSREQUEST");
                    else
                    {
                        if (!mgcOutcomeMatchesSetup)
                        {
                            config.MgcOutcomeTimeOffsetMinutes = 0;
                            config.MgcOutcomeSource = "UNVERIFIED 1M • OUTCOME MATH DISABLED";
                            mgcOutcomeValidation += " • No setup-bar fallback is permitted: P/L and pool math remain disabled until exact 1M aggregation is proven.";
                        }
                    }
                }
                config.OutcomeModelEnabled = (!requiresMnq || mnqOutcomeMatchesSetup) && (!requiresMgc || mgcOutcomeMatchesSetup) ? 1 : 0;
            }
            historicalDataReceipt = BuildHistoricalDataReceipt();
            if (ready)
            {
                if (config.OutcomeModelEnabled == 1)
                    UpdateUi(config.SetupMinutes == 1 ? "DATA READY • DIRECT 1-MINUTE SETUP/OUTCOME SERIES LOADED • BUILDING THE SETUP LEDGER" : "DATA READY • SELECTED-TIMEFRAME SETUPS + DIRECT 1M PRICE PATH VERIFIED • BUILDING THE SETUP LEDGER", Green);
                else
                    UpdateUi("SETUP BARS READY • BUILDING THE SETUP LEDGER • THE SEPARATE DIRECT 1M PRICE PATH DID NOT VERIFY, SO ONLY WIN/LOSS AND POOL MATH ARE BLOCKED", Gold);
            }
            else
            {
                string missing = (!mnqOk ? "MNQ" : string.Empty) + ((!mnqOk && !mgcOk) ? " + " : string.Empty) + (!mgcOk ? "MGC" : string.Empty);
                string diagnostic = string.Join(" | ", historicalRequestDetails.Where(x => x.StartsWith("MGC", StringComparison.OrdinalIgnoreCase) || x.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)).Take(24));
                if (eventText != null) eventText.Text = "DATA INCOMPLETE • " + missing + "\n\nREQUEST DIAGNOSTICS\n" + (string.IsNullOrWhiteSpace(diagnostic) ? "No per-request diagnostic was returned." : diagnostic);
                // A failed receipt must not strand the configuration behind the one-submit lock.
                // The user can correct a date/chart/history issue and press START RESEARCH again;
                // a retry clears the failed receipt before issuing the next independent request.
                researchSubmissionLocked = false;
                UpdateUi("DATA INCOMPLETE • " + missing + " RETURNED 0 BARS AFTER CONTROLLED FULL-RANGE RETRIES • THE COMPLETE REQUEST DIAGNOSTICS ARE SHOWN ABOVE • CORRECT THE DATE/OPEN-CHART HISTORY IF NEEDED, THEN CLICK START RESEARCH AGAIN; NEW TEST IS NOT REQUIRED", Red);
            }
            EndBusy();
            bool startDetection = ready && autoRunResearchAfterLoad;
            autoRunResearchAfterLoad = false;
            UpdateWorkflowState();
            if (startDetection) RunResearch();
        }

        private string BuildHistoricalDataReceipt()
        {
            var sb = new StringBuilder();
            sb.Append("DATA SOURCE: selected setup bars are copied from a matching open NinjaTrader chart when available; otherwise NinjaTrader BarsRequest is used. Each selected timeframe (1M, 5M, 15M, 30M, 60M, or 240M) is loaded directly. A separate direct 1-minute Last-bar series is used only for exact entry/target/stop timing when the selected setup timeframe is above 1M; 1M studies reuse their direct selected series. Trading Hours: Default 24 x 7 | Playback: not used. ");
            sb.Append("Selected test date(s): ").Append(SelectedTestDateLabel()).Append(". ");
            if (config.Scope == "MNQ" || config.Scope == "BOTH") { AppendHistoricalReceiptSegment(sb, "MNQ outcome (" + config.MnqOutcomeSource + ")", config.MnqName, mnqBars); AppendHistoricalReceiptSegment(sb, "MNQ " + config.SetupMinutes + "M setup" + (mnqSetupDerivedFromOpenOneMinute ? " (DERIVED FROM EXACT OPEN 1M)" : (mnqSetupFromOpenChart ? " (OPEN CHART EXACT)" : " (BARSREQUEST)")), config.MnqName, mnqSetupBars); }
            if (config.Scope == "MGC" || config.Scope == "BOTH") { AppendHistoricalReceiptSegment(sb, "MGC outcome (" + config.MgcOutcomeSource + ")", config.MgcName, mgcBars); AppendHistoricalReceiptSegment(sb, "MGC " + config.SetupMinutes + "M setup" + (mgcSetupDerivedFromOpenOneMinute ? " (DERIVED FROM EXACT OPEN 1M)" : (mgcSetupFromOpenChart ? " (OPEN CHART EXACT)" : " (BARSREQUEST)")), config.MgcName, mgcSetupBars); }
            if (config.Scope == "MNQ" || config.Scope == "BOTH") sb.Append(mnqOutcomeValidation).Append(". ");
            if (config.Scope == "MGC" || config.Scope == "BOTH") sb.Append(mgcOutcomeValidation).Append(". ");
            for (int i = 0; i < historicalRequestDetails.Count; i++) sb.Append(historicalRequestDetails[i]).Append(" ");
            sb.Append("Contract selection is automatic: a compatible open chart is preferred; otherwise NinjaTrader resolves a non-expired master-instrument expiry from the requested range end. Exact open-chart bars are preferred whenever their timeframe and coverage match; if only exact open 1M history is available, BH setup bars may be aligned and derived from it, then must pass strict OHLC reconciliation. Futures rollover behavior follows the NinjaTrader global merge policy.");
            return sb.ToString();
        }

        private bool ValidateOutcomeSeries(string symbol, List<KeystoneArcBar> oneMinute, List<KeystoneArcBar> setup, out string detail, out int chosenOffsetMinutes)
        {
            detail = symbol + " outcome validation unavailable"; chosenOffsetMinutes = 0;
            if (oneMinute == null || oneMinute.Count == 0)
            {
                detail = symbol + " 1M validation failed: NinjaTrader returned 0 one-minute bars for the resolved contract/template.";
                return false;
            }
            if (setup == null || setup.Count == 0)
            {
                detail = symbol + " 1M validation failed: no selected " + config.SetupMinutes + "M setup bars were loaded.";
                return false;
            }
            int bestMatched = -1, bestChecked = 0, bestMissing = int.MaxValue, bestOffset = 0;
            string bestDifference = string.Empty;
            const double tolerance = 0.0001;
            // NinjaTrader data adapters can timestamp a completed N-minute bar either at its
            // opening minute or at its closing minute.  A 5M bar stamped 09:30 may therefore
            // aggregate 09:30..09:34 or 09:26..09:30.  The former ±2-minute probe falsely
            // rejected every valid 5M/15M/1H/4H series that used closing timestamps.
            int earliestOffset = -Math.Max(0, config.SetupMinutes - 1);
            var offsets = new List<int> { 0 };
            for (int offset = -1; offset >= earliestOffset; offset--) offsets.Add(offset);
            for (int offset = 1; offset <= 2; offset++) offsets.Add(offset);
            foreach (int offset in offsets)
            {
                int checkedBars = 0, matchedBars = 0, missingBars = 0; string firstDifference = string.Empty;
                // Do not treat a selected range boundary as a data mismatch. The first selected
                // bar can legitimately begin before config.Start when a connection timestamps an
                // N-minute bar at completion. The request carries left context; bars that still
                // predate the returned 1M series cannot fairly be tested and are skipped.
                DateTime outcomeFirst = oneMinute.Min(x => x.Time);
                foreach (KeystoneArcBar setupBar in setup.Where(x => x.Time >= config.Start && x.Time <= config.End).Where(x =>
                {
                    DateTime earliest = x.Time.AddMinutes(earliestOffset);
                    return earliest >= outcomeFirst;
                }).Take(32))
                {
                    DateTime componentStart = setupBar.Time.AddMinutes(offset), componentEnd = componentStart.AddMinutes(config.SetupMinutes);
                    List<KeystoneArcBar> component = oneMinute.Where(x => x.Time >= componentStart && x.Time < componentEnd).OrderBy(x => x.Time).ToList();
                    if (component.Count < config.SetupMinutes) { missingBars++; if (string.IsNullOrEmpty(firstDifference)) firstDifference = setupBar.Time.ToString("HH:mm") + " has " + component.Count + "/" + config.SetupMinutes + " 1M components at offset " + offset; continue; }
                    checkedBars++;
                    double open = component.First().Open, high = component.Max(x => x.High), low = component.Min(x => x.Low), close = component.Last().Close;
                    if (Math.Abs(open - setupBar.Open) <= tolerance && Math.Abs(high - setupBar.High) <= tolerance && Math.Abs(low - setupBar.Low) <= tolerance && Math.Abs(close - setupBar.Close) <= tolerance) matchedBars++;
                    else if (string.IsNullOrEmpty(firstDifference)) firstDifference = setupBar.Time.ToString("HH:mm") + " setup O/H/L/C " + setupBar.Open.ToString("0.00") + "/" + setupBar.High.ToString("0.00") + "/" + setupBar.Low.ToString("0.00") + "/" + setupBar.Close.ToString("0.00") + " vs 1M " + open.ToString("0.00") + "/" + high.ToString("0.00") + "/" + low.ToString("0.00") + "/" + close.ToString("0.00");
                }
                if (matchedBars > bestMatched || (matchedBars == bestMatched && missingBars < bestMissing)) { bestMatched = matchedBars; bestChecked = checkedBars; bestMissing = missingBars; bestOffset = offset; bestDifference = firstDifference; }
            }
            bool pass = bestChecked >= 3 && bestMatched == bestChecked && bestMissing == 0;
            chosenOffsetMinutes = pass ? bestOffset : 0;
            string alignment = bestOffset == 0 ? "opening-minute timestamps" : "normalized " + (bestOffset > 0 ? "+" : string.Empty) + bestOffset + " minute timestamp offset";
            detail = symbol + " 1M↔" + config.SetupMinutes + "M validation: " + bestMatched + "/" + bestChecked + " exact OHLC matches" + (bestMissing > 0 ? "; " + bestMissing + " setup bars missing 1M components" : string.Empty) + (pass ? " • MATCHED • " + alignment : " • MISMATCH — outcome math blocked" + (string.IsNullOrWhiteSpace(bestDifference) ? string.Empty : " • first difference: " + bestDifference));
            return pass;
        }

        private bool TryDeriveVerifiedSetupFromOutcome(string symbol, List<KeystoneArcBar> oneMinute, out List<KeystoneArcBar> derivedSetup, out string detail, out int chosenOffsetMinutes)
        {
            derivedSetup = new List<KeystoneArcBar>();
            detail = symbol + " direct 1M derivation unavailable";
            chosenOffsetMinutes = 0;
            if (config == null || config.SetupMinutes <= 1 || oneMinute == null || oneMinute.Count == 0) return false;
            derivedSetup = AggregateExactOneMinuteSetupBars(oneMinute, config.SetupMinutes);
            if (derivedSetup.Count == 0)
            {
                detail = symbol + " direct 1M derivation failed: no consecutive " + config.SetupMinutes + "M groups were available.";
                return false;
            }
            string derivedDetail;
            int derivedOffset;
            bool verified = ValidateOutcomeSeries(symbol, oneMinute, derivedSetup, out derivedDetail, out derivedOffset);
            chosenOffsetMinutes = derivedOffset;
            detail = verified
                ? symbol + " selected-timeframe receipt differed from direct 1M; " + config.SetupMinutes + "M setup bars were rebuilt from the same direct 1M receipt and strictly verified • " + derivedDetail
                : symbol + " selected-timeframe receipt differed from direct 1M and derived " + config.SetupMinutes + "M verification also failed • " + derivedDetail;
            if (!verified) derivedSetup = new List<KeystoneArcBar>();
            return verified;
        }

        private static void AppendHistoricalReceiptSegment(StringBuilder sb, string label, string contract, List<KeystoneArcBar> bars)
        {
            if (bars == null || bars.Count == 0) { sb.Append(label).Append(" ").Append(contract).Append(": 0 bars returned. "); return; }
            DateTime first = bars.Min(x => x.Time); DateTime last = bars.Max(x => x.Time);
            sb.Append(label).Append(" ").Append(contract).Append(": ").Append(bars.Count.ToString("N0")).Append(" bars returned; first ").Append(first.ToString("yyyy-MM-dd HH:mm")).Append("; last ").Append(last.ToString("yyyy-MM-dd HH:mm")).Append(". ");
        }

        private void DispatchToLab(Action action)
        {
            if (action == null) return;
            Dispatcher dispatcher = window == null ? null : window.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) { action(); return; }
            dispatcher.BeginInvoke(action);
        }

        private void RunResearch()
        {
            if (isProcessing) { UpdateUi("PROCESSING IS ALREADY RUNNING • WAIT FOR STATUS", Gold); return; }
            if (!ConfigurationStillApproved()) { UpdateUi("CONFIGURATION CHANGED • CONFIRM STEP 1 AND REQUEST HISTORY AGAIN", Gold); UpdateWorkflowState(); return; }
            if (!HasSelectedData()) { UpdateUi("STEP 2 REQUIRED • REQUEST COMPLETE HISTORY FOR THE SELECTED SCOPE FIRST", Red); UpdateWorkflowState(); return; }
            KeystoneArcRunConfig workerConfig = CloneConfig(config);
            isProcessing = true;
            bool asianBacktest = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            BeginBusy(asianBacktest ? "RUNNING ASIAN DAILY-CYCLE BACKTEST" : "BUILDING SETUP LEDGER");
            int setupCount = mnqSetupBars.Count + mgcSetupBars.Count;
            UpdateUi((asianBacktest ? "RUNNING ASIAN DAILY-CYCLE BACKTEST IN BACKGROUND" : "BUILDING DETECTOR-QUALIFIED SETUP LEDGER IN BACKGROUND") + " • " + setupCount.ToString("N0") + " DIRECT " + config.SetupMinutes + "M BARS • outcome math " + (config.OutcomeModelEnabled == 1 ? "verified" : "blocked pending matching 1M"), Gold);
            UpdateWorkflowState();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeystoneArcEvent> generated;
                List<KeystoneArcVirtualAccount> generatedAccounts = new List<KeystoneArcVirtualAccount>();
                try
                {
                    generated = BuildDetectorLedger(workerConfig);
                    // Allocation is intentionally not run during detection; Step 3 applies the selected prop scenario.
                }
                catch (Exception ex) { DispatchToLab(delegate { isProcessing = false; EndBusy(); UpdateUi("DETECTION ERROR • " + ex.Message, Red); UpdateWorkflowState(); }); return; }
                DispatchToLab(delegate
                {
                    events = generated ?? new List<KeystoneArcEvent>();
                    for (int i = 0; i < events.Count; i++)
                    {
                        events[i].ReviewState = "ACCEPTED";
                        if (asianBacktest)
                            events[i].ReviewNote = (events[i].ReviewNote ?? string.Empty) + (string.IsNullOrWhiteSpace(events[i].ReviewNote) ? "" : " • ") + "AUTO-INCLUDED: resolved Asian daily-cycle leg";
                        else events[i].ReviewNote = "AUTO-INCLUDED: detector-qualified setup";
                    }
                    accounts = generatedAccounts ?? new List<KeystoneArcVirtualAccount>(); researchRunCompleted = true; unsavedResearch = events.Count > 0; isProcessing = false;
                    KeystoneArcHub.ShowChartMarks = chartMarksBox == null || chartMarksBox.IsChecked != false;
                    KeystoneArcHub.Publish(new List<KeystoneArcEvent>(), config);
                    RenderEvents(); RebuildReviewList(); BuildMathReader(); RefreshLifecycleInputState(); RenderResearchFindings();
                    if (accounts.Count > 0) { RenderPoolLedger(); RenderLifecycle(); RenderPoolDashboard(); }
                    EndBusy();
                    string completion = asianBacktest
                        ? ((config.OutcomeModelEnabled == 1 ? "ASIAN DAILY-CYCLE BACKTEST READY" : "ASIAN BACKTEST DATA UNVERIFIED") + " • " + events.Count + " RESOLVED ENTRY / REVERSAL / EXIT LEGS • READY FOR COPY-ACCOUNT SCENARIO")
                        : ((config.OutcomeModelEnabled == 1 ? "RESOLVED OUTCOME LEDGER READY" : "SETUP LEDGER READY • OUTCOMES UNVERIFIED") + " • " + events.Count + " DETECTOR-QUALIFIED SETUPS • ALL ARE ELIGIBLE FOR THE PROP POOL WHEN 1M VALIDATION PASSES");
                    UpdateUi(completion, config.OutcomeModelEnabled == 1 ? Green : Gold);
                    UpdateWorkflowState();
                    if (events.Count > 0 && workspaceTabs != null) workspaceTabs.SelectedIndex = 1;
                });
            });
        }

        // Detection is always driven by the direct selected-timeframe chart bars.  A symbol with
        // unmatched 1M data still produces an auditable setup ledger, but its entries receive no
        // invented win/loss, exit price, or P/L.  This protects chart validation from contaminating
        // the raw setup count with outcome assumptions.
        private List<KeystoneArcEvent> BuildDetectorLedger(KeystoneArcRunConfig baseConfig)
        {
            var output = new List<KeystoneArcEvent>();
            // Asian is not a setup detector. MNQ and MGC must be evaluated together so the
            // combined target, stop, breakeven, daily limit, and copied account-day result are
            // calculated from one real 1-minute cycle—not two unrelated single-symbol ledgers.
            if (string.Equals(baseConfig.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase))
            {
                bool needsMnq = baseConfig.Scope == "MNQ" || baseConfig.Scope == "BOTH";
                bool needsMgc = baseConfig.Scope == "MGC" || baseConfig.Scope == "BOTH";
                if ((needsMnq && !mnqOutcomeMatchesSetup) || (needsMgc && !mgcOutcomeMatchesSetup)) return output;
                List<KeystoneArcBar> combined = (needsMnq ? mnqBars : new List<KeystoneArcBar>()).Concat(needsMgc ? mgcBars : new List<KeystoneArcBar>()).OrderBy(x => x.Time).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
                return KeystoneArcEngine.DetectAndResolve(combined, combined, baseConfig)
                    .OrderBy(x => x.EntryTime).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
            }
            string[] symbols = baseConfig.Scope == "BOTH" ? new[] { "MNQ", "MGC" } : new[] { baseConfig.Scope };
            for (int i = 0; i < symbols.Length; i++)
            {
                string symbol = symbols[i];
                List<KeystoneArcBar> setup = symbol == "MNQ" ? mnqSetupBars : mgcSetupBars;
                List<KeystoneArcBar> oneMinute = symbol == "MNQ" ? mnqBars : mgcBars;
                bool matched = symbol == "MNQ" ? mnqOutcomeMatchesSetup : mgcOutcomeMatchesSetup;
                KeystoneArcRunConfig symbolConfig = CloneConfig(baseConfig);
                symbolConfig.Scope = symbol;
                symbolConfig.OutcomeModelEnabled = matched ? 1 : 0;
                List<KeystoneArcBar> resolverBars = matched ? oneMinute : setup;
                List<KeystoneArcEvent> eventsForSymbol = KeystoneArcEngine.DetectAndResolve(resolverBars, setup, symbolConfig);
                if (!matched)
                {
                    for (int j = 0; j < eventsForSymbol.Count; j++)
                    {
                        KeystoneArcEvent e = eventsForSymbol[j];
                        e.Outcome = "UNVERIFIED 1M";
                        e.GrossPnl = 0;
                        e.ExitPrice = double.NaN;
                        e.ExitTime = DateTime.MinValue;
                        e.PeakAfterEntry = double.NaN;
                        e.TroughAfterEntry = double.NaN;
                        e.TargetTouched = 0;
                        e.StopTouched = 0;
                        e.ReviewNote = "SETUP DETECTED FROM DIRECT " + baseConfig.SetupMinutes + "M BARS • 1M SERIES DID NOT validate; outcome and pool math are blocked.";
                    }
                }
                output.AddRange(eventsForSymbol);
            }
            return output.OrderBy(x => x.TriggerTime).ThenBy(x => x.Symbol).ToList();
        }

        private void IncludeAllDetectedEvents()
        {
            if (events.Count == 0) { UpdateUi("NO DETECTED EVENTS TO INCLUDE", Gold); return; }
            int excluded = events.Count(x => !string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            if (excluded == 0) { UpdateUi("ALL DETECTED SETUPS ARE ALREADY ELIGIBLE FOR THE VIRTUAL POOL", Green); return; }
            if (MessageBox.Show("Restore all " + excluded + " excluded/flagged rows to the virtual pool? Historical outcomes do not change.", "Restore detected setups to pool", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            for (int i = 0; i < events.Count; i++) { events[i].ReviewState = "ACCEPTED"; events[i].ReviewNote = "AUTO-INCLUDED: detector-qualified setup"; }
            RebuildReviewList(); BuildMathReader(); PublishReviewedEvents(); unsavedResearch = true;
            UpdateWorkflowState();
            if (workspaceTabs != null) workspaceTabs.SelectedIndex = 2;
            UpdateUi("ALL DETECTED SETUPS RESTORED TO THE VIRTUAL POOL • NOW CHOOSE THE POOL SETTINGS, THEN RUN THE VIRTUAL POOL", Green);
        }

        private void ResetReviewStates()
        {
            if (events.Count == 0) { UpdateUi("NO EVENTS TO RESET", Gold); return; }
            if (MessageBox.Show("Restore all detected setups to the virtual pool? Historical outcomes remain unchanged.", "Restore detected setups", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            for (int i = 0; i < events.Count; i++) { events[i].ReviewState = "ACCEPTED"; events[i].ReviewNote = "AUTO-INCLUDED: detector-qualified setup"; }
            accounts.Clear(); RebuildReviewList(); BuildMathReader(); KeystoneArcHub.Publish(new List<KeystoneArcEvent>(), config); unsavedResearch = true;
            UpdateUi("ALL DETECTED SETUPS RESTORED TO THE VIRTUAL POOL", Green);
            UpdateWorkflowState();
        }

        private void SimulatePool()
        {
            if (operationBusy || isProcessing) { UpdateUi("WAIT FOR THE CURRENT OPERATION TO FINISH", Gold); return; }
            if (events.Count == 0) { UpdateUi("RUN DETECTION BEFORE CALCULATING RESULTS", Red); return; }
            // Scenario fields are edited after the historical ledger already exists. They must
            // not silently replace the loaded session/range from the configure controls (which
            // can have been reset or changed) before allocation is run.
            DateTime loadedStart = config.Start, loadedEnd = config.End;
            int loadedOneDay = config.OneDayMode;
            string loadedSessionMode = config.SessionMode;
            int loadedCustomStart = config.CustomStart, loadedEndTime = config.EndTime;
            DateTime s, e; if (!ReadConfig(out s, out e)) return;
            config.Start = loadedStart; config.End = loadedEnd; config.OneDayMode = loadedOneDay;
            config.SessionMode = loadedSessionMode; config.CustomStart = loadedCustomStart; config.EndTime = loadedEndTime;
            config.EvaluationEnabled = config.PoolSize == 1 ? -2 : (loadedOneDay == 1 ? -1 : (accountStartModeBox != null && string.Equals(Convert.ToString(accountStartModeBox.SelectedItem), "DIRECT FUNDED", StringComparison.OrdinalIgnoreCase) ? 0 : 1));
            RefreshLifecycleInputState();
            if (config.OutcomeModelEnabled == 0) { UpdateUi("POOL BLOCKED • THE 1-MINUTE OUTCOME SERIES DID NOT MATCH THE SELECTED SETUP CHART • REVIEW THE DATA RECEIPT", Gold); UpdateWorkflowState(); return; }
            List<KeystoneArcEvent> accepted = events.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)).ToList();
            if (accepted.Count == 0) { UpdateUi("NO ELIGIBLE SETUPS REMAIN • RESTORE ONE OR MORE ROWS IN VERIFY ENTRIES BEFORE RUNNING THE POOL", Gold); return; }
            for (int i = 0; i < events.Count; i++) { events[i].AssignedVirtualAccount = string.Empty; events[i].SkipReason = string.Empty; events[i].ConfigurationKey = config.Snapshot(); }
            KeystoneArcRunConfig workerConfig = CloneConfig(config);
            BeginBusy("ASSIGNING " + accepted.Count + " ELIGIBLE SETUPS ACROSS " + workerConfig.PoolSize + " VIRTUAL ACCOUNTS");
            UpdateUi("RUNNING VIRTUAL POOL IN BACKGROUND • " + accepted.Count + " ELIGIBLE SETUPS • " + workerConfig.PoolSize + " ACCOUNTS", Gold);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<KeystoneArcVirtualAccount> result;
                try { result = KeystoneArcEngine.SimulatePool(accepted, workerConfig); }
                catch (Exception ex) { DispatchToLab(delegate { EndBusy(); UpdateUi("POOL SIMULATION ERROR • " + ex.Message, Red); }); return; }
                DispatchToLab(delegate
                {
                    accounts = result ?? new List<KeystoneArcVirtualAccount>();
                    RenderPoolLedger(); RenderLifecycle(); RenderPoolDashboard(); RenderEvents(); unsavedResearch = true;
                    if (runPoolButton != null) runPoolButton.Content = "RECALCULATE VIRTUAL POOL";
                    EndBusy();
                    if (workspaceTabs != null) workspaceTabs.SelectedIndex = 2;
                    UpdateUi("VIRTUAL POOL COMPLETE • " + accepted.Count + " ELIGIBLE SETUPS • " + accounts.Count + " VIRTUAL ACCOUNTS • ILLUSTRATIVE SCENARIO ASSUMPTIONS", Green);
                    UpdateWorkflowState();
                });
            });
        }

        // Clears only allocation and lifecycle results. The source bars, detected ledger,
        // chart evidence, and selected study configuration stay available for a new scenario.
        private void ClearPoolResults()
        {
            if (operationBusy || isProcessing) return;
            if (accounts.Count == 0) { UpdateUi("NO VIRTUAL POOL RESULTS TO CLEAR", Gold); return; }
            if (MessageBox.Show("Clear only the virtual-account allocation and lifecycle results? Detected setups, chart evidence, dates, and parameters will remain.", "Clear virtual pool results", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            accounts.Clear();
            if (runPoolButton != null) runPoolButton.Content = "RUN VIRTUAL POOL";
            for (int i = 0; i < events.Count; i++)
            {
                events[i].AssignedVirtualAccount = string.Empty;
                events[i].SkipReason = string.Empty;
            }
            RenderPoolLedger(); RenderLifecycle(); RenderPoolDashboard(); BuildMathReader();
            unsavedResearch = true;
            UpdateUi("POOL RESULTS CLEARED • DETECTED SETUPS AND CHART EVIDENCE REMAIN READY FOR A NEW VIRTUAL POOL", Green);
            UpdateWorkflowState();
        }

        private void RebuildReviewList()
        {
            reviewRows = events.OrderBy(x => x.TriggerTime).ThenBy(x => x.Symbol).ToList();
            if (reviewList == null) return;
            reviewList.Items.Clear();
            for (int i = 0; i < reviewRows.Count; i++)
            {
                KeystoneArcEvent e = reviewRows[i];
                reviewList.Items.Add((i + 1).ToString("0000") + " | " + PoolDecisionLabel(e).PadRight(18) + " | " + e.Outcome.PadRight(13) + " | " + e.TriggerTime.ToString("yyyy-MM-dd HH:mm") + " | " + e.Symbol + " | " + e.SetupClass.PadRight(3) + " | " + e.Entry.ToString("0.00"));
            }
            if (reviewRows.Count > 0) reviewList.SelectedIndex = 0;
            if (reviewLedgerText != null) reviewLedgerText.Text = "LEDGER: " + reviewRows.Count.ToString("N0") + " OF " + events.Count.ToString("N0") + " CANDIDATES LOADED • SCROLL TO REVIEW ALL";
            UpdateReviewDetail();
        }

        private KeystoneArcEvent SelectedReviewEvent()
        {
            if (reviewList == null || reviewList.SelectedIndex < 0 || reviewList.SelectedIndex >= reviewRows.Count) return null;
            return reviewRows[reviewList.SelectedIndex];
        }

        private void MoveReviewSelection(int direction)
        {
            if (reviewList == null || reviewRows.Count == 0) { UpdateUi("NO REVIEW ROWS AVAILABLE", Gold); return; }
            int current = reviewList.SelectedIndex;
            if (current < 0) current = 0;
            int next = Math.Max(0, Math.Min(reviewRows.Count - 1, current + direction));
            reviewList.SelectedIndex = next;
            UpdateReviewDetail();
        }

        private void GoToReviewRow(TextBox rowBox)
        {
            if (reviewList == null || reviewRows.Count == 0) { UpdateUi("NO REVIEW ROWS AVAILABLE", Gold); return; }
            int oneBased;
            if (rowBox == null || !int.TryParse(rowBox.Text, out oneBased) || oneBased < 1 || oneBased > reviewRows.Count)
            {
                UpdateUi("ROW ERROR • ENTER A NUMBER FROM 1 TO " + reviewRows.Count, Red);
                return;
            }
            reviewList.SelectedIndex = oneBased - 1;
            UpdateReviewDetail();
        }

        private void UpdateReviewDetail()
        {
            KeystoneArcEvent e = SelectedReviewEvent();
            if (e == null)
            {
                if (reviewDetailText != null) reviewDetailText.Text = "No selected event.";
                if (reviewPositionText != null) reviewPositionText.Text = "POSITION: no selected row.";
                return;
            }
            if (reviewPositionText != null) reviewPositionText.Text = "POSITION " + (reviewList.SelectedIndex + 1).ToString("N0") + " OF " + reviewRows.Count.ToString("N0") + " • SELECTED EVENT SHOWN AT RIGHT";
            var sb = new StringBuilder();
            bool outcomeVerified = config.OutcomeModelEnabled == 1 && !string.Equals(e.Outcome, "UNVERIFIED 1M", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine("POOL DECISION: " + PoolDecisionLabel(e) + " • " + (outcomeVerified ? "historical outcome resolved" : "setup placement only — outcome / P&L blocked"));
            sb.AppendLine("EVENT: " + e.Id);
            sb.AppendLine("INSTRUMENT: " + e.Symbol + " | CLASS: " + e.SetupClass + " | TAG: " + e.StrengthTag);
            sb.AppendLine("REFERENCE: " + e.ReferenceTime.ToString("yyyy-MM-dd HH:mm") + " | TRIGGER: " + e.TriggerTime.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("ENTRY: " + e.Entry.ToString("0.00") + " | TARGET: " + e.Target.ToString("0.00") + " | STOP: " + e.Stop.ToString("0.00"));
            if (outcomeVerified) sb.AppendLine("MODEL EXIT: " + e.ExitTime.ToString("yyyy-MM-dd HH:mm") + " | PRICE " + (double.IsNaN(e.ExitPrice) ? "n/a" : e.ExitPrice.ToString("0.00")) + " | " + e.Outcome + " | " + e.GrossPnl.ToString("C0"));
            else sb.AppendLine("OUTCOME: UNVERIFIED • no target / stop / P&L model is shown until the matching 1-minute series validates.");
            sb.AppendLine("OUTCOME SOURCE: " + (e.Symbol == "MGC" ? config.MgcOutcomeSource : config.MnqOutcomeSource));
            sb.AppendLine("SESSION ORDER: " + e.SessionOrder);
            if (!double.IsNaN(e.FvgLower) && !double.IsNaN(e.FvgUpper)) sb.AppendLine("FVG: " + e.FvgLower.ToString("0.00") + " → " + e.FvgUpper.ToString("0.00") + " | FORMED " + e.FvgFormedTime.ToString("yyyy-MM-dd HH:mm"));
            else sb.AppendLine("FVG: none recorded for this BH event");
            sb.AppendLine("NOTE: " + (e.ReviewNote ?? string.Empty));
            if (reviewDetailText != null) reviewDetailText.Text = sb.ToString();
            if (reviewNoteBox != null) reviewNoteBox.Text = e.ReviewNote ?? string.Empty;
        }

        private void SetSelectedReviewState(string state)
        {
            KeystoneArcEvent e = SelectedReviewEvent();
            if (e == null) { UpdateUi("SELECT AN EVENT FIRST", Gold); return; }
            e.ReviewState = state;
            e.ReviewNote = reviewNoteBox == null ? string.Empty : reviewNoteBox.Text;
            unsavedResearch = true;
            RebuildReviewList(); RenderEvents(); BuildMathReader();
            UpdateWorkflowState();
            if (state == "ACCEPTED" && config.OutcomeModelEnabled == 1 && workspaceTabs != null) workspaceTabs.SelectedIndex = 2;
            UpdateUi(state == "ACCEPTED" ? (config.OutcomeModelEnabled == 1 ? "EVENT INCLUDED IN VIRTUAL POOL • CHOOSE POOL SETTINGS, THEN RUN THE POOL" : "EVENT MARKED FOR POOL • OUTCOME/P&L STAYS BLOCKED UNTIL THE 1M SERIES MATCHES THE CHART") : "POOL DECISION UPDATED • HISTORICAL OUTCOME IS UNCHANGED", state == "ACCEPTED" ? (config.OutcomeModelEnabled == 1 ? Green : Gold) : (state == "REJECTED" ? Red : Gold));
        }

        // Verification is read-only because detector-qualified setups are included by default.
        // This is the single ordered handoff from Step 2 to Step 3.
        private void ProceedFromVerifyToSimulation()
        {
            if (events.Count == 0) { UpdateUi("NO DETECTED SETUPS ARE READY TO PROCEED", Gold); return; }
            if (workspaceTabs != null) workspaceTabs.SelectedIndex = 2;
            UpdateUi(config.OutcomeModelEnabled == 1
                ? "SIMULATION SETTINGS READY • CHOOSE THE ACCOUNT SCENARIO, THEN RUN VIRTUAL POOL"
                : "SIMULATION SETTINGS AND CHART SETUPS ARE READY • VIRTUAL-POOL CALCULATION REMAINS DISABLED UNTIL THE DIRECT 1M PRICE PATH IS VERIFIED", config.OutcomeModelEnabled == 1 ? Green : Gold);
        }

        private void PublishReviewedEvents()
        {
            List<KeystoneArcEvent> accepted = events.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)).ToList();
            ApplyChartReviewOptions();
            KeystoneArcHub.Publish(accepted, config);
            UpdateUi("PUBLISHED " + accepted.Count + " ELIGIBLE SETUPS TO THE KEYSTONE ARC CHART STUDY", accepted.Count > 0 ? Green : Gold);
        }

        private void PublishChartReview()
        {
            if (events.Count == 0) { UpdateUi("RUN DETECTION BEFORE CHART REVIEW", Red); return; }
            string scope = chartReviewScopeBox == null ? "SELECTED CANDIDATE" : Convert.ToString(chartReviewScopeBox.SelectedItem);
            List<KeystoneArcEvent> selected;
            if (string.Equals(scope, "ALL DETECTED CANDIDATES", StringComparison.OrdinalIgnoreCase)) selected = new List<KeystoneArcEvent>(events);
            else if (string.Equals(scope, "ACCEPTED ONLY", StringComparison.OrdinalIgnoreCase)) selected = events.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)).ToList();
            else
            {
                KeystoneArcEvent current = SelectedReviewEvent();
                selected = current == null ? new List<KeystoneArcEvent>() : new List<KeystoneArcEvent> { current };
            }
            if (selected.Count == 0) { UpdateUi("CHART REVIEW HAS NO EVENTS FOR THE CHOSEN SCOPE", Gold); return; }
            ApplyChartReviewOptions();
            KeystoneArcHub.Publish(selected, config);
            UpdateUi("CHART REVIEW PUBLISHED • " + selected.Count + " EVENT(S) • OPEN A MATCHING " + config.SetupMinutes + "-MINUTE " + config.Scope + " CHART WITH KEYSTONE ARC 5M CHART STUDY • SIMPLE ENTRY + WIN/LOSS MARKS", Green);
        }

        private void OpenEvidenceChart()
        {
            if (!HasSelectedData() || events.Count == 0) { UpdateUi("EVIDENCE CHART REQUIRES COMPLETED DATA + DETECTED EVENTS", Gold); return; }
            if (evidenceWindow != null) { if (evidenceWindow.IsVisible) evidenceWindow.Activate(); return; }
            KeystoneArcEvent selected = SelectedReviewEvent();
            string preferredSymbol = selected == null ? (config.Scope == "MGC" ? "MGC" : "MNQ") : selected.Symbol;
            DateTime preferredDay = selected == null ? KeystoneArcEngine.SessionGroupingDate(config.Start, config) : KeystoneArcEngine.SessionGroupingDate(selected.TriggerTime, config);
            var w = new Window { Title = "KEYSTONE ARC • EVIDENCE CHART", Width = 1370, Height = 820, MinWidth = 920, MinHeight = 620, Background = Bg, Foreground = Text, ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true };
            var root = new Grid { Margin = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var compactHeader = new StackPanel { Margin = new Thickness(2) };
            var headerLine = new Grid(); headerLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); headerLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerLine.Children.Add(Txt("KEYSTONE ARC • DIRECT EVIDENCE CHART", Cyan, 18, FontWeights.Bold));
            evidenceControlsToggle = Btn("SHOW CONTROLS", Blue); evidenceControlsToggle.Width = 130; evidenceControlsToggle.Height = 28; evidenceControlsToggle.FontSize = 10; evidenceControlsToggle.Click += delegate { ToggleEvidenceControls(); }; Grid.SetColumn(evidenceControlsToggle, 1); headerLine.Children.Add(evidenceControlsToggle);
            compactHeader.Children.Add(headerLine);
            evidenceStatusText = Txt("CHOOSE A DATE INSIDE THE LOADED RANGE, THEN DRAW THE SELECTED SETUP BARS.", Gold, 10, FontWeights.Bold); compactHeader.Children.Add(evidenceStatusText);
            evidenceMetricsText = Txt("SESSION TOTALS • waiting for selected instrument and date", Muted, 11, FontWeights.Bold);
            evidenceMetricsText.FontFamily = new FontFamily("Consolas");
            evidenceStudyText = Txt(BuildEvidenceStudyLabel(), Text, 11, FontWeights.Bold);
            compactHeader.Children.Add(new Border { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(0, 4, 0, 2), HorizontalAlignment = HorizontalAlignment.Left, Child = evidenceStudyText });
            root.Children.Add(compactHeader);
            var top = Stack();
            top.Children.Add(Txt("EVIDENCE TIMEFRAME: choose 1M, 5M, 15M, 30M, 60M, or 240M. A different timeframe is loaded and detected as a temporary chart preview; it never rewrites the saved Step 1 ledger or final report.", Gold, 10, FontWeights.Bold));
            evidenceInstrumentTabs = new TabControl { Background = Panel, BorderBrush = Blue, BorderThickness = new Thickness(1), Margin = new Thickness(2, 6, 2, 2) };
            if (config.Scope == "MNQ" || config.Scope == "BOTH") evidenceInstrumentTabs.Items.Add(new TabItem { Header = "MNQ EVIDENCE", Content = new Border { Height = 1, Background = Panel } });
            if (config.Scope == "MGC" || config.Scope == "BOTH") evidenceInstrumentTabs.Items.Add(new TabItem { Header = "MGC EVIDENCE", Content = new Border { Height = 1, Background = Panel } });
            evidenceInstrumentTabs.SelectionChanged += delegate
            {
                if (evidenceInstrumentBox == null || evidenceInstrumentTabs.SelectedItem == null) return;
                TabItem selectedTab = evidenceInstrumentTabs.SelectedItem as TabItem;
                string header = selectedTab == null ? string.Empty : Convert.ToString(selectedTab.Header);
                if (header.StartsWith("MNQ")) evidenceInstrumentBox.SelectedItem = "MNQ";
                else if (header.StartsWith("MGC")) evidenceInstrumentBox.SelectedItem = "MGC";
                if (evidenceWindow != null && evidenceDateBox != null) RequestEvidenceBars();
            };
            // Instrument tabs remain visible even while the detailed controls are collapsed.
            compactHeader.Children.Add(evidenceInstrumentTabs);
            var controls = new UniformGrid { Columns = 4, Margin = new Thickness(2) };
            evidenceInstrumentBox = Select("MNQ", "MGC"); evidenceInstrumentBox.SelectedItem = preferredSymbol;
            evidenceTimeframeBox = Select("1M", "5M", "15M", "30M", "60M", "240M"); evidenceTimeframeBox.SelectedItem = config.SetupMinutes.ToString(CultureInfo.InvariantCulture) + "M";
            evidenceTimeframeBox.SelectionChanged += delegate { if (evidenceWindow != null && evidenceDateBox != null) RequestEvidenceBars(); };
            evidenceDateBox = Input(preferredDay.ToString("yyyy-MM-dd"));
            // Evidence always starts with every detected setup from the exact test ledger and full tested session.
            // Outcome switches below are purely view filters; they never alter the actual stored run.
            evidenceScopeBox = Select("ALL DETECTED"); evidenceScopeBox.SelectedIndex = 0;
            evidenceBarsBox = Select("FULL SELECTED SESSION"); evidenceBarsBox.SelectedIndex = 0;
            var render = Btn("DRAW SETUPS", Green); render.Click += delegate { RequestEvidenceBars(); };
            controls.Children.Add(Row("INSTRUMENT", evidenceInstrumentBox)); controls.Children.Add(Row("CHART TIMEFRAME", evidenceTimeframeBox)); controls.Children.Add(Row("SESSION DATE YYYY-MM-DD", evidenceDateBox)); controls.Children.Add(render); top.Children.Add(controls);
            var displayFilters = new UniformGrid { Columns = 2, Margin = new Thickness(2, 2, 2, 2) };
            evidenceSessionFilterBox = Select("FULL LOADED SESSION", "ASIA 18:00-08:00", "NY EARLY 08:00-15:55", "NY OPEN 09:30-15:55"); evidenceSessionFilterBox.SelectedIndex = 0;
            evidenceStrengthBox = Select("ALL DETECTED SETUPS", "AGGRESSION-TAGGED SETUPS"); evidenceStrengthBox.SelectedIndex = 0;
            displayFilters.Children.Add(Row("CHART SESSION FILTER", evidenceSessionFilterBox)); displayFilters.Children.Add(Row("SETUP FILTER", evidenceStrengthBox)); top.Children.Add(displayFilters);
            var filters = new WrapPanel { Margin = new Thickness(4, 2, 4, 2) };
            filters.Children.Add(Txt("VIEW: ", Cyan, 11, FontWeights.Bold));
            evidenceWinsBox = new CheckBox { Content = "WINS", IsChecked = true, Foreground = WinPurple, Margin = new Thickness(6, 0, 10, 0) };
            evidenceLossesBox = new CheckBox { Content = "LOSSES", IsChecked = true, Foreground = LossAmber, Margin = new Thickness(6, 0, 10, 0) };
            evidenceExitsBox = new CheckBox { Content = "SESSION EXITS", IsChecked = true, Foreground = ExitIce, Margin = new Thickness(6, 0, 10, 0) };
            evidenceNoEntryBox = new CheckBox { Content = "NO ENTRY DATA", IsChecked = false, Foreground = Muted, Margin = new Thickness(6, 0, 10, 0) };
            Action refreshEvidenceFilters = delegate { if (evidenceBars != null && evidenceBars.Count > 0) { selectedEvidenceEvent = null; RenderEvidenceChart(); } };
            evidenceWinsBox.Checked += delegate { refreshEvidenceFilters(); }; evidenceWinsBox.Unchecked += delegate { refreshEvidenceFilters(); };
            evidenceLossesBox.Checked += delegate { refreshEvidenceFilters(); }; evidenceLossesBox.Unchecked += delegate { refreshEvidenceFilters(); };
            evidenceExitsBox.Checked += delegate { refreshEvidenceFilters(); }; evidenceExitsBox.Unchecked += delegate { refreshEvidenceFilters(); };
            evidenceNoEntryBox.Checked += delegate { refreshEvidenceFilters(); }; evidenceNoEntryBox.Unchecked += delegate { refreshEvidenceFilters(); };
            evidenceSessionFilterBox.SelectionChanged += delegate { ResetEvidenceViewport(); refreshEvidenceFilters(); };
            evidenceStrengthBox.SelectionChanged += delegate { selectedEvidenceEvent = null; refreshEvidenceFilters(); };
            filters.Children.Add(evidenceWinsBox); filters.Children.Add(evidenceLossesBox); filters.Children.Add(evidenceExitsBox); filters.Children.Add(evidenceNoEntryBox);
            top.Children.Add(filters);
            var zoomControls = new WrapPanel { Margin = new Thickness(4, 1, 4, 3) };
            zoomControls.Children.Add(Txt("CHART ZOOM: ", Cyan, 11, FontWeights.Bold));
            var zoomOut = Btn("−", Card); zoomOut.Width = 34; zoomOut.Height = 24; zoomOut.FontSize = 17; zoomOut.Foreground = Text; zoomOut.ToolTip = "Zoom out"; zoomOut.Click += delegate { SetEvidenceZoom(evidenceZoom - 0.20); };
            var zoomReset = Btn("FIT", Blue); zoomReset.Width = 46; zoomReset.Height = 24; zoomReset.FontSize = 11; zoomReset.Foreground = Bg; zoomReset.ToolTip = "Reset chart to the default readable width"; zoomReset.Click += delegate { SetEvidenceZoom(1.0); };
            var zoomIn = Btn("+", Card); zoomIn.Width = 34; zoomIn.Height = 24; zoomIn.FontSize = 17; zoomIn.Foreground = Text; zoomIn.ToolTip = "Zoom in"; zoomIn.Click += delegate { SetEvidenceZoom(evidenceZoom + 0.20); };
            evidenceZoomText = Txt("TIME 100% • PRICE 100% • drag bottom scale = time • drag right scale = price", Muted, 10, FontWeights.Bold); evidenceZoomText.Margin = new Thickness(8, 4, 0, 0);
            zoomControls.Children.Add(zoomOut); zoomControls.Children.Add(zoomReset); zoomControls.Children.Add(zoomIn); zoomControls.Children.Add(evidenceZoomText);
            top.Children.Add(zoomControls);
            // A TabControl reselects and scrolls its own header strip during content refresh.
            // That was sending a user who chose August back to January.  A fixed button strip
            // keeps selection and horizontal position under this window's control.
            evidenceDateButtons.Clear(); evidenceSelectedDateIndex = -1;
            evidenceDateStrip = new StackPanel { Orientation = Orientation.Horizontal, Background = Panel, Margin = new Thickness(2, 2, 2, 4) };
            List<DateTime> evidenceDays = EvidenceSessionDates(0);
            for (int dayIndex = 0; dayIndex < evidenceDays.Count; dayIndex++)
            {
                DateTime tabDay = evidenceDays[dayIndex];
                Button dateButton = Btn(EvidenceSessionTabLabel(tabDay), Card);
                dateButton.Tag = tabDay.ToString("yyyy-MM-dd"); dateButton.Width = 118; dateButton.Height = 36; dateButton.FontSize = 10; dateButton.Margin = new Thickness(2, 2, 2, 2); dateButton.Foreground = Cyan;
                dateButton.Click += delegate { SelectEvidenceSessionDate(Convert.ToString(dateButton.Tag), true); };
                evidenceDateButtons.Add(dateButton); evidenceDateStrip.Children.Add(dateButton);
            }
            evidenceTabsScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = evidenceDateStrip, Height = 52, Margin = new Thickness(2, 2, 2, 4), Background = Panel };
            // The date navigation is permanent; it does not disappear with the optional controls.
            var dateNavigation = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            dateNavigation.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); dateNavigation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); dateNavigation.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var previousDate = Btn("PREV", Card); previousDate.Width = 70; previousDate.Height = 36; previousDate.FontSize = 10; previousDate.ToolTip = "Previous session date"; previousDate.Click += delegate { if (evidenceSelectedDateIndex > 0 && evidenceSelectedDateIndex < evidenceDateButtons.Count) SelectEvidenceSessionDate(Convert.ToString(evidenceDateButtons[evidenceSelectedDateIndex - 1].Tag), true); };
            var nextDate = Btn("NEXT", Card); nextDate.Width = 70; nextDate.Height = 36; nextDate.FontSize = 10; nextDate.ToolTip = "Next session date"; nextDate.Click += delegate { if (evidenceSelectedDateIndex >= 0 && evidenceSelectedDateIndex + 1 < evidenceDateButtons.Count) SelectEvidenceSessionDate(Convert.ToString(evidenceDateButtons[evidenceSelectedDateIndex + 1].Tag), true); };
            dateNavigation.Children.Add(previousDate); Grid.SetColumn(evidenceTabsScroll, 1); dateNavigation.Children.Add(evidenceTabsScroll); Grid.SetColumn(nextDate, 2); dateNavigation.Children.Add(nextDate); compactHeader.Children.Add(dateNavigation);
            evidenceLegendText = Txt("DEFAULT: normal-size candles and compact W/L/EXIT circles. Hover for time + OHLC; click a circle for its entry/exit audit. Drag anywhere on the plot to pan. Use the bottom axis for time zoom and right axis for price zoom. FIT restores the readable default view.", Text, 10, FontWeights.Bold); top.Children.Add(evidenceLegendText);
            // The timeframe selector is a primary analysis control, not a hidden advanced setting.
            // Start with the chart itself unobstructed; the compact control strip is available on
            // demand through SHOW CONTROLS and never affects the loaded bars or ledger.
            evidenceControlsPanel = top; evidenceControlsVisible = false; evidenceControlsPanel.Visibility = Visibility.Collapsed; evidenceControlsToggle.Content = "SHOW CONTROLS";
            Grid.SetRow(evidenceControlsPanel, 1); root.Children.Add(evidenceControlsPanel);
            var auditStack = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
            evidenceHoverText = Txt("CANDLE INSPECTOR • hover or click any candle to read its exact date/time, open, high, low, close, and range.", Cyan, 11, FontWeights.Bold);
            evidenceHoverText.FontFamily = new FontFamily("Consolas");
            evidenceHoverBorder = new Border { Background = Card, BorderBrush = Blue, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4), HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 940, Child = evidenceHoverText };
            var auditRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            auditRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); auditRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            auditRow.Children.Add(evidenceHoverBorder);
            var totalsBorder = new Border { Background = Card, BorderBrush = Cyan, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(8, 0, 0, 0), Child = evidenceMetricsText };
            Grid.SetColumn(totalsBorder, 1); auditRow.Children.Add(totalsBorder); auditStack.Children.Add(auditRow);
            evidenceDetailText = Txt("CLICK A W/L/EXIT CIRCLE TO AUDIT ONE SETUP. The chart stays clean until selection: then its exact entry and exit prices appear without covering the candles.", Muted, 11, FontWeights.Normal);
            evidenceDetailText.FontFamily = new FontFamily("Consolas");
            evidenceDetailBorder = new Border { Background = Panel, BorderBrush = Cyan, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 940, Visibility = Visibility.Collapsed, Child = evidenceDetailText };
            auditStack.Children.Add(evidenceDetailBorder);
            evidencePnlText = Txt(string.Empty, Text, 19, FontWeights.Bold);
            evidencePnlText.FontFamily = new FontFamily("Consolas");
            evidencePnlBorder = new Border { Background = EvidenceBg, BorderBrush = Cyan, BorderThickness = new Thickness(2), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed, Child = evidencePnlText };
            auditStack.Children.Add(evidencePnlBorder);
            Grid.SetRow(auditStack, 2); root.Children.Add(auditStack);
            evidenceZoom = 1.0; evidenceHorizontalZoom = 1.0; evidenceVerticalZoom = 1.0;
            evidenceCanvas = new Canvas { Background = EvidenceBg, Width = 1200, Height = 620, ClipToBounds = true };
            // A small rubber-band: dragging past the first/last loaded bar still visibly "gives"
            // a little instead of feeling dead at the edge, then springs back on release - the
            // tactile cue that TradingView-style charts give even though this dataset (unlike a
            // live feed) has no more history to reveal past the edge.
            evidencePanOverscrollTransform = new TranslateTransform();
            evidenceCanvas.RenderTransform = evidencePanOverscrollTransform;
            evidenceCanvas.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs args) { if (AdjustEvidenceZoomAtPointer(args)) args.Handled = true; };
            // The canvas remains a fitted viewport. Dedicated navigation bars outside the time
            // and price axes provide fast travel without converting the chart into a browser page.
            evidenceScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = evidenceCanvas, Margin = new Thickness(0), Background = EvidenceBg };
            evidenceCanvas.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                if (evidenceScroll == null) return;
                Point chartPoint = args.GetPosition(evidenceCanvas);
                evidenceScaleDragAxis = EvidenceScaleAxisAt(chartPoint);
                evidenceScaleDragStart = chartPoint;
                evidenceScaleDragStartHorizontalZoom = evidenceHorizontalZoom;
                evidenceScaleDragStartVerticalZoom = evidenceVerticalZoom;
                evidenceScaleDragStartFirstBar = evidenceFirstVisibleBar;
                evidenceScaleDragStartPriceCenter = evidencePriceCenter;
                evidenceScaleDragStartZoom = evidenceScaleDragAxis == 2 ? evidenceVerticalZoom : evidenceHorizontalZoom;
                // TradingView/NinjaTrader behavior: dragging the plot pans; only the dedicated
                // scale gutters resize an axis.  A modifier is no longer required to pan.
                evidencePanning = evidenceScaleDragAxis == 0;
                evidencePanStart = chartPoint;
                evidenceSelectionClickHandled = false;
                pendingEvidenceSelectionAction = null;
                // Cancel any in-flight rubber-band snap-back so a fresh drag isn't fighting an
                // animation still returning the canvas from the previous release.
                if (evidencePanOverscrollTransform != null) evidencePanOverscrollTransform.BeginAnimation(TranslateTransform.XProperty, null);
                evidenceCanvas.CaptureMouse();
            };
            evidenceCanvas.MouseMove += delegate(object sender, MouseEventArgs args)
            {
                if (evidenceScroll == null) return;
                Point hoverPoint = args.GetPosition(evidenceCanvas);
                if (hoverPoint.X >= 72 && hoverPoint.X <= evidenceCanvas.Width - 78 && hoverPoint.Y >= 58 && hoverPoint.Y <= evidenceCanvas.Height - 58)
                {
                    evidenceCrosshairPoint = hoverPoint; evidenceCrosshairVisible = true;
                    // Pure hover (no button pressed): reposition the crosshair overlay only.
                    // This used to call a full RenderEvidenceChart() on every mouse-move tick,
                    // rebuilding every candle/marker just to move two dashed lines.
                    if (evidenceBars != null && evidenceBars.Count > 0 && args.LeftButton != MouseButtonState.Pressed) RequestEvidenceRender(false);
                }
                if (args.LeftButton != MouseButtonState.Pressed) { evidencePanning = false; evidenceScaleDragAxis = 0; return; }
                if (evidenceScaleDragAxis != 0)
                {
                    Point chartPoint = args.GetPosition(evidenceCanvas);
                    double dx = chartPoint.X - evidenceScaleDragStart.X;
                    double dy = evidenceScaleDragStart.Y - chartPoint.Y;
                    if (evidenceScaleDragAxis == 1) evidenceHorizontalZoom = Math.Max(0.04, Math.Min(40.0, evidenceScaleDragStartZoom * Math.Exp(dx / 130.0)));
                    else if (evidenceScaleDragAxis == 2) evidenceVerticalZoom = Math.Max(0.04, Math.Min(40.0, evidenceScaleDragStartZoom * Math.Exp(dy / 130.0)));
                    evidenceZoom = Math.Sqrt(evidenceHorizontalZoom * evidenceVerticalZoom);
                    UpdateEvidenceZoomText();
                    // Coalesced: many MouseMove ticks during one drag now produce at most one
                    // real rebuild per screen refresh instead of one rebuild per tick.
                    if (evidenceBars != null && evidenceBars.Count > 0) RequestEvidenceRender(true);
                    args.Handled = true;
                    return;
                }
                if (!evidencePanning) return;
                Point now = args.GetPosition(evidenceCanvas);
                // A marker click becomes a selection only if the pointer stays within the usual
                // click tolerance. This keeps every selected/pulsing setup fully draggable.
                if (Math.Abs(now.X - evidencePanStart.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(now.Y - evidencePanStart.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    pendingEvidenceSelectionAction = null;
                    evidenceSelectionClickHandled = false;
                }
                int shift = (int)Math.Round((evidencePanStart.X - now.X) / Math.Max(2.0, EvidenceRenderedCandleWidth()));
                int intendedFirst = evidenceScaleDragStartFirstBar + shift;
                evidenceFirstVisibleBar = Math.Max(0, Math.Min(evidenceLayoutLastPossibleFirst, intendedFirst));
                // Rubber-band: once the intended shift goes past what the loaded range allows,
                // let the whole canvas visibly slide a damped, capped amount instead of freezing
                // dead at the boundary - the tactile cue that the drag is still being received.
                int overshootBars = intendedFirst - evidenceFirstVisibleBar;
                double rawOverscrollPixels = -overshootBars * EvidenceRenderedCandleWidth();
                if (evidencePanOverscrollTransform != null) evidencePanOverscrollTransform.X = Math.Max(-48.0, Math.Min(48.0, rawOverscrollPixels * 0.28));
                double plotHeight = Math.Max(1.0, evidenceCanvas.Height - 116.0);
                if (evidenceVisiblePriceRange > 0 && !double.IsNaN(evidencePriceCenter))
                    evidencePriceCenter = evidenceScaleDragStartPriceCenter + (now.Y - evidencePanStart.Y) * evidenceVisiblePriceRange / plotHeight;
                // Coalesced the same way as the scale-drag above: the pan math still runs on
                // every tick (it is cheap), only the expensive rebuild is capped to one per frame.
                if (evidenceBars != null && evidenceBars.Count > 0) RequestEvidenceRender(true);
                args.Handled = true;
            };
            evidenceCanvas.MouseLeftButtonUp += delegate
            {
                bool clickedSelectedObject = evidenceSelectionClickHandled;
                bool wasScaleDrag = evidenceScaleDragAxis != 0;
                Action select = pendingEvidenceSelectionAction;
                evidencePanning = false; evidenceScaleDragAxis = 0; evidenceSelectionClickHandled = false; pendingEvidenceSelectionAction = null;
                if (evidencePanOverscrollTransform != null && evidencePanOverscrollTransform.X != 0)
                {
                    var snapBack = new DoubleAnimation(evidencePanOverscrollTransform.X, 0.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 } };
                    evidencePanOverscrollTransform.BeginAnimation(TranslateTransform.XProperty, snapBack);
                }
                evidenceCanvas.ReleaseMouseCapture();
                if (!wasScaleDrag && select != null) select();
                else if (!wasScaleDrag && !clickedSelectedObject && selectedEvidenceEvent != null) ClearEvidenceSelection(true);
            };
            // Direct dragging replaces the earlier auxiliary scrollbars. Retain the controls
            // internally for compatibility with prior layout state, but keep them hidden.
            evidenceHorizontalScrollBar = new ScrollBar { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 0, SmallChange = 1, LargeChange = 10, Height = 0, Visibility = Visibility.Collapsed };
            evidenceVerticalScrollBar = new ScrollBar { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, SmallChange = 2, LargeChange = 10, Width = 0, Visibility = Visibility.Collapsed };
            evidenceHorizontalScrollBar.ValueChanged += delegate { if (evidenceNavigationUpdating || evidenceBars == null || evidenceBars.Count == 0) return; evidenceFirstVisibleBar = Math.Max(0, (int)Math.Round(evidenceHorizontalScrollBar.Value)); RequestEvidenceRender(true); };
            evidenceVerticalScrollBar.ValueChanged += delegate { if (evidenceNavigationUpdating || evidenceBars == null || evidenceBars.Count == 0 || evidenceScrollPriceMaximum <= evidenceScrollPriceMinimum) return; evidencePriceCenter = evidenceScrollPriceMinimum + (evidenceScrollPriceMaximum - evidenceScrollPriceMinimum) * evidenceVerticalScrollBar.Value / 100.0; RequestEvidenceRender(true); };
            var chartHost = new Grid { Margin = new Thickness(4), Background = EvidenceBg };
            chartHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); chartHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); chartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chartHost.Children.Add(evidenceScroll);
            Grid.SetRow(chartHost, 3); root.Children.Add(chartHost);
            w.Content = root;
            evidenceWindow = w;
            evidenceCloseConfirmed = false;
            w.Closing += delegate(object sender, CancelEventArgs args)
            {
                if (evidenceCloseConfirmed) return;
                MessageBoxResult answer = MessageBox.Show("Close the Evidence Chart? The historical run remains open in Keystone Arc.", "Close Evidence Chart", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) { args.Cancel = true; return; }
                evidenceCloseConfirmed = true;
            };
            w.Closed += delegate { CancelEvidenceRequest(); if (evidenceSelectionTimer != null) evidenceSelectionTimer.Stop(); evidenceSelectionTimer = null; if (evidenceRenderQueued) { CompositionTarget.Rendering -= EvidenceRenderTick; evidenceRenderQueued = false; } evidenceFullRenderNeeded = true; evidenceLastAnimatedRenderKey = null; evidencePanOverscrollTransform = null; evidenceCrosshairHLine = null; evidenceCrosshairVLine = null; evidenceCrosshairPriceLabel = null; evidenceCrosshairTimeLabel = null; evidenceLayoutBars = null; evidenceWindow = null; evidenceCanvas = null; evidenceScroll = null; evidenceHorizontalScrollBar = null; evidenceVerticalScrollBar = null; evidenceTabsScroll = null; evidenceDateStrip = null; evidenceDateButtons.Clear(); evidenceSelectedDateIndex = -1; evidenceControlsPanel = null; evidenceDetailBorder = null; evidencePnlBorder = null; evidenceControlsToggle = null; evidenceStatusText = null; evidenceMetricsText = null; evidenceLegendText = null; evidenceZoomText = null; evidenceStudyText = null; evidenceDetailText = null; evidencePnlText = null; selectedEvidenceEvent = null; evidenceInstrumentTabs = null; evidenceTimeframeBox = null; evidencePreviewEvents.Clear(); evidencePreviewOwnLedger = false; evidencePreviewMinutes = 0; evidencePanning = false; pendingEvidenceSelectionAction = null; };
            w.Show();
            if (evidenceInstrumentTabs != null && evidenceInstrumentTabs.Items.Count > 0)
            {
                for (int tabIndex = 0; tabIndex < evidenceInstrumentTabs.Items.Count; tabIndex++)
                {
                    TabItem tab = evidenceInstrumentTabs.Items[tabIndex] as TabItem;
                    if (tab != null && Convert.ToString(tab.Header).StartsWith(preferredSymbol)) { evidenceInstrumentTabs.SelectedIndex = tabIndex; break; }
                }
            }
            SelectEvidenceSessionDate(preferredDay.ToString("yyyy-MM-dd"), false);
            RequestEvidenceBars();
        }

        private void SelectEvidenceSessionDate(string dateText, bool reload)
        {
            if (string.IsNullOrWhiteSpace(dateText) || evidenceDateBox == null) return;
            int index = -1;
            for (int i = 0; i < evidenceDateButtons.Count; i++)
            {
                Button candidate = evidenceDateButtons[i];
                bool selected = candidate != null && string.Equals(Convert.ToString(candidate.Tag), dateText, StringComparison.Ordinal);
                if (candidate != null) { candidate.Background = selected ? Blue : Card; candidate.Foreground = selected ? Bg : Text; }
                if (selected) index = i;
            }
            if (index < 0) return;
            bool changed = evidenceSelectedDateIndex != index || !string.Equals(evidenceDateBox.Text, dateText, StringComparison.Ordinal);
            evidenceSelectedDateIndex = index;
            evidenceDateBox.Text = dateText;
            // A user who manually scrolled to a later date must stay there after clicking it.
            // Only initial opening may reveal a preselected tab; click navigation never recenters.
            if (!reload) ScrollEvidenceSessionButtonIntoView(index);
            if (changed) ResetEvidenceViewport();
            if (reload && evidenceWindow != null) RequestEvidenceBars();
        }

        private void ScrollEvidenceSessionButtonIntoView(int index)
        {
            if (evidenceTabsScroll == null || index < 0) return;
            // Preserve a manually scrolled month whenever the selected tab remains visible.
            // Only move the strip just enough to reveal a tab that is outside the current view;
            // never recenter it, which was the visual "jump back to January" behavior.
            double tabLeft = index * 122.0, tabRight = tabLeft + 118.0;
            double current = evidenceTabsScroll.HorizontalOffset;
            double viewRight = current + Math.Max(1.0, evidenceTabsScroll.ViewportWidth);
            double desired = current;
            if (tabLeft < current) desired = Math.Max(0, tabLeft - 8.0);
            else if (tabRight > viewRight) desired = Math.Max(0, tabRight - Math.Max(140.0, evidenceTabsScroll.ViewportWidth) + 8.0);
            if (Math.Abs(desired - current) < 1.0) return;
            Dispatcher dispatcher = evidenceWindow == null ? null : evidenceWindow.Dispatcher;
            if (dispatcher == null) evidenceTabsScroll.ScrollToHorizontalOffset(desired);
            else dispatcher.BeginInvoke((Action)delegate { if (evidenceTabsScroll != null) evidenceTabsScroll.ScrollToHorizontalOffset(desired); }, DispatcherPriority.Render);
        }

        private void RequestEvidenceBars()
        {
            if (evidenceWindow == null || evidenceInstrumentBox == null || evidenceDateBox == null) return;
            DateTime day;
            if (!DateTime.TryParseExact(evidenceDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) { SetEvidenceStatus("DATE ERROR • USE YYYY-MM-DD", Red); return; }
            day = day.Date;
            DateTime firstRunDate = KeystoneArcEngine.SessionGroupingDate(config.Start, config), lastRunDate = KeystoneArcEngine.SessionGroupingDate(config.End, config);
            if (day < firstRunDate || day > lastRunDate) { SetEvidenceStatus("SESSION DATE OUTSIDE THE LOADED TEST RANGE • " + SelectedTestDateLabel(), Red); return; }
            string key = Convert.ToString(evidenceInstrumentBox.SelectedItem);
            if (key != "MNQ" && key != "MGC") { SetEvidenceStatus("CHOOSE MNQ OR MGC", Red); return; }
            if (config.Scope != "BOTH" && config.Scope != key) { SetEvidenceStatus(key + " WAS NOT INCLUDED IN THIS LOADED TEST SCOPE", Red); return; }
            int evidenceMinutes = EvidenceSelectedMinutes();
            ResetEvidenceDisplayForRequest();
            evidencePreviewMinutes = evidenceMinutes;
            List<KeystoneArcBar> loadedSetupBars = key == "MNQ" ? mnqSetupBars : mgcSetupBars;
            bool loadedFromChart = key == "MNQ" ? mnqSetupFromOpenChart : mgcSetupFromOpenChart;
            // The completed research request already contains the exact selected-timeframe
            // bars that created this ledger.  Reuse a complete session from that dataset
            // regardless of whether it originated from an open chart or BarsRequest.  This
            // avoids a second narrow request being truncated at midnight after a long-range
            // study and guarantees that the Evidence view uses the same bars as detection.
            if (evidenceMinutes == config.SetupMinutes && HasFullEvidenceSessionCoverage(loadedSetupBars, day))
            {
                CancelEvidenceRequest(); ++evidenceGeneration;
                evidenceBars = new List<KeystoneArcBar>(loadedSetupBars);
                evidencePreviewEvents = new List<KeystoneArcEvent>(); evidencePreviewMinutes = config.SetupMinutes; evidencePreviewOwnLedger = false;
                DateTime sessionStart = ConfiguredSessionStart(day), sessionEnd = TradingSessionEnd(day);
                int count = evidenceBars.Count(x => x.Time >= sessionStart && x.Time <= sessionEnd);
                if (count == 0) { SetEvidenceStatus("THE MATCHING OPEN CHART DOES NOT HAVE THE SELECTED SESSION'S " + config.SetupMinutes + "M BARS LOADED", Red); return; }
                SetEvidenceStatus((loadedFromChart ? "EXACT OPEN-CHART " : "COMPLETED RESEARCH DATASET ") + config.SetupMinutes + "M BARS • " + key + " • " + count + " BARS • FULL " + ConfiguredSessionStart(day).ToString("MM-dd HH:mm") + " → " + TradingSessionEnd(day).ToString("MM-dd HH:mm") + " SESSION • MARKS FROM THE SAME DETECTED LEDGER", Green);
                RenderEvidenceChart();
                return;
            }
            string source;
            Instrument instrument = ResolveDynamicInstrument(key, day, out source);
            string contract = instrument == null ? (key == "MNQ" ? config.MnqName : config.MgcName) : instrument.FullName;
            if (instrument == null) { SetEvidenceStatus("INSTRUMENT NOT RESOLVED • " + key + " • OPEN A MATCHING CHART OR CHECK THE NINJATRADER INSTRUMENT DATABASE", Red); return; }
            CancelEvidenceRequest();
            long run = ++evidenceGeneration;
            evidenceBars = new List<KeystoneArcBar>();
            string hoursSource;
            TradingHours evidenceHours = ResolveRequestTradingHours(key, evidenceMinutes, out hoursSource);
            SetEvidenceStatus("REQUESTING DIRECT NINJATRADER " + evidenceMinutes + "-MINUTE BARS • " + contract + " • " + day.ToString("yyyy-MM-dd") + " • " + source + " • " + hoursSource, Gold);
            try
            {
                DateTime requestStart = ConfiguredSessionStart(day).AddMinutes(-EvidenceContextMinutes(evidenceMinutes));
                DateTime requestEnd = TradingSessionEnd(day);
                evidenceRequestKey = key; evidenceRequestDay = day; evidenceRequestSessionEnd = requestEnd; evidenceRequestInstrument = instrument; evidenceRequestHours = evidenceHours; evidenceRequestFirstPart = new List<KeystoneArcBar>(); evidencePreviewMinutes = evidenceMinutes;
                DateTime firstEnd = requestStart.Date.AddDays(1).AddTicks(-1);
                if (firstEnd > requestEnd) firstEnd = requestEnd;
                BeginEvidenceRequestPiece(requestStart, firstEnd, false, run);
            }
            catch (Exception ex) { SetEvidenceStatus("SETUP-BAR REQUEST FAILED • " + ex.Message, Red); }
        }

        private void BeginEvidenceRequestPiece(DateTime start, DateTime end, bool append, long run)
        {
            if (evidenceRequestInstrument == null) throw new InvalidOperationException("Evidence instrument is not available.");
            evidenceRequest = new BarsRequest(evidenceRequestInstrument, start, end) { BarsPeriod = new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = evidencePreviewMinutes <= 0 ? config.SetupMinutes : evidencePreviewMinutes }, TradingHours = evidenceRequestHours, MergePolicy = MergePolicy.UseGlobalSettings };
            evidenceRequest.Request(delegate(BarsRequest done, ErrorCode error, string message) { CompleteEvidenceRequest(evidenceRequestKey, evidenceRequestDay, done, error, message, run, append); });
        }

        // Never silently display a partial open chart as a full selected trading day.  For ALL
        // ELIGIBLE this means one chart from 18:00 on the named session date through 15:55 next day.
        private bool HasFullEvidenceSessionCoverage(List<KeystoneArcBar> source, DateTime sessionDate)
        {
            if (source == null || source.Count == 0) return false;
            DateTime start = ConfiguredSessionStart(sessionDate), end = TradingSessionEnd(sessionDate);
            int tolerance = Math.Max(1, evidencePreviewMinutes > 0 ? evidencePreviewMinutes : (config == null ? 5 : config.SetupMinutes));
            bool hasStart = source.Any(x => x.Time >= start && x.Time <= start.AddMinutes(tolerance));
            bool hasEnd = source.Any(x => x.Time >= end.AddMinutes(-tolerance) && x.Time <= end.AddMinutes(tolerance));
            return hasStart && hasEnd;
        }

        private void ResetEvidenceDisplayForRequest()
        {
            // Invalidate any asynchronous response for the prior MNQ/MGC/date tab before clearing its view.
            CancelEvidenceRequest();
            if (evidenceSelectionTimer != null) { evidenceSelectionTimer.Stop(); evidenceSelectionTimer = null; }
            selectedEvidenceEvent = null; evidenceSelectionPulseOn = false; evidenceSelectionPulse = 0;
            if (evidenceDetailBorder != null) evidenceDetailBorder.Visibility = Visibility.Collapsed;
            if (evidenceDetailText != null) { evidenceDetailText.Text = "LOADING SELECTED SESSION • previous entry detail cleared."; evidenceDetailText.Foreground = Muted; }
            if (evidencePnlText != null) evidencePnlText.Text = string.Empty;
            if (evidenceHoverText != null) { evidenceHoverText.Text = "CANDLE INSPECTOR • loading the selected instrument and session."; evidenceHoverText.Foreground = Cyan; }
            if (evidenceCanvas != null) evidenceCanvas.Children.Clear();
            evidenceBars = new List<KeystoneArcBar>();
            evidencePreviewEvents = new List<KeystoneArcEvent>();
            evidencePreviewOwnLedger = false;
            ResetEvidenceViewport();
            SetEvidenceStatus("LOADING SELECTED INSTRUMENT / SESSION • previous chart cleared", Gold);
        }

        private void ResetEvidenceViewport()
        {
            evidenceHorizontalZoom = 1.0; evidenceVerticalZoom = 1.0; evidenceZoom = 1.0;
            evidenceFirstVisibleBar = 0; evidencePriceCenter = double.NaN; evidenceVisiblePriceRange = 0;
            evidenceCrosshairVisible = false;
            UpdateEvidenceZoomText();
        }

        private void CompleteEvidenceRequest(string key, DateTime day, BarsRequest request, ErrorCode error, string message, long run, bool append)
        {
            int previewMinutes = evidencePreviewMinutes <= 0 ? config.SetupMinutes : evidencePreviewMinutes;
            if (error != ErrorCode.NoError || request == null || request.Bars == null)
            {
                DispatchToLab(delegate { if (run == evidenceGeneration) SetEvidenceStatus(previewMinutes + "M DATA ERROR • " + error + " • " + message, Red); });
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var list = new List<KeystoneArcBar>();
                try
                {
                    for (int i = 0; i < request.Bars.Count; i++) list.Add(new KeystoneArcBar { Time = request.Bars.GetTime(i), Symbol = key, Open = request.Bars.GetOpen(i), High = request.Bars.GetHigh(i), Low = request.Bars.GetLow(i), Close = request.Bars.GetClose(i), Volume = request.Bars.GetVolume(i) });
                }
                catch (Exception ex) { message = previewMinutes + "M BAR CONVERSION ERROR • " + ex.Message; list.Clear(); }
                DispatchToLab(delegate
                {
                    if (run != evidenceGeneration) return;
                    // A request that crosses midnight can be truncated by some data adapters.
                    // Complete the following calendar-day portion before rendering the selected
                    // 18:00-to-close trading session, rather than displaying a misleading half-day.
                    if (!append && list.Count > 0 && evidenceRequestSessionEnd > list.Last().Time.Date)
                    {
                        evidenceRequestFirstPart = list;
                        DateTime continuationStart = evidenceRequestFirstPart.Count == 0 ? day.Date.AddDays(1) : evidenceRequestFirstPart.Last().Time.Date.AddDays(1);
                        if (continuationStart <= evidenceRequestSessionEnd)
                        {
                            SetEvidenceStatus("LOADING CONTINUATION AFTER MIDNIGHT • completing " + previewMinutes + "M session bars through " + evidenceRequestSessionEnd.ToString("MM-dd HH:mm"), Gold);
                            try { BeginEvidenceRequestPiece(continuationStart, evidenceRequestSessionEnd, true, run); }
                            catch (Exception ex) { SetEvidenceStatus("CONTINUATION REQUEST FAILED • " + ex.Message, Red); }
                            return;
                        }
                    }
                    if (append) list = MergeRequestedBars(evidenceRequestFirstPart, list);
                    DateTime sessionStart = ConfiguredSessionStart(day), sessionEnd = TradingSessionEnd(day);
                    DateTime visibleContextStart = sessionStart.AddMinutes(-EvidenceContextMinutes(previewMinutes));
                    evidenceBars = list.Where(x => x.Time >= visibleContextStart && x.Time <= sessionEnd).OrderBy(x => x.Time).ToList();
                    if (evidenceBars.Count == 0) { SetEvidenceStatus("DIRECT " + previewMinutes + "M REQUEST RETURNED 0 BARS • CHECK NINJATRADER HISTORY FOR " + day.ToString("yyyy-MM-dd"), Red); return; }
                    if (!HasFullEvidenceSessionCoverage(evidenceBars, day))
                    {
                        SetEvidenceStatus("PARTIAL " + previewMinutes + "M SESSION RETURNED • " + evidenceBars.First().Time.ToString("yyyy-MM-dd HH:mm") + " → " + evidenceBars.Last().Time.ToString("yyyy-MM-dd HH:mm") + " • THE FULL " + sessionStart.ToString("MM-dd HH:mm") + " → " + sessionEnd.ToString("MM-dd HH:mm") + " SESSION IS NOT AVAILABLE FROM THIS REQUEST", Gold);
                    }
                    KeystoneArcRunConfig previewConfig = CloneConfig(config); previewConfig.Scope = key; previewConfig.SetupMinutes = previewMinutes; previewConfig.Start = sessionStart; previewConfig.End = sessionEnd;
                    List<KeystoneArcBar> oneMinute = key == "MNQ" ? mnqBars : mgcBars;
                    int offset = 0;
                    bool verified = previewMinutes == 1 || ComparisonOutcomeMatches(oneMinute, evidenceBars, previewMinutes, sessionStart, sessionEnd, out offset);
                    previewConfig.OutcomeModelEnabled = verified ? 1 : 0;
                    if (key == "MNQ") { previewConfig.MnqOutcomeTimeOffsetMinutes = offset; previewConfig.MnqOutcomeSource = verified ? (previewMinutes == 1 ? "DIRECT SHARED 1M" : "VERIFIED 1M • EVIDENCE PREVIEW") : "UNVERIFIED 1M • PREVIEW OUTCOMES BLOCKED"; }
                    else { previewConfig.MgcOutcomeTimeOffsetMinutes = offset; previewConfig.MgcOutcomeSource = verified ? (previewMinutes == 1 ? "DIRECT SHARED 1M" : "VERIFIED 1M • EVIDENCE PREVIEW") : "UNVERIFIED 1M • PREVIEW OUTCOMES BLOCKED"; }
                    List<KeystoneArcBar> outcomeBars = verified ? (previewMinutes == 1 ? evidenceBars : oneMinute) : evidenceBars;
                    evidencePreviewEvents = KeystoneArcEngine.DetectAndResolve(outcomeBars, evidenceBars, previewConfig);
                    for (int i = 0; i < evidencePreviewEvents.Count; i++) { evidencePreviewEvents[i].ReviewState = "ACCEPTED"; evidencePreviewEvents[i].ReviewNote = "TEMPORARY " + previewMinutes + "M EVIDENCE PREVIEW"; }
                    // Temporary evidence previews never allocate virtual accounts.
                    evidencePreviewOwnLedger = true;
                    RenderEvidenceChart();
                });
            });
        }

        private void RenderEvidenceChart()
        {
            if (evidenceCanvas == null || evidenceBars == null || evidenceBars.Count == 0) return;
            string symbol = Convert.ToString(evidenceInstrumentBox == null ? null : evidenceInstrumentBox.SelectedItem);
            int chartMinutes = EvidenceSelectedMinutes();
            DateTime day; if (!DateTime.TryParseExact(evidenceDateBox == null ? string.Empty : evidenceDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return;
            List<KeystoneArcBar> allBars = FilterEvidenceBars(evidenceBars);
            if (allBars.Count == 0) { evidenceCanvas.Children.Clear(); SetEvidenceStatus("NO DIRECT " + chartMinutes + "M BARS IN THE SELECTED DISPLAY WINDOW", Gold); return; }
            // One orchestrated pop-in when a session is actually new (date, symbol, timeframe,
            // or session filter changed) - never replayed on a pan/zoom re-render of the same
            // view, so dragging around never re-triggers the entrance animation.
            string sessionFilterKey = evidenceSessionFilterBox == null ? string.Empty : Convert.ToString(evidenceSessionFilterBox.SelectedItem);
            string renderKey = symbol + "|" + day.ToString("yyyy-MM-dd") + "|" + chartMinutes + "|" + sessionFilterKey;
            bool animateMarkers = !string.Equals(renderKey, evidenceLastAnimatedRenderKey, StringComparison.Ordinal);
            evidenceLastAnimatedRenderKey = renderKey;
            List<KeystoneArcEvent> allMarks = EvidenceEvents(symbol, day);
            UpdateEvidenceSessionMetrics(symbol, day);
            // At 100% candles use a normal readable width. A long session therefore opens as a
            // viewport rather than squeezing hundreds of bars into miniature candles; pan or the
            // external time bar moves through it. Price scale changes the visible range only.
            double chartHeight = Math.Max(420, Math.Min(620, (evidenceWindow == null ? 820 : evidenceWindow.Height) - 365));
            double width = Math.Max(900, evidenceWindow == null ? 1320 : evidenceWindow.Width - 54);
            double left = 72, top = 58, right = 78, bottom = 58;
            double baseCandleWidth = chartMinutes <= 1 ? 8 : (chartMinutes <= 5 ? 14 : (chartMinutes <= 30 ? 20 : 30));
            double candleWidth = baseCandleWidth * Math.Max(0.04, Math.Min(40.0, evidenceHorizontalZoom));
            int visibleCount = Math.Max(1, Math.Min(allBars.Count, (int)Math.Floor((width - left - right) / Math.Max(0.35, candleWidth))));
            int lastPossibleFirst = Math.Max(0, allBars.Count - visibleCount);
            evidenceLayoutLastPossibleFirst = lastPossibleFirst;
            evidenceFirstVisibleBar = Math.Max(0, Math.Min(lastPossibleFirst, evidenceFirstVisibleBar));
            List<KeystoneArcBar> bars = allBars.Skip(evidenceFirstVisibleBar).Take(visibleCount).ToList();
            List<KeystoneArcEvent> marks = allMarks.Where(e => EvidenceEntryBarIndex(bars, e) >= 0).ToList();
            double rawMin = bars.Min(x => x.Low), rawMax = bars.Max(x => x.High);
            foreach (KeystoneArcEvent e in marks) { rawMin = Math.Min(rawMin, e.Entry); rawMax = Math.Max(rawMax, e.Entry); }
            double padding = Math.Max((rawMax - rawMin) * 0.09, Math.Max(symbol == "MGC" ? 1.0 : 8.0, 0.0001));
            double rawCenter = (rawMin + rawMax) / 2.0;
            if (double.IsNaN(evidencePriceCenter)) evidencePriceCenter = rawCenter;
            double halfRange = Math.Max(0.0001, (rawMax - rawMin + padding * 2.0) / Math.Max(0.04, Math.Min(40.0, evidenceVerticalZoom)) / 2.0);
            double min = evidencePriceCenter - halfRange, max = evidencePriceCenter + halfRange;
            evidenceVisiblePriceRange = max - min;
            evidenceCanvas.Width = width; evidenceCanvas.Height = chartHeight;
            evidenceCanvas.Background = EvidenceBg;
            evidenceCanvas.Children.Clear();
            Func<double, double> y = delegate(double price) { return top + (max - price) / Math.Max(0.0000001, max - min) * (chartHeight - top - bottom); };
            for (int p = 0; p <= 5; p++)
            {
                double price = min + (max - min) * p / 5.0; double py = y(price);
                // Price labels remain on the axes, but the full horizontal grid is omitted so
                // candles, wicks, entries, and the dashed cursor stay visually primary.
                string priceLabel = price.ToString(symbol == "MGC" ? "0.0" : "0.00");
                AddCanvasText(priceLabel, 4, py - 8, Text, 10, FontWeights.Normal);
                AddCanvasText(priceLabel, width - right + 7, py - 8, Text, 10, FontWeights.Normal);
            }
            // Crosshair rendering moved to a persistent overlay (see UpdateEvidenceCrosshairOverlay)
            // so a pure hover no longer needs to rebuild the full candle/marker canvas below.
            var index = new Dictionary<DateTime, int>();
            int timeLabelMinutes = EvidenceTimeLabelMinutes();
            double lastTimeLabelX = double.MinValue;
            for (int i = 0; i < bars.Count; i++)
            {
                KeystoneArcBar b = bars[i]; index[b.Time] = i;
                double x = left + i * candleWidth + candleWidth / 2.0;
                Brush bodyBrush = b.Close > b.Open ? CandleUp : (b.Close < b.Open ? CandleDown : Gold);
                var wick = new System.Windows.Shapes.Line { X1 = x, X2 = x, Y1 = y(b.High), Y2 = y(b.Low), Stroke = CandleWick, StrokeThickness = 1.25, Opacity = 0.92, ToolTip = EvidenceBarTooltip(b) };
                wick.MouseMove += delegate { ShowEvidenceHover(b); };
                evidenceCanvas.Children.Add(wick);
                double openY = y(b.Open), closeY = y(b.Close), bodyTop = Math.Min(openY, closeY), bodyHeight = Math.Max(1, Math.Abs(closeY - openY));
                KeystoneArcBar auditBar = b;
                var body = new System.Windows.Shapes.Rectangle { Width = Math.Max(2, candleWidth - 3), Height = bodyHeight, Fill = bodyBrush, Stroke = CandleWick, StrokeThickness = 0.55, ToolTip = EvidenceBarTooltip(auditBar) };
                body.MouseMove += delegate { ShowEvidenceHover(auditBar); };
                body.PreviewMouseLeftButtonDown += delegate { evidenceSelectionClickHandled = true; pendingEvidenceSelectionAction = delegate { ShowEvidenceBarDetail(auditBar); }; };
                Canvas.SetLeft(body, x - body.Width / 2.0); Canvas.SetTop(body, bodyTop); evidenceCanvas.Children.Add(body);
                // Font size never changes with zoom. Labels are thinned only when the available
                // time-axis pixels cannot hold the next fixed-width label without overlap.
                if ((i == 0 || b.Time.Minute % timeLabelMinutes == 0) && (i == 0 || x - lastTimeLabelX >= 52))
                {
                    AddCanvasText(b.Time.ToString("HH:mm"), x - 13, chartHeight - bottom + 10, Muted, 9, FontWeights.Normal);
                    lastTimeLabelX = x;
                }
            }
            DateTime testedStart = EvidenceTestStart(day), testedEnd = EvidenceTestEnd(day);
            AddEvidenceRangeBoundary("TEST START", testedStart, bars, left, candleWidth, top, chartHeight - bottom, Cyan);
            AddEvidenceRangeBoundary("TEST END", testedEnd, bars, left, candleWidth, top, chartHeight - bottom, Gold);
            // Default view is intentionally quiet: one short W/L/E circle above each entry candle.
            // Exact entry/exit lines and price tags exist only while one circle is selected.
            // Several historical references can resolve to the exact same one-minute impulse bar.
            // Preserve every row in the ledger, but draw one compact aggregated badge on that bar so
            // visual validation remains readable instead of stacking W/L/E circles on one candle.
            var markerGroups = marks.Select(mark => new { Event = mark, Index = EvidenceEntryBarIndex(bars, mark) })
                .Where(marker => marker.Index >= 0).GroupBy(marker => marker.Index).OrderBy(group => group.Key).ToList();
            var pinRightEdges = new double[] { double.MinValue, double.MinValue, double.MinValue, double.MinValue };
            for (int markerGroupIndex = 0; markerGroupIndex < markerGroups.Count; markerGroupIndex++)
            {
                List<KeystoneArcEvent> groupedEvents = markerGroups[markerGroupIndex].Select(marker => marker.Event).OrderBy(record => record.TriggerTime).ToList();
                KeystoneArcEvent e = groupedEvents[0];
                int i = markerGroups[markerGroupIndex].Key;
                double x = left + i * candleWidth + candleWidth / 2.0;
                // A dark blue arrow with a pale halo remains visible over both bullish and bearish candles.
                Brush setupBrush = e.SetupClass == "DT" ? Cyan : (e.SetupClass == "FVG" ? Orchid : EntryInk);
                bool allLiveSkipped = string.Equals(config.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase) && groupedEvents.All(IsLiveSkipped);
                Brush resultBrush = allLiveSkipped ? Muted : (e.Outcome == "UNVERIFIED 1M" ? Cyan : (e.Outcome == "WIN" ? WinPurple : (e.Outcome.StartsWith("LOSS") ? LossAmber : ExitIce)));
                KeystoneArcEvent selectedInGroup = selectedEvidenceEvent == null ? null : groupedEvents.FirstOrDefault(record => string.Equals(record.Id, selectedEvidenceEvent.Id, StringComparison.Ordinal));
                bool selectedMark = selectedInGroup != null;
                bool pulseOn = selectedMark && evidenceSelectionPulseOn;
                KeystoneArcEvent markerEvent = selectedInGroup ?? e;
                Action<UIElement> selectEvent = delegate(UIElement element) { element.PreviewMouseLeftButtonDown += delegate { evidenceSelectionClickHandled = true; pendingEvidenceSelectionAction = delegate { ShowEvidenceEventGroupDetail(markerEvent, groupedEvents); }; }; };
                if (chartReviewFvgZonesBox != null && chartReviewFvgZonesBox.IsChecked == true && (e.SetupClass == "FVG" || e.SetupClass == "DT") && !double.IsNaN(e.FvgLower) && !double.IsNaN(e.FvgUpper))
                {
                    int fvgIndex; if (index.TryGetValue(e.FvgFormedTime, out fvgIndex))
                    {
                        var zone = new System.Windows.Shapes.Rectangle { Width = Math.Max(candleWidth, (i - fvgIndex + 1) * candleWidth), Height = Math.Abs(y(e.FvgUpper) - y(e.FvgLower)), Fill = Orchid, Stroke = Orchid, StrokeThickness = 1, Opacity = 0.16 };
                        Canvas.SetLeft(zone, left + fvgIndex * candleWidth); Canvas.SetTop(zone, Math.Min(y(e.FvgUpper), y(e.FvgLower))); evidenceCanvas.Children.Add(zone);
                    }
                }
                double wickTop = y(bars[i].High);
                double wickBottom = y(bars[i].Low);
                int pinLevel = 0;
                bool placed = false;
                for (int level = 0; level < pinRightEdges.Length; level++)
                {
                    if (x - pinRightEdges[level] >= 31) { pinLevel = level; placed = true; break; }
                }
                if (!placed) pinLevel = Array.IndexOf(pinRightEdges, pinRightEdges.Min());
                // W/L/EXIT is tied to the actual bar containing the first one-minute high that
                // reached the entry price, not to a shifted label or a later exit bar.
                double markerX = x;
                bool shortMark = string.Equals(e.Direction, "SHORT", StringComparison.OrdinalIgnoreCase);
                double pinY = shortMark ? Math.Min(chartHeight - bottom - 14, wickBottom + 5 + pinLevel * 14) : Math.Max(top + 6, wickTop - 18 - pinLevel * 14);
                double pinX = Math.Max(left, markerX - 7);
                int groupWins = groupedEvents.Count(record => record.Outcome == "WIN"), groupLosses = groupedEvents.Count(record => record.Outcome.StartsWith("LOSS")), groupExits = groupedEvents.Count(record => record.Outcome == "SESSION EXIT");
                // A setup-only marker is a long-entry candidate, not a sell signal.  The prior
                // "S" label was ambiguous and made every unresolved marker look like SELL.
                string resultShort = groupedEvents.Count == 1 ? EvidenceDisplayTag(e) : groupedEvents.Count.ToString(CultureInfo.InvariantCulture);
                if (groupedEvents.Count > 1 && !allLiveSkipped) resultBrush = groupWins > 0 && groupLosses == 0 && groupExits == 0 ? WinPurple : (groupLosses > 0 && groupWins == 0 && groupExits == 0 ? LossAmber : Cyan);
                Border pin = AddCanvasResultCircle(resultShort, pinX, pinY, resultBrush, selectedMark, pulseOn, animateMarkers, markerGroupIndex);
                pin.ToolTip = (shortMark ? "SHORT " : "LONG ") + (allLiveSkipped ? "VALID DETECTED SETUP • RAW " + e.Outcome + " • NOT SELECTED IN FINAL LIVE LEDGER" : (groupedEvents.Count == 1 ? e.Outcome : (groupedEvents.Count + " setups on this exact entry bar • " + groupWins + " W / " + groupLosses + " L / " + groupExits + " session exit")));
                selectEvent(pin);
                double leaderStartY = shortMark ? pinY : pinY + 14;
                AddDashedEvidenceLeader(markerX, leaderStartY, x, shortMark ? wickBottom + 2 : wickTop - 2, resultBrush, selectedMark ? (pulseOn ? 1.0 : 0.62) : 0.48, selectEvent);
                if (selectedMark)
                {
                    AddSelectedEvidenceEntry(selectedInGroup, bars, x, y(selectedInGroup.Entry), left, candleWidth, width, y, setupBrush, resultBrush, pulseOn, selectEvent);
                }
                pinRightEdges[pinLevel] = pinX + 14;
            }
            AddCanvasText(symbol + " • TEST WINDOW " + testedStart.ToString("yyyy-MM-dd HH:mm") + " → " + testedEnd.ToString("yyyy-MM-dd HH:mm") + " • " + EvidenceContextMinutes(chartMinutes) + "M LEFT CONTEXT • DIRECT NINJATRADER " + chartMinutes + "M BARS • " + bars.Count + " CANDLES • " + marks.Count + " LEDGER " + (marks.Count == 1 ? "MARK" : "MARKS") + " / " + markerGroups.Count + " ENTRY-BAR " + (markerGroups.Count == 1 ? "BADGE" : "BADGES"), left, 4, Cyan, 12, FontWeights.Bold);
            UpdateEvidenceNavigationBars(allBars, visibleCount);
            SetEvidenceStatus("DIRECT " + chartMinutes + "M EVIDENCE READY • " + bars.Count + " OF " + allBars.Count + " CANDLES • " + marks.Count + " SETUPS / " + markerGroups.Count + " ENTRY-BAR " + (markerGroups.Count == 1 ? "BADGE" : "BADGES") + " • " + (config.BhAggressionFilter == "STRONGER" ? "STRONGER BH FILTER" : "ALL VALID BH") + " • DRAG THE PLOT TO PAN • ZOOM ON THE BOTTOM/RIGHT AXES", Green);
            if (selectedEvidenceEvent != null && !marks.Any(record => record.Id == selectedEvidenceEvent.Id)) ClearEvidenceSelection(false);
            // Cache exactly what the lightweight crosshair-only overlay needs, then draw it once
            // on top of the freshly rebuilt chart. Every subsequent pure-hover MouseMove reuses
            // this cached layout instead of re-running the block above.
            evidenceLayoutLeft = left; evidenceLayoutRight = right; evidenceLayoutTop = top; evidenceLayoutBottom = bottom;
            evidenceLayoutWidth = width; evidenceLayoutHeight = chartHeight;
            evidenceLayoutMinPrice = min; evidenceLayoutMaxPrice = max;
            evidenceLayoutCandleWidth = candleWidth;
            evidenceLayoutBars = bars;
            if (!evidencePanning && evidencePanOverscrollTransform != null) evidencePanOverscrollTransform.X = 0;
            EnsureEvidenceCrosshairElements();
            UpdateEvidenceCrosshairOverlay();
        }

        // Creates the four crosshair overlay elements (two dashed lines, two labels) once and
        // re-adds them after RenderEvidenceChart's Children.Clear(). They are reused rather than
        // rebuilt, and IsHitTestVisible = false keeps them from stealing candle hover/click events.
        private void EnsureEvidenceCrosshairElements()
        {
            if (evidenceCanvas == null) return;
            if (evidenceCrosshairHLine != null && evidenceCanvas.Children.Contains(evidenceCrosshairHLine)) return;
            Brush crossBrush = Gold;
            evidenceCrosshairHLine = new System.Windows.Shapes.Line { Stroke = crossBrush, StrokeThickness = 0.9, Opacity = 0.9, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            evidenceCrosshairVLine = new System.Windows.Shapes.Line { Stroke = crossBrush, StrokeThickness = 0.9, Opacity = 0.9, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            evidenceCrosshairPriceLabel = new TextBlock { Foreground = crossBrush, FontSize = 10, FontWeight = FontWeights.Bold, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            evidenceCrosshairTimeLabel = new TextBlock { Foreground = crossBrush, FontSize = 10, FontWeight = FontWeights.Bold, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            evidenceCanvas.Children.Add(evidenceCrosshairHLine);
            evidenceCanvas.Children.Add(evidenceCrosshairVLine);
            evidenceCanvas.Children.Add(evidenceCrosshairPriceLabel);
            evidenceCanvas.Children.Add(evidenceCrosshairTimeLabel);
            System.Windows.Controls.Panel.SetZIndex(evidenceCrosshairHLine, 1000); System.Windows.Controls.Panel.SetZIndex(evidenceCrosshairVLine, 1000);
            System.Windows.Controls.Panel.SetZIndex(evidenceCrosshairPriceLabel, 1001); System.Windows.Controls.Panel.SetZIndex(evidenceCrosshairTimeLabel, 1001);
        }

        // The cheap path: repositions the existing crosshair overlay from the layout cached by
        // the last full render. No Children.Clear(), no candle/marker rebuild, no new shapes -
        // just moving four existing elements, so a pure hover stays smooth however many candles
        // are on screen.
        private void UpdateEvidenceCrosshairOverlay()
        {
            if (evidenceCanvas == null || evidenceLayoutBars == null || evidenceLayoutBars.Count == 0 || evidenceCrosshairHLine == null) return;
            bool visible = evidenceCrosshairVisible
                && evidenceCrosshairPoint.X >= evidenceLayoutLeft && evidenceCrosshairPoint.X <= evidenceLayoutWidth - evidenceLayoutRight
                && evidenceCrosshairPoint.Y >= evidenceLayoutTop && evidenceCrosshairPoint.Y <= evidenceLayoutHeight - evidenceLayoutBottom;
            Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
            evidenceCrosshairHLine.Visibility = v; evidenceCrosshairVLine.Visibility = v;
            evidenceCrosshairPriceLabel.Visibility = v; evidenceCrosshairTimeLabel.Visibility = v;
            if (!visible) return;
            evidenceCrosshairHLine.X1 = evidenceLayoutLeft; evidenceCrosshairHLine.X2 = evidenceLayoutWidth - evidenceLayoutRight;
            evidenceCrosshairHLine.Y1 = evidenceCrosshairHLine.Y2 = evidenceCrosshairPoint.Y;
            evidenceCrosshairVLine.Y1 = evidenceLayoutTop; evidenceCrosshairVLine.Y2 = evidenceLayoutHeight - evidenceLayoutBottom;
            evidenceCrosshairVLine.X1 = evidenceCrosshairVLine.X2 = evidenceCrosshairPoint.X;
            double plotHeight = Math.Max(1.0, evidenceLayoutHeight - evidenceLayoutTop - evidenceLayoutBottom);
            double cursorPrice = evidenceLayoutMaxPrice - (evidenceCrosshairPoint.Y - evidenceLayoutTop) / plotHeight * (evidenceLayoutMaxPrice - evidenceLayoutMinPrice);
            string symbol = evidenceLayoutBars.Count > 0 ? evidenceLayoutBars[0].Symbol : string.Empty;
            evidenceCrosshairPriceLabel.Text = cursorPrice.ToString(string.Equals(symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0.00", CultureInfo.InvariantCulture);
            Canvas.SetLeft(evidenceCrosshairPriceLabel, evidenceLayoutWidth - evidenceLayoutRight + 7); Canvas.SetTop(evidenceCrosshairPriceLabel, evidenceCrosshairPoint.Y - 8);
            int cursorIndex = Math.Max(0, Math.Min(evidenceLayoutBars.Count - 1, (int)Math.Floor((evidenceCrosshairPoint.X - evidenceLayoutLeft) / Math.Max(1.0, evidenceLayoutCandleWidth))));
            evidenceCrosshairTimeLabel.Text = evidenceLayoutBars[cursorIndex].Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Canvas.SetLeft(evidenceCrosshairTimeLabel, evidenceCrosshairPoint.X - 42); Canvas.SetTop(evidenceCrosshairTimeLabel, evidenceLayoutHeight - evidenceLayoutBottom + 26);
        }

        // Coalesces any number of pan/zoom/hover updates that arrive within one screen refresh
        // into a single real render, capped at the display refresh rate (~60 fps via
        // CompositionTarget.Rendering) instead of the raw mouse-event rate. This is the core fix
        // for the choppy drag/zoom: previously every MouseMove tick rebuilt the entire candle and
        // marker canvas synchronously, so perceived smoothness was limited by render cost, not by
        // the monitor. fullRebuild=false is used for a pure hover, which only needs the cheap
        // crosshair reposition, not a full RenderEvidenceChart().
        private void RequestEvidenceRender(bool fullRebuild)
        {
            if (fullRebuild) evidenceFullRenderNeeded = true;
            if (evidenceRenderQueued) return;
            evidenceRenderQueued = true;
            CompositionTarget.Rendering += EvidenceRenderTick;
        }

        private void EvidenceRenderTick(object sender, EventArgs e)
        {
            CompositionTarget.Rendering -= EvidenceRenderTick;
            evidenceRenderQueued = false;
            if (evidenceCanvas == null || evidenceBars == null || evidenceBars.Count == 0) return;
            if (evidenceFullRenderNeeded) { evidenceFullRenderNeeded = false; RenderEvidenceChart(); }
            else UpdateEvidenceCrosshairOverlay();
        }

        private List<KeystoneArcBar> FilterEvidenceBars(List<KeystoneArcBar> source)
        {
            DateTime day;
            if (!DateTime.TryParseExact(evidenceDateBox == null ? string.Empty : evidenceDateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return new List<KeystoneArcBar>();
            DateTime sessionStart, sessionEnd; EvidenceDisplayBounds(day, out sessionStart, out sessionEnd);
            DateTime visibleStart = sessionStart.AddMinutes(-EvidenceContextMinutes());
            IEnumerable<KeystoneArcBar> q = source.Where(x => x.Time >= visibleStart && x.Time <= sessionEnd);
            return q.OrderBy(x => x.Time).ToList();
        }

        private void EvidenceDisplayBounds(DateTime day, out DateTime start, out DateTime end)
        {
            start = ConfiguredSessionStart(day); end = TradingSessionEnd(day);
            string display = evidenceSessionFilterBox == null ? "FULL LOADED SESSION" : Convert.ToString(evidenceSessionFilterBox.SelectedItem);
            DateTime intradayDate = KeystoneArcEngine.UsesOvernightSessionDate(config) ? day.Date.AddDays(1) : day.Date;
            if (display == "ASIA 18:00-08:00") { start = day.Date.AddHours(18); end = day.Date.AddDays(1).AddHours(8); }
            else if (display == "NY EARLY 08:00-15:55") { start = intradayDate.AddHours(8); end = intradayDate.AddHours(15).AddMinutes(55); }
            else if (display == "NY OPEN 09:30-15:55") { start = intradayDate.AddHours(9).AddMinutes(30); end = intradayDate.AddHours(15).AddMinutes(55); }
            // Never show bars outside the study's loaded session. A narrower display filter is
            // allowed, but it cannot manufacture data that was not requested in Step 1.
            DateTime loadedStart = ConfiguredSessionStart(day), loadedEnd = TradingSessionEnd(day);
            if (start < loadedStart) start = loadedStart;
            if (end > loadedEnd) end = loadedEnd;
        }

        private int EvidenceContextMinutes()
        {
            return EvidenceContextMinutes(EvidenceSelectedMinutes());
        }

        private int EvidenceContextMinutes(int minutes)
        {
            // Match the BH request path: three prior selected-timeframe bars are sufficient for
            // the red/reference/next-break pattern. The old fixed 60-minute display suggested an
            // unnecessary history requirement and pushed Full Globex evidence into maintenance.
            return Math.Max(3, Math.Max(1, minutes) * 3);
        }

        private int EvidenceSelectedMinutes()
        {
            string value = evidenceTimeframeBox == null ? string.Empty : Convert.ToString(evidenceTimeframeBox.SelectedItem);
            int minutes;
            if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value.TrimEnd('M', 'm'), out minutes) && minutes > 0) return minutes;
            return config == null ? 5 : Math.Max(1, config.SetupMinutes);
        }

        private int EvidenceTimeLabelMinutes()
        {
            int minutes = EvidenceSelectedMinutes();
            if (minutes <= 1) return 5;
            if (minutes <= 5) return 15;
            if (minutes <= 15) return 30;
            if (minutes <= 30) return 60;
            return minutes;
        }

        private DateTime EvidenceTestStart(DateTime sessionDate)
        {
            DateTime start, endIgnored; EvidenceDisplayBounds(sessionDate, out start, out endIgnored);
            DateTime first = KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date;
            if (sessionDate.Date == first && config.Start > start) start = config.Start;
            return start;
        }

        private DateTime EvidenceTestEnd(DateTime sessionDate)
        {
            DateTime startIgnored, end; EvidenceDisplayBounds(sessionDate, out startIgnored, out end);
            DateTime last = KeystoneArcEngine.SessionGroupingDate(config.End, config).Date;
            if (sessionDate.Date == last && config.End < end) end = config.End;
            return end;
        }

        private void AddEvidenceRangeBoundary(string label, DateTime boundary, List<KeystoneArcBar> bars, double left, double candleWidth, double top, double bottom, Brush accent)
        {
            if (evidenceCanvas == null || bars == null || bars.Count == 0) return;
            int barIndex = bars.FindIndex(bar => bar.Time >= boundary);
            if (barIndex < 0) return;
            double boundaryX = left + barIndex * candleWidth + candleWidth / 2.0;
            evidenceCanvas.Children.Add(new System.Windows.Shapes.Line { X1 = boundaryX, X2 = boundaryX, Y1 = top, Y2 = bottom, Stroke = accent, StrokeThickness = 1.4, Opacity = 0.72 });
            AddCanvasText(label + " " + boundary.ToString("HH:mm"), Math.Min(evidenceCanvas.Width - 115, boundaryX + 4), top + 4, accent, 9, FontWeights.Bold);
        }

        private static string EvidenceBarTooltip(KeystoneArcBar b)
        {
            if (b == null) return string.Empty;
            string format = string.Equals(b.Symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0.00";
            return b.Time.ToString("yyyy-MM-dd HH:mm") + "\nO " + b.Open.ToString(format) + "  H " + b.High.ToString(format) + "\nL " + b.Low.ToString(format) + "  C " + b.Close.ToString(format);
        }

        private void ShowEvidenceHover(KeystoneArcBar b)
        {
            if (b == null || evidenceHoverText == null) return;
            string format = string.Equals(b.Symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0.00";
            evidenceHoverText.Text = "CANDLE • " + b.Symbol + " • " + b.Time.ToString("yyyy-MM-dd HH:mm") + "   |   OPEN " + b.Open.ToString(format) + "   HIGH " + b.High.ToString(format) + "   LOW " + b.Low.ToString(format) + "   CLOSE " + b.Close.ToString(format) + "   |   RANGE " + (b.High - b.Low).ToString(format);
            evidenceHoverText.Foreground = b.Close >= b.Open ? Cyan : Gold;
        }

        private void ShowEvidenceBarDetail(KeystoneArcBar b)
        {
            if (b == null) return;
            ShowEvidenceHover(b);
            if (evidenceSelectionTimer != null) { evidenceSelectionTimer.Stop(); evidenceSelectionTimer = null; }
            selectedEvidenceEvent = null; evidenceSelectionPulseOn = false; evidenceSelectionPulse = 0;
            if (evidenceBars != null && evidenceBars.Count > 0) RenderEvidenceChart();
            if (evidenceDetailText == null) return;
            if (evidenceDetailBorder != null) evidenceDetailBorder.Visibility = Visibility.Visible;
            string format = string.Equals(b.Symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0.00";
            evidenceDetailText.Foreground = Text;
            evidenceDetailText.Text = "CANDLE • " + b.Symbol + " • " + b.Time.ToString("yyyy-MM-dd HH:mm") + "\nOPEN " + b.Open.ToString(format) + "  |  HIGH " + b.High.ToString(format) + "  |  LOW " + b.Low.ToString(format) + "  |  CLOSE " + b.Close.ToString(format) + "\nRANGE " + (b.High - b.Low).ToString(format) + " • Click a W/L/EXIT circle to inspect that setup's exact entry and exit.";
            if (evidencePnlText != null) evidencePnlText.Text = "CANDLE INSPECTION • NO SETUP SELECTED";
        }

        private List<DateTime> EvidenceSessionDates(int maximum)
        {
            var result = new List<DateTime>();
            if (config == null || config.Start == DateTime.MinValue || config.End == DateTime.MinValue) return result;
            DateTime first = KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date;
            DateTime last = KeystoneArcEngine.SessionGroupingDate(config.End, config).Date;
            for (DateTime day = first; day <= last && (maximum <= 0 || result.Count < maximum); day = day.AddDays(1)) result.Add(day);
            if (result.Count == 0) result.Add(KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date);
            return result;
        }

        private string EvidenceSessionTabLabel(DateTime sessionDate)
        {
            if (KeystoneArcEngine.UsesOvernightSessionDate(config)) return sessionDate.ToString("ddd MMM dd") + "/" + sessionDate.AddDays(1).ToString("dd");
            return sessionDate.ToString("ddd MMM dd");
        }

        private int EvidenceSessionDateCount()
        {
            if (config == null || config.Start == DateTime.MinValue || config.End == DateTime.MinValue) return 0;
            DateTime first = KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date;
            DateTime last = KeystoneArcEngine.SessionGroupingDate(config.End, config).Date;
            return Math.Max(1, (int)(last - first).TotalDays + 1);
        }

        private List<KeystoneArcEvent> EvidenceEvents(string symbol, DateTime day)
        {
            string scope = evidenceScopeBox == null ? "ALL DETECTED" : Convert.ToString(evidenceScopeBox.SelectedItem);
            DateTime sessionStart, sessionEnd; EvidenceDisplayBounds(day, out sessionStart, out sessionEnd);
            IEnumerable<KeystoneArcEvent> source = evidencePreviewOwnLedger ? evidencePreviewEvents : events;
            IEnumerable<KeystoneArcEvent> q = source.Where(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase) && x.TriggerTime >= sessionStart && x.TriggerTime <= sessionEnd);
            if (scope == "ACCEPTED ONLY") q = q.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            if (scope == "SELECTED EVENT")
            {
                KeystoneArcEvent selected = SelectedReviewEvent();
                q = selected == null ? Enumerable.Empty<KeystoneArcEvent>() : q.Where(x => x.Id == selected.Id);
            }
            if (evidenceStrengthBox != null && string.Equals(Convert.ToString(evidenceStrengthBox.SelectedItem), "AGGRESSION-TAGGED SETUPS", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => string.Equals(x.StrengthTag, "AGGR", StringComparison.OrdinalIgnoreCase));
            return q.Where(EvidenceFilterIncludes).OrderBy(x => x.TriggerTime).ToList();
        }

        private bool EvidenceFilterIncludes(KeystoneArcEvent e)
        {
            if (e == null) return false;
            if (e.SetupClass == "BH" && chartReviewBhBox != null && chartReviewBhBox.IsChecked == false) return false;
            if (e.SetupClass == "FVG" && chartReviewFvgBox != null && chartReviewFvgBox.IsChecked == false) return false;
            if (e.SetupClass == "DT" && chartReviewDtBox != null && chartReviewDtBox.IsChecked == false) return false;
            if (e.Outcome == "WIN") return evidenceWinsBox == null || evidenceWinsBox.IsChecked != false;
            if (e.Outcome.StartsWith("LOSS")) return evidenceLossesBox == null || evidenceLossesBox.IsChecked != false;
            if (e.Outcome == "SESSION EXIT") return evidenceExitsBox == null || evidenceExitsBox.IsChecked != false;
            if (e.Outcome == "NO ENTRY DATA") return evidenceNoEntryBox != null && evidenceNoEntryBox.IsChecked == true;
            // A setup can be visualized even before a 1M execution series has passed the
            // aggregation check.  It is intentionally tagged SETUP rather than WIN or LOSS.
            return e.Outcome == "UNVERIFIED 1M";
        }

        private static string EvidenceOutcomeTag(KeystoneArcEvent e)
        {
            if (e.Outcome == "WIN") return "WIN";
            if (e.Outcome.StartsWith("LOSS")) return "LOSS";
            if (e.Outcome == "SESSION EXIT") return "EXIT";
            if (e.Outcome == "NO ENTRY DATA") return "NO ENTRY";
            if (e.Outcome == "UNVERIFIED 1M") return "SETUP";
            return e.Outcome ?? string.Empty;
        }

        private bool IsLiveSkipped(KeystoneArcEvent e)
        {
            return string.Equals(config.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(e.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(e.SkipReason);
        }

        private string EvidenceDisplayTag(KeystoneArcEvent e)
        {
            return e.Outcome == "UNVERIFIED 1M" ? (string.Equals(e.Direction, "SHORT", StringComparison.OrdinalIgnoreCase) ? "↓" : "↑") : (e.Outcome == "WIN" ? "W" : (e.Outcome.StartsWith("LOSS") ? "L" : "E"));
        }

        private TextBlock AddCanvasText(string value, double x, double y, Brush brush, double size, FontWeight weight)
        {
            if (evidenceCanvas == null) return null;
            var text = new TextBlock { Text = value, Foreground = brush, FontSize = size, FontWeight = weight, Background = null, Margin = new Thickness(0) };
            Canvas.SetLeft(text, x); Canvas.SetTop(text, y); evidenceCanvas.Children.Add(text); return text;
        }

        private Border AddCanvasBadge(string value, double x, double y, Brush outcomeBrush, bool selected)
        {
            if (evidenceCanvas == null) return null;
            var text = new TextBlock { Text = value, Foreground = Bg, FontSize = selected ? 11 : 9, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 2, 5, 2) };
            var badge = new Border { Background = outcomeBrush, BorderBrush = Text, BorderThickness = new Thickness(selected ? 2 : 1), CornerRadius = new CornerRadius(4), Opacity = selected ? 1.0 : 0.94, Child = text };
            Canvas.SetLeft(badge, x); Canvas.SetTop(badge, y); evidenceCanvas.Children.Add(badge); return badge;
        }

        private Border AddCanvasPin(string number, double x, double y, Brush outcomeBrush, bool selected)
        {
            if (evidenceCanvas == null) return null;
            var text = new TextBlock { Text = number, Foreground = EvidenceBg, FontSize = selected ? 10 : 8, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(3, 1, 3, 1) };
            var pin = new Border { Width = selected ? 22 : 18, Height = selected ? 18 : 16, Background = outcomeBrush, BorderBrush = selected ? Text : EvidenceBg, BorderThickness = new Thickness(selected ? 2 : 1), CornerRadius = new CornerRadius(9), Opacity = 1.0, Child = text };
            Canvas.SetLeft(pin, x); Canvas.SetTop(pin, y); evidenceCanvas.Children.Add(pin); return pin;
        }

        private Border AddCanvasResultCircle(string value, double x, double y, Brush outcomeBrush, bool selected, bool pulseOn, bool animate = false, int animationIndex = 0)
        {
            if (evidenceCanvas == null) return null;
            var text = new TextBlock { Text = value, Foreground = EvidenceBg, FontSize = selected ? 9 : 7, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            double finalOpacity = selected && !pulseOn ? 0.76 : 1.0;
            var circle = new Border
            {
                Width = selected && pulseOn ? 23 : (selected ? 20 : 14), Height = selected && pulseOn ? 23 : (selected ? 20 : 14),
                Background = outcomeBrush, BorderBrush = selected ? Text : EntryInk, BorderThickness = new Thickness(selected ? (pulseOn ? 3 : 2) : 1.25),
                CornerRadius = new CornerRadius(selected && pulseOn ? 12 : (selected ? 10 : 7)), Opacity = animate ? 0.0 : finalOpacity,
                Child = text, ToolTip = "Click for entry / exit price audit", RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(circle, x); Canvas.SetTop(circle, y); evidenceCanvas.Children.Add(circle);
            if (animate)
            {
                // A short, staggered pop-in - one orchestrated reveal when the session first
                // appears, not a per-card hover/idle effect. animateMarkers in RenderEvidenceChart
                // gates this to real view changes so panning/zooming never replays it.
                var scale = new ScaleTransform(0.4, 0.4);
                circle.RenderTransform = scale;
                TimeSpan delay = TimeSpan.FromMilliseconds(Math.Min(360, animationIndex * 14));
                var fade = new DoubleAnimation(0.0, finalOpacity, TimeSpan.FromMilliseconds(240)) { BeginTime = delay, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var grow = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(260)) { BeginTime = delay, EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
                circle.BeginAnimation(UIElement.OpacityProperty, fade);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
            return circle;
        }

        private void AddSelectedEvidenceEntry(KeystoneArcEvent e, List<KeystoneArcBar> bars, double entryX, double entryY, double left, double candleWidth, double chartWidth, Func<double, double> y, Brush entryBrush, Brush outcomeBrush, bool pulseOn, Action<UIElement> bind)
        {
            if (evidenceCanvas == null || e == null) return;
            double selectedThickness = pulseOn ? 5.5 : 3.8;
            var entryLine = new System.Windows.Shapes.Line { X1 = entryX - 30, X2 = entryX + 30, Y1 = entryY, Y2 = entryY, Stroke = entryBrush, StrokeThickness = selectedThickness, Opacity = pulseOn ? 1.0 : 0.80 };
            if (bind != null) bind(entryLine); evidenceCanvas.Children.Add(entryLine);
            AddEvidenceEntryPointer(entryX, entryY, entryBrush, true, string.Equals(e.Direction, "SHORT", StringComparison.OrdinalIgnoreCase), bind);
            AddCanvasPriceTag("ENTRY " + PriceText(e.Symbol, e.Entry), Math.Min(chartWidth - 136, entryX + 34), Math.Max(30, entryY - 15), entryBrush, pulseOn, bind);

            if (double.IsNaN(e.ExitPrice) || e.ExitTime == DateTime.MinValue) return;
            int exitIndex = bars.FindIndex(b => b.Time >= e.ExitTime);
            if (exitIndex < 0) exitIndex = bars.Count - 1;
            if (exitIndex < 0) return;
            double exitX = left + exitIndex * candleWidth + candleWidth / 2.0;
            double exitY = y(e.ExitPrice);
            var exitLine = new System.Windows.Shapes.Line { X1 = exitX - 30, X2 = exitX + 30, Y1 = exitY, Y2 = exitY, Stroke = outcomeBrush, StrokeThickness = selectedThickness, Opacity = pulseOn ? 1.0 : 0.80 };
            if (bind != null) bind(exitLine); evidenceCanvas.Children.Add(exitLine);
            AddCanvasPriceTag("EXIT " + PriceText(e.Symbol, e.ExitPrice), Math.Min(chartWidth - 128, exitX + 34), Math.Max(30, exitY - 15), outcomeBrush, pulseOn, bind);
        }

        private Border AddCanvasPriceTag(string value, double x, double y, Brush accent, bool pulseOn, Action<UIElement> bind)
        {
            if (evidenceCanvas == null) return null;
            var text = new TextBlock { Text = value, Foreground = Text, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 2, 6, 2) };
            var tag = new Border { Background = EvidenceBg, BorderBrush = accent, BorderThickness = new Thickness(pulseOn ? 2.5 : 1.5), CornerRadius = new CornerRadius(4), Opacity = pulseOn ? 1.0 : 0.92, Child = text };
            if (bind != null) bind(tag); Canvas.SetLeft(tag, x); Canvas.SetTop(tag, y); evidenceCanvas.Children.Add(tag); return tag;
        }

        private static string PriceText(string symbol, double value)
        {
            return value.ToString(string.Equals(symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? "0.0" : "0.00", CultureInfo.InvariantCulture);
        }

        private void AddEvidenceEntryPointer(double x, double entryY, Brush brush, bool selected, bool shortSide, Action<UIElement> bind)
        {
            if (evidenceCanvas == null) return;
            double size = selected ? 10 : 7;
            // Pale under-stroke separates the dark entry arrow from wicks; the ink stroke stays crisp.
            double wingY = shortSide ? entryY + size : entryY - size;
            double tipY = shortSide ? entryY + 1 : entryY - 1;
            var leftHalo = new System.Windows.Shapes.Line { X1 = x - size, X2 = x, Y1 = wingY, Y2 = tipY, Stroke = EntryHalo, StrokeThickness = selected ? 6 : 4 };
            var rightHalo = new System.Windows.Shapes.Line { X1 = x + size, X2 = x, Y1 = wingY, Y2 = tipY, Stroke = EntryHalo, StrokeThickness = selected ? 6 : 4 };
            var left = new System.Windows.Shapes.Line { X1 = x - size, X2 = x, Y1 = wingY, Y2 = tipY, Stroke = brush, StrokeThickness = selected ? 4 : 2.5 };
            var right = new System.Windows.Shapes.Line { X1 = x + size, X2 = x, Y1 = wingY, Y2 = tipY, Stroke = brush, StrokeThickness = selected ? 4 : 2.5 };
            if (bind != null) { bind(leftHalo); bind(rightHalo); }
            evidenceCanvas.Children.Add(leftHalo); evidenceCanvas.Children.Add(rightHalo);
            if (bind != null) { bind(left); bind(right); }
            evidenceCanvas.Children.Add(left); evidenceCanvas.Children.Add(right);
        }

        private void SetEvidenceZoom(double requested)
        {
            // Existing +/- controls remain a convenient linked zoom. Scale drags and wheel input
            // change one axis independently.
            evidenceZoom = Math.Max(0.04, Math.Min(40.0, requested));
            evidenceHorizontalZoom = evidenceZoom;
            evidenceVerticalZoom = evidenceZoom;
            evidenceFirstVisibleBar = 0;
            evidencePriceCenter = double.NaN;
            evidenceVisiblePriceRange = 0;
            UpdateEvidenceZoomText();
            if (evidenceBars != null && evidenceBars.Count > 0) RenderEvidenceChart();
        }

        private void UpdateEvidenceNavigationBars(List<KeystoneArcBar> allBars, int visibleCount)
        {
            evidenceNavigationUpdating = true;
            try
            {
                if (evidenceHorizontalScrollBar != null)
                {
                    int lastFirst = Math.Max(0, (allBars == null ? 0 : allBars.Count) - Math.Max(1, visibleCount));
                    evidenceHorizontalScrollBar.Minimum = 0;
                    evidenceHorizontalScrollBar.Maximum = lastFirst;
                    evidenceHorizontalScrollBar.SmallChange = 1;
                    evidenceHorizontalScrollBar.LargeChange = Math.Max(1, visibleCount / 2);
                    evidenceHorizontalScrollBar.IsEnabled = lastFirst > 0;
                    evidenceHorizontalScrollBar.Value = Math.Max(0, Math.Min(lastFirst, evidenceFirstVisibleBar));
                }
                if (evidenceVerticalScrollBar != null)
                {
                    if (allBars == null || allBars.Count == 0)
                    {
                        evidenceVerticalScrollBar.IsEnabled = false;
                        return;
                    }
                    double rawMin = allBars.Min(b => b.Low), rawMax = allBars.Max(b => b.High);
                    double padding = Math.Max((rawMax - rawMin) * 0.10, Math.Max(string.Equals(allBars[0].Symbol, "MGC", StringComparison.OrdinalIgnoreCase) ? 1.0 : 8.0, 0.0001));
                    evidenceScrollPriceMinimum = rawMin - padding;
                    evidenceScrollPriceMaximum = rawMax + padding;
                    double span = Math.Max(0.0001, evidenceScrollPriceMaximum - evidenceScrollPriceMinimum);
                    double center = double.IsNaN(evidencePriceCenter) ? (evidenceScrollPriceMinimum + evidenceScrollPriceMaximum) / 2.0 : evidencePriceCenter;
                    double value = Math.Max(0, Math.Min(100, (center - evidenceScrollPriceMinimum) * 100.0 / span));
                    evidenceVerticalScrollBar.Minimum = 0;
                    evidenceVerticalScrollBar.Maximum = 100;
                    evidenceVerticalScrollBar.SmallChange = 2;
                    evidenceVerticalScrollBar.LargeChange = 12;
                    evidenceVerticalScrollBar.IsEnabled = true;
                    evidenceVerticalScrollBar.Value = value;
                }
            }
            finally { evidenceNavigationUpdating = false; }
        }

        private double EvidenceRenderedCandleWidth()
        {
            int minutes = EvidenceSelectedMinutes();
            double baseWidth = minutes <= 1 ? 8 : (minutes <= 5 ? 14 : (minutes <= 30 ? 20 : 30));
            return Math.Max(0.35, baseWidth * Math.Max(0.04, Math.Min(40.0, evidenceHorizontalZoom)));
        }

        private bool AdjustEvidenceZoomAtPointer(MouseWheelEventArgs args)
        {
            if (evidenceCanvas == null || evidenceScroll == null || args == null) return false;
            Point chartPoint = args.GetPosition(evidenceCanvas);
            int axis = EvidenceScaleAxisAt(chartPoint);
            if (axis == 0)
            {
                // Background wheel movement pans the price viewport. Axis wheel movement remains
                // zoom-only, matching charting conventions without changing candle width while
                // the user reviews a setup.
                if (evidenceBars == null || evidenceBars.Count == 0 || evidenceVisiblePriceRange <= 0) return false;
                if (double.IsNaN(evidencePriceCenter)) evidencePriceCenter = (evidenceScrollPriceMinimum + evidenceScrollPriceMaximum) / 2.0;
                evidencePriceCenter += (args.Delta > 0 ? -1 : 1) * evidenceVisiblePriceRange * 0.10;
                // A fast wheel/trackpad can fire many deltas per frame; coalesce them the same
                // way as pan/scale-drag so the scroll wheel does not out-pace the renderer.
                RequestEvidenceRender(true);
                return true;
            }
            bool vertical = axis == 2;
            double factor = args.Delta > 0 ? 1.15 : (1.0 / 1.15);
            double oldAxis = vertical ? evidenceVerticalZoom : evidenceHorizontalZoom;
            double nextAxis = Math.Max(0.04, Math.Min(40.0, oldAxis * factor));
            if (Math.Abs(nextAxis - oldAxis) < 0.0001) return true;
            if (vertical) evidenceVerticalZoom = nextAxis; else evidenceHorizontalZoom = nextAxis;
            evidenceZoom = Math.Sqrt(evidenceHorizontalZoom * evidenceVerticalZoom);
            UpdateEvidenceZoomText();
            if (evidenceBars == null || evidenceBars.Count == 0) return true;
            RequestEvidenceRender(true);
            return true;
        }

        private int EvidenceScaleAxisAt(Point chartPoint)
        {
            if (evidenceCanvas == null) return 0;
            // The bottom labels are the time scale; the open right-hand gutter is reserved as a
            // price scale. Their zones deliberately do not overlap candle bodies or entry pins.
            if (chartPoint.X >= evidenceCanvas.Width - 78) return 2;
            if (chartPoint.Y >= evidenceCanvas.Height - 52) return 1;
            return 0;
        }

        private void UpdateEvidenceZoomText()
        {
            if (evidenceZoomText == null) return;
            evidenceZoomText.Text = "TIME " + Math.Round(evidenceHorizontalZoom * 100.0).ToString("0", CultureInfo.InvariantCulture) + "% • PRICE " + Math.Round(evidenceVerticalZoom * 100.0).ToString("0", CultureInfo.InvariantCulture) + "% • drag/plot wheel = pan • bottom scale = time zoom • right scale = price zoom";
        }

        private static int EvidenceEntryBarIndex(List<KeystoneArcBar> bars, KeystoneArcEvent e)
        {
            if (bars == null || bars.Count == 0 || e == null) return -1;
            DateTime entryTime = e.EntryTime == DateTime.MinValue ? e.TriggerTime : e.EntryTime;
            int result = -1;
            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].Time > entryTime) break;
                result = i;
            }
            return result >= 0 ? result : bars.FindIndex(b => b.Time >= entryTime);
        }

        private void ToggleEvidenceControls()
        {
            evidenceControlsVisible = !evidenceControlsVisible;
            if (evidenceControlsPanel != null) evidenceControlsPanel.Visibility = evidenceControlsVisible ? Visibility.Visible : Visibility.Collapsed;
            if (evidenceControlsToggle != null) evidenceControlsToggle.Content = evidenceControlsVisible ? "HIDE CONTROLS" : "SHOW CONTROLS";
        }

        private void AddDashedEvidenceLeader(double x1, double y1, double x2, double y2, Brush brush, double opacity, Action<UIElement> bind)
        {
            if (evidenceCanvas == null) return;
            double distance = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            int segments = Math.Max(4, Math.Min(80, (int)Math.Ceiling(distance / 12.0)));
            for (int i = 0; i < segments; i++)
            {
                if (i % 2 != 0) continue;
                double a = i / (double)segments, b = Math.Min(1.0, a + 0.62 / segments);
                var dash = new System.Windows.Shapes.Line
                {
                    X1 = x1 + (x2 - x1) * a, Y1 = y1 + (y2 - y1) * a,
                    X2 = x1 + (x2 - x1) * b, Y2 = y1 + (y2 - y1) * b,
                    Stroke = brush, StrokeThickness = 1, Opacity = opacity
                };
                if (bind != null) bind(dash);
                evidenceCanvas.Children.Add(dash);
            }
        }

        private void ShowEvidenceEventDetail(KeystoneArcEvent e)
        {
            bool changed = selectedEvidenceEvent == null ? e != null : (e == null || !string.Equals(selectedEvidenceEvent.Id, e.Id, StringComparison.Ordinal));
            selectedEvidenceEvent = e;
            if (evidenceDetailText == null) return;
            if (e == null)
            {
                evidenceDetailText.Text = "NO VISIBLE SETUP IS SELECTED • change the ledger scope or click an entry arrow when one is displayed.";
                evidenceDetailText.Foreground = Muted;
                return;
            }
            bool mgc = string.Equals(e.Symbol, "MGC", StringComparison.OrdinalIgnoreCase);
            double valuePerPoint = (mgc ? 10.0 : 2.0) * Math.Max(1, e.Quantity > 0 ? e.Quantity : config.Quantity);
            string source = mgc ? config.MgcOutcomeSource : config.MnqOutcomeSource;
            bool verifiedOutcome = e.Outcome != "UNVERIFIED 1M" && config.OutcomeModelEnabled == 1;
            bool liveSkipped = IsLiveSkipped(e);
            bool liveTraded = string.Equals(config.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase) && string.Equals(e.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase);
            bool asianLeg = string.Equals(e.SetupClass, "ASIA75", StringComparison.OrdinalIgnoreCase);
            bool shortLeg = string.Equals(e.Direction, "SHORT", StringComparison.OrdinalIgnoreCase);
            double peak = double.IsNaN(e.PeakAfterEntry) ? e.Entry : e.PeakAfterEntry;
            double trough = double.IsNaN(e.TroughAfterEntry) ? e.Entry : e.TroughAfterEntry;
            double mfe = (shortLeg ? e.Entry - trough : peak - e.Entry) * valuePerPoint;
            double mae = (shortLeg ? e.Entry - peak : trough - e.Entry) * valuePerPoint;
            string price = mgc ? "0.0" : "0.00";
            var sb = new StringBuilder();
            sb.Append("SELECTED • ").Append(e.Symbol).Append(" ").Append(e.SetupClass).Append(" • ").Append(e.Outcome).Append(" • ").Append(liveTraded ? "FINAL LIVE TRADE" : (liveSkipped ? "NOT TRADED • " + e.SkipReason : e.ReviewState)).Append(" • ").Append(e.TriggerTime.ToString("yyyy-MM-dd HH:mm")).AppendLine();
            sb.Append("ENTRY ").Append(e.Entry.ToString(price)).Append(asianLeg ? "  |  CYCLE TARGET $" + config.AsianCycleTargetDollars.ToString("0") : "  |  TARGET " + e.Target.ToString(price)).Append("  |  STOP ").Append(e.Stop.ToString(price)).Append("  |  QTY ").Append(e.Quantity <= 0 ? config.Quantity.ToString() : e.Quantity.ToString()).Append("  |  RISK MODEL ").Append(e.RiskModel ?? config.StopMode).Append("  |  EXIT ").Append(double.IsNaN(e.ExitPrice) ? "n/a" : e.ExitPrice.ToString(price)).Append(" @ ").Append(e.ExitTime == DateTime.MinValue ? "n/a" : e.ExitTime.ToString("HH:mm")).AppendLine();
            if (verifiedOutcome) sb.Append(liveSkipped ? "RAW OUTCOME (NOT IN FINAL LIVE P/L) " : "P/L ").Append(e.GrossPnl.ToString("C0")).Append("  |  PEAK ").Append(peak.ToString(price)).Append(" (MFE ").Append(mfe.ToString("C0")).Append(")  |  LOW ").Append(trough.ToString(price)).Append(" (MAE ").Append(mae.ToString("C0")).Append(")").AppendLine();
            else sb.Append("OUTCOME / P&L: NOT AVAILABLE • setup placement only until matching 1M aggregation is proven.").AppendLine();
            sb.Append(verifiedOutcome ? "SOURCE: " + source : "SOURCE: " + source);
            evidenceDetailText.Text = sb.ToString();
            Brush outcomeAccent = !verifiedOutcome ? Cyan : (e.Outcome == "WIN" ? WinPurple : (e.Outcome.StartsWith("LOSS") ? LossAmber : ExitIce));
            evidenceDetailText.Foreground = outcomeAccent;
            if (evidenceDetailBorder != null) evidenceDetailBorder.BorderBrush = outcomeAccent;
            if (evidencePnlText != null)
            {
                evidencePnlText.Foreground = outcomeAccent;
                evidencePnlText.Text = verifiedOutcome ? (liveSkipped ? "SKIPPED AFTER DAILY LOCK • RAW " : (e.Outcome == "WIN" ? "WIN" : (e.Outcome.StartsWith("LOSS") ? "LOSS" : "SESSION EXIT")) + "  •  FINAL LIVE ") + "P/L " + e.GrossPnl.ToString("C0") + "  •  ENTRY " + e.Entry.ToString(price) + "  →  EXIT " + (double.IsNaN(e.ExitPrice) ? "n/a" : e.ExitPrice.ToString(price)) : "SETUP PLACEMENT • OUTCOME UNVERIFIED • ENTRY " + e.Entry.ToString(price);
            }
            if (evidencePnlBorder != null) { evidencePnlBorder.BorderBrush = outcomeAccent; evidencePnlBorder.Visibility = Visibility.Visible; }
            if (evidenceDetailBorder != null) evidenceDetailBorder.Visibility = Visibility.Visible;
            if (changed && evidenceCanvas != null && evidenceBars != null && evidenceBars.Count > 0) RenderEvidenceChart();
            BeginEvidenceSelectionPulse();
        }

        private void ShowEvidenceEventGroupDetail(KeystoneArcEvent selected, List<KeystoneArcEvent> groupedEvents)
        {
            ShowEvidenceEventDetail(selected);
            if (groupedEvents == null || groupedEvents.Count <= 1 || evidenceDetailText == null) return;
            int wins = groupedEvents.Count(x => x.Outcome == "WIN");
            int losses = groupedEvents.Count(x => x.Outcome.StartsWith("LOSS"));
            int exits = groupedEvents.Count(x => x.Outcome == "SESSION EXIT");
            evidenceDetailText.Text += "\nSHARED ENTRY BAR: " + groupedEvents.Count + " detector events resolved on this one-minute impulse • " + wins + " WIN / " + losses + " LOSS / " + exits + " EXIT. The compact badge is intentionally aggregated; this panel shows the selected event's exact price path.";
        }

        private void BeginEvidenceSelectionPulse()
        {
            if (selectedEvidenceEvent == null || evidenceWindow == null) return;
            if (evidenceSelectionTimer != null) evidenceSelectionTimer.Stop();
            evidenceSelectionPulse = 0; evidenceSelectionPulseOn = true;
            evidenceSelectionTimer = new DispatcherTimer(DispatcherPriority.Background, evidenceWindow.Dispatcher) { Interval = TimeSpan.FromMilliseconds(340) };
            evidenceSelectionTimer.Tick += delegate
            {
                evidenceSelectionPulse++;
                evidenceSelectionPulseOn = !evidenceSelectionPulseOn;
                if (evidenceBars != null && evidenceBars.Count > 0) RenderEvidenceChart();
            };
            evidenceSelectionTimer.Start();
        }

        private void ClearEvidenceSelection(bool render)
        {
            if (evidenceSelectionTimer != null) { evidenceSelectionTimer.Stop(); evidenceSelectionTimer = null; }
            selectedEvidenceEvent = null; evidenceSelectionPulseOn = false; evidenceSelectionPulse = 0;
            if (evidenceDetailText != null) { evidenceDetailText.Text = "CLICK A W/L/EXIT CIRCLE TO AUDIT ONE SETUP. Click an open chart area or another result circle to clear or replace the selected entry."; evidenceDetailText.Foreground = Muted; }
            if (evidencePnlText != null) evidencePnlText.Text = string.Empty;
            if (evidencePnlBorder != null) evidencePnlBorder.Visibility = Visibility.Collapsed;
            if (evidenceDetailBorder != null) evidenceDetailBorder.Visibility = Visibility.Collapsed;
            if (render && evidenceBars != null && evidenceBars.Count > 0) RenderEvidenceChart();
        }

        private void SetEvidenceStatus(string text, Brush brush)
        {
            if (evidenceStatusText != null) { evidenceStatusText.Text = text; evidenceStatusText.Foreground = brush; }
        }

        private void UpdateEvidenceSessionMetrics(string symbol, DateTime day)
        {
            if (evidenceMetricsText == null) return;
            List<KeystoneArcEvent> rows = EvidenceEvents(symbol, day);
            int wins = rows.Count(x => x.Outcome == "WIN");
            int losses = rows.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
            int exits = rows.Count(x => x.Outcome == "SESSION EXIT");
            if (config.OutcomeModelEnabled != 1)
            {
                evidenceMetricsText.Foreground = Gold;
                evidenceMetricsText.Text = "FILTER TOTALS • " + symbol + " • " + CurrentEvidenceFilterLabel() + " • " + rows.Count + " SETUPS • OUTCOME / P&L NOT VERIFIED";
                return;
            }
            double gross = rows.Sum(x => x.GrossPnl);
            if (string.Equals(config.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase))
            {
                List<KeystoneArcEvent> liveRows = rows.Where(x => string.Equals(x.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase)).ToList();
                int liveWins = liveRows.Count(x => x.Outcome == "WIN"), liveLosses = liveRows.Count(x => x.Outcome.StartsWith("LOSS")), liveExits = liveRows.Count(x => x.Outcome == "SESSION EXIT");
                double livePnl = liveRows.Sum(x => x.GrossPnl);
                int notSelected = rows.Count(x => !string.Equals(x.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.SkipReason));
                evidenceMetricsText.Foreground = livePnl >= 0 ? Green : Red;
                evidenceMetricsText.Text = "RAW DETECTED • " + symbol + " • " + CurrentEvidenceFilterLabel() + " • " + rows.Count + " SETUPS • " + wins + " W • " + losses + " L • " + exits + " SESSION EXIT  |  FINAL LIVE • " + liveRows.Count + " TRADED • " + liveWins + " W • " + liveLosses + " L • " + liveExits + " SESSION EXIT • P/L " + livePnl.ToString("C0") + " • " + notSelected + " LATER / UNAVAILABLE";
                return;
            }
            evidenceMetricsText.Foreground = gross >= 0 ? Green : Red;
            evidenceMetricsText.Text = "FILTER TOTALS • " + symbol + " • " + CurrentEvidenceFilterLabel() + " • " + rows.Count + " SETUPS • " + wins + " W • " + losses + " L • " + exits + " SESSION EXIT • MODEL P/L " + gross.ToString("C0");
        }

        private static double CalculateLiveAccountDayPnl(IEnumerable<KeystoneArcEvent> source, KeystoneArcRunConfig cfg, out int traded)
        {
            traded = 0;
            double total = 0;
            double profitLock = Math.Max(0, cfg.DailyGoal);
            double lossLock = Math.Max(0, cfg.DailyLoss);
            foreach (KeystoneArcEvent e in source.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime))
            {
                if (profitLock > 0 && total >= profitLock) break;
                if (lossLock > 0 && total <= -lossLock) break;
                if (e.Outcome == "UNVERIFIED 1M" || e.Outcome == "NO ENTRY DATA") continue;
                total += e.GrossPnl;
                traded++;
            }
            return total;
        }

        // The live-account result is never the sum of every detected outcome.  It is the one
        // assigned ledger created by RUN LIVE ACCOUNT RESULTS after daily locks are applied.
        // All final live totals, reports, and comparison detail must read this same ledger.
        private List<KeystoneArcEvent> FinalLiveAccountLedger()
        {
            return events.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(x.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
        }

        private bool HasFinalLiveAccountLedger()
        {
            return accounts.Any(x => string.Equals(x.Name, "KA-LIVE", StringComparison.OrdinalIgnoreCase));
        }

        private string CurrentEvidenceFilterLabel()
        {
            string session = evidenceSessionFilterBox == null ? "FULL SESSION" : Convert.ToString(evidenceSessionFilterBox.SelectedItem);
            string strength = evidenceStrengthBox == null ? "ALL" : Convert.ToString(evidenceStrengthBox.SelectedItem);
            return session + " • " + strength;
        }

        private void CancelEvidenceRequest()
        {
            ++evidenceGeneration;
            try { if (evidenceRequest != null) evidenceRequest.Dispose(); } catch { }
            evidenceRequest = null;
        }

        private void ApplyChartReviewOptions()
        {
            KeystoneArcHub.ShowChartMarks = (chartMarksBox == null || chartMarksBox.IsChecked != false) && (chartReviewEnabledBox == null || chartReviewEnabledBox.IsChecked != false);
            KeystoneArcHub.ShowChartOutcomeLabels = outcomesBox == null || outcomesBox.IsChecked != false;
            KeystoneArcHub.ShowChartBh = chartReviewBhBox == null || chartReviewBhBox.IsChecked != false;
            KeystoneArcHub.ShowChartFvg = chartReviewFvgBox == null || chartReviewFvgBox.IsChecked != false;
            KeystoneArcHub.ShowChartDt = chartReviewDtBox == null || chartReviewDtBox.IsChecked != false;
            KeystoneArcHub.ShowChartWins = chartReviewWinsBox == null || chartReviewWinsBox.IsChecked != false;
            KeystoneArcHub.ShowChartLosses = chartReviewLossesBox == null || chartReviewLossesBox.IsChecked != false;
            KeystoneArcHub.ShowChartSessionExits = chartReviewExitsBox != null && chartReviewExitsBox.IsChecked == true;
            KeystoneArcHub.ShowChartNoEntryData = chartReviewNoEntryBox != null && chartReviewNoEntryBox.IsChecked == true;
            KeystoneArcHub.ShowChartFvgZones = chartReviewFvgZonesBox != null && chartReviewFvgZonesBox.IsChecked == true;
            KeystoneArcHub.MaxChartMarks = 250;
        }

        private void RenderPoolLedger()
        {
            if (poolAccountList != null) poolAccountList.Items.Clear();
            if (walkthroughAccountBox != null)
            {
                walkthroughSelectionUpdating = true;
                try { walkthroughAccountBox.Items.Clear(); }
                finally { walkthroughSelectionUpdating = false; }
            }
            if (poolAccountCardStack != null) poolAccountCardStack.Children.Clear();
            int assigned = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount));
            int skipped = events.Count(x => !string.IsNullOrWhiteSpace(x.SkipReason));
            int payouts = accounts.Sum(x => x.Payouts);
            double grossPayoutCash = accounts.Sum(x => x.PayoutGrossWithdrawn);
            double netPayoutCash = accounts.Sum(x => x.PayoutCash);
            int blown = accounts.Count(x => x.Blown);
            UpdatePoolMetricTiles();
            double totalCost = accounts.Sum(x => x.EvaluationCost);
            int allBlowoutEvents = accounts.Sum(x => x.FailedEvaluations + x.FailedFunded);
            int replacementsBought = config.EvaluationEnabled > 0 ? Math.Max(0, accounts.Sum(x => x.EvaluationPurchases) - accounts.Count) : 0;
            bool asianCopy = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            if (poolText != null)
            {
                if (IsOneDayAssignmentMode())
                {
                    int profitLocks = accounts.Count(x => x.DayLocked && x.DayPnl >= Math.Max(0, config.DailyGoal));
                    int lossLocks = accounts.Count(x => x.DayLocked && x.DayPnl <= -Math.Abs(config.DailyLoss));
                    poolText.Text = "ONE-DAY ALLOCATION • ELIGIBLE " + events.Count(x => x.ReviewState == "ACCEPTED") + " • ASSIGNED " + assigned + " • SKIPPED " + skipped + " • TRADED ACCOUNTS " + accounts.Count(x => x.Trades > 0) + " / " + accounts.Count + " • PROFIT LOCKS " + profitLocks + " • LOSS LOCKS " + lossLocks + " • UNUSED " + accounts.Count(x => x.Trades == 0) + " • ASSIGNED P/L " + Cash(accounts.Sum(x => x.TotalPnl)) + ". COLORS: BLUE traded/unlocked • GREEN daily profit lock • RED daily loss lock • GRAY unused. No evaluation, payout, cost, or blowout lifecycle is applied in a one-day study.";
                }
                else
                    poolText.Text = "ELIGIBLE " + events.Count(x => x.ReviewState == "ACCEPTED") + " • " + (asianCopy ? "COPIED LEGS " : "ASSIGNED ") + assigned + " • SKIPPED " + skipped + " • ACCOUNTS " + accounts.Count + " • TERMINAL ENDED " + blown + " • FAILURE EVENTS " + allBlowoutEvents + " • REPLACEMENTS BOUGHT " + replacementsBought + " • PAYOUT CYCLES " + payouts + " • CASH AFTER SHARE (PRE-COST) " + Cash(netPayoutCash) + " • TOTAL COST " + Cash(totalCost) + " • FULL NET AFTER ALL COSTS " + Cash(netPayoutCash - totalCost) + (asianCopy ? " • ASIAN: EACH RESOLVED SESSION IS COPIED TO ALL ACTIVE ACCOUNTS" : string.Empty);
            }
            int firstVisible = -1;
            int visible = 0;
            for (int i = 0; i < accounts.Count; i++)
            {
                KeystoneArcVirtualAccount a = accounts[i];
                if (poolAccountList != null)
                {
                    poolAccountList.Items.Add(a.Name + " • " + AccountPrimaryStage(a));
                }
                if (walkthroughAccountBox != null) walkthroughAccountBox.Items.Add(a.Name + " • " + AccountPrimaryStage(a));
                if (MatchesPoolStateFilter(a))
                {
                    if (firstVisible < 0) firstVisible = i;
                    visible++;
                    AddPoolAccountCard(a, i);
                }
            }
            if (poolAccountsHeadingText != null) poolAccountsHeadingText.Text = IsOneDayAssignmentMode() ? "ONE-DAY VIRTUAL ACCOUNTS • " + visible + " SHOWN / " + accounts.Count + " • WHEEL TO SCROLL • CLICK A CARD" : "VIRTUAL ACCOUNTS • " + visible + " SHOWN / " + accounts.Count + " • WHEEL TO SCROLL • CLICK A CARD";
            if (poolAccountList != null && firstVisible >= 0) poolAccountList.SelectedIndex = firstVisible;
            if (walkthroughAccountBox != null && firstVisible >= 0)
            {
                walkthroughSelectionUpdating = true;
                try { walkthroughAccountBox.SelectedIndex = firstVisible; }
                finally { walkthroughSelectionUpdating = false; }
            }
            UpdatePoolDetail();
            RenderFirstReturnDashboard();
            RenderPayoutCycleDashboard();
            RenderDailySessionScoreboard();
            RenderResearchFindings();
        }

        private List<KeystoneArcResearchFinding> BuildResearchFindings()
        {
            var findings = new List<KeystoneArcResearchFinding>();
            List<KeystoneArcEvent> eligible = (events ?? new List<KeystoneArcEvent>()).Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)).ToList();
            if (config == null || eligible.Count == 0)
            {
                findings.Add(new KeystoneArcResearchFinding { Title = "WAITING FOR A LOADED, ELIGIBLE RUN", Detail = "Request history and build the eligible ledger first. Findings remain empty rather than filling with generic strategy claims.", Accent = Muted });
                return findings;
            }
            bool outcomesReady = config.OutcomeModelEnabled == 1;
            findings.Add(new KeystoneArcResearchFinding
            {
                Title = "SCOPE GUARDRAIL",
                Detail = "This evidence uses only the loaded " + (config.SessionMode ?? "selected") .Replace("_", " ") + " session, " + config.SetupMinutes + "M setup series, " + config.Scope + " scope, and this selected date range. It cannot rank sessions or timeframes that were not separately requested and loaded.",
                Accent = Cyan
            });
            if (!outcomesReady)
            {
                findings.Add(new KeystoneArcResearchFinding { Title = "OUTCOME MATH BLOCKED", Detail = "The 1-minute validation gate has not passed, so win/loss and P/L recommendations are intentionally withheld. Setup counts remain evidence only.", Accent = Red });
                return findings;
            }
            int wins = eligible.Count(x => string.Equals(x.Outcome, "WIN", StringComparison.OrdinalIgnoreCase));
            int losses = eligible.Count(x => (x.Outcome ?? string.Empty).StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
            double total = eligible.Sum(x => x.GrossPnl);
            double winRate = wins + losses == 0 ? 0 : 100.0 * wins / (wins + losses);
            double avgWin = wins == 0 ? 0 : eligible.Where(x => string.Equals(x.Outcome, "WIN", StringComparison.OrdinalIgnoreCase)).Average(x => x.GrossPnl);
            double avgLoss = losses == 0 ? 0 : eligible.Where(x => (x.Outcome ?? string.Empty).StartsWith("LOSS", StringComparison.OrdinalIgnoreCase)).Average(x => x.GrossPnl);
            findings.Add(new KeystoneArcResearchFinding
            {
                Title = "CURRENT-RUN OUTCOME SNAPSHOT",
                Detail = eligible.Count + " eligible setups • " + wins + " wins / " + losses + " losses • " + winRate.ToString("0.0", CultureInfo.InvariantCulture) + "% resolved win rate • model P/L " + Cash(total) + " • average win " + Cash(avgWin) + " • average loss " + Cash(avgLoss) + ". " + (eligible.Count < 30 ? "Small sample: treat this only as a review clue, not an optimization result." : "Use a separate date range before trusting any parameter change."),
                Accent = total > 0 ? Green : (total < 0 ? Red : Gold)
            });
            findings.Add(new KeystoneArcResearchFinding
            {
                Title = "ACTIONABLE OBSERVATION • LOADED DATA ONLY",
                Detail = total > 0
                    ? "The selected " + config.Scope + " / " + (config.SessionMode ?? "selected").Replace("_", " ") + " / " + config.SetupMinutes + "M sample is positive at the currently loaded target and stop. Keep the base BH rule unchanged and challenge the same parameters on a separate date range before changing risk, session, or account count. This is a current-run observation, not a performance promise."
                    : "The selected " + config.Scope + " / " + (config.SessionMode ?? "selected").Replace("_", " ") + " / " + config.SetupMinutes + "M sample is not positive at the currently loaded target and stop. Do not infer that another session, timeframe, or parameter is better unless that alternative is explicitly loaded and compared. Audit the evidence charts and test a separate range before changing risk.",
                Accent = total > 0 ? Green : Red
            });
            foreach (var instrument in eligible.GroupBy(x => x.Symbol ?? "UNKNOWN").OrderByDescending(g => g.Sum(x => x.GrossPnl)))
            {
                int instrumentWins = instrument.Count(x => string.Equals(x.Outcome, "WIN", StringComparison.OrdinalIgnoreCase));
                int instrumentLosses = instrument.Count(x => (x.Outcome ?? string.Empty).StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
                double pnl = instrument.Sum(x => x.GrossPnl);
                findings.Add(new KeystoneArcResearchFinding
                {
                    Title = "INSTRUMENT EVIDENCE • " + instrument.Key,
                    Detail = instrument.Count() + " eligible setups • " + instrumentWins + " wins / " + instrumentLosses + " losses • model P/L " + Cash(pnl) + ". " + (instrument.Count() < 20 ? "Insufficient sample for a stronger-instrument conclusion." : (pnl > 0 ? "This instrument led the current loaded run; validate the same rule in a later independent range." : "This instrument did not lead the current loaded run; review context and risk before increasing exposure.")),
                    Accent = pnl > 0 ? Green : (pnl < 0 ? Red : Cyan)
                });
            }
            foreach (var band in eligible.GroupBy(x => (x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).Hour / 2).OrderByDescending(g => g.Sum(x => x.GrossPnl)).Take(3))
            {
                int hour = band.Key * 2;
                double pnl = band.Sum(x => x.GrossPnl);
                findings.Add(new KeystoneArcResearchFinding
                {
                    Title = "TIME-BAND HYPOTHESIS • " + hour.ToString("00") + ":00–" + ((hour + 2) % 24).ToString("00") + ":00 ET",
                    Detail = band.Count() + " selected-session setups • model P/L " + Cash(pnl) + ". This is an intraday band inside the already loaded session, not a comparison against unavailable sessions. Test any tighter time filter on a fresh holdout range before using it.",
                    Accent = pnl > 0 ? Green : (pnl < 0 ? Red : Gold)
                });
            }
            if (accounts.Count > 0 && config.EvaluationEnabled > 0)
            {
                KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
                findings.Add(new KeystoneArcResearchFinding
                {
                    Title = "LIFECYCLE / CAPITAL OBSERVATION",
                    Detail = capital.FirstPayoutReached
                        ? "First modeled payout: " + capital.FirstPayoutDate.ToString("yyyy-MM-dd") + " after " + Cash(capital.InvestmentThroughFirstPayoutDate) + " modeled evaluation investment. " + (capital.ProfitabilityReached ? "Cumulative modeled payout cash first covered cumulative cost on " + capital.ProfitabilityDate.ToString("yyyy-MM-dd") + "." : "Cumulative payout cash did not yet cover all modeled costs in this range.")
                        : "No modeled payout occurred in this range. Total modeled evaluation / replacement cost was " + Cash(capital.TotalEvaluationCost) + "; assess the selected lifecycle assumptions before extending the range.",
                    Accent = capital.ProfitabilityReached ? Green : (capital.FirstPayoutReached ? Gold : Orchid)
                });
            }
            if (comparisonRows != null && comparisonRows.Count > 0)
            {
                KeystoneArcComparisonRow bestComparison = comparisonRows.OrderByDescending(x => x.AssignedGross).ThenBy(x => x.Symbol).ThenBy(x => x.SetupMinutes).First();
                findings.Add(new KeystoneArcResearchFinding
                {
                    Title = "COMPARISON RESULT • EXPLICITLY LOADED SERIES",
                    Detail = comparisonRows.Count + " direct comparison row(s) were actually built. The highest assigned historical model gross in that in-sample table is " + bestComparison.Symbol + " " + bestComparison.SetupMinutes + "M at " + Cash(bestComparison.AssignedGross) + " with " + bestComparison.Setups + " setup(s). It is a comparison result, not proof that this timeframe or instrument is best outside the loaded sample.",
                    Accent = Orchid
                });
            }
            else findings.Add(new KeystoneArcResearchFinding { Title = "COMPARISON STATUS", Detail = "No broader session/timeframe comparison was loaded for this run. This screen therefore does not label any session or timeframe as best. Build direct comparison rows first, then validate any in-sample leader on separate dates.", Accent = Muted });
            if (optimizationRows != null && optimizationRows.Count > 0)
            {
                KeystoneArcOptimizationRow bestOptimization = optimizationRows.OrderByDescending(x => x.FinalPnl).ThenBy(x => x.MaximumDrawdown).First();
                findings.Add(new KeystoneArcResearchFinding { Title = "TARGET / STOP SENSITIVITY • IN-SAMPLE", Detail = "The saved target/stop grid contains " + optimizationRows.Count + " current-run scenario row(s). Its highest model P/L is " + bestOptimization.Scope + " " + bestOptimization.SetupMinutes + "M • target " + bestOptimization.TargetLabel + " • stop " + bestOptimization.StopLabel + " • P/L " + Cash(bestOptimization.FinalPnl) + " • max drawdown " + Cash(bestOptimization.MaximumDrawdown) + ". Treat it as parameter sensitivity only; it is not a validated optimal setting.", Accent = Gold });
            }
            if (events.Any(x => string.Equals(x.Symbol, "MGC", StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(new KeystoneArcResearchFinding
                {
                    Title = "MGC / MNQ CASH-MODEL AUDIT",
                    Detail = "Both instruments use the identical direct-1-minute entry, target/stop ordering, daily-lock, evaluation, payout, and replacement engine. With the selected " + config.Quantity + " micro contracts, MNQ uses $2 per point per micro and MGC uses $10 per dollar per micro: the entered cash target " + Cash(config.TargetDollars) + " and stop " + Cash(config.StopDollars) + " therefore remain the same cash result, while their required price movements differ. A negative MGC range reflects its loaded MGC setups and resolved 1-minute path, not a different lifecycle simulation.",
                    Accent = Cyan
                });
            }
            findings.Add(new KeystoneArcResearchFinding { Title = "NEXT VALIDATION STEP", Detail = "Keep the base BH rule unchanged for the next check. If reviewing a positive instrument or time band, run the same exact parameters on an out-of-sample date range. If comparing sessions or timeframes, request each series explicitly—filters never fabricate unrequested data.", Accent = Gold });
            return findings;
        }

        private void RenderResearchFindings()
        {
            if (researchFindingsStack == null) return;
            researchFindingsStack.Children.Clear();
            foreach (KeystoneArcResearchFinding finding in BuildResearchFindings().OrderBy(x => x.Title != null && x.Title.StartsWith("ACTIONABLE OBSERVATION", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
            {
                bool actionable = finding.Title != null && finding.Title.StartsWith("ACTIONABLE OBSERVATION", StringComparison.OrdinalIgnoreCase);
                var title = Txt(finding.Title, finding.Accent, actionable ? 15 : 12, FontWeights.Bold); title.Margin = new Thickness(8, actionable ? 8 : 5, 8, 1); title.TextWrapping = TextWrapping.Wrap;
                var detail = Txt(finding.Detail, Text, actionable ? 12 : 10, FontWeights.Normal); detail.Margin = new Thickness(8, 0, 8, actionable ? 10 : 6); detail.TextWrapping = TextWrapping.Wrap;
                var card = new StackPanel(); card.Children.Add(title); card.Children.Add(detail);
                researchFindingsStack.Children.Add(new Border { Background = actionable ? Card : Panel, BorderBrush = finding.Accent, BorderThickness = new Thickness(actionable ? 2.5 : 1.5), CornerRadius = new CornerRadius(5), Margin = new Thickness(3), Child = card });
            }
        }

        private bool MatchesPoolStateFilter(KeystoneArcVirtualAccount account)
        {
            if (IsOneDayAssignmentMode())
            {
                if (account == null) return false;
                if (account.Trades <= 0) return oneDayShowUnusedBox == null || oneDayShowUnusedBox.IsChecked != false;
                if (account.DayLocked && account.DayPnl >= Math.Max(0, config.DailyGoal)) return oneDayShowProfitLocksBox == null || oneDayShowProfitLocksBox.IsChecked != false;
                if (account.DayLocked) return oneDayShowLossLocksBox == null || oneDayShowLossLocksBox.IsChecked != false;
                return oneDayShowTradedBox == null || oneDayShowTradedBox.IsChecked != false;
            }
            string filter = poolStateFilterBox == null ? "ALL" : Convert.ToString(poolStateFilterBox.SelectedItem ?? "ALL");
            if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "FUNDED", StringComparison.OrdinalIgnoreCase)) return account != null && account.Funded && account.Payouts == 0;
            if (string.Equals(filter, "PAYOUT HISTORY", StringComparison.OrdinalIgnoreCase)) return account != null && account.Payouts > 0;
            if (string.Equals(filter, "EVALUATION", StringComparison.OrdinalIgnoreCase)) return account != null && !account.Funded && !account.Blown && !account.ReplacementPending;
            if (string.Equals(filter, "FIRM CAP WAIT", StringComparison.OrdinalIgnoreCase)) return account != null && account.FundedCapPending;
            if (string.Equals(filter, "BENCHED • PAYOUT GATE", StringComparison.OrdinalIgnoreCase)) return account != null && account.ReplacementPending && account.ReplacementBudgetBlocked;
            if (string.Equals(filter, "REPLACEMENT NEXT", StringComparison.OrdinalIgnoreCase)) return account != null && account.ReplacementPending;
            if (string.Equals(filter, "BLOWN / ENDED", StringComparison.OrdinalIgnoreCase)) return account != null && account.Blown && !account.ReplacementPending;
            return true;
        }

        private void UpdatePoolMetricTiles()
        {
            bool oneDay = IsOneDayAssignmentMode();
            RefreshResultsModePresentation(oneDay);
            int payoutCycles = accounts.Sum(x => x.Payouts);
            double grossWithdrawals = accounts.Sum(x => x.PayoutGrossWithdrawn);
            double netCash = accounts.Sum(x => x.PayoutCash);
            double evaluationCost = accounts.Sum(x => x.EvaluationCost);
            double netCashAfterCost = netCash - evaluationCost;
            KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
            bool continuousReplenishment = config != null && config.EvaluationEnabled > 0 && !capital.GateEnabled;
            int replacementPurchases = config.EvaluationEnabled > 0 ? Math.Max(0, accounts.Sum(x => x.EvaluationPurchases) - accounts.Count) : 0;
            int funded = accounts.Count(x => x.Funded);
            int terminalBlown = accounts.Count(x => x.Blown && !x.ReplacementPending);
            if (poolGrossWithdrawalMetric != null) { poolGrossWithdrawalMetric.Text = Cash(grossWithdrawals); poolGrossWithdrawalMetric.Foreground = grossWithdrawals > 0 ? Gold : Muted; }
            if (poolNetCashMetric != null) { poolNetCashMetric.Text = Cash(netCash); poolNetCashMetric.Foreground = netCash > 0 ? Green : Muted; }
            if (poolEvaluationCostMetric != null) { poolEvaluationCostMetric.Text = Cash(evaluationCost); poolEvaluationCostMetric.Foreground = evaluationCost > 0 ? Orchid : Muted; }
            if (continuousReplenishment)
            {
                string firstPayoutDate = capital.FirstPayoutReached ? capital.FirstPayoutDate.ToString("yyyy-MM-dd") : "NO PAYOUT IN RANGE";
                Brush firstPayoutBrush = capital.FirstPayoutReached ? Gold : Muted;
                UpdateMetricTile(poolInitialInvestmentMetric, "FIRST PAYOUT DATE", firstPayoutDate,
                    capital.FirstPayoutReached
                        ? "cash after share " + Cash(capital.PayoutCashThroughFirstPayoutDate) + " • " + capital.EvaluationPurchasesThroughFirstPayoutDate + " total eval purchases / " + capital.ReplacementPurchasesThroughFirstPayoutDate + " replenishment purchase(s) by that date"
                        : "no modeled payout was recorded in this selected range • current full-range evaluation/replacement cost " + Cash(capital.TotalEvaluationCost), firstPayoutBrush);
                UpdateMetricTile(poolPayoutAfterInitialMetric, "INVESTMENT TO FIRST PAYOUT", Cash(capital.InvestmentThroughFirstPayoutDate),
                    capital.FirstPayoutReached
                        ? "initial " + Cash(capital.InitialEvaluationInvestment) + " plus all evaluation/replacement purchases through the first payout • replenished slots before first payout " + capital.ReplenishedSlotsThroughFirstPayoutDate + " • net at first payout " + Cash(capital.NetCashThroughFirstPayoutDate)
                        : "continuous replacement remains enabled • no modeled payout in this selected range • total investment recorded so far", capital.FirstPayoutReached ? Orchid : Muted);
                string profitabilityDate = capital.ProfitabilityReached ? capital.ProfitabilityDate.ToString("yyyy-MM-dd") : "NO PROFITABILITY IN RANGE";
                Brush profitabilityBrush = capital.ProfitabilityReached ? Green : (capital.NetCashAtProfitability < 0 ? Red : Muted);
                UpdateMetricTile(poolCapitalAvailabilityMetric, "FIRST PROFITABLE DATE", profitabilityDate,
                    capital.ProfitabilityReached
                        ? "first date cumulative payout cash covered all modeled costs • cash " + Cash(capital.PayoutCashThroughProfitability) + " • investment " + Cash(capital.InvestmentThroughProfitability) + " • net " + Cash(capital.NetCashAtProfitability)
                        : "cumulative payout cash has not yet covered modeled evaluation/replacement spend • current full net " + Cash(capital.FullNetCashAfterAllCosts), profitabilityBrush);
            }
            else
            {
                UpdateMetricTile(poolInitialInvestmentMetric, "INITIAL EVAL INVESTMENT", Cash(capital.InitialEvaluationInvestment), "initial selected evaluation slot(s) only; excludes replacement purchases", capital.InitialEvaluationInvestment > 0 ? Gold : Muted);
                UpdateMetricTile(poolPayoutAfterInitialMetric, "PAYOUT CASH AFTER INITIAL INVESTMENT", Cash(capital.PayoutCashAfterInitialInvestment), "cash after share less initial evaluation investment only; excludes later replacement cost", capital.PayoutCashAfterInitialInvestment < 0 ? Red : (capital.PayoutCashAfterInitialInvestment > 0 ? Green : Muted));
                UpdateMetricTile(poolCapitalAvailabilityMetric, "REINVESTMENT CASH AVAILABLE", Cash(capital.ReplacementCashAvailable), capital.GateEnabled ? ("payout-funded replacement gate ON • pending slots " + capital.PendingReplacementSlots + " • cash required to release all pending slots " + Cash(capital.CashRequiredForPendingReplacements) + " • shortfall " + Cash(capital.ReplacementCashShortfall)) : "no evaluation replacement gate is active for this start mode", capital.ReplacementCashShortfall > 0 ? Gold : (capital.ReplacementCashAvailable > 0 ? Green : Muted));
            }
            if (poolFirmCapMetric != null)
            {
                if (config != null && config.FirmFundedCapEnabled > 0)
                {
                    int firms = accounts.Where(x => !string.IsNullOrEmpty(x.PropFirmCode)).Select(x => x.PropFirmCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    int capWaiting = accounts.Count(x => x.FundedCapPending);
                    UpdateMetricTile(poolFirmCapMetric, "FIRM FUNDED CAP", funded + " / " + (firms * config.MaxFundedPerFirm), firms + " modeled firm(s) • " + config.EvaluationSlotsPerFirm + " eval slots / firm • " + config.MaxFundedPerFirm + " max funded / firm • passed-eval waiting " + capWaiting, capWaiting > 0 ? Gold : Green);
                }
                else UpdateMetricTile(poolFirmCapMetric, "FIRM FUNDED CAP", "OFF", "all passed evaluations fund under the existing virtual-pool rules", Muted);
            }
            if (poolNetCashAfterCostMetric != null) { poolNetCashAfterCostMetric.Text = Cash(netCashAfterCost); poolNetCashAfterCostMetric.Foreground = netCashAfterCost < 0 ? Red : (netCashAfterCost > 0 ? Green : Muted); }
            if (poolPayoutCycleMetric != null) { poolPayoutCycleMetric.Text = payoutCycles.ToString(CultureInfo.InvariantCulture); poolPayoutCycleMetric.Foreground = payoutCycles > 0 ? Cyan : Muted; }
            if (poolFundedMetric != null) { poolFundedMetric.Text = funded.ToString(CultureInfo.InvariantCulture); poolFundedMetric.Foreground = funded > 0 ? Green : Muted; }
            int evaluationAvailable = accounts.Count(x => !x.Funded && !x.Blown && !x.ReplacementPending && !x.FundedCapPending);
            int evaluationActive = accounts.Count(x => !x.Funded && !x.Blown && !x.ReplacementPending && !x.FundedCapPending && x.Trades > 0);
            UpdateMetricTile(poolReplacementMetric, "EVAL AVAILABLE / ACTIVE", evaluationAvailable + " / " + evaluationActive,
                "open evaluation-stage slots / slots with one or more assignments in this loaded run", evaluationActive > 0 ? Orchid : Muted);
            if (poolBlownMetric != null) { poolBlownMetric.Text = terminalBlown.ToString(CultureInfo.InvariantCulture); poolBlownMetric.Foreground = terminalBlown > 0 ? Red : Muted; }
            int eligible = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            int assigned = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount));
            double assignedPnl = events.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)).Sum(x => x.GrossPnl);
            bool asianCopy = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            int purchases = accounts.Sum(x => x.EvaluationPurchases);
            double costs = evaluationCost;
            if (rangeEligibleMetric != null) { rangeEligibleMetric.Text = eligible.ToString(CultureInfo.InvariantCulture); rangeEligibleMetric.Foreground = eligible > 0 ? Cyan : Muted; }
            if (rangeAssignedMetric != null) { rangeAssignedMetric.Text = assigned.ToString(CultureInfo.InvariantCulture); rangeAssignedMetric.Foreground = assigned > 0 ? Green : Muted; rangeAssignedMetric.ToolTip = asianCopy ? "Resolved Asian legs copied to every active account; this is not a rotation count." : "BH events assigned to a first available virtual slot."; }
            if (rangePnlMetric != null) { rangePnlMetric.Text = Cash(assignedPnl); rangePnlMetric.Foreground = assignedPnl < 0 ? Red : (assignedPnl > 0 ? Green : Muted); rangePnlMetric.ToolTip = asianCopy ? "One-account raw Asian cycle P/L before the identical result is copied to each active account." : "All outcomes assigned to virtual account slots."; }
            if (rangePurchasesMetric != null) { rangePurchasesMetric.Text = purchases.ToString(CultureInfo.InvariantCulture); rangePurchasesMetric.Foreground = purchases > 0 ? Orchid : Muted; }
            if (rangeCostMetric != null) { rangeCostMetric.Text = Cash(costs); rangeCostMetric.Foreground = costs > 0 ? Orchid : Muted; }
            if (rangeInitialInvestmentMetric != null) { rangeInitialInvestmentMetric.Text = Cash(capital.InitialEvaluationInvestment); rangeInitialInvestmentMetric.Foreground = capital.InitialEvaluationInvestment > 0 ? Gold : Muted; }
            if (rangePayoutAfterInitialMetric != null) { rangePayoutAfterInitialMetric.Text = Cash(capital.PayoutCashAfterInitialInvestment); rangePayoutAfterInitialMetric.Foreground = capital.PayoutCashAfterInitialInvestment < 0 ? Red : (capital.PayoutCashAfterInitialInvestment > 0 ? Green : Muted); }
            if (rangeNetCashAfterCostMetric != null) { rangeNetCashAfterCostMetric.Text = Cash(netCashAfterCost); rangeNetCashAfterCostMetric.Foreground = netCashAfterCost < 0 ? Red : (netCashAfterCost > 0 ? Green : Muted); }
            if (rangeCapitalAvailabilityMetric != null) { rangeCapitalAvailabilityMetric.Text = Cash(capital.ReplacementCashAvailable); rangeCapitalAvailabilityMetric.Foreground = capital.ReplacementCashAvailable > 0 ? Green : Muted; }
            if (rangeBlowoutMetric != null) { rangeBlowoutMetric.Text = terminalBlown.ToString(CultureInfo.InvariantCulture); rangeBlowoutMetric.Foreground = terminalBlown > 0 ? Red : Muted; }
            if (oneDay)
            {
                int accountsTraded = accounts.Count(x => x.Trades > 0);
                int profitLocked = accounts.Count(x => x.DayLocked && x.DayPnl >= Math.Max(0, config.DailyGoal));
                int lossLocked = accounts.Count(x => x.DayLocked && x.DayPnl <= -Math.Abs(config.DailyLoss));
                int unused = accounts.Count(x => x.Trades == 0);
                int skipped = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.SkipReason));
                double assignedPnlOneDay = accounts.Sum(x => x.TotalPnl);
                if (oneDayEligibleMetric != null) { oneDayEligibleMetric.Text = eligible.ToString(CultureInfo.InvariantCulture); oneDayEligibleMetric.Foreground = eligible > 0 ? Cyan : Muted; }
                if (oneDayAssignedMetric != null) { oneDayAssignedMetric.Text = assigned.ToString(CultureInfo.InvariantCulture); oneDayAssignedMetric.Foreground = assigned > 0 ? Green : Muted; }
                if (oneDayAccountsTradedMetric != null) { oneDayAccountsTradedMetric.Text = accountsTraded + " / " + accounts.Count; oneDayAccountsTradedMetric.Foreground = accountsTraded > 0 ? Blue : Muted; }
                if (oneDayProfitLockedMetric != null) { oneDayProfitLockedMetric.Text = profitLocked.ToString(CultureInfo.InvariantCulture); oneDayProfitLockedMetric.Foreground = profitLocked > 0 ? Green : Muted; }
                if (oneDayLossLockedMetric != null) { oneDayLossLockedMetric.Text = lossLocked.ToString(CultureInfo.InvariantCulture); oneDayLossLockedMetric.Foreground = lossLocked > 0 ? Red : Muted; }
                if (oneDayUnusedMetric != null) { oneDayUnusedMetric.Text = unused.ToString(CultureInfo.InvariantCulture); oneDayUnusedMetric.Foreground = unused > 0 ? Muted : Green; }
                if (oneDaySkippedMetric != null) { oneDaySkippedMetric.Text = skipped.ToString(CultureInfo.InvariantCulture); oneDaySkippedMetric.Foreground = skipped > 0 ? Gold : Muted; }
                if (oneDayPnlMetric != null) { oneDayPnlMetric.Text = Cash(assignedPnlOneDay); oneDayPnlMetric.Foreground = assignedPnlOneDay < 0 ? Red : (assignedPnlOneDay > 0 ? Green : Muted); }
                if (oneDayRangeSummaryText != null)
                {
                    oneDayRangeSummaryText.Text = "ONE-DAY SCORECARD\n" +
                        "ELIGIBLE " + eligible + " • ASSIGNED " + assigned + " • SKIPPED " + skipped + "\n" +
                        "ACCOUNTS TRADED " + accountsTraded + " / " + accounts.Count + " • UNUSED " + unused + "\n" +
                        "WINS " + accounts.Sum(x => x.Wins) + " • LOSSES " + accounts.Sum(x => x.Losses) + " • ASSIGNED P/L " + Cash(assignedPnlOneDay) + "\n" +
                        "DAILY LOCKS: " + profitLocked + " PROFIT @ " + Cash(config.DailyGoal) + " • " + lossLocked + " LOSS @ -" + Cash(Math.Abs(config.DailyLoss)).Replace("-$", "$") + "\n" +
                        "No evaluation, payout, cost, replacement, or blowout lifecycle applies to this one-day allocation.";
                    oneDayRangeSummaryText.Foreground = assignedPnlOneDay < 0 ? Red : (assignedPnlOneDay > 0 ? Green : Cyan);
                }
            }
        }

        private void RenderPayoutCycleDashboard()
        {
            if (payoutCycleDashboardStack == null) return;
            RenderPayoutAccountDashboard();
            payoutCycleDashboardStack.Children.Clear();
            List<KeystoneArcPayoutCycleRow> cycles = KeystoneArcEngine.BuildPayoutCycleRows(accounts, config);
            if (payoutCycleMonthFilterBox != null)
            {
                string keep = Convert.ToString(payoutCycleMonthFilterBox.SelectedItem ?? "ALL MONTHS");
                payoutCycleFilterUpdating = true;
                try
                {
                    payoutCycleMonthFilterBox.Items.Clear();
                    payoutCycleMonthFilterBox.Items.Add("ALL MONTHS");
                    foreach (string month in cycles.Select(x => x.Day.ToString("yyyy-MM")).Distinct().OrderBy(x => x)) payoutCycleMonthFilterBox.Items.Add(month);
                    payoutCycleMonthFilterBox.SelectedItem = cycles.Any(x => x.Day.ToString("yyyy-MM") == keep) ? keep : "ALL MONTHS";
                }
                finally { payoutCycleFilterUpdating = false; }
            }
            string selectedMonth = payoutCycleMonthFilterBox == null ? "ALL MONTHS" : Convert.ToString(payoutCycleMonthFilterBox.SelectedItem ?? "ALL MONTHS");
            if (selectedMonth != "ALL MONTHS") cycles = cycles.Where(x => x.Day.ToString("yyyy-MM") == selectedMonth).ToList();
            if (cycles.Count == 0)
            {
                payoutCycleDashboardStack.Children.Add(Txt(accounts.Count == 0
                    ? "RUN THE VIRTUAL POOL TO BUILD DATED PAYOUT CYCLES."
                    : "NO MODELLED PAYOUT DATE IN THIS SELECTED RANGE / MONTH. Assigned-trade and lifecycle audit remains available.", Muted, 11, FontWeights.Bold));
                return;
            }
            int shownPayoutCycles = cycles.Sum(x => x.PayoutCycles);
            int shownPaidAccounts = cycles.SelectMany(x => x.PayoutContributors ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            double shownCashAfterShare = cycles.Sum(x => x.NetCashAfterShare);
            double shownNetAfterMonthCosts = cycles.Sum(x => x.NetCashAfterEvaluationCost);
            string summaryPeriod = selectedMonth == "ALL MONTHS" ? "ALL DISPLAYED MONTHS" : selectedMonth;
            payoutCycleDashboardStack.Children.Add(Txt("PAYOUT SUMMARY • " + summaryPeriod + " • PERIOD FLOW ONLY", Cyan, 11, FontWeights.Bold));
            var summaryMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(3, 0, 3, 4) };
            CycleMetric(summaryMetrics, "TOTAL PAYOUTS", shownPayoutCycles.ToString(CultureInfo.InvariantCulture), Gold);
            CycleMetric(summaryMetrics, "ACCOUNTS PAID", shownPaidAccounts.ToString(CultureInfo.InvariantCulture), Blue);
            CycleMetric(summaryMetrics, "CASH AFTER SHARE", Cash(shownCashAfterShare), Green);
            CycleMetric(summaryMetrics, "NET AFTER MONTH COSTS", Cash(shownNetAfterMonthCosts), shownNetAfterMonthCosts < 0 ? Red : Green);
            payoutCycleDashboardStack.Children.Add(new Border { Background = Panel, BorderBrush = selectedMonth == "ALL MONTHS" ? Cyan : Gold, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Margin = new Thickness(2), Child = summaryMetrics });
            payoutCycleDashboardStack.Children.Add(Txt("TOTAL PAYOUTS counts withdrawal cycles on dates in this view. ACCOUNTS PAID counts unique accounts paid in this view. NET AFTER MONTH COSTS equals payout cash after share less only evaluation/replacement costs recorded on those payout dates; it is not a cumulative all-range balance.", Muted, 9, FontWeights.Bold));
            payoutCycleDashboardStack.Children.Add(Txt("EACH COLORED CARD IS ONE PAYOUT DATE. COST THROUGH DATE INCLUDES INITIAL EVALUATIONS (INCLUDING THE FIRST $120 PER SLOT) PLUS REPLACEMENTS PURCHASED UP TO THAT PAYOUT; it is modeled scenario cost, not a trading loss.", Gold, 10, FontWeights.Bold));
            foreach (KeystoneArcPayoutCycleRow cycle in cycles)
            {
                var card = new Grid { Margin = new Thickness(2), Background = Panel };
                card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var header = Txt("CYCLE " + cycle.CycleNumber + " • " + cycle.Day.ToString("yyyy-MM-dd") + " • " + cycle.PayoutAccounts + " ACCOUNT" + (cycle.PayoutAccounts == 1 ? string.Empty : "S") + " PAID • " + string.Join(", ", cycle.PayoutContributors), Gold, 12, FontWeights.Bold); header.Margin = new Thickness(7, 5, 7, 2); header.TextWrapping = TextWrapping.Wrap; card.Children.Add(header);
                var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(4, 0, 4, 3) };
                CycleMetric(metrics, "GROSS WITHDRAWAL", Cash(cycle.GrossWithdrawals), Gold);
                CycleMetric(metrics, "CASH AFTER SHARE", Cash(cycle.NetCashAfterShare), Green);
                CycleMetric(metrics, "ALL COST THROUGH DATE", Cash(cycle.CumulativeEvaluationCostThroughDate), Orchid);
                CycleMetric(metrics, "FULL NET THROUGH DATE", Cash(cycle.CumulativeFullNetCashAfterAllCosts), cycle.CumulativeFullNetCashAfterAllCosts < 0 ? Red : Green);
                Grid.SetRow(metrics, 1); card.Children.Add(metrics);
                var states = Txt("COST ON THIS DATE " + Cash(cycle.EvaluationCostOnDate) + " • EVAL PURCHASES " + cycle.EvaluationPurchases + " • CLOSE STATES: FUNDED " + cycle.FundedAtClose + " • BELOW PAYOUT GOAL " + cycle.FundedBelowPayoutGoal + " • EVAL " + cycle.EvaluationInProgress + " • REPLACEMENT " + cycle.ReplacementNextSession + " • ENDED " + cycle.TerminalBlown, Cyan, 10, FontWeights.Bold); states.Margin = new Thickness(7, 0, 7, 5); states.TextWrapping = TextWrapping.Wrap; Grid.SetRow(states, 2); card.Children.Add(states);
                payoutCycleDashboardStack.Children.Add(new Border { Background = Panel, BorderBrush = cycle.NetCashAfterEvaluationCost < 0 ? Red : Cyan, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Margin = new Thickness(2), Child = card });
            }
            payoutCycleDashboardStack.Children.Add(new Border { Background = Card, BorderBrush = Green, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Margin = new Thickness(3), Padding = new Thickness(7), Child = Txt("TOTAL SHOWN • GROSS " + Cash(cycles.Sum(x => x.GrossWithdrawals)) + " • AFTER SHARE " + Cash(cycles.Sum(x => x.NetCashAfterShare)) + " • EVAL COST RECORDED ON PAYOUT DATES " + Cash(cycles.Sum(x => x.EvaluationCostOnDate)) + " • NET AFTER THOSE DATE COSTS " + Cash(cycles.Sum(x => x.NetCashAfterEvaluationCost)), Green, 11, FontWeights.Bold) });
        }

        private void RenderPayoutAccountDashboard()
        {
            if (payoutAccountList == null || payoutAccountDetailText == null) return;
            List<KeystoneArcVirtualAccount> paid = accounts.Where(x => x.Payouts > 0).OrderBy(x => KeystoneArcEngine.GetFirstPayoutTiming(x).FirstPayoutDate).ThenBy(x => x.Name).ToList();
            payoutAccountUpdating = true;
            try
            {
                payoutAccountList.Items.Clear();
                for (int i = 0; i < paid.Count; i++)
                {
                    KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(paid[i]);
                    payoutAccountList.Items.Add(paid[i].Name + " • " + paid[i].Payouts + " PAYOUT" + (paid[i].Payouts == 1 ? string.Empty : "S") + " • " + (first.HasPayout ? first.FirstPayoutDate.ToString("yyyy-MM-dd") : "DATE N/A"));
                }
                payoutAccountList.SelectedIndex = paid.Count > 0 ? 0 : -1;
            }
            finally { payoutAccountUpdating = false; }
            if (paid.Count == 0)
            {
                payoutAccountDetailText.Text = accounts.Count == 0 ? "RUN THE VIRTUAL POOL TO BUILD PAYOUT-ACCOUNT RECORDS." : "NO ACCOUNTS HAVE PAYOUT HISTORY IN THIS SELECTED RANGE. The dated cycle aggregate remains empty by design.";
                payoutAccountDetailText.Foreground = Muted;
                return;
            }
            RenderPayoutAccountDetail(paid[0]);
        }

        private void RenderPayoutAccountDetail()
        {
            if (payoutAccountList == null || payoutAccountList.SelectedIndex < 0) return;
            List<KeystoneArcVirtualAccount> paid = accounts.Where(x => x.Payouts > 0).OrderBy(x => KeystoneArcEngine.GetFirstPayoutTiming(x).FirstPayoutDate).ThenBy(x => x.Name).ToList();
            if (payoutAccountList.SelectedIndex >= paid.Count) return;
            RenderPayoutAccountDetail(paid[payoutAccountList.SelectedIndex]);
        }

        private void RenderPayoutAccountDetail(KeystoneArcVirtualAccount account)
        {
            if (payoutAccountDetailText == null || account == null) return;
            KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(account);
            List<KeystoneArcAccountDay> days = account.DayHistory.OrderBy(x => x.Day).ToList();
            var weeks = days.GroupBy(x => PerformancePeriodStart(x.Day, "WEEKLY")).ToList();
            var months = days.GroupBy(x => PerformancePeriodStart(x.Day, "MONTHLY")).ToList();
            double balance = config != null && config.EvaluationEnabled == -2 ? account.StartingBalance + account.TotalPnl : CurrentStageBalance(account);
            var text = new StringBuilder();
            text.AppendLine(account.Name + " • " + AccountPrimaryStage(account));
            text.AppendLine("START " + (account.InitialLifecycleStart == DateTime.MinValue ? "NOT RECORDED" : account.InitialLifecycleStart.ToString("yyyy-MM-dd")) + " • CURRENT BALANCE " + Cash(balance));
            text.AppendLine("PAYOUTS " + account.Payouts + " • FIRST " + (first.HasPayout ? first.FirstPayoutDate.ToString("yyyy-MM-dd") : "NOT RECORDED") + " • LAST " + LastPayoutDate(account));
            text.AppendLine("GROSS " + Cash(account.PayoutGrossWithdrawn) + " • CASH AFTER SHARE " + Cash(account.PayoutCash) + " • ALL COSTS " + Cash(account.EvaluationCost) + " • FULL NET " + Cash(account.PayoutCash - account.EvaluationCost));
            text.AppendLine("FIRST RETURN • " + (first.HasPayout ? first.CalendarDaysFromInitial + " calendar days / " + first.RecordedSessionsFromInitial + " sessions" : "not recorded") + " • " + PayoutIntervalSummary(account));
            text.AppendLine("PERIODS • " + days.Count + " daily records • " + weeks.Count + " weekly records • " + months.Count + " monthly records • assigned P/L " + Cash(account.TotalPnl));
            text.AppendLine("BLOWOUT AFTER PAYOUT • " + (account.LastBlowoutDate == DateTime.MinValue ? "NONE" : account.LastBlowoutDate.ToString("yyyy-MM-dd")) + ". Open LIFECYCLE WALKTHROUGH for the day-by-day audit.");
            payoutAccountDetailText.Text = text.ToString();
            payoutAccountDetailText.Foreground = AccountLifecycleBrush(account);
        }

        private static string LastPayoutDate(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.Payouts <= 0) return "NOT RECORDED";
            int prior = 0; DateTime last = DateTime.MinValue;
            foreach (KeystoneArcAccountDay day in account.DayHistory.OrderBy(x => x.Day))
                if (day.PayoutsAfter > prior) { last = day.Day; prior = day.PayoutsAfter; }
            return last == DateTime.MinValue ? "NOT RECORDED" : last.ToString("yyyy-MM-dd");
        }

        private void RenderFirstReturnDashboard()
        {
            if (firstReturnDashboardStack == null) return;
            firstReturnDashboardStack.Children.Clear();
            string startLabel = config != null && config.EvaluationEnabled == 0 ? "INITIAL FUNDED SLOT" : "INITIAL EVALUATION";
            if (firstReturnDashboardText != null) firstReturnDashboardText.Text = "FIRST RETURN • PAID ACCOUNTS ONLY";
            List<KeystoneArcFirstReturnRow> rows = KeystoneArcEngine.BuildFirstReturnRows(accounts).Where(x => x.HasPayout).OrderBy(x => x.FirstPayoutDate).ThenBy(x => x.Account).ToList();
            if (rows.Count == 0)
            {
                firstReturnDashboardStack.Children.Add(Txt(accounts.Count == 0
                    ? "RUN THE VIRTUAL POOL TO BUILD FIRST-RETURN RECORDS."
                    : "NO ACCOUNTS REACHED A FIRST PAYOUT IN THIS SELECTED RANGE. Unpaid accounts remain available in POOL DASHBOARD and LIFECYCLE WALKTHROUGH; the first-return view intentionally lists paid accounts only.", Muted, 11, FontWeights.Bold));
                return;
            }
            DateTime earliest = rows.Min(x => x.FirstPayoutDate);
            firstReturnDashboardStack.Children.Add(Txt("PAID ACCOUNTS " + rows.Count + " • EARLIEST FIRST PAYOUT " + earliest.ToString("yyyy-MM-dd") + " • select an account for its full first-return and period summary.", Gold, 11, FontWeights.Bold));
            var body = new Grid { Margin = new Thickness(2) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.70, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.30, GridUnitType.Star) });
            firstReturnAccountList = new ListBox { Background = Panel, Foreground = Text, BorderBrush = Gold, BorderThickness = new Thickness(1.5), Margin = new Thickness(2), MinHeight = 160 };
            ScrollViewer.SetVerticalScrollBarVisibility(firstReturnAccountList, ScrollBarVisibility.Auto);
            for (int i = 0; i < rows.Count; i++) firstReturnAccountList.Items.Add(rows[i].Account + " • " + rows[i].FirstPayoutDate.ToString("yyyy-MM-dd"));
            firstReturnDetailText = Txt("Select a paid account.", Text, 11, FontWeights.Bold); firstReturnDetailText.TextWrapping = TextWrapping.Wrap;
            var detailScroll = new ScrollViewer { Background = Panel, BorderBrush = Gold, BorderThickness = new Thickness(1.5), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = firstReturnDetailText, Margin = new Thickness(2), MinHeight = 160 };
            firstReturnAccountList.SelectionChanged += delegate
            {
                if (firstReturnAccountUpdating || firstReturnAccountList.SelectedIndex < 0 || firstReturnAccountList.SelectedIndex >= rows.Count) return;
                KeystoneArcFirstReturnRow row = rows[firstReturnAccountList.SelectedIndex];
                KeystoneArcVirtualAccount account = accounts.FirstOrDefault(x => string.Equals(x.Name, row.Account, StringComparison.OrdinalIgnoreCase));
                RenderFirstReturnAccountDetail(account, row, startLabel);
                int poolIndex = accounts.IndexOf(account);
                if (poolIndex >= 0 && poolAccountList != null) poolAccountList.SelectedIndex = poolIndex;
            };
            body.Children.Add(firstReturnAccountList); Grid.SetColumn(detailScroll, 1); body.Children.Add(detailScroll);
            firstReturnDashboardStack.Children.Add(body);
            firstReturnAccountUpdating = true;
            try { firstReturnAccountList.SelectedIndex = 0; }
            finally { firstReturnAccountUpdating = false; }
            if (rows.Count > 0)
            {
                KeystoneArcVirtualAccount first = accounts.FirstOrDefault(x => string.Equals(x.Name, rows[0].Account, StringComparison.OrdinalIgnoreCase));
                RenderFirstReturnAccountDetail(first, rows[0], startLabel);
            }
        }

        private void RenderFirstReturnAccountDetail(KeystoneArcVirtualAccount account, KeystoneArcFirstReturnRow row, string startLabel)
        {
            if (firstReturnDetailText == null || row == null) return;
            if (account == null) { firstReturnDetailText.Text = "The paid account record is no longer available in the current pool."; return; }
            double balance = config != null && config.EvaluationEnabled == -2 ? account.StartingBalance + account.TotalPnl : CurrentStageBalance(account);
            List<KeystoneArcAccountDay> days = account.DayHistory.OrderBy(x => x.Day).ToList();
            double bestDay = days.Count == 0 ? 0 : days.Max(x => x.DayPnl);
            double worstDay = days.Count == 0 ? 0 : days.Min(x => x.DayPnl);
            var weeks = days.GroupBy(x => PerformancePeriodStart(x.Day, "WEEKLY")).ToList();
            var months = days.GroupBy(x => PerformancePeriodStart(x.Day, "MONTHLY")).ToList();
            double bestWeek = weeks.Count == 0 ? 0 : weeks.Max(x => x.Sum(d => d.DayPnl));
            double bestMonth = months.Count == 0 ? 0 : months.Max(x => x.Sum(d => d.DayPnl));
            var text = new StringBuilder();
            text.AppendLine(account.Name + " • " + AccountPrimaryStage(account) + " • FIRST RETURN DETAIL");
            text.AppendLine(startLabel + " START " + (row.InitialSlotStart == DateTime.MinValue ? "NOT RECORDED" : row.InitialSlotStart.ToString("yyyy-MM-dd")) + " • FIRST PAYOUT " + row.FirstPayoutDate.ToString("yyyy-MM-dd"));
            text.AppendLine("FIRST PAYOUT • GROSS " + Cash(row.FirstPayoutGross) + " • CASH AFTER SHARE " + Cash(row.FirstPayoutCashAfterShare) + " • COST THROUGH FIRST RETURN " + Cash(row.EvaluationCostThroughFirstPayout) + " • FULL NET AT FIRST RETURN " + Cash(row.FullNetReturnAfterAllCosts));
            text.AppendLine("CURRENT • BALANCE " + Cash(balance) + " • PAYOUT COUNT " + account.Payouts + " • GROSS WITHDRAWN " + Cash(account.PayoutGrossWithdrawn) + " • CASH AFTER SHARE " + Cash(account.PayoutCash) + " • ALL COSTS " + Cash(account.EvaluationCost) + " • FULL NET " + Cash(account.PayoutCash - account.EvaluationCost));
            text.AppendLine("TIME TO FIRST CASH • " + row.CalendarDaysFromInitial + " calendar days / " + row.RecordedSessionsFromInitial + " recorded sessions • " + PayoutIntervalSummary(account));
            text.AppendLine("DAILY / WEEKLY / MONTHLY • " + days.Count + " recorded days • best day " + Cash(bestDay) + " • worst day " + Cash(worstDay) + " • " + weeks.Count + " weeks (best " + Cash(bestWeek) + ") • " + months.Count + " months (best " + Cash(bestMonth) + ").");
            text.AppendLine("POST-PAYOUT BLOWOUT • " + (account.LastBlowoutDate == DateTime.MinValue ? "NONE RECORDED" : account.LastBlowoutDate.ToString("yyyy-MM-dd")) + " • use LIFECYCLE WALKTHROUGH for every recorded day and the export package for full period rows.");
            firstReturnDetailText.Text = text.ToString();
            firstReturnDetailText.Foreground = AccountLifecycleBrush(account);
        }

        private static DateTime PerformancePeriodStart(DateTime day, string mode)
        {
            DateTime date = day.Date;
            if (string.Equals(mode, "MONTHLY", StringComparison.OrdinalIgnoreCase)) return new DateTime(date.Year, date.Month, 1);
            if (string.Equals(mode, "WEEKLY", StringComparison.OrdinalIgnoreCase)) return date.AddDays(-((7 + (int)date.DayOfWeek - 1) % 7));
            return date;
        }

        private void RenderDailySessionScoreboard()
        {
            if (dailySessionScoreboardStack == null) return;
            dailySessionScoreboardStack.Children.Clear();
            string mode = periodGranularityBox == null ? "DAILY" : Convert.ToString(periodGranularityBox.SelectedItem ?? "DAILY");
            string session = config == null ? "SELECTED SESSION" : (config.SessionMode ?? "SELECTED SESSION").Replace("_", " ");
            if (dailySessionScoreboardText != null) dailySessionScoreboardText.Text = mode + " PERFORMANCE • " + session + " • ASSIGNMENT / LOCK / PAYOUT / COST";
            if (events == null || events.Count == 0 || config == null || config.Start == DateTime.MinValue)
            {
                dailySessionScoreboardStack.Children.Add(Txt("REQUEST DATA AND RUN DETECTION TO BUILD PERIOD PERFORMANCE CARDS.", Muted, 11, FontWeights.Bold));
                return;
            }

            Dictionary<DateTime, List<KeystoneArcEvent>> eventGroups = new Dictionary<DateTime, List<KeystoneArcEvent>>();
            foreach (KeystoneArcEvent e in events)
            {
                DateTime key = PerformancePeriodStart(KeystoneArcEngine.SessionGroupingDate(e.TriggerTime, config), mode);
                List<KeystoneArcEvent> list;
                if (!eventGroups.TryGetValue(key, out list)) { list = new List<KeystoneArcEvent>(); eventGroups[key] = list; }
                list.Add(e);
            }
            Dictionary<DateTime, List<KeystoneArcPayoutAuditDay>> auditGroups = new Dictionary<DateTime, List<KeystoneArcPayoutAuditDay>>();
            foreach (KeystoneArcPayoutAuditDay audit in KeystoneArcEngine.BuildPayoutAuditDays(accounts))
            {
                DateTime key = PerformancePeriodStart(audit.DaySnapshot.Day, mode);
                List<KeystoneArcPayoutAuditDay> list;
                if (!auditGroups.TryGetValue(key, out list)) { list = new List<KeystoneArcPayoutAuditDay>(); auditGroups[key] = list; }
                list.Add(audit);
            }
            List<DateTime> periods = eventGroups.Keys.Union(auditGroups.Keys).OrderBy(x => x).ToList();
            if (periods.Count == 0)
            {
                dailySessionScoreboardStack.Children.Add(Txt("NO PERIOD RECORD IS AVAILABLE FOR THIS SELECTED RANGE.", Muted, 11, FontWeights.Bold));
                return;
            }
            var periodNet = new Dictionary<DateTime, double>();
            foreach (DateTime key in periods)
            {
                List<KeystoneArcPayoutAuditDay> audit; if (!auditGroups.TryGetValue(key, out audit)) audit = new List<KeystoneArcPayoutAuditDay>();
                periodNet[key] = audit.Sum(x => x.DaySnapshot.DayPnl);
            }
            double bestPositive = periodNet.Values.Where(x => x > 0).DefaultIfEmpty(0).Max();
            foreach (DateTime key in periods)
            {
                List<KeystoneArcEvent> rows; if (!eventGroups.TryGetValue(key, out rows)) rows = new List<KeystoneArcEvent>();
                List<KeystoneArcPayoutAuditDay> audit; if (!auditGroups.TryGetValue(key, out audit)) audit = new List<KeystoneArcPayoutAuditDay>();
                int setups = rows.Count;
                int wins = rows.Count(x => string.Equals(x.Outcome, "WIN", StringComparison.OrdinalIgnoreCase));
                int losses = rows.Count(x => (x.Outcome ?? string.Empty).StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
                int accountsAssigned = audit.Where(x => Math.Abs(x.DaySnapshot.DayPnl) > 0.0001).Select(x => x.Account).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int profitLocks = audit.Where(x => x.DaySnapshot.DayLocked && x.DaySnapshot.DayPnl >= Math.Max(0, config.DailyGoal)).Select(x => x.Account).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int lossLocks = audit.Where(x => x.DaySnapshot.DayLocked && x.DaySnapshot.DayPnl <= -Math.Abs(config.DailyLoss)).Select(x => x.Account).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int payoutAccounts = audit.Where(x => x.PayoutDelta > 0).Select(x => x.Account).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int payoutCycles = audit.Sum(x => x.PayoutDelta);
                double assignedPnl = audit.Sum(x => x.DaySnapshot.DayPnl);
                double payoutCash = audit.Sum(x => x.PayoutCashDelta);
                double evaluationCost = audit.Sum(x => x.EvaluationCostDelta);
                bool best = assignedPnl > 0 && Math.Abs(assignedPnl - bestPositive) < 0.0001;
                Brush accent = best ? Green : (assignedPnl < 0 ? Red : Cyan);
                string periodLabel = string.Equals(mode, "MONTHLY", StringComparison.OrdinalIgnoreCase) ? key.ToString("yyyy-MM") : key.ToString("yyyy-MM-dd");
                if (string.Equals(mode, "WEEKLY", StringComparison.OrdinalIgnoreCase)) periodLabel += " → " + key.AddDays(6).ToString("yyyy-MM-dd");
                var card = new Grid { Background = Panel, Margin = new Thickness(2) };
                card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                string header = periodLabel + " • " + mode + " • SETUPS " + setups + " • W " + wins + " / L " + losses + (best ? " • BEST POSITIVE PERIOD" : string.Empty);
                var headerText = Txt(header, accent, 12, FontWeights.Bold); headerText.Margin = new Thickness(8, 5, 8, 2); headerText.TextWrapping = TextWrapping.Wrap; card.Children.Add(headerText);
                var metrics = new UniformGrid { Columns = 5, Margin = new Thickness(4, 0, 4, 3) };
                CycleMetric(metrics, "ACCOUNTS ASSIGNED", accounts.Count > 0 && accountsAssigned == accounts.Count ? "ALL " + accounts.Count : accountsAssigned + " / " + accounts.Count, Cyan);
                CycleMetric(metrics, "PROFIT / LOSS LOCKS", profitLocks + " / " + lossLocks, lossLocks > profitLocks ? Red : Green);
                CycleMetric(metrics, "ASSIGNED P/L", Cash(assignedPnl), assignedPnl < 0 ? Red : (assignedPnl > 0 ? Green : Muted));
                CycleMetric(metrics, "PAYOUT ACCOUNTS", payoutAccounts + " / " + payoutCycles, payoutAccounts > 0 ? Gold : Muted);
                CycleMetric(metrics, "PAYOUT NET / COST", Cash(payoutCash) + " / " + Cash(evaluationCost), payoutCash - evaluationCost < 0 ? Orchid : Green);
                Grid.SetRow(metrics, 1); card.Children.Add(metrics);
                string close = "NET CASH AFTER PERIOD COST " + Cash(payoutCash - evaluationCost) + " • EVAL PURCHASES " + audit.Sum(x => x.EvaluationPurchaseDelta) + " • FUNDED QUALIFYING DAYS " + audit.Count(x => x.DaySnapshot.FundedQualifyingDay) + " • EVAL PASSES " + audit.GroupBy(x => x.Account).Count(g => g.Max(x => x.DaySnapshot.EvaluationPassesAfter) > 0);
                var closeText = Txt(close, Gold, 10, FontWeights.Bold); closeText.Margin = new Thickness(8, 0, 8, 5); closeText.TextWrapping = TextWrapping.Wrap; Grid.SetRow(closeText, 2); card.Children.Add(closeText);
                var accountBadges = new WrapPanel { Margin = new Thickness(6, 0, 6, 5) };
                foreach (var accountPeriod in audit.GroupBy(x => x.Account).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    double accountPnl = accountPeriod.Sum(x => x.DaySnapshot.DayPnl);
                    bool hitProfit = accountPeriod.Any(x => x.DaySnapshot.DayLocked && x.DaySnapshot.DayPnl >= Math.Max(0, config.DailyGoal));
                    bool hitLoss = accountPeriod.Any(x => x.DaySnapshot.DayLocked && x.DaySnapshot.DayPnl <= -Math.Abs(config.DailyLoss));
                    bool paidThisPeriod = accountPeriod.Any(x => x.PayoutDelta > 0);
                    KeystoneArcAccountDay dayClose = accountPeriod.OrderBy(x => x.DaySnapshot.Day).Last().DaySnapshot;
                    string stage = AccountDayPrimaryStage(dayClose);
                    string outcome = paidThisPeriod ? "PAYOUT" : (hitProfit ? "PROFIT LOCK" : (hitLoss ? "LOSS LOCK" : (accountPnl == 0 ? "ACTIVE" : (accountPnl > 0 ? "PROFIT" : "LOSS"))));
                    Brush badgeBrush = paidThisPeriod ? Gold : (hitProfit || accountPnl > 0 ? Green : (hitLoss || accountPnl < 0 ? Red : (stage == "EVALUATION" ? Orchid : Blue)));
                    var badge = new TextBlock { Text = accountPeriod.Key + " • " + stage + " • " + outcome, Foreground = badgeBrush, FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(5, 2, 5, 2), TextWrapping = TextWrapping.NoWrap };
                    accountBadges.Children.Add(new Border { Background = Card, BorderBrush = badgeBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(2), Child = badge });
                }
                if (accountBadges.Children.Count > 0) { Grid.SetRow(accountBadges, 3); card.Children.Add(accountBadges); }
                dailySessionScoreboardStack.Children.Add(new Border { Background = Panel, BorderBrush = accent, BorderThickness = new Thickness(best ? 2.5 : 1.25), CornerRadius = new CornerRadius(5), Margin = new Thickness(3, 2, 3, 2), Child = card });
            }
        }

        private void AddPoolAccountCard(KeystoneArcVirtualAccount account, int index)
        {
            if (poolAccountCardStack == null || account == null) return;
            Brush stateBrush = AccountLifecycleBrush(account);
            bool oneDay = IsOneDayAssignmentMode();
            if (oneDay)
            {
                string status = OneDayAccountStatus(account);
                string window = OneDayAssignmentWindow(account);
                string dailyLimits = "PROFIT LOCK " + Cash(config.DailyGoal) + " • LOSS LOCK -" + Cash(Math.Abs(config.DailyLoss)).Replace("-$", "$");
                var oneDayText = new TextBlock
                {
                    Text = account.Name + " • " + status + "\n" +
                        "TRADES " + account.Trades + " • " + account.Wins + "W / " + account.Losses + "L • DAY " + Cash(account.TotalPnl) + "\n" +
                        "LAST " + window,
                    Foreground = Text,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(7)
                };
                var oneDayButton = new Button { Background = Panel, BorderBrush = stateBrush, BorderThickness = new Thickness(2), Padding = new Thickness(4), MinHeight = 78, Content = oneDayText, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(3), ToolTip = "Select " + account.Name + " for assigned trades, P/L, daily lock, and exact assignment times." };
                int oneDaySelectedIndex = index;
                oneDayButton.Click += delegate { if (poolAccountList != null) poolAccountList.SelectedIndex = oneDaySelectedIndex; UpdatePoolDetail(); };
                poolAccountCardStack.Children.Add(oneDayButton);
                return;
            }
            string stageBalance = config.EvaluationEnabled == -2
                ? Cash(account.StartingBalance + account.TotalPnl)
                : Cash(CurrentStageBalance(account));
            string main = account.Name + "  •  " + AccountPrimaryStage(account);
            string line2 = config.EvaluationEnabled == -2
                ? "START " + Cash(account.StartingBalance) + " • END " + stageBalance + " • RANGE " + Cash(account.TotalPnl)
                : "BALANCE " + stageBalance + " • FULL NET " + Cash(account.PayoutCash - account.EvaluationCost) + " • PAYOUTS " + account.Payouts;
            string line3 = "TRADES " + account.Trades + " • " + account.Wins + "W / " + account.Losses + "L • COST " + Cash(account.EvaluationCost) + " • BLOWOUTS " + (account.FailedEvaluations + account.FailedFunded);
            string line4 = FirstPayoutTimingLine(account);
            var text = new TextBlock { Text = main + "\n" + line2 + "\n" + line3 + "\n" + line4, Foreground = Text, FontSize = 10, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(7) };
            var button = new Button { Background = Panel, BorderBrush = stateBrush, BorderThickness = new Thickness(2), Padding = new Thickness(4), MinHeight = 98, Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(3), ToolTip = "Select " + account.Name + " for its dated payout, blowout, replacement, full-net cash, and assigned-event history." };
            int selectedIndex = index;
            button.Click += delegate { if (poolAccountList != null) poolAccountList.SelectedIndex = selectedIndex; UpdatePoolDetail(); };
            poolAccountCardStack.Children.Add(button);
        }

        private bool IsOneDayAssignmentMode()
        {
            return config != null && config.EvaluationEnabled < 0 && config.EvaluationEnabled != -2;
        }

        private string OneDayAccountStatus(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.Trades <= 0) return "UNUSED • NO ASSIGNMENT";
            if (account.DayLocked)
            {
                if (account.DayPnl >= Math.Max(0, config.DailyGoal)) return "DAILY PROFIT LOCK";
                if (account.DayPnl <= -Math.Abs(config.DailyLoss)) return "DAILY LOSS LOCK";
                return "DAILY LOCK";
            }
            return "TRADED • DAILY LIMIT NOT REACHED";
        }

        private static string OneDayAssignmentWindow(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.Trades <= 0 || account.LastAssignedDate == DateTime.MinValue) return "NO ASSIGNMENT";
            return account.LastAssignedDate.ToString("yyyy-MM-dd") + " • LAST ASSIGNED";
        }

        private string AccountCardCashLine(KeystoneArcVirtualAccount account, string stageBalance)
        {
            if (account == null) return string.Empty;
            if (account.Blown && !account.ReplacementPending)
            {
                string payoutHistory = account.Payouts == 0
                    ? "NO PAYOUT BEFORE BLOWOUT"
                    : account.Payouts + " PAYOUT(S) PAID BEFORE BLOWOUT • FULL NET AFTER COST " + Cash(account.PayoutCash - account.EvaluationCost);
                return "FINAL BALANCE " + stageBalance + "  •  " + payoutHistory;
            }
            if (account.ReplacementPending)
                return account.ReplacementBudgetBlocked
                    ? "BENCHED AFTER FAILURE • WAITING FOR PAYOUT CASH • FULL NET AFTER COST " + Cash(account.PayoutCash - account.EvaluationCost)
                    : "FAILED BALANCE " + stageBalance + "  •  REPLACEMENT EVAL NEXT SESSION • FULL NET AFTER COST " + Cash(account.PayoutCash - account.EvaluationCost);
            return "CURRENT BALANCE " + stageBalance + "  •  PAYOUT CYCLES " + account.Payouts + "  •  FULL NET AFTER ALL COSTS " + Cash(account.PayoutCash - account.EvaluationCost);
        }

        private string FirstPayoutTimingLine(KeystoneArcVirtualAccount account)
        {
            KeystoneArcFirstPayoutTiming timing = KeystoneArcEngine.GetFirstPayoutTiming(account);
            if (!timing.HasPayout) return "FIRST PAYOUT  —  not recorded in selected range";
            return "FIRST PAYOUT " + timing.FirstPayoutDate.ToString("yyyy-MM-dd") + "  •  " + timing.CalendarDaysFromInitial + " calendar days / " + timing.RecordedSessionsFromInitial + " sessions from initial slot start";
        }

        private static string LifecycleDateSummary(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.InitialLifecycleStart == DateTime.MinValue) return "no assigned lifecycle date";
            DateTime end = account.TerminalLifecycleEnd != DateTime.MinValue ? account.TerminalLifecycleEnd : account.LastAssignedDate;
            string result = "start " + account.InitialLifecycleStart.ToString("yyyy-MM-dd") + " • last assigned " + (end == DateTime.MinValue ? "none" : end.ToString("yyyy-MM-dd"));
            if (end != DateTime.MinValue) result += " • " + Math.Max(0, (end.Date - account.InitialLifecycleStart.Date).Days) + " calendar days";
            if (account.LastBlowoutDate != DateTime.MinValue) result += " • latest blowout " + account.LastBlowoutDate.ToString("yyyy-MM-dd");
            if (account.TerminalLifecycleEnd != DateTime.MinValue) result += " • terminal end";
            return result;
        }

        private double CurrentStageBalance(KeystoneArcVirtualAccount account)
        {
            if (account == null) return 0;
            if (config != null && config.EvaluationEnabled == -2) return account.StartingBalance + account.TotalPnl;
            // A direct-funded failure has already cleared the Funded flag; preserve the negative
            // funded balance so the visible card makes the drawdown and terminal blowout explicit.
            if (account.Funded || (account.Blown && account.FailedFunded > 0) || (account.ReplacementPending && account.ReplacementFromFunded)) return account.FundedBalance;
            return account.EvaluationBalance;
        }

        private string AccountLifecycleLabel(KeystoneArcVirtualAccount a)
        {
            if (a == null) return "NO ACCOUNT";
            if (IsOneDayAssignmentMode()) return OneDayAccountStatus(a);
            if (a.Blown && !a.ReplacementPending) return a.Payouts == 0 ? "BLOWN • SLOT ENDED" : "BLOWN AFTER " + a.Payouts + " PAYOUT(S) • SLOT ENDED";
            if (a.ReplacementPending && a.ReplacementBudgetBlocked) return "BENCHED • PAYOUT CASH GATE";
            if (a.ReplacementPending) return "REPLACEMENT EVAL NEXT SESSION";
            if (a.FundedCapPending) return "EVAL PASSED • FIRM CAP WAIT";
            if (config.EvaluationEnabled == -2) return "SINGLE ACCOUNT • RANGE P/L";
            if (a.Payouts > 0 && a.Funded) return "FUNDED • PAYOUT " + a.Payouts + " RECORDED";
            if (a.Funded) return a.LastState == "DAILY LOSS LOCK" ? "FUNDED • DAILY LOSS LOCK" : (a.LastState == "DAILY PROFIT LOCK" ? "FUNDED • DAILY PROFIT LOCK" : "FUNDED • PAYOUT PROGRESS");
            if (a.LastState == "DAILY LOSS LOCK") return "EVALUATION • DAILY LOSS LOCK";
            if (a.LastState == "DAILY PROFIT LOCK") return "EVALUATION • DAILY PROFIT LOCK";
            return "EVALUATION • PASS IN PROGRESS";
        }

        // A list or card should identify one current stage, not concatenate a historical state,
        // a payout count, and a failure note into the account name. The detailed audit retains
        // those facts in its own fields.
        private string AccountPrimaryStage(KeystoneArcVirtualAccount a)
        {
            if (a == null) return "NO ACCOUNT";
            if (IsOneDayAssignmentMode()) return a.Trades <= 0 ? "UNUSED" : (a.DayLocked ? (a.DayPnl >= Math.Max(0, config.DailyGoal) ? "PROFIT LOCK" : "LOSS LOCK") : "ACTIVE");
            if (config != null && config.EvaluationEnabled == -2) return "RANGE P/L";
            if (a.Blown && !a.ReplacementPending) return "BLOWN";
            if (a.ReplacementPending && a.ReplacementBudgetBlocked) return "BENCHED";
            if (a.ReplacementPending) return "REPLACEMENT WAIT";
            if (a.FundedCapPending) return "FIRM CAP WAIT";
            if (a.Payouts > 0) return "PAYOUT HISTORY";
            if (a.Funded) return "FUNDED";
            return "EVALUATION";
        }

        private Brush AccountLifecycleBrush(KeystoneArcVirtualAccount a)
        {
            if (a == null) return Muted;
            if (IsOneDayAssignmentMode())
            {
                if (a.Trades <= 0) return Muted;
                if (a.DayLocked && a.DayPnl >= Math.Max(0, config.DailyGoal)) return Green;
                if (a.DayLocked) return Red;
                return Blue;
            }
            if (a.ReplacementPending && a.ReplacementBudgetBlocked) return Gold;
            if (a.ReplacementPending) return Orange;
            if (a.FundedCapPending) return Gold;
            if (a.Blown || a.LastState == "DAILY LOSS LOCK") return Red;
            if (a.Payouts > 0 && a.Funded) return Gold;
            if (a.Funded) return Green;
            return Orchid;
        }

        private void RenderPoolDashboard()
        {
            if (mathText == null) return;
            UpdatePoolMetricTiles();
            if (false)
            {
                List<KeystoneArcEvent> liveLedger = FinalLiveAccountLedger();
                int liveTrades = liveLedger.Count;
                double livePnl = liveLedger.Sum(x => x.GrossPnl);
                int liveWins = liveLedger.Count(x => x.Outcome == "WIN");
                int liveLosses = liveLedger.Count(x => x.Outcome.StartsWith("LOSS"));
                int liveExits = liveLedger.Count(x => x.Outcome == "SESSION EXIT");
                int liveAccepted = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
                int notSelected = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && !string.IsNullOrWhiteSpace(x.SkipReason));
                int excluded = events.Count - liveAccepted;
                int days = events.Select(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config)).Distinct().Count();
                double endingBalance = config.PersonalStartingBalance + livePnl;
                string byInstrument = string.Join(" | ", new[] { "MNQ", "MGC" }.Where(symbol => config.Scope == "BOTH" || config.Scope == symbol).Select(symbol =>
                {
                    List<KeystoneArcEvent> rows = liveLedger.Where(x => x.Symbol == symbol).ToList();
                    return symbol + " " + rows.Count + " trades • " + rows.Count(x => x.Outcome == "WIN") + "W/" + rows.Count(x => x.Outcome.StartsWith("LOSS")) + "L/" + rows.Count(x => x.Outcome == "SESSION EXIT") + "X • " + rows.Sum(x => x.GrossPnl).ToString("C0");
                }));
                mathText.Text = "LIVE ACCOUNT • FINAL HISTORICAL LEDGER\n" + SelectedTestDateLabel() + " • " + days + " SESSION DAYS • " + config.Scope + " • " + config.SetupMinutes + "M\n\n" +
                    "RULE: one earliest resolved setup across the selected instrument scope per session date • maximum 1 final trade per session date\n" +
                    "TARGET " + config.TargetDollars.ToString("C0") + " • MAX RISK " + config.StopDollars.ToString("C0") + " • STOP MODE " + config.StopMode + "\n" +
                    "START BALANCE " + config.PersonalStartingBalance.ToString("C0") + " • END BALANCE " + endingBalance.ToString("C0") + "\n\n" +
                    "FINAL TRADES " + liveTrades + " = " + liveWins + " W + " + liveLosses + " L + " + liveExits + " SESSION EXIT • FINAL P/L " + livePnl.ToString("C0") + "\n" +
                    byInstrument + "\n\n" +
                    "RECONCILIATION: DETECTED " + events.Count + " = FINAL " + liveTrades + " + LATER / OUTCOME-UNAVAILABLE " + notSelected + " + EXCLUDED " + excluded + ".\n" +
                    "Every raw setup remains visible on the chart with its W/L/E result. Only FINAL rows are included in this P/L.";
                return;
            }
            int accepted = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            int assigned = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount));
            int skipped = events.Count(x => !string.IsNullOrWhiteSpace(x.SkipReason));
            int wins = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && x.Outcome == "WIN");
            int losses = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && x.Outcome.StartsWith("LOSS"));
            int exits = events.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && x.Outcome == "SESSION EXIT");
            double assignedGross = events.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)).Sum(x => x.GrossPnl);
            int passed = accounts.Sum(x => x.EvaluationPasses);
            int funded = accounts.Count(x => x.Funded);
            int replacementPending = accounts.Count(x => x.ReplacementPending);
            int purchases = accounts.Sum(x => x.EvaluationPurchases);
            int payouts = accounts.Sum(x => x.Payouts);
            double payoutGross = accounts.Sum(x => x.PayoutGrossWithdrawn);
            double payoutCash = accounts.Sum(x => x.PayoutCash);
            double evaluationCost = accounts.Sum(x => x.EvaluationCost);
            int evalBlown = accounts.Sum(x => x.FailedEvaluations);
            int fundedBlown = accounts.Sum(x => x.FailedFunded);
            int terminalBlown = accounts.Count(x => x.Blown && !x.ReplacementPending);
            int payoutThenBlown = accounts.Count(x => x.Blown && !x.ReplacementPending && x.Payouts > 0);
            int evalInProgress = accounts.Count(x => !x.Funded && !x.Blown && !x.ReplacementPending);
            bool asianCopy = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            KeystoneArcRiskSequenceStats detectedRisk = CalculateRiskSequenceStats(events, config.PropStartingBalance);
            KeystoneArcRiskSequenceStats propRisk = CalculateWorstVirtualAccountRisk(config.PropStartingBalance);
            var sb = new StringBuilder();
            if (config.EvaluationEnabled == -2)
            {
                KeystoneArcVirtualAccount a = accounts.Count == 0 ? null : accounts[0];
                double pnl = a == null ? 0 : a.TotalPnl;
                double startBalance = a == null ? config.PropStartingBalance : a.StartingBalance;
                sb.AppendLine("SINGLE PROP ACCOUNT • RANGE P/L ONLY" + (asianCopy ? " • ASIAN COPY CYCLE" : string.Empty));
                sb.AppendLine(SelectedTestDateLabel() + " • " + config.Scope + " • " + config.SessionMode + " • " + config.SetupMinutes + "M SETUPS");
                sb.AppendLine();
                sb.AppendLine("START BALANCE " + Cash(startBalance) + " • " + (asianCopy ? "COPIED CYCLE P/L " : "ASSIGNED P/L ") + Cash(pnl) + " • END BALANCE " + Cash(startBalance + pnl));
                sb.AppendLine("ASSIGNED " + assigned + " • SKIPPED " + skipped + " • WINS " + wins + " • LOSSES " + losses + " • SESSION EXITS " + exits);
                sb.AppendLine();
                sb.AppendLine("No evaluation purchases, funded-stage balance, payout cycle, or replacement-account calculation was applied in this one-account mode.");
                mathText.Text = sb.ToString();
                return;
            }
            if (config.EvaluationEnabled < 0)
            {
                int tradedAccounts = accounts.Count(x => x.Trades > 0);
                int profitLockedAccounts = accounts.Count(x => x.DayLocked && x.DayPnl >= Math.Max(0, config.DailyGoal));
                int lossLockedAccounts = accounts.Count(x => x.DayLocked && x.DayPnl <= -Math.Abs(config.DailyLoss));
                sb.AppendLine("ONE-DAY VIRTUAL-ACCOUNT ALLOCATION" + (asianCopy ? " • ASIAN COPY CYCLE" : " • BH SETUP ASSIGNMENT"));
                sb.AppendLine(SelectedTestDateLabel() + " • " + config.Scope + " • " + config.SessionMode + " • " + config.SetupMinutes + "M");
                sb.AppendLine();
                sb.AppendLine("ELIGIBLE SETUPS " + accepted + " • ASSIGNED TRADES " + assigned + " • SKIPPED " + skipped);
                sb.AppendLine("WINS " + wins + " • LOSSES " + losses + " • SESSION EXITS " + exits + " • ASSIGNED MODEL P/L " + Cash(accounts.Sum(x => x.TotalPnl)));
                sb.AppendLine("ACCOUNTS " + accounts.Count + " • TRADED " + tradedAccounts + " • UNUSED " + (accounts.Count - tradedAccounts) + " • PROFIT LOCKS " + profitLockedAccounts + " @ " + Cash(config.DailyGoal) + " • LOSS LOCKS " + lossLockedAccounts + " @ -" + Cash(Math.Abs(config.DailyLoss)).Replace("-$", "$") + ".");
                sb.AppendLine();
                sb.AppendLine("COLOR KEY: BLUE = traded but no daily lock • GREEN = reached daily profit lock • RED = reached daily loss lock • GRAY = unused.");
                sb.AppendLine("This is a one-day allocation only: no evaluation, funded balance, payout, evaluation cost, replacement, or blowout lifecycle is modeled.");
                mathText.Text = sb.ToString();
                return;
            }
            sb.AppendLine("POOL RUN COMPLETE");
            sb.AppendLine(SelectedTestDateLabel() + " • " + config.Scope + " • " + config.SessionMode + " • " + config.SetupMinutes + "M SETUPS");
            sb.AppendLine();
            sb.AppendLine(asianCopy ? "ACCOUNTS / COPY TRADING" : "ACCOUNTS / ASSIGNMENT");
            sb.AppendLine("MODE " + (config.EvaluationEnabled < 0 ? "ONE-DAY" : (config.EvaluationEnabled == 0 ? "DIRECT FUNDED" : "EVALUATION FIRST")) + " • ACCOUNTS " + accounts.Count + " • ELIGIBLE " + accepted);
            sb.AppendLine((asianCopy ? "COPIED LEGS " : "ASSIGNED ") + assigned + " • SKIPPED " + skipped + " • WINS " + wins + " • LOSSES " + losses + " • EXITS " + exits);
            sb.AppendLine((asianCopy ? "ONE-ACCOUNT COPY CYCLE P/L " : "ASSIGNED MODEL P/L ") + Cash(assignedGross) + (asianCopy ? " • ALL ACTIVE-ACCOUNT TOTAL " + Cash(accounts.Sum(x => x.TotalPnl)) : string.Empty));
            sb.AppendLine();
            sb.AppendLine("RISK SEQUENCES • HISTORICAL MODEL ONLY");
            sb.AppendLine("ALL DETECTED OUTCOMES • setups " + detectedRisk.WorstNegativeSetupStreak + " / " + Cash(detectedRisk.WorstNegativeSetupStreakPnl) + " • days " + detectedRisk.WorstNegativeDayStreak + " / " + Cash(detectedRisk.WorstNegativeDayStreakPnl) + " • starting reference " + Cash(detectedRisk.StartingBalance) + " • low " + Cash(detectedRisk.LowestBalance) + " • drawdown " + Cash(detectedRisk.MaximumDrawdown));
            sb.AppendLine("VIRTUAL PROP • WORST INDIVIDUAL ACCOUNT • setups " + propRisk.WorstNegativeSetupStreak + " / " + Cash(propRisk.WorstNegativeSetupStreakPnl) + " • days " + propRisk.WorstNegativeDayStreak + " / " + Cash(propRisk.WorstNegativeDayStreakPnl) + " • starting reference " + Cash(propRisk.StartingBalance) + " • low " + Cash(propRisk.LowestBalance) + " • drawdown " + Cash(propRisk.MaximumDrawdown));
            sb.AppendLine();
            sb.AppendLine("ILLUSTRATIVE LIFECYCLE • EXPLICIT TOTALS");
            sb.AppendLine("COLOR KEY: GREEN = funded / no payout history • GOLD = funded / payout history • PURPLE = evaluation in progress • RED = blown / unavailable.");
            if (config.EvaluationEnabled < 0) sb.AppendLine("One-day " + (asianCopy ? "copy" : "assignment") + " only. No evaluation, funded stage, or payout cycle was applied.");
            else if (config.EvaluationEnabled == 0) sb.AppendLine("DIRECT FUNDED • currently funded " + funded + " • terminal blown " + terminalBlown + " • direct-funded blown slots are not replaced.");
            else sb.AppendLine("EVALUATION PASSES COMPLETED " + passed + " • CURRENTLY FUNDED " + funded + " • EVAL IN PROGRESS " + evalInProgress + " • EVAL ACCOUNTS BLOWN " + evalBlown + " • FUNDED ACCOUNTS BLOWN " + fundedBlown + " • REPLACEMENTS NEXT SESSION " + replacementPending);
            sb.AppendLine("FAILURE EVENTS " + (evalBlown + fundedBlown) + " • TERMINAL ENDED " + terminalBlown + " • REPLACEMENT PURCHASES " + (config.EvaluationEnabled > 0 ? Math.Max(0, purchases - accounts.Count) : 0));
            sb.AppendLine("EVALUATIONS PURCHASED " + purchases + " • TOTAL EVALUATION / REPLACEMENT COST " + Cash(evaluationCost));
            sb.AppendLine("LIFETIME PAYOUT CYCLES " + payouts + " • GROSS WITHDRAWALS " + Cash(payoutGross) + " • CASH AFTER " + config.PayoutProfitSharePercent.ToString("0.#") + "% SHARE (PRE-COST) " + Cash(payoutCash));
            sb.AppendLine("FULL NET CASH AFTER ALL COSTS " + Cash(payoutCash - evaluationCost) + " • payout cash after share less every modeled initial/replacement cost; this is not assigned trading P/L.");
            if (payoutThenBlown > 0) sb.AppendLine("IMPORTANT: " + payoutThenBlown + " terminally blown slot(s) received payout(s) earlier in the selected range. Those historical withdrawals remain cash received; the red state means only that the slot later reached drawdown and cannot trade again.");
            if (config.EvaluationEnabled > 0 && payouts == 0)
            {
                if (passed == 0) sb.AppendLine("WHY PAYOUT CASH IS $0: no virtual slot completed the selected evaluation requirement — target " + config.EvaluationTarget.ToString("C0") + " plus " + config.MinimumPositiveDays + " consecutive qualifying day(s) at the selected daily requirement.");
                else if (funded == 0) sb.AppendLine("WHY PAYOUT CASH IS $0: evaluation passes occurred, but no slot is currently funded; replacement evaluation cycles are still in progress.");
                else sb.AppendLine("WHY PAYOUT CASH IS $0: funded slots have not yet met both the selected ready balance " + config.PayoutThreshold.ToString("C0") + " and " + config.PayoutDaysRequired + " funded qualifying day(s) at least " + config.MinimumQualifyingDayProfit.ToString("C0") + ".");
            }
            sb.AppendLine();
            sb.AppendLine("SELECT AN ACCOUNT in the middle list for its " + (asianCopy ? "copied daily-cycle" : "assigned-event") + " audit. The exported research package contains the full data receipt, skipped-event ledger, daily carry, and every account record.");
            mathText.Foreground = Cyan;
            mathText.Text = "POOL RANGE • " + accounts.Count + " ACCOUNTS • " + accepted + " ELIGIBLE • " + assigned + " ASSIGNED • " + skipped + " SKIPPED\n" +
                "W/L/EXIT " + wins + "/" + losses + "/" + exits + " • ASSIGNED P/L " + Cash(assignedGross) + " • EVAL PASSES " + passed + " • ACTIVE FUNDED " + funded + "\n" +
                "PAYOUTS " + payouts + " • CASH AFTER SHARE " + Cash(payoutCash) + " • ALL COSTS " + Cash(evaluationCost) + " • FULL NET " + Cash(payoutCash - evaluationCost) + ". Open PORTFOLIO CASH or click an account for the detailed audit.";
        }

        private void UpdatePoolDetail()
        {
            if (poolDetailText == null) return;
            if (poolAccountList == null || poolAccountList.SelectedIndex < 0 || poolAccountList.SelectedIndex >= accounts.Count)
            {
                poolDetailText.Text = "No selected virtual account. Choose a bordered account card to see its concise current summary; the dated audit belongs in LIFECYCLE WALKTHROUGH.";
                if (poolTimelineStack != null) poolTimelineStack.Children.Clear();
                UpdateWalkthroughSelection(null);
                UpdateSelectedAccountMetricTiles(null);
                return;
            }
            KeystoneArcVirtualAccount a = accounts[poolAccountList.SelectedIndex];
            UpdateWalkthroughSelection(a);
            UpdateSelectedAccountMetricTiles(a);
            string stage = AccountPrimaryStage(a);
            if (poolDetailHeadingText != null)
            {
                poolDetailHeadingText.Text = a.Name + " • " + stage + " • SELECTED SUMMARY";
                poolDetailHeadingText.Foreground = AccountLifecycleBrush(a);
            }
            double balance = config != null && config.EvaluationEnabled == -2 ? a.StartingBalance + a.TotalPnl : CurrentStageBalance(a);
            KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(a);
            var summary = new StringBuilder();
            summary.AppendLine("CURRENT STAGE • " + stage + " • BALANCE " + Cash(balance));
            summary.AppendLine("ACTIVITY • TRADES " + a.Trades + " • " + a.Wins + " W / " + a.Losses + " L • ASSIGNED P/L " + Cash(a.TotalPnl));
            if (IsOneDayAssignmentMode())
            {
                summary.AppendLine("ONE-DAY RESULT • " + OneDayAccountStatus(a) + " • DAY P/L " + Cash(a.DayPnl) + " • no evaluation, payout, replacement, or blowout lifecycle.");
            }
            else if (config != null && config.EvaluationEnabled == -2)
            {
                summary.AppendLine("SINGLE RANGE P/L • START " + Cash(a.StartingBalance) + " • END " + Cash(balance) + " • lifecycle mechanics are off.");
            }
            else
            {
                summary.AppendLine("CASH • GROSS WITHDRAWALS " + Cash(a.PayoutGrossWithdrawn) + " • CASH AFTER SHARE " + Cash(a.PayoutCash) + " • ALL COSTS " + Cash(a.EvaluationCost) + " • FULL NET " + Cash(a.PayoutCash - a.EvaluationCost));
                summary.AppendLine("DATES • START " + (a.InitialLifecycleStart == DateTime.MinValue ? "NOT RECORDED" : a.InitialLifecycleStart.ToString("yyyy-MM-dd")) + " • FIRST PAYOUT " + (first.HasPayout ? first.FirstPayoutDate.ToString("yyyy-MM-dd") : "NOT REACHED") + " • LAST BLOWOUT " + (a.LastBlowoutDate == DateTime.MinValue ? "NONE" : a.LastBlowoutDate.ToString("yyyy-MM-dd")));
                summary.AppendLine("STATE NOTES • " + AccountLifecycleLabel(a) + " • PAYOUTS " + a.Payouts + " • BLOWOUT EVENTS " + (a.FailedEvaluations + a.FailedFunded) + ".");
            }
            summary.AppendLine("OPEN LIFECYCLE WALKTHROUGH FOR EVERY RECORDED DAY, PASS, PAYOUT, BENCH, REPLACEMENT, AND BLOWOUT. The exported HTML and CSV package retain the full trade ledger.");
            poolDetailText.Text = summary.ToString();
            RenderAccountTimeline(a);
        }

        private void UpdateWalkthroughSelection(KeystoneArcVirtualAccount account)
        {
            if (walkthroughAccountBox != null && poolAccountList != null)
            {
                walkthroughSelectionUpdating = true;
                try { walkthroughAccountBox.SelectedIndex = poolAccountList.SelectedIndex; }
                finally { walkthroughSelectionUpdating = false; }
            }
            if (walkthroughSummaryText == null) return;
            if (account == null)
            {
                walkthroughSummaryText.Text = "Choose a pool card or account name. This view shows the selected slot from initial evaluation through pass, payout, replacement, firm-cap wait, or terminal blowout.";
                walkthroughSummaryText.Foreground = Cyan;
                return;
            }
            KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(account);
            string firm = config != null && config.FirmFundedCapEnabled > 0 ? " • " + account.PropFirmCode + " SLOT " + account.PropFirmSlot + (account.FundedCapPending ? " • WAITING FOR FUNDED CAPACITY" : string.Empty) : string.Empty;
            string payout = first.HasPayout ? "FIRST PAYOUT " + first.FirstPayoutDate.ToString("yyyy-MM-dd") + " • " + first.CalendarDaysFromInitial + " CALENDAR DAYS" : "NO PAYOUT RECORDED";
            walkthroughSummaryText.Text = account.Name + " • " + AccountLifecycleLabel(account) + firm + "\n" +
                "TRADES " + account.Trades + " • " + account.Wins + "W / " + account.Losses + "L • ASSIGNED P/L " + Cash(account.TotalPnl) + " • COST " + Cash(account.EvaluationCost) + " • PAYOUT CASH " + Cash(account.PayoutCash) + " • FULL NET " + Cash(account.PayoutCash - account.EvaluationCost) + "\n" +
                payout + " • BLOWOUT EVENTS " + (account.FailedEvaluations + account.FailedFunded) + " • " + LifecycleDateSummary(account);
            walkthroughSummaryText.Foreground = AccountLifecycleBrush(account);
        }

        private void UpdateSelectedAccountMetricTiles(KeystoneArcVirtualAccount account)
        {
            UpdateOneDayAccountMetricTiles(account);
            if (account == null)
            {
                if (accountBalanceMetric != null) { accountBalanceMetric.Text = "$0"; accountBalanceMetric.Foreground = Muted; }
                if (accountPayoutCashMetric != null) { accountPayoutCashMetric.Text = "$0"; accountPayoutCashMetric.Foreground = Muted; }
                if (accountCostMetric != null) { accountCostMetric.Text = "$0"; accountCostMetric.Foreground = Muted; }
                if (accountNetCashAfterCostMetric != null) { accountNetCashAfterCostMetric.Text = "$0"; accountNetCashAfterCostMetric.Foreground = Muted; }
                if (accountPayoutCyclesMetric != null) { accountPayoutCyclesMetric.Text = "0"; accountPayoutCyclesMetric.Foreground = Muted; }
                if (accountBlowoutMetric != null) { accountBlowoutMetric.Text = "0"; accountBlowoutMetric.Foreground = Muted; }
                if (accountLatestBlowoutMetric != null) { accountLatestBlowoutMetric.Text = "—"; accountLatestBlowoutMetric.Foreground = Muted; }
                if (accountLifecycleMetric != null) { accountLifecycleMetric.Text = "—"; accountLifecycleMetric.Foreground = Muted; }
                if (accountPnlMetric != null) { accountPnlMetric.Text = "$0"; accountPnlMetric.Foreground = Muted; }
                if (accountTradesMetric != null) { accountTradesMetric.Text = "0"; accountTradesMetric.Foreground = Muted; }
                if (accountFirstPayoutMetric != null) { accountFirstPayoutMetric.Text = "—"; accountFirstPayoutMetric.Foreground = Muted; }
                if (accountFirstPayoutDaysMetric != null) { accountFirstPayoutDaysMetric.Text = "—"; accountFirstPayoutDaysMetric.Foreground = Muted; }
                return;
            }
            double balance = config.EvaluationEnabled == -2 ? account.StartingBalance + account.TotalPnl : CurrentStageBalance(account);
            int totalBlowouts = account.FailedEvaluations + account.FailedFunded;
            double fullNetCash = account.PayoutCash - account.EvaluationCost;
            DateTime lifecycleEnd = account.TerminalLifecycleEnd != DateTime.MinValue ? account.TerminalLifecycleEnd : account.LastAssignedDate;
            if (accountBalanceMetric != null) { accountBalanceMetric.Text = Cash(balance); accountBalanceMetric.Foreground = balance < 0 || account.Blown ? Red : AccountLifecycleBrush(account); }
            if (accountPayoutCashMetric != null) { accountPayoutCashMetric.Text = Cash(account.PayoutCash); accountPayoutCashMetric.Foreground = account.PayoutCash > 0 ? Green : Muted; }
            if (accountCostMetric != null) { accountCostMetric.Text = Cash(account.EvaluationCost); accountCostMetric.Foreground = account.EvaluationCost > 0 ? Orchid : Muted; }
            if (accountNetCashAfterCostMetric != null) { accountNetCashAfterCostMetric.Text = Cash(fullNetCash); accountNetCashAfterCostMetric.Foreground = fullNetCash < 0 ? Red : (fullNetCash > 0 ? Green : Muted); }
            if (accountPayoutCyclesMetric != null) { accountPayoutCyclesMetric.Text = account.Payouts.ToString(CultureInfo.InvariantCulture); accountPayoutCyclesMetric.Foreground = account.Payouts > 0 ? Gold : Muted; }
            if (accountBlowoutMetric != null) { accountBlowoutMetric.Text = totalBlowouts.ToString(CultureInfo.InvariantCulture); accountBlowoutMetric.Foreground = totalBlowouts > 0 ? Red : Muted; }
            if (accountLatestBlowoutMetric != null) { accountLatestBlowoutMetric.Text = account.LastBlowoutDate == DateTime.MinValue ? "NONE" : account.LastBlowoutDate.ToString("yyyy-MM-dd"); accountLatestBlowoutMetric.Foreground = account.LastBlowoutDate == DateTime.MinValue ? Muted : Red; }
            if (accountLifecycleMetric != null)
            {
                string dates = account.InitialLifecycleStart == DateTime.MinValue ? "NO ASSIGNMENT" : account.InitialLifecycleStart.ToString("yyyy-MM-dd") + " → " + (lifecycleEnd == DateTime.MinValue ? "ACTIVE" : lifecycleEnd.ToString("yyyy-MM-dd"));
                if (account.InitialLifecycleStart != DateTime.MinValue && lifecycleEnd != DateTime.MinValue) dates += " • " + Math.Max(0, (lifecycleEnd.Date - account.InitialLifecycleStart.Date).Days) + " cal days";
                accountLifecycleMetric.Text = dates;
                accountLifecycleMetric.Foreground = AccountLifecycleBrush(account);
            }
            if (accountPnlMetric != null) { accountPnlMetric.Text = Cash(account.TotalPnl); accountPnlMetric.Foreground = account.TotalPnl < 0 ? Red : (account.TotalPnl > 0 ? Green : Muted); }
            if (accountTradesMetric != null) { accountTradesMetric.Text = account.Trades + " / " + account.Wins + "W " + account.Losses + "L"; accountTradesMetric.Foreground = account.Wins >= account.Losses ? Green : Red; }
            KeystoneArcFirstPayoutTiming timing = KeystoneArcEngine.GetFirstPayoutTiming(account);
            if (accountFirstPayoutMetric != null) { accountFirstPayoutMetric.Text = timing.HasPayout ? timing.FirstPayoutDate.ToString("yyyy-MM-dd") : "NO PAYOUT"; accountFirstPayoutMetric.Foreground = timing.HasPayout ? Gold : Muted; }
            if (accountFirstPayoutDaysMetric != null) { accountFirstPayoutDaysMetric.Text = timing.HasPayout ? timing.CalendarDaysFromInitial + " cal / " + timing.RecordedSessionsFromInitial + " sess" : "NOT RECORDED"; accountFirstPayoutDaysMetric.Foreground = timing.HasPayout ? Gold : Muted; }
        }

        private void UpdateOneDayAccountMetricTiles(KeystoneArcVirtualAccount account)
        {
            bool active = IsOneDayAssignmentMode();
            if (oneDayAccountMetrics != null) oneDayAccountMetrics.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (accountLifecycleMetrics != null) accountLifecycleMetrics.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
            if (oneDayAccountBalanceMetric == null) return;
            if (!active || account == null)
            {
                oneDayAccountBalanceMetric.Text = "$0"; oneDayAccountBalanceMetric.Foreground = Muted;
                if (oneDayAccountPnlMetric != null) { oneDayAccountPnlMetric.Text = "$0"; oneDayAccountPnlMetric.Foreground = Muted; }
                if (oneDayAccountTradesMetric != null) { oneDayAccountTradesMetric.Text = "0"; oneDayAccountTradesMetric.Foreground = Muted; }
                if (oneDayAccountStateMetric != null) { oneDayAccountStateMetric.Text = "UNUSED"; oneDayAccountStateMetric.Foreground = Muted; }
                if (oneDayAccountWindowMetric != null) { oneDayAccountWindowMetric.Text = "—"; oneDayAccountWindowMetric.Foreground = Muted; }
                if (oneDayAccountLimitsMetric != null) { oneDayAccountLimitsMetric.Text = "—"; oneDayAccountLimitsMetric.Foreground = Muted; }
                return;
            }
            Brush stateBrush = AccountLifecycleBrush(account);
            List<KeystoneArcEvent> rows = AccountAuditEvents(account);
            string window = rows.Count == 0 ? "NO ASSIGNMENT" : rows[0].EntryTime.ToString("yyyy-MM-dd HH:mm") + " → " + rows[rows.Count - 1].ExitTime.ToString("HH:mm");
            if (oneDayAccountBalanceMetric != null) { oneDayAccountBalanceMetric.Text = Cash(account.TotalPnl); oneDayAccountBalanceMetric.Foreground = account.TotalPnl < 0 ? Red : (account.TotalPnl > 0 ? Green : Muted); }
            if (oneDayAccountPnlMetric != null) { oneDayAccountPnlMetric.Text = Cash(account.DayPnl); oneDayAccountPnlMetric.Foreground = account.DayPnl < 0 ? Red : (account.DayPnl > 0 ? Green : Muted); }
            if (oneDayAccountTradesMetric != null) { oneDayAccountTradesMetric.Text = account.Trades + " / " + account.Wins + "W " + account.Losses + "L"; oneDayAccountTradesMetric.Foreground = stateBrush; }
            if (oneDayAccountStateMetric != null) { oneDayAccountStateMetric.Text = OneDayAccountStatus(account); oneDayAccountStateMetric.Foreground = stateBrush; }
            if (oneDayAccountWindowMetric != null) { oneDayAccountWindowMetric.Text = window; oneDayAccountWindowMetric.Foreground = rows.Count > 0 ? Cyan : Muted; }
            if (oneDayAccountLimitsMetric != null) { oneDayAccountLimitsMetric.Text = "PROFIT " + Cash(config.DailyGoal) + " • LOSS -" + Cash(Math.Abs(config.DailyLoss)).Replace("-$", "$"); oneDayAccountLimitsMetric.Foreground = Gold; }
        }

        private List<KeystoneArcEvent> AccountAuditEvents(KeystoneArcVirtualAccount account)
        {
            if (account == null || events == null) return new List<KeystoneArcEvent>();
            bool asianCopy = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            if (asianCopy && account.Trades > 0)
                return events.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && x.AssignedVirtualAccount.StartsWith("COPY", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.EntryTime).ToList();
            return events.Where(x => string.Equals(x.AssignedVirtualAccount, account.Name, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.EntryTime).ToList();
        }

        private void RenderAccountTimeline(KeystoneArcVirtualAccount account)
        {
            if (poolTimelineStack == null) return;
            poolTimelineStack.Children.Clear();
            if (account == null) return;
            List<KeystoneArcAccountDay> days = account.DayHistory.OrderBy(x => x.Day).ToList();
            if (days.Count == 0)
            {
                AddAccountTimelineCard(DateTime.MinValue, "WAITING FOR DAILY RECORD", "No completed lifecycle day has been recorded for this account.", Muted);
                return;
            }
            if (IsOneDayAssignmentMode())
            {
                DateTime day = days[0].Day;
                Brush stateBrush = AccountLifecycleBrush(account);
                AddAccountTimelineCard(day, OneDayAccountStatus(account), "Assigned trades " + account.Trades + " • W " + account.Wins + " • L " + account.Losses + " • day P/L " + Cash(account.DayPnl) + " • daily profit lock " + Cash(config.DailyGoal) + " • daily loss lock -" + Cash(Math.Abs(config.DailyLoss)).Replace("-$", "$") + ".", stateBrush);
                List<KeystoneArcEvent> rows = AccountAuditEvents(account);
                foreach (KeystoneArcEvent e in rows)
                    AddAccountTimelineCard(e.EntryTime, e.Outcome + " • " + e.Symbol + " " + e.SetupClass, "Entry " + e.Entry.ToString("0.00") + " • exit " + (double.IsNaN(e.ExitPrice) ? "n/a" : e.ExitPrice.ToString("0.00")) + " @ " + e.ExitTime.ToString("HH:mm") + " • P/L " + Cash(e.GrossPnl) + ".", e.GrossPnl < 0 ? Red : (e.GrossPnl > 0 ? Green : Cyan));
                if (rows.Count == 0) AddAccountTimelineCard(day, "NO ASSIGNMENT", "This account was not reached by the allocation before the selected session ended.", Muted);
                return;
            }
            DateTime first = days[0].Day;
            if (config.EvaluationEnabled == -2)
                AddAccountTimelineCard(first, "SINGLE ACCOUNT RANGE", "Start " + Cash(account.StartingBalance) + " • range P/L " + Cash(account.TotalPnl) + " • no payout lifecycle.", Cyan);
            else if (config.EvaluationEnabled == 0)
                AddAccountTimelineCard(first, "DIRECT-FUNDED START", "This slot began funded. Evaluation purchases and replacements do not apply in direct-funded mode.", Green);
            else
                AddAccountTimelineCard(first, "EVALUATION START", "Evaluation-first lifecycle. Each replacement is a new paid evaluation only after a prior failure.", Orchid);

            int priorPurchases = 0, priorPasses = 0, priorPayouts = 0;
            double priorGross = 0, priorNet = 0;
            bool priorBlown = false;
            foreach (KeystoneArcAccountDay day in days)
            {
                int purchaseDelta = day.EvaluationPurchasesAfter - priorPurchases;
                int passDelta = day.EvaluationPassesAfter - priorPasses;
                int payoutDelta = day.PayoutsAfter - priorPayouts;
                string dayStage = AccountDayPrimaryStage(day);
                string daySignals = (day.DayLocked ? (day.DayPnl >= 0 ? "PROFIT LOCK" : "LOSS LOCK") : "ACTIVE") +
                    (day.EvalQualifyingDay ? " • EVAL QUALIFIED" : string.Empty) +
                    (day.FundedQualifyingDay ? " • FUNDED QUALIFIED" : string.Empty) +
                    (purchaseDelta > 0 ? " • EVAL PURCHASE +" + purchaseDelta : string.Empty) +
                    (passDelta > 0 ? " • PASS +" + passDelta : string.Empty) +
                    (payoutDelta > 0 ? " • PAYOUT +" + payoutDelta : string.Empty) +
                    (day.ReplacementPendingAfter ? " • REPLACEMENT WAIT" : string.Empty) +
                    (day.FundedCapPendingAfter ? " • FIRM CAP WAIT" : string.Empty) +
                    (day.BlownAfter ? " • BLOWN" : string.Empty);
                string dayBalances = "TRADES " + day.TradesAfter + " • W/L " + day.WinsAfter + "/" + day.LossesAfter + " • DAY P/L " + Cash(day.DayPnl) +
                    " • BAL " + Cash(day.BalanceAfter) + " (from " + Cash(day.BalanceBefore) + ") • EVAL BAL " + Cash(day.EvaluationBalanceAfter) + " • FUNDED BAL " + Cash(day.FundedBalanceAfter) +
                    " • COST +" + Cash(day.CostDelta) + " • PAYOUT NET +" + Cash(day.PayoutCashDelta) + " • EVAL DAYS " + day.ConsecutiveEvalQualifyingDaysAfter + " • PAYOUT DAYS " + day.FundedQualifyingDaysAfter +
                    " • CLOSE " + (string.IsNullOrWhiteSpace(day.StateAfterClose) ? dayStage : day.StateAfterClose);
                AddAccountTimelineCard(day.Day, dayStage + " • DAILY RECORD", daySignals + "\n" + dayBalances, AccountDayBrush(day));
                if (day.EvaluationPurchasesAfter > priorPurchases)
                {
                    int added = purchaseDelta;
                    AddAccountTimelineCard(day.Day, "EVALUATION PURCHASED", added + " new evaluation purchase(s) • cumulative cost " + Cash(account.EvaluationCost) + ".", Orchid);
                }
                if (day.EvaluationPassesAfter > priorPasses)
                    AddAccountTimelineCard(day.Day, "EVALUATION PASSED → FUNDED", "Evaluation pass " + day.EvaluationPassesAfter + " completed on " + day.EvaluationPassDateAfter.ToString("yyyy-MM-dd") + ". Funded lifecycle begins after this date.", Green);
                if (day.PayoutsAfter > priorPayouts)
                {
                    int added = payoutDelta;
                    double gross = day.PayoutGrossAfter - priorGross;
                    double net = day.PayoutCashAfter - priorNet;
                    AddAccountTimelineCard(day.Day, "PAYOUT " + day.PayoutsAfter + " RECEIVED", added + " payout cycle • gross withdrawal " + Cash(gross) + " • net cash after share " + Cash(net) + ".", Gold);
                }
                if (day.BlownAfter && !priorBlown)
                {
                    string detail = day.ReplacementPendingAfter
                        ? "Drawdown reached. This slot is unavailable today; a replacement evaluation is scheduled for the next session."
                        : (day.PayoutsAfter == 0
                            ? "Drawdown reached before any payout. This slot is terminally unavailable for the rest of the selected range."
                            : "Drawdown reached after " + day.PayoutsAfter + " historical payout cycle(s). Those payouts were already received; this slot is now terminally unavailable.");
                    AddAccountTimelineCard(day.Day, "BLOWOUT", detail, Red);
                }
                priorPurchases = day.EvaluationPurchasesAfter;
                priorPasses = day.EvaluationPassesAfter;
                priorPayouts = day.PayoutsAfter;
                priorGross = day.PayoutGrossAfter;
                priorNet = day.PayoutCashAfter;
                priorBlown = day.BlownAfter;
            }
            string current = account.Blown && !account.ReplacementPending
                ? "FINAL STATE: BLOWN • slot ended"
                : account.ReplacementPending
                    ? (account.ReplacementBudgetBlocked ? "CURRENT STATE: BENCHED • waiting for modeled payout cash to fund the replacement" : "CURRENT STATE: replacement evaluation pending next session")
                    : "CURRENT STATE: " + AccountLifecycleLabel(account);
            AddAccountTimelineCard(days[days.Count - 1].Day, current, "Current stage balance " + Cash(CurrentStageBalance(account)) + " • lifetime net payout cash " + Cash(account.PayoutCash) + ".", AccountLifecycleBrush(account));
        }

        private static string AccountDayPrimaryStage(KeystoneArcAccountDay day)
        {
            if (day == null) return "NO DAILY RECORD";
            if (day.BlownAfter && !day.ReplacementPendingAfter) return "BLOWN";
            if (day.ReplacementPendingAfter) return "BENCHED / REPLACEMENT";
            if (day.FundedCapPendingAfter) return "FIRM CAP WAIT";
            if (day.PayoutsAfter > 0) return "PAYOUT HISTORY";
            return day.FundedAfter ? "FUNDED" : "EVALUATION";
        }

        private Brush AccountDayBrush(KeystoneArcAccountDay day)
        {
            if (day == null) return Muted;
            if (day.BlownAfter) return Red;
            if (day.ReplacementPendingAfter || day.FundedCapPendingAfter || day.PayoutsAfter > 0) return Gold;
            if (day.FundedAfter) return Green;
            return Orchid;
        }

        private void AddAccountTimelineCard(DateTime day, string title, string detail, Brush brush)
        {
            if (poolTimelineStack == null) return;
            string date = day == DateTime.MinValue ? "" : day.ToString("yyyy-MM-dd") + " • ";
            var text = new TextBlock { Text = date + title + "\n" + detail, Foreground = Text, FontSize = 10, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(7, 4, 7, 4) };
            poolTimelineStack.Children.Add(new Border { Background = Panel, BorderBrush = brush, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(4), Margin = new Thickness(3, 2, 3, 2), Child = text });
        }

        private string PayoutIntervalSummary(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.Payouts <= 0) return "PAYOUT TIMING: no payout cycle recorded in this range.";
            var dates = new List<DateTime>();
            int prior = 0;
            foreach (KeystoneArcAccountDay day in account.DayHistory.OrderBy(x => x.Day))
            {
                if (day.PayoutsAfter > prior) { dates.Add(day.Day); prior = day.PayoutsAfter; }
            }
            if (dates.Count == 0) return "PAYOUT TIMING: " + account.Payouts + " recorded cycle(s); no dated audit row was retained.";
            if (dates.Count == 1) return "PAYOUT TIMING: first recorded " + dates[0].ToString("yyyy-MM-dd") + " • no interval yet.";
            var intervals = new List<int>();
            for (int i = 1; i < dates.Count; i++) intervals.Add((dates[i] - dates[i - 1]).Days);
            return "PAYOUT TIMING: " + dates.Count + " dated cycles • average " + intervals.Average().ToString("0.0") + " calendar days between payouts • last " + dates[dates.Count - 1].ToString("yyyy-MM-dd") + ".";
        }

        private string WeeklyMonthlyPnlSummary(KeystoneArcVirtualAccount account)
        {
            if (account == null || account.DayHistory.Count == 0) return "PERIOD P/L: no completed daily record.";
            List<KeystoneArcAccountDay> days = account.DayHistory.OrderBy(x => x.Day).ToList();
            double dailyAverage = days.Average(x => x.DayPnl);
            var weeks = days.GroupBy(x => x.Day.AddDays(-((7 + (int)x.Day.DayOfWeek - 1) % 7)).Date).ToList();
            var months = days.GroupBy(x => new DateTime(x.Day.Year, x.Day.Month, 1)).ToList();
            double bestWeek = weeks.Max(g => g.Sum(x => x.DayPnl));
            double bestMonth = months.Max(g => g.Sum(x => x.DayPnl));
            return "PERIOD P/L: " + days.Count + " recorded days • average/day " + Cash(dailyAverage) + " • " + weeks.Count + " weeks (best " + Cash(bestWeek) + ") • " + months.Count + " months (best " + Cash(bestMonth) + ").";
        }

        private string AccountListProgress(KeystoneArcVirtualAccount a)
        {
            if (config.EvaluationEnabled == -2) return "END " + (a.StartingBalance + a.TotalPnl).ToString("C0") + " • RANGE P/L " + a.TotalPnl.ToString("C0");
            if (config.EvaluationEnabled < 0) return "ONE-DAY";
            if (a.Blown && !a.ReplacementPending) return "BLOWN • NO MORE ASSIGNMENTS";
            if (a.ReplacementPending && a.ReplacementBudgetBlocked) return "BENCHED • PAYOUT CASH GATE";
            if (a.ReplacementPending) return "REPLACEMENT NEXT SESSION";
            if (!a.Funded) return "EVAL target left " + Math.Max(0, config.EvaluationTarget - a.EvaluationBalance).ToString("C0") + " • consecutive days " + a.ConsecutiveEvalQualifyingDays + "/" + config.MinimumPositiveDays;
            return "PAYOUT left " + Math.Max(0, config.PayoutThreshold - a.FundedBalance).ToString("C0") + " • days " + Math.Max(0, config.PayoutDaysRequired - a.FundedPositiveDays);
        }

        private string AccountEvalProgress(KeystoneArcVirtualAccount a)
        {
            if (config.EvaluationEnabled == -2) return "SINGLE ACCOUNT: lifecycle disabled.";
            if (config.EvaluationEnabled < 0) return "EVALUATION: one-day study; lifecycle not applied.";
            if (a.Blown && !a.ReplacementPending) return "EVALUATION: direct-funded slot is blown and unavailable for the remainder of this range.";
            if (a.Funded) return "EVALUATION: passed / direct-funded start.";
            if (a.ReplacementPending) return a.ReplacementBudgetBlocked
                ? "EVALUATION: failed slot is BENCHED. The optional payout-funded replacement policy will not purchase another evaluation until modeled payout cash after share covers the next evaluation. Completed passes " + a.EvaluationPasses + " • total purchases " + a.EvaluationPurchases
                : "EVALUATION: failed slot is locked; a new evaluation is purchased at the next session only. Completed passes " + a.EvaluationPasses + " • total purchases " + a.EvaluationPurchases;
            double dailyRequirement = Math.Max(config.MinimumQualifyingDayProfit, DailyEvaluationTargetForDisplay());
            return "EVALUATION: balance " + a.EvaluationBalance.ToString("C0") + " • target left " + Math.Max(0, config.EvaluationTarget - a.EvaluationBalance).ToString("C0") + " • consecutive qualifying days " + a.ConsecutiveEvalQualifyingDays + "/" + config.MinimumPositiveDays + " • days left " + Math.Max(0, config.MinimumPositiveDays - a.ConsecutiveEvalQualifyingDays) + " • daily requirement " + dailyRequirement.ToString("C0") + " • total drawdown room " + Math.Max(0, a.EvaluationBalance + config.EvaluationFailure).ToString("C0") + " • completed passes " + a.EvaluationPasses;
        }

        private double DailyEvaluationTargetForDisplay()
        {
            double daily = Math.Max(0, config.DailyGoal);
            double cap = Math.Max(0, config.EvaluationDailyCreditCap);
            return daily <= 0 ? cap : (cap <= 0 ? daily : Math.Min(daily, cap));
        }

        private string AccountPayoutProgress(KeystoneArcVirtualAccount a)
        {
            if (config.EvaluationEnabled == -2) return "PAYOUT: disabled in single-account range P/L mode.";
            if (config.EvaluationEnabled < 0) return "PAYOUT: one-day study; lifecycle not applied.";
            if (a.Blown && !a.ReplacementPending)
            {
                if (a.Payouts == 0) return "PAYOUT: none received. This direct-funded slot reached total drawdown before a payout and is now ended.";
                return "PAYOUT: " + a.Payouts + " historical cycle(s) were received before this slot later reached total drawdown. Gross received " + Cash(a.PayoutGrossWithdrawn) + " • net cash after share " + Cash(a.PayoutCash) + ". No future payout is possible because the slot is ended.";
            }
            if (!a.Funded) return "PAYOUT: not funded yet.";
            double modeledCash = config.PayoutAmount * config.PayoutProfitSharePercent / 100.0;
            return "PAYOUT: cycles received " + a.Payouts + " • net cash received " + a.PayoutCash.ToString("C0") + " • current funded balance " + a.FundedBalance.ToString("C0") + " • ready balance " + config.PayoutThreshold.ToString("C0") + " • threshold left " + Math.Max(0, config.PayoutThreshold - a.FundedBalance).ToString("C0") + " • qualifying days " + a.FundedPositiveDays + "/" + config.PayoutDaysRequired + " • days left " + Math.Max(0, config.PayoutDaysRequired - a.FundedPositiveDays) + " • next gross withdrawal " + config.PayoutAmount.ToString("C0") + " • account share " + config.PayoutProfitSharePercent.ToString("0.#") + "% = " + modeledCash.ToString("C0") + " • balance after next withdrawal " + Math.Max(0, config.PayoutThreshold - config.PayoutAmount).ToString("C0") + " • daily loss " + (config.FundedDailyLoss <= 0 ? "OFF" : config.FundedDailyLoss.ToString("C0")) + " • total drawdown room " + Math.Max(0, a.FundedBalance + config.FundedFailure).ToString("C0") + " • qualifying threshold " + config.MinimumQualifyingDayProfit.ToString("C0");
        }

        private bool ShowPoolEvent(KeystoneArcEvent e)
        {
            if (e == null) return false;
            if (e.Outcome == "WIN") return showWinsBox == null || showWinsBox.IsChecked != false;
            if (e.Outcome.StartsWith("LOSS")) return showLossesBox == null || showLossesBox.IsChecked != false;
            if (e.Outcome == "SESSION EXIT") return showExitsBox == null || showExitsBox.IsChecked != false;
            if (e.Outcome == "NO ENTRY DATA") return showNoEntryBox == null || showNoEntryBox.IsChecked != false;
            return true;
        }

        private void RenderLifecycle()
        {
            if (poolResultBanner != null)
            {
                if (accounts.Count == 0) { poolResultBanner.Text = "POOL RESULTS: no virtual accounts were created."; return; }
                int passed = accounts.Sum(x => x.EvaluationPasses);
                int funded = accounts.Count(x => x.Funded);
                int payouts = accounts.Sum(x => x.Payouts);
                KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
                poolResultBanner.Text = config.EvaluationEnabled == -2
                    ? "SINGLE ACCOUNT COMPLETE: starting balance plus range P/L have been calculated. No payout, evaluation, funded-stage, or replacement lifecycle was applied."
                    : config.EvaluationEnabled < 0
                    ? "ONE-DAY POOL COMPLETE: " + accounts.Count + " ACCOUNTS • TRADED " + accounts.Count(x => x.Trades > 0) + " • PROFIT LOCKS " + accounts.Count(x => x.DayLocked && x.DayPnl >= Math.Max(0, config.DailyGoal)) + " • LOSS LOCKS " + accounts.Count(x => x.DayLocked && x.DayPnl <= -Math.Abs(config.DailyLoss)) + " • SELECT A CARD FOR ITS EXACT ASSIGNED TRADES."
                    : config.EvaluationEnabled == 0
                    ? "POOL COMPLETE: " + accounts.Count + " DIRECT-FUNDED START ACCOUNTS • FUNDED " + funded + " • PAYOUT CYCLES " + payouts + " • SELECT AN ACCOUNT FOR ITS FULL LIFECYCLE."
                    : "POOL COMPLETE: " + accounts.Count + " ACCOUNTS • EVALUATION PASSES " + passed + " • CURRENTLY FUNDED " + funded + " • PAYOUT CYCLES " + payouts + (capital.GateEnabled ? " • WAIT FOR PAYOUT BEFORE REBUY ON • BENCHED " + capital.BenchedReplacementSlots + " • REINVESTMENT CASH " + Cash(capital.ReplacementCashAvailable) + " • RELEASE REQUIRED " + Cash(capital.CashRequiredForPendingReplacements) : " • CONTINUOUS REPLACEMENT ACTIVE • " + capital.ReplacementEvaluationPurchases + " REPLACEMENT EVALS PURCHASED • " + (capital.FirstPayoutReached ? "FIRST PAYOUT " + capital.FirstPayoutDate.ToString("yyyy-MM-dd") + " AFTER " + Cash(capital.InvestmentThroughFirstPayoutDate) + " INVESTMENT" : "NO PAYOUT IN THIS RANGE; ACTIVE SLOTS CONTINUED TRADING")) + " • SELECT AN ACCOUNT FOR ITS FULL LIFECYCLE.";
                return;
            }
            if (lifecycleText == null) return;
            if (accounts.Count == 0) { lifecycleText.Text = "No lifecycle calculation yet. Run accepted-event rotation first."; return; }
            var sb = new StringBuilder();
            sb.AppendLine("ACCOUNT BALANCE & PROGRESS • illustrative scenario");
            sb.AppendLine("Qualifying-day threshold: " + config.MinimumQualifyingDayProfit.ToString("C0"));
            sb.AppendLine();
            for (int i = 0; i < accounts.Count; i++)
            {
                KeystoneArcVirtualAccount a = accounts[i];
                sb.AppendLine(a.Name + " • " + a.LastState);
                sb.AppendLine("  ACCOUNT P/L " + a.TotalPnl.ToString("C0") + " | TRADES " + a.Trades + " | W/L " + a.Wins + "/" + a.Losses + " | COST " + a.EvaluationCost.ToString("C0"));
                sb.AppendLine("  " + AccountEvalProgress(a));
                sb.AppendLine("  " + AccountPayoutProgress(a));
                sb.AppendLine("  PAYOUTS " + a.Payouts + " | GROSS WITHDRAWN " + a.PayoutGrossWithdrawn.ToString("C0") + " | NET ACCOUNT SHARE " + a.PayoutCash.ToString("C0") + " | FAILURES E/F " + a.FailedEvaluations + "/" + a.FailedFunded);
                if (a.DayHistory.Count > 0)
                {
                    KeystoneArcAccountDay last = a.DayHistory[a.DayHistory.Count - 1];
                    sb.AppendLine("  LAST RECORDED DAY " + last.Day.ToString("yyyy-MM-dd") + " | P/L " + last.DayPnl.ToString("C0") + " | EVAL DAY " + (last.EvalQualifyingDay ? "YES" : "NO") + " | PAYOUT DAY " + (last.FundedQualifyingDay ? "YES" : "NO"));
                }
                sb.AppendLine();
            }
            lifecycleText.Text = sb.ToString();
        }

        private void RefreshSavedRuns()
        {
            savedFiles = new List<string>();
            string dir = DataDirectory(); if (Directory.Exists(dir)) savedFiles = Directory.GetFiles(dir).OrderByDescending(x => File.GetLastWriteTime(x)).ToList();
            if (savedRunList != null) savedRunList.Items.Clear();
            for (int i = 0; i < savedFiles.Count; i++) if (savedRunList != null) savedRunList.Items.Add(Path.GetFileName(savedFiles[i]));
            if (savedDataText != null) savedDataText.Text = savedFiles.Count == 0 ? "No Keystone Arc snapshots or exports found." : savedFiles.Count + " file(s) in " + dir + ". Select a file to inspect its path and timestamp.";
            if (savedRunList != null && savedFiles.Count > 0) savedRunList.SelectedIndex = 0;
        }

        private void ShowSavedRunDetail()
        {
            if (savedRunList == null || savedRunList.SelectedIndex < 0 || savedRunList.SelectedIndex >= savedFiles.Count || savedDataText == null) return;
            string file = savedFiles[savedRunList.SelectedIndex];
            savedDataText.Text = "FILE: " + file + "\nMODIFIED: " + File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm") + "\nSIZE: " + new FileInfo(file).Length.ToString("N0") + " bytes\n\nSnapshots and exports are separate from Apex/FVG and can be opened in a spreadsheet or text editor.";
        }

        private bool ReadConfig(out DateTime start, out DateTime end)
        {
            start = DateTime.MinValue; end = DateTime.MinValue;
            if (!DateTime.TryParseExact((startBox == null ? "" : startBox.Text).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) { UpdateUi("DATE ERROR • USE YYYY-MM-DD", Red); return false; }
            bool oneDay = dateModeBox == null || string.Equals(Convert.ToString(dateModeBox.SelectedItem), "ONE DAY", StringComparison.OrdinalIgnoreCase);
            DateTime selectedEnd;
            if (oneDay) selectedEnd = start;
            else if (!DateTime.TryParseExact((endBox == null ? "" : endBox.Text).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedEnd) || start > selectedEnd) { UpdateUi("DATE RANGE ERROR • USE YYYY-MM-DD AND START MUST NOT EXCEED END", Red); return false; }
            bool asian75 = IsAsian75Selected();
            config.OneDayMode = oneDay ? 1 : 0;
            // Asian defaults to BOTH, while the active selector may deliberately isolate MNQ or
            // MGC for a one-instrument cycle backtest.  Never request an unselected instrument.
            config.Scope = scopeBox == null ? (asian75 ? "BOTH" : "MNQ") : Convert.ToString(scopeBox.SelectedItem ?? (asian75 ? "BOTH" : "MNQ"));
            if (config.Scope != "MNQ" && config.Scope != "MGC" && config.Scope != "BOTH") config.Scope = asian75 ? "BOTH" : "MNQ";
            config.AccountPath = "PROP";
            config.StrategyCode = asian75 ? "ASIAN75" : "BH";
            config.SetupMinutes = asian75 ? 1 : SetupMinutesFromDisplay(timeframeBox == null ? string.Empty : Convert.ToString(timeframeBox.SelectedItem));
            config.DirectionMode = "BB";
            config.EnableBh = asian75 ? 0 : 1;
            config.EnableFvg = 0;
            config.BhAggressionFilter = "ALL";
            config.MnqStrongRedCandles = Math.Max(0, Integer(mnqStrongRedBox, 0));
            config.MnqStrongDeclinePoints = Math.Max(0, NumberAllowZero(mnqStrongDeclineBox, 0));
            config.MgcStrongRedCandles = Math.Max(0, Integer(mgcStrongRedBox, 0));
            config.MgcStrongDeclineDollars = Math.Max(0, NumberAllowZero(mgcStrongDeclineBox, 0));
            config.BhStrongCombine = bhStrongCombineBox != null && Convert.ToString(bhStrongCombineBox.SelectedItem).StartsWith("ALL", StringComparison.OrdinalIgnoreCase) ? "ALL" : "ANY";
            bool needMnq = config.Scope == "MNQ" || config.Scope == "BOTH";
            bool needMgc = config.Scope == "MGC" || config.Scope == "BOTH";
            // MNQ did not exist before 2019-05-06. Do not allow a continuous/merged NQ chart to
            // masquerade as Micro Nasdaq history; it creates misleading prices and unverified
            // entries. MGC remains independently available for its own valid date range.
            if (needMnq && start.Date < new DateTime(2019, 5, 6))
            {
                UpdateUi("MNQ HISTORY STARTS 2019-05-06 • THIS RANGE WOULD PRE-DATE THE MICRO NASDAQ CONTRACT AND IS BLOCKED TO AVOID A MISLEADING CHART. SELECT 2019-05-06 OR LATER, OR TEST MGC ALONE.", Red);
                return false;
            }
            Instrument mnq = null, mgc = null; string mnqSource = "NOT SELECTED", mgcSource = "NOT SELECTED";
            // BH deliberately retains the previously working range-end contract anchor.  A
            // 2026 Jan→Sep BH range must not begin by requesting the expired Dec-25 contract
            // merely because it was the first dated chart encountered.  Asian is isolated: its
            // cycle data is anchored at its own session start while its backtester is developed.
            DateTime resolverDate = asian75 ? start.Date : selectedEnd.Date;
            if (needMnq) mnq = ResolveDynamicInstrument("MNQ", resolverDate, out mnqSource);
            // MGC is deliberately independent from MNQ here.  The direct MGC 1M path must begin
            // on the range's first compatible metals contract so NinjaTrader's merge policy can
            // carry the requested historic gold range.  Anchoring gold to a far later current
            // contract produced visible 5M setup marks but an empty/mismatched 1M outcome path.
            if (needMgc) mgc = ResolveDynamicInstrument("MGC", start.Date, out mgcSource);
            if (needMnq && mnq == null) { UpdateUi("MNQ INSTRUMENT ERROR • OPEN OR SELECT THE MNQ CHART, THEN CLICK REFRESH OPEN CHART INSTRUMENTS", Red); return false; }
            if (needMgc && mgc == null) { UpdateUi("MGC INSTRUMENT ERROR • OPEN OR SELECT THE MGC CHART, THEN CLICK REFRESH OPEN CHART INSTRUMENTS", Red); return false; }
            configuredMnqInstrument = mnq; configuredMgcInstrument = mgc;
            config.MnqName = mnq == null ? string.Empty : mnq.FullName; config.MgcName = mgc == null ? string.Empty : mgc.FullName;
            if (mnqBox != null) mnqBox.Text = mnq == null ? "NOT SELECTED" : mnqSource;
            if (mgcBox != null) mgcBox.Text = mgc == null ? "NOT SELECTED" : mgcSource;
            config.SessionMode = asian75 ? "ASIAN75" : SessionModeFromDisplay(sessionBox == null ? "NY AFTER 09:30" : Convert.ToString(sessionBox.SelectedItem));
            config.MnqStart = Integer(mnqStartBox, 930); config.MgcStart = Integer(mgcStartBox, 800); config.CustomStart = Integer(customStartBox, 930); config.EndTime = Integer(endTimeBox, 1555);
            config.AsianStartHhmm = Integer(asianStartTimeBox, 1800);
            config.AsianEndHhmm = Integer(asianEndTimeBox, 1555);
            string asianDirection = asianDirectionBox != null && string.Equals(Convert.ToString(asianDirectionBox.SelectedItem), "SHORT", StringComparison.OrdinalIgnoreCase) ? "SHORT" : "LONG";
            config.AsianMnqInitialDirection = asianDirection;
            config.AsianMgcInitialDirection = asianDirection;
            config.AsianRiskMode = "CASH";
            if (asian75) { config.CustomStart = config.AsianStartHhmm; config.EndTime = config.AsianEndHhmm; }
            if (!IsValidHhmm(config.CustomStart) || !IsValidHhmm(config.EndTime)) { UpdateUi("SESSION TIME ERROR • USE HHMM FROM 0000 TO 2359", Red); return false; }
            DateTime requestStart, requestEnd;
            GetConfiguredSessionBounds(start.Date, selectedEnd.Date, config, out requestStart, out requestEnd);
            config.Start = requestStart; config.End = requestEnd;
            config.Quantity = Integer(quantityBox, 0); config.PoolSize = Number(poolBox, 10); config.CopyTradingPool = copyTradingPoolBox != null && copyTradingPoolBox.IsChecked == true ? 1 : 0; config.AllowMultipleSetupsPerDay = multipleSetupsPerDayBox != null && multipleSetupsPerDayBox.IsChecked == true ? 1 : 0; config.TargetDollars = Number(targetBox, 0); config.StopDollars = Number(stopBox, 0); config.DailyGoal = Number(dailyGoalBox, 0); config.DailyLoss = Number(dailyLossBox, 0);
            RefreshAsianDerivedInputs();
            config.AsianReversalLossDollars = Number(asianReversalLossBox, 75);
            config.AsianMnqReversalPriceMove = Number(asianMnqPriceMoveBox, 37.5);
            config.AsianMgcReversalPriceMove = Number(asianMgcPriceMoveBox, 7.5);
            config.AsianCycleTargetDollars = Number(asianCycleTargetBox, 350);
            config.AsianCombinedStopLossDollars = 0;
            config.AsianDailyLossLimitDollars = Number(asianDailyLossBox, 2000);
            config.AsianInstrumentStopLossDollars = 0;
            config.AsianMnqInstrumentStopLossDollars = 0;
            config.AsianMgcInstrumentStopLossDollars = 0;
            config.AsianBreakEvenTriggerDollars = NumberAllowZero(asianBreakEvenBox, 0);
            config.AsianStartingQuantity = Integer(asianStartingQuantityBox, 1);
            config.AsianMaxReversalsPerInstrument = Math.Max(1, Integer(asianMaxReversalsBox, 4));
            config.AsianMnqMaxReversals = config.AsianMaxReversalsPerInstrument;
            config.AsianMgcMaxReversals = config.AsianMaxReversalsPerInstrument;
            config.AsianMaxTotalLegsPerInstrument = Math.Max(1, Math.Max(config.AsianMnqMaxReversals, config.AsianMgcMaxReversals) + 1);
            string stopDisplay = stopModeBox == null ? string.Empty : Convert.ToString(stopModeBox.SelectedItem);
            config.StopMode = stopDisplay == "LIVE STANDARD • FIXED LOT" ? "LIVE_STANDARD" :
                (stopDisplay == "LIVE BELOW 3-CANDLE LOW • FIXED LOT" ? "LIVE_LOW_FIXED" :
                (stopDisplay == "LIVE BELOW 3-CANDLE LOW • AUTO-RISK LOT" ? "LIVE_LOW_AUTO_RISK" :
                (stopDisplay == "PROP BELOW 3-CANDLE LOW • RISK-SIZED" ? "BELOW_SETUP_LOW" : "STANDARD")));
            config.MnqStopOffsetPoints = NumberAllowZero(mnqStopOffsetBox, 5); config.MgcStopOffsetPoints = NumberAllowZero(mgcStopOffsetBox, 1);
            config.PersonalLotSize = Number(personalLotBox, 0); config.MnqCashPerPointPerLot = Number(mnqCashValueBox, 0); config.MgcCashPerPointPerLot = Number(mgcCashValueBox, 0);
            config.MnqTargetMove = Number(mnqTargetMoveBox, 0); config.MgcTargetMove = Number(mgcTargetMoveBox, 0); config.MnqStandardStopMove = Number(mnqStandardStopMoveBox, 0); config.MgcStandardStopMove = Number(mgcStandardStopMoveBox, 0);
            config.PersonalMaxRiskDollars = Number(personalMaxRiskBox, 0); config.BreakEvenEnabled = breakEvenBox != null && breakEvenBox.IsChecked == true ? 1 : 0; config.BreakEvenTriggerMove = NumberAllowZero(breakEvenMoveBox, 0);
            config.PropStartingBalance = NumberAllowZero(propStartingBalanceBox, 0); config.PersonalStartingBalance = NumberAllowZero(personalStartingBalanceBox, 0);
            if ((!asian75 && (config.Quantity <= 0 || config.TargetDollars <= 0 || config.StopDollars <= 0 || config.MnqStopOffsetPoints < 0 || config.MgcStopOffsetPoints < 0)) || config.DailyGoal <= 0 || config.DailyLoss <= 0)
            { UpdateUi("PROP MODEL ERROR • REQUIRED TARGETS, LIMITS, AND ACTIVE RISK VALUES MUST BE POSITIVE", Red); return false; }
            bool asianRiskInvalid = config.AsianRiskMode == "PRICE"
                ? (config.AsianMnqReversalPriceMove <= 0 || config.AsianMgcReversalPriceMove <= 0)
                : config.AsianReversalLossDollars <= 0;
            if (asian75 && (!IsValidHhmm(config.AsianStartHhmm) || !IsValidHhmm(config.AsianEndHhmm) || asianRiskInvalid || config.AsianCycleTargetDollars <= 0 || config.AsianCombinedStopLossDollars < 0 || config.AsianDailyLossLimitDollars <= 0 || config.AsianMnqInstrumentStopLossDollars < 0 || config.AsianMgcInstrumentStopLossDollars < 0 || config.AsianBreakEvenTriggerDollars < 0 || config.AsianStartingQuantity <= 0 || config.AsianMnqMaxReversals < 0 || config.AsianMgcMaxReversals < 0))
            { UpdateUi("ASIAN BACKTEST ERROR • VALID START/END, REVERSAL LIMIT, COMBINED TARGET/DAILY LIMIT, INITIAL MICRO SIZE, AND PER-INSTRUMENT REVERSAL COUNTS ARE REQUIRED; OPTIONAL STOPS/BREAKEVEN MAY BE 0", Red); return false; }
            // A single selected account is a simple range P/L ledger by request: it does not
            // invent evaluation purchases, funded balances, payouts, or replacement cycles.
            // One-day studies remain assignment-only. Larger pools use the selected lifecycle.
            config.EvaluationEnabled = config.PoolSize == 1 ? -2 : (config.OneDayMode == 1 ? -1 : (accountStartModeBox != null && string.Equals(Convert.ToString(accountStartModeBox.SelectedItem), "DIRECT FUNDED", StringComparison.OrdinalIgnoreCase) ? 0 : 1));
            config.EvaluationTarget = Number(evalTargetBox, 3000); config.PayoutThreshold = Number(payoutThresholdBox, 4000); config.PayoutAmount = Number(payoutAmountBox, 2000); config.PayoutProfitSharePercent = Number(payoutProfitShareBox, 100); config.EvaluationConsistencyPercent = NumberAllowZero(evalConsistencyBox, 0); config.EvaluationFailure = Number(evalFailureBox, 2000); config.EvaluationStageTradeRulesEnabled = !asian75 && evalStageTradeRulesBox != null && evalStageTradeRulesBox.IsChecked == true ? 1 : 0;
            // Default behavior is deliberately simple: evaluation and funded accounts use the
            // normal $1,500 profit / -$500 loss trade and daily limits selected in Step 1.
            // The compact EVAL OVERRIDE switch is the only path that exposes separate values.
            if (config.EvaluationStageTradeRulesEnabled > 0)
            {
                config.EvaluationDailyCreditCap = Number(evalDailyCapBox, config.DailyGoal);
                config.EvaluationDailyLoss = Number(evalDailyLossBox, config.DailyLoss);
                config.EvaluationTradeTargetDollars = Number(evalTradeTargetBox, config.TargetDollars);
                config.EvaluationTradeStopDollars = Number(evalTradeStopBox, config.StopDollars);
            }
            else
            {
                config.EvaluationDailyCreditCap = config.DailyGoal;
                config.EvaluationDailyLoss = config.DailyLoss;
                config.EvaluationTradeTargetDollars = config.TargetDollars;
                config.EvaluationTradeStopDollars = config.StopDollars;
            }
            config.FundedDailyLoss = NumberAllowZero(fundedDailyLossBox, 0); config.FundedFailure = Number(fundedFailureBox, 2000); config.PayoutDaysRequired = Integer(payoutDaysBox, 5); config.MinimumPositiveDays = Integer(minimumDaysBox, 2); config.MinimumQualifyingDayProfit = NumberAllowZero(minimumQualifyingDayBox, 150); config.EvaluationCost = Number(evalCostBox, 120); config.ReplacementsRequirePayoutFunding = replacementFundingGateBox != null && replacementFundingGateBox.IsChecked == true ? 1 : 0;
            config.FirmFundedCapEnabled = config.EvaluationEnabled > 0 && firmFundedCapBox != null && firmFundedCapBox.IsChecked == true ? 1 : 0;
            config.EvaluationSlotsPerFirm = Math.Max(1, Integer(firmEvalSlotsBox, 10));
            config.MaxFundedPerFirm = Math.Max(1, Integer(firmMaxFundedBox, 5));
            if (config.MinimumPositiveDays <= 0 || config.PayoutDaysRequired <= 0 || config.MinimumQualifyingDayProfit < 0 || config.EvaluationDailyLoss < 0 || config.FundedDailyLoss < 0 || config.FundedFailure <= 0 || config.PayoutProfitSharePercent <= 0 || config.PayoutProfitSharePercent > 100 || (config.EvaluationStageTradeRulesEnabled > 0 && (config.EvaluationTradeTargetDollars <= 0 || config.EvaluationTradeStopDollars <= 0)) || (config.FirmFundedCapEnabled > 0 && (config.EvaluationSlotsPerFirm <= 0 || config.MaxFundedPerFirm <= 0 || config.MaxFundedPerFirm > config.EvaluationSlotsPerFirm))) { UpdateUi("LIFECYCLE ERROR • DAYS, STAGE TERMS, ACCOUNT SHARE, AND FIRM CAP VALUES MUST BE VALID", Red); return false; }
            start = requestStart; end = requestEnd;
            RefreshInstrumentSourceText();
            return true;
        }

        private void RenderEvents()
        {
            bool asianBacktest = config != null && string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            int eligible = events.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            int flagged = events.Count(x => string.Equals(x.ReviewState, "FLAGGED", StringComparison.OrdinalIgnoreCase));
            int excluded = events.Count(x => !string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            string byInstrument = string.Join(" • ", new[] { "MNQ", "MGC" }.Where(symbol => config.Scope == "BOTH" || config.Scope == symbol).Select(symbol => symbol + " " + events.Count(e => e.Symbol == symbol) + (asianBacktest ? " cycle legs / " : " setups / ") + (symbol == "MNQ" ? mnqSetupBars.Count : mgcSetupBars.Count) + " " + config.SetupMinutes + "M bars"));
            if (summaryText != null) summaryText.Text = (asianBacktest ? "ASIAN CYCLE BACKTEST " : (config.OutcomeModelEnabled == 1 ? "RESOLVED SETUPS " : "SETUP-ONLY LEDGER ")) + events.Count + " • " + byInstrument + " • " + (asianBacktest ? "COPY-ACCOUNT ELIGIBLE " : "ELIGIBLE FOR POOL ") + eligible + " • EXCLUDED / FLAGGED " + excluded + " (FLAGGED " + flagged + ") • " + (config.OutcomeModelEnabled == 1 ? "OUTCOMES VERIFIED" : "OUTCOME / P&L MATH BLOCKED");
            if (resultScopeText != null) resultScopeText.Text = BuildStudyScopeLabel();
            if (setupStatsText != null) setupStatsText.Text = BuildRawSetupStats(events, config);
            string mode = config.OutcomeModelEnabled == 1 ? (asianBacktest ? "RESOLVED ASIAN CYCLE LEDGER" : "RESOLVED OUTCOME LEDGER") : "SETUP-ONLY LEDGER • MATCHING 1M DATA NOT YET PROVEN; DO NOT READ WIN/LOSS OR P/L";
            if (eventText != null)
            {
                if (asianBacktest && events.Count == 0)
                {
                    List<KeystoneArcBar> direct = mnqBars.Concat(mgcBars).OrderBy(x => x.Time).ThenBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).ToList();
                    eventText.Text = mode + " • NO CYCLE LEGS WERE CREATED. This is not a setup-search result.\n\n" + KeystoneArcEngine.AsianCycleReadinessReceipt(direct, config);
                }
                else
                {
                    eventText.Text = mode + (asianBacktest ? " • EACH COMPLETED DAILY CYCLE IS COPIED TO ACTIVE VIRTUAL ACCOUNTS AFTER STEP 3" : " • POOL DECISION CONTROLS FUTURE VIRTUAL ROTATION WHEN OUTCOME VALIDATION IS AVAILABLE") + "\n\n" + string.Join("\n", events.Take(250).Select(e => e.TriggerTime.ToString("yyyy-MM-dd HH:mm") + " | " + e.Symbol + " | " + e.Direction + " " + e.SetupClass + " " + e.StrengthTag + " | ENTRY " + e.Entry.ToString("0.00") + " | OUTCOME " + e.Outcome + (config.OutcomeModelEnabled == 1 ? " " + e.GrossPnl.ToString("C0") : string.Empty) + " | " + PoolDecisionLabel(e) + (asianBacktest && !string.IsNullOrWhiteSpace(e.ReviewNote) ? " | " + e.ReviewNote : string.Empty))) + (events.Count > 250 ? "\n\n... display limited to first 250 rows; the full ledger is available in the browser report." : string.Empty);
                }
            }
        }

        private static string BuildRawSetupStats(List<KeystoneArcEvent> rows, KeystoneArcRunConfig cfg)
        {
            bool asian = cfg != null && string.Equals(cfg.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            if (rows == null || rows.Count == 0) return asian ? "ASIAN CYCLE TOTALS: 0 resolved legs • no opening price was invented; read the exact-opening diagnostic above." : "RAW SETUP TOTALS: 0.";
            int wins = rows.Count(x => x.Outcome == "WIN"), losses = rows.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase)), exits = rows.Count(x => x.Outcome == "SESSION EXIT");
            int resolved = wins + losses;
            string byInstrument = string.Join(" • ", rows.GroupBy(x => x.Symbol).OrderBy(x => x.Key).Select(g => g.Key + " " + g.Count() + (asian ? " legs" : " setups")));
            string byType = string.Join(" • ", rows.GroupBy(x => x.SetupClass).OrderBy(x => x.Key).Select(g => g.Key + " " + g.Count()));
            bool blocked = rows.Any(x => x.Outcome == "UNVERIFIED 1M");
            if (asian)
            {
                int cycles = rows.Select(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, cfg)).Distinct().Count();
                return "ASIAN CYCLE TOTALS (before copy accounts): " + cycles + " completed daily cycle(s) • " + rows.Count + " resolved legs • " + byInstrument + " • WINS " + wins + " • LOSSES " + losses + " • CYCLE EXITS " + exits + " • NET " + rows.Sum(x => x.GrossPnl).ToString("C0") + " • each cycle is copied once to every active account in Step 3.";
            }
            return "RAW SETUP TOTALS (before accounts/rotation): " + rows.Count + " • " + byInstrument + " • " + byType + (blocked ? " • SETUP-ONLY: the 1M series did not match every selected setup bar, so win/loss, P/L, drawdown, and pool math are intentionally unavailable." : " • WINS " + wins + " • LOSSES " + losses + " • WIN % " + (resolved == 0 ? "n/a" : (wins * 100.0 / resolved).ToString("0.0") + "%") + " • SESSION EXITS " + exits);
        }

        private void BuildMathReaderAsync()
        {
            if (operationBusy || isProcessing) { UpdateUi("WAIT FOR THE CURRENT OPERATION TO FINISH", Gold); return; }
            if (config.AccountPath == "PERSONAL" && HasFinalLiveAccountLedger())
            {
                RenderPoolDashboard();
                UpdateUi("LIVE ACCOUNT SUMMARY REFRESHED FROM THE FINAL ONE-ACCOUNT LEDGER", Green);
                return;
            }
            if (config.OutcomeModelEnabled == 0) { if (mathText != null) mathText.Text = "OUTCOME MATH BLOCKED\n\nThe 1-minute outcome series did not reproduce the selected setup chart's OHLC bars. Raw setup counts and chart review remain available; win/loss, P/L, and virtual-pool math are disabled until the data series match."; UpdateUi("MATH BLOCKED • OUTCOME DATA SERIES MISMATCH", Gold); return; }
            if (events == null || events.Count == 0) { BuildMathReader(); return; }
            List<KeystoneArcEvent> accepted = CloneEvents(events.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)));
            if (accepted.Count == 0) { BuildMathReader(); return; }
            KeystoneArcRunConfig workerConfig = CloneConfig(config);
            int total = events.Count; int pending = 0; int flagged = events.Count(x => x.ReviewState == "FLAGGED"); int rejected = events.Count(x => x.ReviewState == "REJECTED");
            BeginBusy("CALCULATING ELIGIBLE-SETUP MATH");
            UpdateUi("CALCULATING ELIGIBLE-SETUP MATH IN BACKGROUND • " + accepted.Count + " ELIGIBLE SETUPS", Gold);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string text;
                try { text = BuildMathText(accepted, workerConfig, total, pending, flagged, rejected); }
                catch (Exception ex) { text = "MATH ERROR • " + ex.Message; }
                DispatchToLab(delegate { if (mathText != null) mathText.Text = text; EndBusy(); UpdateUi("MATH READER READY • ELIGIBLE DETECTED SETUPS", Green); });
            });
        }

        private void BuildMathReader()
        {
            if (mathText == null) return;
            if (config.AccountPath == "PERSONAL" && HasFinalLiveAccountLedger())
            {
                RenderPoolDashboard();
                return;
            }
            if (events == null || events.Count == 0) { mathText.Text = "WAITING FOR 2. DETECT EVENTS\n\nChoose a date range in RESEARCH SETUP, request NinjaTrader data, then build the resolved setup ledger."; return; }
            int pending = 0;
            int accepted = events.Count(x => x.ReviewState == "ACCEPTED");
            int flagged = events.Count(x => x.ReviewState == "FLAGGED");
            int rejected = events.Count(x => x.ReviewState == "REJECTED");
            if (config.OutcomeModelEnabled == 0)
            {
                mathText.Text = "OUTCOME MATH BLOCKED\n\nSESSION DATE(S) " + SelectedTestDateLabel() + " | RAW SETUPS " + events.Count + " | ELIGIBLE " + accepted + "\n\nThe 1-minute series did not exactly reproduce the selected setup chart's OHLC bars. Setup detection and chart evidence remain available, but win/loss, P/L, virtual accounts, evaluation, and payout calculations are intentionally disabled.";
                return;
            }
            if (accepted > 0)
            {
                mathText.Text = "ELIGIBLE SETUPS READY\n\nSESSION DATE(S) " + SelectedTestDateLabel() + " | " + config.SessionMode + " | ELIGIBLE " + accepted + " | EXCLUDED " + (pending + flagged + rejected) + "\n\nClick 5A • REFRESH MATH to build the selected-range comparison, or click 5B • RUN SELECTED POOL to assign every eligible setup across the chosen virtual accounts.";
                return;
            }
            mathText.Text = "KEYSTONE ARC • SELECTED-RANGE MATH READER\n\nSESSION DATE(S) " + SelectedTestDateLabel() + " | SCOPE " + config.Scope + " | SESSION " + config.SessionMode + " | SETUP " + config.SetupMinutes + "M\n\nNO ELIGIBLE SETUPS REMAIN. Go to Verify Entries and restore one or more excluded rows. Math and pool simulation use every eligible detected setup.";
        }

        private static string BuildMathText(List<KeystoneArcEvent> accepted, KeystoneArcRunConfig cfgBase, int total, int pending, int flagged, int rejected)
        {
            var sb = new StringBuilder();
            sb.AppendLine("KEYSTONE ARC • SELECTED-RANGE MATH READER");
            sb.AppendLine("SESSION DATE(S) " + SessionDateLabel(cfgBase) + " | SCOPE " + cfgBase.Scope + " | SESSION " + cfgBase.SessionMode + " | SETUP " + cfgBase.SetupMinutes + "M | QTY " + cfgBase.Quantity);
            sb.AppendLine("SETUP SOURCE: " + cfgBase.SetupMinutes + "M bars • MNQ OUTCOME SOURCE: " + cfgBase.MnqOutcomeSource + " • MGC OUTCOME SOURCE: " + cfgBase.MgcOutcomeSource + " • MODEL: stop-first within each available outcome bar • NO COMMISSION / SPREAD / SLIPPAGE");
            sb.AppendLine(); sb.AppendLine("VIRTUAL-POOL ELIGIBILITY");
            sb.AppendLine("ELIGIBLE " + accepted.Count + " | FLAGGED " + flagged + " | EXCLUDED " + rejected + " | TOTAL " + total);
            int wins = accepted.Count(x => x.Outcome == "WIN"); int losses = accepted.Count(x => x.Outcome.StartsWith("LOSS")); int exits = accepted.Count(x => x.Outcome == "SESSION EXIT");
            sb.AppendLine(); sb.AppendLine("ELIGIBLE-SETUP OUTCOME MODEL");
            int resolved = wins + losses;
            sb.AppendLine("Events: " + accepted.Count + " | Wins: " + wins + " | Losses: " + losses + " | Win %: " + (resolved == 0 ? "n/a" : (wins * 100.0 / resolved).ToString("0.0") + "%") + " | Session exits: " + exits + " | Gross: " + accepted.Sum(x => x.GrossPnl).ToString("C0"));
            sb.AppendLine(); sb.AppendLine("INSTRUMENT PERFORMANCE"); sb.AppendLine("INSTR | EVENTS | WINS | LOSSES | WIN % | GROSS");
            foreach (var group in accepted.GroupBy(x => x.Symbol).OrderBy(x => x.Key))
            {
                int gw = group.Count(x => x.Outcome == "WIN"), gl = group.Count(x => x.Outcome.StartsWith("LOSS")), gr = gw + gl;
                sb.AppendLine(group.Key.PadRight(5) + " | " + group.Count().ToString().PadLeft(6) + " | " + gw.ToString().PadLeft(4) + " | " + gl.ToString().PadLeft(6) + " | " + (gr == 0 ? " n/a" : (gw * 100.0 / gr).ToString("0.0").PadLeft(5) + "%") + " | " + group.Sum(x => x.GrossPnl).ToString("C0"));
            }
            sb.AppendLine(); sb.AppendLine("SETUP / INSTRUMENT BREAKDOWN"); sb.AppendLine("INSTR | SETUP | EVENTS | WINS | LOSSES | GROSS");
            foreach (var group in accepted.GroupBy(x => x.Symbol + "|" + x.SetupClass).OrderBy(x => x.Key))
            {
                string[] key = group.Key.Split('|'); sb.AppendLine(key[0].PadRight(5) + " | " + key[1].PadRight(5) + " | " + group.Count().ToString().PadLeft(6) + " | " + group.Count(x => x.Outcome == "WIN").ToString().PadLeft(4) + " | " + group.Count(x => x.Outcome.StartsWith("LOSS")).ToString().PadLeft(6) + " | " + group.Sum(x => x.GrossPnl).ToString("C0"));
            }
            sb.AppendLine(); sb.AppendLine("MONTHLY ELIGIBLE-SETUP RESULTS"); sb.AppendLine("MONTH   | EVENTS | WINS | LOSSES | GROSS");
            foreach (var group in accepted.GroupBy(x => x.TriggerTime.ToString("yyyy-MM")).OrderBy(x => x.Key)) sb.AppendLine(group.Key + " | " + group.Count().ToString().PadLeft(6) + " | " + group.Count(x => x.Outcome == "WIN").ToString().PadLeft(4) + " | " + group.Count(x => x.Outcome.StartsWith("LOSS")).ToString().PadLeft(6) + " | " + group.Sum(x => x.GrossPnl).ToString("C0"));
            bool asianCopy = string.Equals(cfgBase.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine(); sb.AppendLine(asianCopy ? "VIRTUAL COPY-TRADING COMPARISON • EACH ACTIVE ACCOUNT RECEIVES THE SAME RESOLVED ASIAN SESSION" : "VIRTUAL NEXT-FREE COMPARISON • ELIGIBLE SETUPS ONLY"); sb.AppendLine("POOL | ASSIGNED | SKIPPED | ASSIGNED LEDGER P/L | PAYOUT CASH* | EVAL COST* | PAYOUT CASH LESS COST*");
            foreach (int size in new[] { 1, 5, 10, 20, 40 })
            {
                var copy = CloneEvents(accepted); var cfg = CloneConfig(cfgBase); cfg.PoolSize = size; var virtualAccounts = KeystoneArcEngine.SimulatePool(copy, cfg);
                int assigned = copy.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)); int skipped = copy.Count(x => !string.IsNullOrWhiteSpace(x.SkipReason)); double assignedGross = copy.Where(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount)).Sum(x => x.GrossPnl); double payoutCash = virtualAccounts.Sum(x => x.PayoutCash); double evalCost = virtualAccounts.Sum(x => x.EvaluationCost);
                sb.AppendLine(size.ToString().PadLeft(4) + " | " + assigned.ToString().PadLeft(8) + " | " + skipped.ToString().PadLeft(7) + " | " + assignedGross.ToString("C0").PadLeft(14) + " | " + payoutCash.ToString("C0").PadLeft(13) + " | " + evalCost.ToString("C0").PadLeft(10) + " | " + (payoutCash - evalCost).ToString("C0"));
            }
            if (cfgBase.PoolSize > 1 && !asianCopy)
            {
                var rotationCfg = CloneConfig(cfgBase); rotationCfg.CopyTradingPool = 0;
                var copyCfg = CloneConfig(cfgBase); copyCfg.CopyTradingPool = 1;
                var rotationEvents = CloneEvents(accepted); var copyEvents = CloneEvents(accepted);
                var rotationAccounts = KeystoneArcEngine.SimulatePool(rotationEvents, rotationCfg);
                var copyAccounts = KeystoneArcEngine.SimulatePool(copyEvents, copyCfg);
                int cohortFailures = copyAccounts.Count == 0 ? 0 : copyAccounts.Max(a => a.FailedEvaluations + a.FailedFunded);
                sb.AppendLine(); sb.AppendLine("ROTATION VS COPY-POOL • SELECTED " + cfgBase.PoolSize + " ACCOUNT SCENARIO");
                sb.AppendLine("MODE | EVENT ASSIGNMENTS | PAYOUT CASH* | EVAL COST* | FULL NET CASH* | COHORT BLOWOUT CYCLES");
                sb.AppendLine("ROTATE | " + rotationEvents.Count(x => !string.IsNullOrEmpty(x.AssignedVirtualAccount)).ToString() + " | " + rotationAccounts.Sum(a => a.PayoutCash).ToString("C0") + " | " + rotationAccounts.Sum(a => a.EvaluationCost).ToString("C0") + " | " + (rotationAccounts.Sum(a => a.PayoutCash) - rotationAccounts.Sum(a => a.EvaluationCost)).ToString("C0") + " | n/a");
                sb.AppendLine("COPY ALL | " + copyEvents.Count(x => !string.IsNullOrEmpty(x.AssignedVirtualAccount)).ToString() + " shared events | " + copyAccounts.Sum(a => a.PayoutCash).ToString("C0") + " | " + copyAccounts.Sum(a => a.EvaluationCost).ToString("C0") + " | " + (copyAccounts.Sum(a => a.PayoutCash) - copyAccounts.Sum(a => a.EvaluationCost)).ToString("C0") + " | " + cohortFailures.ToString());
            }
            sb.AppendLine(); sb.AppendLine("*Payout cash less cost is not trading net P/L. It only compares modeled withdrawals with modeled evaluation purchases. Lifecycle lines are user-entered illustrative assumptions, not firm rules, eligibility, or earnings forecasts."); sb.AppendLine("NEXT: select one pool size and either ROTATE or COPY EVERY SETUP TO ALL ACTIVE to inspect the selected virtual-pool scenario."); return sb.ToString();
        }

        private static List<KeystoneArcEvent> CloneEvents(IEnumerable<KeystoneArcEvent> source)
        {
            return source.Select(x => new KeystoneArcEvent { Id = x.Id, Symbol = x.Symbol, SetupClass = x.SetupClass, Direction = x.Direction, StrengthTag = x.StrengthTag, ReferenceTime = x.ReferenceTime, TriggerTime = x.TriggerTime, EntryTime = x.EntryTime, Entry = x.Entry, Stop = x.Stop, Target = x.Target, Quantity = x.Quantity, StopDistance = x.StopDistance, RiskModel = x.RiskModel, ExitTime = x.ExitTime, ExitPrice = x.ExitPrice, Outcome = x.Outcome, GrossPnl = x.GrossPnl, EvaluationOutcome = x.EvaluationOutcome, EvaluationExitTime = x.EvaluationExitTime, EvaluationExitPrice = x.EvaluationExitPrice, EvaluationGrossPnl = x.EvaluationGrossPnl, EvaluationTarget = x.EvaluationTarget, EvaluationStop = x.EvaluationStop, PeakAfterEntry = x.PeakAfterEntry, TroughAfterEntry = x.TroughAfterEntry, TargetTouched = x.TargetTouched, StopTouched = x.StopTouched, SessionOrder = x.SessionOrder, FvgLower = x.FvgLower, FvgUpper = x.FvgUpper, FvgFormedTime = x.FvgFormedTime, ConfigurationKey = x.ConfigurationKey, ReviewState = x.ReviewState, ReviewNote = x.ReviewNote }).ToList();
        }

        private static KeystoneArcRunConfig CloneConfig(KeystoneArcRunConfig source)
        {
            return new KeystoneArcRunConfig { MnqName = source.MnqName, MgcName = source.MgcName, Scope = source.Scope, AccountPath = source.AccountPath, Start = source.Start, End = source.End, OneDayMode = source.OneDayMode, SetupMinutes = source.SetupMinutes, StrategyCode = source.StrategyCode, EnableBh = source.EnableBh, EnableFvg = source.EnableFvg, DirectionMode = source.DirectionMode, BhAggressionFilter = source.BhAggressionFilter, MnqStrongRedCandles = source.MnqStrongRedCandles, MnqStrongDeclinePoints = source.MnqStrongDeclinePoints, MgcStrongRedCandles = source.MgcStrongRedCandles, MgcStrongDeclineDollars = source.MgcStrongDeclineDollars, BhStrongCombine = source.BhStrongCombine, SessionMode = source.SessionMode, CustomStart = source.CustomStart, MnqStart = source.MnqStart, MgcStart = source.MgcStart, EndTime = source.EndTime, Quantity = source.Quantity, TargetDollars = source.TargetDollars, StopDollars = source.StopDollars, StopFirstOnSameMinute = source.StopFirstOnSameMinute, StopMode = source.StopMode, MnqStopOffsetPoints = source.MnqStopOffsetPoints, MgcStopOffsetPoints = source.MgcStopOffsetPoints, PersonalLotSize = source.PersonalLotSize, MnqCashPerPointPerLot = source.MnqCashPerPointPerLot, MgcCashPerPointPerLot = source.MgcCashPerPointPerLot, MnqTargetMove = source.MnqTargetMove, MgcTargetMove = source.MgcTargetMove, MnqStandardStopMove = source.MnqStandardStopMove, MgcStandardStopMove = source.MgcStandardStopMove, PersonalMaxRiskDollars = source.PersonalMaxRiskDollars, BreakEvenEnabled = source.BreakEvenEnabled, BreakEvenTriggerMove = source.BreakEvenTriggerMove, OutcomeModelEnabled = source.OutcomeModelEnabled, MnqOutcomeTimeOffsetMinutes = source.MnqOutcomeTimeOffsetMinutes, MgcOutcomeTimeOffsetMinutes = source.MgcOutcomeTimeOffsetMinutes, MnqOutcomeSource = source.MnqOutcomeSource, MgcOutcomeSource = source.MgcOutcomeSource, PoolSize = source.PoolSize, CopyTradingPool = source.CopyTradingPool, AllowMultipleSetupsPerDay = source.AllowMultipleSetupsPerDay, DailyGoal = source.DailyGoal, DailyLoss = source.DailyLoss, EvaluationEnabled = source.EvaluationEnabled, EvaluationTarget = source.EvaluationTarget, EvaluationDailyCreditCap = source.EvaluationDailyCreditCap, EvaluationConsistencyPercent = source.EvaluationConsistencyPercent, EvaluationFailure = source.EvaluationFailure, EvaluationDailyLoss = source.EvaluationDailyLoss, EvaluationStageTradeRulesEnabled = source.EvaluationStageTradeRulesEnabled, EvaluationTradeTargetDollars = source.EvaluationTradeTargetDollars, EvaluationTradeStopDollars = source.EvaluationTradeStopDollars, FundedDailyLoss = source.FundedDailyLoss, FundedFailure = source.FundedFailure, MinimumPositiveDays = source.MinimumPositiveDays, MinimumQualifyingDayProfit = source.MinimumQualifyingDayProfit, PayoutThreshold = source.PayoutThreshold, PayoutDaysRequired = source.PayoutDaysRequired, PayoutAmount = source.PayoutAmount, PayoutProfitSharePercent = source.PayoutProfitSharePercent, EvaluationCost = source.EvaluationCost, ReplacementsRequirePayoutFunding = source.ReplacementsRequirePayoutFunding, FirmFundedCapEnabled = source.FirmFundedCapEnabled, EvaluationSlotsPerFirm = source.EvaluationSlotsPerFirm, MaxFundedPerFirm = source.MaxFundedPerFirm, PropStartingBalance = source.PropStartingBalance, PersonalStartingBalance = source.PersonalStartingBalance, AsianStartHhmm = source.AsianStartHhmm, AsianEndHhmm = source.AsianEndHhmm, AsianMnqInitialDirection = source.AsianMnqInitialDirection, AsianMgcInitialDirection = source.AsianMgcInitialDirection, AsianRiskMode = source.AsianRiskMode, AsianReversalLossDollars = source.AsianReversalLossDollars, AsianMnqReversalPriceMove = source.AsianMnqReversalPriceMove, AsianMgcReversalPriceMove = source.AsianMgcReversalPriceMove, AsianCycleTargetDollars = source.AsianCycleTargetDollars, AsianCombinedStopLossDollars = source.AsianCombinedStopLossDollars, AsianDailyLossLimitDollars = source.AsianDailyLossLimitDollars, AsianInstrumentStopLossDollars = source.AsianInstrumentStopLossDollars, AsianMnqInstrumentStopLossDollars = source.AsianMnqInstrumentStopLossDollars, AsianMgcInstrumentStopLossDollars = source.AsianMgcInstrumentStopLossDollars, AsianBreakEvenTriggerDollars = source.AsianBreakEvenTriggerDollars, AsianStartingQuantity = source.AsianStartingQuantity, AsianMaxReversalsPerInstrument = source.AsianMaxReversalsPerInstrument, AsianMnqMaxReversals = source.AsianMnqMaxReversals, AsianMgcMaxReversals = source.AsianMgcMaxReversals, AsianMaxTotalLegsPerInstrument = source.AsianMaxTotalLegsPerInstrument };
        }

        private void SaveSnapshot()
        {
            if (events.Count == 0) { UpdateUi("NOTHING TO SAVE", Red); return; }
            string dir = DataDirectory(); Directory.CreateDirectory(dir); string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); string file = Path.Combine(dir, "run_" + stamp + ".txt");
            var sb = new StringBuilder(); sb.AppendLine("KEYSTONE ARC RESEARCH RUN"); sb.AppendLine("CREATED=" + DateTime.Now.ToString("o")); sb.AppendLine("TESTED_RANGE=" + BuildReportRangeSummary().Replace("\n", " | ")); sb.AppendLine("DATA_RECEIPT=" + historicalDataReceipt); sb.AppendLine("RESOLVED_SETUPS=" + events.Count); sb.AppendLine("ELIGIBLE_SETUPS=" + events.Count(x => x.ReviewState == "ACCEPTED")); sb.AppendLine("FLAGGED=" + events.Count(x => x.ReviewState == "FLAGGED")); sb.AppendLine("EXCLUDED=" + events.Count(x => x.ReviewState == "REJECTED")); sb.AppendLine("ELIGIBLE_GROSS=" + events.Where(x => x.ReviewState == "ACCEPTED").Sum(x => x.GrossPnl).ToString(CultureInfo.InvariantCulture)); sb.AppendLine("POOL_ACCOUNTS=" + accounts.Count); sb.AppendLine("POOL_GROSS=" + accounts.Sum(x => x.TotalPnl).ToString(CultureInfo.InvariantCulture)); sb.AppendLine(CapitalPolicySummaryText()); sb.AppendLine("COMPARISON_SCENARIOS=" + comparisonRows.Count); sb.AppendLine("PAYOUT_CASH_ILLUSTRATIVE=" + accounts.Sum(x => x.PayoutCash).ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(file, sb.ToString()); unsavedResearch = false; RefreshSavedRuns(); if (workspaceTabs != null) workspaceTabs.SelectedIndex = 3; UpdateUi("SAVED SNAPSHOT • OPENED 4. SAVED RUNS • " + Path.GetFileName(file), Green);
        }

        private void ExportCsv()
        {
            if (events.Count == 0) { UpdateUi("RUN DETECTION BEFORE EXPORT", Red); return; }
            string dir = DataDirectory(); Directory.CreateDirectory(dir); string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.WriteAllText(Path.Combine(dir, "events_" + stamp + ".csv"), EventCsv()); File.WriteAllText(Path.Combine(dir, "accounts_" + stamp + ".csv"), AccountCsv()); File.WriteAllText(Path.Combine(dir, "account_days_" + stamp + ".csv"), AccountDayCsv()); File.WriteAllText(Path.Combine(dir, "first_returns_" + stamp + ".csv"), FirstReturnCsv()); File.WriteAllText(Path.Combine(dir, "payout_accounts_" + stamp + ".csv"), PayoutAccountCsv()); File.WriteAllText(Path.Combine(dir, "daily_selected_session_" + stamp + ".csv"), DailySessionScoreCsv()); File.WriteAllText(Path.Combine(dir, "research_findings_" + stamp + ".csv"), ResearchFindingsCsv()); File.WriteAllText(Path.Combine(dir, "payout_cycles_" + stamp + ".csv"), PayoutCycleCsv()); File.WriteAllText(Path.Combine(dir, "balance_matrix_" + stamp + ".csv"), BalanceMatrixCsv()); File.WriteAllText(Path.Combine(dir, "capital_policy_" + stamp + ".csv"), CapitalPolicyCsv()); File.WriteAllText(Path.Combine(dir, "comparison_" + stamp + ".csv"), ComparisonCsv()); File.WriteAllText(Path.Combine(dir, "summary_" + stamp + ".txt"), "TESTED_RANGE=" + BuildReportRangeSummary().Replace("\n", " | ") + "\nDATA_RECEIPT=" + historicalDataReceipt + "\nRESOLVED_OUTCOMES=" + events.Count + "\nELIGIBLE_FOR_POOL=" + events.Count(x => x.ReviewState == "ACCEPTED") + "\nEXCLUDED_OR_FLAGGED=" + events.Count(x => x.ReviewState != "ACCEPTED") + "\nPOOL=" + accounts.Count + "\nPOOL_GROSS=" + accounts.Sum(x => x.TotalPnl).ToString(CultureInfo.InvariantCulture) + "\nPAYOUT_DATES=" + KeystoneArcEngine.BuildPayoutCycleRows(accounts, config).Count + "\n" + CapitalPolicySummaryText().Replace("\n", "\n") + "\nCOMPARISON_SCENARIOS=" + comparisonRows.Count);
            unsavedResearch = false; RefreshSavedRuns(); if (workspaceTabs != null) workspaceTabs.SelectedIndex = 3; UpdateUi("EXPORTED RESEARCH FINDINGS + FIRST-RETURN + PERIOD + ACCOUNT + PAYOUT + BALANCE LEDGERS • OPENED 4. SAVED RUNS • " + dir, Green);
        }

        private void ExportHtmlReport()
        {
            if (events.Count == 0) { UpdateUi("RUN DETECTION BEFORE HTML REPORT EXPORT", Red); return; }
            string dir = DataDirectory(); Directory.CreateDirectory(dir); string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string file = Path.Combine(dir, "KeystoneArc_Report_" + stamp + ".html");
            File.WriteAllText(file, BuildHtmlReport(), Encoding.UTF8);
            unsavedResearch = false; RefreshSavedRuns(); if (workspaceTabs != null) workspaceTabs.SelectedIndex = 3; UpdateUi("EXPORTED PROFESSIONAL HTML REPORT • OPENED 4. SAVED RUNS • " + Path.GetFileName(file), Green);
        }

        // Exports browser-openable SVG evidence charts instead of opaque screenshot pixels.  The files retain
        // the exact requested bars, entry prices, outcomes, and labels, and remain sharp when zoomed or printed.
        private void ExportEvidencePackage()
        {
            if (events.Count == 0) { UpdateUi("RUN DETECTION BEFORE EVIDENCE EXPORT", Red); return; }
            if (operationBusy || isProcessing) { UpdateUi("WAIT FOR THE ACTIVE OPERATION BEFORE EVIDENCE EXPORT", Gold); return; }
            List<KeystoneArcEvent> eventCopy = CloneEvents(events);
            List<KeystoneArcBar> mnqCopy = new List<KeystoneArcBar>(mnqSetupBars);
            List<KeystoneArcBar> mgcCopy = new List<KeystoneArcBar>(mgcSetupBars);
            KeystoneArcRunConfig cfgCopy = CloneConfig(config);
            string rangeName = SessionDateLabel(cfgCopy).Replace(" → ", "_to_").Replace(" ", "_");
            string root = Path.Combine(DataDirectory(), "KeystoneEvidence_" + rangeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            BeginBusy("EXPORTING EVIDENCE PACKAGE");
            UpdateUi("EXPORTING INSTRUMENT FOLDERS, SESSION CHARTS, AND HTML LEDGERS", Gold);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try
                {
                    Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "events.csv"), EventCsv(eventCopy), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "account_snapshot.csv"), AccountCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "account_days.csv"), AccountDayCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "first_returns.csv"), FirstReturnCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "payout_accounts.csv"), PayoutAccountCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "daily_selected_session.csv"), DailySessionScoreCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "research_findings.csv"), ResearchFindingsCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "payout_cycles.csv"), PayoutCycleCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "balance_matrix.csv"), BalanceMatrixCsv(), Encoding.UTF8);
                    File.WriteAllText(Path.Combine(root, "capital_policy.csv"), CapitalPolicyCsv(), Encoding.UTF8);
                    var manifest = new StringBuilder();
                    manifest.Append("<h2>Evidence charts and raw ledgers</h2><div class='card'><strong>One package, one starting page.</strong> The links below open each instrument's SVG session charts and CSV ledger. Charts are generated from the selected direct NinjaTrader setup bars; they are a research record for visual validation, not proof that the detector or outcome model is correct.</div><p><a href='events.csv'>Complete event ledger CSV</a> • <a href='research_findings.csv'>Evidence-only research findings CSV</a> • <a href='first_returns.csv'>First-return dashboard CSV</a> • <a href='payout_accounts.csv'>Payout-account profitability CSV</a> • <a href='daily_selected_session.csv'>Daily selected-session scoreboard CSV</a> • <a href='account_snapshot.csv'>Account snapshot CSV</a> • <a href='account_days.csv'>Daily lifecycle CSV</a> • <a href='payout_cycles.csv'>Payout-cycle dashboard CSV</a> • <a href='balance_matrix.csv'>Balance matrix CSV</a> • <a href='capital_policy.csv'>Initial-investment and replacement policy CSV</a></p><ul>");
                    int written = 0;
                    written += ExportInstrumentEvidence(root, "MNQ", mnqCopy, eventCopy, cfgCopy, manifest);
                    written += ExportInstrumentEvidence(root, "MGC", mgcCopy, eventCopy, cfgCopy, manifest);
                    manifest.Append("</ul><p class='footer'>Charts written: ").Append(written).Append(". This page contains the run summary, account/payout scenario data, filters, comparisons, and detailed ledger; MNQ/MGC folders contain linked visual charts and machine-readable rows.</p>");
                    File.WriteAllText(Path.Combine(root, "index.html"), BuildEvidencePackageIndex(manifest.ToString()), Encoding.UTF8);
                    result = "EVIDENCE PACKAGE READY • OPEN index.html • " + written + " SVG SESSION CHART(S) • ONE MAIN WEBSITE + MNQ/MGC ASSET FOLDERS • " + root;
                }
                catch (Exception ex) { result = "EVIDENCE EXPORT ERROR • " + ex.Message; }
                DispatchToLab(delegate
                {
                    EndBusy();
                    if (result.StartsWith("EVIDENCE PACKAGE READY")) { unsavedResearch = false; RefreshSavedRuns(); UpdateUi(result + " • RUN ARCHIVE UPDATED", Green); }
                    else UpdateUi(result, Red);
                });
            });
        }

        private string BuildEvidencePackageIndex(string evidenceSection)
        {
            string report = BuildHtmlReport();
            int anchor = report.LastIndexOf("<p class='footer'>", StringComparison.OrdinalIgnoreCase);
            if (anchor < 0) anchor = report.LastIndexOf("</div><script>", StringComparison.OrdinalIgnoreCase);
            if (anchor < 0) return report + (evidenceSection ?? string.Empty);
            return report.Insert(anchor, evidenceSection ?? string.Empty);
        }

        private int ExportInstrumentEvidence(string root, string symbol, List<KeystoneArcBar> sourceBars, List<KeystoneArcEvent> sourceEvents, KeystoneArcRunConfig cfg, StringBuilder manifest)
        {
            List<KeystoneArcEvent> rows = sourceEvents.Where(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.TriggerTime).ToList();
            string folder = Path.Combine(root, symbol); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, symbol + "_events.csv"), EventCsv(rows), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, symbol + "_summary.html"), BuildInstrumentEvidenceSummaryHtml(symbol, rows, cfg), Encoding.UTF8);
            var index = new StringBuilder(); index.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(symbol).Append(" evidence</title><style>body{background:#09111f;color:#edf3ff;font:15px Segoe UI,Arial;margin:32px}a{color:#4bd9ff}li{margin:8px}.notice{color:#f6c64e}</style></head><body><h1>").Append(symbol).Append(" Evidence Charts</h1><p><a href='").Append(symbol).Append("_summary.html'>Open ").Append(symbol).Append(" summary and data explanation</a> • <a href='").Append(symbol).Append("_events.csv'>Open full event ledger CSV</a></p><p class='notice'>Each numbered SVG is the exact selected setup-bar chart for one tested session. Elevated badges and dashed leaders point to the exact entry-price line without covering candle bodies or wicks.</p><ol>");
            int number = 0;
            foreach (IGrouping<DateTime, KeystoneArcEvent> group in rows.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, cfg)).OrderBy(x => x.Key))
            {
                DateTime sessionStart = ConfiguredSessionStartFor(group.Key, cfg), sessionEnd = TradingSessionEndFor(group.Key, cfg);
                List<KeystoneArcBar> bars = sourceBars.Where(x => x.Time >= sessionStart && x.Time <= sessionEnd).OrderBy(x => x.Time).ToList();
                if (bars.Count == 0) continue;
                number++;
                string name = number.ToString("D3") + "_" + group.Key.ToString("yyyy-MM-dd") + "_" + symbol + "_setup_evidence.svg";
                File.WriteAllText(Path.Combine(folder, name), BuildEvidenceSvg(symbol, group.Key, bars, group.OrderBy(x => x.TriggerTime).ToList(), cfg), Encoding.UTF8);
                index.Append("<li><a href='").Append(Html(name)).Append("'>").Append(Html(name)).Append("</a> • ").Append(group.Count()).Append(" detected setup(s)</li>");
            }
            if (number == 0) index.Append("<li>No detected setups for this instrument in the selected test range.</li>");
            index.Append("</ol></body></html>"); File.WriteAllText(Path.Combine(folder, "index.html"), index.ToString(), Encoding.UTF8);
            manifest.Append("<li><a href='").Append(symbol).Append("/index.html'>").Append(symbol).Append(" evidence folder</a> • ").Append(number).Append(" session chart(s) • <a href='").Append(symbol).Append("/").Append(symbol).Append("_summary.html'>summary</a></li>");
            return number;
        }

        private string BuildInstrumentEvidenceSummaryHtml(string symbol, List<KeystoneArcEvent> rows, KeystoneArcRunConfig cfg)
        {
            bool outcomesVerified = cfg != null && cfg.OutcomeModelEnabled == 1;
            bool asian = cfg != null && string.Equals(cfg.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            string itemLabel = asian ? "REVERSAL LEGS" : "DETECTED SETUPS";
            int wins = rows.Count(x => x.Outcome == "WIN"), losses = rows.Count(x => x.Outcome.StartsWith("LOSS")), exits = rows.Count(x => x.Outcome == "SESSION EXIT"), noEntry = rows.Count(x => x.Outcome == "NO ENTRY DATA");
            double gross = rows.Sum(x => x.GrossPnl);
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>").Append(symbol).Append(" Keystone Evidence Summary</title><style>body{background:#09111f;color:#edf3ff;font:15px Segoe UI,Arial;margin:32px;line-height:1.45}.card{background:#142039;border:1px solid #2cd7cb;border-radius:10px;padding:16px;margin:14px 0}.grid{display:flex;flex-wrap:wrap;gap:10px}.metric{min-width:150px;background:#111b2d;border:1px solid #2f4669;border-radius:8px;padding:12px}.label{color:#9baec6;font-size:11px;font-weight:700}.value{font-size:22px;font-weight:700;color:#edf3ff}.violet{color:#e563ff}.amber{color:#ffbe3b}.ice{color:#9ee0ff}.cyan{color:#39d9ff}table{border-collapse:collapse;width:100%;font-size:12px}th,td{padding:8px;border-bottom:1px solid #2f4669;text-align:left}th{color:#39d9ff}.note{color:#f6c64e}</style></head><body>");
            sb.Append("<h1>").Append(symbol).Append(" • Keystone Arc Evidence Summary</h1><div class='card'><strong>Tested range:</strong> ").Append(Html(SessionDateLabel(cfg))).Append("<br><strong>Setup timeframe:</strong> ").Append(cfg.SetupMinutes).Append(" minutes<br><strong>Session:</strong> ").Append(Html(cfg.SessionMode)).Append("<br><strong>Outcome boundary:</strong> historical model only; use the chart SVGs to validate setup placement before relying on any outcome aggregation.</div>");
            sb.Append("<div class='grid'><div class='metric'><div class='label'>").Append(itemLabel).Append("</div><div class='value cyan'>").Append(rows.Count).Append("</div></div><div class='metric'><div class='label'>OUTCOME STATUS</div><div class='value ").Append(outcomesVerified ? "violet'>VERIFIED 1M" : "amber'>NOT AVAILABLE").Append("</div></div>");
            if (outcomesVerified) sb.Append("<div class='metric'><div class='label'>WINS</div><div class='value violet'>").Append(wins).Append("</div></div><div class='metric'><div class='label'>LOSSES</div><div class='value amber'>").Append(losses).Append("</div></div><div class='metric'><div class='label'>SESSION EXITS</div><div class='value ice'>").Append(exits).Append("</div></div><div class='metric'><div class='label'>MODEL GROSS</div><div class='value'>").Append(Html(gross.ToString("C0"))).Append("</div></div>");
            sb.Append("</div>");
            sb.Append("<p class='note'>").Append(outcomesVerified ? "No-entry rows: " + noEntry + ". The CSV contains the complete resolved model ledger and pool decision." : "The matching 1-minute series was not proven against the selected setup bars. This package intentionally contains setup placements and no win/loss, exit, P/L, pool, balance, or payout claim.").Append("</p>");
            sb.Append(outcomesVerified ? "<h2>Session chart index</h2><table><thead><tr><th>Session date</th><th>" + (asian ? "Reversal legs" : "Setups") + "</th><th>Wins</th><th>Losses</th><th>Session exits</th><th>Model P/L</th></tr></thead><tbody>" : "<h2>Session chart index • setup placement only</h2><table><thead><tr><th>Session date</th><th>" + (asian ? "Reversal legs" : "Setups") + "</th><th>Outcome status</th></tr></thead><tbody>");
            foreach (var g in rows.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, cfg)).OrderBy(x => x.Key))
            {
                if (outcomesVerified) sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count()).Append("</td><td>").Append(g.Count(x => x.Outcome == "WIN")).Append("</td><td>").Append(g.Count(x => x.Outcome.StartsWith("LOSS"))).Append("</td><td>").Append(g.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td>").Append(Html(g.Sum(x => x.GrossPnl).ToString("C0"))).Append("</td></tr>");
                else sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count()).Append("</td><td class='amber'>UNVERIFIED 1M</td></tr>");
            }
            sb.Append("</tbody></table></body></html>"); return sb.ToString();
        }

        private static DateTime ConfiguredSessionStartFor(DateTime date, KeystoneArcRunConfig cfg)
        {
            DateTime start, end;
            GetConfiguredSessionBounds(date.Date, date.Date, cfg, out start, out end);
            return start;
        }

        private static DateTime TradingSessionEndFor(DateTime date, KeystoneArcRunConfig cfg)
        {
            DateTime start, end;
            GetConfiguredSessionBounds(date.Date, date.Date, cfg, out start, out end);
            return end;
        }

        private static string BuildEvidenceSvg(string symbol, DateTime day, List<KeystoneArcBar> bars, List<KeystoneArcEvent> marks, KeystoneArcRunConfig cfg)
        {
            const double height = 900, left = 85, top = 132, bottom = 70, right = 24;
            double candle = cfg.SetupMinutes <= 1 ? 8 : 18, width = Math.Max(1500, left + right + bars.Count * candle);
            double min = bars.Min(x => x.Low), max = bars.Max(x => x.High); foreach (KeystoneArcEvent e in marks) { min = Math.Min(min, e.Entry); max = Math.Max(max, e.Entry); }
            double pad = Math.Max((max - min) * .09, symbol == "MGC" ? 1 : 8); min -= pad; max += pad;
            Func<double, double> y = p => top + (max - p) / Math.Max(.0000001, max - min) * (height - top - bottom);
            bool asian = cfg != null && string.Equals(cfg.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            var sb = new StringBuilder(); sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='").Append(width.ToString("0", CultureInfo.InvariantCulture)).Append("' height='900' viewBox='0 0 ").Append(width.ToString("0", CultureInfo.InvariantCulture)).Append(" 900'><rect width='100%' height='100%' fill='#0a0f1a'/>");
            sb.Append("<text x='").Append(left).Append("' y='24' fill='#52e0d5' font-family='Segoe UI,Arial' font-size='18' font-weight='700'>").Append(Html(symbol)).Append(" • SESSION ").Append(day.ToString("yyyy-MM-dd")).Append(" • DIRECT ").Append(cfg.SetupMinutes).Append("M SETUP BARS • ").Append(bars.Count).Append(" CANDLES • ").Append(marks.Count).Append(asian ? " ASIAN REVERSAL LEGS" : " DETECTED SETUPS").Append("</text>");
            sb.Append("<text x='").Append(left).Append("' y='48' fill='#dce9f8' font-family='Segoe UI,Arial' font-size='12'>Compact circle: S = setup awaiting verified 1M outcome • W = win • L = loss • E = session exit. Full data are in the package index.</text>");
            for (int p = 0; p <= 5; p++) { double price = min + (max - min) * p / 5, py = y(price); sb.Append("<text x='5' y='").Append((py + 4).ToString("0.0", CultureInfo.InvariantCulture)).Append("' fill='#dce9f8' font-family='Consolas' font-size='11'>").Append(price.ToString(symbol == "MGC" ? "0.0" : "0.00", CultureInfo.InvariantCulture)).Append("</text>"); }
            var index = new Dictionary<DateTime, int>();
            for (int i = 0; i < bars.Count; i++) { KeystoneArcBar b = bars[i]; index[b.Time] = i; double x = left + i * candle + candle / 2, oy = y(b.Open), cy = y(b.Close), hy = y(b.High), ly = y(b.Low); string color = b.Close > b.Open ? "#309780" : (b.Close < b.Open ? "#bc526a" : "#d0a74e"); sb.Append("<line x1='").Append(x.ToString("0.0", CultureInfo.InvariantCulture)).Append("' x2='").Append(x.ToString("0.0", CultureInfo.InvariantCulture)).Append("' y1='").Append(hy.ToString("0.0", CultureInfo.InvariantCulture)).Append("' y2='").Append(ly.ToString("0.0", CultureInfo.InvariantCulture)).Append("' stroke='#c2d3e4' stroke-width='1.25'/><rect x='").Append((x - candle / 2 + 2).ToString("0.0", CultureInfo.InvariantCulture)).Append("' y='").Append(Math.Min(oy, cy).ToString("0.0", CultureInfo.InvariantCulture)).Append("' width='").Append(Math.Max(2, candle - 4).ToString("0.0", CultureInfo.InvariantCulture)).Append("' height='").Append(Math.Max(1, Math.Abs(cy - oy)).ToString("0.0", CultureInfo.InvariantCulture)).Append("' fill='").Append(color).Append("' stroke='#c2d3e4' stroke-width='.55'/>"); if (b.Time.Minute == 0 || i == 0) sb.Append("<text x='").Append((x - 14).ToString("0.0", CultureInfo.InvariantCulture)).Append("' y='").Append(height - 34).Append("' fill='#9aabc0' font-family='Consolas' font-size='10'>").Append(b.Time.ToString("HH:mm")).Append("</text>"); }
            var svgGroups = marks.Select(mark => new { Event = mark, Index = EvidenceEntryBarIndex(bars, mark) }).Where(marker => marker.Index >= 0).GroupBy(marker => marker.Index).OrderBy(group => group.Key).ToList();
            var edges = new double[] { double.MinValue, double.MinValue, double.MinValue, double.MinValue };
            for (int groupNumber = 0; groupNumber < svgGroups.Count; groupNumber++)
            {
                List<KeystoneArcEvent> grouped = svgGroups[groupNumber].Select(marker => marker.Event).OrderBy(record => record.TriggerTime).ToList();
                KeystoneArcEvent e = grouped[0];
                int i = svgGroups[groupNumber].Key;
                double x = left + i * candle + candle / 2, hy = y(bars[i].High);
                string result = e.Outcome == "UNVERIFIED 1M" ? "#39d9ff" : (e.Outcome == "WIN" ? "#c269ff" : (e.Outcome.StartsWith("LOSS") ? "#ffba46" : "#99daff"));
                int wins = grouped.Count(record => record.Outcome == "WIN"), losses = grouped.Count(record => record.Outcome.StartsWith("LOSS")), exits = grouped.Count(record => record.Outcome == "SESSION EXIT");
                if (grouped.Count > 1) result = wins > 0 && losses == 0 && exits == 0 ? "#c269ff" : (losses > 0 && wins == 0 && exits == 0 ? "#ffba46" : "#39d9ff");
                int lane = 0; for (; lane < edges.Length; lane++) if (x - edges[lane] >= 31) break; if (lane >= edges.Length) lane = Array.IndexOf(edges, edges.Min());
                double markerX = x + (i % 2 == 0 ? 9 : -9), pinY = Math.Max(top + 6, hy - 23 - lane * 17), pinX = Math.Max(left, markerX - 8), leaderY = pinY + 16;
                string shortResult = grouped.Count == 1 ? (e.Outcome == "UNVERIFIED 1M" ? "↑" : (e.Outcome == "WIN" ? "W" : (e.Outcome.StartsWith("LOSS") ? "L" : "E"))) : grouped.Count.ToString(CultureInfo.InvariantCulture);
                sb.Append("<line x1='").Append(markerX.ToString("0.0", CultureInfo.InvariantCulture)).Append("' y1='").Append(leaderY.ToString("0.0", CultureInfo.InvariantCulture)).Append("' x2='").Append(x.ToString("0.0", CultureInfo.InvariantCulture)).Append("' y2='").Append((hy - 2).ToString("0.0", CultureInfo.InvariantCulture)).Append("' stroke='").Append(result).Append("' stroke-width='2' stroke-dasharray='5 5' opacity='.50'/><circle cx='").Append((pinX + 8).ToString("0.0", CultureInfo.InvariantCulture)).Append("' cy='").Append((pinY + 8).ToString("0.0", CultureInfo.InvariantCulture)).Append("' r='8' fill='").Append(result).Append("' stroke='#101623'/><text x='").Append((pinX + 5.2).ToString("0.0", CultureInfo.InvariantCulture)).Append("' y='").Append((pinY + 11.2).ToString("0.0", CultureInfo.InvariantCulture)).Append("' fill='#101623' font-family='Segoe UI,Arial' font-size='8' font-weight='700'>").Append(shortResult).Append("</text>");
                edges[lane] = pinX + 16;
            }
            return sb.Append("</svg>").ToString();
        }

        private string BuildHtmlReport()
        {
            List<KeystoneArcEvent> all = events.OrderBy(x => x.EntryTime == DateTime.MinValue ? x.TriggerTime : x.EntryTime).ToList();
            List<KeystoneArcEvent> accepted = all.Where(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase)).ToList();
            bool outcomesVerified = config.OutcomeModelEnabled == 1;
            int wins = all.Count(x => x.Outcome == "WIN");
            int losses = all.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
            int exits = all.Count(x => x.Outcome == "SESSION EXIT");
            int noEntry = all.Count(x => x.Outcome == "NO ENTRY DATA");
            bool asianReport = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            string strategyName = asianReport ? "Asian Cycle Backtest • Copy Trading" : "BH Long • Break-High";
            string strategyAudit = asianReport ? "MNQ + MGC • exact " + config.AsianStartHhmm.ToString("0000") + " ET simultaneous start • " + config.AsianMnqInitialDirection + " MNQ / " + config.AsianMgcInitialDirection + " MGC • " + (config.AsianRiskMode == "PRICE" ? "instrument price-move reversal limits" : "fixed cash reversal limits") + " • next-minute opposite entry • +1 micro per leg • combined boundaries close-confirmed" : "red candle • bullish reference close • immediate next-bar high break";
            int poolWins = accepted.Count(x => x.Outcome == "WIN");
            int poolLosses = accepted.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
            int poolExits = accepted.Count(x => x.Outcome == "SESSION EXIT");
            int assigned = accepted.Count(x => !string.IsNullOrWhiteSpace(x.AssignedVirtualAccount));
            int skipped = accepted.Count(x => !string.IsNullOrWhiteSpace(x.SkipReason));
            int excluded = all.Count(x => !string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase));
            double gross = all.Sum(x => x.GrossPnl);
            double poolGross = accepted.Sum(x => x.GrossPnl);
            bool personalReport = string.Equals(config.AccountPath, "PERSONAL", StringComparison.OrdinalIgnoreCase);
            int testedDays = EvidenceSessionDateCount();
            List<KeystoneArcEvent> finalLiveLedger = personalReport ? FinalLiveAccountLedger() : new List<KeystoneArcEvent>();
            bool liveLedgerReady = personalReport && HasFinalLiveAccountLedger();
            int liveRangeTrades = finalLiveLedger.Count;
            double liveAccountGross = finalLiveLedger.Sum(x => x.GrossPnl);
            int liveWins = finalLiveLedger.Count(x => x.Outcome == "WIN");
            int liveLosses = finalLiveLedger.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase));
            int liveExits = finalLiveLedger.Count(x => x.Outcome == "SESSION EXIT");
            int liveNotSelected = accepted.Count(x => string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && !string.IsNullOrWhiteSpace(x.SkipReason));
            double payoutCash = accounts.Sum(x => x.PayoutCash);
            double evaluationCost = accounts.Sum(x => x.EvaluationCost);
            double netCashAfterEvaluationCost = payoutCash - evaluationCost;
            KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
            List<KeystoneArcPayoutCycleRow> payoutCycles = KeystoneArcEngine.BuildPayoutCycleRows(accounts, config);
            List<KeystoneArcBalanceMatrixRow> balanceMatrix = KeystoneArcEngine.BuildBalanceMatrixRows(accounts, config);
            int evaluationPasses = accounts.Sum(x => x.EvaluationPasses);
            int currentlyFunded = accounts.Count(x => x.Funded);
            string payoutExplanation = config.EvaluationEnabled < 0 ? "One-day study: lifecycle is off."
                : (config.EvaluationEnabled == 0 ? (payoutCash > 0 ? "Payout cycle(s) recorded." : "Direct-funded slots have not met the selected payout gate.")
                : (payoutCash > 0 ? "Payout cycle(s) recorded." : (evaluationPasses == 0 ? "No evaluation passed the selected gate." : (currentlyFunded == 0 ? "No slot is currently funded." : "Funded slots have not met the selected payout gate."))));
            KeystoneArcRiskSequenceStats personalRisk = CalculateRiskSequenceStats(personalReport ? finalLiveLedger : all, config.PersonalStartingBalance);
            KeystoneArcRiskSequenceStats propRisk = CalculateWorstVirtualAccountRisk(config.PropStartingBalance);
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Keystone Arc Research Report</title><style>");
            sb.Append("html{scroll-behavior:smooth}body{margin:0;background:#0a0f1a;color:#eef4fc;font-family:Segoe UI,Arial,sans-serif;line-height:1.35}.wrap{max-width:1500px;margin:auto;padding:28px}.hero{background:linear-gradient(120deg,#13243c,#1a3144);border:1px solid #2cd7cb;border-radius:14px;padding:24px}.brand{color:#2cd7cb;font-size:13px;font-weight:700;letter-spacing:.08em}.hero h1{margin:4px 0 8px;font-size:29px}.muted{color:#9baec6}.notice{margin-top:16px;border-left:4px solid #f4bf4c;background:#242536;padding:12px;color:#f5d790}.goodnote{margin-top:12px;border-left:4px solid #41d98e;background:#102d29;padding:12px;color:#d5f6e7}.report-nav{position:sticky;top:8px;z-index:3;display:flex;flex-wrap:wrap;gap:7px;margin:14px 0;padding:10px;background:rgba(10,15,26,.94);border:1px solid #283d5b;border-radius:10px;backdrop-filter:blur(5px)}.report-nav a{color:#dff8ff;text-decoration:none;background:#183653;border:1px solid #2cd7cb;border-radius:7px;padding:6px 9px;font-size:12px;font-weight:700}.report-nav a:hover{background:#2cd7cb;color:#08101b}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(205px,1fr));gap:12px;margin:18px 0}.card{background:#131c2d;border:1px solid #283d5b;border-radius:10px;padding:14px}.label{color:#9baec6;font-size:11px;font-weight:700;text-transform:uppercase}.value{font-size:24px;font-weight:700;margin-top:3px}.green{color:#41d98e}.red{color:#ef5c69}.cyan{color:#2cd7cb}.gold{color:#f4bf4c}.orchid{color:#ca75ef}h2{font-size:18px;color:#2cd7cb;margin-top:30px;padding-top:8px}h3{font-size:14px;color:#ca75ef;margin:18px 0 8px}table{width:100%;border-collapse:collapse;background:#131c2d;font-size:12px}th{position:sticky;top:0;background:#1c273d;color:#2cd7cb;text-align:left}th,td{padding:8px;border-bottom:1px solid #283d5b;vertical-align:top}tr.win td{background:#102d29}tr.loss td{background:#321b25}tr.exit td{background:#23263a}.table-wrap{overflow:auto;max-height:540px;border:1px solid #283d5b;border-radius:10px}.mono{font-family:Consolas,monospace;font-size:12px;white-space:pre-wrap}.pill{display:inline-block;border:1px solid #365577;border-radius:999px;padding:5px 9px;margin:3px;color:#d7e5f5}.filters{display:flex;flex-wrap:wrap;gap:8px;margin:10px 0}.filters button{cursor:pointer;background:#183653;color:#dff8ff;border:1px solid #2cd7cb;border-radius:7px;padding:7px 10px;font-weight:700}.filters button:hover,.filters button.active{background:#2cd7cb;color:#08101b}.audit{margin:18px 0;background:#101a2a;border:1px solid #365577;border-radius:10px;overflow:hidden}.audit summary{cursor:pointer;padding:13px;color:#caeefa;background:#15243a;font-weight:700}.audit summary:hover{background:#183653}.audit .inside{padding:0 14px 14px}.hide-row{display:none}.footer{margin-top:28px;color:#9baec6;font-size:12px}@media(max-width:700px){.wrap{padding:12px}.hero{padding:16px}.hero h1{font-size:23px}.report-nav{position:static}.value{font-size:21px}}</style></head><body><div class='wrap'>");
            sb.Append("<section class='hero'><div class='brand'>KEYSTONE ARC 5M RESEARCH LAB</div><h1>").Append(personalReport ? "Live-Account Historical Research Report" : "Prop Virtual-Pool Historical Research Report").Append("</h1><div class='muted'>").Append(Html(strategyName)).Append(" • ").Append(Html(strategyAudit)).Append("<br>Created ").Append(Html(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))).Append(" • No live orders • Historical-model output</div><div class='notice'><strong>Interpretation boundary.</strong> This report records ").Append(personalReport ? "one historical account using one earliest resolved setup per session date" : (asianReport ? "the selected virtual-account evaluation/funded scenario using copied daily Asian cycles" : "the selected virtual-account evaluation/funded scenario")).Append(". It is not a verified trading strategy, firm-rule determination, or payout forecast.</div><div class='goodnote'><strong>Data state.</strong> ").Append(outcomesVerified ? "The 1-minute outcome series reproduced the selected setup bars, so target/stop/session-close model fields are enabled." : "The selected setup bars loaded, but no matching 1-minute outcome series was proven. This report is limited to setup placement and counts; win/loss, P/L, drawdown, and account math are intentionally disabled.").Append("</div></section>");
            if (!personalReport) sb.Append(config.EvaluationEnabled < 0 ? "<nav class='report-nav'><a href='#portfolio'>One-Day Scorecard</a><a href='#research-findings'>Research Findings</a><a href='#daily-session'>Period Performance</a><a href='#accounts'>Account Allocation</a><a href='#event-audit'>Event Audit</a></nav>" : "<nav class='report-nav'><a href='#portfolio'>Portfolio</a><a href='#research-findings'>Research Findings</a><a href='#daily-session'>Period Performance</a><a href='#first-return'>First Returns</a><a href='#payout-accounts'>Payout Accounts</a><a href='#payout-cycles'>Payout Cycles</a><a href='#accounts'>Account Snapshot</a><a href='#event-audit'>Event Audit</a></nav>");
            if (personalReport)
                sb.Append("<h2>LIVE ACCOUNT • FINAL RESULT</h2><div class='grid'><div class='card'><div class='label'>Tested session days</div><div class='value cyan'>").Append(testedDays).Append("</div><div class='muted'>Selected session-date range.</div></div><div class='card'><div class='label'>Final W / L / session exits</div><div class='value gold'>").Append(liveLedgerReady ? liveWins + " / " + liveLosses + " / " + liveExits : "RUN RESULTS").Append("</div><div class='muted'>One earliest resolved setup per session date only.</div></div><div class='card'><div class='label'>Final live-account range P/L</div><div class='value ").Append(liveAccountGross >= 0 ? "green" : "red").Append("'>").Append(Html(liveLedgerReady ? liveAccountGross.ToString("C0") : "RUN RESULTS")).Append("</div><div class='muted'>Canonical final ledger.</div></div><div class='card'><div class='label'>Starting / ending balance</div><div class='value cyan'>").Append(Html(config.PersonalStartingBalance.ToString("C0"))).Append(" / ").Append(Html((config.PersonalStartingBalance + liveAccountGross).ToString("C0"))).Append("</div><div class='muted'>Ending balance = start + final ledger P/L.</div></div><div class='card'><div class='label'>Later / unavailable setups</div><div class='value cyan'>").Append(liveNotSelected).Append("</div><div class='muted'>Kept as evidence; excluded from final P/L.</div></div><div class='card'><div class='label'>Personal target / stop / lot</div><div class='value gold'>").Append(Html(LiveTargetRiskCardValue(config))).Append("</div><div class='muted'>").Append(Html(LiveOutcomeModelSummary(config))).Append("</div></div></div>");
            else if (config.EvaluationEnabled < 0)
            {
                int oneDayTraded = accounts.Count(x => x.Trades > 0);
                int oneDayProfitLocks = accounts.Count(x => x.DayLocked && x.DayPnl >= Math.Max(0, config.DailyGoal));
                int oneDayLossLocks = accounts.Count(x => x.DayLocked && x.DayPnl <= -Math.Abs(config.DailyLoss));
                double oneDayPnl = accounts.Sum(x => x.TotalPnl);
                sb.Append("<h2 id='portfolio'>One-day virtual-account scorecard</h2><div class='grid'><div class='card'><div class='label'>Eligible / assigned / skipped</div><div class='value cyan'>").Append(accepted.Count).Append(" / ").Append(assigned).Append(" / ").Append(skipped).Append("</div><div class='muted'>Only assigned rows contribute to the account result.</div></div><div class='card'><div class='label'>Assigned W / L / exits</div><div class='value gold'>").Append(wins).Append(" / ").Append(losses).Append(" / ").Append(exits).Append("</div><div class='muted'>Historical outcomes after account allocation.</div></div><div class='card'><div class='label'>Accounts traded / unused</div><div class='value cyan'>").Append(oneDayTraded).Append(" / ").Append(Math.Max(0, accounts.Count - oneDayTraded)).Append("</div><div class='muted'>Every account can receive more trades only until its selected daily lock.</div></div><div class='card'><div class='label'>Profit locks / loss locks</div><div class='value ").Append(oneDayLossLocks > 0 ? "red" : "green").Append("'>").Append(oneDayProfitLocks).Append(" / ").Append(oneDayLossLocks).Append("</div><div class='muted'>Profit lock ").Append(Html(config.DailyGoal.ToString("C0"))).Append(" • loss lock -").Append(Html(Math.Abs(config.DailyLoss).ToString("C0"))).Append(".</div></div><div class='card'><div class='label'>Assigned model P/L</div><div class='value ").Append(oneDayPnl < 0 ? "red" : "green").Append("'>").Append(Html(oneDayPnl.ToString("C0"))).Append("</div><div class='muted'>No payout, evaluation cost, replacement, or blowout lifecycle is used for a one-day study.</div></div></div>");
            }
            else
                sb.Append("<h2>At-a-glance result</h2><div class='grid'><div class='card'><div class='label'>Strategy</div><div class='value cyan'>").Append(Html(asianReport ? "ASIAN CYCLE COPY" : (config.BhAggressionFilter == "STRONGER" ? "STRONGER BH" : "ALL VALID BH"))).Append("</div><div class='muted'>").Append(Html(asianReport ? strategyAudit : BhAggressionSummary(config))).Append("</div></div><div class='card'><div class='label'>Resolved W / L / exits</div><div class='value gold'>").Append(outcomesVerified ? wins + " / " + losses + " / " + exits : "BLOCKED").Append("</div><div class='muted'>").Append(asianReport ? "Resolved daily-cycle legs; before copy allocation." : "All detected setups; before account allocation.").Append("</div></div><div class='card'><div class='label'>All-outcome model P/L</div><div class='value ").Append(gross >= 0 ? "green" : "red").Append("'>").Append(Html(outcomesVerified ? gross.ToString("C0") : "NOT AVAILABLE")).Append("</div><div class='muted'>Historical model only.</div></div><div class='card'><div class='label'>Pool assigned / skipped</div><div class='value cyan'>").Append(assigned).Append(" / ").Append(skipped).Append("</div><div class='muted'>").Append(asianReport ? "Every resolved daily cycle is copied to active accounts." : "Eligible setups only.").Append("</div></div><div class='card'><div class='label'>Evaluation passes / currently funded</div><div class='value orchid'>").Append(evaluationPasses).Append(" / ").Append(currentlyFunded).Append("</div><div class='muted'>Selected scenario mechanics.</div></div><div class='card'><div class='label'>Payout cash</div><div class='value ").Append(payoutCash > 0 ? "green" : "gold").Append("'>").Append(Html(payoutCash.ToString("C0"))).Append("</div><div class='muted'>").Append(Html(payoutExplanation)).Append("</div></div></div>");
            if (!personalReport) AppendPortfolioCashHtml(sb, capital, config, accounts);
            if (!personalReport) AppendResearchFindingsHtml(sb);
            if (personalReport)
                sb.Append("<h2>Live-account risk sequence</h2><div class='card'><div class='mono'>Worst consecutive losing trades: ").Append(personalRisk.WorstNegativeSetupStreak).Append(" / ").Append(Html(personalRisk.WorstNegativeSetupStreakPnl.ToString("C0"))).Append("\nWorst consecutive negative session days: ").Append(personalRisk.WorstNegativeDayStreak).Append(" / ").Append(Html(personalRisk.WorstNegativeDayStreakPnl.ToString("C0"))).Append("\nStarting balance: ").Append(Html(personalRisk.StartingBalance.ToString("C0"))).Append(" • ending balance: ").Append(Html(personalRisk.EndingBalance.ToString("C0"))).Append(" • lowest balance: ").Append(Html(personalRisk.LowestBalance.ToString("C0"))).Append("\nMaximum drawdown from running peak: ").Append(Html(personalRisk.MaximumDrawdown.ToString("C0"))).Append("</div></div>");
            else
                sb.Append("<h2>Risk sequence • historical model only</h2><div class='grid'><div class='card'><div class='label'>Personal research • all detected setups</div><div class='mono'>Worst negative setup streak: ").Append(personalRisk.WorstNegativeSetupStreak).Append(" / ").Append(Html(personalRisk.WorstNegativeSetupStreakPnl.ToString("C0"))).Append("\nWorst negative-day streak: ").Append(personalRisk.WorstNegativeDayStreak).Append(" / ").Append(Html(personalRisk.WorstNegativeDayStreakPnl.ToString("C0"))).Append("\nStarting balance: ").Append(Html(personalRisk.StartingBalance.ToString("C0"))).Append(" • lowest: ").Append(Html(personalRisk.LowestBalance.ToString("C0"))).Append("\nMaximum drawdown from running peak: ").Append(Html(personalRisk.MaximumDrawdown.ToString("C0"))).Append("</div></div><div class='card'><div class='label'>Virtual-prop allocation • worst individual account</div><div class='mono'>Worst negative setup streak: ").Append(propRisk.WorstNegativeSetupStreak).Append(" / ").Append(Html(propRisk.WorstNegativeSetupStreakPnl.ToString("C0"))).Append("\nWorst negative-day streak: ").Append(propRisk.WorstNegativeDayStreak).Append(" / ").Append(Html(propRisk.WorstNegativeDayStreakPnl.ToString("C0"))).Append("\nStarting balance: ").Append(Html(propRisk.StartingBalance.ToString("C0"))).Append(" • lowest: ").Append(Html(propRisk.LowestBalance.ToString("C0"))).Append("\nMaximum drawdown from running peak: ").Append(Html(propRisk.MaximumDrawdown.ToString("C0"))).Append("</div></div></div>");
            sb.Append("<h2>Tested range and model</h2><div class='card'><div class='mono'>").Append(Html(BuildReportRangeSummary())).Append("</div><p><strong>Historical data receipt:</strong> ").Append(Html(historicalDataReceipt)).Append("</p></div>");
            sb.Append("<div class='grid'><div class='card'><div class='label'>TOTAL TESTED SESSION DAYS</div><div class='value cyan'>").Append(testedDays).Append("</div><div class='muted'>All session dates in the selected range, including no-trade dates.</div></div><div class='card'><div class='label'>").Append(personalReport ? "FINAL LIVE ACCOUNT RANGE P/L" : "RAW MODEL RANGE P/L").Append("</div><div class='value ").Append((personalReport ? liveAccountGross : gross) >= 0 ? "green" : "red").Append("'>").Append(Html(personalReport && !liveLedgerReady ? "RUN RESULTS" : (personalReport ? liveAccountGross : gross).ToString("C0"))).Append("</div><div class='muted'>").Append(personalReport ? "Only first-per-instrument final live rows; all later setup evidence is excluded." : "All detected resolved rows before virtual allocation.").Append("</div></div></div>");
            sb.Append("<h2>Instrument data and outcome check</h2><div class='table-wrap'><table><thead><tr><th>Instrument</th><th>Direct 1M bars</th><th>Direct ").Append(config.SetupMinutes).Append("M bars</th><th>Detected setups</th><th>").Append(personalReport ? "Final live W / L / exit" : "W / L / exit").Append("</th><th>").Append(personalReport ? "Final live P/L" : "Model P/L").Append("</th><th>Outcome series</th></tr></thead><tbody>");
            foreach (string instrument in new[] { "MNQ", "MGC" }.Where(x => config.Scope == "BOTH" || config.Scope == x))
            {
                List<KeystoneArcEvent> rows = all.Where(e => e.Symbol == instrument).ToList();
                List<KeystoneArcEvent> resultRows = personalReport ? finalLiveLedger.Where(e => e.Symbol == instrument).ToList() : rows;
                int instrumentWins = resultRows.Count(e => e.Outcome == "WIN"), instrumentLosses = resultRows.Count(e => e.Outcome.StartsWith("LOSS")), instrumentExits = resultRows.Count(e => e.Outcome == "SESSION EXIT");
                int oneMinuteBars = instrument == "MNQ" ? mnqBars.Count : mgcBars.Count, setupBars = instrument == "MNQ" ? mnqSetupBars.Count : mgcSetupBars.Count;
                string source = instrument == "MNQ" ? config.MnqOutcomeSource : config.MgcOutcomeSource;
                double instrumentGross = resultRows.Sum(e => e.GrossPnl);
                sb.Append("<tr><td><strong>").Append(instrument).Append("</strong></td><td>").Append(oneMinuteBars.ToString("N0")).Append("</td><td>").Append(setupBars.ToString("N0")).Append("</td><td>").Append(rows.Count.ToString("N0")).Append("</td><td>").Append(instrumentWins).Append(" / ").Append(instrumentLosses).Append(" / ").Append(instrumentExits).Append("</td><td class='").Append(instrumentGross >= 0 ? "green" : "red").Append("'>").Append(Html(instrumentGross.ToString("C0"))).Append("</td><td>").Append(Html(source)).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            if (!personalReport) AppendSelectedSessionDailyHtml(sb, all, config, outcomesVerified);
            sb.Append("<h2>").Append(personalReport ? "Live result audit" : "Two separate result layers").Append("</h2><div class='card'><div class='mono'>");
            if (personalReport)
                sb.Append(Html(outcomesVerified ? "FINAL LIVE ACCOUNT LEDGER — THE ONLY FINAL NUMBER\nTraded: " + liveRangeTrades + " = Wins: " + liveWins + " + Losses: " + liveLosses + " + Session exits: " + liveExits + " | Final P/L: " + liveAccountGross.ToString("C0") + "\n\nDETECTED OUTCOME REFERENCE — KEPT AS EVIDENCE\nDetected: " + all.Count + " | Raw wins: " + wins + " | Raw losses: " + losses + " | Raw session exits: " + exits + "\n\nDISPOSITION CHECK\nDetected " + all.Count + " = Final " + liveRangeTrades + " + Later/outcome-unavailable " + liveNotSelected + " + Explicitly excluded " + excluded + ".\n\nReconcile final P/L by summing only FINAL LIVE TRADE rows. Every later same-instrument setup remains visible with its raw W/L/E result but is excluded from final live P/L." : "LIVE RESULT NOT RUN\nDetected setups are available, but no final one-account ledger exists yet. Recalculate the live ledger before reading a live P/L.")).Append("</div></div>");
            else
                sb.Append(Html(outcomesVerified ? "ALL DETECTED OUTCOME LEDGER\nEvents: " + all.Count + " | Wins: " + wins + " | Losses: " + losses + " | Session exits: " + exits + " | No-entry data: " + noEntry + " | Gross: " + gross.ToString("C0") + "\n\nVIRTUAL-POOL INPUT (all eligible detected setups)\nEligible: " + accepted.Count + " | Wins: " + poolWins + " | Losses: " + poolLosses + " | Session exits: " + poolExits + " | Assigned: " + assigned + " | Skipped: " + skipped + " | Gross: " + poolGross.ToString("C0") + "\n\nOnly rows explicitly excluded in Verify Entries are omitted from rotation." : "DETECTED SETUP LEDGER\nEvents: " + all.Count + " | Eligible: " + accepted.Count + " | Explicitly excluded: " + excluded + "\n\nOUTCOME AND VIRTUAL-POOL MATH IS BLOCKED\nThe 1-minute outcome series did not aggregate back to the selected setup bars. This protects the report from inventing win/loss, exit, P/L, payout, or account-balance values. Use the evidence charts to validate setup placement, then repair the data receipt before relying on outcome math.")).Append("</div></div>");
            if (outcomesVerified)
            {
                sb.Append("<h2>").Append(personalReport ? "Final live-account totals by session date" : "Detected outcome totals by session date • all resolved rows").Append(" • TESTED DAYS ").Append(testedDays).Append("</h2><div class='table-wrap'><table><thead><tr><th>Session date</th><th>").Append(personalReport ? "Raw setups" : "Events").Append("</th><th>").Append(personalReport ? "Final trades" : "Wins").Append("</th><th>").Append(personalReport ? "W / L / session exit" : "Losses").Append("</th><th>").Append(personalReport ? "Not selected" : "Session exits").Append("</th><th>").Append(personalReport ? "Final live P/L" : "Model P/L").Append("</th></tr></thead><tbody>");
                if (personalReport)
                {
                    foreach (DateTime day in EvidenceSessionDates(0))
                    {
                        List<KeystoneArcEvent> rawDay = all.Where(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config) == day).ToList();
                        List<KeystoneArcEvent> finalDay = finalLiveLedger.Where(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config) == day).ToList();
                        double dayPnl = finalDay.Sum(x => x.GrossPnl);
                        int notSelectedDay = rawDay.Count(x => string.Equals(x.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(x.AssignedVirtualAccount) && !string.IsNullOrWhiteSpace(x.SkipReason));
                        sb.Append("<tr><td>").Append(day.ToString("ddd yyyy-MM-dd")).Append("</td><td>").Append(rawDay.Count).Append("</td><td>").Append(finalDay.Count).Append("</td><td><span class='green'>").Append(finalDay.Count(x => x.Outcome == "WIN")).Append("</span> / <span class='red'>").Append(finalDay.Count(x => x.Outcome.StartsWith("LOSS"))).Append("</span> / ").Append(finalDay.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td>").Append(notSelectedDay).Append("</td><td class='").Append(dayPnl >= 0 ? "green" : "red").Append("'>").Append(Html(dayPnl.ToString("C0"))).Append("</td></tr>");
                    }
                }
                else
                {
                    foreach (var g in all.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config)).OrderBy(x => x.Key)) { double dayPnl = g.Sum(x => x.GrossPnl); sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count()).Append("</td><td class='green'>").Append(g.Count(x => x.Outcome == "WIN")).Append("</td><td class='red'>").Append(g.Count(x => x.Outcome.StartsWith("LOSS"))).Append("</td><td>").Append(g.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td class='").Append(dayPnl >= 0 ? "green" : "red").Append("'>").Append(Html(dayPnl.ToString("C0"))).Append("</td></tr>"); }
                }
                sb.Append("</tbody></table></div>");
                if (personalReport)
                {
                    sb.Append("<h2>Final live-account weekly totals</h2><div class='table-wrap'><table><thead><tr><th>Week starting</th><th>Trades</th><th>W / L / session exit</th><th>P/L</th></tr></thead><tbody>");
                    foreach (var g in finalLiveLedger.GroupBy(x => { DateTime day = KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config); return day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); }).OrderBy(x => x.Key)) { double value = g.Sum(x => x.GrossPnl); sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count()).Append("</td><td>").Append(g.Count(x => x.Outcome == "WIN")).Append(" / ").Append(g.Count(x => x.Outcome.StartsWith("LOSS"))).Append(" / ").Append(g.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td class='").Append(value >= 0 ? "green" : "red").Append("'>").Append(Html(value.ToString("C0"))).Append("</td></tr>"); }
                    sb.Append("</tbody></table></div><h2>Final live-account monthly totals</h2><div class='table-wrap'><table><thead><tr><th>Month</th><th>Trades</th><th>W / L / session exit</th><th>P/L</th></tr></thead><tbody>");
                    foreach (var g in finalLiveLedger.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config).ToString("yyyy-MM")).OrderBy(x => x.Key)) { double value = g.Sum(x => x.GrossPnl); sb.Append("<tr><td>").Append(g.Key).Append("</td><td>").Append(g.Count()).Append("</td><td>").Append(g.Count(x => x.Outcome == "WIN")).Append(" / ").Append(g.Count(x => x.Outcome.StartsWith("LOSS"))).Append(" / ").Append(g.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td class='").Append(value >= 0 ? "green" : "red").Append("'>").Append(Html(value.ToString("C0"))).Append("</td></tr>"); }
                    sb.Append("</tbody></table></div>");
                }
                if (excluded > 0)
                {
                    sb.Append("<h2>Eligible outcomes by session date • after explicit exclusions</h2><div class='table-wrap'><table><thead><tr><th>Session date</th><th>Eligible</th><th>Wins</th><th>Losses</th><th>Session exits</th><th>Pool gross</th></tr></thead><tbody>");
                    foreach (var g in accepted.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config)).OrderBy(x => x.Key)) sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count()).Append("</td><td class='green'>").Append(g.Count(x => x.Outcome == "WIN")).Append("</td><td class='red'>").Append(g.Count(x => x.Outcome.StartsWith("LOSS"))).Append("</td><td>").Append(g.Count(x => x.Outcome == "SESSION EXIT")).Append("</td><td class='").Append(g.Sum(x => x.GrossPnl) >= 0 ? "green" : "red").Append("'>").Append(Html(g.Sum(x => x.GrossPnl).ToString("C0"))).Append("</td></tr>");
                    sb.Append("</tbody></table></div>");
                }
            }
            else
            {
                sb.Append("<h2>Detected setups by session date • placement review only</h2><div class='table-wrap'><table><thead><tr><th>Session date</th><th>MNQ setups</th><th>MGC setups</th><th>Total</th><th>Outcome state</th></tr></thead><tbody>");
                foreach (var g in all.GroupBy(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config)).OrderBy(x => x.Key)) sb.Append("<tr><td>").Append(g.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(g.Count(x => x.Symbol == "MNQ")).Append("</td><td>").Append(g.Count(x => x.Symbol == "MGC")).Append("</td><td>").Append(g.Count()).Append("</td><td class='gold'>UNVERIFIED 1M</td></tr>");
                sb.Append("</tbody></table></div>");
            }
            if (!personalReport)
            {
                if (config.EvaluationEnabled >= 0)
                {
                    AppendFirstReturnHtml(sb, accounts, config);
                    AppendPayoutAccountHtml(sb, accounts, config);
                    AppendPayoutCycleHtml(sb, payoutCycles, balanceMatrix, config);
                }
                AppendAccountProgressHtml(sb, accounts, config);
                if (config.EvaluationEnabled >= 0) AppendLifecyclePeriodHtml(sb, accounts);
                AppendComparisonHtml(sb);
            }
            sb.Append("<details class='audit' id='event-audit'><summary>").Append(personalReport ? "Open complete detected ledger" : "Open complete detected event ledger • resolved historical outcomes").Append("</summary><div class='inside'><div class='card'>").Append(personalReport ? "For the final live number, sum only rows marked <strong>FINAL LIVE TRADE</strong>. All non-selected rows show their raw historical W/L/E outcome for audit but are excluded from final live P/L." : "Use these filters to audit every completed historical result without changing the saved ledger.").Append(" Peak / trough after entry retain the best favorable and adverse price reached before the modeled exit, so a loss can be inspected for its prior peak.").Append("</div><div class='filters' id='ledgerFilters'><button class='active' onclick=\"setLedger('ALL',this)\">ALL</button><button onclick=\"setLedger('MNQ',this)\">MNQ</button><button onclick=\"setLedger('MGC',this)\">MGC</button><button onclick=\"setLedger('WIN',this)\">WINS</button><button onclick=\"setLedger('LOSS',this)\">LOSSES</button><button onclick=\"setLedger('EXIT',this)\">SESSION EXITS</button></div><div class='table-wrap'><table id='ledger'><thead><tr><th>Session date</th><th>Trigger bar</th><th>Actual 1M entry</th><th>Instrument</th><th>Direction</th><th>Setup</th><th>Strength</th><th>Entry</th><th>Stop</th><th>Target</th><th>Peak / trough after entry</th><th>Outcome</th><th>Exit time</th><th>Exit price</th><th>Exit distance</th><th>Model P/L</th><th>").Append(personalReport ? "Live status" : "Account").Append("</th><th>Disposition</th><th>FVG zone</th></tr></thead><tbody>");
            foreach (var e in all)
            {
                string rowClass = e.Outcome == "WIN" ? "win" : (e.Outcome.StartsWith("LOSS") ? "loss" : "exit");
                string fvg = double.IsNaN(e.FvgLower) || double.IsNaN(e.FvgUpper) ? "—" : e.FvgLower.ToString("0.00") + " to " + e.FvgUpper.ToString("0.00");
                string exitPrice = double.IsNaN(e.ExitPrice) ? "—" : e.ExitPrice.ToString("0.00");
                string exitDistance = double.IsNaN(e.ExitPrice) ? "—" : (e.ExitPrice - e.Entry).ToString("+0.00;-0.00;0.00");
                string outcomeFilter = e.Outcome == "WIN" ? "WIN" : (e.Outcome.StartsWith("LOSS") ? "LOSS" : (e.Outcome == "SESSION EXIT" ? "EXIT" : "OTHER"));
                string excursions = double.IsNaN(e.PeakAfterEntry) || double.IsNaN(e.TroughAfterEntry) ? "—" : e.PeakAfterEntry.ToString("0.00") + " / " + e.TroughAfterEntry.ToString("0.00");
                string liveStatus = personalReport ? (string.Equals(e.AssignedVirtualAccount, "KA-LIVE", StringComparison.OrdinalIgnoreCase) ? "FINAL LIVE TRADE" : (!string.IsNullOrWhiteSpace(e.SkipReason) ? "NOT SELECTED • " + e.SkipReason : "DETECTED • NOT RUN")) : (e.AssignedVirtualAccount ?? string.Empty);
                sb.Append("<tr class='").Append(rowClass).Append("' data-symbol='").Append(Html(e.Symbol)).Append("' data-outcome='").Append(outcomeFilter).Append("'><td>").Append(Html(KeystoneArcEngine.SessionGroupingDate(e.TriggerTime, config).ToString("yyyy-MM-dd"))).Append("</td><td>").Append(Html(e.TriggerTime.ToString("yyyy-MM-dd HH:mm"))).Append("</td><td>").Append(Html(e.EntryTime.ToString("yyyy-MM-dd HH:mm"))).Append("</td><td>").Append(Html(e.Symbol)).Append("</td><td>").Append(Html(e.Direction)).Append("</td><td>").Append(Html(e.SetupClass)).Append("</td><td>").Append(Html(e.StrengthTag)).Append("</td><td>").Append(Html(e.Entry.ToString("0.00"))).Append("</td><td>").Append(Html(e.Stop.ToString("0.00"))).Append("</td><td>").Append(Html(e.Target.ToString("0.00"))).Append("</td><td>").Append(Html(excursions)).Append("</td><td>").Append(Html(e.Outcome)).Append("</td><td>").Append(Html(e.ExitTime.ToString("yyyy-MM-dd HH:mm"))).Append("</td><td>").Append(Html(exitPrice)).Append("</td><td>").Append(Html(exitDistance)).Append("</td><td>").Append(Html(e.GrossPnl.ToString("C0"))).Append("</td><td>").Append(Html(liveStatus)).Append("</td><td>").Append(Html(PoolDecisionLabel(e))).Append("</td><td>").Append(Html(fvg)).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div></details><p class='footer'>Generated by Keystone Arc 5M Research Lab. CSV exports remain the machine-readable source ledgers; this HTML file is the human-readable companion report.</p></div><script>function setLedger(v,b){document.querySelectorAll('#ledger tbody tr').forEach(function(r){r.classList.toggle('hide-row',v!='ALL'&&r.dataset.symbol!=v&&r.dataset.outcome!=v)});document.querySelectorAll('#ledgerFilters button').forEach(function(x){x.classList.remove('active')});if(b)b.classList.add('active');}function setCompare(v,b){document.querySelectorAll('#comparison tbody tr').forEach(function(r){r.classList.toggle('hide-row',v!='ALL'&&r.dataset.symbol!=v&&r.dataset.timeframe!=v)});document.querySelectorAll('#comparisonFilters button').forEach(function(x){x.classList.remove('active')});if(b)b.classList.add('active');}function filterCycles(){var m=document.getElementById('cycleMonth'),d=document.getElementById('cycleDate'),mv=m?m.value:'ALL',dv=d?d.value:'ALL';document.querySelectorAll('#payoutCycles tbody tr').forEach(function(r){var ok=(mv=='ALL'||r.dataset.month==mv)&&(dv=='ALL'||r.dataset.date==dv);r.classList.toggle('hide-row',!ok)});document.querySelectorAll('#balanceMatrix tbody tr').forEach(function(r){var ok=(mv=='ALL'||r.dataset.matrixMonth==mv)&&(dv=='ALL'||r.dataset.matrixDate==dv);r.classList.toggle('hide-row',!ok)});document.querySelectorAll('[data-cycle-month-summary]').forEach(function(r){r.classList.toggle('hide-row',mv!='ALL'&&r.dataset.cycleMonthSummary!=mv)})}function filterAccounts(){var s=document.getElementById('accountSearch'),f=document.getElementById('accountState'),q=s?s.value.toUpperCase():'',v=f?f.value:'ALL';document.querySelectorAll('#accountSnapshot tbody tr').forEach(function(r){var ok=(v=='ALL'||r.dataset.accountState==v)&&(!q||r.dataset.account.indexOf(q)>=0);r.classList.toggle('hide-row',!ok)})}</script></body></html>");
            return sb.ToString();
        }

        private static void AppendPortfolioCashHtml(StringBuilder sb, KeystoneArcCapitalPolicySummary capital, KeystoneArcRunConfig cfg, List<KeystoneArcVirtualAccount> accountRows)
        {
            if (sb == null || capital == null || cfg == null) return;
            bool continuousReplenishment = cfg.EvaluationEnabled > 0 && !capital.GateEnabled;
            sb.Append("<h2 id='portfolio'>Portfolio cash view • selected range</h2><div class='grid'>");
            sb.Append("<div class='card'><div class='label'>Gross withdrawals</div><div class='value gold'>").Append(Html((capital.PayoutCashAfterShare <= 0 ? 0 : capital.PayoutCashAfterShare * 100.0 / Math.Max(0.0001, cfg.PayoutProfitSharePercent)).ToString("C0"))).Append("</div><div class='muted'>Modeled withdrawals before the selected account share.</div></div>");
            sb.Append("<div class='card'><div class='label'>Cash after share • pre-cost</div><div class='value green'>").Append(Html(capital.PayoutCashAfterShare.ToString("C0"))).Append("</div><div class='muted'>Using the selected ").Append(Html(cfg.PayoutProfitSharePercent.ToString("0.#"))).Append("% account share, before modeled evaluation cost.</div></div>");
            sb.Append("<div class='card'><div class='label'>Total eval / replacement cost</div><div class='value orchid'>").Append(Html(capital.TotalEvaluationCost.ToString("C0"))).Append("</div><div class='muted'>All initial and replacement evaluation purchases in this selected range.</div></div>");
            sb.Append("<div class='card'><div class='label'>Full net cash after all costs</div><div class='value ").Append(capital.FullNetCashAfterAllCosts < 0 ? "red" : "green").Append("'>").Append(Html(capital.FullNetCashAfterAllCosts.ToString("C0"))).Append("</div><div class='muted'>Cash after share less every modeled evaluation/replacement cost; not trading P/L.</div></div>");
            if (continuousReplenishment)
            {
                sb.Append("<div class='card'><div class='label'>First payout milestone</div><div class='value ").Append(capital.FirstPayoutReached ? "gold" : "muted").Append("'>").Append(Html(capital.FirstPayoutReached ? capital.FirstPayoutDate.ToString("yyyy-MM-dd") : "NO PAYOUT IN RANGE")).Append("</div><div class='muted'>");
                if (capital.FirstPayoutReached)
                    sb.Append("Cash after share ").Append(Html(capital.PayoutCashThroughFirstPayoutDate.ToString("C0"))).Append(" • investment through first payout ").Append(Html(capital.InvestmentThroughFirstPayoutDate.ToString("C0"))).Append(" • net ").Append(Html(capital.NetCashThroughFirstPayoutDate.ToString("C0"))).Append(".</div></div>");
                else
                    sb.Append("No payout in the selected range. Investment required so far: ").Append(Html(capital.InvestmentThroughFirstPayoutDate.ToString("C0"))).Append(".</div></div>");
                sb.Append("<div class='card'><div class='label'>Replenishment before first payout</div><div class='value orchid'>").Append(capital.ReplacementPurchasesThroughFirstPayoutDate).Append("</div><div class='muted'>Replacement evaluation purchases before first payout • ").Append(capital.ReplenishedSlotsThroughFirstPayoutDate).Append(" unique slot(s) replenished • ").Append(capital.EvaluationPurchasesThroughFirstPayoutDate).Append(" total evaluation purchases by that milestone.</div></div>");
                sb.Append("<div class='card'><div class='label'>First profitable date</div><div class='value ").Append(capital.ProfitabilityReached ? "green" : "red").Append("'>").Append(Html(capital.ProfitabilityReached ? capital.ProfitabilityDate.ToString("yyyy-MM-dd") : "NO PROFITABILITY IN RANGE")).Append("</div><div class='muted'>");
                if (capital.ProfitabilityReached)
                    sb.Append("First date cumulative payout cash covered every modeled cost: cash ").Append(Html(capital.PayoutCashThroughProfitability.ToString("C0"))).Append(" • investment ").Append(Html(capital.InvestmentThroughProfitability.ToString("C0"))).Append(" • net ").Append(Html(capital.NetCashAtProfitability.ToString("C0"))).Append(".</div></div>");
                else
                    sb.Append("Cumulative payout cash has not covered modeled costs in this range. Current full net: ").Append(Html(capital.FullNetCashAfterAllCosts.ToString("C0"))).Append(".</div></div>");
            }
            else
            {
                sb.Append("<div class='card'><div class='label'>Initial eval investment</div><div class='value gold'>").Append(Html(capital.InitialEvaluationInvestment.ToString("C0"))).Append("</div><div class='muted'>").Append(capital.InitialEvaluationPurchases).Append(" initial selected evaluation slot(s) only; excludes replacements.</div></div>");
                sb.Append("<div class='card'><div class='label'>Payout cash after initial investment</div><div class='value ").Append(capital.PayoutCashAfterInitialInvestment < 0 ? "red" : "green").Append("'>").Append(Html(capital.PayoutCashAfterInitialInvestment.ToString("C0"))).Append("</div><div class='muted'>Cash after share less only the initial evaluation investment; excludes later replacement cost.</div></div>");
                sb.Append("<div class='card'><div class='label'>Bench until payout</div><div class='value ").Append(capital.GateEnabled ? "gold" : "muted").Append("'>").Append(capital.GateEnabled ? "ON" : "OFF").Append("</div><div class='muted'>");
                if (capital.GateEnabled)
                    sb.Append("Reinvestment cash ").Append(Html(capital.ReplacementCashAvailable.ToString("C0"))).Append(" • pending slots ").Append(capital.PendingReplacementSlots).Append(" • release requirement ").Append(Html(capital.CashRequiredForPendingReplacements.ToString("C0"))).Append(" • shortfall ").Append(Html(capital.ReplacementCashShortfall.ToString("C0"))).Append(".</div></div>");
                else
                    sb.Append("No evaluation replacement gate applies to this start mode.</div></div>");
            }
            if (cfg.FirmFundedCapEnabled > 0)
            {
                List<KeystoneArcVirtualAccount> firmAccounts = (accountRows ?? new List<KeystoneArcVirtualAccount>());
                int firms = firmAccounts.Where(x => !string.IsNullOrEmpty(x.PropFirmCode)).Select(x => x.PropFirmCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int funded = firmAccounts.Count(x => x.Funded);
                int waiting = firmAccounts.Count(x => x.FundedCapPending);
                sb.Append("<div class='card'><div class='label'>Firm funded capacity</div><div class='value ").Append(waiting > 0 ? "gold" : "green").Append("'>").Append(funded).Append(" / ").Append(firms * cfg.MaxFundedPerFirm).Append("</div><div class='muted'>").Append(firms).Append(" modeled firm(s) • ").Append(cfg.EvaluationSlotsPerFirm).Append(" evaluation slots / firm • ").Append(cfg.MaxFundedPerFirm).Append(" maximum funded / firm • ").Append(waiting).Append(" passed evaluation(s) waiting for firm capacity.</div></div>");
            }
            else sb.Append("<div class='card'><div class='label'>Firm funded capacity</div><div class='value muted'>OFF</div><div class='muted'>No per-firm funded-account limit is modeled for this scenario.</div></div>");
            sb.Append("</div>");
        }

        private static void AppendMetricCard(StringBuilder sb, string label, string value, string color)
        {
            if (sb.ToString().IndexOf("class='grid'", StringComparison.Ordinal) < 0) sb.Append("<div class='grid'>");
            sb.Append("<div class='card'><div class='label'>").Append(Html(label)).Append("</div><div class='value ").Append(Html(color)).Append("'>").Append(Html(value)).Append("</div></div>");
            if (label == "Illustrative evaluation cost") sb.Append("</div>");
        }

        private KeystoneArcRiskSequenceStats CalculateRiskSequenceStats(IEnumerable<KeystoneArcEvent> source, double startingBalance)
        {
            var stats = new KeystoneArcRiskSequenceStats { StartingBalance = Math.Max(0, startingBalance), EndingBalance = Math.Max(0, startingBalance), LowestBalance = Math.Max(0, startingBalance) };
            List<KeystoneArcEvent> ordered = (source ?? Enumerable.Empty<KeystoneArcEvent>()).OrderBy(e => e.EntryTime == DateTime.MinValue ? e.TriggerTime : e.EntryTime).ToList();
            double balance = stats.StartingBalance, peak = balance, currentRunPnl = 0, worstRunPnl = 0;
            int currentRun = 0;
            foreach (KeystoneArcEvent e in ordered)
            {
                balance += e.GrossPnl;
                peak = Math.Max(peak, balance);
                stats.LowestBalance = Math.Min(stats.LowestBalance, balance);
                stats.MaximumDrawdown = Math.Max(stats.MaximumDrawdown, peak - balance);
                if (e.GrossPnl < 0)
                {
                    currentRun++; currentRunPnl += e.GrossPnl;
                    if (currentRun > stats.WorstNegativeSetupStreak || (currentRun == stats.WorstNegativeSetupStreak && currentRunPnl < worstRunPnl)) { stats.WorstNegativeSetupStreak = currentRun; worstRunPnl = currentRunPnl; }
                }
                else { currentRun = 0; currentRunPnl = 0; }
            }
            stats.WorstNegativeSetupStreakPnl = worstRunPnl;
            int dayRun = 0; double dayRunPnl = 0, worstDayPnl = 0;
            foreach (var day in ordered.GroupBy(e => KeystoneArcEngine.SessionGroupingDate(e.TriggerTime, config)).OrderBy(g => g.Key))
            {
                double pnl = day.Sum(e => e.GrossPnl);
                if (pnl < 0)
                {
                    dayRun++; dayRunPnl += pnl;
                    if (dayRun > stats.WorstNegativeDayStreak || (dayRun == stats.WorstNegativeDayStreak && dayRunPnl < worstDayPnl)) { stats.WorstNegativeDayStreak = dayRun; worstDayPnl = dayRunPnl; }
                }
                else { dayRun = 0; dayRunPnl = 0; }
            }
            stats.WorstNegativeDayStreakPnl = worstDayPnl;
            stats.EndingBalance = balance;
            return stats;
        }

        private KeystoneArcRiskSequenceStats CalculateWorstVirtualAccountRisk(double startingBalance)
        {
            KeystoneArcRiskSequenceStats worst = new KeystoneArcRiskSequenceStats { StartingBalance = Math.Max(0, startingBalance), EndingBalance = Math.Max(0, startingBalance), LowestBalance = Math.Max(0, startingBalance) };
            if (accounts == null || accounts.Count == 0) return worst;
            foreach (KeystoneArcVirtualAccount account in accounts)
            {
                KeystoneArcRiskSequenceStats candidate = CalculateRiskSequenceStats(events.Where(e => string.Equals(e.AssignedVirtualAccount, account.Name, StringComparison.OrdinalIgnoreCase)), startingBalance);
                if (candidate.MaximumDrawdown > worst.MaximumDrawdown || (Math.Abs(candidate.MaximumDrawdown - worst.MaximumDrawdown) < 0.0001 && candidate.WorstNegativeSetupStreak > worst.WorstNegativeSetupStreak)) worst = candidate;
            }
            return worst;
        }

        private void AppendComparisonHtml(StringBuilder sb)
        {
            bool live = config != null && config.AccountPath == "PERSONAL";
            sb.Append("<h2>").Append(live ? "Live-account timeframe and instrument comparison" : "Prop virtual-pool timeframe comparison").Append("</h2>");
            if (comparisonRows == null || comparisonRows.Count == 0) sb.Append("<div class='card'>No direct timeframe comparison was built for this export. Use <strong>BUILD 1M–4H COMPARISON</strong> after the normal selected-range load.</div>");
            else
            {
                sb.Append("<div class='card'>Every row uses direct NinjaTrader setup bars and the same verified one-minute outcome safety gate. ").Append(live ? "Live rows use one earliest resolved setup per session date; no evaluation, funded, payout, rotation, or virtual-pool fields are included." : "Prop rows keep raw outcomes separate from virtual allocation and illustrative lifecycle results.").Append("</div>");
                sb.Append("<div class='filters' id='comparisonFilters'><button class='active' onclick=\"setCompare('ALL',this)\">ALL</button><button onclick=\"setCompare('MNQ',this)\">MNQ</button><button onclick=\"setCompare('MGC',this)\">MGC</button><button onclick=\"setCompare('BOTH',this)\">BOTH</button><button onclick=\"setCompare('1M',this)\">1M</button><button onclick=\"setCompare('5M',this)\">5M</button><button onclick=\"setCompare('15M',this)\">15M</button><button onclick=\"setCompare('60M',this)\">60M</button><button onclick=\"setCompare('240M',this)\">4H</button></div><div class='table-wrap'><table id='comparison'><thead><tr>");
                if (live) sb.Append("<th>Instrument</th><th>Timeframe</th><th>Detected</th><th>Final trades</th><th>Raw W/L/exit</th><th>Final live P/L</th><th>Later / unavailable</th><th>Data method</th>");
                else sb.Append("<th>Instrument</th><th>Timeframe</th><th>Start mode</th><th>Accounts</th><th>Setups</th><th>W/L</th><th>Raw gross</th><th>Assigned / skipped</th><th>Assigned gross</th><th>Evaluation passes / funded</th><th>Payouts / account cash / cost</th><th>Data method</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (KeystoneArcComparisonRow r in comparisonRows.OrderByDescending(x => live ? x.AssignedGross : x.PayoutCash - x.EvaluationCost).ThenBy(x => x.Symbol).ThenBy(x => x.SetupMinutes).ThenBy(x => x.PoolSize))
                {
                    sb.Append("<tr data-symbol='").Append(Html(r.Symbol)).Append("' data-timeframe='").Append(r.SetupMinutes).Append("M'><td>").Append(Html(r.Symbol)).Append("</td><td>").Append(r.SetupMinutes).Append("M</td>");
                    if (live) sb.Append("<td>").Append(r.Setups).Append("</td><td>").Append(r.Assigned).Append("</td><td>").Append(r.Wins).Append(" / ").Append(r.Losses).Append(" / ").Append(r.SessionExits).Append("</td><td class='").Append(r.AssignedGross >= 0 ? "green" : "red").Append("'>").Append(Html(r.AssignedGross.ToString("C0"))).Append("</td><td>").Append(r.Skipped).Append("</td><td>").Append(Html(r.DataMethod)).Append("</td>");
                    else sb.Append("<td>").Append(Html(r.StartMode)).Append("</td><td>").Append(r.PoolSize).Append("</td><td>").Append(r.Setups).Append("</td><td>").Append(r.Wins).Append(" / ").Append(r.Losses).Append("</td><td class='").Append(r.AllOutcomeGross >= 0 ? "green" : "red").Append("'>").Append(Html(r.AllOutcomeGross.ToString("C0"))).Append("</td><td>").Append(r.Assigned).Append(" / ").Append(r.Skipped).Append("</td><td class='").Append(r.AssignedGross >= 0 ? "green" : "red").Append("'>").Append(Html(r.AssignedGross.ToString("C0"))).Append("</td><td>").Append(r.EvaluationPassed).Append(" / ").Append(r.Funded).Append("</td><td>").Append(r.Payouts).Append(" / ").Append(Html(r.PayoutCash.ToString("C0"))).Append(" / ").Append(Html(r.EvaluationCost.ToString("C0"))).Append("</td><td>").Append(Html(r.DataMethod)).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            sb.Append("<h2>Target / stop sensitivity • loaded sample only</h2>");
            if (optimizationRows == null || optimizationRows.Count == 0) sb.Append("<div class='card'>No target / stop grid was built for this export. Use <strong>TEST TARGET / STOP GRID</strong> in the comparison workbench. The grid re-resolves the currently loaded timeframe without changing the saved run.</div>");
            else
            {
                sb.Append("<div class='card'><strong>In-sample sensitivity, not a recommendation.</strong> The highest historical result can be overfit. Validate promising settings on separate dates before drawing conclusions.</div><div class='table-wrap'><table><thead><tr><th>Scope</th><th>Timeframe</th><th>" + (live ? "Target move / lot" : "Target") + "</th><th>" + (live ? "Stop model" : "Stop risk") + "</th><th>Detected</th><th>Final trades</th><th>W/L/exit</th><th>Final P/L</th><th>Max drawdown</th><th>Positive days</th>");
                if (!live) sb.Append("<th>Eval passes / funded</th><th>Payouts / account cash / cost</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (KeystoneArcOptimizationRow r in (live ? optimizationRows.OrderByDescending(x => x.FinalPnl).ThenBy(x => x.MaximumDrawdown) : optimizationRows.OrderByDescending(x => x.PayoutCash - x.EvaluationCost).ThenByDescending(x => x.FinalPnl))) { sb.Append("<tr><td>").Append(r.Scope).Append("</td><td>").Append(r.SetupMinutes).Append("M</td><td>").Append(Html(live ? r.TargetLabel : r.TargetDollars.ToString("C0"))).Append("</td><td>").Append(Html(live ? r.StopLabel : r.StopDollars.ToString("C0"))).Append("</td><td>").Append(r.Detected).Append("</td><td>").Append(r.FinalTrades).Append("</td><td>").Append(r.Wins).Append("/").Append(r.Losses).Append("/").Append(r.SessionExits).Append("</td><td class='").Append(r.FinalPnl >= 0 ? "green" : "red").Append("'>").Append(Html(r.FinalPnl.ToString("C0"))).Append("</td><td>").Append(Html(r.MaximumDrawdown.ToString("C0"))).Append("</td><td>").Append(r.PositiveDays).Append("/").Append(r.TotalDays).Append("</td>"); if (!live) sb.Append("<td>").Append(r.EvaluationPasses).Append("/").Append(r.CurrentlyFunded).Append("</td><td>").Append(r.Payouts).Append(" / ").Append(Html(r.PayoutCash.ToString("C0"))).Append(" / ").Append(Html(r.EvaluationCost.ToString("C0"))).Append("</td>"); sb.Append("</tr>"); }
                sb.Append("</tbody></table></div>");
            }
        }

        private string BuildReportRangeSummary()
        {
            bool asian = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            string setups = asian ? "ASIAN CYCLE BACKTEST • MNQ + MGC • exact " + config.AsianStartHhmm.ToString("0000") + " ET start • MNQ " + config.AsianMnqInitialDirection + " / MGC " + config.AsianMgcInitialDirection + " • reverse at the next available 1M open after each configured instrument limit • add one micro per completed leg • individual reversal caps are counted after x1" : "LONG BH • red candle → bullish reference → immediate high break";
            string outcomeModel = asian
                ? "ASIAN BACKTEST MODEL: risk type " + (config.AsianRiskMode == "PRICE" ? "PRICE MOVE (MNQ " + config.AsianMnqReversalPriceMove.ToString("0.####") + ", MGC " + config.AsianMgcReversalPriceMove.ToString("0.####") + ")" : "FIXED CASH " + config.AsianReversalLossDollars.ToString("C0") + " / leg") + " • combined cycle target " + config.AsianCycleTargetDollars.ToString("C0") + " • combined cycle stop " + (config.AsianCombinedStopLossDollars <= 0 ? "OFF" : config.AsianCombinedStopLossDollars.ToString("C0")) + " • combined daily loss limit " + config.AsianDailyLossLimitDollars.ToString("C0") + " • MNQ cap " + (config.AsianMnqInstrumentStopLossDollars <= 0 ? "OFF" : config.AsianMnqInstrumentStopLossDollars.ToString("C0")) + " • MGC cap " + (config.AsianMgcInstrumentStopLossDollars <= 0 ? "OFF" : config.AsianMgcInstrumentStopLossDollars.ToString("C0")) + " • breakeven trigger " + (config.AsianBreakEvenTriggerDollars <= 0 ? "OFF" : config.AsianBreakEvenTriggerDollars.ToString("C0")) + " • initial " + config.AsianStartingQuantity + " micro(s) • MNQ maximum " + config.AsianMnqMaxReversals + " / MGC maximum " + config.AsianMgcMaxReversals + " reversal(s) after x1 • combined boundaries 1M-close confirmed"
                : "OUTCOME MODEL: target " + config.TargetDollars.ToString("C0") + " • maximum stop risk " + config.StopDollars.ToString("C0") + " • stop mode " + config.StopMode + " • fixed quantity " + config.Quantity + " micro(s) when STANDARD is selected • stop-first if both occur in the same available outcome bar";
            string lifecycle = config.EvaluationEnabled == -2
                ? "SINGLE PROP ACCOUNT: starting balance " + config.PropStartingBalance.ToString("C0") + " • range P/L only • no evaluation, payout, replacement, or funded lifecycle"
                : (config.EvaluationEnabled < 0 ? "VIRTUAL POOL: one-day " + (asian ? "copy" : "assignment") + " only" : (config.EvaluationEnabled == 0 ? "VIRTUAL POOL: direct-funded illustrative " + (asian ? "copy" : "start") : "VIRTUAL POOL: illustrative evaluation-first " + (asian ? "copy" : "scenario")));
            return "TESTED SESSION DATE(S): " + SessionDateLabel(config) + "\n" +
                "HISTORICAL DATA WINDOW: " + config.Start.ToString("yyyy-MM-dd HH:mm") + " → " + config.End.ToString("yyyy-MM-dd HH:mm") + "\n" +
                "INSTRUMENT SCOPE: " + config.Scope + "\n" +
                "ACCOUNT PATH: PROP VIRTUAL POOL • illustrative lifecycle scenario\n" +
                "SESSION FILTER: " + config.SessionMode + "\n" +
                "SETUP TIMEFRAME: " + config.SetupMinutes + " minute(s) • SETUPS: " + setups + "\n" +
                outcomeModel + "\n" +
                lifecycle + "\n" +
                (config.EvaluationEnabled > 0 ? "EVAL SETTINGS: target " + config.EvaluationTarget.ToString("C0") + " • max profit/day " + config.EvaluationDailyCreditCap.ToString("C0") + " • daily loss " + (config.EvaluationDailyLoss <= 0 ? "OFF" : config.EvaluationDailyLoss.ToString("C0")) + " • total drawdown " + config.EvaluationFailure.ToString("C0") + " • consecutive qualifying days " + config.MinimumPositiveDays + " • consistency " + (config.EvaluationConsistencyPercent <= 0 ? "OFF" : config.EvaluationConsistencyPercent.ToString("0.##") + "%") + " • cost " + config.EvaluationCost.ToString("C0") + (config.EvaluationStageTradeRulesEnabled > 0 ? " • BH eval trade terms target " + config.EvaluationTradeTargetDollars.ToString("C0") + " / stop " + config.EvaluationTradeStopDollars.ToString("C0") : " • funded/base target-stop terms used during evaluation") + "\n" : string.Empty) +
                (config.EvaluationEnabled > 0 ? "FIRM FUNDED CAP: " + (config.FirmFundedCapEnabled > 0 ? "ON • " + config.EvaluationSlotsPerFirm + " evaluation slots per modeled firm • maximum " + config.MaxFundedPerFirm + " funded slots per modeled firm • passed evaluations wait until that same firm has funded capacity" : "OFF • no per-firm funded-slot limit is modeled") + "\n" : string.Empty) +
                (config.EvaluationEnabled > 0 ? "REPLACEMENT CASH POLICY: " + (config.ReplacementsRequirePayoutFunding > 0 ? "ON • failed slots remain benched until modeled payout cash first recovers the initial selected evaluation investment and then funds every currently pending replacement; no trading P/L is treated as cash" : "OFF • replacement evaluation is purchased at the next session after a failure") + "\n" : string.Empty) +
                (config.EvaluationEnabled >= 0 ? "FUNDED / PAYOUT SETTINGS: ready balance " + config.PayoutThreshold.ToString("C0") + " • withdraw " + config.PayoutAmount.ToString("C0") + " • qualifying days " + config.PayoutDaysRequired + " at least " + config.MinimumQualifyingDayProfit.ToString("C0") + " / day • daily loss " + (config.FundedDailyLoss <= 0 ? "OFF" : config.FundedDailyLoss.ToString("C0")) + " • total drawdown " + config.FundedFailure.ToString("C0") : string.Empty);
        }

        private static string LiveOutcomeModelSummary(KeystoneArcRunConfig cfg)
        {
            if (cfg == null) return "LIVE MODEL: unavailable";
            string target = "NAS100 +" + cfg.MnqTargetMove.ToString("0.####", CultureInfo.InvariantCulture) + " price move • GOLD +" + cfg.MgcTargetMove.ToString("0.####", CultureInfo.InvariantCulture) + " price move";
            string stop;
            if (string.Equals(cfg.StopMode, "STANDARD", StringComparison.OrdinalIgnoreCase))
                stop = "standard stop: NAS100 -" + cfg.MnqStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture) + " • GOLD -" + cfg.MgcStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture) + " price move";
            else
                stop = "three-candle-low stop: NAS100 offset " + cfg.MnqStopOffsetPoints.ToString("0.####", CultureInfo.InvariantCulture) + " • GOLD offset " + cfg.MgcStopOffsetPoints.ToString("0.####", CultureInfo.InvariantCulture) + (string.Equals(cfg.StopMode, "LIVE_LOW_AUTO_RISK", StringComparison.OrdinalIgnoreCase) ? " • auto-risk cap " + cfg.PersonalMaxRiskDollars.ToString("C0") : " • fixed lot");
            string breakeven = cfg.BreakEvenEnabled > 0 && cfg.BreakEvenTriggerMove > 0 ? " • breakeven after +" + cfg.BreakEvenTriggerMove.ToString("0.####", CultureInfo.InvariantCulture) + " price move" : string.Empty;
            return "LIVE LOT / PRICE MODEL: lot " + cfg.PersonalLotSize.ToString("0.####", CultureInfo.InvariantCulture) + " • NAS100 cash " + cfg.MnqCashPerPointPerLot.ToString("C0") + " per point / 1.00 lot • GOLD cash " + cfg.MgcCashPerPointPerLot.ToString("C0") + " per $1 / 1.00 lot • " + target + " • " + stop + breakeven + " • stop-first if both occur in the same available outcome bar";
        }

        private static string LiveTargetRiskCardValue(KeystoneArcRunConfig cfg)
        {
            if (cfg == null) return "RUN RESULTS";
            string target = "NQ +" + cfg.MnqTargetMove.ToString("0.####", CultureInfo.InvariantCulture) + " / GC +" + cfg.MgcTargetMove.ToString("0.####", CultureInfo.InvariantCulture);
            string stop = string.Equals(cfg.StopMode, "STANDARD", StringComparison.OrdinalIgnoreCase)
                ? "STD NQ -" + cfg.MnqStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture) + " / GC -" + cfg.MgcStandardStopMove.ToString("0.####", CultureInfo.InvariantCulture)
                : "3-CANDLE LOW";
            return target + " • " + stop + " • " + cfg.PersonalLotSize.ToString("0.####", CultureInfo.InvariantCulture) + " LOT";
        }

        private static string BhAggressionSummary(KeystoneArcRunConfig cfg)
        {
            if (cfg == null || !string.Equals(cfg.BhAggressionFilter, "STRONGER", StringComparison.OrdinalIgnoreCase)) return "all valid base BH setups";
            string mnq = "MNQ red candles ≥ " + cfg.MnqStrongRedCandles + ", decline ≥ " + cfg.MnqStrongDeclinePoints.ToString("0.##", CultureInfo.InvariantCulture) + " points";
            string mgc = "MGC red candles ≥ " + cfg.MgcStrongRedCandles + ", decline ≥ " + cfg.MgcStrongDeclineDollars.ToString("0.##", CultureInfo.InvariantCulture) + " dollars";
            return "stronger only • " + (cfg.BhStrongCombine == "ALL" ? "all enabled criteria" : "any enabled criterion") + " • " + mnq + " • " + mgc;
        }

        private static void AppendSelectedSessionDailyHtml(StringBuilder sb, List<KeystoneArcEvent> all, KeystoneArcRunConfig cfg, bool outcomesVerified)
        {
            if (cfg == null || cfg.Start == DateTime.MinValue || cfg.End == DateTime.MinValue) return;
            DateTime first = KeystoneArcEngine.SessionGroupingDate(cfg.Start, cfg).Date;
            DateTime last = KeystoneArcEngine.SessionGroupingDate(cfg.End, cfg).Date;
            string session = (cfg.SessionMode ?? "SELECTED SESSION").Replace("_", " ");
            var rows = new List<KeyValuePair<DateTime, List<KeystoneArcEvent>>>();
            for (DateTime day = first; day <= last; day = day.AddDays(1)) rows.Add(new KeyValuePair<DateTime, List<KeystoneArcEvent>>(day, (all ?? new List<KeystoneArcEvent>()).Where(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, cfg) == day).ToList()));
            double bestPositive = outcomesVerified ? rows.Where(x => x.Value.Count > 0).Select(x => x.Value.Sum(e => e.GrossPnl)).Where(x => x > 0).DefaultIfEmpty(0).Max() : 0;
            sb.Append("<h2 id='daily-session'>Daily selected-session scoreboard • ").Append(Html(session)).Append("</h2><div class='card'>One row for every requested session date. This is the <strong>session loaded for this run</strong>; compare another session by running it as its own data study or through the comparison workbench. The green row is the best positive daily result within this selected session.</div>");
            sb.Append("<div class='table-wrap'><table><thead><tr><th>Session date</th><th>Selected session</th><th>MNQ setups</th><th>MGC setups</th><th>Total setups</th><th>Wins</th><th>Losses</th><th>Session exits</th><th>Model P/L</th><th>Daily flag</th></tr></thead><tbody>");
            foreach (KeyValuePair<DateTime, List<KeystoneArcEvent>> pair in rows)
            {
                List<KeystoneArcEvent> dayRows = pair.Value;
                double pnl = dayRows.Sum(x => x.GrossPnl);
                bool best = outcomesVerified && pnl > 0 && Math.Abs(pnl - bestPositive) < 0.0001;
                sb.Append("<tr class='").Append(best ? "win" : (outcomesVerified && pnl < 0 ? "loss" : "")).Append("'><td>").Append(pair.Key.ToString("yyyy-MM-dd")).Append("</td><td>").Append(Html(session)).Append("</td><td>").Append(dayRows.Count(x => string.Equals(x.Symbol, "MNQ", StringComparison.OrdinalIgnoreCase))).Append("</td><td>").Append(dayRows.Count(x => string.Equals(x.Symbol, "MGC", StringComparison.OrdinalIgnoreCase))).Append("</td><td>").Append(dayRows.Count).Append("</td><td class='green'>").Append(outcomesVerified ? dayRows.Count(x => x.Outcome == "WIN").ToString() : "—").Append("</td><td class='red'>").Append(outcomesVerified ? dayRows.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase)).ToString() : "—").Append("</td><td>").Append(outcomesVerified ? dayRows.Count(x => x.Outcome == "SESSION EXIT").ToString() : "—").Append("</td><td class='").Append(!outcomesVerified ? "gold" : (pnl < 0 ? "red" : "green")).Append("'>").Append(Html(outcomesVerified ? pnl.ToString("C0") : "OUTCOME BLOCKED")).Append("</td><td class='").Append(best ? "green" : "muted").Append("'>").Append(best ? "BEST POSITIVE DAILY SESSION" : (dayRows.Count == 0 ? "NO SETUP" : "—")).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        private static void AppendPayoutAccountHtml(StringBuilder sb, List<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            List<KeystoneArcVirtualAccount> paid = (source ?? new List<KeystoneArcVirtualAccount>()).Where(x => x.Payouts > 0).OrderBy(x => KeystoneArcEngine.GetFirstPayoutTiming(x).FirstPayoutDate).ThenBy(x => x.Name).ToList();
            sb.Append("<h2 id='payout-accounts'>Payout Accounts • account profitability and period evidence</h2><div class='card'>This view contains only accounts with payout history. It keeps the account’s start, current stage balance, gross withdrawal, selected-share cash, all modeled costs, full net cash, first/last payout dates, post-payout blowout date, and recorded daily/weekly/monthly period facts separate from the payout-date aggregate.</div>");
            if (paid.Count == 0) { sb.Append("<div class='card'>No payout-history accounts exist in this selected range.</div>"); return; }
            sb.Append("<div class='table-wrap'><table><thead><tr><th>Account / current stage</th><th>Start / current balance</th><th>First / last payout</th><th>Payouts</th><th>Gross / cash after share / all cost / full net</th><th>First-return timing</th><th>Recorded daily / weekly / monthly facts</th><th>Post-payout blowout</th></tr></thead><tbody>");
            foreach (KeystoneArcVirtualAccount a in paid)
            {
                KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(a);
                List<KeystoneArcAccountDay> days = a.DayHistory.OrderBy(x => x.Day).ToList();
                var weeks = days.GroupBy(x => x.Day.AddDays(-((7 + (int)x.Day.DayOfWeek - 1) % 7)).Date).ToList();
                var months = days.GroupBy(x => new DateTime(x.Day.Year, x.Day.Month, 1)).ToList();
                double bestDay = days.Count == 0 ? 0 : days.Max(x => x.DayPnl);
                double bestWeek = weeks.Count == 0 ? 0 : weeks.Max(x => x.Sum(d => d.DayPnl));
                double bestMonth = months.Count == 0 ? 0 : months.Max(x => x.Sum(d => d.DayPnl));
                double balance = cfg != null && cfg.EvaluationEnabled == -2 ? a.StartingBalance + a.TotalPnl : (a.Funded || (a.Blown && a.FailedFunded > 0) || (a.ReplacementPending && a.ReplacementFromFunded) ? a.FundedBalance : a.EvaluationBalance);
                double net = a.PayoutCash - a.EvaluationCost;
                sb.Append("<tr><td><strong>").Append(Html(a.Name)).Append("</strong><br><span class='gold'>").Append(Html(AccountSnapshotState(a))).Append("</span></td><td>").Append(a.InitialLifecycleStart == DateTime.MinValue ? "—" : a.InitialLifecycleStart.ToString("yyyy-MM-dd")).Append("<br>").Append(Html(balance.ToString("C0"))).Append("</td><td>").Append(first.HasPayout ? first.FirstPayoutDate.ToString("yyyy-MM-dd") : "—").Append("<br>").Append(Html(LastPayoutDate(a))).Append("</td><td class='gold'>").Append(a.Payouts).Append("</td><td>Gross ").Append(Html(a.PayoutGrossWithdrawn.ToString("C0"))).Append("<br><span class='green'>Cash ").Append(Html(a.PayoutCash.ToString("C0"))).Append("</span><br><span class='orchid'>Cost ").Append(Html(a.EvaluationCost.ToString("C0"))).Append("</span><br><strong class='").Append(net < 0 ? "red" : "green").Append("'>Full net ").Append(Html(net.ToString("C0"))).Append("</strong></td><td>").Append(first.HasPayout ? first.CalendarDaysFromInitial + " calendar days<br>" + first.RecordedSessionsFromInitial + " recorded sessions" : "—").Append("</td><td>").Append(days.Count).Append(" daily records • best ").Append(Html(bestDay.ToString("C0"))).Append("<br>").Append(weeks.Count).Append(" weekly records • best ").Append(Html(bestWeek.ToString("C0"))).Append("<br>").Append(months.Count).Append(" monthly records • best ").Append(Html(bestMonth.ToString("C0"))).Append("</td><td class='").Append(a.LastBlowoutDate == DateTime.MinValue ? "muted" : "red").Append("'>").Append(a.LastBlowoutDate == DateTime.MinValue ? "NONE" : a.LastBlowoutDate.ToString("yyyy-MM-dd")).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        private static void AppendPayoutCycleHtml(StringBuilder sb, List<KeystoneArcPayoutCycleRow> cycles, List<KeystoneArcBalanceMatrixRow> matrix, KeystoneArcRunConfig cfg)
        {
            sb.Append("<h2 id='payout-cycles'>Payout Cycle Dashboard • dated cash and account-state checkpoints</h2>");
            sb.Append("<div class='card'><strong>Definition.</strong> Each row is one date on which one or more virtual accounts recorded a modeled payout. <strong>Cost on date</strong> is only the purchases recorded that date, while <strong>all cost through payout date</strong> includes the initial evaluation purchases (including the first $120-per-slot assumption) plus replacements up to that date. <strong>Full net through payout date</strong> equals cumulative payout cash after the selected account share less that cumulative modeled cost. These are scenario-accounting views, not trading P/L, a firm statement, or a payout promise.</div>");
            if (cycles == null || cycles.Count == 0)
            {
                sb.Append("<div class='card'>No modeled payout date occurred in this selected range. Account snapshots and daily lifecycle records remain available below.</div>");
                return;
            }
            sb.Append("<div class='filters'><label class='pill'>Month <select id='cycleMonth' onchange='filterCycles()'><option value='ALL'>ALL</option>");
            foreach (string month in cycles.Select(x => x.Day.ToString("yyyy-MM")).Distinct().OrderBy(x => x)) sb.Append("<option value='").Append(month).Append("'>").Append(month).Append("</option>");
            sb.Append("</select></label><label class='pill'>Payout date <select id='cycleDate' onchange='filterCycles()'><option value='ALL'>ALL</option>");
            foreach (KeystoneArcPayoutCycleRow cycle in cycles) sb.Append("<option value='").Append(cycle.Day.ToString("yyyy-MM-dd")).Append("'>").Append(cycle.Day.ToString("yyyy-MM-dd")).Append("</option>");
            sb.Append("</select></label><span class='pill'>Scope: ").Append(Html(cfg.Scope)).Append(" • session: ").Append(Html(cfg.SessionMode)).Append(" • timeframe: ").Append(cfg.SetupMinutes).Append("M</span></div>");
            sb.Append("<div id='payoutMonthSummary'><h3>Monthly payout summary</h3><div class='card'>These cards show only payout dates inside each displayed month. <strong>Accounts paid</strong> is unique accounts in that month. <strong>Net after month costs</strong> is cash after share less evaluation/replacement costs recorded on those payout dates; it is not a cumulative all-range balance.</div>");
            foreach (IGrouping<string, KeystoneArcPayoutCycleRow> monthGroup in cycles.GroupBy(x => x.Day.ToString("yyyy-MM")).OrderBy(x => x.Key))
            {
                List<KeystoneArcPayoutCycleRow> monthRows = monthGroup.ToList();
                int monthPayoutCycles = monthRows.Sum(x => x.PayoutCycles);
                int monthAccounts = monthRows.SelectMany(x => x.PayoutContributors ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                double monthCash = monthRows.Sum(x => x.NetCashAfterShare);
                double monthNet = monthRows.Sum(x => x.NetCashAfterEvaluationCost);
                sb.Append("<div class='card' data-cycle-month-summary='").Append(monthGroup.Key).Append("'><strong>").Append(monthGroup.Key).Append(" • PAYOUT MONTH</strong><br>")
                    .Append("Total payouts <span class='gold'>").Append(monthPayoutCycles).Append("</span> • accounts paid <span class='blue'>").Append(monthAccounts).Append("</span> • cash after share <span class='green'>").Append(Html(monthCash.ToString("C0"))).Append("</span> • net after month costs <span class='").Append(monthNet < 0 ? "red" : "green").Append("'>").Append(Html(monthNet.ToString("C0"))).Append("</span></div>");
            }
            sb.Append("</div>");
            sb.Append("<div class='table-wrap'><table id='payoutCycles'><thead><tr><th>#</th><th>Payout date</th><th>Accounts paid / cycles</th><th>Gross withdrawal</th><th>Cash after share • this date</th><th>Cost on date<br>purchases / cost</th><th>All cost through payout date</th><th>Full net through payout date</th><th>Close-state counts<br>funded / below goal / eval / replacement / ended</th><th>Paying accounts</th></tr></thead><tbody>");
            foreach (KeystoneArcPayoutCycleRow cycle in cycles)
            {
                sb.Append("<tr data-month='").Append(cycle.Day.ToString("yyyy-MM")).Append("' data-date='").Append(cycle.Day.ToString("yyyy-MM-dd")).Append("'><td>").Append(cycle.CycleNumber).Append("</td><td>").Append(cycle.Day.ToString("yyyy-MM-dd")).Append("</td><td>").Append(cycle.PayoutAccounts).Append(" / ").Append(cycle.PayoutCycles).Append("</td><td class='gold'>").Append(Html(cycle.GrossWithdrawals.ToString("C0"))).Append("</td><td class='green'>").Append(Html(cycle.NetCashAfterShare.ToString("C0"))).Append("</td><td>").Append(cycle.EvaluationPurchases).Append(" / <span class='orchid'>").Append(Html(cycle.EvaluationCostOnDate.ToString("C0"))).Append("</span></td><td class='orchid'>").Append(Html(cycle.CumulativeEvaluationCostThroughDate.ToString("C0"))).Append("</td><td class='").Append(cycle.CumulativeFullNetCashAfterAllCosts < 0 ? "red" : "green").Append("'>").Append(Html(cycle.CumulativeFullNetCashAfterAllCosts.ToString("C0"))).Append("</td><td>").Append(cycle.FundedAtClose).Append(" / ").Append(cycle.FundedBelowPayoutGoal).Append(" / ").Append(cycle.EvaluationInProgress).Append(" / ").Append(cycle.ReplacementNextSession).Append(" / ").Append(cycle.TerminalBlown).Append("</td><td>").Append(Html(string.Join(", ", cycle.PayoutContributors))).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<details class='audit'><summary>Open balance matrix • every account at each payout date</summary><div class='inside'><h3>Balance Matrix</h3><div class='card'>A paid account’s balance before equals its funded balance before the modeled withdrawal. Carry-forward is the recorded stage balance after the date. Non-paid accounts are shown as funded below goal, evaluation, replacement next session, or terminally blown based on their daily audit snapshot.</div>");
            sb.Append("<div class='table-wrap'><table id='balanceMatrix'><thead><tr><th>Cycle</th><th>Payout date</th><th>Account</th><th>Status</th><th>Balance before</th><th>Gross payout</th><th>Carry-forward balance</th><th>Day P/L</th></tr></thead><tbody>");
            foreach (KeystoneArcBalanceMatrixRow row in matrix ?? new List<KeystoneArcBalanceMatrixRow>())
            {
                string color = row.Status == "PAID" ? "green" : (row.Status.Contains("BLOWN") ? "red" : (row.Status.Contains("REPLACEMENT") ? "orchid" : "cyan"));
                sb.Append("<tr data-matrix-month='").Append(row.Day.ToString("yyyy-MM")).Append("' data-matrix-date='").Append(row.Day.ToString("yyyy-MM-dd")).Append("'><td>").Append(row.CycleNumber).Append("</td><td>").Append(row.Day.ToString("yyyy-MM-dd")).Append("</td><td>").Append(Html(row.Account)).Append("</td><td class='").Append(color).Append("'>").Append(Html(row.Status)).Append("</td><td>").Append(Html(row.BalanceBefore.ToString("C0"))).Append("</td><td class='gold'>").Append(Html(row.GrossPayout.ToString("C0"))).Append("</td><td>").Append(Html(row.CarryForwardBalance.ToString("C0"))).Append("</td><td class='").Append(row.DayPnl < 0 ? "red" : "green").Append("'>").Append(Html(row.DayPnl.ToString("C0"))).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></div></details>");
        }

        private static string AccountSnapshotState(KeystoneArcVirtualAccount a)
        {
            if (a == null) return "UNKNOWN";
            if (a.Blown && !a.ReplacementPending) return "BLOWN / ENDED";
            if (a.ReplacementPending && a.ReplacementBudgetBlocked) return "BENCHED / PAYOUT GATE";
            if (a.ReplacementPending) return "REPLACEMENT NEXT";
            if (a.FundedCapPending) return "EVAL PASSED / FIRM CAP WAIT";
            if (a.Payouts > 0) return "PAYOUT HISTORY";
            if (a.Funded) return "FUNDED";
            return "EVALUATION";
        }

        private static void AppendFirstReturnHtml(StringBuilder sb, List<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            List<KeystoneArcFirstReturnRow> rows = KeystoneArcEngine.BuildFirstReturnRows(source).Where(x => x.HasPayout).OrderBy(x => x.FirstPayoutDate).ThenBy(x => x.Account).ToList();
            string startLabel = cfg != null && cfg.EvaluationEnabled == 0 ? "initial funded-slot start" : "initial evaluation purchase/start";
            sb.Append("<h2 id='first-return'>First Return Dashboard • paid accounts only</h2><div class='card'><strong>Definition.</strong> Only accounts that actually reached a first payout are listed. Each is measured from its ").Append(Html(startLabel)).Append(" at the beginning of the selected study range. <strong>First payout cash</strong> is only that first modeled cash payment after the selected share. <strong>Full net at first return</strong> is payout cash through that first date less every modeled initial/replacement evaluation cost incurred through that date; it is not trading P/L or a payout promise.</div>");
            if (rows.Count == 0)
            {
                sb.Append("<div class='card'>No accounts reached a first payout in this selected range. Unpaid accounts remain traceable in the account snapshot and daily lifecycle ledger.</div>");
                return;
            }
            sb.Append("<div class='table-wrap'><table><thead><tr><th>Account</th><th>").Append(Html(startLabel)).Append("</th><th>First payout date</th><th>Time to first cash</th><th>First gross withdrawal</th><th>First payout cash after share</th><th>Cost through first return</th><th>Full net at first return</th><th>Status</th></tr></thead><tbody>");
            foreach (KeystoneArcFirstReturnRow row in rows)
                sb.Append("<tr class='win'><td>").Append(Html(row.Account)).Append("</td><td>").Append(row.InitialSlotStart.ToString("yyyy-MM-dd")).Append("</td><td class='gold'>").Append(row.FirstPayoutDate.ToString("yyyy-MM-dd")).Append("</td><td>").Append(row.CalendarDaysFromInitial).Append(" calendar days / ").Append(row.RecordedSessionsFromInitial).Append(" sessions</td><td class='gold'>").Append(Html(row.FirstPayoutGross.ToString("C0"))).Append("</td><td class='green'>").Append(Html(row.FirstPayoutCashAfterShare.ToString("C0"))).Append("</td><td class='orchid'>").Append(Html(row.EvaluationCostThroughFirstPayout.ToString("C0"))).Append("</td><td class='").Append(row.FullNetReturnAfterAllCosts < 0 ? "red" : "green").Append("'>").Append(Html(row.FullNetReturnAfterAllCosts.ToString("C0"))).Append("</td><td>").Append(Html(row.StateAtFirstPayout)).Append("</td></tr>");
            sb.Append("</tbody></table></div>");
        }

        private static void AppendAccountProgressHtml(StringBuilder sb, List<KeystoneArcVirtualAccount> source, KeystoneArcRunConfig cfg)
        {
            sb.Append("<h2 id='accounts'>").Append(cfg != null && cfg.EvaluationEnabled < 0 && cfg.EvaluationEnabled != -2 ? "One-day virtual account allocation" : "Virtual account balance and progression • illustrative scenario").Append("</h2>");
            if (source == null || source.Count == 0)
            {
                sb.Append("<div class='card'>No virtual pool has been run. The event tables above are the complete detected outcome ledger; choose which rows to include, then run the virtual pool to create account balances and day-by-day progression.</div>");
                return;
            }
            if (cfg != null && cfg.EvaluationEnabled < 0 && cfg.EvaluationEnabled != -2)
            {
                sb.Append("<div class='card'><strong>One-day allocation only.</strong> These rows show the actual account assignment, trades, win/loss count, day P/L, and selected daily lock. Evaluation, payout, cost, replacement, and blowout fields are intentionally not shown because they are not applied in this mode.</div>");
                sb.Append("<div class='filters'><input id='accountSearch' oninput='filterAccounts()' placeholder='Search account' style='padding:7px;background:#111b2d;color:#eef4fc;border:1px solid #2cd7cb;border-radius:7px'><label class='pill'>Result <select id='accountState' onchange='filterAccounts()'><option value='ALL'>ALL</option><option value='DAILY PROFIT LOCK'>PROFIT LOCK</option><option value='DAILY LOSS LOCK'>LOSS LOCK</option><option value='TRADED'>TRADED / UNLOCKED</option><option value='UNUSED'>UNUSED</option></select></label><span class='pill'>Filters display existing rows only; they do not rerun the historical study.</span></div>");
                sb.Append("<div class='table-wrap'><table id='accountSnapshot'><thead><tr><th>Account</th><th>One-day status</th><th>Assigned trades</th><th>W / L</th><th>Assigned day P/L</th><th>Daily profit / loss lock</th><th>Assigned session date</th></tr></thead><tbody>");
                foreach (KeystoneArcVirtualAccount a in source)
                {
                    string state = a.Trades <= 0 ? "UNUSED" : (a.DayLocked && a.DayPnl >= Math.Max(0, cfg.DailyGoal) ? "DAILY PROFIT LOCK" : (a.DayLocked ? "DAILY LOSS LOCK" : "TRADED"));
                    string color = state == "DAILY PROFIT LOCK" ? "green" : (state == "DAILY LOSS LOCK" ? "red" : (state == "UNUSED" ? "muted" : "cyan"));
                    sb.Append("<tr data-account-state='").Append(Html(state)).Append("' data-account='").Append(Html(a.Name.ToUpperInvariant())).Append("'><td>").Append(Html(a.Name)).Append("</td><td class='").Append(color).Append("'>").Append(Html(state)).Append("</td><td>").Append(a.Trades).Append("</td><td><span class='green'>").Append(a.Wins).Append("W</span> / <span class='red'>").Append(a.Losses).Append("L</span></td><td class='").Append(a.DayPnl < 0 ? "red" : (a.DayPnl > 0 ? "green" : "muted")).Append("'>").Append(Html(a.DayPnl.ToString("C0"))).Append("</td><td>").Append(Html(cfg.DailyGoal.ToString("C0"))).Append(" / -").Append(Html(Math.Abs(cfg.DailyLoss).ToString("C0"))).Append("</td><td>").Append(a.LastAssignedDate == DateTime.MinValue ? "—" : a.LastAssignedDate.ToString("yyyy-MM-dd")).Append("</td></tr>");
                }
                sb.Append("</tbody></table></div>");
                return;
            }
            sb.Append("<div class='card'><strong>Read these columns separately.</strong> <strong>Assigned-ledger P/L</strong> is every trade assigned to that virtual slot over the full test. <strong>Current stage balance</strong> resets to $0 when a funded account fails and a replacement evaluation starts; it is not meant to equal lifetime P/L. An evaluation pass requires <strong>").Append(cfg.MinimumPositiveDays).Append(" consecutive qualifying sessions</strong> at least the higher of the daily target or minimum qualifying-day input. A replacement is purchased only at the next session after a total-drawdown failure.</div>");
            if (cfg.EvaluationEnabled > 0 && source.Sum(a => a.Payouts) == 0)
            {
                int passes = source.Sum(a => a.EvaluationPasses);
                int funded = source.Count(a => a.Funded);
                string why = passes == 0
                    ? "No payout was modeled because no virtual slot completed the selected evaluation gate."
                    : (funded == 0 ? "No payout was modeled because no virtual slot is currently funded." : "No payout was modeled because funded slots have not yet met both the selected payout balance and qualifying-day gates.");
                sb.Append("<div class='card'><strong>Payout result: $0.</strong> ").Append(Html(why)).Append(" Selected gates: evaluation target ").Append(Html(cfg.EvaluationTarget.ToString("C0"))).Append(", ").Append(cfg.MinimumPositiveDays).Append(" consecutive evaluation day(s), payout balance ").Append(Html(cfg.PayoutThreshold.ToString("C0"))).Append(", and ").Append(cfg.PayoutDaysRequired).Append(" funded qualifying day(s).</div>");
            }
            sb.Append("<div class='filters'><input id='accountSearch' oninput='filterAccounts()' placeholder='Search account' style='padding:7px;background:#111b2d;color:#eef4fc;border:1px solid #2cd7cb;border-radius:7px'><label class='pill'>State <select id='accountState' onchange='filterAccounts()'><option value='ALL'>ALL</option><option value='FUNDED'>FUNDED</option><option value='PAYOUT HISTORY'>PAYOUT HISTORY</option><option value='EVALUATION'>EVALUATION</option><option value='EVAL PASSED / FIRM CAP WAIT'>FIRM CAP WAIT</option><option value='REPLACEMENT NEXT'>REPLACEMENT NEXT</option><option value='BENCHED / PAYOUT GATE'>BENCHED / PAYOUT GATE</option><option value='BLOWN / ENDED'>BLOWN / ENDED</option></select></label><span class='pill'>Filters display existing account rows only; they do not rerun a different instrument, session, timeframe, or contract.</span></div>");
            sb.Append("<div class='table-wrap'><table id='accountSnapshot'><thead><tr><th>Account / modeled firm</th><th>Current state</th><th>Lifecycle dates / blowout events</th><th>First payout / time from initial start</th><th>Cumulative model P/L</th><th>Current lifecycle balance</th><th>Trades W / L</th><th>Evaluation progress</th><th>Funded / payout progress</th><th>Gross / share cash / all cost / full net cash</th></tr></thead><tbody>");
            for (int i = 0; i < source.Count; i++)
            {
                KeystoneArcVirtualAccount a = source[i];
                string eval;
                string funded;
                if (cfg.EvaluationEnabled < 0)
                {
                    eval = "One-day assignment only";
                    funded = "Lifecycle not applied";
                }
                else if (!a.Funded)
                {
                    double evalLeft = Math.Max(0, cfg.EvaluationTarget - a.EvaluationBalance);
                    int daysLeft = Math.Max(0, cfg.MinimumPositiveDays - a.ConsecutiveEvalQualifyingDays);
                    eval = "Balance " + a.EvaluationBalance.ToString("C0") + " • left " + evalLeft.ToString("C0") + "<br>Consecutive qualifying days " + a.ConsecutiveEvalQualifyingDays + "/" + cfg.MinimumPositiveDays + " • left " + daysLeft + "<br>Passes completed " + a.EvaluationPasses;
                    funded = a.ReplacementPending ? "Replacement evaluation starts next session" : "Not funded yet";
                }
                else
                {
                    int payoutDaysLeft = Math.Max(0, cfg.PayoutDaysRequired - a.FundedPositiveDays);
                    double payoutBalanceLeft = Math.Max(0, cfg.PayoutThreshold - a.FundedBalance);
                    eval = "Passed / direct-funded start";
                    funded = "Balance " + a.FundedBalance.ToString("C0") + " • left " + payoutBalanceLeft.ToString("C0") + "<br>Qualifying days " + a.FundedPositiveDays + "/" + cfg.PayoutDaysRequired + " • left " + payoutDaysLeft;
                }
                double liveBalance = cfg.EvaluationEnabled < 0 ? a.TotalPnl : (a.Funded ? a.FundedBalance : a.EvaluationBalance);
                KeystoneArcFirstPayoutTiming timing = KeystoneArcEngine.GetFirstPayoutTiming(a);
                string state = AccountSnapshotState(a);
                string payoutTiming = timing.HasPayout ? timing.FirstPayoutDate.ToString("yyyy-MM-dd") + "<br>" + timing.CalendarDaysFromInitial + " calendar days / " + timing.RecordedSessionsFromInitial + " recorded sessions" : "No payout recorded";
                double netAfterCost = a.PayoutCash - a.EvaluationCost;
                string life = LifecycleDateSummary(a) + " • blowout events " + (a.FailedEvaluations + a.FailedFunded);
                string firm = cfg.FirmFundedCapEnabled > 0 ? a.PropFirmCode + " • slot " + a.PropFirmSlot + " • funded " + source.Count(x => x.Funded && string.Equals(x.PropFirmCode, a.PropFirmCode, StringComparison.OrdinalIgnoreCase)) + "/" + cfg.MaxFundedPerFirm : "Firm cap off";
                sb.Append("<tr data-account-state='").Append(Html(state)).Append("' data-account='").Append(Html(a.Name.ToUpperInvariant())).Append("'><td>").Append(Html(a.Name)).Append("<br><span class='gold'>").Append(Html(firm)).Append("</span></td><td>").Append(Html(state)).Append("<br><span class='muted'>").Append(Html(a.LastState)).Append("</span></td><td class='").Append(a.LastBlowoutDate == DateTime.MinValue ? "cyan" : "red").Append("'>").Append(Html(life)).Append("</td><td class='").Append(timing.HasPayout ? "gold" : "muted").Append("'>").Append(payoutTiming).Append("</td><td class='").Append(a.TotalPnl >= 0 ? "green" : "red").Append("'>").Append(Html(a.TotalPnl.ToString("C0"))).Append("</td><td class='").Append(liveBalance >= 0 ? "green" : "red").Append("'>").Append(Html(liveBalance.ToString("C0"))).Append("</td><td>").Append(a.Trades).Append(" • ").Append(a.Wins).Append(" / ").Append(a.Losses).Append("</td><td>").Append(eval).Append("</td><td>").Append(funded).Append("</td><td>Gross withdrawals ").Append(Html(a.PayoutGrossWithdrawn.ToString("C0"))).Append("<br>Cash after share (pre-cost) ").Append(Html(a.PayoutCash.ToString("C0"))).Append("<br>All eval / replacement cost ").Append(Html(a.EvaluationCost.ToString("C0"))).Append("<br><strong>FULL NET CASH AFTER ALL COSTS <span class='").Append(netAfterCost < 0 ? "red" : "green").Append("'>").Append(Html(netAfterCost.ToString("C0"))).Append("</span></strong></td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<details class='audit'><summary>Open daily carried account record</summary><div class='inside'><h3>Daily carried account record</h3><div class='table-wrap'><table><thead><tr><th>Account</th><th>Session date</th><th>Day P/L</th><th>Daily lock</th><th>Eval qualifying day</th><th>Eval balance / consecutive days</th><th>Funded qualifying day</th><th>Funded balance / payout days</th><th>Purchases / passes</th><th>State after close</th></tr></thead><tbody>");
            int rows = 0;
            for (int i = 0; i < source.Count; i++)
            {
                KeystoneArcVirtualAccount a = source[i];
                for (int j = 0; j < a.DayHistory.Count; j++)
                {
                    KeystoneArcAccountDay d = a.DayHistory[j];
                    rows++;
                    string lockText = d.DayLocked ? "LOCKED" : "OPEN";
                    sb.Append("<tr><td>").Append(Html(a.Name)).Append("</td><td>").Append(d.Day.ToString("yyyy-MM-dd")).Append("</td><td class='").Append(d.DayPnl >= 0 ? "green" : "red").Append("'>").Append(Html(d.DayPnl.ToString("C0"))).Append("</td><td>").Append(lockText).Append("</td><td>").Append(d.EvalQualifyingDay ? "YES" : "NO").Append("</td><td>").Append(Html(d.EvaluationBalanceAfter.ToString("C0"))).Append(" • ").Append(d.ConsecutiveEvalQualifyingDaysAfter).Append(" consecutive</td><td>").Append(d.FundedQualifyingDay ? "YES" : "NO").Append("</td><td>").Append(Html(d.FundedBalanceAfter.ToString("C0"))).Append(" • ").Append(d.FundedQualifyingDaysAfter).Append(" days</td><td>").Append(d.EvaluationPurchasesAfter).Append(" / ").Append(d.EvaluationPassesAfter).Append(d.ReplacementPendingAfter ? " • replacement next" : string.Empty).Append("</td><td>").Append(Html(d.StateAfterClose)).Append("</td></tr>");
                }
            }
            if (rows == 0) sb.Append("<tr><td colspan='10'>No account-day history was created because the pool did not receive an included event.</td></tr>");
            sb.Append("</tbody></table></div></div></details>");
        }

        private void AppendResearchFindingsHtml(StringBuilder sb)
        {
            List<KeystoneArcResearchFinding> findings = BuildResearchFindings();
            sb.Append("<h2 id='research-findings'>Research findings • evidence from this loaded run</h2><div class='card'>These notes are deterministic summaries of the selected range. They do not create a new session/timeframe calculation, make a forward-performance claim, or replace a separate validation range.</div><div class='grid'>");
            foreach (KeystoneArcResearchFinding finding in findings)
            {
                string css = finding.Accent == Green ? "green" : (finding.Accent == Red ? "red" : (finding.Accent == Gold ? "gold" : (finding.Accent == Orchid ? "orchid" : "cyan")));
                sb.Append("<div class='card'><div class='label ").Append(css).Append("'>").Append(Html(finding.Title)).Append("</div><div class='mono'>").Append(Html(finding.Detail)).Append("</div></div>");
            }
            sb.Append("</div>");
        }

        private static void AppendLifecyclePeriodHtml(StringBuilder sb, List<KeystoneArcVirtualAccount> source)
        {
            List<KeystoneArcPayoutAuditDay> audit = KeystoneArcEngine.BuildPayoutAuditDays(source);
            if (audit.Count == 0) return;
            List<IGrouping<DateTime, KeystoneArcPayoutAuditDay>> daily = audit.GroupBy(x => x.DaySnapshot.Day.Date).OrderBy(x => x.Key).ToList();
            List<IGrouping<DateTime, KeystoneArcPayoutAuditDay>> weekly = audit.GroupBy(x => x.DaySnapshot.Day.Date.AddDays(-(((int)x.DaySnapshot.Day.DayOfWeek + 6) % 7))).OrderBy(x => x.Key).ToList();
            List<IGrouping<DateTime, KeystoneArcPayoutAuditDay>> monthly = audit.GroupBy(x => new DateTime(x.DaySnapshot.Day.Year, x.DaySnapshot.Day.Month, 1)).OrderBy(x => x.Key).ToList();
            IGrouping<DateTime, KeystoneArcPayoutAuditDay> bestMonth = monthly.OrderByDescending(x => x.Sum(y => y.DaySnapshot.DayPnl)).FirstOrDefault();
            IGrouping<DateTime, KeystoneArcPayoutAuditDay> bestWeek = weekly.OrderByDescending(x => x.Sum(y => y.DaySnapshot.DayPnl)).FirstOrDefault();
            IGrouping<DateTime, KeystoneArcPayoutAuditDay> highestCostMonth = monthly.OrderByDescending(x => x.Sum(y => y.EvaluationCostDelta)).FirstOrDefault();
            List<DateTime> payoutDates = audit.Where(x => x.PayoutDelta > 0).Select(x => x.DaySnapshot.Day.Date).Distinct().OrderBy(x => x).ToList();
            int longestGap = 0; for (int i = 1; i < payoutDates.Count; i++) longestGap = Math.Max(longestGap, (payoutDates[i] - payoutDates[i - 1]).Days);
            sb.Append("<details class='audit'><summary>Open daily, weekly, and monthly lifecycle rollups</summary><div class='inside'><h2>Daily, weekly, and monthly lifecycle rollup</h2><div class='card'>These totals aggregate existing exported account-day records. They are reporting views of the selected run, not a re-run under a different session, timeframe, instrument, or contract.</div><div class='grid'>");
            sb.Append("<div class='card'><div class='label'>Best monthly assigned P/L</div><div class='value green'>").Append(bestMonth == null ? "—" : Html(bestMonth.Key.ToString("yyyy-MM"))).Append("</div><div class='muted'>").Append(bestMonth == null ? "No month record." : Html(bestMonth.Sum(x => x.DaySnapshot.DayPnl).ToString("C0"))).Append(" assigned day P/L.</div></div>");
            sb.Append("<div class='card'><div class='label'>Best weekly assigned P/L</div><div class='value cyan'>").Append(bestWeek == null ? "—" : Html(bestWeek.Key.ToString("yyyy-MM-dd"))).Append("</div><div class='muted'>").Append(bestWeek == null ? "No week record." : Html(bestWeek.Sum(x => x.DaySnapshot.DayPnl).ToString("C0"))).Append(" assigned day P/L.</div></div>");
            sb.Append("<div class='card'><div class='label'>Highest monthly evaluation cost</div><div class='value orchid'>").Append(highestCostMonth == null ? "—" : Html(highestCostMonth.Key.ToString("yyyy-MM"))).Append("</div><div class='muted'>").Append(highestCostMonth == null ? "No cost record." : Html(highestCostMonth.Sum(x => x.EvaluationCostDelta).ToString("C0"))).Append(" modeled evaluation / replacement cost.</div></div>");
            sb.Append("<div class='card'><div class='label'>Longest payout-date gap</div><div class='value gold'>").Append(payoutDates.Count == 0 ? "NO PAYOUT" : longestGap + " DAYS").Append("</div><div class='muted'>Observed between modeled payout dates in this selected range.</div></div></div>");
            AppendLifecyclePeriodTable(sb, "Daily", daily);
            AppendLifecyclePeriodTable(sb, "Weekly", weekly);
            AppendLifecyclePeriodTable(sb, "Monthly", monthly);
            sb.Append("</div></details>");
        }

        private static void AppendLifecyclePeriodTable(StringBuilder sb, string label, IEnumerable<IGrouping<DateTime, KeystoneArcPayoutAuditDay>> groups)
        {
            sb.Append("<h3>").Append(label).Append(" account lifecycle totals</h3><div class='table-wrap'><table><thead><tr><th>").Append(label == "Monthly" ? "Month" : label + " start").Append("</th><th>Account-day records</th><th>Assigned day P/L</th><th>Accounts positive / negative</th><th>Payout accounts / cycles</th><th>Gross withdrawal</th><th>Cash after share • pre-cost</th><th>Evaluation purchases / cost</th><th>Net cash after costs in period</th></tr></thead><tbody>");
            foreach (IGrouping<DateTime, KeystoneArcPayoutAuditDay> group in groups)
            {
                double pnl = group.Sum(x => x.DaySnapshot.DayPnl);
                int paidAccounts = group.Count(x => x.PayoutDelta > 0);
                int payoutCycles = group.Sum(x => x.PayoutDelta);
                double gross = group.Sum(x => x.PayoutGrossDelta);
                double net = group.Sum(x => x.PayoutCashDelta);
                int purchases = group.Sum(x => x.EvaluationPurchaseDelta);
                double cost = group.Sum(x => x.EvaluationCostDelta);
                sb.Append("<tr><td>").Append(group.Key.ToString(label == "Monthly" ? "yyyy-MM" : "yyyy-MM-dd")).Append("</td><td>").Append(group.Count()).Append("</td><td class='").Append(pnl < 0 ? "red" : "green").Append("'>").Append(Html(pnl.ToString("C0"))).Append("</td><td>").Append(group.Count(x => x.DaySnapshot.DayPnl > 0)).Append(" / ").Append(group.Count(x => x.DaySnapshot.DayPnl < 0)).Append("</td><td>").Append(paidAccounts).Append(" / ").Append(payoutCycles).Append("</td><td class='gold'>").Append(Html(gross.ToString("C0"))).Append("</td><td class='green'>").Append(Html(net.ToString("C0"))).Append("</td><td>").Append(purchases).Append(" / <span class='orchid'>").Append(Html(cost.ToString("C0"))).Append("</span></td><td class='").Append(net - cost < 0 ? "red" : "green").Append("'>").Append(Html((net - cost).ToString("C0"))).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        // Outcome and pool inclusion are intentionally separate. Every newly detected setup is
        // eligible by default; an optional review decision can exclude it from account math.
        private static string PoolDecisionLabel(KeystoneArcEvent e)
        {
            if (e == null) return "NO EVENT";
            if (string.Equals(e.ReviewState, "ACCEPTED", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(e.AssignedVirtualAccount)) return "ELIGIBLE • ASSIGNED";
                if (!string.IsNullOrWhiteSpace(e.SkipReason)) return "ELIGIBLE • SKIPPED";
                return "ELIGIBLE FOR POOL";
            }
            if (string.Equals(e.ReviewState, "FLAGGED", StringComparison.OrdinalIgnoreCase)) return "FLAGGED • EXCLUDED";
            if (string.Equals(e.ReviewState, "REJECTED", StringComparison.OrdinalIgnoreCase)) return "EXCLUDED";
            return "EXCLUDED • NOT IN POOL";
        }

        private static string Html(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private void ClearLab()
        {
            ConfirmResetForNewTest();
        }

        private void ConfirmResetForNewTest()
        {
            if (operationBusy || isProcessing) { UpdateUi("WAIT FOR THE ACTIVE OPERATION TO FINISH BEFORE RESETTING", Gold); return; }
            ShowConfirmation("RESET + START A NEW TEST?", "Confirm clear: this clears the current history, candidates, review decisions, pool results, and all test parameters. Saved snapshots and CSV exports will remain.", "RESET THIS TEST", Red, delegate { ResetForNewTest(); });
        }

        private void ResetForNewTest()
        {
            ClearCurrentResearch(true);
            config = new KeystoneArcRunConfig();
            configuredMnqInstrument = null; configuredMgcInstrument = null;
            mnqSetupFromOpenChart = false; mgcSetupFromOpenChart = false; mnqSetupDerivedFromOpenOneMinute = false; mgcSetupDerivedFromOpenOneMinute = false; mnqOutcomeFromOpenChart = false; mgcOutcomeFromOpenChart = false;
            mnqOutcomeMatchesSetup = true; mgcOutcomeMatchesSetup = true;
            mnqOutcomeValidation = "not checked"; mgcOutcomeValidation = "not checked";
            historicalRequestDetails.Clear(); selectedEvidenceEvent = null;
            evidenceBars.Clear(); CancelEvidenceRequest(); EndBusy(); isProcessing = false; pendingRequests = 0;
            if (strategyBox != null) strategyBox.SelectedIndex = 0;
            if (scopeBox != null) scopeBox.SelectedIndex = 0;
            if (accountPathBox != null) accountPathBox.SelectedIndex = 0;
            if (directionBox != null) directionBox.SelectedIndex = 0;
            if (bhFilterBox != null) bhFilterBox.SelectedIndex = 0;
            if (mnqStrongRedBox != null) mnqStrongRedBox.Text = "3";
            if (mnqStrongDeclineBox != null) mnqStrongDeclineBox.Text = "0";
            if (mgcStrongRedBox != null) mgcStrongRedBox.Text = "0";
            if (mgcStrongDeclineBox != null) mgcStrongDeclineBox.Text = "10";
            if (bhStrongCombineBox != null) bhStrongCombineBox.SelectedIndex = 0;
            if (sessionBox != null) sessionBox.SelectedIndex = 0;
            if (dateModeBox != null) dateModeBox.SelectedIndex = 0;
            if (timeframeBox != null) timeframeBox.SelectedIndex = 1;
            if (mnqBox != null) mnqBox.Text = "AUTO";
            if (mgcBox != null) mgcBox.Text = "AUTO";
            if (startBox != null) startBox.Text = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (endBox != null) endBox.Text = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (bhSetupBox != null) bhSetupBox.IsChecked = true;
            if (fvgSetupBox != null) fvgSetupBox.IsChecked = false;
            if (quantityBox != null) quantityBox.Text = "10";
            if (targetBox != null) targetBox.Text = "1500";
            if (stopBox != null) stopBox.Text = "500";
            if (stopModeBox != null) stopModeBox.SelectedIndex = 0;
            if (mnqStopOffsetBox != null) mnqStopOffsetBox.Text = "5";
            if (mgcStopOffsetBox != null) mgcStopOffsetBox.Text = "1";
            if (dailyGoalBox != null) dailyGoalBox.Text = "1500";
            if (dailyLossBox != null) dailyLossBox.Text = "500";
            if (asianStartTimeBox != null) asianStartTimeBox.Text = "1800";
            if (asianEndTimeBox != null) asianEndTimeBox.Text = "1555";
            if (asianDirectionBox != null) asianDirectionBox.SelectedIndex = 0;
            if (asianMnqDirectionBox != null) asianMnqDirectionBox.SelectedIndex = 0;
            if (asianMgcDirectionBox != null) asianMgcDirectionBox.SelectedIndex = 0;
            if (asianRiskModeBox != null) asianRiskModeBox.SelectedIndex = 0;
            if (asianReversalLossBox != null) asianReversalLossBox.Text = "75";
            if (asianMnqPriceMoveBox != null) asianMnqPriceMoveBox.Text = "37.5";
            if (asianMgcPriceMoveBox != null) asianMgcPriceMoveBox.Text = "7.5";
            if (asianCycleTargetBox != null) asianCycleTargetBox.Text = "350";
            if (asianCombinedStopBox != null) asianCombinedStopBox.Text = "0";
            if (asianDailyLossBox != null) asianDailyLossBox.Text = "600";
            if (asianInstrumentStopBox != null) asianInstrumentStopBox.Text = "0";
            if (asianMnqInstrumentStopBox != null) asianMnqInstrumentStopBox.Text = "0";
            if (asianMgcInstrumentStopBox != null) asianMgcInstrumentStopBox.Text = "0";
            if (asianBreakEvenBox != null) asianBreakEvenBox.Text = "0";
            if (asianStartingQuantityBox != null) asianStartingQuantityBox.Text = "1";
            if (asianMaxReversalsBox != null) asianMaxReversalsBox.Text = "4";
            if (asianMnqMaxReversalsBox != null) asianMnqMaxReversalsBox.Text = "4";
            if (asianMgcMaxReversalsBox != null) asianMgcMaxReversalsBox.Text = "4";
            if (customStartBox != null) customStartBox.Text = "930";
            if (endTimeBox != null) endTimeBox.Text = "1555";
            if (poolBox != null) poolBox.SelectedItem = "20";
            if (propStartingBalanceBox != null) propStartingBalanceBox.Text = "0";
            if (personalStartingBalanceBox != null) personalStartingBalanceBox.Text = "0";
            if (accountStartModeBox != null) accountStartModeBox.SelectedIndex = 0;
            if (evalTargetBox != null) evalTargetBox.Text = "3000";
            if (evalDailyCapBox != null) evalDailyCapBox.Text = "1500";
            if (evalConsistencyBox != null) evalConsistencyBox.Text = "0";
            if (evalFailureBox != null) evalFailureBox.Text = "2000";
            if (evalDailyLossBox != null) evalDailyLossBox.Text = "500";
            if (evalStageTradeRulesBox != null) evalStageTradeRulesBox.IsChecked = false;
            if (evalTradeTargetBox != null) evalTradeTargetBox.Text = "1500";
            if (evalTradeStopBox != null) evalTradeStopBox.Text = "500";
            if (replacementFundingGateBox != null) replacementFundingGateBox.IsChecked = false;
            if (firmFundedCapBox != null) firmFundedCapBox.IsChecked = false;
            if (firmEvalSlotsBox != null) firmEvalSlotsBox.Text = "10";
            if (firmMaxFundedBox != null) firmMaxFundedBox.Text = "5";
            if (fundedDailyLossBox != null) fundedDailyLossBox.Text = "0";
            if (fundedFailureBox != null) fundedFailureBox.Text = "2000";
            if (evalCostBox != null) evalCostBox.Text = "120";
            if (payoutThresholdBox != null) payoutThresholdBox.Text = "4000";
            if (payoutDaysBox != null) payoutDaysBox.Text = "5";
            if (payoutAmountBox != null) payoutAmountBox.Text = "2000";
            if (minimumDaysBox != null) minimumDaysBox.Text = "2";
            if (minimumQualifyingDayBox != null) minimumQualifyingDayBox.Text = "150";
            if (outcomesBox != null) outcomesBox.IsChecked = true;
            if (chartMarksBox != null) chartMarksBox.IsChecked = true;
            if (chartReviewScopeBox != null) chartReviewScopeBox.SelectedIndex = 0;
            if (chartReviewEnabledBox != null) chartReviewEnabledBox.IsChecked = true;
            if (chartReviewBhBox != null) chartReviewBhBox.IsChecked = true;
            if (chartReviewFvgBox != null) chartReviewFvgBox.IsChecked = true;
            if (chartReviewDtBox != null) chartReviewDtBox.IsChecked = true;
            if (chartReviewWinsBox != null) chartReviewWinsBox.IsChecked = true;
            if (chartReviewLossesBox != null) chartReviewLossesBox.IsChecked = true;
            if (chartReviewExitsBox != null) chartReviewExitsBox.IsChecked = false;
            if (chartReviewNoEntryBox != null) chartReviewNoEntryBox.IsChecked = false;
            if (chartReviewFvgZonesBox != null) chartReviewFvgZonesBox.IsChecked = false;
            RefreshSessionInputs();
            RefreshDateInputs(); RefreshInstrumentSourceText();
            RefreshBhAggressionInputState();
            RefreshStrategyInputState();
            RefreshAsianDerivedInputs();
            RefreshLifecycleInputState();
            KeystoneArcHub.Publish(new List<KeystoneArcEvent>(), config);
            HideConfirmation();
            if (workspaceTabs != null) workspaceTabs.SelectedIndex = 0;
            UpdateUi("NEW TEST READY • ENTER YOUR DATES AND PARAMETERS, THEN CONFIRM STEP 1", Blue);
            UpdateWorkflowState();
        }

        private void ClearCurrentResearch(bool resetConfigurationApproval)
        {
            CancelRequests(); CancelEvidenceRequest(); evidenceBars.Clear(); mnqBars.Clear(); mgcBars.Clear(); mnqSetupBars.Clear(); mgcSetupBars.Clear(); events.Clear(); reviewRows.Clear(); accounts.Clear(); researchRunCompleted = false; historicalDataReceipt = "DATA RECEIPT: no completed NinjaTrader historical request recorded for this lab run."; unsavedResearch = false; KeystoneArcHub.Publish(events, config);
            if (resetConfigurationApproval) { configurationApproved = false; configurationApprovalKey = string.Empty; researchSubmissionLocked = false; }
            if (summaryText != null) summaryText.Text = "CLEARED"; if (eventText != null) eventText.Text = "EVENT LEDGER EMPTY"; if (mathText != null) mathText.Text = "WAITING FOR A NEW SELECTED RANGE"; if (poolText != null) poolText.Text = "NO VIRTUAL POOL RUN"; if (poolDetailText != null) poolDetailText.Text = "NO ACCOUNT SELECTED"; if (poolTimelineStack != null) poolTimelineStack.Children.Clear(); if (researchFindingsStack != null) researchFindingsStack.Children.Clear(); if (walkthroughAccountBox != null) walkthroughAccountBox.Items.Clear(); UpdateWalkthroughSelection(null); if (firstReturnDashboardStack != null) firstReturnDashboardStack.Children.Clear(); if (firstReturnDashboardText != null) firstReturnDashboardText.Text = "FIRST RETURN FROM INITIAL EVALUATION • RUN THE VIRTUAL POOL"; if (lifecycleText != null) lifecycleText.Text = "NO LIFECYCLE RUN"; if (reviewList != null) reviewList.Items.Clear(); if (poolAccountList != null) poolAccountList.Items.Clear(); UpdatePoolMetricTiles(); UpdateUi("CLEARED KEYSTONE ARC MEMORY ONLY • SAVED FILES RETAINED", Gold);
            UpdateWorkflowState();
        }

        private void CancelRequests()
        {
            ++generation; pendingRequests = 0; historicalRequestActive = false; historicalRequestQueue.Clear(); isProcessing = false; try { if (mnqRequest != null) mnqRequest.Dispose(); } catch { } try { if (mgcRequest != null) mgcRequest.Dispose(); } catch { } try { if (mnqSetupRequest != null) mnqSetupRequest.Dispose(); } catch { } try { if (mgcSetupRequest != null) mgcSetupRequest.Dispose(); } catch { } mnqRequest = null; mgcRequest = null; mnqSetupRequest = null; mgcSetupRequest = null; EndBusy();
        }

        private string EventCsv()
        {
            return EventCsv(events);
        }

        private string EventCsv(IEnumerable<KeystoneArcEvent> source)
        {
            var sb = new StringBuilder("id,session_date,symbol,direction,setup,strength,reference,trigger,entry_time,entry,stop,target,outcome,exit_time,exit_price,exit_distance,gross,evaluation_outcome,evaluation_exit_time,evaluation_exit_price,evaluation_target,evaluation_stop,evaluation_gross,peak_after_entry,trough_after_entry,target_touched,stop_touched,session_order,fvg_lower,fvg_upper,fvg_formed,pool_decision,pool_decision_note,account,skip,config\n");
            foreach (var e in source ?? Enumerable.Empty<KeystoneArcEvent>()) sb.AppendLine(string.Join(",", new[] { Csv(e.Id), Csv(KeystoneArcEngine.SessionGroupingDate(e.TriggerTime, config).ToString("yyyy-MM-dd")), Csv(e.Symbol), Csv(e.Direction), Csv(e.SetupClass), Csv(e.StrengthTag), Csv(e.ReferenceTime.ToString("o")), Csv(e.TriggerTime.ToString("o")), Csv(e.EntryTime.ToString("o")), e.Entry.ToString(CultureInfo.InvariantCulture), e.Stop.ToString(CultureInfo.InvariantCulture), e.Target.ToString(CultureInfo.InvariantCulture), Csv(e.Outcome), Csv(e.ExitTime.ToString("o")), double.IsNaN(e.ExitPrice) ? string.Empty : e.ExitPrice.ToString(CultureInfo.InvariantCulture), double.IsNaN(e.ExitPrice) ? string.Empty : (e.ExitPrice - e.Entry).ToString(CultureInfo.InvariantCulture), e.GrossPnl.ToString(CultureInfo.InvariantCulture), Csv(e.EvaluationOutcome), Csv(e.EvaluationExitTime == DateTime.MinValue ? string.Empty : e.EvaluationExitTime.ToString("o")), double.IsNaN(e.EvaluationExitPrice) ? string.Empty : e.EvaluationExitPrice.ToString(CultureInfo.InvariantCulture), double.IsNaN(e.EvaluationTarget) ? string.Empty : e.EvaluationTarget.ToString(CultureInfo.InvariantCulture), double.IsNaN(e.EvaluationStop) ? string.Empty : e.EvaluationStop.ToString(CultureInfo.InvariantCulture), e.EvaluationGrossPnl.ToString(CultureInfo.InvariantCulture), e.PeakAfterEntry.ToString(CultureInfo.InvariantCulture), e.TroughAfterEntry.ToString(CultureInfo.InvariantCulture), e.TargetTouched.ToString(), e.StopTouched.ToString(), e.SessionOrder.ToString(), e.FvgLower.ToString(CultureInfo.InvariantCulture), e.FvgUpper.ToString(CultureInfo.InvariantCulture), Csv(e.FvgFormedTime == DateTime.MinValue ? string.Empty : e.FvgFormedTime.ToString("o")), Csv(PoolDecisionLabel(e)), Csv(e.ReviewNote), Csv(e.AssignedVirtualAccount), Csv(e.SkipReason), Csv(e.ConfigurationKey) }));
            return sb.ToString();
        }

        private string AccountCsv()
        {
            var sb = new StringBuilder("account,modeled_firm,firm_eval_slot,account_snapshot_state,assigned_ledger_pnl,peak_ledger_pnl,trades,wins,losses,state,one_day_pnl,one_day_daily_locked,currently_funded,funded_cap_wait,funded_since_date,initial_slot_start,current_lifecycle_start,last_assigned_date,last_blowout_date,terminal_lifecycle_end,total_blowout_events,first_payout_date,days_to_first_payout_calendar,days_to_first_payout_recorded_sessions,eval_stage_balance,eval_target_left,eval_qualifying_days_lifetime,eval_qualifying_days_consecutive,eval_consecutive_days_left,eval_passes,eval_purchases,replacement_pending,replacement_payout_gate_blocked,funded_stage_balance,payout_threshold_left,payout_qualifying_days,payout_days_left,evaluation_cost,failed_evaluations,failed_funded,payouts,payout_gross_withdrawn,payout_cash_after_share_pre_cost,full_net_cash_after_all_costs\n");
            foreach (var a in accounts)
            {
                KeystoneArcFirstPayoutTiming t = KeystoneArcEngine.GetFirstPayoutTiming(a);
                sb.AppendLine(string.Join(",", new[] { Csv(a.Name), Csv(a.PropFirmCode), a.PropFirmSlot.ToString(), Csv(AccountSnapshotState(a)), a.TotalPnl.ToString(CultureInfo.InvariantCulture), a.PeakPnl.ToString(CultureInfo.InvariantCulture), a.Trades.ToString(), a.Wins.ToString(), a.Losses.ToString(), Csv(a.LastState), a.DayPnl.ToString(CultureInfo.InvariantCulture), a.DayLocked ? "1" : "0", a.Funded ? "1" : "0", a.FundedCapPending ? "1" : "0", Csv(a.FundedSinceDate == DateTime.MinValue ? string.Empty : a.FundedSinceDate.ToString("yyyy-MM-dd")), Csv(t.InitialSlotStart == DateTime.MinValue ? string.Empty : t.InitialSlotStart.ToString("yyyy-MM-dd")), Csv(a.CurrentLifecycleStart == DateTime.MinValue ? string.Empty : a.CurrentLifecycleStart.ToString("yyyy-MM-dd")), Csv(a.LastAssignedDate == DateTime.MinValue ? string.Empty : a.LastAssignedDate.ToString("yyyy-MM-dd")), Csv(a.LastBlowoutDate == DateTime.MinValue ? string.Empty : a.LastBlowoutDate.ToString("yyyy-MM-dd")), Csv(a.TerminalLifecycleEnd == DateTime.MinValue ? string.Empty : a.TerminalLifecycleEnd.ToString("yyyy-MM-dd")), (a.FailedEvaluations + a.FailedFunded).ToString(), Csv(t.HasPayout ? t.FirstPayoutDate.ToString("yyyy-MM-dd") : string.Empty), t.HasPayout ? t.CalendarDaysFromInitial.ToString() : string.Empty, t.HasPayout ? t.RecordedSessionsFromInitial.ToString() : string.Empty, a.EvaluationBalance.ToString(CultureInfo.InvariantCulture), Math.Max(0, config.EvaluationTarget - a.EvaluationBalance).ToString(CultureInfo.InvariantCulture), a.PositiveDays.ToString(), a.ConsecutiveEvalQualifyingDays.ToString(), Math.Max(0, config.MinimumPositiveDays - a.ConsecutiveEvalQualifyingDays).ToString(), a.EvaluationPasses.ToString(), a.EvaluationPurchases.ToString(), a.ReplacementPending ? "1" : "0", a.ReplacementBudgetBlocked ? "1" : "0", a.FundedBalance.ToString(CultureInfo.InvariantCulture), Math.Max(0, config.PayoutThreshold - a.FundedBalance).ToString(CultureInfo.InvariantCulture), a.FundedPositiveDays.ToString(), Math.Max(0, config.PayoutDaysRequired - a.FundedPositiveDays).ToString(), a.EvaluationCost.ToString(CultureInfo.InvariantCulture), a.FailedEvaluations.ToString(), a.FailedFunded.ToString(), a.Payouts.ToString(), a.PayoutGrossWithdrawn.ToString(CultureInfo.InvariantCulture), a.PayoutCash.ToString(CultureInfo.InvariantCulture), (a.PayoutCash - a.EvaluationCost).ToString(CultureInfo.InvariantCulture) }));
            }
            return sb.ToString();
        }

        private string AccountDayCsv()
        {
            var sb = new StringBuilder("account,modeled_firm,firm_eval_slot,session_date,day_pnl,trades_after,wins_after,losses_after,balance_before,balance_after,cost_delta,payout_gross_delta,payout_net_delta,eval_purchase_delta,eval_pass_date_after,first_payout_date_after,daily_locked,eval_qualifying_day,eval_balance_after,eval_qualifying_days_lifetime_after,eval_qualifying_days_consecutive_after,funded_qualifying_day,funded_after,funded_cap_wait_after,funded_slots_in_firm_after,funded_balance_after,payout_qualifying_days_after,eval_purchases_after,eval_cost_after,eval_passes_after,lifecycle_start_after,replacement_pending_after,payouts_after,payout_gross_after,payout_net_after,state_after_close\n");
            for (int i = 0; i < accounts.Count; i++)
            {
                KeystoneArcVirtualAccount a = accounts[i];
                for (int j = 0; j < a.DayHistory.Count; j++)
                {
                    KeystoneArcAccountDay d = a.DayHistory[j];
                    sb.AppendLine(string.Join(",", new[] { Csv(a.Name), Csv(a.PropFirmCode), a.PropFirmSlot.ToString(), Csv(d.Day.ToString("yyyy-MM-dd")), d.DayPnl.ToString(CultureInfo.InvariantCulture), d.TradesAfter.ToString(), d.WinsAfter.ToString(), d.LossesAfter.ToString(), d.BalanceBefore.ToString(CultureInfo.InvariantCulture), d.BalanceAfter.ToString(CultureInfo.InvariantCulture), d.CostDelta.ToString(CultureInfo.InvariantCulture), d.PayoutGrossDelta.ToString(CultureInfo.InvariantCulture), d.PayoutCashDelta.ToString(CultureInfo.InvariantCulture), d.EvaluationPurchaseDelta.ToString(), Csv(d.EvaluationPassDateAfter == DateTime.MinValue ? string.Empty : d.EvaluationPassDateAfter.ToString("yyyy-MM-dd")), Csv(d.FirstPayoutDateAfter == DateTime.MinValue ? string.Empty : d.FirstPayoutDateAfter.ToString("yyyy-MM-dd")), d.DayLocked ? "1" : "0", d.EvalQualifyingDay ? "1" : "0", d.EvaluationBalanceAfter.ToString(CultureInfo.InvariantCulture), d.EvaluationQualifyingDaysAfter.ToString(), d.ConsecutiveEvalQualifyingDaysAfter.ToString(), d.FundedQualifyingDay ? "1" : "0", d.FundedAfter ? "1" : "0", d.FundedCapPendingAfter ? "1" : "0", d.FundedSlotsInFirmAfter.ToString(), d.FundedBalanceAfter.ToString(CultureInfo.InvariantCulture), d.FundedQualifyingDaysAfter.ToString(), d.EvaluationPurchasesAfter.ToString(), d.EvaluationCostAfter.ToString(CultureInfo.InvariantCulture), d.EvaluationPassesAfter.ToString(), Csv(d.LifecycleStartAfter == DateTime.MinValue ? string.Empty : d.LifecycleStartAfter.ToString("yyyy-MM-dd")), d.ReplacementPendingAfter ? "1" : "0", d.PayoutsAfter.ToString(), d.PayoutGrossAfter.ToString(CultureInfo.InvariantCulture), d.PayoutCashAfter.ToString(CultureInfo.InvariantCulture), Csv(d.StateAfterClose) }));
                }
            }
            return sb.ToString();
        }

        private string CapitalPolicySummaryText()
        {
            KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
            int modeledFirms = accounts.Where(x => !string.IsNullOrEmpty(x.PropFirmCode)).Select(x => x.PropFirmCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int capWaiting = accounts.Count(x => x.FundedCapPending);
            return "REPLACEMENT_PAYOUT_GATE=" + (capital.GateEnabled ? "ON" : "OFF") + "\n" +
                "FIRM_FUNDED_CAP_ENABLED=" + (config.FirmFundedCapEnabled > 0 ? "ON" : "OFF") + "\n" +
                "MODELED_FIRMS=" + modeledFirms + "\n" +
                "EVALUATION_SLOTS_PER_FIRM=" + config.EvaluationSlotsPerFirm + "\n" +
                "MAX_FUNDED_PER_FIRM=" + config.MaxFundedPerFirm + "\n" +
                "PASSED_EVALUATIONS_WAITING_FOR_FIRM_CAPACITY=" + capWaiting + "\n" +
                "INITIAL_EVALUATION_PURCHASES=" + capital.InitialEvaluationPurchases + "\n" +
                "INITIAL_EVALUATION_INVESTMENT=" + capital.InitialEvaluationInvestment.ToString(CultureInfo.InvariantCulture) + "\n" +
                "REPLACEMENT_EVALUATION_PURCHASES=" + capital.ReplacementEvaluationPurchases + "\n" +
                "REPLACEMENT_EVALUATION_COST=" + capital.ReplacementEvaluationCost.ToString(CultureInfo.InvariantCulture) + "\n" +
                "PAYOUT_CASH_AFTER_SHARE=" + capital.PayoutCashAfterShare.ToString(CultureInfo.InvariantCulture) + "\n" +
                "PAYOUT_CASH_AFTER_INITIAL_INVESTMENT=" + capital.PayoutCashAfterInitialInvestment.ToString(CultureInfo.InvariantCulture) + "\n" +
                "FIRST_PAYOUT_REACHED=" + (capital.FirstPayoutReached ? "1" : "0") + "\n" +
                "FIRST_PAYOUT_DATE=" + (capital.FirstPayoutDate == DateTime.MinValue ? string.Empty : capital.FirstPayoutDate.ToString("yyyy-MM-dd")) + "\n" +
                "PAYOUT_CASH_THROUGH_FIRST_PAYOUT=" + capital.PayoutCashThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture) + "\n" +
                "INVESTMENT_THROUGH_FIRST_PAYOUT=" + capital.InvestmentThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture) + "\n" +
                "EVALUATION_PURCHASES_THROUGH_FIRST_PAYOUT=" + capital.EvaluationPurchasesThroughFirstPayoutDate + "\n" +
                "REPLACEMENT_PURCHASES_THROUGH_FIRST_PAYOUT=" + capital.ReplacementPurchasesThroughFirstPayoutDate + "\n" +
                "REPLENISHED_SLOTS_THROUGH_FIRST_PAYOUT=" + capital.ReplenishedSlotsThroughFirstPayoutDate + "\n" +
                "NET_CASH_THROUGH_FIRST_PAYOUT=" + capital.NetCashThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture) + "\n" +
                "PROFITABILITY_REACHED=" + (capital.ProfitabilityReached ? "1" : "0") + "\n" +
                "PROFITABILITY_DATE=" + (capital.ProfitabilityDate == DateTime.MinValue ? string.Empty : capital.ProfitabilityDate.ToString("yyyy-MM-dd")) + "\n" +
                "PAYOUT_CASH_THROUGH_PROFITABILITY=" + capital.PayoutCashThroughProfitability.ToString(CultureInfo.InvariantCulture) + "\n" +
                "INVESTMENT_THROUGH_PROFITABILITY=" + capital.InvestmentThroughProfitability.ToString(CultureInfo.InvariantCulture) + "\n" +
                "NET_CASH_AT_PROFITABILITY=" + capital.NetCashAtProfitability.ToString(CultureInfo.InvariantCulture) + "\n" +
                "REPLACEMENT_CASH_AVAILABLE=" + capital.ReplacementCashAvailable.ToString(CultureInfo.InvariantCulture) + "\n" +
                "PENDING_REPLACEMENT_SLOTS=" + capital.PendingReplacementSlots + "\n" +
                "CASH_REQUIRED_FOR_PENDING_REPLACEMENTS=" + capital.CashRequiredForPendingReplacements.ToString(CultureInfo.InvariantCulture) + "\n" +
                "REPLACEMENT_CASH_SHORTFALL=" + capital.ReplacementCashShortfall.ToString(CultureInfo.InvariantCulture) + "\n" +
                "BENCHED_REPLACEMENT_SLOTS=" + capital.BenchedReplacementSlots + "\n" +
                "FULL_NET_CASH_AFTER_ALL_COSTS=" + capital.FullNetCashAfterAllCosts.ToString(CultureInfo.InvariantCulture);
        }

        private string CapitalPolicyCsv()
        {
            KeystoneArcCapitalPolicySummary capital = KeystoneArcEngine.BuildCapitalPolicySummary(accounts, config);
            int modeledFirms = accounts.Where(x => !string.IsNullOrEmpty(x.PropFirmCode)).Select(x => x.PropFirmCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int capWaiting = accounts.Count(x => x.FundedCapPending);
            var sb = new StringBuilder("replacement_payout_gate,firm_funded_cap_enabled,modeled_firms,evaluation_slots_per_firm,max_funded_per_firm,passed_evals_waiting_for_firm_capacity,initial_evaluation_purchases,initial_evaluation_investment,replacement_evaluation_purchases,replacement_evaluation_cost,payout_cash_after_share,payout_cash_after_initial_investment,first_payout_reached,first_payout_date,payout_cash_through_first_payout,investment_through_first_payout,eval_purchases_through_first_payout,replacement_purchases_through_first_payout,replenished_slots_through_first_payout,net_cash_through_first_payout,profitability_reached,profitability_date,payout_cash_through_profitability,investment_through_profitability,net_cash_at_profitability,replacement_cash_available,pending_replacement_slots,cash_required_for_pending_replacements,replacement_cash_shortfall,benched_replacement_slots,total_evaluation_cost,full_net_cash_after_all_costs\n");
            sb.AppendLine(string.Join(",", new[]
            {
                capital.GateEnabled ? "1" : "0", config.FirmFundedCapEnabled > 0 ? "1" : "0", modeledFirms.ToString(), config.EvaluationSlotsPerFirm.ToString(), config.MaxFundedPerFirm.ToString(), capWaiting.ToString(), capital.InitialEvaluationPurchases.ToString(), capital.InitialEvaluationInvestment.ToString(CultureInfo.InvariantCulture), capital.ReplacementEvaluationPurchases.ToString(), capital.ReplacementEvaluationCost.ToString(CultureInfo.InvariantCulture), capital.PayoutCashAfterShare.ToString(CultureInfo.InvariantCulture), capital.PayoutCashAfterInitialInvestment.ToString(CultureInfo.InvariantCulture), capital.FirstPayoutReached ? "1" : "0", Csv(capital.FirstPayoutDate == DateTime.MinValue ? string.Empty : capital.FirstPayoutDate.ToString("yyyy-MM-dd")), capital.PayoutCashThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture), capital.InvestmentThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture), capital.EvaluationPurchasesThroughFirstPayoutDate.ToString(), capital.ReplacementPurchasesThroughFirstPayoutDate.ToString(), capital.ReplenishedSlotsThroughFirstPayoutDate.ToString(), capital.NetCashThroughFirstPayoutDate.ToString(CultureInfo.InvariantCulture), capital.ProfitabilityReached ? "1" : "0", Csv(capital.ProfitabilityDate == DateTime.MinValue ? string.Empty : capital.ProfitabilityDate.ToString("yyyy-MM-dd")), capital.PayoutCashThroughProfitability.ToString(CultureInfo.InvariantCulture), capital.InvestmentThroughProfitability.ToString(CultureInfo.InvariantCulture), capital.NetCashAtProfitability.ToString(CultureInfo.InvariantCulture), capital.ReplacementCashAvailable.ToString(CultureInfo.InvariantCulture), capital.PendingReplacementSlots.ToString(), capital.CashRequiredForPendingReplacements.ToString(CultureInfo.InvariantCulture), capital.ReplacementCashShortfall.ToString(CultureInfo.InvariantCulture), capital.BenchedReplacementSlots.ToString(), capital.TotalEvaluationCost.ToString(CultureInfo.InvariantCulture), capital.FullNetCashAfterAllCosts.ToString(CultureInfo.InvariantCulture)
            }));
            return sb.ToString();
        }

        private string PayoutCycleCsv()
        {
            var sb = new StringBuilder("cycle_number,payout_date,accounts_paid,payout_cycles,gross_withdrawals,net_cash_after_account_share,evaluations_purchased_on_date,evaluation_cost_on_date,net_cash_after_evaluation_cost_on_date,cumulative_payout_cash_through_date,cumulative_evaluation_cost_through_date,cumulative_full_net_cash_after_all_costs,funded_at_close,funded_below_payout_goal,evaluation_in_progress,replacement_next_session,terminal_blown,payout_contributors\n");
            foreach (KeystoneArcPayoutCycleRow row in KeystoneArcEngine.BuildPayoutCycleRows(accounts, config))
                sb.AppendLine(string.Join(",", new[] { row.CycleNumber.ToString(), Csv(row.Day.ToString("yyyy-MM-dd")), row.PayoutAccounts.ToString(), row.PayoutCycles.ToString(), row.GrossWithdrawals.ToString(CultureInfo.InvariantCulture), row.NetCashAfterShare.ToString(CultureInfo.InvariantCulture), row.EvaluationPurchases.ToString(), row.EvaluationCostOnDate.ToString(CultureInfo.InvariantCulture), row.NetCashAfterEvaluationCost.ToString(CultureInfo.InvariantCulture), row.CumulativePayoutCashThroughDate.ToString(CultureInfo.InvariantCulture), row.CumulativeEvaluationCostThroughDate.ToString(CultureInfo.InvariantCulture), row.CumulativeFullNetCashAfterAllCosts.ToString(CultureInfo.InvariantCulture), row.FundedAtClose.ToString(), row.FundedBelowPayoutGoal.ToString(), row.EvaluationInProgress.ToString(), row.ReplacementNextSession.ToString(), row.TerminalBlown.ToString(), Csv(string.Join("; ", row.PayoutContributors)) }));
            return sb.ToString();
        }

        private string ResearchFindingsCsv()
        {
            var sb = new StringBuilder("finding_order,title,detail,scope_note\n");
            int order = 0;
            foreach (KeystoneArcResearchFinding finding in BuildResearchFindings())
            {
                order++;
                sb.AppendLine(order.ToString(CultureInfo.InvariantCulture) + "," + Csv(finding.Title) + "," + Csv(finding.Detail) + "," + Csv("Deterministic observation from the loaded selected-range run; not a forecast or a re-run under another session/timeframe."));
            }
            return sb.ToString();
        }

        private string FirstReturnCsv()
        {
            var sb = new StringBuilder("account,initial_slot_start,first_payout_recorded,first_payout_date,calendar_days_to_first_cash,recorded_sessions_to_first_cash,first_gross_withdrawal,first_payout_cash_after_share,evaluation_replacement_cost_through_first_return,full_net_cash_at_first_return,state_at_first_payout\n");
            foreach (KeystoneArcFirstReturnRow row in KeystoneArcEngine.BuildFirstReturnRows(accounts))
            {
                sb.AppendLine(string.Join(",", new[] { Csv(row.Account), Csv(row.InitialSlotStart == DateTime.MinValue ? string.Empty : row.InitialSlotStart.ToString("yyyy-MM-dd")), row.HasPayout ? "1" : "0", Csv(row.HasPayout ? row.FirstPayoutDate.ToString("yyyy-MM-dd") : string.Empty), row.HasPayout ? row.CalendarDaysFromInitial.ToString() : string.Empty, row.HasPayout ? row.RecordedSessionsFromInitial.ToString() : string.Empty, row.HasPayout ? row.FirstPayoutGross.ToString(CultureInfo.InvariantCulture) : string.Empty, row.HasPayout ? row.FirstPayoutCashAfterShare.ToString(CultureInfo.InvariantCulture) : string.Empty, row.HasPayout ? row.EvaluationCostThroughFirstPayout.ToString(CultureInfo.InvariantCulture) : string.Empty, row.HasPayout ? row.FullNetReturnAfterAllCosts.ToString(CultureInfo.InvariantCulture) : string.Empty, Csv(row.StateAtFirstPayout) }));
            }
            return sb.ToString();
        }

        private string PayoutAccountCsv()
        {
            var sb = new StringBuilder("account,current_stage,initial_lifecycle_start,current_balance,payout_count,first_payout_date,last_payout_date,first_return_calendar_days,first_return_recorded_sessions,gross_withdrawals,payout_cash_after_share,all_evaluation_replacement_cost,full_net_cash_after_all_costs,assigned_ledger_pnl,daily_records,weekly_records,monthly_records,best_daily_pnl,best_weekly_pnl,best_monthly_pnl,post_payout_blowout_date\n");
            foreach (KeystoneArcVirtualAccount a in accounts.Where(x => x.Payouts > 0).OrderBy(x => x.Name))
            {
                KeystoneArcFirstPayoutTiming first = KeystoneArcEngine.GetFirstPayoutTiming(a);
                List<KeystoneArcAccountDay> days = a.DayHistory.OrderBy(x => x.Day).ToList();
                var weeks = days.GroupBy(x => PerformancePeriodStart(x.Day, "WEEKLY")).ToList();
                var months = days.GroupBy(x => PerformancePeriodStart(x.Day, "MONTHLY")).ToList();
                double balance = config.EvaluationEnabled == -2 ? a.StartingBalance + a.TotalPnl : CurrentStageBalance(a);
                sb.AppendLine(string.Join(",", new[] { Csv(a.Name), Csv(AccountPrimaryStage(a)), Csv(a.InitialLifecycleStart == DateTime.MinValue ? string.Empty : a.InitialLifecycleStart.ToString("yyyy-MM-dd")), balance.ToString(CultureInfo.InvariantCulture), a.Payouts.ToString(), Csv(first.HasPayout ? first.FirstPayoutDate.ToString("yyyy-MM-dd") : string.Empty), Csv(LastPayoutDate(a)), first.HasPayout ? first.CalendarDaysFromInitial.ToString() : string.Empty, first.HasPayout ? first.RecordedSessionsFromInitial.ToString() : string.Empty, a.PayoutGrossWithdrawn.ToString(CultureInfo.InvariantCulture), a.PayoutCash.ToString(CultureInfo.InvariantCulture), a.EvaluationCost.ToString(CultureInfo.InvariantCulture), (a.PayoutCash - a.EvaluationCost).ToString(CultureInfo.InvariantCulture), a.TotalPnl.ToString(CultureInfo.InvariantCulture), days.Count.ToString(), weeks.Count.ToString(), months.Count.ToString(), (days.Count == 0 ? 0 : days.Max(x => x.DayPnl)).ToString(CultureInfo.InvariantCulture), (weeks.Count == 0 ? 0 : weeks.Max(x => x.Sum(d => d.DayPnl))).ToString(CultureInfo.InvariantCulture), (months.Count == 0 ? 0 : months.Max(x => x.Sum(d => d.DayPnl))).ToString(CultureInfo.InvariantCulture), Csv(a.LastBlowoutDate == DateTime.MinValue ? string.Empty : a.LastBlowoutDate.ToString("yyyy-MM-dd")) }));
            }
            return sb.ToString();
        }

        private string DailySessionScoreCsv()
        {
            var sb = new StringBuilder("session_date,selected_session,mnq_setups,mgc_setups,total_setups,wins,losses,session_exits,model_pnl,outcome_math_verified\n");
            if (config == null || config.Start == DateTime.MinValue || config.End == DateTime.MinValue) return sb.ToString();
            DateTime first = KeystoneArcEngine.SessionGroupingDate(config.Start, config).Date;
            DateTime last = KeystoneArcEngine.SessionGroupingDate(config.End, config).Date;
            for (DateTime day = first; day <= last; day = day.AddDays(1))
            {
                List<KeystoneArcEvent> dayRows = events.Where(x => KeystoneArcEngine.SessionGroupingDate(x.TriggerTime, config) == day).ToList();
                sb.AppendLine(string.Join(",", new[] { Csv(day.ToString("yyyy-MM-dd")), Csv(config.SessionMode), dayRows.Count(x => string.Equals(x.Symbol, "MNQ", StringComparison.OrdinalIgnoreCase)).ToString(), dayRows.Count(x => string.Equals(x.Symbol, "MGC", StringComparison.OrdinalIgnoreCase)).ToString(), dayRows.Count.ToString(), dayRows.Count(x => x.Outcome == "WIN").ToString(), dayRows.Count(x => x.Outcome.StartsWith("LOSS", StringComparison.OrdinalIgnoreCase)).ToString(), dayRows.Count(x => x.Outcome == "SESSION EXIT").ToString(), dayRows.Sum(x => x.GrossPnl).ToString(CultureInfo.InvariantCulture), config.OutcomeModelEnabled == 1 ? "1" : "0" }));
            }
            return sb.ToString();
        }

        private string BalanceMatrixCsv()
        {
            var sb = new StringBuilder("cycle_number,payout_date,account,status,balance_before,gross_payout,carry_forward_balance,day_pnl\n");
            foreach (KeystoneArcBalanceMatrixRow row in KeystoneArcEngine.BuildBalanceMatrixRows(accounts, config))
                sb.AppendLine(string.Join(",", new[] { row.CycleNumber.ToString(), Csv(row.Day.ToString("yyyy-MM-dd")), Csv(row.Account), Csv(row.Status), row.BalanceBefore.ToString(CultureInfo.InvariantCulture), row.GrossPayout.ToString(CultureInfo.InvariantCulture), row.CarryForwardBalance.ToString(CultureInfo.InvariantCulture), row.DayPnl.ToString(CultureInfo.InvariantCulture) }));
            return sb.ToString();
        }

        private string ComparisonCsv()
        {
            var sb = new StringBuilder("instrument,timeframe_minutes,start_mode,pool,raw_setups,wins,losses,session_exits,win_rate,all_outcome_gross,assigned,skipped,assigned_gross,eval_passed,funded,payouts,payout_cash,eval_cost,net_after_cost,data_method,notes\n");
            for (int i = 0; i < comparisonRows.Count; i++)
            {
                KeystoneArcComparisonRow r = comparisonRows[i];
                int resolved = r.Wins + r.Losses;
                double winRate = resolved == 0 ? 0 : r.Wins * 100.0 / resolved;
                double netAfterCost = r.PayoutCash - r.EvaluationCost;
                sb.AppendLine(string.Join(",", new[] { Csv(r.Symbol), r.SetupMinutes.ToString(), Csv(r.StartMode), r.PoolSize.ToString(), r.Setups.ToString(), r.Wins.ToString(), r.Losses.ToString(), r.SessionExits.ToString(), winRate.ToString(CultureInfo.InvariantCulture), r.AllOutcomeGross.ToString(CultureInfo.InvariantCulture), r.Assigned.ToString(), r.Skipped.ToString(), r.AssignedGross.ToString(CultureInfo.InvariantCulture), r.EvaluationPassed.ToString(), r.Funded.ToString(), r.Payouts.ToString(), r.PayoutCash.ToString(CultureInfo.InvariantCulture), r.EvaluationCost.ToString(CultureInfo.InvariantCulture), netAfterCost.ToString(CultureInfo.InvariantCulture), Csv(r.DataMethod), Csv(r.Note) }));
            }
            return sb.ToString();
        }

        private static string Csv(string s) { return "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\""; }
        private static string DataDirectory() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeystoneArc5MResearch"); }
        private static string SessionDateLabel(KeystoneArcRunConfig cfg)
        {
            if (cfg == null || cfg.Start == DateTime.MinValue) return "not selected";
            DateTime first = KeystoneArcEngine.SessionGroupingDate(cfg.Start, cfg);
            DateTime last = KeystoneArcEngine.SessionGroupingDate(cfg.End, cfg);
            return cfg.OneDayMode == 1 ? first.ToString("yyyy-MM-dd") : first.ToString("yyyy-MM-dd") + " → " + last.ToString("yyyy-MM-dd");
        }
        private string SelectedTestDateLabel()
        {
            return SessionDateLabel(config);
        }

        private string BuildStudyScopeLabel()
        {
            if (config == null || config.Start == DateTime.MinValue) return "CURRENT STUDY • configure a date, session, instruments, and setup timeframe.";
            bool asian = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase);
            string setup = asian ? "ASIAN CYCLE BACKTEST • COPY" : (config.EnableBh == 1 && config.EnableFvg == 1 ? "BH + FVG" : (config.EnableBh == 1 ? "BH" : "FVG"));
            string detail = asian
                ? config.AsianStartHhmm.ToString("0000") + " ET " + config.AsianMnqInitialDirection + " MNQ / " + config.AsianMgcInitialDirection + " MGC • " + (config.AsianRiskMode == "PRICE" ? "PRICE-MOVE REVERSALS" : "FIXED-CASH REVERSALS")
                : (config.BhAggressionFilter == "STRONGER" ? "STRONGER BH" : "ALL VALID BH");
            return "CURRENT STUDY • SESSION DATE(S) " + SessionDateLabel(config) + " • DATA WINDOW " + config.Start.ToString("yyyy-MM-dd HH:mm") + " → " + config.End.ToString("yyyy-MM-dd HH:mm") + " • " + config.Scope + " • " + config.SessionMode + " • " + config.SetupMinutes + "M • " + setup + " • " + detail;
        }

        private string BuildEvidenceStudyLabel()
        {
            if (config == null || config.Start == DateTime.MinValue) return "CURRENT TEST RANGE • not configured";
            string setup = string.Equals(config.StrategyCode, "ASIAN75", StringComparison.OrdinalIgnoreCase) ? "ASIAN 75 REVERSAL • COPY • FIXED 1M" : (config.BhAggressionFilter == "STRONGER" ? "STRONGER BH FILTER" : "ALL VALID BH");
            return "CURRENT TEST RANGE • " + SessionDateLabel(config) + " • " + config.Scope + " • " + config.SessionMode + " • " + config.SetupMinutes + "-MINUTE SETUPS • " + setup + " • SELECT A DATE TAB BELOW TO INSPECT ONE COMPLETE SESSION";
        }
        private static void GetConfiguredSessionBounds(DateTime firstDate, DateTime lastDate, KeystoneArcRunConfig cfg, out DateTime start, out DateTime end)
        {
            bool overnight = KeystoneArcEngine.UsesOvernightSessionDate(cfg);
            int startHhmm = cfg.SessionMode == "ASIA" ? 1900 : ((cfg.SessionMode == "FULL_GLOBEX" || cfg.SessionMode == "ALL_ELIGIBLE") ? 1800 : (cfg.SessionMode == "ASIAN75" ? cfg.AsianStartHhmm : cfg.CustomStart));
            if (cfg.SessionMode == "LONDON") startHhmm = 300;
            if (cfg.SessionMode == "NY_EARLY") startHhmm = 800;
            if (cfg.SessionMode == "NY_OPEN" || cfg.SessionMode == "NY_AFTER_0930") startHhmm = 930;
            // BOTH must request from the earliest instrument-specific session (MGC 08:00).
            // The detector still applies MNQ 09:30 / MGC 08:00 independently, but the shared
            // chart and export window must never silently start at midnight.
            if (cfg.SessionMode == "INSTRUMENT_DEFAULT") startHhmm = 800;
            int endHhmm = cfg.SessionMode == "ASIA" ? 300 : (cfg.SessionMode == "LONDON" ? 1130 : (cfg.SessionMode == "ASIAN75" ? cfg.AsianEndHhmm : cfg.EndTime));
            start = firstDate.Date.AddHours(startHhmm / 100).AddMinutes(startHhmm % 100);
            end = lastDate.Date.AddHours(endHhmm / 100).AddMinutes(endHhmm % 100);
            if (overnight || end < start) end = end.AddDays(1);
        }
        private DateTime ConfiguredSessionStart(DateTime sessionDate)
        {
            if (config == null) return sessionDate.Date.AddHours(18);
            DateTime start, end; GetConfiguredSessionBounds(sessionDate.Date, sessionDate.Date, config, out start, out end); return start;
        }
        private DateTime TradingSessionEnd(DateTime sessionDate)
        {
            if (config == null) return sessionDate.Date.AddDays(1).AddHours(15).AddMinutes(55);
            DateTime start, end; GetConfiguredSessionBounds(sessionDate.Date, sessionDate.Date, config, out start, out end); return end;
        }
        private void UpdateUi(string text, Brush brush) { if (statusText != null) { statusText.Text = text; statusText.Foreground = brush; } }
        private static double Number(TextBox box, double fallback) { double v; return box != null && double.TryParse(box.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out v) && v > 0 ? v : fallback; }
        private static double NumberAllowZero(TextBox box, double fallback) { double v; return box != null && double.TryParse(box.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out v) && v >= 0 ? v : fallback; }
        private static int Integer(TextBox box, int fallback) { int v; return box != null && int.TryParse(box.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out v) && v >= 0 ? v : fallback; }
        private static int Number(ComboBox box, int fallback) { int v; return box != null && int.TryParse((box.SelectedItem ?? "").ToString(), out v) ? v : fallback; }
        private static int SetupMinutesFromDisplay(string value)
        {
            string first = (value ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            int minutes;
            return int.TryParse(first, out minutes) && (minutes == 1 || minutes == 5 || minutes == 15 || minutes == 30 || minutes == 60 || minutes == 240) ? minutes : 5;
        }
        private static string DirectionModeFromDisplay(string value)
        {
            string first = (value ?? string.Empty).Trim().Split(new[] { ' ', '•' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            first = first.ToUpperInvariant();
            return first == "SS" || first == "SL" || first == "BS" ? first : "BB";
        }
        private static bool IsValidHhmm(int value) { return value >= 0 && value <= 2359 && value % 100 < 60; }
        private static string SessionModeFromDisplay(string value)
        {
            string v = (value ?? string.Empty).ToUpperInvariant();
            if (v.StartsWith("ASIAN 75")) return "ASIAN75";
            if (v.StartsWith("ASIA")) return "ASIA";
            if (v.StartsWith("LONDON")) return "LONDON";
            if (v.StartsWith("NY EARLY")) return "NY_EARLY";
            if (v.StartsWith("NY OPEN")) return "NY_OPEN";
            if (v.StartsWith("FULL GLOBEX")) return "FULL_GLOBEX";
            if (v.StartsWith("CUSTOM")) return "CUSTOM";
            return "INSTRUMENT_DEFAULT";
        }
        private static SolidColorBrush ColorBrush(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
        private static string Cash(double value)
        {
            return value < 0 ? "-$" + Math.Abs(value).ToString("N0", CultureInfo.InvariantCulture) : "$" + value.ToString("N0", CultureInfo.InvariantCulture);
        }
        private static TextBlock MetricTile(Panel parent, string label, string initialValue, string caption, Brush accent, double minHeight = 82, double valueFontSize = 25)
        {
            var content = new StackPanel { Margin = new Thickness(5) };
            var title = new TextBlock { Text = label, Foreground = accent, FontSize = 10, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            content.Children.Add(title);
            var value = new TextBlock { Text = initialValue, Foreground = accent, FontSize = valueFontSize, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 3, 0, 0) };
            content.Children.Add(value);
            var note = new TextBlock { Text = caption, Foreground = Muted, FontSize = 9, FontWeight = FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            content.Children.Add(note);
            var border = new Border { Background = Card, BorderBrush = accent, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Padding = new Thickness(6), Margin = new Thickness(3), MinHeight = minHeight, Child = content };
            value.Tag = Tuple.Create(title, note, border);
            parent.Children.Add(border);
            return value;
        }

        private static void UpdateMetricTile(TextBlock value, string label, string displayedValue, string caption, Brush accent)
        {
            if (value == null) return;
            value.Text = displayedValue;
            value.Foreground = accent;
            value.ToolTip = caption;
            var parts = value.Tag as Tuple<TextBlock, TextBlock, Border>;
            if (parts == null) return;
            parts.Item1.Text = label;
            parts.Item1.Foreground = accent;
            parts.Item2.Text = caption;
            parts.Item3.BorderBrush = accent;
        }
        private static void CycleMetric(Panel parent, string label, string value, Brush accent)
        {
            var content = new StackPanel { Margin = new Thickness(5) };
            content.Children.Add(new TextBlock { Text = label, Foreground = accent, FontSize = 9, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = value, Foreground = accent, FontSize = 16, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            parent.Children.Add(new Border { Background = Card, BorderBrush = accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(2), MinHeight = 48, Child = content });
        }
        private static TextBlock Txt(string text, Brush brush, double size, FontWeight weight) { return new TextBlock { Text = text, Foreground = brush, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) }; }
        private static TextBox Input(string value) { return new TextBox { Text = value, Background = Card, Foreground = Text, Margin = new Thickness(6), Padding = new Thickness(5) }; }
        private static ComboBox Select(params string[] values) { var x = new ComboBox { Background = Card, Foreground = Text, Margin = new Thickness(6) }; foreach (string s in values) x.Items.Add(s); return x; }
        private static Button Btn(string text, Brush brush) { return new Button { Content = text, Background = brush, Foreground = Text, FontWeight = FontWeights.Bold, Height = 32, Margin = new Thickness(4) }; }
        private static StackPanel Stack() { return new StackPanel { Margin = new Thickness(8) }; }
        private static Grid TwoColumns() { var g = new Grid { Margin = new Thickness(6) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); return g; }
        private static Border PanelCard(UIElement child) { return new Border { Background = Panel, BorderBrush = Card, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Margin = new Thickness(6), Child = child }; }
        private static Grid Row(string label, UIElement control) { var g = new Grid { Margin = new Thickness(2) }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.Children.Add(Txt(label, Muted, 10, FontWeights.Bold)); Grid.SetColumn(control, 1); g.Children.Add(control); return g; }
        // Pool settings must remain readable at normal NinjaTrader window sizes.  These rows have
        // a stable compact width, so a WrapPanel creates clean rows instead of vertically-stretched
        // controls or a hidden lower half of the settings page.
        private static Grid PoolRow(string label, UIElement control) { var g = new Grid { Width = 270, MinHeight = 34, Margin = new Thickness(3, 2, 3, 2), VerticalAlignment = VerticalAlignment.Top }; g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) }); var caption = Txt(label, Muted, 9, FontWeights.Bold); caption.TextWrapping = TextWrapping.Wrap; caption.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(caption); FrameworkElement compactControl = control as FrameworkElement; if (compactControl != null) { compactControl.VerticalAlignment = VerticalAlignment.Center; compactControl.Height = 28; compactControl.Margin = new Thickness(3, 1, 3, 1); } Grid.SetColumn(control, 1); g.Children.Add(control); return g; }
    }
}
