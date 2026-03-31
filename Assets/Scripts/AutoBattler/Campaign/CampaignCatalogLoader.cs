using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class CampaignCatalogs
    {
        public CampaignCatalogs(
            Dictionary<string, MapDefinition> mapDefinitions,
            Dictionary<string, UnitCardDefinition> unitCardDefinitions,
            StartingLoadoutDefinition startingLoadout,
            Dictionary<string, MapModifierTemplateDefinition> mapModifierTemplates)
        {
            MapDefinitions = mapDefinitions ?? new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
            UnitCardDefinitions = unitCardDefinitions ?? new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase);
            StartingLoadout = startingLoadout ?? new StartingLoadoutDefinition();
            MapModifierTemplates = mapModifierTemplates ?? new Dictionary<string, MapModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, MapDefinition> MapDefinitions { get; }
        public Dictionary<string, UnitCardDefinition> UnitCardDefinitions { get; }
        public StartingLoadoutDefinition StartingLoadout { get; }
        public Dictionary<string, MapModifierTemplateDefinition> MapModifierTemplates { get; }

        public bool TryGetMapDefinition(string mapDefinitionId, out MapDefinition definition)
        {
            return MapDefinitions.TryGetValue(mapDefinitionId ?? string.Empty, out definition);
        }

        public bool TryGetUnitCardDefinition(string definitionId, out UnitCardDefinition definition)
        {
            return UnitCardDefinitions.TryGetValue(definitionId ?? string.Empty, out definition);
        }

        public bool TryGetMapModifierTemplate(string mapModifierTemplateId, out MapModifierTemplateDefinition definition)
        {
            return MapModifierTemplates.TryGetValue(mapModifierTemplateId ?? string.Empty, out definition);
        }
    }

    public static class CampaignCatalogLoader
    {
        private const string MapDefinitionsPath = "Campaign/MapDefinitions";
        private const string StartingLoadoutPath = "Campaign/StartingLoadout";
        private const string MapModifierTemplatesPath = "Campaign/MapModifierTemplates";

        public static CampaignCatalogs Load()
        {
            var mapDefinitions = LoadMapDefinitions();
            var unitCardDefinitions = BuildUnitCardDefinitionsFromGameUnits();
            var startingLoadout = LoadStartingLoadout();
            var mapModifierTemplates = LoadMapModifierTemplates();
            return new CampaignCatalogs(mapDefinitions, unitCardDefinitions, startingLoadout, mapModifierTemplates);
        }

        private static Dictionary<string, MapDefinition> LoadMapDefinitions()
        {
            var asset = Resources.Load<TextAsset>(MapDefinitionsPath);
            if (asset == null)
            {
                return CreateDefaultMapDefinitions();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "maps");
            var definitions = new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var mapDefinitionId = JsonDataHelper.GetString(item, "mapDefinitionId", string.Empty);
                var sceneName = JsonDataHelper.GetString(item, "sceneName", string.Empty);
                if (string.IsNullOrWhiteSpace(mapDefinitionId) || string.IsNullOrWhiteSpace(sceneName))
                {
                    continue;
                }

                definitions[mapDefinitionId] = new MapDefinition
                {
                    mapDefinitionId = mapDefinitionId,
                    displayName = JsonDataHelper.GetString(item, "displayName", mapDefinitionId),
                    sceneName = sceneName,
                    description = JsonDataHelper.GetString(item, "description", string.Empty),
                    tier = Mathf.Max(1, JsonDataHelper.GetInt(item, "tier", 1)),
                    baseLootTableId = JsonDataHelper.GetString(item, "baseLootTableId", string.Empty)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultMapDefinitions();
        }

        private static Dictionary<string, UnitCardDefinition> BuildUnitCardDefinitionsFromGameUnits()
        {
            var gameCatalog = GameDataCatalogLoader.Load();
            var definitions = new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var template in gameCatalog.GetUnitTemplates())
            {
                if (template == null || string.IsNullOrWhiteSpace(template.UnitTypeKey))
                {
                    continue;
                }

                definitions[template.UnitTypeKey] = new UnitCardDefinition
                {
                    unitCardDefinitionId = template.UnitTypeKey,
                    displayName = string.IsNullOrWhiteSpace(template.UnitName) ? template.UnitTypeKey : template.UnitName,
                    baseTemplateId = template.UnitTypeKey,
                    purchaseCostGold = Mathf.Max(0, template.PurchaseCostGold),
                    defaultItemSlots = new List<string> { "Utility", "Weapon" }
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultUnitCardDefinitions();
        }

        private static Dictionary<string, MapDefinition> CreateDefaultMapDefinitions()
        {
            return new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["sample_operation"] = new MapDefinition
                {
                    mapDefinitionId = "sample_operation",
                    displayName = "Sample Operation",
                    sceneName = "SampleScene",
                    description = "Secure the objective and keep the roster alive.",
                    tier = 1,
                    baseLootTableId = "sample_operation_victory"
                }
            };
        }

        private static Dictionary<string, UnitCardDefinition> CreateDefaultUnitCardDefinitions()
        {
            return new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["guard_infantry_card"] = new UnitCardDefinition
                {
                    unitCardDefinitionId = "Guard Infantry",
                    displayName = "Guard Infantry",
                    baseTemplateId = "Guard Infantry",
                    purchaseCostGold = 10,
                    defaultItemSlots = new List<string> { "Utility", "Weapon" }
                }
            };
        }

        private static StartingLoadoutDefinition LoadStartingLoadout()
        {
            var asset = Resources.Load<TextAsset>(StartingLoadoutPath);
            if (asset == null)
            {
                return CreateDefaultStartingLoadout();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            if (root == null)
            {
                return CreateDefaultStartingLoadout();
            }

            var loadout = new StartingLoadoutDefinition
            {
                startingExperience = Mathf.Max(0, JsonDataHelper.GetInt(root, "startingExperience", 0)),
                startingGold = Mathf.Max(0, JsonDataHelper.GetInt(root, "startingGold", 50))
            };

            var startingMaps = JsonDataHelper.GetArray(root, "startingMaps");
            for (var i = 0; i < startingMaps.Count; i++)
            {
                var item = JsonDataHelper.AsObject(startingMaps[i]);
                if (item == null)
                {
                    continue;
                }

                var mapDefinitionId = JsonDataHelper.GetString(item, "mapDefinitionId", string.Empty);
                if (string.IsNullOrWhiteSpace(mapDefinitionId))
                {
                    continue;
                }

                loadout.startingMaps.Add(new StartingMapEntry
                {
                    mapDefinitionId = mapDefinitionId,
                    count = Mathf.Max(1, JsonDataHelper.GetInt(item, "count", 1)),
                    instanceNamePrefix = JsonDataHelper.GetString(item, "instanceNamePrefix", string.Empty),
                    appliedMapModifierTemplateIds = GetStringList(item, "appliedMapModifierTemplateIds")
                });
            }

            var startingUnitCards = JsonDataHelper.GetArray(root, "startingUnitCards");
            for (var i = 0; i < startingUnitCards.Count; i++)
            {
                var item = JsonDataHelper.AsObject(startingUnitCards[i]);
                if (item == null)
                {
                    continue;
                }

                var unitCardDefinitionId = JsonDataHelper.GetString(item, "unitCardDefinitionId", string.Empty);
                if (string.IsNullOrWhiteSpace(unitCardDefinitionId))
                {
                    continue;
                }

                loadout.startingUnitCards.Add(new StartingUnitCardEntry
                {
                    unitCardDefinitionId = unitCardDefinitionId,
                    count = Mathf.Max(1, JsonDataHelper.GetInt(item, "count", 1)),
                    displayNamePrefix = JsonDataHelper.GetString(item, "displayNamePrefix", string.Empty),
                    overrideJson = BuildStartingUnitCardOverrideJson(item)
                });
            }

            var startingCurrencyItems = JsonDataHelper.GetArray(root, "startingCurrencyItems");
            for (var i = 0; i < startingCurrencyItems.Count; i++)
            {
                var item = JsonDataHelper.AsObject(startingCurrencyItems[i]);
                if (item == null)
                {
                    continue;
                }

                var currencyItemDefinitionId = JsonDataHelper.GetString(item, "currencyItemDefinitionId", string.Empty);
                if (string.IsNullOrWhiteSpace(currencyItemDefinitionId))
                {
                    continue;
                }

                loadout.startingCurrencyItems.Add(new StartingCurrencyItemEntry
                {
                    currencyItemDefinitionId = currencyItemDefinitionId,
                    amount = Mathf.Max(1, JsonDataHelper.GetInt(item, "amount", 1))
                });
            }

            if (loadout.startingMaps.Count == 0 && loadout.startingUnitCards.Count == 0 && loadout.startingCurrencyItems.Count == 0)
            {
                return CreateDefaultStartingLoadout();
            }

            return loadout;
        }

        private static string BuildStartingUnitCardOverrideJson(Dictionary<string, object> source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            var sanitized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                if (string.Equals(pair.Key, "unitCardDefinitionId", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "count", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "displayNamePrefix", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "unitType", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "unitName", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sanitized[pair.Key] = pair.Value;
            }

            return sanitized.Count == 0 ? string.Empty : MiniJson.Serialize(sanitized);
        }

        private static StartingLoadoutDefinition CreateDefaultStartingLoadout()
        {
            var loadout = new StartingLoadoutDefinition
            {
                startingExperience = 0,
                startingGold = 50
            };

            loadout.startingMaps.Add(new StartingMapEntry
            {
                mapDefinitionId = "sample_operation",
                count = 2,
                instanceNamePrefix = "Sample Operation"
            });

            loadout.startingMaps.Add(new StartingMapEntry
            {
                mapDefinitionId = "sample_operation",
                count = 1,
                instanceNamePrefix = "Reinforced Sample Operation",
                appliedMapModifierTemplateIds = new List<string> { "enemy_all_health_t1" }
            });

            loadout.startingUnitCards.Add(new StartingUnitCardEntry
            {
                unitCardDefinitionId = "Guard Infantry",
                count = 1,
                displayNamePrefix = "Rook"
            });

            loadout.startingCurrencyItems.Add(new StartingCurrencyItemEntry
            {
                currencyItemDefinitionId = "map_survey_orb",
                amount = 2
            });

            return loadout;
        }

        private static Dictionary<string, MapModifierTemplateDefinition> LoadMapModifierTemplates()
        {
            var asset = Resources.Load<TextAsset>(MapModifierTemplatesPath);
            if (asset == null)
            {
                return CreateDefaultMapModifierTemplates();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "modifiers");
            var definitions = new Dictionary<string, MapModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var modifierId = JsonDataHelper.GetString(item, "mapModifierTemplateId", string.Empty);
                if (string.IsNullOrWhiteSpace(modifierId))
                {
                    continue;
                }

                var template = new MapModifierTemplateDefinition
                {
                    mapModifierTemplateId = modifierId,
                    displayName = JsonDataHelper.GetString(item, "displayName", modifierId),
                    description = JsonDataHelper.GetString(item, "description", string.Empty),
                    tier = Mathf.Max(1, JsonDataHelper.GetInt(item, "tier", 1)),
                    weight = Mathf.Max(1, JsonDataHelper.GetInt(item, "weight", 1)),
                    targetScope = JsonDataHelper.GetEnum(item, "targetScope", MapModifierTargetScope.RedTeam),
                    selectors = ParseMapModifierSelectors(item),
                    threatDeltaOverride = JsonDataHelper.GetFloat(item, "threatDeltaOverride", -1f)
                };

                var effectObjects = JsonDataHelper.GetArray(item, "effects");
                for (var effectIndex = 0; effectIndex < effectObjects.Count; effectIndex++)
                {
                    var effectObject = JsonDataHelper.AsObject(effectObjects[effectIndex]);
                    if (effectObject == null)
                    {
                        continue;
                    }

                    template.effects.Add(new MapModifierEffectTemplateDefinition
                    {
                        effectType = JsonDataHelper.GetEnum(effectObject, "effectType", MapModifierEffectType.ModifyUnitStat),
                        statKey = JsonDataHelper.GetString(effectObject, "statKey", string.Empty),
                        operation = JsonDataHelper.GetEnum(effectObject, "operation", MapModifierOperation.Add),
                        minValue = JsonDataHelper.GetInt(effectObject, "minValue", 0),
                        maxValue = JsonDataHelper.GetInt(effectObject, "maxValue", JsonDataHelper.GetInt(effectObject, "minValue", 0)),
                        ammoType = JsonDataHelper.GetString(effectObject, "ammoType", string.Empty),
                        replacementUnitType = JsonDataHelper.GetString(effectObject, "replacementUnitType", string.Empty),
                        maxAffectedEntries = Mathf.Max(0, JsonDataHelper.GetInt(effectObject, "maxAffectedEntries", 0))
                    });
                }

                definitions[modifierId] = template;
            }

            return definitions.Count > 0 ? definitions : CreateDefaultMapModifierTemplates();
        }

        private static MapModifierSelectorDefinition ParseMapModifierSelectors(Dictionary<string, object> item)
        {
            var selectorsObject = JsonDataHelper.AsObject(item != null && item.TryGetValue("selectors", out var value) ? value : null);
            if (selectorsObject == null)
            {
                return new MapModifierSelectorDefinition();
            }

            return new MapModifierSelectorDefinition
            {
                all = GetBool(selectorsObject, "all", !selectorsObject.ContainsKey("unitType")),
                unitType = JsonDataHelper.GetString(selectorsObject, "unitType", string.Empty)
            };
        }

        private static Dictionary<string, MapModifierTemplateDefinition> CreateDefaultMapModifierTemplates()
        {
            var templates = new Dictionary<string, MapModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase);

            templates["enemy_all_health_t1"] = new MapModifierTemplateDefinition
            {
                mapModifierTemplateId = "enemy_all_health_t1",
                displayName = "Reinforced Enemy Forces",
                description = "All enemy units gain extra health.",
                tier = 1,
                weight = 10,
                targetScope = MapModifierTargetScope.RedTeam,
                selectors = new MapModifierSelectorDefinition { all = true },
                effects = new List<MapModifierEffectTemplateDefinition>
                {
                    new MapModifierEffectTemplateDefinition
                    {
                        effectType = MapModifierEffectType.ModifyUnitStat,
                        statKey = "maxHealth",
                        operation = MapModifierOperation.Add,
                        minValue = 2,
                        maxValue = 4
                    }
                }
            };

            templates["enemy_guard_to_raider_t1"] = new MapModifierTemplateDefinition
            {
                mapModifierTemplateId = "enemy_guard_to_raider_t1",
                displayName = "Raider Deployment",
                description = "Some enemy guard infantry are replaced by raiders.",
                tier = 1,
                weight = 6,
                targetScope = MapModifierTargetScope.RedTeam,
                selectors = new MapModifierSelectorDefinition { all = false, unitType = "Guard Infantry" },
                effects = new List<MapModifierEffectTemplateDefinition>
                {
                    new MapModifierEffectTemplateDefinition
                    {
                        effectType = MapModifierEffectType.ReplaceUnitType,
                        replacementUnitType = "Raider Infantry",
                        maxAffectedEntries = 2
                    }
                }
            };

            return templates;
        }

        private static List<string> GetStringList(Dictionary<string, object> source, string key)
        {
            var values = new List<string>();
            var items = JsonDataHelper.GetArray(source, key);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    values.Add(stringValue);
                }
            }

            return values;
        }

        private static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            if (value is string stringValue && bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
