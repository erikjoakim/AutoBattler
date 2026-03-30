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
            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                created.hexBoardState.Add(new HexSlotSaveData
                {
                    hexSlotId = definition.SlotId,
                    state = definition.InitiallyOpen ? CampaignHexState.Open : CampaignHexState.Locked
                });
            }

            created.ownedMapItems.Add(new OwnedMapItem
            {
                mapItemId = "map_item_001",
                mapDefinitionId = "sample_operation",
                instanceName = "Sample Operation I"
            });
            created.ownedMapItems.Add(new OwnedMapItem
            {
                mapItemId = "map_item_002",
                mapDefinitionId = "sample_operation",
                instanceName = "Sample Operation II"
            });
            created.ownedMapItems.Add(new OwnedMapItem
            {
                mapItemId = "map_item_003",
                mapDefinitionId = "sample_operation",
                instanceName = "Sample Operation III"
            });

            created.ownedUnitCards.Add(new OwnedUnitCard
            {
                unitCardId = "unit_card_001",
                definitionId = "guard_infantry_card",
                displayName = "Rook-1",
                baseTemplateId = "Guard Infantry",
                status = UnitCardStatus.Available
            });

            return created;
        }

        private void EnsureSaveDefaults(CampaignSaveData data)
        {
            data.ownedMapItems ??= new List<OwnedMapItem>();
            data.ownedUnitCards ??= new List<OwnedUnitCard>();
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
