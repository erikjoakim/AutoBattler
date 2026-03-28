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
        [SerializeField] private int ammunitionCount = -1;

        public AmmoDefinition(string ammoName, UnitType requiredUserType, int damage, float radius, int ammunitionCount)
        {
            this.ammoName = ammoName;
            this.requiredUserType = requiredUserType;
            this.damage = damage;
            this.radius = radius;
            this.ammunitionCount = ammunitionCount;
        }

        public string AmmoName => ammoName;
        public UnitType RequiredUserType => requiredUserType;
        public int Damage => damage;
        public float Radius => radius;
        public int AmmunitionCount => ammunitionCount;
    }
}
