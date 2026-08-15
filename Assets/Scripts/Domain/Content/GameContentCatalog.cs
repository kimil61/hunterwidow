using System;
using System.Collections.Generic;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Combat;
using HunterWidow.Domain.Economy;
using HunterWidow.Domain.Erosion;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Domain.Content
{
    public sealed class DropEntry
    {
        public DropEntry(string materialId, double weight, string upgradedMaterialId)
        {
            MaterialId = materialId;
            Weight = weight;
            UpgradedMaterialId = upgradedMaterialId;
        }

        public string MaterialId { get; }

        public double Weight { get; }

        public string UpgradedMaterialId { get; }
    }

    public sealed class GameContentCatalog
    {
        private readonly ContentDatabase database;

        public GameContentCatalog(ContentDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<string> GetAllKnownIds()
        {
            var ids = new List<string>();
            for (var itemIndex = 0; itemIndex < database.AllItems.Count; itemIndex++)
            {
                var id = database.AllItems[itemIndex].Id;
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        public string GetDefaultLocale()
        {
            foreach (var pack in database.FindByType("pack"))
            {
                var defaultLocale = pack.GetString("defaultLocale");
                if (!string.IsNullOrEmpty(defaultLocale))
                {
                    return defaultLocale;
                }
            }

            return "ko";
        }

        public ContentItem Require(string id)
        {
            ContentItem item;
            if (!database.TryGet(id, out item))
            {
                throw new KeyNotFoundException("Content item is missing: " + id);
            }

            return item;
        }

        public CombatTuning CreateCombatTuning()
        {
            return CombatTuning.FromContent(database);
        }

        public ContentItem GetCombatConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Combat);
        }

        public ContentItem GetErosionConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Erosion);
        }

        public ContentItem GetInventoryConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Inventory);
        }

        public ContentItem GetEconomyConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Economy);
        }

        public ContentItem GetProgressionConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Progression);
        }

        public ContentItem GetOptionsConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Options);
        }

        public ContentItem GetAudioConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Audio);
        }

        public ContentItem GetUiConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Ui);
        }

        public ContentItem GetSimulationConfig()
        {
            return ContentConfigRoles.Require(database, ContentConfigRoles.Simulation);
        }

        public ErosionSettings CreateErosionSettings(string floorId, double decayMultiplier)
        {
            var erosionConfig = GetErosionConfig();
            var floor = Require(floorId);
            var bands = new List<ErosionBandDefinition>();
            var rawBands = erosionConfig.GetArray("bands");
            if (rawBands == null)
            {
                throw new InvalidOperationException("The erosion configuration requires a bands array.");
            }

            for (var bandIndex = 0; bandIndex < rawBands.Count; bandIndex++)
            {
                var rawBand = ContentValues.AsObject(rawBands[bandIndex]);
                if (rawBand == null)
                {
                    throw new InvalidOperationException("An erosion band is invalid.");
                }

                bands.Add(new ErosionBandDefinition(
                    ContentValues.GetString(rawBand, "id"),
                    ContentValues.GetNumber(rawBand, "minimumValue"),
                    ContentValues.GetNumber(rawBand, "dropUpgradeChance")));
            }

            return new ErosionSettings(
                erosionConfig.GetNumber("maxValue"),
                floor.GetNumber("startingErosion"),
                floor.GetNumber("decayPerSecond") * decayMultiplier,
                bands);
        }

        public IReadOnlyList<ContentItem> GetFloors()
        {
            return FindByType("floor");
        }

        public FloorLayoutDefinition GetFloorLayout(string floorId)
        {
            return FloorLayoutDefinition.FromContent(Require(floorId));
        }

        public FloorAccessRequirement GetFloorAccessRequirement(string floorId)
        {
            return FloorAccessRequirement.FromContent(Require(floorId));
        }

        public ErosionBandPresentationDefinition GetErosionBandPresentation(string bandId)
        {
            var rawBands = GetErosionConfig().GetArray("bands");
            if (rawBands != null)
            {
                for (var bandIndex = 0; bandIndex < rawBands.Count; bandIndex++)
                {
                    var presentation = ErosionBandPresentationDefinition.FromValue(rawBands[bandIndex]);
                    if (string.Equals(presentation.Id, bandId, StringComparison.Ordinal))
                    {
                        return presentation;
                    }
                }
            }

            throw new KeyNotFoundException("Erosion band presentation is missing: " + bandId);
        }

        public TutorialDefinition GetTutorialDefinition()
        {
            return TutorialDefinition.FromValue(GetProgressionConfig().GetObject("tutorial"));
        }

        public IReadOnlyList<ContentItem> GetEnemiesForFloor(string floorId)
        {
            var results = new List<ContentItem>();
            foreach (var enemy in database.FindByType("enemy"))
            {
                if (string.Equals(enemy.GetString("floorId"), floorId, StringComparison.Ordinal))
                {
                    results.Add(enemy);
                }
            }

            results.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return results;
        }

        /// <summary>
        /// Returns the content-owned enemy pool that can be instantiated at regular
        /// spawn points. Tutorial targets live in the same floor data but are created
        /// only by the tutorial flow, so they are deliberately excluded here.
        /// </summary>
        public IReadOnlyList<ContentItem> GetSpawnableEnemiesForFloor(string floorId)
        {
            var results = new List<ContentItem>();
            foreach (var enemy in GetEnemiesForFloor(floorId))
            {
                if (!enemy.GetBool("isTutorialTarget"))
                {
                    results.Add(enemy);
                }
            }

            return results;
        }

        public IReadOnlyList<ContentItem> GetMaterialsBySource(string source)
        {
            var results = new List<ContentItem>();
            foreach (var material in database.FindByType("material"))
            {
                if (string.Equals(material.GetString("source"), source, StringComparison.Ordinal))
                {
                    results.Add(material);
                }
            }

            return results;
        }

        public IReadOnlyList<DropEntry> GetDropEntries(string dropTableId)
        {
            var table = Require(dropTableId);
            var results = new List<DropEntry>();
            var rawEntries = table.GetArray("entries");
            if (rawEntries == null)
            {
                return results;
            }

            for (var entryIndex = 0; entryIndex < rawEntries.Count; entryIndex++)
            {
                var rawEntry = ContentValues.AsObject(rawEntries[entryIndex]);
                if (rawEntry != null)
                {
                    results.Add(new DropEntry(
                        ContentValues.GetString(rawEntry, "materialId"),
                        ContentValues.GetNumber(rawEntry, "weight"),
                        ContentValues.GetString(rawEntry, "upgradedMaterialId")));
                }
            }

            return results;
        }

        public int GetMaterialStackLimit(string materialId)
        {
            return (int)Require(materialId).GetNumber("stackLimit");
        }

        public int GetMaterialGrade(string materialId)
        {
            return (int)Require(materialId).GetNumber("grade");
        }

        public int GetMaterialBasePrice(string materialId)
        {
            return (int)Require(materialId).GetNumber("basePrice");
        }

        public IReadOnlyList<RecipeDefinition> GetRecipes()
        {
            var recipes = new List<RecipeDefinition>();
            foreach (var recipe in database.FindByType("recipe"))
            {
                recipes.Add(RecipeDefinition.FromContent(recipe, GetMaterialGrade));
            }

            return recipes;
        }

        public IReadOnlyList<UpgradeDefinition> GetUpgrades()
        {
            var upgrades = new List<UpgradeDefinition>();
            foreach (var upgrade in database.FindByType("upgrade"))
            {
                upgrades.Add(UpgradeDefinition.FromContent(upgrade));
            }

            upgrades.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return upgrades;
        }

        public IReadOnlyList<CgThreshold> GetCgThresholds()
        {
            var thresholds = new List<CgThreshold>();
            foreach (var cg in database.FindByType("cg"))
            {
                thresholds.Add(new CgThreshold(cg.Id, (int)cg.GetNumber("requiredAffinity")));
            }

            return thresholds;
        }

        public IReadOnlyList<StoryEventDefinition> GetStoryEvents()
        {
            var events = new List<StoryEventDefinition>();
            foreach (var storyEvent in database.FindByType("story_event"))
            {
                events.Add(StoryEventDefinition.FromContent(storyEvent));
            }

            return events;
        }

        public IReadOnlyList<ContentItem> GetEndings()
        {
            return FindByType("ending");
        }

        public PricingSettings CreatePricingSettings()
        {
            var config = GetEconomyConfig();
            return new PricingSettings(
                config.GetNumber("rawSaleRatio"),
                ReadNumbers(config.GetArray("tierThresholds")),
                ReadNumbers(config.GetArray("tierMultipliers")));
        }

        public UpgradeEffectRegistry CreateUpgradeEffectRegistry()
        {
            var config = GetProgressionConfig();
            return new UpgradeEffectRegistry(new Dictionary<string, double>
            {
                { "backpack_slots", config.GetNumber("baseBackpackSlots") },
                { "erosion_decay_rate", config.GetNumber("baseErosionDecayRate") },
                { "weapon_damage", config.GetNumber("baseWeaponDamage") },
                { "weapon_scale", config.GetNumber("baseWeaponScale") },
                { "cauldron_slots", config.GetNumber("baseCauldronSlots") },
                { "refinery_level", config.GetNumber("baseRefineryLevel") }
            });
        }

        public string GetNextFloorId(string floorId)
        {
            return Require(floorId).GetString("nextFloorId");
        }

        private IReadOnlyList<ContentItem> FindByType(string type)
        {
            var items = new List<ContentItem>();
            foreach (var item in database.FindByType(type))
            {
                items.Add(item);
            }

            items.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return items;
        }

        private static List<double> ReadNumbers(IList<object> rawValues)
        {
            var values = new List<double>();
            if (rawValues == null)
            {
                return values;
            }

            for (var valueIndex = 0; valueIndex < rawValues.Count; valueIndex++)
            {
                if (rawValues[valueIndex] is double)
                {
                    values.Add((double)rawValues[valueIndex]);
                }
            }

            return values;
        }
    }
}
