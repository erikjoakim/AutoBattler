using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class UnitInspectorHud : MonoBehaviour
    {
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
        private BattleUnit selectedUnit;
        private TerrainProbeInfo terrainProbe;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private Mouse mouse;

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
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                InspectUnderCursor();
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                selectedUnit = null;
                terrainProbe = default;
            }
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

        private string BuildStatsText()
        {
            builder.Clear();
            if (selectedUnit != null && selectedUnit.Definition != null)
            {
                AppendUnitStats(selectedUnit);
            }

            if (terrainProbe.IsValid)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                AppendTerrainProbeStats();
            }

            return builder.ToString();
        }

        private void AppendUnitStats(BattleUnit unit)
        {
            var definition = unit.Definition;
            builder.AppendLine("Team: " + unit.Team);
            builder.AppendLine("Mission: " + unit.Mission);
            builder.AppendLine("Class: " + definition.UnitType);
            builder.AppendLine("Health: " + unit.CurrentHealth + " / " + definition.MaxHealth);
            builder.AppendLine("Armor: " + definition.Armor);
            builder.AppendLine("Speed: " + definition.Speed.ToString("0.0") + " (target " + unit.CurrentMoveSpeed.ToString("0.0") + ")");
            builder.AppendLine("Velocity: " + unit.CurrentVelocity.ToString("0.0"));
            builder.AppendLine("Move Blocked: " + (unit.IsMovementTemporarilyBlocked ? "yes" : "no"));
            builder.AppendLine("Nav Agent: " + unit.NavigationAgentTypeName);
            builder.AppendLine("Path: " + unit.NavigationPathStatus);
            builder.AppendLine(
                "Path Costs: Grass " + definition.TerrainPathCostProfile.GetModifier("Grass").ToString("0.0")
                + "  Road " + definition.TerrainPathCostProfile.GetModifier("Road").ToString("0.0"));
            builder.AppendLine("Vision: " + definition.VisionRange.ToString("0.0"));
            builder.AppendLine("Reload Remaining: " + unit.RemainingReloadTime.ToString("0.0"));
            builder.AppendLine("Accuracy: " + ToPercent(definition.Accuracy));
            builder.AppendLine("Fire Reliability: " + ToPercent(definition.FireReliability));
            builder.AppendLine("Move Reliability: " + ToPercent(definition.MoveReliability));
            builder.AppendLine();
            builder.AppendLine("Ammunition:");

            var ammunition = definition.Ammunition;
            if (ammunition == null || ammunition.Length == 0)
            {
                builder.AppendLine("  None");
                return;
            }

            for (var i = 0; i < ammunition.Length; i++)
            {
                var ammo = ammunition[i];
                if (ammo == null)
                {
                    continue;
                }

                var count = unit.GetAmmoRemaining(i);
                builder.AppendLine("  " + ammo.AmmoName + " [" + (count < 0 ? "inf" : count.ToString()) + "]");
                builder.AppendLine(
                    "    Dmg " + ammo.Damage
                    + "  Rad " + ammo.Radius.ToString("0.0")
                    + "  Rng " + ammo.AttackRange.ToString("0.0")
                    + "  Rld " + ammo.ReloadTime.ToString("0.0"));
                builder.AppendLine(
                    "    Acc " + ToPercent(ammo.Accuracy)
                    + "  DmgRel " + ToPercent(ammo.DamageReliability));
            }
        }

        private void AppendTerrainProbeStats()
        {
            builder.AppendLine("Terrain Probe:");
            builder.AppendLine("Pos: " + terrainProbe.Position.x.ToString("0.0") + ", " + terrainProbe.Position.z.ToString("0.0"));
            builder.AppendLine("Terrain: " + terrainProbe.TerrainType);
            builder.AppendLine("Nav Area: " + terrainProbe.NavAreaName + " (" + terrainProbe.NavAreaIndex + ")");

            if (selectedUnit != null)
            {
                builder.AppendLine("Area Cost: " + terrainProbe.AreaCost.ToString("0.0"));
                builder.AppendLine("Path To Probe: " + (terrainProbe.HasPath ? terrainProbe.PathStatus.ToString() : "Unavailable"));
                if (terrainProbe.HasPath)
                {
                    builder.AppendLine("Path Length: " + terrainProbe.PathLength.ToString("0.0"));
                }
            }
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
            UiDebugConsole.LogIfEnabled("InspectorClick", title + "\n" + BuildStatsText());
        }

        private static string ToPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
            {
                return;
            }

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = false
            };
        }
    }
}
