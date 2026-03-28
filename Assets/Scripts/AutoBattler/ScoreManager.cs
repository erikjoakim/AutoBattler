using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class ScoreManager : MonoBehaviour
    {
        private static readonly Dictionary<Team, int> Scores = new Dictionary<Team, int>
        {
            { Team.Blue, 0 },
            { Team.Red, 0 }
        };

        public static ScoreManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetScores();
        }

        public void AddPoint(Team team, int amount = 1)
        {
            Scores[team] += amount;
        }

        public int GetScore(Team team)
        {
            return Scores[team];
        }

        public void ResetScores()
        {
            Scores[Team.Blue] = 0;
            Scores[Team.Red] = 0;
        }
    }
}
