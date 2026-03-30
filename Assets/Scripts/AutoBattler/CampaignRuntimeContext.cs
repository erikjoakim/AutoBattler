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
        private CampaignSaveData saveData;
        private PreparedMissionData activeMission;
        private BattleResultData pendingBattleResult;
        private bool awaitingMissionSceneLoad;
        private bool headQuarterStartupApplied;

        public CampaignCatalogs Catalogs => catalogs;
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
                card.status = UnitCardStatus.Available;
                card.assignedHexSlotId = string.Empty;
            }

            Save();
            return true;
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

                card.assignedHexSlotId = string.Empty;
                card.timesDeployed++;
                if (deadCards.Contains(card.unitCardId))
                {
                    card.status = UnitCardStatus.Dead;
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

                var unitSpawn = UnitSpawnConfig.FromTemplate(catalog, template.UnitTypeKey, string.IsNullOrWhiteSpace(ownedCard.displayName) ? template.UnitName : ownedCard.displayName, 1);
                unitSpawn.ownedUnitCardId = ownedCard.unitCardId;
                result.Add(unitSpawn);
            }

            return result;
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
                        instanceName = prefix + " " + ToRomanNumeral(count + 1)
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
                    instanceName = instanceName
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
