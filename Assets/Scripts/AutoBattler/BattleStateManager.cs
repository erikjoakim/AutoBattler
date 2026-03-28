using UnityEngine;

namespace AutoBattler
{
    public sealed class BattleStateManager : MonoBehaviour
    {
        public static BattleStateManager Instance { get; private set; }

        public bool IsBattleOver { get; private set; }
        public Team? Winner { get; private set; }
        public string ResultMessage { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetBattle();
        }

        public void ResetBattle()
        {
            IsBattleOver = false;
            Winner = null;
            ResultMessage = string.Empty;
        }

        public void EndBattle(Team winner, string resultMessage)
        {
            if (IsBattleOver)
            {
                return;
            }

            IsBattleOver = true;
            Winner = winner;
            ResultMessage = resultMessage;
        }
    }
}
