using UnityEngine;

namespace AutoBattler
{
    internal static class CombatRoller
    {
        public static float CombineProbability(float first, float second)
        {
            return Mathf.Clamp01(first * second);
        }

        public static bool RollProbability(float chance)
        {
            return Random.value <= Mathf.Clamp01(chance);
        }

        public static Vector3 ResolveImpactPoint(Vector3 targetPosition, float distanceToTarget, float finalAccuracy)
        {
            finalAccuracy = Mathf.Clamp01(finalAccuracy);
            if (finalAccuracy >= 0.999f || RollProbability(finalAccuracy))
            {
                return targetPosition;
            }

            var missSeverity = 1f - finalAccuracy;
            var scatterRadius = Mathf.Max(0.5f, (distanceToTarget * 0.35f * missSeverity) + (2.25f * missSeverity));
            var scatterOffset = Random.insideUnitCircle * scatterRadius;
            return new Vector3(
                targetPosition.x + scatterOffset.x,
                targetPosition.y,
                targetPosition.z + scatterOffset.y);
        }

        public static float GetMovementReliabilityCheckInterval(float moveReliability)
        {
            var unreliability = 1f - Mathf.Clamp01(moveReliability);
            return Mathf.Lerp(2.5f, 0.9f, unreliability);
        }

        public static float GetMovementBreakdownDuration(float moveReliability)
        {
            var unreliability = 1f - Mathf.Clamp01(moveReliability);
            return Mathf.Lerp(0.45f, 1.5f, unreliability) + Random.Range(0f, 0.35f);
        }
    }
}
