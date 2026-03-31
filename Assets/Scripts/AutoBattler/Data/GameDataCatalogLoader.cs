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
            var unitTemplates = ParseUnitCatalog(unitAsset.text, ammoTemplates);
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
                    ResolveAmmoDamageMin(item),
                    ResolveAmmoDamageMax(item),
                    Mathf.Max(0f, JsonDataHelper.GetFloat(item, "radius", 0f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "attackRange", 3f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "reloadTime", 1f)),
                    Mathf.Clamp01(JsonDataHelper.GetFloat(item, "accuracy", 1f)),
                    Mathf.Clamp01(JsonDataHelper.GetFloat(item, "damageReliability", 1f)));
            }

            return templates;
        }

        private static Dictionary<string, GameUnitTemplate> ParseUnitCatalog(string json, Dictionary<string, GameAmmoTemplate> ammoTemplates)
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

                var ammoLoadout = ParseAmmunitionLoadout(item, ammoTemplates);
                var terrainSpeedProfile = ParseTerrainSpeedProfile(item);
                var terrainPathCostProfile = ParseTerrainPathCostProfile(item);
                templates[unitTypeKey] = new GameUnitTemplate(
                    unitTypeKey,
                    JsonDataHelper.GetString(item, "unitName", unitTypeKey),
                    JsonDataHelper.GetEnum(item, "classType", UnitType.Infantry),
                    JsonDataHelper.GetEnum(item, "mission", MissionType.SeekAndDestroy),
                    Mathf.Max(1, JsonDataHelper.GetInt(item, "maxHealth", 1)),
                    Mathf.Max(0, JsonDataHelper.GetInt(item, "armor", 0)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "visionRange", 5f)),
                    Mathf.Max(0.1f, JsonDataHelper.GetFloat(item, "speed", 3f)),
                    Mathf.Clamp01(JsonDataHelper.GetFloat(item, "accuracy", 1f)),
                    Mathf.Clamp01(JsonDataHelper.GetFloat(item, "fireReliability", 1f)),
                    Mathf.Clamp01(JsonDataHelper.GetFloat(item, "moveReliability", 1f)),
                    Mathf.Max(0f, JsonDataHelper.GetFloat(item, "threatValue", 1f)),
                    Mathf.Max(0, JsonDataHelper.GetInt(item, "purchaseCostGold", 10)),
                    JsonDataHelper.GetString(item, "navigationAgentType", string.Empty),
                    terrainSpeedProfile,
                    terrainPathCostProfile,
                    ammoLoadout.ToArray());
            }

            return templates;
        }

        private static List<GameUnitAmmoLoadout> ParseAmmunitionLoadout(Dictionary<string, object> unitObject, Dictionary<string, GameAmmoTemplate> ammoTemplates)
        {
            var loadout = new List<GameUnitAmmoLoadout>();
            var ammunitionRefs = JsonDataHelper.GetArray(unitObject, "ammunition");
            var rangeOverride = unitObject != null && unitObject.TryGetValue("attackRange", out var rangeValue) ? rangeValue : null;
            var reloadOverride = unitObject != null && unitObject.TryGetValue("reloadTime", out var reloadValue) ? reloadValue : null;

            for (var ammoIndex = 0; ammoIndex < ammunitionRefs.Count; ammoIndex++)
            {
                string ammoType;
                Dictionary<string, object> ammoObject = null;

                if (ammunitionRefs[ammoIndex] is string directAmmoType)
                {
                    ammoType = directAmmoType;
                }
                else
                {
                    ammoObject = JsonDataHelper.AsObject(ammunitionRefs[ammoIndex]);
                    if (ammoObject == null)
                    {
                        continue;
                    }

                    ammoType = JsonDataHelper.GetString(ammoObject, "ammoType", string.Empty);
                }

                if (string.IsNullOrWhiteSpace(ammoType))
                {
                    continue;
                }

                if (!ammoTemplates.TryGetValue(ammoType, out var ammoTemplate))
                {
                    Debug.LogWarning("Unknown ammo template in GameUnits catalog: " + ammoType);
                    continue;
                }

                var baseAttackRange = JsonDataHelper.GetModifiedFloat(rangeOverride, ammoTemplate.AttackRange);
                var baseReloadTime = JsonDataHelper.GetModifiedFloat(reloadOverride, ammoTemplate.ReloadTime);
                var baseAccuracy = ammoTemplate.Accuracy;
                var baseDamageReliability = ammoTemplate.DamageReliability;
                var resolvedAttackRange = Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(ammoObject, "attackRange", baseAttackRange));
                var resolvedReloadTime = Mathf.Max(0.1f, JsonDataHelper.GetModifiedFloat(ammoObject, "reloadTime", baseReloadTime));
                var resolvedDamageMin = ResolveModifiedAmmoDamageMin(ammoObject, ammoTemplate.DamageMin);
                var resolvedDamageMax = ResolveModifiedAmmoDamageMax(ammoObject, ammoTemplate.DamageMax, resolvedDamageMin);
                var resolvedDefinition = new AmmoDefinition(
                    JsonDataHelper.GetString(ammoObject, "ammoName", ammoTemplate.AmmoName),
                    ammoObject != null && ammoObject.ContainsKey("requiredUserType")
                        ? JsonDataHelper.GetEnum(ammoObject, "requiredUserType", ammoTemplate.RequiredUserType)
                        : ammoTemplate.RequiredUserType,
                    resolvedDamageMin,
                    resolvedDamageMax,
                    Mathf.Max(0f, JsonDataHelper.GetModifiedFloat(ammoObject, "radius", ammoTemplate.Radius)),
                    resolvedAttackRange,
                    resolvedReloadTime,
                    Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(ammoObject, "accuracy", baseAccuracy)),
                    Mathf.Clamp01(JsonDataHelper.GetModifiedFloat(ammoObject, "damageReliability", baseDamageReliability)));

                var ammunitionCount = ammoObject == null
                    ? -1
                    : ResolveAmmoCount(ammoObject, -1);

                loadout.Add(new GameUnitAmmoLoadout(ammoType, resolvedDefinition, ammunitionCount));
            }

            return loadout;
        }

        private static int ResolveAmmoCount(Dictionary<string, object> ammoObject, int baseAmmoCount)
        {
            var resolvedCount = JsonDataHelper.GetModifiedInt(ammoObject, "ammunitionCount", baseAmmoCount);
            return resolvedCount < 0 ? -1 : resolvedCount;
        }

        private static int ResolveAmmoDamageMin(Dictionary<string, object> source)
        {
            var legacyDamage = Mathf.Max(0, JsonDataHelper.GetInt(source, "damage", 0));
            return Mathf.Max(0, JsonDataHelper.GetInt(source, "damageMin", legacyDamage));
        }

        private static int ResolveAmmoDamageMax(Dictionary<string, object> source)
        {
            var minDamage = ResolveAmmoDamageMin(source);
            var legacyDamage = Mathf.Max(0, JsonDataHelper.GetInt(source, "damage", minDamage));
            return Mathf.Max(minDamage, JsonDataHelper.GetInt(source, "damageMax", legacyDamage));
        }

        private static int ResolveModifiedAmmoDamageMin(Dictionary<string, object> source, int baseDamageMin)
        {
            if (source == null)
            {
                return Mathf.Max(0, baseDamageMin);
            }

            if (source.ContainsKey("damageMin"))
            {
                return Mathf.Max(0, JsonDataHelper.GetModifiedInt(source, "damageMin", baseDamageMin));
            }

            if (source.ContainsKey("damage"))
            {
                return Mathf.Max(0, JsonDataHelper.GetModifiedInt(source, "damage", baseDamageMin));
            }

            return Mathf.Max(0, baseDamageMin);
        }

        private static int ResolveModifiedAmmoDamageMax(Dictionary<string, object> source, int baseDamageMax, int resolvedDamageMin)
        {
            if (source == null)
            {
                return Mathf.Max(resolvedDamageMin, baseDamageMax);
            }

            if (source.ContainsKey("damageMax"))
            {
                return Mathf.Max(resolvedDamageMin, JsonDataHelper.GetModifiedInt(source, "damageMax", baseDamageMax));
            }

            if (source.ContainsKey("damage"))
            {
                var resolvedDamage = Mathf.Max(0, JsonDataHelper.GetModifiedInt(source, "damage", baseDamageMax));
                return Mathf.Max(resolvedDamageMin, resolvedDamage);
            }

            return Mathf.Max(resolvedDamageMin, baseDamageMax);
        }

        private static TerrainSpeedProfile ParseTerrainSpeedProfile(Dictionary<string, object> source)
        {
            return TerrainSpeedProfile.Empty.WithOverrides(JsonDataHelper.AsObject(source.TryGetValue("terrainSpeedModifiers", out var value) ? value : null));
        }

        private static TerrainSpeedProfile ParseTerrainPathCostProfile(Dictionary<string, object> source)
        {
            return TerrainSpeedProfile.Empty.WithOverrides(JsonDataHelper.AsObject(source.TryGetValue("terrainPathCosts", out var value) ? value : null));
        }
    }
}
