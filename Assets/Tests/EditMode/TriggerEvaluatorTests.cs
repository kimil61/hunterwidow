using System.Collections.Generic;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Progression;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class TriggerEvaluatorTests
    {
        [Test]
        public void EverySupportedConditionUsesAndSemantics()
        {
            var state = BuildState();
            var conditions = new List<TriggerCondition>
            {
                new TriggerCondition("affinity_at_least", "", 2d),
                new TriggerCondition("affinity_below", "", 3d),
                new TriggerCondition("cycle_at_least", "", 4d),
                new TriggerCondition("floor_first_reached", "floor_mountain", 0d),
                new TriggerCondition("recipe_unlocked", "rcp_tonic", 0d),
                new TriggerCondition("item_crafted", "itm_tonic", 0d),
                new TriggerCondition("upgrade_at_least", "backpack", 1d),
                new TriggerCondition("flag_set", "flag_seen", 0d),
                new TriggerCondition("flag_not_set", "flag_absent", 0d),
                new TriggerCondition("gold_at_least", "", 10d)
            };

            Assert.That(TriggerEvaluator.EvaluateAll(conditions, state), Is.True);
            conditions.Add(new TriggerCondition("gold_at_least", "", 11d));
            Assert.That(TriggerEvaluator.EvaluateAll(conditions, state), Is.False);
        }

        [Test]
        public void EverySupportedEffectMutatesOnlyTheTargetState()
        {
            var state = new ProgressionState();
            var inventory = new ItemLedger();
            var affinity = new AffinityLogic(new List<CgThreshold>());
            var context = new NarrativeExecutionContext(state, inventory, affinity);
            var effects = new[]
            {
                new NarrativeEffect("set_flag", "flag_seen", 0d),
                new NarrativeEffect("clear_flag", "flag_seen", 0d),
                new NarrativeEffect("add_affinity", "", 2d),
                new NarrativeEffect("unlock_recipe", "rcp_tonic", 0d),
                new NarrativeEffect("unlock_cg", "cg_first", 0d),
                new NarrativeEffect("give_material", "mat_herb", 3d),
                new NarrativeEffect("give_gold", "", 20d),
                new NarrativeEffect("unlock_floor", "floor_mountain", 0d),
                new NarrativeEffect("trigger_ending", "end_hope", 0d)
            };

            for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
            {
                TriggerEvaluator.Apply(effects[effectIndex], context);
            }

            Assert.That(state.HasFlag("flag_seen"), Is.False);
            Assert.That(state.Affinity, Is.EqualTo(2));
            Assert.That(state.HasRecipe("rcp_tonic"), Is.True);
            Assert.That(state.HasCg("cg_first"), Is.True);
            Assert.That(inventory.GetCount("mat_herb"), Is.EqualTo(3));
            Assert.That(state.Gold, Is.EqualTo(20));
            Assert.That(state.HasFloor("floor_mountain"), Is.True);
            Assert.That(state.TriggeredEndingId, Is.EqualTo("end_hope"));
        }

        [Test]
        public void StoryDirectorFiresEligibleEventsOnceInPriorityOrder()
        {
            var state = BuildState();
            var context = new NarrativeExecutionContext(state, new ItemLedger(), new AffinityLogic(new List<CgThreshold>()));
            var director = new StoryDirector(new List<StoryEventDefinition>
            {
                new StoryEventDefinition("evt_low", 1, new List<TriggerCondition>(), new List<NarrativeEffect>()),
                new StoryEventDefinition("evt_high", 9, new List<TriggerCondition>(), new List<NarrativeEffect>())
            });

            var firstReturn = director.EvaluateTownReturn(context);
            var secondReturn = director.EvaluateTownReturn(context);

            Assert.That(firstReturn.Count, Is.EqualTo(2));
            Assert.That(firstReturn[0].Id, Is.EqualTo("evt_high"));
            Assert.That(firstReturn[1].Id, Is.EqualTo("evt_low"));
            Assert.That(secondReturn, Is.Empty);
        }

        private static ProgressionState BuildState()
        {
            var state = new ProgressionState();
            state.AddGold(10);
            state.AddAffinity(2);
            state.SetCycleCount(4);
            state.UnlockFloor("floor_mountain");
            state.UnlockRecipe("rcp_tonic");
            state.MarkCrafted("itm_tonic");
            state.SetUpgradeLevel("backpack", 1);
            state.SetFlag("flag_seen");
            return state;
        }
    }
}
