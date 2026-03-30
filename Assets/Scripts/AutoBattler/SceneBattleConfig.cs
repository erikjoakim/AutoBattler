using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class SceneBattleConfig
    {
        public FormationConfig formation;
        public TerrainMovementConfig terrainMovement;
        public TeamConfig blueTeam;
        public TeamConfig redTeam;

        public void EnsureDefaults()
        {
            if (formation == null)
            {
                formation = new FormationConfig();
            }

            if (blueTeam == null)
            {
                blueTeam = new TeamConfig { units = Array.Empty<UnitSpawnConfig>() };
            }

            if (terrainMovement == null)
            {
                terrainMovement = new TerrainMovementConfig();
            }

            if (redTeam == null)
            {
                redTeam = new TeamConfig { units = Array.Empty<UnitSpawnConfig>() };
            }

            formation.Sanitize();
            terrainMovement.Sanitize();
        }

        public static SceneBattleConfig CreateDefault(GameDataCatalog catalog)
        {
            return new SceneBattleConfig
            {
                formation = new FormationConfig(),
                terrainMovement = new TerrainMovementConfig(),
                blueTeam = TeamConfig.CreateDefaultBlue(catalog),
                redTeam = TeamConfig.CreateDefaultRed(catalog)
            };
        }
    }

    [Serializable]
    public sealed class FormationConfig
    {
        public int unitsPerRow = 4;
        public float lateralSpacing = 3.5f;
        public float forwardSpacing = 4f;
        public float distanceFromStartPoint = 6f;

        public void Sanitize()
        {
            unitsPerRow = Mathf.Max(1, unitsPerRow);
            lateralSpacing = Mathf.Max(1f, lateralSpacing);
            forwardSpacing = Mathf.Max(1f, forwardSpacing);
            distanceFromStartPoint = Mathf.Max(0f, distanceFromStartPoint);
        }
    }

    [Serializable]
    public sealed class TerrainMovementConfig
    {
        public float sampleCellSize = 2f;
        public string defaultTerrainType = "Grass";
        public string defaultNavArea = "GrassArea";
        public TerrainLayerMappingConfig[] mappings;

        public void Sanitize()
        {
            sampleCellSize = Mathf.Max(0.5f, sampleCellSize);
            defaultTerrainType = string.IsNullOrWhiteSpace(defaultTerrainType) ? "Grass" : defaultTerrainType;
            defaultNavArea = string.IsNullOrWhiteSpace(defaultNavArea) ? "GrassArea" : defaultNavArea;
            mappings ??= Array.Empty<TerrainLayerMappingConfig>();
        }
    }

    [Serializable]
    public sealed class TerrainLayerMappingConfig
    {
        public string terrainLayer;
        public string terrainType = "Grass";
        public string navArea = "GrassArea";
    }

    [Serializable]
    public sealed class TeamConfig
    {
        public UnitSpawnConfig[] units;

        public static TeamConfig CreateDefaultBlue(GameDataCatalog catalog)
        {
            return CreateDefaultTeam(catalog, true);
        }

        public static TeamConfig CreateDefaultRed(GameDataCatalog catalog)
        {
            return CreateDefaultTeam(catalog, false);
        }

        private static TeamConfig CreateDefaultTeam(GameDataCatalog catalog, bool isBlue)
        {
            return new TeamConfig
            {
                units = new[]
                {
                    UnitSpawnConfig.FromTemplate(catalog, "Guard Tank", isBlue ? "Blue Guard Tank" : "Red Guard Tank"),
                    UnitSpawnConfig.FromTemplate(catalog, "Assault Tank", isBlue ? "Blue Assault Tank" : "Red Assault Tank"),
                    UnitSpawnConfig.FromTemplate(catalog, "Guard Infantry", isBlue ? "Blue Guard Infantry" : "Red Guard Infantry", 2),
                    UnitSpawnConfig.FromTemplate(catalog, "Raider Infantry", isBlue ? "Blue Raiders" : "Red Raiders", 2)
                }
            };
        }
    }

    [Serializable]
    public sealed class UnitSpawnConfig
    {
        public int count;
        public MissionType mission;
        public UnitDefinition definition;
        public string ownedUnitCardId;
        public string lootTableId;

        public static UnitSpawnConfig FromTemplate(GameDataCatalog catalog, string templateId, string unitName, int count = 1)
        {
            if (!catalog.TryGetUnitTemplate(templateId, out var template))
            {
                throw new InvalidOperationException("Unknown unit template: " + templateId);
            }

            var ammunition = BuildDefaultAmmunition(template, out var ammunitionCounts);

            return new UnitSpawnConfig
            {
                count = Mathf.Max(1, count),
                mission = template.Mission,
                definition = template.BuildDefinition(unitName, ammunition, ammunitionCounts)
            };
        }

        private static AmmoDefinition[] BuildDefaultAmmunition(GameUnitTemplate template, out int[] ammunitionCounts)
        {
            var loadout = template.GetAmmunitionLoadout();
            var ammunition = new AmmoDefinition[loadout.Length];
            ammunitionCounts = new int[loadout.Length];

            for (var i = 0; i < loadout.Length; i++)
            {
                ammunitionCounts[i] = loadout[i].AmmunitionCount;
                var ammoDefinition = loadout[i].Definition;
                ammunition[i] = ammoDefinition == null
                    ? new AmmoDefinition(loadout[i].AmmoType, template.UnitType, 0, 0f, 0.1f, 1f, 1f, 1f)
                    : new AmmoDefinition(
                        ammoDefinition.AmmoName,
                        ammoDefinition.RequiredUserType,
                        ammoDefinition.Damage,
                        ammoDefinition.Radius,
                        ammoDefinition.AttackRange,
                        ammoDefinition.ReloadTime,
                        ammoDefinition.Accuracy,
                        ammoDefinition.DamageReliability);
            }

            return ammunition;
        }
    }
}
