using System;
using System.Collections.Generic;
using HunterWidow.Domain.Inventory;

namespace HunterWidow.Domain.Progression
{
    public sealed class ProgressionState
    {
        private readonly HashSet<string> unlockedRecipes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockedCgs = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockedFloors = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> craftedItems = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> upgradeLevels = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Gold { get; private set; }

        public int Affinity { get; private set; }

        public int CycleCount { get; private set; }

        public string TriggeredEndingId { get; private set; }

        public IReadOnlyCollection<string> UnlockedRecipes => unlockedRecipes;

        public IReadOnlyCollection<string> UnlockedCgs => unlockedCgs;

        public IReadOnlyCollection<string> UnlockedFloors => unlockedFloors;

        public IReadOnlyCollection<string> CraftedItems => craftedItems;

        public IReadOnlyCollection<string> Flags => flags;

        public void AddGold(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Gold += amount;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        public void AddAffinity(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Affinity += amount;
        }

        public void SetCycleCount(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            CycleCount = count;
        }

        public void AdvanceCycle()
        {
            CycleCount++;
        }

        public void UnlockRecipe(string id)
        {
            AddToSet(unlockedRecipes, id);
        }

        public void UnlockCg(string id)
        {
            AddToSet(unlockedCgs, id);
        }

        public void UnlockFloor(string id)
        {
            AddToSet(unlockedFloors, id);
        }

        public void MarkCrafted(string id)
        {
            AddToSet(craftedItems, id);
        }

        public void SetFlag(string id)
        {
            AddToSet(flags, id);
        }

        public void ClearFlag(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                flags.Remove(id);
            }
        }

        public bool HasRecipe(string id) => unlockedRecipes.Contains(id);

        public bool HasCg(string id) => unlockedCgs.Contains(id);

        public bool HasFloor(string id) => unlockedFloors.Contains(id);

        public bool HasCrafted(string id) => craftedItems.Contains(id);

        public bool HasFlag(string id) => flags.Contains(id);

        public int GetUpgradeLevel(string axisId)
        {
            int level;
            return upgradeLevels.TryGetValue(axisId, out level) ? level : 0;
        }

        public void SetUpgradeLevel(string axisId, int level)
        {
            if (string.IsNullOrEmpty(axisId) || level < 0)
            {
                throw new ArgumentException("Upgrade axis and level are invalid.");
            }

            upgradeLevels[axisId] = level;
        }

        public IReadOnlyList<IdCount> GetUpgradeLevels()
        {
            var values = new List<IdCount>();
            foreach (var pair in upgradeLevels)
            {
                values.Add(new IdCount(pair.Key, pair.Value));
            }

            values.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return values;
        }

        public void TriggerEnding(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Ending ID is required.", nameof(id));
            }

            TriggeredEndingId = id;
        }

        public void Restore(
            int gold,
            int affinity,
            int cycleCount,
            string triggeredEndingId,
            IReadOnlyList<IdCount> restoredUpgradeLevels,
            IReadOnlyList<string> restoredRecipes,
            IReadOnlyList<string> restoredCgs,
            IReadOnlyList<string> restoredFloors,
            IReadOnlyList<string> restoredCraftedItems,
            IReadOnlyList<string> restoredFlags)
        {
            if (gold < 0 || affinity < 0 || cycleCount < 0)
            {
                throw new ArgumentOutOfRangeException("Restored progression values cannot be negative.");
            }

            Gold = gold;
            Affinity = affinity;
            CycleCount = cycleCount;
            TriggeredEndingId = triggeredEndingId ?? string.Empty;
            upgradeLevels.Clear();
            unlockedRecipes.Clear();
            unlockedCgs.Clear();
            unlockedFloors.Clear();
            craftedItems.Clear();
            flags.Clear();
            RestoreCounts(restoredUpgradeLevels, upgradeLevels);
            RestoreIds(restoredRecipes, unlockedRecipes);
            RestoreIds(restoredCgs, unlockedCgs);
            RestoreIds(restoredFloors, unlockedFloors);
            RestoreIds(restoredCraftedItems, craftedItems);
            RestoreIds(restoredFlags, flags);
        }

        private static void AddToSet(HashSet<string> set, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Content ID is required.", nameof(id));
            }

            set.Add(id);
        }

        private static void RestoreCounts(IReadOnlyList<IdCount> values, Dictionary<string, int> destination)
        {
            if (values == null)
            {
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];
                if (value != null && !string.IsNullOrEmpty(value.Id) && value.Count > 0)
                {
                    destination[value.Id] = value.Count;
                }
            }
        }

        private static void RestoreIds(IReadOnlyList<string> values, HashSet<string> destination)
        {
            if (values == null)
            {
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (!string.IsNullOrEmpty(values[valueIndex]))
                {
                    destination.Add(values[valueIndex]);
                }
            }
        }
    }
}
