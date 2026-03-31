using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AutoBattler
{
    public sealed class CampaignRuntimeContext : MonoBehaviour
    {
        private const string HeadQuarterSceneName = "HeadQuarter";
        private const string SaveFileName = "CampaignSave.json";

        public static CampaignRuntimeContext Instance { get; private set; }

        private CampaignCatalogs catalogs;
        private LootCatalogs lootCatalogs;
        private CampaignSaveData saveData;
        private PreparedMissionData activeMission;
        private BattleResultData pendingBattleResult;
        private bool awaitingMissionSceneLoad;
        private bool headQuarterStartupApplied;

        public CampaignCatalogs Catalogs => catalogs;
        public LootCatalogs LootCatalogs => lootCatalogs;
        public CampaignSaveData SaveData => saveData;
        public PreparedMissionData ActiveMission => activeMission;
        public BattleResultData PendingBattleResult => pendingBattleResult;
        public bool HasActiveMission => activeMission != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<CampaignRuntimeContext>() != null)
            {
                return;
            }

            var contextObject = new GameObject("CampaignRuntimeContext");
            contextObject.AddComponent<CampaignRuntimeContext>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            catalogs = CampaignCatalogLoader.Load();
            lootCatalogs = LootCatalogLoader.Load();
            saveData = LoadOrCreateSave();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        public IReadOnlyList<OwnedMapItem> GetAvailableMapItems()
        {
            var occupiedMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < saveData.hexBoardState.Count; i++)
            {
                var slot = saveData.hexBoardState[i];
                if (!string.IsNullOrWhiteSpace(slot.occupiedMapItemId))
                {
                    occupiedMapIds.Add(slot.occupiedMapItemId);
                }
            }

            var items = new List<OwnedMapItem>();
            for (var i = 0; i < saveData.ownedMapItems.Count; i++)
            {
                var item = saveData.ownedMapItems[i];
                if (item == null || occupiedMapIds.Contains(item.mapItemId))
                {
                    continue;
                }

                items.Add(item);
            }

            return items;
        }

        public IReadOnlyList<CurrencyItemStack> GetCurrencyItemStacks()
        {
            return saveData.currencyItemStacks;
        }

        public IReadOnlyList<OwnedUnitItem> GetOwnedUnitItems()
        {
            return saveData.ownedUnitItems;
        }

        public IReadOnlyList<OwnedUnitItem> GetEquippedUnitItems(string unitCardId)
        {
            var items = new List<OwnedUnitItem>();
            if (string.IsNullOrWhiteSpace(unitCardId))
            {
                return items;
            }

            for (var i = 0; i < saveData.ownedUnitItems.Count; i++)
            {
                var item = saveData.ownedUnitItems[i];
                if (item != null && string.Equals(item.equippedToUnitCardId, unitCardId, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public IReadOnlyList<OwnedUnitItem> GetCompatibleUnequippedUnitItems(string unitCardId)
        {
            var items = new List<OwnedUnitItem>();
            var card = FindUnitCard(unitCardId);
            if (card == null)
            {
                return items;
            }

            for (var i = 0; i < saveData.ownedUnitItems.Count; i++)
            {
                var item = saveData.ownedUnitItems[i];
                if (item == null || !string.IsNullOrWhiteSpace(item.equippedToUnitCardId))
                {
                    continue;
                }

                if (CanEquipItemToCard(item, card, out _))
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public int GetCurrencyAmount(string currencyItemDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(currencyItemDefinitionId))
            {
                return 0;
            }

            for (var i = 0; i < saveData.currencyItemStacks.Count; i++)
            {
                var stack = saveData.currencyItemStacks[i];
                if (stack != null && string.Equals(stack.currencyItemDefinitionId, currencyItemDefinitionId, StringComparison.OrdinalIgnoreCase))
                {
                    return Mathf.Max(0, stack.amount);
                }
            }

            return 0;
        }

        public IReadOnlyList<OwnedUnitCard> GetCardsAssignedToSlot(string hexSlotId)
        {
            var cards = new List<OwnedUnitCard>();
            for (var i = 0; i < saveData.ownedUnitCards.Count; i++)
            {
                var card = saveData.ownedUnitCards[i];
                if (card != null && string.Equals(card.assignedHexSlotId, hexSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        public IReadOnlyList<OwnedUnitCard> GetAvailableUnitCards(string selectedHexSlotId)
        {
            var cards = new List<OwnedUnitCard>();
            for (var i = 0; i < saveData.ownedUnitCards.Count; i++)
            {
                var card = saveData.ownedUnitCards[i];
                if (card == null || card.status == UnitCardStatus.Dead)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(card.assignedHexSlotId)
                    || string.Equals(card.assignedHexSlotId, selectedHexSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        public HexSlotSaveData GetHexSlotState(string hexSlotId)
        {
            return GetOrCreateSlotState(hexSlotId);
        }

        public bool TryPlaceMapItem(string mapItemId, string hexSlotId, out string error)
        {
            error = string.Empty;
            var slot = GetOrCreateSlotState(hexSlotId);
            if (slot.state != CampaignHexState.Open)
            {
                error = "Only open hexes can receive maps.";
                return false;
            }

            var mapItem = FindMapItem(mapItemId);
            if (mapItem == null)
            {
                error = "Map item was not found in inventory.";
                return false;
            }

            slot.state = CampaignHexState.Occupied;
            slot.occupiedMapItemId = mapItem.mapItemId;
            slot.selectedUnitCardIds.Clear();
            Save();
            return true;
        }

        public bool TryClearHex(string hexSlotId, out string error)
        {
            error = string.Empty;
            var slot = GetOrCreateSlotState(hexSlotId);
            if (slot.state != CampaignHexState.Occupied)
            {
                error = "Only occupied hexes can be cleared.";
                return false;
            }

            for (var i = 0; i < slot.selectedUnitCardIds.Count; i++)
            {
                var card = FindUnitCard(slot.selectedUnitCardIds[i]);
                if (card != null && card.status != UnitCardStatus.Dead)
                {
                    RefundDeploymentGold(card);
                    card.status = UnitCardStatus.Available;
                    card.assignedHexSlotId = string.Empty;
                }
            }

            slot.selectedUnitCardIds.Clear();
            slot.occupiedMapItemId = string.Empty;
            slot.state = CampaignHexState.Open;
            Save();
            return true;
        }

        public bool TryAssignUnitCardToHex(string unitCardId, string hexSlotId, out string error)
        {
            error = string.Empty;
            var slot = GetOrCreateSlotState(hexSlotId);
            if (slot.state != CampaignHexState.Occupied)
            {
                error = "Assign cards only after placing a map.";
                return false;
            }

            var card = FindUnitCard(unitCardId);
            if (card == null || card.status == UnitCardStatus.Dead)
            {
                error = "That unit card is no longer available.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(card.assignedHexSlotId)
                && !string.Equals(card.assignedHexSlotId, hexSlotId, StringComparison.OrdinalIgnoreCase))
            {
                error = "That unit card is already assigned to another map.";
                return false;
            }

            if (!slot.selectedUnitCardIds.Contains(card.unitCardId))
            {
                var fieldingCost = GetFieldingGoldCost(card);
                if (fieldingCost > 0)
                {
                    if (saveData.gold < fieldingCost)
                    {
                        error = "Not enough gold to field " + card.displayName + ". Cost: " + fieldingCost + ".";
                        return false;
                    }

                    saveData.gold -= fieldingCost;
                    card.deploymentGoldCostPaid = fieldingCost;
                }

                slot.selectedUnitCardIds.Add(card.unitCardId);
            }

            card.status = UnitCardStatus.Assigned;
            card.assignedHexSlotId = hexSlotId;
            Save();
            return true;
        }

        public bool TryUnassignUnitCardFromHex(string unitCardId, string hexSlotId, out string error)
        {
            error = string.Empty;
            var slot = GetOrCreateSlotState(hexSlotId);
            var card = FindUnitCard(unitCardId);
            if (slot == null || card == null)
            {
                error = "Unable to find the selected card.";
                return false;
            }

            slot.selectedUnitCardIds.Remove(unitCardId);
            if (card.status != UnitCardStatus.Dead)
            {
                RefundDeploymentGold(card);
                card.status = UnitCardStatus.Available;
                card.assignedHexSlotId = string.Empty;
            }

            Save();
            return true;
        }

        public bool TryEquipItemToUnitCard(string itemInstanceId, string unitCardId, out string error)
        {
            error = string.Empty;
            var item = FindOwnedUnitItem(itemInstanceId);
            var card = FindUnitCard(unitCardId);
            if (item == null || card == null)
            {
                error = "Unable to find the selected item or unit card.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.equippedToUnitCardId))
            {
                error = "That item is already equipped.";
                return false;
            }

            if (!CanEquipItemToCard(item, card, out error))
            {
                return false;
            }

            item.equippedToUnitCardId = card.unitCardId;
            card.equippedItemIds ??= new List<string>();
            if (!card.equippedItemIds.Contains(item.itemInstanceId))
            {
                card.equippedItemIds.Add(item.itemInstanceId);
            }

            Save();
            return true;
        }

        public bool TryUnequipItemFromUnitCard(string itemInstanceId, string unitCardId, out string error)
        {
            error = string.Empty;
            var item = FindOwnedUnitItem(itemInstanceId);
            var card = FindUnitCard(unitCardId);
            if (item == null || card == null)
            {
                error = "Unable to find the selected item or unit card.";
                return false;
            }

            if (!string.Equals(item.equippedToUnitCardId, card.unitCardId, StringComparison.OrdinalIgnoreCase))
            {
                error = "That item is not equipped to the selected unit.";
                return false;
            }

            item.equippedToUnitCardId = string.Empty;
            card.equippedItemIds?.Remove(item.itemInstanceId);
            Save();
            return true;
        }

        public bool TryApplyItemModification(string itemInstanceId, string currencyItemDefinitionId, out string error)
        {
            error = string.Empty;
            var item = FindOwnedUnitItem(itemInstanceId);
            if (item == null)
            {
                error = "Unable to find the selected item.";
                return false;
            }

            if (lootCatalogs == null || !lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var definition) || definition == null)
            {
                error = "The selected item definition was not found.";
                return false;
            }

            if (!lootCatalogs.TryGetCurrencyItemDefinition(currencyItemDefinitionId, out var currencyDefinition) || currencyDefinition == null)
            {
                error = "The selected currency definition was not found.";
                return false;
            }

            if (currencyDefinition.targetTypes != null
                && currencyDefinition.targetTypes.Count > 0
                && !ContainsIgnoreCase(currencyDefinition.targetTypes, definition.itemType))
            {
                error = "That currency cannot be used on " + definition.itemType + " items.";
                return false;
            }

            return currencyDefinition.actionType switch
            {
                CurrencyActionType.AddModifiers => TryApplyAddModifiersCurrency(item, definition, currencyDefinition, out error),
                _ => UnsupportedCurrencyAction(currencyDefinition, out error)
            };
        }

        public bool TryBuildResolvedUnitSpawnForCard(string unitCardId, out UnitSpawnConfig unitSpawn)
        {
            unitSpawn = null;
            var ownedCard = FindUnitCard(unitCardId);
            if (ownedCard == null || ownedCard.status == UnitCardStatus.Dead)
            {
                return false;
            }

            var catalog = GameDataCatalogLoader.Load();
            if (!catalog.TryGetUnitTemplate(ownedCard.baseTemplateId, out var template))
            {
                return false;
            }

            unitSpawn = BuildUnitSpawnConfigFromOwnedCard(catalog, template, ownedCard);
            if (unitSpawn == null)
            {
                return false;
            }

            ApplyEquippedItemEffects(ownedCard, unitSpawn);
            unitSpawn.ownedUnitCardId = ownedCard.unitCardId;
            return true;
        }

        public bool TryApplyMapModification(string mapItemId, string currencyItemDefinitionId, out string error)
        {
            error = string.Empty;
            var mapItem = FindMapItem(mapItemId);
            if (mapItem == null)
            {
                error = "Unable to find the selected map.";
                return false;
            }

            if (catalogs == null || !catalogs.TryGetMapDefinition(mapItem.mapDefinitionId, out var mapDefinition) || mapDefinition == null)
            {
                error = "The selected map definition was not found.";
                return false;
            }

            if (!lootCatalogs.TryGetCurrencyItemDefinition(currencyItemDefinitionId, out var currencyDefinition) || currencyDefinition == null)
            {
                error = "The selected currency definition was not found.";
                return false;
            }

            if (currencyDefinition.targetTypes != null
                && currencyDefinition.targetTypes.Count > 0
                && !ContainsIgnoreCase(currencyDefinition.targetTypes, "Map"))
            {
                error = "That currency cannot be used on maps.";
                return false;
            }

            return currencyDefinition.actionType switch
            {
                CurrencyActionType.AddModifiers => TryApplyAddModifiersCurrencyToMap(mapItem, mapDefinition, currencyDefinition, out error),
                _ => UnsupportedCurrencyAction(currencyDefinition, out error)
            };
        }

        private bool TryApplyAddModifiersCurrency(OwnedUnitItem item, ItemDefinition definition, CurrencyItemDefinition currencyDefinition, out string error)
        {
            error = string.Empty;
            if (item == null || definition == null || currencyDefinition == null || lootCatalogs == null)
            {
                error = "Unable to apply that currency to the selected item.";
                return false;
            }

            item.appliedModifiers ??= new List<AppliedItemModifierData>();
            var existingCount = item.appliedModifiers.Count;
            if (existingCount < currencyDefinition.minExistingModifiers || existingCount > currencyDefinition.maxExistingModifiers)
            {
                error = "That currency cannot be used on an item with the current number of modifiers.";
                return false;
            }

            if (existingCount >= currencyDefinition.maxModifiersPerItem)
            {
                error = "That item cannot hold more modifiers.";
                return false;
            }

            var candidates = BuildModifierTemplatePool(item, definition);
            if (candidates.Count == 0)
            {
                error = "No valid modifier templates matched that item.";
                return false;
            }

            if (!TrySpendCurrency(currencyDefinition.currencyItemDefinitionId, 1))
            {
                error = "Not enough " + currencyDefinition.currencyItemDefinitionId + ".";
                return false;
            }

            var addCount = UnityEngine.Random.Range(currencyDefinition.minAddedModifiers, currencyDefinition.maxAddedModifiers + 1);
            var remainingCapacity = Mathf.Max(0, currencyDefinition.maxModifiersPerItem - existingCount);
            addCount = Mathf.Clamp(addCount, 0, remainingCapacity);
            var addedCount = 0;
            for (var addIndex = 0; addIndex < addCount; addIndex++)
            {
                var template = SelectWeightedModifierTemplate(candidates);
                if (template == null)
                {
                    break;
                }

                item.appliedModifiers.Add(InstantiateModifier(template, currencyDefinition.currencyItemDefinitionId));
                candidates.Remove(template);
                addedCount++;
            }

            if (addedCount <= 0)
            {
                error = "No valid modifier could be applied.";
                return false;
            }

            Save();
            return true;
        }

        private bool TryApplyAddModifiersCurrencyToMap(OwnedMapItem mapItem, MapDefinition mapDefinition, CurrencyItemDefinition currencyDefinition, out string error)
        {
            error = string.Empty;
            if (mapItem == null || mapDefinition == null || currencyDefinition == null || catalogs == null)
            {
                error = "Unable to apply that currency to the selected map.";
                return false;
            }

            mapItem.appliedMapModifiers ??= new List<AppliedMapModifierData>();
            var existingCount = mapItem.appliedMapModifiers.Count;
            if (existingCount < currencyDefinition.minExistingModifiers || existingCount > currencyDefinition.maxExistingModifiers)
            {
                error = "That currency cannot be used on a map with the current number of modifiers.";
                return false;
            }

            if (existingCount >= currencyDefinition.maxModifiersPerItem)
            {
                error = "That map cannot hold more modifiers.";
                return false;
            }

            var candidates = BuildMapModifierTemplatePool(mapItem, mapDefinition);
            if (candidates.Count == 0)
            {
                error = "No valid map modifier templates matched that map.";
                return false;
            }

            if (!TrySpendCurrency(currencyDefinition.currencyItemDefinitionId, 1))
            {
                error = "Not enough " + currencyDefinition.currencyItemDefinitionId + ".";
                return false;
            }

            var addCount = UnityEngine.Random.Range(currencyDefinition.minAddedModifiers, currencyDefinition.maxAddedModifiers + 1);
            var remainingCapacity = Mathf.Max(0, currencyDefinition.maxModifiersPerItem - existingCount);
            addCount = Mathf.Clamp(addCount, 0, remainingCapacity);
            var addedCount = 0;
            for (var addIndex = 0; addIndex < addCount; addIndex++)
            {
                var template = SelectWeightedMapModifierTemplate(candidates);
                if (template == null)
                {
                    break;
                }

                mapItem.appliedMapModifiers.Add(InstantiateMapModifier(template));
                candidates.Remove(template);
                addedCount++;
            }

            if (addedCount <= 0)
            {
                error = "No valid map modifier could be applied.";
                return false;
            }

            Save();
            return true;
        }

        private static bool UnsupportedCurrencyAction(CurrencyItemDefinition currencyDefinition, out string error)
        {
            error = "Currency action not implemented: " + (currencyDefinition != null ? currencyDefinition.actionType.ToString() : "Unknown");
            return false;
        }

        public bool TryLaunchMissionFromHex(string hexSlotId, out string error)
        {
            error = string.Empty;
            if (pendingBattleResult != null)
            {
                error = "Resolve the current battle result before opening another map.";
                return false;
            }

            var slot = GetOrCreateSlotState(hexSlotId);
            if (slot.state != CampaignHexState.Occupied || string.IsNullOrWhiteSpace(slot.occupiedMapItemId))
            {
                error = "Place a map on the hex before opening it.";
                return false;
            }

            var mapItem = FindMapItem(slot.occupiedMapItemId);
            if (mapItem == null)
            {
                error = "The selected map item was not found.";
                return false;
            }

            if (!catalogs.TryGetMapDefinition(mapItem.mapDefinitionId, out var mapDefinition))
            {
                error = "The selected map definition was not found.";
                return false;
            }

            if (!SceneLoadUtility.CanLoadScene(mapDefinition.sceneName, out error))
            {
                return false;
            }

            activeMission = new PreparedMissionData
            {
                preparedMissionId = Guid.NewGuid().ToString("N"),
                hexSlotId = slot.hexSlotId,
                mapItemId = mapItem.mapItemId,
                mapDefinitionId = mapDefinition.mapDefinitionId,
                sceneName = mapDefinition.sceneName,
                selectedUnitCardIds = new List<string>(slot.selectedUnitCardIds)
            };

            awaitingMissionSceneLoad = true;
            Save();
            SceneLoadUtility.LoadScene(mapDefinition.sceneName);
            return true;
        }

        public void ApplyPreparedMission(SceneBattleConfig config, string sceneName)
        {
            if (config == null || activeMission == null || !string.Equals(activeMission.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            config.redTeam ??= new TeamConfig { units = Array.Empty<UnitSpawnConfig>() };
            var baseThreat = CalculateTeamThreat(config.redTeam.units);
            ApplyMapModifiersToMission(config);
            var modifiedThreat = CalculateTeamThreat(config.redTeam.units);
            activeMission.rewardProfile = BuildRewardProfile(baseThreat, modifiedThreat);

            var extraBlueUnits = BuildMissionBlueUnitCards(activeMission.selectedUnitCardIds);
            if (extraBlueUnits.Count == 0)
            {
                return;
            }

            config.blueTeam ??= new TeamConfig { units = Array.Empty<UnitSpawnConfig>() };
            var existingUnits = config.blueTeam.units ?? Array.Empty<UnitSpawnConfig>();
            var merged = new UnitSpawnConfig[existingUnits.Length + extraBlueUnits.Count];
            Array.Copy(existingUnits, merged, existingUnits.Length);
            for (var i = 0; i < extraBlueUnits.Count; i++)
            {
                merged[existingUnits.Length + i] = extraBlueUnits[i];
            }

            config.blueTeam.units = merged;
        }

        public void SetPendingBattleResult(BattleResultData result)
        {
            pendingBattleResult = result;
        }

        public void ApplyHeadQuarterStartupSettings(bool loadExistingSaveFile)
        {
            if (headQuarterStartupApplied)
            {
                return;
            }

            headQuarterStartupApplied = true;
            if (loadExistingSaveFile)
            {
                return;
            }

            saveData = CreateDefaultSave();
            activeMission = null;
            pendingBattleResult = null;
            awaitingMissionSceneLoad = false;
            Save();
        }

        public void FinalizePendingBattleResult()
        {
            if (pendingBattleResult == null || activeMission == null)
            {
                return;
            }

            var slot = GetOrCreateSlotState(activeMission.hexSlotId);
            if (slot != null)
            {
                slot.occupiedMapItemId = string.Empty;
                slot.selectedUnitCardIds.Clear();

                if (pendingBattleResult.victory)
                {
                    slot.state = CampaignHexState.Completed;
                    UnlockNeighboringHexes(slot.hexSlotId);
                }
                else
                {
                    slot.state = CampaignHexState.Open;
                }
            }

            var deadCards = new HashSet<string>(pendingBattleResult.deadUnitCardIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var survivingCards = new HashSet<string>(pendingBattleResult.survivingUnitCardIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < activeMission.selectedUnitCardIds.Count; i++)
            {
                var card = FindUnitCard(activeMission.selectedUnitCardIds[i]);
                if (card == null)
                {
                    continue;
                }

                card.deploymentGoldCostPaid = 0;
                card.assignedHexSlotId = string.Empty;
                card.timesDeployed++;
                if (deadCards.Contains(card.unitCardId))
                {
                    card.status = UnitCardStatus.Dead;
                    RemoveEquippedItemsForCard(card.unitCardId);
                }
                else
                {
                    card.status = UnitCardStatus.Available;
                    if (survivingCards.Contains(card.unitCardId))
                    {
                        card.timesSurvived++;
                    }
                }
            }

            ApplyClaimedLoot(pendingBattleResult.claimedLoot);
            ApplyAwardedUnitCards(pendingBattleResult.awardedUnitCards);
            saveData.lastResolvedBattleResult = pendingBattleResult;
            pendingBattleResult = null;
            activeMission = null;
            awaitingMissionSceneLoad = false;
            Save();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.Equals(scene.name, HeadQuarterSceneName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeadQuarterCamera();
                if (FindAnyObjectByType<HeadQuarterBootstrap>() == null)
                {
                    var bootstrapObject = new GameObject("HeadQuarterBootstrap");
                    bootstrapObject.AddComponent<HeadQuarterBootstrap>();
                }
            }

            if (!string.Equals(scene.name, HeadQuarterSceneName, StringComparison.OrdinalIgnoreCase)
                && activeMission != null
                && string.Equals(scene.name, activeMission.sceneName, StringComparison.OrdinalIgnoreCase)
                && FindAnyObjectByType<AutoBattlerBootstrap>() == null)
            {
                var bootstrapObject = new GameObject("AutoBattlerBootstrap");
                bootstrapObject.AddComponent<AutoBattlerBootstrap>();
            }

            if (activeMission == null || !awaitingMissionSceneLoad)
            {
                return;
            }

            if (!string.Equals(scene.name, activeMission.sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            awaitingMissionSceneLoad = false;
            ConsumeMapForActiveMission();
            Save();
        }

        private static void EnsureHeadQuarterCamera()
        {
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = new Color(0.1f, 0.14f, 0.16f);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.14f, 0.16f);
            cameraObject.AddComponent<AudioListener>();
        }

        private void ApplyMapModifiersToMission(SceneBattleConfig config)
        {
            var mapItem = activeMission == null ? null : FindMapItem(activeMission.mapItemId);
            if (config?.redTeam == null || mapItem?.appliedMapModifiers == null || mapItem.appliedMapModifiers.Count == 0)
            {
                return;
            }

            var enemyUnits = new List<UnitSpawnConfig>(config.redTeam.units ?? Array.Empty<UnitSpawnConfig>());
            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                ApplyMapModifierEffects(enemyUnits, mapItem.appliedMapModifiers[i], MapModifierEffectType.AdjustUnitCount, MapModifierEffectType.ReplaceUnitType);
            }

            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                ApplyMapModifierEffects(enemyUnits, mapItem.appliedMapModifiers[i], MapModifierEffectType.ModifyUnitStat);
            }

            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                ApplyMapModifierEffects(enemyUnits, mapItem.appliedMapModifiers[i], MapModifierEffectType.ModifyAmmoStat);
            }

            enemyUnits.RemoveAll(unit => unit == null || unit.count <= 0 || unit.definition == null);
            config.redTeam.units = enemyUnits.ToArray();

            Debug.Log("Applied map modifiers to " + activeMission.mapDefinitionId + ": " + string.Join(", ", BuildMapModifierDebugLabels(mapItem.appliedMapModifiers)));
        }

        private void ApplyMapModifierEffects(List<UnitSpawnConfig> units, AppliedMapModifierData modifier, params MapModifierEffectType[] supportedEffectTypes)
        {
            if (units == null
                || modifier == null
                || modifier.targetScope != MapModifierTargetScope.RedTeam
                || modifier.effects == null
                || modifier.effects.Count == 0)
            {
                return;
            }

            for (var effectIndex = 0; effectIndex < modifier.effects.Count; effectIndex++)
            {
                var effect = modifier.effects[effectIndex];
                if (effect == null || Array.IndexOf(supportedEffectTypes, effect.effectType) < 0)
                {
                    continue;
                }

                var targetIndexes = GetMatchingUnitIndexes(units, modifier.selectors, effect.maxAffectedEntries);
                if (targetIndexes.Count == 0)
                {
                    continue;
                }

                switch (effect.effectType)
                {
                    case MapModifierEffectType.AdjustUnitCount:
                        ApplyAdjustUnitCount(units, targetIndexes, effect);
                        break;
                    case MapModifierEffectType.ReplaceUnitType:
                        ApplyReplaceUnitType(units, targetIndexes, effect);
                        break;
                    case MapModifierEffectType.ModifyUnitStat:
                        ApplyModifyUnitStat(units, targetIndexes, effect);
                        break;
                    case MapModifierEffectType.ModifyAmmoStat:
                        ApplyModifyAmmoStat(units, targetIndexes, effect);
                        break;
                }
            }
        }

        private static List<int> GetMatchingUnitIndexes(List<UnitSpawnConfig> units, MapModifierSelectorDefinition selectors, int maxAffectedEntries)
        {
            var results = new List<int>();
            if (units == null)
            {
                return results;
            }

            var allowAll = selectors == null || selectors.all || string.IsNullOrWhiteSpace(selectors.unitType);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit?.definition == null)
                {
                    continue;
                }

                if (!allowAll
                    && !string.Equals(unit.definition.TemplateId, selectors.unitType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(i);
                if (maxAffectedEntries > 0 && results.Count >= maxAffectedEntries)
                {
                    break;
                }
            }

            return results;
        }

        private static void ApplyAdjustUnitCount(List<UnitSpawnConfig> units, List<int> indexes, AppliedMapModifierEffectData effect)
        {
            for (var i = 0; i < indexes.Count; i++)
            {
                var unit = units[indexes[i]];
                if (unit == null)
                {
                    continue;
                }

                unit.count = Mathf.Max(1, ApplyIntOperation(unit.count, effect.operation, effect.rolledValue));
            }
        }

        private static void ApplyModifyUnitStat(List<UnitSpawnConfig> units, List<int> indexes, AppliedMapModifierEffectData effect)
        {
            for (var i = 0; i < indexes.Count; i++)
            {
                var unit = units[indexes[i]];
                if (unit?.definition == null)
                {
                    continue;
                }

                var definition = unit.definition;
                var ammunition = CloneAmmunition(definition.Ammunition);
                var ammunitionCounts = CloneAmmunitionCounts(definition.AmmunitionCounts);
                var updatedDefinition = BuildUpdatedUnitDefinition(
                    definition,
                    effect.statKey,
                    effect.operation,
                    effect.rolledValue,
                    ammunitionCounts,
                    ammunition);

                if (updatedDefinition != null)
                {
                    unit.definition = updatedDefinition;
                }
            }
        }

        private static void ApplyModifyAmmoStat(List<UnitSpawnConfig> units, List<int> indexes, AppliedMapModifierEffectData effect)
        {
            for (var i = 0; i < indexes.Count; i++)
            {
                var unit = units[indexes[i]];
                if (unit?.definition == null)
                {
                    continue;
                }

                var definition = unit.definition;
                var ammunition = CloneAmmunition(definition.Ammunition);
                var ammunitionCounts = CloneAmmunitionCounts(definition.AmmunitionCounts);
                var changed = false;
                for (var ammoIndex = 0; ammoIndex < ammunition.Length; ammoIndex++)
                {
                    var ammo = ammunition[ammoIndex];
                    if (ammo == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(effect.ammoType)
                        && !string.Equals(ammo.AmmoName, effect.ammoType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ammunition[ammoIndex] = BuildUpdatedAmmoDefinition(ammo, effect.statKey, effect.operation, effect.rolledValue);
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                unit.definition = new UnitDefinition(
                    definition.TemplateId,
                    definition.UnitName,
                    definition.UnitType,
                    definition.MaxHealth,
                    definition.Armor,
                    definition.VisionRange,
                    definition.Speed,
                    definition.Accuracy,
                    definition.FireReliability,
                    definition.MoveReliability,
                    definition.OutgoingDamageBonusMin,
                    definition.OutgoingDamageBonusMax,
                    definition.NavigationAgentType,
                    definition.TerrainSpeedProfile,
                    definition.TerrainPathCostProfile,
                    ammunitionCounts,
                    ammunition);
            }
        }

        private void ApplyReplaceUnitType(List<UnitSpawnConfig> units, List<int> indexes, AppliedMapModifierEffectData effect)
        {
            if (string.IsNullOrWhiteSpace(effect.replacementUnitType))
            {
                return;
            }

            var gameCatalog = GameDataCatalogLoader.Load();
            if (!gameCatalog.TryGetUnitTemplate(effect.replacementUnitType, out var replacementTemplate) || replacementTemplate == null)
            {
                Debug.LogWarning("Unknown replacement unit template in map modifier: " + effect.replacementUnitType);
                return;
            }

            for (var i = 0; i < indexes.Count; i++)
            {
                var source = units[indexes[i]];
                if (source?.definition == null)
                {
                    continue;
                }

                var replacementAmmo = BuildTemplateAmmunition(replacementTemplate, out var replacementCounts);
                source.definition = replacementTemplate.BuildDefinition(source.definition.UnitName, replacementAmmo, replacementCounts);
            }
        }

        private static AmmoDefinition[] BuildTemplateAmmunition(GameUnitTemplate template, out int[] ammunitionCounts)
        {
            var loadout = template.GetAmmunitionLoadout();
            var ammunition = new AmmoDefinition[loadout.Length];
            ammunitionCounts = new int[loadout.Length];
            for (var i = 0; i < loadout.Length; i++)
            {
                ammunitionCounts[i] = loadout[i].AmmunitionCount;
                var ammo = loadout[i].Definition;
                ammunition[i] = ammo == null
                    ? new AmmoDefinition(loadout[i].AmmoType, template.UnitType, 0, 0, 0f, 0.1f, 1f, 1f, 1f)
                    : new AmmoDefinition(
                        ammo.AmmoName,
                        ammo.RequiredUserType,
                        ammo.DamageMin,
                        ammo.DamageMax,
                        ammo.Radius,
                        ammo.AttackRange,
                        ammo.ReloadTime,
                        ammo.Accuracy,
                        ammo.DamageReliability);
            }

            return ammunition;
        }

        private static UnitDefinition BuildUpdatedUnitDefinition(
            UnitDefinition definition,
            string statKey,
            MapModifierOperation operation,
            int rolledValue,
            int[] ammunitionCounts,
            AmmoDefinition[] ammunition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(statKey))
            {
                return null;
            }

            var maxHealth = definition.MaxHealth;
            var armor = definition.Armor;
            var visionRange = definition.VisionRange;
            var speed = definition.Speed;
            var accuracy = definition.Accuracy;
            var fireReliability = definition.FireReliability;
            var moveReliability = definition.MoveReliability;

            switch (statKey)
            {
                case "maxHealth":
                    maxHealth = Mathf.Max(1, ApplyIntOperation(maxHealth, operation, rolledValue));
                    break;
                case "armor":
                    armor = Mathf.Max(0, ApplyIntOperation(armor, operation, rolledValue));
                    break;
                case "visionRange":
                    visionRange = Mathf.Max(0.1f, ApplyFloatOperation(visionRange, operation, rolledValue));
                    break;
                case "speed":
                    speed = Mathf.Max(0.1f, ApplyFloatOperation(speed, operation, rolledValue));
                    break;
                case "accuracy":
                    accuracy = Mathf.Clamp01(ApplyFloatOperation(accuracy, operation, rolledValue));
                    break;
                case "fireReliability":
                    fireReliability = Mathf.Clamp01(ApplyFloatOperation(fireReliability, operation, rolledValue));
                    break;
                case "moveReliability":
                    moveReliability = Mathf.Clamp01(ApplyFloatOperation(moveReliability, operation, rolledValue));
                    break;
                default:
                    return null;
            }

            return new UnitDefinition(
                definition.TemplateId,
                definition.UnitName,
                definition.UnitType,
                maxHealth,
                armor,
                visionRange,
                speed,
                accuracy,
                fireReliability,
                moveReliability,
                definition.OutgoingDamageBonusMin,
                definition.OutgoingDamageBonusMax,
                definition.NavigationAgentType,
                definition.TerrainSpeedProfile,
                definition.TerrainPathCostProfile,
                ammunitionCounts,
                ammunition);
        }

        private static AmmoDefinition BuildUpdatedAmmoDefinition(AmmoDefinition ammo, string statKey, MapModifierOperation operation, int rolledValue)
        {
            if (ammo == null || string.IsNullOrWhiteSpace(statKey))
            {
                return ammo;
            }

            var damageMin = ammo.DamageMin;
            var damageMax = ammo.DamageMax;
            var radius = ammo.Radius;
            var attackRange = ammo.AttackRange;
            var reloadTime = ammo.ReloadTime;
            var accuracy = ammo.Accuracy;
            var damageReliability = ammo.DamageReliability;

            switch (statKey)
            {
                case "damageMin":
                    damageMin = Mathf.Max(0, ApplyIntOperation(damageMin, operation, rolledValue));
                    damageMax = Mathf.Max(damageMin, damageMax);
                    break;
                case "damageMax":
                    damageMax = Mathf.Max(damageMin, ApplyIntOperation(damageMax, operation, rolledValue));
                    break;
                case "radius":
                    radius = Mathf.Max(0f, ApplyFloatOperation(radius, operation, rolledValue));
                    break;
                case "attackRange":
                    attackRange = Mathf.Max(0.1f, ApplyFloatOperation(attackRange, operation, rolledValue));
                    break;
                case "reloadTime":
                    reloadTime = Mathf.Max(0.1f, ApplyFloatOperation(reloadTime, operation, rolledValue));
                    break;
                case "accuracy":
                    accuracy = Mathf.Clamp01(ApplyFloatOperation(accuracy, operation, rolledValue));
                    break;
                case "damageReliability":
                    damageReliability = Mathf.Clamp01(ApplyFloatOperation(damageReliability, operation, rolledValue));
                    break;
                default:
                    return ammo;
            }

            return new AmmoDefinition(
                ammo.AmmoName,
                ammo.RequiredUserType,
                damageMin,
                damageMax,
                radius,
                attackRange,
                reloadTime,
                accuracy,
                damageReliability);
        }

        private static int ApplyIntOperation(int currentValue, MapModifierOperation operation, int rolledValue)
        {
            return operation switch
            {
                MapModifierOperation.Multiply => Mathf.RoundToInt(currentValue * rolledValue),
                MapModifierOperation.Set => rolledValue,
                _ => currentValue + rolledValue
            };
        }

        private static float ApplyFloatOperation(float currentValue, MapModifierOperation operation, int rolledValue)
        {
            return operation switch
            {
                MapModifierOperation.Multiply => currentValue * rolledValue,
                MapModifierOperation.Set => rolledValue,
                _ => currentValue + rolledValue
            };
        }

        private static AmmoDefinition[] CloneAmmunition(AmmoDefinition[] source)
        {
            if (source == null)
            {
                return Array.Empty<AmmoDefinition>();
            }

            var clone = new AmmoDefinition[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var ammo = source[i];
                clone[i] = ammo == null
                    ? null
                    : new AmmoDefinition(
                        ammo.AmmoName,
                        ammo.RequiredUserType,
                        ammo.DamageMin,
                        ammo.DamageMax,
                        ammo.Radius,
                        ammo.AttackRange,
                        ammo.ReloadTime,
                        ammo.Accuracy,
                        ammo.DamageReliability);
            }

            return clone;
        }

        private static int[] CloneAmmunitionCounts(int[] source)
        {
            if (source == null)
            {
                return Array.Empty<int>();
            }

            var clone = new int[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        private float CalculateTeamThreat(UnitSpawnConfig[] units)
        {
            if (units == null || units.Length == 0)
            {
                return 0f;
            }

            var gameCatalog = GameDataCatalogLoader.Load();
            var totalThreat = 0f;
            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit?.definition == null)
                {
                    continue;
                }

                totalThreat += CalculateUnitThreat(unit, gameCatalog);
            }

            totalThreat += CalculateMapModifierThreatOverride();
            return Mathf.Max(0f, totalThreat);
        }

        private float CalculateMapModifierThreatOverride()
        {
            var mapItem = activeMission == null ? null : FindMapItem(activeMission.mapItemId);
            if (mapItem?.appliedMapModifiers == null)
            {
                return 0f;
            }

            var totalOverride = 0f;
            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                var modifier = mapItem.appliedMapModifiers[i];
                if (modifier != null && modifier.threatDeltaOverride > 0f)
                {
                    totalOverride += modifier.threatDeltaOverride;
                }
            }

            return totalOverride;
        }

        private static float CalculateUnitThreat(UnitSpawnConfig unit, GameDataCatalog gameCatalog)
        {
            if (unit?.definition == null || gameCatalog == null || !gameCatalog.TryGetUnitTemplate(unit.definition.TemplateId, out var template) || template == null)
            {
                return 0f;
            }

            var count = Mathf.Max(1, unit.count);
            var perUnitThreat = Mathf.Max(0f, template.ThreatValue);
            var definition = unit.definition;

            perUnitThreat += Mathf.Max(0f, RatioDelta(definition.MaxHealth, template.MaxHealth)) * 5f;
            perUnitThreat += Mathf.Max(0f, definition.Armor - template.Armor) * 0.6f;
            perUnitThreat += Mathf.Max(0f, RatioDelta(definition.Speed, template.Speed)) * 3f;
            perUnitThreat += Mathf.Max(0f, RatioDelta(definition.Accuracy, template.Accuracy)) * 4f;
            perUnitThreat += Mathf.Max(0f, RatioDelta(definition.FireReliability, template.FireReliability)) * 2f;
            perUnitThreat += Mathf.Max(0f, RatioDelta(definition.MoveReliability, template.MoveReliability)) * 1.5f;

            var baseLoadout = template.GetAmmunitionLoadout();
            var currentAmmo = definition.Ammunition ?? Array.Empty<AmmoDefinition>();
            var ammoCount = Mathf.Min(baseLoadout.Length, currentAmmo.Length);
            for (var ammoIndex = 0; ammoIndex < ammoCount; ammoIndex++)
            {
                var baseAmmo = baseLoadout[ammoIndex].Definition;
                var modifiedAmmo = currentAmmo[ammoIndex];
                if (baseAmmo == null || modifiedAmmo == null)
                {
                    continue;
                }

                perUnitThreat += Mathf.Max(0f, RatioDelta(modifiedAmmo.Damage, baseAmmo.Damage)) * 6f;
                perUnitThreat += Mathf.Max(0f, RatioDelta(baseAmmo.ReloadTime, modifiedAmmo.ReloadTime)) * 5f;
                perUnitThreat += Mathf.Max(0f, RatioDelta(modifiedAmmo.AttackRange, baseAmmo.AttackRange)) * 2.5f;
                perUnitThreat += Mathf.Max(0f, RatioDelta(modifiedAmmo.Radius, baseAmmo.Radius)) * 3f;
            }

            return perUnitThreat * count;
        }

        private static float RatioDelta(float currentValue, float baseValue)
        {
            if (baseValue <= 0.0001f)
            {
                return 0f;
            }

            return (currentValue / baseValue) - 1f;
        }

        private static PreparedMissionRewardProfile BuildRewardProfile(float baseThreat, float modifiedThreat)
        {
            var rewardProfile = new PreparedMissionRewardProfile
            {
                baseThreat = Mathf.Max(0f, baseThreat),
                modifiedThreat = Mathf.Max(0f, modifiedThreat),
                threatRatio = baseThreat > 0.01f ? modifiedThreat / baseThreat : 1f
            };

            var effectiveRatio = Mathf.Max(1f, rewardProfile.threatRatio);
            if (effectiveRatio <= 1.1f)
            {
                rewardProfile.rewardMultiplier = effectiveRatio > 1f ? 1.05f : 1f;
                rewardProfile.bonusLootRollChance = effectiveRatio > 1f ? 0.1f : 0f;
                return rewardProfile;
            }

            if (effectiveRatio <= 1.25f)
            {
                rewardProfile.rewardMultiplier = 1.15f;
                rewardProfile.bonusLootRollChance = 0.2f;
                return rewardProfile;
            }

            if (effectiveRatio <= 1.5f)
            {
                rewardProfile.rewardMultiplier = 1.3f;
                rewardProfile.bonusLootRollChance = 0.35f;
                return rewardProfile;
            }

            if (effectiveRatio <= 2f)
            {
                rewardProfile.rewardMultiplier = 1.6f;
                rewardProfile.bonusLootRollChance = 0.5f;
                return rewardProfile;
            }

            rewardProfile.rewardMultiplier = 2f;
            rewardProfile.bonusLootRollChance = 0.75f;
            return rewardProfile;
        }

        private static string[] BuildMapModifierDebugLabels(List<AppliedMapModifierData> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return Array.Empty<string>();
            }

            var labels = new List<string>(modifiers.Count);
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null)
                {
                    continue;
                }

                labels.Add(string.IsNullOrWhiteSpace(modifier.displayName) ? modifier.mapModifierTemplateId : modifier.displayName);
            }

            return labels.ToArray();
        }

        private List<AppliedMapModifierData> CreateAppliedMapModifiers(StartingMapEntry entry)
        {
            var modifiers = new List<AppliedMapModifierData>();
            if (entry?.appliedMapModifierTemplateIds == null || entry.appliedMapModifierTemplateIds.Count == 0 || catalogs == null)
            {
                return modifiers;
            }

            for (var i = 0; i < entry.appliedMapModifierTemplateIds.Count; i++)
            {
                var templateId = entry.appliedMapModifierTemplateIds[i];
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    continue;
                }

                if (!catalogs.TryGetMapModifierTemplate(templateId, out var template) || template == null)
                {
                    Debug.LogWarning("Unknown map modifier template in starting loadout: " + templateId);
                    continue;
                }

                modifiers.Add(InstantiateMapModifier(template));
            }

            return modifiers;
        }

        private static AppliedMapModifierData InstantiateMapModifier(MapModifierTemplateDefinition template)
        {
            var applied = new AppliedMapModifierData
            {
                mapModifierTemplateId = template.mapModifierTemplateId,
                displayName = template.displayName,
                description = template.description,
                targetScope = template.targetScope,
                selectors = template.selectors == null
                    ? new MapModifierSelectorDefinition()
                    : new MapModifierSelectorDefinition
                    {
                        all = template.selectors.all,
                        unitType = template.selectors.unitType
                    },
                threatDeltaOverride = template.threatDeltaOverride
            };

            if (template.effects == null)
            {
                return applied;
            }

            for (var i = 0; i < template.effects.Count; i++)
            {
                var effect = template.effects[i];
                if (effect == null)
                {
                    continue;
                }

                applied.effects.Add(new AppliedMapModifierEffectData
                {
                    effectType = effect.effectType,
                    statKey = effect.statKey,
                    operation = effect.operation,
                    rolledValue = RollInclusive(effect.minValue, effect.maxValue),
                    ammoType = effect.ammoType,
                    replacementUnitType = effect.replacementUnitType,
                    maxAffectedEntries = effect.maxAffectedEntries
                });
            }

            return applied;
        }

        private List<UnitSpawnConfig> BuildMissionBlueUnitCards(List<string> selectedUnitCardIds)
        {
            var result = new List<UnitSpawnConfig>();
            if (selectedUnitCardIds == null || selectedUnitCardIds.Count == 0)
            {
                return result;
            }

            var catalog = GameDataCatalogLoader.Load();
            for (var i = 0; i < selectedUnitCardIds.Count; i++)
            {
                var ownedCard = FindUnitCard(selectedUnitCardIds[i]);
                if (ownedCard == null || ownedCard.status == UnitCardStatus.Dead)
                {
                    continue;
                }

                if (!catalog.TryGetUnitTemplate(ownedCard.baseTemplateId, out var template))
                {
                    Debug.LogWarning("Unknown unit template for owned unit card: " + ownedCard.baseTemplateId);
                    continue;
                }

                var unitSpawn = BuildUnitSpawnConfigFromOwnedCard(catalog, template, ownedCard);
                if (unitSpawn == null)
                {
                    continue;
                }

                ApplyEquippedItemEffects(ownedCard, unitSpawn);
                unitSpawn.ownedUnitCardId = ownedCard.unitCardId;
                result.Add(unitSpawn);
            }

            return result;
        }

        private static UnitSpawnConfig BuildUnitSpawnConfigFromOwnedCard(GameDataCatalog catalog, GameUnitTemplate template, OwnedUnitCard ownedCard)
        {
            if (catalog == null || template == null || ownedCard == null)
            {
                return null;
            }

            var resolvedName = string.IsNullOrWhiteSpace(ownedCard.displayName) ? template.UnitName : ownedCard.displayName;
            if (string.IsNullOrWhiteSpace(ownedCard.overrideJson))
            {
                return UnitSpawnConfig.FromTemplate(catalog, template.UnitTypeKey, resolvedName, 1);
            }

            var source = JsonDataHelper.AsObject(MiniJson.Deserialize(ownedCard.overrideJson));
            if (source == null)
            {
                return UnitSpawnConfig.FromTemplate(catalog, template.UnitTypeKey, resolvedName, 1);
            }

            source["unitType"] = template.UnitTypeKey;
            source["unitName"] = resolvedName;
            source["count"] = 1L;
            if (!SceneBattleConfigLoader.TryBuildUnitSpawnConfigFromSource(source, catalog, out var unitSpawn))
            {
                Debug.LogWarning("Failed to build owned unit card from override payload: " + ownedCard.unitCardId);
                return null;
            }

            return unitSpawn;
        }

        private void ApplyEquippedItemEffects(OwnedUnitCard ownedCard, UnitSpawnConfig unitSpawn)
        {
            if (ownedCard == null || unitSpawn == null || unitSpawn.definition == null || lootCatalogs == null)
            {
                return;
            }

            var effects = new List<ItemEffectDefinition>();
            var damageBonusMin = 0;
            var damageBonusMax = 0;
            for (var i = 0; i < ownedCard.equippedItemIds.Count; i++)
            {
                var ownedItem = FindOwnedUnitItem(ownedCard.equippedItemIds[i]);
                if (ownedItem == null || !string.Equals(ownedItem.equippedToUnitCardId, ownedCard.unitCardId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!lootCatalogs.TryGetItemDefinition(ownedItem.itemDefinitionId, out var itemDefinition) || itemDefinition == null)
                {
                    continue;
                }

                AppendItemEffects(effects, itemDefinition.effects);
                AppendAppliedModifierEffects(effects, ownedItem, ref damageBonusMin, ref damageBonusMax);
            }

            if (effects.Count == 0 && damageBonusMin == 0 && damageBonusMax == 0)
            {
                return;
            }

            unitSpawn.definition = ApplyItemEffectsToDefinition(unitSpawn.definition, effects, damageBonusMin, damageBonusMax);
        }

        private static UnitDefinition ApplyItemEffectsToDefinition(UnitDefinition definition, List<ItemEffectDefinition> effects, int damageBonusMin, int damageBonusMax)
        {
            if (definition == null)
            {
                return definition;
            }

            var maxHealth = definition.MaxHealth;
            var armor = definition.Armor;
            var visionRange = definition.VisionRange;
            var speed = definition.Speed;
            var accuracy = definition.Accuracy;
            var fireReliability = definition.FireReliability;
            var moveReliability = definition.MoveReliability;

            for (var i = 0; effects != null && i < effects.Count; i++)
            {
                var effect = effects[i];
                switch (effect.statKey)
                {
                    case "maxHealth":
                        maxHealth = Mathf.Max(1, Mathf.RoundToInt(ApplyOperation(maxHealth, effect)));
                        break;
                    case "armor":
                        armor = Mathf.Max(0, Mathf.RoundToInt(ApplyOperation(armor, effect)));
                        break;
                    case "visionRange":
                        visionRange = Mathf.Max(0.1f, ApplyOperation(visionRange, effect));
                        break;
                    case "speed":
                        speed = Mathf.Max(0.1f, ApplyOperation(speed, effect));
                        break;
                    case "accuracy":
                        accuracy = Mathf.Clamp01(ApplyOperation(accuracy, effect));
                        break;
                    case "fireReliability":
                        fireReliability = Mathf.Clamp01(ApplyOperation(fireReliability, effect));
                        break;
                    case "moveReliability":
                        moveReliability = Mathf.Clamp01(ApplyOperation(moveReliability, effect));
                        break;
                }
            }

            var ammoCounts = (int[])definition.AmmunitionCounts.Clone();
            var ammoDefinitions = new AmmoDefinition[definition.Ammunition.Length];
            Array.Copy(definition.Ammunition, ammoDefinitions, ammoDefinitions.Length);

            return new UnitDefinition(
                definition.TemplateId,
                definition.UnitName,
                definition.UnitType,
                maxHealth,
                armor,
                visionRange,
                speed,
                accuracy,
                fireReliability,
                moveReliability,
                Mathf.Max(0, definition.OutgoingDamageBonusMin + damageBonusMin),
                Mathf.Max(0, definition.OutgoingDamageBonusMax + damageBonusMax),
                definition.NavigationAgentType,
                definition.TerrainSpeedProfile,
                definition.TerrainPathCostProfile,
                ammoCounts,
                ammoDefinitions);
        }

        private static float ApplyOperation(float currentValue, ItemEffectDefinition effect)
        {
            return effect.operation == ItemEffectOperation.Multiply
                ? currentValue * effect.value
                : currentValue + effect.value;
        }

        private void UnlockNeighboringHexes(string hexSlotId)
        {
            var definition = CampaignBoardLayout.GetDefinition(hexSlotId);
            if (definition == null)
            {
                return;
            }

            for (var i = 0; i < definition.Neighbors.Length; i++)
            {
                var neighbor = GetOrCreateSlotState(definition.Neighbors[i]);
                if (neighbor.state == CampaignHexState.Locked)
                {
                    neighbor.state = CampaignHexState.Open;
                }
            }
        }

        private void ConsumeMapForActiveMission()
        {
            if (activeMission == null)
            {
                return;
            }

            for (var i = saveData.ownedMapItems.Count - 1; i >= 0; i--)
            {
                if (string.Equals(saveData.ownedMapItems[i].mapItemId, activeMission.mapItemId, StringComparison.OrdinalIgnoreCase))
                {
                    saveData.ownedMapItems.RemoveAt(i);
                    return;
                }
            }
        }

        private CampaignSaveData LoadOrCreateSave()
        {
            var path = GetSavePath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonUtility.FromJson<CampaignSaveData>(json);
                    if (loaded != null)
                    {
                        EnsureSaveDefaults(loaded);
                        return loaded;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Failed to load campaign save. Recreating default save. " + exception.Message);
                }
            }

            var created = CreateDefaultSave();
            Save(created);
            return created;
        }

        private CampaignSaveData CreateDefaultSave()
        {
            var created = new CampaignSaveData();
            var startingLoadout = catalogs?.StartingLoadout ?? new StartingLoadoutDefinition();
            created.playerExperience = Mathf.Max(0, startingLoadout.startingExperience);
            created.gold = Mathf.Max(0, startingLoadout.startingGold);

            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                created.hexBoardState.Add(new HexSlotSaveData
                {
                    hexSlotId = definition.SlotId,
                    state = definition.InitiallyOpen ? CampaignHexState.Open : CampaignHexState.Locked
                });
            }

            for (var i = 0; i < startingLoadout.startingCurrencyItems.Count; i++)
            {
                var entry = startingLoadout.startingCurrencyItems[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.currencyItemDefinitionId))
                {
                    continue;
                }

                created.currencyItemStacks.Add(new CurrencyItemStack
                {
                    currencyItemDefinitionId = entry.currencyItemDefinitionId,
                    amount = Mathf.Max(1, entry.amount)
                });
            }

            var mapItemSequence = 1;
            for (var i = 0; i < startingLoadout.startingMaps.Count; i++)
            {
                var entry = startingLoadout.startingMaps[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.mapDefinitionId))
                {
                    continue;
                }

                var prefix = ResolveMapInstanceNamePrefix(entry);
                for (var count = 0; count < Mathf.Max(1, entry.count); count++)
                {
                    created.ownedMapItems.Add(new OwnedMapItem
                    {
                        mapItemId = "map_item_" + mapItemSequence.ToString("D3"),
                        mapDefinitionId = entry.mapDefinitionId,
                        instanceName = prefix + " " + ToRomanNumeral(count + 1),
                        appliedMapModifiers = CreateAppliedMapModifiers(entry)
                    });
                    mapItemSequence++;
                }
            }

            var unitCardSequence = 1;
            for (var i = 0; i < startingLoadout.startingUnitCards.Count; i++)
            {
                var entry = startingLoadout.startingUnitCards[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.unitCardDefinitionId))
                {
                    continue;
                }

                if (!catalogs.TryGetUnitCardDefinition(entry.unitCardDefinitionId, out var definition))
                {
                    Debug.LogWarning("Unknown starting unit card definition: " + entry.unitCardDefinitionId);
                    continue;
                }

                var prefix = ResolveUnitCardNamePrefix(entry, definition);
                for (var count = 0; count < Mathf.Max(1, entry.count); count++)
                {
                    created.ownedUnitCards.Add(new OwnedUnitCard
                    {
                        unitCardId = "unit_card_" + unitCardSequence.ToString("D3"),
                        definitionId = definition.unitCardDefinitionId,
                        displayName = prefix + "-" + (count + 1),
                        baseTemplateId = definition.baseTemplateId,
                        overrideJson = entry.overrideJson,
                        status = UnitCardStatus.Available
                    });
                    unitCardSequence++;
                }
            }

            return created;
        }

        private string ResolveMapInstanceNamePrefix(StartingMapEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.instanceNamePrefix))
            {
                return entry.instanceNamePrefix;
            }

            if (catalogs != null && catalogs.TryGetMapDefinition(entry.mapDefinitionId, out var definition))
            {
                return string.IsNullOrWhiteSpace(definition.displayName) ? entry.mapDefinitionId : definition.displayName;
            }

            return entry.mapDefinitionId;
        }

        private static string ResolveUnitCardNamePrefix(StartingUnitCardEntry entry, UnitCardDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(entry.displayNamePrefix))
            {
                return entry.displayNamePrefix;
            }

            return string.IsNullOrWhiteSpace(definition.displayName) ? definition.unitCardDefinitionId : definition.displayName;
        }

        private string GenerateUniqueOwnedUnitCardName(string requestedName, UnitCardDefinition definition)
        {
            var baseName = requestedName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : definition != null ? definition.unitCardDefinitionId : "Unit";
            }

            var candidate = baseName;
            var suffix = 2;
            while (HasOwnedUnitCardName(candidate))
            {
                candidate = baseName + "-" + suffix;
                suffix++;
            }

            return candidate;
        }

        private bool HasOwnedUnitCardName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            for (var i = 0; i < saveData.ownedUnitCards.Count; i++)
            {
                var card = saveData.ownedUnitCards[i];
                if (card != null && string.Equals(card.displayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyClaimedLoot(List<DroppedLootEntry> claimedLoot)
        {
            if (claimedLoot == null || claimedLoot.Count == 0)
            {
                return;
            }

            for (var i = 0; i < claimedLoot.Count; i++)
            {
                var entry = claimedLoot[i];
                if (entry == null)
                {
                    continue;
                }

                switch (entry.rewardType)
                {
                    case LootRewardType.Gold:
                        saveData.gold += Mathf.Max(0, entry.amount);
                        break;

                    case LootRewardType.MapItem:
                        AddOwnedMapItem(entry);
                        break;

                    case LootRewardType.UnitItem:
                        AddOwnedUnitItem(entry);
                        break;

                    case LootRewardType.CurrencyItem:
                        AddCurrencyItemStack(entry);
                        break;
                }
            }
        }

        private void ApplyAwardedUnitCards(List<AwardedUnitCardData> awardedUnitCards)
        {
            if (awardedUnitCards == null || awardedUnitCards.Count == 0)
            {
                return;
            }

            for (var i = 0; i < awardedUnitCards.Count; i++)
            {
                AddAwardedUnitCard(awardedUnitCards[i]);
            }
        }

        private void AddAwardedUnitCard(AwardedUnitCardData awardedCard)
        {
            if (awardedCard == null || string.IsNullOrWhiteSpace(awardedCard.baseTemplateId))
            {
                return;
            }

            if (!catalogs.TryGetUnitCardDefinition(awardedCard.baseTemplateId, out var definition) || definition == null)
            {
                Debug.LogWarning("Unable to award unit card for unknown template: " + awardedCard.baseTemplateId);
                return;
            }

            saveData.ownedUnitCards.Add(new OwnedUnitCard
            {
                unitCardId = "unit_card_" + Guid.NewGuid().ToString("N"),
                definitionId = definition.unitCardDefinitionId,
                displayName = GenerateUniqueOwnedUnitCardName(awardedCard.displayName, definition),
                baseTemplateId = definition.baseTemplateId,
                overrideJson = awardedCard.overrideJson ?? string.Empty,
                status = UnitCardStatus.Available
            });
        }

        private void AddOwnedMapItem(DroppedLootEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.mapDefinitionId))
            {
                return;
            }

            var instanceName = entry.displayName;
            if (catalogs != null && catalogs.TryGetMapDefinition(entry.mapDefinitionId, out var definition))
            {
                instanceName = string.IsNullOrWhiteSpace(definition.displayName) ? entry.mapDefinitionId : definition.displayName;
            }

            for (var count = 0; count < Mathf.Max(1, entry.amount); count++)
            {
                saveData.ownedMapItems.Add(new OwnedMapItem
                {
                    mapItemId = "map_item_" + Guid.NewGuid().ToString("N"),
                    mapDefinitionId = entry.mapDefinitionId,
                    instanceName = instanceName,
                    appliedMapModifiers = new List<AppliedMapModifierData>()
                });
            }
        }

        private void AddOwnedUnitItem(DroppedLootEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemDefinitionId))
            {
                return;
            }

            for (var count = 0; count < Mathf.Max(1, entry.amount); count++)
            {
                saveData.ownedUnitItems.Add(new OwnedUnitItem
                {
                    itemInstanceId = "item_" + Guid.NewGuid().ToString("N"),
                    itemDefinitionId = entry.itemDefinitionId,
                    equippedToUnitCardId = string.Empty
                });
            }
        }

        private void AddCurrencyItemStack(DroppedLootEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.currencyItemDefinitionId))
            {
                return;
            }

            for (var i = 0; i < saveData.currencyItemStacks.Count; i++)
            {
                var stack = saveData.currencyItemStacks[i];
                if (stack != null && string.Equals(stack.currencyItemDefinitionId, entry.currencyItemDefinitionId, StringComparison.OrdinalIgnoreCase))
                {
                    stack.amount += Mathf.Max(1, entry.amount);
                    return;
                }
            }

            saveData.currencyItemStacks.Add(new CurrencyItemStack
            {
                currencyItemDefinitionId = entry.currencyItemDefinitionId,
                amount = Mathf.Max(1, entry.amount)
            });
        }

        private static string ToRomanNumeral(int value)
        {
            if (value <= 0)
            {
                return value.ToString();
            }

            var numerals = new (int value, string numeral)[]
            {
                (1000, "M"),
                (900, "CM"),
                (500, "D"),
                (400, "CD"),
                (100, "C"),
                (90, "XC"),
                (50, "L"),
                (40, "XL"),
                (10, "X"),
                (9, "IX"),
                (5, "V"),
                (4, "IV"),
                (1, "I")
            };

            var remainder = value;
            var result = string.Empty;
            for (var i = 0; i < numerals.Length; i++)
            {
                while (remainder >= numerals[i].value)
                {
                    result += numerals[i].numeral;
                    remainder -= numerals[i].value;
                }
            }

            return result;
        }

        private void EnsureSaveDefaults(CampaignSaveData data)
        {
            data.currencyItemStacks ??= new List<CurrencyItemStack>();
            data.ownedMapItems ??= new List<OwnedMapItem>();
            data.ownedUnitCards ??= new List<OwnedUnitCard>();
            data.ownedUnitItems ??= new List<OwnedUnitItem>();
            data.hexBoardState ??= new List<HexSlotSaveData>();
            for (var i = 0; i < data.ownedUnitCards.Count; i++)
            {
                if (data.ownedUnitCards[i] != null)
                {
                    data.ownedUnitCards[i].equippedItemIds ??= new List<string>();
                }
            }
            for (var i = 0; i < data.ownedUnitItems.Count; i++)
            {
                var item = data.ownedUnitItems[i];
                if (item == null)
                {
                    continue;
                }

                item.appliedModifiers ??= new List<AppliedItemModifierData>();
                NormalizeAppliedModifiers(item);
                MigrateLegacyItemUpgrades(item);
            }
            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                if (GetSlotState(data, definition.SlotId) == null)
                {
                    data.hexBoardState.Add(new HexSlotSaveData
                    {
                        hexSlotId = definition.SlotId,
                        state = definition.InitiallyOpen ? CampaignHexState.Open : CampaignHexState.Locked
                    });
                }
            }
        }

        public void Save()
        {
            Save(saveData);
        }

        private void Save(CampaignSaveData data)
        {
            var path = GetSavePath();
            var json = JsonUtility.ToJson(data, true);
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", true);
            }

            File.WriteAllText(path, json);
        }

        private string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        private HexSlotSaveData GetOrCreateSlotState(string hexSlotId)
        {
            var slot = GetSlotState(saveData, hexSlotId);
            if (slot != null)
            {
                return slot;
            }

            slot = new HexSlotSaveData
            {
                hexSlotId = hexSlotId,
                state = CampaignHexState.Locked
            };
            saveData.hexBoardState.Add(slot);
            return slot;
        }

        private static HexSlotSaveData GetSlotState(CampaignSaveData data, string hexSlotId)
        {
            if (data == null || data.hexBoardState == null)
            {
                return null;
            }

            for (var i = 0; i < data.hexBoardState.Count; i++)
            {
                var slot = data.hexBoardState[i];
                if (slot != null && string.Equals(slot.hexSlotId, hexSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            return null;
        }

        private OwnedMapItem FindMapItem(string mapItemId)
        {
            for (var i = 0; i < saveData.ownedMapItems.Count; i++)
            {
                var item = saveData.ownedMapItems[i];
                if (item != null && string.Equals(item.mapItemId, mapItemId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private OwnedUnitCard FindUnitCard(string unitCardId)
        {
            for (var i = 0; i < saveData.ownedUnitCards.Count; i++)
            {
                var card = saveData.ownedUnitCards[i];
                if (card != null && string.Equals(card.unitCardId, unitCardId, StringComparison.OrdinalIgnoreCase))
                {
                    return card;
                }
            }

            return null;
        }

        private OwnedUnitItem FindOwnedUnitItem(string itemInstanceId)
        {
            for (var i = 0; i < saveData.ownedUnitItems.Count; i++)
            {
                var item = saveData.ownedUnitItems[i];
                if (item != null && string.Equals(item.itemInstanceId, itemInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private void AppendAppliedModifierEffects(List<ItemEffectDefinition> destination, OwnedUnitItem ownedItem, ref int damageBonusMin, ref int damageBonusMax)
        {
            if (destination == null || ownedItem?.appliedModifiers == null || lootCatalogs == null)
            {
                return;
            }

            for (var i = 0; i < ownedItem.appliedModifiers.Count; i++)
            {
                var appliedModifier = ownedItem.appliedModifiers[i];
                if (appliedModifier == null
                    || string.IsNullOrWhiteSpace(appliedModifier.modifierTemplateId))
                {
                    continue;
                }

                AppendAppliedModifierEffect(destination, appliedModifier, ref damageBonusMin, ref damageBonusMax);
            }
        }

        private static void AppendAppliedModifierEffect(List<ItemEffectDefinition> destination, AppliedItemModifierData appliedModifier, ref int damageBonusMin, ref int damageBonusMax)
        {
            switch (appliedModifier.modifierType)
            {
                case ModifierType.MaxHealth:
                    destination.Add(CreateSingleValueEffect("maxHealth", appliedModifier.rolledValueA));
                    break;
                case ModifierType.Armor:
                    destination.Add(CreateSingleValueEffect("armor", appliedModifier.rolledValueA));
                    break;
                case ModifierType.VisionRange:
                    destination.Add(CreateSingleValueEffect("visionRange", appliedModifier.rolledValueA));
                    break;
                case ModifierType.Speed:
                    destination.Add(CreateSingleValueEffect("speed", appliedModifier.rolledValueA));
                    break;
                case ModifierType.Accuracy:
                    destination.Add(CreateSingleValueEffect("accuracy", appliedModifier.rolledValueA));
                    break;
                case ModifierType.FireReliability:
                    destination.Add(CreateSingleValueEffect("fireReliability", appliedModifier.rolledValueA));
                    break;
                case ModifierType.MoveReliability:
                    destination.Add(CreateSingleValueEffect("moveReliability", appliedModifier.rolledValueA));
                    break;
                case ModifierType.Damage:
                    damageBonusMin += appliedModifier.rolledValueA;
                    damageBonusMax += Mathf.Max(appliedModifier.rolledValueA, appliedModifier.rolledValueB);
                    break;
            }
        }

        private static ItemEffectDefinition CreateSingleValueEffect(string statKey, int value)
        {
            return new ItemEffectDefinition
            {
                statKey = statKey,
                operation = ItemEffectOperation.Add,
                value = value
            };
        }

        private static void AppendItemEffects(List<ItemEffectDefinition> destination, List<ItemEffectDefinition> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var effect = source[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.statKey))
                {
                    continue;
                }

                destination.Add(new ItemEffectDefinition
                {
                    statKey = effect.statKey,
                    operation = effect.operation,
                    value = effect.value
                });
            }
        }

        private List<ModifierTemplateDefinition> BuildModifierTemplatePool(OwnedUnitItem ownedItem, ItemDefinition itemDefinition)
        {
            var results = new List<ModifierTemplateDefinition>();
            if (itemDefinition == null || lootCatalogs?.ModifierTemplates == null)
            {
                return results;
            }

            foreach (var pair in lootCatalogs.ModifierTemplates)
            {
                var template = pair.Value;
                if (template == null
                    || !ModifierTemplateMatchesItemType(template, itemDefinition.itemType)
                    || template.tier != itemDefinition.tier
                    || HasModifier(ownedItem, template.modifierTemplateId))
                {
                    continue;
                }

                results.Add(template);
            }

            return results;
        }

        private List<MapModifierTemplateDefinition> BuildMapModifierTemplatePool(OwnedMapItem mapItem, MapDefinition mapDefinition)
        {
            var results = new List<MapModifierTemplateDefinition>();
            if (mapDefinition == null || catalogs?.MapModifierTemplates == null)
            {
                return results;
            }

            foreach (var pair in catalogs.MapModifierTemplates)
            {
                var template = pair.Value;
                if (template == null
                    || template.tier != mapDefinition.tier
                    || HasMapModifier(mapItem, template.mapModifierTemplateId))
                {
                    continue;
                }

                results.Add(template);
            }

            return results;
        }

        private static bool ModifierTemplateMatchesItemType(ModifierTemplateDefinition template, string itemType)
        {
            if (template == null || string.IsNullOrWhiteSpace(itemType))
            {
                return false;
            }

            if (template.itemTypes == null || template.itemTypes.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < template.itemTypes.Count; i++)
            {
                if (string.Equals(template.itemTypes[i], itemType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIgnoreCase(List<string> values, string candidate)
        {
            if (values == null || string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ModifierTemplateDefinition SelectWeightedModifierTemplate(List<ModifierTemplateDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(1, candidates[i].weight);
            }

            if (totalWeight <= 0)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            var roll = UnityEngine.Random.Range(0, totalWeight);
            var runningWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                runningWeight += Mathf.Max(1, candidates[i].weight);
                if (roll < runningWeight)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static MapModifierTemplateDefinition SelectWeightedMapModifierTemplate(List<MapModifierTemplateDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(1, candidates[i].weight);
            }

            if (totalWeight <= 0)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            var roll = UnityEngine.Random.Range(0, totalWeight);
            var runningWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                runningWeight += Mathf.Max(1, candidates[i].weight);
                if (roll < runningWeight)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static bool HasModifier(OwnedUnitItem ownedItem, string modifierTemplateId)
        {
            if (ownedItem?.appliedModifiers == null)
            {
                return false;
            }

            for (var i = 0; i < ownedItem.appliedModifiers.Count; i++)
            {
                var appliedModifier = ownedItem.appliedModifiers[i];
                if (appliedModifier != null
                    && string.Equals(appliedModifier.modifierTemplateId, modifierTemplateId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMapModifier(OwnedMapItem mapItem, string modifierTemplateId)
        {
            if (mapItem?.appliedMapModifiers == null)
            {
                return false;
            }

            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                var appliedModifier = mapItem.appliedMapModifiers[i];
                if (appliedModifier != null
                    && string.Equals(appliedModifier.mapModifierTemplateId, modifierTemplateId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static AppliedItemModifierData InstantiateModifier(ModifierTemplateDefinition template, string sourceCurrencyItemDefinitionId)
        {
            var appliedModifier = new AppliedItemModifierData
            {
                modifierTemplateId = template.modifierTemplateId,
                modifierType = template.modifierType,
                rolledValueA = RollInclusive(template.rollAMin, template.rollAMax),
                rolledValueB = RollInclusive(template.rollBMin, template.rollBMax),
                sourceCurrencyItemDefinitionId = sourceCurrencyItemDefinitionId
            };

            if (appliedModifier.modifierType == ModifierType.Damage)
            {
                appliedModifier.rolledValueB = Mathf.Max(appliedModifier.rolledValueA, appliedModifier.rolledValueB);
            }
            else
            {
                appliedModifier.rolledValueB = 0;
            }

            return appliedModifier;
        }

        private static int RollInclusive(int minValue, int maxValue)
        {
            if (maxValue < minValue)
            {
                (minValue, maxValue) = (maxValue, minValue);
            }

            return UnityEngine.Random.Range(minValue, maxValue + 1);
        }

        private void MigrateLegacyItemUpgrades(OwnedUnitItem item)
        {
            if (item == null || item.legacyUpgradeLevel <= 0 || lootCatalogs == null)
            {
                return;
            }

            var legacyModifierId = GetLegacyModifierDefinitionId(item.itemDefinitionId);
            if (string.IsNullOrWhiteSpace(legacyModifierId) || !lootCatalogs.TryGetModifierTemplate(legacyModifierId, out var template) || template == null)
            {
                item.legacyUpgradeLevel = 0;
                return;
            }

            item.appliedModifiers ??= new List<AppliedItemModifierData>();
            for (var i = 0; i < item.legacyUpgradeLevel; i++)
            {
                item.appliedModifiers.Add(InstantiateModifier(template, "legacy_migration"));
            }

            item.legacyUpgradeLevel = 0;
        }

        private static string GetLegacyModifierDefinitionId(string itemDefinitionId)
        {
            return string.Equals(itemDefinitionId, "field_plating", StringComparison.OrdinalIgnoreCase)
                ? "legacy_field_plating_armor_boost"
                : string.Empty;
        }

        private void NormalizeAppliedModifiers(OwnedUnitItem item)
        {
            if (item?.appliedModifiers == null || lootCatalogs == null)
            {
                return;
            }

            for (var i = 0; i < item.appliedModifiers.Count; i++)
            {
                var appliedModifier = item.appliedModifiers[i];
                if (appliedModifier == null || string.IsNullOrWhiteSpace(appliedModifier.modifierTemplateId))
                {
                    continue;
                }

                if (string.Equals(appliedModifier.modifierTemplateId, "reinforced_lining", StringComparison.OrdinalIgnoreCase))
                {
                    appliedModifier.modifierTemplateId = "utility_t1_health";
                    appliedModifier.modifierType = ModifierType.MaxHealth;
                    appliedModifier.rolledValueA = appliedModifier.rolledValueA == 0 ? 2 : appliedModifier.rolledValueA;
                    appliedModifier.rolledValueB = 0;
                    continue;
                }

                if (string.Equals(appliedModifier.modifierTemplateId, "legacy_field_plating_armor_boost", StringComparison.OrdinalIgnoreCase))
                {
                    appliedModifier.modifierType = ModifierType.Armor;
                    appliedModifier.rolledValueA = appliedModifier.rolledValueA == 0 ? 1 : appliedModifier.rolledValueA;
                    appliedModifier.rolledValueB = 0;
                    continue;
                }

                if (!lootCatalogs.TryGetModifierTemplate(appliedModifier.modifierTemplateId, out var template) || template == null)
                {
                    continue;
                }

                appliedModifier.modifierType = template.modifierType;
                if (appliedModifier.modifierType == ModifierType.Damage)
                {
                    if (appliedModifier.rolledValueA == 0 && appliedModifier.rolledValueB == 0)
                    {
                        appliedModifier.rolledValueA = template.rollAMin;
                        appliedModifier.rolledValueB = Mathf.Max(template.rollAMin, template.rollBMin);
                    }
                    else
                    {
                        appliedModifier.rolledValueB = Mathf.Max(appliedModifier.rolledValueA, appliedModifier.rolledValueB);
                    }
                }
                else if (appliedModifier.rolledValueA == 0)
                {
                    appliedModifier.rolledValueA = template.rollAMin;
                    appliedModifier.rolledValueB = 0;
                }
            }
        }

        private bool CanEquipItemToCard(OwnedUnitItem item, OwnedUnitCard card, out string error)
        {
            error = string.Empty;
            if (item == null || card == null)
            {
                error = "Invalid item or unit card.";
                return false;
            }

            if (!catalogs.TryGetUnitCardDefinition(card.definitionId, out var cardDefinition) || cardDefinition == null)
            {
                error = "Unable to resolve the selected unit card definition.";
                return false;
            }

            if (lootCatalogs == null || !lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var itemDefinition) || itemDefinition == null)
            {
                error = "Unable to resolve the selected item definition.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemDefinition.itemSlotType))
            {
                error = "That item has no valid slot type.";
                return false;
            }

            var totalSlots = 0;
            for (var i = 0; i < cardDefinition.defaultItemSlots.Count; i++)
            {
                if (string.Equals(cardDefinition.defaultItemSlots[i], itemDefinition.itemSlotType, StringComparison.OrdinalIgnoreCase))
                {
                    totalSlots++;
                }
            }

            if (totalSlots <= 0)
            {
                error = "That unit has no " + itemDefinition.itemSlotType + " slot.";
                return false;
            }

            var usedSlots = 0;
            for (var i = 0; i < card.equippedItemIds.Count; i++)
            {
                var equippedItem = FindOwnedUnitItem(card.equippedItemIds[i]);
                if (equippedItem == null)
                {
                    continue;
                }

                if (lootCatalogs.TryGetItemDefinition(equippedItem.itemDefinitionId, out var equippedDefinition)
                    && equippedDefinition != null
                    && string.Equals(equippedDefinition.itemSlotType, itemDefinition.itemSlotType, StringComparison.OrdinalIgnoreCase))
                {
                    usedSlots++;
                }
            }

            if (usedSlots >= totalSlots)
            {
                error = "No free " + itemDefinition.itemSlotType + " slot is available.";
                return false;
            }

            return true;
        }

        private bool TrySpendCurrency(string currencyItemDefinitionId, int amount)
        {
            if (string.IsNullOrWhiteSpace(currencyItemDefinitionId) || amount <= 0)
            {
                return true;
            }

            for (var i = 0; i < saveData.currencyItemStacks.Count; i++)
            {
                var stack = saveData.currencyItemStacks[i];
                if (stack == null || !string.Equals(stack.currencyItemDefinitionId, currencyItemDefinitionId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (stack.amount < amount)
                {
                    return false;
                }

                stack.amount -= amount;
                if (stack.amount <= 0)
                {
                    saveData.currencyItemStacks.RemoveAt(i);
                }

                return true;
            }

            return false;
        }

        private int GetFieldingGoldCost(OwnedUnitCard card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.baseTemplateId))
            {
                return 0;
            }

            var gameCatalog = GameDataCatalogLoader.Load();
            if (!gameCatalog.TryGetUnitTemplate(card.baseTemplateId, out var template) || template == null)
            {
                return 0;
            }

            return template.UnitType switch
            {
                UnitType.Tank => 50,
                UnitType.Infantry => 25,
                _ => 25
            };
        }

        private void RefundDeploymentGold(OwnedUnitCard card)
        {
            if (card == null || card.deploymentGoldCostPaid <= 0)
            {
                return;
            }

            saveData.gold += card.deploymentGoldCostPaid;
            card.deploymentGoldCostPaid = 0;
        }

        private void RemoveEquippedItemsForCard(string unitCardId)
        {
            if (string.IsNullOrWhiteSpace(unitCardId))
            {
                return;
            }

            for (var i = saveData.ownedUnitItems.Count - 1; i >= 0; i--)
            {
                var item = saveData.ownedUnitItems[i];
                if (item != null && string.Equals(item.equippedToUnitCardId, unitCardId, StringComparison.OrdinalIgnoreCase))
                {
                    saveData.ownedUnitItems.RemoveAt(i);
                }
            }

            var card = FindUnitCard(unitCardId);
            card?.equippedItemIds?.Clear();
        }
    }

    internal static class SceneLoadUtility
    {
        public static bool CanLoadScene(string sceneName, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                error = "No scene name was configured for the selected map.";
                return false;
            }

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return true;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(FindScenePath(sceneName)))
            {
                return true;
            }
#endif

            error = "Scene '" + sceneName + "' could not be loaded. Add it to Build Settings or keep testing in the editor.";
            return false;
        }

        public static void LoadScene(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

#if UNITY_EDITOR
            var scenePath = FindScenePath(sceneName);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif

            Debug.LogError("Scene '" + sceneName + "' could not be loaded.");
        }

#if UNITY_EDITOR
        private static string FindScenePath(string sceneName)
        {
            var guids = AssetDatabase.FindAssets(sceneName + " t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return string.Empty;
        }
#endif
    }
}
