using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class BattleLootManager : MonoBehaviour
    {
        private const int MaxFeedEntries = 16;

        public static BattleLootManager Instance { get; private set; }

        private readonly List<DroppedLootEntry> droppedLoot = new List<DroppedLootEntry>();
        private LootCatalogs catalogs;
        private bool victoryLootResolved;

        public IReadOnlyList<DroppedLootEntry> DroppedLoot => droppedLoot;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            catalogs = LootCatalogLoader.Load();
            BattleUnit.UnitDied += HandleUnitDied;
        }

        private void OnDisable()
        {
            BattleUnit.UnitDied -= HandleUnitDied;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResetLoot()
        {
            droppedLoot.Clear();
            victoryLootResolved = false;
        }

        public void PopulateBattleResult(BattleResultData result, bool victory)
        {
            if (result == null)
            {
                return;
            }

            EnsureVictoryLootResolved(victory);
            if (victory)
            {
                result.claimedLoot = CloneLootEntries(droppedLoot);
                result.lostLoot = new List<DroppedLootEntry>();
                return;
            }

            result.claimedLoot = new List<DroppedLootEntry>();
            result.lostLoot = CloneLootEntries(droppedLoot);
        }

        public IReadOnlyList<string> GetRecentDropDisplayEntries(int maxCount = MaxFeedEntries)
        {
            if (droppedLoot.Count == 0)
            {
                return Array.Empty<string>();
            }

            var startIndex = Mathf.Max(0, droppedLoot.Count - Mathf.Max(1, maxCount));
            var entries = new List<string>(droppedLoot.Count - startIndex);
            for (var i = startIndex; i < droppedLoot.Count; i++)
            {
                entries.Add(FormatFeedLabel(droppedLoot[i]));
            }

            return entries;
        }

        private void Update()
        {
            if (BattleStateManager.Instance == null || !BattleStateManager.Instance.IsBattleOver)
            {
                return;
            }

            EnsureVictoryLootResolved(BattleStateManager.Instance.Winner == Team.Blue);
        }

        private void HandleUnitDied(BattleUnit unit, BattleUnit attacker)
        {
            if (unit == null || unit.Team != Team.Red)
            {
                return;
            }

            var lootTableId = ResolveEnemyLootTableId(unit);
            if (string.IsNullOrWhiteSpace(lootTableId))
            {
                return;
            }

            RollLootTable(lootTableId, unit.Definition != null ? unit.Definition.UnitName : "Enemy unit");
        }

        private string ResolveEnemyLootTableId(BattleUnit unit)
        {
            if (unit != null && !string.IsNullOrWhiteSpace(unit.LootTableId))
            {
                return unit.LootTableId;
            }

            return BattleScenario.Instance != null ? BattleScenario.Instance.DefaultEnemyLootTableId : string.Empty;
        }

        private void EnsureVictoryLootResolved(bool victory)
        {
            if (!victory || victoryLootResolved)
            {
                return;
            }

            victoryLootResolved = true;
            var lootTableId = ResolveVictoryLootTableId();
            if (string.IsNullOrWhiteSpace(lootTableId))
            {
                return;
            }

            RollLootTable(lootTableId, "Scenario Victory");
            TryRollBonusVictoryEntry(lootTableId, "Scenario Victory");
        }

        private string ResolveVictoryLootTableId()
        {
            if (BattleScenario.Instance != null && !string.IsNullOrWhiteSpace(BattleScenario.Instance.VictoryLootTableId))
            {
                return BattleScenario.Instance.VictoryLootTableId;
            }

            var context = CampaignRuntimeContext.Instance;
            var mission = context != null ? context.ActiveMission : null;
            if (context == null || mission == null)
            {
                return string.Empty;
            }

            return context.Catalogs.TryGetMapDefinition(mission.mapDefinitionId, out var definition)
                ? definition.baseLootTableId
                : string.Empty;
        }

        private void RollLootTable(string lootTableId, string sourceDescription)
        {
            if (!catalogs.TryGetLootTable(lootTableId, out var lootTable) || lootTable == null)
            {
                Debug.LogWarning("Unknown loot table: " + lootTableId);
                return;
            }

            var guaranteedEntries = new List<LootTableEntryDefinition>();
            for (var i = 0; i < lootTable.entries.Count; i++)
            {
                var entry = lootTable.entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.guaranteed)
                {
                    guaranteedEntries.Add(entry);
                    continue;
                }

                if (entry.dropChance > 0f && UnityEngine.Random.value <= entry.dropChance)
                {
                    TryAddDrop(entry, sourceDescription);
                }
            }

            ApplyGuaranteedEntries(lootTable, guaranteedEntries, sourceDescription);
        }

        private void ApplyGuaranteedEntries(LootTableDefinition lootTable, List<LootTableEntryDefinition> guaranteedEntries, string sourceDescription)
        {
            if (guaranteedEntries == null || guaranteedEntries.Count == 0)
            {
                return;
            }

            var selectionCount = lootTable.guaranteedMaxCount <= 0
                ? guaranteedEntries.Count
                : Mathf.Min(lootTable.guaranteedMaxCount, guaranteedEntries.Count);

            for (var i = 0; i < guaranteedEntries.Count; i++)
            {
                var swapIndex = UnityEngine.Random.Range(i, guaranteedEntries.Count);
                (guaranteedEntries[i], guaranteedEntries[swapIndex]) = (guaranteedEntries[swapIndex], guaranteedEntries[i]);
            }

            for (var i = 0; i < selectionCount; i++)
            {
                TryAddDrop(guaranteedEntries[i], sourceDescription);
            }
        }

        private void TryAddDrop(LootTableEntryDefinition entry, string sourceDescription)
        {
            if (entry == null)
            {
                return;
            }

            var scaledAmount = ScaleDropAmount(entry);
            droppedLoot.Add(new DroppedLootEntry
            {
                lootItemId = entry.entryId,
                displayName = string.IsNullOrWhiteSpace(entry.displayName) ? entry.entryId : entry.displayName,
                rewardType = entry.rewardType,
                amount = scaledAmount,
                mapDefinitionId = entry.mapDefinitionId,
                itemDefinitionId = entry.itemDefinitionId,
                currencyItemDefinitionId = entry.currencyItemDefinitionId,
                sourceDescription = sourceDescription
            });
        }

        private void TryRollBonusVictoryEntry(string lootTableId, string sourceDescription)
        {
            var rewardProfile = GetRewardProfile();
            if (rewardProfile == null || rewardProfile.bonusLootRollChance <= 0f || UnityEngine.Random.value > rewardProfile.bonusLootRollChance)
            {
                return;
            }

            if (!catalogs.TryGetLootTable(lootTableId, out var lootTable) || lootTable?.entries == null || lootTable.entries.Count == 0)
            {
                return;
            }

            var candidates = new List<LootTableEntryDefinition>();
            for (var i = 0; i < lootTable.entries.Count; i++)
            {
                var entry = lootTable.entries[i];
                if (entry != null && !entry.guaranteed)
                {
                    candidates.Add(entry);
                }
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < lootTable.entries.Count; i++)
                {
                    if (lootTable.entries[i] != null)
                    {
                        candidates.Add(lootTable.entries[i]);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            TryAddDrop(candidates[UnityEngine.Random.Range(0, candidates.Count)], sourceDescription + " Bonus");
        }

        private int ScaleDropAmount(LootTableEntryDefinition entry)
        {
            var baseAmount = Mathf.Max(1, entry.amount);
            if (entry.rewardType != LootRewardType.Gold && entry.rewardType != LootRewardType.CurrencyItem)
            {
                return baseAmount;
            }

            var rewardProfile = GetRewardProfile();
            if (rewardProfile == null || rewardProfile.rewardMultiplier <= 1f)
            {
                return baseAmount;
            }

            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * rewardProfile.rewardMultiplier));
        }

        private PreparedMissionRewardProfile GetRewardProfile()
        {
            var mission = CampaignRuntimeContext.Instance != null ? CampaignRuntimeContext.Instance.ActiveMission : null;
            return mission?.rewardProfile;
        }

        private static List<DroppedLootEntry> CloneLootEntries(List<DroppedLootEntry> source)
        {
            var clone = new List<DroppedLootEntry>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null)
                {
                    continue;
                }

                clone.Add(new DroppedLootEntry
                {
                    lootItemId = entry.lootItemId,
                    displayName = entry.displayName,
                    rewardType = entry.rewardType,
                    amount = entry.amount,
                    mapDefinitionId = entry.mapDefinitionId,
                    itemDefinitionId = entry.itemDefinitionId,
                    currencyItemDefinitionId = entry.currencyItemDefinitionId,
                    sourceDescription = entry.sourceDescription
                });
            }

            return clone;
        }

        private static string FormatFeedLabel(DroppedLootEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var amountSuffix = entry.amount > 1 ? " x" + entry.amount : string.Empty;
            return entry.displayName + amountSuffix;
        }
    }
}
