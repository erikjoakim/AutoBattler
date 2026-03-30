using System;
using UnityEngine;

namespace AutoBattler
{
    public sealed class EnemySpawnerMarker : MonoBehaviour
    {
        [Serializable]
        public struct SpawnEntry
        {
            public string unitType;
            public int weight;
        }

        [SerializeField] private string spawnerId = "EnemySpawner";
        [SerializeField] private float startTime;
        [SerializeField] private float spawnInterval = 10f;
        [SerializeField] private int initialBurstCount;
        [SerializeField] private int countPerWave = 1;
        [SerializeField] private int totalSpawnLimit = 10;
        [SerializeField] private int aliveUnitCap = 4;
        [SerializeField] private float spawnRadius = 4f;
        [SerializeField] private MissionType spawnedUnitMission = MissionType.SeekAndDestroy;
        [SerializeField] private string targetVictoryPointId = string.Empty;
        [SerializeField] private bool isDestructible;
        [SerializeField] private int maxHealth = 20;
        [SerializeField] private int armor = 2;
        [SerializeField] private bool destroyedStopsSpawning = true;
        [SerializeField] private SpawnEntry[] spawnTable = Array.Empty<SpawnEntry>();

        public string SpawnerId => string.IsNullOrWhiteSpace(spawnerId) ? name : spawnerId;
        public float StartTime => Mathf.Max(0f, startTime);
        public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
        public int InitialBurstCount => Mathf.Max(0, initialBurstCount);
        public int CountPerWave => Mathf.Max(1, countPerWave);
        public int TotalSpawnLimit => Mathf.Max(0, totalSpawnLimit);
        public int AliveUnitCap => Mathf.Max(1, aliveUnitCap);
        public float SpawnRadius => Mathf.Max(0.5f, spawnRadius);
        public MissionType SpawnedUnitMission => spawnedUnitMission;
        public string TargetVictoryPointId => targetVictoryPointId;
        public bool IsDestructible => isDestructible;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int Armor => Mathf.Max(0, armor);
        public bool DestroyedStopsSpawning => destroyedStopsSpawning;
        public SpawnEntry[] SpawnTable => spawnTable ?? Array.Empty<SpawnEntry>();

        private void Reset()
        {
            spawnerId = name;
            spawnInterval = 10f;
            countPerWave = 1;
            totalSpawnLimit = 10;
            aliveUnitCap = 4;
            spawnRadius = 4f;
            spawnedUnitMission = MissionType.SeekAndDestroy;
            maxHealth = 20;
            armor = 2;
            destroyedStopsSpawning = true;
            spawnTable = Array.Empty<SpawnEntry>();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.15f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, SpawnRadius);

            Gizmos.color = new Color(1f, 0.5f, 0.15f, 0.2f);
            Gizmos.DrawSphere(transform.position, 0.25f);
        }
    }
}
