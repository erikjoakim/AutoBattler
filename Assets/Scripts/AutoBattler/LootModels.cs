using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public enum LootRewardType
    {
        Gold,
        MapItem,
        UnitItem,
        CurrencyItem
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string itemDefinitionId;
        public string displayName;
        public string description;
        public string itemSlotType;
        public string iconId;
    }

    [Serializable]
    public sealed class CurrencyItemDefinition
    {
        public string currencyItemDefinitionId;
        public string displayName;
        public string description;
        public string iconId;
    }

    [Serializable]
    public sealed class LootItemDefinition
    {
        public string lootItemId;
        public LootRewardType rewardType;
        public string displayName;
        public int amount = 1;
        public string mapDefinitionId;
        public string itemDefinitionId;
        public string currencyItemDefinitionId;
    }

    [Serializable]
    public sealed class LootTableEntryDefinition
    {
        public string lootItemId;
        public bool guaranteed;
        public float dropChance;
        public string sourceTag;
    }

    [Serializable]
    public sealed class LootTableDefinition
    {
        public string lootTableId;
        public int guaranteedMaxCount = -1;
        public List<LootTableEntryDefinition> entries = new List<LootTableEntryDefinition>();
    }

    [Serializable]
    public sealed class DroppedLootEntry
    {
        public string lootItemId;
        public string displayName;
        public LootRewardType rewardType;
        public int amount = 1;
        public string mapDefinitionId;
        public string itemDefinitionId;
        public string currencyItemDefinitionId;
        public string sourceDescription;
    }

    [Serializable]
    public sealed class OwnedUnitItem
    {
        public string itemInstanceId;
        public string itemDefinitionId;
        public string equippedToUnitCardId;
    }

    [Serializable]
    public sealed class CurrencyItemStack
    {
        public string currencyItemDefinitionId;
        public int amount;
    }

    public sealed class LootCatalogs
    {
        public LootCatalogs(
            Dictionary<string, LootItemDefinition> lootItems,
            Dictionary<string, LootTableDefinition> lootTables,
            Dictionary<string, ItemDefinition> itemDefinitions,
            Dictionary<string, CurrencyItemDefinition> currencyItemDefinitions)
        {
            LootItems = lootItems ?? new Dictionary<string, LootItemDefinition>(StringComparer.OrdinalIgnoreCase);
            LootTables = lootTables ?? new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);
            ItemDefinitions = itemDefinitions ?? new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            CurrencyItemDefinitions = currencyItemDefinitions ?? new Dictionary<string, CurrencyItemDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, LootItemDefinition> LootItems { get; }
        public Dictionary<string, LootTableDefinition> LootTables { get; }
        public Dictionary<string, ItemDefinition> ItemDefinitions { get; }
        public Dictionary<string, CurrencyItemDefinition> CurrencyItemDefinitions { get; }

        public bool TryGetLootItem(string lootItemId, out LootItemDefinition definition)
        {
            return LootItems.TryGetValue(lootItemId ?? string.Empty, out definition);
        }

        public bool TryGetLootTable(string lootTableId, out LootTableDefinition definition)
        {
            return LootTables.TryGetValue(lootTableId ?? string.Empty, out definition);
        }

        public bool TryGetItemDefinition(string itemDefinitionId, out ItemDefinition definition)
        {
            return ItemDefinitions.TryGetValue(itemDefinitionId ?? string.Empty, out definition);
        }

        public bool TryGetCurrencyItemDefinition(string currencyItemDefinitionId, out CurrencyItemDefinition definition)
        {
            return CurrencyItemDefinitions.TryGetValue(currencyItemDefinitionId ?? string.Empty, out definition);
        }
    }
}
