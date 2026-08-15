using System;
using System.Collections.Generic;
using HunterWidow.Domain.Rng;

namespace HunterWidow.Domain.Inventory
{
    public sealed class BackpackSlot
    {
        public BackpackSlot(string itemId, int count, int maximumStack)
        {
            ItemId = itemId;
            Count = count;
            MaximumStack = maximumStack;
        }

        public string ItemId { get; }

        public int Count { get; internal set; }

        public int MaximumStack { get; }
    }

    public sealed class BackpackAddResult
    {
        public BackpackAddResult(int addedCount, int rejectedCount)
        {
            AddedCount = addedCount;
            RejectedCount = rejectedCount;
        }

        public int AddedCount { get; }

        public int RejectedCount { get; }

        public bool IsFull => RejectedCount > 0;
    }

    public sealed class BackpackReplacementResult
    {
        public BackpackReplacementResult(bool replaced, BackpackSlot discarded, BackpackAddResult added)
        {
            Replaced = replaced;
            Discarded = discarded;
            Added = added;
        }

        public bool Replaced { get; }

        public BackpackSlot Discarded { get; }

        public BackpackAddResult Added { get; }
    }

    public sealed class BackpackLossResult
    {
        public BackpackLossResult(IReadOnlyDictionary<string, int> lostByItem, int totalLost)
        {
            LostByItem = lostByItem;
            TotalLost = totalLost;
        }

        public IReadOnlyDictionary<string, int> LostByItem { get; }

        public int TotalLost { get; }
    }

    public sealed class BackpackSnapshot
    {
        public BackpackSnapshot(int capacity, IReadOnlyList<BackpackSlot> slots)
        {
            Capacity = capacity;
            Slots = slots;
        }

        public int Capacity { get; }

        public IReadOnlyList<BackpackSlot> Slots { get; }

        public int UsedSlots => Slots.Count;
    }

    public sealed class BackpackLogic
    {
        private readonly List<BackpackSlot> slots = new List<BackpackSlot>();
        private int capacity;

        public BackpackLogic(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
        }

        public BackpackSnapshot GetState()
        {
            return new BackpackSnapshot(capacity, CopySlots());
        }

        public void SetCapacity(int newCapacity)
        {
            if (newCapacity < slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newCapacity), "Capacity cannot remove occupied slots.");
            }

            capacity = newCapacity;
        }

        public BackpackAddResult TryAdd(string itemId, int count, int maximumStack)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            if (count <= 0 || maximumStack <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var remaining = count;
            for (var slotIndex = 0; slotIndex < slots.Count && remaining > 0; slotIndex++)
            {
                var slot = slots[slotIndex];
                if (!string.Equals(slot.ItemId, itemId, StringComparison.Ordinal) || slot.Count >= slot.MaximumStack)
                {
                    continue;
                }

                var space = slot.MaximumStack - slot.Count;
                var added = Math.Min(space, remaining);
                slot.Count += added;
                remaining -= added;
            }

            while (remaining > 0 && slots.Count < capacity)
            {
                var added = Math.Min(maximumStack, remaining);
                slots.Add(new BackpackSlot(itemId, added, maximumStack));
                remaining -= added;
            }

            return new BackpackAddResult(count - remaining, remaining);
        }

        public BackpackReplacementResult TryReplaceSlot(int slotIndex, string itemId, int count, int maximumStack)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return new BackpackReplacementResult(false, null, new BackpackAddResult(0, count));
            }

            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            if (count <= 0 || maximumStack <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var backup = CopySlots();
            var discarded = slots[slotIndex];
            var discardedCopy = new BackpackSlot(discarded.ItemId, discarded.Count, discarded.MaximumStack);
            slots.RemoveAt(slotIndex);
            var added = TryAdd(itemId, count, maximumStack);
            if (added.RejectedCount > 0)
            {
                RestoreSlots(backup);
                return new BackpackReplacementResult(false, null, added);
            }

            return new BackpackReplacementResult(true, discardedCopy, added);
        }

        public int GetCount(string itemId)
        {
            var count = 0;
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                if (string.Equals(slots[slotIndex].ItemId, itemId, StringComparison.Ordinal))
                {
                    count += slots[slotIndex].Count;
                }
            }

            return count;
        }

        public bool TryRemove(string itemId, int count)
        {
            if (count <= 0 || GetCount(itemId) < count)
            {
                return false;
            }

            var remaining = count;
            for (var slotIndex = slots.Count - 1; slotIndex >= 0 && remaining > 0; slotIndex--)
            {
                var slot = slots[slotIndex];
                if (!string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var removed = Math.Min(slot.Count, remaining);
                slot.Count -= removed;
                remaining -= removed;
                if (slot.Count == 0)
                {
                    slots.RemoveAt(slotIndex);
                }
            }

            return true;
        }

        public BackpackLossResult LoseFraction(double fraction, SeededRng rng)
        {
            if (fraction < 0d || fraction > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(fraction));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var totalCount = 0;
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                totalCount += slots[slotIndex].Count;
            }

            var targetLoss = (int)Math.Floor(totalCount * fraction);
            var lostByItem = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var lostCount = 0; lostCount < targetLoss; lostCount++)
            {
                var populatedSlots = new List<BackpackSlot>();
                for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    if (slots[slotIndex].Count > 0)
                    {
                        populatedSlots.Add(slots[slotIndex]);
                    }
                }

                if (populatedSlots.Count == 0)
                {
                    break;
                }

                var slot = populatedSlots[rng.NextInt(populatedSlots.Count)];
                slot.Count--;
                int existing;
                lostByItem.TryGetValue(slot.ItemId, out existing);
                lostByItem[slot.ItemId] = existing + 1;
            }

            for (var slotIndex = slots.Count - 1; slotIndex >= 0; slotIndex--)
            {
                if (slots[slotIndex].Count == 0)
                {
                    slots.RemoveAt(slotIndex);
                }
            }

            return new BackpackLossResult(lostByItem, targetLoss);
        }

        private List<BackpackSlot> CopySlots()
        {
            var copies = new List<BackpackSlot>();
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var slot = slots[slotIndex];
                copies.Add(new BackpackSlot(slot.ItemId, slot.Count, slot.MaximumStack));
            }

            return copies;
        }

        private void RestoreSlots(IReadOnlyList<BackpackSlot> source)
        {
            slots.Clear();
            for (var slotIndex = 0; slotIndex < source.Count; slotIndex++)
            {
                var slot = source[slotIndex];
                slots.Add(new BackpackSlot(slot.ItemId, slot.Count, slot.MaximumStack));
            }
        }
    }
}
