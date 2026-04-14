using System;

namespace Multi_bloob_adventure_idle
{
    public static class ClanProgressionFormula
    {
        private const double Post100A = 0.000358709929;
        private const double Post100B = 0.511661009;
        private const double Post100C = 1264.06507;
        private const double Post100D = 1223650.89;

        public static int GetPrestigeForLevel(int level)
        {
            if (level <= 0)
                return 0;

            return level / 100;
        }

        public static int GetNextPrestigeLevel(int level)
        {
            return (GetPrestigeForLevel(level) + 1) * 100;
        }

        public static double GetXpForNextLevel(int currentLevel)
        {
            int targetLevel = Math.Max(1, currentLevel + 1);
            return GetLevelRequirement(targetLevel);
        }

        public static double GetTotalXpForLevel(int level)
        {
            if (level <= 1)
                return 0d;

            if (level <= 100)
                return GetRuneScapeTotalXp(level);

            double total = GetRuneScapeTotalXp(100);
            for (int targetLevel = 101; targetLevel <= level; targetLevel++)
                total += GetPost100Requirement(targetLevel);

            return total;
        }

        public static double GetLevelRequirement(int targetLevel)
        {
            if (targetLevel <= 1)
                return 0d;

            if (targetLevel <= 100)
                return GetRuneScapeTotalXp(targetLevel) - GetRuneScapeTotalXp(targetLevel - 1);

            return GetPost100Requirement(targetLevel);
        }

        private static double GetRuneScapeTotalXp(int level)
        {
            if (level <= 1)
                return 0d;

            double points = 0d;
            for (int lvl = 1; lvl < level; lvl++)
                points += Math.Floor(lvl + 300d * Math.Pow(2d, lvl / 7d));

            return Math.Floor(points / 4d);
        }

        private static double GetPost100Requirement(int targetLevel)
        {
            double level = Math.Max(101, targetLevel);
            double raw = (Post100A * Math.Pow(level, 3d)) + (Post100B * Math.Pow(level, 2d)) + (Post100C * level) + Post100D;
            return RoundToNearestHundred(raw);
        }

        private static double RoundToNearestHundred(double value)
        {
            return Math.Round(value / 100d, MidpointRounding.AwayFromZero) * 100d;
        }
    }
}
