using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class GameDataCatalog
    {
        private readonly Dictionary<string, GameAmmoTemplate> ammoTemplates;
        private readonly Dictionary<string, GameUnitTemplate> unitTemplates;

        public GameDataCatalog(Dictionary<string, GameAmmoTemplate> ammoTemplates, Dictionary<string, GameUnitTemplate> unitTemplates)
        {
            this.ammoTemplates = ammoTemplates;
            this.unitTemplates = unitTemplates;
        }

        public bool TryGetAmmoTemplate(string ammoType, out GameAmmoTemplate template)
        {
            return ammoTemplates.TryGetValue(ammoType, out template);
        }

        public bool TryGetUnitTemplate(string unitType, out GameUnitTemplate template)
        {
            return unitTemplates.TryGetValue(unitType, out template);
        }

        public static GameDataCatalog CreateDefault()
        {
            var ammoTemplates = new Dictionary<string, GameAmmoTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                { "Tank Cannon", new GameAmmoTemplate("Tank Cannon", "Tank Cannon", UnitType.Tank, 6, 0.6f, 10f, 1.8f, 0.8f, 0.96f) },
                { "Tank Shell", new GameAmmoTemplate("Tank Shell", "Tank Shell", UnitType.Tank, 4, 2.5f, 10f, 1.8f, 0.72f, 0.93f) },
                { "Rifle Burst", new GameAmmoTemplate("Rifle Burst", "Rifle Burst", UnitType.Infantry, 2, 0.4f, 6f, 1.1f, 0.78f, 0.99f) },
                { "Grenade", new GameAmmoTemplate("Grenade", "Grenade", UnitType.Infantry, 3, 1.8f, 6f, 1.1f, 0.62f, 0.95f) }
            };

            var unitTemplates = new Dictionary<string, GameUnitTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "Guard Tank",
                    new GameUnitTemplate(
                        "Guard Tank",
                        "Guard Tank",
                        UnitType.Tank,
                        MissionType.Guard,
                        20,
                        2,
                        14f,
                        2.2f,
                        0.95f,
                        0.96f,
                        0.94f,
                        string.Empty,
                        CreateTerrainSpeedProfile(("Road", 1.3f), ("Grass", 1f), ("Mud", 0.65f), ("Rock", 0.75f)),
                        CreateTerrainSpeedProfile(("Road", 1f), ("Grass", 2.5f), ("Mud", 5f), ("Rock", 3.5f)),
                        new[]
                        {
                            CreateLoadout(ammoTemplates["Tank Cannon"], -1),
                            CreateLoadout(ammoTemplates["Tank Shell"], -1)
                        })
                },
                {
                    "Assault Tank",
                    new GameUnitTemplate(
                        "Assault Tank",
                        "Assault Tank",
                        UnitType.Tank,
                        MissionType.SeekAndDestroy,
                        20,
                        2,
                        14f,
                        2.4f,
                        1f,
                        0.98f,
                        0.9f,
                        "TankAgent",
                        CreateTerrainSpeedProfile(("Road", 1.35f), ("Grass", 1f), ("Mud", 0.6f), ("Rock", 0.75f)),
                        CreateTerrainSpeedProfile(("Road", 1f), ("Grass", 3f), ("Mud", 6f), ("Rock", 4f)),
                        new[]
                        {
                            CreateLoadout(ammoTemplates["Tank Cannon"], -1, reloadTime: 1.7f),
                            CreateLoadout(ammoTemplates["Tank Shell"], -1, reloadTime: 1.7f)
                        })
                },
                {
                    "Guard Infantry",
                    new GameUnitTemplate(
                        "Guard Infantry",
                        "Guard Infantry",
                        UnitType.Infantry,
                        MissionType.Guard,
                        8,
                        0,
                        10f,
                        3.4f,
                        0.98f,
                        0.99f,
                        0.97f,
                        string.Empty,
                        CreateTerrainSpeedProfile(("Road", 1.15f), ("Grass", 1f), ("Mud", 0.85f), ("Rock", 0.95f)),
                        CreateTerrainSpeedProfile(("Road", 1f), ("Grass", 1.4f), ("Mud", 2.2f), ("Rock", 1.8f)),
                        new[]
                        {
                            CreateLoadout(ammoTemplates["Rifle Burst"], -1),
                            CreateLoadout(ammoTemplates["Grenade"], 3)
                        })
                },
                {
                    "Raider Infantry",
                    new GameUnitTemplate(
                        "Raider Infantry",
                        "Raider Infantry",
                        UnitType.Infantry,
                        MissionType.SeekAndDestroy,
                        8,
                        0,
                        10f,
                        3.8f,
                        0.92f,
                        0.95f,
                        0.95f,
                        string.Empty,
                        CreateTerrainSpeedProfile(("Road", 1.2f), ("Grass", 1f), ("Mud", 0.9f), ("Rock", 1f)),
                        CreateTerrainSpeedProfile(("Road", 1f), ("Grass", 1.3f), ("Mud", 2f), ("Rock", 1.4f)),
                        new[]
                        {
                            CreateLoadout(ammoTemplates["Rifle Burst"], -1, reloadTime: 1f),
                            CreateLoadout(ammoTemplates["Grenade"], 4, reloadTime: 1f)
                        })
                }
            };

            return new GameDataCatalog(ammoTemplates, unitTemplates);
        }

        private static GameUnitAmmoLoadout CreateLoadout(
            GameAmmoTemplate template,
            int ammunitionCount,
            float? attackRange = null,
            float? reloadTime = null,
            float? accuracy = null,
            float? damageReliability = null)
        {
            return new GameUnitAmmoLoadout(
                template.AmmoType,
                new AmmoDefinition(
                    template.AmmoName,
                    template.RequiredUserType,
                    template.Damage,
                    template.Radius,
                    attackRange ?? template.AttackRange,
                    reloadTime ?? template.ReloadTime,
                    accuracy ?? template.Accuracy,
                    damageReliability ?? template.DamageReliability),
                ammunitionCount);
        }

        private static TerrainSpeedProfile CreateTerrainSpeedProfile(params (string TerrainType, float Modifier)[] entries)
        {
            var modifiers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Length; i++)
            {
                modifiers[entries[i].TerrainType] = entries[i].Modifier;
            }

            return new TerrainSpeedProfile(modifiers);
        }
    }

    public sealed class GameAmmoTemplate
    {
        public GameAmmoTemplate(
            string ammoType,
            string ammoName,
            UnitType requiredUserType,
            int damage,
            float radius,
            float attackRange,
            float reloadTime,
            float accuracy,
            float damageReliability)
        {
            AmmoType = ammoType;
            AmmoName = ammoName;
            RequiredUserType = requiredUserType;
            Damage = damage;
            Radius = radius;
            AttackRange = attackRange;
            ReloadTime = reloadTime;
            Accuracy = accuracy;
            DamageReliability = damageReliability;
        }

        public string AmmoType { get; }
        public string AmmoName { get; }
        public UnitType RequiredUserType { get; }
        public int Damage { get; }
        public float Radius { get; }
        public float AttackRange { get; }
        public float ReloadTime { get; }
        public float Accuracy { get; }
        public float DamageReliability { get; }
    }

    public readonly struct GameUnitAmmoLoadout
    {
        public GameUnitAmmoLoadout(string ammoType, AmmoDefinition definition, int ammunitionCount)
        {
            AmmoType = ammoType;
            Definition = definition;
            AmmunitionCount = ammunitionCount;
        }

        public string AmmoType { get; }
        public AmmoDefinition Definition { get; }
        public int AmmunitionCount { get; }
    }

    public sealed class GameUnitTemplate
    {
        private readonly GameUnitAmmoLoadout[] ammunitionLoadout;

        public GameUnitTemplate(
            string unitTypeKey,
            string unitName,
            UnitType unitType,
            MissionType mission,
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
            GameUnitAmmoLoadout[] ammunitionLoadout)
        {
            UnitTypeKey = unitTypeKey;
            UnitName = unitName;
            UnitType = unitType;
            Mission = mission;
            MaxHealth = maxHealth;
            Armor = armor;
            VisionRange = visionRange;
            Speed = speed;
            Accuracy = accuracy;
            FireReliability = fireReliability;
            MoveReliability = moveReliability;
            NavigationAgentType = navigationAgentType;
            TerrainSpeedProfile = terrainSpeedProfile ?? TerrainSpeedProfile.Empty;
            TerrainPathCostProfile = terrainPathCostProfile ?? TerrainSpeedProfile.Empty;
            this.ammunitionLoadout = ammunitionLoadout ?? Array.Empty<GameUnitAmmoLoadout>();
        }

        public string UnitTypeKey { get; }
        public string UnitName { get; }
        public UnitType UnitType { get; }
        public MissionType Mission { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public float VisionRange { get; }
        public float Speed { get; }
        public float Accuracy { get; }
        public float FireReliability { get; }
        public float MoveReliability { get; }
        public string NavigationAgentType { get; }
        public TerrainSpeedProfile TerrainSpeedProfile { get; }
        public TerrainSpeedProfile TerrainPathCostProfile { get; }

        public UnitDefinition BuildDefinition(string resolvedUnitName, AmmoDefinition[] ammunition, int[] ammunitionCounts)
        {
            return new UnitDefinition(
                string.IsNullOrWhiteSpace(resolvedUnitName) ? UnitName : resolvedUnitName,
                UnitType,
                MaxHealth,
                Armor,
                VisionRange,
                Speed,
                Accuracy,
                FireReliability,
                MoveReliability,
                NavigationAgentType,
                TerrainSpeedProfile,
                TerrainPathCostProfile,
                ammunitionCounts,
                ammunition);
        }

        public GameUnitAmmoLoadout[] GetAmmunitionLoadout()
        {
            return ammunitionLoadout;
        }
    }
}
