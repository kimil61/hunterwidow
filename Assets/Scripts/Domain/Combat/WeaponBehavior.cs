using System;
using System.Collections.Generic;
using HunterWidow.Domain.Content;

namespace HunterWidow.Domain.Combat
{
    /// <summary>
    /// A weapon behavior owns both its data schema and the pure combat logic it
    /// assembles. Adding a weapon never expands a controller switch statement.
    /// </summary>
    public interface IWeaponBehavior : IContentBehaviorSchema
    {
        CombatTuning CreateTuning(ContentItem weapon, ContentItem combatConfig);
    }

    public static class WeaponBehaviorRegistry
    {
        private static readonly IWeaponBehavior[] behaviors =
        {
            new ChargeWaveBehavior()
        };

        public static IReadOnlyList<IWeaponBehavior> All => behaviors;

        public static bool TryGet(string behaviorId, out IWeaponBehavior behavior)
        {
            for (var behaviorIndex = 0; behaviorIndex < behaviors.Length; behaviorIndex++)
            {
                if (string.Equals(behaviors[behaviorIndex].BehaviorId, behaviorId, StringComparison.Ordinal))
                {
                    behavior = behaviors[behaviorIndex];
                    return true;
                }
            }

            behavior = null;
            return false;
        }
    }

    public sealed class ChargeWaveBehavior : IWeaponBehavior
    {
        private static readonly string[] Parameters =
        {
            "minCharge",
            "sweetStart",
            "sweetEnd",
            "maxCharge",
            "maxRange",
            "speed",
            "returnSpeed",
            "damage",
            "lateDamageMultiplier",
            "returnDamageMultiplier",
            "timeoutSeconds"
        };

        public string ContentType => "weapon";

        public string BehaviorId => "charge_wave";

        public IReadOnlyList<string> RequiredNumericParameters => Parameters;

        public CombatTuning CreateTuning(ContentItem weapon, ContentItem combatConfig)
        {
            if (weapon == null || !string.Equals(weapon.Type, ContentType, StringComparison.Ordinal))
            {
                throw new ArgumentException("A weapon content item is required.", nameof(weapon));
            }

            if (combatConfig == null)
            {
                throw new ArgumentNullException(nameof(combatConfig));
            }

            var parameters = weapon.GetObject("params");
            return new CombatTuning(
                RequireNumber(parameters, "minCharge"),
                RequireNumber(parameters, "sweetStart"),
                RequireNumber(parameters, "sweetEnd"),
                RequireNumber(parameters, "maxCharge"),
                RequireNumber(parameters, "maxRange"),
                RequireNumber(parameters, "speed"),
                RequireNumber(parameters, "returnSpeed"),
                RequireNumber(parameters, "damage"),
                RequireNumber(parameters, "lateDamageMultiplier"),
                RequireNumber(parameters, "returnDamageMultiplier"),
                RequireNumber(parameters, "timeoutSeconds"),
                RequireNumber(combatConfig.Fields, "chargeMoveMultiplier"));
        }

        private static double RequireNumber(IDictionary<string, object> values, string key)
        {
            object value;
            if (values != null && values.TryGetValue(key, out value) && value is double)
            {
                return (double)value;
            }

            throw new InvalidOperationException("Required numeric tuning field is missing: " + key);
        }

        private static double RequireNumber(IReadOnlyDictionary<string, object> values, string key)
        {
            object value;
            if (values != null && values.TryGetValue(key, out value) && value is double)
            {
                return (double)value;
            }

            throw new InvalidOperationException("Required numeric tuning field is missing: " + key);
        }
    }
}
