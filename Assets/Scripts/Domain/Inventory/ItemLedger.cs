using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Inventory
{
    public sealed class IdCount
    {
        public IdCount(string id, int count)
        {
            Id = id;
            Count = count;
        }

        public string Id { get; }

        public int Count { get; }
    }

    public sealed class ItemLedger
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public int GetCount(string itemId)
        {
            int count;
            return counts.TryGetValue(itemId, out count) ? count : 0;
        }

        public void Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            counts[itemId] = GetCount(itemId) + count;
        }

        public bool TryRemove(string itemId, int count)
        {
            if (count <= 0 || GetCount(itemId) < count)
            {
                return false;
            }

            var remaining = GetCount(itemId) - count;
            if (remaining == 0)
            {
                counts.Remove(itemId);
            }
            else
            {
                counts[itemId] = remaining;
            }

            return true;
        }

        public IReadOnlyList<IdCount> GetState()
        {
            var state = new List<IdCount>();
            foreach (var pair in counts)
            {
                state.Add(new IdCount(pair.Key, pair.Value));
            }

            state.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return state;
        }

        public void ReplaceWith(IReadOnlyList<IdCount> values)
        {
            counts.Clear();
            if (values == null)
            {
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];
                if (value != null && !string.IsNullOrEmpty(value.Id) && value.Count > 0)
                {
                    counts[value.Id] = value.Count;
                }
            }
        }
    }
}
