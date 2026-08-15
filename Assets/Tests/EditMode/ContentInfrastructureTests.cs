using System;
using System.IO;
using HunterWidow.Domain.Content;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class ContentInfrastructureTests
    {
        [Test]
        public void MinimalContentPackPassesValidation()
        {
            var result = ContentLoader.Load(GetProjectPath("Assets", "StreamingAssets", "content_minimal"));

            Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());
            Assert.That(result.HasContent, Is.True);
            Assert.That(ContentBootStatus.From(result).CanStart, Is.True);
            Assert.That(MvpContentRequirements.IsReady(result.Database), Is.False);
        }

        [Test]
        public void FullContentPackDeclaresThePlayableRuntimeContract()
        {
            var result = ContentLoader.Load(GetProjectPath("Assets", "StreamingAssets", "content"));

            Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());
            Assert.That(MvpContentRequirements.IsReady(result.Database), Is.True);
        }

        [Test]
        public void EmptyContentPackProducesRecoverableNoContentState()
        {
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.HasContent, Is.False);
                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(ContentBootStatus.From(result).CanStart, Is.False);
                Assert.That(result.Report.ToMultilineText(), Does.Contain("CONTENT_EMPTY"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void ValidatorCollectsIndependentErrorsInOneReport()
        {
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                File.WriteAllText(Path.Combine(temporaryPath, "pack.json"), "{ \"schemaVersion\": 1, \"items\": [] }");
                Directory.CreateDirectory(Path.Combine(temporaryPath, "locale"));
                File.WriteAllText(Path.Combine(temporaryPath, "locale", "strings.csv"), "key,ko\n");
                File.WriteAllText(
                    Path.Combine(temporaryPath, "bad.json"),
                    "{ \"schemaVersion\": 1, \"items\": [ { \"id\": \"wpn_bad\", \"type\": \"weapon\", \"behavior\": \"unknown\", \"params\": {}, \"nameKey\": \"missing.key\", \"dropTableId\": \"drop_missing\" } ] }");

                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.Issues.Count, Is.GreaterThanOrEqualTo(4));
                Assert.That(result.Report.ToMultilineText(), Does.Contain("BEHAVIOR_UNREGISTERED"));
                Assert.That(result.Report.ToMultilineText(), Does.Contain("LOCALE_KEY_MISSING"));
                Assert.That(result.Report.ToMultilineText(), Does.Contain("REFERENCE_MISSING"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void BehaviorSchemasOwnTheValidatorParameterContract()
        {
            IContentBehaviorSchema weaponSchema;
            IContentBehaviorSchema enemySchema;

            Assert.That(ContentBehaviorSchemaRegistry.TryGet("weapon", "charge_wave", out weaponSchema), Is.True);
            Assert.That(ContentBehaviorSchemaRegistry.TryGet("enemy", "ranged", out enemySchema), Is.True);
            Assert.That(weaponSchema.RequiredNumericParameters, Does.Contain("sweetStart"));
            Assert.That(enemySchema.RequiredNumericParameters, Does.Contain("retreatDistance"));
        }

        [Test]
        public void PlayablePackCanRenameConfigIdsWithoutChangingRuntimeContracts()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                RenameConfigIds(temporaryPath);

                var result = ContentLoader.Load(temporaryPath);
                Assert.That(result.Report.HasErrors, Is.False, result.Report.ToMultilineText());
                Assert.That(MvpContentRequirements.IsReady(result.Database), Is.True);

                var catalog = new GameContentCatalog(result.Database);
                Assert.That(catalog.GetCombatConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetErosionConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetInventoryConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetEconomyConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetProgressionConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetOptionsConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetAudioConfig().Id, Does.EndWith("_variant"));
                Assert.That(catalog.GetUiConfig().Id, Does.EndWith("_variant"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void FloorAccessRequirementMustReferenceADeclaredUpgradeAxis()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                var floorPath = Path.Combine(temporaryPath, "entities", "floors.json");
                var floors = File.ReadAllText(floorPath);
                Assert.That(floors, Does.Contain("\"upgradeAxisId\": \"resistance\""));
                File.WriteAllText(floorPath, floors.Replace("\"upgradeAxisId\": \"resistance\"", "\"upgradeAxisId\": \"missing_axis\""));

                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.ToMultilineText(), Does.Contain("FLOOR_ACCESS_UPGRADE_MISSING"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void PlayablePackMustChooseAWeaponForItsStarterCombatBehavior()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                var combatPath = Path.Combine(temporaryPath, "config", "combat.json");
                var combat = File.ReadAllText(combatPath);
                Assert.That(combat, Does.Contain("\"starterWeaponId\": \"wpn_charge_wave\""));
                File.WriteAllText(combatPath, combat.Replace("\"starterWeaponId\": \"wpn_charge_wave\"", "\"starterWeaponId\": \"itm_tonic\""));

                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.ToMultilineText(), Does.Contain("STARTER_WEAPON_INVALID"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void UpgradeUiGroupsMustReferenceAndCoverEveryDeclaredAxis()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                var uiPath = Path.Combine(temporaryPath, "config", "ui.json");
                var ui = File.ReadAllText(uiPath);
                Assert.That(ui, Does.Contain("\"backpack\", \"resistance\", \"weapon\""));
                File.WriteAllText(uiPath, ui.Replace("\"backpack\", \"resistance\", \"weapon\"", "\"missing_axis\", \"resistance\", \"weapon\""));

                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.ToMultilineText(), Does.Contain("UI_UPGRADE_GROUP_AXIS_MISSING"));
                Assert.That(result.Report.ToMultilineText(), Does.Contain("UI_UPGRADE_GROUP_AXIS_UNASSIGNED"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
            }
        }

        [Test]
        public void OptionsLanguagesMustHaveUniqueCodesAndLocalizedNames()
        {
            var sourcePath = GetProjectPath("Assets", "StreamingAssets", "content");
            var temporaryPath = CreateTemporaryDirectory();
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                var optionsPath = Path.Combine(temporaryPath, "config", "options.json");
                var options = File.ReadAllText(optionsPath);
                Assert.That(options, Does.Contain("\"code\": \"en\""));
                File.WriteAllText(optionsPath, options.Replace("\"code\": \"en\"", "\"code\": \"\""));

                var result = ContentLoader.Load(temporaryPath);

                Assert.That(result.Report.HasErrors, Is.True);
                Assert.That(result.Report.ToMultilineText(), Does.Contain("OPTIONS_LANGUAGE_CODE_REQUIRED"));
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryPath);
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

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "hunterwidow-content-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void RenameConfigIds(string rootPath)
        {
            var configIds = new[]
            {
                "cfg_combat",
                "cfg_erosion",
                "cfg_inventory",
                "cfg_economy",
                "cfg_progression",
                "cfg_options",
                "cfg_audio",
                "cfg_ui"
            };
            var files = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories);
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var text = File.ReadAllText(files[fileIndex]);
                for (var idIndex = 0; idIndex < configIds.Length; idIndex++)
                {
                    text = text.Replace(configIds[idIndex], configIds[idIndex] + "_variant");
                }

                File.WriteAllText(files[fileIndex], text);
            }
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
    }
}
