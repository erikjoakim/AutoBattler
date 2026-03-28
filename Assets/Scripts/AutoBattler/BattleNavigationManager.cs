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
        private Transform surfaceRoot;

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
        }

        public void RebuildNavigation(SceneBattleConfig config)
        {
            EnsureSurfaceRoot();

            var requiredAgentTypeIds = CollectAgentTypeIds(config);
            RemoveUnusedSurfaces(requiredAgentTypeIds);

            foreach (var agentTypeId in requiredAgentTypeIds)
            {
                var surface = GetOrCreateSurface(agentTypeId);
                surface.RemoveData();
                surface.BuildNavMesh();
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
    }
}
