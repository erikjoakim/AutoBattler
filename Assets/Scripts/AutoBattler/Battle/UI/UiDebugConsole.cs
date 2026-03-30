using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public static class UiDebugConsole
    {
        private static readonly Dictionary<string, string> LastMessages = new Dictionary<string, string>();

        public static void LogIfEnabled(string channel, string message)
        {
            if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (BattleScenario.Instance == null || !BattleScenario.Instance.DebugLogUiToConsole)
            {
                return;
            }

            if (LastMessages.TryGetValue(channel, out var previousMessage) && previousMessage == message)
            {
                return;
            }

            LastMessages[channel] = message;
            Debug.Log("[UI][" + channel + "]\n" + message);
        }

        public static void Reset()
        {
            LastMessages.Clear();
        }
    }
}
