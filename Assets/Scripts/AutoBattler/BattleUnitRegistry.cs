using System.Collections.Generic;
using UnityEngine;

namespace AutoBattler
{
    public static class BattleUnitRegistry
    {
        private static readonly List<BattleUnit> Units = new List<BattleUnit>();

        public static void Register(BattleUnit unit)
        {
            if (unit == null || Units.Contains(unit))
            {
                return;
            }

            Units.Add(unit);
        }

        public static void Unregister(BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            Units.Remove(unit);
        }

        public static BattleUnit GetClosestEnemy(BattleUnit source, float maxRange)
        {
            BattleUnit closest = null;
            var closestDistanceSqr = maxRange * maxRange;

            for (var i = Units.Count - 1; i >= 0; i--)
            {
                var candidate = Units[i];
                if (candidate == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (candidate == source || !candidate.IsAlive || candidate.Team == source.Team)
                {
                    continue;
                }

                var delta = candidate.transform.position - source.transform.position;
                delta.y = 0f;
                var distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > closestDistanceSqr)
                {
                    continue;
                }

                closestDistanceSqr = distanceSqr;
                closest = candidate;
            }

            return closest;
        }

        public static int CountEnemiesInRadius(Team team, Vector3 center, float radius)
        {
            var count = 0;
            var radiusSqr = radius * radius;

            for (var i = Units.Count - 1; i >= 0; i--)
            {
                var candidate = Units[i];
                if (candidate == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsAlive || candidate.Team == team)
                {
                    continue;
                }

                var delta = candidate.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        public static void ApplySplashDamage(Team attackerTeam, Vector3 center, int damage, float radius, BattleUnit attacker)
        {
            var radiusSqr = radius * radius;

            for (var i = Units.Count - 1; i >= 0; i--)
            {
                var candidate = Units[i];
                if (candidate == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsAlive || candidate.Team == attackerTeam)
                {
                    continue;
                }

                var delta = candidate.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    candidate.ApplyDamage(damage, attacker);
                }
            }
        }

        public static int CountAlive(Team team)
        {
            var count = 0;

            for (var i = Units.Count - 1; i >= 0; i--)
            {
                var candidate = Units[i];
                if (candidate == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (candidate.IsAlive && candidate.Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        public static bool IsTeamOccupyingRadius(Team team, Vector3 center, float radius)
        {
            var radiusSqr = radius * radius;

            for (var i = Units.Count - 1; i >= 0; i--)
            {
                var candidate = Units[i];
                if (candidate == null)
                {
                    Units.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsAlive || candidate.Team != team)
                {
                    continue;
                }

                var delta = candidate.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
