using System;
using UnityEngine;

namespace AutoBattler
{
    public sealed class BattleObjectiveManager : MonoBehaviour
    {
        public static BattleObjectiveManager Instance { get; private set; }

        private VictoryPointMarker[] victoryPoints = Array.Empty<VictoryPointMarker>();
        private ObjectiveOwner[] currentOwners = Array.Empty<ObjectiveOwner>();
        private ObjectiveOwner[] pendingOwners = Array.Empty<ObjectiveOwner>();
        private float[] captureProgressSeconds = Array.Empty<float>();
        private Vector3 blueFallbackObjective;
        private Vector3 redFallbackObjective;
        private bool isInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Initialize(StartAreaMarker[] blueStartAreas, StartAreaMarker[] redStartAreas, VictoryPointMarker[] points)
        {
            blueFallbackObjective = ComputeCentroid(blueStartAreas, new Vector3(-20f, 0f, 0f));
            redFallbackObjective = ComputeCentroid(redStartAreas, new Vector3(20f, 0f, 0f));
            victoryPoints = points ?? Array.Empty<VictoryPointMarker>();
            currentOwners = new ObjectiveOwner[victoryPoints.Length];
            pendingOwners = new ObjectiveOwner[victoryPoints.Length];
            captureProgressSeconds = new float[victoryPoints.Length];

            for (var i = 0; i < victoryPoints.Length; i++)
            {
                var point = victoryPoints[i];
                if (point == null)
                {
                    currentOwners[i] = ObjectiveOwner.Neutral;
                    pendingOwners[i] = ObjectiveOwner.Neutral;
                    captureProgressSeconds[i] = 0f;
                    continue;
                }

                currentOwners[i] = point.InitialOwner;
                pendingOwners[i] = ObjectiveOwner.Neutral;
                captureProgressSeconds[i] = 0f;
                point.ResetRuntimeState();
            }

            isInitialized = true;
        }

        public string GetObjectiveSummary()
        {
            var requiredCount = CountRequiredVictoryPoints();
            if (requiredCount == 0)
            {
                return "Eliminate all enemies";
            }

            return "Capture required victory points (" + CountRequiredOwnedBy(ObjectiveOwner.Blue) + "/" + requiredCount + ")";
        }

        public string GetProgressSummary()
        {
            for (var i = 0; i < victoryPoints.Length; i++)
            {
                var point = victoryPoints[i];
                if (point == null || pendingOwners[i] == ObjectiveOwner.Neutral)
                {
                    continue;
                }

                return point.DisplayName + " " + Mathf.RoundToInt(GetCaptureProgressNormalized(i) * 100f) + "%";
            }

            return string.Empty;
        }

        public Vector3 GetObjectiveDestination(Team team, Vector3 requesterPosition, Vector3 fallbackDestination)
        {
            if (!isInitialized || victoryPoints.Length == 0)
            {
                return team == Team.Blue ? redFallbackObjective : blueFallbackObjective;
            }

            var teamOwner = team == Team.Blue ? ObjectiveOwner.Blue : ObjectiveOwner.Red;
            VictoryPointMarker bestPoint = null;
            var bestDistanceSqr = float.MaxValue;

            for (var i = 0; i < victoryPoints.Length; i++)
            {
                var point = victoryPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (point.RequiredForVictory && currentOwners[i] == teamOwner)
                {
                    continue;
                }

                var distanceSqr = (point.Position - requesterPosition).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestPoint = point;
                }
            }

            if (bestPoint != null)
            {
                return bestPoint.GetApproachPosition(requesterPosition);
            }

            return fallbackDestination;
        }

        public int CountRequiredVictoryPoints()
        {
            var count = 0;
            for (var i = 0; i < victoryPoints.Length; i++)
            {
                if (victoryPoints[i] != null && victoryPoints[i].RequiredForVictory)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountRequiredOwnedBy(ObjectiveOwner owner)
        {
            var count = 0;
            for (var i = 0; i < victoryPoints.Length; i++)
            {
                if (victoryPoints[i] != null && victoryPoints[i].RequiredForVictory && currentOwners[i] == owner)
                {
                    count++;
                }
            }

            return count;
        }

        public bool AreAllRequiredOwnedBy(ObjectiveOwner owner)
        {
            var requiredCount = CountRequiredVictoryPoints();
            return requiredCount > 0 && CountRequiredOwnedBy(owner) == requiredCount;
        }

        public int CountOwned(ObjectiveOwner owner, VictoryPointMarker[] points)
        {
            if (!isInitialized || points == null || points.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < points.Length; i++)
            {
                var index = GetPointIndex(points[i]);
                if (index >= 0 && currentOwners[index] == owner)
                {
                    count++;
                }
            }

            return count;
        }

        public bool AreAllOwned(ObjectiveOwner owner, VictoryPointMarker[] points)
        {
            if (!isInitialized || points == null || points.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < points.Length; i++)
            {
                var index = GetPointIndex(points[i]);
                if (index < 0 || currentOwners[index] != owner)
                {
                    return false;
                }
            }

            return true;
        }

        public ObjectiveOwner GetOwner(VictoryPointMarker point)
        {
            var index = GetPointIndex(point);
            return index >= 0 ? currentOwners[index] : ObjectiveOwner.Neutral;
        }

        private void Update()
        {
            if (!isInitialized || BattleStateManager.Instance == null || BattleStateManager.Instance.IsBattleOver)
            {
                return;
            }

            UpdateVictoryPoints();
        }

        private void UpdateVictoryPoints()
        {
            for (var i = 0; i < victoryPoints.Length; i++)
            {
                var point = victoryPoints[i];
                if (point == null)
                {
                    continue;
                }

                var blueCount = BattleUnitRegistry.CountAliveInRadius(Team.Blue, point.Position, point.CaptureRadius);
                var redCount = BattleUnitRegistry.CountAliveInRadius(Team.Red, point.Position, point.CaptureRadius);
                var capturingOwner = ResolveCapturingOwner(blueCount, redCount);

                if (capturingOwner == ObjectiveOwner.Neutral || capturingOwner == currentOwners[i])
                {
                    pendingOwners[i] = ObjectiveOwner.Neutral;
                    captureProgressSeconds[i] = 0f;
                    point.SetRuntimeState(currentOwners[i], pendingOwners[i], 0f);
                    continue;
                }

                if (pendingOwners[i] != capturingOwner)
                {
                    pendingOwners[i] = capturingOwner;
                    captureProgressSeconds[i] = 0f;
                }

                captureProgressSeconds[i] += Time.deltaTime;
                if (captureProgressSeconds[i] >= point.CaptureTime)
                {
                    currentOwners[i] = capturingOwner;
                    pendingOwners[i] = ObjectiveOwner.Neutral;
                    captureProgressSeconds[i] = 0f;
                    point.SetRuntimeState(currentOwners[i], pendingOwners[i], 0f);
                    continue;
                }

                point.SetRuntimeState(currentOwners[i], pendingOwners[i], GetCaptureProgressNormalized(i));
            }
        }

        private float GetCaptureProgressNormalized(int index)
        {
            var point = victoryPoints[index];
            if (point == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(captureProgressSeconds[index] / point.CaptureTime);
        }

        private int GetPointIndex(VictoryPointMarker point)
        {
            if (point == null)
            {
                return -1;
            }

            for (var i = 0; i < victoryPoints.Length; i++)
            {
                if (victoryPoints[i] == point)
                {
                    return i;
                }
            }

            return -1;
        }

        private static ObjectiveOwner ResolveCapturingOwner(int blueCount, int redCount)
        {
            if (blueCount > 0 && redCount == 0)
            {
                return ObjectiveOwner.Blue;
            }

            if (redCount > 0 && blueCount == 0)
            {
                return ObjectiveOwner.Red;
            }

            return ObjectiveOwner.Neutral;
        }

        private static Vector3 ComputeCentroid(StartAreaMarker[] areas, Vector3 fallback)
        {
            if (areas == null || areas.Length == 0)
            {
                return fallback;
            }

            var total = Vector3.zero;
            var count = 0;
            for (var i = 0; i < areas.Length; i++)
            {
                if (areas[i] == null)
                {
                    continue;
                }

                total += areas[i].Center;
                count++;
            }

            return count == 0 ? fallback : total / count;
        }
    }
}
