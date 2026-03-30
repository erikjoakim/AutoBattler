using System;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public static class NavMeshAgentTypeResolver
    {
        public static int GetDefaultAgentTypeId()
        {
            return NavMesh.GetSettingsCount() > 0
                ? NavMesh.GetSettingsByIndex(0).agentTypeID
                : 0;
        }

        public static int ResolveAgentTypeId(string agentTypeName)
        {
            if (string.IsNullOrWhiteSpace(agentTypeName))
            {
                return GetDefaultAgentTypeId();
            }

            for (var i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                if (string.Equals(NavMesh.GetSettingsNameFromID(settings.agentTypeID), agentTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return settings.agentTypeID;
                }
            }

            Debug.LogWarning("Unknown NavMesh agent type: " + agentTypeName + ". Falling back to the default agent type.");
            return GetDefaultAgentTypeId();
        }
    }
}
