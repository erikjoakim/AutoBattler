using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AutoBattler
{
    public enum ScenarioConditionMode
    {
        All,
        Any
    }

    public enum ScenarioConditionType
    {
        CaptureAllRequiredVictoryPoints,
        CaptureSpecificVictoryPoints,
        EliminateTeam,
        DestroySpecificSpawners,
        DestroyAllSpawners,
        SurviveForTime,
        HoldSpecificVictoryPointsForTime,
        TimeExpires
    }

    [Serializable]
    public sealed class ScenarioConditionEntry
    {
        public ScenarioConditionType conditionType = ScenarioConditionType.CaptureAllRequiredVictoryPoints;
        public VictoryPointMarker[] victoryPoints = Array.Empty<VictoryPointMarker>();
        public EnemySpawnerMarker[] spawners = Array.Empty<EnemySpawnerMarker>();
        public float duration = 10f;
        public string displayText = string.Empty;
    }

    public sealed class BattleScenario : MonoBehaviour
    {
        public static BattleScenario Instance { get; private set; }

        [SerializeField] private string missionName = "Mission";
        [SerializeField] private string missionDescription = string.Empty;
        [SerializeField] private TextAsset sceneConfigAsset;
        [SerializeField] private ScenarioConditionMode winEvaluationMode = ScenarioConditionMode.Any;
        [SerializeField] private ScenarioConditionMode loseEvaluationMode = ScenarioConditionMode.Any;
        [FormerlySerializedAs("winConditions")]
        [SerializeField] private ScenarioConditionEntry[] playerWinConditions = Array.Empty<ScenarioConditionEntry>();
        [FormerlySerializedAs("loseConditions")]
        [SerializeField] private ScenarioConditionEntry[] playerLoseConditions = Array.Empty<ScenarioConditionEntry>();

        private VictoryPointMarker[] victoryPoints = Array.Empty<VictoryPointMarker>();
        private EnemySpawnerMarker[] spawners = Array.Empty<EnemySpawnerMarker>();
        private float[] playerWinProgressSeconds = Array.Empty<float>();
        private float[] playerLoseProgressSeconds = Array.Empty<float>();
        private float elapsedTime;
        private bool isInitialized;
        private bool useRuntimeFallbackRules;

        public string MissionName => string.IsNullOrWhiteSpace(missionName) ? gameObject.scene.name : missionName;
        public string MissionDescription => missionDescription;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ConfigureRuntimeFallback()
        {
            useRuntimeFallbackRules = true;
            missionName = "Battle";
            missionDescription = string.Empty;
        }

        public SceneBattleConfig LoadSceneConfig()
        {
            if (sceneConfigAsset != null)
            {
                return SceneBattleConfigLoader.Load(sceneConfigAsset, sceneConfigAsset.name);
            }

            return SceneBattleConfigLoader.LoadForActiveScene();
        }

        public void Initialize(VictoryPointMarker[] sceneVictoryPoints, EnemySpawnerMarker[] sceneSpawners)
        {
            victoryPoints = sceneVictoryPoints ?? Array.Empty<VictoryPointMarker>();
            spawners = sceneSpawners ?? Array.Empty<EnemySpawnerMarker>();
            elapsedTime = 0f;

            if (useRuntimeFallbackRules)
            {
                BuildRuntimeFallbackConditions();
            }

            playerWinConditions ??= Array.Empty<ScenarioConditionEntry>();
            playerLoseConditions ??= Array.Empty<ScenarioConditionEntry>();
            playerWinProgressSeconds = new float[playerWinConditions.Length];
            playerLoseProgressSeconds = new float[playerLoseConditions.Length];
            isInitialized = true;
        }

        public string GetObjectiveSummary()
        {
            if (!isInitialized)
            {
                return string.IsNullOrWhiteSpace(missionDescription) ? "Preparing mission..." : missionDescription;
            }

            var activeSummary = GetNextOutstandingObjectiveSummary(playerWinConditions, playerWinProgressSeconds, Team.Blue);
            if (!string.IsNullOrWhiteSpace(activeSummary))
            {
                if (!string.IsNullOrWhiteSpace(missionDescription))
                {
                    return missionDescription + " | " + activeSummary;
                }

                return activeSummary;
            }

            return string.IsNullOrWhiteSpace(missionDescription) ? "Mission objectives complete" : missionDescription;
        }

        public string GetProgressSummary()
        {
            if (!isInitialized)
            {
                return string.Empty;
            }

            for (var i = 0; i < playerWinConditions.Length; i++)
            {
                var condition = playerWinConditions[i];
                if (!IsConditionRelevant(condition))
                {
                    continue;
                }

                EvaluateCondition(condition, Team.Blue, playerWinProgressSeconds, i, advanceTimers: false, out var met);
                if (met)
                {
                    continue;
                }

                var progressText = BuildProgressText(condition, Team.Blue, playerWinProgressSeconds, i);
                if (!string.IsNullOrWhiteSpace(progressText))
                {
                    return progressText;
                }
            }

            return string.Empty;
        }

        public void ValidateSceneSetup()
        {
            if (sceneConfigAsset == null)
            {
                Debug.LogWarning("BattleScenario on " + name + " has no scene config asset assigned. Falling back to scene-name-based config lookup.");
            }

            ValidateConditions(playerWinConditions, "player win");
            ValidateConditions(playerLoseConditions, "player lose");
        }

        private void Update()
        {
            if (!isInitialized || BattleStateManager.Instance == null || BattleStateManager.Instance.IsBattleOver)
            {
                return;
            }

            elapsedTime += Time.deltaTime;

            if (EvaluateConditions(playerLoseConditions, Team.Red, loseEvaluationMode, playerLoseProgressSeconds))
            {
                BattleStateManager.Instance.EndBattle(Team.Red, BuildOutcomeMessage(Team.Red, playerLoseConditions, playerLoseProgressSeconds, loseEvaluationMode, "Red"));
                return;
            }

            if (EvaluateConditions(playerWinConditions, Team.Blue, winEvaluationMode, playerWinProgressSeconds))
            {
                BattleStateManager.Instance.EndBattle(Team.Blue, BuildOutcomeMessage(Team.Blue, playerWinConditions, playerWinProgressSeconds, winEvaluationMode, "Blue"));
            }
        }

        private bool EvaluateConditions(ScenarioConditionEntry[] conditions, Team subjectTeam, ScenarioConditionMode mode, float[] progressSeconds)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return false;
            }

            var anyRelevant = false;
            var anyMet = false;
            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (!IsConditionRelevant(condition))
                {
                    continue;
                }

                anyRelevant = true;
                var isMet = EvaluateCondition(condition, subjectTeam, progressSeconds, i, advanceTimers: true, out _);
                anyMet |= isMet;
                if (mode == ScenarioConditionMode.All && !isMet)
                {
                    return false;
                }

                if (mode == ScenarioConditionMode.Any && isMet)
                {
                    return true;
                }
            }

            if (!anyRelevant)
            {
                return false;
            }

            return mode == ScenarioConditionMode.All && anyMet;
        }

        private bool EvaluateCondition(ScenarioConditionEntry condition, Team subjectTeam, float[] progressSeconds, int index, bool advanceTimers, out bool conditionMet)
        {
            conditionMet = false;
            var objectiveManager = BattleObjectiveManager.Instance;
            var owner = ToObjectiveOwner(subjectTeam);
            var duration = Mathf.Max(0.1f, condition.duration);

            switch (condition.conditionType)
            {
                case ScenarioConditionType.CaptureAllRequiredVictoryPoints:
                    conditionMet = objectiveManager != null && objectiveManager.AreAllRequiredOwnedBy(owner);
                    ResetProgressIfNeeded(progressSeconds, index, conditionMet);
                    return conditionMet;

                case ScenarioConditionType.CaptureSpecificVictoryPoints:
                    conditionMet = objectiveManager != null && objectiveManager.AreAllOwned(owner, condition.victoryPoints);
                    ResetProgressIfNeeded(progressSeconds, index, conditionMet);
                    return conditionMet;

                case ScenarioConditionType.EliminateTeam:
                    conditionMet = BattleUnitRegistry.CountAlive(GetOpposingTeam(subjectTeam)) == 0;
                    ResetProgressIfNeeded(progressSeconds, index, conditionMet);
                    return conditionMet;

                case ScenarioConditionType.DestroySpecificSpawners:
                    conditionMet = CountDestroyedSpawners(condition.spawners) == CountValidSpawners(condition.spawners) && CountValidSpawners(condition.spawners) > 0;
                    ResetProgressIfNeeded(progressSeconds, index, conditionMet);
                    return conditionMet;

                case ScenarioConditionType.DestroyAllSpawners:
                    conditionMet = spawners.Length > 0 && CountDestroyedSpawners(spawners) == CountValidSpawners(spawners);
                    ResetProgressIfNeeded(progressSeconds, index, conditionMet);
                    return conditionMet;

                case ScenarioConditionType.SurviveForTime:
                    progressSeconds[index] = elapsedTime;
                    conditionMet = elapsedTime >= duration;
                    return conditionMet;

                case ScenarioConditionType.HoldSpecificVictoryPointsForTime:
                {
                    var ownsPoints = objectiveManager != null && objectiveManager.AreAllOwned(owner, condition.victoryPoints);
                    UpdateTimedProgress(progressSeconds, index, advanceTimers && ownsPoints ? Time.deltaTime : 0f, ownsPoints);
                    conditionMet = progressSeconds[index] >= duration;
                    return conditionMet;
                }

                case ScenarioConditionType.TimeExpires:
                    progressSeconds[index] = elapsedTime;
                    conditionMet = elapsedTime >= duration;
                    return conditionMet;

                default:
                    progressSeconds[index] = 0f;
                    return false;
            }
        }

        private string GetNextOutstandingObjectiveSummary(ScenarioConditionEntry[] conditions, float[] progressSeconds, Team subjectTeam)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return "Eliminate all enemies";
            }

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (!IsConditionRelevant(condition))
                {
                    continue;
                }

                EvaluateCondition(condition, subjectTeam, progressSeconds, Mathf.Min(i, Mathf.Max(0, progressSeconds.Length - 1)), advanceTimers: false, out var met);
                if (!met)
                {
                    return DescribeCondition(condition, subjectTeam);
                }
            }

            return string.Empty;
        }

        private string DescribeCondition(ScenarioConditionEntry condition, Team subjectTeam)
        {
            if (!string.IsNullOrWhiteSpace(condition.displayText))
            {
                return condition.displayText;
            }

            switch (condition.conditionType)
            {
                case ScenarioConditionType.CaptureAllRequiredVictoryPoints:
                    return "Capture all required victory points";
                case ScenarioConditionType.CaptureSpecificVictoryPoints:
                    return "Capture the selected victory points";
                case ScenarioConditionType.EliminateTeam:
                    return "Eliminate all " + GetOpposingTeam(subjectTeam) + " units";
                case ScenarioConditionType.DestroySpecificSpawners:
                    return "Destroy the selected enemy spawners";
                case ScenarioConditionType.DestroyAllSpawners:
                    return "Destroy all enemy spawners";
                case ScenarioConditionType.SurviveForTime:
                    return "Survive for " + Mathf.CeilToInt(Mathf.Max(1f, condition.duration)) + "s";
                case ScenarioConditionType.HoldSpecificVictoryPointsForTime:
                    return "Hold the selected victory points for " + Mathf.CeilToInt(Mathf.Max(1f, condition.duration)) + "s";
                case ScenarioConditionType.TimeExpires:
                    return "Mission timer expires in " + Mathf.CeilToInt(Mathf.Max(1f, condition.duration)) + "s";
                default:
                    return "Complete mission objectives";
            }
        }

        private string BuildProgressText(ScenarioConditionEntry condition, Team subjectTeam, float[] progressSeconds, int index)
        {
            var objectiveManager = BattleObjectiveManager.Instance;
            var owner = ToObjectiveOwner(subjectTeam);
            switch (condition.conditionType)
            {
                case ScenarioConditionType.CaptureAllRequiredVictoryPoints:
                    if (objectiveManager == null)
                    {
                        return string.Empty;
                    }

                    var requiredCount = objectiveManager.CountRequiredVictoryPoints();
                    if (requiredCount <= 0)
                    {
                        return string.Empty;
                    }

                    return "Victory points owned: "
                        + objectiveManager.CountRequiredOwnedBy(owner)
                        + "/"
                        + requiredCount;

                case ScenarioConditionType.CaptureSpecificVictoryPoints:
                    if (objectiveManager == null)
                    {
                        return string.Empty;
                    }

                    return "Selected points owned: "
                        + objectiveManager.CountOwned(owner, condition.victoryPoints)
                        + "/"
                        + CountValidVictoryPoints(condition.victoryPoints);

                case ScenarioConditionType.EliminateTeam:
                    return GetOpposingTeam(subjectTeam) + " units alive: " + BattleUnitRegistry.CountAlive(GetOpposingTeam(subjectTeam));

                case ScenarioConditionType.DestroySpecificSpawners:
                    return "Spawners destroyed: "
                        + CountDestroyedSpawners(condition.spawners)
                        + "/"
                        + CountValidSpawners(condition.spawners);

                case ScenarioConditionType.DestroyAllSpawners:
                    return "Spawners destroyed: "
                        + CountDestroyedSpawners(spawners)
                        + "/"
                        + CountValidSpawners(spawners);

                case ScenarioConditionType.SurviveForTime:
                case ScenarioConditionType.TimeExpires:
                case ScenarioConditionType.HoldSpecificVictoryPointsForTime:
                    if (progressSeconds == null || index < 0 || index >= progressSeconds.Length)
                    {
                        return string.Empty;
                    }

                    return Mathf.CeilToInt(progressSeconds[index]) + " / " + Mathf.CeilToInt(Mathf.Max(1f, condition.duration)) + "s";

                default:
                    return string.Empty;
            }
        }

        private bool IsConditionRelevant(ScenarioConditionEntry condition)
        {
            if (condition == null)
            {
                return false;
            }

            switch (condition.conditionType)
            {
                case ScenarioConditionType.CaptureAllRequiredVictoryPoints:
                    return BattleObjectiveManager.Instance != null && BattleObjectiveManager.Instance.CountRequiredVictoryPoints() > 0;
                case ScenarioConditionType.CaptureSpecificVictoryPoints:
                case ScenarioConditionType.HoldSpecificVictoryPointsForTime:
                    return CountValidVictoryPoints(condition.victoryPoints) > 0;
                case ScenarioConditionType.DestroySpecificSpawners:
                    return CountValidSpawners(condition.spawners) > 0;
                case ScenarioConditionType.DestroyAllSpawners:
                    return CountValidSpawners(spawners) > 0;
                default:
                    return true;
            }
        }

        private string BuildOutcomeMessage(
            Team winner,
            ScenarioConditionEntry[] conditions,
            float[] progressSeconds,
            ScenarioConditionMode mode,
            string winnerLabel)
        {
            var completed = new List<string>();
            if (conditions != null)
            {
                for (var i = 0; i < conditions.Length; i++)
                {
                    var condition = conditions[i];
                    if (!IsConditionRelevant(condition))
                    {
                        continue;
                    }

                    EvaluateCondition(condition, winner, progressSeconds, i, false, out var met);
                    if (met)
                    {
                        completed.Add(DescribeCondition(condition, winner));
                    }
                }
            }

            if (completed.Count == 0)
            {
                return winnerLabel + " wins";
            }

            if (mode == ScenarioConditionMode.Any)
            {
                return winnerLabel + " wins: " + completed[0];
            }

            return winnerLabel + " wins: " + string.Join(", ", completed);
        }

        private void BuildRuntimeFallbackConditions()
        {
            var conditions = new List<ScenarioConditionEntry>();
            if (victoryPoints.Length > 0)
            {
                conditions.Add(new ScenarioConditionEntry
                {
                    conditionType = ScenarioConditionType.CaptureAllRequiredVictoryPoints,
                    displayText = "Capture all required victory points"
                });
            }

            conditions.Add(new ScenarioConditionEntry
            {
                conditionType = ScenarioConditionType.EliminateTeam,
                displayText = "Eliminate all enemies"
            });

            playerWinConditions = conditions.ToArray();
            playerLoseConditions = new[]
            {
                new ScenarioConditionEntry
                {
                    conditionType = ScenarioConditionType.EliminateTeam,
                    displayText = "Keep at least one player unit alive"
                }
            };
            winEvaluationMode = ScenarioConditionMode.Any;
            loseEvaluationMode = ScenarioConditionMode.Any;
        }

        private void ValidateConditions(ScenarioConditionEntry[] conditions, string label)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return;
            }

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                switch (condition.conditionType)
                {
                    case ScenarioConditionType.CaptureSpecificVictoryPoints:
                    case ScenarioConditionType.HoldSpecificVictoryPointsForTime:
                        if (CountValidVictoryPoints(condition.victoryPoints) == 0)
                        {
                            Debug.LogWarning("BattleScenario " + name + " has a " + label + " condition with no victory points assigned.");
                        }

                        break;

                    case ScenarioConditionType.DestroySpecificSpawners:
                        if (CountValidSpawners(condition.spawners) == 0)
                        {
                            Debug.LogWarning("BattleScenario " + name + " has a " + label + " condition with no spawners assigned.");
                        }

                        break;
                }
            }
        }

        private static int CountValidVictoryPoints(VictoryPointMarker[] points)
        {
            if (points == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountValidSpawners(EnemySpawnerMarker[] targetSpawners)
        {
            if (targetSpawners == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < targetSpawners.Length; i++)
            {
                if (targetSpawners[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDestroyedSpawners(EnemySpawnerMarker[] targetSpawners)
        {
            if (targetSpawners == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < targetSpawners.Length; i++)
            {
                if (targetSpawners[i] == null || !targetSpawners[i].gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static ObjectiveOwner ToObjectiveOwner(Team team)
        {
            return team == Team.Blue ? ObjectiveOwner.Blue : ObjectiveOwner.Red;
        }

        private static Team GetOpposingTeam(Team team)
        {
            return team == Team.Blue ? Team.Red : Team.Blue;
        }

        private static void ResetProgressIfNeeded(float[] progressSeconds, int index, bool conditionMet)
        {
            if (progressSeconds == null || index < 0 || index >= progressSeconds.Length)
            {
                return;
            }

            progressSeconds[index] = conditionMet ? progressSeconds[index] : 0f;
        }

        private static void UpdateTimedProgress(float[] progressSeconds, int index, float delta, bool continueCounting)
        {
            if (progressSeconds == null || index < 0 || index >= progressSeconds.Length)
            {
                return;
            }

            progressSeconds[index] = continueCounting
                ? progressSeconds[index] + delta
                : 0f;
        }
    }
}
