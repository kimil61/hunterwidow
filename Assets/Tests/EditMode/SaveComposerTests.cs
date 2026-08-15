using System;
using System.Collections.Generic;
using System.IO;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Persistence;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class SaveComposerTests
    {
        [Test]
        public void SaveRoundTripsHumanReadableIds()
        {
            var path = CreatePath();
            try
            {
                var composer = CreateComposer();
                composer.Save(path, new GameSaveState
                {
                    Gold = 42,
                    Affinity = 3,
                    CycleCount = 5,
                    TriggeredEndingId = "end_hope",
                    Inventory = new List<IdCount> { new IdCount("mat_herb", 4) },
                    UpgradeLevels = new List<IdCount> { new IdCount("upg_backpack", 1) },
                    UnlockedRecipes = new List<string> { "rcp_tonic" },
                    UnlockedCgs = new List<string> { "cg_first" },
                    UnlockedFloors = new List<string> { "floor_mountain" },
                    CraftedItems = new List<string> { "itm_tonic" },
                    Flags = new List<string> { "flag_seen" },
                    CauldronJobs = new List<CauldronJobState> { new CauldronJobState("rcp_tonic", 2) }
                });

                var loaded = composer.Load(path);

                Assert.That(loaded.UsedBackup, Is.False);
                Assert.That(loaded.State.Version, Is.EqualTo(1));
                Assert.That(loaded.State.Gold, Is.EqualTo(42));
                Assert.That(loaded.State.Inventory[0].Id, Is.EqualTo("mat_herb"));
                Assert.That(loaded.State.CauldronJobs[0].RecipeId, Is.EqualTo("rcp_tonic"));
                Assert.That(loaded.State.CauldronJobs[0].RemainingCycles, Is.EqualTo(2));
                Assert.That(File.ReadAllText(path), Does.Contain("\"mat_herb\""));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Test]
        public void MissingFieldsDefaultAndUnknownIdsAreIgnored()
        {
            var path = CreatePath();
            try
            {
                File.WriteAllText(path, "{ \"version\": 1, \"gold\": 7, \"inventory\": [ { \"id\": \"mat_herb\", \"count\": 2 }, { \"id\": \"mat_unknown\", \"count\": 9 } ], \"flags\": [ \"flag_seen\", \"flag_unknown\" ] }");

                var loaded = CreateComposer().Load(path);

                Assert.That(loaded.State.Gold, Is.EqualTo(7));
                Assert.That(loaded.State.Affinity, Is.EqualTo(0));
                Assert.That(loaded.State.Inventory.Count, Is.EqualTo(1));
                Assert.That(loaded.State.Flags, Is.EqualTo(new[] { "flag_seen" }));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Test]
        public void CorruptPrimaryFallsBackToPreviousBackup()
        {
            var path = CreatePath();
            try
            {
                var composer = CreateComposer();
                composer.Save(path, new GameSaveState { Gold = 10 });
                composer.Save(path, new GameSaveState { Gold = 20 });
                File.WriteAllText(path, "{ bad json");

                var loaded = composer.Load(path);

                Assert.That(loaded.UsedBackup, Is.True);
                Assert.That(loaded.RecoveredFromInvalidSave, Is.True);
                Assert.That(loaded.State.Gold, Is.EqualTo(10));
            }
            finally
            {
                DeletePath(path);
            }
        }

        private static SaveComposer CreateComposer()
        {
            return new SaveComposer(new[]
            {
                "mat_herb", "upg_backpack", "rcp_tonic", "cg_first", "floor_mountain", "itm_tonic", "flag_seen", "end_hope"
            });
        }

        private static string CreatePath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hunterwidow-save-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "save.json");
        }

        private static void DeletePath(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
