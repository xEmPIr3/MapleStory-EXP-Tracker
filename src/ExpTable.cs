// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;

namespace MapleExpTracker
{
    /// <summary>
    /// EXP needed to go from each level to the next in MapleStory Classic, levels 1 to 99.
    /// Copied from https://meowdb.com/msclassic/guides/exp-table-level-1-to-100 and checked against
    /// the formulas on that page and the game itself (level 41: 194,111 EXP shows as 59.87% of 324,240).
    /// </summary>
    public static class ExpTable
    {
        public const int MaxLevel = 99;   // highest level with a known requirement
        const double Tolerance = 1e-4;

        static readonly long[] ToNext =
        {
            15L, 34L, 57L, 92L, 135L, 372L,
            560L, 840L, 1242L, 1716L, 2360L, 3216L,
            4200L, 5460L, 7050L, 8840L, 11040L, 13716L,
            16680L, 20216L, 24402L, 28980L, 34320L, 40512L,
            47216L, 54900L, 63666L, 73080L, 83720L, 95700L,
            108480L, 122760L, 138666L, 155540L, 174216L, 194832L,
            216600L, 240500L, 266682L, 294216L, 324240L, 356916L,
            391160L, 428280L, 468450L, 510420L, 555680L, 604416L,
            655200L, 709716L, 748608L, 789631L, 832902L, 878545L,
            926689L, 977471L, 1031036L, 1087536L, 1147132L, 1209994L,
            1276301L, 1346242L, 1420016L, 1497832L, 1579913L, 1666492L,
            1757815L, 1854143L, 1955750L, 2062925L, 2175973L, 2295216L,
            2420993L, 2553663L, 2693603L, 2841212L, 2996910L, 3161140L,
            3334370L, 3517093L, 3709829L, 3913127L, 4127566L, 4353756L,
            4592341L, 4844001L, 5109452L, 5389449L, 5684790L, 11938059L,
            25069923L, 52646838L, 110558359L, 232172553L, 487562361L, 1023880958L,
            2150150011L, 4515315023L, 9482161548L
        };

        /// <summary>EXP needed to finish <paramref name="level"/>, or 0 when unknown.</summary>
        public static long Required(int level)
        {
            return level >= 1 && level <= MaxLevel ? ToNext[level - 1] : 0;
        }

        /// <summary>True when the level's requirement lies within the given bounds.</summary>
        public static bool Fits(int level, double lo, double hi)
        {
            long r = Required(level);
            return r > 0 && r >= lo * (1 - Tolerance) && r <= hi * (1 + Tolerance);
        }

        /// <summary>
        /// The level whose requirement lies within the bounds: the level if exactly one fits,
        /// 0 if none does, -1 if several do (the bounds are too loose to tell).
        /// </summary>
        public static int Match(double lo, double hi)
        {
            int found = 0;
            for (int level = 1; level <= MaxLevel; level++)
            {
                if (!Fits(level, lo, hi)) continue;
                if (found != 0) return -1;
                found = level;
            }
            return found;
        }
    }
}
