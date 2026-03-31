using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AutoBattler
{
    public enum LootRewardType
    {
        Gold,
        MapItem,
        UnitItem,
        CurrencyItem
    }

    public enum ItemEffectOperation
    {
        Add,
        Multiply
    }

    public enum CurrencyActionType
    {
        None,
        AddModifiers,
        RaiseTier,
        TransformItem,
        RemoveModifierByType
    }

    public enum ModifierType
    {
        MaxHealth,
        Armor,
        Damage,
        VisionRange,
        Speed,
        Accuracy,
        FireReliability,
        MoveReliability
    }

    [Serializable]
    public sealed class ItemEffectDefinition
    {
        public string statKey;
        public ItemEffectOperation operation = ItemEffectOperation.Add;
        public float value;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string itemDefinitionId;
        public string displayName;
        public string description;
        public string itemType;
        public string itemSlotType;
        public int tier = 1;
        public string iconId;
        public List<ItemEffectDefinition> effects = new List<ItemEffectDefinition>();
    }

    [Serializable]
    public sealed class ModifierTemplateDefinition
    {
        public string modifierTemplateId;
        public string displayName;
        public string description;
        public ModifierType modifierType = ModifierType.MaxHealth;
        public List<string> itemTypes = new List<string>();
        public int tier = 1;
        public int weight = 1;
        public int rollAMin;
        public int rollAMax;
        public int rollBMin;
        public int rollBMax;
    }

    [Serializable]
    public sealed class CurrencyItemDefinition
    {
        public string currencyItemDefinitionId;
        public string displayName;
        public string description;
        public string iconId;
        public CurrencyActionType actionType = CurrencyActionType.None;
        public List<string> targetTypes = new List<string>();
        public int minExistingModifiers;
        public int maxExistingModifiers;
        public int minAddedModifiers = 1;
        public int maxAddedModifiers = 1;
        public int maxModifiersPerItem = 2;
    }

    [Serializable]
    public sealed class LootTableEntryDefinition
    {
        public string entryId;
        public LootRewardType rewardType;
        public string displayName;
        public int amount = 1;
        public string mapDefinitionId;
        public string itemDefinitionId;
        public string currencyItemDefinitionId;
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
        public List<AppliedItemModifierData> appliedModifiers = new List<AppliedItemModifierData>();
        [FormerlySerializedAs("upgradeLevel")]
        public int legacyUpgradeLevel;
    }

    [Serializable]
    public sealed class AppliedItemModifierData
    {
        [FormerlySerializedAs("modifierDefinitionId")]
        public string modifierTemplateId;
        public ModifierType modifierType = ModifierType.MaxHealth;
        public int rolledValueA;
        public int rolledValueB;
        public string sourceCurrencyItemDefinitionId;
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
            Dictionary<string, LootTableDefinition> lootTables,
            Dictionary<string, ItemDefinition> itemDefinitions,
            Dictionary<string, CurrencyItemDefinition> currencyItemDefinitions,
            Dictionary<string, ModifierTemplateDefinition> modifierTemplates)
        {
            LootTables = lootTables ?? new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);
            ItemDefinitions = itemDefinitions ?? new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            CurrencyItemDefinitions = currencyItemDefinitions ?? new Dictionary<string, CurrencyItemDefinition>(StringComparer.OrdinalIgnoreCase);
            ModifierTemplates = modifierTemplates ?? new Dictionary<string, ModifierTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, LootTableDefinition> LootTables { get; }
        public Dictionary<string, ItemDefinition> ItemDefinitions { get; }
        public Dictionary<string, CurrencyItemDefinition> CurrencyItemDefinitions { get; }
        public Dictionary<string, ModifierTemplateDefinition> ModifierTemplates { get; }

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

        public bool TryGetModifierTemplate(string modifierTemplateId, out ModifierTemplateDefinition definition)
        {
            return ModifierTemplates.TryGetValue(modifierTemplateId ?? string.Empty, out definition);
        }

    }
}
