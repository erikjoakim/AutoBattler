using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class SceneBattleConfig
    {
        public FormationConfig formation;
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

            if (redTeam == null)
            {
                redTeam = new TeamConfig { units = Array.Empty<UnitSpawnConfig>() };
            }

            formation.Sanitize();
        }

        public static SceneBattleConfig CreateDefault(GameDataCatalog catalog)
        {
            return new SceneBattleConfig
            {
                formation = new FormationConfig(),
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

        public static UnitSpawnConfig FromTemplate(GameDataCatalog catalog, string templateId, string unitName, int count = 1)
        {
            if (!catalog.TryGetUnitTemplate(templateId, out var template))
            {
                throw new InvalidOperationException("Unknown unit template: " + templateId);
            }

            var ammunition = BuildDefaultAmmunition(catalog, template);

            return new UnitSpawnConfig
            {
                count = Mathf.Max(1, count),
                mission = template.Mission,
                definition = template.BuildDefinition(unitName, ammunition)
            };
        }

        private static AmmoDefinition[] BuildDefaultAmmunition(GameDataCatalog catalog, GameUnitTemplate template)
        {
            var ammoTypes = template.GetAmmunitionTypes();
            var ammunition = new AmmoDefinition[ammoTypes.Length];

            for (var i = 0; i < ammoTypes.Length; i++)
            {
                if (!catalog.TryGetAmmoTemplate(ammoTypes[i], out var ammoTemplate))
                {
                    ammunition[i] = new AmmoDefinition(ammoTypes[i], template.UnitType, 0, 0f, -1);
                    continue;
                }

                ammunition[i] = new AmmoDefinition(
                    ammoTemplate.AmmoName,
                    ammoTemplate.RequiredUserType,
                    ammoTemplate.Damage,
                    ammoTemplate.Radius,
                    ammoTemplate.AmmunitionCount);
            }

            return ammunition;
        }
    }
}
