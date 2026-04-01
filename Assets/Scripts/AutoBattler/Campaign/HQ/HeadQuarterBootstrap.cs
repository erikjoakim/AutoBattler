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
        private string selectedMapItemId;
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
            UpdateCursorFeedback();
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
                CampaignRuntimeContext.Instance.TryBuildMapRewardPreview(mapItem.mapItemId, out var rewardPreview, out _);
                var card = BuildInventoryCard(
                    mapItem.instanceName,
                    BuildMapSubtitle(mapItem, definition, rewardPreview),
                    "inventory-card-map");
                card.userData = mapItem.mapItemId;
                if (string.Equals(selectedMapItemId, mapItem.mapItemId, StringComparison.OrdinalIgnoreCase))
                {
                    card.AddToClassList("inventory-card-selected");
                }

                if (ShouldHighlightMapCurrencyTargets())
                {
                    if (CampaignRuntimeContext.Instance.CanApplyMapModification(mapItem.mapItemId, activeCurrencyItemDefinitionId, out _))
                    {
                        card.AddToClassList("inventory-card-valid-target");
                    }
                    else
                    {
                        card.AddToClassList("inventory-card-invalid-target");
                    }
                }

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
                if (ShouldHighlightItemCurrencyTargets())
                {
                    if (CampaignRuntimeContext.Instance.CanApplyItemModification(item.itemInstanceId, activeCurrencyItemDefinitionId, out _))
                    {
                        card.AddToClassList("inventory-card-valid-target");
                    }
                    else
                    {
                        card.AddToClassList("inventory-card-invalid-target");
                    }
                }

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

            if (!string.IsNullOrWhiteSpace(draggedItemInstanceId))
            {
                if (CampaignRuntimeContext.Instance.CanEquipItemToUnitCard(draggedItemInstanceId, card.unitCardId, out _))
                {
                    rootElement.AddToClassList("unit-card-valid-target");
                }
                else
                {
                    rootElement.AddToClassList("unit-card-invalid-target");
                }
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
                    var refund = Mathf.Max(0, card.deploymentGoldCostPaid);
                    CampaignRuntimeContext.Instance.TryUnassignUnitCardFromHex(card.unitCardId, selectedHexSlotId, out var error);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        SetStatus(error);
                    }
                    else
                    {
                        SetStatus(refund > 0
                            ? card.displayName + " removed. Refunded " + refund + " gold."
                            : card.displayName + " removed from the map.");
                    }
                }
                else
                {
                    CampaignRuntimeContext.Instance.TryAssignUnitCardToHex(card.unitCardId, selectedHexSlotId, out var error);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        SetStatus(error);
                    }
                    else
                    {
                        var cost = Mathf.Max(0, card.deploymentGoldCostPaid);
                        SetStatus(cost > 0
                            ? card.displayName + " selected. Fielding cost " + cost + " gold."
                            : card.displayName + " selected for deployment.");
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

                element.EnableInClassList("hex-valid-target", !string.IsNullOrWhiteSpace(draggedMapItemId) && slot.state == CampaignHexState.Open);
                element.EnableInClassList("hex-invalid-target", !string.IsNullOrWhiteSpace(draggedMapItemId) && slot.state != CampaignHexState.Open);
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

            var detailMap = ResolveDetailMap(slot);
            if (detailMap != null && CampaignRuntimeContext.Instance.Catalogs.TryGetMapDefinition(detailMap.mapDefinitionId, out var mapDefinition))
            {
                lines.Add(string.Empty);
                lines.Add("Mission Briefing");
                lines.Add("Map: " + detailMap.instanceName);
                lines.Add("Mission: " + ResolveMissionName(mapDefinition));
                lines.Add("Scene: " + mapDefinition.sceneName);
                lines.Add("Tier: " + mapDefinition.tier);
                lines.Add("Modifier Count: " + (detailMap.appliedMapModifiers?.Count ?? 0));
                var missionDescription = ResolveMissionDescription(mapDefinition);
                if (!string.IsNullOrWhiteSpace(missionDescription))
                {
                    lines.Add(string.Empty);
                    lines.Add(missionDescription);
                }

                var primaryObjective = ResolvePrimaryObjective(mapDefinition);
                if (!string.IsNullOrWhiteSpace(primaryObjective))
                {
                    lines.Add(string.Empty);
                    lines.Add("Primary Objective");
                    lines.Add(primaryObjective);
                }

                var loseCondition = ResolveLoseCondition(mapDefinition);
                if (!string.IsNullOrWhiteSpace(loseCondition))
                {
                    lines.Add(string.Empty);
                    lines.Add("Failure Condition");
                    lines.Add(loseCondition);
                }

                lines.Add(string.Empty);
                lines.Add("Spawner Presence: " + (mapDefinition.hasSpawners ? "Expected" : "None expected"));
                var scenarioTags = ResolveScenarioTags(mapDefinition);
                if (!string.IsNullOrWhiteSpace(scenarioTags))
                {
                    lines.Add("Scenario Tags: " + scenarioTags);
                }

                if (CampaignRuntimeContext.Instance.TryBuildMapRewardPreview(detailMap.mapItemId, out var rewardProfile, out var previewError))
                {
                    lines.Add(string.Empty);
                    lines.Add("Risk");
                    lines.Add("Base Threat: " + rewardProfile.baseThreat.ToString("0.0"));
                    lines.Add("Modified Threat: " + rewardProfile.modifiedThreat.ToString("0.0"));
                    lines.Add("Threat Ratio: x" + Mathf.Max(1f, rewardProfile.threatRatio).ToString("0.00"));
                    lines.Add("Risk Level: " + BuildRiskLabel(rewardProfile));
                    lines.Add(string.Empty);
                    lines.Add("Reward");
                    lines.Add("Reward Multiplier: x" + rewardProfile.rewardMultiplier.ToString("0.00"));
                    lines.Add("Bonus Loot Roll Chance: " + Mathf.RoundToInt(rewardProfile.bonusLootRollChance * 100f) + "%");
                    if (rewardProfile.rewardMultiplier >= 1.6f)
                    {
                        lines.Add("Warning: high-risk operation.");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(previewError))
                {
                    lines.Add(string.Empty);
                    lines.Add("Preview: " + previewError);
                }

                AppendMapModifierLines(lines, detailMap);
                if (!string.IsNullOrWhiteSpace(mapDefinition.description))
                {
                    lines.Add(string.Empty);
                    lines.Add("Overview");
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
            var deploymentGold = 0;
            for (var i = 0; i < selectedCards.Count; i++)
            {
                if (selectedCards[i] != null)
                {
                    deploymentGold += Mathf.Max(0, selectedCards[i].deploymentGoldCostPaid);
                }
            }

            lines.Add("Deployment Gold: " + deploymentGold);
            if (selectedCards.Count > 0)
            {
                lines.Add("Selected Units:");
                for (var i = 0; i < selectedCards.Count; i++)
                {
                    var selectedCard = selectedCards[i];
                    if (selectedCard == null)
                    {
                        continue;
                    }

                    var costLabel = selectedCard.deploymentGoldCostPaid > 0 ? " [" + selectedCard.deploymentGoldCostPaid + "g]" : string.Empty;
                    lines.Add("- " + selectedCard.displayName + costLabel);
                }
            }

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
                "Ended Because:",
                result.resultMessage
            };

            var activeMission = CampaignRuntimeContext.Instance.ActiveMission;
            if (activeMission != null && CampaignRuntimeContext.Instance.Catalogs.TryGetMapDefinition(activeMission.mapDefinitionId, out var mapDefinition) && mapDefinition != null)
            {
                lines.Insert(2, "Operation: " + ResolveMissionName(mapDefinition));
                lines.Insert(3, string.Empty);
            }

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
                AppendAwardedCardLines(lines, result.awardedUnitCards);
            }

            if (result.claimedLoot != null && result.claimedLoot.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Loot Secured:");
                AppendLootLines(lines, result.claimedLoot);
            }

            if (result.lostLoot != null && result.lostLoot.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Loot Lost:");
                AppendLootLines(lines, result.lostLoot);
            }

            resultLabel.text = string.Join("\n", lines);
        }

        private void RegisterMapDrag(VisualElement element, string mapItemId, string label)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                SelectMapItemForDetails(mapItemId, false);
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
                SetStatus("Dragging map " + label + ". Drop it on an open hex.");
                UpdateCursorFeedback();
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
                SetStatus("Dragging item " + label + ". Drop it on a compatible troop card.");
                UpdateCursorFeedback();
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
                SetStatus(displayName + " selected. Right-click a highlighted valid target to apply it.");
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

            var item = FindOwnedUnitItem(draggedItemInstanceId);
            var itemLabel = item != null && lootCatalogs != null && lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var itemDefinition) && itemDefinition != null
                ? itemDefinition.displayName
                : (item != null ? item.itemDefinitionId : "Item");
            var card = FindUnitCard(unitCardId);
            var cardLabel = card != null ? card.displayName : unitCardId;
            if (!CampaignRuntimeContext.Instance.TryEquipItemToUnitCard(draggedItemInstanceId, unitCardId, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus(itemLabel + " equipped to " + cardLabel + ".");
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

            UpdateCursorFeedback();
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

            var itemLabel = item.itemDefinitionId;
            if (lootCatalogs != null && lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var definition) && definition != null)
            {
                itemLabel = definition.displayName;
            }

            SetStatus(itemLabel + " returned to inventory.");
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
            var slot = CampaignRuntimeContext.Instance.GetHexSlotState(selectedHexSlotId);
            if (!string.IsNullOrWhiteSpace(slot.occupiedMapItemId))
            {
                selectedMapItemId = slot.occupiedMapItemId;
            }
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
            selectedMapItemId = mapItemId;
            var map = FindMapById(mapItemId);
            SetStatus((map != null ? map.instanceName : "Map") + " placed on " + hexSlotId.ToUpperInvariant() + ".");
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
            var slot = CampaignRuntimeContext.Instance.GetHexSlotState(selectedHexSlotId);
            var mapLabel = ResolveMapLabel(slot.occupiedMapItemId);
            var assignedCount = slot.selectedUnitCardIds?.Count ?? 0;
            if (!CampaignRuntimeContext.Instance.TryClearHex(selectedHexSlotId, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus(mapLabel + " removed from " + selectedHexSlotId.ToUpperInvariant() + ". Released " + assignedCount + " assigned card(s).");
            RefreshAll();
        }

        private void ConfirmResult()
        {
            var result = CampaignRuntimeContext.Instance.PendingBattleResult;
            CampaignRuntimeContext.Instance.FinalizePendingBattleResult();
            if (result != null)
            {
                var claimedCount = result.claimedLoot?.Count ?? 0;
                var lostCount = result.lostLoot?.Count ?? 0;
                SetStatus((result.victory ? "Victory" : "Defeat") + " confirmed. Loot secured: " + claimedCount + ". Loot lost: " + lostCount + ".");
            }
            else
            {
                SetStatus("Battle result confirmed.");
            }

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

        private void AppendAwardedCardLines(List<string> lines, List<AwardedUnitCardData> awardedCards)
        {
            var recovered = new List<AwardedUnitCardData>();
            var captured = new List<AwardedUnitCardData>();
            for (var i = 0; i < awardedCards.Count; i++)
            {
                var awardedCard = awardedCards[i];
                if (awardedCard == null)
                {
                    continue;
                }

                if (string.Equals(awardedCard.sourceLabel, "Captured", StringComparison.OrdinalIgnoreCase))
                {
                    captured.Add(awardedCard);
                }
                else
                {
                    recovered.Add(awardedCard);
                }
            }

            if (recovered.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Returned To HQ:");
                for (var i = 0; i < recovered.Count; i++)
                {
                    lines.Add("- " + recovered[i].displayName);
                }
            }

            if (captured.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Captured:");
                for (var i = 0; i < captured.Count; i++)
                {
                    lines.Add("- " + captured[i].displayName);
                }
            }
        }

        private static void AppendLootLines(List<string> lines, List<DroppedLootEntry> loot)
        {
            for (var i = 0; i < loot.Count; i++)
            {
                var entry = loot[i];
                if (entry == null)
                {
                    continue;
                }

                var amountLabel = entry.amount > 1 ? " x" + entry.amount : string.Empty;
                lines.Add("- " + entry.displayName + amountLabel);
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

        private static string ResolveMissionName(MapDefinition mapDefinition)
        {
            if (mapDefinition == null)
            {
                return "Unknown Mission";
            }

            if (!string.IsNullOrWhiteSpace(mapDefinition.missionName))
            {
                return mapDefinition.missionName;
            }

            return string.IsNullOrWhiteSpace(mapDefinition.displayName) ? mapDefinition.mapDefinitionId : mapDefinition.displayName;
        }

        private static string ResolveMissionDescription(MapDefinition mapDefinition)
        {
            if (mapDefinition == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(mapDefinition.missionDescription)
                ? mapDefinition.missionDescription
                : mapDefinition.description;
        }

        private static string ResolvePrimaryObjective(MapDefinition mapDefinition)
        {
            if (mapDefinition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(mapDefinition.primaryObjective)
                ? "Complete the mission objectives in the battle scene."
                : mapDefinition.primaryObjective;
        }

        private static string ResolveLoseCondition(MapDefinition mapDefinition)
        {
            if (mapDefinition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(mapDefinition.loseCondition)
                ? "Avoid mission failure conditions and keep the roster alive."
                : mapDefinition.loseCondition;
        }

        private static string ResolveScenarioTags(MapDefinition mapDefinition)
        {
            if (mapDefinition?.scenarioTags == null || mapDefinition.scenarioTags.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" | ", mapDefinition.scenarioTags);
        }

        private static string BuildMapSubtitle(OwnedMapItem mapItem, MapDefinition definition)
        {
            return BuildMapSubtitle(mapItem, definition, null);
        }

        private static string BuildMapSubtitle(OwnedMapItem mapItem, MapDefinition definition, PreparedMissionRewardProfile rewardProfile)
        {
            var modifierCount = mapItem?.appliedMapModifiers?.Count ?? 0;
            var risk = rewardProfile == null ? string.Empty : "  " + BuildRiskLabel(rewardProfile);
            return modifierCount > 0
                ? "Map  T" + (definition?.tier ?? 1) + "  Mods " + modifierCount + risk
                : "Map  T" + (definition?.tier ?? 1) + risk;
        }

        private void AppendMapModifierLines(List<string> lines, OwnedMapItem mapItem)
        {
            if (lines == null || mapItem?.appliedMapModifiers == null || mapItem.appliedMapModifiers.Count == 0)
            {
                return;
            }

            lines.Add(string.Empty);
            lines.Add("Modifiers:");
            for (var i = 0; i < mapItem.appliedMapModifiers.Count; i++)
            {
                var modifier = mapItem.appliedMapModifiers[i];
                if (modifier == null)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(modifier.displayName) ? modifier.mapModifierTemplateId : modifier.displayName;
                CampaignRuntimeContext.Instance.TryGetMapModifierThreatContribution(mapItem.mapItemId, i, out var threatContribution, out _);
                var threatLabel = threatContribution > 0.05f ? "  [Threat +" + threatContribution.ToString("0.0") + "]" : "  [Neutral]";
                var summary = BuildMapModifierSummary(modifier);
                lines.Add("- " + label + threatLabel);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    lines.Add("  " + summary);
                }
            }
        }

        private void SelectMapItemForDetails(string mapItemId, bool refreshAll)
        {
            selectedMapItemId = mapItemId;
            if (refreshAll)
            {
                RefreshAll();
                return;
            }

            RefreshDetails();
            RefreshMapInventorySelectionStates();
        }

        private void RefreshMapInventorySelectionStates()
        {
            if (inventoryList == null)
            {
                return;
            }

            for (var i = 0; i < inventoryList.childCount; i++)
            {
                var child = inventoryList[i];
                if (child == null || child.userData is not string mapItemId)
                {
                    continue;
                }

                child.EnableInClassList("inventory-card-selected", string.Equals(selectedMapItemId, mapItemId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private OwnedMapItem ResolveDetailMap(HexSlotSaveData selectedSlot)
        {
            if (!string.IsNullOrWhiteSpace(selectedMapItemId))
            {
                var selectedMap = FindMapById(selectedMapItemId);
                if (selectedMap != null)
                {
                    return selectedMap;
                }

                selectedMapItemId = string.Empty;
            }

            if (selectedSlot != null && !string.IsNullOrWhiteSpace(selectedSlot.occupiedMapItemId))
            {
                selectedMapItemId = selectedSlot.occupiedMapItemId;
                return FindMapById(selectedSlot.occupiedMapItemId);
            }

            return null;
        }

        private bool ShouldHighlightMapCurrencyTargets()
        {
            if (string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId) || lootCatalogs == null)
            {
                return false;
            }

            return lootCatalogs.TryGetCurrencyItemDefinition(activeCurrencyItemDefinitionId, out var currencyDefinition)
                && currencyDefinition != null
                && (currencyDefinition.targetTypes == null
                    || currencyDefinition.targetTypes.Count == 0
                    || ContainsIgnoreCase(currencyDefinition.targetTypes, "Map"));
        }

        private bool ShouldHighlightItemCurrencyTargets()
        {
            if (string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId) || lootCatalogs == null)
            {
                return false;
            }

            if (!lootCatalogs.TryGetCurrencyItemDefinition(activeCurrencyItemDefinitionId, out var currencyDefinition) || currencyDefinition == null)
            {
                return false;
            }

            return currencyDefinition.targetTypes != null
                && currencyDefinition.targetTypes.Count > 0
                && !ContainsIgnoreCase(currencyDefinition.targetTypes, "Map");
        }

        private static bool ContainsIgnoreCase(List<string> values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildRiskLabel(PreparedMissionRewardProfile rewardProfile)
        {
            if (rewardProfile == null)
            {
                return "Baseline";
            }

            var ratio = Mathf.Max(1f, rewardProfile.threatRatio);
            if (ratio <= 1.01f)
            {
                return "Baseline";
            }

            if (ratio <= 1.25f)
            {
                return "Elevated";
            }

            if (ratio <= 1.5f)
            {
                return "Dangerous";
            }

            return "Extreme";
        }

        private static string BuildMapModifierSummary(AppliedMapModifierData modifier)
        {
            if (modifier?.effects == null || modifier.effects.Count == 0)
            {
                return string.Empty;
            }

            var selectorLabel = modifier.selectors == null || modifier.selectors.all || string.IsNullOrWhiteSpace(modifier.selectors.unitType)
                ? "all enemy units"
                : modifier.selectors.unitType;
            var parts = new List<string>();
            for (var i = 0; i < modifier.effects.Count; i++)
            {
                var effect = modifier.effects[i];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.effectType)
                {
                    case MapModifierEffectType.AdjustUnitCount:
                        parts.Add(selectorLabel + " count " + BuildOperationSummary(effect.operation, effect.rolledValue));
                        break;
                    case MapModifierEffectType.ReplaceUnitType:
                        parts.Add("replace " + selectorLabel + " with " + effect.replacementUnitType);
                        break;
                    case MapModifierEffectType.ModifyUnitStat:
                        parts.Add(selectorLabel + " " + effect.statKey + " " + BuildOperationSummary(effect.operation, effect.rolledValue));
                        break;
                    case MapModifierEffectType.ModifyAmmoStat:
                    {
                        var ammoLabel = string.IsNullOrWhiteSpace(effect.ammoType) ? "all ammo" : effect.ammoType;
                        parts.Add(selectorLabel + " " + ammoLabel + " " + effect.statKey + " " + BuildOperationSummary(effect.operation, effect.rolledValue));
                        break;
                    }
                }
            }

            return string.Join("; ", parts);
        }

        private static string BuildOperationSummary(MapModifierOperation operation, int rolledValue)
        {
            return operation switch
            {
                MapModifierOperation.Multiply => "x" + rolledValue,
                MapModifierOperation.Set => "=" + rolledValue,
                _ => (rolledValue >= 0 ? "+" : string.Empty) + rolledValue
            };
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

            if (ShouldHighlightItemCurrencyTargets())
            {
                if (CampaignRuntimeContext.Instance.CanApplyItemModification(item.itemInstanceId, activeCurrencyItemDefinitionId, out _))
                {
                    chip.AddToClassList("equipped-item-chip-valid-target");
                }
                else
                {
                    chip.AddToClassList("equipped-item-chip-invalid-target");
                }
            }

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
            UpdateCursorFeedback();
        }

        private void ClearCurrencyCursor()
        {
            activeCurrencyItemDefinitionId = string.Empty;
            activeCurrencyDisplayName = string.Empty;
            if (draggedMapGhost != null && string.IsNullOrWhiteSpace(draggedMapItemId) && string.IsNullOrWhiteSpace(draggedItemInstanceId))
            {
                draggedMapGhost.style.display = DisplayStyle.None;
            }

            UpdateCursorFeedback();
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

            var item = FindOwnedUnitItem(itemInstanceId);
            var itemLabel = item != null && lootCatalogs != null && lootCatalogs.TryGetItemDefinition(item.itemDefinitionId, out var definition) && definition != null
                ? definition.displayName
                : (item != null ? item.itemDefinitionId : "item");
            SetStatus(activeCurrencyDisplayName + " applied to " + itemLabel + ".");
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

            var mapItem = FindMapById(mapItemId);
            var previousModifierCount = mapItem?.appliedMapModifiers?.Count ?? 0;
            if (!CampaignRuntimeContext.Instance.TryApplyMapModification(mapItemId, activeCurrencyItemDefinitionId, out var error))
            {
                SetStatus(error);
                return;
            }

            var keepSelected = (evt.modifiers & EventModifiers.Shift) != 0
                && CampaignRuntimeContext.Instance.GetCurrencyAmount(activeCurrencyItemDefinitionId) > 0;

            SelectMapItemForDetails(mapItemId, false);
            var updatedMapItem = FindMapById(mapItemId);
            var addedModifier = updatedMapItem != null
                && updatedMapItem.appliedMapModifiers != null
                && updatedMapItem.appliedMapModifiers.Count > previousModifierCount
                ? updatedMapItem.appliedMapModifiers[updatedMapItem.appliedMapModifiers.Count - 1]
                : null;
            var modifierLabel = addedModifier == null
                ? "modifier"
                : (string.IsNullOrWhiteSpace(addedModifier.displayName) ? addedModifier.mapModifierTemplateId : addedModifier.displayName);
            var previewSuffix = string.Empty;
            if (CampaignRuntimeContext.Instance.TryBuildMapRewardPreview(mapItemId, out var rewardProfile, out _))
            {
                previewSuffix = " Threat x" + Mathf.Max(1f, rewardProfile.threatRatio).ToString("0.00")
                    + " Reward x" + rewardProfile.rewardMultiplier.ToString("0.00");
            }

            SetStatus(activeCurrencyDisplayName + " added " + modifierLabel + " to " + (updatedMapItem != null ? updatedMapItem.instanceName : "the map") + "." + previewSuffix);
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

        private void UpdateCursorFeedback()
        {
            if (root == null)
            {
                return;
            }

            var draggingMap = !string.IsNullOrWhiteSpace(draggedMapItemId);
            var draggingItem = !string.IsNullOrWhiteSpace(draggedItemInstanceId);
            var selectingCurrency = !string.IsNullOrWhiteSpace(activeCurrencyItemDefinitionId);
            root.EnableInClassList("hq-cursor-map", draggingMap);
            root.EnableInClassList("hq-cursor-item", draggingItem);
            root.EnableInClassList("hq-cursor-currency", !draggingMap && !draggingItem && selectingCurrency);

            if (draggedMapGhost == null)
            {
                return;
            }

            draggedMapGhost.EnableInClassList("dragged-ghost-map", draggingMap);
            draggedMapGhost.EnableInClassList("dragged-ghost-item", draggingItem);
            draggedMapGhost.EnableInClassList("dragged-ghost-currency", !draggingMap && !draggingItem && selectingCurrency);
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
