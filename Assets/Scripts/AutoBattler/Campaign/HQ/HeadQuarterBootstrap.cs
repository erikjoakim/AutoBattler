using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AutoBattler
{
    public sealed class HeadQuarterBootstrap : MonoBehaviour
    {
        private const string HeadQuarterSceneName = "HeadQuarter";
        private const string LayoutResourcePath = "HeadQuarter/HeadQuarterLayout";
        private const string StyleResourcePath = "HeadQuarter/HeadQuarterStyle";
        private const string ThemeResourcePath = "HeadQuarter/UnityDefaultRuntimeTheme";

        private readonly Dictionary<string, VisualElement> hexElements = new Dictionary<string, VisualElement>(StringComparer.OrdinalIgnoreCase);

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement inventoryList;
        private VisualElement availableCardsList;
        private VisualElement selectedCardsList;
        private VisualElement boardContainer;
        private Label topBarLabel;
        private Label detailsLabel;
        private Label statusLabel;
        private Button openMapButton;
        private Button clearHexButton;
        private VisualElement resultOverlay;
        private Label resultLabel;
        private string selectedHexSlotId;
        private string draggedMapItemId;
        private string draggedItemInstanceId;
        private VisualElement draggedMapGhost;
        private LootCatalogs lootCatalogs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, HeadQuarterSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindAnyObjectByType<HeadQuarterBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("HeadQuarterBootstrap");
            bootstrapObject.AddComponent<HeadQuarterBootstrap>();
        }

        private void Start()
        {
            EnsureCamera();
            lootCatalogs = LootCatalogLoader.Load();
            EnsureUiDocument();
            BindUi();
            BuildHexBoard();
            SelectInitialHex();
            RefreshAll();
        }

        private void EnsureUiDocument()
        {
            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            var style = Resources.Load<StyleSheet>(StyleResourcePath);
            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException("Missing UI Toolkit layout: " + LayoutResourcePath);
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ConfigurePanelSettings(panelSettings);
            if (theme != null)
            {
                panelSettings.themeStyleSheet = theme;
            }
            uiDocument = gameObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;

            root = uiDocument.rootVisualElement;
            root.style.flexGrow = 1f;
            layout.CloneTree(root);
            if (style != null)
            {
                root.styleSheets.Add(style);
            }

            root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);
        }

        private static void ConfigurePanelSettings(PanelSettings panelSettings)
        {
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.match = 0.5f;

#if UNITY_EDITOR
            if (panelSettings.themeStyleSheet != null)
            {
                return;
            }

            var themeType = Type.GetType("UnityEngine.UIElements.ThemeStyleSheet, UnityEngine.UIElementsModule")
                ?? Type.GetType("UnityEngine.UIElements.ThemeStyleSheet, UnityEngine");
            if (themeType == null)
            {
                return;
            }

            var themeProperty = typeof(PanelSettings).GetProperty("themeStyleSheet");
            if (themeProperty == null)
            {
                return;
            }

            var themeGuidMatches = AssetDatabase.FindAssets("UnityDefaultRuntimeTheme");
            for (var i = 0; i < themeGuidMatches.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(themeGuidMatches[i]);
                if (!path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var themeAsset = AssetDatabase.LoadAssetAtPath(path, themeType);
                if (themeAsset != null)
                {
                    themeProperty.SetValue(panelSettings, themeAsset);
                    break;
                }
            }
#endif
        }

        private void BindUi()
        {
            inventoryList = Require("MapInventoryList");
            availableCardsList = Require("AvailableCardsList");
            selectedCardsList = Require("SelectedCardsList");
            boardContainer = Require("BoardContainer");
            topBarLabel = RequireLabel("TopBarLabel");
            detailsLabel = RequireLabel("MapDetails");
            statusLabel = RequireLabel("StatusLabel");
            openMapButton = RequireButton("OpenMapButton");
            clearHexButton = RequireButton("ClearHexButton");
            resultOverlay = Require("ResultOverlay");
            resultLabel = RequireLabel("ResultLabel");

            openMapButton.clicked += TryOpenSelectedHex;
            clearHexButton.clicked += TryClearSelectedHex;
            RequireButton("ResultConfirmButton").clicked += ConfirmResult;
            inventoryList.RegisterCallback<PointerUpEvent>(OnInventoryPointerUp);
        }

        private void BuildHexBoard()
        {
            boardContainer.Clear();
            hexElements.Clear();

            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                var element = new VisualElement();
                element.name = definition.SlotId;
                element.AddToClassList("hex-slot");
                var position = CampaignBoardLayout.GetUiPosition(definition);
                element.style.left = position.x + 300f;
                element.style.top = position.y + 210f;

                var title = new Label(definition.SlotId.ToUpperInvariant());
                title.AddToClassList("hex-title");
                element.Add(title);

                var state = new Label();
                state.name = "StateLabel";
                state.AddToClassList("hex-state");
                element.Add(state);

                element.RegisterCallback<ClickEvent>(_ => OnHexClicked(definition.SlotId));
                element.RegisterCallback<PointerUpEvent>(_ => TryDropDraggedMapToSlot(definition.SlotId));

                boardContainer.Add(element);
                hexElements[definition.SlotId] = element;
            }
        }

        private void RefreshAll()
        {
            if (CampaignRuntimeContext.Instance == null)
            {
                return;
            }

            PopulateMapInventory();
            PopulateCards();
            RefreshBoard();
            RefreshDetails();
            RefreshTopBar();
            RefreshButtons();
            RefreshResultOverlay();
        }

        private void PopulateMapInventory()
        {
            inventoryList.Clear();
            var maps = CampaignRuntimeContext.Instance.GetAvailableMapItems();
            var currencyStacks = CampaignRuntimeContext.Instance.GetCurrencyItemStacks();
            var ownedItems = CampaignRuntimeContext.Instance.GetOwnedUnitItems();
            for (var i = 0; i < maps.Count; i++)
            {
                var mapItem = maps[i];
                CampaignRuntimeContext.Instance.Catalogs.TryGetMapDefinition(mapItem.mapDefinitionId, out var definition);
                var card = BuildInventoryCard(
                    definition?.displayName ?? mapItem.instanceName,
                    "Map  T" + (definition?.tier ?? 1),
                    "inventory-card-map");
                RegisterMapDrag(card, mapItem.mapItemId, definition?.displayName ?? mapItem.instanceName);
                inventoryList.Add(card);
            }

            for (var i = 0; i < currencyStacks.Count; i++)
            {
                var stack = currencyStacks[i];
                if (stack == null || stack.amount <= 0)
                {
                    continue;
                }

                var displayName = stack.currencyItemDefinitionId;
                if (lootCatalogs != null && lootCatalogs.TryGetCurrencyItemDefinition(stack.currencyItemDefinitionId, out var definition))
                {
                    displayName = definition.displayName;
                }

                inventoryList.Add(BuildInventoryCard(displayName, "Currency  x" + stack.amount, "inventory-card-currency"));
            }

            for (var i = 0; i < ownedItems.Count; i++)
            {
                var item = ownedItems[i];
                if (item == null || !string.IsNullOrWhiteSpace(item.equippedToUnitCardId))
                {
                    continue;
                }

                var displayName = item.itemDefinitionId;
                var subtitle = "Item";
                if (lootCatalogs != null && lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var definition))
                {
                    displayName = definition.displayName;
                    subtitle = BuildOwnedItemSubtitle(item, definition);
                }

                var card = BuildOwnedInventoryItemCard(item, displayName, subtitle);
                RegisterItemDrag(card, item, displayName);
                inventoryList.Add(card);
            }

            if (inventoryList.childCount == 0)
            {
                inventoryList.Add(BuildPlaceholder("Inventory is empty."));
            }
        }

        private static VisualElement BuildInventoryCard(string titleText, string subtitleText, string typeClass)
        {
            var card = new VisualElement();
            card.AddToClassList("inventory-card");
            if (!string.IsNullOrWhiteSpace(typeClass))
            {
                card.AddToClassList(typeClass);
            }

            var title = new Label(titleText);
            title.AddToClassList("inventory-card-title");
            var subtitle = new Label(subtitleText);
            subtitle.AddToClassList("inventory-card-subtitle");

            card.Add(title);
            card.Add(subtitle);
            return card;
        }

        private VisualElement BuildOwnedInventoryItemCard(OwnedUnitItem item, string titleText, string subtitleText)
        {
            var card = BuildInventoryCard(titleText, subtitleText, "inventory-card-item");
            var modifyButton = new Button(() => TryModifyItem(item.itemInstanceId))
            {
                text = "Mod"
            };
            modifyButton.AddToClassList("inventory-item-button");
            modifyButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            card.Add(modifyButton);
            return card;
        }

        private void PopulateCards()
        {
            availableCardsList.Clear();
            selectedCardsList.Clear();

            var available = CampaignRuntimeContext.Instance.GetAvailableUnitCards(selectedHexSlotId);
            var selected = CampaignRuntimeContext.Instance.GetCardsAssignedToSlot(selectedHexSlotId);

            for (var i = 0; i < available.Count; i++)
            {
                var card = available[i];
                if (string.Equals(card.assignedHexSlotId, selectedHexSlotId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                availableCardsList.Add(BuildUnitCard(card, false));
            }

            for (var i = 0; i < selected.Count; i++)
            {
                selectedCardsList.Add(BuildUnitCard(selected[i], true));
            }

            if (availableCardsList.childCount == 0)
            {
                availableCardsList.Add(BuildPlaceholder("No available troop cards."));
            }

            if (selectedCardsList.childCount == 0)
            {
                selectedCardsList.Add(BuildPlaceholder("Select an occupied hex and add troop cards."));
            }
        }

        private VisualElement BuildUnitCard(OwnedUnitCard card, bool assigned)
        {
            var rootElement = new VisualElement();
            rootElement.AddToClassList("unit-card");
            if (assigned)
            {
                rootElement.AddToClassList("unit-card-assigned");
            }

            var headerRow = new VisualElement();
            headerRow.AddToClassList("unit-card-header");

            var textBlock = new VisualElement();
            textBlock.AddToClassList("unit-card-text");
            textBlock.Add(new Label(card.displayName) { name = "UnitName" });
            textBlock.Add(new Label(card.baseTemplateId) { name = "UnitClass" });
            headerRow.Add(textBlock);

            var action = new Button(() =>
            {
                if (assigned)
                {
                    CampaignRuntimeContext.Instance.TryUnassignUnitCardFromHex(card.unitCardId, selectedHexSlotId, out var error);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        SetStatus(error);
                    }
                }
                else
                {
                    CampaignRuntimeContext.Instance.TryAssignUnitCardToHex(card.unitCardId, selectedHexSlotId, out var error);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        SetStatus(error);
                    }
                }

                RefreshAll();
            })
            {
                text = assigned ? "Remove" : "Select"
            };
            action.AddToClassList("small-action-button");
            headerRow.Add(action);
            rootElement.Add(headerRow);

            var equippedItems = CampaignRuntimeContext.Instance.GetEquippedUnitItems(card.unitCardId);
            var itemsRow = new VisualElement();
            itemsRow.AddToClassList("equipped-items-row");
            if (equippedItems.Count > 0)
            {
                for (var i = 0; i < equippedItems.Count; i++)
                {
                    itemsRow.Add(BuildEquippedItemChip(equippedItems[i]));
                }
            }
            else
            {
                var emptyLabel = new Label("Drop item here");
                emptyLabel.AddToClassList("equipped-item-empty");
                itemsRow.Add(emptyLabel);
            }

            rootElement.Add(itemsRow);
            rootElement.RegisterCallback<PointerUpEvent>(_ => TryDropDraggedItemToUnitCard(card.unitCardId));
            return rootElement;
        }

        private void RefreshBoard()
        {
            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                if (!hexElements.TryGetValue(definition.SlotId, out var element))
                {
                    continue;
                }

                element.RemoveFromClassList("hex-open");
                element.RemoveFromClassList("hex-occupied");
                element.RemoveFromClassList("hex-completed");
                element.RemoveFromClassList("hex-locked");
                element.RemoveFromClassList("hex-selected");

                var slot = CampaignRuntimeContext.Instance.GetHexSlotState(definition.SlotId);
                var stateLabel = element.Q<Label>("StateLabel");
                switch (slot.state)
                {
                    case CampaignHexState.Open:
                        element.AddToClassList("hex-open");
                        stateLabel.text = "OPEN";
                        break;
                    case CampaignHexState.Occupied:
                        element.AddToClassList("hex-occupied");
                        stateLabel.text = ResolveMapLabel(slot.occupiedMapItemId);
                        break;
                    case CampaignHexState.Completed:
                        element.AddToClassList("hex-completed");
                        stateLabel.text = "COMPLETED";
                        break;
                    default:
                        element.AddToClassList("hex-locked");
                        stateLabel.text = "LOCKED";
                        break;
                }

                if (string.Equals(selectedHexSlotId, definition.SlotId, StringComparison.OrdinalIgnoreCase))
                {
                    element.AddToClassList("hex-selected");
                }
            }
        }

        private void RefreshDetails()
        {
            var slot = CampaignRuntimeContext.Instance.GetHexSlotState(selectedHexSlotId);
            var lines = new List<string>
            {
                "Hex: " + selectedHexSlotId,
                "State: " + slot.state
            };

            if (!string.IsNullOrWhiteSpace(slot.occupiedMapItemId))
            {
                var mapItem = FindMapById(slot.occupiedMapItemId);
                if (mapItem != null && CampaignRuntimeContext.Instance.Catalogs.TryGetMapDefinition(mapItem.mapDefinitionId, out var mapDefinition))
                {
                    lines.Add(string.Empty);
                    lines.Add("Map: " + mapDefinition.displayName);
                    lines.Add("Scene: " + mapDefinition.sceneName);
                    lines.Add("Tier: " + mapDefinition.tier);
                    lines.Add(mapDefinition.description);
                }
            }
            else if (slot.state == CampaignHexState.Open)
            {
                lines.Add(string.Empty);
                lines.Add("Drag a map from the left inventory into this open hex.");
            }
            else if (slot.state == CampaignHexState.Locked)
            {
                lines.Add(string.Empty);
                lines.Add("Win adjacent operations to unlock this hex.");
            }
            else if (slot.state == CampaignHexState.Completed)
            {
                lines.Add(string.Empty);
                lines.Add("This hex is completed. Victory opened the surrounding operations.");
            }

            var selectedCards = CampaignRuntimeContext.Instance.GetCardsAssignedToSlot(selectedHexSlotId);
            lines.Add(string.Empty);
            lines.Add("Selected Troop Cards: " + selectedCards.Count);
            detailsLabel.text = string.Join("\n", lines);
        }

        private void RefreshTopBar()
        {
            var save = CampaignRuntimeContext.Instance.SaveData;
            var scrapParts = CampaignRuntimeContext.Instance.GetCurrencyAmount("scrap_parts");
            topBarLabel.text = "HEADQUARTER    XP " + save.playerExperience + "    GOLD " + save.gold + "    SCRAP " + scrapParts + "    MAPS " + save.ownedMapItems.Count + "    CARDS " + CountLivingCards();
        }

        private void RefreshButtons()
        {
            var slot = CampaignRuntimeContext.Instance.GetHexSlotState(selectedHexSlotId);
            var hasPendingResult = CampaignRuntimeContext.Instance.PendingBattleResult != null;
            openMapButton.SetEnabled(!hasPendingResult && slot.state == CampaignHexState.Occupied);
            clearHexButton.SetEnabled(!hasPendingResult && slot.state == CampaignHexState.Occupied);
        }

        private void RefreshResultOverlay()
        {
            var result = CampaignRuntimeContext.Instance.PendingBattleResult;
            resultOverlay.style.display = result == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (result == null)
            {
                return;
            }

            var lines = new List<string>
            {
                result.victory ? "OPERATION SUCCESSFUL" : "OPERATION FAILED",
                string.Empty,
                result.resultMessage
            };

            if (result.survivingUnitCardIds.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Survived:");
                AppendCardNames(lines, result.survivingUnitCardIds);
            }

            if (result.deadUnitCardIds.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Lost:");
                AppendCardNames(lines, result.deadUnitCardIds);
            }

            if (result.claimedLoot != null && result.claimedLoot.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Loot secured.");
            }
            else if (result.lostLoot != null && result.lostLoot.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Dropped loot was lost.");
            }

            resultLabel.text = string.Join("\n", lines);
        }

        private void RegisterMapDrag(VisualElement element, string mapItemId, string label)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                draggedItemInstanceId = string.Empty;
                draggedMapItemId = mapItemId;
                EnsureDragGhost();
                draggedMapGhost.Q<Label>("DragLabel").text = label;
                draggedMapGhost.style.display = DisplayStyle.Flex;
                UpdateDragGhost(evt.position);
                evt.StopPropagation();
            });
        }

        private void RegisterItemDrag(VisualElement element, OwnedUnitItem item, string label)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                draggedMapItemId = string.Empty;
                draggedItemInstanceId = item.itemInstanceId;
                EnsureDragGhost();
                draggedMapGhost.Q<Label>("DragLabel").text = label;
                draggedMapGhost.style.display = DisplayStyle.Flex;
                UpdateDragGhost(evt.position);
                evt.StopPropagation();
            });
        }

        private void OnRootPointerMove(PointerMoveEvent evt)
        {
            if ((string.IsNullOrWhiteSpace(draggedMapItemId) && string.IsNullOrWhiteSpace(draggedItemInstanceId)) || draggedMapGhost == null)
            {
                return;
            }

            UpdateDragGhost(evt.position);
        }

        private void OnRootPointerUp(PointerUpEvent evt)
        {
            EndDrag();
        }

        private void TryDropDraggedMapToSlot(string hexSlotId)
        {
            if (string.IsNullOrWhiteSpace(draggedMapItemId))
            {
                return;
            }

            if (TryPlaceMapItem(draggedMapItemId, hexSlotId))
            {
                EndDrag();
            }
        }

        private void TryDropDraggedItemToUnitCard(string unitCardId)
        {
            if (string.IsNullOrWhiteSpace(draggedItemInstanceId))
            {
                return;
            }

            if (!CampaignRuntimeContext.Instance.TryEquipItemToUnitCard(draggedItemInstanceId, unitCardId, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus("Item equipped.");
            RefreshAll();
            EndDrag();
        }

        private void EnsureDragGhost()
        {
            if (draggedMapGhost != null)
            {
                return;
            }

            draggedMapGhost = new VisualElement();
            draggedMapGhost.name = "DraggedMapGhost";
            draggedMapGhost.AddToClassList("dragged-map-ghost");
            var label = new Label();
            label.name = "DragLabel";
            draggedMapGhost.Add(label);
            root.Add(draggedMapGhost);
        }

        private void UpdateDragGhost(Vector2 position)
        {
            draggedMapGhost.style.left = position.x + 14f;
            draggedMapGhost.style.top = position.y + 14f;
        }

        private void EndDrag()
        {
            draggedMapItemId = string.Empty;
            draggedItemInstanceId = string.Empty;
            if (draggedMapGhost != null)
            {
                draggedMapGhost.style.display = DisplayStyle.None;
            }
        }

        private void OnInventoryPointerUp(PointerUpEvent evt)
        {
            if (string.IsNullOrWhiteSpace(draggedItemInstanceId))
            {
                return;
            }

            var item = FindOwnedUnitItem(draggedItemInstanceId);
            if (item == null || string.IsNullOrWhiteSpace(item.equippedToUnitCardId))
            {
                return;
            }

            if (!CampaignRuntimeContext.Instance.TryUnequipItemFromUnitCard(item.itemInstanceId, item.equippedToUnitCardId, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus("Item removed.");
            RefreshAll();
            EndDrag();
            evt.StopPropagation();
        }

        private void OnHexClicked(string hexSlotId)
        {
            if (!string.IsNullOrWhiteSpace(selectedHexSlotId)
                && string.Equals(selectedHexSlotId, hexSlotId, StringComparison.OrdinalIgnoreCase))
            {
                TryOpenSelectedHex();
                return;
            }

            selectedHexSlotId = hexSlotId;
            ClearStatus();
            RefreshAll();
        }

        private bool TryPlaceMapItem(string mapItemId, string hexSlotId)
        {
            if (CampaignRuntimeContext.Instance == null)
            {
                SetStatus("Campaign context is not ready.");
                return false;
            }

            if (!CampaignRuntimeContext.Instance.TryPlaceMapItem(mapItemId, hexSlotId, out var error))
            {
                SetStatus(error);
                return false;
            }

            selectedHexSlotId = hexSlotId;
            SetStatus("Map placed on the selected hex.");
            RefreshAll();
            return true;
        }

        private void TryOpenSelectedHex()
        {
            if (CampaignRuntimeContext.Instance == null)
            {
                return;
            }

            if (!CampaignRuntimeContext.Instance.TryLaunchMissionFromHex(selectedHexSlotId, out var error))
            {
                SetStatus(error);
            }
        }

        private void TryClearSelectedHex()
        {
            if (!CampaignRuntimeContext.Instance.TryClearHex(selectedHexSlotId, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus("Hex cleared.");
            RefreshAll();
        }

        private void ConfirmResult()
        {
            CampaignRuntimeContext.Instance.FinalizePendingBattleResult();
            SetStatus("Battle result confirmed.");
            RefreshAll();
        }

        private void SelectInitialHex()
        {
            for (var i = 0; i < CampaignBoardLayout.DefaultSlots.Length; i++)
            {
                var definition = CampaignBoardLayout.DefaultSlots[i];
                var slot = CampaignRuntimeContext.Instance.GetHexSlotState(definition.SlotId);
                if (slot.state == CampaignHexState.Occupied || slot.state == CampaignHexState.Open)
                {
                    selectedHexSlotId = definition.SlotId;
                    return;
                }
            }

            selectedHexSlotId = CampaignBoardLayout.DefaultSlots[0].SlotId;
        }

        private int CountLivingCards()
        {
            var count = 0;
            var cards = CampaignRuntimeContext.Instance.SaveData.ownedUnitCards;
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].status != UnitCardStatus.Dead)
                {
                    count++;
                }
            }

            return count;
        }

        private void AppendCardNames(List<string> lines, List<string> cardIds)
        {
            for (var i = 0; i < cardIds.Count; i++)
            {
                var card = FindUnitCard(cardIds[i]);
                lines.Add("- " + (card != null ? card.displayName : cardIds[i]));
            }
        }

        private string ResolveMapLabel(string mapItemId)
        {
            var mapItem = FindMapById(mapItemId);
            if (mapItem != null && CampaignRuntimeContext.Instance.Catalogs.TryGetMapDefinition(mapItem.mapDefinitionId, out var definition))
            {
                return definition.displayName;
            }

            return "MAP READY";
        }

        private OwnedMapItem FindMapById(string mapItemId)
        {
            var save = CampaignRuntimeContext.Instance.SaveData;
            for (var i = 0; i < save.ownedMapItems.Count; i++)
            {
                if (string.Equals(save.ownedMapItems[i].mapItemId, mapItemId, StringComparison.OrdinalIgnoreCase))
                {
                    return save.ownedMapItems[i];
                }
            }

            if (CampaignRuntimeContext.Instance.ActiveMission != null
                && string.Equals(CampaignRuntimeContext.Instance.ActiveMission.mapItemId, mapItemId, StringComparison.OrdinalIgnoreCase))
            {
                return new OwnedMapItem
                {
                    mapItemId = CampaignRuntimeContext.Instance.ActiveMission.mapItemId,
                    mapDefinitionId = CampaignRuntimeContext.Instance.ActiveMission.mapDefinitionId,
                    instanceName = CampaignRuntimeContext.Instance.ActiveMission.mapDefinitionId
                };
            }

            return null;
        }

        private OwnedUnitCard FindUnitCard(string unitCardId)
        {
            var cards = CampaignRuntimeContext.Instance.SaveData.ownedUnitCards;
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && string.Equals(cards[i].unitCardId, unitCardId, StringComparison.OrdinalIgnoreCase))
                {
                    return cards[i];
                }
            }

            return null;
        }

        private OwnedUnitItem FindOwnedUnitItem(string itemInstanceId)
        {
            var items = CampaignRuntimeContext.Instance.SaveData.ownedUnitItems;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null && string.Equals(items[i].itemInstanceId, itemInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return items[i];
                }
            }

            return null;
        }

        private VisualElement BuildPlaceholder(string text)
        {
            var placeholder = new Label(text);
            placeholder.AddToClassList("placeholder");
            return placeholder;
        }

        private VisualElement BuildEquippedItemChip(OwnedUnitItem item)
        {
            var chip = new VisualElement();
            chip.AddToClassList("equipped-item-chip");

            var title = item.itemDefinitionId;
            var subtitle = "Item";
            if (lootCatalogs != null && lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var definition) && definition != null)
            {
                title = definition.displayName;
                subtitle = BuildOwnedItemSubtitle(item, definition);
            }

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("equipped-item-chip-title");
            chip.Add(titleLabel);

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("equipped-item-chip-subtitle");
            chip.Add(subtitleLabel);

            var modifyButton = new Button(() => TryModifyItem(item.itemInstanceId))
            {
                text = "Mod"
            };
            modifyButton.AddToClassList("equipped-item-chip-button");
            modifyButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            chip.Add(modifyButton);

            RegisterItemDrag(chip, item, title);
            return chip;
        }

        private void TryModifyItem(string itemInstanceId)
        {
            const string currencyItemDefinitionId = "scrap_parts";
            if (!CampaignRuntimeContext.Instance.TryApplyItemModification(itemInstanceId, currencyItemDefinitionId, out var error))
            {
                SetStatus(error);
                return;
            }

            if (lootCatalogs != null && lootCatalogs.TryGetCurrencyItemDefinition(currencyItemDefinitionId, out var currencyDefinition) && currencyDefinition != null)
            {
                SetStatus(currencyDefinition.displayName + " applied.");
            }
            else
            {
                SetStatus("Item modified.");
            }
            RefreshAll();
        }

        private void EnsureCamera()
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

        private string BuildOwnedItemSubtitle(OwnedUnitItem item, ItemDefinition definition)
        {
            var parts = new List<string>();
            if (definition != null && !string.IsNullOrWhiteSpace(definition.itemSlotType))
            {
                parts.Add(definition.itemSlotType);
            }

            var baseSummary = BuildEffectSummary(definition?.effects);
            if (!string.IsNullOrWhiteSpace(baseSummary))
            {
                parts.Add(baseSummary);
            }

            var modifierSummary = BuildModifierSummary(item);
            if (!string.IsNullOrWhiteSpace(modifierSummary))
            {
                parts.Add("Mods: " + modifierSummary);
            }

            return string.Join("  ", parts);
        }

        private string BuildModifierSummary(OwnedUnitItem item)
        {
            if (item?.appliedModifiers == null || item.appliedModifiers.Count == 0 || lootCatalogs == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = 0; i < item.appliedModifiers.Count; i++)
            {
                var appliedModifier = item.appliedModifiers[i];
                if (appliedModifier == null
                    || string.IsNullOrWhiteSpace(appliedModifier.modifierTemplateId)
                    || !lootCatalogs.TryGetModifierTemplate(appliedModifier.modifierTemplateId, out var modifier)
                    || modifier == null)
                {
                    continue;
                }

                var effectSummary = BuildAppliedModifierSummary(appliedModifier);
                parts.Add(string.IsNullOrWhiteSpace(effectSummary) ? modifier.displayName : modifier.displayName + " " + effectSummary);
            }

            return string.Join(", ", parts);
        }

        private static string BuildAppliedModifierSummary(AppliedItemModifierData appliedModifier)
        {
            if (appliedModifier == null)
            {
                return string.Empty;
            }

            return appliedModifier.modifierType switch
            {
                ModifierType.MaxHealth => "maxHealth +" + appliedModifier.rolledValueA,
                ModifierType.Armor => "armor +" + appliedModifier.rolledValueA,
                ModifierType.VisionRange => "visionRange +" + appliedModifier.rolledValueA,
                ModifierType.Speed => "speed +" + appliedModifier.rolledValueA,
                ModifierType.Accuracy => "accuracy +" + appliedModifier.rolledValueA,
                ModifierType.FireReliability => "fireReliability +" + appliedModifier.rolledValueA,
                ModifierType.MoveReliability => "moveReliability +" + appliedModifier.rolledValueA,
                ModifierType.Damage => "damage " + appliedModifier.rolledValueA + "-" + Mathf.Max(appliedModifier.rolledValueA, appliedModifier.rolledValueB),
                _ => string.Empty
            };
        }

        private static string BuildEffectSummary(List<ItemEffectDefinition> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.statKey))
                {
                    continue;
                }

                var label = effect.operation == ItemEffectOperation.Multiply
                    ? effect.statKey + " x" + effect.value.ToString("0.##")
                    : effect.statKey + " +" + effect.value.ToString("0.##");
                parts.Add(label);
            }

            return string.Join(", ", parts);
        }

        private void SetStatus(string message)
        {
            statusLabel.text = message;
        }

        private void ClearStatus()
        {
            statusLabel.text = string.Empty;
        }

        private VisualElement Require(string name)
        {
            var element = root.Q<VisualElement>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Missing UI element: " + name);
            }

            return element;
        }

        private Label RequireLabel(string name)
        {
            var label = root.Q<Label>(name);
            if (label == null)
            {
                throw new InvalidOperationException("Missing label: " + name);
            }

            return label;
        }

        private Button RequireButton(string name)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                throw new InvalidOperationException("Missing button: " + name);
            }

            return button;
        }
    }
}
