using System;
using System.IO;
using HunterWidow.Domain.Combat;
using HunterWidow.Domain.Content;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class CombatTuningTests
    {
        [Test]
        public void ContentTuningCanRoundTripThroughJsonOverride()
        {
            var content = ContentLoader.Load(GetContentPath());
            Assert.That(content.Report.HasErrors, Is.False, content.Report.ToMultilineText());
            var defaults = CombatTuning.FromContent(content.Database);
            var path = Path.Combine(Path.GetTempPath(), "hunterwidow-tuning-" + Guid.NewGuid().ToString("N"), "combat.json");
            var store = new TuningOverrideStore(path);

            try
            {
                store.Save(defaults.WithOverrides(MiniJson.Parse("{ \"damage\": 31, \"sweetStart\": 0.5 }") as System.Collections.Generic.IDictionary<string, object>));
                var loaded = store.Load(defaults);

                Assert.That(loaded.Damage, Is.EqualTo(31d));
                Assert.That(loaded.SweetStart, Is.EqualTo(0.5d));
                Assert.That(loaded.MaxRange, Is.EqualTo(defaults.MaxRange));
            }
            finally
            {
                var directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void ExistingWeaponBehaviorCanBeSelectedFromContentWithoutCodeChanges()
        {
            var sourcePath = GetContentPath();
            var temporaryPath = Path.Combine(Path.GetTempPath(), "hunterwidow-weapon-content-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(sourcePath, temporaryPath);
                AddContentOnlyWeapon(temporaryPath);

                var content = ContentLoader.Load(temporaryPath);
                Assert.That(content.Report.HasErrors, Is.False, content.Report.ToMultilineText());

                var tuning = CombatTuning.FromContent(content.Database);
                Assert.That(tuning.Damage, Is.EqualTo(31d));
                Assert.That(tuning.MaxRange, Is.EqualTo(9d));
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, true);
                }
            }
        }

        private static void AddContentOnlyWeapon(string rootPath)
        {
            var weaponPath = Path.Combine(rootPath, "entities", "weapons.json");
            var weapons = File.ReadAllText(weaponPath);
            var weaponArrayEnd = weapons.LastIndexOf(']');
            Assert.That(weaponArrayEnd, Is.GreaterThan(0));
            var weapon = ",\n    {\n      \"id\": \"wpn_content_only\",\n      \"type\": \"weapon\",\n      \"behavior\": \"charge_wave\",\n      \"nameKey\": \"weapon.charge_wave.name\",\n      \"descKey\": \"weapon.charge_wave.desc\",\n      \"params\": {\n        \"minCharge\": 0.15, \"sweetStart\": 0.55, \"sweetEnd\": 0.8, \"maxCharge\": 1.2,\n        \"maxRange\": 9, \"speed\": 12, \"returnSpeed\": 15, \"damage\": 31,\n        \"lateDamageMultiplier\": 0.65, \"returnDamageMultiplier\": 1, \"timeoutSeconds\": 4\n      }\n    }\n  ";
            File.WriteAllText(weaponPath, weapons.Insert(weaponArrayEnd, weapon));

            var combatPath = Path.Combine(rootPath, "config", "combat.json");
            var combat = File.ReadAllText(combatPath);
            Assert.That(combat, Does.Contain("\"starterWeaponId\": \"wpn_charge_wave\""));
            File.WriteAllText(combatPath, combat.Replace("\"starterWeaponId\": \"wpn_charge_wave\"", "\"starterWeaponId\": \"wpn_content_only\""));
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
