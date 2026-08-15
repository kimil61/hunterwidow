using System;
using System.Collections.Generic;
using System.IO;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Narrative;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class ContentDrivenLayoutTests
    {
        [Test]
        public void FullPackDefinesAllFloorSpawnAndRopeContractsInContent()
        {
            var result = ContentLoader.Load(GetProjectPath("Assets", "StreamingAssets", "content"));
            var catalog = new GameContentCatalog(result.Database);

            var mountain = catalog.GetFloorLayout("floor_mountain");
            var mine = catalog.GetFloorLayout("floor_mine");
            var depth = catalog.GetFloorLayout("floor_depth");

            Assert.That(mountain.RopeCandidates.Count, Is.EqualTo(5));
            Assert.That(mountain.ActiveRopeCount, Is.EqualTo(2));
            Assert.That(mountain.EnemySpawns.Count, Is.InRange(5, 7));
            Assert.That(mountain.GatherSpawns.Count, Is.InRange(4, 5));
            Assert.That(mine.EnemySpawns.Count, Is.InRange(7, 9));
            Assert.That(depth.EnemySpawns.Count, Is.InRange(9, 12));
            Assert.That(depth.PurifierPositions.Count, Is.InRange(0, 1));

            var tutorial = catalog.GetTutorialDefinition();
            Assert.That(tutorial.TargetPositions.Count, Is.EqualTo(3));
            Assert.That(tutorial.IntroTextKeys.Count, Is.EqualTo(3));
            Assert.That(tutorial.TargetEnemyId, Is.EqualTo("enm_training_target"));
            Assert.That(tutorial.CompletionFlagId, Is.EqualTo("flag_tutorial_sweet"));
        }

        [Test]
        public void ExistingEnemyBehaviorCanBeAddedThroughEnemiesContentOnly()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = Path.Combine(Path.GetTempPath(), "hunterwidow-layout-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                var floorPath = Path.Combine(temporaryPath, "entities", "floors.json");
                var originalFloors = File.ReadAllText(floorPath);
                AddContentOnlyEnemy(temporaryPath);

                var result = ContentLoader.Load(temporaryPath);
                Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());

                var catalog = new GameContentCatalog(result.Database);
                Assert.That(File.ReadAllText(floorPath), Is.EqualTo(originalFloors));
                Assert.That(ContainsId(catalog.GetSpawnableEnemiesForFloor("floor_mountain"), "enm_content_only"), Is.True);
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }
            }
        }

        [Test]
        public void AdditionalFloorCanBeChainedUsingOnlyContent()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = Path.Combine(Path.GetTempPath(), "hunterwidow-floor-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                AddContentOnlyFloor(temporaryPath);

                var result = ContentLoader.Load(temporaryPath);
                Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());

                var layout = new GameContentCatalog(result.Database).GetFloorLayout("floor_content_only");
                Assert.That(layout.EnemySpawns.Count, Is.EqualTo(5));
                Assert.That(layout.DescentPosition, Is.Null);
                Assert.That(ContainsId(new GameContentCatalog(result.Database).GetSpawnableEnemiesForFloor("floor_content_only"), "enm_floor_content_only"), Is.True);
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }
            }
        }

        [Test]
        public void StoryEventCanBeAddedUsingOnlyNarrativeContent()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = Path.Combine(Path.GetTempPath(), "hunterwidow-story-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                AddContentOnlyStoryEvent(temporaryPath);

                var result = ContentLoader.Load(temporaryPath);
                Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());

                var events = new GameContentCatalog(result.Database).GetStoryEvents();
                Assert.That(ContainsStoryEventId(events, "evt_content_only"), Is.True);
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }
            }
        }

        private static void AddContentOnlyEnemy(string rootPath)
        {
            var enemyPath = Path.Combine(rootPath, "entities", "enemies.json");
            var enemies = File.ReadAllText(enemyPath);
            var arrayEnd = enemies.LastIndexOf(']');
            Assert.That(arrayEnd, Is.GreaterThan(0));
            var entry = ",\n    {\n      \"id\": \"enm_content_only\",\n      \"type\": \"enemy\",\n      \"behavior\": \"chaser\",\n      \"nameKey\": \"enemy.shadow.name\",\n      \"descKey\": \"enemy.shadow.desc\",\n      \"tint\": [0.25, 0.75, 0.65, 1],\n      \"params\": { \"maxHealth\": 45, \"moveSpeed\": 2.4, \"contactDamage\": 9, \"wanderDistance\": 0, \"wanderMoveMultiplier\": 0, \"retreatDistance\": 0 },\n      \"dropTableId\": \"drop_mountain\",\n      \"floorId\": \"floor_mountain\"\n    }\n  ";
            File.WriteAllText(enemyPath, enemies.Insert(arrayEnd, entry));
        }

        private static void AddContentOnlyFloor(string rootPath)
        {
            var floorPath = Path.Combine(rootPath, "entities", "floors.json");
            var floors = File.ReadAllText(floorPath).Replace("\"decayPerSecond\": 0.35,", "\"decayPerSecond\": 0.35,\n      \"nextFloorId\": \"floor_content_only\",");
            var arrayEnd = floors.LastIndexOf(']');
            Assert.That(arrayEnd, Is.GreaterThan(0));
            var entry = ",\n    {\n      \"id\": \"floor_content_only\",\n      \"type\": \"floor\",\n      \"nameKey\": \"floor.mountain.name\",\n      \"descKey\": \"floor.mountain.desc\",\n      \"startingErosion\": 35,\n      \"decayPerSecond\": 0.4,\n      \"visual\": {\n        \"backgroundColor\": [0.12, 0.16, 0.2, 1], \"groundColor\": [0.08, 0.1, 0.14, 1], \"pathColor\": [0.18, 0.16, 0.2, 1],\n        \"ropeColor\": [0.8, 0.7, 0.3, 1], \"purifierColor\": [0.3, 0.7, 0.8, 1], \"descentColor\": [0.15, 0.15, 0.15, 1],\n        \"playerColor\": [0.35, 0.8, 1, 1], \"waveForwardColor\": [1, 0.9, 0.35, 1], \"waveReturnColor\": [1, 0.5, 0.2, 1]\n      },\n      \"layout\": {\n        \"playerStart\": [-7, 0], \"ropeCandidates\": [[-8, 5], [8, -2], [-6, 3], [7, 5]], \"activeRopeCount\": 2, \"purifierPositions\": [],\n        \"enemySpawns\": [\n          { \"position\": [-1, 2], \"candidateIds\": [\"enm_floor_content_only\"] }, { \"position\": [2, -1], \"candidateIds\": [\"enm_floor_content_only\"] },\n          { \"position\": [5, 1], \"candidateIds\": [\"enm_floor_content_only\"] }, { \"position\": [-3, 3], \"candidateIds\": [\"enm_floor_content_only\"] }, { \"position\": [3, 4], \"candidateIds\": [\"enm_floor_content_only\"] }\n        ],\n        \"gatherSpawns\": [{ \"position\": [-4, -2], \"candidateIds\": [\"mat_herb\"] }, { \"position\": [2, 2], \"candidateIds\": [\"mat_herb\"] }, { \"position\": [4, -2], \"candidateIds\": [\"mat_herb\"] }]\n      }\n    }\n  ";
            File.WriteAllText(floorPath, floors.Insert(arrayEnd, entry));

            var enemyPath = Path.Combine(rootPath, "entities", "enemies.json");
            var enemies = File.ReadAllText(enemyPath);
            var enemyArrayEnd = enemies.LastIndexOf(']');
            Assert.That(enemyArrayEnd, Is.GreaterThan(0));
            var enemyEntry = ",\n    {\n      \"id\": \"enm_floor_content_only\",\n      \"type\": \"enemy\",\n      \"behavior\": \"ranged\",\n      \"nameKey\": \"enemy.ranged.name\",\n      \"descKey\": \"enemy.ranged.desc\",\n      \"tint\": [0.65, 0.35, 0.9, 1],\n      \"params\": { \"maxHealth\": 75, \"moveSpeed\": 1.8, \"contactDamage\": 12, \"wanderDistance\": 0, \"wanderMoveMultiplier\": 0, \"retreatDistance\": 3 },\n      \"dropTableId\": \"drop_depth\",\n      \"floorId\": \"floor_content_only\"\n    }\n  ";
            File.WriteAllText(enemyPath, enemies.Insert(enemyArrayEnd, enemyEntry));
        }

        private static void AddContentOnlyStoryEvent(string rootPath)
        {
            var storyPath = Path.Combine(rootPath, "narrative", "story_events.json");
            var events = File.ReadAllText(storyPath);
            var arrayEnd = events.LastIndexOf(']');
            Assert.That(arrayEnd, Is.GreaterThan(0));
            var entry = ",\n    {\n      \"id\": \"evt_content_only\",\n      \"type\": \"story_event\",\n      \"priority\": 5,\n      \"portraitCgId\": \"cg_first_memory\",\n      \"textKey\": \"story.first_return.text\",\n      \"conditions\": [{ \"type\": \"flag_not_set\", \"id\": \"flag_evt_first_return\" }],\n      \"effects\": [{ \"type\": \"set_flag\", \"id\": \"flag_evt_first_return\" }]\n    }\n  ";
            File.WriteAllText(storyPath, events.Insert(arrayEnd, entry));
        }

        private static bool ContainsId(IReadOnlyList<ContentItem> items, string id)
        {
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (string.Equals(items[itemIndex].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsStoryEventId(IReadOnlyList<StoryEventDefinition> events, string id)
        {
            for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                if (string.Equals(events[eventIndex].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);
            foreach (var filePath in Directory.GetFiles(sourcePath))
            {
                File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)), true);
            }

            foreach (var directoryPath in Directory.GetDirectories(sourcePath))
            {
                CopyDirectory(directoryPath, Path.Combine(destinationPath, Path.GetFileName(directoryPath)));
            }
        }

        private static string GetProjectPath(params string[] relativeSegments)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets"))
                    && Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
                {
                    var path = directory.FullName;
                    for (var segmentIndex = 0; segmentIndex < relativeSegments.Length; segmentIndex++)
                    {
                        path = Path.Combine(path, relativeSegments[segmentIndex]);
                    }

                    return path;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the Unity project root.");
        }
    }
}
