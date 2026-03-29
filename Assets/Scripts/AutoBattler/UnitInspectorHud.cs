using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoBattler
{
    public sealed class UnitInspectorHud : MonoBehaviour
    {
        private readonly StringBuilder builder = new StringBuilder(512);
        private BattleUnit selectedUnit;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private Mouse mouse;

        private void Update()
        {
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
                SelectUnitUnderCursor();
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                selectedUnit = null;
            }
        }

        private void OnGUI()
        {
            if (selectedUnit == null || selectedUnit.Definition == null)
            {
                return;
            }

            EnsureStyles();

            var area = new Rect(Screen.width - 376f, 16f, 360f, Screen.height - 32f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(selectedUnit.Definition.UnitName, headerStyle);
            GUILayout.Label(BuildStatsText(selectedUnit), bodyStyle);
            GUILayout.EndArea();
        }

        private void SelectUnitUnderCursor()
        {
            if (Camera.main == null || mouse == null)
            {
                selectedUnit = null;
                return;
            }

            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f))
            {
                selectedUnit = null;
                return;
            }

            selectedUnit = hit.collider.GetComponentInParent<BattleUnit>();
        }

        private string BuildStatsText(BattleUnit unit)
        {
            builder.Clear();
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
                return builder.ToString();
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

            return builder.ToString();
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
