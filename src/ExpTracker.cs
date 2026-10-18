using System;
using System.Collections.Generic;

namespace MapleExpTracker
{
    public enum FeedOutcome { NoData, Starting, Started, Unchanged, Gained, Confirming, Jumped, Lost, LevelUp, Resynced, Rejected }

    /// <summary>
    /// Follows the character's EXP from successive screen readings and works out session gains,
    /// EXP per hour and time to level.
    ///
    /// The EXP a level needs follows from the displayed number and percentage. While those match
    /// the MapleStory Classic EXP table, the table decides the level and every reading has to fit
    /// a real level, which filters out misreads (a cursor over the digits, a popup, OCR noise).
    /// If the game's numbers ever stop matching the table, the tracker works from the
    /// percentage alone.
    /// </summary>
    public sealed class ExpTracker
    {
        public static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan LongRateWindow = TimeSpan.FromHours(1);
        static readonly TimeSpan MinRateWindow = TimeSpan.FromSeconds(30);
        static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(20);
        const double Slack = 0.003;          // tolerance when comparing level sizes
        const double MaxStepShare = 0.25;    // single gains bigger than this share of a level must be seen twice
        const double MaxDeathShare = 0.15;   // a death never costs more than this share of a level
        const double MaxLevelGrowth = 4.0;   // the next level never needs more than this many times the EXP
        const int TableGiveUp = 20;          // precise readings that miss the table before it's switched off

        enum TableState { Unknown, Matches, Off }

        struct Sample
        {
            public readonly DateTime Time;
            public readonly long Gain;
            public Sample(DateTime time, long gain) { Time = time; Gain = gain; }
        }

        readonly List<Sample> history = new List<Sample>();
        ExpReading pending;
        FeedOutcome pendingKind;
        int pendingCount;
        DateTime pendingTime;
        bool resync;
        int level;
        TableState table;
        int tableMisses;

        public ExpTracker()
        {
            Percent = double.NaN;
            TotalHi = double.PositiveInfinity;
            LastNote = "";
        }

        public bool Started { get; private set; }
        public long Exp { get; private set; }
        public double Percent { get; private set; }
        public double TotalLo { get; private set; } // bounds on the EXP the current level needs
        public double TotalHi { get; private set; }
        public long SessionGain { get; private set; }
        public DateTime SessionStart { get; private set; }
        public DateTime LastGood { get; private set; }
        public string LastNote { get; private set; }

        /// <summary>Character level, 0 when unknown. Detected from the EXP table when possible.</summary>
        public int Level
        {
            get { return level; }
            set
            {
                level = value;
                PinLevelSize();
            }
        }

        /// <summary>True once the game's numbers have been seen to match the EXP table.</summary>
        public bool UsesTable
        {
            get { return table == TableState.Matches; }
        }

        /// <summary>The EXP the current level needs, or 0 when unknown.</summary>
        public double LevelSize
        {
            get
            {
                long required = TableRequired();
                if (required > 0) return required;
                return double.IsInfinity(TotalHi) ? 0 : (TotalLo + TotalHi) / 2;
            }
        }

        public double DisplayPercent
        {
            get
            {
                if (!double.IsNaN(Percent)) return Percent;
                double size = LevelSize;
                return size > 0 ? Exp * 100.0 / size : double.NaN;
            }
        }

        public FeedOutcome Feed(IList<ExpReading> candidates, DateTime now)
        {
            if (candidates == null || candidates.Count == 0)
            {
                LastNote = "No EXP numbers found in the capture.";
                return FeedOutcome.NoData;
            }
            if (!Started || resync) return FeedStart(candidates, now);

            ExpReading confirm = null;
            FeedOutcome confirmKind = FeedOutcome.Rejected;
            foreach (ExpReading c in candidates)
            {
                FeedOutcome kind = Classify(c);
                if (kind == FeedOutcome.Unchanged)
                {
                    Refine(c);
                    ClearPending();
                    LastGood = now;
                    LastNote = "No change.";
                    return kind;
                }
                if (kind == FeedOutcome.Gained)
                {
                    long gain = c.Exp - Exp;
                    Refine(c);
                    ClearPending();
                    Record(c, gain, now);
                    LastNote = "Gained " + Fmt.Exp(gain) + " EXP.";
                    return kind;
                }
                if (confirm == null && kind != FeedOutcome.Rejected)
                {
                    confirm = c;
                    confirmKind = kind;
                }
            }
            if (confirm == null)
            {
                LastNote = UsesTable ? "Reading doesn't match your level in the EXP table, ignored." : "Reading doesn't fit the current level, ignored.";
                return FeedOutcome.Rejected;
            }
            return Confirm(confirm, confirmKind, now);
        }

        /// <summary>Adopt the next readings without counting EXP (after a pause or a region change).</summary>
        public void BeginResync()
        {
            if (Started) resync = true;
            ClearPending();
        }

        public void ResetSession(DateTime now)
        {
            SessionStart = now;
            SessionGain = 0;
            history.Clear();
            history.Add(new Sample(now, 0));
        }

        public long GainSince(DateTime time)
        {
            if (!Started) return 0;
            long before = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Time <= time)
                {
                    before = history[i].Gain;
                    break;
                }
            }
            return SessionGain - before;
        }

        public long RecentGain(DateTime now)
        {
            return GainSince(now - RecentWindow);
        }

        /// <summary>EXP per hour over the last 30 minutes (or the whole session if shorter).</summary>
        public bool TryGetRate(DateTime now, out double perHour)
        {
            return TryGetRate(now, RateWindow, out perHour);
        }

        /// <summary>EXP per hour over the last <paramref name="span"/> (or the whole session if shorter).</summary>
        public bool TryGetRate(DateTime now, TimeSpan span, out double perHour)
        {
            perHour = 0;
            if (!Started) return false;
            TimeSpan elapsed = now - SessionStart;
            TimeSpan window = elapsed < span ? elapsed : span;
            if (window < MinRateWindow) return false;
            perHour = GainSince(now - window) / window.TotalHours;
            return true;
        }

        /// <summary>How long the session has been running (the tracker's clock stops while paused).</summary>
        public TimeSpan SessionLength(DateTime now)
        {
            return Started && now > SessionStart ? now - SessionStart : TimeSpan.Zero;
        }

        public bool TryGetTimeToLevel(DateTime now, out TimeSpan eta)
        {
            eta = TimeSpan.Zero;
            double rate, size = LevelSize;
            if (size <= 0 || !TryGetRate(now, out rate) || rate <= 0) return false;
            double hours = Math.Max(0, size - Exp) / rate;
            if (hours > 24 * 999) return false;
            eta = TimeSpan.FromHours(hours);
            return true;
        }

        // ------------------------------------------------------------ starting and re-syncing

        FeedOutcome FeedStart(IList<ExpReading> candidates, DateTime now)
        {
            ExpReading c = null;
            if (pending != null && pendingKind == FeedOutcome.Starting)
            {
                foreach (ExpReading x in candidates)
                {
                    if (Chains(pending, x) && TableLevel(x) != 0 && !Incomplete(x))
                    {
                        c = x;
                        break;
                    }
                }
            }
            if (c == null && table != TableState.Off)
                c = FirstOrNull(candidates, x => TableLevel(x) != 0 && !Incomplete(x));
            if (c == null)
            {
                if (UsesTable)
                {
                    LastNote = "Reading doesn't match any level in the EXP table, ignored.";
                    return FeedOutcome.Rejected;
                }
                c = FirstOrNull(candidates, x => !Incomplete(x));
                if (c == null)
                {
                    LastNote = "Partial reading (no percentage), ignored.";
                    return FeedOutcome.Rejected;
                }
            }
            if (!Continue(c, FeedOutcome.Starting, now)) Begin(c, FeedOutcome.Starting, now);

            int needed = pending.Quality >= ExpReading.High ? 2 : 3;
            if (pendingCount < needed)
            {
                LastNote = "Reading EXP (" + pendingCount + "/" + needed + ").";
                return FeedOutcome.Starting;
            }
            ExpReading r = pending;
            ClearPending();
            resync = false;
            NoteTable(r);
            int match = UsesTable ? TableLevel(r) : -1;

            if (Started)
            {
                bool leveled = false;
                if (match > 0)
                {
                    leveled = match != level;
                    level = match;
                }
                else if (!UsesTable && !Overlaps(r.Lo, r.Hi, TotalLo, TotalHi) && LooksLikeLevelUp(r))
                {
                    leveled = true;
                    if (level > 0) level++;
                }
                Adopt(r, now);
                LastNote = leveled ? "Re-synced (now level " + level + ")." : "Re-synced.";
                return FeedOutcome.Resynced;
            }

            if (match > 0) level = match;
            Adopt(r, now);
            Started = true;
            ResetSession(now);
            LastNote = match > 0 ? "Tracking started. Level " + match + " detected from your EXP." : "Tracking started.";
            return FeedOutcome.Started;
        }

        // ------------------------------------------------------------ classifying a reading

        /// <summary>
        /// Once the display has shown a percentage, a reading without one is a partial read (the
        /// percentage is covered, e.g. by the mouse cursor) and can't be checked, so it's ignored.
        /// </summary>
        bool Incomplete(ExpReading c)
        {
            return !c.HasPercent && c.Total <= 0 && (UsesTable || !double.IsNaN(Percent));
        }

        FeedOutcome Classify(ExpReading c)
        {
            if (Incomplete(c)) return FeedOutcome.Rejected;
            if (UsesTable && ExpTable.Required(level) > 0) return ClassifyWithTable(c);

            long delta = c.Exp - Exp;
            if (Overlaps(c.Lo, c.Hi, TotalLo, TotalHi))
            {
                if (delta == 0) return FeedOutcome.Unchanged;
                if (delta > 0) return delta <= MaxStep(TotalLo, TotalHi, Exp) ? FeedOutcome.Gained : FeedOutcome.Jumped;

                double size = LevelSize > 0 ? LevelSize : TotalLo;
                bool bigDrop = size > Exp ? -delta > MaxDeathShare * size : c.Exp < Exp / 2;
                if (!bigDrop) return FeedOutcome.Lost;
                // Too big for a death. It's a level up only if this reading can't tell the level
                // sizes apart (e.g. "50 [0.00%]"); a precise reading of the same size is a misread.
                if (!Precise(c) && PercentFell(c)) return FeedOutcome.LevelUp;
                return c.Quality >= ExpReading.High ? FeedOutcome.Resynced : FeedOutcome.Rejected;
            }
            // The reading implies a different level size.
            if (LooksLikeLevelUp(c)) return FeedOutcome.LevelUp;
            return c.Quality >= ExpReading.High ? FeedOutcome.Resynced : FeedOutcome.Rejected;
        }

        /// <summary>
        /// With the EXP table every reading must fit a real level: the current one (gain, death),
        /// the next one right after EXP resets (level up), or clearly another one (re-sync, e.g. a
        /// character switch). Anything else is a misread.
        /// </summary>
        FeedOutcome ClassifyWithTable(ExpReading c)
        {
            int match = TableLevel(c);
            if (match == 0) return FeedOutcome.Rejected;

            long need = ExpTable.Required(level);
            long delta = c.Exp - Exp;
            bool reset = delta < 0 && PercentFell(c) && -delta > MaxDeathShare * need;
            if (reset && ExpTable.Fits(level + 1, c.Lo, c.Hi)) return FeedOutcome.LevelUp;
            if (ExpTable.Fits(level, c.Lo, c.Hi))
            {
                if (delta == 0) return FeedOutcome.Unchanged;
                if (delta > 0) return delta <= Math.Max(MaxStepShare * need, 50) ? FeedOutcome.Gained : FeedOutcome.Jumped;
                return reset ? FeedOutcome.Rejected : FeedOutcome.Lost;
            }
            return match > 0 && c.Quality >= ExpReading.High ? FeedOutcome.Resynced : FeedOutcome.Rejected;
        }

        /// <summary>
        /// Without the table: a level up resets EXP, so it must go down, the percentage must fall,
        /// and the new level must need more EXP than the old one (but not absurdly more).
        /// </summary>
        bool LooksLikeLevelUp(ExpReading c)
        {
            if (!c.KnowsLevelSize || c.Exp >= Exp || !PercentFell(c)) return false;
            if (c.Lo < TotalLo * (1 - Slack)) return false;
            double size = LevelSize > 0 ? LevelSize : TotalLo;
            return size <= 0 || c.Lo <= size * MaxLevelGrowth;
        }

        bool PercentFell(ExpReading c)
        {
            double p = c.PercentValue;
            return double.IsNaN(p) || double.IsNaN(Percent) || p < Percent - 1;
        }

        static bool Precise(ExpReading c)
        {
            return c.KnowsLevelSize && c.Hi <= c.Lo * 1.02;
        }

        // ------------------------------------------------------------ EXP table

        /// <summary>The level the reading fits in the EXP table: the level, 0 for none, -1 if it can't tell.</summary>
        int TableLevel(ExpReading c)
        {
            if (table == TableState.Off || !c.KnowsLevelSize) return -1;
            return ExpTable.Match(c.Lo, c.Hi);
        }

        /// <summary>Learns whether the game's numbers follow the EXP table.</summary>
        void NoteTable(ExpReading r)
        {
            if (table == TableState.Off) return;
            int match = ExpTable.Match(r.Lo, r.Hi);
            if (r.KnowsLevelSize && match > 0)
            {
                table = TableState.Matches;
                tableMisses = 0;
            }
            else if (table == TableState.Unknown && Precise(r) && match == 0 && ++tableMisses >= TableGiveUp)
            {
                table = TableState.Off;
            }
        }

        long TableRequired()
        {
            return UsesTable ? ExpTable.Required(level) : 0;
        }

        void PinLevelSize()
        {
            long required = TableRequired();
            if (required > 0) TotalLo = TotalHi = required;
        }

        // ------------------------------------------------------------ confirming changes

        FeedOutcome Confirm(ExpReading c, FeedOutcome kind, DateTime now)
        {
            // Switching to a different level without a level up needs a longer, precise look.
            if (kind == FeedOutcome.Resynced && UsesTable && !Precise(c)) return FeedOutcome.Rejected;
            int needed = kind == FeedOutcome.Resynced ? 5 : 2;
            if (!Continue(c, kind, now)) Begin(c, kind, now);
            if (pendingCount < needed)
            {
                LastNote = "Checking a possible " + Describe(kind) + " (" + pendingCount + "/" + needed + ").";
                return FeedOutcome.Confirming;
            }

            ExpReading r = pending;
            ClearPending();
            switch (kind)
            {
                case FeedOutcome.Jumped:
                case FeedOutcome.Lost:
                    long delta = r.Exp - Exp;
                    Refine(r);
                    Record(r, delta, now);
                    LastNote = (delta >= 0 ? "Gained " : "Lost ") + Fmt.Exp(Math.Abs(delta)) + " EXP.";
                    break;
                case FeedOutcome.LevelUp:
                    long need = TableRequired();
                    double size = need > 0 ? need : LevelSize > 0 ? LevelSize : TotalLo;
                    long rest = size > Exp ? (long)Math.Round(size - Exp) : 0;
                    if (level > 0) level++;
                    TotalLo = r.Lo;
                    TotalHi = r.Hi;
                    PinLevelSize();
                    Record(r, rest + r.Exp, now);
                    LastNote = level > 0 ? "Level up! Now level " + level + "." : "Level up!";
                    break;
                default:
                    int match = TableLevel(r);
                    if (UsesTable && match > 0) level = match;
                    Adopt(r, now);
                    LastNote = UsesTable && match > 0
                        ? "EXP fits level " + match + "; re-synced without counting EXP."
                        : "EXP display changed; re-synced without counting EXP.";
                    break;
            }
            return kind;
        }

        static string Describe(FeedOutcome kind)
        {
            switch (kind)
            {
                case FeedOutcome.Jumped: return "big EXP jump";
                case FeedOutcome.Lost: return "EXP loss";
                case FeedOutcome.LevelUp: return "level up";
                default: return "change of level or EXP display";
            }
        }

        bool Continue(ExpReading c, FeedOutcome kind, DateTime now)
        {
            if (pending == null || pendingKind != kind || now - pendingTime > PendingTimeout || !Chains(pending, c))
                return false;
            c.Lo = Math.Max(c.Lo, pending.Lo);
            c.Hi = Math.Min(c.Hi, pending.Hi);
            if (c.Lo > c.Hi) c.Lo = c.Hi = (c.Lo + c.Hi) / 2;
            pending = c;
            pendingCount++;
            pendingTime = now;
            return true;
        }

        void Begin(ExpReading c, FeedOutcome kind, DateTime now)
        {
            pending = c;
            pendingKind = kind;
            pendingCount = 1;
            pendingTime = now;
        }

        void ClearPending()
        {
            pending = null;
            pendingCount = 0;
        }

        /// <summary>True when <paramref name="c"/> plausibly follows <paramref name="p"/> on the same level.</summary>
        static bool Chains(ExpReading p, ExpReading c)
        {
            return Overlaps(c.Lo, c.Hi, p.Lo, p.Hi) && c.Exp >= p.Exp && c.Exp - p.Exp <= MaxStep(p.Lo, p.Hi, p.Exp);
        }

        static double MaxStep(double lo, double hi, long exp)
        {
            double size = double.IsInfinity(hi) ? lo : (lo + hi) / 2;
            if (size > exp && size > 0) return Math.Max(MaxStepShare * size, 50);
            return Math.Max(1000, exp * 0.5);
        }

        static bool Overlaps(double lo1, double hi1, double lo2, double hi2)
        {
            return lo1 <= hi2 * (1 + Slack) && lo2 <= hi1 * (1 + Slack);
        }

        static ExpReading FirstOrNull(IList<ExpReading> list, Predicate<ExpReading> match)
        {
            foreach (ExpReading r in list)
                if (match(r)) return r;
            return null;
        }

        // ------------------------------------------------------------ state updates

        void Refine(ExpReading c)
        {
            NoteTable(c);
            if (UsesTable && level <= 0)
            {
                int match = TableLevel(c);
                if (match > 0) level = match;
            }
            if (TableRequired() > 0)
            {
                PinLevelSize();
            }
            else
            {
                TotalLo = Math.Max(TotalLo, c.Lo);
                TotalHi = Math.Min(TotalHi, c.Hi);
                if (TotalLo > TotalHi) TotalLo = TotalHi = (TotalLo + TotalHi) / 2;
            }
            if (!double.IsNaN(c.PercentValue)) Percent = c.PercentValue;
        }

        void Adopt(ExpReading r, DateTime now)
        {
            Exp = r.Exp;
            Percent = r.PercentValue;
            TotalLo = r.Lo;
            TotalHi = r.Hi;
            PinLevelSize();
            LastGood = now;
        }

        void Record(ExpReading r, long gain, DateTime now)
        {
            SessionGain += gain;
            Exp = r.Exp;
            Percent = r.PercentValue;
            LastGood = now;
            history.Add(new Sample(now, SessionGain));

            DateTime cutoff = now - LongRateWindow - TimeSpan.FromMinutes(1);
            int drop = 0;
            while (drop + 1 < history.Count && history[drop + 1].Time <= cutoff) drop++;
            if (drop > 0) history.RemoveRange(0, drop);
        }
    }
}
