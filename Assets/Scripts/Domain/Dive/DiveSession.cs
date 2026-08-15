using System;
using System.Collections.Generic;
using HunterWidow.Domain.Common;
using HunterWidow.Domain.Erosion;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Rng;

namespace HunterWidow.Domain.Dive
{
    public enum DiveEndReason
    {
        Extracted,
        ForcedReturn
    }

    public sealed class DiveHit
    {
        public DiveHit(string sourceId, double erosionDamage)
        {
            SourceId = sourceId;
            ErosionDamage = erosionDamage;
        }

        public string SourceId { get; }

        public double ErosionDamage { get; }
    }

    public sealed class DiveSnapshot
    {
        public DiveSnapshot(bool isActive, string floorId, Vec2 playerPosition, ErosionSnapshot erosion, BackpackSnapshot backpack)
        {
            IsActive = isActive;
            FloorId = floorId;
            PlayerPosition = playerPosition;
            Erosion = erosion;
            Backpack = backpack;
        }

        public bool IsActive { get; }

        public string FloorId { get; }

        public Vec2 PlayerPosition { get; }

        public ErosionSnapshot Erosion { get; }

        public BackpackSnapshot Backpack { get; }
    }

    public sealed class DiveResult
    {
        public DiveResult(DiveEndReason reason, string floorId, BackpackSnapshot backpack, BackpackLossResult loss)
        {
            Reason = reason;
            FloorId = floorId;
            Backpack = backpack;
            Loss = loss;
        }

        public DiveEndReason Reason { get; }

        public string FloorId { get; }

        public BackpackSnapshot Backpack { get; }

        public BackpackLossResult Loss { get; }
    }

    public sealed class DiveSession
    {
        private readonly ErosionLogic erosion;
        private readonly BackpackLogic backpack;
        private readonly SeededRng rng;
        private readonly double forcedLossFraction;
        private bool isActive;
        private string floorId;
        private Vec2 playerPosition;

        public DiveSession(ErosionLogic erosion, BackpackLogic backpack, SeededRng rng, double forcedLossFraction)
        {
            this.erosion = erosion ?? throw new ArgumentNullException(nameof(erosion));
            this.backpack = backpack ?? throw new ArgumentNullException(nameof(backpack));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (forcedLossFraction < 0d || forcedLossFraction > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(forcedLossFraction));
            }

            this.forcedLossFraction = forcedLossFraction;
            this.erosion.BandChanged += band => BandChanged?.Invoke(band);
        }

        public event Action<string> BandChanged;

        public event Action<DiveResult> Finished;

        public void Start(string newFloorId, Vec2 startPosition)
        {
            if (string.IsNullOrEmpty(newFloorId))
            {
                throw new ArgumentException("Floor ID is required.", nameof(newFloorId));
            }

            floorId = newFloorId;
            playerPosition = startPosition;
            erosion.Reset();
            isActive = true;
        }

        public DiveSnapshot GetState()
        {
            return new DiveSnapshot(isActive, floorId, playerPosition, erosion.GetState(), backpack.GetState());
        }

        public bool ChangeFloor(string newFloorId, ErosionSettings settings)
        {
            if (!isActive || string.IsNullOrEmpty(newFloorId) || settings == null)
            {
                return false;
            }

            floorId = newFloorId;
            erosion.SetSettings(settings, false);
            return true;
        }

        public bool Purify(double amount)
        {
            if (!isActive || amount <= 0d)
            {
                return false;
            }

            erosion.Purify(amount);
            return true;
        }

        public BackpackAddResult TryCollect(string itemId, int count, int maximumStack)
        {
            if (!isActive)
            {
                return new BackpackAddResult(0, count);
            }

            return backpack.TryAdd(itemId, count, maximumStack);
        }

        public BackpackReplacementResult TryReplacePickup(int slotIndex, string itemId, int count, int maximumStack)
        {
            if (!isActive)
            {
                return new BackpackReplacementResult(false, null, new BackpackAddResult(0, count));
            }

            return backpack.TryReplaceSlot(slotIndex, itemId, count, maximumStack);
        }

        public void Tick(double deltaTime, Vec2 newPlayerPosition, IReadOnlyList<DiveHit> hits)
        {
            if (!isActive)
            {
                return;
            }

            playerPosition = newPlayerPosition;
            if (hits != null)
            {
                for (var hitIndex = 0; hitIndex < hits.Count; hitIndex++)
                {
                    var hit = hits[hitIndex];
                    if (hit != null)
                    {
                        erosion.ApplyHit(hit.ErosionDamage);
                    }
                }
            }

            if (erosion.GetState().IsDepleted)
            {
                FinishForcedReturn();
                return;
            }

            erosion.Tick(deltaTime);
            if (erosion.GetState().IsDepleted)
            {
                FinishForcedReturn();
            }
        }

        public bool RequestExtract()
        {
            if (!isActive)
            {
                return false;
            }

            isActive = false;
            Finished?.Invoke(new DiveResult(DiveEndReason.Extracted, floorId, backpack.GetState(), new BackpackLossResult(new Dictionary<string, int>(), 0)));
            return true;
        }

        private void FinishForcedReturn()
        {
            if (!isActive)
            {
                return;
            }

            isActive = false;
            var loss = backpack.LoseFraction(forcedLossFraction, rng);
            Finished?.Invoke(new DiveResult(DiveEndReason.ForcedReturn, floorId, backpack.GetState(), loss));
        }
    }
}
