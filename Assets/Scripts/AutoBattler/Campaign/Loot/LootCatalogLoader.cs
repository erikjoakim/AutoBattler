using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public static class LootCatalogLoader
    {
        private const string LootTablesPath = "Campaign/LootTables";
        private const string ItemDefinitionsPath = "Campaign/ItemDefinitions";
        private const string CurrencyItemDefinitionsPath = "Campaign/CurrencyItemDefinitions";
        private const string ModifierTemplatesPath = "Campaign/ModifierTemplates";

        public static LootCatalogs Load()
        {
            return new LootCatalogs(
                LoadLootTables(),
                LoadItemDefinitions(),
                LoadCurrencyItemDefinitions(),
                LoadModifierTemplates());
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

                    var entryId = JsonDataHelper.GetString(entryObject, "entryId", string.Empty);
                    if (string.IsNullOrWhiteSpace(entryId))
                    {
                        entryId = lootTableId + "_entry_" + entryIndex;
                    }

                    definition.entries.Add(new LootTableEntryDefinition
                    {
                        entryId = entryId,
                        rewardType = JsonDataHelper.GetEnum(entryObject, "rewardType", LootRewardType.Gold),
                        displayName = JsonDataHelper.GetString(entryObject, "displayName", entryId),
                        amount = Mathf.Max(1, JsonDataHelper.GetInt(entryObject, "amount", 1)),
                        mapDefinitionId = JsonDataHelper.GetString(entryObject, "mapDefinitionId", string.Empty),
                        itemDefinitionId = JsonDataHelper.GetString(entryObject, "itemDefinitionId", string.Empty),
                        currencyItemDefinitionId = JsonDataHelper.GetString(entryObject, "currencyItemDefinitionId", string.Empty),
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
                    itemType = JsonDataHelper.GetString(item, "itemType", JsonDataHelper.GetString(item, "itemSlotType", string.Empty)),
                    itemSlotType = JsonDataHelper.GetString(item, "itemSlotType", string.Empty),
                    tier = Mathf.Max(1, JsonDataHelper.GetInt(item, "tier", 1)),
                    iconId = JsonDataHelper.GetString(item, "iconId", string.Empty),
                    effects = ParseItemEffects(item)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultItemDefinitions();
        }

        private static List<ItemEffectDefinition> ParseItemEffects(Dictionary<string, object> item)
        {
            var effects = new List<ItemEffectDefinition>();
            var effectEntries = JsonDataHelper.GetArray(item, "effects");
            for (var i = 0; i < effectEntries.Count; i++)
            {
                var effectObject = JsonDataHelper.AsObject(effectEntries[i]);
                if (effectObject == null)
                {
                    continue;
                }

                var statKey = JsonDataHelper.GetString(effectObject, "statKey", string.Empty);
                if (string.IsNullOrWhiteSpace(statKey))
                {
                    continue;
                }

                effects.Add(new ItemEffectDefinition
                {
                    statKey = statKey,
                    operation = JsonDataHelper.GetEnum(effectObject, "operation", ItemEffectOperation.Add),
                    value = JsonDataHelper.GetFloat(effectObject, "value", 0f)
                });
            }

            return effects;
        }

        private static Dictionary<string, ModifierTemplateDefinition> LoadModifierTemplates()
        {
            var asset = Resources.Load<TextAsset>(ModifierTemplatesPath);
            if (asset == null)
            {
                return CreateDefaultModifierTemplates();
            }

            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(asset.text));
            var items = JsonDataHelper.GetArray(root, "modifiers");
            var definitions = new Dictionary<string, ModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var modifierTemplateId = JsonDataHelper.GetString(item, "modifierTemplateId", string.Empty);
                if (string.IsNullOrWhiteSpace(modifierTemplateId))
                {
                    continue;
                }

                definitions[modifierTemplateId] = new ModifierTemplateDefinition
                {
                    modifierTemplateId = modifierTemplateId,
                    displayName = JsonDataHelper.GetString(item, "displayName", modifierTemplateId),
                    description = JsonDataHelper.GetString(item, "description", string.Empty),
                    modifierType = JsonDataHelper.GetEnum(item, "modifierType", ModifierType.MaxHealth),
                    itemTypes = ParseModifierItemTypes(item),
                    tier = Mathf.Max(1, JsonDataHelper.GetInt(item, "tier", 1)),
                    weight = Mathf.Max(1, JsonDataHelper.GetInt(item, "weight", 1)),
                    rollAMin = JsonDataHelper.GetInt(item, "rollAMin", 0),
                    rollAMax = JsonDataHelper.GetInt(item, "rollAMax", 0),
                    rollBMin = JsonDataHelper.GetInt(item, "rollBMin", 0),
                    rollBMax = JsonDataHelper.GetInt(item, "rollBMax", 0)
                };
            }

            return definitions.Count > 0 ? definitions : CreateDefaultModifierTemplates();
        }

        private static List<string> ParseModifierItemTypes(Dictionary<string, object> item)
        {
            var results = new List<string>();
            var values = JsonDataHelper.GetArray(item, "itemTypes");
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    results.Add(stringValue);
                }
            }

            if (results.Count == 0)
            {
                var singleItemType = JsonDataHelper.GetString(item, "itemType", string.Empty);
                if (!string.IsNullOrWhiteSpace(singleItemType))
                {
                    results.Add(singleItemType);
                }
            }

            return results;
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
                    iconId = JsonDataHelper.GetString(item, "iconId", string.Empty),
                    actionType = JsonDataHelper.GetEnum(item, "actionType", CurrencyActionType.None),
                    targetTypes = ParseStringList(item, "targetTypes"),
                    minExistingModifiers = Mathf.Max(0, JsonDataHelper.GetInt(item, "minExistingModifiers", 0)),
                    maxExistingModifiers = Mathf.Max(0, JsonDataHelper.GetInt(item, "maxExistingModifiers", 0)),
                    minAddedModifiers = Mathf.Max(1, JsonDataHelper.GetInt(item, "minAddedModifiers", 1)),
                    maxAddedModifiers = Mathf.Max(1, JsonDataHelper.GetInt(item, "maxAddedModifiers", 1)),
                    maxModifiersPerItem = Mathf.Max(1, JsonDataHelper.GetInt(item, "maxModifiersPerItem", 2))
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
                        new LootTableEntryDefinition { entryId = "gold_medium", rewardType = LootRewardType.Gold, displayName = "Recovered Treasury", amount = 50, guaranteed = true },
                        new LootTableEntryDefinition { entryId = "currency_scrap_parts_x2", rewardType = LootRewardType.CurrencyItem, displayName = "Scrap Parts", currencyItemDefinitionId = "scrap_parts", amount = 2, dropChance = 0.75f },
                        new LootTableEntryDefinition { entryId = "item_field_plating", rewardType = LootRewardType.UnitItem, displayName = "Field Plating", itemDefinitionId = "field_plating", dropChance = 0.3f },
                        new LootTableEntryDefinition { entryId = "map_sample_operation", rewardType = LootRewardType.MapItem, displayName = "Sample Operation", mapDefinitionId = "sample_operation", dropChance = 0.2f }
                    }
                },
                ["enemy_unit_basic"] = new LootTableDefinition
                {
                    lootTableId = "enemy_unit_basic",
                    entries = new List<LootTableEntryDefinition>
                    {
                        new LootTableEntryDefinition { entryId = "gold_small", rewardType = LootRewardType.Gold, displayName = "Gold Cache", amount = 25, dropChance = 0.22f },
                        new LootTableEntryDefinition { entryId = "currency_scrap_parts_x2", rewardType = LootRewardType.CurrencyItem, displayName = "Scrap Parts", currencyItemDefinitionId = "scrap_parts", amount = 2, dropChance = 0.1f }
                    }
                },
                ["enemy_elite_tank"] = new LootTableDefinition
                {
                    lootTableId = "enemy_elite_tank",
                    guaranteedMaxCount = 1,
                    entries = new List<LootTableEntryDefinition>
                    {
                        new LootTableEntryDefinition { entryId = "gold_small", rewardType = LootRewardType.Gold, displayName = "Gold Cache", amount = 25, guaranteed = true },
                        new LootTableEntryDefinition { entryId = "item_field_plating", rewardType = LootRewardType.UnitItem, displayName = "Field Plating", itemDefinitionId = "field_plating", dropChance = 0.25f }
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
                    itemType = "Utility",
                    itemSlotType = "Utility",
                    tier = 1,
                    effects = new List<ItemEffectDefinition>
                    {
                        new ItemEffectDefinition
                        {
                            statKey = "armor",
                            operation = ItemEffectOperation.Add,
                            value = 1f
                        }
                    }
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
                    description = "General-purpose battlefield salvage.",
                    actionType = CurrencyActionType.AddModifiers,
                    targetTypes = new List<string> { "Utility", "Weapon" },
                    minExistingModifiers = 0,
                    maxExistingModifiers = 0,
                    minAddedModifiers = 1,
                    maxAddedModifiers = 2,
                    maxModifiersPerItem = 2
                },
                ["map_survey_orb"] = new CurrencyItemDefinition
                {
                    currencyItemDefinitionId = "map_survey_orb",
                    displayName = "Survey Orb",
                    description = "A cartographic orb used to alter hostile map conditions.",
                    actionType = CurrencyActionType.AddModifiers,
                    targetTypes = new List<string> { "Map" },
                    minExistingModifiers = 0,
                    maxExistingModifiers = 0,
                    minAddedModifiers = 1,
                    maxAddedModifiers = 1,
                    maxModifiersPerItem = 2
                }
            };
        }

        private static List<string> ParseStringList(Dictionary<string, object> source, string key)
        {
            var results = new List<string>();
            var values = JsonDataHelper.GetArray(source, key);
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    results.Add(stringValue);
                }
            }

            return results;
        }

        private static Dictionary<string, ModifierTemplateDefinition> CreateDefaultModifierTemplates()
        {
            return new Dictionary<string, ModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["utility_t1_health"] = new ModifierTemplateDefinition
                {
                    modifierTemplateId = "utility_t1_health",
                    displayName = "Reinforced Lining",
                    description = "Adds layered interior protection.",
                    modifierType = ModifierType.MaxHealth,
                    itemTypes = new List<string> { "Utility" },
                    tier = 1,
                    weight = 10,
                    rollAMin = 1,
                    rollAMax = 4
                },
                ["utility_t1_armor"] = new ModifierTemplateDefinition
                {
                    modifierTemplateId = "utility_t1_armor",
                    displayName = "Plated Bracing",
                    description = "Adds extra armor support.",
                    modifierType = ModifierType.Armor,
                    itemTypes = new List<string> { "Utility" },
                    tier = 1,
                    weight = 8,
                    rollAMin = 1,
                    rollAMax = 2
                },
                ["utility_t1_damage"] = new ModifierTemplateDefinition
                {
                    modifierTemplateId = "utility_t1_damage",
                    displayName = "Reactive Charge Matrix",
                    description = "Adds a fluctuating damage band.",
                    modifierType = ModifierType.Damage,
                    itemTypes = new List<string> { "Utility" },
                    tier = 1,
                    weight = 4,
                    rollAMin = 1,
                    rollAMax = 2,
                    rollBMin = 3,
                    rollBMax = 4
                },
                ["legacy_field_plating_armor_boost"] = new ModifierTemplateDefinition
                {
                    modifierTemplateId = "legacy_field_plating_armor_boost",
                    displayName = "Legacy Armor Reinforcement",
                    description = "Converted from a previous item upgrade.",
                    modifierType = ModifierType.Armor,
                    itemTypes = new List<string> { "Utility" },
                    tier = 1,
                    weight = 1,
                    rollAMin = 1,
                    rollAMax = 1
                }
            };
        }

    }
}
