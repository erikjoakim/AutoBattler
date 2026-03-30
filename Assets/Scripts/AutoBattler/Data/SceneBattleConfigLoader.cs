using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public static class SceneBattleConfigLoader
    {
        private const string SceneConfigResourcePath = "SceneConfigs/";
        private const string PlayerConfigResourceName = "PlayerUnits";

        public static SceneBattleConfig LoadForActiveScene()
        {
            return Load(SceneManager.GetActiveScene().name);
        }

        public static SceneBattleConfig Load(string sceneName)
        {
            var sceneConfigAsset = Resources.Load<TextAsset>(SceneConfigResourcePath + sceneName);
            var playerConfigAsset = Resources.Load<TextAsset>(SceneConfigResourcePath + PlayerConfigResourceName);
            return Load(sceneConfigAsset, playerConfigAsset, sceneName);
        }

        public static SceneBattleConfig Load(TextAsset sceneConfigAsset, TextAsset playerConfigAsset, string configName = null)
        {
            var catalog = GameDataCatalogLoader.Load();
            if (sceneConfigAsset == null)
            {
                var label = string.IsNullOrWhiteSpace(configName) ? "the requested config" : configName;
                Debug.LogWarning("No scene config found for " + label + ". Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            var sceneRoot = JsonDataHelper.AsObject(MiniJson.Deserialize(sceneConfigAsset.text));
            if (sceneRoot == null)
            {
                var label = string.IsNullOrWhiteSpace(configName) ? sceneConfigAsset.name : configName;
                Debug.LogWarning("Failed to parse scene config for " + label + ". Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            var playerRoot = playerConfigAsset == null ? null : JsonDataHelper.AsObject(MiniJson.Deserialize(playerConfigAsset.text));
            var config = new SceneBattleConfig
            {
                formation = ParseFormation(sceneRoot),
                terrainMovement = ParseTerrainMovement(sceneRoot),
                victoryLootTableId = JsonDataHelper.GetString(sceneRoot, "victoryLootTableId", string.Empty),
                defaultEnemyLootTableId = JsonDataHelper.GetString(sceneRoot, "defaultEnemyLootTableId", string.Empty),
                blueTeam = ParsePlayerTeam(playerRoot, sceneRoot, catalog),
                redTeam = ParseTeam(sceneRoot, "redTeam", catalog)
            };

            config.EnsureDefaults();
            if (CountConfiguredUnits(config) == 0)
            {
                var label = string.IsNullOrWhiteSpace(configName) ? sceneConfigAsset.name : configName;
                Debug.LogWarning("Scene config for " + label + " resolved to zero units. Using the built-in fallback config.");
                return SceneBattleConfig.CreateDefault(catalog);
            }

            return config;
        }

        private static TeamConfig ParsePlayerTeam(Dictionary<string, object> playerRoot, Dictionary<string, object> sceneRoot, GameDataCatalog catalog)
        {
            if (playerRoot != null)
            {
                if (TryParseTopLevelUnits(playerRoot, catalog, out var topLevelUnits))
                {
                    return new TeamConfig { units = topLevelUnits };
                }

                return ParseTeam(playerRoot, "blueTeam", catalog);
            }

            return ParseTeam(sceneRoot, "blueTeam", catalog);
        }

        private static bool TryParseTopLevelUnits(Dictionary<string, object> root, GameDataCatalog catalog, out UnitSpawnConfig[] units)
        {
            units = Array.Empty<UnitSpawnConfig>();
            if (root == null || !root.ContainsKey("units"))
            {
                return false;
            }

            var entries = JsonDataHelper.GetArray(root, "units");
            var resolvedUnits = new List<UnitSpawnConfig>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entryObject = JsonDataHelper.AsObject(entries[i]);
                if (entryObject == null)
                {
                    continue;
                }

                if (TryBuildUnitSpawnConfig(entryObject, catalog, out var unitSpawnConfig))
                {
                    resolvedUnits.Add(unitSpawnConfig);
                }
            }

            units = resolvedUnits.ToArray();
            return true;
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

        private static TerrainMovementConfig ParseTerrainMovement(Dictionary<string, object> root)
        {
            var terrainMovement = new TerrainMovementConfig();
            var mappings = JsonDataHelper.GetArray(root, "terrainMapping");
            var resolvedMappings = new List<TerrainLayerMappingConfig>();

            for (var i = 0; i < mappings.Count; i++)
            {
                var mappingObject = JsonDataHelper.AsObject(mappings[i]);
                if (mappingObject == null)
                {
                    continue;
                }

                var terrainLayer = JsonDataHelper.GetString(mappingObject, "terrainLayer", string.Empty);
                if (string.IsNullOrWhiteSpace(terrainLayer))
                {
                    continue;
                }

                resolvedMappings.Add(new TerrainLayerMappingConfig
                {
                    terrainLayer = terrainLayer,
                    terrainType = JsonDataHelper.GetString(mappingObject, "terrainType", terrainMovement.defaultTerrainType),
                    navArea = JsonDataHelper.GetString(mappingObject, "navArea", terrainMovement.defaultNavArea)
                });
            }

            terrainMovement.mappings = resolvedMappings.ToArray();
            terrainMovement.Sanitize();
            return terrainMovement;
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

        public static bool TryBuildUnitSpawnConfigFromSource(Dictionary<string, object> source, GameDataCatalog catalog, out UnitSpawnConfig unitSpawnConfig)
        {
            return TryBuildUnitSpawnConfig(source, catalog, out unitSpawnConfig);
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

            var resolvedAmmo = ResolveAmmunition(template, source, out var resolvedAmmoCounts);
            var resolvedTerrainSpeedProfile = ResolveTerrainSpeedProfile(template, source);
            var resolvedTerrainPathCostProfile = ResolveTerrainPathCostProfile(template, source);
            var definition = new UnitDefinition(
                templateId,
                string.IsNullOrWhiteSpace(resolvedName) ? template.UnitName : resolvedName,
                template.UnitType,
                Mathf.Max(1, JsonDataHelper.GetModifiedInt(source, "maxHealth", template.MaxHealth)),
                Mathf.Max(0, JsonDataHelper.GetModifiedInt(source, "armor", template.Armor)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "visionRange", template.VisionRange)),
                Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(source, "speed", template.Speed)),
                Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(source, "accuracy", template.Accuracy)),
                Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(source, "fireReliability", template.FireReliability)),
                Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(source, "moveReliability", template.MoveReliability)),
                JsonDataHelper.GetString(source, "navigationAgentType", template.NavigationAgentType),
                resolvedTerrainSpeedProfile,
                resolvedTerrainPathCostProfile,
                resolvedAmmoCounts,
                resolvedAmmo);

            unitSpawnConfig = new UnitSpawnConfig
            {
                count = Mathf.Max(1, JsonDataHelper.GetInt(source, "count", 1)),
                mission = resolvedMission,
                definition = definition,
                lootTableId = JsonDataHelper.GetString(source, "lootTableId", string.Empty)
            };

            return true;
        }

        private static TerrainSpeedProfile ResolveTerrainSpeedProfile(GameUnitTemplate template, Dictionary<string, object> source)
        {
            var overrides = JsonDataHelper.AsObject(source.TryGetValue("terrainSpeedModifiers", out var value) ? value : null);
            return template.TerrainSpeedProfile.WithOverrides(overrides);
        }

        private static TerrainSpeedProfile ResolveTerrainPathCostProfile(GameUnitTemplate template, Dictionary<string, object> source)
        {
            var overrides = JsonDataHelper.AsObject(source.TryGetValue("terrainPathCosts", out var value) ? value : null);
            return template.TerrainPathCostProfile.WithOverrides(overrides);
        }

        private static AmmoDefinition[] ResolveAmmunition(GameUnitTemplate template, Dictionary<string, object> source, out int[] resolvedAmmoCounts)
        {
            var ammoOverrideMap = BuildAmmoOverrideMap(source);
            var loadout = template.GetAmmunitionLoadout();
            var resolvedAmmo = new List<AmmoDefinition>(loadout.Length);
            resolvedAmmoCounts = new int[loadout.Length];
            var unitAttackRangeOverride = source != null && source.TryGetValue("attackRange", out var attackRangeValue) ? attackRangeValue : null;
            var unitReloadTimeOverride = source != null && source.TryGetValue("reloadTime", out var reloadTimeValue) ? reloadTimeValue : null;

            for (var i = 0; i < loadout.Length; i++)
            {
                var ammoType = loadout[i].AmmoType;
                ammoOverrideMap.TryGetValue(ammoType, out var ammoOverride);
                var baseAmmo = loadout[i].Definition;
                if (baseAmmo == null)
                {
                    resolvedAmmoCounts[i] = loadout[i].AmmunitionCount;
                    resolvedAmmo.Add(new AmmoDefinition(ammoType, template.UnitType, 0, 0f, 0.1f, 1f, 1f, 1f));
                    continue;
                }

                var baseAttackRange = JsonDataHelper.GetModifiedFloat(unitAttackRangeOverride, baseAmmo.AttackRange);
                var baseReloadTime = JsonDataHelper.GetModifiedFloat(unitReloadTimeOverride, baseAmmo.ReloadTime);
                resolvedAmmoCounts[i] = ResolveAmmoCount(ammoOverride, loadout[i].AmmunitionCount);
                resolvedAmmo.Add(new AmmoDefinition(
                    JsonDataHelper.GetString(ammoOverride, "ammoName", baseAmmo.AmmoName),
                    ammoOverride != null && ammoOverride.ContainsKey("requiredUserType")
                        ? JsonDataHelper.GetEnum(ammoOverride, "requiredUserType", baseAmmo.RequiredUserType)
                        : baseAmmo.RequiredUserType,
                    Mathf.Max(0, JsonDataHelper.GetModifiedInt(ammoOverride, "damage", baseAmmo.Damage)),
                    Mathf.Max(0f, JsonDataHelper.GetModifiedFloat(ammoOverride, "radius", baseAmmo.Radius)),
                    Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(ammoOverride, "attackRange", baseAttackRange)),
                    Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(ammoOverride, "reloadTime", baseReloadTime)),
                    Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(ammoOverride, "accuracy", baseAmmo.Accuracy)),
                    Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(ammoOverride, "damageReliability", baseAmmo.DamageReliability))));
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
