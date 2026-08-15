using System;
using System.IO;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Progression;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class MvpContentContractTests
    {
        [Test]
        public void DiveContentKeepsTheDocumentedErosionInventoryAndContactContracts()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.Require("floor_mountain").GetNumber("startingErosion"), Is.EqualTo(100d));
            Assert.That(catalog.Require("floor_mountain").GetNumber("decayPerSecond"), Is.EqualTo(0.2d));
            Assert.That(catalog.Require("floor_mine").GetNumber("startingErosion"), Is.EqualTo(70d));
            Assert.That(catalog.Require("floor_mine").GetNumber("decayPerSecond"), Is.EqualTo(0.25d));
            Assert.That(catalog.Require("floor_depth").GetNumber("startingErosion"), Is.EqualTo(40d));
            Assert.That(catalog.Require("floor_depth").GetNumber("decayPerSecond"), Is.EqualTo(0.35d));
            Assert.That(catalog.GetErosionConfig().GetNumber("purifyAmount"), Is.InRange(15d, 20d));
            Assert.That(catalog.GetInventoryConfig().GetNumber("gatherHoldSeconds"), Is.EqualTo(3d));
            Assert.That(catalog.GetInventoryConfig().GetNumber("forcedLossFraction"), Is.EqualTo(0.5d));
            var depthRequirement = catalog.GetFloorAccessRequirement("floor_depth");
            Assert.That(depthRequirement.UpgradeAxisId, Is.EqualTo("resistance"));
            Assert.That(depthRequirement.MinimumLevel, Is.EqualTo(1));
            Assert.That(catalog.GetMaterialStackLimit("mat_herb"), Is.EqualTo(5));
            Assert.That(catalog.GetMaterialStackLimit("mat_forbidden"), Is.EqualTo(3));
            Assert.That(catalog.Require("enm_shadow").GetObject("params")["contactDamage"], Is.EqualTo(10d));
            Assert.That(catalog.Require("enm_wanderer").GetObject("params")["contactDamage"], Is.EqualTo(11d));
            Assert.That(catalog.Require("enm_ranged").GetObject("params")["contactDamage"], Is.EqualTo(12d));
        }

        [Test]
        public void FullPackKeepsTheDocumentedGrowthAndEndingCounts()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.GetUpgrades().Count, Is.EqualTo(15));
            Assert.That(catalog.GetCgThresholds().Count, Is.EqualTo(8));
            Assert.That(catalog.GetEndings().Count, Is.InRange(2, 3));
            var entries = catalog.Require("drop_depth").GetArray("entries");
            var entry = ContentValues.AsObject(entries[0]);
            Assert.That(entry, Is.Not.Null);
            Assert.That(ContentValues.GetString(entry, "materialId"), Is.EqualTo("mat_deep"));
        }

        [Test]
        public void CauldronUpgradesIncreaseSlotsFromTheBaseCapacity()
        {
            var catalog = LoadCatalog();
            var state = new ProgressionState();
            state.AddGold(1000);
            var effects = catalog.CreateUpgradeEffectRegistry();
            var upgrades = new UpgradeLogic();

            Assert.That(effects.GetValue("cauldron_slots"), Is.EqualTo(1d));
            for (var level = 1; level <= 3; level++)
            {
                var definition = FindUpgrade(catalog, "cauldron", level);
                Assert.That(upgrades.TryPurchase(definition, state, effects), Is.True);
                Assert.That(effects.GetValue("cauldron_slots"), Is.EqualTo(level + 1d));
            }
        }

        [Test]
        public void CombatAudioAndEndingFallbackFollowTheMvpContracts()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.GetAudioConfig().GetBool("playBgmDuringDive"), Is.False);
            Assert.That(catalog.GetAudioConfig().GetNumber("loopSeconds"), Is.InRange(60d, 90d));
            var combat = catalog.GetCombatConfig();
            Assert.That(combat.GetNumber("toneSeconds"), Is.InRange(0.1d, 1.5d));
            Assert.That(combat.GetNumber("forwardHitStopSeconds"), Is.InRange(0.05d, 0.12d));
            Assert.That(combat.GetNumber("returnHitStopSeconds"), Is.InRange(0.05d, 0.12d));
            var toneKeys = new[] { "sweetToneHz", "lateToneHz", "returnToneHz", "cancelToneHz", "hitToneHz" };
            for (var toneIndex = 0; toneIndex < toneKeys.Length; toneIndex++)
            {
                Assert.That(combat.GetNumber(toneKeys[toneIndex]), Is.GreaterThan(0d));
            }

            Assert.That(SelectFinalMedicineEnding(catalog, 0), Is.EqualTo("end_solitude"));
            Assert.That(SelectFinalMedicineEnding(catalog, 3), Is.EqualTo("end_hope"));
            Assert.That(SelectFinalMedicineEnding(catalog, 6), Is.EqualTo("end_devotion"));
        }

        private static string SelectFinalMedicineEnding(GameContentCatalog catalog, int affinity)
        {
            var state = new ProgressionState();
            state.MarkCrafted("itm_final_medicine");
            state.AddAffinity(affinity);
            return EndingSelector.Select(catalog.GetEndings(), state);
        }

        private static UpgradeDefinition FindUpgrade(GameContentCatalog catalog, string axisId, int level)
        {
            var upgrades = catalog.GetUpgrades();
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                var upgrade = upgrades[upgradeIndex];
                if (string.Equals(upgrade.AxisId, axisId, StringComparison.Ordinal) && upgrade.Level == level)
                {
                    return upgrade;
                }
            }

            Assert.Fail("Missing upgrade: " + axisId + " level " + level + ".");
            return null;
        }

        private static GameContentCatalog LoadCatalog()
        {
            var content = ContentLoader.Load(GetContentPath());
            Assert.That(content.Report.HasErrors, Is.False, content.Report.ToMultilineText());
            return new GameContentCatalog(content.Database);
        }

        private static string GetContentPath()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "content");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the content pack.");
        }
    }
}
