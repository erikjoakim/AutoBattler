using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class BattleCampaignBridge : MonoBehaviour
    {
        public static BattleCampaignBridge Instance { get; private set; }

        private readonly HashSet<string> deadUnitCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool resultSubmitted;
        private bool returnRequested;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            BattleUnit.UnitDied += HandleUnitDied;
        }

        private void OnDisable()
        {
            BattleUnit.UnitDied -= HandleUnitDied;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "HeadQuarter", StringComparison.OrdinalIgnoreCase))
            {
                Destroy(this);
                return;
            }

            if (CampaignRuntimeContext.Instance == null || !CampaignRuntimeContext.Instance.HasActiveMission)
            {
                return;
            }

            if (!resultSubmitted)
            {
                if (BattleStateManager.Instance == null || !BattleStateManager.Instance.IsBattleOver)
                {
                    return;
                }

                SubmitBattleResult();
            }

            if (returnRequested)
            {
                returnRequested = false;
                SceneLoadUtility.LoadScene("HeadQuarter");
            }
        }

        private void HandleUnitDied(BattleUnit unit, BattleUnit attacker)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.OwnedUnitCardId))
            {
                return;
            }

            deadUnitCardIds.Add(unit.OwnedUnitCardId);
        }

        private void SubmitBattleResult()
        {
            var mission = CampaignRuntimeContext.Instance.ActiveMission;
            if (mission == null)
            {
                return;
            }

            var result = new BattleResultData
            {
                victory = BattleStateManager.Instance != null && BattleStateManager.Instance.Winner == Team.Blue,
                sceneName = gameObject.scene.name,
                hexSlotId = mission.hexSlotId,
                resultMessage = BattleStateManager.Instance != null ? BattleStateManager.Instance.ResultMessage : "Battle resolved."
            };

            for (var i = 0; i < mission.selectedUnitCardIds.Count; i++)
            {
                var unitCardId = mission.selectedUnitCardIds[i];
                if (deadUnitCardIds.Contains(unitCardId))
                {
                    result.deadUnitCardIds.Add(unitCardId);
                }
                else
                {
                    result.survivingUnitCardIds.Add(unitCardId);
                }
            }

            CampaignRuntimeContext.Instance.SetPendingBattleResult(result);
            resultSubmitted = true;
        }

        public void RequestReturnToHeadQuarter()
        {
            if (!resultSubmitted)
            {
                SubmitBattleResult();
            }

            returnRequested = true;
        }
    }
}
