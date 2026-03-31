using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public enum CampaignHexState
    {
        Locked,
        Open,
        Occupied,
        Completed
    }

    public enum UnitCardStatus
    {
        Available,
        Assigned,
        Dead
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public string mapDefinitionId;
        public string displayName;
        public string sceneName;
        public string description;
        public int tier = 1;
        public string baseLootTableId;
    }

    [Serializable]
    public sealed class MapDefinitionCatalog
    {
        public List<MapDefinition> maps = new List<MapDefinition>();
    }

    [Serializable]
    public sealed class UnitCardDefinition
    {
        public string unitCardDefinitionId;
        public string displayName;
        public string baseTemplateId;
        public int purchaseCostGold = 10;
        public List<string> defaultItemSlots = new List<string>();
    }

    [Serializable]
    public sealed class UnitCardDefinitionCatalog
    {
        public List<UnitCardDefinition> unitCards = new List<UnitCardDefinition>();
    }

    [Serializable]
    public sealed class StartingMapEntry
    {
        public string mapDefinitionId;
        public int count = 1;
        public string instanceNamePrefix;
    }

    [Serializable]
    public sealed class StartingUnitCardEntry
    {
        public string unitCardDefinitionId;
        public int count = 1;
        public string displayNamePrefix;
        public string overrideJson;
    }

    [Serializable]
    public sealed class StartingLoadoutDefinition
    {
        public int startingExperience;
        public int startingGold = 50;
        public List<StartingMapEntry> startingMaps = new List<StartingMapEntry>();
        public List<StartingUnitCardEntry> startingUnitCards = new List<StartingUnitCardEntry>();
    }

    [Serializable]
    public sealed class OwnedMapItem
    {
        public string mapItemId;
        public string mapDefinitionId;
        public string instanceName;
    }

    [Serializable]
    public sealed class OwnedUnitCard
    {
        public string unitCardId;
        public string definitionId;
        public string displayName;
        public string baseTemplateId;
        public string overrideJson;
        public int experience;
        public int level = 1;
        public UnitCardStatus status = UnitCardStatus.Available;
        public string assignedHexSlotId;
        public int timesDeployed;
        public int timesSurvived;
        public List<string> equippedItemIds = new List<string>();
    }

    [Serializable]
    public sealed class HexSlotSaveData
    {
        public string hexSlotId;
        public CampaignHexState state;
        public string occupiedMapItemId;
        public List<string> selectedUnitCardIds = new List<string>();
    }

    [Serializable]
    public sealed class CampaignSaveData
    {
        public int saveVersion = 2;
        public int playerExperience;
        public int gold = 50;
        public List<CurrencyItemStack> currencyItemStacks = new List<CurrencyItemStack>();
        public List<OwnedMapItem> ownedMapItems = new List<OwnedMapItem>();
        public List<OwnedUnitCard> ownedUnitCards = new List<OwnedUnitCard>();
        public List<OwnedUnitItem> ownedUnitItems = new List<OwnedUnitItem>();
        public List<HexSlotSaveData> hexBoardState = new List<HexSlotSaveData>();
        public BattleResultData lastResolvedBattleResult;
    }

    [Serializable]
    public sealed class PreparedMissionData
    {
        public string preparedMissionId;
        public string hexSlotId;
        public string mapItemId;
        public string mapDefinitionId;
        public string sceneName;
        public List<string> selectedUnitCardIds = new List<string>();
    }

    [Serializable]
    public sealed class BattleResultData
    {
        public bool victory;
        public string sceneName;
        public string hexSlotId;
        public string resultMessage;
        public List<string> deadUnitCardIds = new List<string>();
        public List<string> survivingUnitCardIds = new List<string>();
        public List<DroppedLootEntry> claimedLoot = new List<DroppedLootEntry>();
        public List<DroppedLootEntry> lostLoot = new List<DroppedLootEntry>();
    }

    public sealed class CampaignHexSlotDefinition
    {
        public CampaignHexSlotDefinition(string slotId, int q, int r, bool initiallyOpen, params string[] neighbors)
        {
            SlotId = slotId;
            Q = q;
            R = r;
            InitiallyOpen = initiallyOpen;
            Neighbors = neighbors ?? Array.Empty<string>();
        }

        public string SlotId { get; }
        public int Q { get; }
        public int R { get; }
        public bool InitiallyOpen { get; }
        public string[] Neighbors { get; }
    }

    public static class CampaignBoardLayout
    {
        public static readonly CampaignHexSlotDefinition[] DefaultSlots =
        {
            new CampaignHexSlotDefinition("hex_a1", 0, 0, true, "hex_a2", "hex_b1"),
            new CampaignHexSlotDefinition("hex_a2", 1, 0, true, "hex_a1", "hex_a3", "hex_b1", "hex_b2"),
            new CampaignHexSlotDefinition("hex_a3", 2, 0, true, "hex_a2", "hex_b2", "hex_b3"),
            new CampaignHexSlotDefinition("hex_b1", 0, 1, false, "hex_a1", "hex_a2", "hex_b2", "hex_c1"),
            new CampaignHexSlotDefinition("hex_b2", 1, 1, false, "hex_a2", "hex_a3", "hex_b1", "hex_b3", "hex_c1"),
            new CampaignHexSlotDefinition("hex_b3", 2, 1, false, "hex_a3", "hex_b2", "hex_c1"),
            new CampaignHexSlotDefinition("hex_c1", 1, 2, false, "hex_b1", "hex_b2", "hex_b3")
        };

        public static CampaignHexSlotDefinition GetDefinition(string slotId)
        {
            for (var i = 0; i < DefaultSlots.Length; i++)
            {
                if (string.Equals(DefaultSlots[i].SlotId, slotId, StringComparison.OrdinalIgnoreCase))
                {
                    return DefaultSlots[i];
                }
            }

            return null;
        }

        public static Vector2 GetUiPosition(CampaignHexSlotDefinition definition)
        {
            return new Vector2((definition.Q + (definition.R * 0.5f)) * 180f, -definition.R * 156f);
        }
    }
}
