using UnityEngine;

namespace AutoBattler
{
    public static class UnitFactory
    {
        public static GameObject CreateUnitObject(UnitDefinition definition, Team team, Transform parent, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>("Units/" + definition.UnitType);
            var unitObject = prefab != null
                ? Object.Instantiate(prefab, parent)
                : CreateFallbackVisual(definition.UnitType, parent);

            unitObject.transform.position = position;
            ApplyTeamColors(unitObject, team, definition.UnitType);
            EnsureCollider(unitObject, definition.UnitType);
            return unitObject;
        }

        private static GameObject CreateFallbackVisual(UnitType unitType, Transform parent)
        {
            var primitiveType = unitType == UnitType.Tank ? PrimitiveType.Cube : PrimitiveType.Capsule;
            var unitObject = GameObject.CreatePrimitive(primitiveType);
            unitObject.transform.SetParent(parent, false);
            unitObject.transform.localScale = unitType == UnitType.Tank
                ? new Vector3(1.8f, 1.5f, 2.4f)
                : new Vector3(1f, 1.8f, 1f);
            return unitObject;
        }

        private static void ApplyTeamColors(GameObject unitObject, Team team, UnitType unitType)
        {
            var color = GetUnitColor(team, unitType);
            var renderers = unitObject.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.material.color = color;
            }
        }

        private static void EnsureCollider(GameObject unitObject, UnitType unitType)
        {
            if (unitObject.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            if (unitType == UnitType.Tank)
            {
                unitObject.AddComponent<BoxCollider>().size = new Vector3(1.8f, 1.5f, 2.4f);
                return;
            }

            var capsule = unitObject.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.45f;
        }

        private static Color GetUnitColor(Team team, UnitType unitType)
        {
            if (team == Team.Blue)
            {
                return unitType == UnitType.Tank
                    ? new Color(0.18f, 0.36f, 0.78f)
                    : new Color(0.36f, 0.65f, 0.95f);
            }

            return unitType == UnitType.Tank
                ? new Color(0.72f, 0.18f, 0.18f)
                : new Color(0.95f, 0.44f, 0.32f);
        }
    }
}
