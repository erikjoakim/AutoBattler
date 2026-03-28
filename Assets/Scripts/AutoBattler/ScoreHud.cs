using UnityEngine;

namespace AutoBattler
{
    public sealed class ScoreHud : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;

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
            GUILayout.Label("Objective: occupy the opposing start point", bodyStyle);

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                GUILayout.Space(8f);
                GUILayout.Label(BattleStateManager.Instance.ResultMessage, headerStyle);
            }

            GUILayout.EndArea();
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
        }
    }
}
