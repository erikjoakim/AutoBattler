using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public sealed class BattleNavigationManager : MonoBehaviour
    {
        public static BattleNavigationManager Instance { get; private set; }

        private NavMeshSurface navMeshSurface;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSurface();
        }

        public void RebuildNavigation()
        {
            EnsureSurface();
            navMeshSurface.RemoveData();
            navMeshSurface.BuildNavMesh();
        }

        private void EnsureSurface()
        {
            if (navMeshSurface != null)
            {
                return;
            }

            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }

            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.layerMask = ~0;
        }
    }
}
