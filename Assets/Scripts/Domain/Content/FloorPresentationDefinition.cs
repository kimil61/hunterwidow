using System;
using System.Collections.Generic;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// Content-only world geometry and presentation values. Unity converts these
    /// neutral values into scene objects, keeping floors and spawns editable in JSON.
    /// </summary>
    public sealed class ContentPoint
    {
        public ContentPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public static ContentPoint FromValue(object value, string fieldName)
        {
            var values = ContentValues.AsArray(value);
            if (values == null || values.Count != 2 || !(values[0] is double) || !(values[1] is double))
            {
                throw new InvalidOperationException(fieldName + " must be a two-number array.");
            }

            return new ContentPoint((double)values[0], (double)values[1]);
        }
    }

    public sealed class ContentColor
    {
        public ContentColor(double red, double green, double blue, double alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public double Red { get; }

        public double Green { get; }

        public double Blue { get; }

        public double Alpha { get; }

        public static ContentColor FromValue(object value, string fieldName)
        {
            var values = ContentValues.AsArray(value);
            if (values == null || (values.Count != 3 && values.Count != 4))
            {
                throw new InvalidOperationException(fieldName + " must be a three- or four-number color array.");
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (!(values[valueIndex] is double))
                {
                    throw new InvalidOperationException(fieldName + " must contain only numbers.");
                }
            }

            return new ContentColor(
                (double)values[0],
                (double)values[1],
                (double)values[2],
                values.Count == 4 ? (double)values[3] : 1d);
        }
    }

    public sealed class FloorSpawnDefinition
    {
        public FloorSpawnDefinition(ContentPoint position, IReadOnlyList<string> candidateIds)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            CandidateIds = candidateIds ?? throw new ArgumentNullException(nameof(candidateIds));
        }

        public ContentPoint Position { get; }

        public IReadOnlyList<string> CandidateIds { get; }
    }

    public sealed class FloorVisualDefinition
    {
        public FloorVisualDefinition(
            ContentColor background,
            ContentColor ground,
            ContentColor path,
            ContentColor rope,
            ContentColor purifier,
            ContentColor descent,
            ContentColor player,
            ContentColor waveForward,
            ContentColor waveReturn)
        {
            Background = background;
            Ground = ground;
            Path = path;
            Rope = rope;
            Purifier = purifier;
            Descent = descent;
            Player = player;
            WaveForward = waveForward;
            WaveReturn = waveReturn;
        }

        public ContentColor Background { get; }

        public ContentColor Ground { get; }

        public ContentColor Path { get; }

        public ContentColor Rope { get; }

        public ContentColor Purifier { get; }

        public ContentColor Descent { get; }

        public ContentColor Player { get; }

        public ContentColor WaveForward { get; }

        public ContentColor WaveReturn { get; }
    }

    /// <summary>
    /// Optional, data-owned progression gate for entering a floor. Unity supplies
    /// only the interaction; the floor data decides which upgrade is required.
    /// </summary>
    public sealed class FloorAccessRequirement
    {
        public FloorAccessRequirement(string upgradeAxisId, int minimumLevel, string nameKey)
        {
            UpgradeAxisId = upgradeAxisId;
            MinimumLevel = minimumLevel;
            NameKey = nameKey;
        }

        public string UpgradeAxisId { get; }

        public int MinimumLevel { get; }

        public string NameKey { get; }

        public bool IsMet(int currentLevel)
        {
            return currentLevel >= MinimumLevel;
        }

        public static FloorAccessRequirement FromContent(ContentItem floor)
        {
            if (floor == null || !string.Equals(floor.Type, "floor", StringComparison.Ordinal))
            {
                throw new ArgumentException("A floor content item is required.", nameof(floor));
            }

            var rawRequirement = floor.GetObject("accessRequirement");
            if (rawRequirement == null)
            {
                return null;
            }

            var axisId = ContentValues.GetString(rawRequirement, "upgradeAxisId");
            var minimumLevel = (int)ContentValues.GetNumber(rawRequirement, "minimumLevel");
            var nameKey = ContentValues.GetString(rawRequirement, "nameKey");
            if (string.IsNullOrEmpty(axisId) || minimumLevel <= 0 || string.IsNullOrEmpty(nameKey))
            {
                throw new InvalidOperationException(floor.Id + ".accessRequirement requires upgradeAxisId, a positive minimumLevel, and nameKey.");
            }

            return new FloorAccessRequirement(axisId, minimumLevel, nameKey);
        }
    }

    public sealed class FloorLayoutDefinition
    {
        public FloorLayoutDefinition(
            FloorVisualDefinition visual,
            ContentPoint playerStart,
            IReadOnlyList<ContentPoint> ropeCandidates,
            int activeRopeCount,
            IReadOnlyList<ContentPoint> purifierPositions,
            ContentPoint descentPosition,
            IReadOnlyList<FloorSpawnDefinition> enemySpawns,
            IReadOnlyList<FloorSpawnDefinition> gatherSpawns)
        {
            Visual = visual ?? throw new ArgumentNullException(nameof(visual));
            PlayerStart = playerStart ?? throw new ArgumentNullException(nameof(playerStart));
            RopeCandidates = ropeCandidates ?? throw new ArgumentNullException(nameof(ropeCandidates));
            ActiveRopeCount = activeRopeCount;
            PurifierPositions = purifierPositions ?? throw new ArgumentNullException(nameof(purifierPositions));
            DescentPosition = descentPosition;
            EnemySpawns = enemySpawns ?? throw new ArgumentNullException(nameof(enemySpawns));
            GatherSpawns = gatherSpawns ?? throw new ArgumentNullException(nameof(gatherSpawns));
        }

        public FloorVisualDefinition Visual { get; }

        public ContentPoint PlayerStart { get; }

        public IReadOnlyList<ContentPoint> RopeCandidates { get; }

        public int ActiveRopeCount { get; }

        public IReadOnlyList<ContentPoint> PurifierPositions { get; }

        public ContentPoint DescentPosition { get; }

        public IReadOnlyList<FloorSpawnDefinition> EnemySpawns { get; }

        public IReadOnlyList<FloorSpawnDefinition> GatherSpawns { get; }

        public static FloorLayoutDefinition FromContent(ContentItem floor)
        {
            if (floor == null || !string.Equals(floor.Type, "floor", StringComparison.Ordinal))
            {
                throw new ArgumentException("A floor content item is required.", nameof(floor));
            }

            var visual = RequireObject(floor.GetObject("visual"), floor.Id + ".visual");
            var layout = RequireObject(floor.GetObject("layout"), floor.Id + ".layout");
            return new FloorLayoutDefinition(
                new FloorVisualDefinition(
                    ContentColor.FromValue(GetRequired(visual, "backgroundColor"), floor.Id + ".visual.backgroundColor"),
                    ContentColor.FromValue(GetRequired(visual, "groundColor"), floor.Id + ".visual.groundColor"),
                    ContentColor.FromValue(GetRequired(visual, "pathColor"), floor.Id + ".visual.pathColor"),
                    ContentColor.FromValue(GetRequired(visual, "ropeColor"), floor.Id + ".visual.ropeColor"),
                    ContentColor.FromValue(GetRequired(visual, "purifierColor"), floor.Id + ".visual.purifierColor"),
                    ContentColor.FromValue(GetRequired(visual, "descentColor"), floor.Id + ".visual.descentColor"),
                    ContentColor.FromValue(GetRequired(visual, "playerColor"), floor.Id + ".visual.playerColor"),
                    ContentColor.FromValue(GetRequired(visual, "waveForwardColor"), floor.Id + ".visual.waveForwardColor"),
                    ContentColor.FromValue(GetRequired(visual, "waveReturnColor"), floor.Id + ".visual.waveReturnColor")),
                ContentPoint.FromValue(GetRequired(layout, "playerStart"), floor.Id + ".layout.playerStart"),
                ReadPoints(layout, "ropeCandidates", floor.Id),
                (int)ContentValues.GetNumber(layout, "activeRopeCount"),
                ReadPoints(layout, "purifierPositions", floor.Id),
                ReadOptionalPoint(layout, "descentPosition", floor.Id),
                ReadSpawns(layout, "enemySpawns", floor.Id),
                ReadSpawns(layout, "gatherSpawns", floor.Id));
        }

        private static IDictionary<string, object> RequireObject(IDictionary<string, object> value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException(fieldName + " must be an object.");
            }

            return value;
        }

        private static object GetRequired(IDictionary<string, object> value, string fieldName)
        {
            object result;
            if (!value.TryGetValue(fieldName, out result))
            {
                throw new InvalidOperationException(fieldName + " is required.");
            }

            return result;
        }

        private static ContentPoint ReadOptionalPoint(IDictionary<string, object> layout, string fieldName, string floorId)
        {
            object value;
            return layout.TryGetValue(fieldName, out value) ? ContentPoint.FromValue(value, floorId + ".layout." + fieldName) : null;
        }

        private static IReadOnlyList<ContentPoint> ReadPoints(IDictionary<string, object> layout, string fieldName, string floorId)
        {
            object value;
            var rawPoints = layout.TryGetValue(fieldName, out value) ? ContentValues.AsArray(value) : null;
            if (rawPoints == null)
            {
                throw new InvalidOperationException(floorId + ".layout." + fieldName + " must be an array.");
            }

            var points = new List<ContentPoint>();
            for (var pointIndex = 0; pointIndex < rawPoints.Count; pointIndex++)
            {
                points.Add(ContentPoint.FromValue(rawPoints[pointIndex], floorId + ".layout." + fieldName));
            }

            return points;
        }

        private static IReadOnlyList<FloorSpawnDefinition> ReadSpawns(IDictionary<string, object> layout, string fieldName, string floorId)
        {
            object value;
            var rawSpawns = layout.TryGetValue(fieldName, out value) ? ContentValues.AsArray(value) : null;
            if (rawSpawns == null)
            {
                throw new InvalidOperationException(floorId + ".layout." + fieldName + " must be an array.");
            }

            var spawns = new List<FloorSpawnDefinition>();
            for (var spawnIndex = 0; spawnIndex < rawSpawns.Count; spawnIndex++)
            {
                var spawn = ContentValues.AsObject(rawSpawns[spawnIndex]);
                if (spawn == null)
                {
                    throw new InvalidOperationException(floorId + ".layout." + fieldName + " entries must be objects.");
                }

                spawns.Add(new FloorSpawnDefinition(
                    ContentPoint.FromValue(GetRequired(spawn, "position"), floorId + ".layout." + fieldName + ".position"),
                    ReadIds(spawn, floorId + ".layout." + fieldName + ".candidateIds")));
            }

            return spawns;
        }

        private static IReadOnlyList<string> ReadIds(IDictionary<string, object> spawn, string fieldName)
        {
            object value;
            var rawIds = spawn.TryGetValue("candidateIds", out value) ? ContentValues.AsArray(value) : null;
            if (rawIds == null || rawIds.Count == 0)
            {
                throw new InvalidOperationException(fieldName + " must be a non-empty array.");
            }

            var ids = new List<string>();
            for (var idIndex = 0; idIndex < rawIds.Count; idIndex++)
            {
                var id = rawIds[idIndex] as string;
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException(fieldName + " must contain non-empty IDs.");
                }

                ids.Add(id);
            }

            return ids;
        }
    }

    public sealed class ErosionBandPresentationDefinition
    {
        public ErosionBandPresentationDefinition(
            string id,
            ContentColor overlayColor,
            double overlayAmount,
            double ambiencePitch,
            double ambienceLowPassCutoff,
            double ambienceReverbLevel)
        {
            Id = id;
            OverlayColor = overlayColor;
            OverlayAmount = overlayAmount;
            AmbiencePitch = ambiencePitch;
            AmbienceLowPassCutoff = ambienceLowPassCutoff;
            AmbienceReverbLevel = ambienceReverbLevel;
        }

        public string Id { get; }

        public ContentColor OverlayColor { get; }

        public double OverlayAmount { get; }

        public double AmbiencePitch { get; }

        public double AmbienceLowPassCutoff { get; }

        public double AmbienceReverbLevel { get; }

        public static ErosionBandPresentationDefinition FromValue(object value)
        {
            var rawBand = ContentValues.AsObject(value);
            if (rawBand == null)
            {
                throw new InvalidOperationException("Erosion band must be an object.");
            }

            object overlayColor;
            if (!rawBand.TryGetValue("overlayColor", out overlayColor))
            {
                throw new InvalidOperationException("Erosion band overlayColor is required.");
            }

            return new ErosionBandPresentationDefinition(
                ContentValues.GetString(rawBand, "id"),
                ContentColor.FromValue(overlayColor, "erosion band overlayColor"),
                RequireNumber(rawBand, "overlayAmount"),
                RequireNumber(rawBand, "ambiencePitch"),
                RequireNumber(rawBand, "ambienceLowPassCutoff"),
                RequireNumber(rawBand, "ambienceReverbLevel"));
        }

        private static double RequireNumber(IDictionary<string, object> values, string fieldName)
        {
            object value;
            if (!values.TryGetValue(fieldName, out value) || !(value is double))
            {
                throw new InvalidOperationException("Erosion band " + fieldName + " is required.");
            }

            return (double)value;
        }
    }

    public sealed class TutorialDefinition
    {
        public TutorialDefinition(
            string floorId,
            string completionFlagId,
            string targetEnemyId,
            IReadOnlyList<ContentPoint> targetPositions,
            IReadOnlyList<string> introTextKeys)
        {
            FloorId = floorId;
            CompletionFlagId = completionFlagId;
            TargetEnemyId = targetEnemyId;
            TargetPositions = targetPositions ?? throw new ArgumentNullException(nameof(targetPositions));
            IntroTextKeys = introTextKeys ?? throw new ArgumentNullException(nameof(introTextKeys));
        }

        public string FloorId { get; }

        public string CompletionFlagId { get; }

        public string TargetEnemyId { get; }

        public IReadOnlyList<ContentPoint> TargetPositions { get; }

        public IReadOnlyList<string> IntroTextKeys { get; }

        public static TutorialDefinition FromValue(object value)
        {
            var rawTutorial = ContentValues.AsObject(value);
            if (rawTutorial == null)
            {
                return null;
            }

            var rawPositions = ContentValues.GetArray(rawTutorial, "targetPositions");
            if (rawPositions == null || rawPositions.Count == 0)
            {
                throw new InvalidOperationException("Tutorial targetPositions must be a non-empty array.");
            }

            var positions = new List<ContentPoint>();
            for (var positionIndex = 0; positionIndex < rawPositions.Count; positionIndex++)
            {
                positions.Add(ContentPoint.FromValue(rawPositions[positionIndex], "tutorial.targetPositions"));
            }

            var floorId = ContentValues.GetString(rawTutorial, "floorId");
            var flagId = ContentValues.GetString(rawTutorial, "completionFlagId");
            var targetEnemyId = ContentValues.GetString(rawTutorial, "targetEnemyId");
            var introTextKeys = ReadTextKeys(rawTutorial, "introTextKeys");
            if (string.IsNullOrEmpty(floorId)
                || string.IsNullOrEmpty(flagId)
                || string.IsNullOrEmpty(targetEnemyId)
                || positions.Count != 3
                || introTextKeys.Count != 3)
            {
                throw new InvalidOperationException("Tutorial requires IDs plus exactly three target positions and three intro text keys.");
            }

            return new TutorialDefinition(floorId, flagId, targetEnemyId, positions, introTextKeys);
        }

        private static IReadOnlyList<string> ReadTextKeys(IDictionary<string, object> values, string fieldName)
        {
            var rawKeys = ContentValues.GetArray(values, fieldName);
            if (rawKeys == null)
            {
                throw new InvalidOperationException("Tutorial " + fieldName + " must be an array.");
            }

            var keys = new List<string>();
            for (var keyIndex = 0; keyIndex < rawKeys.Count; keyIndex++)
            {
                var key = rawKeys[keyIndex] as string;
                if (string.IsNullOrEmpty(key))
                {
                    throw new InvalidOperationException("Tutorial " + fieldName + " must contain non-empty keys.");
                }

                keys.Add(key);
            }

            return keys;
        }
    }
}
