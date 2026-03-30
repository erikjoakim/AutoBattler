using UnityEngine;

namespace AutoBattler
{
    public static class DamageResolver
    {
        public static int Resolve(int incomingDamage, int armor)
        {
            return Mathf.Max(0, incomingDamage - armor);
        }
    }
}
