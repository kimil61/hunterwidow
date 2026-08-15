using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Inventory;

namespace HunterWidow.Domain.Persistence
{
    public sealed class GameSaveState
    {
        public int Version { get; set; } = 1;

        public int Gold { get; set; }

        public int Affinity { get; set; }

        public int CycleCount { get; set; }

        public string TriggeredEndingId { get; set; } = string.Empty;

        public IReadOnlyList<IdCount> Inventory { get; set; } = new List<IdCount>();

        public IReadOnlyList<IdCount> UpgradeLevels { get; set; } = new List<IdCount>();

        public IReadOnlyList<string> UnlockedRecipes { get; set; } = new List<string>();

        public IReadOnlyList<string> UnlockedCgs { get; set; } = new List<string>();

        public IReadOnlyList<string> UnlockedFloors { get; set; } = new List<string>();

        public IReadOnlyList<string> CraftedItems { get; set; } = new List<string>();

        public IReadOnlyList<string> Flags { get; set; } = new List<string>();

        public IReadOnlyList<CauldronJobState> CauldronJobs { get; set; } = new List<CauldronJobState>();
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadResult(GameSaveState state, bool usedBackup, bool recoveredFromInvalidSave)
        {
            State = state;
            UsedBackup = usedBackup;
            RecoveredFromInvalidSave = recoveredFromInvalidSave;
        }

        public GameSaveState State { get; }

        public bool UsedBackup { get; }

        public bool RecoveredFromInvalidSave { get; }
    }

    public sealed class SaveComposer
    {
        private readonly HashSet<string> knownIds;

        public SaveComposer(IReadOnlyCollection<string> knownIds)
        {
            this.knownIds = new HashSet<string>(knownIds ?? throw new ArgumentNullException(nameof(knownIds)), StringComparer.Ordinal);
        }

        public void Save(string path, GameSaveState state)
        {
            if (string.IsNullOrEmpty(path) || state == null)
            {
                throw new ArgumentException("Save path and state are required.");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var backupPath = GetBackupPath(path);
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
            }

            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, ToJson(state), Encoding.UTF8);
            File.Copy(temporaryPath, path, true);
            File.Delete(temporaryPath);
        }

        public SaveLoadResult Load(string path)
        {
            GameSaveState state;
            if (TryLoad(path, out state))
            {
                return new SaveLoadResult(state, false, false);
            }

            if (TryLoad(GetBackupPath(path), out state))
            {
                return new SaveLoadResult(state, true, true);
            }

            return new SaveLoadResult(new GameSaveState(), false, File.Exists(path) || File.Exists(GetBackupPath(path)));
        }

        private bool TryLoad(string path, out GameSaveState state)
        {
            state = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var root = ContentValues.AsObject(MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)));
                if (root == null)
                {
                    return false;
                }

                state = new GameSaveState
                {
                    Version = Number(root, "version", 1),
                    Gold = Number(root, "gold", 0),
                    Affinity = Number(root, "affinity", 0),
                    CycleCount = Number(root, "cycleCount", 0),
                    TriggeredEndingId = KnownString(root, "triggeredEndingId"),
                    Inventory = ReadIdCounts(root, "inventory"),
                    UpgradeLevels = ReadIdCounts(root, "upgradeLevels"),
                    UnlockedRecipes = ReadIds(root, "unlockedRecipes"),
                    UnlockedCgs = ReadIds(root, "unlockedCgs"),
                    UnlockedFloors = ReadIds(root, "unlockedFloors"),
                    CraftedItems = ReadIds(root, "craftedItems"),
                    Flags = ReadIds(root, "flags"),
                    CauldronJobs = ReadCauldronJobs(root)
                };
                return true;
            }
            catch (Exception)
            {
                state = null;
                return false;
            }
        }

        private IReadOnlyList<IdCount> ReadIdCounts(IDictionary<string, object> root, string key)
        {
            var values = new List<IdCount>();
            var array = ContentValues.GetArray(root, key);
            if (array == null)
            {
                return values;
            }

            for (var index = 0; index < array.Count; index++)
            {
                var entry = ContentValues.AsObject(array[index]);
                if (entry == null)
                {
                    continue;
                }

                var id = ContentValues.GetString(entry, "id");
                var count = Number(entry, "count", 0);
                if (knownIds.Contains(id) && count > 0)
                {
                    values.Add(new IdCount(id, count));
                }
            }

            return values;
        }

        private IReadOnlyList<string> ReadIds(IDictionary<string, object> root, string key)
        {
            var values = new List<string>();
            var array = ContentValues.GetArray(root, key);
            if (array == null)
            {
                return values;
            }

            for (var index = 0; index < array.Count; index++)
            {
                var id = array[index] as string;
                if (!string.IsNullOrEmpty(id) && knownIds.Contains(id))
                {
                    values.Add(id);
                }
            }

            return values;
        }

        private IReadOnlyList<CauldronJobState> ReadCauldronJobs(IDictionary<string, object> root)
        {
            var states = new List<CauldronJobState>();
            var array = ContentValues.GetArray(root, "cauldronJobs");
            if (array == null)
            {
                return states;
            }

            for (var index = 0; index < array.Count; index++)
            {
                var entry = ContentValues.AsObject(array[index]);
                if (entry == null)
                {
                    continue;
                }

                var recipeId = ContentValues.GetString(entry, "recipeId");
                var remainingCycles = Number(entry, "remainingCycles", 0);
                if (knownIds.Contains(recipeId) && remainingCycles > 0)
                {
                    states.Add(new CauldronJobState(recipeId, remainingCycles));
                }
            }

            return states;
        }

        private string KnownString(IDictionary<string, object> root, string key)
        {
            var id = ContentValues.GetString(root, key);
            return knownIds.Contains(id) ? id : string.Empty;
        }

        private static int Number(IDictionary<string, object> values, string key, int fallback)
        {
            object value;
            return values.TryGetValue(key, out value) && value is double ? (int)(double)value : fallback;
        }

        private static string GetBackupPath(string path)
        {
            return path + ".bak";
        }

        private static string ToJson(GameSaveState state)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            AppendNumber(builder, "version", state.Version, true);
            AppendNumber(builder, "gold", state.Gold, true);
            AppendNumber(builder, "affinity", state.Affinity, true);
            AppendNumber(builder, "cycleCount", state.CycleCount, true);
            AppendString(builder, "triggeredEndingId", state.TriggeredEndingId, true);
            AppendIdCounts(builder, "inventory", state.Inventory, true);
            AppendIdCounts(builder, "upgradeLevels", state.UpgradeLevels, true);
            AppendIds(builder, "unlockedRecipes", state.UnlockedRecipes, true);
            AppendIds(builder, "unlockedCgs", state.UnlockedCgs, true);
            AppendIds(builder, "unlockedFloors", state.UnlockedFloors, true);
            AppendIds(builder, "craftedItems", state.CraftedItems, true);
            AppendIds(builder, "flags", state.Flags, true);
            AppendCauldronJobs(builder, state.CauldronJobs, false);
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendNumber(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value);
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendString(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append("\"");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendIdCounts(StringBuilder builder, string key, IReadOnlyList<IdCount> values, bool comma)
        {
            builder.Append("  \"").Append(key).AppendLine("\": [");
            if (values != null)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    var value = values[index];
                    builder.Append("    { \"id\": \"").Append(Escape(value.Id)).Append("\", \"count\": ").Append(value.Count).Append(" }");
                    builder.AppendLine(index + 1 < values.Count ? "," : string.Empty);
                }
            }

            builder.Append("  ]").AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendIds(StringBuilder builder, string key, IReadOnlyList<string> values, bool comma)
        {
            builder.Append("  \"").Append(key).AppendLine("\": [");
            if (values != null)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    builder.Append("    \"").Append(Escape(values[index])).Append("\"");
                    builder.AppendLine(index + 1 < values.Count ? "," : string.Empty);
                }
            }

            builder.Append("  ]").AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendCauldronJobs(StringBuilder builder, IReadOnlyList<CauldronJobState> values, bool comma)
        {
            builder.Append("  \"cauldronJobs\": [");
            if (values != null)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    var value = values[index];
                    builder.AppendLine();
                    builder.Append("    { \"recipeId\": \"").Append(Escape(value.RecipeId)).Append("\", \"remainingCycles\": ").Append(value.RemainingCycles).Append(" }");
                    builder.Append(index + 1 < values.Count ? "," : string.Empty);
                }

                if (values.Count > 0)
                {
                    builder.AppendLine();
                }
            }

            builder.Append("  ]").AppendLine(comma ? "," : string.Empty);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
