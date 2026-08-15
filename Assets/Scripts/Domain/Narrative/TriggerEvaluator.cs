using System;
using System.Collections.Generic;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Progression;

namespace HunterWidow.Domain.Narrative
{
    public sealed class TriggerCondition
    {
        public TriggerCondition(string type, string id, double value)
        {
            Type = type;
            Id = id;
            Value = value;
        }

        public string Type { get; }

        public string Id { get; }

        public double Value { get; }

        public static TriggerCondition FromContent(IDictionary<string, object> values)
        {
            return new TriggerCondition(
                ContentValues.GetString(values, "type"),
                ContentValues.GetString(values, "id"),
                ContentValues.GetNumber(values, "value"));
        }
    }

    public sealed class NarrativeEffect
    {
        public NarrativeEffect(string type, string id, double value)
        {
            Type = type;
            Id = id;
            Value = value;
        }

        public string Type { get; }

        public string Id { get; }

        public double Value { get; }

        public static NarrativeEffect FromContent(IDictionary<string, object> values)
        {
            return new NarrativeEffect(
                ContentValues.GetString(values, "type"),
                ContentValues.GetString(values, "id"),
                ContentValues.GetNumber(values, "value"));
        }
    }

    public sealed class StoryEventDefinition
    {
        public StoryEventDefinition(string id, int priority, IReadOnlyList<TriggerCondition> conditions, IReadOnlyList<NarrativeEffect> effects)
        {
            Id = id;
            Priority = priority;
            Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public string Id { get; }

        public int Priority { get; }

        public IReadOnlyList<TriggerCondition> Conditions { get; }

        public IReadOnlyList<NarrativeEffect> Effects { get; }

        public static StoryEventDefinition FromContent(ContentItem item)
        {
            return new StoryEventDefinition(
                item.Id,
                (int)item.GetNumber("priority"),
                ReadConditions(item.GetArray("conditions")),
                ReadEffects(item.GetArray("effects")));
        }

        private static IReadOnlyList<TriggerCondition> ReadConditions(IList<object> rawConditions)
        {
            var conditions = new List<TriggerCondition>();
            if (rawConditions == null)
            {
                return conditions;
            }

            for (var conditionIndex = 0; conditionIndex < rawConditions.Count; conditionIndex++)
            {
                var rawCondition = ContentValues.AsObject(rawConditions[conditionIndex]);
                if (rawCondition != null)
                {
                    conditions.Add(TriggerCondition.FromContent(rawCondition));
                }
            }

            return conditions;
        }

        private static IReadOnlyList<NarrativeEffect> ReadEffects(IList<object> rawEffects)
        {
            var effects = new List<NarrativeEffect>();
            if (rawEffects == null)
            {
                return effects;
            }

            for (var effectIndex = 0; effectIndex < rawEffects.Count; effectIndex++)
            {
                var rawEffect = ContentValues.AsObject(rawEffects[effectIndex]);
                if (rawEffect != null)
                {
                    effects.Add(NarrativeEffect.FromContent(rawEffect));
                }
            }

            return effects;
        }
    }

    public sealed class NarrativeExecutionContext
    {
        public NarrativeExecutionContext(ProgressionState state, ItemLedger inventory, AffinityLogic affinity)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Affinity = affinity;
        }

        public ProgressionState State { get; }

        public ItemLedger Inventory { get; }

        public AffinityLogic Affinity { get; }
    }

    public static class TriggerEvaluator
    {
        public static bool EvaluateAll(IReadOnlyList<TriggerCondition> conditions, ProgressionState state)
        {
            if (conditions == null || state == null)
            {
                return false;
            }

            for (var conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                if (!Evaluate(conditions[conditionIndex], state))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Evaluate(TriggerCondition condition, ProgressionState state)
        {
            if (condition == null || state == null)
            {
                return false;
            }

            switch (condition.Type)
            {
                case "affinity_at_least": return state.Affinity >= condition.Value;
                case "affinity_below": return state.Affinity < condition.Value;
                case "cycle_at_least": return state.CycleCount >= condition.Value;
                case "floor_first_reached": return state.HasFloor(condition.Id);
                case "recipe_unlocked": return state.HasRecipe(condition.Id);
                case "item_crafted": return state.HasCrafted(condition.Id);
                case "upgrade_at_least": return state.GetUpgradeLevel(condition.Id) >= condition.Value;
                case "flag_set": return state.HasFlag(condition.Id);
                case "flag_not_set": return !state.HasFlag(condition.Id);
                case "gold_at_least": return state.Gold >= condition.Value;
                default: return false;
            }
        }

        public static void Apply(NarrativeEffect effect, NarrativeExecutionContext context)
        {
            if (effect == null || context == null)
            {
                return;
            }

            switch (effect.Type)
            {
                case "set_flag":
                    context.State.SetFlag(effect.Id);
                    break;
                case "clear_flag":
                    context.State.ClearFlag(effect.Id);
                    break;
                case "add_affinity":
                    context.State.AddAffinity((int)effect.Value);
                    if (context.Affinity != null)
                    {
                        var unlockedCgs = context.Affinity.Add((int)effect.Value);
                        for (var cgIndex = 0; cgIndex < unlockedCgs.Count; cgIndex++)
                        {
                            context.State.UnlockCg(unlockedCgs[cgIndex]);
                        }
                    }
                    break;
                case "unlock_recipe":
                    context.State.UnlockRecipe(effect.Id);
                    break;
                case "unlock_cg":
                    context.State.UnlockCg(effect.Id);
                    break;
                case "give_material":
                    context.Inventory.Add(effect.Id, (int)effect.Value);
                    break;
                case "give_gold":
                    context.State.AddGold((int)effect.Value);
                    break;
                case "unlock_floor":
                    context.State.UnlockFloor(effect.Id);
                    break;
                case "trigger_ending":
                    context.State.TriggerEnding(effect.Id);
                    break;
                default:
                    throw new InvalidOperationException("Narrative effect type is not registered.");
            }
        }
    }
}
