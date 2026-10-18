using System;
using System.Globalization;

namespace MapleExpTracker
{
    /// <summary>Number and time formatting used by the overlay ("+13.5K", "2h 24m").</summary>
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Exp(long value)
        {
            return value.ToString("N0", Inv);
        }

        public static string Short(double value)
        {
            double a = Math.Abs(value);
            string sign = value < 0 && Math.Round(a) > 0 ? "-" : "";
            if (a < 999.5) return sign + Math.Round(a).ToString("0", Inv);
            if (a < 999950) return sign + (a / 1e3).ToString("0.0", Inv) + "K";
            if (a < 9995000) return sign + (a / 1e6).ToString("0.00", Inv) + "M";
            if (a < 999950000) return sign + (a / 1e6).ToString("0.0", Inv) + "M";
            return sign + (a / 1e9).ToString("0.00", Inv) + "B";
        }

        public static string Signed(double value)
        {
            string s = Short(value);
            return s.StartsWith("-") ? s : "+" + s;
        }

        /// <summary>A running clock: "0:04:09", "1:23:45", "26:00:00".</summary>
        public static string Clock(TimeSpan time)
        {
            long s = Math.Max(0, (long)time.TotalSeconds);
            return (s / 3600).ToString(Inv) + ":" + (s % 3600 / 60).ToString("00", Inv) + ":" + (s % 60).ToString("00", Inv);
        }

        public static string Duration(TimeSpan time)
        {
            long s = (long)Math.Round(time.TotalSeconds);
            if (s >= 86400) return s / 86400 + "d " + s % 86400 / 3600 + "h";
            if (s >= 3600) return s / 3600 + "h " + s % 3600 / 60 + "m";
            if (s >= 60) return s / 60 + "m " + s % 60 + "s";
            return s + "s";
        }
    }
}
