using System.Collections.Generic;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Common;
using HunterWidow.Domain.Cycle;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Erosion;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Progression;
using HunterWidow.Domain.Rng;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class CycleSessionTests
    {
        [Test]
        public void ReturnSettlementMovesLootAdvancesBrewAndThenCountsCycle()
        {
            var state = new ProgressionState();
            var inventory = new ItemLedger();
            inventory.Add("mat_herb", 2);
            var recipe = new RecipeDefinition(
                "rcp_tonic",
                new List<RecipeIngredient> { new RecipeIngredient("mat_herb", 2, 1) },
                "itm_tonic",
                1,
                1,
                100);
            var cauldron = new CauldronLogic(1);
            Assert.That(cauldron.TryStart(recipe, inventory), Is.True);
            var cycle = new CycleSession(state, inventory, cauldron);
            var diveResult = CreateExtractedDiveResult();

            var returnResult = cycle.CompleteReturn(diveResult, null, null);

            Assert.That(state.CycleCount, Is.EqualTo(1));
            Assert.That(inventory.GetCount("mat_forbidden"), Is.EqualTo(1));
            Assert.That(inventory.GetCount("itm_tonic"), Is.EqualTo(1));
            Assert.That(state.HasCrafted("itm_tonic"), Is.True);
            Assert.That(returnResult.CraftedItems.Count, Is.EqualTo(1));
        }

        private static DiveResult CreateExtractedDiveResult()
        {
            var backpack = new BackpackLogic(8);
            var session = new DiveSession(
                new ErosionLogic(new ErosionSettings(100d, 100d, 0d, 60d, 30d)),
                backpack,
                new SeededRng(1u),
                0.5d);
            DiveResult result = null;
            session.Finished += value => result = value;
            session.Start("floor_mountain", new Vec2(0d, 0d));
            session.TryCollect("mat_forbidden", 1, 3);
            session.RequestExtract();
            return result;
        }
    }
}
