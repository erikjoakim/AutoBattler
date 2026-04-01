using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class AutoBattlerBootstrap : MonoBehaviour
    {
        private const string HeadQuarterSceneName = "HeadQuarter";
        private const string BlueStartPointName = "StartPoint1";
        private const string RedStartPointName = "StartPoint2";
        private const string FallbackLayoutRootName = "FallbackBattleLayout";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, HeadQuarterSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindAnyObjectByType<AutoBattlerBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("AutoBattlerBootstrap");
            bootstrapObject.AddComponent<AutoBattlerBootstrap>();
        }

        private void Start()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, HeadQuarterSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(gameObject);
                return;
            }

            EnsureSupportObjects();
            ResetBattleState();

            var scenario = ResolveScenario();
            var config = scenario.LoadSceneConfig();
            CampaignRuntimeContext.Instance?.ApplyPreparedMission(config, SceneManager.GetActiveScene().name);
            if (BattleNavigationManager.Instance != null)
            {
                BattleNavigationManager.Instance.RebuildNavigation(config);
            }

            var layout = ResolveSceneLayout();
            ValidateSceneLayout(layout, config);
            scenario.ValidateSceneSetup();
            ConfigureCamera(layout);
            var unitsRoot = SpawnBattlefield(layout, config);
            InitializeObjectives(layout, scenario);
            InitializeSpawners(layout, unitsRoot);
            LogBattleSetupSummary(layout, scenario);
        }

        private void EnsureSupportObjects()
        {
            if (FindAnyObjectByType<ScoreManager>() == null)
            {
                var supportObject = new GameObject("AutoBattlerSystems");
                supportObject.AddComponent<ScoreManager>();
                supportObject.AddComponent<BattleStateManager>();
                supportObject.AddComponent<BattleObjectiveManager>();
                supportObject.AddComponent<ScoreHud>();
                supportObject.AddComponent<UnitInspectorHud>();
                supportObject.AddComponent<GameSpeedController>();
                supportObject.AddComponent<BattleNavigationManager>();
                supportObject.AddComponent<BattleLootManager>();
            }
            else if (FindAnyObjectByType<ScoreHud>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<ScoreHud>();
            }

            if (FindAnyObjectByType<UnitInspectorHud>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<UnitInspectorHud>();
            }

            if (FindAnyObjectByType<BattleStateManager>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<BattleStateManager>();
            }

            if (FindAnyObjectByType<BattleObjectiveManager>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<BattleObjectiveManager>();
            }

            if (FindAnyObjectByType<BattleNavigationManager>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<BattleNavigationManager>();
            }

            if (FindAnyObjectByType<GameSpeedController>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<GameSpeedController>();
            }

            if (FindAnyObjectByType<BattleLootManager>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<BattleLootManager>();
            }

            if (CampaignRuntimeContext.Instance != null
                && CampaignRuntimeContext.Instance.HasActiveMission
                && FindAnyObjectByType<BattleCampaignBridge>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<BattleCampaignBridge>();
            }
        }

        private void ResetBattleState()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScores();
            }

            if (BattleStateManager.Instance != null)
            {
                BattleStateManager.Instance.ResetBattle();
            }

            if (BattleLootManager.Instance != null)
            {
                BattleLootManager.Instance.ResetLoot();
            }
        }

        private Transform SpawnBattlefield(SceneLayout layout, SceneBattleConfig config)
        {
            var existingRoot = GameObject.Find("UnitsRoot");
            if (existingRoot != null)
            {
                Destroy(existingRoot);
            }

            var unitsRoot = new GameObject("UnitsRoot");
            SpawnTeam(unitsRoot.transform, Team.Blue, layout.BlueStartAreas, layout.GetInitialObjectivePoint(Team.Blue), config.blueTeam, config.formation);
            SpawnTeam(unitsRoot.transform, Team.Red, layout.RedStartAreas, layout.GetInitialObjectivePoint(Team.Red), config.redTeam, config.formation);
            return unitsRoot.transform;
        }

        private void InitializeSpawners(SceneLayout layout, Transform unitsRoot)
        {
            if (layout.EnemySpawners == null || layout.EnemySpawners.Length == 0)
            {
                return;
            }

            var fallbackObjective = layout.GetInitialObjectivePoint(Team.Red);
            for (var i = 0; i < layout.EnemySpawners.Length; i++)
            {
                var marker = layout.EnemySpawners[i];
                if (marker == null)
                {
                    continue;
                }

                var runtime = marker.GetComponent<EnemySpawnerRuntime>();
                if (runtime == null)
                {
                    runtime = marker.gameObject.AddComponent<EnemySpawnerRuntime>();
                }

                runtime.Initialize(marker, unitsRoot, layout.VictoryPoints, fallbackObjective);
            }
        }

        private void InitializeObjectives(SceneLayout layout, BattleScenario scenario)
        {
            var objectiveManager = FindAnyObjectByType<BattleObjectiveManager>();
            if (objectiveManager == null)
            {
                return;
            }

            objectiveManager.Initialize(layout.BlueStartAreas, layout.RedStartAreas, layout.VictoryPoints);
            if (scenario != null)
            {
                scenario.Initialize(layout.VictoryPoints, layout.EnemySpawners);
            }
        }

        private void ConfigureCamera(SceneLayout layout)
        {
            if (Camera.main == null)
            {
                return;
            }

            var midpoint = (layout.BlueCenter + layout.RedCenter) * 0.5f;
            var mapSpan = GetMapSpan(layout);
            var cameraTransform = Camera.main.transform;

            cameraTransform.position = midpoint + new Vector3(0f, mapSpan * 0.9f, -mapSpan * 0.75f);
            cameraTransform.rotation = Quaternion.LookRotation(midpoint - cameraTransform.position, Vector3.up);
        }

        private void SpawnTeam(
            Transform parent,
            Team team,
            StartAreaMarker[] startAreas,
            Vector3 targetPoint,
            TeamConfig teamConfig,
            FormationConfig formation)
        {
            if (teamConfig == null || teamConfig.units == null || startAreas == null || startAreas.Length == 0)
            {
                return;
            }

            var areaUnitTotals = CalculateAreaUnitTotals(startAreas.Length, teamConfig);
            var areaSpawnCounts = new int[startAreas.Length];
            var spawnedCount = 0;

            for (var i = 0; i < teamConfig.units.Length; i++)
            {
                var unitConfig = teamConfig.units[i];
                if (unitConfig == null)
                {
                    continue;
                }

                var definition = unitConfig.definition;
                var mission = unitConfig.mission;
                var count = Mathf.Max(1, unitConfig.count);

                for (var unitIndex = 0; unitIndex < count; unitIndex++)
                {
                    var areaIndex = spawnedCount % startAreas.Length;
                    var area = startAreas[areaIndex];
                    var areaLocalIndex = areaSpawnCounts[areaIndex]++;
                    var worldPosition = area.GetSpawnPosition(areaLocalIndex, areaUnitTotals[areaIndex], formation);
                    BattleSpawnUtility.SpawnUnit(
                        parent,
                        definition,
                        team,
                        mission,
                        worldPosition,
                        targetPoint,
                        unitConfig.ownedUnitCardId,
                        unitConfig.lootTableId,
                        unitConfig.returnToHeadquartersIfSurvives,
                        unitConfig.captureAsUnitCardOnDeath,
                        unitConfig.persistentOverrideJson);
                    spawnedCount++;
                }
            }
        }

        private static int[] CalculateAreaUnitTotals(int areaCount, TeamConfig teamConfig)
        {
            var totals = new int[Mathf.Max(1, areaCount)];
            if (teamConfig == null || teamConfig.units == null || areaCount <= 0)
            {
                return totals;
            }

            var globalIndex = 0;
            for (var i = 0; i < teamConfig.units.Length; i++)
            {
                var unitConfig = teamConfig.units[i];
                if (unitConfig == null)
                {
                    continue;
                }

                var count = Mathf.Max(1, unitConfig.count);
                for (var unitIndex = 0; unitIndex < count; unitIndex++)
                {
                    totals[globalIndex % areaCount]++;
                    globalIndex++;
                }
            }

            return totals;
        }

        private SceneLayout ResolveSceneLayout()
        {
            var startAreas = FindObjectsByType<StartAreaMarker>();
            var victoryPoints = FindObjectsByType<VictoryPointMarker>();
            var spawners = FindObjectsByType<EnemySpawnerMarker>();
            var blueStartAreas = FilterStartAreas(startAreas, Team.Blue);
            var redStartAreas = FilterStartAreas(startAreas, Team.Red);

            if (blueStartAreas.Length == 0 || redStartAreas.Length == 0)
            {
                return CreateFallbackLayout(victoryPoints, spawners, blueStartAreas, redStartAreas);
            }

            SortByPriority(blueStartAreas);
            SortByPriority(redStartAreas);
            return new SceneLayout(blueStartAreas, redStartAreas, victoryPoints, spawners);
        }

        private SceneLayout CreateFallbackLayout(VictoryPointMarker[] existingVictoryPoints, EnemySpawnerMarker[] enemySpawners, StartAreaMarker[] blueStartAreas, StartAreaMarker[] redStartAreas)
        {
            var fallbackRoot = GameObject.Find(FallbackLayoutRootName);
            if (fallbackRoot == null)
            {
                fallbackRoot = new GameObject(FallbackLayoutRootName);
            }

            if (blueStartAreas.Length == 0)
            {
                blueStartAreas = new[] { GetOrCreateFallbackArea(fallbackRoot.transform, BlueStartPointName, ResolveLegacyStartPoint(BlueStartPointName, new Vector3(-20f, 0f, 0f)), Team.Blue) };
                Debug.LogWarning("No player start areas were found. Using a fallback player start area.");
            }

            if (redStartAreas.Length == 0)
            {
                redStartAreas = new[] { GetOrCreateFallbackArea(fallbackRoot.transform, RedStartPointName, ResolveLegacyStartPoint(RedStartPointName, new Vector3(20f, 0f, 0f)), Team.Red) };
                Debug.LogWarning("No enemy start areas were found. Using a fallback enemy start area.");
            }

            SortByPriority(blueStartAreas);
            SortByPriority(redStartAreas);
            return new SceneLayout(blueStartAreas, redStartAreas, existingVictoryPoints, enemySpawners);
        }

        private static StartAreaMarker[] FilterStartAreas(StartAreaMarker[] areas, Team team)
        {
            if (areas == null || areas.Length == 0)
            {
                return Array.Empty<StartAreaMarker>();
            }

            var count = 0;
            for (var i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null && areas[i].Team == team)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return Array.Empty<StartAreaMarker>();
            }

            var filtered = new StartAreaMarker[count];
            var index = 0;
            for (var i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null && areas[i].Team == team)
                {
                    filtered[index++] = areas[i];
                }
            }

            return filtered;
        }

        private static void SortByPriority(StartAreaMarker[] areas)
        {
            Array.Sort(areas, (left, right) =>
            {
                if (left == null && right == null)
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                var priorityComparison = left.Priority.CompareTo(right.Priority);
                return priorityComparison != 0
                    ? priorityComparison
                    : string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static StartAreaMarker GetOrCreateFallbackArea(Transform parent, string areaName, Vector3 position, Team team)
        {
            var child = parent.Find(areaName);
            StartAreaMarker marker;
            if (child == null)
            {
                var areaObject = new GameObject(areaName);
                areaObject.transform.SetParent(parent, false);
                areaObject.transform.position = position;
                marker = areaObject.AddComponent<StartAreaMarker>();
            }
            else
            {
                marker = child.GetComponent<StartAreaMarker>();
                if (marker == null)
                {
                    marker = child.gameObject.AddComponent<StartAreaMarker>();
                }

                child.position = position;
            }

            marker.ConfigureRuntimeMarker(team, new Vector3(12f, 2f, 12f));
            return marker;
        }

        private static Vector3 ResolveLegacyStartPoint(string objectName, Vector3 fallbackPosition)
        {
            var existingPoint = GameObject.Find(objectName);
            return existingPoint != null ? existingPoint.transform.position : fallbackPosition;
        }

        private void ValidateSceneLayout(SceneLayout layout, SceneBattleConfig config)
        {
            if (layout.VictoryPoints.Length == 0)
            {
                Debug.LogWarning("No victory points were found in the scene. Battle will fall back to elimination-only victory.");
            }

            if (config.blueTeam == null || config.blueTeam.units == null || config.blueTeam.units.Length == 0)
            {
                Debug.LogWarning("No player units were configured for this battle scene.");
            }

            if ((config.redTeam == null || config.redTeam.units == null || config.redTeam.units.Length == 0) && layout.RedStartAreas.Length == 0)
            {
                Debug.LogWarning("No enemy start areas and no configured enemy units were found.");
            }

            if (layout.EnemySpawners == null)
            {
                return;
            }

            for (var i = 0; i < layout.EnemySpawners.Length; i++)
            {
                var marker = layout.EnemySpawners[i];
                if (marker == null)
                {
                    continue;
                }

                if (!marker.TryGetResolvedConfig(out var spawnerConfig, out var warning))
                {
                    Debug.LogWarning("Spawner '" + marker.name + "' is using fallback config. " + warning);
                }

                if (spawnerConfig == null)
                {
                    Debug.LogWarning("Spawner '" + marker.name + "' has no resolved configuration.");
                    continue;
                }

                if (spawnerConfig.spawnTable == null || spawnerConfig.spawnTable.Length == 0)
                {
                    Debug.LogWarning("Spawner '" + marker.name + "' has an empty spawn table.");
                }

                if (spawnerConfig.totalSpawnLimit == 0)
                {
                    Debug.LogWarning("Spawner '" + marker.name + "' has a total spawn limit of 0 and will never spawn reinforcements.");
                }

                if (spawnerConfig.aliveUnitCap < spawnerConfig.countPerWave)
                {
                    Debug.LogWarning("Spawner '" + marker.name + "' has an alive-unit cap lower than its wave size.");
                }
            }
        }

        private void LogBattleSetupSummary(SceneLayout layout, BattleScenario scenario)
        {
            var rewardProfile = CampaignRuntimeContext.Instance?.ActiveMission?.rewardProfile;
            var spawnerCount = layout.EnemySpawners?.Length ?? 0;
            var modifierCount = CampaignRuntimeContext.Instance?.FindActiveMissionMapModifierCount() ?? 0;
            Debug.Log("Battle scene ready. Mission: " + (scenario != null ? scenario.MissionName : gameObject.scene.name)
                + "  Reward x" + (rewardProfile != null ? rewardProfile.rewardMultiplier.ToString("0.00") : "1.00")
                + "  Threat x" + (rewardProfile != null ? Mathf.Max(1f, rewardProfile.threatRatio).ToString("0.00") : "1.00")
                + "  Spawners: " + spawnerCount
                + "  MapModifiers: " + modifierCount);
        }

        private static BattleScenario ResolveScenario()
        {
            var scenario = FindAnyObjectByType<BattleScenario>();
            if (scenario != null)
            {
                return scenario;
            }

            var scenarioObject = new GameObject("BattleScenario");
            scenario = scenarioObject.AddComponent<BattleScenario>();
            scenario.ConfigureRuntimeFallback();
            return scenario;
        }

        private float GetMapSpan(SceneLayout layout)
        {
            var anchorDistance = Vector3.Distance(layout.BlueCenter, layout.RedCenter);
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                return Mathf.Max(40f, anchorDistance * 1.2f);
            }

            var terrainSize = terrain.terrainData.size;
            return Mathf.Max(anchorDistance * 1.2f, Mathf.Max(terrainSize.x, terrainSize.z) * 0.8f);
        }

        private readonly struct SceneLayout
        {
            public SceneLayout(StartAreaMarker[] blueStartAreas, StartAreaMarker[] redStartAreas, VictoryPointMarker[] victoryPoints, EnemySpawnerMarker[] enemySpawners)
            {
                BlueStartAreas = blueStartAreas ?? Array.Empty<StartAreaMarker>();
                RedStartAreas = redStartAreas ?? Array.Empty<StartAreaMarker>();
                VictoryPoints = victoryPoints ?? Array.Empty<VictoryPointMarker>();
                EnemySpawners = enemySpawners ?? Array.Empty<EnemySpawnerMarker>();
                BlueCenter = ComputeCentroid(BlueStartAreas, new Vector3(-20f, 0f, 0f));
                RedCenter = ComputeCentroid(RedStartAreas, new Vector3(20f, 0f, 0f));
            }

            public StartAreaMarker[] BlueStartAreas { get; }
            public StartAreaMarker[] RedStartAreas { get; }
            public VictoryPointMarker[] VictoryPoints { get; }
            public EnemySpawnerMarker[] EnemySpawners { get; }
            public Vector3 BlueCenter { get; }
            public Vector3 RedCenter { get; }

            public Vector3 GetInitialObjectivePoint(Team team)
            {
                if (team == Team.Blue)
                {
                    for (var i = 0; i < VictoryPoints.Length; i++)
                    {
                        if (VictoryPoints[i] != null && VictoryPoints[i].RequiredForVictory)
                        {
                            return VictoryPoints[i].Position;
                        }
                    }

                    return RedCenter;
                }

                for (var i = 0; i < VictoryPoints.Length; i++)
                {
                    if (VictoryPoints[i] != null && VictoryPoints[i].InitialOwner == ObjectiveOwner.Blue)
                    {
                        return VictoryPoints[i].Position;
                    }
                }

                return BlueCenter;
            }

            private static Vector3 ComputeCentroid(StartAreaMarker[] areas, Vector3 fallback)
            {
                if (areas == null || areas.Length == 0)
                {
                    return fallback;
                }

                var total = Vector3.zero;
                var count = 0;
                for (var i = 0; i < areas.Length; i++)
                {
                    if (areas[i] == null)
                    {
                        continue;
                    }

                    total += areas[i].Center;
                    count++;
                }

                return count == 0 ? fallback : total / count;
            }
        }
    }
}
