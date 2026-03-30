using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public static class LootCatalogLoader
    {
        private const string LootItemsPath = "Campaign/LootItemDefinitions";
        private const string LootTablesPath = "Campaign/LootTables";
        private const string ItemDefinitionsPath = "Campaign/ItemDefinitions";
        private const string CurrencyItemDefinitionsPath = "Campaign/CurrencyItemDefinitions";

        public static LootCatalogs Load()
        {
            return new LootCatalogs(
                LoadLootItems(),
                LoadLootTables(),
                LoadItemDefinitions(),
                LoadCurrencyItemDefinitions());
        }

        private static Dictionary<string, LootItemDefinition> LoadLootItems()
        {
            var asset = Resources.Load<TextAsset>(LootItemsPath);
            if (asset == null)
            {
                return CreateDefaultLootItems();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "lootItems");
            var definitions = new Dictionary<string, LootItemDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var lootItemId = JsonDataHelper.GetString(item, "lootItemId", string.Empty);
                if (string.IsNullOrWhiteSpace(lootItemId))
                {
                    continue;
                }

                definitions[lootItemId] = new LootItemDefinition
                {
                    lootItemId = lootItemId,
                    rewardType = JsonDataHelper.GetEnum(item, "rewardType", LootRewardType.Gold),
                    displayName = JsonDataHelper.GetString(item, "displayName", lootItemId),
                    amount = Mathf.Max(1, JsonDataHelper.GetInt(item, "amount", 1)),
                    mapDefinitionId = JsonDataHelper.GetString(item, "mapDefinitionId", string.Empty),
                    itemDefinitionId = JsonDataHelper.GetString(item, "itemDefinitionId", string.Empty),
                    currencyItemDefinitionId = JsonDataHelper.GetString(item, "currencyItemDefinitionId", string.Empty)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultLootItems();
        }

        private static Dictionary<string, LootTableDefinition> LoadLootTables()
        {
            var asset = Resources.Load<TextAsset>(LootTablesPath);
            if (asset == null)
            {
                return CreateDefaultLootTables();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "lootTables");
            var definitions = new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var lootTableId = JsonDataHelper.GetString(item, "lootTableId", string.Empty);
                if (string.IsNullOrWhiteSpace(lootTableId))
                {
                    continue;
                }

                var definition = new LootTableDefinition
                {
                    lootTableId = lootTableId,
                    guaranteedMaxCount = JsonDataHelper.GetInt(item, "guaranteedMaxCount", -1)
                };

                var entries = JsonDataHelper.GetArray(item, "entries");
                for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    var entryObject = JsonDataHelper.AsObject(entries[entryIndex]);
                    if (entryObject == null)
                    {
                        continue;
                    }

                    var lootItemId = JsonDataHelper.GetString(entryObject, "lootItemId", string.Empty);
                    if (string.IsNullOrWhiteSpace(lootItemId))
                    {
                        continue;
                    }

                    definition.entries.Add(new LootTableEntryDefinition
                    {
                        lootItemId = lootItemId,
                        guaranteed = ParseBool(entryObject, "guaranteed", false),
                        dropChance = Mathf.Clamp01(JsonDataHelper.GetFloat(entryObject, "dropChance", 0f)),
                        sourceTag = JsonDataHelper.GetString(entryObject, "sourceTag", string.Empty)
                    });
                }

                definitions[lootTableId] = definition;
            }

            return definitions.Count > 0 ? definitions : CreateDefaultLootTables();
        }

        private static Dictionary<string, ItemDefinition> LoadItemDefinitions()
        {
            var asset = Resources.Load<TextAsset>(ItemDefinitionsPath);
            if (asset == null)
            {
                return CreateDefaultItemDefinitions();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "items");
            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var itemDefinitionId = JsonDataHelper.GetString(item, "itemDefinitionId", string.Empty);
                if (string.IsNullOrWhiteSpace(itemDefinitionId))
                {
                    continue;
                }

                definitions[itemDefinitionId] = new ItemDefinition
                {
                    itemDefinitionId = itemDefinitionId,
                    displayName = JsonDataHelper.GetString(item, "displayName", itemDefinitionId),
                    description = JsonDataHelper.GetString(item, "description", string.Empty),
                    itemSlotType = JsonDataHelper.GetString(item, "itemSlotType", string.Empty),
                    iconId = JsonDataHelper.GetString(item, "iconId", string.Empty)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultItemDefinitions();
        }

        private static Dictionary<string, CurrencyItemDefinition> LoadCurrencyItemDefinitions()
        {
            var asset = Resources.Load<TextAsset>(CurrencyItemDefinitionsPath);
            if (asset == null)
            {
                return CreateDefaultCurrencyItemDefinitions();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "currencyItems");
            var definitions = new Dictionary<string, CurrencyItemDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var currencyItemDefinitionId = JsonDataHelper.GetString(item, "currencyItemDefinitionId", string.Empty);
                if (string.IsNullOrWhiteSpace(currencyItemDefinitionId))
                {
                    continue;
                }

                definitions[currencyItemDefinitionId] = new CurrencyItemDefinition
                {
                    currencyItemDefinitionId = currencyItemDefinitionId,
                    displayName = JsonDataHelper.GetString(item, "displayName", currencyItemDefinitionId),
                    description = JsonDataHelper.GetString(item, "description", string.Empty),
                    iconId = JsonDataHelper.GetString(item, "iconId", string.Empty)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultCurrencyItemDefinitions();
        }

        private static bool ParseBool(Dictionary<string, object> source, string key, bool fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            return value switch
            {
                bool boolValue => boolValue,
                long longValue => longValue != 0,
                double doubleValue => Math.Abs(doubleValue) > 0.001d,
                string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                _ => fallback
            };
        }

        private static Dictionary<string, LootItemDefinition> CreateDefaultLootItems()
        {
            return new Dictionary<string, LootItemDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["gold_small"] = new LootItemDefinition
                {
                    lootItemId = "gold_small",
                    rewardType = LootRewardType.Gold,
                    displayName = "Gold Cache",
                    amount = 25
                },
                ["gold_medium"] = new LootItemDefinition
                {
                    lootItemId = "gold_medium",
                    rewardType = LootRewardType.Gold,
                    displayName = "Recovered Treasury",
                    amount = 50
                },
                ["map_sample_operation"] = new LootItemDefinition
                {
                    lootItemId = "map_sample_operation",
                    rewardType = LootRewardType.MapItem,
                    displayName = "Sample Operation",
                    mapDefinitionId = "sample_operation"
                },
                ["currency_scrap_parts_x2"] = new LootItemDefinition
                {
                    lootItemId = "currency_scrap_parts_x2",
                    rewardType = LootRewardType.CurrencyItem,
                    displayName = "Scrap Parts",
                    currencyItemDefinitionId = "scrap_parts",
                    amount = 2
                },
                ["item_field_plating"] = new LootItemDefinition
                {
                    lootItemId = "item_field_plating",
                    rewardType = LootRewardType.UnitItem,
                    displayName = "Field Plating",
                    itemDefinitionId = "field_plating"
                }
            };
        }

        private static Dictionary<string, LootTableDefinition> CreateDefaultLootTables()
        {
            return new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["sample_operation_victory"] = new LootTableDefinition
                {
                    lootTableId = "sample_operation_victory",
                    guaranteedMaxCount = 1,
                    entries = new List<LootTableEntryDefinition>
                    {
                        new LootTableEntryDefinition { lootItemId = "gold_medium", guaranteed = true },
                        new LootTableEntryDefinition { lootItemId = "currency_scrap_parts_x2", dropChance = 0.75f },
                        new LootTableEntryDefinition { lootItemId = "item_field_plating", dropChance = 0.3f },
                        new LootTableEntryDefinition { lootItemId = "map_sample_operation", dropChance = 0.2f }
                    }
                },
                ["enemy_unit_basic"] = new LootTableDefinition
                {
                    lootTableId = "enemy_unit_basic",
                    entries = new List<LootTableEntryDefinition>
                    {
                        new LootTableEntryDefinition { lootItemId = "gold_small", dropChance = 0.22f },
                        new LootTableEntryDefinition { lootItemId = "currency_scrap_parts_x2", dropChance = 0.1f }
                    }
                },
                ["enemy_elite_tank"] = new LootTableDefinition
                {
                    lootTableId = "enemy_elite_tank",
                    guaranteedMaxCount = 1,
                    entries = new List<LootTableEntryDefinition>
                    {
                        new LootTableEntryDefinition { lootItemId = "gold_small", guaranteed = true },
                        new LootTableEntryDefinition { lootItemId = "item_field_plating", dropChance = 0.25f }
                    }
                }
            };
        }

        private static Dictionary<string, ItemDefinition> CreateDefaultItemDefinitions()
        {
            return new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["field_plating"] = new ItemDefinition
                {
                    itemDefinitionId = "field_plating",
                    displayName = "Field Plating",
                    description = "Recovered armor plating ready for refit.",
                    itemSlotType = "Utility"
                }
            };
        }

        private static Dictionary<string, CurrencyItemDefinition> CreateDefaultCurrencyItemDefinitions()
        {
            return new Dictionary<string, CurrencyItemDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["scrap_parts"] = new CurrencyItemDefinition
                {
                    currencyItemDefinitionId = "scrap_parts",
                    displayName = "Scrap Parts",
                    description = "General-purpose battlefield salvage."
                }
            };
        }
    }
}
