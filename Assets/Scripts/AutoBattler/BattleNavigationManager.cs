using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public sealed class BattleNavigationManager : MonoBehaviour
    {
        public static BattleNavigationManager Instance { get; private set; }

        private readonly Dictionary<int, NavMeshSurface> navMeshSurfaces = new Dictionary<int, NavMeshSurface>();
        private readonly Dictionary<int, string> areaNames = new Dictionary<int, string>();
        private Transform surfaceRoot;
        private Transform modifierVolumeRoot;
        private TerrainMovementMap terrainMovementMap;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSurfaceRoot();
            EnsureModifierVolumeRoot();
        }

        public void RebuildNavigation(SceneBattleConfig config)
        {
            EnsureSurfaceRoot();
            EnsureModifierVolumeRoot();

            RebuildAreaNames(config);
            var defaultAreaIndex = ResolveDefaultAreaIndex(config);
            terrainMovementMap = TerrainMovementMap.Build(Terrain.activeTerrain, config != null ? config.terrainMovement : null);
            RebuildModifierVolumes();

            var requiredAgentTypeIds = CollectAgentTypeIds(config);
            RemoveUnusedSurfaces(requiredAgentTypeIds);

            foreach (var agentTypeId in requiredAgentTypeIds)
            {
                var surface = GetOrCreateSurface(agentTypeId);
                surface.defaultArea = defaultAreaIndex;
                surface.RemoveData();
                surface.BuildNavMesh();
            }
        }

        public void ConfigureAgent(NavMeshAgent agent, UnitDefinition definition)
        {
            if (agent == null || definition == null)
            {
                return;
            }

            if (terrainMovementMap == null)
            {
                return;
            }

            var bindings = terrainMovementMap.AreaBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                agent.SetAreaCost(binding.AreaIndex, ResolveAreaCost(definition, binding.TerrainType));
            }
        }

        public float GetSpeedMultiplier(UnitDefinition definition, Vector3 position)
        {
            if (definition == null)
            {
                return 1f;
            }

            var terrainType = terrainMovementMap != null
                ? terrainMovementMap.GetTerrainType(position)
                : "Grass";

            return definition.TerrainSpeedProfile.GetModifier(terrainType);
        }

        public string GetTerrainType(Vector3 position)
        {
            return terrainMovementMap != null ? terrainMovementMap.GetTerrainType(position) : "Grass";
        }

        public bool TryGetNavArea(Vector3 position, int agentTypeId, out int areaIndex, out string areaName)
        {
            areaIndex = 0;
            areaName = GetAreaName(areaIndex);

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = NavMesh.AllAreas
            };

            if (!NavMesh.SamplePosition(position, out var hit, 6f, filter))
            {
                return false;
            }

            areaIndex = ResolveAreaIndexFromMask(hit.mask);
            areaName = GetAreaName(areaIndex);
            return true;
        }

        private void RebuildModifierVolumes()
        {
            for (var i = modifierVolumeRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(modifierVolumeRoot.GetChild(i).gameObject);
            }

            if (terrainMovementMap == null)
            {
                return;
            }

            var rectangles = terrainMovementMap.ModifierRectangles;
            for (var i = 0; i < rectangles.Count; i++)
            {
                var rectangle = rectangles[i];
                var volumeObject = new GameObject("TerrainArea_" + rectangle.TerrainType + "_" + i);
                volumeObject.transform.SetParent(modifierVolumeRoot, false);
                volumeObject.transform.position = rectangle.Center;

                var volume = volumeObject.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = rectangle.Size;
                volume.area = rectangle.AreaIndex;
            }
        }

        private HashSet<int> CollectAgentTypeIds(SceneBattleConfig config)
        {
            var agentTypeIds = new HashSet<int>();
            CollectAgentTypeIds(config != null ? config.blueTeam : null, agentTypeIds);
            CollectAgentTypeIds(config != null ? config.redTeam : null, agentTypeIds);

            if (agentTypeIds.Count == 0)
            {
                agentTypeIds.Add(NavMeshAgentTypeResolver.GetDefaultAgentTypeId());
            }

            return agentTypeIds;
        }

        private static void CollectAgentTypeIds(TeamConfig teamConfig, HashSet<int> agentTypeIds)
        {
            if (teamConfig == null || teamConfig.units == null)
            {
                return;
            }

            for (var i = 0; i < teamConfig.units.Length; i++)
            {
                var definition = teamConfig.units[i] != null ? teamConfig.units[i].definition : null;
                agentTypeIds.Add(NavMeshAgentTypeResolver.ResolveAgentTypeId(definition != null ? definition.NavigationAgentType : string.Empty));
            }
        }

        private void RemoveUnusedSurfaces(HashSet<int> requiredAgentTypeIds)
        {
            var staleAgentTypeIds = new List<int>();
            foreach (var pair in navMeshSurfaces)
            {
                if (!requiredAgentTypeIds.Contains(pair.Key))
                {
                    pair.Value.RemoveData();
                    Destroy(pair.Value.gameObject);
                    staleAgentTypeIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < staleAgentTypeIds.Count; i++)
            {
                navMeshSurfaces.Remove(staleAgentTypeIds[i]);
            }
        }

        private NavMeshSurface GetOrCreateSurface(int agentTypeId)
        {
            if (navMeshSurfaces.TryGetValue(agentTypeId, out var existingSurface) && existingSurface != null)
            {
                return existingSurface;
            }

            EnsureSurfaceRoot();

            var surfaceObject = new GameObject("NavMeshSurface_" + agentTypeId);
            surfaceObject.transform.SetParent(surfaceRoot, false);

            var surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = agentTypeId;

            navMeshSurfaces[agentTypeId] = surface;
            return surface;
        }

        private void EnsureSurfaceRoot()
        {
            if (surfaceRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("NavMeshSurfaces");
            rootObject.transform.SetParent(transform, false);
            surfaceRoot = rootObject.transform;
        }

        private void EnsureModifierVolumeRoot()
        {
            if (modifierVolumeRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("NavMeshModifierVolumes");
            rootObject.transform.SetParent(transform, false);
            modifierVolumeRoot = rootObject.transform;
        }

        private static int ResolveDefaultAreaIndex(SceneBattleConfig config)
        {
            var terrainMovement = config != null ? config.terrainMovement : null;
            var defaultAreaName = terrainMovement != null ? terrainMovement.defaultNavArea : string.Empty;
            var defaultAreaIndex = !string.IsNullOrWhiteSpace(defaultAreaName)
                ? NavMesh.GetAreaFromName(defaultAreaName)
                : -1;
            return defaultAreaIndex >= 0 ? defaultAreaIndex : 0;
        }

        private static float ResolveAreaCost(UnitDefinition definition, string terrainType)
        {
            if (definition == null)
            {
                return 1f;
            }

            var pathCosts = definition.TerrainPathCostProfile;
            if (pathCosts != null && pathCosts.HasOverrides)
            {
                return Mathf.Clamp(pathCosts.GetModifier(terrainType), 1f, 20f);
            }

            var fastestModifier = definition.TerrainSpeedProfile.GetMaxModifier();
            return ConvertSpeedModifierToAreaCost(definition.TerrainSpeedProfile.GetModifier(terrainType), fastestModifier);
        }

        private static float ConvertSpeedModifierToAreaCost(float speedModifier, float fastestModifier)
        {
            return Mathf.Clamp(Mathf.Max(1f, fastestModifier) / Mathf.Max(0.05f, speedModifier), 1f, 20f);
        }

        private void RebuildAreaNames(SceneBattleConfig config)
        {
            areaNames.Clear();
            areaNames[0] = "Walkable";
            areaNames[1] = "Not Walkable";
            areaNames[2] = "Jump";

            var terrainMovement = config != null ? config.terrainMovement : null;
            if (terrainMovement == null)
            {
                return;
            }

            RegisterAreaName(terrainMovement.defaultNavArea);
            var mappings = terrainMovement.mappings;
            if (mappings == null)
            {
                return;
            }

            for (var i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                if (mapping == null)
                {
                    continue;
                }

                RegisterAreaName(mapping.navArea);
            }
        }

        private void RegisterAreaName(string areaName)
        {
            if (string.IsNullOrWhiteSpace(areaName))
            {
                return;
            }

            var areaIndex = NavMesh.GetAreaFromName(areaName);
            if (areaIndex >= 0)
            {
                areaNames[areaIndex] = areaName;
            }
        }

        private static int ResolveAreaIndexFromMask(int mask)
        {
            if (mask <= 0)
            {
                return 0;
            }

            for (var areaIndex = 0; areaIndex < 32; areaIndex++)
            {
                if ((mask & (1 << areaIndex)) != 0)
                {
                    return areaIndex;
                }
            }

            return 0;
        }

        private string GetAreaName(int areaIndex)
        {
            return areaNames.TryGetValue(areaIndex, out var areaName)
                ? areaName
                : "Area " + areaIndex;
        }
    }
}
