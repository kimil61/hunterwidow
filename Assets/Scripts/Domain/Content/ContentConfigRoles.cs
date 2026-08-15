using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// Names the configuration responsibilities used by code. The concrete content
    /// item IDs remain data, so a pack can rename them without a C# change.
    /// </summary>
    public static class ContentConfigRoles
    {
        public const string Combat = "combat";
        public const string Erosion = "erosion";
        public const string Inventory = "inventory";
        public const string Economy = "economy";
        public const string Progression = "progression";
        public const string Options = "options";
        public const string Audio = "audio";
        public const string Ui = "ui";
        public const string Simulation = "simulation";

        private static readonly string[] RuntimeRoles =
        {
            Combat,
            Erosion,
            Inventory,
            Economy,
            Progression,
            Options,
            Audio,
            Ui
        };

        public static IReadOnlyList<string> RequiredForPlayableMvp => RuntimeRoles;

        public static bool TryFind(ContentDatabase database, string role, out ContentItem config)
        {
            config = null;
            if (database == null || string.IsNullOrEmpty(role))
            {
                return false;
            }

            foreach (var candidate in database.FindByType("config"))
            {
                if (string.Equals(candidate.GetString("role"), role, StringComparison.Ordinal))
                {
                    config = candidate;
                    return true;
                }
            }

            return false;
        }

        public static ContentItem Require(ContentDatabase database, string role)
        {
            ContentItem config;
            if (TryFind(database, role, out config))
            {
                return config;
            }

            throw new KeyNotFoundException("Configuration role is missing: " + role);
        }
    }
}
