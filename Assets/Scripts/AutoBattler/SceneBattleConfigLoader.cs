using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public static class SceneBattleConfigLoader
    {
        private const string SceneConfigResourcePath = "SceneConfigs/";

        public static SceneBattleConfig LoadForActiveScene()
        {
            return Load(SceneManager.GetActiveScene().name);
        }

        public static SceneBattleConfig Load(string sceneName)
        {
            var catalog = GameDataCatalogLoader.Load();
            var configAsset = Resources.Load<TextAsset>(SceneConfigResourcePath + sceneName);
            if (configAsset == null)
            {
                Debug.LogWarning("No scene config found for " + sceneName + ". Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(configAsset.text));
            if (root == null)
            {
                Debug.LogWarning("Failed to parse scene config for " + sceneName + ". Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            var config = new SceneBattleConfig
            {
                formation = ParseFormation(root),
                blueTeam = ParseTeam(root, "blueTeam", catalog),
                redTeam = ParseTeam(root, "redTeam", catalog)
            };

            config.EnsureDefaults();
            if (CountConfiguredUnits(config) == 0)
            {
                Debug.LogWarning("Scene config for " + sceneName + " resolved to zero units. Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            return config;
        }

        private static FormationConfig ParseFormation(Dictionary<string, object> root)
        {
            var formationObject = JsonDataHelper.AsObject(root.TryGetValue("formation", out var value) ? value : null);
            var formation = new FormationConfig();
            if (formationObject == null)
            {
                formation.Sanitize();
                return formation;
            }

            formation.unitsPerRow = Mathf.Max(1, JsonDataHelper.GetInt(formationObject, "unitsPerRow", formation.unitsPerRow));
            formation.lateralSpacing = Mathf.Max(1f, JsonDataHelper.GetFloat(formationObject, "lateralSpacing", formation.lateralSpacing));
            formation.forwardSpacing = Mathf.Max(1f, JsonDataHelper.GetFloat(formationObject, "forwardSpacing", formation.forwardSpacing));
            formation.distanceFromStartPoint = Mathf.Max(0f, JsonDataHelper.GetFloat(formationObject, "distanceFromStartPoint", formation.distanceFromStartPoint));
            return formation;
        }

        private static TeamConfig ParseTeam(Dictionary<string, object> root, string key, GameDataCatalog catalog)
        {
            var teamObject = JsonDataHelper.AsObject(root.TryGetValue(key, out var value) ? value : null);
            var entries = teamObject == null ? new List<object>() : JsonDataHelper.GetArray(teamObject, "units");
            var units = new List<UnitSpawnConfig>();

            for (var i = 0; i < entries.Count; i++)
            {
                var entryObject = JsonDataHelper.AsObject(entries[i]);
                if (entryObject == null)
                {
                    continue;
                }

                if (TryBuildUnitSpawnConfig(entryObject, catalog, out var unitSpawnConfig))
                {
                    units.Add(unitSpawnConfig);
                }
            }

            return new TeamConfig { units = units.ToArray() };
        }

        private static bool TryBuildUnitSpawnConfig(Dictionary<string, object> source, GameDataCatalog catalog, out UnitSpawnConfig unitSpawnConfig)
        {
            unitSpawnConfig = null;

            var templateId = JsonDataHelper.GetString(source, "unitType", string.Empty);
            if (string.IsNullOrWhiteSpace(templateId) || !catalog.TryGetUnitTemplate(templateId, out var template))
            {
                Debug.LogWarning("Unknown scene unit template: " + templateId);
                return false;
            }

            var resolvedName = JsonDataHelper.GetString(source, "unitName", template.UnitName);
            var resolvedMission = source.ContainsKey("mission")
                ? JsonDataHelper.GetEnum(source, "mission", template.Mission)
                : template.Mission;

            var resolvedAmmo = ResolveAmmunition(template, source, catalog);
            var definition = new UnitDefinition(
                string.IsNullOrWhiteSpace(resolvedName) ? template.UnitName : resolvedName,
                template.UnitType,
                Mathf.Max(1, JsonDataHelper.GetModifiedInt(source, "maxHealth", template.MaxHealth)),
                Mathf.Max(0, JsonDataHelper.GetModifiedInt(source, "armor", template.Armor)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "visionRange", template.VisionRange)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "attackRange", template.AttackRange)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "speed", template.Speed)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "reloadTime", template.ReloadTime)),
                JsonDataHelper.GetString(source, "navigationAgentType", template.NavigationAgentType),
                resolvedAmmo);

            unitSpawnConfig = new UnitSpawnConfig
            {
                count = Mathf.Max(1, JsonDataHelper.GetInt(source, "count", 1)),
                mission = resolvedMission,
                definition = definition
            };

            return true;
        }

        private static AmmoDefinition[] ResolveAmmunition(GameUnitTemplate template, Dictionary<string, object> source, GameDataCatalog catalog)
        {
            var ammoOverrideMap = BuildAmmoOverrideMap(source);
            var ammoTypes = template.GetAmmunitionTypes();
            var resolvedAmmo = new List<AmmoDefinition>(ammoTypes.Length);

            for (var i = 0; i < ammoTypes.Length; i++)
            {
                var ammoType = ammoTypes[i];
                if (!catalog.TryGetAmmoTemplate(ammoType, out var ammoTemplate))
                {
                    Debug.LogWarning("Unknown ammo template: " + ammoType + " on unit template " + template.UnitTypeKey);
                    continue;
                }

                ammoOverrideMap.TryGetValue(ammoType, out var ammoOverride);
                resolvedAmmo.Add(new AmmoDefinition(
                    JsonDataHelper.GetString(ammoOverride, "ammoName", ammoTemplate.AmmoName),
                    ammoOverride != null && ammoOverride.ContainsKey("requiredUserType")
                        ? JsonDataHelper.GetEnum(ammoOverride, "requiredUserType", ammoTemplate.RequiredUserType)
                        : ammoTemplate.RequiredUserType,
                    Mathf.Max(0, JsonDataHelper.GetModifiedInt(ammoOverride, "damage", ammoTemplate.Damage)),
                    Mathf.Max(0f, JsonDataHelper.GetModifiedFloat(ammoOverride, "radius", ammoTemplate.Radius)),
                    ResolveAmmoCount(ammoOverride, ammoTemplate.AmmunitionCount)));
            }

            return resolvedAmmo.ToArray();
        }

        private static Dictionary<string, Dictionary<string, object>> BuildAmmoOverrideMap(Dictionary<string, object> source)
        {
            var map = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            var ammunition = JsonDataHelper.GetArray(source, "ammunition");

            for (var i = 0; i < ammunition.Count; i++)
            {
                var ammoObject = JsonDataHelper.AsObject(ammunition[i]);
                if (ammoObject == null)
                {
                    continue;
                }

                var ammoType = JsonDataHelper.GetString(ammoObject, "ammoType", string.Empty);
                if (string.IsNullOrWhiteSpace(ammoType))
                {
                    ammoType = JsonDataHelper.GetString(ammoObject, "ammoName", string.Empty);
                }

                if (!string.IsNullOrWhiteSpace(ammoType))
                {
                    map[ammoType] = ammoObject;
                }
            }

            return map;
        }

        private static int ResolveAmmoCount(Dictionary<string, object> ammoOverride, int baseAmmoCount)
        {
            var resolvedCount = JsonDataHelper.GetModifiedInt(ammoOverride, "ammunitionCount", baseAmmoCount);
            return resolvedCount < 0 ? -1 : resolvedCount;
        }

        private static int CountConfiguredUnits(SceneBattleConfig config)
        {
            return CountTeamUnits(config.blueTeam) + CountTeamUnits(config.redTeam);
        }

        private static int CountTeamUnits(TeamConfig team)
        {
            if (team == null || team.units == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < team.units.Length; i++)
            {
                if (team.units[i] == null)
                {
                    continue;
                }

                total += Mathf.Max(0, team.units[i].count);
            }

            return total;
        }
    }
}
