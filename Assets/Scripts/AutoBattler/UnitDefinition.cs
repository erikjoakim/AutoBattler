using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class UnitDefinition
    {
        [SerializeField] private string unitName;
        [SerializeField] private UnitType unitType;
        [SerializeField] private int maxHealth = 1;
        [SerializeField] private int armor;
        [SerializeField] private float visionRange = 5f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float speed = 3f;
        [SerializeField] private float reloadTime = 1f;
        [SerializeField] private AmmoDefinition[] ammunition;

        public UnitDefinition(
            string unitName,
            UnitType unitType,
            int maxHealth,
            int armor,
            float visionRange,
            float attackRange,
            float speed,
            float reloadTime,
            params AmmoDefinition[] ammunition)
        {
            this.unitName = unitName;
            this.unitType = unitType;
            this.maxHealth = maxHealth;
            this.armor = armor;
            this.visionRange = visionRange;
            this.attackRange = attackRange;
            this.speed = speed;
            this.reloadTime = reloadTime;
            this.ammunition = ammunition;
        }

        public string UnitName => unitName;
        public UnitType UnitType => unitType;
        public int MaxHealth => maxHealth;
        public int Armor => armor;
        public float VisionRange => visionRange;
        public float AttackRange => attackRange;
        public float Speed => speed;
        public float ReloadTime => reloadTime;
        public AmmoDefinition[] Ammunition => ammunition;
    }
}
