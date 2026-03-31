using System;
using UnityEngine;

namespace AutoBattler
{
    [Serializable]
    public sealed class AmmoDefinition
    {
        [SerializeField] private string ammoName;
        [SerializeField] private UnitType requiredUserType;
        [SerializeField] private int damageMin = 1;
        [SerializeField] private int damageMax = 1;
        [SerializeField] private float radius;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float reloadTime = 1f;
        [SerializeField] private float accuracy = 1f;
        [SerializeField] private float damageReliability = 1f;

        public AmmoDefinition(
            string ammoName,
            UnitType requiredUserType,
            int damageMin,
            int damageMax,
            float radius,
            float attackRange,
            float reloadTime,
            float accuracy,
            float damageReliability)
        {
            this.ammoName = ammoName;
            this.requiredUserType = requiredUserType;
            this.damageMin = Mathf.Max(0, damageMin);
            this.damageMax = Mathf.Max(this.damageMin, damageMax);
            this.radius = radius;
            this.attackRange = attackRange;
            this.reloadTime = reloadTime;
            this.accuracy = accuracy;
            this.damageReliability = damageReliability;
        }

        public string AmmoName => ammoName;
        public UnitType RequiredUserType => requiredUserType;
        public int DamageMin => damageMin;
        public int DamageMax => damageMax;
        public int Damage => Mathf.RoundToInt((damageMin + damageMax) * 0.5f);
        public float Radius => radius;
        public float AttackRange => attackRange;
        public float ReloadTime => reloadTime;
        public float Accuracy => accuracy;
        public float DamageReliability => damageReliability;
    }
}
