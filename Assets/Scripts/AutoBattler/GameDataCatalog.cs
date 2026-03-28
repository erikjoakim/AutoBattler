using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public sealed class GameDataCatalog
    {
        private readonly Dictionary<string, GameAmmoTemplate> ammoTemplates;
        private readonly Dictionary<string, GameUnitTemplate> unitTemplates;

        public GameDataCatalog(Dictionary<string, GameAmmoTemplate> ammoTemplates, Dictionary<string, GameUnitTemplate> unitTemplates)
        {
            this.ammoTemplates = ammoTemplates;
            this.unitTemplates = unitTemplates;
        }

        public bool TryGetAmmoTemplate(string ammoType, out GameAmmoTemplate template)
        {
            return ammoTemplates.TryGetValue(ammoType, out template);
        }

        public bool TryGetUnitTemplate(string unitType, out GameUnitTemplate template)
        {
            return unitTemplates.TryGetValue(unitType, out template);
        }

        public static GameDataCatalog CreateDefault()
        {
            var ammoTemplates = new Dictionary<string, GameAmmoTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                { "Tank Cannon", new GameAmmoTemplate("Tank Cannon", "Tank Cannon", UnitType.Tank, 6, 0.6f, -1) },
                { "Tank Shell", new GameAmmoTemplate("Tank Shell", "Tank Shell", UnitType.Tank, 4, 2.5f, -1) },
                { "Rifle Burst", new GameAmmoTemplate("Rifle Burst", "Rifle Burst", UnitType.Infantry, 2, 0.4f, -1) },
                { "Grenade", new GameAmmoTemplate("Grenade", "Grenade", UnitType.Infantry, 3, 1.8f, -1) }
            };

            var unitTemplates = new Dictionary<string, GameUnitTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                { "Guard Tank", new GameUnitTemplate("Guard Tank", "Guard Tank", UnitType.Tank, MissionType.Guard, 20, 2, 14f, 10f, 2.2f, 1.8f, string.Empty, new[] { "Tank Cannon", "Tank Shell" }) },
                { "Assault Tank", new GameUnitTemplate("Assault Tank", "Assault Tank", UnitType.Tank, MissionType.SeekAndDestroy, 20, 2, 14f, 10f, 2.4f, 1.7f, string.Empty, new[] { "Tank Cannon", "Tank Shell" }) },
                { "Guard Infantry", new GameUnitTemplate("Guard Infantry", "Guard Infantry", UnitType.Infantry, MissionType.Guard, 8, 0, 10f, 6f, 3.4f, 1.1f, string.Empty, new[] { "Rifle Burst", "Grenade" }) },
                { "Raider Infantry", new GameUnitTemplate("Raider Infantry", "Raider Infantry", UnitType.Infantry, MissionType.SeekAndDestroy, 8, 0, 10f, 6f, 3.8f, 1.0f, string.Empty, new[] { "Rifle Burst", "Grenade" }) }
            };

            return new GameDataCatalog(ammoTemplates, unitTemplates);
        }
    }

    public sealed class GameAmmoTemplate
    {
        public GameAmmoTemplate(string ammoType, string ammoName, UnitType requiredUserType, int damage, float radius, int ammunitionCount)
        {
            AmmoType = ammoType;
            AmmoName = ammoName;
            RequiredUserType = requiredUserType;
            Damage = damage;
            Radius = radius;
            AmmunitionCount = ammunitionCount;
        }

        public string AmmoType { get; }
        public string AmmoName { get; }
        public UnitType RequiredUserType { get; }
        public int Damage { get; }
        public float Radius { get; }
        public int AmmunitionCount { get; }
    }

    public sealed class GameUnitTemplate
    {
        private readonly string[] ammunitionTypes;

        public GameUnitTemplate(
            string unitTypeKey,
            string unitName,
            UnitType unitType,
            MissionType mission,
            int maxHealth,
            int armor,
            float visionRange,
            float attackRange,
            float speed,
            float reloadTime,
            string navigationAgentType,
            string[] ammunitionTypes)
        {
            UnitTypeKey = unitTypeKey;
            UnitName = unitName;
            UnitType = unitType;
            Mission = mission;
            MaxHealth = maxHealth;
            Armor = armor;
            VisionRange = visionRange;
            AttackRange = attackRange;
            Speed = speed;
            ReloadTime = reloadTime;
            NavigationAgentType = navigationAgentType;
            this.ammunitionTypes = ammunitionTypes;
        }

        public string UnitTypeKey { get; }
        public string UnitName { get; }
        public UnitType UnitType { get; }
        public MissionType Mission { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public float VisionRange { get; }
        public float AttackRange { get; }
        public float Speed { get; }
        public float ReloadTime { get; }
        public string NavigationAgentType { get; }

        public UnitDefinition BuildDefinition(string resolvedUnitName, AmmoDefinition[] ammunition)
        {
            return new UnitDefinition(
                string.IsNullOrWhiteSpace(resolvedUnitName) ? UnitName : resolvedUnitName,
                UnitType,
                MaxHealth,
                Armor,
                VisionRange,
                AttackRange,
                Speed,
                ReloadTime,
                NavigationAgentType,
                ammunition);
        }

        public string[] GetAmmunitionTypes()
        {
            return ammunitionTypes;
        }
    }
}
