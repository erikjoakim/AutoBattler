using UnityEngine;

namespace AutoBattler
{
    public sealed class ScoreHud : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle splashTitleStyle;
        private GUIStyle splashMessageStyle;
        private GUIStyle splashPanelStyle;

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(16f, 16f, 300f, 150f), GUI.skin.box);
            GUILayout.Label("AutoBattler", headerStyle);

            if (ScoreManager.Instance == null)
            {
                GUILayout.Label("Waiting for score manager...", bodyStyle);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(
                "Blue  score: " + ScoreManager.Instance.GetScore(Team.Blue) + "  alive: " + BattleUnitRegistry.CountAlive(Team.Blue),
                bodyStyle);
            GUILayout.Label(
                "Red   score: " + ScoreManager.Instance.GetScore(Team.Red) + "  alive: " + BattleUnitRegistry.CountAlive(Team.Red),
                bodyStyle);

            GUILayout.Space(8f);
            if (BattleScenario.Instance != null)
            {
                GUILayout.Label("Mission: " + BattleScenario.Instance.MissionName, bodyStyle);
                GUILayout.Label("Objective: " + BattleScenario.Instance.GetObjectiveSummary(), bodyStyle);
                var progressSummary = BattleScenario.Instance.GetProgressSummary();
                if (!string.IsNullOrWhiteSpace(progressSummary))
                {
                    GUILayout.Label(progressSummary, bodyStyle);
                }
            }
            else if (BattleObjectiveManager.Instance != null)
            {
                GUILayout.Label("Objective: " + BattleObjectiveManager.Instance.GetObjectiveSummary(), bodyStyle);
            }
            else
            {
                GUILayout.Label("Objective: eliminate all enemies", bodyStyle);
            }

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                GUILayout.Space(8f);
                GUILayout.Label(BattleStateManager.Instance.ResultMessage, headerStyle);
            }

            GUILayout.EndArea();

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                DrawBattleResultSplash();
            }
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
                fontStyle = FontStyle.Bold
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };

            splashTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            splashMessageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            splashPanelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                padding = new RectOffset(24, 24, 24, 24)
            };
        }

        private void DrawBattleResultSplash()
        {
            var battleState = BattleStateManager.Instance;
            if (battleState == null)
            {
                return;
            }

            var blueWon = battleState.Winner.HasValue && battleState.Winner.Value == Team.Blue;
            var title = string.IsNullOrWhiteSpace(battleState.WinnerTitle)
                ? (blueWon ? "Blue Wins" : "Red Wins")
                : battleState.WinnerTitle;
            var perspectiveText = blueWon ? "Victory" : "Defeat";
            var titleColor = blueWon
                ? new Color(0.32f, 0.8f, 1f)
                : new Color(1f, 0.42f, 0.42f);
            var overlayRect = new Rect(0f, 0f, Screen.width, Screen.height);
            var panelWidth = Mathf.Min(520f, Screen.width - 48f);
            var panelHeight = 220f;
            var panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.36f);
            GUI.DrawTexture(overlayRect, Texture2D.whiteTexture);

            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.Box(panelRect, GUIContent.none, splashPanelStyle);

            var previousTitleColor = splashTitleStyle.normal.textColor;
            var previousMessageColor = splashMessageStyle.normal.textColor;
            var previousBodyColor = bodyStyle.normal.textColor;
            splashTitleStyle.normal.textColor = titleColor;
            splashMessageStyle.normal.textColor = Color.white;
            bodyStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);

            GUILayout.BeginArea(panelRect);
            GUILayout.FlexibleSpace();
            GUILayout.Label(title, splashTitleStyle);
            GUILayout.Space(6f);
            GUILayout.Label(perspectiveText, splashMessageStyle);
            GUILayout.Space(12f);
            GUILayout.Label(battleState.ResultMessage, splashMessageStyle);
            GUILayout.Space(10f);
            GUILayout.Label("Left-click units to inspect final stats.", bodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();

            splashTitleStyle.normal.textColor = previousTitleColor;
            splashMessageStyle.normal.textColor = previousMessageColor;
            bodyStyle.normal.textColor = previousBodyColor;
            GUI.color = previousColor;
        }
    }
}
