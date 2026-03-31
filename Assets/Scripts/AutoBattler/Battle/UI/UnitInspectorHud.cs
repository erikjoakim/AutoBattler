using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class UnitInspectorHud : MonoBehaviour
    {
        private const string IncreasedColor = "#63B8FF";
        private const string ReducedColor = "#FF4D4D";

        private struct TerrainProbeInfo
        {
            public bool IsValid;
            public Vector3 Position;
            public string TerrainType;
            public string NavAreaName;
            public int NavAreaIndex;
            public NavMeshPathStatus PathStatus;
            public float PathLength;
            public bool HasPath;
            public float AreaCost;
        }

        private readonly StringBuilder builder = new StringBuilder(512);
        private readonly StringBuilder plainTextBuilder = new StringBuilder(512);
        private BattleUnit selectedUnit;
        private TerrainProbeInfo terrainProbe;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private Mouse mouse;
        private Keyboard keyboard;
        private GameDataCatalog gameDataCatalog;
        private GameObject selectionMarker;
        private Transform selectionRing;
        private Transform selectionBeacon;

        private void Update()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "HeadQuarter", System.StringComparison.OrdinalIgnoreCase))
            {
                selectedUnit = null;
                terrainProbe = default;
                return;
            }

            if (selectedUnit != null && !selectedUnit.IsAlive)
            {
                selectedUnit = null;
            }

            mouse ??= Mouse.current;
            keyboard ??= Keyboard.current;
            if (mouse == null)
            {
                return;
            }

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                SelectNextPlayerUnit();
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                InspectUnderCursor();
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                selectedUnit = null;
                terrainProbe = default;
            }

            UpdateSelectionMarker();
        }

        private void OnGUI()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "HeadQuarter", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if ((selectedUnit == null || selectedUnit.Definition == null) && !terrainProbe.IsValid)
            {
                return;
            }

            EnsureStyles();

            var area = new Rect(Screen.width - 376f, 16f, 360f, Screen.height - 32f);
            GUILayout.BeginArea(area, GUI.skin.box);
            var title = selectedUnit != null && selectedUnit.Definition != null
                ? selectedUnit.Definition.UnitName
                : "Terrain Probe";
            var bodyText = BuildStatsText();
            GUILayout.Label(title, headerStyle);
            GUILayout.Label(bodyText, bodyStyle);
            GUILayout.EndArea();
        }

        private void InspectUnderCursor()
        {
            if (Camera.main == null || mouse == null)
            {
                selectedUnit = null;
                terrainProbe = default;
                return;
            }

            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f))
            {
                terrainProbe = default;
                return;
            }

            var hitUnit = hit.collider.GetComponentInParent<BattleUnit>();
            if (hitUnit != null)
            {
                selectedUnit = hitUnit;
                terrainProbe = default;
                LogCurrentSelection();
                return;
            }

            UpdateTerrainProbe(hit.point);
            LogCurrentSelection();
        }

        private void SelectNextPlayerUnit()
        {
            var selectedTeam = selectedUnit != null ? selectedUnit.Team : Team.Blue;
            var teamUnits = BattleUnitRegistry.GetAliveUnits(selectedTeam);
            if (teamUnits.Count == 0)
            {
                selectedUnit = null;
                terrainProbe = default;
                return;
            }

            var nextIndex = 0;
            if (selectedUnit != null)
            {
                var currentIndex = teamUnits.IndexOf(selectedUnit);
                nextIndex = currentIndex >= 0
                    ? (currentIndex + 1) % teamUnits.Count
                    : 0;
            }

            selectedUnit = teamUnits[nextIndex];
            terrainProbe = default;
            LogCurrentSelection();
        }

        private string BuildStatsText()
        {
            return BuildStatsText(richText: true);
        }

        private string BuildStatsText(bool richText)
        {
            var targetBuilder = richText ? builder : plainTextBuilder;
            targetBuilder.Clear();
            if (selectedUnit != null && selectedUnit.Definition != null)
            {
                AppendUnitStats(selectedUnit, targetBuilder, richText);
            }

            if (terrainProbe.IsValid)
            {
                if (targetBuilder.Length > 0)
                {
                    targetBuilder.AppendLine();
                }

                AppendTerrainProbeStats(targetBuilder);
            }

            return targetBuilder.ToString();
        }

        private void AppendUnitStats(BattleUnit unit, StringBuilder targetBuilder, bool richText)
        {
            var definition = unit.Definition;
            var baseTemplate = ResolveBaseTemplate(definition);
            var baseLoadout = baseTemplate != null ? baseTemplate.GetAmmunitionLoadout() : null;

            targetBuilder.AppendLine("Team: " + unit.Team);
            targetBuilder.AppendLine("Mission: " + unit.Mission);
            targetBuilder.AppendLine("Class: " + definition.UnitType);
            targetBuilder.AppendLine("Health: " + unit.CurrentHealth + " / " + FormatInt(definition.MaxHealth, baseTemplate?.MaxHealth, richText));
            targetBuilder.AppendLine("Armor: " + FormatInt(definition.Armor, baseTemplate?.Armor, richText));
            targetBuilder.AppendLine("Speed: " + FormatFloat(definition.Speed, baseTemplate?.Speed, richText) + " (target " + unit.CurrentMoveSpeed.ToString("0.0") + ")");
            targetBuilder.AppendLine("Velocity: " + unit.CurrentVelocity.ToString("0.0"));
            targetBuilder.AppendLine("Move Blocked: " + (unit.IsMovementTemporarilyBlocked ? "yes" : "no"));
            targetBuilder.AppendLine("Nav Agent: " + unit.NavigationAgentTypeName);
            targetBuilder.AppendLine("Path: " + unit.NavigationPathStatus);
            targetBuilder.AppendLine(
                "Path Costs: Grass " + FormatFloat(definition.TerrainPathCostProfile.GetModifier("Grass"), baseTemplate?.TerrainPathCostProfile.GetModifier("Grass"), richText)
                + "  Road " + FormatFloat(definition.TerrainPathCostProfile.GetModifier("Road"), baseTemplate?.TerrainPathCostProfile.GetModifier("Road"), richText));
            targetBuilder.AppendLine("Vision: " + FormatFloat(definition.VisionRange, baseTemplate?.VisionRange, richText));
            targetBuilder.AppendLine("Reload Remaining: " + unit.RemainingReloadTime.ToString("0.0"));
            targetBuilder.AppendLine("Accuracy: " + FormatPercent(definition.Accuracy, baseTemplate?.Accuracy, richText));
            targetBuilder.AppendLine("Fire Reliability: " + FormatPercent(definition.FireReliability, baseTemplate?.FireReliability, richText));
            targetBuilder.AppendLine("Move Reliability: " + FormatPercent(definition.MoveReliability, baseTemplate?.MoveReliability, richText));
            targetBuilder.AppendLine();
            targetBuilder.AppendLine("Ammunition:");

            var ammunition = definition.Ammunition;
            if (ammunition == null || ammunition.Length == 0)
            {
                targetBuilder.AppendLine("  None");
                return;
            }

            for (var i = 0; i < ammunition.Length; i++)
            {
                var ammo = ammunition[i];
                if (ammo == null)
                {
                    continue;
                }

                var baseAmmo = baseLoadout != null && i < baseLoadout.Length ? baseLoadout[i].Definition : null;
                var baseAmmoCount = baseLoadout != null && i < baseLoadout.Length ? baseLoadout[i].AmmunitionCount : (int?)null;
                var count = unit.GetAmmoRemaining(i);
                targetBuilder.AppendLine("  " + ammo.AmmoName + " [" + FormatAmmoCount(count, baseAmmoCount, richText) + "]");
                targetBuilder.AppendLine(
                    "    Dmg " + FormatDamageRange(ammo.DamageMin, ammo.DamageMax, baseAmmo?.DamageMin, baseAmmo?.DamageMax, richText)
                    + "  Rad " + FormatFloat(ammo.Radius, baseAmmo?.Radius, richText)
                    + "  Rng " + FormatFloat(ammo.AttackRange, baseAmmo?.AttackRange, richText)
                    + "  Rld " + FormatFloat(ammo.ReloadTime, baseAmmo?.ReloadTime, richText));
                targetBuilder.AppendLine(
                    "    Acc " + FormatPercent(ammo.Accuracy, baseAmmo?.Accuracy, richText)
                    + "  DmgRel " + FormatPercent(ammo.DamageReliability, baseAmmo?.DamageReliability, richText));
            }
        }

        private void AppendTerrainProbeStats(StringBuilder targetBuilder)
        {
            targetBuilder.AppendLine("Terrain Probe:");
            targetBuilder.AppendLine("Pos: " + terrainProbe.Position.x.ToString("0.0") + ", " + terrainProbe.Position.z.ToString("0.0"));
            targetBuilder.AppendLine("Terrain: " + terrainProbe.TerrainType);
            targetBuilder.AppendLine("Nav Area: " + terrainProbe.NavAreaName + " (" + terrainProbe.NavAreaIndex + ")");

            if (selectedUnit != null)
            {
                targetBuilder.AppendLine("Area Cost: " + terrainProbe.AreaCost.ToString("0.0"));
                targetBuilder.AppendLine("Path To Probe: " + (terrainProbe.HasPath ? terrainProbe.PathStatus.ToString() : "Unavailable"));
                if (terrainProbe.HasPath)
                {
                    targetBuilder.AppendLine("Path Length: " + terrainProbe.PathLength.ToString("0.0"));
                }
            }
        }

        private static string FormatDamageRange(int valueMin, int valueMax, int? baseMin, int? baseMax, bool richText)
        {
            var current = valueMin == valueMax ? valueMin.ToString() : valueMin + "-" + valueMax;
            if (!baseMin.HasValue || !baseMax.HasValue)
            {
                return current;
            }

            var reference = baseMin.Value == baseMax.Value ? baseMin.Value.ToString() : baseMin.Value + "-" + baseMax.Value;
            if (valueMin == baseMin.Value && valueMax == baseMax.Value)
            {
                return current;
            }

            if (!richText)
            {
                return current + " (" + reference + ")";
            }

            var isImproved = valueMin >= baseMin.Value && valueMax >= baseMax.Value && (valueMin > baseMin.Value || valueMax > baseMax.Value);
            var isReduced = valueMin <= baseMin.Value && valueMax <= baseMax.Value && (valueMin < baseMin.Value || valueMax < baseMax.Value);
            if (isImproved)
            {
                return "<color=#7ec8ff>" + current + "</color> (" + reference + ")";
            }

            if (isReduced)
            {
                return "<color=#ff6d6d>" + current + "</color> (" + reference + ")";
            }

            return current + " (" + reference + ")";
        }

        private void UpdateTerrainProbe(Vector3 hitPoint)
        {
            var navigationManager = BattleNavigationManager.Instance;
            terrainProbe = new TerrainProbeInfo
            {
                IsValid = true,
                Position = hitPoint,
                TerrainType = navigationManager != null ? navigationManager.GetTerrainType(hitPoint) : "Unknown",
                NavAreaName = "Unknown",
                NavAreaIndex = -1,
                PathStatus = NavMeshPathStatus.PathInvalid,
                PathLength = 0f,
                HasPath = false,
                AreaCost = 1f
            };

            if (selectedUnit == null)
            {
                return;
            }

            if (navigationManager != null
                && navigationManager.TryGetNavArea(hitPoint, selectedUnit.NavigationAgentTypeId, out var areaIndex, out var areaName))
            {
                terrainProbe.NavAreaIndex = areaIndex;
                terrainProbe.NavAreaName = areaName;
                terrainProbe.AreaCost = selectedUnit.GetAreaCost(areaIndex);
            }

            if (selectedUnit.TryCalculatePathTo(hitPoint, out var pathStatus, out var pathLength))
            {
                terrainProbe.HasPath = true;
                terrainProbe.PathStatus = pathStatus;
                terrainProbe.PathLength = pathLength;
            }
        }

        private void LogCurrentSelection()
        {
            if (selectedUnit == null && !terrainProbe.IsValid)
            {
                return;
            }

            var title = selectedUnit != null && selectedUnit.Definition != null
                ? selectedUnit.Definition.UnitName
                : "Terrain Probe";
            UiDebugConsole.LogIfEnabled("InspectorClick", title + "\n" + BuildStatsText(richText: false));
        }

        private void UpdateSelectionMarker()
        {
            if (selectedUnit == null || !selectedUnit.IsAlive)
            {
                if (selectionMarker != null)
                {
                    selectionMarker.SetActive(false);
                }

                return;
            }

            EnsureSelectionMarker();
            var bounds = CalculateSelectionBounds(selectedUnit.gameObject);
            var radius = Mathf.Max(0.8f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.35f);
            var markerColor = selectedUnit.Team == Team.Blue
                ? new Color(0.25f, 0.85f, 1f, 0.9f)
                : new Color(1f, 0.35f, 0.25f, 0.9f);
            selectionMarker.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.06f, bounds.center.z);
            if (selectionRing != null)
            {
                selectionRing.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
                if (selectionRing.TryGetComponent<Renderer>(out var ringRenderer))
                {
                    ringRenderer.material.color = markerColor;
                }
            }

            if (selectionBeacon != null)
            {
                selectionBeacon.localPosition = new Vector3(0f, bounds.size.y + 1.2f, 0f);
                selectionBeacon.localScale = Vector3.one * Mathf.Max(0.35f, radius * 0.3f);
                if (selectionBeacon.TryGetComponent<Renderer>(out var beaconRenderer))
                {
                    beaconRenderer.material.color = markerColor;
                }
            }

            selectionMarker.SetActive(true);
        }

        private void EnsureSelectionMarker()
        {
            if (selectionMarker != null)
            {
                return;
            }

            selectionMarker = new GameObject("SelectionMarker");
            selectionMarker.hideFlags = HideFlags.HideAndDontSave;
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                selectionMarker.layer = ignoreRaycastLayer;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(selectionMarker.transform, false);
            ring.transform.localPosition = Vector3.zero;
            Object.Destroy(ring.GetComponent<Collider>());
            var ringRenderer = ring.GetComponent<Renderer>();
            ringRenderer.material = new Material(Shader.Find("Sprites/Default"));
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            selectionRing = ring.transform;

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon";
            beacon.transform.SetParent(selectionMarker.transform, false);
            Object.Destroy(beacon.GetComponent<Collider>());
            var beaconRenderer = beacon.GetComponent<Renderer>();
            beaconRenderer.material = new Material(Shader.Find("Sprites/Default"));
            beaconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beaconRenderer.receiveShadows = false;
            selectionBeacon = beacon.transform;
        }

        private static Bounds CalculateSelectionBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var combinedBounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                return combinedBounds;
            }

            var colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                var combinedBounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; i++)
                {
                    combinedBounds.Encapsulate(colliders[i].bounds);
                }

                return combinedBounds;
            }

            return new Bounds(target.transform.position, Vector3.one);
        }

        private static string ToPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private GameUnitTemplate ResolveBaseTemplate(UnitDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TemplateId))
            {
                return null;
            }

            gameDataCatalog ??= GameDataCatalogLoader.Load();
            return gameDataCatalog != null && gameDataCatalog.TryGetUnitTemplate(definition.TemplateId, out var template)
                ? template
                : null;
        }

        private static string FormatInt(int currentValue, int? baseValue, bool richText)
        {
            if (!baseValue.HasValue || currentValue == baseValue.Value)
            {
                return currentValue.ToString();
            }

            var baseText = baseValue.Value.ToString();
            if (!richText)
            {
                return currentValue + " (" + baseText + ")";
            }

            var color = currentValue > baseValue.Value ? IncreasedColor : ReducedColor;
            return "<color=" + color + ">" + currentValue + "</color> (" + baseText + ")";
        }

        private static string FormatFloat(float currentValue, float? baseValue, bool richText)
        {
            return FormatComparison(currentValue.ToString("0.0"), currentValue, baseValue, richText, baseValue?.ToString("0.0"));
        }

        private static string FormatPercent(float currentValue, float? baseValue, bool richText)
        {
            return FormatComparison(ToPercent(currentValue), currentValue, baseValue, richText, baseValue.HasValue ? ToPercent(baseValue.Value) : null);
        }

        private static string FormatAmmoCount(int currentValue, int? baseValue, bool richText)
        {
            var currentText = currentValue < 0 ? "inf" : currentValue.ToString();
            var baseText = !baseValue.HasValue ? null : (baseValue.Value < 0 ? "inf" : baseValue.Value.ToString());
            return FormatComparison(currentText, currentValue, baseValue, richText, baseText);
        }

        private static string FormatComparison(string currentText, float currentValue, float? baseValue, bool richText, string baseText = null)
        {
            if (!baseValue.HasValue || Mathf.Approximately(currentValue, baseValue.Value))
            {
                return currentText;
            }

            var formattedBaseText = string.IsNullOrWhiteSpace(baseText) ? baseValue.Value.ToString("0.0") : baseText;
            if (!richText)
            {
                return currentText + " (" + formattedBaseText + ")";
            }

            var color = currentValue > baseValue.Value ? IncreasedColor : ReducedColor;
            return "<color=" + color + ">" + currentText + "</color> (" + formattedBaseText + ")";
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
            {
                return;
            }

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                richText = true
            };
        }

        private void OnDestroy()
        {
            if (selectionMarker != null)
            {
                Destroy(selectionMarker);
            }
        }
    }
}
