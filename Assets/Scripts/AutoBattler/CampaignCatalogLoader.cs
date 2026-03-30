using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class CampaignCatalogs
    {
        public CampaignCatalogs(Dictionary<string, MapDefinition> mapDefinitions, Dictionary<string, UnitCardDefinition> unitCardDefinitions)
        {
            MapDefinitions = mapDefinitions ?? new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
            UnitCardDefinitions = unitCardDefinitions ?? new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, MapDefinition> MapDefinitions { get; }
        public Dictionary<string, UnitCardDefinition> UnitCardDefinitions { get; }

        public bool TryGetMapDefinition(string mapDefinitionId, out MapDefinition definition)
        {
            return MapDefinitions.TryGetValue(mapDefinitionId ?? string.Empty, out definition);
        }

        public bool TryGetUnitCardDefinition(string definitionId, out UnitCardDefinition definition)
        {
            return UnitCardDefinitions.TryGetValue(definitionId ?? string.Empty, out definition);
        }
    }

    public static class CampaignCatalogLoader
    {
        private const string MapDefinitionsPath = "Campaign/MapDefinitions";
        private const string UnitCardDefinitionsPath = "Campaign/UnitCardDefinitions";

        public static CampaignCatalogs Load()
        {
            var mapDefinitions = LoadMapDefinitions();
            var unitCardDefinitions = LoadUnitCardDefinitions();
            return new CampaignCatalogs(mapDefinitions, unitCardDefinitions);
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
                    tier = Mathf.Max(1, JsonDataHelper.GetInt(item, "tier", 1))
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultMapDefinitions();
        }

        private static Dictionary<string, UnitCardDefinition> LoadUnitCardDefinitions()
        {
            var asset = Resources.Load<TextAsset>(UnitCardDefinitionsPath);
            if (asset == null)
            {
                return CreateDefaultUnitCardDefinitions();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "unitCards");
            var definitions = new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var definitionId = JsonDataHelper.GetString(item, "unitCardDefinitionId", string.Empty);
                var baseTemplateId = JsonDataHelper.GetString(item, "baseTemplateId", string.Empty);
                if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(baseTemplateId))
                {
                    continue;
                }

                definitions[definitionId] = new UnitCardDefinition
                {
                    unitCardDefinitionId = definitionId,
                    displayName = JsonDataHelper.GetString(item, "displayName", definitionId),
                    baseTemplateId = baseTemplateId,
                    purchaseCostGold = Mathf.Max(0, JsonDataHelper.GetInt(item, "purchaseCostGold", 10))
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
                    tier = 1
                }
            };
        }

        private static Dictionary<string, UnitCardDefinition> CreateDefaultUnitCardDefinitions()
        {
            return new Dictionary<string, UnitCardDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["guard_infantry_card"] = new UnitCardDefinition
                {
                    unitCardDefinitionId = "guard_infantry_card",
                    displayName = "Guard Infantry",
                    baseTemplateId = "Guard Infantry",
                    purchaseCostGold = 10
                }
            };
        }
    }
}
