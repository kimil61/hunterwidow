using System;
using System.Collections.Generic;
using System.IO;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// Validates content data without depending on Unity. All checks append findings so
    /// a content author receives one complete report instead of a fix-run-repeat loop.
    /// </summary>
    public static class ContentValidator
    {
        private static readonly HashSet<string> KnownConditionTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "affinity_at_least",
            "affinity_below",
            "cycle_at_least",
            "floor_first_reached",
            "recipe_unlocked",
            "item_crafted",
            "upgrade_at_least",
            "flag_set",
            "flag_not_set",
            "gold_at_least"
        };

        private static readonly HashSet<string> KnownEffectTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "set_flag",
            "clear_flag",
            "add_affinity",
            "unlock_recipe",
            "unlock_cg",
            "give_material",
            "give_gold",
            "unlock_floor",
            "trigger_ending"
        };

        private static readonly HashSet<string> KnownEffectKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "backpack_slots",
            "erosion_decay_rate",
            "weapon_damage",
            "weapon_scale",
            "cauldron_slots",
            "refinery_level"
        };

        private static readonly string[] ContentPrefixes =
        {
            "wpn_",
            "enm_",
            "mat_",
            "itm_",
            "rcp_",
            "upg_",
            "drop_",
            "floor_",
            "evt_",
            "cg_",
            "end_",
            "flag_",
            "cfg_"
        };

        public static void Validate(string rootPath, ContentDatabase database, ContentValidationReport report)
        {
            ValidateIdentifiers(database, report);
            ValidateConfigRoles(database, report);
            ValidateReferences(database, report);
            ValidateRegistriesAndParameters(database, report);
            ValidateLocalization(rootPath, database, report);
            ValidateAssetPaths(rootPath, database, report);
            ValidatePresentationData(database, report);
            ValidateFloorGraph(database, report);
            ValidateRecipeReachability(database, report);
            ValidateEndings(database, report);
        }

        private static void ValidateConfigRoles(ContentDatabase database, ContentValidationReport report)
        {
            var itemByRole = new Dictionary<string, ContentItem>(StringComparer.Ordinal);
            foreach (var config in database.FindByType("config"))
            {
                var role = config.GetString("role");
                if (string.IsNullOrEmpty(role))
                {
                    Add(report, "CONFIG_ROLE_REQUIRED", config, "Every config item requires a role.");
                    continue;
                }

                ContentItem existing;
                if (itemByRole.TryGetValue(role, out existing))
                {
                    Add(report, "CONFIG_ROLE_DUPLICATE", config, "Configuration role '" + role + "' is already used by '" + existing.Id + "'.");
                    continue;
                }

                itemByRole.Add(role, config);
            }

            if (!MvpContentRequirements.DeclaresPlayableMvp(database))
            {
                return;
            }

            var roles = ContentConfigRoles.RequiredForPlayableMvp;
            for (var roleIndex = 0; roleIndex < roles.Count; roleIndex++)
            {
                if (!itemByRole.ContainsKey(roles[roleIndex]))
                {
                    report.Add("CONFIG_ROLE_MISSING", "pack.json", 1, "Playable MVP content requires configuration role '" + roles[roleIndex] + "'.");
                }
            }
        }

        private static void ValidateIdentifiers(ContentDatabase database, ContentValidationReport report)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in database.AllItems)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    Add(report, "ITEM_ID_REQUIRED", item, "Every item requires a non-empty id.");
                    continue;
                }

                if (!IsLowerSnakeCase(item.Id))
                {
                    Add(report, "ITEM_ID_FORMAT", item, "ID must be lowercase snake case.");
                }

                if (!HasContentPrefix(item.Id))
                {
                    Add(report, "ITEM_ID_PREFIX", item, "ID does not use a registered content prefix.");
                }

                if (!seenIds.Add(item.Id))
                {
                    Add(report, "ITEM_ID_DUPLICATE", item, "ID is duplicated elsewhere in this content pack.");
                }
            }
        }

        private static void ValidateReferences(ContentDatabase database, ContentValidationReport report)
        {
            foreach (var item in database.AllItems)
            {
                ValidateReferencesInValue(item, item.Fields, "", database, report);
            }
        }

        private static void ValidateReferencesInValue(
            ContentItem owner,
            object value,
            string fieldName,
            ContentDatabase database,
            ContentValidationReport report)
        {
            var objectValue = ContentValues.AsObject(value);
            if (objectValue != null)
            {
                foreach (var pair in objectValue)
                {
                    ValidateReferencesInValue(owner, pair.Value, pair.Key, database, report);
                }

                return;
            }

            var arrayValue = ContentValues.AsArray(value);
            if (arrayValue != null)
            {
                for (var elementIndex = 0; elementIndex < arrayValue.Count; elementIndex++)
                {
                    ValidateReferencesInValue(owner, arrayValue[elementIndex], fieldName, database, report);
                }

                return;
            }

            var stringValue = value as string;
            if (stringValue == null || string.Equals(fieldName, "type", StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(fieldName, "id", StringComparison.Ordinal) && string.Equals(stringValue, owner.Id, StringComparison.Ordinal))
            {
                return;
            }

            if (!HasContentPrefix(stringValue))
            {
                return;
            }

            ContentItem referencedItem;
            if (!database.TryGet(stringValue, out referencedItem))
            {
                Add(report, "REFERENCE_MISSING", owner, "Reference '" + stringValue + "' in '" + fieldName + "' does not exist.");
            }
        }

        private static void ValidateRegistriesAndParameters(ContentDatabase database, ContentValidationReport report)
        {
            foreach (var item in database.AllItems)
            {
                if (string.Equals(item.Type, "weapon", StringComparison.Ordinal))
                {
                    ValidateBehaviorAndParameters(item, report);
                }

                if (string.Equals(item.Type, "enemy", StringComparison.Ordinal))
                {
                    ValidateBehaviorAndParameters(item, report);
                }

                if (string.Equals(item.Type, "upgrade", StringComparison.Ordinal))
                {
                    var effects = item.GetArray("effects");
                    if (effects == null)
                    {
                        ValidateUpgradeEffectKey(item, item.GetString("effectKey"), report);
                    }
                    else
                    {
                        for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                        {
                            var effect = ContentValues.AsObject(effects[effectIndex]);
                            if (effect == null)
                            {
                                Add(report, "UPGRADE_EFFECT_OBJECT", item, "Each upgrade effect must be an object.");
                                continue;
                            }

                            ValidateUpgradeEffectKey(item, ContentValues.GetString(effect, "key"), report);
                            var operation = ContentValues.GetString(effect, "operation");
                            if (!string.Equals(operation, "set", StringComparison.Ordinal)
                                && !string.Equals(operation, "add", StringComparison.Ordinal)
                                && !string.Equals(operation, "mul", StringComparison.Ordinal))
                            {
                                Add(report, "UPGRADE_OPERATION_UNREGISTERED", item, "Upgrade operation must be set, add, or mul.");
                            }
                        }
                    }
                }

                if (string.Equals(item.Type, "story_event", StringComparison.Ordinal))
                {
                    ValidateConditions(item, report);
                    ValidateEffects(item, report);
                }
                else if (string.Equals(item.Type, "ending", StringComparison.Ordinal))
                {
                    ValidateConditions(item, report);
                }
            }

            if (MvpContentRequirements.DeclaresPlayableMvp(database))
            {
                ValidateStarterWeapon(database, report);
            }
        }

        private static void ValidateStarterWeapon(ContentDatabase database, ContentValidationReport report)
        {
            ContentItem combatConfig;
            if (!ContentConfigRoles.TryFind(database, ContentConfigRoles.Combat, out combatConfig))
            {
                return;
            }

            var starterWeaponId = combatConfig.GetString("starterWeaponId");
            ContentItem weapon;
            if (string.IsNullOrEmpty(starterWeaponId)
                || !database.TryGet(starterWeaponId, out weapon)
                || !string.Equals(weapon.Type, "weapon", StringComparison.Ordinal))
            {
                Add(report, "STARTER_WEAPON_INVALID", combatConfig, "starterWeaponId must reference weapon content.");
            }
        }

        private static void ValidateBehaviorAndParameters(ContentItem item, ContentValidationReport report)
        {
            var behavior = item.GetString("behavior");
            IContentBehaviorSchema schema;
            if (!ContentBehaviorSchemaRegistry.TryGet(item.Type, behavior, out schema))
            {
                Add(report, "BEHAVIOR_UNREGISTERED", item, "behavior is not registered for this item type.");
                return;
            }

            ValidateParameterSchema(item, schema.RequiredNumericParameters, report);
        }

        private static void ValidateUpgradeEffectKey(ContentItem item, string effectKey, ContentValidationReport report)
        {
            if (!KnownEffectKeys.Contains(effectKey))
            {
                Add(report, "UPGRADE_EFFECT_KEY", item, "effectKey is not registered.");
            }
        }

        private static void ValidateParameterSchema(ContentItem item, IReadOnlyList<string> requiredNames, ContentValidationReport report)
        {
            var parameters = item.GetObject("params");
            if (parameters == null)
            {
                Add(report, "PARAMS_OBJECT_REQUIRED", item, "Behavior items require a params object.");
                return;
            }

            for (var nameIndex = 0; nameIndex < requiredNames.Count; nameIndex++)
            {
                var name = requiredNames[nameIndex];
                object value;
                if (!parameters.TryGetValue(name, out value))
                {
                    Add(report, "PARAM_REQUIRED", item, "params." + name + " is required.");
                    continue;
                }

                if (!(value is double))
                {
                    Add(report, "PARAM_NUMBER_REQUIRED", item, "params." + name + " must be a number.");
                }
            }
        }

        private static void ValidateConditions(ContentItem item, ContentValidationReport report)
        {
            var conditions = item.GetArray("conditions");
            if (conditions == null)
            {
                return;
            }

            for (var conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                var condition = ContentValues.AsObject(conditions[conditionIndex]);
                if (condition == null)
                {
                    Add(report, "CONDITION_OBJECT_REQUIRED", item, "Each condition must be an object.");
                    continue;
                }

                var type = ContentValues.GetString(condition, "type");
                if (!KnownConditionTypes.Contains(type))
                {
                    Add(report, "CONDITION_TYPE_UNREGISTERED", item, "Condition type is not registered.");
                }
            }
        }

        private static void ValidateEffects(ContentItem item, ContentValidationReport report)
        {
            var effects = item.GetArray("effects");
            if (effects == null)
            {
                return;
            }

            for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                var effect = ContentValues.AsObject(effects[effectIndex]);
                if (effect == null)
                {
                    Add(report, "EFFECT_OBJECT_REQUIRED", item, "Each effect must be an object.");
                    continue;
                }

                var type = ContentValues.GetString(effect, "type");
                if (!KnownEffectTypes.Contains(type))
                {
                    Add(report, "EFFECT_TYPE_UNREGISTERED", item, "Effect type is not registered.");
                }
            }
        }

        private static void ValidateLocalization(string rootPath, ContentDatabase database, ContentValidationReport report)
        {
            var localePath = Path.Combine(rootPath, "locale", "strings.csv");
            if (!File.Exists(localePath))
            {
                report.Add("LOCALE_FILE_MISSING", "locale/strings.csv", 1, "strings.csv is required.");
                return;
            }

            var availableKeys = ReadLocaleKeys(localePath, report, rootPath);
            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in database.AllItems)
            {
                FindLocaleKeys(item, item.Fields, string.Empty, usedKeys);
            }

            foreach (var usedKey in usedKeys)
            {
                if (!availableKeys.Contains(usedKey))
                {
                    report.Add("LOCALE_KEY_MISSING", "locale/strings.csv", 1, "Content refers to undefined locale key '" + usedKey + "'.");
                }
            }

            foreach (var availableKey in availableKeys)
            {
                if (!usedKeys.Contains(availableKey))
                {
                    report.Add("LOCALE_KEY_UNUSED", "locale/strings.csv", 1, "Locale key '" + availableKey + "' is not used by content.");
                }
            }
        }

        private static HashSet<string> ReadLocaleKeys(string localePath, ContentValidationReport report, string rootPath)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string[] lines;
            try
            {
                lines = File.ReadAllLines(localePath);
            }
            catch (Exception exception)
            {
                report.Add("LOCALE_READ_FAILED", ContentLoader.MakeRelativePath(rootPath, localePath), 1, exception.Message);
                return keys;
            }

            if (lines.Length == 0 || !lines[0].StartsWith("key,", StringComparison.Ordinal))
            {
                report.Add("LOCALE_HEADER_INVALID", ContentLoader.MakeRelativePath(rootPath, localePath), 1, "The first CSV column must be key.");
            }

            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var commaIndex = line.IndexOf(',');
                var key = (commaIndex < 0 ? line : line.Substring(0, commaIndex)).Trim('"');
                if (key.Length == 0)
                {
                    report.Add("LOCALE_KEY_REQUIRED", ContentLoader.MakeRelativePath(rootPath, localePath), lineIndex + 1, "Locale row has no key.");
                    continue;
                }

                if (!keys.Add(key))
                {
                    report.Add("LOCALE_KEY_DUPLICATE", ContentLoader.MakeRelativePath(rootPath, localePath), lineIndex + 1, "Locale key is duplicated.");
                }
            }

            return keys;
        }

        private static void FindLocaleKeys(ContentItem owner, object value, string fieldName, HashSet<string> usedKeys)
        {
            var objectValue = ContentValues.AsObject(value);
            if (objectValue != null)
            {
                foreach (var pair in objectValue)
                {
                    FindLocaleKeys(owner, pair.Value, pair.Key, usedKeys);
                }

                return;
            }

            var arrayValue = ContentValues.AsArray(value);
            if (arrayValue != null)
            {
                for (var elementIndex = 0; elementIndex < arrayValue.Count; elementIndex++)
                {
                    FindLocaleKeys(owner, arrayValue[elementIndex], fieldName, usedKeys);
                }

                return;
            }

            if (!IsLocaleField(fieldName))
            {
                return;
            }

            var key = value as string;
            if (!string.IsNullOrEmpty(key))
            {
                usedKeys.Add(key);
            }
        }

        private static bool IsLocaleField(string fieldName)
        {
            return string.Equals(fieldName, "nameKey", StringComparison.Ordinal)
                || string.Equals(fieldName, "descKey", StringComparison.Ordinal)
                || string.Equals(fieldName, "textKey", StringComparison.Ordinal)
                || string.Equals(fieldName, "dialogueKey", StringComparison.Ordinal)
                || string.Equals(fieldName, "introTextKeys", StringComparison.Ordinal);
        }

        private static void ValidateAssetPaths(string rootPath, ContentDatabase database, ContentValidationReport report)
        {
            foreach (var item in database.AllItems)
            {
                ValidateAssetPathsInValue(rootPath, item, item.Fields, string.Empty, report);
            }
        }

        private static void ValidateAssetPathsInValue(
            string rootPath,
            ContentItem owner,
            object value,
            string fieldName,
            ContentValidationReport report)
        {
            var objectValue = ContentValues.AsObject(value);
            if (objectValue != null)
            {
                foreach (var pair in objectValue)
                {
                    ValidateAssetPathsInValue(rootPath, owner, pair.Value, pair.Key, report);
                }

                return;
            }

            var arrayValue = ContentValues.AsArray(value);
            if (arrayValue != null)
            {
                for (var elementIndex = 0; elementIndex < arrayValue.Count; elementIndex++)
                {
                    ValidateAssetPathsInValue(rootPath, owner, arrayValue[elementIndex], fieldName, report);
                }

                return;
            }

            if (!fieldName.EndsWith("Path", StringComparison.Ordinal) || string.Equals(fieldName, "path", StringComparison.Ordinal))
            {
                return;
            }

            var relativePath = value as string;
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(rootPath, normalized)))
            {
                Add(report, "ASSET_PATH_MISSING", owner, "Asset path '" + relativePath + "' does not exist in the content pack.");
            }
        }

        private static void ValidateFloorGraph(ContentDatabase database, ContentValidationReport report)
        {
            var nextFloorById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var floor in database.FindByType("floor"))
            {
                var nextFloorId = floor.GetString("nextFloorId");
                if (!string.IsNullOrEmpty(nextFloorId))
                {
                    nextFloorById[floor.Id] = nextFloorId;
                }
            }

            var stateById = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in nextFloorById)
            {
                if (HasFloorCycle(pair.Key, nextFloorById, stateById))
                {
                    ContentItem floor;
                    if (database.TryGet(pair.Key, out floor))
                    {
                        Add(report, "FLOOR_CHAIN_CYCLE", floor, "nextFloorId creates a cycle.");
                    }
                }
            }
        }

        private static void ValidatePresentationData(ContentDatabase database, ContentValidationReport report)
        {
            if (!MvpContentRequirements.IsReady(database))
            {
                return;
            }

            ValidateUiUpgradeGroups(database, report);
            ValidateUiLanguageOptions(database, report);

            foreach (var floor in database.FindByType("floor"))
            {
                try
                {
                    var layout = FloorLayoutDefinition.FromContent(floor);
                    var accessRequirement = FloorAccessRequirement.FromContent(floor);
                    if (layout.RopeCandidates.Count < 4 || layout.RopeCandidates.Count > 5)
                    {
                        Add(report, "FLOOR_ROPE_CANDIDATE_COUNT", floor, "Floors require four or five rope candidates.");
                    }

                    if (layout.ActiveRopeCount != 2 || layout.ActiveRopeCount > layout.RopeCandidates.Count)
                    {
                        Add(report, "FLOOR_ACTIVE_ROPE_COUNT", floor, "Floors require exactly two active ropes.");
                    }

                    ValidateSpawnCandidates(database, report, floor, layout.EnemySpawns, "enemy", "FLOOR_ENEMY_SPAWN");
                    ValidateSpawnCandidates(database, report, floor, layout.GatherSpawns, "material", "FLOOR_GATHER_SPAWN");
                    if (layout.EnemySpawns.Count > 0 && !HasSpawnableEnemyForFloor(database, floor.Id))
                    {
                        Add(report, "FLOOR_ENEMY_SOURCE_MISSING", floor, "Regular enemy spawns require at least one non-tutorial enemy assigned to this floor.");
                    }

                    if (accessRequirement != null && !HasUpgradeAxis(database, accessRequirement.UpgradeAxisId))
                    {
                        Add(report, "FLOOR_ACCESS_UPGRADE_MISSING", floor, "accessRequirement upgradeAxisId is not declared by any upgrade.");
                    }
                }
                catch (Exception exception)
                {
                    Add(report, "FLOOR_LAYOUT_INVALID", floor, exception.Message);
                }
            }

            ContentItem progression;
            if (ContentConfigRoles.TryFind(database, ContentConfigRoles.Progression, out progression))
            {
                try
                {
                    var tutorial = TutorialDefinition.FromValue(progression.GetObject("tutorial"));
                    if (tutorial != null)
                    {
                        ValidateExplicitTypedReference(database, report, progression, "tutorial.floorId", "floor", "TUTORIAL_FLOOR_REFERENCE", tutorial.FloorId);
                        ValidateExplicitTypedReference(database, report, progression, "tutorial.targetEnemyId", "enemy", "TUTORIAL_TARGET_REFERENCE", tutorial.TargetEnemyId);
                        ValidateExplicitTypedReference(database, report, progression, "tutorial.completionFlagId", "flag", "TUTORIAL_FLAG_REFERENCE", tutorial.CompletionFlagId);
                    }
                }
                catch (Exception exception)
                {
                    Add(report, "TUTORIAL_INVALID", progression, exception.Message);
                }
            }

            ContentItem erosion;
            if (ContentConfigRoles.TryFind(database, ContentConfigRoles.Erosion, out erosion))
            {
                var bands = erosion.GetArray("bands");
                if (bands != null)
                {
                    for (var bandIndex = 0; bandIndex < bands.Count; bandIndex++)
                    {
                        try
                        {
                            ErosionBandPresentationDefinition.FromValue(bands[bandIndex]);
                        }
                        catch (Exception exception)
                        {
                            Add(report, "EROSION_PRESENTATION_INVALID", erosion, exception.Message);
                        }
                    }
                }
            }

            foreach (var cg in database.FindByType("cg"))
            {
                if (string.IsNullOrEmpty(cg.GetString("nameKey")))
                {
                    Add(report, "CG_NAME_KEY_REQUIRED", cg, "CG placeholders require a nameKey.");
                }

                try
                {
                    ContentColor.FromValue(cg.GetArray("placeholderColor"), cg.Id + ".placeholderColor");
                }
                catch (Exception exception)
                {
                    Add(report, "CG_PLACEHOLDER_COLOR_INVALID", cg, exception.Message);
                }
            }

            foreach (var ending in database.FindByType("ending"))
            {
                ValidateTypedReference(database, report, ending, "cgId", "cg", "ENDING_CG_REFERENCE");
            }

            foreach (var storyEvent in database.FindByType("story_event"))
            {
                ValidateTypedReference(database, report, storyEvent, "portraitCgId", "cg", "STORY_PORTRAIT_REFERENCE");
            }
        }

        private static bool HasUpgradeAxis(ContentDatabase database, string axisId)
        {
            foreach (var upgrade in database.FindByType("upgrade"))
            {
                if (string.Equals(upgrade.GetString("axisId"), axisId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateUiUpgradeGroups(ContentDatabase database, ContentValidationReport report)
        {
            ContentItem ui;
            if (!ContentConfigRoles.TryFind(database, ContentConfigRoles.Ui, out ui))
            {
                return;
            }

            var groups = ui.GetObject("upgradeGroups");
            if (groups == null)
            {
                Add(report, "UI_UPGRADE_GROUPS_MISSING", ui, "UI config requires an upgradeGroups object.");
                return;
            }

            var declaredAxes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var upgrade in database.FindByType("upgrade"))
            {
                var axisId = upgrade.GetString("axisId");
                if (!string.IsNullOrEmpty(axisId))
                {
                    declaredAxes.Add(axisId);
                }
            }

            var assignedAxes = new HashSet<string>(StringComparer.Ordinal);
            var requiredGroups = new[] { "workbench", "pharmacy" };
            for (var groupIndex = 0; groupIndex < requiredGroups.Length; groupIndex++)
            {
                object rawAxisIds;
                if (!groups.TryGetValue(requiredGroups[groupIndex], out rawAxisIds))
                {
                    Add(report, "UI_UPGRADE_GROUP_MISSING", ui, "upgradeGroups requires '" + requiredGroups[groupIndex] + "'.");
                    continue;
                }

                var axisIds = ContentValues.AsArray(rawAxisIds);
                if (axisIds == null)
                {
                    Add(report, "UI_UPGRADE_GROUP_INVALID", ui, "upgradeGroups.'" + requiredGroups[groupIndex] + "' must be an array.");
                    continue;
                }

                for (var axisIndex = 0; axisIndex < axisIds.Count; axisIndex++)
                {
                    var axisId = axisIds[axisIndex] as string;
                    if (string.IsNullOrEmpty(axisId) || !declaredAxes.Contains(axisId))
                    {
                        Add(report, "UI_UPGRADE_GROUP_AXIS_MISSING", ui, "upgradeGroups refers to an undefined upgrade axis.");
                    }
                    else if (!assignedAxes.Add(axisId))
                    {
                        Add(report, "UI_UPGRADE_GROUP_AXIS_DUPLICATE", ui, "An upgrade axis may appear in only one UI group.");
                    }
                }
            }

            foreach (var axisId in declaredAxes)
            {
                if (!assignedAxes.Contains(axisId))
                {
                    Add(report, "UI_UPGRADE_GROUP_AXIS_UNASSIGNED", ui, "An upgrade axis is not assigned to a UI group.");
                }
            }
        }

        private static void ValidateUiLanguageOptions(ContentDatabase database, ContentValidationReport report)
        {
            ContentItem options;
            if (!ContentConfigRoles.TryFind(database, ContentConfigRoles.Options, out options))
            {
                return;
            }

            var languages = options.GetArray("languages");
            if (languages == null || languages.Count == 0)
            {
                Add(report, "OPTIONS_LANGUAGES_MISSING", options, "Options config requires at least one language entry.");
                return;
            }

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
            {
                var language = ContentValues.AsObject(languages[languageIndex]);
                if (language == null)
                {
                    Add(report, "OPTIONS_LANGUAGE_INVALID", options, "Each language entry must be an object.");
                    continue;
                }

                var code = ContentValues.GetString(language, "code");
                var nameKey = ContentValues.GetString(language, "nameKey");
                if (string.IsNullOrEmpty(code))
                {
                    Add(report, "OPTIONS_LANGUAGE_CODE_REQUIRED", options, "Language entries require a code.");
                }
                else if (!seenCodes.Add(code))
                {
                    Add(report, "OPTIONS_LANGUAGE_CODE_DUPLICATE", options, "Language codes must be unique.");
                }

                if (string.IsNullOrEmpty(nameKey))
                {
                    Add(report, "OPTIONS_LANGUAGE_NAME_KEY_REQUIRED", options, "Language entries require a nameKey.");
                }
            }
        }

        private static bool HasSpawnableEnemyForFloor(ContentDatabase database, string floorId)
        {
            foreach (var enemy in database.FindByType("enemy"))
            {
                if (string.Equals(enemy.GetString("floorId"), floorId, StringComparison.Ordinal)
                    && !enemy.GetBool("isTutorialTarget"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateSpawnCandidates(
            ContentDatabase database,
            ContentValidationReport report,
            ContentItem floor,
            IReadOnlyList<FloorSpawnDefinition> spawns,
            string expectedType,
            string code)
        {
            for (var spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex++)
            {
                var candidates = spawns[spawnIndex].CandidateIds;
                for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    ContentItem candidate;
                    if (!database.TryGet(candidates[candidateIndex], out candidate)
                        || !string.Equals(candidate.Type, expectedType, StringComparison.Ordinal))
                    {
                        Add(report, code, floor, "Spawn candidates must reference " + expectedType + " content.");
                    }
                }
            }
        }

        private static void ValidateTypedReference(
            ContentDatabase database,
            ContentValidationReport report,
            ContentItem owner,
            string fieldName,
            string expectedType,
            string code)
        {
            var id = owner.GetString(fieldName);
            ContentItem target;
            if (string.IsNullOrEmpty(id) || !database.TryGet(id, out target) || !string.Equals(target.Type, expectedType, StringComparison.Ordinal))
            {
                Add(report, code, owner, fieldName + " must reference " + expectedType + " content.");
            }
        }

        private static void ValidateExplicitTypedReference(
            ContentDatabase database,
            ContentValidationReport report,
            ContentItem owner,
            string fieldName,
            string expectedType,
            string code,
            string id)
        {
            ContentItem target;
            if (string.IsNullOrEmpty(id) || !database.TryGet(id, out target) || !string.Equals(target.Type, expectedType, StringComparison.Ordinal))
            {
                Add(report, code, owner, fieldName + " must reference " + expectedType + " content.");
            }
        }

        private static bool HasFloorCycle(string id, Dictionary<string, string> nextFloorById, Dictionary<string, int> stateById)
        {
            int state;
            if (stateById.TryGetValue(id, out state))
            {
                return state == 1;
            }

            stateById[id] = 1;
            string next;
            var hasCycle = nextFloorById.TryGetValue(id, out next) && HasFloorCycle(next, nextFloorById, stateById);
            stateById[id] = 2;
            return hasCycle;
        }

        private static void ValidateRecipeReachability(ContentDatabase database, ContentValidationReport report)
        {
            var sourceMaterials = new HashSet<string>(StringComparer.Ordinal);
            foreach (var material in database.FindByType("material"))
            {
                var source = material.GetString("source");
                if (string.IsNullOrEmpty(source))
                {
                    Add(report, "MATERIAL_SOURCE_REQUIRED", material, "Materials must declare a source.");
                }
                else
                {
                    sourceMaterials.Add(material.Id);
                }
            }

            foreach (var recipe in database.FindByType("recipe"))
            {
                var ingredients = recipe.GetArray("ingredients");
                if (ingredients == null || ingredients.Count == 0)
                {
                    Add(report, "RECIPE_INGREDIENTS_REQUIRED", recipe, "Recipes need at least one ingredient.");
                    continue;
                }

                for (var ingredientIndex = 0; ingredientIndex < ingredients.Count; ingredientIndex++)
                {
                    var ingredient = ContentValues.AsObject(ingredients[ingredientIndex]);
                    if (ingredient == null)
                    {
                        Add(report, "RECIPE_INGREDIENT_OBJECT", recipe, "Each recipe ingredient must be an object.");
                        continue;
                    }

                    var materialId = ContentValues.GetString(ingredient, "materialId");
                    ContentItem material;
                    if (!database.TryGet(materialId, out material) || !string.Equals(material.Type, "material", StringComparison.Ordinal))
                    {
                        Add(report, "RECIPE_MATERIAL_MISSING", recipe, "Recipe ingredient does not point to a material.");
                    }
                    else if (!sourceMaterials.Contains(materialId))
                    {
                        Add(report, "RECIPE_MATERIAL_UNREACHABLE", recipe, "Recipe ingredient cannot be acquired from content.");
                    }

                    if (ContentValues.GetNumber(ingredient, "amount") <= 0d)
                    {
                        Add(report, "RECIPE_AMOUNT_INVALID", recipe, "Recipe ingredient amount must be greater than zero.");
                    }
                }

                var outputItemId = recipe.GetString("outputItemId");
                ContentItem outputItem;
                if (!database.TryGet(outputItemId, out outputItem))
                {
                    Add(report, "RECIPE_OUTPUT_MISSING", recipe, "Recipe outputItemId must point to an item.");
                }
            }
        }

        private static void ValidateEndings(ContentDatabase database, ContentValidationReport report)
        {
            var endings = new List<ContentItem>();
            foreach (var ending in database.FindByType("ending"))
            {
                endings.Add(ending);
            }

            if (endings.Count == 0)
            {
                report.Add("ENDING_MISSING", "narrative/endings.json", 1, "At least one ending is required.");
                return;
            }

            var anyPossible = false;
            for (var endingIndex = 0; endingIndex < endings.Count; endingIndex++)
            {
                var ending = endings[endingIndex];
                var conditions = ending.GetArray("conditions");
                if (conditions == null || conditions.Count == 0)
                {
                    Add(report, "ENDING_CONDITIONS_REQUIRED", ending, "Endings must state their conditions.");
                    continue;
                }

                var canBeMet = true;
                for (var conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
                {
                    var condition = ContentValues.AsObject(conditions[conditionIndex]);
                    if (condition == null || !ConditionCanBeMet(condition, database))
                    {
                        canBeMet = false;
                        break;
                    }
                }

                anyPossible |= canBeMet;
            }

            if (!anyPossible)
            {
                report.Add("ENDING_UNREACHABLE", "narrative/endings.json", 1, "Every ending has an impossible condition set.");
            }
        }

        private static bool ConditionCanBeMet(IDictionary<string, object> condition, ContentDatabase database)
        {
            var type = ContentValues.GetString(condition, "type");
            if (!KnownConditionTypes.Contains(type))
            {
                return false;
            }

            if (string.Equals(type, "item_crafted", StringComparison.Ordinal)
                || string.Equals(type, "recipe_unlocked", StringComparison.Ordinal)
                || string.Equals(type, "floor_first_reached", StringComparison.Ordinal)
                || string.Equals(type, "upgrade_at_least", StringComparison.Ordinal)
                || string.Equals(type, "flag_set", StringComparison.Ordinal)
                || string.Equals(type, "flag_not_set", StringComparison.Ordinal))
            {
                var id = ContentValues.GetString(condition, "id");
                ContentItem ignored;
                return database.TryGet(id, out ignored) || (string.Equals(type, "flag_set", StringComparison.Ordinal) || string.Equals(type, "flag_not_set", StringComparison.Ordinal)) && HasContentPrefix(id);
            }

            return ContentValues.GetNumber(condition, "value", 0d) >= 0d;
        }

        private static bool IsLowerSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsLower(value[0]))
            {
                return false;
            }

            for (var characterIndex = 1; characterIndex < value.Length; characterIndex++)
            {
                var character = value[characterIndex];
                if (!(char.IsLower(character) || char.IsDigit(character) || character == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasContentPrefix(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (var prefixIndex = 0; prefixIndex < ContentPrefixes.Length; prefixIndex++)
            {
                if (id.StartsWith(ContentPrefixes[prefixIndex], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Add(ContentValidationReport report, string code, ContentItem item, string message)
        {
            report.Add(code, item.SourceFile, item.SourceLine, message);
        }
    }
}
