using UnityEngine;

namespace AutoBattler
{
    public sealed class StartAreaMarker : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Blue;
        [SerializeField] private int priority;
        [SerializeField] private Vector3 size = new Vector3(12f, 2f, 12f);

        public Team Team => team;
        public int Priority => priority;
        public Vector3 Size => size;
        public Vector3 Center => transform.position;

        public void ConfigureRuntimeMarker(Team runtimeTeam, Vector3 runtimeSize, int runtimePriority = 0)
        {
            team = runtimeTeam;
            size = runtimeSize;
            priority = runtimePriority;
        }

        public Vector3 GetSpawnPosition(int spawnIndex, int totalUnitsForArea, FormationConfig formation)
        {
            var sanitizedSize = GetSanitizedSize();
            var usableWidth = Mathf.Max(1f, sanitizedSize.x - 1f);
            var usableDepth = Mathf.Max(1f, sanitizedSize.z - 1f);
            var desiredColumns = Mathf.Max(1, formation.unitsPerRow);
            var maxColumns = Mathf.Max(1, Mathf.FloorToInt(usableWidth / Mathf.Max(1f, formation.lateralSpacing)) + 1);
            var unitsPerRow = Mathf.Clamp(Mathf.Min(totalUnitsForArea, desiredColumns), 1, maxColumns);
            var totalRows = Mathf.Max(1, Mathf.CeilToInt(totalUnitsForArea / (float)unitsPerRow));
            var row = spawnIndex / unitsPerRow;
            var column = spawnIndex % unitsPerRow;
            var centeredColumn = column - ((unitsPerRow - 1) * 0.5f);
            var lateralSpacing = unitsPerRow > 1
                ? Mathf.Min(formation.lateralSpacing, usableWidth / (unitsPerRow - 1))
                : 0f;
            var localX = centeredColumn * lateralSpacing;
            var frontLimit = (sanitizedSize.z * 0.5f) - 0.5f;
            var backLimit = (-sanitizedSize.z * 0.5f) + 0.5f;
            var frontRowZ = Mathf.Clamp((sanitizedSize.z * 0.5f) - formation.distanceFromStartPoint, backLimit, frontLimit);
            var forwardSpacing = formation.forwardSpacing;
            if (totalRows > 1)
            {
                var maxSpacing = Mathf.Max(0.25f, (frontRowZ - backLimit) / (totalRows - 1));
                forwardSpacing = Mathf.Min(formation.forwardSpacing, maxSpacing);
            }

            var localZ = frontRowZ - (row * forwardSpacing);

            return transform.TransformPoint(new Vector3(localX, 0f, localZ));
        }

        public Bounds GetWorldBounds()
        {
            var sanitizedSize = GetSanitizedSize();
            return new Bounds(transform.position, new Vector3(sanitizedSize.x, sanitizedSize.y, sanitizedSize.z));
        }

        private Vector3 GetSanitizedSize()
        {
            return new Vector3(
                Mathf.Max(2f, size.x),
                Mathf.Max(0.5f, size.y),
                Mathf.Max(2f, size.z));
        }

        private void Reset()
        {
            size = new Vector3(12f, 2f, 12f);
        }

        private void OnDrawGizmos()
        {
            var sanitizedSize = GetSanitizedSize();
            var color = team == Team.Blue
                ? new Color(0.2f, 0.45f, 1f, 0.2f)
                : new Color(1f, 0.25f, 0.25f, 0.2f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = color;
            Gizmos.DrawCube(Vector3.zero, sanitizedSize);

            Gizmos.color = new Color(color.r, color.g, color.b, 0.95f);
            Gizmos.DrawWireCube(Vector3.zero, sanitizedSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
