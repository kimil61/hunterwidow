using System;
using System.Collections.Generic;
using HunterWidow.Domain.Combat;
using HunterWidow.Domain.Enemy;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// Each supported content behavior owns the numeric parameter contract that the
    /// validator enforces. Adding a behavior means adding one schema and registering it.
    /// </summary>
    public interface IContentBehaviorSchema
    {
        string ContentType { get; }

        string BehaviorId { get; }

        IReadOnlyList<string> RequiredNumericParameters { get; }
    }

    public static class ContentBehaviorSchemaRegistry
    {
        private static readonly Dictionary<string, IContentBehaviorSchema> schemas = CreateSchemas();

        public static bool TryGet(string contentType, string behaviorId, out IContentBehaviorSchema schema)
        {
            return schemas.TryGetValue(CreateKey(contentType, behaviorId), out schema);
        }

        private static Dictionary<string, IContentBehaviorSchema> CreateSchemas()
        {
            var values = new List<IContentBehaviorSchema>();
            foreach (var weaponBehavior in WeaponBehaviorRegistry.All)
            {
                values.Add(weaponBehavior);
            }

            foreach (var enemyBehavior in EnemyBehaviorRegistry.All)
            {
                values.Add(enemyBehavior);
            }

            var result = new Dictionary<string, IContentBehaviorSchema>(StringComparer.Ordinal);
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var schema = values[valueIndex];
                result.Add(CreateKey(schema.ContentType, schema.BehaviorId), schema);
            }

            return result;
        }

        private static string CreateKey(string contentType, string behaviorId)
        {
            return (contentType ?? string.Empty) + ":" + (behaviorId ?? string.Empty);
        }
    }
}
