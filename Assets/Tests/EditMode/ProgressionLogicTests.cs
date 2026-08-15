using System.Collections.Generic;
using HunterWidow.Domain.Progression;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class ProgressionLogicTests
    {
        [Test]
        public void UpgradeAppliesRegisteredEffectsInOrder()
        {
            var state = new ProgressionState();
            state.AddGold(100);
            var registry = new UpgradeEffectRegistry(new Dictionary<string, double>
            {
                { "weapon_damage", 10d },
                { "weapon_scale", 1d }
            });
            var definition = new UpgradeDefinition(
                "upg_weapon_one",
                "weapon",
                1,
                60,
                new List<UpgradeEffect>
                {
                    new UpgradeEffect("weapon_damage", UpgradeOperation.Multiply, 1.4d),
                    new UpgradeEffect("weapon_scale", UpgradeOperation.Multiply, 1.15d)
                });

            var purchased = new UpgradeLogic().TryPurchase(definition, state, registry);

            Assert.That(purchased, Is.True);
            Assert.That(state.Gold, Is.EqualTo(40));
            Assert.That(state.GetUpgradeLevel("weapon"), Is.EqualTo(1));
            Assert.That(registry.GetValue("weapon_damage"), Is.EqualTo(14d));
            Assert.That(registry.GetValue("weapon_scale"), Is.EqualTo(1.15d));
        }

        [Test]
        public void PurchaseStateSeparatesPurchasedAvailableLockedAndUnaffordableLevels()
        {
            var state = new ProgressionState();
            state.AddGold(70);
            var registry = new UpgradeEffectRegistry(new Dictionary<string, double>
            {
                { "cauldron_slots", 1d }
            });
            var first = new UpgradeDefinition(
                "upg_cauldron_one",
                "cauldron",
                1,
                70,
                new List<UpgradeEffect> { new UpgradeEffect("cauldron_slots", UpgradeOperation.Set, 2d) });
            var second = new UpgradeDefinition(
                "upg_cauldron_two",
                "cauldron",
                2,
                140,
                new List<UpgradeEffect> { new UpgradeEffect("cauldron_slots", UpgradeOperation.Set, 3d) });
            var third = new UpgradeDefinition(
                "upg_cauldron_three",
                "cauldron",
                3,
                210,
                new List<UpgradeEffect> { new UpgradeEffect("cauldron_slots", UpgradeOperation.Set, 4d) });
            var logic = new UpgradeLogic();

            Assert.That(logic.GetPurchaseState(first, state), Is.EqualTo(UpgradePurchaseState.Available));
            Assert.That(logic.GetPurchaseState(second, state), Is.EqualTo(UpgradePurchaseState.RequiresPreviousLevel));
            Assert.That(logic.TryPurchase(first, state, registry), Is.True);
            Assert.That(registry.GetValue("cauldron_slots"), Is.EqualTo(2d));
            Assert.That(logic.GetPurchaseState(first, state), Is.EqualTo(UpgradePurchaseState.AlreadyPurchased));
            Assert.That(logic.GetPurchaseState(second, state), Is.EqualTo(UpgradePurchaseState.InsufficientGold));
            Assert.That(logic.GetPurchaseState(third, state), Is.EqualTo(UpgradePurchaseState.RequiresPreviousLevel));
        }

        [Test]
        public void AffinityUnlocksMultipleThresholdsInOrder()
        {
            var affinity = new AffinityLogic(new List<CgThreshold>
            {
                new CgThreshold("cg_late", 3),
                new CgThreshold("cg_first", 1),
                new CgThreshold("cg_second", 2)
            });

            var unlocked = affinity.Add(3);

            Assert.That(unlocked, Is.EqualTo(new[] { "cg_first", "cg_second", "cg_late" }));
            Assert.That(affinity.IsUnlocked("cg_late"), Is.True);
        }
    }
}
