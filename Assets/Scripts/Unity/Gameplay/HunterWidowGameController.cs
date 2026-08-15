using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HunterWidow.Domain.Alchemy;
using HunterWidow.Domain.Combat;
using HunterWidow.Domain.Common;
using HunterWidow.Domain.Content;
using HunterWidow.Domain.Cycle;
using HunterWidow.Domain.Dive;
using HunterWidow.Domain.Enemy;
using HunterWidow.Domain.Economy;
using HunterWidow.Domain.Erosion;
using HunterWidow.Domain.Inventory;
using HunterWidow.Domain.Narrative;
using HunterWidow.Domain.Persistence;
using HunterWidow.Domain.Progression;
using HunterWidow.Domain.Rng;
using HunterWidow.Unity.Content;
using HunterWidow.Unity.Presentation;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace HunterWidow.Unity.Gameplay
{
    public sealed class HunterWidowGameController : MonoBehaviour
    {
        private enum GameScreen
        {
            Title,
            Village,
            Dive,
            Result,
            Pharmacy,
            Workbench,
            Gallery,
            Options,
            Ending
        }

        private enum PharmacyTab
        {
            Craft,
            Sell,
            Use,
            Facility
        }

        private const float UiPadding = 18f;
        private const int TuningWindowId = 91340;
        private const string SavePathVariable = "HUNTERWIDOW_SAVE_PATH";
        private const string TelemetryPathVariable = "HUNTERWIDOW_TELEMETRY_PATH";
        private const string AudioMixerResourceName = "HunterWidowMixer";
        private const string BgmMixerGroupName = "BGM";
        private const string SfxMixerGroupName = "SFX";
        private const string AmbienceMixerGroupName = "Ambience";

        private readonly List<WorldEnemy> enemies = new List<WorldEnemy>();
        private readonly List<WorldPickup> pickups = new List<WorldPickup>();
        private readonly List<WorldMarker> ropeMarkers = new List<WorldMarker>();
        private readonly List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        private readonly Dictionary<string, RecipeDefinition> recipesById = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        private readonly List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        private readonly string[] tuningLabelKeys =
        {
            "ui.tuning.min_charge", "ui.tuning.sweet_start", "ui.tuning.sweet_end", "ui.tuning.max_charge",
            "ui.tuning.max_range", "ui.tuning.wave_speed", "ui.tuning.return_speed", "ui.tuning.damage",
            "ui.tuning.late_damage_multiplier", "ui.tuning.return_damage_multiplier", "ui.tuning.wave_timeout", "ui.tuning.charge_move_multiplier"
        };

        private ContentLoadResult content;
        private GameContentCatalog catalog;
        private ContentLocalizer localizer;
        private ProgressionState progression;
        private ItemLedger townInventory;
        private AffinityLogic affinity;
        private UpgradeEffectRegistry upgradeEffects;
        private UpgradeLogic upgradeLogic;
        private CauldronLogic cauldron;
        private CycleSession cycle;
        private StoryDirector storyDirector;
        private NarrativeExecutionContext narrativeContext;
        private SaveComposer saveComposer;
        private TuningOverrideStore tuningStore;
        private CombatTelemetryLogStore telemetryStore;
        private CombatTuning defaultTuning;
        private CombatTuning tuning;
        private ChargeLogic charge;
        private SwordWaveLogic wave;
        private CombatTelemetry telemetry;
        private string telemetrySessionId;
        private bool telemetryLogUnavailable;
        private DiveSession dive;
        private SeededRng lootRng;
        private FloorLayoutDefinition currentFloorLayout;
        private DiveResult lastDiveResult;
        private IReadOnlyList<StoryEventDefinition> lastStoryEvents = new List<StoryEventDefinition>();
        private IReadOnlyList<StoryEventDefinition> storyQueue = new List<StoryEventDefinition>();
        private IReadOnlyList<string> tutorialIntroKeys = new List<string>();
        private int storyQueueIndex;
        private int tutorialIntroIndex;
        private GameScreen screen;
        private GameScreen optionsReturnScreen;
        private PharmacyTab pharmacyTab;
        private Camera worldCamera;
        private Transform worldRoot;
        private Transform player;
        private SpriteRenderer waveRenderer;
        private AudioSource audioSource;
        private AudioSource bgmSource;
        private AudioSource ambienceSource;
        private AudioMixer audioMixer;
        private AudioMixerGroup bgmMixerGroup;
        private AudioMixerGroup sfxMixerGroup;
        private AudioMixerGroup ambienceMixerGroup;
        private AudioLowPassFilter ambienceLowPassFilter;
        private AudioReverbFilter ambienceReverbFilter;
        private AudioClip sweetTone;
        private AudioClip lateTone;
        private AudioClip returnTone;
        private AudioClip cancelTone;
        private AudioClip hitTone;
        private string currentFloorId;
        private string currentEndingId;
        private string statusKey;
        private string statusFallback;
        private GameObject pendingReplacementObject;
        private string pendingReplacementItemId;
        private int pendingReplacementCount;
        private float gatherProgress;
        private WorldGatherable activeGatherable;
        private float hitStopRemaining;
        private float shakeRemaining;
        private float shakeAmount;
        private ChargeOutcome? activeWaveOutcome;
        private int tutorialTargetsRemaining;
        private Vector3 cameraRestPosition;
        private float bgmVolume = 0.7f;
        private float sfxVolume = 0.7f;
        private float ambienceVolume = 0.7f;
        private int resolutionWidth;
        private int resolutionHeight;
        private int resolutionIndex;
        private bool showTuning;
        private Rect tuningWindow = new Rect(28f, 58f, 550f, 670f);
        private string[] tuningValues;

        private void Start()
        {
            var bootstrap = GetComponent<HunterWidowBootstrap>();
            if (bootstrap == null || bootstrap.CurrentContent == null || bootstrap.CurrentContent.Report.HasErrors)
            {
                enabled = false;
                return;
            }

            content = bootstrap.CurrentContent;
            catalog = new GameContentCatalog(content.Database);
            localizer = new ContentLocalizer(Path.Combine(content.RootPath, "locale", "strings.csv"), catalog.GetDefaultLocale());
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = sfxVolume;
            CreateModels();
            CreateAudioBuses();
            resolutionWidth = UnityEngine.Screen.width;
            resolutionHeight = UnityEngine.Screen.height;
            screen = GameScreen.Title;
            UpdateBgmForCurrentScreen();
            SetStatus("ui.status.title", "");
        }

        private void Update()
        {
            UpdateBgmForCurrentScreen();
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && screen == GameScreen.Dive)
            {
                showTuning = !showTuning;
            }

            if (screen == GameScreen.Dive)
            {
                UpdateDive(Time.unscaledDeltaTime);
            }
        }

        private void OnGUI()
        {
            if (catalog == null)
            {
                return;
            }

            switch (screen)
            {
                case GameScreen.Title:
                    DrawTitle();
                    break;
                case GameScreen.Village:
                    DrawVillage();
                    break;
                case GameScreen.Dive:
                    DrawDiveHud();
                    break;
                case GameScreen.Result:
                    DrawResult();
                    break;
                case GameScreen.Pharmacy:
                    DrawPharmacy();
                    break;
                case GameScreen.Workbench:
                    DrawWorkbench();
                    break;
                case GameScreen.Gallery:
                    DrawGallery();
                    break;
                case GameScreen.Options:
                    DrawOptions();
                    break;
                case GameScreen.Ending:
                    DrawEnding();
                    break;
            }

            if (screen == GameScreen.Dive && showTuning)
            {
                tuningWindow = GUI.Window(TuningWindowId, tuningWindow, DrawTuningWindow, T("ui.tuning.title"));
            }

            if (tutorialIntroIndex < tutorialIntroKeys.Count)
            {
                DrawTutorialIntroOverlay();
            }
            else if (storyQueueIndex < storyQueue.Count)
            {
                DrawStoryOverlay();
            }
        }

        private void CreateModels()
        {
            progression = new ProgressionState();
            townInventory = new ItemLedger();
            affinity = new AffinityLogic(catalog.GetCgThresholds());
            upgradeEffects = catalog.CreateUpgradeEffectRegistry();
            upgradeLogic = new UpgradeLogic();
            cauldron = new CauldronLogic((int)upgradeEffects.GetValue("cauldron_slots"));
            cycle = new CycleSession(progression, townInventory, cauldron);
            storyDirector = new StoryDirector(catalog.GetStoryEvents());
            narrativeContext = new NarrativeExecutionContext(progression, townInventory, affinity);
            recipes.Clear();
            recipesById.Clear();
            var recipeValues = catalog.GetRecipes();
            for (var recipeIndex = 0; recipeIndex < recipeValues.Count; recipeIndex++)
            {
                recipes.Add(recipeValues[recipeIndex]);
                recipesById.Add(recipeValues[recipeIndex].Id, recipeValues[recipeIndex]);
            }

            upgrades.Clear();
            var upgradeValues = catalog.GetUpgrades();
            for (var upgradeIndex = 0; upgradeIndex < upgradeValues.Count; upgradeIndex++)
            {
                upgrades.Add(upgradeValues[upgradeIndex]);
            }

            var knownIds = new List<string>(catalog.GetAllKnownIds());
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                if (!knownIds.Contains(upgrades[upgradeIndex].AxisId))
                {
                    knownIds.Add(upgrades[upgradeIndex].AxisId);
                }
            }

            saveComposer = new SaveComposer(knownIds);
            defaultTuning = catalog.CreateCombatTuning();
            tuningStore = new TuningOverrideStore(Path.Combine(Application.persistentDataPath, "tuning", "combat.json"));
            telemetryStore = new CombatTelemetryLogStore(GetTelemetryPath());
            telemetry = null;
            telemetrySessionId = null;
            telemetryLogUnavailable = false;
            tuning = tuningStore.Load(defaultTuning);
            tuningValues = BuildTuningValues(tuning);
            CreateTones();
        }

        private void StartNewGame()
        {
            DestroyWorld();
            CreateModels();
            BeginTutorialIntro();
            progression.UnlockFloor(catalog.GetProgressionConfig().GetString("startingFloorId"));
            progression.UnlockRecipe(catalog.GetProgressionConfig().GetString("startingRecipeId"));
            SaveGame();
            screen = GameScreen.Village;
            BuildVillageWorld();
            SetStatus("ui.status.new_game", "");
        }

        private void ContinueGame()
        {
            DestroyWorld();
            CreateModels();
            tutorialIntroKeys = new List<string>();
            tutorialIntroIndex = 0;
            var loaded = saveComposer.Load(GetSavePath());
            progression.Restore(
                loaded.State.Gold,
                loaded.State.Affinity,
                loaded.State.CycleCount,
                loaded.State.TriggeredEndingId,
                loaded.State.UpgradeLevels,
                loaded.State.UnlockedRecipes,
                loaded.State.UnlockedCgs,
                loaded.State.UnlockedFloors,
                loaded.State.CraftedItems,
                loaded.State.Flags);
            townInventory.ReplaceWith(loaded.State.Inventory);
            affinity = new AffinityLogic(catalog.GetCgThresholds(), loaded.State.Affinity);
            var affinityCgs = affinity.Add(0);
            for (var cgIndex = 0; cgIndex < affinityCgs.Count; cgIndex++)
            {
                progression.UnlockCg(affinityCgs[cgIndex]);
            }
            narrativeContext = new NarrativeExecutionContext(progression, townInventory, affinity);
            upgrades.Sort((left, right) =>
            {
                var levelComparison = left.Level.CompareTo(right.Level);
                return levelComparison != 0 ? levelComparison : string.CompareOrdinal(left.Id, right.Id);
            });
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                upgradeLogic.RestorePurchase(upgrades[upgradeIndex], progression, upgradeEffects);
            }

            cauldron.SetSlotCapacity((int)upgradeEffects.GetValue("cauldron_slots"));
            cauldron.RestoreJobs(loaded.State.CauldronJobs, FindRecipeById);
            if (!progression.HasFloor(catalog.GetProgressionConfig().GetString("startingFloorId")))
            {
                progression.UnlockFloor(catalog.GetProgressionConfig().GetString("startingFloorId"));
                progression.UnlockRecipe(catalog.GetProgressionConfig().GetString("startingRecipeId"));
                SaveGame();
            }

            screen = GameScreen.Village;
            BuildVillageWorld();
            SetStatus(loaded.RecoveredFromInvalidSave ? "ui.status.save_recovered" : "ui.status.continued", "");
        }

        private void BuildVillageWorld()
        {
            DestroyWorld();
            SetupCamera(new Color(0.11f, 0.09f, 0.16f, 1f));
            CreateSpriteObject("VillageGround", new Color(0.2f, 0.18f, 0.16f, 1f), Vector2.zero, new Vector2(28f, 16f), -10, false);
            CreateSpriteObject("Apothecary", new Color(0.6f, 0.36f, 0.65f, 1f), new Vector2(-5f, 1f), new Vector2(3f, 3f), 0, false);
            CreateSpriteObject("Workbench", new Color(0.55f, 0.35f, 0.16f, 1f), new Vector2(0f, -2f), new Vector2(3f, 2f), 0, false);
            CreateSpriteObject("MountainRoad", new Color(0.23f, 0.52f, 0.28f, 1f), new Vector2(6f, 1f), new Vector2(4f, 4f), 0, false);
        }

        private void EnterDive(string floorId)
        {
            if (!CanAccessFloor(floorId, true))
            {
                return;
            }

            currentFloorId = floorId;
            var erosion = catalog.CreateErosionSettings(floorId, upgradeEffects.GetValue("erosion_decay_rate"));
            var backpack = new BackpackLogic((int)upgradeEffects.GetValue("backpack_slots"));
            dive = new DiveSession(
                new ErosionLogic(erosion),
                backpack,
                new SeededRng((uint)(progression.CycleCount + 1)),
                catalog.GetInventoryConfig().GetNumber("forcedLossFraction"));
            dive.Finished += HandleDiveFinished;
            dive.BandChanged += HandleErosionBandChanged;
            lootRng = new SeededRng((uint)(progression.CycleCount + floorId.Length + 1));
            var floorLayout = catalog.GetFloorLayout(floorId);
            dive.Start(floorId, new Vec2(floorLayout.PlayerStart.X, floorLayout.PlayerStart.Y));
            BuildDiveWorld();
            BeginCombatTelemetrySession("dive_started");
            CreateCombatLogic();
            screen = GameScreen.Dive;
            SetStatus("ui.status.dive_started", GetDisplayName(floorId));
            if (IsTutorialDive())
            {
                SetStatus("ui.status.tutorial_sweet_required", "");
            }
        }

        private void BuildDiveWorld()
        {
            DestroyWorld();
            currentFloorLayout = catalog.GetFloorLayout(currentFloorId);
            var visual = currentFloorLayout.Visual;
            SetupCamera(ToUnityColor(visual.Background));
            CreateSpriteObject("DiveGround", ToUnityColor(visual.Ground), Vector2.zero, new Vector2(30f, 18f), -10, false);
            CreateSpriteObject("DivePath", ToUnityColor(visual.Path), Vector2.zero, new Vector2(24f, 3f), -9, false);
            player = CreateSpriteObject("Hunter", ToUnityColor(visual.Player), ToUnityPoint(currentFloorLayout.PlayerStart), new Vector2(0.85f, 0.85f), 2, true).transform;
            waveRenderer = CreateSpriteObject("SwordWave", ToUnityColor(visual.WaveForward), Vector2.zero, new Vector2(0.65f, 0.65f), 3, true).GetComponent<SpriteRenderer>();
            waveRenderer.gameObject.SetActive(false);
            CreateWorldMarkers(currentFloorLayout);
            CreateEnemiesForFloor(currentFloorLayout);
            CreateGatherables(currentFloorLayout);
            if (dive != null)
            {
                HandleErosionBandChanged(dive.GetState().Erosion.BandId);
            }
        }

        private void CreateWorldMarkers(FloorLayoutDefinition layout)
        {
            var activeRopeCount = Mathf.Min(layout.ActiveRopeCount, layout.RopeCandidates.Count);
            var ropeOffset = layout.RopeCandidates.Count == 0 ? 0 : progression.CycleCount % layout.RopeCandidates.Count;
            for (var ropeIndex = 0; ropeIndex < activeRopeCount; ropeIndex++)
            {
                var candidateIndex = (ropeOffset + ropeIndex) % layout.RopeCandidates.Count;
                var rope = CreateSpriteObject("ExtractionRope_" + ropeIndex, ToUnityColor(layout.Visual.Rope), ToUnityPoint(layout.RopeCandidates[candidateIndex]), new Vector2(0.7f, 2f), 0, true);
                var marker = rope.AddComponent<WorldMarker>();
                marker.Initialize(WorldMarkerKind.Extract, string.Empty, 0f);
                ropeMarkers.Add(marker);
            }

            for (var purifierIndex = 0; purifierIndex < layout.PurifierPositions.Count; purifierIndex++)
            {
                var purifier = CreateSpriteObject("PurificationNode_" + purifierIndex, ToUnityColor(layout.Visual.Purifier), ToUnityPoint(layout.PurifierPositions[purifierIndex]), new Vector2(0.9f, 0.9f), 0, true);
                purifier.AddComponent<WorldMarker>().Initialize(WorldMarkerKind.Purify, string.Empty, (float)catalog.GetErosionConfig().GetNumber("purifyAmount"));
            }

            var nextFloor = catalog.GetNextFloorId(currentFloorId);
            if (!string.IsNullOrEmpty(nextFloor) && layout.DescentPosition != null)
            {
                var descent = CreateSpriteObject("Descent", ToUnityColor(layout.Visual.Descent), ToUnityPoint(layout.DescentPosition), new Vector2(1.2f, 1.2f), 0, true);
                descent.AddComponent<WorldMarker>().Initialize(WorldMarkerKind.Descend, nextFloor, 0f);
            }
        }

        private void CreateEnemiesForFloor(FloorLayoutDefinition layout)
        {
            if (CreateTutorialTargets())
            {
                return;
            }

            var enemyPool = catalog.GetSpawnableEnemiesForFloor(currentFloorId);
            if (enemyPool.Count == 0)
            {
                return;
            }

            for (var spawnIndex = 0; spawnIndex < layout.EnemySpawns.Count; spawnIndex++)
            {
                var spawn = layout.EnemySpawns[spawnIndex];
                var selection = (progression.CycleCount + spawnIndex) % enemyPool.Count;
                CreateWorldEnemy(enemyPool[selection], spawn.Position, spawnIndex, false);
            }
        }

        private bool CreateTutorialTargets()
        {
            var tutorial = catalog.GetTutorialDefinition();
            if (tutorial == null
                || progression.HasFlag(tutorial.CompletionFlagId)
                || !string.Equals(currentFloorId, tutorial.FloorId, StringComparison.Ordinal))
            {
                return false;
            }

            ContentItem definition;
            if (!content.Database.TryGet(tutorial.TargetEnemyId, out definition))
            {
                return false;
            }

            tutorialTargetsRemaining = 0;
            for (var targetIndex = 0; targetIndex < tutorial.TargetPositions.Count; targetIndex++)
            {
                CreateWorldEnemy(definition, tutorial.TargetPositions[targetIndex], targetIndex, true);
                tutorialTargetsRemaining++;
            }

            return tutorialTargetsRemaining > 0;
        }

        private void CreateWorldEnemy(ContentItem definition, ContentPoint position, int instanceIndex, bool isTutorialTarget)
        {
            var objectInstance = CreateSpriteObject(definition.Id + "_" + instanceIndex, ToUnityColor(ContentColor.FromValue(definition.GetArray("tint"), definition.Id + ".tint")), ToUnityPoint(position), new Vector2(0.95f, 0.95f), 1, true);
            var enemy = objectInstance.AddComponent<WorldEnemy>();
            var parameters = definition.GetObject("params");
            enemy.Initialize(
                definition.Id + "_" + instanceIndex,
                definition.GetString("behavior"),
                definition.GetString("dropTableId"),
                (float)(double)parameters["maxHealth"],
                (float)(double)parameters["moveSpeed"],
                (float)(double)parameters["contactDamage"],
                (float)(double)parameters["wanderDistance"],
                (float)(double)parameters["wanderMoveMultiplier"],
                (float)(double)parameters["retreatDistance"],
                (float)catalog.GetCombatConfig().GetNumber("contactIntervalSeconds"),
                isTutorialTarget);
            enemies.Add(enemy);
        }

        private bool IsTutorialDive()
        {
            var tutorial = catalog.GetTutorialDefinition();
            return tutorial != null
                && !progression.HasFlag(tutorial.CompletionFlagId)
                && string.Equals(currentFloorId, tutorial.FloorId, StringComparison.Ordinal);
        }

        private void CreateGatherables(FloorLayoutDefinition layout)
        {
            for (var spawnIndex = 0; spawnIndex < layout.GatherSpawns.Count; spawnIndex++)
            {
                var spawn = layout.GatherSpawns[spawnIndex];
                ContentItem material;
                if (!content.Database.TryGet(SelectCandidateId(spawn.CandidateIds, spawnIndex), out material))
                {
                    continue;
                }

                var objectInstance = CreateSpriteObject("Gather_" + material.Id + "_" + spawnIndex, ToUnityColor(ContentColor.FromValue(material.GetArray("tint"), material.Id + ".tint")), ToUnityPoint(spawn.Position), new Vector2(0.6f, 0.6f), 1, true);
                objectInstance.AddComponent<WorldGatherable>().Initialize(material.Id);
            }
        }

        private string SelectCandidateId(IReadOnlyList<string> candidateIds, int spawnIndex)
        {
            if (candidateIds == null || candidateIds.Count == 0)
            {
                return string.Empty;
            }

            var selection = (progression.CycleCount + spawnIndex) % candidateIds.Count;
            return candidateIds[selection];
        }

        private void CreateCombatLogic()
        {
            var damageMultiplier = upgradeEffects.GetValue("weapon_damage") / catalog.GetProgressionConfig().GetNumber("baseWeaponDamage");
            var scaleMultiplier = upgradeEffects.GetValue("weapon_scale") / catalog.GetProgressionConfig().GetNumber("baseWeaponScale");
            var appliedTuning = new CombatTuning(
                tuning.MinCharge,
                tuning.SweetStart,
                tuning.SweetEnd,
                tuning.MaxCharge,
                tuning.MaxRange,
                tuning.WaveSpeed,
                tuning.ReturnSpeed,
                tuning.Damage * damageMultiplier,
                tuning.LateDamageMultiplier,
                tuning.ReturnDamageMultiplier,
                tuning.WaveTimeout,
                tuning.ChargeMoveMultiplier);
            charge = new ChargeLogic(appliedTuning.ToChargeSettings());
            wave = new SwordWaveLogic(appliedTuning.ToWaveSettings());
            if (telemetry == null)
            {
                BeginCombatTelemetrySession("combat_started");
            }

            charge.Released += OnChargeReleased;
            wave.Completed += OnWaveCompleted;
            wave.HitRegistered += OnWaveHit;
            waveRenderer.transform.localScale = new Vector3(
                0.65f * (float)scaleMultiplier,
                0.65f * (float)scaleMultiplier,
                1f);
        }

        private void BeginCombatTelemetrySession(string eventName)
        {
            telemetry = new CombatTelemetry();
            telemetrySessionId = Guid.NewGuid().ToString("N");
            AppendCombatTelemetry(eventName);
        }

        private void OnChargeReleased(ChargeRelease release)
        {
            telemetry.Record(release);
            AppendCombatTelemetry("charge_release");
        }

        private void OnWaveCompleted(SwordWaveCompletion completion)
        {
            telemetry.Record(completion);
            activeWaveOutcome = null;
            AppendCombatTelemetry("wave_completed");
        }

        private void AppendCombatTelemetry(string eventName)
        {
            if (telemetryStore == null || telemetry == null || tuning == null || string.IsNullOrEmpty(telemetrySessionId) || telemetryLogUnavailable)
            {
                return;
            }

            try
            {
                telemetryStore.Append(DateTime.UtcNow, telemetrySessionId, eventName, telemetry.GetState(), tuning);
            }
            catch (Exception exception)
            {
                telemetryLogUnavailable = true;
                Debug.LogWarning("Combat telemetry log was not saved: " + exception.Message);
            }
        }

        private void UpdateDive(float deltaTime)
        {
            if (dive == null || player == null)
            {
                return;
            }

            UpdateCameraShake(deltaTime);
            if (hitStopRemaining > 0f)
            {
                hitStopRemaining -= deltaTime;
                return;
            }

            UpdatePlayer(deltaTime);
            UpdateCombat(deltaTime);
            UpdateEnemies(deltaTime);
            if (CheckDiveInteractions(deltaTime))
            {
                return;
            }

            DetectWaveHits();
            var hits = CollectContactHits(deltaTime);
            dive.Tick(deltaTime, ToDomain(player.position), hits);
        }

        private void UpdatePlayer(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (input.sqrMagnitude <= 0f)
            {
                return;
            }

            var speed = (float)catalog.GetCombatConfig().GetNumber("playerSpeed");
            if (charge.IsCharging)
            {
                speed *= (float)tuning.ChargeMoveMultiplier;
            }

            player.position += (Vector3)(input.normalized * speed * deltaTime);
        }

        private void UpdateCombat(float deltaTime)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && charge.Begin())
            {
                wave.BeginCharging();
            }

            var release = charge.Tick(deltaTime);
            if (release == null && mouse != null && mouse.rightButton.wasReleasedThisFrame)
            {
                release = charge.Release();
            }

            if (release != null)
            {
                ReleaseWave(release);
            }

            wave.Tick(deltaTime, ToDomain(player.position));
            var state = wave.GetState();
            var visible = state.State == SwordWaveState.Flying || state.State == SwordWaveState.Returning;
            waveRenderer.gameObject.SetActive(visible);
            if (visible)
            {
                waveRenderer.transform.position = ToUnity(state.Position);
                waveRenderer.color = state.State == SwordWaveState.Returning
                    ? ToUnityColor(currentFloorLayout.Visual.WaveReturn)
                    : ToUnityColor(currentFloorLayout.Visual.WaveForward);
            }
        }

        private void ReleaseWave(ChargeRelease release)
        {
            var aim = GetAimDirection();
            wave.Release(release, ToDomain(player.position), new Vec2(aim.x, aim.y));
            activeWaveOutcome = release.LaunchesWave ? release.Outcome : (ChargeOutcome?)null;
            if (release.Outcome == ChargeOutcome.Cancelled)
            {
                PlayTone(cancelTone);
                SetStatus("ui.status.charge_cancelled", "");
            }
            else if (release.Outcome == ChargeOutcome.SweetSpot)
            {
                PlayTone(sweetTone);
                SetStatus("ui.status.charge_sweet", "");
            }
            else
            {
                PlayTone(lateTone);
                SetStatus("ui.status.charge_late", "");
            }
        }

        private Vector2 GetAimDirection()
        {
            var mouse = Mouse.current;
            if (mouse == null || worldCamera == null)
            {
                return Vector2.right;
            }

            var screenPoint = mouse.position.ReadValue();
            var worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -worldCamera.transform.position.z));
            var direction = (Vector2)(worldPoint - player.position);
            return direction.sqrMagnitude <= 0f ? Vector2.right : direction.normalized;
        }

        private void UpdateEnemies(float deltaTime)
        {
            for (var enemyIndex = enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
            {
                var enemy = enemies[enemyIndex];
                if (enemy == null || !enemy.IsAlive)
                {
                    enemies.RemoveAt(enemyIndex);
                    continue;
                }

                enemy.Tick(player.position, deltaTime);
            }
        }

        private bool CheckDiveInteractions(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var marker = FindNearby<WorldMarker>();
            if (marker != null)
            {
                if (marker.Kind == WorldMarkerKind.Extract)
                {
                    dive.RequestExtract();
                    return true;
                }

                if (marker.Kind == WorldMarkerKind.Purify)
                {
                    if (dive.Purify(marker.Value))
                    {
                        marker.gameObject.SetActive(false);
                        SetStatus("ui.status.purified", "");
                    }

                    return false;
                }

                if (marker.Kind == WorldMarkerKind.Descend && keyboard.eKey.wasPressedThisFrame)
                {
                    Descend(marker.TargetId);
                    return true;
                }
            }

            var pickup = FindNearby<WorldPickup>();
            if (pickup != null && keyboard.eKey.wasPressedThisFrame)
            {
                Collect(pickup.ItemId, pickup.Count, pickup.gameObject);
                return false;
            }

            var gatherable = FindNearby<WorldGatherable>();
            if (gatherable != null && keyboard.eKey.isPressed)
            {
                if (activeGatherable != gatherable)
                {
                    activeGatherable = gatherable;
                    gatherProgress = 0f;
                }

                gatherProgress += deltaTime;
                if (gatherProgress >= catalog.GetInventoryConfig().GetNumber("gatherHoldSeconds"))
                {
                    Collect(gatherable.MaterialId, 1, gatherable.gameObject);
                    gatherProgress = 0f;
                    activeGatherable = null;
                }
            }
            else
            {
                gatherProgress = 0f;
                activeGatherable = null;
            }

            return false;
        }

        private void Descend(string nextFloorId)
        {
            if (!CanAccessFloor(nextFloorId, false))
            {
                return;
            }

            progression.UnlockFloor(nextFloorId);
            currentFloorId = nextFloorId;
            var settings = catalog.CreateErosionSettings(nextFloorId, upgradeEffects.GetValue("erosion_decay_rate"));
            if (dive.ChangeFloor(nextFloorId, settings))
            {
                BuildDiveWorld();
                CreateCombatLogic();
                SetStatus("ui.status.descended", GetDisplayName(nextFloorId));
            }
        }

        private bool CanAccessFloor(string floorId, bool requireUnlocked)
        {
            if (string.IsNullOrEmpty(floorId))
            {
                return false;
            }

            if (requireUnlocked && !progression.HasFloor(floorId))
            {
                SetStatus("ui.status.floor_locked", GetDisplayName(floorId));
                return false;
            }

            var requirement = catalog.GetFloorAccessRequirement(floorId);
            if (requirement == null || requirement.IsMet(progression.GetUpgradeLevel(requirement.UpgradeAxisId)))
            {
                return true;
            }

            var detail = string.Format(
                CultureInfo.InvariantCulture,
                T("ui.status.upgrade_requirement"),
                T(requirement.NameKey),
                requirement.MinimumLevel);
            SetStatus("ui.status.floor_requires_upgrade", detail);
            return false;
        }

        private void Collect(string itemId, int count, GameObject objectInstance)
        {
            var result = dive.TryCollect(itemId, count, catalog.GetMaterialStackLimit(itemId));
            if (result.RejectedCount > 0)
            {
                var pickup = objectInstance == null ? null : objectInstance.GetComponent<WorldPickup>();
                if (pickup != null && result.AddedCount > 0)
                {
                    pickup.SetCount(result.RejectedCount);
                }

                SetPendingReplacement(itemId, result.RejectedCount, objectInstance);
                SetStatus("ui.status.backpack_full", GetDisplayName(itemId));
                return;
            }

            Destroy(objectInstance);
            SetStatus("ui.status.collected", GetDisplayName(itemId));
        }

        private void SetPendingReplacement(string itemId, int count, GameObject objectInstance)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0 || objectInstance == null)
            {
                ClearPendingReplacement();
                return;
            }

            pendingReplacementItemId = itemId;
            pendingReplacementCount = count;
            pendingReplacementObject = objectInstance;
        }

        private void TryReplacePending(int slotIndex)
        {
            if (dive == null || player == null || pendingReplacementObject == null)
            {
                ClearPendingReplacement();
                return;
            }

            var interactionRadius = (float)catalog.GetCombatConfig().GetNumber("interactionRadius");
            if (Vector2.Distance(player.position, pendingReplacementObject.transform.position) > interactionRadius)
            {
                var itemId = pendingReplacementItemId;
                ClearPendingReplacement();
                SetStatus("ui.status.backpack_full", GetDisplayName(itemId));
                return;
            }

            var replacement = dive.TryReplacePickup(
                slotIndex,
                pendingReplacementItemId,
                pendingReplacementCount,
                catalog.GetMaterialStackLimit(pendingReplacementItemId));
            if (!replacement.Replaced)
            {
                SetStatus("ui.status.backpack_full", GetDisplayName(pendingReplacementItemId));
                return;
            }

            Destroy(pendingReplacementObject);
            var discarded = FormatItemCount(replacement.Discarded.ItemId, replacement.Discarded.Count);
            var collected = FormatItemCount(pendingReplacementItemId, pendingReplacementCount);
            ClearPendingReplacement();
            SetStatus("ui.status.replaced", string.Format(CultureInfo.InvariantCulture, T("ui.common.replacement"), discarded, collected));
        }

        private List<DiveHit> CollectContactHits(float deltaTime)
        {
            var hits = new List<DiveHit>();
            var contactRadius = (float)catalog.GetCombatConfig().GetNumber("contactRadius");
            var colliders = Physics2D.OverlapCircleAll(player.position, contactRadius);
            for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                var enemy = colliders[colliderIndex].GetComponent<WorldEnemy>();
                if (enemy != null && enemy.TryContact())
                {
                    hits.Add(new DiveHit(enemy.Id, enemy.ContactDamage));
                }
            }

            return hits;
        }

        private void DetectWaveHits()
        {
            if (waveRenderer == null || !waveRenderer.gameObject.activeSelf)
            {
                return;
            }

            var colliders = Physics2D.OverlapCircleAll(waveRenderer.transform.position, (float)catalog.GetCombatConfig().GetNumber("hitRadius"));
            for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                var enemy = colliders[colliderIndex].GetComponent<WorldEnemy>();
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (enemy.IsTutorialTarget && activeWaveOutcome != ChargeOutcome.SweetSpot)
                {
                    continue;
                }

                WaveHit hit;
                if (wave.TryRegisterHit(enemy.Id, out hit))
                {
                    enemy.ApplyDamage((float)hit.Damage);
                    if (!enemy.IsAlive)
                    {
                        CreateDrop(enemy);
                    }
                }
            }
        }

        private void CreateDrop(WorldEnemy enemy)
        {
            if (enemy.IsTutorialTarget)
            {
                tutorialTargetsRemaining--;
                if (tutorialTargetsRemaining <= 0)
                {
                    CompleteTutorial();
                }

                return;
            }

            var entries = catalog.GetDropEntries(enemy.DropTableId);
            if (entries.Count == 0)
            {
                return;
            }

            var totalWeight = 0d;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++) totalWeight += entries[entryIndex].Weight;
            var target = lootRng.NextUnit() * totalWeight;
            var selected = entries[entries.Count - 1];
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                target -= entries[entryIndex].Weight;
                if (target <= 0d)
                {
                    selected = entries[entryIndex];
                    break;
                }
            }

            var itemId = selected.MaterialId;
            if (!string.IsNullOrEmpty(selected.UpgradedMaterialId)
                && lootRng.NextUnit() < dive.GetState().Erosion.DropUpgradeChance)
            {
                itemId = selected.UpgradedMaterialId;
            }

            var added = dive.TryCollect(itemId, 1, catalog.GetMaterialStackLimit(itemId));
            if (added.RejectedCount == 0)
            {
                SetStatus("ui.status.collected", GetDisplayName(itemId));
                return;
            }

            var objectInstance = CreateSpriteObject("Drop_" + itemId, new Color(1f, 0.77f, 0.25f, 1f), enemy.transform.position, new Vector2(0.45f, 0.45f), 2, true);
            var pickup = objectInstance.AddComponent<WorldPickup>();
            pickup.Initialize(itemId, 1);
            pickups.Add(pickup);
            SetStatus("ui.status.backpack_full", GetDisplayName(itemId));
        }

        private void CompleteTutorial()
        {
            var tutorial = catalog.GetTutorialDefinition();
            if (tutorial == null)
            {
                return;
            }

            progression.SetFlag(tutorial.CompletionFlagId);
            tutorialTargetsRemaining = 0;
            CreateEnemiesForFloor(currentFloorLayout);
            SetStatus("ui.status.tutorial_complete", "");
        }

        private void OnWaveHit(WaveHit hit)
        {
            var isForwardHit = hit.Phase == WaveHitPhase.Forward;
            PlayTone(isForwardHit ? hitTone : returnTone);
            hitStopRemaining = (float)catalog.GetCombatConfig().GetNumber(
                isForwardHit ? "forwardHitStopSeconds" : "returnHitStopSeconds");
            shakeRemaining = (float)catalog.GetCombatConfig().GetNumber(
                isForwardHit ? "forwardScreenShakeSeconds" : "returnScreenShakeSeconds");
            shakeAmount = (float)catalog.GetCombatConfig().GetNumber(
                isForwardHit ? "forwardScreenShakeAmount" : "returnScreenShakeAmount");
            SetStatus(isForwardHit ? "ui.status.forward_hit" : "ui.status.return_hit", "");
        }

        private void HandleDiveFinished(DiveResult result)
        {
            AppendCombatTelemetry("dive_finished");
            lastDiveResult = result;
            DestroyWorld();
            dive = null;
            screen = GameScreen.Result;
            SetStatus(result.Reason == DiveEndReason.Extracted ? "ui.status.extracted" : "ui.status.forced_return", "");
        }

        private void SettleReturn()
        {
            if (lastDiveResult == null)
            {
                return;
            }

            lastStoryEvents = cycle.CompleteReturn(lastDiveResult, storyDirector, narrativeContext).FiredEvents;
            storyQueue = lastStoryEvents;
            storyQueueIndex = 0;
            SaveGame();
            if (TryChooseEnding())
            {
                screen = GameScreen.Ending;
                return;
            }

            screen = GameScreen.Village;
            BuildVillageWorld();
            SetStatus("ui.status.settled", "");
        }

        private bool TryChooseEnding()
        {
            var endingId = EndingSelector.Select(catalog.GetEndings(), progression);
            if (string.IsNullOrEmpty(endingId))
            {
                return false;
            }

            currentEndingId = endingId;
            progression.TriggerEnding(currentEndingId);
            SaveGame();
            return true;
        }

        private void TryStartRecipe(RecipeDefinition recipe)
        {
            if (progression.HasRecipe(recipe.Id) && cycle.TryStartRecipe(recipe))
            {
                SetStatus("ui.status.recipe_started", GetDisplayName(recipe.Id));
            }
            else
            {
                SetStatus("ui.status.recipe_missing", GetDisplayName(recipe.Id));
            }
        }

        private void TrySell(string itemId)
        {
            ContentItem item;
            if (!content.Database.TryGet(itemId, out item))
            {
                return;
            }

            PriceQuote quote;
            var success = false;
            if (string.Equals(item.Type, "material", StringComparison.Ordinal))
            {
                success = cycle.TrySellRaw(itemId, 1, catalog.GetMaterialBasePrice(itemId), catalog.CreatePricingSettings(), out quote);
            }
            else
            {
                RecipeDefinition recipe = FindRecipeByOutput(itemId);
                if (recipe != null)
                {
                    success = cycle.TrySellProcessed(itemId, 1, recipe, GetRefineryBonus(), catalog.CreatePricingSettings(), out quote);
                }
                else
                {
                    quote = null;
                }
            }

            if (success)
            {
                SaveGame();
                SetStatus("ui.status.sold", quote.Price.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void UseMedicine(string itemId)
        {
            if (!townInventory.TryRemove(itemId, 1))
            {
                return;
            }

            ContentItem item;
            var amount = content.Database.TryGet(itemId, out item) ? (int)item.GetNumber("affinityValue", 1d) : 1;
            AddAffinity(amount);
            SaveGame();
            SetStatus("ui.status.medicine_used", "");
        }

        private void AddAffinity(int amount)
        {
            progression.AddAffinity(amount);
            var unlocked = affinity.Add(amount);
            for (var cgIndex = 0; cgIndex < unlocked.Count; cgIndex++)
            {
                progression.UnlockCg(unlocked[cgIndex]);
            }
        }

        private void TryPurchase(UpgradeDefinition definition)
        {
            if (!upgradeLogic.TryPurchase(definition, progression, upgradeEffects))
            {
                SetStatus("ui.status.upgrade_unavailable", GetDisplayName(definition.Id));
                return;
            }

            cauldron.SetSlotCapacity((int)upgradeEffects.GetValue("cauldron_slots"));
            SaveGame();
            SetStatus("ui.status.upgrade_bought", GetDisplayName(definition.Id));
        }

        private void SaveGame()
        {
            saveComposer.Save(GetSavePath(), new GameSaveState
            {
                Gold = progression.Gold,
                Affinity = progression.Affinity,
                CycleCount = progression.CycleCount,
                TriggeredEndingId = progression.TriggeredEndingId,
                Inventory = townInventory.GetState(),
                UpgradeLevels = progression.GetUpgradeLevels(),
                UnlockedRecipes = CopyIds(progression.UnlockedRecipes),
                UnlockedCgs = CopyIds(progression.UnlockedCgs),
                UnlockedFloors = CopyIds(progression.UnlockedFloors),
                CraftedItems = CopyIds(progression.CraftedItems),
                Flags = CopyIds(progression.Flags),
                CauldronJobs = cauldron.GetJobStates()
            });
        }

        private static IReadOnlyList<string> CopyIds(IReadOnlyCollection<string> source)
        {
            var values = new List<string>();
            foreach (var value in source) values.Add(value);
            values.Sort(StringComparer.Ordinal);
            return values;
        }

        private string GetSavePath()
        {
            var overridePath = Environment.GetEnvironmentVariable(SavePathVariable);
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Application.persistentDataPath, "saves", "slot1.json")
                : Path.GetFullPath(overridePath);
        }

        private string GetTelemetryPath()
        {
            var overridePath = Environment.GetEnvironmentVariable(TelemetryPathVariable);
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Application.persistentDataPath, "telemetry", "combat-telemetry.csv")
                : Path.GetFullPath(overridePath);
        }

        private RecipeDefinition FindRecipeByOutput(string itemId)
        {
            for (var recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
            {
                if (string.Equals(recipes[recipeIndex].OutputItemId, itemId, StringComparison.Ordinal))
                {
                    return recipes[recipeIndex];
                }
            }

            return null;
        }

        private RecipeDefinition FindRecipeById(string recipeId)
        {
            RecipeDefinition recipe;
            return recipesById.TryGetValue(recipeId, out recipe) ? recipe : null;
        }

        private double GetRefineryBonus()
        {
            var bonuses = catalog.GetEconomyConfig().GetArray("refineryBonuses");
            var level = (int)upgradeEffects.GetValue("refinery_level");
            if (bonuses == null || bonuses.Count == 0)
            {
                return 0d;
            }

            var index = Math.Min(level, bonuses.Count - 1);
            return bonuses[index] is double ? (double)bonuses[index] : 0d;
        }

        private double EstimateQuality(RecipeDefinition recipe)
        {
            if (recipe == null || recipe.Ingredients.Count == 0)
            {
                return 0d;
            }

            var grades = new List<int>();
            for (var ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
            {
                grades.Add(recipe.Ingredients[ingredientIndex].Grade);
            }

            return PricingLogic.QuoteProcessed(recipe.BasePrice, grades, GetRefineryBonus(), catalog.CreatePricingSettings()).QualityScore;
        }

        private void DrawTitle()
        {
            DrawPanel(new Rect(UnityEngine.Screen.width * 0.5f - 220f, UnityEngine.Screen.height * 0.5f - 190f, 440f, 380f), T("ui.title.main"));
            var x = UnityEngine.Screen.width * 0.5f - 150f;
            var y = UnityEngine.Screen.height * 0.5f - 115f;
            if (GUI.Button(new Rect(x, y, 300f, 38f), T("ui.title.new_game"))) StartNewGame();
            if (GUI.Button(new Rect(x, y + 50f, 300f, 38f), T("ui.title.continue"))) ContinueGame();
            if (GUI.Button(new Rect(x, y + 100f, 300f, 38f), T("ui.title.options"))) ShowOptions(GameScreen.Title);
            if (GUI.Button(new Rect(x, y + 150f, 300f, 38f), T("ui.title.quit"))) Application.Quit();
            GUI.Label(new Rect(x, y + 205f, 300f, 28f), T("ui.title.subtitle"));
        }

        private void DrawVillage()
        {
            DrawPanel(new Rect(UiPadding, UiPadding, 360f, 286f), T("ui.village.title"));
            GUI.Label(new Rect(UiPadding + 12f, UiPadding + 32f, 330f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.village.status"), progression.Gold, progression.Affinity, progression.CycleCount));
            if (GUI.Button(new Rect(UiPadding + 12f, UiPadding + 65f, 155f, 30f), T("ui.village.apothecary"))) screen = GameScreen.Pharmacy;
            if (GUI.Button(new Rect(UiPadding + 182f, UiPadding + 65f, 155f, 30f), T("ui.village.workbench"))) screen = GameScreen.Workbench;
            if (GUI.Button(new Rect(UiPadding + 12f, UiPadding + 104f, 155f, 30f), T("ui.village.gallery"))) screen = GameScreen.Gallery;
            if (GUI.Button(new Rect(UiPadding + 182f, UiPadding + 104f, 155f, 30f), T("ui.common.options"))) ShowOptions(GameScreen.Village);
            var floorY = UiPadding + 146f;
            var floors = catalog.GetFloors();
            for (var floorIndex = 0; floorIndex < floors.Count; floorIndex++)
            {
                var floor = floors[floorIndex];
                if (!progression.HasFloor(floor.Id))
                {
                    continue;
                }

                if (GUI.Button(new Rect(UiPadding + 12f, floorY, 325f, 28f), string.Format(CultureInfo.InvariantCulture, T("ui.village.enter_floor"), T(floor.GetString("nameKey")))))
                {
                    EnterDive(floor.Id);
                }

                floorY += 34f;
            }

            GUI.Label(new Rect(UiPadding + 12f, UiPadding + 254f, 325f, 22f), StatusText());
        }

        private void DrawDiveHud()
        {
            if (dive == null || charge == null)
            {
                return;
            }

            var state = dive.GetState();
            DrawPanel(new Rect(UiPadding, UiPadding, 700f, 152f), T("ui.dive.title"));
            GUI.Label(new Rect(UiPadding + 12f, UiPadding + 30f, 520f, 22f), string.Format(CultureInfo.InvariantCulture, T("ui.dive.floor"), GetDisplayName(state.FloorId), GetBandDisplayName(state.Erosion.BandId)));
            GUI.Label(new Rect(UiPadding + 12f, UiPadding + 52f, 520f, 22f), string.Format(CultureInfo.InvariantCulture, T("ui.dive.slots"), state.Backpack.UsedSlots, state.Backpack.Capacity));
            DrawErosionBar(new Rect(UiPadding + 12f, UiPadding + 78f, 400f, 18f), state.Erosion.CurrentValue / state.Erosion.Maximum);
            DrawChargeBar(new Rect(UiPadding + 12f, UiPadding + 104f, 400f, 18f));
            GUI.Label(new Rect(UiPadding + 420f, UiPadding + 76f, 115f, 46f), StatusText());
            var telemetryState = telemetry.GetState();
            GUI.Label(new Rect(UiPadding + 12f, UiPadding + 122f, 670f, 18f), string.Format(
                CultureInfo.InvariantCulture,
                T("ui.dive.metrics"),
                telemetryState.CancelledRatio,
                telemetryState.SweetSpotRatio,
                telemetryState.LateRatio,
                telemetryState.AverageChargeDuration,
                telemetryState.ForwardHitRate,
                telemetryState.RoundTripHitRate));
            DrawRopeDirection();
            DrawPendingReplacement(state);
        }

        private void DrawPendingReplacement(DiveSnapshot state)
        {
            if (pendingReplacementObject == null || pendingReplacementCount <= 0)
            {
                ClearPendingReplacement();
                return;
            }

            var height = 62f + (state.Backpack.Slots.Count * 30f);
            var panel = new Rect(UiPadding, UiPadding + 164f, 700f, height);
            var title = string.Format(
                CultureInfo.InvariantCulture,
                T("ui.dive.replace_prompt"),
                GetDisplayName(pendingReplacementItemId),
                pendingReplacementCount);
            DrawPanel(panel, title);
            for (var slotIndex = 0; slotIndex < state.Backpack.Slots.Count; slotIndex++)
            {
                var slot = state.Backpack.Slots[slotIndex];
                var label = string.Format(
                    CultureInfo.InvariantCulture,
                    T("ui.dive.replace_slot"),
                    GetDisplayName(slot.ItemId),
                    slot.Count);
                if (GUI.Button(new Rect(UiPadding + 12f, UiPadding + 198f + (slotIndex * 30f), 320f, 26f), label))
                {
                    TryReplacePending(slotIndex);
                }
            }
        }

        private void DrawRopeDirection()
        {
            if (worldCamera == null || ropeMarkers.Count == 0)
            {
                return;
            }

            WorldMarker nearest = null;
            var nearestDistance = float.MaxValue;
            for (var markerIndex = 0; markerIndex < ropeMarkers.Count; markerIndex++)
            {
                var marker = ropeMarkers[markerIndex];
                if (marker == null || !marker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distance = Vector2.Distance(player.position, marker.transform.position);
                if (distance < nearestDistance)
                {
                    nearest = marker;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                return;
            }

            var viewport = worldCamera.WorldToViewportPoint(nearest.transform.position);
            if (viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
            {
                return;
            }

            var x = Mathf.Clamp(viewport.x, 0.04f, 0.96f) * UnityEngine.Screen.width;
            var y = (1f - Mathf.Clamp(viewport.y, 0.06f, 0.94f)) * UnityEngine.Screen.height;
            var horizontal = Mathf.Abs(viewport.x - 0.5f) > Mathf.Abs(viewport.y - 0.5f);
            var arrowKey = horizontal
                ? (viewport.x < 0.5f ? "ui.dive.arrow_left" : "ui.dive.arrow_right")
                : (viewport.y < 0.5f ? "ui.dive.arrow_down" : "ui.dive.arrow_up");
            GUI.Label(new Rect(x - 16f, y - 18f, 32f, 32f), T(arrowKey));
            GUI.Label(new Rect(x - 54f, y + 12f, 108f, 22f), T("ui.dive.rope_direction"));
        }

        private void DrawResult()
        {
            if (lastDiveResult == null)
            {
                return;
            }

            DrawPanel(new Rect(UnityEngine.Screen.width * 0.5f - 250f, 80f, 500f, 500f), T("ui.result.title"));
            var reasonKey = lastDiveResult.Reason == DiveEndReason.Extracted ? "ui.result.extracted" : "ui.result.forced";
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 125f, 450f, 24f), T(reasonKey));
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 154f, 450f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.result.floor"), GetDisplayName(lastDiveResult.FloorId)));
            var y = 188f;
            for (var slotIndex = 0; slotIndex < lastDiveResult.Backpack.Slots.Count; slotIndex++)
            {
                var slot = lastDiveResult.Backpack.Slots[slotIndex];
                GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, y, 450f, 22f), FormatItemCount(slot.ItemId, slot.Count));
                y += 23f;
            }

            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, y + 12f, 450f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.result.loss"), lastDiveResult.Loss.TotalLost));
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, y + 40f, 450f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.result.expected_value"), EstimateRawSaleValue(lastDiveResult)));
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, y + 64f, 450f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.result.telemetry_log"), telemetryLogUnavailable ? T("ui.result.telemetry_unavailable") : Path.GetFileName(GetTelemetryPath())));
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f - 150f, 520f, 300f, 34f), T("ui.result.settle"))) SettleReturn();
        }

        private int EstimateRawSaleValue(DiveResult result)
        {
            if (result == null)
            {
                return 0;
            }

            var total = 0;
            var settings = catalog.CreatePricingSettings();
            for (var slotIndex = 0; slotIndex < result.Backpack.Slots.Count; slotIndex++)
            {
                var slot = result.Backpack.Slots[slotIndex];
                ContentItem item;
                if (!content.Database.TryGet(slot.ItemId, out item) || !string.Equals(item.Type, "material", StringComparison.Ordinal))
                {
                    continue;
                }

                total += PricingLogic.QuoteRaw(catalog.GetMaterialBasePrice(slot.ItemId), settings).Price * slot.Count;
            }

            return total;
        }

        private void DrawPharmacy()
        {
            DrawPanel(new Rect(UiPadding, UiPadding, 860f, 630f), T("ui.pharmacy.title"));
            DrawPharmacyTabButton(PharmacyTab.Craft, 32f, "ui.pharmacy.craft");
            DrawPharmacyTabButton(PharmacyTab.Sell, 142f, "ui.pharmacy.sell");
            DrawPharmacyTabButton(PharmacyTab.Use, 252f, "ui.pharmacy.use");
            DrawPharmacyTabButton(PharmacyTab.Facility, 362f, "ui.pharmacy.facility");
            if (GUI.Button(new Rect(720f, 48f, 130f, 28f), T("ui.common.back"))) ReturnToVillage();
            switch (pharmacyTab)
            {
                case PharmacyTab.Craft: DrawCraftTab(); break;
                case PharmacyTab.Sell: DrawSellTab(); break;
                case PharmacyTab.Use: DrawUseTab(); break;
                case PharmacyTab.Facility: DrawFacilityTab(); break;
            }
        }

        private void DrawPharmacyTabButton(PharmacyTab tab, float x, string key)
        {
            if (GUI.Button(new Rect(x, 48f, 100f, 28f), T(key))) pharmacyTab = tab;
        }

        private void DrawCraftTab()
        {
            GUI.Label(new Rect(32f, 94f, 790f, 22f), string.Format(CultureInfo.InvariantCulture, T("ui.pharmacy.cauldron"), cauldron.GetState().Count, cauldron.SlotCapacity));
            var y = 126f;
            for (var recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
            {
                var recipe = recipes[recipeIndex];
                if (!progression.HasRecipe(recipe.Id))
                {
                    continue;
                }

                var preview = RecipeLogic.Preview(recipe, townInventory);
                var hasCauldronSlot = cauldron.GetState().Count < cauldron.SlotCapacity;
                var readiness = preview.CanStart
                    ? (hasCauldronSlot ? T("ui.pharmacy.ready") : T("ui.pharmacy.no_slot"))
                    : string.Format(CultureInfo.InvariantCulture, T("ui.pharmacy.missing_details"), FormatMissingIngredients(preview.MissingIngredients));
                GUI.Label(new Rect(32f, y, 410f, 26f), FormatNameDetail(recipe.Id, readiness));
                GUI.Label(new Rect(450f, y, 220f, 26f), string.Format(CultureInfo.InvariantCulture, T("ui.pharmacy.quality"), EstimateQuality(recipe)));
                var previousEnabled = GUI.enabled;
                GUI.enabled = preview.CanStart && hasCauldronSlot;
                var shouldStart = GUI.Button(new Rect(680f, y, 130f, 25f), T("ui.pharmacy.start"));
                GUI.enabled = previousEnabled;
                if (shouldStart) TryStartRecipe(recipe);
                y += 34f;
            }
        }

        private string FormatMissingIngredients(IReadOnlyList<IdCount> missingIngredients)
        {
            var entries = new List<string>();
            for (var ingredientIndex = 0; ingredientIndex < missingIngredients.Count; ingredientIndex++)
            {
                var missing = missingIngredients[ingredientIndex];
                entries.Add(FormatItemCount(missing.Id, missing.Count));
            }

            return string.Join(T("ui.common.list_separator"), entries.ToArray());
        }

        private void DrawSellTab()
        {
            var y = 104f;
            var entries = townInventory.GetState();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                GUI.Label(new Rect(32f, y, 420f, 26f), FormatItemCount(entry.Id, entry.Count));
                if (GUI.Button(new Rect(470f, y, 160f, 25f), T("ui.pharmacy.sell_one"))) TrySell(entry.Id);
                y += 33f;
            }
        }

        private void DrawUseTab()
        {
            GUI.Label(new Rect(32f, 104f, 720f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.pharmacy.affinity"), progression.Affinity));
            var y = 140f;
            var entries = townInventory.GetState();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                ContentItem item;
                if (!content.Database.TryGet(entry.Id, out item) || !string.Equals(item.Type, "item", StringComparison.Ordinal)) continue;
                GUI.Label(new Rect(32f, y, 420f, 26f), FormatItemCount(entry.Id, entry.Count));
                if (GUI.Button(new Rect(470f, y, 160f, 25f), T("ui.pharmacy.use"))) UseMedicine(entry.Id);
                y += 33f;
            }
        }

        private void DrawFacilityTab()
        {
            GUI.Label(new Rect(32f, 104f, 720f, 24f), T("ui.pharmacy.facility_help"));
            var y = 140f;
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                var upgrade = upgrades[upgradeIndex];
                if (!IsUpgradeInUiGroup(upgrade, "pharmacy")) continue;
                DrawUpgradeRow(upgrade, y);
                y += 34f;
            }
        }

        private void DrawWorkbench()
        {
            DrawPanel(new Rect(UiPadding, UiPadding, 860f, 630f), T("ui.workbench.title"));
            GUI.Label(new Rect(32f, 54f, 780f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.workbench.gold"), progression.Gold));
            if (GUI.Button(new Rect(720f, 48f, 130f, 28f), T("ui.common.back"))) ReturnToVillage();
            var y = 98f;
            for (var upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
            {
                var upgrade = upgrades[upgradeIndex];
                if (!IsUpgradeInUiGroup(upgrade, "workbench")) continue;
                DrawUpgradeRow(upgrade, y);
                y += 34f;
            }
        }

        private void DrawUpgradeRow(UpgradeDefinition upgrade, float y)
        {
            GUI.Label(
                new Rect(32f, y, 500f, 26f),
                string.Format(CultureInfo.InvariantCulture, T("ui.workbench.upgrade_cost"), GetDisplayName(upgrade.Id), upgrade.Level, upgrade.Cost));

            var purchaseState = upgradeLogic.GetPurchaseState(upgrade, progression);
            var buttonLabel = T("ui.workbench.buy");
            var canPurchase = purchaseState == UpgradePurchaseState.Available;
            switch (purchaseState)
            {
                case UpgradePurchaseState.AlreadyPurchased:
                    buttonLabel = T("ui.workbench.purchased");
                    break;
                case UpgradePurchaseState.RequiresPreviousLevel:
                    buttonLabel = T("ui.workbench.prerequisite");
                    break;
                case UpgradePurchaseState.InsufficientGold:
                    buttonLabel = T("ui.workbench.insufficient_gold");
                    break;
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canPurchase;
            if (GUI.Button(new Rect(560f, y, 150f, 25f), buttonLabel)) TryPurchase(upgrade);
            GUI.enabled = previousEnabled;
        }

        private void DrawGallery()
        {
            DrawPanel(new Rect(UiPadding, UiPadding, 960f, 630f), T("ui.gallery.title"));
            if (GUI.Button(new Rect(810f, 48f, 130f, 28f), T("ui.common.back"))) ReturnToVillage();
            var cgs = catalog.GetCgThresholds();
            for (var cgIndex = 0; cgIndex < cgs.Count; cgIndex++)
            {
                var column = cgIndex % 4;
                var row = cgIndex / 4;
                var rect = new Rect(32f + (column * 220f), 100f + (row * 220f), 190f, 180f);
                var unlocked = progression.HasCg(cgs[cgIndex].CgId);
                DrawCgPlaceholder(rect, cgs[cgIndex].CgId, unlocked);
                GUI.Label(new Rect(rect.x + 10f, rect.y + 145f, rect.width - 20f, 24f), unlocked ? T("ui.gallery.unlocked") : T("ui.gallery.locked"));
            }
        }

        private void DrawCgPlaceholder(Rect rect, string cgId, bool unlocked)
        {
            ContentItem cg;
            if (!unlocked || string.IsNullOrEmpty(cgId) || !content.Database.TryGet(cgId, out cg))
            {
                GUI.Box(rect, T("ui.gallery.locked"));
                return;
            }

            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ToUnityColor(ContentColor.FromValue(cg.GetArray("placeholderColor"), cgId + ".placeholderColor")), 0f, 0f);
            GUI.Box(rect, GetDisplayName(cgId));
        }

        private void DrawOptions()
        {
            DrawPanel(new Rect(UnityEngine.Screen.width * 0.5f - 260f, 70f, 520f, 430f), T("ui.options.title"));
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 125f, 170f, 24f), T("ui.options.bgm"));
            bgmVolume = GUI.HorizontalSlider(new Rect(UnityEngine.Screen.width * 0.5f - 50f, 130f, 230f, 18f), bgmVolume, 0f, 1f);
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 165f, 170f, 24f), T("ui.options.sfx"));
            sfxVolume = GUI.HorizontalSlider(new Rect(UnityEngine.Screen.width * 0.5f - 50f, 170f, 230f, 18f), sfxVolume, 0f, 1f);
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 205f, 170f, 24f), T("ui.options.ambience"));
            ambienceVolume = GUI.HorizontalSlider(new Rect(UnityEngine.Screen.width * 0.5f - 50f, 210f, 230f, 18f), ambienceVolume, 0f, 1f);
            audioSource.volume = sfxVolume;
            bgmSource.volume = bgmVolume;
            ambienceSource.volume = ambienceVolume;
            DrawLanguageButtons();
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 305f, 250f, 24f), string.Format(CultureInfo.InvariantCulture, T("ui.options.resolution"), resolutionWidth, resolutionHeight));
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f + 35f, 300f, 145f, 30f), T("ui.options.change_resolution"))) CycleResolution();
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 340f, 160f, 30f), T("ui.options.fullscreen"))) UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen;
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 225f, 385f, 420f, 60f), T("ui.options.controls"));
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f - 80f, 465f, 160f, 32f), T("ui.common.back")))
            {
                screen = optionsReturnScreen;
            }
        }

        private void DrawLanguageButtons()
        {
            var languages = catalog.GetOptionsConfig().GetArray("languages");
            if (languages == null)
            {
                return;
            }

            var x = UnityEngine.Screen.width * 0.5f - 225f;
            const float buttonWidth = 95f;
            const float buttonGap = 10f;
            for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
            {
                var language = ContentValues.AsObject(languages[languageIndex]);
                var code = ContentValues.GetString(language, "code");
                var nameKey = ContentValues.GetString(language, "nameKey");
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(nameKey))
                {
                    continue;
                }

                if (GUI.Button(new Rect(x + (languageIndex * (buttonWidth + buttonGap)), 260f, buttonWidth, 30f), T(nameKey)))
                {
                    localizer.SetLanguage(code);
                }
            }
        }

        private void BeginTutorialIntro()
        {
            tutorialIntroIndex = 0;
            var tutorial = catalog.GetTutorialDefinition();
            tutorialIntroKeys = tutorial == null ? new List<string>() : tutorial.IntroTextKeys;
        }

        private void DrawTutorialIntroOverlay()
        {
            if (tutorialIntroIndex >= tutorialIntroKeys.Count)
            {
                return;
            }

            DrawPanel(new Rect(UnityEngine.Screen.width * 0.5f - 280f, 140f, 560f, 300f), T("ui.tutorial.title"));
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 240f, 225f, 480f, 100f), T(tutorialIntroKeys[tutorialIntroIndex]));
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f - 90f, 365f, 180f, 34f), T("ui.story.next")))
            {
                tutorialIntroIndex++;
            }
        }

        private void CycleResolution()
        {
            var values = catalog.GetOptionsConfig().GetArray("resolutions");
            if (values == null || values.Count == 0)
            {
                return;
            }

            resolutionIndex = (resolutionIndex + 1) % values.Count;
            var resolution = ContentValues.AsObject(values[resolutionIndex]);
            if (resolution == null)
            {
                return;
            }

            resolutionWidth = (int)ContentValues.GetNumber(resolution, "width");
            resolutionHeight = (int)ContentValues.GetNumber(resolution, "height");
            if (resolutionWidth > 0 && resolutionHeight > 0)
            {
                UnityEngine.Screen.SetResolution(resolutionWidth, resolutionHeight, UnityEngine.Screen.fullScreen);
            }
        }

        private void ShowOptions(GameScreen returnScreen)
        {
            optionsReturnScreen = returnScreen;
            screen = GameScreen.Options;
        }

        private void DrawEnding()
        {
            ContentItem ending;
            if (!content.Database.TryGet(currentEndingId, out ending))
            {
                return;
            }

            DrawPanel(new Rect(UnityEngine.Screen.width * 0.5f - 300f, 80f, 600f, 440f), T("ui.ending.title"));
            DrawCgPlaceholder(new Rect(UnityEngine.Screen.width * 0.5f - 240f, 145f, 480f, 210f), ending.GetString("cgId"), true);
            GUI.Label(new Rect(UnityEngine.Screen.width * 0.5f - 240f, 380f, 480f, 48f), T(ending.GetString("textKey")));
            if (GUI.Button(new Rect(UnityEngine.Screen.width * 0.5f - 120f, 450f, 240f, 32f), T("ui.ending.title_return")))
            {
                screen = GameScreen.Title;
                DestroyWorld();
            }
        }

        private void DrawStoryOverlay()
        {
            var story = storyQueue[storyQueueIndex];
            ContentItem item;
            if (!content.Database.TryGet(story.Id, out item))
            {
                storyQueueIndex++;
                return;
            }

            var panel = new Rect(UnityEngine.Screen.width * 0.5f - 310f, UnityEngine.Screen.height - 260f, 620f, 220f);
            GUI.Box(panel, T("ui.story.title"));
            DrawCgPlaceholder(new Rect(panel.x + 22f, panel.y + 42f, 126f, 126f), item.GetString("portraitCgId"), true);
            GUI.Label(new Rect(panel.x + 170f, panel.y + 48f, 410f, 104f), T(item.GetString("textKey")));
            if (GUI.Button(new Rect(panel.x + 430f, panel.y + 172f, 150f, 28f), T("ui.story.next")))
            {
                storyQueueIndex++;
            }
        }

        private void DrawTuningWindow(int windowId)
        {
            GUI.Label(new Rect(16f, 28f, 510f, 22f), T("ui.tuning.help"));
            var y = 54f;
            for (var fieldIndex = 0; fieldIndex < tuningLabelKeys.Length; fieldIndex++)
            {
                GUI.Label(new Rect(16f, y, 210f, 22f), T(tuningLabelKeys[fieldIndex]));
                tuningValues[fieldIndex] = GUI.TextField(new Rect(230f, y, 140f, 22f), tuningValues[fieldIndex]);
                y += 30f;
            }

            if (GUI.Button(new Rect(16f, y + 4f, 110f, 26f), T("ui.tuning.apply"))) ApplyTuning(false);
            if (GUI.Button(new Rect(138f, y + 4f, 110f, 26f), T("ui.tuning.save"))) ApplyTuning(true);
            if (GUI.Button(new Rect(260f, y + 4f, 130f, 26f), T("ui.tuning.reload")))
            {
                tuning = tuningStore.Load(defaultTuning);
                tuningValues = BuildTuningValues(tuning);
                SetStatus("ui.status.tuning_reloaded", "");
            }

            if (GUI.Button(new Rect(404f, y + 4f, 110f, 26f), T("ui.common.close"))) showTuning = false;
            GUI.DragWindow(new Rect(0f, 0f, 550f, 24f));
        }

        private void ApplyTuning(bool save)
        {
            try
            {
                var updatedTuning = new CombatTuning(
                    ReadTuning(0), ReadTuning(1), ReadTuning(2), ReadTuning(3), ReadTuning(4), ReadTuning(5),
                    ReadTuning(6), ReadTuning(7), ReadTuning(8), ReadTuning(9), ReadTuning(10), ReadTuning(11));
                if (charge != null)
                {
                    AppendCombatTelemetry("tuning_changed");
                }

                tuning = updatedTuning;
                tuningValues = BuildTuningValues(tuning);
                if (charge != null)
                {
                    BeginCombatTelemetrySession("tuning_session_started");
                    CreateCombatLogic();
                }

                if (save)
                {
                    tuningStore.Save(tuning);
                    SetStatus("ui.status.tuning_saved", "");
                }
                else
                {
                    SetStatus("ui.status.tuning_applied", "");
                }
            }
            catch (Exception exception)
            {
                SetStatus("ui.status.tuning_invalid", exception.Message);
            }
        }

        private double ReadTuning(int index)
        {
            double value;
            if (!double.TryParse(tuningValues[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException(T(tuningLabelKeys[index]));
            }

            return value;
        }

        private void DrawErosionBar(Rect rect, double normalizedValue)
        {
            GUI.Box(rect, string.Empty);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * (float)normalizedValue, rect.height - 4f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.35f, 0.85f, 0.82f, 1f), 0f, 0f);
        }

        private void DrawChargeBar(Rect rect)
        {
            GUI.Box(rect, string.Empty);
            var state = charge.GetState();
            var sweetStart = (float)(tuning.SweetStart / tuning.MaxCharge);
            var sweetEnd = (float)(tuning.SweetEnd / tuning.MaxCharge);
            GUI.DrawTexture(new Rect(rect.x + (rect.width * sweetStart), rect.y + 2f, rect.width * (sweetEnd - sweetStart), rect.height - 4f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.28f, 0.85f, 0.35f, 0.8f), 0f, 0f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 4f, (rect.width - 4f) * (float)state.NormalizedDuration, rect.height - 8f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(1f, 0.88f, 0.3f, 1f), 0f, 0f);
        }

        private void ReturnToVillage()
        {
            screen = GameScreen.Village;
            BuildVillageWorld();
        }

        private void DrawPanel(Rect rect, string title)
        {
            GUI.Box(rect, title);
        }

        private void SetupCamera(Color background)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("HunterWidowCamera");
                worldCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 6.5f;
            worldCamera.backgroundColor = background;
            cameraRestPosition = new Vector3(0f, 0f, -10f);
            worldCamera.transform.position = cameraRestPosition;
            worldRoot = new GameObject("HunterWidowWorld").transform;
        }

        private GameObject CreateSpriteObject(string objectName, Color color, Vector2 position, Vector2 scale, int order, bool addCollider)
        {
            var objectInstance = new GameObject(objectName);
            objectInstance.transform.SetParent(worldRoot, false);
            objectInstance.transform.position = position;
            objectInstance.transform.localScale = scale;
            var renderer = objectInstance.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            if (addCollider)
            {
                objectInstance.AddComponent<CircleCollider2D>();
            }

            return objectInstance;
        }

        private void DestroyWorld()
        {
            enemies.Clear();
            pickups.Clear();
            ropeMarkers.Clear();
            player = null;
            waveRenderer = null;
            currentFloorLayout = null;
            tutorialTargetsRemaining = 0;
            activeWaveOutcome = null;
            ClearPendingReplacement();
            if (worldRoot != null)
            {
                Destroy(worldRoot.gameObject);
                worldRoot = null;
            }
        }

        private void ClearPendingReplacement()
        {
            pendingReplacementObject = null;
            pendingReplacementItemId = null;
            pendingReplacementCount = 0;
        }

        private void HandleErosionBandChanged(string bandId)
        {
            if (worldCamera == null || currentFloorLayout == null)
            {
                return;
            }

            var presentation = catalog.GetErosionBandPresentation(bandId);
            worldCamera.backgroundColor = Color.Lerp(
                ToUnityColor(currentFloorLayout.Visual.Background),
                ToUnityColor(presentation.OverlayColor),
                (float)presentation.OverlayAmount);
            if (ambienceSource != null)
            {
                ambienceSource.pitch = (float)presentation.AmbiencePitch;
            }

            if (ambienceLowPassFilter != null)
            {
                ambienceLowPassFilter.cutoffFrequency = (float)presentation.AmbienceLowPassCutoff;
            }

            if (ambienceReverbFilter != null)
            {
                ambienceReverbFilter.reverbLevel = (float)presentation.AmbienceReverbLevel;
            }
        }

        private void UpdateCameraShake(float deltaTime)
        {
            if (worldCamera == null)
            {
                return;
            }

            if (shakeRemaining <= 0f)
            {
                worldCamera.transform.position = cameraRestPosition;
                return;
            }

            shakeRemaining -= deltaTime;
            var offset = UnityEngine.Random.insideUnitCircle * shakeAmount;
            worldCamera.transform.position = cameraRestPosition + new Vector3(offset.x, offset.y, 0f);
        }

        private T FindNearby<T>() where T : Component
        {
            var colliders = Physics2D.OverlapCircleAll(player.position, (float)catalog.GetCombatConfig().GetNumber("interactionRadius"));
            for (var colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                var component = colliders[colliderIndex].GetComponent<T>();
                if (component != null && component.gameObject.activeInHierarchy)
                {
                    return component;
                }
            }

            return null;
        }

        private void CreateTones()
        {
            sweetTone = CreateTone("sweetToneHz");
            lateTone = CreateTone("lateToneHz");
            returnTone = CreateTone("returnToneHz");
            cancelTone = CreateTone("cancelToneHz");
            hitTone = CreateTone("hitToneHz");
        }

        private void CreateAudioBuses()
        {
            audioMixer = Resources.Load<AudioMixer>(AudioMixerResourceName);
            bgmMixerGroup = FindAudioMixerGroup(BgmMixerGroupName);
            sfxMixerGroup = FindAudioMixerGroup(SfxMixerGroupName);
            ambienceMixerGroup = FindAudioMixerGroup(AmbienceMixerGroupName);
            audioSource.outputAudioMixerGroup = sfxMixerGroup;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.clip = CreateLoopTone("bgmHz");
            bgmSource.volume = bgmVolume;
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;
            bgmSource.Play();

            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
            ambienceSource.clip = CreateLoopTone("ambienceHz");
            ambienceSource.volume = ambienceVolume;
            ambienceSource.outputAudioMixerGroup = ambienceMixerGroup;
            ambienceLowPassFilter = ambienceSource.gameObject.AddComponent<AudioLowPassFilter>();
            ambienceReverbFilter = ambienceSource.gameObject.AddComponent<AudioReverbFilter>();
            ambienceReverbFilter.reverbPreset = AudioReverbPreset.User;
            ambienceSource.Play();
        }

        private void UpdateBgmForCurrentScreen()
        {
            if (bgmSource == null || catalog == null)
            {
                return;
            }

            bgmSource.mute = screen == GameScreen.Dive
                && !catalog.GetAudioConfig().GetBool("playBgmDuringDive");
        }

        private AudioMixerGroup FindAudioMixerGroup(string groupName)
        {
            if (audioMixer == null || string.IsNullOrEmpty(groupName))
            {
                return null;
            }

            var groups = audioMixer.FindMatchingGroups(groupName);
            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group != null && string.Equals(group.name, groupName, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        private AudioClip CreateTone(string frequencyKey)
        {
            var frequency = (float)catalog.GetCombatConfig().GetNumber(frequencyKey);
            var seconds = (float)catalog.GetCombatConfig().GetNumber("toneSeconds");
            var volume = (float)catalog.GetCombatConfig().GetNumber("toneVolume");
            var sampleRate = (int)catalog.GetAudioConfig().GetNumber("sampleRate");
            var count = Mathf.Max(1, Mathf.CeilToInt(seconds * sampleRate));
            var samples = new float[count];
            for (var sampleIndex = 0; sampleIndex < count; sampleIndex++)
            {
                samples[sampleIndex] = Mathf.Sin(2f * Mathf.PI * frequency * sampleIndex / sampleRate) * volume;
            }

            var clip = AudioClip.Create("HunterWidowTone", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateLoopTone(string frequencyKey)
        {
            var frequency = (float)catalog.GetAudioConfig().GetNumber(frequencyKey);
            var seconds = (float)catalog.GetAudioConfig().GetNumber("loopSeconds");
            var volume = (float)catalog.GetAudioConfig().GetNumber("toneVolume");
            var sampleRate = (int)catalog.GetAudioConfig().GetNumber("sampleRate");
            var count = Mathf.Max(1, Mathf.CeilToInt(seconds * sampleRate));
            var samples = new float[count];
            for (var sampleIndex = 0; sampleIndex < count; sampleIndex++)
            {
                samples[sampleIndex] = Mathf.Sin(2f * Mathf.PI * frequency * sampleIndex / sampleRate) * volume;
            }

            var clip = AudioClip.Create("HunterWidowLoop", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayTone(AudioClip clip)
        {
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnApplicationQuit()
        {
            if (screen == GameScreen.Dive)
            {
                AppendCombatTelemetry("application_quit");
            }
        }

        private void SetStatus(string key, string fallback)
        {
            statusKey = key;
            statusFallback = fallback;
        }

        private string StatusText()
        {
            if (string.IsNullOrEmpty(statusKey))
            {
                return statusFallback;
            }

            var localized = T(statusKey);
            return string.IsNullOrEmpty(statusFallback)
                ? localized
                : string.Format(CultureInfo.InvariantCulture, localized, statusFallback);
        }

        private string T(string key)
        {
            return localizer.Get(key);
        }

        private string FormatItemCount(string itemId, int count)
        {
            return string.Format(CultureInfo.InvariantCulture, T("ui.common.item_count"), GetDisplayName(itemId), count);
        }

        private string FormatNameDetail(string contentId, string detail)
        {
            return string.Format(CultureInfo.InvariantCulture, T("ui.common.name_detail"), GetDisplayName(contentId), detail);
        }

        private bool IsUpgradeInUiGroup(UpgradeDefinition upgrade, string groupId)
        {
            var groups = catalog.GetUiConfig().GetObject("upgradeGroups");
            if (groups == null || upgrade == null || string.IsNullOrEmpty(groupId))
            {
                return false;
            }

            object rawAxisIds;
            var axisIds = groups.TryGetValue(groupId, out rawAxisIds) ? ContentValues.AsArray(rawAxisIds) : null;
            if (axisIds == null)
            {
                return false;
            }

            for (var axisIndex = 0; axisIndex < axisIds.Count; axisIndex++)
            {
                if (string.Equals(axisIds[axisIndex] as string, upgrade.AxisId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetDisplayName(string contentId)
        {
            ContentItem item;
            if (!content.Database.TryGet(contentId, out item))
            {
                return contentId;
            }

            var nameKey = item.GetString("nameKey");
            return string.IsNullOrEmpty(nameKey) ? contentId : T(nameKey);
        }

        private string GetBandDisplayName(string bandId)
        {
            var bands = catalog.GetErosionConfig().GetArray("bands");
            if (bands != null)
            {
                for (var bandIndex = 0; bandIndex < bands.Count; bandIndex++)
                {
                    var band = ContentValues.AsObject(bands[bandIndex]);
                    if (band == null || !string.Equals(ContentValues.GetString(band, "id"), bandId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var nameKey = ContentValues.GetString(band, "nameKey");
                    return string.IsNullOrEmpty(nameKey) ? bandId : T(nameKey);
                }
            }

            return bandId;
        }

        private static string[] BuildTuningValues(CombatTuning source)
        {
            return new[]
            {
                Number(source.MinCharge), Number(source.SweetStart), Number(source.SweetEnd), Number(source.MaxCharge),
                Number(source.MaxRange), Number(source.WaveSpeed), Number(source.ReturnSpeed), Number(source.Damage),
                Number(source.LateDamageMultiplier), Number(source.ReturnDamageMultiplier), Number(source.WaveTimeout), Number(source.ChargeMoveMultiplier)
            };
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Vec2 ToDomain(Vector3 value)
        {
            return new Vec2(value.x, value.y);
        }

        private static Vector3 ToUnity(Vec2 value)
        {
            return new Vector3((float)value.X, (float)value.Y, 0f);
        }

        private static Vector2 ToUnityPoint(ContentPoint value)
        {
            return new Vector2((float)value.X, (float)value.Y);
        }

        private static Color ToUnityColor(ContentColor value)
        {
            return new Color((float)value.Red, (float)value.Green, (float)value.Blue, (float)value.Alpha);
        }

        public sealed class WorldEnemy : MonoBehaviour
        {
            private float moveSpeed;
            private float health;
            private float contactCooldown;
            private float contactInterval;
            private float wanderDistanceSquared;
            private float wanderMoveMultiplier;
            private float retreatDistanceSquared;
            private SpriteRenderer spriteRenderer;
            private IEnemyBehavior behavior;
            private double elapsedSeconds;

            public string Id { get; private set; }

            public string DropTableId { get; private set; }

            public float ContactDamage { get; private set; }

            public bool IsAlive => health > 0f;

            public bool IsTutorialTarget { get; private set; }

            public void Initialize(
                string id,
                string behaviorId,
                string dropTableId,
                float maxHealth,
                float speed,
                float contactDamage,
                float wanderDistance,
                float wanderMultiplier,
                float retreatDistance,
                float contactIntervalSeconds,
                bool isTutorialTarget)
            {
                IEnemyBehavior resolvedBehavior;
                if (!EnemyBehaviorRegistry.TryGet(behaviorId, out resolvedBehavior))
                {
                    throw new InvalidOperationException("Enemy behavior is not registered: " + behaviorId);
                }

                Id = id;
                behavior = resolvedBehavior;
                DropTableId = dropTableId;
                health = maxHealth;
                moveSpeed = speed;
                ContactDamage = contactDamage;
                wanderDistanceSquared = wanderDistance * wanderDistance;
                wanderMoveMultiplier = wanderMultiplier;
                retreatDistanceSquared = retreatDistance * retreatDistance;
                contactInterval = Mathf.Max(0f, contactIntervalSeconds);
                IsTutorialTarget = isTutorialTarget;
                spriteRenderer = GetComponent<SpriteRenderer>();
                elapsedSeconds = 0d;
            }

            public void Tick(Vector3 playerPosition, float deltaTime)
            {
                contactCooldown = Mathf.Max(0f, contactCooldown - deltaTime);
                if (behavior == null)
                {
                    return;
                }

                elapsedSeconds += Math.Max(0d, deltaTime);
                var velocity = behavior.GetVelocity(new EnemyBehaviorContext(
                    new Vec2(transform.position.x, transform.position.y),
                    new Vec2(playerPosition.x, playerPosition.y),
                    moveSpeed,
                    Mathf.Sqrt(wanderDistanceSquared),
                    wanderMoveMultiplier,
                    Mathf.Sqrt(retreatDistanceSquared),
                    elapsedSeconds));
                transform.position += new Vector3((float)velocity.X, (float)velocity.Y, 0f) * deltaTime;
            }

            public bool TryContact()
            {
                if (contactCooldown > 0f)
                {
                    return false;
                }

                contactCooldown = contactInterval;
                return true;
            }

            public void ApplyDamage(float damage)
            {
                health -= damage;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                }

                if (health <= 0f)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        public sealed class WorldPickup : MonoBehaviour
        {
            public string ItemId { get; private set; }

            public int Count { get; private set; }

            public void Initialize(string itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }

            public void SetCount(int count)
            {
                Count = Mathf.Max(0, count);
            }
        }

        public sealed class WorldGatherable : MonoBehaviour
        {
            public string MaterialId { get; private set; }

            public void Initialize(string materialId)
            {
                MaterialId = materialId;
            }
        }

        public enum WorldMarkerKind
        {
            Extract,
            Descend,
            Purify
        }

        public sealed class WorldMarker : MonoBehaviour
        {
            public WorldMarkerKind Kind { get; private set; }

            public string TargetId { get; private set; }

            public float Value { get; private set; }

            public void Initialize(WorldMarkerKind kind, string targetId, float value)
            {
                Kind = kind;
                TargetId = targetId;
                Value = value;
            }
        }
    }
}
