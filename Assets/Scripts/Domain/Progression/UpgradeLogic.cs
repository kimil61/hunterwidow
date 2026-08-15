using System;
using System.Collections.Generic;
using HunterWidow.Domain.Content;

namespace HunterWidow.Domain.Progression
{
    public enum UpgradeOperation
    {
        Set,
        Add,
        Multiply
    }

    public enum UpgradePurchaseState
    {
        AlreadyPurchased,
        Available,
        RequiresPreviousLevel,
        InsufficientGold
    }

    public sealed class UpgradeEffect
    {
        public UpgradeEffect(string key, UpgradeOperation operation, double value)
        {
            Key = key;
            Operation = operation;
            Value = value;
        }

        public string Key { get; }

        public UpgradeOperation Operation { get; }

        public double Value { get; }
    }

    public sealed class UpgradeDefinition
    {
        public UpgradeDefinition(string id, string axisId, int level, int cost, IReadOnlyList<UpgradeEffect> effects)
        {
            Id = id;
            AxisId = axisId;
            Level = level;
            Cost = cost;
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public string Id { get; }

        public string AxisId { get; }

        public int Level { get; }

        public int Cost { get; }

        public IReadOnlyList<UpgradeEffect> Effects { get; }

        public static UpgradeDefinition FromContent(ContentItem item)
        {
            var effects = new List<UpgradeEffect>();
            var rawEffects = item.GetArray("effects");
            if (rawEffects != null)
            {
                for (var effectIndex = 0; effectIndex < rawEffects.Count; effectIndex++)
                {
                    var raw = ContentValues.AsObject(rawEffects[effectIndex]);
                    if (raw == null)
                    {
                        throw new InvalidOperationException("Upgrade effect is invalid.");
                    }

                    effects.Add(new UpgradeEffect(
                        ContentValues.GetString(raw, "key"),
                        ParseOperation(ContentValues.GetString(raw, "operation")),
                        ContentValues.GetNumber(raw, "value")));
                }
            }
            else
            {
                effects.Add(new UpgradeEffect(item.GetString("effectKey"), ParseOperation(item.GetString("operation")), item.GetNumber("value")));
            }

            return new UpgradeDefinition(
                item.Id,
                item.GetString("axisId"),
                (int)item.GetNumber("level"),
                (int)item.GetNumber("cost"),
                effects);
        }

        internal static UpgradeOperation ParseOperation(string value)
        {
            switch (value)
            {
                case "set": return UpgradeOperation.Set;
                case "add": return UpgradeOperation.Add;
                case "mul": return UpgradeOperation.Multiply;
                default: throw new InvalidOperationException("Upgrade operation is not registered.");
            }
        }
    }

    public sealed class UpgradeEffectRegistry
    {
        private readonly Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.Ordinal);

        public UpgradeEffectRegistry(IReadOnlyDictionary<string, double> initialValues)
        {
            if (initialValues == null)
            {
                throw new ArgumentNullException(nameof(initialValues));
            }

            foreach (var pair in initialValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        public double GetValue(string key)
        {
            double value;
            if (!values.TryGetValue(key, out value))
            {
                throw new KeyNotFoundException("Effect key is not registered: " + key);
            }

            return value;
        }

        public void Apply(UpgradeEffect effect)
        {
            var current = GetValue(effect.Key);
            switch (effect.Operation)
            {
                case UpgradeOperation.Set:
                    values[effect.Key] = effect.Value;
                    break;
                case UpgradeOperation.Add:
                    values[effect.Key] = current + effect.Value;
                    break;
                case UpgradeOperation.Multiply:
                    values[effect.Key] = current * effect.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public sealed class UpgradeLogic
    {
        private readonly HashSet<string> purchasedIds = new HashSet<string>(StringComparer.Ordinal);

        public UpgradePurchaseState GetPurchaseState(UpgradeDefinition definition, ProgressionState state)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var currentLevel = state.GetUpgradeLevel(definition.AxisId);
            if (currentLevel >= definition.Level)
            {
                return UpgradePurchaseState.AlreadyPurchased;
            }

            if (currentLevel + 1 != definition.Level)
            {
                return UpgradePurchaseState.RequiresPreviousLevel;
            }

            return state.Gold >= definition.Cost
                ? UpgradePurchaseState.Available
                : UpgradePurchaseState.InsufficientGold;
        }

        public bool TryPurchase(UpgradeDefinition definition, ProgressionState state, UpgradeEffectRegistry registry)
        {
            if (definition == null || state == null || registry == null || purchasedIds.Contains(definition.Id))
            {
                return false;
            }

            if (GetPurchaseState(definition, state) != UpgradePurchaseState.Available || !state.TrySpendGold(definition.Cost))
            {
                return false;
            }

            for (var effectIndex = 0; effectIndex < definition.Effects.Count; effectIndex++)
            {
                registry.Apply(definition.Effects[effectIndex]);
            }

            state.SetUpgradeLevel(definition.AxisId, definition.Level);
            purchasedIds.Add(definition.Id);
            return true;
        }

        public bool RestorePurchase(UpgradeDefinition definition, ProgressionState state, UpgradeEffectRegistry registry)
        {
            if (definition == null || state == null || registry == null || purchasedIds.Contains(definition.Id))
            {
                return false;
            }

            if (state.GetUpgradeLevel(definition.AxisId) < definition.Level)
            {
                return false;
            }

            for (var effectIndex = 0; effectIndex < definition.Effects.Count; effectIndex++)
            {
                registry.Apply(definition.Effects[effectIndex]);
            }

            purchasedIds.Add(definition.Id);
            return true;
        }
    }
}
