using System;
using System.Collections.Generic;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Economy;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Domain.Cycle
{
    public sealed class CycleReturnResult
    {
        public CycleReturnResult(IReadOnlyList<CraftedItem> craftedItems, IReadOnlyList<StoryEventDefinition> firedEvents)
        {
            CraftedItems = craftedItems;
            FiredEvents = firedEvents;
        }

        public IReadOnlyList<CraftedItem> CraftedItems { get; }

        public IReadOnlyList<StoryEventDefinition> FiredEvents { get; }
    }

    public sealed class CycleSession
    {
        private readonly ProgressionState progression;
        private readonly ItemLedger townInventory;
        private readonly CauldronLogic cauldron;

        public CycleSession(ProgressionState progression, ItemLedger townInventory, CauldronLogic cauldron)
        {
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.townInventory = townInventory ?? throw new ArgumentNullException(nameof(townInventory));
            this.cauldron = cauldron ?? throw new ArgumentNullException(nameof(cauldron));
        }

        public ProgressionState Progression => progression;

        public ItemLedger TownInventory => townInventory;

        public CauldronLogic Cauldron => cauldron;

        public CycleReturnResult CompleteReturn(DiveResult diveResult, StoryDirector storyDirector, NarrativeExecutionContext narrativeContext)
        {
            if (diveResult == null)
            {
                throw new ArgumentNullException(nameof(diveResult));
            }

            var slots = diveResult.Backpack.Slots;
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                townInventory.Add(slots[slotIndex].ItemId, slots[slotIndex].Count);
            }

            progression.AdvanceCycle();
            var crafted = cauldron.AdvanceDive(townInventory);
            for (var craftedIndex = 0; craftedIndex < crafted.Count; craftedIndex++)
            {
                progression.MarkCrafted(crafted[craftedIndex].ItemId);
            }

            var events = storyDirector == null || narrativeContext == null
                ? new List<StoryEventDefinition>()
                : storyDirector.EvaluateTownReturn(narrativeContext);
            return new CycleReturnResult(crafted, events);
        }

        public bool TryStartRecipe(RecipeDefinition recipe)
        {
            return cauldron.TryStart(recipe, townInventory);
        }

        public bool TrySellRaw(string itemId, int count, int basePrice, PricingSettings settings, out PriceQuote quote)
        {
            quote = PricingLogic.QuoteRaw(basePrice, settings);
            if (!townInventory.TryRemove(itemId, count))
            {
                return false;
            }

            progression.AddGold(quote.Price * count);
            return true;
        }

        public bool TrySellProcessed(string itemId, int count, RecipeDefinition recipe, double refineryBonus, PricingSettings settings, out PriceQuote quote)
        {
            var grades = new List<int>();
            for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
            {
                grades.Add(recipe.Ingredients[ingredientIndex].Grade);
            }

            quote = PricingLogic.QuoteProcessed(recipe.BasePrice, grades, refineryBonus, settings);
            if (!townInventory.TryRemove(itemId, count))
            {
                return false;
            }

            progression.AddGold(quote.Price * count);
            return true;
        }
    }
}
