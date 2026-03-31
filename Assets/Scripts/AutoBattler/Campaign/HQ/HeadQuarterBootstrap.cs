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
        private VisualElement unitDetailOverlay;
        private Label unitDetailTitle;
        private VisualElement unitDetailContent;
        private VisualElement resultOverlay;
        private Label resultLabel;
        private string selectedHexSlotId;
        private string draggedMapItemId;
        private string draggedItemInstanceId;
        private string activeCurrencyItemDefinitionId;
        private string activeCurrencyDisplayName;
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

            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
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
            unitDetailOverlay = Require("UnitDetailOverlay");
            unitDetailTitle = RequireLabel("UnitDetailTitle");
            unitDetailContent = Require("UnitDetailContent");
            resultOverlay = Require("ResultOverlay");
            resultLabel = RequireLabel("ResultLabel");

            openMapButton.clicked += TryOpenSelectedHex;
            clearHexButton.clicked += TryClearSelectedHex;
            RequireButton("UnitDetailCloseButton").clicked += CloseUnitDetailPopup;
            RequireButton("ResultConfirmButton").clicked += ConfirmResult;
            inventoryList.RegisterCallback<PointerUpEvent>(OnInventoryPointerUp);
            unitDetailOverlay.RegisterCallback<PointerDownEvent>(OnUnitDetailOverlayPointerDown);
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

            CloseUnitDetailPopup();
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
                    mapItem.instanceName,
                    BuildMapSubtitle(mapItem, definition),
                    "inventory-card-map");
                RegisterMapDrag(card, mapItem.mapItemId, mapItem.instanceName);
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

                var currencyCard = BuildInventoryCard(displayName, "Currency  x" + stack.amount, "inventory-card-currency");
                if (string.Equals(activeCurrencyItemDefinitionId, stack.currencyItemDefinitionId, StringComparison.OrdinalIgnoreCase))
                {
                    currencyCard.AddToClassList("inventory-card-currency-selected");
                }

                RegisterCurrencySelection(currencyCard, stack.currencyItemDefinitionId, displayName);
                inventoryList.Add(currencyCard);
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
            return BuildInventoryCard(titleText, subtitleText, "inventory-card-item");
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
            action.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
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
            if (assigned)
            {
                rootElement.RegisterCallback<ClickEvent>(_ => OpenUnitDetailPopup(card));
            }

            rootElement.RegisterCallback<PointerUpEvent>(_ => TryDropDraggedItemToUnitCard(card.unitCardId));
            return rootElement;
        }

        private VisualElement BuildAssignedUnitDetails(OwnedUnitCard card)
        {
            var details = new VisualElement();
            details.AddToClassList("unit-card-details");

            if (CampaignRuntimeContext.Instance == null
                || !CampaignRuntimeContext.Instance.TryBuildResolvedUnitSpawnForCard(card.unitCardId, out var unitSpawn)
                || unitSpawn?.definition == null)
            {
                details.Add(BuildDetailLine("Preview unavailable."));
                return details;
            }

            var definition = unitSpawn.definition;
            details.Add(BuildDetailSectionLabel("Core"));
            details.Add(BuildDetailLine("Mission: " + unitSpawn.mission + "  Type: " + definition.UnitType + "  Nav: " + BuildNavigationLabel(definition)));
            details.Add(BuildDetailLine("Health: " + definition.MaxHealth + "  Armor: " + definition.Armor + "  Vision: " + definition.VisionRange.ToString("0.##")));
            details.Add(BuildDetailLine("Speed: " + definition.Speed.ToString("0.##")
                + "  Accuracy: " + definition.Accuracy.ToString("0.##")
                + "  Fire Rel: " + definition.FireReliability.ToString("0.##")
                + "  Move Rel: " + definition.MoveReliability.ToString("0.##")));

            if (definition.OutgoingDamageBonusMin > 0 || definition.OutgoingDamageBonusMax > 0)
            {
                details.Add(BuildDetailLine("Damage Bonus: +" + definition.OutgoingDamageBonusMin + "-" + Mathf.Max(definition.OutgoingDamageBonusMin, definition.OutgoingDamageBonusMax)));
            }

            if (!string.IsNullOrWhiteSpace(card.overrideJson))
            {
                details.Add(BuildDetailLine("Card overrides: applied"));
            }

            details.Add(BuildDetailSectionLabel("Terrain"));
            details.Add(BuildDetailLine("Speed: " + BuildTerrainProfileSummary(definition.TerrainSpeedProfile)));
            details.Add(BuildDetailLine("Path: " + BuildTerrainProfileSummary(definition.TerrainPathCostProfile)));

            details.Add(BuildDetailSectionLabel("Ammo"));
            var ammoLines = BuildAmmoLines(definition);
            if (ammoLines.Count == 0)
            {
                details.Add(BuildDetailLine("No ammo configured."));
            }
            else
            {
                for (var i = 0; i < ammoLines.Count; i++)
                {
                    details.Add(BuildDetailLine(ammoLines[i], true));
                }
            }

            if (card.equippedItemIds != null && card.equippedItemIds.Count > 0)
            {
                details.Add(BuildDetailSectionLabel("Modifiers"));
                for (var i = 0; i < card.equippedItemIds.Count; i++)
                {
                    var ownedItem = FindOwnedUnitItem(card.equippedItemIds[i]);
                    if (ownedItem == null)
                    {
                        continue;
                    }

                    var itemTitle = ownedItem.itemDefinitionId;
                    var itemSummary = "Item";
                    if (lootCatalogs != null && lootCatalogs.TryGetItemDefinition(ownedItem.itemDefinitionId, out var itemDefinition) && itemDefinition != null)
                    {
                        itemTitle = itemDefinition.displayName;
                        itemSummary = BuildOwnedItemSubtitle(ownedItem, itemDefinition);
                    }

                    details.Add(BuildDetailLine(itemTitle + ": " + itemSummary, true));
                }
            }

            return details;
        }

        private void OpenUnitDetailPopup(OwnedUnitCard card)
        {
            if (card == null || unitDetailOverlay == null || unitDetailTitle == null || unitDetailContent == null)
            {
                return;
            }

            unitDetailTitle.text = card.displayName + "  [" + card.baseTemplateId + "]";
            unitDetailContent.Clear();
            unitDetailContent.Add(BuildAssignedUnitDetails(card));
            unitDetailOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseUnitDetailPopup()
        {
            if (unitDetailOverlay != null)
            {
                unitDetailOverlay.style.display = DisplayStyle.None;
            }
        }

        private void OnUnitDetailOverlayPointerDown(PointerDownEvent evt)
        {
            if (evt == null || unitDetailOverlay == null)
            {
                return;
            }

            if (ReferenceEquals(evt.target, unitDetailOverlay))
            {
                CloseUnitDetailPopup();
                evt.StopPropagation();
            }
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
                    lines.Add("Map: " + mapItem.instanceName);
                    lines.Add("Scene: " + mapDefinition.sceneName);
                    lines.Add("Tier: " + mapDefinition.tier);
                    AppendMapModifierLines(lines, mapItem);
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

            if (result.awardedUnitCards != null && result.awardedUnitCards.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("New Cards:");
                for (var i = 0; i < result.awardedUnitCards.Count; i++)
                {
                    var awardedCard = result.awardedUnitCards[i];
                    if (awardedCard == null)
                    {
                        continue;
                    }

                    lines.Add("- " + awardedCard.displayName + (string.IsNullOrWhiteSpace(awardedCard.sourceLabel) ? string.Empty : " [" + awardedCard.sourceLabel + "]"));
                }
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
                if (evt.button == 1)
                {
                    if (!string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
                    {
                        TryApplySelectedCurrencyToMap(mapItemId, evt);
                    }
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0)
                {
                    return;
                }

                ClearCurrencyCursor();
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
                if (evt.button == 1)
                {
                    if (!string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
                    {
                        TryApplySelectedCurrencyToItem(item.itemInstanceId, evt);
                    }
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0)
                {
                    return;
                }

                ClearCurrencyCursor();
                draggedMapItemId = string.Empty;
                draggedItemInstanceId = item.itemInstanceId;
                EnsureDragGhost();
                draggedMapGhost.Q<Label>("DragLabel").text = label;
                draggedMapGhost.style.display = DisplayStyle.Flex;
                UpdateDragGhost(evt.position);
                evt.StopPropagation();
            });
        }

        private void RegisterCurrencySelection(VisualElement element, string currencyItemDefinitionId, string displayName)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                if (string.Equals(activeCurrencyItemDefinitionId, currencyItemDefinitionId, StringComparison.OrdinalIgnoreCase))
                {
                    ClearCurrencyCursor();
                    SetStatus("Currency selection cleared.");
                    RefreshAll();
                    evt.StopPropagation();
                    return;
                }

                ActivateCurrencyCursor(currencyItemDefinitionId, displayName, evt.position);
                SetStatus(displayName + " selected. Right-click a valid item or map to apply it.");
                RefreshAll();
                evt.StopPropagation();
            });
        }

        private void OnRootPointerMove(PointerMoveEvent evt)
        {
            if ((string.IsNullOrWhiteSpace(draggedMapItemId) && string.IsNullOrWhiteSpace(draggedItemInstanceId) && string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId)) || draggedMapGhost == null)
            {
                return;
            }

            UpdateDragGhost(evt.position);
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 1 || string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
            {
                return;
            }

            ClearCurrencyCursor();
            SetStatus("Currency selection cleared.");
            RefreshAll();
        }

        private void OnRootPointerUp(PointerUpEvent evt)
        {
            if (!string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
            {
                return;
            }

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
            if (mapItem != null)
            {
                return mapItem.instanceName;
            }

            return "MAP READY";
        }

        private static string BuildMapSubtitle(OwnedMapItem mapItem, MapDefinition definition)
        {
            var modifierCount = mapItem?.appliedMapModifiers?.Count ?? 0;
            return modifierCount > 0
                ? "Map  T" + (definition?.tier ?? 1) + "  Mods " + modifierCount
                : "Map  T" + (definition?.tier ?? 1);
        }

        private static void AppendMapModifierLines(List<string> lines, OwnedMapItem mapItem)
        {
            if (lines == null || mapItem?.appliedMapModifiers == null || mapItem.appliedMapModifiers.Count == 0)
            {
                return;
            }

            lines.Add("Modifiers:");
            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                var modifier = mapItem.appliedMapModifiers[i];
                if (modifier == null)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(modifier.displayName) ? modifier.mapModifierTemplateId : modifier.displayName;
                lines.Add("- " + label);
            }
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

            RegisterItemDrag(chip, item, title);
            return chip;
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

        private static Label BuildDetailSectionLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("unit-card-section-label");
            return label;
        }

        private static Label BuildDetailLine(string text, bool compact = false)
        {
            var label = new Label(text);
            label.AddToClassList(compact ? "unit-card-detail-line-compact" : "unit-card-detail-line");
            return label;
        }

        private static string BuildNavigationLabel(UnitDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition?.NavigationAgentType) ? "Default" : definition.NavigationAgentType;
        }

        private static string BuildTerrainProfileSummary(TerrainSpeedProfile profile)
        {
            if (profile == null || !profile.HasOverrides || profile.Modifiers == null || profile.Modifiers.Count == 0)
            {
                return "None";
            }

            var parts = new List<string>();
            foreach (var pair in profile.Modifiers)
            {
                parts.Add(pair.Key + " " + pair.Value.ToString("0.##"));
            }

            return string.Join("  ", parts);
        }

        private static List<string> BuildAmmoLines(UnitDefinition definition)
        {
            var lines = new List<string>();
            if (definition?.Ammunition == null || definition.AmmunitionCounts == null)
            {
                return lines;
            }

            for (var i = 0; i < definition.Ammunition.Length; i++)
            {
                var ammo = definition.Ammunition[i];
                if (ammo == null)
                {
                    continue;
                }

                var count = i < definition.AmmunitionCounts.Length ? definition.AmmunitionCounts[i] : 0;
                var countLabel = count < 0 ? "inf" : count.ToString();
                lines.Add(ammo.AmmoName
                    + " x" + countLabel
                    + "  Dmg " + FormatDamageRange(ammo.DamageMin, ammo.DamageMax)
                    + "  Rad " + ammo.Radius.ToString("0.##")
                    + "  Rng " + ammo.AttackRange.ToString("0.##")
                    + "  Rld " + ammo.ReloadTime.ToString("0.##")
                    + "  Acc " + ammo.Accuracy.ToString("0.##")
                    + "  Rel " + ammo.DamageReliability.ToString("0.##"));
            }

            return lines;
        }

        private static string FormatDamageRange(int minDamage, int maxDamage)
        {
            return minDamage == maxDamage ? minDamage.ToString() : minDamage + "-" + maxDamage;
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

        private void ActivateCurrencyCursor(string currencyItemDefinitionId, string displayName, Vector2 position)
        {
            EndDrag();
            activeCurrencyItemDefinitionId = currencyItemDefinitionId;
            activeCurrencyDisplayName = displayName;
            EnsureDragGhost();
            draggedMapGhost.Q<Label>("DragLabel").text = displayName;
            draggedMapGhost.style.display = DisplayStyle.Flex;
            UpdateDragGhost(position);
        }

        private void ClearCurrencyCursor()
        {
            activeCurrencyItemDefinitionId = string.Empty;
            activeCurrencyDisplayName = string.Empty;
            if (draggedMapGhost != null && string.IsNullOrWhiteSpace(draggedMapItemId) && string.IsNullOrWhiteSpace(draggedItemInstanceId))
            {
                draggedMapGhost.style.display = DisplayStyle.None;
            }
        }

        private void TryApplySelectedCurrencyToItem(string itemInstanceId, PointerDownEvent evt)
        {
            if (string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
            {
                return;
            }

            if (!CampaignRuntimeContext.Instance.TryApplyItemModification(itemInstanceId, activeCurrencyItemDefinitionId, out var error))
            {
                SetStatus(error);
                return;
            }

            var keepSelected = (evt.modifiers & EventModifiers.Shift) != 0
                && CampaignRuntimeContext.Instance.GetCurrencyAmount(activeCurrencyItemDefinitionId) > 0;

            SetStatus(activeCurrencyDisplayName + " applied.");
            if (!keepSelected)
            {
                ClearCurrencyCursor();
            }

            RefreshAll();
        }

        private void TryApplySelectedCurrencyToMap(string mapItemId, PointerDownEvent evt)
        {
            if (string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId))
            {
                return;
            }

            if (!CampaignRuntimeContext.Instance.TryApplyMapModification(mapItemId, activeCurrencyItemDefinitionId, out var error))
            {
                SetStatus(error);
                return;
            }

            var keepSelected = (evt.modifiers & EventModifiers.Shift) != 0
                && CampaignRuntimeContext.Instance.GetCurrencyAmount(activeCurrencyItemDefinitionId) > 0;

            SetStatus(activeCurrencyDisplayName + " applied to map.");
            if (!keepSelected)
            {
                ClearCurrencyCursor();
            }

            RefreshAll();
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
