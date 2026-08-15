using System;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// A valid canary pack proves the loader and validator work. A playable MVP pack
    /// additionally declares the runtime contracts it provides in pack.json.
    /// </summary>
    public static class MvpContentRequirements
    {
        public static bool IsReady(ContentDatabase database)
        {
            if (!DeclaresPlayableMvp(database))
            {
                return false;
            }

            foreach (var pack in database.FindByType("pack"))
            {
                var requiredIds = pack.GetArray("runtimeRequiredIds");
                if (requiredIds == null || requiredIds.Count == 0)
                {
                    continue;
                }

                for (var idIndex = 0; idIndex < requiredIds.Count; idIndex++)
                {
                    var id = requiredIds[idIndex] as string;
                    ContentItem ignored;
                    if (string.IsNullOrEmpty(id) || !database.TryGet(id, out ignored))
                    {
                        return false;
                    }
                }

                var roles = ContentConfigRoles.RequiredForPlayableMvp;
                for (var roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                {
                    ContentItem ignored;
                    if (!ContentConfigRoles.TryFind(database, roles[roleIndex], out ignored))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static bool DeclaresPlayableMvp(ContentDatabase database)
        {
            if (database == null)
            {
                return false;
            }

            foreach (var pack in database.FindByType("pack"))
            {
                var requiredIds = pack.GetArray("runtimeRequiredIds");
                if (requiredIds != null && requiredIds.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
