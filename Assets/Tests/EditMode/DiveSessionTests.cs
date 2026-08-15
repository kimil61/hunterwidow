using System.Collections.Generic;
using HunterWidow.Domain.Common;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Erosion;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Rng;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class DiveSessionTests
    {
        [Test]
        public void ZeroErosionForcesImmediateReturnAndLosesHalfBackpack()
        {
            var session = CreateSession(2d, 0d);
            DiveResult result = null;
            session.Finished += value => result = value;
            session.Start("floor_mountain", new Vec2(0d, 0d));
            session.TryCollect("mat_herb", 4, 5);

            session.Tick(0d, new Vec2(1d, 0d), new List<DiveHit> { new DiveHit("enm_shadow", 2d) });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Reason, Is.EqualTo(DiveEndReason.ForcedReturn));
            Assert.That(result.Loss.TotalLost, Is.EqualTo(2));
            Assert.That(result.Backpack.Slots[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void RopeExtractionWinsWhenRequestedBeforeSameFrameDepletion()
        {
            var session = CreateSession(1d, 1d);
            DiveResult result = null;
            session.Finished += value => result = value;
            session.Start("floor_mountain", new Vec2(0d, 0d));
            session.TryCollect("mat_herb", 2, 5);

            Assert.That(session.RequestExtract(), Is.True);
            session.Tick(1d, new Vec2(1d, 0d), new List<DiveHit> { new DiveHit("enm_shadow", 4d) });

            Assert.That(result.Reason, Is.EqualTo(DiveEndReason.Extracted));
            Assert.That(result.Loss.TotalLost, Is.EqualTo(0));
            Assert.That(result.Backpack.Slots[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void PurificationRestoresErosionOnlyDuringAnActiveDive()
        {
            var session = CreateSession(40d, 0d);

            Assert.That(session.Purify(10d), Is.False);

            session.Start("floor_mountain", new Vec2(0d, 0d));

            Assert.That(session.Purify(10d), Is.True);
            Assert.That(session.GetState().Erosion.CurrentValue, Is.EqualTo(50d));
        }

        [Test]
        public void ActiveDiveAllowsAnExplicitPickupReplacement()
        {
            var session = CreateSession(100d, 0d);
            session.Start("floor_mountain", new Vec2(0d, 0d));
            for (var slotIndex = 0; slotIndex < 8; slotIndex++)
            {
                session.TryCollect("slot_" + slotIndex, 1, 1);
            }

            var replacement = session.TryReplacePickup(0, "mat_deep", 1, 3);
            var state = session.GetState();

            Assert.That(replacement.Replaced, Is.True);
            Assert.That(replacement.Discarded.ItemId, Is.EqualTo("slot_0"));
            Assert.That(state.Backpack.UsedSlots, Is.EqualTo(8));
            Assert.That(state.Backpack.Slots[0].ItemId, Is.EqualTo("slot_1"));
            Assert.That(state.Backpack.Slots[7].ItemId, Is.EqualTo("mat_deep"));
        }

        private static DiveSession CreateSession(double startingValue, double decayPerSecond)
        {
            var erosion = new ErosionLogic(new ErosionSettings(100d, startingValue, decayPerSecond, 60d, 30d));
            return new DiveSession(erosion, new BackpackLogic(8), new SeededRng(4u), 0.5d);
        }
    }
}
