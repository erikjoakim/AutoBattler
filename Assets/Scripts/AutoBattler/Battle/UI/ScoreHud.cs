using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class ScoreHud : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle splashTitleStyle;
        private GUIStyle splashMessageStyle;
        private GUIStyle splashPanelStyle;
        private GUIStyle splashButtonStyle;
        private Vector2 lootScrollPosition;

        private void OnGUI()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "HeadQuarter", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(new Rect(16f, 16f, 380f, 240f), GUI.skin.box);
            GUILayout.Label("AutoBattler", headerStyle);

            if (ScoreManager.Instance == null)
            {
                GUILayout.Label("Waiting for score manager...", bodyStyle);
                GUILayout.EndArea();
                return;
            }

            var blueSummary = "Blue  score: " + ScoreManager.Instance.GetScore(Team.Blue) + "  alive: " + BattleUnitRegistry.CountAlive(Team.Blue);
            var redSummary = "Red   score: " + ScoreManager.Instance.GetScore(Team.Red) + "  alive: " + BattleUnitRegistry.CountAlive(Team.Red);
            GUILayout.Label(blueSummary, bodyStyle);
            GUILayout.Label(redSummary, bodyStyle);

            GUILayout.Space(8f);
            DrawGameSpeedControls();
            GUILayout.Space(8f);
            if (BattleScenario.Instance != null)
            {
                var missionSummary = "Mission: " + BattleScenario.Instance.MissionName;
                var descriptionSummary = string.IsNullOrWhiteSpace(BattleScenario.Instance.MissionDescription)
                    ? string.Empty
                    : "Briefing: " + BattleScenario.Instance.MissionDescription;
                var objectiveSummary = "Primary: " + BattleScenario.Instance.GetPrimaryObjectiveSummary();
                var loseSummary = "Failure: " + BattleScenario.Instance.GetLoseConditionSummary();
                var tagSummary = "Scenario: " + BattleScenario.Instance.GetScenarioTagSummary();
                var spawnerSummary = "Spawners: " + (BattleScenario.Instance.HasSpawnerPresence() ? "Present" : "None detected");
                GUILayout.Label(missionSummary, bodyStyle);
                if (!string.IsNullOrWhiteSpace(descriptionSummary))
                {
                    GUILayout.Label(descriptionSummary, bodyStyle);
                }
                GUILayout.Label(objectiveSummary, bodyStyle);
                GUILayout.Label(loseSummary, bodyStyle);
                GUILayout.Label(tagSummary, bodyStyle);
                GUILayout.Label(spawnerSummary, bodyStyle);
                var progressSummary = BattleScenario.Instance.GetProgressSummary();
                if (!string.IsNullOrWhiteSpace(progressSummary))
                {
                    GUILayout.Label("Progress: " + progressSummary, bodyStyle);
                }
            }
            else if (BattleObjectiveManager.Instance != null)
            {
                var objectiveSummary = "Objective: " + BattleObjectiveManager.Instance.GetObjectiveSummary();
                GUILayout.Label(objectiveSummary, bodyStyle);
            }
            else
            {
                const string objectiveSummary = "Objective: eliminate all enemies";
                GUILayout.Label(objectiveSummary, bodyStyle);
            }

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                GUILayout.Space(8f);
                GUILayout.Label(BattleStateManager.Instance.ResultMessage, headerStyle);
            }

            GUILayout.EndArea();

            DrawLootFeed();

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                DrawBattleResultSplash();
            }
        }

        private void DrawGameSpeedControls()
        {
            if (GameSpeedController.Instance == null)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed: " + GameSpeedController.Instance.CurrentSpeed.ToString("0.0") + "x", bodyStyle, GUILayout.Width(92f));

            DrawSpeedButton("0.5x", 0.5f);
            DrawSpeedButton("1x", 1f);
            DrawSpeedButton("2x", 2f);
            DrawSpeedButton("4x", 4f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Keys: 1-4 or +/-", bodyStyle);
        }

        private void DrawSpeedButton(string label, float speed)
        {
            if (GUILayout.Button(label, GUILayout.Width(42f)))
            {
                GameSpeedController.Instance.SetSpeedByValue(speed);
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

            splashButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            splashButtonStyle.normal.textColor = Color.white;
            splashButtonStyle.hover.textColor = Color.white;
            splashButtonStyle.active.textColor = Color.white;
            splashButtonStyle.focused.textColor = Color.white;
            splashButtonStyle.onNormal.textColor = Color.white;
            splashButtonStyle.onHover.textColor = Color.white;
            splashButtonStyle.onActive.textColor = Color.white;
            splashButtonStyle.onFocused.textColor = Color.white;
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
            var showReturnButton = CampaignRuntimeContext.Instance != null
                && CampaignRuntimeContext.Instance.HasActiveMission
                && BattleCampaignBridge.Instance != null;
            var panelHeight = showReturnButton ? 272f : 220f;
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
            GUI.color = previousColor;

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
            GUILayout.Label("Ended Because:", splashMessageStyle);
            GUILayout.Space(4f);
            GUILayout.Label(battleState.ResultMessage, splashMessageStyle);
            GUILayout.Space(10f);
            GUILayout.Label("Left-click units to inspect final stats.", bodyStyle);
            if (showReturnButton)
            {
                GUILayout.Space(18f);
                if (GUILayout.Button("Return To HQ", splashButtonStyle, GUILayout.Height(38f)))
                {
                    BattleCampaignBridge.Instance.RequestReturnToHeadQuarter();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();

            splashTitleStyle.normal.textColor = previousTitleColor;
            splashMessageStyle.normal.textColor = previousMessageColor;
            bodyStyle.normal.textColor = previousBodyColor;
            GUI.color = previousColor;
        }

        private void DrawLootFeed()
        {
            if (BattleLootManager.Instance == null)
            {
                return;
            }

            var entries = BattleLootManager.Instance.GetRecentDropDisplayEntries();
            GUILayout.BeginArea(new Rect(16f, 266f, 320f, 210f), GUI.skin.box);
            GUILayout.Label("Loot Dropped", headerStyle);
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("No loot dropped yet.", bodyStyle);
                GUILayout.EndArea();
                return;
            }

            lootScrollPosition = GUILayout.BeginScrollView(lootScrollPosition, false, true);
            for (var i = 0; i < entries.Count; i++)
            {
                GUILayout.Label(entries[i], bodyStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}
