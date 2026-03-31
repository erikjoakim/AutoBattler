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
        private readonly List<AwardedUnitCardData> capturedUnitCards = new List<AwardedUnitCardData>();

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
            if (unit == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(unit.OwnedUnitCardId))
            {
                deadUnitCardIds.Add(unit.OwnedUnitCardId);
            }

            if (unit.CaptureAsUnitCardOnDeath
                && string.IsNullOrWhiteSpace(unit.OwnedUnitCardId)
                && unit.Team == Team.Red
                && attacker != null
                && attacker.Team == Team.Blue)
            {
                capturedUnitCards.Add(BuildAwardedUnitCard(unit, "Captured"));
            }
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

            var survivingBlueUnits = BattleUnitRegistry.GetAliveUnits(Team.Blue);
            for (var i = 0; i < survivingBlueUnits.Count; i++)
            {
                var unit = survivingBlueUnits[i];
                if (unit == null
                    || !unit.ReturnToHeadquartersIfSurvives
                    || !string.IsNullOrWhiteSpace(unit.OwnedUnitCardId))
                {
                    continue;
                }

                result.awardedUnitCards.Add(BuildAwardedUnitCard(unit, "Recovered"));
            }

            if (result.victory && capturedUnitCards.Count > 0)
            {
                result.awardedUnitCards.AddRange(capturedUnitCards);
            }

            BattleLootManager.Instance?.PopulateBattleResult(result, result.victory);
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

        private static AwardedUnitCardData BuildAwardedUnitCard(BattleUnit unit, string sourceLabel)
        {
            return new AwardedUnitCardData
            {
                displayName = unit != null && unit.Definition != null ? unit.Definition.UnitName : "Recovered Unit",
                baseTemplateId = unit != null && unit.Definition != null ? unit.Definition.TemplateId : string.Empty,
                overrideJson = unit != null ? unit.PersistentOverrideJson : string.Empty,
                sourceLabel = sourceLabel
            };
        }
    }
}
