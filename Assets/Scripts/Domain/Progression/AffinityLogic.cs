using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Progression
{
    public sealed class CgThreshold
    {
        public CgThreshold(string cgId, int requiredAffinity)
        {
            CgId = cgId;
            RequiredAffinity = requiredAffinity;
        }

        public string CgId { get; }

        public int RequiredAffinity { get; }
    }

    public sealed class AffinityLogic
    {
        private readonly List<CgThreshold> thresholds;
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private int value;

        public AffinityLogic(IReadOnlyList<CgThreshold> definitions, int startingValue = 0)
        {
            thresholds = new List<CgThreshold>(definitions ?? throw new ArgumentNullException(nameof(definitions)));
            thresholds.Sort((left, right) =>
            {
                var thresholdComparison = left.RequiredAffinity.CompareTo(right.RequiredAffinity);
                return thresholdComparison != 0 ? thresholdComparison : string.CompareOrdinal(left.CgId, right.CgId);
            });
            value = startingValue;
        }

        public int Value => value;

        public IReadOnlyList<string> Add(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            value += amount;
            var newlyUnlocked = new List<string>();
            for (var thresholdIndex = 0; thresholdIndex < thresholds.Count; thresholdIndex++)
            {
                var threshold = thresholds[thresholdIndex];
                if (value >= threshold.RequiredAffinity && unlocked.Add(threshold.CgId))
                {
                    newlyUnlocked.Add(threshold.CgId);
                }
            }

            return newlyUnlocked;
        }

        public bool IsUnlocked(string cgId)
        {
            return unlocked.Contains(cgId);
        }
    }
}
