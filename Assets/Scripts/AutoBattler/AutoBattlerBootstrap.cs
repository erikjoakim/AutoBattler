using UnityEngine;

namespace AutoBattler
{
    public sealed class AutoBattlerBootstrap : MonoBehaviour
    {
        private const string BlueStartPointName = "StartPoint1";
        private const string RedStartPointName = "StartPoint2";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            if (FindAnyObjectByType<AutoBattlerBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("AutoBattlerBootstrap");
            bootstrapObject.AddComponent<AutoBattlerBootstrap>();
        }

        private void Start()
        {
            EnsureSupportObjects();
            ResetBattleState();

            var anchors = ResolveSceneAnchors();
            var config = SceneBattleConfigLoader.LoadForActiveScene();
            if (BattleNavigationManager.Instance != null)
            {
                BattleNavigationManager.Instance.RebuildNavigation(config);
            }

            ConfigureCamera(anchors);
            SpawnBattlefield(anchors, config);
            InitializeObjectives(anchors);
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
                supportObject.AddComponent<BattleNavigationManager>();
            }
            else if (FindAnyObjectByType<ScoreHud>() == null)
            {
                ScoreManager.Instance.gameObject.AddComponent<ScoreHud>();
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
        }

        private void SpawnBattlefield(SceneAnchors anchors, SceneBattleConfig config)
        {
            var existingRoot = GameObject.Find("UnitsRoot");
            if (existingRoot != null)
            {
                Destroy(existingRoot);
            }

            var unitsRoot = new GameObject("UnitsRoot");

            SpawnTeam(unitsRoot.transform, Team.Blue, anchors.BlueStartPoint.position, anchors.RedStartPoint.position, config.blueTeam, config.formation);
            SpawnTeam(unitsRoot.transform, Team.Red, anchors.RedStartPoint.position, anchors.BlueStartPoint.position, config.redTeam, config.formation);
        }

        private void InitializeObjectives(SceneAnchors anchors)
        {
            var objectiveManager = FindAnyObjectByType<BattleObjectiveManager>();
            if (objectiveManager == null)
            {
                return;
            }

            objectiveManager.Initialize(anchors.BlueStartPoint.position, anchors.RedStartPoint.position);
        }

        private void ConfigureCamera(SceneAnchors anchors)
        {
            if (Camera.main == null)
            {
                return;
            }

            var midpoint = (anchors.BlueStartPoint.position + anchors.RedStartPoint.position) * 0.5f;
            var mapSpan = GetMapSpan(anchors);
            var cameraTransform = Camera.main.transform;

            cameraTransform.position = midpoint + new Vector3(0f, mapSpan * 0.9f, -mapSpan * 0.75f);
            cameraTransform.rotation = Quaternion.LookRotation(midpoint - cameraTransform.position, Vector3.up);
        }

        private void SpawnTeam(
            Transform parent,
            Team team,
            Vector3 spawnPoint,
            Vector3 targetPoint,
            TeamConfig teamConfig,
            FormationConfig formation)
        {
            if (teamConfig == null || teamConfig.units == null)
            {
                return;
            }

            var forward = GetPlanarDirection(spawnPoint, targetPoint);
            var right = new Vector3(forward.z, 0f, -forward.x);
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
                    var worldPosition = spawnPoint + GetFormationOffset(spawnedCount, formation, forward, right);
                    SpawnUnit(parent, definition, team, mission, worldPosition, targetPoint);
                    spawnedCount++;
                }
            }
        }

        private SceneAnchors ResolveSceneAnchors()
        {
            var blueStartPoint = GameObject.Find(BlueStartPointName);
            var redStartPoint = GameObject.Find(RedStartPointName);

            if (blueStartPoint != null && redStartPoint != null)
            {
                return new SceneAnchors(blueStartPoint.transform, redStartPoint.transform);
            }

            Debug.LogWarning("StartPoint1 and/or StartPoint2 were not found. Falling back to generated anchors.");
            return CreateFallbackAnchors();
        }

        private SceneAnchors CreateFallbackAnchors()
        {
            var fallbackRoot = GameObject.Find("FallbackBattleAnchors");
            if (fallbackRoot == null)
            {
                fallbackRoot = new GameObject("FallbackBattleAnchors");
            }

            var blue = GetOrCreateAnchor(fallbackRoot.transform, BlueStartPointName, new Vector3(-20f, 0f, 0f));
            var red = GetOrCreateAnchor(fallbackRoot.transform, RedStartPointName, new Vector3(20f, 0f, 0f));

            return new SceneAnchors(blue, red);
        }

        private static Transform GetOrCreateAnchor(Transform parent, string anchorName, Vector3 position)
        {
            var existingAnchor = parent.Find(anchorName);
            if (existingAnchor != null)
            {
                return existingAnchor;
            }

            var anchorObject = new GameObject(anchorName);
            anchorObject.transform.SetParent(parent, false);
            anchorObject.transform.position = position;
            return anchorObject.transform;
        }

        private static void SpawnUnit(
            Transform parent,
            UnitDefinition definition,
            Team team,
            MissionType mission,
            Vector3 position,
            Vector3 targetPoint)
        {
            var unitObject = UnitFactory.CreateUnitObject(definition, team, parent, position);
            unitObject.name = team + " " + definition.UnitName + " " + mission;

            var unit = unitObject.AddComponent<BattleUnit>();
            unit.Initialize(definition, team, mission, position, targetPoint);
        }

        private static Vector3 GetFormationOffset(int spawnIndex, FormationConfig formation, Vector3 forward, Vector3 right)
        {
            var unitsPerRow = Mathf.Max(1, formation.unitsPerRow);
            var row = spawnIndex / unitsPerRow;
            var column = spawnIndex % unitsPerRow;
            var centeredColumn = column - ((unitsPerRow - 1) * 0.5f);

            return (right * (centeredColumn * formation.lateralSpacing))
                - (forward * (formation.distanceFromStartPoint + (row * formation.forwardSpacing)));
        }

        private float GetMapSpan(SceneAnchors anchors)
        {
            var anchorDistance = Vector3.Distance(anchors.BlueStartPoint.position, anchors.RedStartPoint.position);
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                return Mathf.Max(40f, anchorDistance * 1.2f);
            }

            var terrainSize = terrain.terrainData.size;
            return Mathf.Max(anchorDistance * 1.2f, Mathf.Max(terrainSize.x, terrainSize.z) * 0.8f);
        }

        private static Vector3 GetPlanarDirection(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return Vector3.forward;
            }

            return direction.normalized;
        }

        private readonly struct SceneAnchors
        {
            public SceneAnchors(Transform blueStartPoint, Transform redStartPoint)
            {
                BlueStartPoint = blueStartPoint;
                RedStartPoint = redStartPoint;
            }

            public Transform BlueStartPoint { get; }
            public Transform RedStartPoint { get; }
        }
    }
}
