using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public sealed class TerrainMovementMap
    {
        private readonly Terrain terrain;
        private readonly Vector3 terrainOrigin;
        private readonly Vector3 terrainSize;
        private readonly float cellSize;
        private readonly int cellCountX;
        private readonly int cellCountZ;
        private readonly TerrainCell[] cells;
        private readonly List<TerrainAreaBinding> areaBindings;
        private readonly List<TerrainAreaRectangle> modifierRectangles;
        private readonly string defaultTerrainType;

        private TerrainMovementMap(
            Terrain terrain,
            Vector3 terrainOrigin,
            Vector3 terrainSize,
            float cellSize,
            int cellCountX,
            int cellCountZ,
            TerrainCell[] cells,
            List<TerrainAreaBinding> areaBindings,
            List<TerrainAreaRectangle> modifierRectangles,
            string defaultTerrainType)
        {
            this.terrain = terrain;
            this.terrainOrigin = terrainOrigin;
            this.terrainSize = terrainSize;
            this.cellSize = cellSize;
            this.cellCountX = cellCountX;
            this.cellCountZ = cellCountZ;
            this.cells = cells ?? Array.Empty<TerrainCell>();
            this.areaBindings = areaBindings ?? new List<TerrainAreaBinding>();
            this.modifierRectangles = modifierRectangles ?? new List<TerrainAreaRectangle>();
            this.defaultTerrainType = string.IsNullOrWhiteSpace(defaultTerrainType) ? "Grass" : defaultTerrainType;
        }

        public IReadOnlyList<TerrainAreaBinding> AreaBindings => areaBindings;
        public IReadOnlyList<TerrainAreaRectangle> ModifierRectangles => modifierRectangles;

        public string GetTerrainType(Vector3 worldPosition)
        {
            if (terrain == null || cells.Length == 0)
            {
                return defaultTerrainType;
            }

            var localX = worldPosition.x - terrainOrigin.x;
            var localZ = worldPosition.z - terrainOrigin.z;
            if (localX < 0f || localZ < 0f || localX > terrainSize.x || localZ > terrainSize.z)
            {
                return defaultTerrainType;
            }

            var x = Mathf.Clamp(Mathf.FloorToInt(localX / cellSize), 0, cellCountX - 1);
            var z = Mathf.Clamp(Mathf.FloorToInt(localZ / cellSize), 0, cellCountZ - 1);
            return cells[(z * cellCountX) + x].TerrainType;
        }

        public static TerrainMovementMap Build(Terrain terrain, TerrainMovementConfig config)
        {
            config ??= new TerrainMovementConfig();
            config.Sanitize();

            var defaultAreaIndex = ResolveAreaIndex(config.defaultNavArea);
            var areaBindings = new List<TerrainAreaBinding>();
            var areaLookup = new Dictionary<int, string>();
            RegisterAreaBinding(areaBindings, areaLookup, defaultAreaIndex, config.defaultTerrainType);

            if (terrain == null || terrain.terrainData == null)
            {
                return new TerrainMovementMap(
                    terrain,
                    Vector3.zero,
                    Vector3.zero,
                    config.sampleCellSize,
                    0,
                    0,
                    Array.Empty<TerrainCell>(),
                    areaBindings,
                    new List<TerrainAreaRectangle>(),
                    config.defaultTerrainType);
            }

            var terrainData = terrain.terrainData;
            var terrainOrigin = terrain.GetPosition();
            var terrainSize = terrainData.size;
            var cellCountX = Mathf.Max(1, Mathf.CeilToInt(terrainSize.x / config.sampleCellSize));
            var cellCountZ = Mathf.Max(1, Mathf.CeilToInt(terrainSize.z / config.sampleCellSize));
            var cells = new TerrainCell[cellCountX * cellCountZ];

            var mappingLookup = BuildMappingLookup(config);
            var terrainLayers = terrainData.terrainLayers;
            var resolvedLayerMappings = ResolveLayerMappings(terrainLayers, mappingLookup, config, areaBindings, areaLookup);
            var alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);

            for (var z = 0; z < cellCountZ; z++)
            {
                for (var x = 0; x < cellCountX; x++)
                {
                    var localX = Mathf.Min(((x + 0.5f) * config.sampleCellSize), Mathf.Max(0f, terrainSize.x - 0.001f));
                    var localZ = Mathf.Min(((z + 0.5f) * config.sampleCellSize), Mathf.Max(0f, terrainSize.z - 0.001f));
                    var dominantLayerIndex = FindDominantLayerIndex(alphamaps, terrainData, localX, localZ, terrainSize);
                    var mapping = dominantLayerIndex >= 0 && dominantLayerIndex < resolvedLayerMappings.Length
                        ? resolvedLayerMappings[dominantLayerIndex]
                        : new ResolvedLayerMapping(config.defaultTerrainType, defaultAreaIndex);

                    cells[(z * cellCountX) + x] = new TerrainCell(mapping.TerrainType, mapping.AreaIndex);
                }
            }

            var rectangles = BuildModifierRectangles(
                cells,
                cellCountX,
                cellCountZ,
                config.sampleCellSize,
                terrainOrigin,
                terrainSize,
                defaultAreaIndex);

            return new TerrainMovementMap(
                terrain,
                terrainOrigin,
                terrainSize,
                config.sampleCellSize,
                cellCountX,
                cellCountZ,
                cells,
                areaBindings,
                rectangles,
                config.defaultTerrainType);
        }

        private static Dictionary<string, TerrainLayerMappingConfig> BuildMappingLookup(TerrainMovementConfig config)
        {
            var lookup = new Dictionary<string, TerrainLayerMappingConfig>(StringComparer.OrdinalIgnoreCase);
            var mappings = config.mappings ?? Array.Empty<TerrainLayerMappingConfig>();

            for (var i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.terrainLayer))
                {
                    continue;
                }

                lookup[mapping.terrainLayer] = mapping;
            }

            return lookup;
        }

        private static ResolvedLayerMapping[] ResolveLayerMappings(
            TerrainLayer[] terrainLayers,
            Dictionary<string, TerrainLayerMappingConfig> mappingLookup,
            TerrainMovementConfig config,
            List<TerrainAreaBinding> areaBindings,
            Dictionary<int, string> areaLookup)
        {
            var resolved = new ResolvedLayerMapping[terrainLayers != null ? terrainLayers.Length : 0];
            for (var i = 0; i < resolved.Length; i++)
            {
                var terrainLayer = terrainLayers[i];
                var mapping = ResolveMappingForLayer(terrainLayer, mappingLookup, config);
                var areaIndex = ResolveAreaIndex(mapping.navArea);
                RegisterAreaBinding(areaBindings, areaLookup, areaIndex, mapping.terrainType);
                resolved[i] = new ResolvedLayerMapping(mapping.terrainType, areaIndex);
            }

            return resolved;
        }

        private static TerrainLayerMappingConfig ResolveMappingForLayer(
            TerrainLayer terrainLayer,
            Dictionary<string, TerrainLayerMappingConfig> mappingLookup,
            TerrainMovementConfig config)
        {
            if (terrainLayer != null)
            {
                if (!string.IsNullOrWhiteSpace(terrainLayer.name) && mappingLookup.TryGetValue(terrainLayer.name, out var directMatch))
                {
                    return directMatch;
                }

                if (terrainLayer.diffuseTexture != null
                    && !string.IsNullOrWhiteSpace(terrainLayer.diffuseTexture.name)
                    && mappingLookup.TryGetValue(terrainLayer.diffuseTexture.name, out var textureMatch))
                {
                    return textureMatch;
                }
            }

            return new TerrainLayerMappingConfig
            {
                terrainLayer = terrainLayer != null ? terrainLayer.name : string.Empty,
                terrainType = config.defaultTerrainType,
                navArea = config.defaultNavArea
            };
        }

        private static int FindDominantLayerIndex(
            float[,,] alphamaps,
            TerrainData terrainData,
            float localX,
            float localZ,
            Vector3 terrainSize)
        {
            if (alphamaps == null || terrainData.alphamapLayers == 0)
            {
                return -1;
            }

            var normalizedX = terrainSize.x > 0.001f ? Mathf.Clamp01(localX / terrainSize.x) : 0f;
            var normalizedZ = terrainSize.z > 0.001f ? Mathf.Clamp01(localZ / terrainSize.z) : 0f;
            var alphaX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (terrainData.alphamapWidth - 1)), 0, terrainData.alphamapWidth - 1);
            var alphaZ = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (terrainData.alphamapHeight - 1)), 0, terrainData.alphamapHeight - 1);

            var bestWeight = float.MinValue;
            var bestIndex = -1;
            for (var layerIndex = 0; layerIndex < terrainData.alphamapLayers; layerIndex++)
            {
                var weight = alphamaps[alphaZ, alphaX, layerIndex];
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestIndex = layerIndex;
                }
            }

            return bestIndex;
        }

        private static List<TerrainAreaRectangle> BuildModifierRectangles(
            TerrainCell[] cells,
            int cellCountX,
            int cellCountZ,
            float cellSize,
            Vector3 terrainOrigin,
            Vector3 terrainSize,
            int defaultAreaIndex)
        {
            var rectangles = new List<TerrainAreaRectangle>();
            var activeRuns = new Dictionary<RunKey, RectangleAccumulator>();

            for (var z = 0; z < cellCountZ; z++)
            {
                var rowRuns = BuildRowRuns(cells, cellCountX, z, defaultAreaIndex);
                var nextActiveRuns = new Dictionary<RunKey, RectangleAccumulator>();

                for (var i = 0; i < rowRuns.Count; i++)
                {
                    var run = rowRuns[i];
                    if (activeRuns.TryGetValue(run.Key, out var accumulator))
                    {
                        accumulator.RowCount++;
                        nextActiveRuns[run.Key] = accumulator;
                        activeRuns.Remove(run.Key);
                    }
                    else
                    {
                        nextActiveRuns[run.Key] = new RectangleAccumulator(run.Key, z);
                    }
                }

                FlushAccumulators(activeRuns, rectangles, cellSize, terrainOrigin, terrainSize);
                activeRuns = nextActiveRuns;
            }

            FlushAccumulators(activeRuns, rectangles, cellSize, terrainOrigin, terrainSize);
            return rectangles;
        }

        private static List<RowRun> BuildRowRuns(TerrainCell[] cells, int cellCountX, int z, int defaultAreaIndex)
        {
            var runs = new List<RowRun>();
            RowRun? currentRun = null;

            for (var x = 0; x < cellCountX; x++)
            {
                var cell = cells[(z * cellCountX) + x];
                if (cell.AreaIndex == defaultAreaIndex)
                {
                    if (currentRun.HasValue)
                    {
                        runs.Add(currentRun.Value);
                        currentRun = null;
                    }

                    continue;
                }

                if (currentRun.HasValue && currentRun.Value.CanExtend(cell, x))
                {
                    var updatedRun = currentRun.Value;
                    updatedRun.Extend();
                    currentRun = updatedRun;
                    continue;
                }

                if (currentRun.HasValue)
                {
                    runs.Add(currentRun.Value);
                }

                currentRun = new RowRun(new RunKey(x, 1, cell.AreaIndex, cell.TerrainType));
            }

            if (currentRun.HasValue)
            {
                runs.Add(currentRun.Value);
            }

            return runs;
        }

        private static void FlushAccumulators(
            Dictionary<RunKey, RectangleAccumulator> accumulators,
            List<TerrainAreaRectangle> rectangles,
            float cellSize,
            Vector3 terrainOrigin,
            Vector3 terrainSize)
        {
            foreach (var pair in accumulators)
            {
                var accumulator = pair.Value;
                var xMin = accumulator.Key.StartX * cellSize;
                var zMin = accumulator.StartRow * cellSize;
                var width = Mathf.Min(accumulator.Key.Length * cellSize, terrainSize.x - xMin);
                var depth = Mathf.Min(accumulator.RowCount * cellSize, terrainSize.z - zMin);

                if (width <= 0f || depth <= 0f)
                {
                    continue;
                }

                var center = new Vector3(
                    terrainOrigin.x + xMin + (width * 0.5f),
                    terrainOrigin.y + (terrainSize.y * 0.5f),
                    terrainOrigin.z + zMin + (depth * 0.5f));

                var height = Mathf.Max(terrainSize.y + 10f, 10f);
                rectangles.Add(new TerrainAreaRectangle(
                    accumulator.Key.TerrainType,
                    accumulator.Key.AreaIndex,
                    center,
                    new Vector3(width, height, depth)));
            }
        }

        private static void RegisterAreaBinding(
            List<TerrainAreaBinding> bindings,
            Dictionary<int, string> areaLookup,
            int areaIndex,
            string terrainType)
        {
            if (areaLookup.ContainsKey(areaIndex))
            {
                return;
            }

            areaLookup[areaIndex] = terrainType;
            bindings.Add(new TerrainAreaBinding(terrainType, areaIndex));
        }

        private static int ResolveAreaIndex(string areaName)
        {
            var resolved = !string.IsNullOrWhiteSpace(areaName) ? NavMesh.GetAreaFromName(areaName) : -1;
            return resolved >= 0 ? resolved : 0;
        }

        private readonly struct TerrainCell
        {
            public TerrainCell(string terrainType, int areaIndex)
            {
                TerrainType = string.IsNullOrWhiteSpace(terrainType) ? "Grass" : terrainType;
                AreaIndex = areaIndex;
            }

            public string TerrainType { get; }
            public int AreaIndex { get; }
        }

        public readonly struct TerrainAreaBinding
        {
            public TerrainAreaBinding(string terrainType, int areaIndex)
            {
                TerrainType = terrainType;
                AreaIndex = areaIndex;
            }

            public string TerrainType { get; }
            public int AreaIndex { get; }
        }

        public readonly struct TerrainAreaRectangle
        {
            public TerrainAreaRectangle(string terrainType, int areaIndex, Vector3 center, Vector3 size)
            {
                TerrainType = terrainType;
                AreaIndex = areaIndex;
                Center = center;
                Size = size;
            }

            public string TerrainType { get; }
            public int AreaIndex { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
        }

        private readonly struct ResolvedLayerMapping
        {
            public ResolvedLayerMapping(string terrainType, int areaIndex)
            {
                TerrainType = terrainType;
                AreaIndex = areaIndex;
            }

            public string TerrainType { get; }
            public int AreaIndex { get; }
        }

        private struct RowRun
        {
            public RowRun(RunKey key)
            {
                Key = key;
            }

            public RunKey Key { get; private set; }

            public bool CanExtend(TerrainCell cell, int x)
            {
                return Key.AreaIndex == cell.AreaIndex
                    && string.Equals(Key.TerrainType, cell.TerrainType, StringComparison.OrdinalIgnoreCase)
                    && x == Key.StartX + Key.Length;
            }

            public void Extend()
            {
                Key = new RunKey(Key.StartX, Key.Length + 1, Key.AreaIndex, Key.TerrainType);
            }
        }

        private readonly struct RunKey : IEquatable<RunKey>
        {
            public RunKey(int startX, int length, int areaIndex, string terrainType)
            {
                StartX = startX;
                Length = length;
                AreaIndex = areaIndex;
                TerrainType = terrainType ?? string.Empty;
            }

            public int StartX { get; }
            public int Length { get; }
            public int AreaIndex { get; }
            public string TerrainType { get; }

            public bool Equals(RunKey other)
            {
                return StartX == other.StartX
                    && Length == other.Length
                    && AreaIndex == other.AreaIndex
                    && string.Equals(TerrainType, other.TerrainType, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is RunKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StartX, Length, AreaIndex, StringComparer.OrdinalIgnoreCase.GetHashCode(TerrainType));
            }
        }

        private sealed class RectangleAccumulator
        {
            public RectangleAccumulator(RunKey key, int startRow)
            {
                Key = key;
                StartRow = startRow;
                RowCount = 1;
            }

            public RunKey Key { get; }
            public int StartRow { get; }
            public int RowCount { get; set; }
        }
    }
}
