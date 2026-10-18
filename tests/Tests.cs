// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace MapleExpTracker.Tests
{
    static class TestProgram
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly DateTime T0 = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        static int passed, failed;

        [STAThread]
        static int Main(string[] args)
        {
            ParserTests();
            TableTests();
            TrackerTests();
            FalseLevelUpTests();
            FormatTests();
            PixelFontTests();
            OcclusionTests();
            OcrTests();
            ScreenCaptureTest();
            if (args.Length >= 4) ImageFileTest(args[0], args[1], long.Parse(args[2], Inv), double.Parse(args[3], Inv));

            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        static void Check(bool ok, string name, string detail)
        {
            if (ok) passed++;
            else failed++;
            Console.WriteLine((ok ? "ok    " : "FAIL  ") + name + (detail != null ? "   -> " + detail : ""));
        }

        // ---------------------------------------------------------------- parser

        static void ParserTests()
        {
            Console.WriteLine("== Parser");
            ExpectTop("EXP 613128 [62.73%]", 613128, 62.73, true);
            ExpectTop("EXP 613,128 [62.73%]", 613128, 62.73, true);
            ExpectTop("613128[62.73%]", 613128, 62.73, true);
            ExpectTop("EXP 613128 (62.73 %)", 613128, 62.73, true);
            ExpectTop("EXP 613128 [ 62.73% ]", 613128, 62.73, true);
            ExpectTop("EXP 613128 [62,73%]", 613128, 62.73, true);
            ExpectTop("EXP 6l3128 [62.73%]", 613128, 62.73, true);
            ExpectTop("EXP 613128 l62.73%]", 613128, 62.73, true);
            ExpectTop("EXP 613128 |62.73%|", 613128, 62.73, true);
            ExpectTop("EXP 613128 [62.73]", 613128, 62.73, true);
            ExpectTop("EXP 1234567 [8.050/01", 1234567, 8.05, true);
            ExpectTop("EXP 613128 [62.73o/o]", 613128, 62.73, true);
            // misreads seen from Windows OCR on outlined bar text
            ExpectTop("EXP S (0.33%)", 5, 0.33, true);
            ExpectTop("1.234.S67 (8.05%)", 1234567, 8.05, true);
            ExpectTop("EXP 5 10.33%)", 5, 0.33, true);
            ExpectAny("EXP 5 10.33%)", 5, 10.33);
            ExpectTop("FXP 99.999 199.99%)", 99999, 99.99, true);
            ExpectTop("EXP 1,234.567 18.05%)", 1234567, 8.05, true);
            ExpectTop("UP 613.128 [62.73%)", 613128, 62.73, true);
            ExpectTop("EXP 61312O [62.73%]", 613120, 62.73, true);
            ExpectTop("EXP 0 [0.00%]", 0, 0, true);
            ExpectTop("EXP 5 [0.33%]", 5, 0.33, true);
            ExpectTop("EXP 1,234,567 [12.5%]", 1234567, 12.5, true);
            ExpectTop("EXP 613128 [62%]", 613128, 62, true);
            ExpectTop("613,128 / 977,408", 613128, 62.73, true);
            ExpectTop("613128 / 977408 [62.73%]", 613128, 62.73, true);
            ExpectTop("EXP 613128 [6273%]", 613128, 62.73, false);
            ExpectTop("EXP 613128162.73%]", 613128, 62.73, false);
            ExpectAny("61312862.73%", 613128, 62.73);
            ExpectAny("EXP 613128 162.73%]", 613128, 62.73);
            ExpectNone("");
            ExpectNone("Loading...");
            ExpectNone("HP 100/100 MP 50/50");
            ExpectNone("62.73%");
        }

        static void ExpectTop(string text, long exp, double pct, bool clean)
        {
            List<ExpReading> r = ExpParser.Parse(text);
            bool ok = r.Count > 0 && r[0].Exp == exp && Math.Abs(r[0].PercentValue - pct) < 0.01
                && (r[0].Quality >= ExpReading.High) == clean;
            Check(ok, "parse \"" + text + "\"", r.Count == 0 ? "nothing" : Describe(r));
        }

        static void ExpectAny(string text, long exp, double pct)
        {
            List<ExpReading> r = ExpParser.Parse(text);
            bool ok = r.Exists(x => x.Exp == exp && Math.Abs(x.PercentValue - pct) < 0.01);
            Check(ok, "parse \"" + text + "\" (contains)", r.Count == 0 ? "nothing" : Describe(r));
        }

        static void ExpectNone(string text)
        {
            List<ExpReading> r = ExpParser.Parse(text);
            Check(r.Count == 0, "parse \"" + text + "\" (nothing)", r.Count == 0 ? null : Describe(r));
        }

        static string Describe(List<ExpReading> r)
        {
            var parts = new List<string>();
            foreach (ExpReading x in r) parts.Add(x.ToString());
            return string.Join(" | ", parts.ToArray());
        }

        // ---------------------------------------------------------------- tracker

        static List<ExpReading> At(long exp, long levelSize)
        {
            double pct = Math.Round(exp * 10000.0 / levelSize) / 100;  // the game rounds to 2 decimals
            return ExpParser.Parse("EXP " + exp + " [" + pct.ToString("0.00", Inv) + "%]");
        }

        static void TableTests()
        {
            Console.WriteLine("== EXP table");
            Check(ExpTable.Required(1) == 15 && ExpTable.Required(41) == 324240 && ExpTable.Required(99) == 9482161548L, "known values", null);
            Check(ExpTable.Required(0) == 0 && ExpTable.Required(100) == 0, "outside 1-99", null);
            // Formulas published with the table.
            long prev = 0;
            int bad = 0;
            for (int level = 1; level <= ExpTable.MaxLevel; level++)
            {
                long f;
                if (level <= 5) f = (level * level / 2 + 15) * level;
                else if (level <= 50) { long q = level * level / 3; f = (q + 19) * q; }
                else if (level <= 89) f = (long)Math.Truncate(prev * 1.0548m);
                else f = (long)Math.Truncate(prev * 2.1m);
                if (f != ExpTable.Required(level)) bad++;
                prev = ExpTable.Required(level);
            }
            Check(bad == 0, "every level matches the published formulas", bad + " mismatches");
            ExpReading r = ExpParser.Parse("EXP 194,111 [59.87%]")[0];
            Check(ExpTable.Match(r.Lo, r.Hi) == 41, "194,111 at 59.87% is level 41", ExpTable.Match(r.Lo, r.Hi).ToString(Inv));
            r = ExpParser.Parse("EXP 194,111 [5.87%]")[0];
            Check(ExpTable.Match(r.Lo, r.Hi) == 0, "a misread matches no level", ExpTable.Match(r.Lo, r.Hi).ToString(Inv));
        }

        static void TrackerTests()
        {
            Console.WriteLine("== Tracker");
            long L56 = ExpTable.Required(56), L57 = ExpTable.Required(57);
            var t = new ExpTracker { Level = 56 };
            DateTime now = T0;
            Func<int, DateTime> Sec = s => T0.AddSeconds(s);

            Check(t.Feed(At(613128, L56), Sec(0)) == FeedOutcome.Starting, "first reading waits for confirmation", null);
            Check(t.Feed(At(613128, L56), Sec(1)) == FeedOutcome.Started && t.Exp == 613128, "second reading starts tracking", null);
            Check(t.LevelSize == L56 && t.UsesTable, "level size from the EXP table", t.LevelSize.ToString("0", Inv));
            Check(t.Feed(At(614000, L56), Sec(2)) == FeedOutcome.Gained && t.SessionGain == 872, "kill gain counted", t.SessionGain.ToString(Inv));

            List<ExpReading> misread = ExpParser.Parse("EXP 61400 [62.81%]");
            FeedOutcome o = t.Feed(misread, Sec(3));
            Check(o != FeedOutcome.Gained && t.Exp == 614000, "single OCR misread ignored", o.ToString());
            Check(t.Feed(At(615000, L56), Sec(4)) == FeedOutcome.Gained && t.SessionGain == 1872, "tracking continues after misread", t.SessionGain.ToString(Inv));

            Check(t.Feed(At(517260, L56), Sec(5)) == FeedOutcome.Confirming, "EXP loss waits for confirmation", null);
            Check(t.Feed(At(517260, L56), Sec(6)) == FeedOutcome.Lost && t.SessionGain == 1872 - 97740, "death penalty subtracted", t.SessionGain.ToString(Inv));

            t.Feed(At(700000, L56), Sec(7));
            t.Feed(At(900000, L56), Sec(8));
            t.Feed(At(977000, L56), Sec(9));
            long beforeLevel = t.SessionGain;
            Check(beforeLevel == 1872 - 97740 + (977000 - 517260), "steady gains counted", beforeLevel.ToString(Inv));

            Check(t.Feed(At(12000, L57), Sec(10)) == FeedOutcome.Confirming, "level up waits for confirmation", null);
            o = t.Feed(At(12000, L57), Sec(11));
            long levelGain = t.SessionGain - beforeLevel;
            Check(o == FeedOutcome.LevelUp && t.Level == 57, "level up detected", o + ", level " + t.Level);
            Check(levelGain == L56 - 977000 + 12000, "EXP across level up counted exactly", levelGain.ToString(Inv));
            Check(t.LevelSize == L57, "new level size from the table", t.LevelSize.ToString("0", Inv));

            // Level up where the new level shows 0.00%; the wrong starting level is corrected from the EXP.
            var z = new ExpTracker { Level = 10 };
            z.Feed(At(977000, L56), Sec(0));
            z.Feed(At(977000, L56), Sec(1));
            Check(z.Level == 56, "level detected from EXP (was set to 10)", "level " + z.Level);
            z.Feed(ExpParser.Parse("EXP 50 [0.00%]"), Sec(2));
            o = z.Feed(ExpParser.Parse("EXP 50 [0.00%]"), Sec(3));
            Check(o == FeedOutcome.LevelUp && z.Level == 57 && z.SessionGain == L56 - 977000 + 50, "level up into 0.00%", o + " level " + z.Level + " gain " + z.SessionGain);
            o = z.Feed(At(20000, L57), Sec(4));
            Check(o == FeedOutcome.Gained && z.LevelSize == L57, "gains after 0.00%", o + " size " + z.LevelSize.ToString("0", Inv));

            // Steady grinding: 1,000 EXP every 10 s for 40 minutes
            var r = new ExpTracker { Level = 56 };
            r.Feed(At(100000, L56), T0);
            r.Feed(At(100000, L56), T0);
            DateTime last = T0;
            for (int i = 1; i <= 240; i++)
            {
                last = T0.AddSeconds(i * 10);
                r.Feed(At(100000 + i * 1000, L56), last);
            }
            double rate;
            TimeSpan eta;
            Check(r.RecentGain(last) == 30000, "last 5 minutes", r.RecentGain(last).ToString(Inv));
            Check(r.TryGetRate(last, out rate) && Math.Abs(rate - 360000) < 1, "EXP/h over 30 minutes", rate.ToString("0", Inv));
            Check(r.TryGetTimeToLevel(last, out eta) && Math.Abs(eta.TotalMinutes - (L56 - 340000) / 6000.0) < 1, "time to level", Fmt.Duration(eta));
            Check(r.SessionGain == 240000, "session total", r.SessionGain.ToString(Inv));
            Check(r.TryGetRate(last, ExpTracker.LongRateWindow, out rate) && Math.Abs(rate - 360000) < 1, "EXP/h over 1 hour (session shorter than an hour)", rate.ToString("0", Inv));
            Check(r.SessionLength(last) == TimeSpan.FromMinutes(40), "session time", Fmt.Clock(r.SessionLength(last)));

            // An hour at 1,000 EXP per 10 s, then half an hour at 2,000: the 30-minute rate sees only the
            // faster half hour, the 1-hour rate sees half of each.
            var h = new ExpTracker { Level = 56 };
            h.Feed(At(100000, L56), T0);
            h.Feed(At(100000, L56), T0);
            long hexp = 100000;
            DateTime hlast = T0;
            for (int i = 1; i <= 540; i++)
            {
                hexp += i <= 360 ? 1000 : 2000;
                hlast = T0.AddSeconds(i * 10);
                h.Feed(At(hexp, L56), hlast);
            }
            double rate30, rate60;
            h.TryGetRate(hlast, out rate30);
            h.TryGetRate(hlast, ExpTracker.LongRateWindow, out rate60);
            Check(Math.Abs(rate30 - 720000) < 1 && Math.Abs(rate60 - 540000) < 1, "30-minute vs 1-hour rate", Fmt.Short(rate30) + " vs " + Fmt.Short(rate60));
            Check(Fmt.Clock(h.SessionLength(hlast)) == "1:30:00", "session clock after 90 minutes", Fmt.Clock(h.SessionLength(hlast)));

            // Quest turn-in: a big jump is accepted after being seen twice
            Check(r.Feed(At(640000, L56), last.AddSeconds(1)) == FeedOutcome.Confirming, "big jump waits for confirmation", null);
            Check(r.Feed(At(640000, L56), last.AddSeconds(2)) == FeedOutcome.Jumped && r.SessionGain == 540000, "big jump counted", r.SessionGain.ToString(Inv));

            // Pause / region change: re-sync without counting
            r.BeginResync();
            r.Feed(At(700000, L56), last.AddSeconds(3));
            o = r.Feed(At(700000, L56), last.AddSeconds(4));
            Check(o == FeedOutcome.Resynced && r.SessionGain == 540000 && r.Exp == 700000, "re-sync doesn't count EXP", o.ToString());

            // Damaged OCR text can still start tracking
            var d = new ExpTracker();
            d.Feed(ExpParser.Parse("EXP 613128162.73%]"), T0);
            d.Feed(ExpParser.Parse("EXP 613128162.73%]"), T0.AddSeconds(1));
            o = d.Feed(ExpParser.Parse("EXP 613128162.73%]"), T0.AddSeconds(2));
            Check(o == FeedOutcome.Started && d.Exp == 613128, "start from run-together text", o + " exp " + d.Exp);

            // A game whose numbers don't follow the table still works from the percentage alone.
            // (It waits about 20 readings before giving up on the table, so a misread can't start it.)
            const long Fake = 777777, FakeNext = 850000;
            var n = new ExpTracker { Level = 30 };
            for (int i = 0; i < 19; i++) n.Feed(At(600000, Fake), T0.AddSeconds(i));
            Check(!n.Started, "a reading that fits no level doesn't start tracking right away", null);
            for (int i = 19; i < 22; i++) n.Feed(At(600000, Fake), T0.AddSeconds(i));
            Check(n.Started && !n.UsesTable, "starts once the table is switched off", null);
            for (int i = 1; i <= 25; i++) n.Feed(At(600000 + i * 7000, Fake), T0.AddSeconds(22 + i));
            n.Feed(At(9000, FakeNext), T0.AddSeconds(50));
            o = n.Feed(At(9000, FakeNext), T0.AddSeconds(51));
            Check(!n.UsesTable && o == FeedOutcome.LevelUp && n.Level == 31, "no-table fallback: gains and level up", o + ", table " + n.UsesTable + ", level " + n.Level);
        }

        /// <summary>Misreads that repeat identically while the player is idle must never count as a level up.</summary>
        static void FalseLevelUpTests()
        {
            Console.WriteLine("== False level ups");
            long L41 = ExpTable.Required(41), L42 = ExpTable.Required(42);
            var t = new ExpTracker { Level = 41 };
            t.Feed(At(194111, L41), T0);
            t.Feed(At(194111, L41), T0.AddSeconds(1));

            // Percentage loses a digit: looks like a much bigger level, but EXP didn't drop.
            for (int i = 0; i < 6; i++) t.Feed(ExpParser.Parse("EXP 194,111 [5.87%]"), T0.AddSeconds(2 + i));
            Check(t.Level == 41 && t.SessionGain == 0, "dropped percent digit, repeated", "level " + t.Level + ", gain " + t.SessionGain);

            // Back to the right reading.
            for (int i = 0; i < 6; i++) t.Feed(At(194111, L41), T0.AddSeconds(10 + i));
            Check(t.Level == 41 && t.SessionGain == 0 && t.Exp == 194111, "recovers after the misread", "level " + t.Level + ", exp " + t.Exp);

            // EXP and percentage both lose a digit: a big drop on the same level size isn't a level up.
            for (int i = 0; i < 6; i++) t.Feed(ExpParser.Parse("EXP 19,411 [5.98%]"), T0.AddSeconds(20 + i));
            Check(t.Level == 41 && t.SessionGain == 0, "dropped digits in both numbers, repeated", "level " + t.Level + ", gain " + t.SessionGain);

            // EXP gains an extra digit.
            var u = new ExpTracker { Level = 41 };
            u.Feed(At(194111, L41), T0);
            u.Feed(At(194111, L41), T0.AddSeconds(1));
            for (int i = 0; i < 6; i++) u.Feed(ExpParser.Parse("EXP 1,941,111 [59.87%]"), T0.AddSeconds(2 + i));
            Check(u.Level == 41 && u.SessionGain == 0, "extra EXP digit, repeated", "level " + u.Level + ", gain " + u.SessionGain);

            // A real level up still counts.
            var v = new ExpTracker { Level = 41 };
            v.Feed(At(320000, L41), T0);
            v.Feed(At(320000, L41), T0.AddSeconds(1));
            v.Feed(At(5000, L42), T0.AddSeconds(2));
            FeedOutcome o = v.Feed(At(5000, L42), T0.AddSeconds(3));
            Check(o == FeedOutcome.LevelUp && v.Level == 42 && v.SessionGain == L41 - 320000 + 5000, "real level up 41 -> 42", o + ", level " + v.Level + ", gain " + v.SessionGain);

            // Level set wrong by hand: corrected from the EXP without counting anything.
            var w = new ExpTracker { Level = 41 };
            w.Feed(At(194111, L41), T0);
            w.Feed(At(194111, L41), T0.AddSeconds(1));
            w.Level = 42;
            for (int i = 0; i < 5; i++) w.Feed(At(194111, L41), T0.AddSeconds(2 + i));
            Check(w.Level == 41 && w.SessionGain == 0, "manual level 42 corrected back to 41", "level " + w.Level + ", gain " + w.SessionGain);

            // Unknown level is filled in from the EXP.
            var x = new ExpTracker();
            x.Feed(At(194111, L41), T0);
            x.Feed(At(194111, L41), T0.AddSeconds(1));
            Check(x.Level == 41, "unknown level detected", "level " + x.Level);
        }

        /// <summary>
        /// The game's cursor and popups can cover part of the EXP text, leaving a partial number that
        /// repeats while the mouse rests there. Sweep occluders over a real capture: nothing may change.
        /// </summary>
        static void OcclusionTests()
        {
            Console.WriteLine("== Cursor / popup over the EXP text");
            string error;
            if (!OcrService.TryInit(out error)) return;
            using (var clean = new Bitmap(Fixture("exp_194111_59.87.png")))
            {
                var t = new ExpTracker { Level = 41 };
                int preferred = 0;
                DateTime now = T0;
                for (int i = 0; i < 2; i++)
                {
                    now = now.AddSeconds(1);
                    t.Feed(OcrService.Read(clean, ref preferred, false).Candidates, now);
                }
                int frames = 0;
                var seen = new List<string>();
                foreach (bool dark in new[] { false, true })
                    for (int y = 24; y <= 44; y += 10)
                        for (int x = 60; x <= 250; x += 12)
                        {
                            using (var img = new Bitmap(clean))
                            {
                                using (Graphics g = Graphics.FromImage(img))
                                {
                                    // A glove-like cursor (white, black outline) or a dark popup corner.
                                    var box = new Rectangle(x, y, dark ? 40 : 18, dark ? 30 : 24);
                                    g.FillRectangle(dark ? Brushes.Black : Brushes.White, box);
                                    if (!dark) g.DrawRectangle(new Pen(Color.Black, 2), box);
                                }
                                for (int k = 0; k < 2; k++)
                                {
                                    now = now.AddSeconds(1);
                                    OcrRead read = OcrService.Read(img, ref preferred, false);
                                    t.Feed(read.Candidates, now);
                                    frames++;
                                    if (read.Candidates.Count > 0 && !seen.Contains(read.Candidates[0].ToString())) seen.Add(read.Candidates[0].ToString());
                                }
                            }
                        }
                Check(t.Level == 41 && t.SessionGain == 0 && t.Exp == 194111, "level and EXP unchanged over " + frames + " occluded frames",
                    "level " + t.Level + ", gain " + t.SessionGain + ", exp " + t.Exp + "; partial readings seen: " + string.Join(" | ", seen.ToArray()));
            }
        }

        // ---------------------------------------------------------------- MapleStory font

        static string Fixture(string name)
        {
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(TestProgram).Assembly.Location), "fixtures", name);
        }

        static void PixelFontTests()
        {
            Console.WriteLine("== MapleStory font (real captures)");
            string error;
            OcrService.TryInit(out error);
            using (var exp = new Bitmap(Fixture("exp_194111_59.87.png")))
            {
                foreach (double scale in new[] { 1.0, 0.5, 1.5, 2.0 })
                {
                    using (Bitmap img = Rescale(exp, scale))
                    {
                        // "." and "," look alike at small sizes; the parser accepts either.
                        string text = PixelFontReader.Read(img);
                        List<ExpReading> r = text == null ? new List<ExpReading>() : ExpParser.Parse(text);
                        bool ok = r.Count > 0 && r[0].Exp == 194111 && Math.Abs(r[0].PercentValue - 59.87) < 0.01;
                        Check(ok, "font reader at game scale x" + (2 * scale), text ?? "null");
                        if (scale == 1.0) CheckRead("EXP capture through Read()", img, 194111, 59.87);
                    }
                }
            }
            // Other players' game scales: crisp (pixel-perfect) and smoothed stretching. Each must read
            // correctly where listed, and at no scale may the tracker end up with wrong numbers: neither a
            // fresh one starting up, nor one already tracking.
            Console.WriteLine("== Game scales (real capture resized)");
            long l41 = ExpTable.Required(41);
            using (var src = new Bitmap(Fixture("exp_194111_59.87.png")))
            {
                double[] crispOk = { 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4 };
                double[] smoothOk = { 1.25, 1.75, 2, 2.5, 3, 4 };
                double[] all = { 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4 };
                foreach (bool smooth in new[] { false, true })
                {
                    var failed = new List<string>();
                    var wrong = new List<string>();
                    foreach (double gs in all)
                    {
                        using (Bitmap img = Resize(src, gs / 2, smooth))
                        {
                            int preferred = 0;
                            OcrRead read = OcrService.Read(img, ref preferred, false);
                            bool right = read.Candidates.Count > 0 && read.Candidates[0].Exp == 194111 && Math.Abs(read.Candidates[0].PercentValue - 59.87) < 0.01;
                            if (!right && Array.IndexOf(smooth ? smoothOk : crispOk, gs) >= 0) failed.Add(gs + "x");

                            var fresh = new ExpTracker();
                            var tracking = new ExpTracker();
                            tracking.Feed(At(194111, l41), T0);
                            tracking.Feed(At(194111, l41), T0.AddSeconds(1));
                            for (int k = 0; k < 3; k++)
                            {
                                fresh.Feed(read.Candidates, T0.AddSeconds(2 + k));
                                tracking.Feed(read.Candidates, T0.AddSeconds(2 + k));
                            }
                            if (fresh.Started && (fresh.Exp != 194111 || fresh.Level != 41)) wrong.Add(gs + "x fresh start: " + fresh.Exp + " lv " + fresh.Level);
                            if (tracking.Exp != 194111 || tracking.Level != 41 || tracking.SessionGain != 0) wrong.Add(gs + "x tracking: " + tracking.Exp + " lv " + tracking.Level);
                        }
                    }
                    string kind = smooth ? "smoothed" : "crisp";
                    Check(failed.Count == 0, kind + " scales " + string.Join(", ", Array.ConvertAll(smooth ? smoothOk : crispOk, d => d + "x")) + " read correctly",
                        failed.Count == 0 ? null : "failed at " + string.Join(", ", failed.ToArray()));
                    Check(wrong.Count == 0, kind + ": tracker never takes wrong numbers at any scale", wrong.Count == 0 ? null : string.Join("; ", wrong.ToArray()));
                }
            }

            using (var hpmp = new Bitmap(Fixture("hp_566_1381_mp_472_1140.png")))
            {
                string text = PixelFontReader.Read(hpmp);
                Check(text != null && text.Contains("[566/1381]") && text.Contains("[472/1140]"), "font reader on HP/MP (all digits)", text ?? "null");
            }
        }

        static Bitmap Resize(Bitmap src, double factor, bool smooth)
        {
            int w = (int)Math.Round(src.Width * factor), h = (int)Math.Round(src.Height * factor);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = smooth ? InterpolationMode.HighQualityBilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        static Bitmap Rescale(Bitmap src, double scale)
        {
            int w = (int)Math.Round(src.Width * scale), h = (int)Math.Round(src.Height * scale);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        // ---------------------------------------------------------------- formatting

        static void FormatTests()
        {
            Console.WriteLine("== Format");
            Eq(Fmt.Signed(13500), "+13.5K");
            Eq(Fmt.Short(151800), "151.8K");
            Eq(Fmt.Signed(316300), "+316.3K");
            Eq(Fmt.Short(999), "999");
            Eq(Fmt.Short(999990), "1.00M");
            Eq(Fmt.Short(1250000), "1.25M");
            Eq(Fmt.Short(25300000), "25.3M");
            Eq(Fmt.Signed(-1200), "-1.2K");
            Eq(Fmt.Signed(0), "+0");
            Eq(Fmt.Duration(new TimeSpan(2, 24, 10)), "2h 24m");
            Eq(Fmt.Duration(new TimeSpan(0, 7, 5)), "7m 5s");
            Eq(Fmt.Duration(TimeSpan.FromHours(50)), "2d 2h");
            Eq(Fmt.Clock(new TimeSpan(0, 4, 9)), "0:04:09");
            Eq(Fmt.Clock(new TimeSpan(1, 23, 45)), "1:23:45");
            Eq(Fmt.Clock(TimeSpan.FromHours(26)), "26:00:00");
        }

        static void Eq(string actual, string expected)
        {
            Check(actual == expected, "format " + expected, actual);
        }

        // ---------------------------------------------------------------- OCR

        static void OcrTests()
        {
            Console.WriteLine("== Windows OCR on synthetic EXP bars");
            string error;
            if (!OcrService.TryInit(out error))
            {
                Check(false, "Windows OCR available", error);
                return;
            }
            Color white = Color.White, black = Color.Black;
            Color yellowTop = Color.FromArgb(255, 222, 90), yellowBottom = Color.FromArgb(214, 140, 20);
            Color greenTop = Color.FromArgb(190, 245, 110), greenBottom = Color.FromArgb(80, 160, 30);
            Color darkTop = Color.FromArgb(40, 34, 28), darkBottom = Color.FromArgb(18, 15, 12);

            OcrCase("pixel font 9px, outlined, yellow bar", "EXP 613128 [62.73%]", "Tahoma", 9, FontStyle.Regular, true, white, black, yellowTop, yellowBottom, 613128, 62.73);
            OcrCase("pixel font 11px, outlined, green bar", "EXP 613128 [62.73%]", "Tahoma", 11, FontStyle.Bold, true, white, black, greenTop, greenBottom, 613128, 62.73);
            OcrCase("smooth 12px, dark text, yellow bar", "EXP 1234567 [8.05%]", "Arial", 12, FontStyle.Bold, false, black, Color.Empty, yellowTop, yellowBottom, 1234567, 8.05);
            OcrCase("smooth 13px, white on dark", "613,128 / 977,408", "Segoe UI", 13, FontStyle.Bold, false, white, Color.Empty, darkTop, darkBottom, 613128, 62.73);
            OcrCase("pixel font 10px, outlined, half-filled bar", "EXP 52 [0.33%]", "Tahoma", 10, FontStyle.Bold, true, white, black, greenTop, darkBottom, 52, 0.33);
        }

        static void OcrCase(string name, string text, string font, float px, FontStyle style, bool pixelFont,
            Color textColor, Color outline, Color top, Color bottom, long exp, double pct)
        {
            using (Bitmap bar = MakeBar(text, font, px, style, pixelFont, textColor, outline, top, bottom))
                CheckRead(name, bar, exp, pct);
        }

        static void CheckRead(string name, Bitmap bmp, long exp, double pct)
        {
            int filter = 0;
            OcrRead read = OcrService.Read(bmp, ref filter, false);
            bool ok = read.Candidates.Count > 0 && read.Candidates[0].Exp == exp && Math.Abs(read.Candidates[0].PercentValue - pct) < 0.01;
            Check(ok, name, "\"" + read.Text + "\" via " + read.Filter + ", " + read.Milliseconds + " ms");
        }

        static Bitmap MakeBar(string text, string font, float px, FontStyle style, bool pixelFont,
            Color textColor, Color outline, Color top, Color bottom)
        {
            using (var f = new Font(font, px, style, GraphicsUnit.Pixel))
            {
                Size size;
                using (var probe = new Bitmap(1, 1))
                using (Graphics pg = Graphics.FromImage(probe))
                {
                    SizeF s = pg.MeasureString(text, f);
                    size = new Size((int)s.Width + 16, (int)s.Height + 6);
                }
                var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    using (var b = new LinearGradientBrush(new Rectangle(0, -1, size.Width, size.Height + 2), top, bottom, 90f))
                        g.FillRectangle(b, 0, 0, size.Width, size.Height);
                    g.TextRenderingHint = pixelFont ? TextRenderingHint.SingleBitPerPixelGridFit : TextRenderingHint.AntiAliasGridFit;
                    float x = 8, y = 3;
                    if (outline != Color.Empty)
                        using (var ob = new SolidBrush(outline))
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dy = -1; dy <= 1; dy++)
                                    g.DrawString(text, f, ob, x + dx, y + dy);
                    using (var tb = new SolidBrush(textColor)) g.DrawString(text, f, tb, x, y);
                }
                return bmp;
            }
        }

        /// <summary>Shows a bar on screen and reads it back through the real capture path.</summary>
        static void ScreenCaptureTest()
        {
            Console.WriteLine("== Screen capture");
            string error;
            if (!OcrService.TryInit(out error)) return;
            using (Bitmap bar = MakeBar("EXP 613128 [62.73%]", "Tahoma", 11, FontStyle.Bold, true, Color.White, Color.Black,
                Color.FromArgb(255, 222, 90), Color.FromArgb(214, 140, 20)))
            using (var form = new Form())
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                form.FormBorderStyle = FormBorderStyle.None;
                form.StartPosition = FormStartPosition.Manual;
                form.ShowInTaskbar = false;
                form.TopMost = true;
                form.Bounds = new Rectangle(wa.Left + 120, wa.Top + 120, bar.Width, bar.Height);
                form.BackgroundImage = bar;
                form.Show();
                for (int i = 0; i < 10; i++)
                {
                    Application.DoEvents();
                    Thread.Sleep(50);
                }
                Rectangle region = form.Bounds;
                using (Bitmap shot = OcrService.Capture(region))
                    CheckRead("read EXP bar from the screen at " + region, shot, 613128, 62.73);
                form.Close();
            }
        }

        static void ImageFileTest(string path, string crop, long exp, double pct)
        {
            Console.WriteLine("== Image file");
            string[] p = crop.Split(',');
            var rect = new Rectangle(int.Parse(p[0], Inv), int.Parse(p[1], Inv), int.Parse(p[2], Inv), int.Parse(p[3], Inv));
            using (var src = new Bitmap(path))
            using (Bitmap part = src.Clone(rect, PixelFormat.Format32bppArgb))
                CheckRead("read " + System.IO.Path.GetFileName(path) + " " + crop, part, exp, pct);
        }
    }
}
