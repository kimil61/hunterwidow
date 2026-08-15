using System;
using System.Collections.Generic;
using System.Globalization;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Cycle;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Economy;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Tools.EconomySim
{
    internal static class Program
    {
        private static int Main(string[] arguments)
        {
            string contentPath;
            int cycles;
            if (!TryParseArguments(arguments, out contentPath, out cycles))
            {
                Console.Error.WriteLine("Usage: dotnet run --project Tools/EconomySim -- <content-path> --cycles <count>");
                return 2;
            }

            var content = ContentLoader.Load(contentPath);
            if (content.Report.HasErrors)
            {
                Console.Error.WriteLine(content.Report.ToMultilineText());
                return 1;
            }

            var simulation = new Simulator(content.Database);
            Console.WriteLine("cycle,gold,unlocked_recipes,upgrade_levels,crafted_items");
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                var state = simulation.Advance(cycle);
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4}",
                    cycle,
                    state.Gold,
                    state.UnlockedRecipes,
                    state.UpgradeLevels,
                    state.CraftedItems));
            }

            return 0;
        }

        private static bool TryParseArguments(string[] arguments, out string contentPath, out int cycles)
        {
            contentPath = null;
            cycles = 0;
            for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            {
                var argument = arguments[argumentIndex];
                if (string.Equals(argument, "--cycles", StringComparison.Ordinal) && argumentIndex + 1 < arguments.Length)
                {
                    int.TryParse(arguments[++argumentIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out cycles);
                }
                else if (!argument.StartsWith("--", StringComparison.Ordinal) && contentPath == null)
                {
                    contentPath = argument;
                }
            }

            return !string.IsNullOrEmpty(contentPath) && cycles > 0;
        }

        private sealed class Simulator
        {
            private readonly ContentDatabase database;
            private readonly GameContentCatalog catalog;
            private readonly string startingFloorId;
            private readonly CycleSession cycle;
            private readonly PricingSettings pricing;
            private readonly Dictionary<string, RecipeDefinition> recipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            private readonly List<string> recipePriorityIds = new List<string>();
            private readonly List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
            private readonly UpgradeLogic upgradeLogic = new UpgradeLogic();
            private readonly UpgradeEffectRegistry effects;
            private readonly List<Gain> gains = new List<Gain>();

            public Simulator(ContentDatabase database)
            {
                this.database = database;
                catalog = new GameContentCatalog(database);
                var economyConfig = catalog.GetEconomyConfig();
                var progressionConfig = catalog.GetProgressionConfig();
                var simulationConfig = catalog.GetSimulationConfig();
                startingFloorId = progressionConfig.GetString("startingFloorId");
                pricing = new PricingSettings(
                    economyConfig.GetNumber("rawSaleRatio"),
                    ReadNumbers(economyConfig.GetArray("tierThresholds")),
                    ReadNumbers(economyConfig.GetArray("tierMultipliers")));
                var progression = new ProgressionState();
                var inventory = new ItemLedger();
                cycle = new CycleSession(
                    progression,
                    inventory,
                    new CauldronLogic((int)progressionConfig.GetNumber("baseCauldronSlots")));
                effects = new UpgradeEffectRegistry(new Dictionary<string, double>
                {
                    { "backpack_slots", progressionConfig.GetNumber("baseBackpackSlots") },
                    { "erosion_decay_rate", progressionConfig.GetNumber("baseErosionDecayRate") },
                    { "weapon_damage", progressionConfig.GetNumber("baseWeaponDamage") },
                    { "weapon_scale", progressionConfig.GetNumber("baseWeaponScale") },
                    { "cauldron_slots", progressionConfig.GetNumber("baseCauldronSlots") },
                    { "refinery_level", progressionConfig.GetNumber("baseRefineryLevel") }
                });
                ReadRecipes(progression);
                ReadGains(simulationConfig);
                ReadRecipePriority(simulationConfig);
                ReadUpgrades();
            }

            public SimulationRow Advance(int cycleNumber)
            {
                var slots = new List<BackpackSlot>();
                for (var gainIndex = 0; gainIndex < gains.Count; gainIndex++)
                {
                    var gain = gains[gainIndex];
                    if (cycleNumber >= gain.StartCycle)
                    {
                        slots.Add(new BackpackSlot(gain.ItemId, gain.Count, gain.Count));
                    }
                }

                var dive = new DiveResult(
                    DiveEndReason.Extracted,
                    startingFloorId,
                    new BackpackSnapshot(slots.Count, slots),
                    new BackpackLossResult(new Dictionary<string, int>(), 0));
                var returned = cycle.CompleteReturn(dive, null, null);
                for (var craftedIndex = 0; craftedIndex < returned.CraftedItems.Count; craftedIndex++)
                {
                    var crafted = returned.CraftedItems[craftedIndex];
                    RecipeDefinition recipe;
                    if (recipes.TryGetValue(crafted.RecipeId, out recipe))
                    {
                        PriceQuote ignored;
                        cycle.TrySellProcessed(crafted.ItemId, crafted.Count, recipe, effects.GetValue("refinery_level"), pricing, out ignored);
                    }
                }

                for (var recipeIndex = 0; recipeIndex < recipePriorityIds.Count; recipeIndex++)
                {
                    RecipeDefinition recipe;
                    if (!recipes.TryGetValue(recipePriorityIds[recipeIndex], out recipe))
                    {
                        continue;
                    }

                    while (cycle.Cauldron.GetState().Count < cycle.Cauldron.SlotCapacity && cycle.TryStartRecipe(recipe))
                    {
                    }
                }

                for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
                {
                    upgradeLogic.TryPurchase(upgrades[upgradeIndex], cycle.Progression, effects);
                }

                return new SimulationRow(
                    cycle.Progression.Gold,
                    cycle.Progression.UnlockedRecipes.Count,
                    cycle.Progression.GetUpgradeLevels().Count,
                    cycle.Progression.CraftedItems.Count);
            }

            private void ReadRecipes(ProgressionState progression)
            {
                foreach (var item in database.FindByType("recipe"))
                {
                    var recipe = RecipeDefinition.FromContent(item, ResolveMaterialGrade);
                    recipes.Add(recipe.Id, recipe);
                    progression.UnlockRecipe(recipe.Id);
                }
            }

            private void ReadGains(ContentItem simulationConfig)
            {
                var rawGains = simulationConfig.GetArray("gains");
                if (rawGains == null)
                {
                    return;
                }

                for (var gainIndex = 0; gainIndex < rawGains.Count; gainIndex++)
                {
                    var rawGain = ContentValues.AsObject(rawGains[gainIndex]);
                    if (rawGain != null)
                    {
                        gains.Add(new Gain(
                            ContentValues.GetString(rawGain, "id"),
                            (int)ContentValues.GetNumber(rawGain, "count"),
                            (int)ContentValues.GetNumber(rawGain, "startCycle")));
                    }
                }
            }

            private void ReadRecipePriority(ContentItem simulationConfig)
            {
                var rawIds = simulationConfig.GetArray("recipePriorityIds");
                if (rawIds == null)
                {
                    return;
                }

                for (var idIndex = 0; idIndex < rawIds.Count; idIndex++)
                {
                    var id = rawIds[idIndex] as string;
                    if (!string.IsNullOrEmpty(id))
                    {
                        recipePriorityIds.Add(id);
                    }
                }
            }

            private void ReadUpgrades()
            {
                foreach (var item in database.FindByType("upgrade"))
                {
                    upgrades.Add(UpgradeDefinition.FromContent(item));
                }

                upgrades.Sort((left, right) =>
                {
                    var costComparison = left.Cost.CompareTo(right.Cost);
                    return costComparison != 0 ? costComparison : string.CompareOrdinal(left.Id, right.Id);
                });
            }

            private int ResolveMaterialGrade(string materialId)
            {
                ContentItem material;
                return database.TryGet(materialId, out material) ? (int)material.GetNumber("grade") : 0;
            }

            private static List<double> ReadNumbers(IList<object> values)
            {
                var numbers = new List<double>();
                if (values == null)
                {
                    return numbers;
                }

                for (var index = 0; index < values.Count; index++)
                {
                    if (values[index] is double)
                    {
                        numbers.Add((double)values[index]);
                    }
                }

                return numbers;
            }
        }

        private sealed class Gain
        {
            public Gain(string itemId, int count, int startCycle)
            {
                ItemId = itemId;
                Count = count;
                StartCycle = startCycle;
            }

            public string ItemId { get; }

            public int Count { get; }

            public int StartCycle { get; }
        }

        private sealed class SimulationRow
        {
            public SimulationRow(int gold, int unlockedRecipes, int upgradeLevels, int craftedItems)
            {
                Gold = gold;
                UnlockedRecipes = unlockedRecipes;
                UpgradeLevels = upgradeLevels;
                CraftedItems = craftedItems;
            }

            public int Gold { get; }

            public int UnlockedRecipes { get; }

            public int UpgradeLevels { get; }

            public int CraftedItems { get; }
        }
    }
}
