using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public static class GameDataCatalogLoader
    {
        private const string AmmoCatalogPath = "GameData/GameAmmo";
        private const string UnitCatalogPath = "GameData/GameUnits";

        public static GameDataCatalog Load()
        {
            var ammoAsset = Resources.Load<TextAsset>(AmmoCatalogPath);
            var unitAsset = Resources.Load<TextAsset>(UnitCatalogPath);

            if (ammoAsset == null || unitAsset == null)
            {
                Debug.LogWarning("GameData catalogs are missing. Using built-in fallback data.");
                return GameDataCatalog.CreateDefault();
            }

            var ammoTemplates = ParseAmmoCatalog(ammoAsset.text);
            var unitTemplates = ParseUnitCatalog(unitAsset.text);
            if (ammoTemplates.Count == 0 || unitTemplates.Count == 0)
            {
                Debug.LogWarning("GameData catalogs were invalid. Using built-in fallback data.");
                return GameDataCatalog.CreateDefault();
            }

            return new GameDataCatalog(ammoTemplates, unitTemplates);
        }

        private static Dictionary<string, GameAmmoTemplate> ParseAmmoCatalog(string json)
        {
            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(json));
            var items = JsonDataHelper.GetArray(root, "ammunition");
            var templates = new Dictionary<string, GameAmmoTemplate>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var ammoType = JsonDataHelper.GetString(item, "ammoType", string.Empty);
                if (string.IsNullOrWhiteSpace(ammoType))
                {
                    continue;
                }

                templates[ammoType] = new GameAmmoTemplate(
                    ammoType,
                    JsonDataHelper.GetString(item, "ammoName", ammoType),
                    JsonDataHelper.GetEnum(item, "requiredUserType", UnitType.Infantry),
                    Mathf.Max(0, JsonDataHelper.GetInt(item, "damage", 0)),
                    Mathf.Max(0f, JsonDataHelper.GetFloat(item, "radius", 0f)),
                    JsonDataHelper.GetInt(item, "ammunitionCount", -1));
            }

            return templates;
        }

        private static Dictionary<string, GameUnitTemplate> ParseUnitCatalog(string json)
        {
            var root = JsonDataHelper.AsObject(MiniJson.Deserialize(json));
            var items = JsonDataHelper.GetArray(root, "units");
            var templates = new Dictionary<string, GameUnitTemplate>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < items.Count; i++)
            {
                var item = JsonDataHelper.AsObject(items[i]);
                if (item == null)
                {
                    continue;
                }

                var unitTypeKey = JsonDataHelper.GetString(item, "unitType", string.Empty);
                if (string.IsNullOrWhiteSpace(unitTypeKey))
                {
                    continue;
                }

                var ammunitionRefs = JsonDataHelper.GetArray(item, "ammunition");
                var ammoTypes = new List<string>();
                for (var ammoIndex = 0; ammoIndex < ammunitionRefs.Count; ammoIndex++)
                {
                    var ammoRef = ammunitionRefs[ammoIndex];
                    if (ammoRef is string directAmmoType)
                    {
                        ammoTypes.Add(directAmmoType);
                        continue;
                    }

                    var ammoObject = JsonDataHelper.AsObject(ammoRef);
                    if (ammoObject == null)
                    {
                        continue;
                    }

                    var ammoType = JsonDataHelper.GetString(ammoObject, "ammoType", string.Empty);
                    if (!string.IsNullOrWhiteSpace(ammoType))
                    {
                        ammoTypes.Add(ammoType);
                    }
                }

                templates[unitTypeKey] = new GameUnitTemplate(
                    unitTypeKey,
                    JsonDataHelper.GetString(item, "unitName", unitTypeKey),
                    JsonDataHelper.GetEnum(item, "classType", UnitType.Infantry),
                    JsonDataHelper.GetEnum(item, "mission", MissionType.SeekAndDestroy),
                    Mathf.Max(1, JsonDataHelper.GetInt(item, "maxHealth", 1)),
                    Mathf.Max(0, JsonDataHelper.GetInt(item, "armor", 0)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "visionRange", 5f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "attackRange", 3f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "speed", 3f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "reloadTime", 1f)),
                    JsonDataHelper.GetString(item, "navigationAgentType", string.Empty),
                    ammoTypes.ToArray());
            }

            return templates;
        }
    }
}
