using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class UnitDefinition
    {
        [SerializeField] private string templateId;
        [SerializeField] private string unitName;
        [SerializeField] private UnitType unitType;
        [SerializeField] private int maxHealth = 1;
        [SerializeField] private int armor;
        [SerializeField] private float visionRange = 5f;
        [SerializeField] private float speed = 3f;
        [SerializeField] private float accuracy = 1f;
        [SerializeField] private float fireReliability = 1f;
        [SerializeField] private float moveReliability = 1f;
        [SerializeField] private string navigationAgentType;
        [SerializeField] private AmmoDefinition[] ammunition;
        [SerializeField] private int[] ammunitionCounts;
        private readonly TerrainSpeedProfile terrainSpeedProfile;
        private readonly TerrainSpeedProfile terrainPathCostProfile;

        public UnitDefinition(
            string templateId,
            string unitName,
            UnitType unitType,
            int maxHealth,
            int armor,
            float visionRange,
            float speed,
            float accuracy,
            float fireReliability,
            float moveReliability,
            string navigationAgentType,
            TerrainSpeedProfile terrainSpeedProfile,
            TerrainSpeedProfile terrainPathCostProfile,
            int[] ammunitionCounts,
            params AmmoDefinition[] ammunition)
        {
            this.templateId = templateId;
            this.unitName = unitName;
            this.unitType = unitType;
            this.maxHealth = maxHealth;
            this.armor = armor;
            this.visionRange = visionRange;
            this.speed = speed;
            this.accuracy = accuracy;
            this.fireReliability = fireReliability;
            this.moveReliability = moveReliability;
            this.navigationAgentType = navigationAgentType;
            this.terrainSpeedProfile = terrainSpeedProfile ?? TerrainSpeedProfile.Empty;
            this.terrainPathCostProfile = terrainPathCostProfile ?? TerrainSpeedProfile.Empty;
            this.ammunition = ammunition;
            this.ammunitionCounts = ammunitionCounts ?? Array.Empty<int>();
        }

        public string TemplateId => templateId;
        public string UnitName => unitName;
        public UnitType UnitType => unitType;
        public int MaxHealth => maxHealth;
        public int Armor => armor;
        public float VisionRange => visionRange;
        public float Speed => speed;
        public float Accuracy => accuracy;
        public float FireReliability => fireReliability;
        public float MoveReliability => moveReliability;
        public string NavigationAgentType => navigationAgentType;
        public AmmoDefinition[] Ammunition => ammunition;
        public int[] AmmunitionCounts => ammunitionCounts;
        public TerrainSpeedProfile TerrainSpeedProfile => terrainSpeedProfile;
        public TerrainSpeedProfile TerrainPathCostProfile => terrainPathCostProfile;
    }
}
