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
            public int maxSpawns;
            [NonSerialized] public UnitSpawnConfig unitSpawnConfig;
        }

        [SerializeField] private string spawnerId = "EnemySpawner";
        [SerializeField] private TextAsset spawnerConfigAsset;
        [SerializeField] private float spawnRadius = 4f;
        [SerializeField, HideInInspector] private float startTime;
        [SerializeField, HideInInspector] private float spawnInterval = 10f;
        [SerializeField, HideInInspector] private int initialBurstCount;
        [SerializeField, HideInInspector] private int countPerWave = 1;
        [SerializeField, HideInInspector] private int totalSpawnLimit = 10;
        [SerializeField, HideInInspector] private int aliveUnitCap = 4;
        [SerializeField, HideInInspector] private MissionType spawnedUnitMission = MissionType.SeekAndDestroy;
        [SerializeField, HideInInspector] private string targetVictoryPointId = string.Empty;
        [SerializeField, HideInInspector] private bool isDestructible;
        [SerializeField, HideInInspector] private int maxHealth = 20;
        [SerializeField, HideInInspector] private int armor = 2;
        [SerializeField, HideInInspector] private bool destroyedStopsSpawning = true;
        [SerializeField, HideInInspector] private SpawnEntry[] spawnTable = Array.Empty<SpawnEntry>();

        public string SpawnerId => GetResolvedConfig().spawnerId;
        public TextAsset SpawnerConfigAsset => spawnerConfigAsset;
        public float SpawnRadius => Mathf.Max(0.5f, spawnRadius);
        public float StartTime => GetResolvedConfig().startTime;
        public float SpawnInterval => GetResolvedConfig().spawnInterval;
        public int InitialBurstCount => GetResolvedConfig().initialBurstCount;
        public int CountPerWave => GetResolvedConfig().countPerWave;
        public int TotalSpawnLimit => GetResolvedConfig().totalSpawnLimit;
        public int AliveUnitCap => GetResolvedConfig().aliveUnitCap;
        public MissionType SpawnedUnitMission => GetResolvedConfig().spawnedUnitMission;
        public string TargetVictoryPointId => GetResolvedConfig().targetVictoryPointId;
        public bool IsDestructible => GetResolvedConfig().isDestructible;
        public int MaxHealth => GetResolvedConfig().maxHealth;
        public int Armor => GetResolvedConfig().armor;
        public bool DestroyedStopsSpawning => GetResolvedConfig().destroyedStopsSpawning;
        public SpawnEntry[] SpawnTable => GetResolvedConfig().spawnTable;

        internal string LegacySpawnerId => string.IsNullOrWhiteSpace(spawnerId) ? name : spawnerId;
        internal float LegacyStartTime => Mathf.Max(0f, startTime);
        internal float LegacySpawnInterval => Mathf.Max(0.1f, spawnInterval);
        internal int LegacyInitialBurstCount => Mathf.Max(0, initialBurstCount);
        internal int LegacyCountPerWave => Mathf.Max(1, countPerWave);
        internal int LegacyTotalSpawnLimit => Mathf.Max(0, totalSpawnLimit);
        internal int LegacyAliveUnitCap => Mathf.Max(1, aliveUnitCap);
        internal MissionType LegacySpawnedUnitMission => spawnedUnitMission;
        internal string LegacyTargetVictoryPointId => targetVictoryPointId;
        internal bool LegacyIsDestructible => isDestructible;
        internal int LegacyMaxHealth => Mathf.Max(1, maxHealth);
        internal int LegacyArmor => Mathf.Max(0, armor);
        internal bool LegacyDestroyedStopsSpawning => destroyedStopsSpawning;
        internal SpawnEntry[] LegacySpawnTable => spawnTable ?? Array.Empty<SpawnEntry>();

        private EnemySpawnerConfig resolvedConfig;
        private TextAsset cachedConfigAsset;
        private string cachedConfigText;

        private void Reset()
        {
            spawnerId = name;
            spawnerConfigAsset = null;
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

        public bool TryGetResolvedConfig(out EnemySpawnerConfig config, out string warning)
        {
            warning = string.Empty;
            if (spawnerConfigAsset != null)
            {
                if (EnemySpawnerConfigLoader.TryLoad(spawnerConfigAsset, out config, out var loadError))
                {
                    if (string.IsNullOrWhiteSpace(config.spawnerId))
                    {
                        config.spawnerId = LegacySpawnerId;
                    }

                    config.Sanitize();
                    return true;
                }

                config = EnemySpawnerConfig.CreateFallback(this);
                warning = loadError;
                return false;
            }

            config = EnemySpawnerConfig.CreateFallback(this);
            warning = "No spawner config asset assigned. Using legacy fallback values.";
            return false;
        }

        private EnemySpawnerConfig GetResolvedConfig()
        {
            var currentText = spawnerConfigAsset != null ? spawnerConfigAsset.text : string.Empty;
            if (resolvedConfig != null
                && cachedConfigAsset == spawnerConfigAsset
                && string.Equals(cachedConfigText, currentText, StringComparison.Ordinal))
            {
                return resolvedConfig;
            }

            TryGetResolvedConfig(out resolvedConfig, out _);
            cachedConfigAsset = spawnerConfigAsset;
            cachedConfigText = currentText;
            return resolvedConfig ?? EnemySpawnerConfig.CreateFallback(this);
        }
    }
}
