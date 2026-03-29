using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class AmmoDefinition
    {
        [SerializeField] private string ammoName;
        [SerializeField] private UnitType requiredUserType;
        [SerializeField] private int damage = 1;
        [SerializeField] private float radius;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float reloadTime = 1f;
        [SerializeField] private float accuracy = 1f;
        [SerializeField] private float damageReliability = 1f;

        public AmmoDefinition(
            string ammoName,
            UnitType requiredUserType,
            int damage,
            float radius,
            float attackRange,
            float reloadTime,
            float accuracy,
            float damageReliability)
        {
            this.ammoName = ammoName;
            this.requiredUserType = requiredUserType;
            this.damage = damage;
            this.radius = radius;
            this.attackRange = attackRange;
            this.reloadTime = reloadTime;
            this.accuracy = accuracy;
            this.damageReliability = damageReliability;
        }

        public string AmmoName => ammoName;
        public UnitType RequiredUserType => requiredUserType;
        public int Damage => damage;
        public float Radius => radius;
        public float AttackRange => attackRange;
        public float ReloadTime => reloadTime;
        public float Accuracy => accuracy;
        public float DamageReliability => damageReliability;
    }
}
