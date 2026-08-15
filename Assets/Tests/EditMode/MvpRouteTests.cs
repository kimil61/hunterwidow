using System;
using System.Collections.Generic;
using System.IO;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Cycle;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Economy;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Progression;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class MvpRouteTests
    {
        [Test]
        public void FreshProgressionUsesTheEconomyGateBeforeReachingAnEnding()
        {
            var content = ContentLoader.Load(GetContentPath());
            Assert.That(content.Report.HasErrors, Is.False, content.Report.ToMultilineText());

            var catalog = new GameContentCatalog(content.Database);
            var progression = new ProgressionState();
            var inventory = new ItemLedger();
            var affinity = new AffinityLogic(catalog.GetCgThresholds());
            var cycle = new CycleSession(
                progression,
                inventory,
                new CauldronLogic((int)catalog.GetProgressionConfig().GetNumber("baseCauldronSlots")));
            var director = new StoryDirector(catalog.GetStoryEvents());
            var context = new NarrativeExecutionContext(progression, inventory, affinity);
            var upgrades = new UpgradeLogic();
            var effects = catalog.CreateUpgradeEffectRegistry();
            var recipes = catalog.GetRecipes();

            var startFloorId = catalog.GetProgressionConfig().GetString("startingFloorId");
            var startRecipeId = catalog.GetProgressionConfig().GetString("startingRecipeId");
            progression.UnlockFloor(startFloorId);
            progression.UnlockRecipe(startRecipeId);

            var mineFloorId = ReachNextFloor(catalog, progression, startFloorId);
            var depthFloorId = catalog.GetNextFloorId(mineFloorId);
            var depthRequirement = catalog.GetFloorAccessRequirement(depthFloorId);
            Assert.That(depthRequirement, Is.Not.Null);
            Assert.That(depthRequirement.IsMet(progression.GetUpgradeLevel(depthRequirement.UpgradeAxisId)), Is.False);

            var tonic = FindRecipe(recipes, startRecipeId);
            var finalRecipe = FindEndingRecipe(recipes, catalog.GetEndings());
            var mountainFinalIngredient = FindFloorReward(catalog, startFloorId, finalRecipe);
            var tonicIngredient = tonic.Ingredients[0];

            cycle.CompleteReturn(
                CreateReturn(
                    mineFloorId,
                    new IdCount(tonicIngredient.MaterialId, tonicIngredient.Amount),
                    mountainFinalIngredient),
                director,
                context);
            Assert.That(cycle.TryStartRecipe(tonic), Is.True);

            var tonicCompletion = cycle.CompleteReturn(CreateReturn(mineFloorId), director, context);
            Assert.That(tonicCompletion.CraftedItems.Count, Is.EqualTo(1));
            PriceQuote tonicSale;
            Assert.That(
                cycle.TrySellProcessed(
                    tonic.OutputItemId,
                    tonic.OutputCount,
                    tonic,
                    effects.GetValue("refinery_level"),
                    catalog.CreatePricingSettings(),
                    out tonicSale),
                Is.True);

            var resistanceUpgrade = FindUpgrade(catalog.GetUpgrades(), depthRequirement.UpgradeAxisId, depthRequirement.MinimumLevel);
            Assert.That(progression.Gold, Is.GreaterThanOrEqualTo(resistanceUpgrade.Cost));
            Assert.That(upgrades.TryPurchase(resistanceUpgrade, progression, effects), Is.True);
            Assert.That(depthRequirement.IsMet(progression.GetUpgradeLevel(depthRequirement.UpgradeAxisId)), Is.True);

            ReachNextFloor(catalog, progression, mineFloorId);
            var depthFinalIngredient = FindFloorReward(catalog, depthFloorId, finalRecipe);
            var depthReturn = cycle.CompleteReturn(CreateReturn(depthFloorId, depthFinalIngredient), director, context);
            Assert.That(progression.HasRecipe(finalRecipe.Id), Is.True);
            Assert.That(depthReturn.FiredEvents.Count, Is.GreaterThan(0));
            Assert.That(cycle.TryStartRecipe(finalRecipe), Is.True);

            for (var cycleIndex = 0; cycleIndex < finalRecipe.DurationCycles; cycleIndex++)
            {
                cycle.CompleteReturn(CreateReturn(depthFloorId), director, context);
            }

            Assert.That(progression.HasCrafted(finalRecipe.OutputItemId), Is.True);
            var endingId = EndingSelector.Select(catalog.GetEndings(), progression);
            Assert.That(endingId, Is.Not.Empty);
            progression.TriggerEnding(endingId);
            Assert.That(progression.TriggeredEndingId, Is.EqualTo(endingId));
        }

        private static string ReachNextFloor(GameContentCatalog catalog, ProgressionState progression, string currentFloorId)
        {
            var nextFloorId = catalog.GetNextFloorId(currentFloorId);
            Assert.That(nextFloorId, Is.Not.Empty);
            var requirement = catalog.GetFloorAccessRequirement(nextFloorId);
            Assert.That(
                requirement == null || requirement.IsMet(progression.GetUpgradeLevel(requirement.UpgradeAxisId)),
                Is.True,
                "The progression gate must be satisfied before a descent unlocks the next floor.");
            progression.UnlockFloor(nextFloorId);
            return nextFloorId;
        }

        private static IdCount FindFloorReward(
            GameContentCatalog catalog,
            string floorId,
            RecipeDefinition recipe)
        {
            var enemies = catalog.GetSpawnableEnemiesForFloor(floorId);
            for (var enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                var drops = catalog.GetDropEntries(enemies[enemyIndex].GetString("dropTableId"));
                for (var dropIndex = 0; dropIndex < drops.Count; dropIndex++)
                {
                    for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
                    {
                        var ingredient = recipe.Ingredients[ingredientIndex];
                        if (string.Equals(drops[dropIndex].MaterialId, ingredient.MaterialId, StringComparison.Ordinal))
                        {
                            return new IdCount(ingredient.MaterialId, ingredient.Amount);
                        }
                    }
                }
            }

            throw new InvalidOperationException("No required final-recipe material drops on floor " + floorId + ".");
        }

        private static RecipeDefinition FindEndingRecipe(
            IReadOnlyList<RecipeDefinition> recipes,
            IReadOnlyList<ContentItem> endings)
        {
            for (var endingIndex = 0; endingIndex < endings.Count; endingIndex++)
            {
                var definition = StoryEventDefinition.FromContent(endings[endingIndex]);
                for (var conditionIndex = 0; conditionIndex < definition.Conditions.Count; conditionIndex++)
                {
                    var condition = definition.Conditions[conditionIndex];
                    if (!string.Equals(condition.Type, "item_crafted", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    for (var recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
                    {
                        if (string.Equals(recipes[recipeIndex].OutputItemId, condition.Id, StringComparison.Ordinal))
                        {
                            return recipes[recipeIndex];
                        }
                    }
                }
            }

            throw new InvalidOperationException("No ending recipe is declared by content.");
        }

        private static UpgradeDefinition FindUpgrade(
            IReadOnlyList<UpgradeDefinition> upgrades,
            string axisId,
            int level)
        {
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                var upgrade = upgrades[upgradeIndex];
                if (string.Equals(upgrade.AxisId, axisId, StringComparison.Ordinal) && upgrade.Level == level)
                {
                    return upgrade;
                }
            }

            throw new InvalidOperationException("The required floor-access upgrade is missing.");
        }

        private static RecipeDefinition FindRecipe(IReadOnlyList<RecipeDefinition> recipes, string recipeId)
        {
            for (var recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
            {
                if (string.Equals(recipes[recipeIndex].Id, recipeId, StringComparison.Ordinal))
                {
                    return recipes[recipeIndex];
                }
            }

            throw new InvalidOperationException("The starting recipe is missing.");
        }

        private static DiveResult CreateReturn(string floorId, params IdCount[] rewards)
        {
            var slots = new List<BackpackSlot>();
            for (var rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
            {
                var reward = rewards[rewardIndex];
                if (reward != null && reward.Count > 0)
                {
                    slots.Add(new BackpackSlot(reward.Id, reward.Count, reward.Count));
                }
            }

            return new DiveResult(
                DiveEndReason.Extracted,
                floorId,
                new BackpackSnapshot(8, slots),
                new BackpackLossResult(new Dictionary<string, int>(), 0));
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
