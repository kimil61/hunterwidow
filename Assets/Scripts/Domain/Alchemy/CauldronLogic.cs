using System;
using System.Collections.Generic;
using HunterWidow.Domain.Inventory;

namespace HunterWidow.Domain.Alchemy
{
    public sealed class CauldronJob
    {
        internal CauldronJob(RecipeDefinition recipe, int remainingCycles)
        {
            Recipe = recipe;
            RemainingCycles = remainingCycles;
        }

        public RecipeDefinition Recipe { get; }

        public int RemainingCycles { get; internal set; }
    }

    public sealed class CraftedItem
    {
        public CraftedItem(string recipeId, string itemId, int count)
        {
            RecipeId = recipeId;
            ItemId = itemId;
            Count = count;
        }

        public string RecipeId { get; }

        public string ItemId { get; }

        public int Count { get; }
    }

    public sealed class CauldronJobState
    {
        public CauldronJobState(string recipeId, int remainingCycles)
        {
            RecipeId = recipeId;
            RemainingCycles = remainingCycles;
        }

        public string RecipeId { get; }

        public int RemainingCycles { get; }
    }

    public sealed class CauldronLogic
    {
        private readonly List<CauldronJob> jobs = new List<CauldronJob>();
        private int slotCapacity;

        public CauldronLogic(int slotCapacity)
        {
            if (slotCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCapacity));
            }

            this.slotCapacity = slotCapacity;
        }

        public int SlotCapacity => slotCapacity;

        public IReadOnlyList<CauldronJob> GetState()
        {
            var copies = new List<CauldronJob>();
            for (var jobIndex = 0; jobIndex < jobs.Count; jobIndex++)
            {
                copies.Add(new CauldronJob(jobs[jobIndex].Recipe, jobs[jobIndex].RemainingCycles));
            }

            return copies;
        }

        public IReadOnlyList<CauldronJobState> GetJobStates()
        {
            var states = new List<CauldronJobState>();
            for (var jobIndex = 0; jobIndex < jobs.Count; jobIndex++)
            {
                states.Add(new CauldronJobState(jobs[jobIndex].Recipe.Id, jobs[jobIndex].RemainingCycles));
            }

            return states;
        }

        public void SetSlotCapacity(int newCapacity)
        {
            if (newCapacity < jobs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newCapacity));
            }

            slotCapacity = newCapacity;
        }

        public void RestoreJobs(IReadOnlyList<CauldronJobState> states, Func<string, RecipeDefinition> recipeResolver)
        {
            if (recipeResolver == null)
            {
                throw new ArgumentNullException(nameof(recipeResolver));
            }

            jobs.Clear();
            if (states == null)
            {
                return;
            }

            for (var stateIndex = 0; stateIndex < states.Count && jobs.Count < slotCapacity; stateIndex++)
            {
                var state = states[stateIndex];
                if (state == null || state.RemainingCycles <= 0)
                {
                    continue;
                }

                var recipe = recipeResolver(state.RecipeId);
                if (recipe != null)
                {
                    jobs.Add(new CauldronJob(recipe, state.RemainingCycles));
                }
            }
        }

        public bool TryStart(RecipeDefinition recipe, ItemLedger inventory)
        {
            if (recipe == null || inventory == null || jobs.Count >= slotCapacity)
            {
                return false;
            }

            if (!RecipeLogic.TryConsumeIngredients(recipe, inventory))
            {
                return false;
            }

            jobs.Add(new CauldronJob(recipe, recipe.DurationCycles));
            return true;
        }

        public IReadOnlyList<CraftedItem> AdvanceDive(ItemLedger inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            var crafted = new List<CraftedItem>();
            for (var jobIndex = jobs.Count - 1; jobIndex >= 0; jobIndex--)
            {
                var job = jobs[jobIndex];
                job.RemainingCycles--;
                if (job.RemainingCycles > 0)
                {
                    continue;
                }

                inventory.Add(job.Recipe.OutputItemId, job.Recipe.OutputCount);
                crafted.Add(new CraftedItem(job.Recipe.Id, job.Recipe.OutputItemId, job.Recipe.OutputCount));
                jobs.RemoveAt(jobIndex);
            }

            crafted.Reverse();
            return crafted;
        }
    }
}
