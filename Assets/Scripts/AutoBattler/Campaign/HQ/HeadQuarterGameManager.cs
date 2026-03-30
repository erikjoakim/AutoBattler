using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoBattler
{
    public sealed class HeadQuarterGameManager : MonoBehaviour
    {
        [SerializeField] private bool loadExistingSaveFile = true;

        private void Awake()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, "HeadQuarter", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CampaignRuntimeContext.Instance?.ApplyHeadQuarterStartupSettings(loadExistingSaveFile);
        }
    }
}
