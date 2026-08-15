using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Rng;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class BackpackLogicTests
    {
        [Test]
        public void AddsStacksUntilCapacityThenRejectsWithoutAutoDiscard()
        {
            var backpack = new BackpackLogic(2);

            var first = backpack.TryAdd("mat_herb", 6, 5);
            var second = backpack.TryAdd("mat_forbidden", 3, 3);

            Assert.That(first.AddedCount, Is.EqualTo(6));
            Assert.That(first.RejectedCount, Is.EqualTo(0));
            Assert.That(second.AddedCount, Is.EqualTo(0));
            Assert.That(second.RejectedCount, Is.EqualTo(3));
            Assert.That(backpack.GetCount("mat_herb"), Is.EqualTo(6));
            Assert.That(backpack.GetState().UsedSlots, Is.EqualTo(2));
        }

        [Test]
        public void ForcedLossRemovesConfiguredFractionDeterministically()
        {
            var backpack = new BackpackLogic(3);
            backpack.TryAdd("mat_herb", 4, 5);
            backpack.TryAdd("mat_forbidden", 2, 3);

            var loss = backpack.LoseFraction(0.5d, new SeededRng(123u));

            Assert.That(loss.TotalLost, Is.EqualTo(3));
            Assert.That(backpack.GetCount("mat_herb") + backpack.GetCount("mat_forbidden"), Is.EqualTo(3));
        }

        [Test]
        public void ExplicitReplacementDiscardsOnlyTheChosenSlotAndAddsThePendingPickup()
        {
            var backpack = new BackpackLogic(2);
            backpack.TryAdd("mat_herb", 5, 5);
            backpack.TryAdd("mat_forbidden", 3, 3);

            var replacement = backpack.TryReplaceSlot(0, "mat_deep", 1, 3);

            Assert.That(replacement.Replaced, Is.True);
            Assert.That(replacement.Discarded.ItemId, Is.EqualTo("mat_herb"));
            Assert.That(replacement.Discarded.Count, Is.EqualTo(5));
            Assert.That(replacement.Added.AddedCount, Is.EqualTo(1));
            Assert.That(backpack.GetCount("mat_herb"), Is.EqualTo(0));
            Assert.That(backpack.GetCount("mat_deep"), Is.EqualTo(1));
            Assert.That(backpack.GetCount("mat_forbidden"), Is.EqualTo(3));
        }

        [Test]
        public void InvalidReplacementDoesNotDiscardAnything()
        {
            var backpack = new BackpackLogic(1);
            backpack.TryAdd("mat_herb", 5, 5);

            var replacement = backpack.TryReplaceSlot(1, "mat_deep", 1, 3);

            Assert.That(replacement.Replaced, Is.False);
            Assert.That(backpack.GetCount("mat_herb"), Is.EqualTo(5));
            Assert.That(backpack.GetCount("mat_deep"), Is.EqualTo(0));
        }
    }
}
