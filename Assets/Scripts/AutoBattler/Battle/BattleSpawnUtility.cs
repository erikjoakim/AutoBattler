using UnityEngine;

namespace AutoBattler
{
    public static class BattleSpawnUtility
    {
        public static BattleUnit SpawnUnit(
            Transform parent,
            UnitDefinition definition,
            Team team,
            MissionType mission,
            Vector3 position,
            Vector3 targetPoint,
            string ownedUnitCardId = null,
            string lootTableId = null,
            bool returnToHeadquartersIfSurvives = false,
            bool captureAsUnitCardOnDeath = false,
            string persistentOverrideJson = null)
        {
            if (definition == null)
            {
                return null;
            }

            var unitObject = UnitFactory.CreateUnitObject(definition, team, parent, position);
            unitObject.name = team + " " + definition.UnitName + " " + mission;

            var unit = unitObject.AddComponent<BattleUnit>();
            unit.Initialize(definition, team, mission, position, targetPoint, lootTableId);
            if (!string.IsNullOrWhiteSpace(ownedUnitCardId))
            {
                unit.LinkOwnedUnitCard(ownedUnitCardId);
            }

            unit.ConfigureCampaignTransfer(returnToHeadquartersIfSurvives, captureAsUnitCardOnDeath, persistentOverrideJson);
            return unit;
        }
    }
}
