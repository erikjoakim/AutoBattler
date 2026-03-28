using UnityEngine;

namespace AutoBattler
{
    public sealed class BattleObjectiveManager : MonoBehaviour
    {
        private const float CaptureRadius = 2.5f;

        private Vector3 blueStartPoint;
        private Vector3 redStartPoint;
        private bool isInitialized;

        public Vector3 BlueStartPoint => blueStartPoint;
        public Vector3 RedStartPoint => redStartPoint;

        public void Initialize(Vector3 blueSpawnPoint, Vector3 redSpawnPoint)
        {
            blueStartPoint = blueSpawnPoint;
            redStartPoint = redSpawnPoint;
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized || BattleStateManager.Instance == null || BattleStateManager.Instance.IsBattleOver)
            {
                return;
            }

            if (BattleUnitRegistry.CountAlive(Team.Blue) == 0)
            {
                BattleStateManager.Instance.EndBattle(Team.Red, "Red wins by elimination");
                return;
            }

            if (BattleUnitRegistry.CountAlive(Team.Red) == 0)
            {
                BattleStateManager.Instance.EndBattle(Team.Blue, "Blue wins by elimination");
                return;
            }

            if (BattleUnitRegistry.IsTeamOccupyingRadius(Team.Blue, redStartPoint, CaptureRadius))
            {
                BattleStateManager.Instance.EndBattle(Team.Blue, "Blue captured StartPoint2");
                return;
            }

            if (BattleUnitRegistry.IsTeamOccupyingRadius(Team.Red, blueStartPoint, CaptureRadius))
            {
                BattleStateManager.Instance.EndBattle(Team.Red, "Red captured StartPoint1");
            }
        }

        private void OnDrawGizmos()
        {
            if (!isInitialized)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(blueStartPoint, CaptureRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(redStartPoint, CaptureRadius);
        }
    }
}
