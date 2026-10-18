using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MapleExpTracker
{
    /// <summary>One possible interpretation of the EXP text read from the screen.</summary>
    public sealed class ExpReading
    {
        public const int Low = 1;   // a guess made from run-together or damaged text
        public const int High = 2;  // cleanly separated numbers

        public long Exp;
        public double Percent = double.NaN;  // percentage as displayed, e.g. 62.73
        public double PercentStep = 0.01;    // display resolution of Percent
        public long Total;                    // EXP needed for the level, when the game shows "exp / total"
        public int Quality = High;

        // Bounds on the EXP needed to finish the level, implied by this reading.
        public double Lo;
        public double Hi = double.PositiveInfinity;

        public bool HasPercent
        {
            get { return !double.IsNaN(Percent); }
        }

        /// <summary>Progress through the level in percent, or NaN when unknown.</summary>
        public double PercentValue
        {
            get { return Total > 0 ? Exp * 100.0 / Total : Percent; }
        }

        /// <summary>True when the reading says something about how much EXP the level needs.</summary>
        public bool KnowsLevelSize
        {
            get { return Total > 0 || (HasPercent && Exp > 0); }
        }

        public void ComputeBounds()
        {
            if (Total > 0)
            {
                Lo = Hi = Total;
                return;
            }
            Lo = Exp;
            Hi = double.PositiveInfinity;
            if (!HasPercent || Exp <= 0) return;
            // The game rounds or truncates the percentage, so the true value is within one step of it.
            Lo = Math.Max(Lo, Exp * 100.0 / (Percent + PercentStep));
            if (Percent - PercentStep > 0) Hi = Exp * 100.0 / (Percent - PercentStep);
        }

        public override string ToString()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string s = Exp.ToString("N0", inv);
            if (Total > 0) s += " / " + Total.ToString("N0", inv);
            if (HasPercent)
            {
                string format = PercentStep < 0.1 ? "0.00" : PercentStep < 1 ? "0.0" : "0";
                s += " [" + Percent.ToString(format, inv) + "%]";
            }
            return Quality >= High ? s : s + "  (guess)";
        }
    }

    /// <summary>Turns OCR text such as "EXP 613128 [62.73%]" into EXP readings.</summary>
    public static class ExpParser
    {
        const string Number = @"\d{1,3}(?:[,.']\d{3})+|\d+";

        // A bracket that OCR misread as l, I, | or ! between the EXP number and the percentage.
        static readonly Regex BracketLike = new Regex(@"(?<=\d)\s*[\[\({<|lIi!cC]\s*(?=\d{1,3}\s*[.,]\s*\d{1,2}\s*%)");
        // "%" misread as "0/0", "o/o" or "°/o" after a decimal percentage.
        static readonly Regex PercentLike = new Regex(@"(?<=[.,]\s*\d{1,2})\s*[0oO°º]\s*/\s*[0oO°º]");
        // Letters OCR confuses with digits, when they sit inside or right next to a number.
        static readonly Regex ZeroLike = new Regex(@"(?<=\d[.,]?)[oOQD](?![A-Za-z])|(?<![A-Za-z])[oOQD](?=[.,]?\d)");
        static readonly Regex OneLike = new Regex(@"(?<=\d)[lI|!](?=\d)");
        static readonly Regex FiveLike = new Regex(@"(?<=\d[.,]?)[sS](?![A-Za-z])|(?<![A-Za-z])[sS](?=[.,]?\d)");
        static readonly Regex EightLike = new Regex(@"(?<=\d[.,]?)B(?![A-Za-z])|(?<![A-Za-z])B(?=[.,]?\d)");
        // A lone look-alike right before the percentage bracket: "EXP S [0.33%]".
        static readonly Regex LoneDigitLike = new Regex(@"(?<![A-Za-z0-9])[sSoOB](?=\s*[\[\({]\s*\d)");

        // Everything from the first digit up to a "%" (or a closing bracket right after "d.dd").
        static readonly Regex PercentRx = new Regex(@"(?<body>\d[\d\s,.'\[\({<:]*?)(?:\s*%(?<close>\s*[\]\)}>|])?|(?<=\d[.,]\d{2})\s*(?<close>[\]\)}>]))");
        static readonly Regex SplitRx = new Regex(@"^(?<pre>.*?)(?<int>\d+)(?:\s*[.,]\s*(?<dec>\d{1,2}))?$", RegexOptions.Singleline);
        static readonly Regex ExpTailRx = new Regex(@"(?<exp>" + Number + @")(?<gap>\D*)$");
        static readonly Regex FractionRx = new Regex(@"(?<a>" + Number + @")\s*/\s*(?<b>" + Number + @")");
        static readonly Regex NumberRx = new Regex(Number);

        public static List<ExpReading> Parse(string text)
        {
            var found = new List<ExpReading>();
            if (string.IsNullOrEmpty(text)) return found;
            string s = Normalize(text);

            foreach (Match m in PercentRx.Matches(s))
                AddPercentReadings(m.Groups["body"].Value, m.Groups["close"].Success, found);

            foreach (Match m in FractionRx.Matches(s))
            {
                long a, b;
                if (!TryNumber(m.Groups["a"].Value, out a) || !TryNumber(m.Groups["b"].Value, out b) || a >= b) continue;
                // "613128 / 977408 [62.73%]": the percent pattern grabbed the total as the EXP number.
                ExpReading match = found.Find(r => (r.Exp == a || r.Exp == b) && r.Total == 0);
                if (match != null)
                {
                    match.Exp = a;
                    match.Total = b;
                    match.Quality = ExpReading.High;
                }
                else
                {
                    found.Add(new ExpReading { Exp = a, Total = b, Quality = found.Count == 0 ? ExpReading.High : ExpReading.Low });
                }
            }

            if (found.Count == 0)
            {
                // Games that show only the EXP number: accept a lone number as a weak reading.
                MatchCollection numbers = NumberRx.Matches(s);
                long only;
                if (numbers.Count == 1 && TryNumber(numbers[0].Value, out only))
                    found.Add(new ExpReading { Exp = only, Quality = ExpReading.Low });
            }

            var unique = new List<ExpReading>();
            foreach (ExpReading r in found)
            {
                if (r.Exp < 0) continue;
                if (r.HasPercent && (r.Percent < 0 || r.Percent > 100.0001)) continue;
                if (unique.Exists(x => SameReading(x, r))) continue;
                r.ComputeBounds();
                unique.Add(r);
            }
            var ordered = unique.FindAll(r => r.Quality >= ExpReading.High);
            ordered.AddRange(unique.FindAll(r => r.Quality < ExpReading.High));
            return ordered;
        }

        public static bool SameReading(ExpReading x, ExpReading y)
        {
            if (x.Exp != y.Exp || x.Total != y.Total) return false;
            if (x.HasPercent != y.HasPercent) return false;
            return !x.HasPercent || Math.Abs(x.Percent - y.Percent) < 1e-9;
        }

        static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '％': sb.Append('%'); break;
                    case '［': sb.Append('['); break;
                    case '］': sb.Append(']'); break;
                    case '（': sb.Append('('); break;
                    case '）': sb.Append(')'); break;
                    case '，': sb.Append(','); break;
                    case '．': sb.Append('.'); break;
                    case '\r':
                    case '\n':
                    case '\t': sb.Append(' '); break;
                    default: sb.Append(ch); break;
                }
            }
            string s = PercentLike.Replace(sb.ToString(), "%");
            s = BracketLike.Replace(s, " [");
            s = LoneDigitLike.Replace(s, m => m.Value == "B" ? "8" : m.Value == "s" || m.Value == "S" ? "5" : "0");
            s = ZeroLike.Replace(s, "0");
            s = OneLike.Replace(s, "1");
            s = FiveLike.Replace(s, "5");
            return EightLike.Replace(s, "8");
        }

        static void AddPercentReadings(string body, bool closed, List<ExpReading> found)
        {
            Match m = SplitRx.Match(body);
            if (!m.Success) return;
            string ip = m.Groups["int"].Value;
            string dec = m.Groups["dec"].Success ? m.Groups["dec"].Value : null;
            Match em = ExpTailRx.Match(m.Groups["pre"].Value);
            string exp = em.Success ? Digits(em.Groups["exp"].Value) : null;

            // "EXP 5 10.33%)": a closing bracket but no opening one, so the "1" in front of the
            // percentage is probably the opening bracket. Prefer that reading, keep the other as a guess.
            if (closed && exp != null && dec != null && ip.Length >= 2 && ip[0] == '1' && em.Groups["gap"].Value.Trim().Length == 0
                && ValidPercent(ip.Substring(1), dec))
            {
                Add(found, exp, ip.Substring(1), dec, ExpReading.High);
                if (ValidPercent(ip, dec)) Add(found, exp, ip, dec, ExpReading.Low);
                return;
            }

            if (dec == null)
            {
                // No decimal point: a whole-number percentage, or OCR dropped the point.
                if (exp != null && ip.Length <= 2) Add(found, exp, ip, null, ExpReading.High);
                if (ip.Length >= 3) AddSplits(found, exp, ip.Substring(0, ip.Length - 2), ip.Substring(ip.Length - 2), ExpReading.Low);
                return;
            }
            AddSplits(found, exp, ip, dec, ExpReading.High);
        }

        /// <summary>
        /// Adds readings for a percentage whose integer part is <paramref name="ip"/>. When OCR runs
        /// the numbers together, ip also holds the EXP number and/or a bracket misread as "1".
        /// </summary>
        static void AddSplits(List<ExpReading> found, string exp, string ip, string dec, int quality)
        {
            if (exp != null)
            {
                if (ip.Length <= 3 && ValidPercent(ip, dec))
                {
                    Add(found, exp, ip, dec, quality);
                    return;
                }
                // "613128 162.73%": a bracket read as a digit in front of the percentage.
                for (int k = 2; k >= 1; k--)
                    if (ip.Length > k && ValidPercent(ip.Substring(ip.Length - k), dec))
                        Add(found, exp, ip.Substring(ip.Length - k), dec, ExpReading.Low);
                return;
            }
            if (ip.Length <= 3 && ValidPercent(ip, dec)) return;  // a percentage with no EXP number

            // "613128162.73%": EXP, maybe a bracket read as "1", and the percentage run together.
            for (int k = 2; k >= 1; k--)
            {
                if (ip.Length <= k + 1) continue;
                string pct = ip.Substring(ip.Length - k), rest = ip.Substring(0, ip.Length - k);
                if (ValidPercent(pct, dec) && rest[rest.Length - 1] == '1')
                    Add(found, rest.Substring(0, rest.Length - 1), pct, dec, ExpReading.Low);
            }
            for (int k = 2; k >= 1; k--)
            {
                if (ip.Length <= k) continue;
                string pct = ip.Substring(ip.Length - k);
                if (ValidPercent(pct, dec)) Add(found, ip.Substring(0, ip.Length - k), pct, dec, ExpReading.Low);
            }
        }

        static bool ValidPercent(string ip, string dec)
        {
            if (ip.Length == 0 || (ip.Length > 1 && ip[0] == '0')) return false;
            return ParsePercent(ip, dec) <= 100.0;
        }

        static double ParsePercent(string ip, string dec)
        {
            return double.Parse(dec == null ? ip : ip + "." + dec, CultureInfo.InvariantCulture);
        }

        static void Add(List<ExpReading> found, string expDigits, string ip, string dec, int quality)
        {
            long exp;
            if (!TryNumber(expDigits, out exp)) return;
            found.Add(new ExpReading
            {
                Exp = exp,
                Percent = ParsePercent(ip, dec),
                PercentStep = dec == null ? 1 : dec.Length == 1 ? 0.1 : 0.01,
                Quality = quality
            });
        }

        static bool TryNumber(string text, out long value)
        {
            string digits = Digits(text);
            value = 0;
            return digits.Length > 0 && digits.Length <= 15 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        static string Digits(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
                if (ch >= '0' && ch <= '9') sb.Append(ch);
            return sb.ToString();
        }
    }
}
