using System.Collections.Generic;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Inventory;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class RecipeAndCauldronTests
    {
        [Test]
        public void PreviewListsMissingIngredientsWithoutConsumingAnything()
        {
            var recipe = CreateRecipe();
            var inventory = new ItemLedger();
            inventory.Add("mat_herb", 1);

            var preview = RecipeLogic.Preview(recipe, inventory);

            Assert.That(preview.CanStart, Is.False);
            Assert.That(preview.MissingIngredients[0].Id, Is.EqualTo("mat_herb"));
            Assert.That(preview.MissingIngredients[0].Count, Is.EqualTo(1));
            Assert.That(inventory.GetCount("mat_herb"), Is.EqualTo(1));
        }

        [Test]
        public void CauldronUsesDiveCountsRatherThanRealtime()
        {
            var recipe = CreateRecipe();
            var inventory = new ItemLedger();
            inventory.Add("mat_herb", 2);
            var cauldron = new CauldronLogic(1);

            Assert.That(cauldron.TryStart(recipe, inventory), Is.True);
            Assert.That(inventory.GetCount("mat_herb"), Is.EqualTo(0));
            Assert.That(cauldron.AdvanceDive(inventory), Is.Empty);
            Assert.That(cauldron.GetState()[0].RemainingCycles, Is.EqualTo(1));

            var crafted = cauldron.AdvanceDive(inventory);

            Assert.That(crafted.Count, Is.EqualTo(1));
            Assert.That(crafted[0].ItemId, Is.EqualTo("itm_tonic"));
            Assert.That(inventory.GetCount("itm_tonic"), Is.EqualTo(1));
            Assert.That(cauldron.GetState(), Is.Empty);
        }

        [Test]
        public void CauldronRestoresOnlyKnownUnfinishedJobs()
        {
            var recipe = CreateRecipe();
            var cauldron = new CauldronLogic(1);

            cauldron.RestoreJobs(
                new[]
                {
                    new CauldronJobState("rcp_tonic", 1),
                    new CauldronJobState("rcp_missing", 2)
                },
                recipeId => recipeId == recipe.Id ? recipe : null);

            Assert.That(cauldron.GetState().Count, Is.EqualTo(1));
            Assert.That(cauldron.GetState()[0].Recipe.Id, Is.EqualTo("rcp_tonic"));
            Assert.That(cauldron.GetState()[0].RemainingCycles, Is.EqualTo(1));
        }

        private static RecipeDefinition CreateRecipe()
        {
            return new RecipeDefinition(
                "rcp_tonic",
                new List<RecipeIngredient> { new RecipeIngredient("mat_herb", 2, 1) },
                "itm_tonic",
                1,
                2,
                100);
        }
    }
}
